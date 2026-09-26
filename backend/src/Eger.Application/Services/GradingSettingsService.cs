using Eger.Application.Abstractions;
using Eger.Application.Dtos;
using Eger.Application.Exceptions;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class GradingSettingsService
{
    private readonly IGradingSettingsRepository _settings;

    public GradingSettingsService(IGradingSettingsRepository settings) => _settings = settings;

    public async Task<GradingSettingsResponse> GetAsync(CancellationToken ct = default) =>
        Map(await _settings.GetAsync(ct));

    public async Task<GradingSettingsResponse> UpdateAsync(UpdateGradingSettingsRequest request, CancellationToken ct = default)
    {
        if (request.CurrentMax + request.FinalMax > 100)
            throw new AppException(400, "Сума максимумів поточних і підсумкових балів не може перевищувати 100");
        if (request.PassThreshold > request.CurrentMax + request.FinalMax)
            throw new AppException(400, "Поріг зарахування не може бути вищим за суму максимумів");

        var settings = await _settings.GetAsync(ct);
        settings.PassThreshold = request.PassThreshold;
        settings.CurrentMax = request.CurrentMax;
        settings.FinalMax = request.FinalMax;
        await _settings.UpdateAsync(settings, ct);
        return Map(settings);
    }

    private static GradingSettingsResponse Map(GradingSettings settings) => new()
    {
        PassThreshold = settings.PassThreshold,
        CurrentMax = settings.CurrentMax,
        FinalMax = settings.FinalMax
    };
}
