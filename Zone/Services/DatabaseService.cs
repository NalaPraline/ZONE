using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Zone.Models;

namespace Zone.Services;

public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private PluginConfig? _configCache;

    private const string DefaultPassword = "zone2025";

    public DatabaseService()
    {
        var dir = Plugin.PluginInterface.ConfigDirectory.FullName;
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "zone.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void InitializeDatabase()
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Performances (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DjName TEXT NOT NULL,
                    Day INTEGER NOT NULL,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT NOT NULL,
                    StreamUrl TEXT,
                    IsLive INTEGER DEFAULT 0,
                    AvatarPath TEXT
                );

                CREATE TABLE IF NOT EXISTS Activities (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    Day INTEGER NOT NULL,
                    StartTime TEXT NOT NULL,
                    LocationName TEXT,
                    TerritoryId INTEGER,
                    CoordinateX REAL,
                    CoordinateY REAL,
                    CoordinateZ REAL,
                    StreamUrl TEXT
                );

                CREATE TABLE IF NOT EXISTS Staff (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CharacterName TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    Description TEXT,
                    AvatarPath TEXT,
                    DiscordTag TEXT,
                    TwitchUrl TEXT,
                    TwitterHandle TEXT,
                    IsOnline INTEGER DEFAULT 0,
                    ContentId INTEGER DEFAULT 0,
                    World TEXT,
                    Color TEXT
                );

                CREATE TABLE IF NOT EXISTS Partners (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name        TEXT NOT NULL UNIQUE,
                    Description TEXT,
                    LogoPath    TEXT,
                    DiscordUrl  TEXT,
                    TwitchUrl   TEXT,
                    WebsiteUrl  TEXT
                );

                CREATE TABLE IF NOT EXISTS Config (
                    Id INTEGER PRIMARY KEY DEFAULT 1,
                    ZoneVisionEnabled INTEGER DEFAULT 0,
                    TimeLockEnabled INTEGER DEFAULT 0,
                    NotificationsEnabled INTEGER DEFAULT 1,
                    LastSeenDjId INTEGER DEFAULT 0,
                    AdminPasswordHash TEXT DEFAULT '',
                    StaffApiUrl TEXT DEFAULT ''
                );

                INSERT OR IGNORE INTO Config (Id) VALUES (1);
            ";
            cmd.ExecuteNonQuery();

            // Set default admin password if not set
            var config = ReadConfigFromDb(conn);
            if (string.IsNullOrEmpty(config.AdminPasswordHash))
            {
                config.AdminPasswordHash = HashPassword(DefaultPassword);
                WriteConfigToDb(conn, config);
            }
            _configCache = config;

            // Migrations for older installs
            RunMigration(conn, "ALTER TABLE Staff ADD COLUMN ContentId INTEGER DEFAULT 0");
            RunMigration(conn, "ALTER TABLE Staff ADD COLUMN World TEXT");
            RunMigration(conn, "ALTER TABLE Staff ADD COLUMN Color TEXT");
            RunMigration(conn, "ALTER TABLE Performances ADD COLUMN TwitchLogin TEXT");
            RunMigration(conn, "ALTER TABLE Performances ADD COLUMN LogoPath TEXT");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Zone] Failed to initialize database");
        }
    }

    private static void RunMigration(SqliteConnection conn, string sql)
    {
        try { using var c = conn.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }
        catch { /* column already exists, skip */ }
    }

    public PluginConfig GetConfig()
    {
        if (_configCache != null) return _configCache;
        try
        {
            using var conn = OpenConnection();
            _configCache = ReadConfigFromDb(conn);
            return _configCache;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Zone] Failed to read config");
            return new PluginConfig();
        }
    }

    public void SaveConfig(PluginConfig config)
    {
        _configCache = config;
        try
        {
            using var conn = OpenConnection();
            WriteConfigToDb(conn, config);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Zone] Failed to save config");
        }
    }

    private static PluginConfig ReadConfigFromDb(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ZoneVisionEnabled, TimeLockEnabled, NotificationsEnabled, LastSeenDjId, AdminPasswordHash, StaffApiUrl FROM Config WHERE Id = 1";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new PluginConfig
            {
                ZoneVisionEnabled = reader.GetInt32(0) == 1,
                TimeLockEnabled = reader.GetInt32(1) == 1,
                NotificationsEnabled = reader.GetInt32(2) == 1,
                LastSeenDjId = reader.GetInt32(3),
                AdminPasswordHash = reader.IsDBNull(4) ? "" : reader.GetString(4),
                StaffApiUrl = reader.IsDBNull(5) ? "" : reader.GetString(5)
            };
        }
        return new PluginConfig();
    }

    private static void WriteConfigToDb(SqliteConnection conn, PluginConfig config)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE Config SET
                ZoneVisionEnabled = @zv,
                TimeLockEnabled = @tl,
                NotificationsEnabled = @notif,
                LastSeenDjId = @lastDj,
                AdminPasswordHash = @pass,
                StaffApiUrl = @apiUrl
            WHERE Id = 1";
        cmd.Parameters.AddWithValue("@zv", config.ZoneVisionEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@tl", config.TimeLockEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@notif", config.NotificationsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@lastDj", config.LastSeenDjId);
        cmd.Parameters.AddWithValue("@pass", config.AdminPasswordHash);
        cmd.Parameters.AddWithValue("@apiUrl", config.StaffApiUrl ?? "");
        cmd.ExecuteNonQuery();
    }

    public List<Performance> GetAllPerformances()
    {
        var list = new List<Performance>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, DjName, Day, StartTime, EndTime, StreamUrl, IsLive, AvatarPath, TwitchLogin, LogoPath FROM Performances ORDER BY Day, StartTime";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadPerformance(r));
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetAllPerformances failed"); }
        return list;
    }

    public List<Performance> GetPerformancesForDay(int day)
    {
        var list = new List<Performance>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, DjName, Day, StartTime, EndTime, StreamUrl, IsLive, AvatarPath, TwitchLogin, LogoPath FROM Performances WHERE Day = @day ORDER BY StartTime";
            cmd.Parameters.AddWithValue("@day", day);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadPerformance(r));
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetPerformancesForDay failed"); }
        return list;
    }

    public Performance? GetLivePerformance()
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, DjName, Day, StartTime, EndTime, StreamUrl, IsLive, AvatarPath, TwitchLogin, LogoPath FROM Performances WHERE IsLive = 1 LIMIT 1";
            using var r = cmd.ExecuteReader();
            if (r.Read()) return ReadPerformance(r);
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetLivePerformance failed"); }
        return null;
    }

    public void AddPerformance(Performance p)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Performances (DjName, Day, StartTime, EndTime, StreamUrl, IsLive, AvatarPath, TwitchLogin, LogoPath) VALUES (@n,@d,@s,@e,@url,@live,@av,@tw,@logo)";
            cmd.Parameters.AddWithValue("@n", p.DjName);
            cmd.Parameters.AddWithValue("@d", p.Day);
            cmd.Parameters.AddWithValue("@s", p.StartTime);
            cmd.Parameters.AddWithValue("@e", p.EndTime);
            cmd.Parameters.AddWithValue("@url", p.StreamUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@live", p.IsLive ? 1 : 0);
            cmd.Parameters.AddWithValue("@av", p.AvatarPath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tw", p.TwitchLogin ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@logo", p.LogoPath ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] AddPerformance failed"); }
    }

    public void UpdatePerformance(Performance p)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Performances SET DjName=@n, Day=@d, StartTime=@s, EndTime=@e, StreamUrl=@url, IsLive=@live, AvatarPath=@av, TwitchLogin=@tw, LogoPath=@logo WHERE Id=@id";
            cmd.Parameters.AddWithValue("@n", p.DjName);
            cmd.Parameters.AddWithValue("@d", p.Day);
            cmd.Parameters.AddWithValue("@s", p.StartTime);
            cmd.Parameters.AddWithValue("@e", p.EndTime);
            cmd.Parameters.AddWithValue("@url", p.StreamUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@live", p.IsLive ? 1 : 0);
            cmd.Parameters.AddWithValue("@av", p.AvatarPath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@tw", p.TwitchLogin ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@logo", p.LogoPath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@id", p.Id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] UpdatePerformance failed"); }
    }

    public void DeletePerformance(int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Performances WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] DeletePerformance failed"); }
    }

    public void SetLivePerformance(int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Performances SET IsLive = 0";
            cmd.ExecuteNonQuery();
            if (id > 0)
            {
                cmd.CommandText = "UPDATE Performances SET IsLive = 1 WHERE Id = @id";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] SetLivePerformance failed"); }
    }

    private static Performance ReadPerformance(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        DjName = r.GetString(1),
        Day = r.GetInt32(2),
        StartTime = r.GetString(3),
        EndTime = r.GetString(4),
        StreamUrl = r.IsDBNull(5) ? null : r.GetString(5),
        IsLive = r.GetInt32(6) == 1,
        AvatarPath = r.IsDBNull(7) ? null : r.GetString(7),
        TwitchLogin = r.IsDBNull(8) ? null : r.GetString(8),
        LogoPath = r.IsDBNull(9) ? null : r.GetString(9)
    };

    public void PatchLogoPathsIfNeeded(string logoDir)
    {
        var patches = new (string name, string file)[]
        {
            // Day 1
            ("Khyddin",      "Khyddin.png"),
            ("Mochiii",      "Mochiii.png"),
            ("Devil Cyborg", "Devil Cyborg.png"),
            ("Khangomon",    "Khangomon.png"),
            ("8bit",         "8bit.png"),
            ("Cynaxia",      "Cynaxia.png"),
            ("Raindrop",     "raindrop.png"),
            ("Skytex",       "skytex.png"),
            ("Swayywave",    "SwayWave.png"),
            // Day 2
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

        try
        {
            using var conn = OpenConnection();
            foreach (var (name, file) in patches)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Performances SET LogoPath = @logo WHERE DjName = @name";
                cmd.Parameters.AddWithValue("@logo", Path.Combine(logoDir, file));
                cmd.Parameters.AddWithValue("@name", name);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] PatchLogoPathsIfNeeded failed"); }
    }

    public void SeedDay1IfEmpty(string logoDir)
    {
        if (GetPerformancesForDay(1).Count > 0) return;

        var day1 = new (string name, string start, string end, string twitch, string? logo)[]
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
        {
            AddPerformance(new Performance
            {
                DjName      = name,
                Day         = 1,
                StartTime   = start,
                EndTime     = end,
                TwitchLogin = twitch,
                StreamUrl   = $"https://www.twitch.tv/{twitch}",
                LogoPath    = logo != null ? Path.Combine(logoDir, logo) : null,
            });
        }
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
        {
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
    }

    public List<Activity> GetAllActivities()
    {
        var list = new List<Activity>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id,Name,Description,Day,StartTime,LocationName,TerritoryId,CoordinateX,CoordinateY,CoordinateZ,StreamUrl FROM Activities ORDER BY Day, StartTime";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadActivity(r));
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetAllActivities failed"); }
        return list;
    }

    public List<Activity> GetActivitiesForDay(int day)
    {
        var list = new List<Activity>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id,Name,Description,Day,StartTime,LocationName,TerritoryId,CoordinateX,CoordinateY,CoordinateZ,StreamUrl FROM Activities WHERE Day = @day ORDER BY StartTime";
            cmd.Parameters.AddWithValue("@day", day);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadActivity(r));
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetActivitiesForDay failed"); }
        return list;
    }

    public void AddActivity(Activity a)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Activities (Name,Description,Day,StartTime,LocationName,TerritoryId,CoordinateX,CoordinateY,CoordinateZ,StreamUrl) VALUES (@n,@desc,@d,@s,@loc,@tid,@x,@y,@z,@url)";
            BindActivity(cmd, a);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] AddActivity failed"); }
    }

    public void UpdateActivity(Activity a)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Activities SET Name=@n,Description=@desc,Day=@d,StartTime=@s,LocationName=@loc,TerritoryId=@tid,CoordinateX=@x,CoordinateY=@y,CoordinateZ=@z,StreamUrl=@url WHERE Id=@id";
            BindActivity(cmd, a);
            cmd.Parameters.AddWithValue("@id", a.Id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] UpdateActivity failed"); }
    }

    public void DeleteActivity(int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Activities WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] DeleteActivity failed"); }
    }

    private static void BindActivity(SqliteCommand cmd, Activity a)
    {
        cmd.Parameters.AddWithValue("@n", a.Name);
        cmd.Parameters.AddWithValue("@desc", a.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@d", a.Day);
        cmd.Parameters.AddWithValue("@s", a.StartTime);
        cmd.Parameters.AddWithValue("@loc", a.LocationName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@tid", a.TerritoryId.HasValue ? a.TerritoryId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@x", a.CoordinateX.HasValue ? a.CoordinateX.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@y", a.CoordinateY.HasValue ? a.CoordinateY.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@z", a.CoordinateZ.HasValue ? a.CoordinateZ.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@url", a.StreamUrl ?? (object)DBNull.Value);
    }

    private static Activity ReadActivity(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Name = r.GetString(1),
        Description = r.IsDBNull(2) ? null : r.GetString(2),
        Day = r.GetInt32(3),
        StartTime = r.GetString(4),
        LocationName = r.IsDBNull(5) ? null : r.GetString(5),
        TerritoryId = r.IsDBNull(6) ? null : r.GetInt32(6),
        CoordinateX = r.IsDBNull(7) ? null : (float)r.GetDouble(7),
        CoordinateY = r.IsDBNull(8) ? null : (float)r.GetDouble(8),
        CoordinateZ = r.IsDBNull(9) ? null : (float)r.GetDouble(9),
        StreamUrl = r.IsDBNull(10) ? null : r.GetString(10)
    };

    public List<StaffMember> GetAllStaff()
    {
        var list = new List<StaffMember>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id,CharacterName,Role,Description,AvatarPath,DiscordTag,TwitchUrl,TwitterHandle,IsOnline,ContentId,World,Color FROM Staff ORDER BY Role, CharacterName";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadStaff(r));
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetAllStaff failed"); }
        return list;
    }

    public void AddStaff(StaffMember s)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Staff (CharacterName,Role,Description,AvatarPath,DiscordTag,TwitchUrl,TwitterHandle,IsOnline,ContentId,World,Color) VALUES (@cn,@r,@desc,@av,@disc,@tw,@twit,@on,@cid,@world,@color)";
            BindStaff(cmd, s);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] AddStaff failed"); }
    }

    public void UpdateStaff(StaffMember s)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Staff SET CharacterName=@cn,Role=@r,Description=@desc,AvatarPath=@av,DiscordTag=@disc,TwitchUrl=@tw,TwitterHandle=@twit,IsOnline=@on,ContentId=@cid,World=@world,Color=@color WHERE Id=@id";
            BindStaff(cmd, s);
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] UpdateStaff failed"); }
    }

    public void DeleteStaff(int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Staff WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] DeleteStaff failed"); }
    }

    public void UpsertStaffFromApi(List<StaffMember> apiStaff)
    {
        try
        {
            using var conn = OpenConnection();
            // Clear and repopulate from API data
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM Staff";
            del.ExecuteNonQuery();

            foreach (var s in apiStaff)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO Staff (CharacterName,Role,Description,AvatarPath,DiscordTag,TwitchUrl,TwitterHandle,IsOnline,ContentId,World,Color) VALUES (@cn,@r,@desc,@av,@disc,@tw,@twit,@on,@cid,@world,@color)";
                BindStaff(cmd, s);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] UpsertStaffFromApi failed"); }
    }

    private static void BindStaff(SqliteCommand cmd, StaffMember s)
    {
        cmd.Parameters.AddWithValue("@cn", s.CharacterName);
        cmd.Parameters.AddWithValue("@r", s.Role);
        cmd.Parameters.AddWithValue("@desc", s.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@av", s.AvatarPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@disc", s.DiscordTag ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@tw", s.TwitchUrl ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@twit", s.TwitterHandle ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@on", s.IsOnline ? 1 : 0);
        cmd.Parameters.AddWithValue("@cid", (long)s.ContentId);
        cmd.Parameters.AddWithValue("@world", s.World ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@color", s.Color ?? (object)DBNull.Value);
    }

    private static StaffMember ReadStaff(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        CharacterName = r.GetString(1),
        Role = r.GetString(2),
        Description = r.IsDBNull(3) ? null : r.GetString(3),
        AvatarPath = r.IsDBNull(4) ? null : r.GetString(4),
        DiscordTag = r.IsDBNull(5) ? null : r.GetString(5),
        TwitchUrl = r.IsDBNull(6) ? null : r.GetString(6),
        TwitterHandle = r.IsDBNull(7) ? null : r.GetString(7),
        IsOnline = r.GetInt32(8) == 1,
        ContentId = r.IsDBNull(9) ? 0ul : (ulong)r.GetInt64(9),
        World     = r.IsDBNull(10) ? null : r.GetString(10),
        Color     = r.IsDBNull(11) ? null : r.GetString(11)
    };

    public static string HashPassword(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();

    public static bool VerifyPassword(string input, string storedHash)
        => HashPassword(input) == storedHash;

    public void ResetAllData()
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Performances; DELETE FROM Activities; DELETE FROM Staff;";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] ResetAllData failed"); }
    }

    public List<Partner> GetAllPartners()
    {
        var list = new List<Partner>();
        try
        {
            using var conn = OpenConnection();
            using var cmd  = conn.CreateCommand();
            cmd.CommandText = "SELECT Id,Name,Description,LogoPath,DiscordUrl,TwitchUrl,WebsiteUrl FROM Partners ORDER BY Id";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(new Partner
            {
                Id          = r.GetInt32(0),
                Name        = r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                LogoPath    = r.IsDBNull(3) ? null : r.GetString(3),
                DiscordUrl  = r.IsDBNull(4) ? null : r.GetString(4),
                TwitchUrl   = r.IsDBNull(5) ? null : r.GetString(5),
                WebsiteUrl  = r.IsDBNull(6) ? null : r.GetString(6),
            });
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] GetAllPartners failed"); }
        return list;
    }

    public void SeedPartnersIfEmpty(string partnerDir)
    {
        var partners = new (string name, string logo, string discord)[]
        {
            ("ENVY",           "ENVY.png",           "https://discord.com/invite/envyxiv"),
            ("Urban",          "URBAN.png",           "https://discord.com/invite/urbanclub"),
            ("Project XIV",    "Project XIV.png",     "https://discord.gg/projectxiv"),
            ("Nocturn",        "Nocturn.png",         "https://discord.gg/nocturn"),
            ("Phoenix Nights", "Phoenix Nights.png",  "https://discord.com/invite/pnx"),
            ("Clubbers LS",    "Clubbers LS.png",     "https://discord.com/invite/Kqru8e4kwp"),
            ("Habitat",        "Habitat.png",         "https://discord.com/invite/habitatxiv"),
            ("Gipsy Cat",      "Gipsy Cat.png",       "https://discord.com/invite/6wD8bt2jCT"),
            ("Desert Rose",    "Desert Rose.png",     "https://discord.com/invite/pgQ9Dp5GqM"),
            ("Moonlit Kiss",   "Moonlit Kiss.png",    "https://discord.com/invite/moonlitkissclub"),
            ("Selune",         "Selune.png",          "https://discord.com/invite/selunenightclub"),
            ("Prism",          "Prism.png",           "https://discord.com/invite/prismffxiv"),
            ("Tempest",        "Tempest.png",         "https://discord.com/invite/zqp6HUknPy"),
            ("Woah!",          "Woah.png",            "https://discord.com/invite/djgaia"),
            ("Black Sapphire", "Black Sapphire.png",  "https://discord.com/invite/vuD5DF266j"),
            ("Ignite",         "Ignite.png",          "https://discord.com/invite/2fTgzvbNHW"),
            ("Eden",           "Eden.png",            "https://discord.com/invite/x3U2j4NcJA"),
            ("The Beat",       "The Beat.png",        "https://discord.com/invite/NVKwWnSVwW"),
            ("Nedori",         "Nedori.png",          "https://discord.com/invite/VKePWVCRCS"),
            ("Church of Metal","Church of Metal.png", "https://discord.com/invite/jKRVUHVb6j"),
            ("Psyko NightClub",        "Psyko NightClub.png",        "https://discord.com/invite/kENPZPHnuE"),
            ("L'Atelier des Saveurs",  "L'Atelier des Saveurs.png",  "https://discord.com/invite/ADSaveurs"),
        };

        try
        {
            using var conn = OpenConnection();

            using var dedup = conn.CreateCommand();
            dedup.CommandText = "DELETE FROM Partners WHERE rowid NOT IN (SELECT MIN(rowid) FROM Partners GROUP BY Name)";
            dedup.ExecuteNonQuery();

            foreach (var (name, logo, discord) in partners)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Partners (Name,LogoPath,DiscordUrl) SELECT @n,@l,@dc WHERE NOT EXISTS (SELECT 1 FROM Partners WHERE Name = @n);
                    UPDATE Partners SET LogoPath=@l, DiscordUrl=@dc WHERE Name=@n";
                cmd.Parameters.AddWithValue("@n",  name);
                cmd.Parameters.AddWithValue("@l",  Path.Combine(partnerDir, logo));
                cmd.Parameters.AddWithValue("@dc", discord);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] SeedPartnersIfEmpty failed"); }
    }

    public void Dispose() { }
}
