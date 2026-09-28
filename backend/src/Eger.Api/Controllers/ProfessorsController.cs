using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/professors")]
public class ProfessorsController : ControllerBase
{
    private readonly ProfessorService _professors;

    public ProfessorsController(ProfessorService professors) => _professors = professors;

    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<IReadOnlyList<ProfessorResponse>>> List(CancellationToken ct)
    {
        return Ok(await _professors.ListAsync(ct));
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Professor)]
    public async Task<ActionResult<ProfessorResponse>> Me(CancellationToken ct)
    {
        var actor = HttpActor.Current(User);
        return Ok(await _professors.MeAsync(actor.UserId, ct));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<ProfessorResponse>> Get(string id, CancellationToken ct)
    {
        return Ok(await _professors.GetAsync(id, ct));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ProfessorResponse>> Create([FromBody] CreateProfessorRequest request, CancellationToken ct)
    {
        var created = await _professors.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ProfessorResponse>> Update(string id, [FromBody] UpdateProfessorRequest request, CancellationToken ct)
    {
        return Ok(await _professors.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _professors.DeleteAsync(id, ct);
        return NoContent();
    }
}
