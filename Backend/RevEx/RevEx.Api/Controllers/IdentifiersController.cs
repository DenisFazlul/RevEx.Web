using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RevEx.Domain.Identifiers;

namespace RevEx.Api.Controllers;

[ApiController]
[Route("api/identifiers")]
[Authorize(Policy = "EditContent")]
public sealed class IdentifiersController : ControllerBase
{
    [HttpPost("uuid-v7")]
    public Guid GenerateUuid7()
    {
        return Uuid7.Create();
    }
}
