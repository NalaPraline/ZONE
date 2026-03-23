using System;
using System.Runtime.InteropServices;

namespace Zone.Services;

public sealed class TimeLockService : IDisposable
{
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(
        nint lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);

    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    private static readonly byte[] TimeOriginal    = [0x4D, 0x8B, 0x8A, 0x78, 0x17, 0x00, 0x00];
    private static readonly byte[] TimeReplacement = [0x49, 0xC7, 0xC1, 0x00, 0x00, 0x00, 0x00];

    private nint _timePatchAddr;
    private bool _timeApplied;

    public TimeLockService()
    {
        try
        {
            nint timeSig   = Plugin.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 30 4C 8B 15");
            _timePatchAddr = timeSig + 0x19;
            Plugin.Log.Information($"[Zone] TimeLock patch located at 0x{_timePatchAddr:X}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[Zone] TimeLock: time signature not found — may need update after a game patch.");
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (_timePatchAddr != nint.Zero && enabled != _timeApplied)
        {
            WriteBytes(_timePatchAddr, enabled ? TimeReplacement : TimeOriginal);
            _timeApplied = enabled;
            Plugin.Log.Information($"[Zone] TimeLock {(enabled ? "enabled — Eorzea time frozen at midnight." : "disabled — Eorzea time restored.")}");
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
