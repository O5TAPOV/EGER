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

    public AcademicController(AcademicService academic) => _academic = academic;

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

    [HttpGet("at-risk")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<AtRiskResponse>> AtRisk(CancellationToken ct)
    {
        return Ok(await _academic.AtRiskAsync(HttpActor.Current(User), ct));
    }
}
