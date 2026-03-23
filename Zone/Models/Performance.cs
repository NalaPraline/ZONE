namespace Zone.Models;

public class Performance
{
    public int Id { get; set; }
    public string DjName { get; set; } = "";
    public int Day { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string? StreamUrl { get; set; }
    public bool IsLive { get; set; }
    public string? AvatarPath { get; set; }
    public string? TwitchLogin { get; set; }
    public string? LogoPath { get; set; }
}
