using Dalamud.Configuration;

namespace Zone.Models;

public class PluginConfig : IPluginConfiguration
{
    public int  Version              { get; set; } = 1;
    public bool ZoneVisionEnabled    { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int  LastSeenDjId         { get; set; }
}
