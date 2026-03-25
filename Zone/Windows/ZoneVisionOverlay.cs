using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Zone.Windows;

public class ZoneVisionOverlay : Window
{
    private static readonly Vector4 Red    = new(0.80f, 0f,    0f,    1f);
    private static readonly Vector4 BrRed  = new(1f,    0.10f, 0.10f, 1f);
    private static readonly Vector4 White  = new(1f,    1f,    1f,    1f);
    private static readonly Vector4 Grey   = new(0.50f, 0.50f, 0.50f, 1f);
    private static readonly Vector4 DkGrey = new(0.22f, 0.22f, 0.22f, 1f);

    private const float W = 420f;

    private ISharedImmediateTexture? _logoTexture;
    private readonly string _logoPath;

    public ZoneVisionOverlay()
        : base("##ZoneVisionOverlay",
            ImGuiWindowFlags.NoDecoration          |
            ImGuiWindowFlags.NoNav                 |
            ImGuiWindowFlags.NoMove                |
            ImGuiWindowFlags.AlwaysAutoResize      |
            ImGuiWindowFlags.NoSavedSettings       |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoFocusOnAppearing)
    {
        IsOpen = false;
        RespectCloseHotkey = false;
        DisableFadeInFadeOut = true;
        _logoPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Data", "VISIONLogo.png");
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(
            viewport.Pos + viewport.Size - new Vector2(16f, 16f),
            ImGuiCond.Always, new Vector2(1f, 1f));
        ImGui.SetNextWindowSizeConstraints(new Vector2(W, 0f), new Vector2(W, float.MaxValue));
        ImGui.SetNextWindowBgAlpha(0.94f);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,    new Vector2(12f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,      new Vector2(8f,  6f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,  new Vector4(0.05f, 0.05f, 0.05f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.22f, 0f,    0f,    1f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    public override void Draw()
    {
        try { DrawContent(); }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] ZoneVisionOverlay draw crashed"); }
    }

    private void DrawContent()
    {
        var dl   = ImGui.GetWindowDrawList();
        var wpos = ImGui.GetWindowPos();

        bool timeLock = Plugin.TimeLock.IsEnabled;
        if (DrawDayNightToggle(timeLock, "##toggleZM"))
            Plugin.TimeLock.SetEnabled(!timeLock);
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 11f);
        ImGui.TextColored(timeLock ? BrRed : White, timeLock ? "ZONE MODE ON" : "ZONE MODE OFF");

        // Logo via draw list so it doesn't affect layout
        _logoTexture ??= Plugin.TextureProvider.GetFromFile(new FileInfo(_logoPath));
        var logoWrap = _logoTexture.GetWrapOrDefault();
        if (logoWrap != null)
        {
            float logoH = 36f;
            float logoW = logoWrap.Size.X / logoWrap.Size.Y * logoH;
            float logoX = wpos.X + W - 12f - logoW;
            float logoY = wpos.Y + 10f;
            dl.AddImage(logoWrap.Handle,
                new Vector2(logoX, logoY),
                new Vector2(logoX + logoW, logoY + logoH));
        }

        ImGui.Separator();

        var live = Plugin.Db.GetLivePerformance();

        if (live != null)
        {
            float blink = (float)(Math.Sin(ImGui.GetTime() * 4) * 0.5 + 0.5);

            ImGui.SetWindowFontScale(2.0f);
            var dot = "● ";
            ImGui.TextColored(new Vector4(1f, 0.1f, 0.1f, blink), dot);
            ImGui.SameLine(0f, 0f);
            ImGui.TextColored(White, $"DJS {live.DjName.ToUpperInvariant()}");
            ImGui.SetWindowFontScale(1.0f);
        }
        else
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 6f);
            ImGui.TextColored(White, "● NO LIVE DJ");
        }

        ImGui.Separator();

        var target     = Plugin.TargetManager.Target;
        var targetName = target?.Name.TextValue;

        string line; Vector4 lineCol;
        if (!string.IsNullOrEmpty(targetName))
        {
            var allStaff = Plugin.Db.GetAllStaff();

            // Lookup by ContentId first, then name+world, then name only
            Zone.Models.StaffMember? staff = null;
            ulong contentId  = GetTargetContentId(target.Address);
            string? worldName = GetTargetWorld(target.Address);

            if (contentId != 0)
                staff = allStaff.FirstOrDefault(s => s.ContentId != 0 && s.ContentId == contentId);

            if (staff == null && worldName != null)
                staff = allStaff.FirstOrDefault(s =>
                    s.CharacterName.Equals(targetName, StringComparison.OrdinalIgnoreCase) &&
                    s.World != null && s.World.Equals(worldName, StringComparison.OrdinalIgnoreCase));

            staff ??= allStaff.FirstOrDefault(s => s.CharacterName.Equals(targetName, StringComparison.OrdinalIgnoreCase));

            if (staff != null)
            {
                string venueStr = !string.IsNullOrEmpty(staff.Venue) ? $"  ·  {staff.Venue.ToUpperInvariant()}" : "";
                line    = $"< {staff.CharacterName.ToUpperInvariant()}  {staff.Role.ToUpperInvariant()}{venueStr} >";
                lineCol = ParseColor(staff.Color) ?? BrRed;
            }
            else
            {
                line    = $"< {targetName.ToUpperInvariant()} >";
                lineCol = White;
            }
        }
        else
        {
            line    = "< TARGET >";
            lineCol = White;
        }

        float tw = ImGui.CalcTextSize(line).X;
        ImGui.SetCursorPosX(MathF.Max((W - tw) * 0.5f, 12f));
        ImGui.TextColored(lineCol, line);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);

        // Red left border (drawn after layout so height is known)
        var wheight = ImGui.GetWindowHeight();
        dl.AddLine(wpos, wpos + new Vector2(0f, wheight), ImGui.ColorConvertFloat4ToU32(Red), 2f);
    }

    private static uint U(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);

    private static Vector4? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return null;
        try
        {
            float r = Convert.ToInt32(hex[..2], 16) / 255f;
            float g = Convert.ToInt32(hex[2..4], 16) / 255f;
            float b = Convert.ToInt32(hex[4..6], 16) / 255f;
            return new Vector4(r, g, b, 1f);
        }
        catch { return null; }
    }

    private static bool DrawDayNightToggle(bool enabled, string id)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        const float W = 80f, H = 36f, R = H / 2f, KR = R - 3f;

        ImGui.InvisibleButton(id, new Vector2(W, H));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();
        float cy = pos.Y + H / 2f;

        uint bg = enabled ? U(new Vector4(0.06f, 0.06f, 0.18f, 1f))
                          : U(new Vector4(0.22f, 0.50f, 0.88f, 1f));
        dl.AddRectFilled(pos, pos + new Vector2(W, H), bg, R);

        if (enabled)
        {
            uint star = U(new Vector4(1f, 1f, 0.85f, 0.95f));
            dl.AddCircleFilled(new Vector2(pos.X + 15f, pos.Y + 10f), 2.0f, star);
            dl.AddCircleFilled(new Vector2(pos.X + 26f, pos.Y + 21f), 1.5f, star);
            dl.AddCircleFilled(new Vector2(pos.X + 13f, pos.Y + 25f), 1.0f, star);
        }
        else
        {
            uint sun = U(new Vector4(1f, 0.88f, 0.15f, 1f));
            dl.AddCircleFilled(new Vector2(pos.X + W - 23f, pos.Y + 12f), 7f, sun);
            uint cloud = U(new Vector4(1f, 1f, 1f, 0.90f));
            dl.AddCircleFilled(new Vector2(pos.X + W - 30f, pos.Y + 24f), 5.0f, cloud);
            dl.AddCircleFilled(new Vector2(pos.X + W - 21f, pos.Y + 22f), 6.5f, cloud);
            dl.AddCircleFilled(new Vector2(pos.X + W - 13f, pos.Y + 24f), 4.5f, cloud);
            dl.AddRectFilled(new Vector2(pos.X + W - 35f, pos.Y + 24f),
                             new Vector2(pos.X + W -  8f, pos.Y + 30f), cloud);
        }

        float knobX = enabled ? pos.X + W - R : pos.X + R;
        dl.AddCircleFilled(new Vector2(knobX, cy), KR, 0xFFFFFFFF);
        if (enabled)
            dl.AddCircleFilled(new Vector2(knobX + 4f, cy - 3f), KR * 0.72f,
                               U(new Vector4(0.06f, 0.06f, 0.18f, 1f)));

        dl.AddRect(pos, pos + new Vector2(W, H),
                   U(new Vector4(1f, 1f, 1f, hovered ? 0.35f : 0.10f)), R, ImDrawFlags.None, 1.5f);
        return clicked;
    }

    private static unsafe ulong GetTargetContentId(nint address)
    {
        if (address == nint.Zero) return 0;
        try { return ((BattleChara*)address)->ContentId; }
        catch { return 0; }
    }

    private static unsafe string? GetTargetWorld(nint address)
    {
        if (address == nint.Zero) return null;
        try
        {
            ushort worldId = ((BattleChara*)address)->Character.HomeWorld;
            var sheet = Plugin.DataManager?.GetExcelSheet<Lumina.Excel.Sheets.World>();
            return sheet?.GetRow(worldId).Name.ToString();
        }
        catch { return null; }
    }
}
