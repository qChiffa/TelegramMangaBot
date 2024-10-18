using Microsoft.EntityFrameworkCore;
using TelegramMangaBot.Entities;


namespace TelegramMangaBot.Data;

public class MyDbContext(DbContextOptions<MyDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Manga> Manga { get; set; }
    
}