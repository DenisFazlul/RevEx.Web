using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Api.Authorization;
using RevEx.Application.Abstractions.Storage;

namespace RevEx.Api.Controllers;

public sealed record RevitPublication(bool IsPublished);
