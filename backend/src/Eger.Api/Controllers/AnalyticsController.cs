using Eger.Api.Auth;
using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analytics;

    public AnalyticsController(AnalyticsService analytics) => _analytics = analytics;

    [HttpGet("overview")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<OverviewResponse>> Overview(CancellationToken ct)
    {
        return Ok(await _analytics.OverviewAsync(ct));
    }

    [HttpGet("subject/{subjectId}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Professor}")]
    public async Task<ActionResult<ClassAnalyticsResponse>> Subject(string subjectId, [FromQuery] string? group, CancellationToken ct)
    {
        return Ok(await _analytics.SubjectAsync(HttpActor.Current(User), subjectId, group, ct));
    }

    [HttpGet("me")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<StudentAnalyticsResponse>> Me(CancellationToken ct)
    {
        return Ok(await _analytics.ForUserAsync(HttpActor.Current(User).UserId, ct));
    }

    [HttpGet("student/{studentId}")]
    public async Task<ActionResult<StudentAnalyticsResponse>> Student(string studentId, CancellationToken ct)
    {
        return Ok(await _analytics.ForStudentIdAsync(HttpActor.Current(User), studentId, ct));
    }
}
