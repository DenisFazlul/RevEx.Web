using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.DesktopReleases;

namespace RevEx.Api.Controllers;

internal sealed class TrackedDesktopFileResult(IActionResult file, DesktopUpdateOffer? offer, string version, string fileName,
    string? userId = null, string? userName = null) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var services = context.HttpContext.RequestServices;
        var tracking = services.GetRequiredService<IDesktopUpdateTrackingStore>();
        Guid id;
        try { id = await tracking.StartDownloadAsync(offer, version, fileName, userId, userName, context.HttpContext.RequestAborted); }
        catch
        {
            if (file is FileStreamResult streamResult) await streamResult.FileStream.DisposeAsync();
            throw;
        }
        var status = "failed";
        try
        {
            await file.ExecuteResultAsync(context);
            status = context.HttpContext.RequestAborted.IsCancellationRequested ? "cancelled" :
                context.HttpContext.Response.StatusCode == 200 ? "transferred" :
                context.HttpContext.Response.StatusCode == 206 ? "partial" : "rejected";
        }
        catch (OperationCanceledException) { status = "cancelled"; throw; }
        finally
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await tracking.FinishDownloadAsync(id, status, timeout.Token);
            }
            catch (Exception exception)
            {
                services.GetRequiredService<ILogger<TrackedDesktopFileResult>>().LogError(exception, "Could not finish desktop download event {Id}", id);
            }
        }
    }
}
