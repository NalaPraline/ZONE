using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Zone.Windows;

public class SettingsWindow : Window
{
    private static readonly Vector4 CRed    = new(0.80f, 0f,    0f,    1f);
    private static readonly Vector4 CWhite  = new(1f,    1f,    1f,    1f);
    private static readonly Vector4 CGrey   = new(0.50f, 0.50f, 0.50f, 1f);
    private static readonly Vector4 CDkGrey = new(0.28f, 0.28f, 0.28f, 1f);
    private static readonly Vector4 CGreen  = new(0f,    0.78f, 0.20f, 1f);

    private static uint U(Vector4 v)              => ImGui.ColorConvertFloat4ToU32(v);
    private static uint WithAlpha(uint c, byte a) => (c & 0x00FFFFFF) | ((uint)a << 24);

    public SettingsWindow() : base("###ZoneSettings",
        ImGuiWindowFlags.NoScrollbar        |
        ImGuiWindowFlags.NoScrollWithMouse  |
        ImGuiWindowFlags.NoTitleBar         |
        ImGuiWindowFlags.NoCollapse         |
        ImGuiWindowFlags.NoResize)
    {
        Size          = new Vector2(420, 345);
        SizeCondition = ImGuiCond.Always;
        IsOpen        = false;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,    new Vector2(16f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,      new Vector2(10f,  8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,    4f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,       new Vector4(0.05f, 0.05f, 0.05f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,        new Vector4(0.07f, 0.07f, 0.07f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button,         new Vector4(0.50f, 0f,    0f,    0.85f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,  new Vector4(0.75f, 0f,    0f,    1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,        new Vector4(0.10f, 0.10f, 0.10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.15f, 0.05f, 0.05f, 1f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(4);
    }

    public override void Draw()
    {
        try
        {
            var wdl  = ImGui.GetWindowDrawList();
            var wpos = ImGui.GetWindowPos();
            var wsz  = ImGui.GetWindowSize();

            if (DrawTitleBar()) { IsOpen = false; return; }

            DrawContent();

            DrawHudRect(wdl, wpos + new Vector2(1, 1), wpos + wsz - new Vector2(1, 1),
                        14f, U(CRed), 1.5f, glow: true);
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] SettingsWindow draw crashed"); }
    }

    private bool DrawTitleBar()
    {
        var dl    = ImGui.GetWindowDrawList();
        float avail = ImGui.GetContentRegionAvail().X;
        const float H     = 38f;
        const float btnSz = 20f;
        const float btnGap = 5f;

        var sp = ImGui.GetCursorScreenPos();
        var lp = ImGui.GetCursorPos();

        dl.AddRectFilled(sp, sp + new Vector2(avail, H),
                         U(new Vector4(0.03f, 0.03f, 0.03f, 1f)));
        dl.AddRectFilled(sp, sp + new Vector2(3f, H), U(CRed));

        float btnY   = lp.Y + (H - btnSz) * 0.5f;
        float btnYsc = sp.Y + (H - btnSz) * 0.5f;

        // Close button, registered first for input priority
        float closeLocX = lp.X + avail - btnSz - btnGap;
        float closeScX  = sp.X + avail - btnSz - btnGap;
        ImGui.SetCursorPos(new Vector2(closeLocX, btnY));
        ImGui.InvisibleButton("##sClose", new Vector2(btnSz, btnSz));
        bool closeHov     = ImGui.IsItemHovered();
        bool closeClicked = ImGui.IsItemClicked();

        var cMin = new Vector2(closeScX, btnYsc);
        var cMax = cMin + new Vector2(btnSz, btnSz);
        DrawHudRect(dl, cMin, cMax, 4f,
                    closeHov ? U(CRed) : WithAlpha(U(CRed), 0x55), 1.2f, closeHov);
        float cx = cMin.X + btnSz * 0.5f, cy = cMin.Y + btnSz * 0.5f;
        uint xCol = closeHov ? 0xFFFFFFFF : U(CDkGrey);
        dl.AddLine(new Vector2(cx - 3.5f, cy - 3.5f), new Vector2(cx + 3.5f, cy + 3.5f), xCol, 1.5f);
        dl.AddLine(new Vector2(cx + 3.5f, cy - 3.5f), new Vector2(cx - 3.5f, cy + 3.5f), xCol, 1.5f);

        // Drag area, declared last for lower input priority
        float dragW = closeLocX - lp.X - btnGap;
        ImGui.SetCursorPos(lp);
        ImGui.InvisibleButton("##sDrag", new Vector2(dragW, H));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);

        const string title = "ZONE  ·  SETTINGS";
        float tw = ImGui.CalcTextSize(title).X;
        dl.AddText(
            new Vector2(sp.X + (avail - tw) * 0.5f, sp.Y + (H - ImGui.GetFontSize()) * 0.5f),
            0xFFFFFFFF, title);

        dl.AddLine(new Vector2(sp.X, sp.Y + H), new Vector2(sp.X + avail, sp.Y + H),
                   U(CRed), 1.5f);

        ImGui.SetCursorPos(new Vector2(lp.X, lp.Y + H + 2));
        return closeClicked;
    }

    private static void DrawContent()
    {
        var config = Plugin.Db.GetConfig();
        const float indent = 16f;

        ImGui.Spacing();
        ImGui.Spacing();

        DrawSectionHeader("NOTIFICATIONS");
        ImGui.Spacing();

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        bool notifs = config.NotificationsEnabled;
        if (ImGui.Checkbox("DJ change alerts##notif", ref notifs))
        {
            config.NotificationsEnabled = notifs;
            Plugin.Db.SaveConfig(config);
        }
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        ImGui.TextColored(CGrey, "Receive a notification when a new DJ goes live.");

        ImGui.Spacing();
        ImGui.Spacing();
        DrawThinSeparator();
        ImGui.Spacing();
        ImGui.Spacing();

        DrawSectionHeader("ZONE VISION OVERLAY");
        ImGui.Spacing();

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        bool overlay = config.ZoneVisionEnabled;
        if (ImGui.Checkbox("Show HUD overlay##overlay", ref overlay))
        {
            config.ZoneVisionEnabled = overlay;
            Plugin.Db.SaveConfig(config);
            Plugin.Overlay.IsOpen = overlay;
        }
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        ImGui.TextColored(CGrey, "Compact HUD widget showing current DJ and mode status.");

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        ImGui.TextColored(CGrey, "Default pinned corner:");
        ImGui.Spacing();

        // 2x2 corner picker
        // corners: 0=TL 1=TR 2=BL 3=BR
        string[] labels  = { "TOP LEFT", "TOP RIGHT", "BOTTOM LEFT", "BOTTOM RIGHT" };
        int[]    corners = { 0, 1, 2, 3 };
        int      current = config.OverlayCorner;

        using var fr  = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding,  3f);
        using var isp = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 6f));

        float btnW = (ImGui.GetContentRegionAvail().X - indent - 6f) * 0.5f;
        float btnH = ImGui.GetTextLineHeight() + 10f;

        for (int i = 0; i < 4; i++)
        {
            if (i == 0 || i == 2) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);

            bool selected = current == corners[i];
            using var bc  = ImRaii.PushColor(ImGuiCol.Button,        selected ? new Vector4(0.50f, 0f, 0f, 0.90f) : new Vector4(0.12f, 0.12f, 0.12f, 1f));
            using var bch = ImRaii.PushColor(ImGuiCol.ButtonHovered, selected ? new Vector4(0.70f, 0f, 0f, 1f)    : new Vector4(0.22f, 0.22f, 0.22f, 1f));
            using var tc  = ImRaii.PushColor(ImGuiCol.Text, selected ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.55f, 0.55f, 0.55f, 1f));

            if (ImGui.Button(labels[i] + "##corner" + i, new Vector2(btnW, btnH)))
            {
                config.OverlayCorner = corners[i];
                Plugin.Db.SaveConfig(config);
            }
            if (i % 2 == 0) ImGui.SameLine();
        }
    }

    private static void DrawSectionHeader(string text)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(new Vector2(pos.X, pos.Y + 2f), new Vector2(pos.X + 3f, pos.Y + 15f), U(CRed));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 10f);
        ImGui.TextColored(CWhite, text);
    }

    private static void DrawThinSeparator()
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        dl.AddLine(pos, pos + new Vector2(ImGui.GetContentRegionAvail().X, 0),
                   U(new Vector4(0.14f, 0f, 0f, 1f)), 1f);
        ImGui.Dummy(new Vector2(0, 1));
    }

    private static void DrawHudRect(ImDrawListPtr dl, Vector2 min, Vector2 max,
                                     float cut, uint color, float thickness = 1.5f, bool glow = false)
    {
        if (glow)
        {
            DrawChamfered(dl, min, max, cut, WithAlpha(color, 0x18), thickness * 6f);
            DrawChamfered(dl, min, max, cut, WithAlpha(color, 0x40), thickness * 2.5f);
        }
        DrawChamfered(dl, min, max, cut, color, thickness);

        float x = min.X, y = min.Y, x2 = max.X, y2 = max.Y;
        uint dot = WithAlpha(color, 0xCC);
        dl.AddCircleFilled(new Vector2(x + cut,  y),       2f, dot);
        dl.AddCircleFilled(new Vector2(x2 - cut, y),       2f, dot);
        dl.AddCircleFilled(new Vector2(x2,  y + cut),      2f, dot);
        dl.AddCircleFilled(new Vector2(x2,  y2 - cut),     2f, dot);
        dl.AddCircleFilled(new Vector2(x2 - cut, y2),      2f, dot);
        dl.AddCircleFilled(new Vector2(x + cut,  y2),      2f, dot);
        dl.AddCircleFilled(new Vector2(x,   y2 - cut),     2f, dot);
        dl.AddCircleFilled(new Vector2(x,   y + cut),      2f, dot);
    }

    private static void DrawChamfered(ImDrawListPtr dl, Vector2 min, Vector2 max,
                                       float cut, uint color, float thickness)
    {
        float x = min.X, y = min.Y, x2 = max.X, y2 = max.Y;
        dl.AddLine(new Vector2(x + cut,  y),    new Vector2(x2 - cut, y),    color, thickness);
        dl.AddLine(new Vector2(x2 - cut, y),    new Vector2(x2, y + cut),    color, thickness);
        dl.AddLine(new Vector2(x2, y + cut),    new Vector2(x2, y2 - cut),   color, thickness);
        dl.AddLine(new Vector2(x2, y2 - cut),   new Vector2(x2 - cut, y2),   color, thickness);
        dl.AddLine(new Vector2(x2 - cut, y2),   new Vector2(x + cut,  y2),   color, thickness);
        dl.AddLine(new Vector2(x + cut,  y2),   new Vector2(x,  y2 - cut),   color, thickness);
        dl.AddLine(new Vector2(x,  y2 - cut),   new Vector2(x,  y + cut),    color, thickness);
        dl.AddLine(new Vector2(x,  y + cut),    new Vector2(x + cut,  y),    color, thickness);
    }
}
