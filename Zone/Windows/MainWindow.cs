using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Zone.Models;

namespace Zone.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly StaffDetailPopup _staffPopup;
    private readonly SettingsWindow   _settings;

    private List<Performance>  _performances  = new();
    private List<Activity>     _activities    = new();
    private List<StaffMember>  _staff         = new();
    private List<Partner>      _partners      = new();
    private DateTime           _dataRefreshed = DateTime.MinValue;
    private bool               _forceRefresh;

    private int    _activeTab   = 0;
    private int    _lineupDay   = 1;
    private int    _activityDay = 1;
    private string _staffSearch = "";


    private static readonly string[] TabNames = { "HOME", "AMBIANCE", "LINEUP", "ACTIVITIES", "STAFF", "PARTNERS" };

    private readonly Dictionary<string, ISharedImmediateTexture> _texCache = new();

    private static readonly Vector4 CRed    = new(0.80f, 0f,    0f,    1f);
    private static readonly Vector4 CBrRed  = new(1f,    0.10f, 0.10f, 1f);
    private static readonly Vector4 CWhite  = new(1f,    1f,    1f,    1f);
    private static readonly Vector4 CGrey   = new(0.50f, 0.50f, 0.50f, 1f);
    private static readonly Vector4 CDkGrey = new(0.28f, 0.28f, 0.28f, 1f);
    private static readonly Vector4 CGreen  = new(0f,    0.78f, 0.20f, 1f);

    private const string EventDescription = "ZONE is ultimately at its core a music festival. Designed to maintain its focus around music and celebrating that to bring together a wealth of talents in the community to participate in a celebration of music.";
    private const string EventWebsiteUrl   = "https://thezone.pro/";
    private const string EventMerchUrl     = "https://thezone.pro/vault";
    private const string LightlessId       = "TODO";
    private const string LightlessPass     = "TODO";
    private const string RavaId            = "TODO";
    private const string RavaPass          = "TODO";
    private const string PlayerSyncId      = "TODO";
    private const string PlayerSyncPass    = "TODO";
    // Lifestream: Shirogane=3, Ward 1, Plot 7
    private const int    TpWard  = 1;
    private const int    TpPlot  = 7;

    private static uint U(Vector4 v)              => ImGui.ColorConvertFloat4ToU32(v);
    private static uint WithAlpha(uint c, byte a) => (c & 0x00FFFFFF) | ((uint)a << 24);

    public MainWindow(StaffDetailPopup staffPopup, SettingsWindow settings)
        : base("###ZoneMainWindow",
               ImGuiWindowFlags.NoScrollbar         |
               ImGuiWindowFlags.NoScrollWithMouse   |
               ImGuiWindowFlags.NoTitleBar          |
               ImGuiWindowFlags.NoCollapse)
    {
        _staffPopup = staffPopup;
        _settings   = settings;

        Size = new Vector2(720, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(640, 440),
            MaximumSize = new Vector2(1280, 960)
        };
        DisableFadeInFadeOut = true;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,    new Vector2(16f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,      new Vector2(10f,  8f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,    4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,    4f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg,            new Vector4(0.05f, 0.05f, 0.05f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg,             new Vector4(0.07f, 0.07f, 0.07f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button,              new Vector4(0.50f, 0f,    0f,    0.85f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,       new Vector4(0.75f, 0f,    0f,    1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,        new Vector4(1f,    0.10f, 0.10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header,              new Vector4(0.40f, 0f,    0f,    0.60f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,       new Vector4(0.55f, 0f,    0f,    0.80f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,             new Vector4(0.10f, 0.10f, 0.10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,      new Vector4(0.15f, 0.05f, 0.05f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Separator,           new Vector4(0.18f, 0f,    0f,    1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,         new Vector4(0.05f, 0.05f, 0.05f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,       new Vector4(0.35f, 0f,    0f,    1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered,new Vector4(0.55f, 0f,    0f,    1f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(13);
        ImGui.PopStyleVar(5);
    }

    private void RefreshData()
    {
        _performances  = Plugin.Db.GetAllPerformances();
        _activities    = Plugin.Db.GetAllActivities();
        _staff         = Plugin.Db.GetAllStaff();
        _partners      = Plugin.Db.GetAllPartners();
        _dataRefreshed = DateTime.Now;
        _forceRefresh  = false;
    }

    public override void Draw()
    {
        try { DrawInternal(); }
        catch (Exception ex) { Plugin.Log.Error(ex, "[Zone] MainWindow draw crashed"); }
    }

    private void DrawInternal()
    {
        if (_forceRefresh || (DateTime.Now - _dataRefreshed).TotalSeconds > 30)
            RefreshData();

        var onlineNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in Plugin.ObjectTable)
            if ((int)obj.ObjectKind == 1) // ObjectKind.Player = 1
                onlineNames.Add(obj.Name.TextValue);
        foreach (var s in _staff)
            s.IsOnlineDetected = onlineNames.Contains(s.CharacterName);

        var wdl  = ImGui.GetWindowDrawList();
        var wpos = ImGui.GetWindowPos();
        var wsz  = ImGui.GetWindowSize();

        if (DrawTitleBar()) { IsOpen = false; return; }
        DrawCustomTabBar();

        using (var content = ImRaii.Child("##content", Vector2.Zero, false))
        {
            if (content)
            {
                switch (_activeTab)
                {
                    case 0: DrawHomeTab(wsz.X);   break;
                    case 1: DrawAmbianceTab();    break;
                    case 2: DrawLineupTab();      break;
                    case 3: DrawActivitiesTab();  break;
                    case 4: DrawStaffTab();        break;
                    case 5: DrawPartnersTab();     break;
                }
            }
        }

        DrawHudRect(wdl, wpos + new Vector2(1, 1), wpos + wsz - new Vector2(1, 1),
                    14f, U(CRed), 1.5f, glow: true);
    }

    private bool DrawTitleBar()
    {
        var dl    = ImGui.GetWindowDrawList();
        float avail = ImGui.GetContentRegionAvail().X;
        const float H     = 46f;
        const float fs    = 24f;
        const float btnSz = 22f;
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
        ImGui.InvisibleButton("##tbClose", new Vector2(btnSz, btnSz));
        bool closeHov     = ImGui.IsItemHovered();
        bool closeClicked = ImGui.IsItemClicked();

        var cMin = new Vector2(closeScX, btnYsc);
        var cMax = cMin + new Vector2(btnSz, btnSz);
        DrawHudRect(dl, cMin, cMax, 5f,
                    closeHov ? U(CRed) : WithAlpha(U(CRed), 0x55), 1.2f, closeHov);
        float cx = cMin.X + btnSz * 0.5f, cy = cMin.Y + btnSz * 0.5f;
        uint xCol = closeHov ? 0xFFFFFFFF : U(CDkGrey);
        dl.AddLine(new Vector2(cx - 4, cy - 4), new Vector2(cx + 4, cy + 4), xCol, 1.5f);
        dl.AddLine(new Vector2(cx + 4, cy - 4), new Vector2(cx - 4, cy + 4), xCol, 1.5f);

        float settLocX = closeLocX - btnSz - btnGap;
        float settScX  = closeScX  - btnSz - btnGap;
        ImGui.SetCursorPos(new Vector2(settLocX, btnY));
        ImGui.InvisibleButton("##tbSettings", new Vector2(btnSz, btnSz));
        bool settHov     = ImGui.IsItemHovered();
        bool settClicked = ImGui.IsItemClicked();

        var sMin = new Vector2(settScX, btnYsc);
        var sMax = sMin + new Vector2(btnSz, btnSz);
        DrawHudRect(dl, sMin, sMax, 5f,
                    settHov ? U(CRed) : WithAlpha(U(new Vector4(0.5f, 0f, 0f, 1f)), 0x50),
                    1.2f, settHov);
        uint lineCol = settHov ? 0xFFFFFFFF : U(CDkGrey);
        float lx = sMin.X + 5f, lw = btnSz - 10f;
        dl.AddLine(new Vector2(lx, sMin.Y + 7f),  new Vector2(lx + lw, sMin.Y + 7f),  lineCol, 1.2f);
        dl.AddLine(new Vector2(lx, sMin.Y + 11f), new Vector2(lx + lw, sMin.Y + 11f), lineCol, 1.2f);
        dl.AddLine(new Vector2(lx, sMin.Y + 15f), new Vector2(lx + lw, sMin.Y + 15f), lineCol, 1.2f);

        // Drag area, declared last for lower input priority
        float dragW = settLocX - lp.X - btnGap;
        ImGui.SetCursorPos(lp);
        ImGui.InvisibleButton("##tbDrag", new Vector2(dragW, H));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);

        var   font  = ImGui.GetFont();
        float scale = fs / ImGui.GetFontSize();
        float w1    = ImGui.CalcTextSize("ZONE  ").X * scale;
        float wV    = ImGui.CalcTextSize("V").X      * scale;
        float wRest = ImGui.CalcTextSize("ISION").X  * scale;
        float totalW = w1 + wV + wRest;
        float tx = sp.X + (avail - totalW) * 0.5f;
        float ty = sp.Y + (H - fs)         * 0.5f + 1f;

        dl.AddText(font, fs, new Vector2(tx,           ty), 0xFFFFFFFF, "ZONE  ");
        dl.AddText(font, fs, new Vector2(tx + w1,      ty), U(CRed),   "V");
        dl.AddText(font, fs, new Vector2(tx + w1 + wV, ty), 0xFFFFFFFF, "ISION");

        dl.AddLine(new Vector2(sp.X, sp.Y + H), new Vector2(sp.X + avail, sp.Y + H),
                   U(CRed), 1.5f);

        ImGui.SetCursorPos(new Vector2(lp.X, lp.Y + H + 2));

        if (settClicked) _settings.IsOpen = !_settings.IsOpen;
        return closeClicked;
    }


    private void DrawCustomTabBar()
    {
        var dl      = ImGui.GetWindowDrawList();
        float avail = ImGui.GetContentRegionAvail().X;
        const float H = 40f;
        int   n     = TabNames.Length;
        float W     = MathF.Floor(avail / n);

        var screenOrigin = ImGui.GetCursorScreenPos();
        var localOrigin  = ImGui.GetCursorPos();

        for (int i = 0; i < n; i++)
        {
            var tMin = new Vector2(screenOrigin.X + i * W, screenOrigin.Y);
            var tMax = new Vector2(tMin.X + W, tMin.Y + H);

            ImGui.SetCursorPos(new Vector2(localOrigin.X + i * W, localOrigin.Y));
            ImGui.InvisibleButton($"##tab{i}", new Vector2(W, H));

            bool active  = _activeTab == i;
            bool hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) _activeTab = i;

            uint bg = active  ? U(new Vector4(0.38f, 0f,    0f,    1f))
                    : hovered ? U(new Vector4(0.13f, 0.04f, 0.04f, 1f))
                    :           U(new Vector4(0.07f, 0.07f, 0.07f, 1f));
            dl.AddRectFilled(tMin, tMax, bg);

            if (active)
                dl.AddLine(new Vector2(tMin.X + 8, tMax.Y - 2f),
                           new Vector2(tMax.X - 8, tMax.Y - 2f),
                           U(new Vector4(1f, 0.10f, 0.10f, 1f)), 2.5f);

            var sz = ImGui.CalcTextSize(TabNames[i]);
            dl.AddText(new Vector2(tMin.X + (W - sz.X) * 0.5f, tMin.Y + (H - sz.Y) * 0.5f),
                       active  ? 0xFFFFFFFF
                       : hovered ? U(new Vector4(0.85f, 0.85f, 0.85f, 1f))
                       :           U(new Vector4(0.45f, 0.45f, 0.45f, 1f)),
                       TabNames[i]);

            if (i < n - 1)
                dl.AddLine(new Vector2(tMax.X, tMin.Y + 10),
                           new Vector2(tMax.X, tMax.Y - 10),
                           U(new Vector4(0.14f, 0.14f, 0.14f, 1f)), 1f);
        }

        dl.AddLine(new Vector2(screenOrigin.X, screenOrigin.Y + H),
                   new Vector2(screenOrigin.X + avail, screenOrigin.Y + H),
                   U(new Vector4(0.50f, 0f, 0f, 1f)), 1f);

        ImGui.SetCursorPos(new Vector2(localOrigin.X, localOrigin.Y + H + 3));
    }

    private void DrawHomeTab(float outerW)
    {
        var now    = DateTime.UtcNow;
        var day1   = new DateTime(2026, 3, 27);
        var day2   = new DateTime(2026, 3, 28);
        int? evDay = now.Date == day1 ? 1 : now.Date == day2 ? 2 : (int?)null;

        using var scroll = ImRaii.Child("HomeScroll", Vector2.Zero, false);
        if (!scroll) return;

        var   dl     = ImGui.GetWindowDrawList();
        float availW = ImGui.GetContentRegionAvail().X;

        {
            bool pre  = now.Date < day1;
            bool isEv = evDay.HasValue;

            string line1, line2;
            uint   col1;

            if (pre)
            {
                var diff = day1.AddHours(17) - now;
                int d = (int)diff.TotalDays, h = diff.Hours, m = diff.Minutes;
                line1 = d > 0 ? $"STARTS IN {d}D {h:D2}H {m:D2}M" : $"STARTS IN {h:D2}H {m:D2}M";
                line2 = "ZONE  ·  MARCH 27 & 28, 2026";
                col1  = U(CDkGrey);
            }
            else if (isEv)
            {
                line1 = $"DAY {evDay} — IN PROGRESS";
                line2 = evDay == 1 ? "MARCH 27, 2026" : "MARCH 28, 2026";
                col1  = U(CRed);
            }
            else
            {
                line1 = "EVENT CONCLUDED";
                line2 = "THANK YOU FOR ATTENDING  ·  ZONE 2026";
                col1  = U(CDkGrey);
            }

            var sp = ImGui.GetCursorScreenPos();
            const float H = 54f;

            dl.AddRectFilled(sp, sp + new Vector2(availW, H), U(new Vector4(0.05f, 0.01f, 0.01f, 1f)));
            if (isEv) dl.AddRectFilled(sp, sp + new Vector2(3f, H), U(CRed));

            var  s1 = ImGui.CalcTextSize(line1);
            var  s2 = ImGui.CalcTextSize(line2);
            float ty = sp.Y + (H - s1.Y - 4f - s2.Y) * 0.5f;
            dl.AddText(new Vector2(sp.X + 16f, ty),           col1,      line1);
            dl.AddText(new Vector2(sp.X + 16f, ty + s1.Y + 4f), U(CDkGrey), line2);

            if (isEv)
            {
                float blink = (float)(Math.Sin(ImGui.GetTime() * 3.0) * 0.35 + 0.65);
                dl.AddCircleFilled(new Vector2(sp.X + availW - 20f, sp.Y + H * 0.5f),
                                   4.5f, WithAlpha(U(CRed), (byte)(blink * 255)));
            }

            DrawHudRect(dl, sp, sp + new Vector2(availW, H), 8f, U(new Vector4(0.25f, 0f, 0f, 0.8f)), 1f);
            ImGui.Dummy(new Vector2(availW, H));
        }

        ImGui.Spacing();

        var live = _performances.Find(p => p.IsLive);

        Performance? nextDj;
        if (evDay.HasValue)
        {
            var perfsToday = _performances
                .FindAll(p => p.Day == evDay.Value)
                .OrderBy(p => EventMinutes(p.StartTime))
                .ToList();

            if (live != null)
                nextDj = perfsToday.FirstOrDefault(p => !p.IsLive && EventMinutes(p.StartTime) > EventMinutes(live.StartTime));
            else
            {
                int nowMin = (int)now.TimeOfDay.TotalMinutes;
                if (nowMin < 360) nowMin += 1440;
                nextDj = perfsToday.FirstOrDefault(p => EventMinutes(p.StartTime) > nowMin);
            }
        }
        else
        {
            nextDj = _performances
                .FindAll(p => p.Day == 1)
                .OrderBy(p => EventMinutes(p.StartTime))
                .FirstOrDefault();
        }

        if (live != null)
        {
            DrawSectionHeader("NOW PLAYING");
            ImGui.Spacing();
            DrawLiveNowCard(live);
            ImGui.Spacing();
        }

        if (nextDj != null)
        {
            DrawSectionHeader("UP NEXT");
            ImGui.Spacing();
            DrawHomeNextDjCard(nextDj);
            ImGui.Spacing();
        }

        DrawHomeSyncSection();
        ImGui.Spacing();

        {
            const float btnH = 42f;
            const float gap  = 8f;
            float btnW = (availW - gap * 2f) / 3f;
            var   lp   = ImGui.GetCursorPos();

            if (DrawHudButton("##tpBtn",    "TELEPORT TO EVENT", new Vector2(lp.X,                     lp.Y), new Vector2(btnW, btnH), primary: true))
                TeleportToEvent();
            if (DrawHudButton("##webBtn",   "WEBSITE",           new Vector2(lp.X + btnW + gap,         lp.Y), new Vector2(btnW, btnH), primary: false))
                Dalamud.Utility.Util.OpenLink(EventWebsiteUrl);
            if (DrawHudButton("##merchBtn", "DOWNLOAD MERCH",    new Vector2(lp.X + (btnW + gap) * 2f,  lp.Y), new Vector2(btnW, btnH), primary: false))
                Dalamud.Utility.Util.OpenLink(EventMerchUrl);

            ImGui.Dummy(new Vector2(availW, btnH));
        }

        ImGui.Spacing();

        var presentedBy = _partners.FindAll(p =>
            p.Name is "ENVY" or "Urban" or "Phoenix Nights" or "Project XIV" or "Nocturn");

        if (presentedBy.Count > 0)
        {
            DrawSectionHeader("PRESENTED BY");
            ImGui.Spacing();

            const float logoH   = 52f;
            const float logoW   = 80f;
            const float logoGap = 16f;
            float       totalW  = presentedBy.Count * logoW + (presentedBy.Count - 1) * logoGap;
            float       startX  = ImGui.GetCursorScreenPos().X + (availW - totalW) * 0.5f;
            var         baseY   = ImGui.GetCursorScreenPos().Y;

            for (int i = 0; i < presentedBy.Count; i++)
            {
                var   p  = presentedBy[i];
                float x  = startX + i * (logoW + logoGap);

                if (!string.IsNullOrWhiteSpace(p.LogoPath) && File.Exists(p.LogoPath))
                {
                    if (!_texCache.TryGetValue(p.LogoPath, out var shared))
                    {
                        shared = Plugin.TextureProvider.GetFromFile(new FileInfo(p.LogoPath));
                        _texCache[p.LogoPath] = shared;
                    }
                    var wrap = shared.GetWrapOrDefault();
                    if (wrap != null)
                    {
                        float ratio = wrap.Size.X / wrap.Size.Y;
                        float iw    = MathF.Min(logoW, logoH * ratio);
                        float ih    = iw / ratio;
                        float ix    = x + (logoW - iw) * 0.5f;
                        float iy    = baseY + (logoH - ih) * 0.5f;
                        dl.AddImage(wrap.Handle, new Vector2(ix, iy), new Vector2(ix + iw, iy + ih),
                                    Vector2.Zero, Vector2.One, 0xCCFFFFFF);
                    }
                }
            }

            ImGui.Dummy(new Vector2(availW, logoH));
            ImGui.Spacing();

            float btnW2 = ImGui.CalcTextSize("IN COLLABORATION WITH OUR PARTNERS").X + 32f;
            var   lp2   = ImGui.GetCursorPos();
            if (DrawHudButton("##partnerBtn", "IN COLLABORATION WITH OUR PARTNERS",
                              new Vector2(lp2.X + (availW - btnW2) * 0.5f, lp2.Y),
                              new Vector2(btnW2, 34f), primary: false))
                _activeTab = 5;
            ImGui.Dummy(new Vector2(availW, 34f));
            ImGui.Spacing();
        }

        {
            int qDay      = evDay ?? (now.Date < day1 ? 1 : 2);
            var actsToday = _activities.FindAll(a => a.Day == qDay);
            actsToday.Sort((a, b) => string.Compare(a.StartTime, b.StartTime, StringComparison.Ordinal));

            DrawSectionHeader("ACTIVITIES");
            ImGui.Spacing();

            if (actsToday.Count == 0)
            {
                var sp = ImGui.GetCursorScreenPos();
                const float H = 52f;
                dl.AddRectFilled(sp, sp + new Vector2(availW, H), U(new Vector4(0.05f, 0.02f, 0.02f, 1f)), 4f);
                DrawHudRect(dl, sp + Vector2.One, sp + new Vector2(availW, H) - Vector2.One,
                            6f, U(new Vector4(0.22f, 0f, 0f, 0.6f)), 1f);
                const string msg = "NO ACTIVITIES SCHEDULED AT THIS TIME";
                var ts = ImGui.CalcTextSize(msg);
                dl.AddText(new Vector2(sp.X + (availW - ts.X) * 0.5f, sp.Y + (H - ts.Y) * 0.5f), U(CDkGrey), msg);
                ImGui.Dummy(new Vector2(availW, H));
            }
            else
            {
                foreach (var act in actsToday)
                {
                    using var bg   = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.05f, 0.02f, 0.02f, 1f));
                    using var bsz  = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
                    using var card = ImRaii.Child($"hAct{act.Id}", new Vector2(-1, 56f), false);
                    if (!card) continue;

                    var cp  = ImGui.GetWindowPos();
                    var csz = ImGui.GetWindowSize();
                    var cdl = ImGui.GetWindowDrawList();

                    DrawHudRect(cdl, cp + Vector2.One, cp + csz - Vector2.One,
                                6f, U(new Vector4(0.22f, 0f, 0f, 0.6f)), 1f);

                    float lh   = ImGui.GetTextLineHeightWithSpacing();
                    float txtY = (56f - lh * 2f) * 0.5f;

                    ImGui.SetCursorPos(new Vector2(12f, txtY));
                    ImGui.TextColored(CWhite, act.Name.ToUpperInvariant());
                    ImGui.SetCursorPos(new Vector2(12f, txtY + lh));

                    string sub = act.StartTime + "  ·  ST";
                    if (!string.IsNullOrWhiteSpace(act.LocationName))
                        sub += $"  ·  {act.LocationName}";
                    ImGui.TextColored(CDkGrey, sub);
                }
            }
        }

    }

    private static void DrawHomeInfoPanel(float splitX)
    {
        const float panelH = 110f;
        float divX = splitX + 10f;

        var   dl     = ImGui.GetWindowDrawList();
        var   sp     = ImGui.GetCursorScreenPos();
        var   lp     = ImGui.GetCursorPos();
        float availW = ImGui.GetContentRegionAvail().X;

        dl.AddRectFilled(sp, sp + new Vector2(availW, panelH),
                         U(new Vector4(0.05f, 0.02f, 0.02f, 1f)), 4f);
        DrawHudRect(dl, sp + Vector2.One, sp + new Vector2(availW, panelH) - Vector2.One,
                    6f, U(new Vector4(0.28f, 0f, 0f, 0.8f)), 1f);

        dl.AddLine(new Vector2(sp.X + divX, sp.Y + 14f),
                   new Vector2(sp.X + divX, sp.Y + panelH - 14f),
                   U(new Vector4(0.25f, 0f, 0f, 0.9f)), 1f);

        ImGui.SetCursorPos(new Vector2(lp.X + 14f, lp.Y + 12f));
        ImGui.PushTextWrapPos(splitX - 6f);
        ImGui.TextColored(new Vector4(0.50f, 0.50f, 0.50f, 1f), EventDescription);
        ImGui.PopTextWrapPos();

        float rightStart = lp.X + divX + 12f;
        float rightW     = availW - divX - 20f;
        const float btnH = 38f, gap = 8f;
        float btnW = (rightW - gap) * 0.5f;
        float btnY = lp.Y + (panelH - btnH) * 0.5f;

        if (DrawHudButton("##tpBtn",  "TELEPORT TO EVENT", new Vector2(rightStart,          btnY), new Vector2(btnW, btnH), primary: true))
            TeleportToEvent();
        if (DrawHudButton("##webBtn", "WEBSITE",          new Vector2(rightStart + btnW + gap, btnY), new Vector2(btnW, btnH), primary: false))
            Dalamud.Utility.Util.OpenLink(EventWebsiteUrl);

        ImGui.SetCursorPos(new Vector2(lp.X, lp.Y + panelH));
    }

    private static void DrawHomeSyncSection()
    {
        DrawSectionHeader("SYNCSHELL");
        ImGui.Spacing();

        float availW = ImGui.GetContentRegionAvail().X;
        const float gap   = 8f;
        const float cardH = 88f;
        float cardW = MathF.Floor((availW - gap * 2f) / 3f);

        DrawSyncCard("LIGHTLESS SYNCSHELL", LightlessId, LightlessPass, "syncLS", cardW, cardH);
        ImGui.SameLine(0, gap);
        DrawSyncCard("RAVA SYNCSHELL",      RavaId,      RavaPass,      "syncRV", cardW, cardH);
        ImGui.SameLine(0, gap);
        DrawSyncCard("PLAYERSYNC",          PlayerSyncId, PlayerSyncPass, "syncPS", cardW, cardH);
    }

    private static void DrawSyncCard(string label, string id, string pass, string uid,
                                      float cardW, float cardH)
    {
        using var bg     = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.06f, 0.05f, 0.05f, 1f));
        using var border = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
        using var card   = ImRaii.Child(uid, new Vector2(cardW, cardH), false);
        if (!card) return;

        var cpos = ImGui.GetWindowPos();
        var csz  = ImGui.GetWindowSize();
        var cdl  = ImGui.GetWindowDrawList();

        const float hdrH = 26f;
        cdl.AddRectFilled(cpos, cpos + new Vector2(csz.X, hdrH),
                          U(new Vector4(0.10f, 0.02f, 0.02f, 1f)));
        cdl.AddLine(cpos + new Vector2(0, hdrH), cpos + new Vector2(csz.X, hdrH),
                    U(new Vector4(0.38f, 0f, 0f, 0.9f)), 1f);

        var   lblSz = ImGui.CalcTextSize(label);
        float lblX  = (csz.X - lblSz.X) * 0.5f;
        float lblY  = (hdrH  - lblSz.Y) * 0.5f;
        cdl.AddText(cpos + new Vector2(lblX, lblY), U(CWhite), label);

        float lineH = ImGui.GetTextLineHeightWithSpacing();
        float idY   = hdrH + (cardH - hdrH - lineH - 28f) * 0.5f;

        ImGui.SetCursorPos(new Vector2(10f, idY));
        ImGui.TextColored(CDkGrey, "ID:");
        ImGui.SameLine(0, 5);
        ImGui.TextColored(CGrey, id);

        const float btnH  = 22f;
        const float margin = 8f;
        float btnY  = cardH - btnH - margin;
        float halfW = (csz.X - margin * 2f - 4f) * 0.5f;

        if (DrawHudButton($"##ci{uid}", "COPY ID",   new Vector2(margin,              btnY), new Vector2(halfW, btnH), primary: false))
            ImGui.SetClipboardText(id);

        if (DrawHudButton($"##cp{uid}", "COPY PASS", new Vector2(margin + halfW + 4f, btnY), new Vector2(halfW, btnH), primary: false))
            ImGui.SetClipboardText(pass);

        DrawHudRect(cdl, cpos + Vector2.One, cpos + csz - Vector2.One,
                    5f, U(new Vector4(0.25f, 0f, 0f, 0.7f)), 1f);
    }

    private static bool DrawHudButton(string id, string label, Vector2 localPos, Vector2 size, bool primary)
    {
        ImGui.SetCursorPos(localPos);
        ImGui.InvisibleButton(id, size);
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();

        var dl  = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        uint fill = primary
            ? (hovered ? U(new Vector4(0.50f, 0f,    0f,    1f)) : U(new Vector4(0.28f, 0f,    0f,    0.9f)))
            : (hovered ? U(new Vector4(0.16f, 0.05f, 0.05f, 1f)) : U(new Vector4(0.07f, 0.02f, 0.02f, 0.9f)));

        dl.AddRectFilled(min, max, fill, 3f);

        DrawHudRect(dl, min, max, 4f,
            primary ? U(hovered ? CBrRed : new Vector4(0.55f, 0f, 0f, 1f))
                    : U(new Vector4(0.28f, 0.04f, 0.04f, 0.8f)),
            1f, glow: hovered && primary);

        var   textSz = ImGui.CalcTextSize(label);
        var   center = (min + max) * 0.5f;
        uint  textCol = hovered    ? 0xFFFFFFFF
                      : primary    ? U(new Vector4(0.88f, 0.88f, 0.88f, 1f))
                      :              U(new Vector4(0.50f, 0.50f, 0.50f, 1f));
        dl.AddText(center - textSz * 0.5f, textCol, label);

        return clicked;
    }

    private static void TeleportToEvent()
    {
        try
        {
            // Let Lifestream resolve "Raiden" to the correct world ID
            var address = Plugin.PluginInterface
                  .GetIpcSubscriber<string, string, string, string, bool, bool, (string, int, int, int, int, int, int, bool, bool, string)>("Lifestream.BuildAddressBookEntry")
                  .InvokeFunc("Raiden", "Shirogane", TpWard.ToString(), TpPlot.ToString(), false, false);
            Plugin.PluginInterface
                  .GetIpcSubscriber<(string, int, int, int, int, int, int, bool, bool, string), object>("Lifestream.GoToHousingAddress")
                  .InvokeAction(address);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[Zone] Lifestream IPC failed");
            Plugin.Notifications.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
            {
                Title           = "ZONE",
                Content         = "Lifestream is not installed or unavailable.",
                Type            = Dalamud.Interface.ImGuiNotification.NotificationType.Warning,
                InitialDuration = TimeSpan.FromSeconds(4)
            });
        }
    }

    private void DrawHomeNextDjCard(Performance p)
    {
        const float cardH  = 72f;
        const float imgSz  = 52f;
        const float imgPad = 10f;

        using var bg     = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.07f, 0.07f, 0.07f, 1f));
        using var border = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
        using var card   = ImRaii.Child($"nextDj{p.Id}", new Vector2(-1, cardH), false);
        if (!card) return;

        var cdl  = ImGui.GetWindowDrawList();
        var cpos = ImGui.GetWindowPos();
        var csz  = ImGui.GetWindowSize();

        DrawHudRect(cdl, cpos + Vector2.One, cpos + csz - Vector2.One,
                    6f, U(new Vector4(0.22f, 0f, 0f, 0.6f)), 1f);

        ImGui.SetCursorPos(new Vector2(imgPad, (cardH - imgSz) * 0.5f));
        DrawTextureOrPlaceholder(p.LogoPath, new Vector2(imgSz, imgSz));

        float textX = imgPad + imgSz + 12f;
        float lh    = ImGui.GetTextLineHeightWithSpacing();
        float textY = (cardH - lh * 2f) * 0.5f;

        ImGui.SetCursorPos(new Vector2(textX, textY));
        ImGui.TextColored(CWhite, p.DjName.ToUpperInvariant());
        ImGui.SetCursorPos(new Vector2(textX, textY + lh));
        ImGui.TextColored(CDkGrey, $"{p.StartTime}  –  {p.EndTime}  ·  ST");
    }

    private void DrawAmbianceTab()
    {
        var   dl     = ImGui.GetWindowDrawList();
        var   origin = ImGui.GetCursorScreenPos();
        var   lp     = ImGui.GetCursorPos();
        float availW = ImGui.GetContentRegionAvail().X;
        float availH = ImGui.GetContentRegionAvail().Y;
        bool  active = Plugin.TimeLock.IsEnabled;

        uint scanCol = WithAlpha(U(CRed), 0x07);
        for (float oy = 0; oy < availH; oy += 8f)
            dl.AddLine(new Vector2(origin.X, origin.Y + oy),
                       new Vector2(origin.X + availW, origin.Y + oy), scanCol, 1f);

        const float M = 14f, BL = 22f;
        const float BThk = 1.5f;
        uint bCol = WithAlpha(U(CRed), 0x55);
        dl.AddLine(new Vector2(origin.X + M,          origin.Y + M),          new Vector2(origin.X + M + BL, origin.Y + M),          bCol, BThk);
        dl.AddLine(new Vector2(origin.X + M,          origin.Y + M),          new Vector2(origin.X + M,       origin.Y + M + BL),     bCol, BThk);
        dl.AddLine(new Vector2(origin.X + availW - M, origin.Y + M),          new Vector2(origin.X + availW - M - BL, origin.Y + M),  bCol, BThk);
        dl.AddLine(new Vector2(origin.X + availW - M, origin.Y + M),          new Vector2(origin.X + availW - M, origin.Y + M + BL),  bCol, BThk);
        dl.AddLine(new Vector2(origin.X + M,          origin.Y + availH - M), new Vector2(origin.X + M + BL, origin.Y + availH - M),  bCol, BThk);
        dl.AddLine(new Vector2(origin.X + M,          origin.Y + availH - M), new Vector2(origin.X + M,      origin.Y + availH - M - BL), bCol, BThk);
        dl.AddLine(new Vector2(origin.X + availW - M, origin.Y + availH - M), new Vector2(origin.X + availW - M - BL, origin.Y + availH - M),     bCol, BThk);
        dl.AddLine(new Vector2(origin.X + availW - M, origin.Y + availH - M), new Vector2(origin.X + availW - M,      origin.Y + availH - M - BL), bCol, BThk);

        uint cLbl = WithAlpha(U(CRed), 0x28);
        float fh = ImGui.GetFontSize();
        dl.AddText(new Vector2(origin.X + M + BL + 6f,      origin.Y + M - 1f),            cLbl, "SYS");
        dl.AddText(new Vector2(origin.X + availW - M - BL - 26f, origin.Y + M - 1f),       cLbl, "ATM");
        dl.AddText(new Vector2(origin.X + M + BL + 6f,      origin.Y + availH - M - fh),   cLbl, "V1");
        dl.AddText(new Vector2(origin.X + availW - M - BL - 30f, origin.Y + availH - M - fh), cLbl, "CTRL");

        const float PW = 370f, PH = 200f;
        const float HDRH = 30f, FTNH = 24f;
        float px   = origin.X + (availW - PW) * 0.5f;
        float py   = origin.Y + (availH - PH) * 0.5f;
        var   pMin = new Vector2(px, py);
        var   pMax = new Vector2(px + PW, py + PH);

        dl.AddRectFilled(pMin, pMax, U(new Vector4(0.04f, 0.01f, 0.01f, 0.98f)));

        dl.AddRectFilled(pMin, new Vector2(pMax.X, py + HDRH), U(new Vector4(0.09f, 0.02f, 0.02f, 1f)));
        dl.AddLine(new Vector2(px, py + HDRH), new Vector2(pMax.X, py + HDRH),
                   active ? U(CRed) : U(new Vector4(0.22f, 0f, 0f, 1f)), 1f);

        const string hdrText = "ATMOSPHERE CONTROL";
        var hdrSz = ImGui.CalcTextSize(hdrText);
        dl.AddText(new Vector2(px + (PW - hdrSz.X) * 0.5f, py + (HDRH - hdrSz.Y) * 0.5f),
                   active ? U(CBrRed) : U(CDkGrey), hdrText);

        float dotX = px + 14f, dotY = py + HDRH * 0.5f;
        float blink = active ? (float)(Math.Sin(ImGui.GetTime() * 3.5) * 0.35 + 0.65) : 1f;
        uint dotCol = active ? WithAlpha(U(CRed), (byte)(blink * 255)) : U(new Vector4(0.22f, 0.22f, 0.22f, 1f));
        dl.AddCircleFilled(new Vector2(dotX, dotY), 4f, dotCol);
        if (active)
            dl.AddCircle(new Vector2(dotX, dotY), 7.5f, WithAlpha(U(CRed), 0x35), 0, 1f);

        dl.AddRectFilled(new Vector2(px, py + PH - FTNH), pMax, U(new Vector4(0.06f, 0.01f, 0.01f, 1f)));
        dl.AddLine(new Vector2(px, py + PH - FTNH), new Vector2(pMax.X, py + PH - FTNH),
                   active ? U(new Vector4(0.22f, 0f, 0f, 1f)) : U(new Vector4(0.10f, 0f, 0f, 1f)), 1f);

        string statusStr = active ? "● ACTIVE" : "○ STANDBY";
        var    statusSz  = ImGui.CalcTextSize(statusStr);
        float  fty       = py + PH - FTNH + (FTNH - statusSz.Y) * 0.5f;
        dl.AddText(new Vector2(px + PW - statusSz.X - 14f, fty),
                   active ? U(CGreen) : U(CDkGrey), statusStr);
        dl.AddText(new Vector2(px + 14f, fty),
                   U(new Vector4(0.20f, 0.20f, 0.20f, 1f)), "EORZEA ATMOSPHERE");

        uint panelBorder = active ? U(CRed) : U(new Vector4(0.28f, 0f, 0f, 1f));
        DrawHudRect(dl, pMin, pMax, 12f, panelBorder, 1.5f, glow: active);

        float midY  = origin.Y + availH * 0.5f;
        uint  lnCol = WithAlpha(U(CRed), 0x25);
        uint  tkCol = WithAlpha(U(CRed), 0x50);
        dl.AddLine(new Vector2(origin.X + M + BL + 10f, midY), new Vector2(px - 18f, midY), lnCol, 1f);
        dl.AddLine(new Vector2(px - 18f, midY - 6f), new Vector2(px - 18f, midY + 6f), tkCol, 1.5f);
        dl.AddLine(new Vector2(px - 10f, midY - 3f), new Vector2(px - 10f, midY + 3f), tkCol, 1f);
        dl.AddLine(new Vector2(pMax.X + 18f, midY), new Vector2(origin.X + availW - M - BL - 10f, midY), lnCol, 1f);
        dl.AddLine(new Vector2(pMax.X + 18f, midY - 6f), new Vector2(pMax.X + 18f, midY + 6f), tkCol, 1.5f);
        dl.AddLine(new Vector2(pMax.X + 10f, midY - 3f), new Vector2(pMax.X + 10f, midY + 3f), tkCol, 1f);

        float lpx    = lp.X + (availW - PW) * 0.5f;
        float lpy    = lp.Y + (availH - PH) * 0.5f + HDRH;
        float innerH = PH - HDRH - FTNH;

        const float TGLW  = 80f;
        string statusLabel = active ? "ZONE MODE ACTIVE" : "ZONE MODE OFF";
        float  labelW      = ImGui.CalcTextSize(statusLabel).X;
        float  rowW        = TGLW + ImGui.GetStyle().ItemSpacing.X + labelW;
        float  toggleX     = lpx + (PW - rowW) * 0.5f;
        float  toggleY     = lpy + innerH * 0.38f - 20f;

        bool inHousing = Plugin.TimeLock.IsHousingInterior;

        ImGui.SetCursorPos(new Vector2(toggleX, toggleY));
        if (inHousing) ImGui.BeginDisabled();
        if (DrawDayNightToggle(active, "##timeLock"))
            Plugin.TimeLock.SetEnabled(!active);
        if (inHousing) ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.SetCursorPosY(toggleY + 11f);
        ImGui.TextColored(inHousing || !active ? CDkGrey : CBrRed, statusLabel);

        string desc = inHousing
            ? "Unavailable inside housing interiors."
            : active
                ? "Eorzea time locked to midnight. Clear skies active."
                : "Locks Eorzea time to midnight and clears the sky.";
        float  descW  = ImGui.CalcTextSize(desc).X;
        const float DM = 20f;
        float  descX  = lpx + MathF.Max((PW - descW) * 0.5f, DM);
        ImGui.PushTextWrapPos(lpx + PW - DM);
        ImGui.SetCursorPos(new Vector2(descX, toggleY + 46f));
        ImGui.TextColored(inHousing ? new Vector4(CGrey.X, CGrey.Y, CGrey.Z, 0.33f) : CGrey, desc);
        ImGui.PopTextWrapPos();
    }

    private void DrawLineupTab()
    {
        var live = _performances.Find(p => p.IsLive);
        if (live != null)
        {
            ImGui.Spacing();
            DrawLiveNowCard(live);
        }

        ImGui.Spacing();
        DrawDaySelector(ref _lineupDay);
        ImGui.Spacing();

        var today = _performances.FindAll(p => p.Day == _lineupDay);
        today.Sort((a, b) => EventMinutes(a.StartTime).CompareTo(EventMinutes(b.StartTime)));

        using var scroll = ImRaii.Child("LineupScroll", Vector2.Zero, false);
        if (!scroll) return;

        var now = DateTime.UtcNow;
        var today2 = now.Date;
        // Day 1 = 27 March, Day 2 = 28 March
        var eventDate = _lineupDay == 1
            ? new DateTime(2026, 3, 27)
            : new DateTime(2026, 3, 28);
        bool isEventDay = today2 == eventDate;

        const float cardH = 96f;
        const float imgSz = 72f;
        const float imgPad = 10f;
        const float textX = imgPad + imgSz + 12f;

        foreach (var p in today)
        {
            ImGui.PushID(p.Id);
            try
            {
                bool isPast = isEventDay && TimeSpan.TryParse(p.EndTime, out var endT) && endT < now.TimeOfDay;

                using var alpha  = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.38f, isPast);
                using var bg     = ImRaii.PushColor(ImGuiCol.ChildBg, p.IsLive
                    ? new Vector4(0.10f, 0.01f, 0.01f, 1f)
                    : new Vector4(0.07f, 0.07f, 0.07f, 1f));
                using var border = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);

                using (var card = ImRaii.Child($"card{p.Id}", new Vector2(-1, cardH), false))
                {
                    if (card)
                    {
                        var cdl  = ImGui.GetWindowDrawList();
                        var cpos = ImGui.GetWindowPos();
                        var csz  = ImGui.GetWindowSize();

                        uint frameCol = p.IsLive ? U(CRed) : U(new Vector4(0.28f, 0f, 0f, 0.9f));
                        DrawHudRect(cdl, cpos + new Vector2(1, 1), cpos + csz - new Vector2(1, 1),
                                    8f, frameCol, p.IsLive ? 1.5f : 1f, glow: p.IsLive);

                        ImGui.SetCursorPos(new Vector2(imgPad, (cardH - imgSz) / 2f));
                        DrawTextureOrPlaceholder(p.LogoPath, new Vector2(imgSz, imgSz));

                        float lineH = ImGui.GetTextLineHeightWithSpacing();
                        float textStartY = (cardH - lineH * 2f - 2f) / 2f;

                        ImGui.SetCursorPos(new Vector2(textX, textStartY));
                        ImGui.TextColored(CWhite, p.DjName.ToUpperInvariant());

                        ImGui.SetCursorPos(new Vector2(textX, textStartY + lineH));
                        ImGui.TextColored(CDkGrey, $"{p.StartTime}  –  {p.EndTime}  ·  ST");

                        if (p.IsLive)
                        {
                            ImGui.SameLine();
                            float a = (float)(Math.Sin(ImGui.GetTime() * 4) * 0.5 + 0.5);
                            ImGui.TextColored(new Vector4(1f, 0.10f, 0.10f, a), "  ● LIVE");
                        }

                        if (!string.IsNullOrWhiteSpace(p.StreamUrl))
                        {
                            const string btnLbl = "WATCH";
                            float btnW = ImGui.CalcTextSize(btnLbl).X + 18f;
                            float btnH2 = ImGui.GetTextLineHeight() + 10f;
                            float btnX = csz.X - btnW - imgPad;
                            float btnY = (cardH - btnH2) / 2f;
                            ImGui.SetCursorPos(new Vector2(btnX, btnY));
                            if (ImGui.Button(btnLbl, new Vector2(btnW, btnH2)))
                                Dalamud.Utility.Util.OpenLink(p.StreamUrl);
                        }
                    }
                }
            }
            finally
            {
                ImGui.PopID();
            }
            ImGui.Spacing();
        }

        if (today.Count == 0)
            DrawEmptyState("NO PERFORMANCES SCHEDULED");
    }

    private void DrawLiveNowCard(Performance live)
    {
        const float heroH  = 120f;
        const float heroImg = 96f;
        using var heroBg     = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.09f, 0.01f, 0.01f, 1f));
        using var heroBorder = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
        using var card       = ImRaii.Child("liveHero", new Vector2(-1, heroH), false);
        if (!card) return;

        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var sz  = ImGui.GetWindowSize();

        DrawHudRect(dl, pos + new Vector2(1, 1), pos + sz - new Vector2(1, 1),
                    10f, U(CRed), 1.8f, glow: true);

        ImGui.SetCursorPos(new Vector2(10f, (heroH - heroImg) / 2f));
        DrawTextureOrPlaceholder(live.LogoPath, new Vector2(heroImg, heroImg));

        float htextX = 10f + heroImg + 14f;
        float lineH  = ImGui.GetTextLineHeightWithSpacing();
        float textStartY = (heroH - lineH * 3f - 4f) / 2f;

        float blink = (float)(Math.Sin(ImGui.GetTime() * 4) * 0.5 + 0.5);
        ImGui.SetCursorPos(new Vector2(htextX, textStartY));
        ImGui.TextColored(new Vector4(1f, 0.12f, 0.12f, blink), "● LIVE NOW");

        ImGui.SetCursorPos(new Vector2(htextX, textStartY + lineH));
        ImGui.TextColored(CWhite, live.DjName.ToUpperInvariant());

        ImGui.SetCursorPos(new Vector2(htextX, textStartY + lineH * 2f));
        ImGui.TextColored(CDkGrey, $"{live.StartTime}  –  {live.EndTime}  ·  ST");

        if (!string.IsNullOrWhiteSpace(live.StreamUrl))
        {
            float btnW = ImGui.CalcTextSize("WATCH STREAM").X + 24f;
            const float btnH = 32f;
            float btnX = sz.X - btnW - 10f;
            float btnY = (heroH - btnH) / 2f;
            ImGui.SetCursorPos(new Vector2(btnX, btnY));
            if (ImGui.Button("WATCH STREAM", new Vector2(btnW, btnH)))
                Dalamud.Utility.Util.OpenLink(live.StreamUrl);
        }
    }

    private void DrawActivitiesTab()
    {
        ImGui.Spacing();
        DrawDaySelector(ref _activityDay);
        ImGui.Spacing();

        var today = _activities.FindAll(a => a.Day == _activityDay);
        today.Sort((a, b) => string.Compare(a.StartTime, b.StartTime, StringComparison.Ordinal));

        using var scroll = ImRaii.Child("ActScroll", Vector2.Zero, false);
        if (!scroll) return;

        var now = DateTime.UtcNow.TimeOfDay;
        foreach (var act in today)
        {
            ImGui.PushID(act.Id);
            try
            {
                bool isNow = false, isPast = false;
                if (TimeSpan.TryParse(act.StartTime, out var aStart))
                {
                    var dur = TimeSpan.FromHours(1);
                    isNow  = aStart <= now && now <= aStart + dur;
                    isPast = now > aStart + dur;
                }

                using var actAlpha  = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.38f, isPast);
                using var actBg     = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.08f, 0.08f, 0.08f, 1f));

                using (var card = ImRaii.Child($"act{act.Id}", new Vector2(-1, 60), true))
                {
                    if (card)
                    {
                        ImGui.SetCursorPosX(10f);
                        ImGui.TextColored(CDkGrey, act.StartTime);
                        ImGui.SameLine(70 * ImGuiHelpers.GlobalScale);
                        ImGui.TextColored(CWhite, act.Name.ToUpperInvariant());
                        if (isNow) { ImGui.SameLine(); ImGui.TextColored(CGreen, "  ● NOW"); }

                        float rightX = ImGui.GetWindowWidth() - 150 * ImGuiHelpers.GlobalScale;
                        ImGui.SameLine(rightX);
                        ImGui.TextColored(CDkGrey, act.LocationName ?? "");

                        ImGui.SetCursorPosX(70 * ImGuiHelpers.GlobalScale);
                        if (!string.IsNullOrWhiteSpace(act.Description))
                            ImGui.TextColored(CGrey, act.Description.Length > 50
                                ? act.Description[..50] + "…" : act.Description);

                        ImGui.SameLine(rightX);
                        if (ImGui.SmallButton("TAKE ME THERE")) HandleTakeMeThere(act);
                    }
                }
            }
            finally
            {
                ImGui.PopID();
            }
            ImGui.Spacing();
        }

        if (today.Count == 0)
            DrawEmptyState("NO ACTIVITIES SCHEDULED");
    }

    private void HandleTakeMeThere(Activity act)
    {
        if (!string.IsNullOrWhiteSpace(act.StreamUrl))
        {
            Dalamud.Utility.Util.OpenLink(act.StreamUrl);
            return;
        }

        if (act.TerritoryId.HasValue && act.CoordinateX.HasValue && act.CoordinateY.HasValue)
        {
            try
            {
                var payload = new Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload(
                    (uint)act.TerritoryId.Value, (uint)act.TerritoryId.Value,
                    act.CoordinateX.Value, act.CoordinateY.Value);
                Plugin.GameGui.OpenMapWithMapLink(payload);
                return;
            }
            catch (Exception ex) { Plugin.Log.Warning(ex, "[Zone] Failed to open map link"); }
        }

        Plugin.Notifications.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
        {
            Title           = "ZONE",
            Content         = "No location data configured for this activity.",
            Type            = Dalamud.Interface.ImGuiNotification.NotificationType.Warning,
            InitialDuration = TimeSpan.FromSeconds(4)
        });
    }

    private void DrawStaffTab()
    {
        var dl = ImGui.GetWindowDrawList();

        ImGui.Spacing();

        {
            using var framePad  = ImRaii.PushStyle(ImGuiStyleVar.FramePadding,  new Vector2(38f, 10f));
            using var frameRnd  = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f);
            using var frameBg   = ImRaii.PushColor(ImGuiCol.FrameBg,        new Vector4(0.05f, 0.01f, 0.01f, 1f));
            using var frameBgHv = ImRaii.PushColor(ImGuiCol.FrameBgHovered, new Vector4(0.09f, 0.02f, 0.02f, 1f));
            using var frameBgAc = ImRaii.PushColor(ImGuiCol.FrameBgActive,  new Vector4(0.09f, 0.02f, 0.02f, 1f));

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##staffSearch", "Search name or role", ref _staffSearch, 128);
        }

        // Border + loupe icon drawn after so they're on top
        var iMin = ImGui.GetItemRectMin();
        var iMax = ImGui.GetItemRectMax();
        dl.AddRect(iMin, iMax, U(new Vector4(0.35f, 0f, 0f, 1f)), 4f, ImDrawFlags.None, 1f);

        float midY  = (iMin.Y + iMax.Y) * 0.5f;
        float iconX = iMin.X + 16f;
        uint  iconC = string.IsNullOrEmpty(_staffSearch) ? U(new Vector4(0.30f, 0.10f, 0.10f, 1f)) : U(CRed);
        dl.AddCircle(new Vector2(iconX, midY - 1f), 5.5f, iconC, 0, 1.3f);
        dl.AddLine(new Vector2(iconX + 4f, midY + 3.5f), new Vector2(iconX + 8.5f, midY + 8f), iconC, 1.3f);

        ImGui.Spacing();

        var filter   = _staffSearch.Trim().ToLower();
        var filtered = _staff.FindAll(s =>
            string.IsNullOrEmpty(filter) ||
            s.CharacterName.ToLower().Contains(filter) ||
            s.Role.ToLower().Contains(filter));

        using var scroll = ImRaii.Child("StaffScroll", Vector2.Zero, false);
        if (!scroll) return;

        if (filtered.Count == 0)
        {
            DrawEmptyState("NO STAFF MEMBERS FOUND");
            return;
        }

        float cardW   = (ImGui.GetContentRegionAvail().X - 8f) / 2f;
        const float cardH  = 88f;
        const float imgSz  = 68f;
        const float pad    = 10f;

        for (int i = 0; i < filtered.Count; i++)
        {
            var s = filtered[i];
            ImGui.PushID(s.Id);
            try
            {
                if (i % 2 != 0) ImGui.SameLine(cardW + 8f);

                using var staffBg     = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.08f, 0.07f, 0.07f, 1f));
                using var staffBorder = ImRaii.PushStyle(ImGuiStyleVar.ChildBorderSize, 0f);
                using (var card = ImRaii.Child($"staff{s.Id}", new Vector2(cardW, cardH), false))
                {
                    if (card)
                    {
                        var cdl = ImGui.GetWindowDrawList();
                        var cp  = ImGui.GetWindowPos();
                        var csz = ImGui.GetWindowSize();

                        if (s.IsOnlineDetected)
                            cdl.AddRectFilled(cp, cp + new Vector2(3f, csz.Y), U(CGreen));

                        uint borderCol = s.IsOnlineDetected
                            ? U(new Vector4(0f, 0.45f, 0.12f, 0.55f))
                            : U(new Vector4(0.20f, 0f, 0f, 0.70f));
                        cdl.AddRect(cp + Vector2.One, cp + csz - Vector2.One, borderCol, 4f);

                        float imgX = s.IsOnlineDetected ? pad + 3f : pad;
                        float imgY = (cardH - imgSz) * 0.5f;
                        ImGui.SetCursorPos(new Vector2(imgX, imgY));
                        DrawTextureOrPlaceholder(s.AvatarPath, new Vector2(imgSz, imgSz));

                        float textX  = imgX + imgSz + pad;
                        var   roleCol = ParseColor(s.Color) ?? CRed;

                        float lineH  = ImGui.GetTextLineHeightWithSpacing();
                        float totalH = lineH * 2f + (s.IsOnlineDetected ? lineH : 0f);
                        float textY  = (cardH - totalH) * 0.5f;

                        ImGui.SetCursorPos(new Vector2(textX, textY));
                        ImGui.TextColored(CWhite, s.CharacterName);

                        ImGui.SetCursorPos(new Vector2(textX, textY + lineH));
                        ImGui.TextColored(roleCol, s.Role.ToUpperInvariant());

                        if (s.IsOnlineDetected)
                        {
                            ImGui.SetCursorPos(new Vector2(textX, textY + lineH * 2f));
                            ImGui.TextColored(CGreen, "● Online");
                        }

                        const string btnLabel = "VIEW PROFILE";
                        const string tgtLabel = "TARGET";
                        float btnW = Math.Max(ImGui.CalcTextSize(btnLabel).X, ImGui.CalcTextSize(tgtLabel).X) + 14f;
                        float btnH = ImGui.GetTextLineHeight() + 6f;
                        float btnX = csz.X - btnW - pad;
                        const float btnGap = 4f;
                        float totalBtnH = s.IsOnlineDetected ? btnH * 2 + btnGap : btnH;
                        float btnY = (cardH - totalBtnH) * 0.5f;

                        using var frameRound = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 3f);

                        ImGui.SetCursorPos(new Vector2(btnX, btnY));
                        {
                            using var btnCol = ImRaii.PushColor(ImGuiCol.Button,        new Vector4(0.18f, 0f, 0f, 0.9f));
                            using var btnHov = ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.55f, 0f, 0f, 1f));
                            if (ImGui.Button(btnLabel, new Vector2(btnW, btnH)))
                                _staffPopup.OpenFor(s);
                        }

                        if (s.IsOnlineDetected)
                        {
                            ImGui.SetCursorPos(new Vector2(btnX, btnY + btnH + btnGap));
                            using var tgtCol = ImRaii.PushColor(ImGuiCol.Button,        new Vector4(0f, 0.25f, 0.08f, 0.9f));
                            using var tgtHov = ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0f, 0.50f, 0.18f, 1f));
                            if (ImGui.Button(tgtLabel, new Vector2(btnW, btnH)))
                            {
                                var obj = Plugin.ObjectTable.FirstOrDefault(
                                    o => (int)o.ObjectKind == 1 && o.Name.TextValue == s.CharacterName);
                                if (obj != null) Plugin.TargetManager.Target = obj;
                            }
                        }
                    }
                }
            }
            finally
            {
                ImGui.PopID();
            }
            if (i % 2 == 0 && i == filtered.Count - 1) ImGui.NewLine();
        }
    }

    // Treats times before noon as next-day so midnight sets sort after late evening
    private static int EventMinutes(string t)
    {
        if (!TimeSpan.TryParse(t, out var ts)) return 0;
        int m = (int)ts.TotalMinutes;
        return m < 720 ? m + 1440 : m;
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
        dl.AddCircleFilled(new Vector2(x + cut,  y),   2f, dot);
        dl.AddCircleFilled(new Vector2(x2 - cut, y),   2f, dot);
        dl.AddCircleFilled(new Vector2(x2, y + cut),   2f, dot);
        dl.AddCircleFilled(new Vector2(x2, y2 - cut),  2f, dot);
        dl.AddCircleFilled(new Vector2(x2 - cut, y2),  2f, dot);
        dl.AddCircleFilled(new Vector2(x + cut,  y2),  2f, dot);
        dl.AddCircleFilled(new Vector2(x,  y2 - cut),  2f, dot);
        dl.AddCircleFilled(new Vector2(x,  y + cut),   2f, dot);
    }

    private static void DrawChamfered(ImDrawListPtr dl, Vector2 min, Vector2 max,
                                       float cut, uint color, float thickness)
    {
        float x = min.X, y = min.Y, x2 = max.X, y2 = max.Y;
        dl.AddLine(new Vector2(x + cut,  y),   new Vector2(x2 - cut, y),   color, thickness);
        dl.AddLine(new Vector2(x2 - cut, y),   new Vector2(x2, y + cut),   color, thickness);
        dl.AddLine(new Vector2(x2, y + cut),   new Vector2(x2, y2 - cut),  color, thickness);
        dl.AddLine(new Vector2(x2, y2 - cut),  new Vector2(x2 - cut, y2),  color, thickness);
        dl.AddLine(new Vector2(x2 - cut, y2),  new Vector2(x + cut,  y2),  color, thickness);
        dl.AddLine(new Vector2(x + cut,  y2),  new Vector2(x,  y2 - cut),  color, thickness);
        dl.AddLine(new Vector2(x,  y2 - cut),  new Vector2(x,  y + cut),   color, thickness);
        dl.AddLine(new Vector2(x,  y + cut),   new Vector2(x + cut,  y),   color, thickness);
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

    private void DrawDaySelector(ref int day)
    {
        var   dl    = ImGui.GetWindowDrawList();
        var   sp    = ImGui.GetCursorScreenPos();
        var   lp    = ImGui.GetCursorPos();
        float availW = ImGui.GetContentRegionAvail().X;
        const float H = 36f;
        float segW  = MathF.Floor(availW / 2f);

        dl.AddRectFilled(sp, sp + new Vector2(availW, H),
                         U(new Vector4(0.05f, 0.01f, 0.01f, 1f)));

        for (int i = 0; i < 2; i++)
        {
            float sx  = sp.X + i * segW;
            float lx  = lp.X + i * segW;
            bool  act = (day == i + 1);

            ImGui.SetCursorPos(new Vector2(lx, lp.Y));
            ImGui.InvisibleButton($"##ds{i}", new Vector2(segW, H));
            bool hov = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) day = i + 1;

            uint bg = act ? U(new Vector4(0.28f, 0f, 0f, 1f))
                    : hov ? U(new Vector4(0.12f, 0.03f, 0.03f, 1f))
                    : 0x00000000u;
            dl.AddRectFilled(new Vector2(sx, sp.Y), new Vector2(sx + segW, sp.Y + H), bg);

            if (act)
                dl.AddLine(new Vector2(sx + 10f, sp.Y + H - 2f),
                           new Vector2(sx + segW - 10f, sp.Y + H - 2f),
                           U(CRed), 2.5f);

            string lbl = $"DAY {i + 1}";
            var    sz  = ImGui.CalcTextSize(lbl);
            uint   tc  = act ? 0xFFFFFFFF
                       : hov ? U(new Vector4(0.75f, 0.75f, 0.75f, 1f))
                       :       U(CDkGrey);
            dl.AddText(new Vector2(sx + (segW - sz.X) * 0.5f, sp.Y + (H - sz.Y) * 0.5f), tc, lbl);

            if (i == 0)
                dl.AddLine(new Vector2(sx + segW, sp.Y + 8f),
                           new Vector2(sx + segW, sp.Y + H - 8f),
                           U(new Vector4(0.22f, 0f, 0f, 1f)), 1f);
        }

        DrawHudRect(dl, sp, sp + new Vector2(availW, H), 6f,
                    U(new Vector4(0.30f, 0f, 0f, 1f)), 1f, glow: false);

        ImGui.SetCursorPos(new Vector2(lp.X, lp.Y + H + 4f));
    }

    private static void DrawEmptyState(string message)
    {
        var   dl    = ImGui.GetWindowDrawList();
        var   sp    = ImGui.GetCursorScreenPos();
        float availW = ImGui.GetContentRegionAvail().X;
        float availH = ImGui.GetContentRegionAvail().Y;

        const float M = 18f, BL = 16f;
        uint bCol = WithAlpha(U(CRed), 0x28);
        dl.AddLine(new Vector2(sp.X + M,          sp.Y + M),          new Vector2(sp.X + M + BL, sp.Y + M),          bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + M,          sp.Y + M),          new Vector2(sp.X + M,       sp.Y + M + BL),     bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + availW - M, sp.Y + M),          new Vector2(sp.X + availW - M - BL, sp.Y + M),  bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + availW - M, sp.Y + M),          new Vector2(sp.X + availW - M, sp.Y + M + BL),  bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + M,          sp.Y + availH - M), new Vector2(sp.X + M + BL, sp.Y + availH - M),  bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + M,          sp.Y + availH - M), new Vector2(sp.X + M,      sp.Y + availH - M - BL), bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + availW - M, sp.Y + availH - M), new Vector2(sp.X + availW - M - BL, sp.Y + availH - M),     bCol, 1.2f);
        dl.AddLine(new Vector2(sp.X + availW - M, sp.Y + availH - M), new Vector2(sp.X + availW - M,      sp.Y + availH - M - BL), bCol, 1.2f);

        var   sz    = ImGui.CalcTextSize(message);
        float cx    = sp.X + (availW - sz.X) * 0.5f;
        float cy    = sp.Y + (availH - sz.Y) * 0.5f;
        float lineY = cy + sz.Y * 0.5f;
        float gap   = 14f;
        uint  lnCol = WithAlpha(U(CRed), 0x22);
        uint  tkCol = WithAlpha(U(CRed), 0x45);

        dl.AddLine(new Vector2(sp.X + M + BL + 10f, lineY), new Vector2(cx - gap, lineY), lnCol, 1f);
        dl.AddLine(new Vector2(cx - gap, lineY - 5f), new Vector2(cx - gap, lineY + 5f), tkCol, 1.5f);
        dl.AddLine(new Vector2(cx + sz.X + gap, lineY), new Vector2(sp.X + availW - M - BL - 10f, lineY), lnCol, 1f);
        dl.AddLine(new Vector2(cx + sz.X + gap, lineY - 5f), new Vector2(cx + sz.X + gap, lineY + 5f), tkCol, 1.5f);

        dl.AddText(new Vector2(cx, cy), U(CDkGrey), message);

        ImGui.Dummy(new Vector2(availW, availH));
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

    private void DrawTextureOrPlaceholder(string? path, Vector2 size)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            if (!_texCache.TryGetValue(path, out var shared))
            {
                shared = Plugin.TextureProvider.GetFromFile(new FileInfo(path));
                _texCache[path] = shared;
            }
            var wrap = shared.GetWrapOrDefault();
            if (wrap != null)
            {
                float ratio = wrap.Size.X / wrap.Size.Y;
                float iw    = MathF.Min(size.X, size.Y * ratio);
                float ih    = iw / ratio;
                var   origin = ImGui.GetCursorScreenPos();
                var   offset = new Vector2((size.X - iw) * 0.5f, (size.Y - ih) * 0.5f);
                ImGui.GetWindowDrawList().AddImage(wrap.Handle, origin + offset, origin + offset + new Vector2(iw, ih));
                ImGui.Dummy(size);
                return;
            }
        }
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(p, p + size, 0xFF222222);
        ImGui.Dummy(size);
    }

    private void DrawPartnersTab()
    {
        if (_partners.Count == 0) { DrawEmptyState("NO PARTNERS"); return; }

        using var scroll = ImRaii.Child("##partnersScroll", Vector2.Zero, false);
        if (!scroll) return;

        const int   cols  = 4;
        const float gap   = 8f;
        const float tileH = 85f;
        const float nameH = 20f;
        float availW = ImGui.GetContentRegionAvail().X;
        float tileW  = MathF.Floor((availW - gap * (cols - 1)) / cols);

        ImGui.Dummy(new Vector2(0, 8f));

        for (int i = 0; i < _partners.Count; i++)
        {
            var p = _partners[i];
            ImGui.PushID(p.Id);
            try
            {
                if (i % cols != 0) ImGui.SameLine(0f, gap);

                var dl = ImGui.GetWindowDrawList();
                var sp = ImGui.GetCursorScreenPos();

                ImGui.InvisibleButton($"##p{p.Id}", new Vector2(tileW, tileH + nameH));
                bool hov     = ImGui.IsItemHovered();
                bool clicked = ImGui.IsItemClicked();

                uint logoBg = hov ? U(new Vector4(0.12f, 0.04f, 0.04f, 1f))
                                  : U(new Vector4(0.08f, 0.05f, 0.05f, 1f));
                dl.AddRectFilled(sp, sp + new Vector2(tileW, tileH), logoBg, 4f);
                dl.AddRect(sp, sp + new Vector2(tileW, tileH),
                           hov ? U(new Vector4(0.55f, 0f, 0f, 0.85f))
                               : U(new Vector4(0.20f, 0f, 0f, 0.55f)),
                           4f, ImDrawFlags.None, hov ? 1.5f : 1f);

                if (hov)
                    dl.AddRect(sp - Vector2.One, sp + new Vector2(tileW, tileH) + Vector2.One,
                               WithAlpha(U(CRed), 0x22), 5f, ImDrawFlags.None, 3f);

                if (!string.IsNullOrWhiteSpace(p.LogoPath) && File.Exists(p.LogoPath))
                {
                    if (!_texCache.TryGetValue(p.LogoPath, out var shared))
                    {
                        shared = Plugin.TextureProvider.GetFromFile(new FileInfo(p.LogoPath));
                        _texCache[p.LogoPath] = shared;
                    }
                    var wrap = shared.GetWrapOrDefault();
                    if (wrap != null)
                    {
                        float maxH  = tileH - 14f;
                        float ratio = wrap.Size.X / wrap.Size.Y;
                        float lw    = MathF.Min(maxH * ratio, tileW - 14f);
                        float lh    = lw / ratio;
                        var   lpos  = sp + new Vector2((tileW - lw) * 0.5f, (tileH - lh) * 0.5f);
                        dl.AddImage(wrap.Handle, lpos, lpos + new Vector2(lw, lh),
                                    Vector2.Zero, Vector2.One, hov ? 0xFFFFFFFF : 0xDDFFFFFF);
                    }
                }

                var nsz  = ImGui.CalcTextSize(p.Name);
                float nx = sp.X + (tileW - nsz.X) * 0.5f;
                float ny = sp.Y + tileH + (nameH - nsz.Y) * 0.5f;
                dl.AddText(new Vector2(nx, ny),
                           hov ? 0xFFFFFFFF : U(new Vector4(0.55f, 0.55f, 0.55f, 1f)),
                           p.Name);

                if (clicked && !string.IsNullOrWhiteSpace(p.DiscordUrl))
                    Dalamud.Utility.Util.OpenLink(p.DiscordUrl);

                if (hov) ImGui.SetTooltip("Click to open Discord");
            }
            finally { ImGui.PopID(); }

            if (i % cols == cols - 1) ImGui.Dummy(new Vector2(0, gap));
        }
    }

    public void Dispose() => _texCache.Clear();
}
