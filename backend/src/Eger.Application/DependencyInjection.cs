using Eger.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Eger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<StudentService>();
        services.AddScoped<ProfessorService>();
        services.AddScoped<SubjectService>();
        services.AddScoped<GradeService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<GradingSettingsService>();
        services.AddScoped<AcademicService>();
        services.AddScoped<RegisterService>();
        services.AddScoped<SeedService>();
        services.AddScoped<AdminBootstrap>();
        return services;
    }
}
