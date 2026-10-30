using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Storage;
using RevEx.Domain.Contracts.DesktopReleases;

namespace RevEx.Api.Controllers;

public sealed class DesktopReleaseUpload
{
    public IFormFile File { get; set; } = null!;
}
