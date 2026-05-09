namespace BookApi.Models;

public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string Author { get; set; } = "";
    public int UserId { get; set; }
}