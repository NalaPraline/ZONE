namespace Zone.Models;

public class Partner
{
    public int     Id          { get; set; }
    public string  Name        { get; set; } = "";
    public string? Description { get; set; }
    public string? LogoPath    { get; set; }
    public string? DiscordUrl  { get; set; }
    public string? TwitchUrl   { get; set; }
    public string? WebsiteUrl  { get; set; }
}
