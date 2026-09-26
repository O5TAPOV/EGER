using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/students")]
public class StudentsController : ControllerBase
{
    private readonly StudentService _students;

    public StudentsController(StudentService students) => _students = students;

    [HttpGet]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<IReadOnlyList<StudentResponse>>> List(CancellationToken ct)
    {
        return Ok(await _students.ListAsync(ct));
    }

    [HttpGet("groups")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<IReadOnlyList<string>>> Groups(CancellationToken ct)
    {
        return Ok(await _students.GroupsAsync(ct));
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<StudentResponse>> Me(CancellationToken ct)
    {
        var actor = HttpActor.Current(User);
        return Ok(await _students.MeAsync(actor.UserId, ct));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StudentResponse>> Get(string id, CancellationToken ct)
    {
        return Ok(await _students.GetAsync(id, HttpActor.Current(User), ct));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<StudentResponse>> Create([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        var created = await _students.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<StudentResponse>> Update(string id, [FromBody] UpdateStudentRequest request, CancellationToken ct)
    {
        return Ok(await _students.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _students.DeleteAsync(id, ct);
        return NoContent();
    }
}
