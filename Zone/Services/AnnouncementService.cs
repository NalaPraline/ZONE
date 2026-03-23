using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;

namespace Zone.Services;

public class AnnouncementService : IDisposable
{
    private const string WsUrl = "wss://zsysadmiannonce.nalapraline.com/ws";

    private ClientWebSocket?  _ws;
    private CancellationTokenSource _cts = new();
    private bool _disposed;

    public AnnouncementService()
    {
        _ = ConnectLoopAsync();
    }

    private async Task ConnectLoopAsync()
    {
        while (!_disposed)
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(WsUrl), _cts.Token).ConfigureAwait(false);
                Plugin.Log.Information("[Announce] Connected to announcement server.");
                await ReceiveLoopAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "[Announce] WebSocket error, reconnecting in 10s...");
            }
            finally
            {
                _ws?.Dispose();
                _ws = null;
            }

            if (!_disposed)
                await Task.Delay(10_000, _cts.Token).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[4096];
        while (_ws!.State == WebSocketState.Open && !_disposed)
        {
            var result = await _ws.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            HandleMessage(json);
        }
    }

    private static void HandleMessage(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root        = doc.RootElement;
            var message     = root.GetProperty("message").GetString() ?? "";
            var type        = root.TryGetProperty("type", out var t) ? t.GetString() ?? "info" : "info";

            var notifType = type switch
            {
                "alert" => NotificationType.Warning,
                "event" => NotificationType.Success,
                _       => NotificationType.Info
            };

            Plugin.Log.Information($"[Announce] Received: {message}");

            // Must show notification on the main thread
            var title = type switch
            {
                "alert" => "ALERT",
                "event" => "EVENT",
                _       => "INFORMATION"
            };

            Plugin.Framework.RunOnTick(() =>
            {
                Plugin.Notifications.AddNotification(new Notification
                {
                    Content   = message,
                    Title     = title,
                    Type      = notifType,
                    Minimized = false,
                });
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[Announce] Failed to parse message.");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _cts.Cancel();
        _ws?.Dispose();
    }
}
