using Eger.Application.Dtos;
using Eger.Application.Services;
using Eger.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings/grading")]
public class SettingsController : ControllerBase
{
    private readonly GradingSettingsService _settings;

    public SettingsController(GradingSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<ActionResult<GradingSettingsResponse>> Get(CancellationToken ct)
    {
        return Ok(await _settings.GetAsync(ct));
    }

    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<GradingSettingsResponse>> Update(
        [FromBody] UpdateGradingSettingsRequest request,
        CancellationToken ct)
    {
        return Ok(await _settings.UpdateAsync(request, ct));
    }
}
