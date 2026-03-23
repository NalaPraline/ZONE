using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
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

    private const float W = 340f;

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
        _logoPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Data", "ZONEVision.png");
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
        var col = timeLock ? BrRed : DkGrey;
        float rowStartY = ImGui.GetCursorPosY();

        ImGui.TextColored(col, "ACTIVATE");
        ImGui.TextColored(col, "ZONE MODE");

        var bMin = ImGui.GetItemRectMin() - new Vector2(0f, ImGui.GetTextLineHeightWithSpacing());
        var bMax = ImGui.GetItemRectMax();
        ImGui.SetCursorScreenPos(bMin);
        if (ImGui.InvisibleButton("##toggle", bMax - bMin))
            Plugin.TimeLock.SetEnabled(!timeLock);

        // Logo via draw list so it doesn't affect layout
        var logoWrap = Plugin.TextureProvider.GetFromFile(new FileInfo(_logoPath)).GetWrapOrDefault();
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
            ImGui.TextColored(DkGrey, "● NO LIVE DJ");
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
                line    = $"< {staff.CharacterName.ToUpperInvariant()}  {staff.Role.ToUpperInvariant()} >";
                lineCol = BrRed;
            }
            else
            {
                line    = $"< {targetName.ToUpperInvariant()} >";
                lineCol = Grey;
            }
        }
        else
        {
            line    = "< TARGET >";
            lineCol = DkGrey;
        }

        float tw = ImGui.CalcTextSize(line).X;
        ImGui.SetCursorPosX(MathF.Max((W - tw) * 0.5f, 12f));
        ImGui.TextColored(lineCol, line);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);

        // Red left border (drawn after layout so height is known)
        var wheight = ImGui.GetWindowHeight();
        dl.AddLine(wpos, wpos + new Vector2(0f, wheight), ImGui.ColorConvertFloat4ToU32(Red), 2f);
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
