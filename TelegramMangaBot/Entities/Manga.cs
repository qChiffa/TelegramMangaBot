namespace TelegramMangaBot.Entities;

public class Manga
{
    public int Id { get; set; }
    public string Title { get; set; }
    public long UserId { get; set; }
    
    
    public User User { get; set; }
}