using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Eger.Infrastructure.Telegram;

public class TelegramLinkHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramLinkHostedService> _logger;

    public TelegramLinkHostedService(IConfiguration configuration, ILogger<TelegramLinkHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogInformation("TELEGRAM_BOT_TOKEN не задано. Прив'язку Chat ID через бота вимкнено.");
            return;
        }

        var bot = new TelegramBotClient(token);
        var offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdates(offset: offset, timeout: 20, cancellationToken: stoppingToken);
                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    var text = update.Message?.Text;
                    if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/start", StringComparison.Ordinal))
                        continue;

                    var chatId = update.Message!.Chat.Id;
                    await bot.SendMessage(
                        chatId,
                        $"Ваш Telegram Chat ID: {chatId}. Вставте його в кабінеті EGER, щоб увімкнути двофакторну автентифікацію.",
                        cancellationToken: stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Помилка опитування Telegram");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
