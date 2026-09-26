using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Eger.Application.Abstractions;
using Eger.Application.Exceptions;
using Eger.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Eger.Api.Security;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ISessionStore _sessions;

    public JwtTokenService(IConfiguration configuration, ISessionStore sessions)
    {
        _configuration = configuration;
        _sessions = sessions;
    }

    public async Task<string> CreateTokenAsync(User user, string? fullName, CancellationToken ct = default)
    {
        var secret = _configuration["Jwt:Secret"] ?? "";
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new AppException(500, "JWT-секрет не налаштовано або він коротший за 32 байти");

        var minutes = 480;
        if (int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var configured) && configured > 0)
            minutes = configured;

        var ttl = TimeSpan.FromMinutes(minutes);
        var jti = Guid.NewGuid().ToString("N");
        await _sessions.StoreAsync(user.Id, jti, ttl, ct);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(ttl),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
