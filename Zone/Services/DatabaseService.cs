using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zone.Models;

namespace Zone.Services;

public class DatabaseService : IDisposable
{
    private PluginConfig _config;

    private readonly List<Performance>  _performances = new();
    private readonly List<Activity>     _activities   = new();
    private readonly List<StaffMember>  _staff        = new();
    private readonly List<Partner>      _partners     = new();

    private int _nextPerfId    = 1;
    private int _nextActId     = 1;
    private int _nextStaffId   = 1;
    private int _nextPartnerId = 1;

    public DatabaseService()
    {
        _config = Plugin.PluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
    }

    public PluginConfig GetConfig() => _config;

    public void SaveConfig(PluginConfig config)
    {
        _config = config;
        Plugin.PluginInterface.SavePluginConfig(config);
    }

    public List<Performance> GetAllPerformances() =>
        _performances.OrderBy(p => p.Day).ThenBy(p => p.StartTime).ToList();

    public List<Performance> GetPerformancesForDay(int day) =>
        _performances.Where(p => p.Day == day).OrderBy(p => p.StartTime).ToList();

    public Performance? GetLivePerformance() =>
        _performances.FirstOrDefault(p => p.IsLive);

    public void AddPerformance(Performance p)
    {
        p.Id = _nextPerfId++;
        _performances.Add(p);
    }

    public void UpdatePerformance(Performance p)
    {
        var idx = _performances.FindIndex(x => x.Id == p.Id);
        if (idx >= 0) _performances[idx] = p;
    }

    public void DeletePerformance(int id) =>
        _performances.RemoveAll(p => p.Id == id);

    public void SetLivePerformance(int id)
    {
        foreach (var p in _performances) p.IsLive = false;
        if (id > 0)
        {
            var p = _performances.FirstOrDefault(x => x.Id == id);
            if (p != null) p.IsLive = true;
        }
    }

    public void PatchLogoPathsIfNeeded(string logoDir)
    {
        var patches = new (string name, string file)[]
        {
            ("Khyddin",      "Khyddin.png"),
            ("Mochiii",      "Mochiii.png"),
            ("Devil Cyborg", "Devil Cyborg.png"),
            ("Khangomon",    "Khangomon.png"),
            ("8bit",         "8bit.png"),
            ("Cynaxia",      "Cynaxia.png"),
            ("Raindrop",     "raindrop.png"),
            ("Skytex",       "skytex.png"),
            ("Swayywave",    "SwayWave.png"),
            ("Rin",          "Rin.png"),
            ("Leysy",        "Leysy.png"),
            ("Elana",        "Elana.png"),
            ("Swordmaid",    "Swordmaid.png"),
            ("Gaia",         "Gaia.png"),
            ("Miyu Fhey",    "Miyu Fhey.png"),
            ("Aemilia",      "Aemilia.png"),
            ("Swage",        "swage.png"),
            ("Schoki",       "Schoki.png"),
        };

        foreach (var (name, file) in patches)
        {
            var p = _performances.FirstOrDefault(x => x.DjName == name);
            if (p != null) p.LogoPath = Path.Combine(logoDir, file);
        }
    }

    public void SeedDay1IfEmpty(string logoDir)
    {
        if (GetPerformancesForDay(1).Count > 0) return;

        var day1 = new (string name, string start, string end, string twitch, string logo)[]
        {
            ("Khyddin",      "17:00", "18:00", "khyddin",       "Khyddin.png"),
            ("Mochiii",      "18:00", "19:00", "dj_mochiii",    "Mochiii.png"),
            ("Devil Cyborg", "19:00", "20:00", "devil_cyborg",  "Devil Cyborg.png"),
            ("Khangomon",    "20:00", "21:00", "khangomon",     "Khangomon.png"),
            ("8bit",         "21:00", "22:00", "8bitdoll",      "8bit.png"),
            ("Cynaxia",      "22:00", "23:00", "cynaxia",       "Cynaxia.png"),
            ("Raindrop",     "23:00", "00:00", "raindropkitty", "raindrop.png"),
            ("Skytex",       "00:00", "01:00", "skytex_vt",     "skytex.png"),
            ("Swayywave",    "01:00", "02:00", "swayywave",     "SwayWave.png"),
        };

        foreach (var (name, start, end, twitch, logo) in day1)
            AddPerformance(new Performance
            {
                DjName      = name,
                Day         = 1,
                StartTime   = start,
                EndTime     = end,
                TwitchLogin = twitch,
                StreamUrl   = $"https://www.twitch.tv/{twitch}",
                LogoPath    = Path.Combine(logoDir, logo),
            });
    }

    public void SeedDay2IfEmpty(string logoDir)
    {
        if (GetPerformancesForDay(2).Count > 0) return;

        var day2 = new (string name, string start, string end, string twitch, string logo)[]
        {
            ("Rin",       "17:00", "18:00", "rin_tsukii",   "Rin.png"),
            ("Leysy",     "18:00", "19:00", "leysy_a",      "Leysy.png"),
            ("Elana",     "19:00", "20:00", "elanaastaria", "Elana.png"),
            ("Swordmaid", "20:00", "21:00", "swordmaid",    "Swordmaid.png"),
            ("Gaia",      "21:00", "22:00", "dj_gaia",      "Gaia.png"),
            ("Miyu Fhey", "22:00", "23:00", "miyu_fhey",    "Miyu Fhey.png"),
            ("Aemilia",   "23:00", "00:00", "aemilia",      "Aemilia.png"),
            ("Swage",     "00:00", "01:00", "swage",        "swage.png"),
            ("Schoki",    "01:00", "02:00", "djschoki",     "Schoki.png"),
        };

        foreach (var (name, start, end, twitch, logo) in day2)
            AddPerformance(new Performance
            {
                DjName      = name,
                Day         = 2,
                StartTime   = start,
                EndTime     = end,
                TwitchLogin = twitch,
                StreamUrl   = $"https://www.twitch.tv/{twitch}",
                LogoPath    = Path.Combine(logoDir, logo),
            });
    }

    public List<Activity> GetAllActivities() =>
        _activities.OrderBy(a => a.Day).ThenBy(a => a.StartTime).ToList();

    public List<Activity> GetActivitiesForDay(int day) =>
        _activities.Where(a => a.Day == day).OrderBy(a => a.StartTime).ToList();

    public void AddActivity(Activity a)
    {
        a.Id = _nextActId++;
        _activities.Add(a);
    }

    public void UpdateActivity(Activity a)
    {
        var idx = _activities.FindIndex(x => x.Id == a.Id);
        if (idx >= 0) _activities[idx] = a;
    }

    public void DeleteActivity(int id) =>
        _activities.RemoveAll(a => a.Id == id);

    public List<StaffMember> GetAllStaff() =>
        _staff.OrderBy(s => s.Role).ThenBy(s => s.CharacterName).ToList();

    public void AddStaff(StaffMember s)
    {
        s.Id = _nextStaffId++;
        _staff.Add(s);
    }

    public void UpdateStaff(StaffMember s)
    {
        var idx = _staff.FindIndex(x => x.Id == s.Id);
        if (idx >= 0) _staff[idx] = s;
    }

    public void DeleteStaff(int id) =>
        _staff.RemoveAll(s => s.Id == id);

    public void UpsertStaffFromApi(List<StaffMember> apiStaff)
    {
        _staff.Clear();
        _nextStaffId = 1;
        foreach (var s in apiStaff) AddStaff(s);
    }

    public List<Partner> GetAllPartners() =>
        _partners.OrderBy(p => p.Id).ToList();

    public void SeedPartnersIfEmpty(string partnerDir)
    {
        if (_partners.Count > 0) return;

        var partners = new (string name, string logo, string discord)[]
        {
            ("ENVY",                    "ENVY.png",                    "https://discord.com/invite/envyxiv"),
            ("Urban",                   "URBAN.png",                   "https://discord.com/invite/urbanclub"),
            ("Project XIV",             "Project XIV.png",             "https://discord.gg/projectxiv"),
            ("Nocturn",                 "Nocturn.png",                 "https://discord.gg/nocturn"),
            ("Phoenix Nights",          "Phoenix Nights.png",          "https://discord.com/invite/pnx"),
            ("Clubbers LS",             "Clubbers LS.png",             "https://discord.com/invite/Kqru8e4kwp"),
            ("Habitat",                 "Habitat.png",                 "https://discord.com/invite/habitatxiv"),
            ("Gipsy Cat",               "Gipsy Cat.png",               "https://discord.com/invite/6wD8bt2jCT"),
            ("Desert Rose",             "Desert Rose.png",             "https://discord.com/invite/pgQ9Dp5GqM"),
            ("Moonlit Kiss",            "Moonlit Kiss.png",            "https://discord.com/invite/moonlitkissclub"),
            ("Selune",                  "Selune.png",                  "https://discord.com/invite/selunenightclub"),
            ("Prism",                   "Prism.png",                   "https://discord.com/invite/prismffxiv"),
            ("Tempest",                 "Tempest.png",                 "https://discord.com/invite/zqp6HUknPy"),
            ("Woah!",                   "Woah.png",                    "https://discord.com/invite/djgaia"),
            ("Black Sapphire",          "Black Sapphire.png",          "https://discord.com/invite/vuD5DF266j"),
            ("Ignite",                  "Ignite.png",                  "https://discord.com/invite/2fTgzvbNHW"),
            ("Eden",                    "Eden.png",                    "https://discord.com/invite/x3U2j4NcJA"),
            ("The Beat",                "The Beat.png",                "https://discord.com/invite/NVKwWnSVwW"),
            ("Nedori",                  "Nedori.png",                  "https://discord.com/invite/VKePWVCRCS"),
            ("Church of Metal",         "Church of Metal.png",         "https://discord.com/invite/jKRVUHVb6j"),
            ("Psyko NightClub",         "Psyko NightClub.png",         "https://discord.com/invite/kENPZPHnuE"),
            ("L'Atelier des Saveurs",   "L'Atelier des Saveurs.png",   "https://discord.com/invite/ADSaveurs"),
        };

        foreach (var (name, logo, discord) in partners)
        {
            var p = new Partner
            {
                Id         = _nextPartnerId++,
                Name       = name,
                LogoPath   = Path.Combine(partnerDir, logo),
                DiscordUrl = discord,
            };
            _partners.Add(p);
        }
    }

    public void ResetAllData()
    {
        _performances.Clear();
        _activities.Clear();
        _staff.Clear();
        _nextPerfId  = 1;
        _nextActId   = 1;
        _nextStaffId = 1;
    }

    public void Dispose() { }
}
