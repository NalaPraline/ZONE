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

    private bool     _pinned    = true;
    private Vector2? _pinnedPos = null;

    private ISharedImmediateTexture? _logoTexture;
    private readonly string _logoPath;

    public ZoneVisionOverlay()
        : base("##ZoneVisionOverlay",
            ImGuiWindowFlags.NoDecoration          |
            ImGuiWindowFlags.NoNav                 |
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
        if (_pinned)
        {
            if (_pinnedPos.HasValue)
            {
                ImGui.SetNextWindowPos(_pinnedPos.Value, ImGuiCond.Always);
            }
            else
            {
                var viewport = ImGui.GetMainViewport();
                int corner = Plugin.Db.GetConfig().OverlayCorner;
                bool left   = corner == 0 || corner == 2;
                bool top    = corner == 0 || corner == 1;
                var pivot   = new Vector2(left ? 0f : 1f, top ? 0f : 1f);
                var offset  = new Vector2(left ? 16f : -16f, top ? 16f : -16f);
                ImGui.SetNextWindowPos(
                    viewport.Pos + new Vector2(left ? 0f : viewport.Size.X, top ? 0f : viewport.Size.Y) + offset,
                    ImGuiCond.Always, pivot);
            }
        }
        Flags = _pinned
            ? Flags |  ImGuiWindowFlags.NoMove
            : Flags & ~ImGuiWindowFlags.NoMove;
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
        bool inHouse  = Plugin.TimeLock.IsHousingInterior;

        // Small discrete toggle
        if (DrawSimpleToggle(timeLock, "##toggleZM", inHouse))
        {
            if (!inHouse)
                Plugin.TimeLock.SetEnabled(!timeLock);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5f);
        if (inHouse)
            ImGui.TextColored(DkGrey, "UNAVAILABLE IN HOUSE");
        else
            ImGui.TextColored(timeLock ? BrRed : Grey, timeLock ? "ZONE MODE ON" : "ZONE MODE OFF");

        // Pin + Reset buttons (top-right, before logo)
        ImGui.SameLine();
        _logoTexture ??= Plugin.TextureProvider.GetFromFile(new FileInfo(_logoPath));
        var logoWrap = _logoTexture.GetWrapOrDefault();
        float logoW = 0f;
        if (logoWrap != null)
        {
            float logoH = 24f;
            logoW = logoWrap.Size.X / logoWrap.Size.Y * logoH;
            float logoX = wpos.X + W - 12f - logoW;
            float logoY = wpos.Y + 8f;
            dl.AddImage(logoWrap.Handle,
                new Vector2(logoX, logoY),
                new Vector2(logoX + logoW, logoY + logoH),
                Vector2.Zero, Vector2.One, 0xAAFFFFFF);
        }
        const float btnS = 22f, btnGap = 4f;
        float pinX   = W - 12f - logoW - 6f - btnS;
        float resetX = pinX - btnGap - btnS;
        float btnY   = ImGui.GetCursorPosY() - 5f;

        ImGui.SetCursorPos(new Vector2(resetX, btnY));
        if (DrawResetButton(_pinnedPos.HasValue || !_pinned, "##reset"))
        {
            _pinnedPos = null;
            _pinned    = true;
        }

        ImGui.SetCursorPos(new Vector2(pinX, btnY));
        if (DrawPinButton(_pinned, "##pin"))
        {
            if (!_pinned)
                _pinnedPos = ImGui.GetWindowPos();
            _pinned = !_pinned;
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

    private static bool DrawSimpleToggle(bool enabled, string id, bool disabled = false)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        const float S = 22f, R = 4f;

        ImGui.InvisibleButton(id, new Vector2(S, S));
        bool clicked = ImGui.IsItemClicked() && !disabled;
        bool hovered = ImGui.IsItemHovered() && !disabled;

        uint bg     = disabled ? U(new Vector4(0.08f, 0.08f, 0.08f, 1f))
                    : enabled  ? U(new Vector4(0.55f, 0.02f, 0.02f, 1f))
                               : U(new Vector4(0.12f, 0.12f, 0.12f, 1f));
        uint border = disabled ? U(new Vector4(0.15f, 0.15f, 0.15f, 1f))
                    : enabled  ? U(BrRed)
                               : U(hovered ? Grey : DkGrey);
        dl.AddRectFilled(pos, pos + new Vector2(S, S), bg, R);
        dl.AddRect(pos, pos + new Vector2(S, S), border, R, ImDrawFlags.None, 1f);

        uint dot = disabled ? U(new Vector4(0.18f, 0.18f, 0.18f, 1f))
                 : enabled  ? U(BrRed) : U(DkGrey);
        dl.AddCircleFilled(pos + new Vector2(S / 2f, S / 2f), enabled && !disabled ? 5f : 3.5f, dot);

        return clicked;
    }

    private static bool DrawResetButton(bool active, string id)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        const float S = 22f, R = 4f;

        ImGui.InvisibleButton(id, new Vector2(S, S));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();

        uint fill   = active ? U(new Vector4(0.18f, 0.18f, 0.18f, 1f)) : U(new Vector4(0.10f, 0.10f, 0.10f, 1f));
        uint border = active ? U(Grey) : U(hovered ? Grey : DkGrey);
        dl.AddRectFilled(pos, pos + new Vector2(S, S), fill, R);
        dl.AddRect(pos, pos + new Vector2(S, S), border, R, ImDrawFlags.None, 1f);

        // House icon
        uint ic = active ? U(White) : U(Grey);
        float cx = pos.X + S / 2f;
        float cy = pos.Y + S / 2f;
        dl.AddLine(new Vector2(cx - 5f, cy - 1f), new Vector2(cx,       cy - 6f), ic, 1.5f);
        dl.AddLine(new Vector2(cx,       cy - 6f), new Vector2(cx + 5f, cy - 1f), ic, 1.5f);
        dl.AddLine(new Vector2(cx - 4f, cy - 1f), new Vector2(cx - 4f, cy + 5f),  ic, 1.5f);
        dl.AddLine(new Vector2(cx + 4f, cy - 1f), new Vector2(cx + 4f, cy + 5f),  ic, 1.5f);
        dl.AddLine(new Vector2(cx - 4f, cy + 5f), new Vector2(cx + 4f, cy + 5f),  ic, 1.5f);

        if (hovered)
        {
            const string tip = "Reset position";
            var ts  = ImGui.CalcTextSize(tip);
            var tp  = pos + new Vector2(-ts.X - 6f, (S - ts.Y) / 2f);
            dl.AddRectFilled(tp - new Vector2(4f, 2f), tp + ts + new Vector2(4f, 2f),
                             U(new Vector4(0.1f, 0.1f, 0.1f, 0.9f)), 3f);
            dl.AddText(tp, U(Grey), tip);
        }

        return clicked && active;
    }

    private static bool DrawPinButton(bool pinned, string id)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        const float S = 22f, R = 4f;

        ImGui.InvisibleButton(id, new Vector2(S, S));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();

        uint border = pinned ? U(Grey) : U(hovered ? Grey : DkGrey);
        uint fill   = pinned ? U(new Vector4(0.18f, 0.18f, 0.18f, 1f)) : U(new Vector4(0.10f, 0.10f, 0.10f, 1f));
        dl.AddRectFilled(pos, pos + new Vector2(S, S), fill, R);
        dl.AddRect(pos, pos + new Vector2(S, S), border, R, ImDrawFlags.None, 1f);

        // Simple pin icon: vertical line + horizontal bar
        var cx = pos.X + S / 2f;
        var cy = pos.Y + S / 2f;
        uint ic = pinned ? U(White) : U(Grey);
        dl.AddLine(new Vector2(cx, cy - 5f), new Vector2(cx, cy + 5f), ic, 1.5f);
        dl.AddLine(new Vector2(cx - 4f, cy - 2f), new Vector2(cx + 4f, cy - 2f), ic, 1.5f);
        dl.AddCircleFilled(new Vector2(cx, cy - 5f), 2.5f, ic);

        if (hovered)
        {
            var ts  = ImGui.CalcTextSize(pinned ? "Pinned" : "Unlock");
            var tip = pos + new Vector2(-ts.X - 6f, (S - ts.Y) / 2f);
            dl.AddRectFilled(tip - new Vector2(4f, 2f), tip + ts + new Vector2(4f, 2f),
                             U(new Vector4(0.1f, 0.1f, 0.1f, 0.9f)), 3f);
            dl.AddText(tip, U(Grey), pinned ? "Pinned" : "Unlock");
        }

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
