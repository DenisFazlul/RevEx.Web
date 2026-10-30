using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Storage;

namespace RevEx.Api.Controllers;

public sealed class RevitUpload
{
    public string Version { get; set; } = "";
    public string RevitVersion { get; set; } = "";
    public string Notes { get; set; } = "";
    public IFormFile? File { get; set; }
}
