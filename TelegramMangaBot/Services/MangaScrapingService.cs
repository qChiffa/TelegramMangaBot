using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMangaBot.Data;

namespace TelegramMangaBot.Services;

public interface IMangaScrapingService
{
    Task MangaScraping(string titleConverted , string titleName, long userId);
}

public class MangaScrapingService(ITelegramBotClient botClient, ILogger<MangaScrapingService> logger, HttpClient httpClient) : IMangaScrapingService
{
    private static readonly Regex MyRegex = new(@"(\d+)\s*(minutes?|hours?|days?|weeks?)\s*ago");

    public async Task MangaScraping(string titleConverted, string titleName,long userId)
    {
        const string url = "https://asuracomic.net";
        var html = await httpClient.GetStringAsync(url).ConfigureAwait(false);
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        
        
        var allTitles =
            htmlDocument.DocumentNode.SelectNodes(
                "//div[@class='w-full p-1 pt-1 pb-3 border-b-[1px] border-b-[#312f40]']");
        
        if (allTitles == null)
        {
            Console.WriteLine("No titles found.");
            return;
        }

        foreach (var title in allTitles)
        {

            var allChapters =
                title.SelectNodes(".//div[contains (@class , 'flex flex-row justify-between rounded-sm')]");
            
            int processedChapters = 0;

            foreach (var chapter in allChapters)
            {
                var chapterNode = chapter.SelectSingleNode($".//a[contains(@href, '{titleConverted}')]");

                if (chapterNode != null)
                {
                    var chapterNum = chapterNode.SelectSingleNode("./span").InnerText.Trim();
                    var chapterLink = chapter.SelectSingleNode(".//a")?.GetAttributeValue("href", string.Empty) ?? "";
                    var fullChapterLink = "https://asuracomic.net" + chapterLink;

                    var timeAgoStr = chapter.SelectSingleNode(".//p[contains(@class, 'flex items-end  ml-2 text-[12px] text-[#555555]')]")?.InnerText.Trim() ?? "";

                    var match = MyRegex.Match(timeAgoStr);
                    if (match.Success)
                    {
                        var amount = int.Parse(match.Groups[1].Value);
                        var unit = match.Groups[2].Value.ToLower();

                        var timeSpan = unit.ToLower() switch
                        {
                            "minute" or "minutes" => TimeSpan.FromMinutes(amount),
                            "hour" or "hours" => TimeSpan.FromHours(amount),
                            "day" or "days" => TimeSpan.FromDays(amount),
                            "week" or "weeks" => TimeSpan.FromDays(amount * 7),
                            _ => throw new InvalidOperationException("Unknown unit.")
                        };
                        var timeAgoDateTime = DateTime.Now - timeSpan;

                        if ((DateTime.Now - timeAgoDateTime).TotalHours < 1)
                        {
                            var imgSrc = htmlDocument.DocumentNode.SelectSingleNode($".//a[contains(@href, '{titleConverted}')]/img")
                                .GetAttributeValue("src", "nf");
                            
                            await botClient.SendPhotoAsync(
                                chatId: userId,
                                photo: new InputFileUrl(imgSrc),
                                caption:
                                $"""
                                *{titleName}*
                                {chapterNum}
                                {timeAgoStr} 
                                [Читать Главу]({fullChapterLink})
                                
                                """,
                                parseMode: ParseMode.Markdown
                            );
                        }
                        processedChapters++;
                    }
                    if (processedChapters == 3)
                    {
                        Console.WriteLine($"Обработано 3 главы для манги {titleName}");
                        break; // Прерываем цикл после обработки 3 глав
                    }

                }

            }
        }
    }
}