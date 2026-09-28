namespace Eger.Domain.Entities;

public class User
{
    public string Id { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool Is2FAEnabled { get; set; }

    public string? TelegramChatId { get; set; }
}
