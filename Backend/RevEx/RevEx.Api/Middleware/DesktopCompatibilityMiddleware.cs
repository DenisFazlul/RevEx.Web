using RevEx.Application.Abstractions.Storage;
using RevEx.Application.DTOs.DesktopReleases;

namespace RevEx.Api.Middleware;

public sealed class DesktopCompatibilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDesktopReleaseStore releases)
    {
        var client = context.Request.Headers["X-RevEx-Desktop-Version"].ToString();
        if (client.Length > 0 && context.Request.Path.StartsWithSegments("/api")
            && !context.Request.Path.StartsWithSegments("/api/desktop-releases")
            && !context.Request.Path.StartsWithSegments("/api/desktop-release"))
        {
            var active = await releases.GetActiveAsync(context.RequestAborted);
            var supported = false;
            try
            {
                var version = DesktopVersions.Parse(client);
                supported = active != null && version >= DesktopVersions.Parse(active.MinimumDesktopVersion)
                    && version <= DesktopVersions.Parse(active.MaximumDesktopVersion);
            }
            catch (ArgumentException) { }
            if (!supported)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { code = "desktop_version_unsupported", message = "Версия десктопа не поддерживается. Перезапустите приложение для обновления." });
                return;
            }
        }
        await next(context);
    }
}
