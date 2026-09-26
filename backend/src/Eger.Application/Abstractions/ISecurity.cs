using Eger.Domain.Entities;

namespace Eger.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenService
{
    Task<string> CreateTokenAsync(User user, string? fullName, CancellationToken ct = default);
}

public interface ISessionStore
{
    Task StoreAsync(string userId, string jti, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> IsActiveAsync(string userId, string jti, CancellationToken ct = default);
    Task RevokeAsync(string userId, CancellationToken ct = default);
}

public interface ITwoFactorService
{
    Task IssueAsync(string userId, string telegramChatId, CancellationToken ct = default);
    Task<bool> VerifyAsync(string userId, string code, CancellationToken ct = default);
}

public interface ITelegramNotifier
{
    Task SendAuthCodeAsync(string chatId, string code, CancellationToken ct = default);
}
