namespace TelegramMangaBot.Entities;

public class User
{
    public long UserId { get; set; }
    public string Name { get; set; }
    
    public ICollection<Manga> Manga { get; set; }
}