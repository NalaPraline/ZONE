using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Zone.Services;

public class ImageCacheService : IDisposable
{
    private const string BaseUrl = "https://raw.githubusercontent.com/NalaPraline/ZONE/main/Zone/Data/";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public string DjLogosDir  { get; }
    public string PartnersDir { get; }

    private static readonly string[] DjLogoFiles =
    [
        "Khyddin.png", "Mochiii.png", "Devil Cyborg.png", "Khangomon.png",
        "8bit.png", "Cynaxia.png", "raindrop.png", "skytex.png", "SwayWave.png",
        "Rin.png", "Leysy.png", "Elana.png", "Swordmaid.png", "Gaia.png",
        "Miyu Fhey.png", "Aemilia.png", "swage.png", "Schoki.png"
    ];

    private static readonly string[] PartnerFiles =
    [
        "ENVY.png", "URBAN.png", "Project XIV.png", "Nocturn.png", "Phoenix Nights.png",
        "Clubbers LS.png", "Habitat.png", "Gipsy Cat.png", "Desert Rose.png",
        "Moonlit Kiss.png", "Selune.png", "Prism.png", "Tempest.png", "Woah.png",
        "Black Sapphire.png", "Ignite.png", "Eden.png", "The Beat.png", "Nedori.png",
        "Church of Metal.png", "Psyko NightClub.png", "L'Atelier des Saveurs.png"
    ];

    public ImageCacheService()
    {
        DjLogosDir  = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "djlogos");
        PartnersDir = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "partners");
        Directory.CreateDirectory(DjLogosDir);
        Directory.CreateDirectory(PartnersDir);
        _ = DownloadAllAsync();
    }

    private async Task DownloadAllAsync()
    {
        foreach (var file in DjLogoFiles)
            await DownloadIfMissingAsync($"djlogos/{Uri.EscapeDataString(file)}", Path.Combine(DjLogosDir, file)).ConfigureAwait(false);

        foreach (var file in PartnerFiles)
            await DownloadIfMissingAsync($"partners/{Uri.EscapeDataString(file)}", Path.Combine(PartnersDir, file)).ConfigureAwait(false);
    }

    private async Task DownloadIfMissingAsync(string relativePath, string localPath)
    {
        if (File.Exists(localPath)) return;
        try
        {
            var bytes = await _http.GetByteArrayAsync(BaseUrl + relativePath).ConfigureAwait(false);
            await File.WriteAllBytesAsync(localPath, bytes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"[Zone] Failed to download {relativePath}");
        }
    }

    public void Dispose() => _http.Dispose();
}
