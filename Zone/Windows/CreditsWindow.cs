using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Zone.Windows;

public class CreditsWindow : Window, IDisposable
{
    private float _scrollY = 0f;
    private const float ScrollSpeed = 45f;

    private static readonly uint CRed    = ImGui.ColorConvertFloat4ToU32(new Vector4(0.90f, 0.12f, 0.12f, 1f));
    private static readonly uint CRedSft = ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.30f, 0.30f, 1f));
    private static readonly uint CWhite  = 0xFFFFFFFF;
    private static readonly uint CGrey   = 0xFF888888;
    private static readonly uint CDim    = 0xFFAAAAAA;

    private readonly string _logoPath;
    private readonly Dictionary<string, ISharedImmediateTexture> _texCache = new();

    public CreditsWindow() : base("##ZoneCredits",
        ImGuiWindowFlags.NoDecoration      |
        ImGuiWindowFlags.NoMove            |
        ImGuiWindowFlags.NoSavedSettings   |
        ImGuiWindowFlags.NoScrollbar       |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        DisableFadeInFadeOut = true;
        RespectCloseHotkey   = false;
        _logoPath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Data", "ZONEVisionLandscapeLogo.png");
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowFocus();
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0f, 0f, 0f, 0.93f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }

    public override void Draw()
    {
        try { DrawInternal(); }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] CreditsWindow draw crashed"); }
    }

    private void DrawInternal()
    {
        var dl     = ImGui.GetWindowDrawList();
        var screen = ImGuiHelpers.MainViewport.Size;
        float cx   = screen.X * 0.5f;
        var font   = ImGui.GetFont();

        // While credits are open, prevent main window from closing on ESC
        Plugin.MainWin.RespectCloseHotkey = false;

        // Click anywhere to close
        ImGui.SetCursorPos(Vector2.Zero);
        ImGui.InvisibleButton("##credBg", screen);
        bool close    = ImGui.IsItemClicked();
        bool closeHov = ImGui.IsItemHovered();

        // ── Advance scroll ───────────────────────────────────────────────────
        _scrollY += ScrollSpeed * ImGui.GetIO().DeltaTime;

        float y          = screen.Y - _scrollY;
        float baseFontSz = ImGui.GetFontSize();

        // ── Helpers ──────────────────────────────────────────────────────────
        float DrawCentered(string text, float posY, uint color, float sz)
        {
            float scale = sz / baseFontSz;
            ImGui.SetWindowFontScale(scale);
            var tsz = ImGui.CalcTextSize(text);
            ImGui.SetWindowFontScale(1f);
            if (posY < screen.Y + 100f && posY > -100f)
                dl.AddText(font, sz, new Vector2(cx - tsz.X * 0.5f, posY), color, text);
            return tsz.Y + 8f;
        }

        void DrawSep(float posY)
        {
            if (posY > -10f && posY < screen.Y + 10f)
                dl.AddLine(new Vector2(cx - 220f, posY + 6f),
                           new Vector2(cx + 220f, posY + 6f), 0xFF2A0000, 1f);
        }

        // ── Title ─────────────────────────────────────────────────────────────
        y += DrawCentered("ZONE", y, CRed, 64f);
        y += DrawCentered("THANK YOU FOR ATTENDING", y, CWhite, 24f);
        y += DrawCentered("ZONE EVENT 2026", y, CGrey, 20f);
        y += 40f; DrawSep(y); y += 36f;

        // ── DJs ───────────────────────────────────────────────────────────────
        y += DrawCentered("DJs", y, CRed, 34f);
        y += 16f;
        var perfs = Plugin.Db.GetAllPerformances();
        foreach (var dayGroup in perfs.GroupBy(p => p.Day).OrderBy(g => g.Key))
        {
            y += DrawCentered($"DAY {dayGroup.Key}", y, CDim, 17f);
            y += 4f;
            foreach (var p in dayGroup)
                y += DrawCentered(p.DjName, y, CWhite, 22f);
            y += 14f;
        }
        y += 20f; DrawSep(y); y += 36f;

        // ── Staff ─────────────────────────────────────────────────────────────
        y += DrawCentered("STAFF", y, CRed, 34f);
        y += 16f;
        var staffByRole = Plugin.Db.GetAllStaff()
            .Where(s => s.Role != "VIP")
            .GroupBy(s => s.Role)
            .OrderBy(g => g.Key);
        foreach (var group in staffByRole)
        {
            y += DrawCentered(group.Key.ToUpper(), y, CDim, 17f);
            y += 4f;
            foreach (var s in group)
                y += DrawCentered(s.CharacterName, y, CWhite, 22f);
            y += 16f;
        }
        y += 20f; DrawSep(y); y += 36f;

        // ── Partners ──────────────────────────────────────────────────────────
        y += DrawCentered("PARTNERS", y, CRed, 34f);
        y += 16f;
        foreach (var p in Plugin.Db.GetAllPartners())
        {
            bool drew = false;
            if (!string.IsNullOrWhiteSpace(p.LogoPath) && File.Exists(p.LogoPath))
            {
                if (!_texCache.TryGetValue(p.LogoPath, out var tex))
                {
                    tex = Plugin.TextureProvider.GetFromFile(new FileInfo(p.LogoPath));
                    _texCache[p.LogoPath] = tex;
                }
                var wrap = tex.GetWrapOrDefault();
                if (wrap != null)
                {
                    float maxH  = 75f;
                    float ratio = wrap.Size.X / wrap.Size.Y;
                    float lw    = MathF.Min(maxH * ratio, 280f);
                    float lh    = lw / ratio;
                    if (y > -100f && y < screen.Y + 100f)
                        dl.AddImage(wrap.Handle,
                            new Vector2(cx - lw * 0.5f, y),
                            new Vector2(cx + lw * 0.5f, y + lh));
                    y += lh + 14f;
                    drew = true;
                }
            }
            if (!drew)
                y += DrawCentered(p.Name, y, CWhite, 22f);
        }
        y += 20f; DrawSep(y); y += 36f;

        // ── VIPs ──────────────────────────────────────────────────────────────
        var vips = Plugin.Db.GetAllStaff()
            .Where(s => s.Role == "VIP" && s.VipPrice.HasValue)
            .OrderByDescending(s => s.VipPrice)
            .ToList();

        if (vips.Count > 0)
        {
            y += DrawCentered("SPECIAL THANKS TO OUR VIPs", y, CRed, 34f);
            y += 16f;

            // Two centered columns
            const float PairW = 560f;
            const float Gap   = 60f;
            float colW  = (PairW - Gap) / 2f;
            float col1X = cx - PairW * 0.5f;
            float col2X = col1X + colW + Gap;

            float rowH = 22f + 18f + 10f;

            for (int i = 0; i < vips.Count; i += 2)
            {
                var v1 = vips[i];
                var v2 = i + 1 < vips.Count ? vips[i + 1] : null;

                if (y > -100f && y < screen.Y + 100f)
                {
                    float scale22 = 22f / baseFontSz;
                    float scale18 = 18f / baseFontSz;
                    float col1CX = col1X + colW * 0.5f;
                    float col2CX = col2X + colW * 0.5f;

                    var n1sz = ImGui.CalcTextSize(v1.CharacterName) * scale22;
                    var g1sz = ImGui.CalcTextSize($"{v1.VipPrice!.Value:N0} gil") * scale18;
                    dl.AddText(font, 22f, new Vector2(col1CX - n1sz.X * 0.5f, y), CWhite, v1.CharacterName);
                    dl.AddText(font, 18f, new Vector2(col1CX - g1sz.X * 0.5f, y + 26f), CRedSft, $"{v1.VipPrice!.Value:N0} gil");

                    if (v2 != null)
                    {
                        var n2sz = ImGui.CalcTextSize(v2.CharacterName) * scale22;
                        var g2sz = ImGui.CalcTextSize($"{v2.VipPrice!.Value:N0} gil") * scale18;
                        dl.AddText(font, 22f, new Vector2(col2CX - n2sz.X * 0.5f, y), CWhite, v2.CharacterName);
                        dl.AddText(font, 18f, new Vector2(col2CX - g2sz.X * 0.5f, y + 26f), CRedSft, $"{v2.VipPrice!.Value:N0} gil");
                    }
                }
                y += rowH;
            }

            y += 20f;
            long total = vips.Sum(v => (long)v.VipPrice!.Value);
            y += DrawCentered("TOTAL DONATED BY VIPs", y, CDim, 20f);
            y += DrawCentered($"{total:N0} gil", y, CRed, 34f);
            y += 20f; DrawSep(y); y += 36f;
        }

        // ── Closing ───────────────────────────────────────────────────────────
        if (!_texCache.TryGetValue(_logoPath, out var logoTex))
        {
            logoTex = Plugin.TextureProvider.GetFromFile(new FileInfo(_logoPath));
            _texCache[_logoPath] = logoTex;
        }
        var logoWrap = logoTex.GetWrapOrDefault();
        if (logoWrap != null)
        {
            float lw = MathF.Min(screen.X * 0.4f, 420f);
            float lh = lw / (logoWrap.Size.X / logoWrap.Size.Y);
            if (y > -100f && y < screen.Y + 100f)
                dl.AddImage(logoWrap.Handle,
                    new Vector2(cx - lw * 0.5f, y),
                    new Vector2(cx + lw * 0.5f, y + lh));
            y += lh + 20f;
        }

        y += DrawCentered("SEE YOU NEXT TIME", y, CRed, 30f);
        y += DrawCentered("thezone.pro", y, CGrey, 18f);
        y += 40f;
        y += DrawCentered("PLUGIN DEVELOPED BY", y, CGrey, 16f);
        y += DrawCentered("Nala Praline", y, CDim, 22f);
        y += 160f;

        // Auto-close when fully scrolled off
        if (y < 0f)
        {
            IsOpen   = false;
            _scrollY = 0f;
            Plugin.MainWin.RespectCloseHotkey = true;
        }

        // ── Click hint (top right) ────────────────────────────────────────────
        const string hint     = "CLICK ANYWHERE TO CLOSE";
        float        hintSz   = 22f;
        float        hintScl  = hintSz / baseFontSz;
        var          hintDim  = ImGui.CalcTextSize(hint) * hintScl;
        dl.AddText(font, hintSz, new Vector2(screen.X - hintDim.X - 24f, 22f),
                   closeHov ? 0xCCFFFFFF : 0x88FFFFFF, hint);

        if (close)
        {
            IsOpen   = false;
            _scrollY = 0f;
            Plugin.MainWin.RespectCloseHotkey = true;
        }
    }

    public void Dispose()
    {
        _texCache.Clear();
    }
}
