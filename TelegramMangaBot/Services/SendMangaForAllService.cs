using TelegramMangaBot.Data;

namespace TelegramMangaBot.Services;

public interface ISendMangaForAllService
{
    public Task SendMangaForAll(MyDbContext context);
}

public class SendMangaForAllService(IMangaScrapingService mangaScrapingService): ISendMangaForAllService
{
    public async Task SendMangaForAll(MyDbContext context)
    {
        var users = context.Users
            .Select(u => new
            {
                u.UserId,
                MangaTitles = u.Manga.Select(m => m.Title).ToList()
            });

        foreach (var user in users)
        {
            Console.WriteLine($"User ID: {user.UserId}");
            Console.WriteLine("Manga titles:");
            foreach (var titleName in user.MangaTitles)
            {
                var titleConverted = titleName.ToLower().Replace(' ', '-');
                await mangaScrapingService.MangaScraping(titleConverted, titleName , user.UserId);
            }
            Console.WriteLine();
        }
    }
}