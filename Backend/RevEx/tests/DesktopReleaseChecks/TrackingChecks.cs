using Microsoft.Extensions.Options;
using RevEx.Domain.Contracts.DesktopReleases;
using RevEx.Infrastructure.Persistence;

internal static class TrackingChecks
{
    public static async Task RunAsync(string connectionString)
    {
        var options = Options.Create(new ContentHistoryOptions { ConnectionString = connectionString });
        await using var store = new PostgresDesktopUpdateTrackingStore(options);
        var ct = CancellationToken.None;
        await store.InitializeAsync(ct);
        await store.InitializeAsync(ct);
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS: " + name);
        }
        async Task Reject(Func<Task> action, string name)
        {
            try { await action(); }
            catch (Exception exception) when (exception is ArgumentException or UnauthorizedAccessException)
            { Check(true, name); return; }
            throw new Exception("Expected rejection: " + name);
        }
        var request = new DesktopInstallationRequest(Guid.NewGuid(), new string('A', 64), "3.0.0", "win-x64");
        await store.ObserveAsync(request, null, null, ct);
        var policy = new DesktopPolicy("required", "2.0.0", "Rollback", new("2.0.0", "win-x64", "RevEx-full.nupkg", 123, new string('B', 64), new string('C', 40)), true);
        var offer = await store.CreateOfferAsync(request, policy, ct);
        Check(await store.GetOfferAsync(offer.Id, new string('D', 64), ct) == null, "Offer requires the installation secret");
        await Reject(() => store.ObserveAsync(request with { InstallationKey = new string('D', 64) }, "intruder", "Intruder", ct), "Installation cannot be claimed using only its ID");
        var download = await store.StartDownloadAsync(offer, "2.0.0", "RevEx-full.nupkg", null, null, ct);
        await store.FinishDownloadAsync(download, "transferred", ct);
        var before = await store.GetHistoryAsync(500, ct);
        Check(before.Updates.Single(x => x.Id == offer.Id).ConfirmedAt == null, "Downloaded package is not treated as installed");
        await Reject(() => store.ObserveAsync(request with { CompletedOfferId = offer.Id }, null, null, ct), "Confirmation must report the actual target version");
        await Reject(() => store.ObserveAsync(request with { Version = "2.0.0", Platform = "osx-arm64", CompletedOfferId = offer.Id }, null, null, ct), "Confirmation must match the offered platform");
        await store.FailOfferAsync(offer.Id, request.InstallationKey, "Retry required", ct);
        Check((await store.GetOfferAsync(offer.Id, request.InstallationKey, ct))?.Failure == "Retry required", "Update failure persisted");
        var installed = request with { Version = "2.0.0", CompletedOfferId = offer.Id };
        await store.ObserveAsync(installed, null, null, ct);
        var first = (await store.GetOfferAsync(offer.Id, request.InstallationKey, ct))!.ConfirmedAt;
        await store.ObserveAsync(installed, "alice-id", "Alice", ct);
        await store.ObserveAsync(installed, "alice-id", "Alice", ct);
        var confirmed = await store.GetOfferAsync(offer.Id, request.InstallationKey, ct);
        Check(confirmed!.ConfirmedAt == first && confirmed.Failure == null, "Restart confirmation is idempotent and clears prior failure");
        var secondMachine = request with { InstallationId = Guid.NewGuid(), Version = "2.0.0" };
        await store.ObserveAsync(secondMachine, "alice-id", "Alice", ct);
        await using var restarted = new PostgresDesktopUpdateTrackingStore(options);
        await restarted.InitializeAsync(ct);
        var history = await restarted.GetHistoryAsync(500, ct);
        Check(history.Installations.Count(x => x.UserId == "alice-id" && (x.InstallationId == request.InstallationId || x.InstallationId == secondMachine.InstallationId)) == 2,
            "User versions are tracked separately for multiple machines");
        Check(history.Downloads.Single(x => x.Id == download) is { Status: "transferred", UserId: "alice-id", FinishedAt: not null },
            "Anonymous download is attributed after authenticated login and survives store restart");
        Check(history.Updates.Single(x => x.Id == offer.Id) is { ConfirmedAt: not null, Policy.IsDowngrade: true, UserId: "alice-id" },
            "Confirmed rollback history survives PostgreSQL store restart");
        await store.ObserveAsync(installed, "bob-id", "Bob", ct);
        Check((await store.GetOfferAsync(offer.Id, request.InstallationKey, ct))!.UserId == "alice-id", "Later login cannot rewrite historical download author");
    }
}
