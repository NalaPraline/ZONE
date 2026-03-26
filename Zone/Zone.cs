using System;
using System.IO;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.Windowing;
using ECommons;
using Zone.Services;
using Zone.Windows;

namespace Zone;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager        CommandManager   { get; private set; } = null!;
    [PluginService] internal static IFramework             Framework        { get; private set; } = null!;
    [PluginService] internal static INotificationManager   Notifications    { get; private set; } = null!;
    [PluginService] internal static IGameGui               GameGui          { get; private set; } = null!;
    [PluginService] internal static IPluginLog             Log              { get; private set; } = null!;
[PluginService] internal static ITextureProvider       TextureProvider  { get; private set; } = null!;
    [PluginService] internal static ISigScanner            SigScanner       { get; private set; } = null!;
    [PluginService] internal static ITargetManager         TargetManager    { get; private set; } = null!;
    [PluginService] internal static IDataManager           DataManager      { get; private set; } = null!;
    [PluginService] internal static IObjectTable           ObjectTable      { get; private set; } = null!;
    [PluginService] internal static IClientState           ClientState      { get; private set; } = null!;

    internal static DatabaseService      Db            { get; private set; } = null!;
    internal static StaffApiService      StaffApi      { get; private set; } = null!;
    internal static ImageCacheService    ImageCache    { get; private set; } = null!;
    internal static TimeLockService      TimeLock      { get; private set; } = null!;
    internal static AnnouncementService  Announcements { get; private set; } = null!;
    internal static TwitchService        Twitch        { get; private set; } = null!;
    internal static ZoneVisionOverlay   Overlay       { get; private set; } = null!;

    private DateTime _lastStaffSync = DateTime.Now;
    private const double StaffSyncIntervalMinutes = 5.0;

    private readonly TimeLockService     _timeLock;
    private readonly NotificationService _notifications;
    private readonly WindowSystem        _windowSystem = new("Zone");
    private readonly MainWindow          _mainWindow;
    private readonly SettingsWindow      _settingsWindow;
    private readonly ZoneVisionOverlay   _overlay;
    private readonly StaffDetailPopup    _staffPopup;

    private const string CmdZone       = "/zone";
    private const string CmdZoneVision = "/zonevision";

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this);

        Db            = new DatabaseService();
        StaffApi      = new StaffApiService();
        ImageCache    = new ImageCacheService();
        Announcements = new AnnouncementService();
        Twitch        = new TwitchService();

        Db.SeedDay1IfEmpty(ImageCache.DjLogosDir);
        Db.SeedDay2IfEmpty(ImageCache.DjLogosDir);
        Db.PatchLogoPathsIfNeeded(ImageCache.DjLogosDir);
        Db.SeedPartnersIfEmpty(ImageCache.PartnersDir);
        Db.SeedActivitiesIfEmpty();
        Twitch.ResetIfNotEventDay();

        _timeLock      = new TimeLockService();
        TimeLock       = _timeLock;
        _notifications = new NotificationService();

        _staffPopup     = new StaffDetailPopup();
        _settingsWindow = new SettingsWindow();
        _mainWindow     = new MainWindow(_staffPopup, _settingsWindow);
        _overlay        = new ZoneVisionOverlay();
        Overlay         = _overlay;

        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_settingsWindow);
        _windowSystem.AddWindow(_overlay);
        _windowSystem.AddWindow(_staffPopup);

        PluginInterface.UiBuilder.Draw         += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi   += ToggleMain;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettings;

        Framework.Update            += OnFrameworkUpdate;
        ClientState.TerritoryChanged += OnTerritoryChanged;

        CommandManager.AddHandler(CmdZone, new CommandInfo(OnZoneCommand)
        {
            HelpMessage = "Open the ZONE plugin window"
        });
        CommandManager.AddHandler(CmdZoneVision, new CommandInfo(OnVisionCommand)
        {
            HelpMessage = "Toggle the Zone Vision HUD overlay"
        });

        // Init housing state for current territory (plugin may load while already inside a house)
        _timeLock.OnTerritoryChanged(ClientState.TerritoryType);

        var cfg = Db.GetConfig();
        if (cfg.ZoneVisionEnabled)
            _overlay.IsOpen = true;

        Log.Information("[Zone] Plugin loaded.");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CmdZone);
        CommandManager.RemoveHandler(CmdZoneVision);

        Framework.Update             -= OnFrameworkUpdate;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        PluginInterface.UiBuilder.Draw         -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi   -= ToggleMain;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettings;

        _windowSystem.RemoveAllWindows();
        _mainWindow.Dispose();
        _staffPopup.Dispose();
        _timeLock.Dispose();
        Announcements.Dispose();
        Twitch.Dispose();
        StaffApi.Dispose();
        Db.Dispose();

        ECommonsMain.Dispose();
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        _notifications.Update();
        Twitch.Update();

        if ((DateTime.Now - _lastStaffSync).TotalMinutes >= StaffSyncIntervalMinutes)
        {
            _lastStaffSync = DateTime.Now;
            _ = StaffApi.SyncStaffAsync();
        }
    }

    private void OnTerritoryChanged(ushort id) => _timeLock.OnTerritoryChanged(id);

    private void OnZoneCommand(string cmd, string args) => ToggleMain();

    private void OnVisionCommand(string cmd, string args)
    {
        _overlay.Toggle();
        var config = Db.GetConfig();
        config.ZoneVisionEnabled = _overlay.IsOpen;
        Db.SaveConfig(config);
    }

    private void ToggleMain()     => _mainWindow.Toggle();
    private void ToggleSettings() => _settingsWindow.Toggle();
}
