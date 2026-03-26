using System;
using System.Runtime.InteropServices;
using Lumina.Excel.Sheets;

namespace Zone.Services;

public sealed class TimeLockService : IDisposable
{
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(
        nint lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);

    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    private static readonly byte[] TimeOriginal      = [0x4D, 0x8B, 0x8A, 0x78, 0x17, 0x00, 0x00];
    private static readonly byte[] TimeReplacement   = [0x49, 0xC7, 0xC1, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] WeatherReplacement = [0xB2, 0x01, 0x90, 0x90];

    // TerritoryIntendedUse row ID for housing interiors in FFXIV game data
    private const uint HousingIntendedUse = 14;

    private nint   _timePatchAddr;
    private nint   _weatherPatchAddr;
    private byte[] _weatherOriginal = [];
    private bool   _applied;

    public bool IsEnabled          => _applied;
    public bool IsHousingInterior  { get; private set; }

    public TimeLockService()
    {
        try
        {
            nint timeSig       = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 30 4C 8B 15");
            _timePatchAddr     = timeSig + 0x19;
            Plugin.Log.Information($"[Zone] Time patch at 0x{_timePatchAddr:X}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[Zone] Time sig not found.");
        }

        try
        {
            nint weatherSig    = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 30 80 B9 ?? ?? ?? ?? ?? 49 8B F8 0F 29 74 24 ?? 48 8B D9 0F 28 F1");
            _weatherPatchAddr  = weatherSig + 0x55;
            _weatherOriginal   = new byte[4];
            Marshal.Copy(_weatherPatchAddr, _weatherOriginal, 0, 4);
            Plugin.Log.Information($"[Zone] Weather patch at 0x{_weatherPatchAddr:X}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[Zone] Weather sig not found.");
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled && !Plugin.ClientState.IsLoggedIn) return;
        if (enabled && IsHousingInterior) return;
        if (enabled == _applied) return;

        if (_timePatchAddr != nint.Zero)
            WriteBytes(_timePatchAddr, enabled ? TimeReplacement : TimeOriginal);

        if (_weatherPatchAddr != nint.Zero && _weatherOriginal.Length > 0)
            WriteBytes(_weatherPatchAddr, enabled ? WeatherReplacement : _weatherOriginal);

        _applied = enabled;
        Plugin.Log.Information($"[Zone] Atmosphere {(enabled ? "enabled." : "disabled.")}");
    }

    public void OnTerritoryChanged(ushort territoryId)
    {
        if (territoryId == 0) return;
        var row = Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId);
        IsHousingInterior = row?.TerritoryIntendedUse.RowId == HousingIntendedUse;
        Plugin.Log.Information($"[Zone] Territory changed: {territoryId}, housing={IsHousingInterior}");
        if (IsHousingInterior && _applied)
        {
            SetEnabled(false);
            Plugin.Log.Information("[Zone] Atmosphere disabled — housing interior detected.");
        }
    }

    private void WriteBytes(nint addr, byte[] bytes)
    {
        VirtualProtect(addr, (nuint)bytes.Length, PAGE_EXECUTE_READWRITE, out var old);
        Marshal.Copy(bytes, 0, addr, bytes.Length);
        VirtualProtect(addr, (nuint)bytes.Length, old, out _);
    }

    public void Dispose() => SetEnabled(false);
}
