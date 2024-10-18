using Quartz;
using TelegramMangaBot.Data;
using TelegramMangaBot.Services;


namespace TelegramMangaBot.QuartzJobs;

public class MangaSenderJob(ISendMangaForAllService sendMangaForAllService, MyDbContext dbContext) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await sendMangaForAllService.SendMangaForAll(dbContext);
    }
}