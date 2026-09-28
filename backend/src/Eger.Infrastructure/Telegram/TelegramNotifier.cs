using Eger.Application.Abstractions;
using Eger.Application.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Eger.Infrastructure.Telegram;

public class TelegramNotifier : ITelegramNotifier
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(IConfiguration configuration, ILogger<TelegramNotifier> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAuthCodeAsync(string chatId, string code, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var parsed) || parsed <= 0)
            throw new AppException(400, "Некоректний Telegram Chat ID");

        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
            throw new AppException(503, "Telegram-бот не налаштовано. Задайте змінну TELEGRAM_BOT_TOKEN.");

        var text = $"Твій код авторизації в EGER: {code}";
        try
        {
            var bot = new TelegramBotClient(token);
            await bot.SendMessage(parsed, text, cancellationToken: ct);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося надіслати код двофакторної перевірки в Telegram");
            throw new AppException(503, "Не вдалося надіслати код у Telegram. Перевірте токен бота та Chat ID.");
        }
    }
}
