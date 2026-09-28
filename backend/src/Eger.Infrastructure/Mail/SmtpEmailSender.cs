using Eger.Application.Abstractions;
using Eger.Application.Exceptions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Eger.Infrastructure.Mail;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"]);

    public async Task SendAuthCodeAsync(string email, string code, CancellationToken ct = default)
    {
        var host = _configuration["Smtp:Host"]?.Trim();
        if (string.IsNullOrWhiteSpace(host))
            throw new AppException(503, "Надсилання коду на пошту не налаштовано.");

        if (!int.TryParse(_configuration["Smtp:Port"], out var port) || port <= 0)
            port = 1025;

        var from = _configuration["Smtp:From"]?.Trim();
        if (string.IsNullOrWhiteSpace(from))
            from = "eger@localhost";

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Код авторизації в EGER";
        message.Body = new TextPart("plain")
        {
            Text = $"Твій код авторизації в EGER: {code}"
        };

        try
        {
            using var client = new SmtpClient();
            var secure = port switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.None
            };
            await client.ConnectAsync(host, port, secure, ct);
            var user = _configuration["Smtp:User"]?.Trim();
            var password = _configuration["Smtp:Password"] ?? "";
            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося надіслати код двофакторної перевірки на пошту");
            throw new AppException(503, "Не вдалося надіслати код на пошту. Перевірте параметри SMTP.");
        }
    }
}
