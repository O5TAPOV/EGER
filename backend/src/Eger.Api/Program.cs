using System.Text;
using System.Text.Json;
using Eger.Api.Middleware;
using Eger.Api.Security;
using Eger.Application;
using Eger.Application.Abstractions;
using Eger.Application.Services;
using Eger.Infrastructure;
using Eger.Infrastructure.Mongo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

static void BindEnv(WebApplicationBuilder webBuilder, string envName, string configKey)
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value))
        webBuilder.Configuration[configKey] = value;
}

var builder = WebApplication.CreateBuilder(args);

BindEnv(builder, "MONGO_CONNECTION_STRING", "Mongo:ConnectionString");
BindEnv(builder, "MONGO_DATABASE", "Mongo:Database");
BindEnv(builder, "REDIS_CONNECTION_STRING", "Redis:ConnectionString");
BindEnv(builder, "JWT_SECRET", "Jwt:Secret");
BindEnv(builder, "JWT_ISSUER", "Jwt:Issuer");
BindEnv(builder, "JWT_AUDIENCE", "Jwt:Audience");
BindEnv(builder, "TELEGRAM_BOT_TOKEN", "Telegram:BotToken");
BindEnv(builder, "CORS_ORIGINS", "Cors:Origins");
BindEnv(builder, "SEED_ADMIN_EMAIL", "Seed:AdminEmail");
BindEnv(builder, "SEED_ADMIN_PASSWORD", "Seed:AdminPassword");

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "";
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Jwt:Secret має містити щонайменше 32 байти. Задайте JWT_SECRET.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors)
                .Select(error =>
                {
                    var message = error.ErrorMessage;
                    if (string.IsNullOrWhiteSpace(message) || message.Contains("request body", StringComparison.OrdinalIgnoreCase))
                        return "Перевірте заповнені поля";
                    return message;
                })
                .Distinct()
                .ToArray();
            return new BadRequestObjectResult(new { message = errors.Length == 0 ? "Некоректні дані" : string.Join(" ", errors) });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EGER API",
        Version = "v1",
        Description = "Educational Grade Evaluation & Reporting — навчальна система оцінювання та звітності"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT у форматі: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var origins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:5173,http://localhost:8080")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(jti))
                {
                    context.Fail("Недійсний токен");
                    return;
                }

                var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
                if (!await sessions.IsActiveAsync(userId, jti, context.HttpContext.RequestAborted))
                    context.Fail("Сесію завершено");
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Потрібна авторизація" }));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Недостатньо прав" }));
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await services.GetRequiredService<IUserRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<IStudentRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<IProfessorRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<ISubjectRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<IGradeRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<IClassSessionRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<IJournalSheetRepository>().EnsureIndexesAsync();
    await services.GetRequiredService<IGradingSettingsRepository>().GetAsync();

    var adminEmail = app.Configuration["Seed:AdminEmail"] ?? "admin@eger.ua";
    var adminPassword = app.Configuration["Seed:AdminPassword"] ?? "Admin123!";
    await services.GetRequiredService<AdminBootstrap>().EnsureAdminAsync(adminEmail, adminPassword);
    app.Logger.LogInformation("EgerDb готова. Початковий адміністратор: {Email}", adminEmail);
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "EGER API v1");
    options.RoutePrefix = "swagger";
});
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (MongoContext mongo, IConnectionMultiplexer redis) =>
{
    await mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    var pong = await redis.GetDatabase().PingAsync();
    return Results.Ok(new { status = "ok", redisMs = Math.Round(pong.TotalMilliseconds, 1) });
});

app.Run();
