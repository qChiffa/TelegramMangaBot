using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMangaBot.Data;
using TelegramMangaBot.Entities;
using User = TelegramMangaBot.Entities.User;


namespace TelegramMangaBot.Services;



public class UpdateHandler(
    ITelegramBotClient bot
    , ILogger<UpdateHandler> logger
    , MyDbContext context 
    , IMangaScrapingService mangaScrapingService
   ) : IUpdateHandler
{

    private static readonly Dictionary<long, string> UserStates = new();
    private static readonly Dictionary<long, Timer> UserTimers = new();

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        logger.LogInformation("HandleError: {Exception}", exception);
        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
    
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await (update switch
        {
            { Message: { } message }                        => OnMessage(message),
            { CallbackQuery: { } callbackQuery }            => OnCallbackQuery(callbackQuery),
            
            _                                               => UnknownUpdateHandlerAsync(update)
        });
    }
    private Task UnknownUpdateHandlerAsync(Update update)
    {
        logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    private async Task OnMessage(Message msg)
    {
        logger.LogInformation("Receive message type: {MessageType}", msg.Type);
        if (msg.Text is not { } messageText)
            return;

        Message sentMessage = await (messageText.Split(' ')[0] switch
        {
            "/start" => SendButtonsOnStart(msg),
             
            _ => Usage(msg)
        });
        logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
    }

    async Task<Message> SendButtonsOnStart(Message msg)
    {
        var chatId = msg.Chat.Id;
        var userName = msg.From.Username;
        UserStates[chatId] = "none";
        logger.LogInformation($"Send buttons on start to {userName} {chatId}");
        
        var chekUser = await context.Users.SingleOrDefaultAsync(u => u.UserId == chatId);
        
        if (chekUser != null)
        {
            chekUser.Name = $"{userName}";
            context.SaveChanges();
            logger.LogInformation($"такой user {userName}, уже существует в бд");
        }
        else
        {
            context.Users.Add(new User { UserId = chatId, Name = $"{userName}" });
            await context.SaveChangesAsync();
            logger.LogInformation($"информация о пользователе {userName} , с айди {chatId} была добавлена в бд");
        }
        
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("Добавить мангу", "callback_data_1"),
            InlineKeyboardButton.WithCallbackData("Список вашей манги", "callback_data_2"),
        }).AddNewRow(new []
        {
            InlineKeyboardButton.WithCallbackData("Удалить мангу", "callback_data_3"),
            InlineKeyboardButton.WithCallbackData("Обновление сайта", "callback_data_4")
        });
        return await bot.SendTextMessageAsync(msg.Chat,
            text: "Привет , я твой манга бот." +
                  "\nВыберите один из вариантов ответа:",
            replyMarkup: inlineKeyboard);
    }
    
    
    async Task<Message> Usage(Message msg)
    {
        var chatId = msg.Chat.Id;

        if (UserStates.ContainsKey(chatId) && UserStates[chatId] == "expecting_manga_title_to_add")
        {
            var mangaTitle = msg.Text;
            var manga = new Manga {Title = mangaTitle, UserId = chatId};
            context.Manga.Add(manga);
            
            logger.LogInformation($"This ({manga.Title}) has been added to {chatId} in the Database");
            
            await context.SaveChangesAsync();
            UserStates.Remove(chatId);
            UserTimers[chatId].Dispose();
            UserTimers.Remove(chatId);
            return await bot.SendTextMessageAsync(chatId, $"Манга '{mangaTitle}' добавлена в ваш список");
        }
        
        if (UserStates.ContainsKey(chatId) && UserStates[chatId] == "expecting_manga_title_to_delete")
        {
            var mangaTitle = msg.Text;
            var user = await context.Users.Include(u => u.Manga).FirstOrDefaultAsync(u => u.UserId == chatId);
            if (user != null)
            {
                var mangaToDelete = user.Manga.FirstOrDefault(m => m.Title == mangaTitle);
                if (mangaToDelete != null)
                {
                    user.Manga.Remove(mangaToDelete); 
                    await context.SaveChangesAsync();
                }
            }
            UserStates.Remove(chatId);
            return await bot.SendTextMessageAsync(chatId, $"Манга '{mangaTitle}' была удалена из вашего списка");
        }

        if (msg.Type == MessageType.Text && UserStates.Count == 0)
        {
            const string usage = """
                                     <b><u>Начать общение с ботом</u></b>:
                                              ┏━━━━━━━━━━┓
                                                 /start  
                                              ┗━━━━━━━━━━┛
                                 """;
            
            return await bot.SendTextMessageAsync(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
        }

        return msg;
    }
    
    
    private async Task OnCallbackQuery(CallbackQuery callbackQuery)
    {
        logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
        var chatId = callbackQuery.Message.Chat.Id;
        switch (callbackQuery.Data)
        {
            case "callback_data_1":
                bot.AnswerCallbackQueryAsync(callbackQuery.Id, $"Добавить мангу");
                bot.SendTextMessageAsync(
                    callbackQuery.Message.Chat,
                    "Введите название манги. Оно должно быть таким же как и на сайте [asuracomic](https://asuracomic.net)" +
                    "\n Пример : Primer Manga",
                    parseMode: ParseMode.Markdown);
                UserStates[chatId] = "expecting_manga_title_to_add";
                StartTimer(chatId, TimeSpan.FromMinutes(5));
                break;
            case "callback_data_2":
                bot.AnswerCallbackQueryAsync(callbackQuery.Id, $"Список вашей манги");
                var user = await context.Users.Include(u => u.Manga).FirstOrDefaultAsync(u => u.UserId == chatId);
                    
                var mangaList = "Список вашей манги:\n";
                foreach (var manga in user.Manga)
                {
                    mangaList += $"{manga.Title}\n";
                }
                    
                bot.SendTextMessageAsync(chatId,
                    $"{mangaList}");
                break;
            case "callback_data_3":
                bot.AnswerCallbackQueryAsync(callbackQuery.Id, $"Удалить мангу");
                bot.SendTextMessageAsync(
                    callbackQuery.Message.Chat.Id, 
                    "Для удаления введите название манги точно также, как оно написано в вашем списке");
                UserStates[chatId] = "expecting_manga_title_to_delete";
                StartTimer(chatId, TimeSpan.FromMinutes(5));
                break;
            case "callback_data_4":
                bot.AnswerCallbackQueryAsync(callbackQuery.Id, $"Обновление сайта");
                
                await SendMyManga(context, chatId);
                break;
            default:
                bot.AnswerCallbackQueryAsync(callbackQuery.Id, $"Неизвестный выбор");
                break;
        }
    }
    

    private async Task SendMyManga(MyDbContext context, long userId)
    {
        var mangaTitles = context.Manga
             .Where(m => m.UserId == userId)
             .Select(m => m.Title)
             .ToList();
    
         foreach (var mangaTitle in mangaTitles)
         {
             logger.LogInformation($"Processing manga title: {mangaTitle}");
             var titleConverted = mangaTitle.ToLower().Replace(' ', '-');
             await mangaScrapingService.MangaScraping(titleConverted, mangaTitle , userId);
         }
    }

    private void StartTimer(long chatId, TimeSpan timeout)
    {
        if (UserTimers.ContainsKey(chatId))
        {
            UserTimers[chatId].Dispose(); // Останавливаем предыдущий таймер, если он был
        }

        var timer = new Timer(_ =>
        {
            UserStates.Remove(chatId); // Удаляем состояние пользователя
            UserTimers.Remove(chatId);
            bot.SendTextMessageAsync(chatId, "Время ожидания истекло. Пожалуйста, попробуйте снова.").Wait();
        }, null, timeout, Timeout.InfiniteTimeSpan);

        UserTimers[chatId] = timer;
    }
    
}
