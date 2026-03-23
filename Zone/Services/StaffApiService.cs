using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zone.Models;

namespace Zone.Services;

public class StaffApiService : IDisposable
{
    private const string StaffApiUrl = "https://broad-shape-ac65.yunookami.workers.dev/staff.json";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    private readonly string _avatarCacheDir;

    public StaffApiService()
    {
        _avatarCacheDir = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "avatars");
        Directory.CreateDirectory(_avatarCacheDir);
        _ = SyncStaffAsync();
    }

    public async Task<bool> SyncStaffAsync()
    {
        try
        {
            var json  = await _http.GetStringAsync(StaffApiUrl).ConfigureAwait(false);
            var staff = JsonSerializer.Deserialize<List<StaffMember>>(json, JsonOptions);
            if (staff == null || staff.Count == 0) return false;

            // Download avatar images that are URLs
            foreach (var s in staff)
            {
                if (!string.IsNullOrWhiteSpace(s.AvatarPath) &&
                    s.AvatarPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    s.AvatarPath = await DownloadAvatarAsync(s.AvatarPath).ConfigureAwait(false)
                                   ?? s.AvatarPath;
                }
            }

            Plugin.Db.UpsertStaffFromApi(staff);
            Plugin.Log.Information($"[Zone] Synced {staff.Count} staff members from API.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Zone] Failed to sync staff from API");
            return false;
        }
    }

    private async Task<string?> DownloadAvatarAsync(string url)
    {
        try
        {
            // Use URL hash as filename to avoid re-downloading
            var fileName = $"{Math.Abs(url.GetHashCode())}.png";
            var localPath = Path.Combine(_avatarCacheDir, fileName);

            if (!File.Exists(localPath))
            {
                var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
                await File.WriteAllBytesAsync(localPath, bytes).ConfigureAwait(false);
                Plugin.Log.Information($"[Zone] Downloaded avatar to {localPath}");
            }
            return localPath;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"[Zone] Failed to download avatar from {url}");
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
