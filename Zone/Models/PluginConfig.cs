namespace Zone.Models;

public class PluginConfig
{
    public bool ZoneVisionEnabled { get; set; }
    public bool TimeLockEnabled { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int LastSeenDjId { get; set; }
}
