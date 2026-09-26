using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly SeedService _seed;

    public AdminController(SeedService seed) => _seed = seed;

    [HttpPost("seed")]
    public async Task<ActionResult<SeedResult>> Seed(CancellationToken ct)
    {
        return Ok(await _seed.GenerateAsync(ct));
    }
}
