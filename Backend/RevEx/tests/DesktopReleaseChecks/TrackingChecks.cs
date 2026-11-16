using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RevEx.Domain.Contracts.DesktopReleases;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;

internal static class TrackingChecks
{
    public static async Task RunAsync(string connectionString)
    {
        await using var admin = Npgsql.NpgsqlDataSource.Create(connectionString);
        var schema = "desktop_tracking_checks_" + Guid.NewGuid().ToString("N");

        await using (var command = admin.CreateCommand($"CREATE SCHEMA {schema}"))
        {
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var isolatedConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = $"{schema},public"
            }.ConnectionString;
            var options = Options.Create(new DatabaseOptions
            {
                ConnectionString = isolatedConnectionString
            });

            await VerifyMigrationIdempotencyAsync(options);
            await VerifyTrackingAsync(options);
        }
        finally
        {
            await using var cleanup = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyMigrationIdempotencyAsync(IOptions<DatabaseOptions> options)
    {
        var factory = new PostgresContextFactory(options);

        await using (var database = factory.Create())
        {
            await database.Database.MigrateAsync();
            database.History.Add(new ContentHistoryEvent
            {
                ContentId = 123,
                Description = "Preserve data",
                Author = "checks"
            });
            await database.SaveChangesAsync();
        }

        await using (var database = factory.Create())
        {
            await database.Database.MigrateAsync();
            var historyCount = await database.History.CountAsync();

            Check(historyCount == 1, "Initial migration is idempotent and preserves data");
        }
    }

    private static async Task VerifyTrackingAsync(IOptions<DatabaseOptions> options)
    {
        await using var store = new PostgresDesktopUpdateTrackingStore(options);
        var cancellationToken = CancellationToken.None;
        var request = new DesktopInstallationRequest(
            Guid.NewGuid(),
            new string('A', 64),
            "3.0.0",
            "win-x64");

        await store.ObserveAsync(request, null, null, cancellationToken);

        var policy = new DesktopPolicy(
            "required",
            "2.0.0",
            "Rollback",
            new DesktopPackage(
                "2.0.0",
                "win-x64",
                "RevEx-full.nupkg",
                123,
                new string('B', 64),
                new string('C', 40)),
            true);
        var offer = await store.CreateOfferAsync(request, policy, cancellationToken);
        var unauthorizedOffer = await store.GetOfferAsync(offer.Id, new string('D', 64), cancellationToken);

        Check(unauthorizedOffer == null, "Offer requires the installation secret");

        await RejectAsync(
            () => store.ObserveAsync(
                request with { InstallationKey = new string('D', 64) },
                "intruder",
                "Intruder",
                cancellationToken),
            "Installation cannot be claimed using only its ID");

        var download = await store.StartDownloadAsync(
            offer,
            "2.0.0",
            "RevEx-full.nupkg",
            null,
            null,
            cancellationToken);
        await store.FinishDownloadAsync(download, "transferred", cancellationToken);

        var before = await store.GetHistoryAsync(500, cancellationToken);
        var downloadedOffer = before.Updates.Single(update => update.Id == offer.Id);

        Check(downloadedOffer.ConfirmedAt == null, "Downloaded package is not treated as installed");

        await RejectAsync(
            () => store.ObserveAsync(
                request with { CompletedOfferId = offer.Id },
                null,
                null,
                cancellationToken),
            "Confirmation must report the actual target version");
        await RejectAsync(
            () => store.ObserveAsync(
                request with
                {
                    Version = "2.0.0",
                    Platform = "osx-arm64",
                    CompletedOfferId = offer.Id
                },
                null,
                null,
                cancellationToken),
            "Confirmation must match the offered platform");

        await store.FailOfferAsync(offer.Id, request.InstallationKey, "Retry required", cancellationToken);

        var failedOffer = await store.GetOfferAsync(offer.Id, request.InstallationKey, cancellationToken);

        Check(failedOffer?.Failure == "Retry required", "Update failure persisted");

        var installed = request with
        {
            Version = "2.0.0",
            CompletedOfferId = offer.Id
        };
        await store.ObserveAsync(installed, null, null, cancellationToken);

        var firstConfirmation = await store.GetOfferAsync(offer.Id, request.InstallationKey, cancellationToken);
        var firstConfirmedAt = firstConfirmation!.ConfirmedAt;

        await store.ObserveAsync(installed, "alice-id", "Alice", cancellationToken);
        await store.ObserveAsync(installed, "alice-id", "Alice", cancellationToken);

        var confirmed = await store.GetOfferAsync(offer.Id, request.InstallationKey, cancellationToken);

        Check(
            confirmed!.ConfirmedAt == firstConfirmedAt && confirmed.Failure == null,
            "Restart confirmation is idempotent and clears prior failure");

        var secondMachine = request with
        {
            InstallationId = Guid.NewGuid(),
            Version = "2.0.0"
        };
        await store.ObserveAsync(secondMachine, "alice-id", "Alice", cancellationToken);

        await using var restarted = new PostgresDesktopUpdateTrackingStore(options);
        var history = await restarted.GetHistoryAsync(500, cancellationToken);
        var userInstallationCount = history.Installations.Count(installation =>
            installation.UserId == "alice-id" &&
            (installation.InstallationId == request.InstallationId ||
             installation.InstallationId == secondMachine.InstallationId));
        var persistedDownload = history.Downloads.Single(item => item.Id == download);
        var persistedOffer = history.Updates.Single(item => item.Id == offer.Id);

        Check(userInstallationCount == 2, "User versions are tracked separately for multiple machines");
        Check(
            persistedDownload is
            {
                Status: "transferred",
                UserId: "alice-id",
                FinishedAt: not null
            },
            "Anonymous download is attributed after authenticated login and survives store restart");
        Check(
            persistedOffer is
            {
                ConfirmedAt: not null,
                Policy.IsDowngrade: true,
                UserId: "alice-id"
            },
            "Confirmed rollback history survives PostgreSQL store restart");

        await store.ObserveAsync(installed, "bob-id", "Bob", cancellationToken);

        var claimedOffer = await store.GetOfferAsync(offer.Id, request.InstallationKey, cancellationToken);

        Check(claimedOffer!.UserId == "alice-id", "Later login cannot rewrite historical download author");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(name);
        }

        Console.WriteLine("PASS: " + name);
    }

    private static async Task RejectAsync(Func<Task> action, string name)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is ArgumentException or UnauthorizedAccessException)
        {
            Check(true, name);
            return;
        }

        throw new InvalidOperationException("Expected rejection: " + name);
    }
}
