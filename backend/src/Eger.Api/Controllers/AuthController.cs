using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        return Ok(await _auth.LoginAsync(request, ct));
    }

    [HttpPost("verify-2fa")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Verify([FromBody] Verify2FaRequest request, CancellationToken ct)
    {
        return Ok(await _auth.VerifyAsync(request, ct));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var actor = HttpActor.Current(User);
        await _auth.LogoutAsync(actor.UserId, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var actor = HttpActor.Current(User);
        return Ok(await _auth.MeAsync(actor.UserId, ct));
    }

    [Authorize]
    [HttpPut("2fa")]
    public async Task<ActionResult<MeResponse>> UpdateTwoFactor([FromBody] Update2FaRequest request, CancellationToken ct)
    {
        var actor = HttpActor.Current(User);
        return Ok(await _auth.UpdateTwoFactorAsync(actor.UserId, request, ct));
    }
}
