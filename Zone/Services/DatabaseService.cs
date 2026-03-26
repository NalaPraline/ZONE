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
        _performances.OrderBy(p => p.Day).ThenBy(p => EventMinutes(p.StartTime)).ToList();

    private static int EventMinutes(string? t)
    {
        if (t == null) return 0;
        var parts = t.Split(':');
        if (parts.Length != 2) return 0;
        int h = int.Parse(parts[0]), m = int.Parse(parts[1]);
        return (h < 6 ? h + 24 : h) * 60 + m;
    }

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

    public void SeedActivitiesIfEmpty()
    {
        if (_activities.Count > 0) return;

        // Friday
        AddActivity(new Activity { Name = "First Bingo",        Description = "Don't you just stop focusing. One number changes everything.", PotAmount = "BOOSTED POT 50,000,000 Gil", Host = "Nala Praline",              Day = 1, StartTime = "18:00 - 20:00", LocationName = "Nocturn",        MapZone = "GAMES",     PartnerId = 4,  Details = "Don't you just stop focusing.\nOne number changes everything.\n\nPOT\n50,000,000 Gil\n\nTICKETS\nUp to 8 tickets per game\n250,000 Gil per ticket" });
        AddActivity(new Activity { Name = "Second Bingo",       Description = "Eyes down. Don't miss it.",                                        PotAmount = "BOOSTED POT 50,000,000 Gil", Host = "Neru Yurei",               Day = 1, StartTime = "21:00 - 23:00", LocationName = "Selune",          MapZone = "GAMES",     PartnerId = 11, Details = "Eyes down. Don't miss it.\nCome on just hold your wits.\n\nPOT\n50,000,000 Gil\n\nTICKETS\nUp to 12 tickets per game\n250,000 Gil per ticket\n3,000,000 Gil for a full set" });
        AddActivity(new Activity { Name = "Wheel of Fortune",   Description = "Spin, and spin, and spin the wheel again.",                         PotAmount = "100,000 GIL PER SPIN",        Host = "Schoki Nekono",            Day = 1, StartTime = "17:00 - 01:00", LocationName = "The Beat",        MapZone = "GAMES",     PartnerId = 18, Details = "Spin, and spin, and spin the wheel again.\nMay luck perchance smile upon your gain.\n\nPRICE PER SPIN\n100,000 Gil\n\nPRIZES\n100,000 Gil\n2,000,000 Gil\n3,000,000 Gil\nMinions\nChocolates" });
        AddActivity(new Activity { Name = "Blackjack",          Description = "Hit or stand. No in-between.",                                      Host = "Lindhard Foreste, Sakuya Haibara",  Day = 1, StartTime = "18:00 - 00:00", LocationName = "Project XIV",     MapZone = "BLACKJACK", PartnerId = 3,  Details = "Hit or stand. No in-between.\nMake the call and pull a queen.\n\nHOW TO PLAY\nBeat the dealer without going over 21.\nFace cards are worth 10. Aces count as 1 or 11.\nHit to draw another card. Stand to hold your hand.\nGo over 21 and you bust.\n\nNOTE\nRules may vary depending on the dealer." });
        AddActivity(new Activity { Name = "Poker",              Description = "Texas Hold'em. Think you can take the pot?",                        PotAmount = "BOOSTED POT 100,000,000 Gil", Host = "Enzo Valentine",           Day = 1, StartTime = "19:30 - 01:00",          PartnerId = 3, ContactUrl = "https://discordid.netlify.app/?id=1287676375133720596", Details = "To enter, message enzoxiv on Discord at least 1 hour before start (19:30 ST).\n\nRULESET\nTexas Hold'em\nMax 30 spots across 3 full tables (first come, first served).\nBlinds increase every 20 minutes of live play. No antes.\n\nPRIZES\n1st Place: 65% of overall pot\n2nd Place: 25% of overall pot\n3rd Place: 10% of overall pot\n\nPOT\n100,000,000 Gil boosted\n2,500,000 Gil buy-in" });
        AddActivity(new Activity { Name = "Shots",              Description = "One shot, one roll. Hit a double or triple to win.",                PotAmount = "BASE POT 10,000,000 Gil",    Host = "Remy Mochi",               Day = 1, StartTime = "19:30 - 23:30", LocationName = "Habitat",         MapZone = "ROAMING",   PartnerId = 7,  Details = "SHOTS AVAILABLE\nGin  /  Vodka  /  Rum\n\nPRICING\n100,000 Gil per shot\nUp to 15 shots per person\n100% of each shot added to the pot\n\nHOW TO WIN\nBuy a shot and roll /random.\nHit a Double or Triple (11, 22... 111, 222... 999) to win.\nShots are sold by roaming staff, not at the bar.\n\nRESETS\nIf the pot is won, it resets to 1,000,000 Gil.\nMaximum 5 resets.\n\nCLOSURE RULE\nIf unclaimed at closure, the pot is added to the Raffle pot.\nThe final raffle roll decides the winner of the combined prize." });
        AddActivity(new Activity { Name = "Raffle",             Description = "One ticket. One winner. Fifty million Gil.",                        PotAmount = "BASE POT 50,000,000 Gil",    Host = "Kazu Mochi",               Day = 1, StartTime = "19:30 - 23:30", LocationName = "Habitat",         MapZone = "ROAMING",   PartnerId = 7,  Details = "BASE POT\n50,000,000 Gil\n\nPRICING\n100,000 Gil per ticket\nUp to 30 tickets per person\n100% added to the pot\n\nBONUS\nBuy 30 tickets and receive 1 free Shot roll.\n\nCLOSURE RULE\nWinner announced between 23:30 - 23:45 ST.\nIf the Shot pot is unclaimed at closure, it is added to the Raffle pot.\nThe final Raffle roll decides the winner of the combined prize." });
        AddActivity(new Activity { Name = "Scratch Cards",      Description = "Scratch your way to a prize.",                                       Host = "Nala Praline",             Day = 1, StartTime = "21:00 - 01:00", LocationName = "Nocturn",         MapZone = "GAMES",     PartnerId = 4,  Details = "HOW TO PLAY\nBuy a scratch card and reveal your prizes.\nScratch to try to win!\n\nTICKETS\nUp to 8 tickets per person\n250,000 Gil per ticket\n2,000,000 Gil for a full set of 8\n\nPRIZES\n200,000 Gil\n500,000 Gil\n5,000,000 Gil\nMinion\nMount\nNala Hug" });
        AddActivity(new Activity { Name = "Three-Card Monte",  Description = "The Queen is hidden. Can you find her?",                             Host = "Gipsy Cat",                Day = 1, StartTime = "20:00 - 01:00", LocationName = "Gipsy Cat",       PartnerId = 8, Details = "Like the classical Three-Card Monte, the dealer shuffles three cards, hiding the Queen. Can you find her?\n\nFIRST VERSION\nChoose card 1, 2, or 3\nThe dealer rolls a /dice: 1 = card 1 / 2 = card 2 / 3 = card 3\nIf your choice matches the Queen, you win a prize.\nIf not, you leave empty-handed... but with a free pat!\n\nSECOND VERSION\nThe same game, but with stakes.\nPlace a bet before choosing your card.\nThe dealer rolls a /dice: 1-2 = card 1 / 3-4 = card 2 / 5 = card 3\n\nRIGHT GUESS\nCard 1 or 2: gain your bet +50%\nCard 3: double your bet\n\nBoth versions can be played solo or with multiple players!" });
        AddActivity(new Activity { Name = "Dice Challenge",    Description = "Higher, lower, or equal. First to 5 wins.",                            Host = "Gipsy Cat",                Day = 1, StartTime = "20:00 - 01:00", LocationName = "Gipsy Cat",       PartnerId = 8, Details = "A game of higher or lower with a twist!\nThe dealer rolls a /dice 12. Players take turns predicting the next roll.\nAnnounce higher, lower, or equal, then roll a /dice 12.\n\nOUTCOMES\nCorrect higher/lower: +1 point\nWrong higher/lower: no change\nEqual (exact same): +2 points\nEqual (miss): -1 point\n\nVICTORY\nFirst to reach 5 points wins." });
        AddActivity(new Activity { Name = "Arm Wrestling",     Description = "A Deathroll-based arm wrestling showdown. Defeat the host to win.",    Host = "Gipsy Cat",                Day = 1, StartTime = "20:00 - 01:00", LocationName = "Gipsy Cat",       PartnerId = 8, Details = "A Deathroll-based arm wrestling game!\nA 1v1 mini-game representing the moment a participant falters.\n\nHOW TO PLAY\nStart with a /dice 500.\nThe first to hit 1 loses.\n\nPRIZE\nDefeat the host and earn a prize!" });

        // Saturday
        AddActivity(new Activity { Name = "Third Bingo",        Description = "Did someone yell bingo or was it your imagination?",               PotAmount = "BOOSTED POT 50,000,000 Gil", Host = "Bean Buns",                Day = 2, StartTime = "18:00 - 20:00", LocationName = "Phoenix Nights",  MapZone = "GAMES",     PartnerId = 5,  Details = "Did someone yell bingo or was it your imagination?\nYou cannot tell in your winning wishes affliction.\n\nPOT\n50,000,000 Gil\n\nTICKETS\nUp to 8 tickets per game\n250,000 Gil per ticket" });
        AddActivity(new Activity { Name = "Fourth Bingo",       Description = "There at bay, in the gold den.",                                   PotAmount = "BOOSTED POT 50,000,000 Gil", Host = "Reika Maya, Astrin Maye + 1", Day = 2, StartTime = "21:00 - 23:00", LocationName = "Project XIV",   MapZone = "GAMES",     PartnerId = 3,  Details = "There at bay, in the gold den.\nMine to claim, the prize golden.\n\nPOT\n50,000,000 Gil\n\nTICKETS\nUp to 8 tickets per game\n250,000 Gil per ticket" });
        AddActivity(new Activity { Name = "Wheel of Fortune",   Description = "Ah, shit, here we go again.",                                       PotAmount = "100,000 GIL PER SPIN",        Host = "Schoki Nekono",            Day = 2, StartTime = "17:00 - 22:30", LocationName = "The Beat",        MapZone = "GAMES",     PartnerId = 18, Details = "Ah, shit, here we go again.\nThe doom's day wheel of gain.\n\nPRICE PER SPIN\n100,000 Gil\n\nPRIZES\n100,000 Gil\n2,000,000 Gil\n3,000,000 Gil\nMinions\nChocolates" });
        AddActivity(new Activity { Name = "Blackjack",          Description = "Hit, stand, and double-down.",                                      Host = "Ellie Gator, Steffomatus Torus",   Day = 2, StartTime = "18:00 - 00:00", LocationName = "Phoenix Nights",  MapZone = "BLACKJACK", PartnerId = 5,  Details = "Hit, stand, and double-down.\nMake the call, retrieve your crown.\n\nHOW TO PLAY\nBeat the dealer without going over 21.\nFace cards are worth 10. Aces count as 1 or 11.\nHit to draw another card. Stand to hold your hand.\nGo over 21 and you bust.\n\nNOTE\nRules may vary depending on the dealer." });
        AddActivity(new Activity { Name = "Scratch Cards",      Description = "Scratch your way to a prize.",                                       Host = "Ellie Gator, Nala Praline",        Day = 2, StartTime = "18:00 - 01:00", LocationName = "PNX & Nocturn",   MapZone = "GAMES",     PartnerId = 4,  Details = "HOW TO PLAY\nBuy a scratch card and reveal your prizes.\nScratch to try to win!\n\nTICKETS\nUp to 8 tickets per person\n250,000 Gil per ticket\n2,000,000 Gil for a full set of 8\n\nPRIZES\n200,000 Gil\n500,000 Gil\n5,000,000 Gil\nMinion\nMount\nNala Hug" });
        AddActivity(new Activity { Name = "Three-Card Monte",  Description = "The Queen is hidden. Can you find her?",                             Host = "Gipsy Cat",                Day = 2, StartTime = "20:00 - 01:00", LocationName = "Gipsy Cat",       PartnerId = 8, Details = "Like the classical Three-Card Monte, the dealer shuffles three cards, hiding the Queen. Can you find her?\n\nFIRST VERSION\nChoose card 1, 2, or 3\nThe dealer rolls a /dice: 1 = card 1 / 2 = card 2 / 3 = card 3\nIf your choice matches the Queen, you win a prize.\nIf not, you leave empty-handed... but with a free pat!\n\nSECOND VERSION\nThe same game, but with stakes.\nPlace a bet before choosing your card.\nThe dealer rolls a /dice: 1-2 = card 1 / 3-4 = card 2 / 5 = card 3\n\nRIGHT GUESS\nCard 1 or 2: gain your bet +50%\nCard 3: double your bet\n\nBoth versions can be played solo or with multiple players!" });
        AddActivity(new Activity { Name = "Dice Challenge",    Description = "Higher, lower, or equal. First to 5 wins.",                            Host = "Gipsy Cat",                Day = 2, StartTime = "20:00 - 01:00", LocationName = "Gipsy Cat",       PartnerId = 8, Details = "A game of higher or lower with a twist!\nThe dealer rolls a /dice 12. Players take turns predicting the next roll.\nAnnounce higher, lower, or equal, then roll a /dice 12.\n\nOUTCOMES\nCorrect higher/lower: +1 point\nWrong higher/lower: no change\nEqual (exact same): +2 points\nEqual (miss): -1 point\n\nVICTORY\nFirst to reach 5 points wins." });
        AddActivity(new Activity { Name = "Arm Wrestling",     Description = "A Deathroll-based arm wrestling showdown. Defeat the host to win.",    Host = "Gipsy Cat",                Day = 2, StartTime = "20:00 - 01:00", LocationName = "Gipsy Cat",       PartnerId = 8, Details = "A Deathroll-based arm wrestling game!\nA 1v1 mini-game representing the moment a participant falters.\n\nHOW TO PLAY\nStart with a /dice 500.\nThe first to hit 1 loses.\n\nPRIZE\nDefeat the host and earn a prize!" });
        AddActivity(new Activity { Name = "Deathroll Tournament", Description = "First to hit 1 is out. Last one standing wins.",                  Host = "Rin Tsukii",                       Day = 2, StartTime = "21:00 - 01:00",          LocationName = "Ignite",          PartnerId = 16, Details = "PRIZES\n1st Place: Dais of Darkness\n2nd Place: Genie of the Lamp\n\nMAX SPOTS\n32 players\n\nHOW TO ENTER\nSign up on the Ignite Discord between 18:00 - 21:00 ST.\nBe present at the designated place at 21:00 ST.\n\nRULES\nSingle-elimination format.\nEach duel starts with /dice 500.\nFirst player to hit 1 loses.\nTurns are announced in-game." });
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
