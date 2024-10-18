﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Telegram.Bot;
using TelegramMangaBot;
using TelegramMangaBot.Data;
using TelegramMangaBot.QuartzJobs;
using TelegramMangaBot.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Register Bot configuration
        services.Configure<BotConfiguration>(context.Configuration.GetSection("BotConfiguration"));

        // Register named HttpClient to benefits from IHttpClientFactory
        // and consume it with ITelegramBotClient typed client.
        // More read:
        //  https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#typed-clients
        //  https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
        services.AddHttpClient("telegram_bot_client").RemoveAllLoggers()
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                BotConfiguration? botConfiguration = sp.GetService<IOptions<BotConfiguration>>()?.Value;
                ArgumentNullException.ThrowIfNull(botConfiguration);
                TelegramBotClientOptions options = new(botConfiguration.BotToken);
                return new TelegramBotClient(options, httpClient);
            });

       
        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();
        var absolute = @"C:\Code\TelegramMangaBot\TelegramMangaBot\TelegramMangaBot.db";
        services.AddDbContext<MyDbContext>(
            options => options.UseSqlite($"Data Source={absolute}"));
        services.AddScoped<IMangaScrapingService, MangaScrapingService>();
        services.AddScoped<ISendMangaForAllService, SendMangaForAllService>();
        services.AddQuartz(q =>
        {
            q.AddJob<MangaSenderJob>(opts => opts.WithIdentity("MangaSenderJob"));
            
            q.AddTrigger(opts => opts
                .ForJob("MangaSenderJob")
                .WithIdentity("MangaSenderJob-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(1) 
                    .RepeatForever())); 
            
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        });
    })
    .Build();

await host.RunAsync();