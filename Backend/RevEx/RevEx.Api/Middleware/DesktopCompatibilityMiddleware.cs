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
            && !context.Request.Path.StartsWithSegments("/api/desktop-release")
            && !context.Request.Path.StartsWithSegments("/api/desktop-updates")
            && !context.Request.Path.StartsWithSegments("/api/desktop-installers"))
        {
            var supported = false;
            try
            {
                var platform = context.Request.Headers["X-RevEx-Desktop-Platform"].ToString();
                supported = await releases.IsSupportedAsync(client, context.RequestAborted,
                    string.IsNullOrEmpty(platform) ? null : platform);
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
