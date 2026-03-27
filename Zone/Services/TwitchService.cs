using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Zone.Services;

public class TwitchService : IDisposable
{
    private static readonly HttpClient Http = new();
    private const string WorkerUrl = "https://zone-api.yunookami.workers.dev/twitch-status";
    private const double CheckIntervalSeconds = 60.0;

    private DateTime _lastCheck = DateTime.MinValue;

    private static readonly DateTime[] EventDates =
    {
        new(2026, 3, 27),
        new(2026, 3, 28),
    };

    private static DateTime EventDate(DateTime utc)
        => utc.Hour < 6 ? utc.Date.AddDays(-1) : utc.Date;

    public void Update()
    {
        if (!EventDates.Contains(EventDate(DateTime.UtcNow))) return;
        if ((DateTime.UtcNow - _lastCheck).TotalSeconds < CheckIntervalSeconds) return;
        _lastCheck = DateTime.UtcNow;
        _ = CheckAllAsync();
    }

    private async Task CheckAllAsync()
    {
        try
        {
            var utcNow  = DateTime.UtcNow;
            var adjDate = utcNow.Hour < 3 ? utcNow.Date.AddDays(-1) : utcNow.Date;
            int? evDay  = adjDate == new DateTime(2026, 3, 27) ? 1
                        : adjDate == new DateTime(2026, 3, 28) ? 2
                        : (int?)null;

            var performances = Plugin.Db.GetAllPerformances();
            int liveId = 0;

            var now = utcNow.TimeOfDay;

            foreach (var p in performances)
            {
                if (string.IsNullOrEmpty(p.TwitchLogin)) continue;
                if (p.Day != evDay) continue;

                // Ignore DJs whose set is already over
                if (TimeSpan.TryParse(p.EndTime, out var endT))
                {
                    // Handle sets that go past midnight (e.g. 23:00 - 00:00)
                    var adjustedEnd = endT < TimeSpan.FromHours(3) ? endT.Add(TimeSpan.FromHours(24)) : endT;
                    var adjustedNow = now < TimeSpan.FromHours(3) ? now.Add(TimeSpan.FromHours(24)) : now;
                    if (adjustedNow > adjustedEnd) continue;
                }

                var json = await Http.GetStringAsync($"{WorkerUrl}?channel={p.TwitchLogin}");
                var data = JsonSerializer.Deserialize<TwitchStatusResponse>(json);
                if (data?.IsLive == true)
                {
                    liveId = p.Id;
                    break;
                }
            }

            Plugin.Db.SetLivePerformance(liveId);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[Zone] Twitch status check failed");
        }
    }

    public void ResetIfNotEventDay()
    {
        if (!EventDates.Contains(EventDate(DateTime.UtcNow)))
            Plugin.Db.SetLivePerformance(0);
    }

    public void Dispose() { }

    private class TwitchStatusResponse
    {
        [JsonPropertyName("isLive")]
        public bool IsLive { get; set; }
    }
}
