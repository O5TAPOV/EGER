using Eger.Application.Abstractions;
using Eger.Infrastructure.Mongo;
using Eger.Infrastructure.Redis;
using Eger.Infrastructure.Security;
using Eger.Infrastructure.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Eger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<MongoContext>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IProfessorRepository, ProfessorRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<IGradeRepository, GradeRepository>();
        services.AddScoped<IGradingSettingsRepository, GradingSettingsRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connection = configuration["Redis:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connection))
                connection = "localhost:6379,abortConnect=false";
            if (!connection.Contains("abortConnect", StringComparison.OrdinalIgnoreCase))
                connection += ",abortConnect=false";
            return ConnectionMultiplexer.Connect(connection);
        });
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
        services.AddSingleton<ITwoFactorService, RedisTwoFactorService>();
        services.AddHostedService<TelegramLinkHostedService>();
        return services;
    }
}
