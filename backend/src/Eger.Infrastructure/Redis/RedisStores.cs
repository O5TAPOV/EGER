using System.Security.Cryptography;
using System.Text;
using Eger.Application.Abstractions;
using StackExchange.Redis;

namespace Eger.Infrastructure.Redis;

public class RedisSessionStore : ISessionStore
{
    private readonly IDatabase _db;

    public RedisSessionStore(IConnectionMultiplexer multiplexer) => _db = multiplexer.GetDatabase();

    public Task StoreAsync(string userId, string jti, TimeSpan ttl, CancellationToken ct = default) =>
        _db.StringSetAsync(SessionKey(userId), jti, ttl);

    public async Task<bool> IsActiveAsync(string userId, string jti, CancellationToken ct = default)
    {
        var stored = await _db.StringGetAsync(SessionKey(userId));
        if (!stored.HasValue)
            return false;
        var expected = stored.ToString();
        var actual = jti ?? "";
        if (expected.Length != actual.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }

    public Task RevokeAsync(string userId, CancellationToken ct = default) =>
        _db.KeyDeleteAsync(SessionKey(userId));

    private static string SessionKey(string userId) => $"eger:session:{userId}";
}

public class RedisTwoFactorService : ITwoFactorService
{
    private readonly IDatabase _db;
    private readonly ITelegramNotifier _telegram;
    private readonly IEmailSender _email;

    public RedisTwoFactorService(IConnectionMultiplexer multiplexer, ITelegramNotifier telegram, IEmailSender email)
    {
        _db = multiplexer.GetDatabase();
        _telegram = telegram;
        _email = email;
    }

    public async Task IssueAsync(string userId, TwoFactorChannel channel, string destination, CancellationToken ct = default)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var key = Key(userId);
        await _db.StringSetAsync(key, code, TimeSpan.FromMinutes(5));
        try
        {
            if (channel == TwoFactorChannel.Email)
                await _email.SendAuthCodeAsync(destination, code, ct);
            else
                await _telegram.SendAuthCodeAsync(destination, code, ct);
        }
        catch
        {
            await _db.KeyDeleteAsync(key);
            throw;
        }
    }

    public async Task<bool> VerifyAsync(string userId, string code, CancellationToken ct = default)
    {
        if (code.Length != 6 || !code.All(char.IsDigit))
            return false;

        var stored = await _db.StringGetAsync(Key(userId));
        if (!stored.HasValue)
            return false;

        var expected = stored.ToString();
        var matches = expected.Length == code.Length &&
                      CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(code));
        if (!matches)
            return false;

        await _db.KeyDeleteAsync(Key(userId));
        return true;
    }

    private static string Key(string userId) => $"eger:2fa:{userId}";
}
