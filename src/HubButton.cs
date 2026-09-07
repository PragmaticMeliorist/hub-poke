using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>
/// Tiny Hub Poke. No console. Not always-on-top.
/// Default click = gentle CCD 4K120 renegotiate. Mapping is a separate button.
/// Failsafe button opens Cold Retraining (Hub power-cycle walkthrough; same dialog after all three tries).
/// Sizable, column spec sheet, PerMonitorV2 + WM_DPICHANGED relayout.
/// </summary>
public class HubButton : Form {
    const int WM_SETICON = 0x0080;
    const int WM_DPICHANGED = 0x02E0;
    const int WM_DISPLAYCHANGE = 0x007E;
    const int ICON_SMALL = 0;
    const int ICON_BIG = 1;
    const int SM_CXICON = 11;
    const int SM_CXSMICON = 49;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_NOCOPYBITS = 0x0100;
    const float DesignDpi = 96f;
    const int DesignPad = 16;
    const int DesignMinInnerW = 480;
    const int DesignPokeH = 52;
    const int DesignSecondaryH = 36;
    const int DesignFailsafeH = 40;
    const string PokeEmoji = "\U0001FAF5";
    const string PokeLabel = " Hub";
    const string FailsafeButtonText = "Tried all three, and it still isn't working?";

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll")]
    static extern int GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    TableLayoutPanel top;
    TableLayoutPanel bottom;
    HubPrimaryButton poke;
    Button hard;
    Button mapTouch;
    Button failsafe;
    Label hint;
    Label lead;
    TableLayoutPanel sheet;
    Label[] sheetHdr;
    Label[][] sheetBody;
    Label signalNote;
    ToolTip tips;
    Icon iconSmall;
    Icon iconBig;
    string icoPath;
    bool busy;
    bool triedPoke;
    bool triedMap;
    bool triedHard;
    bool failsafeAutoShown;
    int currentDpi = 96;
    bool applyingDpi;
    Font pokeLabelFont;
    Font pokeEmojiFont;
    Font bodyFont;
    Font secondaryFont;
    Font sheetFont;
    Font hdrFont;
    Font noteFont;

    [STAThread]
    public static void Main(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string cmd = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
        if (cmd == "/status") {
            HubRenegotiate.Result r = HubRenegotiate.CurrentStatus();
            HubRenegotiate.Log("status " + r.Message);
            return;
        }
        if (cmd == "/gentle") {
            HubRenegotiate.Result r = HubRenegotiate.GentleHandshake();
            MessageBox.Show(r.Message, "Hub Poke", MessageBoxButtons.OK,
                r.RolledBack ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            return;
        }
        if (cmd == "/hard") {
            if (!HubRenegotiate.IsAdmin()) {
                RelaunchElevated("/hard");
                return;
            }
            HubRenegotiate.Result r = HubRenegotiate.HardRetrain();
            MessageBox.Show(r.Message, "Hub hot retraining", MessageBoxButtons.OK,
                r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return;
        }
        if (cmd == "/maptouch") {
            RunMapTouchStandalone();
            return;
        }
        Application.Run(new HubButton());
    }

    static void RunMapTouchStandalone() {
        if (!HubRenegotiate.IsAdmin()) {
            RelaunchElevated("/maptouch");
            return;
        }
        HubRenegotiate.Result r = HubRenegotiate.BindDigitizerToHub();
        MoreInfoDialog.Notify(null, "Map Touch → Hub", r.Message, r.MappingDetails, !r.MappingOk);
    }

    static void RelaunchElevated(string arg) {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = Application.ExecutablePath;
        psi.Arguments = arg;
        psi.UseShellExecute = true;
        psi.Verb = "runas";
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        try { Process.Start(psi); }
        catch { MessageBox.Show("UAC cancelled. Nothing changed.", "Hub Poke", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    public HubButton() {
        Text = "Hub Poke";
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.Sizable;
        ControlBox = true;
        MinimizeBox = true;
        MaximizeBox = true;
        TopMost = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(48, 48);
        AutoScaleMode = AutoScaleMode.None;
        AutoScaleDimensions = new SizeF(96f, 96f);
        DoubleBuffered = true;
        BackColor = Color.FromArgb(18, 18, 22);
        ForeColor = Color.WhiteSmoke;
        Padding = new Padding(0);
        TryLoadAppIcon();
        tips = new ToolTip();
        BuildLayout();
        ClientSize = new Size(DesignMinInnerW + DesignPad * 2, 490);
        Shown += OnFirstShown;
        Resize += OnFormResize;
    }

    void BuildLayout() {
        top = new TableLayoutPanel();
        top.Dock = DockStyle.Top;
        top.AutoSize = true;
        top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        top.ColumnCount = 1;
        top.RowCount = 6;
        top.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        bottom = new TableLayoutPanel();
        bottom.Dock = DockStyle.Bottom;
        bottom.AutoSize = true;
        bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        bottom.ColumnCount = 1;
        bottom.RowCount = 2;
        bottom.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(bottom);
        Controls.Add(top);

        poke = new HubPrimaryButton();
        poke.Text = PokeEmoji + PokeLabel;
        poke.AccessibleName = "Hub";
        poke.Dock = DockStyle.Top;
        poke.AutoSize = true;
        poke.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        poke.TextAlign = ContentAlignment.MiddleCenter;
        poke.BackColor = Color.FromArgb(46, 176, 96);
        poke.ForeColor = Color.FromArgb(12, 22, 16);
        poke.FlatStyle = FlatStyle.Flat;
        poke.FlatAppearance.BorderSize = 0;
        poke.Cursor = Cursors.Hand;
        poke.Click += PokeClick;
        tips.SetToolTip(poke, "Renegotiate Hub link (after sleep, input cycle, or cable hot-swap).");
        top.Controls.Add(poke, 0, 0);

        hint = new Label();
        hint.Text = "Renegotiate Hub link after sleep, an input cycle, or a cable hot-swap.";
        hint.AutoSize = true;
        hint.Dock = DockStyle.Top;
        hint.ForeColor = Color.FromArgb(168, 186, 172);
        hint.UseMnemonic = false;
        top.Controls.Add(hint, 0, 1);

        mapTouch = new Button();
        mapTouch.Text = "Map Touch → Hub";
        mapTouch.Dock = DockStyle.Top;
        mapTouch.AutoSize = true;
        mapTouch.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        mapTouch.TextAlign = ContentAlignment.MiddleCenter;
        mapTouch.BackColor = Color.FromArgb(44, 56, 82);
        mapTouch.ForeColor = Color.FromArgb(220, 226, 236);
        mapTouch.FlatStyle = FlatStyle.Flat;
        mapTouch.FlatAppearance.BorderColor = Color.FromArgb(70, 86, 118);
        mapTouch.FlatAppearance.BorderSize = 1;
        mapTouch.Click += MapTouchClick;
        tips.SetToolTip(mapTouch, "Maps Hub HID to the 84-inch (UID264). Does not restart FirePro.");
        top.Controls.Add(mapTouch, 0, 2);

        hard = new Button();
        hard.Text = "Hot Retraining";
        hard.Dock = DockStyle.Top;
        hard.AutoSize = true;
        hard.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        hard.TextAlign = ContentAlignment.MiddleCenter;
        hard.BackColor = Color.FromArgb(28, 28, 34);
        hard.ForeColor = Color.FromArgb(168, 140, 118);
        hard.FlatStyle = FlatStyle.Flat;
        hard.FlatAppearance.BorderColor = Color.FromArgb(64, 54, 48);
        hard.FlatAppearance.BorderSize = 1;
        hard.Click += HardClick;
        tips.SetToolTip(hard, "Hot retraining: live FirePro disable/enable while Windows is running. Can TDR and glitch 3D. Not the default.");
        top.Controls.Add(hard, 0, 3);

        failsafe = new Button();
        failsafe.Text = FailsafeButtonText;
        failsafe.Dock = DockStyle.Top;
        failsafe.AutoSize = true;
        failsafe.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        failsafe.TextAlign = ContentAlignment.MiddleCenter;
        failsafe.BackColor = Color.FromArgb(72, 52, 32);
        failsafe.ForeColor = Color.FromArgb(248, 214, 150);
        failsafe.FlatStyle = FlatStyle.Flat;
        failsafe.FlatAppearance.BorderColor = Color.FromArgb(140, 100, 52);
        failsafe.FlatAppearance.BorderSize = 1;
        failsafe.UseMnemonic = false;
        failsafe.Click += FailsafeClick;
        tips.SetToolTip(failsafe, "Cold retraining walkthrough (Hub power cycle) when 4K120 is still dead after the three buttons.");
        top.Controls.Add(failsafe, 0, 4);

        lead = new Label();
        lead.AutoSize = true;
        lead.Dock = DockStyle.Top;
        lead.ForeColor = Color.FromArgb(210, 214, 220);
        lead.UseMnemonic = false;
        lead.Visible = false;
        top.Controls.Add(lead, 0, 5);

        sheet = BuildSheet();
        bottom.Controls.Add(sheet, 0, 0);

        signalNote = new Label();
        signalNote.AutoSize = true;
        signalNote.Dock = DockStyle.Top;
        signalNote.ForeColor = Color.FromArgb(120, 128, 140);
        signalNote.UseMnemonic = false;
        bottom.Controls.Add(signalNote, 0, 1);

        ApplyScaledChrome();
    }

    TableLayoutPanel BuildSheet() {
        TableLayoutPanel t = new TableLayoutPanel();
        t.ColumnCount = 4;
        t.RowCount = 1;
        t.AutoSize = true;
        t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        t.Dock = DockStyle.Top;
        t.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sheetHdr = new Label[4];
        sheetBody = new Label[0][];
        string[] headers = new string[] { "Monitor", "Resolution", "Refresh", "Status" };
        for (int c = 0; c < 4; c++) {
            sheetHdr[c] = SheetCell(headers[c], true);
            t.Controls.Add(sheetHdr[c], c, 0);
        }
        return t;
    }

    Label SheetCell(string text, bool header) {
        Label l = new Label();
        l.Text = text;
        l.AutoSize = true;
        l.Dock = DockStyle.Fill;
        l.TextAlign = ContentAlignment.MiddleLeft;
        l.UseMnemonic = false;
        l.AutoEllipsis = true;
        l.Margin = new Padding(0, 3, 8, 3);
        l.ForeColor = header ? Color.FromArgb(140, 148, 160) : Color.FromArgb(210, 214, 220);
        return l;
    }

    int Scale(int px96) {
        return (int)Math.Round(px96 * (currentDpi / DesignDpi));
    }

    float Pt(float designPt) {
        return designPt * (currentDpi / DesignDpi);
    }

    int SafeDpi() {
        try {
            if (IsHandleCreated) {
                int d = GetDpiForWindow(Handle);
                if (d > 0) return d;
            }
        } catch { }
        return currentDpi > 0 ? currentDpi : 96;
    }

    void DisposeFont(ref Font f) {
        if (f != null) {
            f.Dispose();
            f = null;
        }
    }

    Font MakeFont(string family, float designPt, FontStyle style) {
        try { return new Font(family, Pt(designPt), style, GraphicsUnit.Point); }
        catch { return new Font("Segoe UI", Pt(designPt), style, GraphicsUnit.Point); }
    }

    void ApplyScaledChrome() {
        AutoScaleMode = AutoScaleMode.None;
        AutoScaleDimensions = new SizeF(96f, 96f);
        DisposeFont(ref bodyFont);
        DisposeFont(ref secondaryFont);
        DisposeFont(ref pokeLabelFont);
        DisposeFont(ref pokeEmojiFont);
        DisposeFont(ref sheetFont);
        DisposeFont(ref hdrFont);
        DisposeFont(ref noteFont);
        bodyFont = MakeFont("Segoe UI", 10.5f, FontStyle.Regular);
        secondaryFont = MakeFont("Segoe UI", 10.5f, FontStyle.Regular);
        pokeLabelFont = MakeFont("Segoe UI", 13f, FontStyle.Bold);
        pokeEmojiFont = MakeFont("Segoe UI Emoji", 13f, FontStyle.Regular);
        sheetFont = MakeFont("Segoe UI", 10.5f, FontStyle.Regular);
        hdrFont = MakeFont("Segoe UI", 9.5f, FontStyle.Regular);
        noteFont = MakeFont("Segoe UI", 9.5f, FontStyle.Regular);
        Font = bodyFont;
        poke.Font = pokeLabelFont;
        poke.EmojiFont = pokeEmojiFont;
        poke.LabelFont = pokeLabelFont;
        hint.Font = bodyFont;
        mapTouch.Font = secondaryFont;
        hard.Font = secondaryFont;
        failsafe.Font = secondaryFont;
        lead.Font = bodyFont;
        signalNote.Font = noteFont;
        ApplySheetFonts();
        int pad = Scale(DesignPad);
        Padding = new Padding(pad);
        poke.Margin = new Padding(0, 0, 0, Scale(10));
        hint.Margin = new Padding(0, 0, 0, Scale(12));
        mapTouch.Margin = new Padding(0, 0, 0, Scale(10));
        hard.Margin = new Padding(0, 0, 0, Scale(10));
        failsafe.Margin = new Padding(0, 0, 0, Scale(12));
        lead.Margin = new Padding(0, 0, 0, Scale(8));
        sheet.Margin = new Padding(0, 0, 0, Scale(6));
        signalNote.Margin = new Padding(0, 0, 0, 0);
        poke.Padding = new Padding(Scale(12), Scale(10), Scale(12), Scale(10));
        mapTouch.Padding = new Padding(Scale(12), Scale(8), Scale(12), Scale(8));
        hard.Padding = new Padding(Scale(12), Scale(8), Scale(12), Scale(8));
        failsafe.Padding = new Padding(Scale(12), Scale(8), Scale(12), Scale(8));
        poke.MinimumSize = new Size(0, Scale(DesignPokeH));
        mapTouch.MinimumSize = new Size(0, Scale(DesignSecondaryH));
        hard.MinimumSize = new Size(0, Scale(DesignSecondaryH));
        failsafe.MinimumSize = new Size(0, Scale(DesignFailsafeH));
        int cellH = Scale(26);
        if (sheetHdr != null) {
            for (int c = 0; c < sheetHdr.Length; c++)
                sheetHdr[c].MinimumSize = new Size(0, cellH);
        }
        if (sheetBody != null) {
            for (int r = 0; r < sheetBody.Length; r++) {
                if (sheetBody[r] == null) continue;
                for (int c = 0; c < sheetBody[r].Length; c++)
                    sheetBody[r][c].MinimumSize = new Size(0, cellH);
            }
        }
        FitWrapWidths();
    }

    void ApplySheetFonts() {
        if (sheetHdr != null) {
            for (int c = 0; c < sheetHdr.Length; c++)
                if (sheetHdr[c] != null) sheetHdr[c].Font = hdrFont;
        }
        if (sheetBody != null) {
            for (int r = 0; r < sheetBody.Length; r++) {
                if (sheetBody[r] == null) continue;
                for (int c = 0; c < sheetBody[r].Length; c++)
                    if (sheetBody[r][c] != null) sheetBody[r][c].Font = sheetFont;
            }
        }
    }

    void FitWrapWidths() {
        int inner = ClientSize.Width - Padding.Horizontal;
        if (inner < Scale(160)) inner = Scale(160);
        hint.MaximumSize = new Size(inner, 0);
        lead.MaximumSize = new Size(inner, 0);
        signalNote.MaximumSize = new Size(inner, 0);
    }

    Size ComputeMinOuterSize() {
        int innerW = Scale(DesignMinInnerW);
        hint.MaximumSize = new Size(innerW, 0);
        lead.MaximumSize = new Size(innerW, 0);
        signalNote.MaximumSize = new Size(innerW, 0);
        top.PerformLayout();
        bottom.PerformLayout();
        Size topPref = top.GetPreferredSize(new Size(innerW, 0));
        Size botPref = bottom.GetPreferredSize(new Size(innerW, 0));
        int needW = Math.Max(innerW, Math.Max(topPref.Width, botPref.Width)) + Padding.Horizontal;
        int pokeW = TextRenderer.MeasureText(PokeEmoji + PokeLabel, pokeLabelFont).Width + poke.Padding.Horizontal + Scale(32);
        int mapW = TextRenderer.MeasureText(mapTouch.Text, mapTouch.Font).Width + mapTouch.Padding.Horizontal + Scale(24);
        int hardW = TextRenderer.MeasureText(hard.Text, hard.Font).Width + hard.Padding.Horizontal + Scale(24);
        int failW = TextRenderer.MeasureText(failsafe.Text, failsafe.Font).Width + failsafe.Padding.Horizontal + Scale(24);
        needW = Math.Max(needW, Math.Max(pokeW, Math.Max(mapW, Math.Max(hardW, failW))) + Padding.Horizontal);
        int needH = topPref.Height + botPref.Height + Padding.Vertical;
        int nMon = sheetBody == null ? 0 : sheetBody.Length;
        int floorH = Padding.Vertical
            + Scale(DesignPokeH) + poke.Margin.Vertical
            + Scale(36) + hint.Margin.Vertical
            + Scale(DesignSecondaryH) + mapTouch.Margin.Vertical
            + Scale(DesignSecondaryH) + hard.Margin.Vertical
            + Scale(DesignFailsafeH) + failsafe.Margin.Vertical
            + Scale(26) * (1 + nMon) + sheet.Margin.Vertical
            + Scale(18);
        if (needH < floorH) needH = floorH;
        int ncW = Width - ClientSize.Width;
        int ncH = Height - ClientSize.Height;
        if (ncW < 2) ncW = Scale(16);
        if (ncH < 2) ncH = Scale(40);
        return new Size(needW + ncW, needH + ncH);
    }

    void ApplyMinAndFit(bool forceToMin) {
        Size min = ComputeMinOuterSize();
        MinimumSize = min;
        if (forceToMin || Width < min.Width || Height < min.Height)
            Size = min;
        FitWrapWidths();
        PerformLayout();
    }

    void OnMonitorDpiChanged(int newDpi, HubRenegotiate.RECT suggested) {
        if (newDpi <= 0) newDpi = 96;
        applyingDpi = true;
        try {
            currentDpi = newDpi;
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ApplyScaledChrome();
            ApplyWindowIcons();
            ResumeLayout(true);
            Size min = ComputeMinOuterSize();
            MinimumSize = min;
            int x = suggested.left;
            int y = suggested.top;
            int w = suggested.right - suggested.left;
            int h = suggested.bottom - suggested.top;
            if (w < min.Width) w = min.Width;
            if (h < min.Height) h = min.Height;
            SetWindowPos(Handle, IntPtr.Zero, x, y, w, h,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOCOPYBITS);
            FitWrapWidths();
            PerformLayout();
        } finally {
            applyingDpi = false;
        }
    }

    void OnFirstShown(object sender, EventArgs e) {
        currentDpi = SafeDpi();
        ApplyScaledChrome();
        ApplyWindowIcons();
        RefreshStatus();
        ApplyMinAndFit(true);
    }

    void OnFormResize(object sender, EventArgs e) {
        if (applyingDpi || top == null) return;
        FitWrapWidths();
    }

    void TryLoadAppIcon() {
        try {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            icoPath = Path.Combine(dir, "assets", "hub-poke.ico");
            if (!File.Exists(icoPath)) return;
            ApplyWindowIcons();
        } catch { }
    }

    void ApplyWindowIcons() {
        if (icoPath == null || !File.Exists(icoPath)) return;
        int sm = GetSystemMetrics(SM_CXSMICON);
        int bg = GetSystemMetrics(SM_CXICON);
        if (sm < 16) sm = 16;
        if (bg < 32) bg = 32;
        Icon nextSmall = new Icon(icoPath, sm, sm);
        Icon nextBig = new Icon(icoPath, bg, bg);
        Icon = nextBig;
        if (IsHandleCreated) {
            SendMessage(Handle, WM_SETICON, (IntPtr)ICON_SMALL, nextSmall.Handle);
            SendMessage(Handle, WM_SETICON, (IntPtr)ICON_BIG, nextBig.Handle);
        }
        if (iconSmall != null) iconSmall.Dispose();
        if (iconBig != null && iconBig != Icon) iconBig.Dispose();
        iconSmall = nextSmall;
        iconBig = nextBig;
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        currentDpi = SafeDpi();
        ApplyWindowIcons();
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == WM_DPICHANGED) {
            int newDpi = (int)(m.WParam.ToInt64() & 0xFFFF);
            HubRenegotiate.RECT suggested = (HubRenegotiate.RECT)Marshal.PtrToStructure(m.LParam, typeof(HubRenegotiate.RECT));
            OnMonitorDpiChanged(newDpi, suggested);
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == WM_DISPLAYCHANGE) {
            base.WndProc(ref m);
            if (!busy) {
                RefreshStatus();
                ApplyMinAndFit(false);
            }
            return;
        }
        base.WndProc(ref m);
    }

    void SetBusy(bool on) {
        busy = on;
        poke.Enabled = !on;
        hard.Enabled = !on;
        mapTouch.Enabled = !on;
        UseWaitCursor = on;
    }

    void SetLead(string text) {
        lead.Text = text == null ? "" : text;
        lead.Visible = lead.Text.Length > 0;
    }

    Color StatusColor(string st) {
        if (st == "OK") return Color.FromArgb(120, 200, 140);
        if (st == "BAD") return Color.FromArgb(220, 120, 100);
        return Color.FromArgb(160, 166, 176);
    }

    void RebuildSheet(HubRenegotiate.LiveMonitor[] mons) {
        if (mons == null) mons = new HubRenegotiate.LiveMonitor[0];
        sheet.SuspendLayout();
        sheet.Controls.Clear();
        int rows = 1 + mons.Length;
        sheet.RowCount = rows;
        sheet.RowStyles.Clear();
        for (int i = 0; i < rows; i++)
            sheet.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        string[] headers = new string[] { "Monitor", "Resolution", "Refresh", "Status" };
        sheetHdr = new Label[4];
        int cellH = Scale(26);
        for (int c = 0; c < 4; c++) {
            sheetHdr[c] = SheetCell(headers[c], true);
            sheetHdr[c].Font = hdrFont != null ? hdrFont : Font;
            sheetHdr[c].MinimumSize = new Size(0, cellH);
            sheet.Controls.Add(sheetHdr[c], c, 0);
        }
        sheetBody = new Label[mons.Length][];
        for (int r = 0; r < mons.Length; r++) {
            HubRenegotiate.LiveMonitor mon = mons[r];
            string st = mon.DeskW == 0 ? "—" : (mon.Ok ? "OK" : "BAD");
            string[] vals = new string[] {
                mon.Name == null || mon.Name.Length == 0 ? "Monitor" : mon.Name,
                HubRenegotiate.Px(mon.DeskW, mon.DeskH),
                HubRenegotiate.HzText(mon.Hz),
                st
            };
            sheetBody[r] = new Label[4];
            for (int c = 0; c < 4; c++) {
                sheetBody[r][c] = SheetCell(vals[c], false);
                sheetBody[r][c].Font = sheetFont != null ? sheetFont : Font;
                sheetBody[r][c].MinimumSize = new Size(0, cellH);
                if (c == 3) sheetBody[r][c].ForeColor = StatusColor(st);
                sheet.Controls.Add(sheetBody[r][c], c, r + 1);
            }
        }
        sheet.ResumeLayout(true);
    }

    void BindSheet(HubRenegotiate.Result r) {
        HubRenegotiate.LiveMonitor[] mons = r == null ? null : r.Monitors;
        RebuildSheet(mons);
        signalNote.Text = HubRenegotiate.SignalFootnote(r);
    }

    void ShowStatus(string leadText, HubRenegotiate.Result r) {
        SetLead(leadText);
        BindSheet(r);
        ApplyMinAndFit(false);
    }

    void RefreshStatus() {
        try {
            HubRenegotiate.Result r = HubRenegotiate.CurrentStatus();
            ShowStatus(null, r);
        } catch (Exception ex) {
            SetLead(ex.Message);
        }
    }

    void PokeClick(object sender, EventArgs e) {
        if (busy) return;
        if ((ModifierKeys & Keys.Shift) == Keys.Shift) {
            HardClick(sender, e);
            return;
        }
        SetBusy(true);
        SetLead("Renegotiating Hub link…");
        Refresh();
        try {
            HubRenegotiate.Result r = HubRenegotiate.GentleHandshake();
            triedPoke = true;
            ShowStatus(
                r.RolledBack ? "Rolled back." : (r.Ok ? "Gentle handshake applied." : "Handshake ran; Hub not fully 4K120."),
                r);
            if (r.RolledBack)
                MessageBox.Show(this, r.Message, "Rolled back", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            MaybeAutoShowFailsafe(r);
        } catch (Exception ex) {
            SetLead(ex.Message);
        } finally { SetBusy(false); }
    }

    void HardClick(object sender, EventArgs e) {
        if (busy) return;
        DialogResult d = MessageBox.Show(this,
            "Live-restart the FirePro W7100 while Windows is running?\n\n" +
            "This can TDR, drop GPU contexts, and cause flicker/tearing/artifacting on a live 3D desktop.\n" +
            "Prefer the green Hub poke (gentle CCD renegotiate) first.\n\n" +
            "Samsung should stay 1280x720. This is not Settings → Extend.",
            "Hot Retraining",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (d != DialogResult.OK) return;
        triedHard = true;
        if (!HubRenegotiate.IsAdmin()) {
            SetLead("UAC: hot retraining in an elevated copy…");
            RelaunchElevated("/hard");
            MaybeAutoShowFailsafe(SafeStatus());
            return;
        }
        SetBusy(true);
        SetLead("Hot retraining FirePro…");
        Refresh();
        try {
            HubRenegotiate.Result r = HubRenegotiate.HardRetrain();
            triedHard = true;
            ShowStatus("Hot retraining done.", r);
            MaybeAutoShowFailsafe(r);
        } catch (Exception ex) {
            SetLead(ex.Message);
        } finally { SetBusy(false); }
    }

    void MapTouchClick(object sender, EventArgs e) {
        if (busy) return;
        DialogResult d = MoreInfoDialog.Confirm(this, "Map Touch → Hub",
            HubRenegotiate.MapTouchConfirmCopy(),
            HubRenegotiate.TouchMappingDetails());
        if (d != DialogResult.OK) return;
        triedMap = true;
        if (!HubRenegotiate.IsAdmin()) {
            SetLead("UAC: map touch to Hub (not a GPU restart)…");
            RelaunchElevated("/maptouch");
            MaybeAutoShowFailsafe(SafeStatus());
            return;
        }
        SetBusy(true);
        try {
            HubRenegotiate.Result r = HubRenegotiate.BindDigitizerToHub();
            ShowStatus(r.MappingNote != null ? r.MappingNote : r.Message, r);
            MoreInfoDialog.Notify(this, "Map Touch → Hub", r.Message, r.MappingDetails, !r.MappingOk);
            MaybeAutoShowFailsafe(r);
        } finally { SetBusy(false); }
    }

    void FailsafeClick(object sender, EventArgs e) {
        FailsafeDialog.ShowWalkthrough(this, currentDpi);
    }

    HubRenegotiate.Result SafeStatus() {
        try { return HubRenegotiate.CurrentStatus(); }
        catch { return null; }
    }

    void MaybeAutoShowFailsafe(HubRenegotiate.Result r) {
        if (failsafeAutoShown) return;
        if (!triedPoke || !triedMap || !triedHard) return;
        if (r != null && r.HubDesktopGood) return;
        failsafeAutoShown = true;
        FailsafeDialog.ShowWalkthrough(this, currentDpi);
    }
}

/// <summary>
/// Cold Retraining walkthrough (Hub power cycle). Same dialog for the button and the all-three-tried auto-show.
/// W7100 Advanced shots: two MST tiles is the healthy target; empty Connected () is leftover HPD.
/// </summary>
class FailsafeDialog : Form {
    const string WalkTitle = "Cold Retraining - Hub 4K 120 failsafe";
    const string IntroCopy =
        "Cold retraining is a Hub power cycle, not Hot Retraining (live GPU restart). When 4K120 is dead and you still have picture at 4K30, with empty Connected () on the second GPU DP:";
    const string StepsCopy =
        "1. Leave the PC running if you want.\r\n" +
        "2. Power the Hub display fully off (cold Hub, not PC sleep).\r\n" +
        "3. Confirm both Replacement-PC DP cables are seated (Hub and W7100).\r\n" +
        "4. Power the Hub on. Do not hot-plug the second cable after the image is up.\r\n" +
        "5. You want desktop 3840×2160 @ 120 Hz. Empty () is leftover HPD, not \"buy cables.\"";
    const string BadHead = "BAD — one MST + empty Connected ()";
    const string GoodHead = "GOOD — both MST after cold retraining";
    const string BadCapA = "DP-3 MST + DP-4 Connected ()";
    const string BadCapB = "DP-1 Connected () + DP-3 MST";
    const string GoodCap = "DP-1 and DP-3 both Unsupported Type (MST). Red X is AMD’s MST UI, not a dead link.";
    const string GoodFile = "failsafe-w7100-good-dp1-dp3-mst.png";
    const string BadFileA = "failsafe-w7100-bad-dp3-mst-dp4-empty.png";
    const string BadFileB = "failsafe-w7100-bad-dp1-empty-dp3-mst.png";

    readonly List<Image> loaded = new List<Image>();

    public static void ShowWalkthrough(IWin32Window owner, int dpi) {
        using (FailsafeDialog f = new FailsafeDialog(dpi)) {
            if (owner != null) f.ShowDialog(owner);
            else f.ShowDialog();
        }
    }

    FailsafeDialog(int dpi) {
        if (dpi <= 0) dpi = 96;
        float s = dpi / 96f;
        Text = WalkTitle;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(18, 18, 22);
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 12.5f * s, FontStyle.Regular, GraphicsUnit.Point);
        Padding = new Padding(ScalePx(s, 16));

        TableLayoutPanel root = new TableLayoutPanel();
        root.Dock = DockStyle.Fill;
        root.ColumnCount = 1;
        root.RowCount = 2;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        Panel scroll = new Panel();
        scroll.Dock = DockStyle.Fill;
        scroll.AutoScroll = true;
        scroll.BackColor = BackColor;
        root.Controls.Add(scroll, 0, 0);

        TableLayoutPanel body = new TableLayoutPanel();
        body.AutoSize = true;
        body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        body.Dock = DockStyle.Top;
        body.ColumnCount = 1;
        body.RowCount = 5;
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 5; i++)
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroll.Controls.Add(body);

        Label title = MakeLabel(WalkTitle, new Font("Segoe UI", 16f * s, FontStyle.Bold, GraphicsUnit.Point),
            Color.FromArgb(248, 214, 150));
        title.Margin = new Padding(0, 0, 0, ScalePx(s, 8));
        body.Controls.Add(title, 0, 0);

        Label intro = MakeLabel(IntroCopy, Font, Color.FromArgb(220, 226, 232));
        intro.Margin = new Padding(0, 0, 0, ScalePx(s, 10));
        body.Controls.Add(intro, 0, 1);

        Label steps = MakeLabel(StepsCopy, new Font("Segoe UI", 13f * s, FontStyle.Regular, GraphicsUnit.Point),
            Color.FromArgb(236, 238, 242));
        steps.Margin = new Padding(0, 0, 0, ScalePx(s, 14));
        body.Controls.Add(steps, 0, 2);

        TableLayoutPanel shots = new TableLayoutPanel();
        shots.AutoSize = true;
        shots.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        shots.Dock = DockStyle.Top;
        shots.ColumnCount = 2;
        shots.RowCount = 4;
        shots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        shots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (int i = 0; i < 4; i++)
            shots.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label badHead = MakeLabel(BadHead, new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Point),
            Color.FromArgb(220, 120, 100));
        Label goodHead = MakeLabel(GoodHead, new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Point),
            Color.FromArgb(120, 200, 140));
        shots.Controls.Add(badHead, 0, 0);
        shots.Controls.Add(goodHead, 1, 0);

        PictureBox badA = MakeShot(BadFileA, s);
        PictureBox badB = MakeShot(BadFileB, s);
        PictureBox good = MakeShot(GoodFile, s);
        shots.Controls.Add(badA, 0, 1);
        shots.SetRowSpan(good, 2);
        shots.Controls.Add(good, 1, 1);
        shots.Controls.Add(badB, 0, 2);

        Label badCap = MakeLabel(BadCapA + "\r\n" + BadCapB, new Font("Segoe UI", 10.5f * s, FontStyle.Regular, GraphicsUnit.Point),
            Color.FromArgb(200, 170, 160));
        Label goodCap = MakeLabel(GoodCap, new Font("Segoe UI", 10.5f * s, FontStyle.Regular, GraphicsUnit.Point),
            Color.FromArgb(168, 196, 176));
        shots.Controls.Add(badCap, 0, 3);
        shots.Controls.Add(goodCap, 1, 3);
        shots.Margin = new Padding(0, 0, 0, ScalePx(s, 8));
        body.Controls.Add(shots, 0, 3);

        Button ok = new Button();
        ok.Text = "OK";
        ok.DialogResult = DialogResult.OK;
        ok.AutoSize = true;
        ok.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ok.FlatStyle = FlatStyle.Flat;
        ok.BackColor = Color.FromArgb(46, 176, 96);
        ok.ForeColor = Color.FromArgb(12, 22, 16);
        ok.FlatAppearance.BorderSize = 0;
        ok.Font = new Font("Segoe UI", 12.5f * s, FontStyle.Bold, GraphicsUnit.Point);
        ok.Padding = new Padding(ScalePx(s, 28), ScalePx(s, 8), ScalePx(s, 28), ScalePx(s, 8));
        ok.MinimumSize = new Size(ScalePx(s, 120), ScalePx(s, 40));
        ok.Margin = new Padding(0, ScalePx(s, 8), 0, 0);
        ok.Anchor = AnchorStyles.Right;
        AcceptButton = ok;
        CancelButton = ok;

        FlowLayoutPanel okRow = new FlowLayoutPanel();
        okRow.AutoSize = true;
        okRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        okRow.Dock = DockStyle.Fill;
        okRow.FlowDirection = FlowDirection.RightToLeft;
        okRow.WrapContents = false;
        okRow.Padding = new Padding(0);
        okRow.Controls.Add(ok);
        root.Controls.Add(okRow, 0, 1);

        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        try {
            if (Owner != null) wa = Screen.FromControl((Control)Owner).WorkingArea;
        } catch { }
        int wantW = ScalePx(s, 980);
        int wantH = ScalePx(s, 820);
        if (wantW > wa.Width - 24) wantW = Math.Max(ScalePx(s, 640), wa.Width - 24);
        if (wantH > wa.Height - 24) wantH = Math.Max(ScalePx(s, 480), wa.Height - 24);
        ClientSize = new Size(wantW, wantH);
        MinimumSize = new Size(ScalePx(s, 640), ScalePx(s, 420));
    }

    static int ScalePx(float s, int px96) {
        return (int)Math.Round(px96 * s);
    }

    static Label MakeLabel(string text, Font font, Color color) {
        Label l = new Label();
        l.Text = text;
        l.Font = font;
        l.ForeColor = color;
        l.AutoSize = true;
        l.Dock = DockStyle.Top;
        l.UseMnemonic = false;
        l.Margin = new Padding(0, 0, 8, 6);
        return l;
    }

    PictureBox MakeShot(string fileName, float s) {
        PictureBox p = new PictureBox();
        p.SizeMode = PictureBoxSizeMode.Zoom;
        p.Dock = DockStyle.Top;
        p.Width = ScalePx(s, 440);
        p.Height = ScalePx(s, 176);
        p.MinimumSize = new Size(ScalePx(s, 280), ScalePx(s, 140));
        p.Margin = new Padding(0, 0, 10, 8);
        p.BackColor = Color.FromArgb(28, 28, 34);
        p.BorderStyle = BorderStyle.FixedSingle;
        Image img = TryLoadAsset(fileName);
        if (img != null) {
            loaded.Add(img);
            p.Image = img;
        }
        return p;
    }

    static Image TryLoadAsset(string fileName) {
        try {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string path = Path.Combine(dir, "assets", fileName);
            if (!File.Exists(path)) return null;
            using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(path)))
            using (Image tmp = Image.FromStream(ms))
                return new Bitmap(tmp);
        } catch {
            return null;
        }
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        try {
            Control origin = Owner != null ? (Control)Owner : this;
            wa = Screen.FromControl(origin).WorkingArea;
        } catch { }
        int maxW = Math.Max(640, wa.Width - 24);
        int maxH = Math.Max(420, wa.Height - 24);
        if (Width > maxW) Width = maxW;
        if (Height > maxH) Height = maxH;
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        FitWrap();
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        FitWrap();
    }

    void FitWrap() {
        int inner = ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8;
        if (inner < 200) inner = 200;
        foreach (Control c in Controls) {
            TableLayoutPanel root = c as TableLayoutPanel;
            if (root == null) continue;
            foreach (Control kid in root.Controls) {
                Panel scroll = kid as Panel;
                if (scroll == null) continue;
                foreach (Control innerC in scroll.Controls) {
                    TableLayoutPanel body = innerC as TableLayoutPanel;
                    if (body == null) continue;
                    ApplyMaxWidth(body, inner);
                }
            }
        }
    }

    static void ApplyMaxWidth(Control parent, int inner) {
        foreach (Control c in parent.Controls) {
            Label l = c as Label;
            if (l != null) l.MaximumSize = new Size(inner, 0);
            TableLayoutPanel t = c as TableLayoutPanel;
            if (t != null) ApplyMaxWidth(t, Math.Max(160, (inner / 2) - 12));
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            for (int i = 0; i < loaded.Count; i++) {
                if (loaded[i] != null) loaded[i].Dispose();
            }
            loaded.Clear();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Short summary plus collapsible technical dump. Replaces verbose MessageBox for mapping.</summary>
class MoreInfoDialog : Form {
    const int Pad = 12;
    const int FormW = 470;
    Label body;
    Button more;
    TextBox dump;
    Button ok;
    Button cancel;
    bool expanded;

    public static DialogResult Confirm(IWin32Window owner, string title, string summary, string details) {
        using (MoreInfoDialog f = new MoreInfoDialog(title, summary, details, true))
            return owner != null ? f.ShowDialog(owner) : f.ShowDialog();
    }

    public static void Notify(IWin32Window owner, string title, string summary, string details, bool warning) {
        using (MoreInfoDialog f = new MoreInfoDialog(title, summary, details, false)) {
            if (warning) f.Text = title;
            if (owner != null) f.ShowDialog(owner);
            else f.ShowDialog();
        }
    }

    MoreInfoDialog(string title, string summary, string details, bool confirm) {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9f);

        body = new Label();
        body.AutoSize = false;
        body.Text = summary == null ? "" : summary;
        body.Location = new Point(Pad, Pad);
        body.Size = new Size(FormW - Pad * 2, 92);
        Controls.Add(body);

        more = new Button();
        more.Text = "More Information >>";
        more.Location = new Point(Pad, body.Bottom + 6);
        more.Size = new Size(160, 26);
        more.Click += MoreClick;
        more.Visible = details != null && details.Length > 0;
        Controls.Add(more);

        dump = new TextBox();
        dump.Multiline = true;
        dump.ReadOnly = true;
        dump.ScrollBars = ScrollBars.Vertical;
        dump.WordWrap = true;
        dump.Font = new Font("Consolas", 8f);
        dump.Text = details == null ? "" : details;
        dump.HideSelection = true;
        dump.Visible = false;
        dump.Location = new Point(Pad, more.Bottom + 8);
        dump.Size = new Size(FormW - Pad * 2, 240);
        Controls.Add(dump);

        ok = new Button();
        ok.Text = confirm ? "OK" : "OK";
        ok.DialogResult = DialogResult.OK;
        ok.Size = new Size(88, 26);

        cancel = new Button();
        cancel.Text = "Cancel";
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Size = new Size(88, 26);
        cancel.Visible = confirm;

        AcceptButton = ok;
        CancelButton = confirm ? (IButtonControl)cancel : ok;
        Controls.Add(ok);
        Controls.Add(cancel);
        PlaceButtons();
        ClientSize = new Size(FormW, ButtonRowTop() + ok.Height + Pad);
    }

    int ButtonRowTop() {
        if (expanded) return dump.Bottom + 10;
        if (more.Visible) return more.Bottom + 10;
        return body.Bottom + 10;
    }

    void PlaceButtons() {
        int y = ButtonRowTop();
        int right = FormW - Pad;
        if (cancel.Visible) {
            cancel.Location = new Point(right - cancel.Width, y);
            ok.Location = new Point(cancel.Left - 8 - ok.Width, y);
        } else {
            ok.Location = new Point(right - ok.Width, y);
        }
    }

    void MoreClick(object sender, EventArgs e) {
        expanded = !expanded;
        dump.Visible = expanded;
        more.Text = expanded ? "More Information <<" : "More Information >>";
        PlaceButtons();
        ClientSize = new Size(FormW, ButtonRowTop() + ok.Height + Pad);
    }
}

/// <summary>
/// Green primary: emoji from Segoe UI Emoji, bold Hub from Segoe UI. Avoids tofu on the pointing-hand glyph.
/// </summary>
class HubPrimaryButton : Button {
    public Font EmojiFont;
    public Font LabelFont;

    public HubPrimaryButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UseVisualStyleBackColor = false;
        UseCompatibleTextRendering = false;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent) { }

    protected override void OnPaint(PaintEventArgs e) {
        Rectangle rc = ClientRectangle;
        Color bg = Enabled ? BackColor : Color.FromArgb(70, 90, 78);
        using (SolidBrush b = new SolidBrush(bg))
            e.Graphics.FillRectangle(b, rc);
        const string emoji = "\U0001FAF5";
        const string label = " Hub";
        TextFormatFlags tf = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
            | TextFormatFlags.PreserveGraphicsClipping;
        Font ef = EmojiFont != null ? EmojiFont : Font;
        Font lf = LabelFont != null ? LabelFont : Font;
        Size es = TextRenderer.MeasureText(e.Graphics, emoji, ef, new Size(int.MaxValue, int.MaxValue), tf);
        Size ls = TextRenderer.MeasureText(e.Graphics, label, lf, new Size(int.MaxValue, int.MaxValue), tf);
        int x = rc.Left + Math.Max(0, (rc.Width - es.Width - ls.Width) / 2);
        int ye = rc.Top + Math.Max(0, (rc.Height - es.Height) / 2);
        int yl = rc.Top + Math.Max(0, (rc.Height - ls.Height) / 2);
        Color fg = Enabled ? ForeColor : Color.FromArgb(90, 100, 94);
        TextRenderer.DrawText(e.Graphics, emoji, ef, new Rectangle(x, ye, Math.Max(es.Width, 1), Math.Max(es.Height, 1)), fg, tf);
        TextRenderer.DrawText(e.Graphics, label, lf, new Rectangle(x + es.Width, yl, Math.Max(ls.Width, 1), Math.Max(ls.Height, 1)), fg, tf);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(rc.X + 3, rc.Y + 3, rc.Width - 6, rc.Height - 6));
    }
}
