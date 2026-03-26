using Dalamud.Configuration;

namespace Zone.Models;

public class PluginConfig : IPluginConfiguration
{
    public int  Version              { get; set; } = 1;
    public bool ZoneVisionEnabled    { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public int  LastSeenDjId         { get; set; }
    // 0 = Top Left, 1 = Top Right, 2 = Bottom Left, 3 = Bottom Right
    public int  OverlayCorner        { get; set; } = 3;
}
