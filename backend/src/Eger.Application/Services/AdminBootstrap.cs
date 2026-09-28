using Eger.Application.Abstractions;
using Eger.Domain;
using Eger.Domain.Entities;

namespace Eger.Application.Services;

public class AdminBootstrap
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public AdminBootstrap(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task EnsureAdminAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (password.Length < 8)
            throw new InvalidOperationException("Пароль початкового адміністратора має містити щонайменше 8 символів");

        if (await _users.GetByEmailAsync(normalized, ct) is not null)
            return;

        await _users.CreateAsync(new User
        {
            Email = normalized,
            PasswordHash = _hasher.Hash(password),
            Role = Roles.Admin,
            Is2FAEnabled = false
        }, ct);
    }
}
