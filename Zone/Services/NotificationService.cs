using System;
using Dalamud.Interface.ImGuiNotification;

namespace Zone.Services;

public class NotificationService
{
    private DateTime _lastCheck = DateTime.MinValue;
    private const double CheckIntervalSeconds = 30.0;

    public void Update()
    {
        if ((DateTime.UtcNow - _lastCheck).TotalSeconds < CheckIntervalSeconds) return;
        _lastCheck = DateTime.UtcNow;

        CheckDjChange();
    }

    private void CheckDjChange()
    {
        var config = Plugin.Db.GetConfig();
        if (!config.NotificationsEnabled) return;

        var live = Plugin.Db.GetLivePerformance();
        var currentId = live?.Id ?? 0;

        if (live == null) return;
        if (currentId == config.LastSeenDjId) return;

        Plugin.Notifications.AddNotification(new Notification
        {
            Title = "ZONE — Now Live",
            Content = $"DJ {live.DjName} is on stage!",
            Type = NotificationType.Info,
            InitialDuration = TimeSpan.FromSeconds(6),
            Minimized = false
        });

        config.LastSeenDjId = currentId;
        Plugin.Db.SaveConfig(config);
    }
}
