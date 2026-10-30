using System.Security.Claims;
using RevEx.Application.Abstractions.Identity;

namespace RevEx.Api.Authorization;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string Author =>
        accessor.HttpContext?.User.FindFirstValue("preferred_username")
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? accessor.HttpContext?.User.FindFirstValue("email")
        ?? accessor.HttpContext?.User.FindFirstValue("sub")
        ?? "unknown";
}
