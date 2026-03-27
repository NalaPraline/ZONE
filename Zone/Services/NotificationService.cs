using System;
using System.Collections.Generic;
using Dalamud.Interface.ImGuiNotification;

namespace Zone.Services;

public class NotificationService
{
    private DateTime _lastCheck = DateTime.MinValue;
    private readonly HashSet<int> _notifiedActivityIds = new();
    private const double CheckIntervalSeconds = 30.0;

    public void Update()
    {
        if ((DateTime.UtcNow - _lastCheck).TotalSeconds < CheckIntervalSeconds) return;
        _lastCheck = DateTime.UtcNow;

        CheckDjChange();
        CheckUpcomingActivities();
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

    private void CheckUpcomingActivities()
    {
        var config = Plugin.Db.GetConfig();
        if (!config.NotificationsEnabled) return;

        var utcNow  = DateTime.UtcNow;
        var adjDate = utcNow.Hour < 3 ? utcNow.Date.AddDays(-1) : utcNow.Date;
        int? evDay  = adjDate == new DateTime(2026, 3, 27) ? 1
                    : adjDate == new DateTime(2026, 3, 28) ? 2
                    : (int?)null;
        if (evDay == null) return;

        var now        = utcNow.TimeOfDay;
        var activities = Plugin.Db.GetAllActivities().FindAll(a => a.Day == evDay);

        foreach (var activity in activities)
        {
            if (!TimeSpan.TryParse(activity.StartTime, out var startTime)) continue;
            if (_notifiedActivityIds.Contains(activity.Id)) continue;

            var timeUntil = startTime - now;
            if (timeUntil >= TimeSpan.Zero && timeUntil <= TimeSpan.FromMinutes(5))
            {
                Plugin.Notifications.AddNotification(new Notification
                {
                    Title = "ZONE — Starting Soon",
                    Content = $"{activity.Name} begins in 5 minutes.",
                    Type = NotificationType.Info,
                    InitialDuration = TimeSpan.FromSeconds(6),
                    Minimized = false
                });
                _notifiedActivityIds.Add(activity.Id);
            }
        }
    }
}
