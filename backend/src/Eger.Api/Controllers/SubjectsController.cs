using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/subjects")]
public class SubjectsController : ControllerBase
{
    private readonly SubjectService _subjects;

    public SubjectsController(SubjectService subjects) => _subjects = subjects;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubjectResponse>>> List(CancellationToken ct)
    {
        return Ok(await _subjects.ListAsync(ct));
    }

    [HttpGet("mine")]
    [Authorize(Roles = Roles.Professor)]
    public async Task<ActionResult<IReadOnlyList<SubjectResponse>>> Mine(CancellationToken ct)
    {
        var actor = HttpActor.Current(User);
        return Ok(await _subjects.MineAsync(actor.UserId, ct));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SubjectResponse>> Get(string id, CancellationToken ct)
    {
        return Ok(await _subjects.GetAsync(id, ct));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SubjectResponse>> Create([FromBody] SubjectRequest request, CancellationToken ct)
    {
        var created = await _subjects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SubjectResponse>> Update(string id, [FromBody] SubjectRequest request, CancellationToken ct)
    {
        return Ok(await _subjects.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _subjects.DeleteAsync(id, ct);
        return NoContent();
    }
}
