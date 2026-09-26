using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic")]
public class AcademicController : ControllerBase
{
    private readonly AcademicService _academic;
    private readonly RegisterService _register;

    public AcademicController(AcademicService academic, RegisterService register)
    {
        _academic = academic;
        _register = register;
    }

    [HttpGet("card/me")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<StudentCardResponse>> MyCard(CancellationToken ct)
    {
        return Ok(await _academic.CardForUserAsync(HttpActor.Current(User).UserId, ct));
    }

    [HttpGet("card/{studentId}")]
    public async Task<ActionResult<StudentCardResponse>> Card(string studentId, CancellationToken ct)
    {
        return Ok(await _academic.CardAsync(HttpActor.Current(User), studentId, ct));
    }

    [HttpGet("journal")]
    public async Task<ActionResult<JournalResponse>> Journal(
        [FromQuery] string studentId,
        [FromQuery] string subjectId,
        CancellationToken ct)
    {
        return Ok(await _academic.JournalAsync(HttpActor.Current(User), studentId, subjectId, ct));
    }

    [HttpGet("statement")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<StatementResponse>> Statement(
        [FromQuery] string subjectId,
        [FromQuery] string group,
        CancellationToken ct)
    {
        return Ok(await _academic.StatementAsync(HttpActor.Current(User), subjectId, group, ct));
    }

    [HttpGet("register")]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromQuery] string subjectId,
        [FromQuery] string? group,
        [FromQuery] string? studentId,
        CancellationToken ct)
    {
        return Ok(await _register.GetAsync(HttpActor.Current(User), subjectId, group, studentId, ct));
    }

    [HttpPost("register/columns")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<RegisterResponse>> AddColumn([FromBody] AddColumnRequest request, CancellationToken ct)
    {
        return Ok(await _register.AddColumnAsync(HttpActor.Current(User), request, ct));
    }

    [HttpPut("register/cells")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<RegisterResponse>> SetCell([FromBody] SetCellRequest request, CancellationToken ct)
    {
        return Ok(await _register.SetCellAsync(HttpActor.Current(User), request, ct));
    }

    [HttpGet("at-risk")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<AtRiskResponse>> AtRisk(CancellationToken ct)
    {
        return Ok(await _academic.AtRiskAsync(HttpActor.Current(User), ct));
    }
}
