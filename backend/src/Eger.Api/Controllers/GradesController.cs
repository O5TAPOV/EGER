using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/grades")]
public class GradesController : ControllerBase
{
    private readonly GradeService _grades;

    public GradesController(GradeService grades) => _grades = grades;

    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<IReadOnlyList<GradeResponse>>> List(
        [FromQuery] string? studentId,
        [FromQuery] string? subjectId,
        CancellationToken ct)
    {
        return Ok(await _grades.ListAsync(HttpActor.Current(User), studentId, subjectId, ct));
    }

    [HttpGet("my")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<IReadOnlyList<GradeResponse>>> Mine(CancellationToken ct)
    {
        return Ok(await _grades.MineAsync(HttpActor.Current(User).UserId, ct));
    }

    [HttpGet("grid")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<GradeGridResponse>> Grid(
        [FromQuery] string subjectId,
        [FromQuery] string group,
        CancellationToken ct)
    {
        return Ok(await _grades.GridAsync(HttpActor.Current(User), subjectId, group, ct));
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<GradeResponse>> Create([FromBody] GradeRequest request, CancellationToken ct)
    {
        var created = await _grades.CreateAsync(HttpActor.Current(User), request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("bulk")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<IReadOnlyList<GradeResponse>>> Bulk([FromBody] BulkGradeRequest request, CancellationToken ct)
    {
        return Ok(await _grades.BulkAsync(HttpActor.Current(User), request, ct));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<GradeResponse>> Update(string id, [FromBody] GradeRequest request, CancellationToken ct)
    {
        return Ok(await _grades.UpdateAsync(HttpActor.Current(User), id, request, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _grades.DeleteAsync(HttpActor.Current(User), id, ct);
        return NoContent();
    }
}
