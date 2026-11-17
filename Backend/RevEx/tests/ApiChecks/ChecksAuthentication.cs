using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RevEx.Api.Controllers;
using RevEx.Api.Extensions;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Contracts.ContentHistory;
using RevEx.Domain.Entities;
using RevEx.Infrastructure.Persistence;
using RevEx.Infrastructure.Repositories;
using RevEx.Infrastructure.Storage;

sealed class ChecksAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers.ContainsKey("X-Check-Moderator")
            ? RevEx.Domain.Security.RevExRoles.Moderator
            : RevEx.Domain.Security.RevExRoles.Administrator;
        var identity = new ClaimsIdentity(
            [new Claim("preferred_username", "checks"), new Claim(ClaimTypes.Role, role)],
            "Checks");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Checks");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
