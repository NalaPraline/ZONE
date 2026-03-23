using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Zone.Models;

namespace Zone.Windows;

public class StaffDetailPopup : Window, IDisposable
{
    private StaffMember? _member;
    private ISharedImmediateTexture? _avatarTex;
    private string? _lastAvatarPath;

    private static readonly Vector4 Red    = new(0.80f, 0f,    0f,    1f);
    private static readonly Vector4 Grey   = new(0.45f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 DkGrey = new(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Vector4 White  = new(1f,    1f,    1f,    1f);
    private static readonly Vector4 Green  = new(0f,    0.75f, 0.20f, 1f);

    private const float W   = 360f; // total window width = photo width
    private const float Pad = 16f;

    public StaffDetailPopup()
        : base("##ZoneStaffDetail",
            ImGuiWindowFlags.NoTitleBar        |
            ImGuiWindowFlags.NoScrollbar       |
            ImGuiWindowFlags.NoCollapse        |
            ImGuiWindowFlags.AlwaysAutoResize  |
            ImGuiWindowFlags.NoSavedSettings)
    {
        IsOpen = false;
        RespectCloseHotkey = true;
    }

    public void OpenFor(StaffMember member)
    {
        _member = member;
        IsOpen  = true;
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.Pos + vp.Size * 0.5f, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSizeConstraints(new Vector2(W, 0f), new Vector2(W, 9999f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,    Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,      new Vector2(0f, 0f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.06f, 0.06f, 0.06f, 0.97f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(1);
        ImGui.PopStyleVar(3);
    }

    public override void Draw()
    {
        try { DrawContent(); }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] StaffDetailPopup draw crashed"); }
    }

    private void DrawContent()
    {
        if (_member == null) { IsOpen = false; return; }
        var m  = _member;
        var dl = ImGui.GetWindowDrawList();
        var wp = ImGui.GetWindowPos();

        var roleCol = ParseColor(m.Color) ?? Red;

        // Drag area registered first for input priority
        ImGui.InvisibleButton("##drag", new Vector2(W - 28f, 28f));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(wp + ImGui.GetIO().MouseDelta);

        ImGui.SameLine(0f, 0f);
        ImGui.InvisibleButton("##close_x", new Vector2(28f, 28f));
        bool xClicked = ImGui.IsItemClicked();
        bool xHov     = ImGui.IsItemHovered();
        var  xMin     = ImGui.GetItemRectMin();
        dl.AddRectFilled(xMin, xMin + new Vector2(28f, 28f), xHov ? 0xBB550000 : 0x00000000, 3f);
        float cx = xMin.X + 14f, cy = xMin.Y + 14f;
        dl.AddLine(new Vector2(cx - 5f, cy - 5f), new Vector2(cx + 5f, cy + 5f), 0xCCFFFFFF, 1.5f);
        dl.AddLine(new Vector2(cx + 5f, cy - 5f), new Vector2(cx - 5f, cy + 5f), 0xCCFFFFFF, 1.5f);
        if (xClicked) { IsOpen = false; return; }

        ImGui.SetCursorPos(Vector2.Zero);
        var wrap = GetAvatarWrap(m.AvatarPath);
        float imgH = 0f;
        if (wrap != null)
        {
            float ratio = wrap.Size.Y / wrap.Size.X;
            imgH = MathF.Min(W * ratio, 260f);
            Vector2 uv0 = Vector2.Zero;
            Vector2 uv1 = W * ratio > 260f ? new Vector2(1f, 260f / (W * ratio)) : Vector2.One;
            ImGui.Image(wrap.Handle, new Vector2(W, imgH), uv0, uv1);
            var bot = wp + new Vector2(0f, imgH);
            dl.AddRectFilledMultiColor(bot - new Vector2(0f, 70f), bot + new Vector2(W, 0f),
                0x00000000, 0x00000000, 0xF50F0F0F, 0xF50F0F0F);
        }
        else
        {
            dl.AddRectFilled(wp, wp + new Vector2(W, 160f), 0xFF111111);
            imgH = 160f;
            ImGui.Dummy(new Vector2(W, imgH));
        }

        ImGui.Dummy(new Vector2(W, 8f));
        ImGui.SetWindowFontScale(1.15f);
        float nw = ImGui.CalcTextSize(m.CharacterName).X;
        ImGui.SetCursorPosX((W - nw) * 0.5f);
        ImGui.TextColored(White, m.CharacterName);
        ImGui.SetWindowFontScale(1f);

        if (!string.IsNullOrWhiteSpace(m.World))
        {
            float ww = ImGui.CalcTextSize(m.World).X;
            ImGui.SetCursorPosX((W - ww) * 0.5f);
            ImGui.TextColored(DkGrey, m.World);
        }

        ImGui.Dummy(new Vector2(W, 6f));

        ImGui.SetWindowFontScale(0.9f);
        string rt      = m.Role.ToUpperInvariant();
        float  rw      = ImGui.CalcTextSize(rt).X + 22f;
        float  rh      = ImGui.GetTextLineHeight() + 8f;
        float  badgeY  = ImGui.GetCursorPosY();
        var    rp      = ImGui.GetCursorScreenPos() + new Vector2((W - rw) * 0.5f, 0f);
        dl.AddRectFilled(rp, rp + new Vector2(rw, rh),
            ImGui.ColorConvertFloat4ToU32(roleCol with { W = 0.18f }), 3f);
        dl.AddRect(rp, rp + new Vector2(rw, rh),
            ImGui.ColorConvertFloat4ToU32(roleCol with { W = 0.6f }), 3f, ImDrawFlags.None, 1f);
        ImGui.SetCursorPos(new Vector2((W - rw) * 0.5f + 11f, badgeY + 4f));
        ImGui.TextColored(roleCol, rt);
        ImGui.SetWindowFontScale(1f);
        ImGui.SetCursorPosY(badgeY + rh);
        ImGui.Dummy(new Vector2(W, 6f));

        if (m.IsOnlineDetected)
        {
            float ow = ImGui.CalcTextSize("● Online").X;
            ImGui.SetCursorPosX((W - ow) * 0.5f);
            ImGui.TextColored(Green, "● Online");
        }

        ImGui.Dummy(new Vector2(W, 10f));

        var sp = ImGui.GetCursorScreenPos();
        dl.AddLine(sp + new Vector2(Pad, 0f), sp + new Vector2(W - Pad, 0f),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.22f, 0f, 0f, 1f)), 1f);
        ImGui.Dummy(new Vector2(W, 8f));

        if (!string.IsNullOrWhiteSpace(m.Description))
        {
            ImGui.SetCursorPosX(Pad);
            ImGui.PushTextWrapPos(W - Pad);
            ImGui.TextColored(Grey, m.Description);
            ImGui.PopTextWrapPos();
            ImGui.Dummy(new Vector2(W, 6f));
        }

        float vx = 90f;
        if (!string.IsNullOrWhiteSpace(m.DiscordTag))
        {
            ImGui.SetCursorPosX(Pad); ImGui.TextColored(DkGrey, "Discord");
            ImGui.SameLine(vx);      ImGui.TextColored(White,  m.DiscordTag);
        }
        if (!string.IsNullOrWhiteSpace(m.TwitterHandle))
        {
            var h = m.TwitterHandle.StartsWith('@') ? m.TwitterHandle : "@" + m.TwitterHandle;
            ImGui.SetCursorPosX(Pad); ImGui.TextColored(DkGrey, "Twitter");
            ImGui.SameLine(vx);      ImGui.TextColored(White, h);
        }
        if (!string.IsNullOrWhiteSpace(m.TwitchUrl))
        {
            ImGui.SetCursorPosX(Pad); ImGui.TextColored(DkGrey, "Twitch");
            ImGui.SameLine(vx);
            ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.5f, 0f, 0f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0f, 0f, 1f));
            if (ImGui.SmallButton("OPEN")) Dalamud.Utility.Util.OpenLink(m.TwitchUrl);
            ImGui.PopStyleColor(2);
        }

        ImGui.Dummy(new Vector2(W, 8f));
        float bw = 100f;
        ImGui.SetCursorPosX((W - bw) * 0.5f);
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.18f, 0f, 0f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.55f, 0f, 0f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
        if (ImGui.Button("CLOSE", new Vector2(bw, 26f))) IsOpen = false;
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
        ImGui.Dummy(new Vector2(W, 10f));

        var ws = ImGui.GetWindowSize();
        dl.AddRect(wp, wp + ws, ImGui.ColorConvertFloat4ToU32(new Vector4(0.28f, 0f, 0f, 1f)));
        dl.AddLine(wp, wp + new Vector2(ws.X, 0f), ImGui.ColorConvertFloat4ToU32(Red), 2f);
    }

    private Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? GetAvatarWrap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        if (path != _lastAvatarPath)
        {
            _lastAvatarPath = path;
            _avatarTex = Plugin.TextureProvider.GetFromFile(new FileInfo(path));
        }
        return _avatarTex?.GetWrapOrDefault();
    }

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

    public void Dispose() { }
}
