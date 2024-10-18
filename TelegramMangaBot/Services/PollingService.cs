using Microsoft.Extensions.Logging;
using TelegramMangaBot.Abstract;

namespace TelegramMangaBot.Services;

// Compose Polling and ReceiverService implementations
public class PollingService(IServiceProvider serviceProvider, ILogger<PollingService> logger)
    : PollingServiceBase<ReceiverService>(serviceProvider, logger);
