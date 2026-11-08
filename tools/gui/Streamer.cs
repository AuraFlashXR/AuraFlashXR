// AuraFlashXR.exe: the runtime window. Checks the GPU and the OpenXR registration, starts the streamer host as soon as it opens
// (no start button), shows the headset / game state and the key numbers every 10 s, and offers the live settings (display gamma,
// debug logging). Pairing and the one-time settings live in configure.exe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AuraFlashXR
{
    public class RuntimeForm : DpiForm
    {
        readonly AppSettings settings = new AppSettings();
        readonly HostProcess host = new HostProcess();
        readonly AudioRoute audio = new AudioRoute();
        // DESIGN 2026 : statut casque dans une bulle (pastille verte/orange/rouge selon l'état de liaison)
        // bloc de statut unifie : texte simple (plus de pilules), la couleur d'etat passe par ForeColor
        readonly Label headset = Ui.Line(Lang.Get("Streamer.HeadsetNone"));
        // bouton dans la bulle casque : 20 % moins haut et 10 % plus etroit que le bouton standard
        readonly ModernButton find = new ModernButton { Text = Lang.Get("Streamer.BtnFindHeadset"), AutoSize = true, MinimumSize = new Size(0, 24), Padding = new Padding(9, 1, 9, 1), Margin = new Padding(8, 0, 2, 0), AccentTint = Color.FromArgb(0xFF, 0x98, 0x00), ActiveState = true };
        readonly Label findHint = Ui.Line(Lang.Get("Streamer.FindHint"));
        // Windows Firewall: the rule belongs to this exact folder; a missing or blocking rule means no discovery and no connection
        readonly Label fwWarn = Ui.Line("", Ui.Bold);
        readonly ModernButton fwFix = new ModernButton { Text = Lang.Get("Streamer.BtnAllowFirewall"), AutoSize = true, AccentTint = Color.Firebrick };
        readonly Dictionary<string, object> check;
        readonly bool autoFind;
        bool finding, gameRunning;
        readonly Label game = Ui.Line(Lang.Get("Streamer.GameNone"));
        readonly Label runtime = Ui.Line("");
        Action refreshRtStatus;
        readonly ModernButton btnHostToggle = new ModernButton { Text = Lang.Get("Streamer.BtnHostActive"), AutoSize = false, Dock = DockStyle.Fill, Height = 24, Margin = new Padding(4, 2, 4, 2), Font = Ui.SemiBold };
        readonly ModernButton btnLaunchSteamVr = new ModernButton { Text = Lang.Get("Streamer.BtnLaunchSteamVr"), AutoSize = false, Dock = DockStyle.Fill, Height = 24, Margin = new Padding(4, 2, 4, 2) };
        readonly ModernButton btnAuraFlash = new ModernButton { Text = Lang.Get("Launcher.Btn.ActivateAuraFlashXr"), AutoSize = false, Dock = DockStyle.Fill, Height = 24, Margin = new Padding(4, 2, 4, 2), Font = Ui.SemiBold };
        readonly ModernButton btnSteamVr = new ModernButton { Text = Lang.Get("Launcher.Btn.ActivateSteamVr"), AutoSize = false, Dock = DockStyle.Fill, Height = 24, Margin = new Padding(4, 2, 4, 2) };
        readonly ModernButton btnAdb = new ModernButton { Text = Lang.Get("Launcher.Btn.EnableAdb"), AutoSize = false, Dock = DockStyle.Fill, Height = 24, Margin = new Padding(4, 2, 4, 2) };
        readonly ModernButton btnInstallApk = new ModernButton { Text = Lang.Get("Launcher.Btn.InstallQuestApk"), AutoSize = false, Dock = DockStyle.Fill, Height = 24, Margin = new Padding(4, 2, 4, 2) };
        Process adbProcess = null;
        // DESIGN 2026 : les lignes de statut (casque, jeu, runtime, encodeur) vivent dans des bulles arrondies
        // (pastille de couleur d'état à gauche) au lieu de texte brut.
        readonly Label encInfo = Ui.Line(Lang.Get("Streamer.EncoderInit"));
        // ligne « Resolution » : oeil, image encodee et taux de compression foveee
        readonly Label resInfo = Ui.Line("");
        // badge CONFIG CHARGEE, a droite de la ligne encodeur
        readonly Label presetBadge = new Label { AutoSize = false, Visible = false, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Color.FromArgb(0xFF, 0xC8, 0x64), BackColor = Color.FromArgb(0x1A, 0x22, 0x30), Padding = new Padding(10, 0, 10, 0), Margin = new Padding(10, 0, 0, 0) };

        /// DESIGN 2026 : le preset chargé s'affiche en badge à droite de la bulle encodeur (comme la bulle
        /// CONFIG CHARGÉE de configure.exe) : « HIGH HEVC 10-bit ».
        void RefreshPresetBadge()
        {
            var active = Preset.IsActiveDefault ? Preset.DefaultName : Preset.Active;
            presetBadge.Text = string.Format(Lang.Get("Config.Bubble"), active);
            presetBadge.Visible = presetBadge.Text.Length > 0;
        }
        // the Streaming box: tiles (live numbers during a game, the last session's after it) and one line of text under them
        readonly Tile tGame = Ui.MakeTile(Lang.T("FPS jeu")), tStreamed = Ui.MakeTile(Lang.T("FPS diffusé")), tShown = Ui.MakeTile(Lang.T("FPS casque")), tBitrate = Ui.MakeTile(Lang.T("Débit")), tLatency = Ui.MakeTile(Lang.T("Latence")), tLoss = Ui.MakeTile(Lang.T("Pertes"));
        readonly Label stats = Ui.Line(Lang.Get("Streamer.StatsWaiting"));
        // the Streaming box shows live numbers while a game runs and, once it has ended, the run's record and what will be proposed next time
        readonly ModernGroupBox statsBox = new ModernGroupBox { Text = Lang.Get("Streamer.BoxLiveStreaming"), AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 6) };
        // refresh interval of the live numbers, in the title bar of the Streaming box (1..10 s)
        readonly ModernDropdownButton statsEveryDrop = new ModernDropdownButton();
        /// allowed values (seconds) for the statistics refresh interval
        static readonly int[] StatsEverySecs = { 1, 2, 3, 4, 5, 10 };
        bool lastSession;
        // display controls: (label, min, max, default, settings getter/setter), values x100 on the track bars
        class Knob { public string Name; public TrackBar Bar; public Label Value; public double Def; public Func<double> Get; public Action<double> Set; }
        readonly List<Knob> knobs = new List<Knob>();
        readonly CheckBox debug = new CheckBox { Text = Lang.Get("Streamer.CheckDebugLogging"), AutoSize = true, Font = Ui.SemiBold, ForeColor = Ui.DarkMuted };
        readonly ToggleSwitch frameGen = new ToggleSwitch { Margin = new Padding(8, 2, 12, 2), AccessibleName = "Frame generation" };
        readonly ToggleSwitch postProcToggle = new ToggleSwitch { Margin = new Padding(6, 2, 16, 2), AccessibleName = "Post-Processing" };
        readonly Segmented sunGlasses = new Segmented("Off", "Light", "Dark", "TruNite") { AccessibleName = "Sun Glasses", Margin = new Padding(0, 2, 0, 0), Tint = Color.FromArgb(0x3A, 0x44, 0x58) };
        readonly ToggleSwitch mirror = new ToggleSwitch { Margin = new Padding(3, 4, 8, 3), AccessibleName = "Mirror the Windows desktop between sessions" };
        // the game's rate as a fraction of the display rate: 1/3, 1/2 (default), 2/3 (settings video.frame_gen_rate third | half | two_thirds)
        static readonly string[] FgRates = { "third", "half", "two_thirds" };
        // resolved lazily via FgRateTextAt() because Lang.Init() runs in the constructor, AFTER static field initializers.
        static string FgRateTextAt(int i)
        {
            switch (i)
            {
                case 0: return Lang.Get("Streamer.FgRateThird");
                case 1: return Lang.Get("Streamer.FgRateHalf");
                default: return Lang.Get("Streamer.FgRateTwoThirds");
            }
        }
        readonly Segmented fgRate = new Segmented("1/3", "1/2", "2/3") { AccessibleName = "Game rate", Margin = new Padding(0, 2, 0, 0) };
        class TipText { public Action<string> On; public string Text { set { On?.Invoke(value); } } }   // explanations go to tooltips, not to the window
        readonly TipText fgRateText = new TipText();
        readonly Segmented heightMode = new Segmented(Lang.Get("Streamer.ModeAuto"), Lang.Get("Streamer.ModeManual")) { AccessibleName = "Eye height mode", Margin = new Padding(0, 2, 0, 0) };
        readonly NumericUpDown heightBox = new NumericUpDown { DecimalPlaces = 2, Increment = 0.05m, Minimum = 0.5m, Maximum = 2.2m, Width = 68, BackColor = Color.FromArgb(0x10, 0x17, 0x24), ForeColor = Ui.DarkText, BorderStyle = BorderStyle.FixedSingle, Font = Ui.SemiBold, AccessibleName = "Eye height in metres" };
        readonly Label heightReport = new Label { AutoSize = true, ForeColor = Ui.DarkMuted, Margin = new Padding(8, 7, 0, 0), Visible = false };
        readonly Label audioLine = new Label { AutoSize = true, Visible = false, Margin = new Padding(4, 2, 4, 0), MaximumSize = new Size(1100, 0) };   // the virtual audio device's volume, only when it matters
        readonly Label exportStatus = new Label { AutoSize = true, Margin = new Padding(4, 2, 4, 0), MaximumSize = new Size(900, 0) };
        readonly LinkLabel exportLink = new LinkLabel { Text = Lang.Get("Streamer.LinkOpenFolder"), AutoSize = true, Margin = new Padding(4, 2, 4, 0) };
        // uncapped generation (settings video.frame_gen_rate uncapped, docs/FRAME_GENERATION_UNCAPPED.md): the game runs at its own rate, every output interpolated
        // experimental, set in the Advanced settings window: here only the state, with a note under the slider while it is on
        readonly CheckBox fgUnc = new CheckBox { Text = "Uncapped", AutoSize = true, Visible = false };
        readonly Label fgUncNote = new Label { Text = Lang.Get("Streamer.FgUncNote"), AutoSize = true, ForeColor = Color.DarkGoldenrod, Visible = false, Margin = new Padding(3, 0, 0, 2) };
        readonly ToggleButton fgRecommend = new ToggleButton { Text = "Auto", Margin = new Padding(0, 2, 8, 0), AccessibleName = "Choose the game rate automatically from this game's recorded runs" };
        readonly ModernButton recenter = new ModernButton { Text = Lang.Get("Streamer.BtnRecenter"), AutoSize = true, Margin = new Padding(10, 2, 4, 2) };
        bool fgRateFromHost;   // the slider follows the host's rate: no command back
        // Game profiles (config\games\<exe>.json, written by the streamer at every game end: per game, headset and level the last run's numbers). The
        // launcher proposes a level from them when "Recommended" is ticked: the slider is set and greyed; unticked, the slider is yours again.
        // No run recorded for the game on this headset: nothing proposed, the slider stays live. The streamer itself decides nothing.
        string currentExe = "", headsetName = "";
        bool recommendationApplied;
        readonly Label footer = Ui.Line("", new Font("Segoe UI", 8.5f));
        string pairedName = "", pairedIp = "";
        bool closing;
        int restarts;
        string setupError;   // the streamer's `setup_error` message: a problem to fix, restarting it would not help

        public RuntimeForm(Dictionary<string, object> check, bool autoFind = false)
        {
            Lang.Init();
            this.check = check;
            this.autoFind = autoFind;
            Text = "AuraFlashXR";
            Icon = Ui.AppIcon();
            AutoScaleDimensions = new SizeF(96F, 96F);   // the pixel sizes below are designed at 96 dpi; without this nothing scales and only the fonts grow
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Body;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;   // like AuraFlashXR's dashboard, not the top-left corner (user, 2026-10-17)
            MaximizeBox = false;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;

            const int TargetWidth = 1210;

            // the window is as big as its content, up to the work area; beyond that the body scrolls (Ui.AutoFitWindow)
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.None,
                Location = Point.Empty,
                AutoSize = true,
                ColumnCount = 1,
                MinimumSize = new Size(TargetWidth, 0),
                Padding = new Padding(14, 8, 14, 12),
                BackColor = Ui.DarkBg
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var gpu = check != null && check.TryGetValue("gpu", out var g) ? g as Dictionary<string, object> : null;
            Action refreshAdb = () =>
            {
                bool adbOn = (adbProcess != null && !adbProcess.HasExited) || Process.GetProcessesByName("ADBForwarder").Length > 0;
                btnAdb.Text = adbOn ? Lang.Get("Launcher.Btn.DisableAdb") : Lang.Get("Launcher.Btn.EnableAdbUsb");
                btnAdb.ActiveState = adbOn;
                btnAdb.AccentTint = adbOn ? Color.FromArgb(0x28, 0xC7, 0x6F) : (Color?)null;
            };

            Action refreshRt = () =>
            {
                bool ours = OpenXr.IsOurs();
                var cur = OpenXr.ActiveRuntime();
                string curName = ours ? Lang.Get("Streamer.RuntimeDirect") : (cur.Contains("steamxr") ? Lang.Get("Streamer.RuntimeSteamVrDriver") : (cur.Length > 0 ? OpenXr.Name(cur) + Lang.Get("Streamer.RuntimeExternalSuffix") : Lang.Get("Streamer.RuntimeNotRegistered")));
                runtime.Text = string.Format(Lang.Get("Streamer.RuntimeLine"), S(gpu, "name"), S(gpu, "arch_name"), S(gpu, "driver"), curName);   // nom de la puce graphique retire, pilote conserve
                runtime.ForeColor = Color.FromArgb(0x76, 0xB9, 0x00);   // vert NVIDIA


                btnAuraFlash.Text = ours ? "AuraFlashXR Active" : "Activate AuraFlashXR";
                btnAuraFlash.ActiveState = ours;
                btnAuraFlash.AccentTint = ours ? Ui.AccentLight : (Color?)null;

                bool isSteam = cur.Contains("steamxr");
                btnSteamVr.Text = isSteam ? Lang.Get("Launcher.Btn.SteamVrActive") : Lang.Get("Launcher.Btn.ActivateSteamVr");
                btnSteamVr.ActiveState = isSteam;
                btnSteamVr.AccentTint = isSteam ? Color.FromArgb(0x28, 0xC7, 0x6F) : (Color?)null;

                bool hostOn = host.Running;
                btnHostToggle.Text = hostOn ? Lang.Get("Streamer.BtnHostActive") : Lang.Get("Streamer.BtnHostInactive");
                btnHostToggle.ActiveState = hostOn;
                btnHostToggle.AccentTint = hostOn ? Color.FromArgb(0x28, 0xC7, 0x6F) : Color.Firebrick;

                bool vrRunning = Process.GetProcessesByName("vrserver").Length > 0;
                btnLaunchSteamVr.Text = vrRunning ? Lang.Get("Streamer.BtnSteamVrRunning") : Lang.Get("Streamer.BtnLaunchSteamVr");
                btnLaunchSteamVr.ActiveState = vrRunning;
                btnLaunchSteamVr.AccentTint = vrRunning ? Ui.AccentLight : (Color?)null;

                refreshAdb();
            };
            refreshRtStatus = refreshRt;

            btnHostToggle.Click += (s, e) =>
            {
                if (host.Running)
                {
                    manualStop = true;   // distinguish a manual stop from a crash so HostExited does not auto-restart
                    host.Stop(1500);
                    Log.Write("INFO", "auraflashxr_host.exe stopped manually via Streamer GUI");
                }
                else
                {
                    steamVrYield = false;
                    StartHost();
                    Log.Write("INFO", "auraflashxr_host.exe started manually via Streamer GUI");
                }
                refreshRt();
            };

            btnLaunchSteamVr.Click += (s, e) =>
            {
                if (Process.GetProcessesByName("vrserver").Length > 0)
                {
                    refreshRt();
                    return;
                }
                if (!LaunchSteamVr())
                    MessageBox.Show(this, Lang.Get("Streamer.SteamVrNotFound"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                refreshRt();
            };
            btnInstallApk.Click += (s, e) =>
            {
                string scriptPath = Path.Combine(Paths.Dir, "tools", "install", "install_apk_quest.bat");
                if (!File.Exists(scriptPath))
                    scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "install", "install_apk_quest.bat");
                if (!File.Exists(scriptPath))
                    scriptPath = Path.Combine(Paths.Dir, "..", "tools", "install", "install_apk_quest.bat");

                if (!File.Exists(scriptPath))
                {
                    MessageBox.Show(this, Lang.Get("Streamer.ScriptNotFound") + scriptPath, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = scriptPath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Lang.Get("Streamer.ApkInstallError") + ex.Message, Lang.Get("Streamer.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            refreshRt();

            btnAuraFlash.Click += (s, e) =>
            {
                OpenXr.Register(this);
                refreshRt();
            };

            btnSteamVr.Click += (s, e) =>
            {
                if (!OpenXr.SetSteamVr()) OpenXr.Unregister();
                refreshRt();
            };

            btnAdb.Click += (s, e) =>
            {
                bool adbOn = (adbProcess != null && !adbProcess.HasExited) || Process.GetProcessesByName("ADBForwarder").Length > 0;
                if (adbOn)
                {
                    try { if (adbProcess != null && !adbProcess.HasExited) adbProcess.Kill(); } catch { }
                    foreach (var p in Process.GetProcessesByName("ADBForwarder")) { try { p.Kill(); } catch { } }
                    adbProcess = null;
                }
                else
                {
                    string adbExe = Paths.AdbForwarder;
                    if (File.Exists(adbExe))
                    {
                        try
                        {
                            var psi = new ProcessStartInfo(adbExe) { WorkingDirectory = Path.GetDirectoryName(adbExe), UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized };
                            adbProcess = Process.Start(psi);
                        }
                        catch (Exception ex) { MessageBox.Show(this, Lang.Get("Streamer.AdbError") + ex.Message, Lang.Get("App.Title")); }
                    }
                    else
                    {
                        MessageBox.Show(this, Lang.Get("Streamer.AdbNotFound") + adbExe, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                refreshAdb();
            };

            // =========================================================================
            // 1. TOP BENTO STATUS GRID (3 rows: Casque+Bouton / Jeu+GPU / Encodeur)
            // =========================================================================
            encInfo.ForeColor = Ui.AccentLight;
            find.Click += (s, e) => FindHeadset(false);
            findHint.ForeColor = Ui.DarkMuted;
            findHint.Font = new Font("Segoe UI", 8.5f);
            tips.SetToolTip(find, Lang.Get("Streamer.TipFindHeadset"));

            // bloc de statut unifie : 3 lignes pleine largeur separees par des filets fins
            var border = Color.FromArgb(0x22, 0x2A, 0x38);
            // panneau « INFORMATION EN DIRECT » : meme cadre que la Diffusion en direct, 4 lignes de statut
            // padding haut 26 (titre) et bas 26 : le bloc est centre entre le titre et la bordure basse
            var infoBox = new ModernGroupBox { Text = Lang.Get("Streamer.InfoBoxTitle"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 6), MinimumSize = new Size(TargetWidth - 28, 0), Padding = new Padding(12, 15, 12, 12) };
            // le bloc occupe toute la hauteur disponible du panneau et ses 4 lignes sont centrees verticalement.
            // Il est ajoute APRES les controles du panneau pour rester au premier plan (sinon le panneau le masque).
            var topBento = new Panel { AutoSize = false, Height = 132, Dock = DockStyle.Top, BackColor = Color.FromArgb(0x11, 0x16, 0x20), Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = new Padding(0) };
            rows.RowCount = 4;
            for (int r = 0; r < 4; r++) rows.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
            topBento.Controls.Add(rows);
            topBento.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var pen = new Pen(border, 1)) g.DrawRectangle(pen, 0, 0, topBento.Width - 1, topBento.Height - 1);
                // filets horizontaux entre les lignes
                using (var pen = new Pen(border, 1))
                {
                    for (int r = 1; r < 4; r++)
                    {
                        int y = 4 + r * (topBento.Height - 8) / 4;
                        g.DrawLine(pen, 1, y, topBento.Width - 1, y);
                    }
                }
            };

            // ligne 1 : casque a gauche, bouton a droite
            var row1 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headset.AutoSize = false; headset.Dock = DockStyle.Fill; headset.TextAlign = ContentAlignment.MiddleLeft; headset.MaximumSize = Size.Empty;
            find.AutoSize = false;
            find.Size = new Size(Math.Max(90, (int)(TextRenderer.MeasureText(find.Text, Ui.SemiBold).Width * 0.9) + 14), 22);
            find.Anchor = AnchorStyles.Right;
            find.Margin = new Padding(10, 0, 0, 0);
            // meme taille de police pour GPU, jeu, casque et encodeur
            var statusFont = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Point);   // 13 pt semi-gras
            runtime.Font = statusFont; game.Font = statusFont; headset.Font = statusFont; encInfo.Font = statusFont;
            // majuscule au premier mot de chaque segment (apres « : » et apres « | »)
            Action<Label> upperSegments = lbl => lbl.TextChanged += (s, e) =>
            {
                var txt = lbl.Text;
                if (string.IsNullOrEmpty(txt)) return;
                var sb = new System.Text.StringBuilder(txt);
                bool up = true;
                for (int i = 0; i < sb.Length; i++)
                {
                    if (up && char.IsLetter(sb[i])) { sb[i] = char.ToUpperInvariant(sb[i]); up = false; }
                    if (sb[i] == ':' || sb[i] == '|') up = true;
                }
                var res = sb.ToString();
                if (res != txt) lbl.Text = res;
            };
            foreach (var l in new[] { runtime, game, headset, encInfo, resInfo }) upperSegments(l);
            // texte centre horizontalement dans chaque case
            foreach (var l in new[] { runtime, game, headset, encInfo, resInfo }) l.TextAlign = ContentAlignment.MiddleCenter;
            // ligne 1 : GPU seul sur toute la largeur
            runtime.AutoSize = false; runtime.Dock = DockStyle.Fill; runtime.TextAlign = ContentAlignment.MiddleLeft; runtime.MaximumSize = Size.Empty; runtime.AutoEllipsis = true;
            row1.Controls.Add(runtime, 0, 0);
            row1.SetColumnSpan(runtime, 2);
            rows.Controls.Add(row1, 0, 0);

            // ligne 2 : jeu | casque, trait vertical au milieu
            var row2 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            foreach (var l in new[] { game, headset })
            {
                l.AutoSize = false; l.Dock = DockStyle.Fill; l.TextAlign = ContentAlignment.MiddleLeft; l.MaximumSize = Size.Empty;
                l.AutoEllipsis = true;
            }
            game.Margin = new Padding(0, 0, 8, 0);
            headset.Margin = new Padding(10, 0, 0, 0);
            // le bouton « Trouver mon casque » est dans la case du casque, a droite de son texte
            var headCell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(10, 0, 0, 0), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            headCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headCell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headCell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            headset.Margin = new Padding(0);
            headCell.Controls.Add(headset, 0, 0);
            headCell.Controls.Add(find, 1, 0);
            row2.Controls.Add(game, 0, 0);
            row2.Controls.Add(headCell, 1, 0);
            row2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            row2.Paint += (s, e) =>
            {
                using (var pen = new Pen(border, 1)) e.Graphics.DrawLine(pen, row2.Width / 2, 3, row2.Width / 2, row2.Height - 3);
            };
            rows.Controls.Add(row2, 0, 1);

            // ligne 3 : encodeur a gauche (ellipse si trop long), badge CONFIG CHARGEE a droite
            var row3 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            row3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            encInfo.AutoSize = false; encInfo.Dock = DockStyle.Fill; encInfo.TextAlign = ContentAlignment.MiddleLeft; encInfo.MaximumSize = Size.Empty; encInfo.AutoEllipsis = true;
            // style copie du bouton « AuraFlashXR Active » de la TOOLBOX (etat actif, teinte accent) — sans lien avec lui
            presetBadge.AutoSize = false;
            presetBadge.Height = 24;
            presetBadge.Margin = new Padding(10, 0, 0, 0);
            presetBadge.Anchor = AnchorStyles.Right;
            presetBadge.Font = Ui.SemiBold;
            presetBadge.ForeColor = Color.White;
            presetBadge.Width = Math.Max(150, (int)(TextRenderer.MeasureText(string.Format(Lang.Get("Config.Bubble"), "MON PRESET"), Ui.SemiBold).Width) + 26);
            presetBadge.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Ui.DarkCard);
                var ac = Ui.AccentLight;
                var r = new Rectangle(1, 1, presetBadge.Width - 3, presetBadge.Height - 3);
                using (var path = Ui.RoundRect(r, 8))
                {
                    using (var b = new SolidBrush(Color.FromArgb(45, ac.R, ac.G, ac.B))) g.FillPath(b, path);
                    using (var p = new Pen(ac, 1f)) g.DrawPath(p, path);
                }
                TextRenderer.DrawText(g, presetBadge.Text, Ui.SemiBold, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            };
            row3.Controls.Add(encInfo, 0, 0);
            row3.Controls.Add(presetBadge, 1, 0);
            rows.Controls.Add(row3, 0, 2);

            // ligne 4 : resolution (oeil, image encodee, taux de compression foveee)
            var row4 = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            row4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row4.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resInfo.AutoSize = false; resInfo.Dock = DockStyle.Fill; resInfo.TextAlign = ContentAlignment.MiddleLeft; resInfo.MaximumSize = Size.Empty; resInfo.AutoEllipsis = true;
            resInfo.Font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Point);
            resInfo.ForeColor = Ui.GlowYellow;
            row4.Controls.Add(resInfo, 0, 0);
            rows.Controls.Add(row4, 0, 3);
            infoBox.Controls.Add(topBento);
            body.Controls.Add(infoBox);

            var fwRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = Ui.DarkBg };
            fwRow.Controls.Add(fwWarn); fwRow.Controls.Add(fwFix);
            body.Controls.Add(fwRow);
            fwWarn.ForeColor = Color.Firebrick;
            fwWarn.Visible = fwFix.Visible = false;
            fwFix.Click += (s, e) => { Firewall.Allow(Paths.Host); CheckFirewall(); };

            // =========================================================================
            // 2. DIFFUSION BOX WITH 6 FULL-WIDTH TILES (6 equal columns)
            // =========================================================================
            var box = statsBox;
            box.MinimumSize = new Size(TargetWidth - 28, 0);
            statsBox.ForeColor = Ui.DarkText;
            // titre du panneau en MAJUSCULES
            statsBox.TextChanged += (s, e) => { var up = statsBox.Text.ToUpperInvariant(); if (up != statsBox.Text) statsBox.Text = up; };
            statsBox.Text = statsBox.Text.ToUpperInvariant();
            stats.Font = new Font("Segoe UI", 8.5f);
            stats.MinimumSize = new Size(0, 24);
            stats.ForeColor = Ui.DarkMuted;
            stats.BackColor = Ui.DarkCard;
            stats.Margin = new Padding(4, 4, 4, 2);

            var tiles = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 6,
                RowCount = 1,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(TargetWidth - 52, 72),
                Font = Ui.Body,
                BackColor = Ui.DarkCard,
                ForeColor = Ui.DarkText,
                Margin = new Padding(0, 2, 0, 2)
            };
            for (int c = 0; c < 6; c++)
                tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666f));

            var tileArray = new[] { tGame, tStreamed, tShown, tBitrate, tLatency, tLoss };
            for (int i = 0; i < tileArray.Length; i++)
            {
                tileArray[i].Dock = DockStyle.Fill;
                tileArray[i].Margin = new Padding(i == 0 ? 0 : 4, 2, i == tileArray.Length - 1 ? 0 : 4, 2);
                tiles.Controls.Add(tileArray[i], i, 0);
            }
            tLoss.Paint += (s, e) =>
            {
                bool zero = tLoss.Value == "0" || string.IsNullOrEmpty(tLoss.Value) || tLoss.Value.Trim() == "-";
                using (var pen = new Pen(zero ? Color.FromArgb(0x28, 0xC7, 0x6F) : Color.Firebrick, 1.5f))
                    e.Graphics.DrawRectangle(pen, 1, 1, tLoss.Width - 3, tLoss.Height - 3);
            };

            var statsCol = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill, Font = Ui.Body, BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            statsCol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            statsCol.Controls.Add(tiles, 0, 0);

            int statsEverySaved = settings.StatsRefreshSecs;
            if (statsEverySaved < 1 || statsEverySaved > 10) statsEverySaved = 10;
            int statsEveryIdx = StatsEverySecs.Length - 1;
            for (int k = 0; k < StatsEverySecs.Length; k++) { if (StatsEverySecs[k] == statsEverySaved) { statsEveryIdx = k; break; } }
            statsEveryDrop.SetItems(StatsEverySecs.Select(v => v + " s"), statsEveryIdx);
            statsEveryDrop.AccessibleName = Lang.Get("Streamer.StatsEveryHint");
            statsEveryDrop.SelectedIndexChanged += (se, ev) =>
            {
                int secs = StatsEverySecs[statsEveryDrop.SelectedIndex];
                settings.StatsRefreshSecs = secs;
                settings.Save();
                host.Send("status_secs " + secs);
                statsBox.Text = string.Format(Lang.Get("Streamer.BoxLiveStreamingFormat"), secs);
                stats.Text = string.Format(Lang.Get("Streamer.StatsWaitingFormat"), secs);
                Log.Write("INFO", "statistics refresh interval: " + secs + " s");
            };
            tips.SetToolTip(statsEveryDrop, Lang.Get("Streamer.StatsEveryHint"));
            statsBox.HeaderControl = statsEveryDrop;
            statsBox.Controls.Add(statsEveryDrop);
            statsBox.Text = string.Format(Lang.Get("Streamer.BoxLiveStreamingFormat"), StatsEverySecs[statsEveryIdx]);
            statsCol.Controls.Add(stats, 0, 1);
            box.Controls.Add(statsCol);
            body.Controls.Add(box);

            // =========================================================================
            // 3. TWO-COLUMN STACKED LAYOUT (Image 2 structure)
            // Left Col: Génération d'images (compact bar) + Post-Processing
            // Right Col: TOOLBOX AFXR (3x2 grid) + Position & Espace
            // =========================================================================
            if (!settings.HasDisplay) { settings.Gamma = AppSettings.DefaultGamma; settings.Save(); }
            ModernGroupBox Group(string title, Control content)
            {
                var g2 = new ModernGroupBox { Text = title.ToUpperInvariant(), AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4), Padding = new Padding(12, 24, 12, 4) };
                content.Font = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Point); content.Dock = DockStyle.Fill; content.BackColor = Ui.DarkCard;
                g2.Controls.Add(content);
                return g2;
            }
            FlowLayoutPanel Col() => new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill, BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            FlowLayoutPanel Row() => new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };

            // --- Left Top: Compact inline "Génération d'images" bar ---
            fgRateText.On = t => { foreach (var c in new Control[] { frameGen, fgRecommend }.Concat(fgRate.Controls.Cast<Control>())) tips.SetToolTip(c, t); };
            // meme cadre que la TOOLBOX AFXR (ModernGroupBox, titre en majuscules)
            var fgInlineTable = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2,
                MinimumSize = new Size(0, 60),   // meme hauteur de contenu que la TOOLBOX AFXR (copiee, pas liee)
                MaximumSize = new Size(0, 60),
                Dock = DockStyle.Fill,
                BackColor = Ui.DarkCard,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0)
            };
            fgInlineTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            fgInlineTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));

            var fgLeftFlow = Row();
            fgLeftFlow.AutoSize = false;
            fgLeftFlow.Dock = DockStyle.Fill;
            // libelle avant l'interrupteur (comme « Actif : » du Post-Processing)
            fgLeftFlow.Controls.Add(new Label { Text = Lang.Get("Streamer.LabelFrameGenActive"), AutoSize = true, Font = Ui.Bold, ForeColor = Ui.DarkText, Margin = new Padding(3, 7, 6, 0) });
            fgLeftFlow.Controls.Add(frameGen);

            var fgRightFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Dock = DockStyle.Fill, BackColor = Ui.DarkCard, Margin = new Padding(0) };
            fgRightFlow.Controls.Add(fgRate);
            fgRightFlow.Controls.Add(fgRecommend);

            fgInlineTable.Controls.Add(fgLeftFlow, 0, 0);
            fgInlineTable.Controls.Add(fgRightFlow, 1, 0);
            fgInlineTable.Controls.Add(fgUncNote, 0, 1);
            fgInlineTable.SetColumnSpan(fgUncNote, 2);

            // --- Right Top: TOOLBOX AFXR (3x2 stretched buttons) ---
            var rtGrid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(540, 60),
                BackColor = Ui.DarkCard,
                Margin = new Padding(0)
            };
            rtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            rtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            rtGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            rtGrid.Controls.Add(btnAuraFlash, 0, 0);
            rtGrid.Controls.Add(btnSteamVr, 1, 0);
            rtGrid.Controls.Add(btnHostToggle, 2, 0);
            rtGrid.Controls.Add(btnLaunchSteamVr, 0, 1);
            rtGrid.Controls.Add(btnAdb, 1, 1);
            rtGrid.Controls.Add(btnInstallApk, 2, 1);

            var grpRt = Group("TOOLBOX AFXR", rtGrid);

            // --- Right Bottom: Position & Espace ---
            var posCol = Col();
            var heightRow = Row();
            heightRow.Margin = new Padding(0, 4, 0, 10);
            heightRow.Controls.Add(new Label { Text = Lang.Get("Streamer.LabelEyeHeight"), AutoSize = true, Margin = new Padding(3, 7, 8, 0), Font = Ui.SemiBold });
            heightRow.Controls.Add(heightMode);
            heightBox.Margin = new Padding(8, 3, 2, 0);
            heightRow.Controls.Add(heightBox);
            heightRow.Controls.Add(new Label { Text = "m", AutoSize = true, Margin = new Padding(2, 7, 8, 0) });
            heightRow.Controls.Add(recenter);
            heightReport.Margin = new Padding(6, 7, 0, 0);
            heightRow.Controls.Add(heightReport);
            posCol.Controls.Add(heightRow);

            var posRow = Row();
            posRow.Margin = new Padding(0, 2, 0, 4);
            posRow.Controls.Add(mirror);
            posRow.Controls.Add(new Label { Text = Lang.Get("Streamer.LabelDesktopMirror"), AutoSize = true, Margin = new Padding(4, 6, 0, 0), Font = Ui.SemiBold });
            posCol.Controls.Add(posRow);

            var grpPos = Group(Lang.Get("Streamer.GroupPositionSpace"), posCol);
            grpPos.Margin = new Padding(0);

            // --- Left Bottom: Post-Processing ---
            var ppTopRow = Row();
            ppTopRow.Margin = new Padding(0, 0, 0, 6);
            ppTopRow.Controls.Add(new Label { Text = Lang.Get("Streamer.LabelPostProcActive"), AutoSize = true, Margin = new Padding(3, 7, 2, 0), Font = Ui.Bold });
            ppTopRow.Controls.Add(postProcToggle);
            ppTopRow.Controls.Add(new Label { Text = Lang.Get("Streamer.LabelSunGlasses"), AutoSize = true, Margin = new Padding(4, 7, 6, 0) });
            ppTopRow.Controls.Add(sunGlasses);

            var pic = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Font = Ui.Body, BackColor = Ui.DarkCard, ForeColor = Ui.DarkText, Margin = new Padding(0) };
            var picL = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 6, 0), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            var picR = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            pic.Controls.Add(picL); pic.Controls.Add(picR);
            AddKnob(picL, 0, Lang.Get("Streamer.KnobGamma"), 50, 200, AppSettings.DefaultGamma, () => settings.Gamma, v => settings.Gamma = v, Lang.Get("Streamer.TipGamma"));
            AddKnob(picR, 1, Lang.Get("Streamer.KnobSharpening"), 0, 100, 0, () => settings.Sharpening, v => settings.Sharpening = v, Lang.Get("Streamer.TipSharpening"));
            AddKnob(picL, 1, Lang.Get("Streamer.KnobContrast"), -50, 50, 0, () => settings.Contrast, v => settings.Contrast = v, Lang.Get("Streamer.TipContrast"));
            AddKnob(picR, 2, Lang.Get("Streamer.KnobBrightness"), -50, 50, 0, () => settings.Brightness, v => settings.Brightness = v, Lang.Get("Streamer.TipBrightness"));
            AddKnob(picL, 2, Lang.Get("Streamer.KnobExposure"), -50, 50, 0, () => settings.Exposure, v => settings.Exposure = v, Lang.Get("Streamer.TipExposure"));
            AddKnob(picR, 3, Lang.Get("Streamer.KnobSaturation"), -50, 50, 0, () => settings.Saturation, v => settings.Saturation = v, Lang.Get("Streamer.TipSaturation"));
            AddKnob(picL, 3, Lang.Get("Streamer.KnobVibrance"), -50, 50, 0, () => settings.Vibrance, v => settings.Vibrance = v, Lang.Get("Streamer.TipVibrance"));
            AddKnob(picR, 4, Lang.Get("Streamer.KnobHighlights"), -50, 50, 0, () => settings.Highlights, v => settings.Highlights = v, Lang.Get("Streamer.TipHighlights"));
            AddKnob(picR, 0, Lang.Get("Streamer.KnobShadows"), -50, 50, 0, () => settings.Shadows, v => settings.Shadows = v, Lang.Get("Streamer.TipShadows"));

            void ApplyPostPreset(double g, double sh, double co, double br, double ex, double sa, double vi, double hi, double sd, int sg = 0)
            {
                if (!postProcToggle.Checked) postProcToggle.Checked = true;
                sunGlasses.Value = sg;
                double[] vals = { g, sh, co, br, ex, sa, vi, hi, sd };
                for (int i = 0; i < knobs.Count && i < vals.Length; i++)
                {
                    var k = knobs[i];
                    k.Bar.Value = Math.Max(k.Bar.Minimum, Math.Min(k.Bar.Maximum, (int)Math.Round(vals[i] * 100)));
                }
            }

            var presetRow = Row();
            presetRow.Margin = new Padding(0, 8, 0, 0);
            // pas de libelle « Profils : » : les boutons commencent la ligne
            var btnPresetNatural = new ModernButton { Text = Lang.Get("Streamer.BtnPresetNatural"), AutoSize = true, Margin = new Padding(2, 2, 5, 2) };
            var btnPresetVivid = new ModernButton { Text = Lang.Get("Streamer.BtnPresetVivid"), AutoSize = true, Margin = new Padding(2, 2, 5, 2), AccentTint = Color.FromArgb(0xE8, 0x4A, 0x1E) };
            var btnPresetSharp = new ModernButton { Text = Lang.Get("Streamer.BtnPresetSharp"), AutoSize = true, Margin = new Padding(2, 2, 5, 2), AccentTint = Color.FromArgb(0x28, 0xC7, 0x6F) };
            var btnPresetCinema = new ModernButton { Text = Lang.Get("Streamer.BtnPresetCinema"), AutoSize = true, Margin = new Padding(2, 2, 10, 2), AccentTint = Color.FromArgb(0x9B, 0x6B, 0xFF) };
            var reset = new ModernButton { Text = Lang.Get("Streamer.BtnResetDefaults"), AutoSize = true, Margin = new Padding(2, 2, 2, 2) };

            btnPresetNatural.Click += (s, e) => ApplyPostPreset(1.10, 0.15, 0.00, 0.00, 0.00, 0.00, 0.05, 0.00, 0.00, 0);
            btnPresetVivid.Click += (s, e) => ApplyPostPreset(1.10, 0.35, 0.06, 0.00, 0.02, 0.15, 0.20, 0.04, -0.04, 0);
            btnPresetSharp.Click += (s, e) => ApplyPostPreset(1.15, 0.70, 0.08, 0.02, 0.05, 0.08, 0.10, -0.05, 0.12, 0);
            btnPresetCinema.Click += (s, e) => ApplyPostPreset(1.05, 0.20, 0.12, -0.02, 0.00, 0.10, 0.12, 0.05, -0.08, 1);
            reset.Click += (s, e) =>
            {
                sunGlasses.Value = 0;
                foreach (var k in knobs) k.Bar.Value = (int)Math.Round(k.Def * 100);
            };

            postProcToggle.SetCheckedInstant(settings.PostProcessingEnabled);
            sunGlasses.Value = Math.Max(0, Math.Min(3, settings.SunGlasses));
            Action syncPpUi = () =>
            {
                bool on = postProcToggle.Checked;
                pic.Enabled = on;
                sunGlasses.Enabled = on;
                presetRow.Enabled = on;
            };
            syncPpUi();
            postProcToggle.CheckedChanged += (s, e) =>
            {
                settings.PostProcessingEnabled = postProcToggle.Checked;
                settings.Save();
                syncPpUi();
                SendDisplay();
            };
            sunGlasses.ValueChanged += (s, e) =>
            {
                settings.SunGlasses = sunGlasses.Value;
                settings.Save();
                SendDisplay();
            };

            tips.SetToolTip(postProcToggle, Lang.Get("Streamer.TipPostProcToggle"));
            tips.SetToolTip(sunGlasses, Lang.Get("Streamer.TipSunGlasses"));
            tips.SetToolTip(btnPresetNatural, Lang.Get("Streamer.TipPresetNatural"));
            tips.SetToolTip(btnPresetVivid, Lang.Get("Streamer.TipPresetVivid"));
            tips.SetToolTip(btnPresetSharp, Lang.Get("Streamer.TipPresetSharp"));
            tips.SetToolTip(btnPresetCinema, Lang.Get("Streamer.TipPresetCinema"));
            tips.SetToolTip(reset, Lang.Get("Streamer.TipReset"));

            presetRow.Controls.Add(btnPresetNatural);
            presetRow.Controls.Add(btnPresetVivid);
            presetRow.Controls.Add(btnPresetSharp);
            // « Cinéma » suit « Compétitif » sur la meme ligne
            presetRow.Controls.Add(btnPresetCinema);
            presetRow.Controls.Add(reset);

            // les boutons de presets passent a la ligne au lieu de sortir du panneau (info croppee)
            presetRow.AutoSize = true;
            presetRow.WrapContents = true;
            presetRow.MaximumSize = new Size(TargetWidth / 2 - 60, 0);
            presetRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var picCol = Col();
            picCol.Controls.Add(ppTopRow);
            picCol.Controls.Add(pic);
            picCol.Controls.Add(presetRow);
            var grpPp = Group(Lang.Get("Streamer.GroupPostProcessing"), picCol);
            grpPp.Margin = new Padding(0);

            // les deux colonnes partagent les memes hauteurs de ligne (Toolbox = Generation d'images, Position = Post-Processing)
            // deux rangees de deux panneaux : chaque rangee prend la hauteur de son panneau le plus haut,
            // les deux panneaux d'une rangee ont donc la meme hauteur (les rangees ne sont pas liees entre elles)
            var grpFg = Group(Lang.Get("Streamer.GroupFrameGen"), fgInlineTable);
            var topRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, BackColor = Ui.DarkBg, Margin = new Padding(0, 0, 0, 4) };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            topRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grpFg.Margin = new Padding(0, 0, 6, 0);
            grpRt.Margin = new Padding(6, 0, 0, 0);
            topRow.Controls.Add(grpFg, 0, 0);
            topRow.Controls.Add(grpRt, 1, 0);

            var bottomRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, BackColor = Ui.DarkBg, Margin = new Padding(0, 0, 0, 6) };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            bottomRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grpPp.Margin = new Padding(0, 0, 6, 0);
            grpPos.Margin = new Padding(6, 0, 0, 0);
            bottomRow.Controls.Add(grpPp, 0, 0);
            bottomRow.Controls.Add(grpPos, 1, 0);

            var twoCols = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(TargetWidth - 28, 0),
                BackColor = Ui.DarkBg,
                Margin = new Padding(0, 2, 0, 6)
            };
            twoCols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            twoCols.Controls.Add(topRow, 0, 0);
            twoCols.Controls.Add(bottomRow, 0, 1);
            body.Controls.Add(twoCols);

            tips.SetToolTip(mirror, Lang.Get("Streamer.TipMirror"));
            tips.SetToolTip(fgRecommend, Lang.Get("Streamer.TipFgRecommend"));
            tips.SetToolTip(recenter, Lang.Get("Streamer.TipRecenter"));
            tips.SetToolTip(heightMode, Lang.Get("Streamer.TipHeightMode"));
            tips.SetToolTip(heightBox, Lang.Get("Streamer.TipHeightBox"));

            // =========================================================================
            // 4. DEBUG & ACTION BAR (Left: Debug checkbox | Right: Exporter, Ouvrir, Menu principal)
            // =========================================================================
            var dbgRow = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(TargetWidth - 28, 36),
                Margin = new Padding(0, 4, 0, 6),
                BackColor = Ui.DarkBg,
                ForeColor = Ui.DarkText
            };
            dbgRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            dbgRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));

            var exportBtn = new ModernButton { Text = Lang.Get("Streamer.BtnExportLogs"), AutoSize = true, Margin = new Padding(6, 2, 0, 2) };
            var openLogs = new ModernButton { Text = Lang.Get("Streamer.BtnOpenLogs"), AutoSize = true, Margin = new Padding(6, 2, 0, 2) };
            exportStatus.Visible = exportLink.Visible = false;
            exportLink.LinkClicked += (s, e) => LogExport.Reveal(exportLink.Tag as string);
            exportBtn.Click += (s, e) => LogExport.Run(this, currentExe.Length > 0 ? Games.Stem(currentExe) : null, exportStatus, exportLink);
            openLogs.Click += (s, e) => { Directory.CreateDirectory(Paths.Logs); Process.Start("explorer.exe", Paths.Logs); };
            debug.Margin = new Padding(4, 7, 14, 0);
            tips.SetToolTip(debug, Lang.Get("Streamer.TipDebug"));

            var mainMenu = new ModernButton { Text = Lang.Get("Streamer.BtnMainMenu"), AutoSize = true, Margin = new Padding(8, 2, 0, 2), AccentTint = Color.FromArgb(0xFF, 0x98, 0x00), ActiveState = true };
            mainMenu.Click += (s, e) =>
            {
                var exe = Path.Combine(Paths.Bin, "auraflashxr_launcher.exe");
                try { if (File.Exists(exe)) Process.Start(exe); else MessageBox.Show(this, Lang.Get("Streamer.LauncherMissing"), Lang.Get("App.Title")); }
                catch (Exception ex) { footer.Text = Lang.Get("Streamer.CannotOpenMainMenu") + ex.Message; footer.ForeColor = Color.Firebrick; }
            };
            tips.SetToolTip(mainMenu, Lang.Get("Streamer.TipMainMenu"));

            var dbgRightFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Dock = DockStyle.Fill,
                BackColor = Ui.DarkBg,
                Margin = new Padding(0)
            };
            dbgRightFlow.Controls.Add(mainMenu);
            dbgRightFlow.Controls.Add(openLogs);
            dbgRightFlow.Controls.Add(exportBtn);

            dbgRow.Controls.Add(debug, 0, 0);
            dbgRow.Controls.Add(dbgRightFlow, 1, 0);
            body.Controls.Add(dbgRow);

            // =========================================================================
            // 5. BOTTOM CONSOLE LOGS BOX (#07090E)
            // =========================================================================
            var exportRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), BackColor = Color.FromArgb(0x07, 0x09, 0x0E), ForeColor = Ui.DarkText };
            exportRow.Controls.Add(exportStatus); exportRow.Controls.Add(exportLink);
            footer.ForeColor = Ui.DarkMuted;

            var notices = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(10, 6, 10, 6),
                MinimumSize = new Size(TargetWidth - 28, 52),
                BackColor = Color.FromArgb(0x07, 0x09, 0x0E),
                ForeColor = Ui.DarkText
            };
            notices.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(0x1E, 0x26, 0x36), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, notices.Width - 1, notices.Height - 1);
            };
            notices.Controls.Add(exportRow); notices.Controls.Add(audioLine); notices.Controls.Add(footer);
            body.Controls.Add(notices);

            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.DarkBg };
            scroller.Controls.Add(body);
            Controls.Add(scroller);
            var hdr = Ui.CompactHeader(this, Lang.Get("Streamer.HeaderSubtitle"));
            Controls.Add(hdr);
            Lang.TranslateControl(body);
            Ui.AutoFitWindow(this, hdr, body, true, WithWorstCase);
            FormBorderStyle = FormBorderStyle.None;


            debug.Checked = settings.Debug;
            debug.CheckedChanged += (s, e) =>
            {
                host.Send("debug " + (debug.Checked ? "on" : "off"));
                settings.Debug = debug.Checked;
                settings.Save();
            };
            heightBox.Value = (decimal)Math.Max(0.5, Math.Min(2.2, settings.Height > 0 ? settings.Height : AppSettings.DefaultHeight));
            UpdateHeightUi();
            heightMode.ValueChanged += (s, e) =>
            {
                if (heightUiFromCode) return;
                settings.HeightMode = heightMode.Value == 0 ? "auto" : "manual"; settings.Save();
                Log.Write("INFO", "eye height " + settings.HeightMode);
                UpdateHeightUi(); SendDisplay();
            };
            heightBox.ValueChanged += (s, e) => { settings.Height = (double)heightBox.Value; settings.Save(); if (!HeightAuto) SendDisplay(); };
            mirrorTimer.Tick += (s, e) => MirrorStart();
            frameGen.Checked = settings.FrameGen;
            mirror.Checked = settings.MirrorDesktop;
            mirror.CheckedChanged += (s, e) =>
            {
                settings.MirrorDesktop = mirror.Checked;
                MirrorSync();
                settings.Save();
                Log.Write("INFO", "desktop mirror " + (mirror.Checked ? "on" : "off"));
            };
            Action fgEnable = () => { fgRate.Enabled = frameGen.Checked && !fgUnc.Checked; fgRecommend.Enabled = frameGen.Checked && !fgUnc.Checked; fgUnc.Enabled = frameGen.Checked; };
            fgEnableAll = fgEnable;
            frameGen.CheckedChanged += (s, e) =>
            {
                host.Send("frame_gen " + (frameGen.Checked ? "on" : "off"));   // the running streamer switches at the next boundary; the setting is read at start too
                settings.FrameGen = frameGen.Checked;
                settings.Save();
                fgEnable();
                SendLiveControl();
                Log.Write("INFO", "frame generation " + (frameGen.Checked ? "on" : "off"));
            };
            // "uncapped" is the tick; the slider keeps its own position (1/2 when the config holds uncapped) for when the tick goes off
            var savedRate = Array.IndexOf(FgRates, settings.FrameGenRate);
            fgRate.Value = savedRate >= 0 ? savedRate : 1;
            fgRateText.Text = FgRateTextAt(fgRate.Value);
            fgUnc.Checked = settings.FrameGenRate == "uncapped";
            fgRecommend.Checked = settings.FrameGenRecommended;
            fgEnable();
            fgRate.ValueChanged += (s, e) =>
            {
                var rate = FgRates[fgRate.Value];
                fgRateText.Text = FgRateTextAt(fgRate.Value);
                if (fgRateFromHost || fgUnc.Checked) return;   // uncapped: the slider only waits for the tick to go off
                if (fgRecommend.Checked)
                {
                    fgRecommend.Checked = false;
                }
                if (rate == settings.FrameGenRate) { SendLiveControl(); return; }
                host.Send("frame_gen_rate " + rate);
                settings.FrameGenRate = rate;
                settings.Save();
                SendLiveControl();
                Log.Write("INFO", "frame generation rate " + rate);
            };
            fgUnc.CheckedChanged += (s, e) =>
            {
                fgEnable();
                if (fgUncNote.Visible != fgUnc.Checked) { fgUncNote.Visible = fgUnc.Checked; if (IsHandleCreated) BeginInvoke((Action)(() => Ui.Refit(this))); }
            };
            fgUncNote.Visible = fgUnc.Checked;
            ConfigWatchStart();
            PresetWatchStart();
            RefreshPresetBadge();
            fgRecommend.CheckedChanged += (s, e) =>
            {
                settings.FrameGenRecommended = fgRecommend.Checked;
                settings.Save();
                Log.Write("INFO", "recommended rate " + (fgRecommend.Checked ? "on" : "off"));
                ApplyRecommendation(fgRecommend.Checked);
                SendLiveControl();
            };
            recenter.Click += (s, e) => { liveRecenterId++; SendLiveControl(); host.Send("recenter"); Log.Write("INFO", "recenter"); };


            // a blocking message (firewall, a setup or encode error in the footer) is shown in full: the window grows for it, once
            fwWarn.VisibleChanged += (s, e) => { if (IsHandleCreated) BeginInvoke((Action)(() => Ui.Refit(this))); };
            footer.TextChanged += (s, e) => { if (IsHandleCreated) BeginInvoke((Action)(() => Ui.Refit(this))); };
            host.OnSend += l => Sent.Add(l);
            host.OnEvent += (name, data) => BeginInvoke((Action)(() => HostEvent(name, data)));
            host.OnExit += code => BeginInvoke((Action)(() => HostExited(code)));
            Shown += (s, e) =>
            {
                FormBorderStyle = FormBorderStyle.None;
                StartHost();
                CheckFirewall();
                if (autoFind) { var t = new Timer { Interval = 3000 }; t.Tick += (a, b) => { t.Stop(); FindHeadset(true); }; t.Start(); }
            };
            Ui.ScreenshotWhenShown(this, 12000);
            FormClosing += (s, e) => { closing = true; MirrorSync(); footer.Text = Lang.Get("Streamer.Stopping"); Refresh(); host.Stop(); audio.Stop(); try { if (adbProcess != null && !adbProcess.HasExited) adbProcess.Kill(); } catch { } { var m = PowerPlan.Restore(); if (m != null) Log.Write("INFO", m); } Log.Write("INFO", "streamer.exe closed"); };
        }

        readonly Timer configWatch = new Timer { Interval = 1500 };

        static string DetectActiveSteamVrGame()
        {
            try
            {
                string steamDir = OpenXr.FindSteamVrDirectory();
                string logsDir = !string.IsNullOrEmpty(steamDir) ? Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(steamDir)), "logs") : null;
                if (string.IsNullOrEmpty(logsDir) || !Directory.Exists(logsDir))
                {
                    string defaultLogs = @"C:\Program Files (x86)\Steam\logs";
                    if (Directory.Exists(defaultLogs)) logsDir = defaultLogs;
                }

                if (!string.IsNullOrEmpty(logsDir) && Directory.Exists(logsDir))
                {
                    string vrserverLog = Path.Combine(logsDir, "vrserver.txt");
                    if (File.Exists(vrserverLog))
                    {
                        using (var fs = new FileStream(vrserverLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8))
                        {
                            var recentLines = new List<string>();
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (line.Contains("New Connect message from") || line.Contains("StartSceneApplicationTransitionFromProcess"))
                                    recentLines.Add(line);
                            }
                            for (int i = recentLines.Count - 1; i >= 0; i--)
                            {
                                string l = recentLines[i];
                                if (l.Contains("vrstartup") || l.Contains("vrmonitor") || l.Contains("vrwebhelper") ||
                                    l.Contains("vrpathreg") || l.Contains("vrserver") || l.Contains("vrcompositor") ||
                                    l.Contains("vrdashboard") || l.Contains("Steam.exe") || l.Contains("steamwebhelper") ||
                                    l.Contains("mirrordesktop")) continue;

                                int idx = l.IndexOf("New Connect message from ");
                                if (idx >= 0)
                                {
                                    string rest = l.Substring(idx + "New Connect message from ".Length).Trim();
                                    int end = rest.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                                    if (end > 0)
                                    {
                                        string exePath = rest.Substring(0, end + 4);
                                        return Path.GetFileName(exePath);
                                    }
                                }
                                int idx2 = l.IndexOf("StartSceneApplicationTransitionFromProcess(");
                                if (idx2 >= 0)
                                {
                                    string[] parts = l.Split(',');
                                    if (parts.Length >= 2)
                                    {
                                        string p = parts[1].Trim();
                                        if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(p);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var procs = Process.GetProcesses();
                foreach (var p in procs)
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();
                        if (name.Contains("vrserver") || name.Contains("vrcompositor") || name.Contains("vrmonitor") ||
                            name.Contains("vrdashboard") || name.Contains("vrwebhelper") || name.Contains("steam") ||
                            name.Contains("explorer") || name.Contains("auraflashxr") || name.Contains("streamer") ||
                            name.Contains("mirrordesktop") || name.Contains("svchost")) continue;

                        if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                        {
                            return p.ProcessName + ".exe";
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        enum Link { Red, Orange, Green }
        Link link = Link.Red;
        DateTime lastSeen = DateTime.MinValue;
        readonly Timer linkTimer = new Timer { Interval = 1000 };
        bool steamVrYield;
        bool manualStop;
        static bool RuntimeConnecting()
        {
            try { if (Json.Get(Session.Load(), "client_connections") is Dictionary<string, object> cc) foreach (var v in cc.Values) if (v is Dictionary<string, object> c && c.TryGetValue("connection_state", out var st) && st as string == "Connecting") return true; } catch { }
            return false;
        }
        static bool RuntimeStreaming(out string hsName, out string hsIp)
        {
            hsName = ""; hsIp = "";
            try
            {
                if (Json.Get(Session.Load(), "client_connections") is Dictionary<string, object> cc)
                    foreach (var kv in cc)
                        if (kv.Value is Dictionary<string, object> c && c.TryGetValue("connection_state", out var st) && st as string == "Streaming")
                        {
                            hsName = c.TryGetValue("display_name", out var dn) && dn as string != null ? dn as string : kv.Key;
                            hsIp = c.TryGetValue("last_local_ip", out var ip) && ip as string != null ? ip as string : "";
                            return true;
                        }
            }
            catch { }
            return false;
        }
        void SetLink(Link l, string text)
        {
            link = l;
            headset.Text = text;
            headset.ForeColor = l == Link.Green ? Color.SeaGreen : l == Link.Orange ? Color.DarkGoldenrod : Color.Firebrick;
        }
        void LinkTick()
        {
            if (closing || finding) return;
            bool vrRunning = Process.GetProcessesByName("vrserver").Length > 0;
            if (vrRunning)
            {
                bool streamingNow = RuntimeStreaming(out _, out _);
                if (host.Running && !streamingNow)
                {
                    steamVrYield = true;
                    host.Stop(1500);
                    Log.Write("INFO", "SteamVR (vrserver.exe) detected: paused auraflashxr_host.exe so driver_auraflashxr.dll owns UDP 9943/9944");
                }
                else if (host.Running)
                {
                    Log.Write("INFO", "SteamVR (vrserver.exe) detected: headset streaming through auraflashxr_host.exe, host kept running");
                }
                else steamVrYield = true;

                var hs = Session.Headset(Session.Load());
                if (hs != null) { pairedName = hs.Item2 ?? pairedName; pairedIp = hs.Item3 ?? pairedIp; }
                string statusPath = Path.Combine(Preset.IsActiveDefault ? Paths.Logs : Path.Combine(Preset.ActiveDir, "logs"), "steamvr_status.json");
                if (!File.Exists(statusPath)) statusPath = Path.Combine(Paths.Logs, "steamvr_status.json");
                Dictionary<string, object> st = null;
                bool freshStats = false;
                try
                {
                    if (File.Exists(statusPath) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(statusPath)).TotalSeconds < 5)
                    {
                        st = Json.Load(statusPath);
                        freshStats = st != null && st.Count > 0;
                    }
                }
                catch { }

                bool streaming = RuntimeStreaming(out string sName, out string sIp) || freshStats;
                string dispName = sName.Length > 0 ? sName : (pairedName.Length > 0 ? pairedName : "Meta Quest");
                string dispIp = sIp.Length > 0 ? sIp : (pairedIp.Length > 0 ? pairedIp : "SteamVR");
                if (streaming)
                {
                    SetLink(Link.Green, string.Format(Lang.Get("Streamer.HeadsetConnectedSteamVr"), dispName, dispIp));
                    NeedFind(false);
                    string vrGame = DetectActiveSteamVrGame();
                    if (!string.IsNullOrEmpty(vrGame))
                    {
                        currentExe = vrGame;
                        game.Text = string.Format(Lang.Get("Streamer.GameSteamVrRunning"), vrGame);
                    }
                    else
                    {
                        game.Text = Lang.Get("Streamer.GameSteamVrWaiting");
                    }
                    encInfo.Text = Lang.Get("Streamer.EncoderSteamVrDriver");
                    statsBox.Text = Lang.Get("Streamer.BoxLiveStreamingSteamVr");
                    if (freshStats)
                    {
                        double sFps = D(st, "server_fps"), cFps = D(st, "client_fps"), mbps = D(st, "mbps"), totLat = D(st, "total_latency_ms");
                        double encMs = D(st, "encode_ms"), netMs = D(st, "network_ms"), decMs = D(st, "decode_ms");
                        double gameMs = D(st, "game_ms"), compMs = D(st, "server_compositor_ms"), decQ = D(st, "decoder_queue_ms"), vsyncQ = D(st, "vsync_queue_ms");
                        double lost = D(st, "packets_lost"), bat = D(st, "battery_hmd");
                        tGame.Set(F(sFps, "0.0") + " fps", bat > 0 ? string.Format(Lang.Get("Streamer.TileHeadsetBattery"), bat) : "SteamVR");
                        tStreamed.Set(F(sFps, "0.0") + " fps", "SteamVR OpenVR");
                        tShown.Set(F(cFps, "0.0") + " fps", "");
                        tBitrate.Set(F(mbps, "0") + " Mbps", "");
                        tLatency.Set(F(totLat, "0") + " ms", string.Format(Lang.Get("Streamer.LatencyBreakdownNet"), F(encMs, "0.0"), F(netMs, "0.0"), F(decMs, "0.0")));
                        tLoss.Set(F(lost, "0"), Lang.Get("Streamer.TileLossPackets"), lost > 0 ? Color.Firebrick : (Color?)Color.FromArgb(0x5E, 0xB2, 0x71));
                        stats.Text = string.Format(Lang.Get("Streamer.LatencyBreakdownFull"), F(gameMs, "0.0"), F(compMs, "0.0"), F(encMs, "0.0"), F(netMs, "0.0"), F(decMs, "0.0"), F(decQ, "0.0"), F(vsyncQ, "0.0"));
                    }
                    footer.Text = Lang.Get("Streamer.FooterSteamVrLive");
                }
                else
                {
                    SetLink(Link.Orange, string.Format(Lang.Get("Streamer.HeadsetSteamVrWaiting"), dispName, dispIp));
                    string vrGame = DetectActiveSteamVrGame();
                    if (!string.IsNullOrEmpty(vrGame))
                    {
                        currentExe = vrGame;
                        game.Text = string.Format(Lang.Get("Streamer.GameSteamVrRunning"), vrGame);
                    }
                    else
                    {
                        game.Text = Lang.Get("Streamer.GameSteamVrPending");
                    }
                    footer.Text = Lang.Get("Streamer.FooterSteamVrLaunched");
                }
                return;
            }
            else if (steamVrYield)
            {
                steamVrYield = false;
                restarts = 0;
                game.Text = Lang.Get("Streamer.GameNone");
                StartHost();
                return;
            }

            if (link == Link.Green) return;
            var name = pairedName.Length > 0 ? $"{pairedName} ({pairedIp})" : "headset";
            if (RuntimeConnecting()) { if (link != Link.Orange || !headset.Text.Contains("connecting")) SetLink(Link.Orange, string.Format(Lang.Get("Streamer.HeadsetSeenWaitingSimple"), name)); }
            else if (link == Link.Orange && (DateTime.UtcNow - lastSeen).TotalSeconds > 30) SetLink(Link.Red, string.Format(Lang.Get("Streamer.HeadsetNotFoundSearching"), pairedName, pairedIp));
            refreshRtStatus?.Invoke();
        }
        DateTime configSeen; string watchRate; double watchCap, watchLag;
        void ConfigWatchStart()
        {
            watchRate = settings.FrameGenRate; watchCap = settings.FrameGenCapFps; watchLag = settings.FrameGenLagMs;
            try { configSeen = File.GetLastWriteTimeUtc(Paths.Settings); } catch { }
            configWatch.Tick += (s, e) =>
            {
                DateTime w; try { w = File.GetLastWriteTimeUtc(Paths.Settings); } catch { return; }
                if (w == configSeen) return;
                configSeen = w;
                settings.Root = Json.Load(Paths.Settings);
                if (settings.FrameGenRate != watchRate)
                {
                    watchRate = settings.FrameGenRate;
                    host.Send("frame_gen_rate " + watchRate);
                    fgRateFromHost = true;
                    fgUnc.Checked = watchRate == "uncapped";
                    var pos = Array.IndexOf(FgRates, watchRate);
                    if (pos >= 0 && fgRate.Value != pos) fgRate.Value = pos;
                    fgRateFromHost = false;
                    Log.Write("INFO", "frame generation rate " + watchRate + " (Advanced settings)");
                }
                if (settings.FrameGenCapFps != watchCap) { watchCap = settings.FrameGenCapFps; host.Send("frame_gen_cap " + watchCap.ToString("0.#", CultureInfo.InvariantCulture)); Log.Write("INFO", "frame generation cap " + watchCap + " fps (Advanced settings)"); }
                if (settings.FrameGenLagMs != watchLag) { watchLag = settings.FrameGenLagMs; host.Send("frame_gen_lag " + watchLag.ToString("0.#", CultureInfo.InvariantCulture)); Log.Write("INFO", "frame generation lag " + watchLag + " ms (Advanced settings)"); }
            };
            configWatch.Start();
        }

        string presetSeen = null;
        void PresetWatchStart()
        {
            presetSeen = Preset.Active;
            configWatch.Tick += (s, e) =>
            {
                try
                {
                    var active = Preset.Active;
                    if (active == presetSeen) return;
                    presetSeen = active;
                    settings.Reload();
                    RefreshPresetBadge();
                    Log.Write("INFO", "preset changed to '" + (active.Length == 0 ? Preset.DefaultName : active) + "' (seen by the streamer)");
                    if (!Process.GetProcessesByName("auraflashxr_host").Any()) return;
                    foreach (var hp in Process.GetProcessesByName("auraflashxr_host")) HostProcess.StopGracefully(hp);
                    if (Process.GetProcessesByName("vrserver").Length == 0)
                    {
                        var hostExe = Path.Combine(Paths.Bin, "auraflashxr_host.exe");
                        if (!File.Exists(hostExe)) hostExe = Paths.Host;
                        if (File.Exists(hostExe))
                        {
                            if (!Preset.IsActiveDefault) Preset.SyncPairing(Preset.Active);
                            var psi = new ProcessStartInfo(hostExe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Paths.Dir, Arguments = $"--install-dir \"{Preset.HostInstallDir}\" --shim --daemon --gui" };
                            Process.Start(psi);
                            Log.Write("INFO", "auraflashxr_host.exe restarted with the new preset");
                        }
                    }
                    else Log.Write("INFO", "host not restarted after preset switch: SteamVR (vrserver.exe) is running");
                }
                catch { }
            };
        }

        readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400 };

        static bool IsMirrorApp(string exe) => Games.IsHidden(exe);
        bool mirrorInHost;
        Process mirrorProc; bool headsetConnected; int mirrorFails; DateTime mirrorStarted;
        readonly Timer mirrorTimer = new Timer { Interval = 1500 };
        void MirrorSync()
        {
            mirrorTimer.Stop();
            bool want = mirror.Checked && headsetConnected && !gameRunning && !closing && !finding && mirrorFails < 3;
            bool alive = mirrorProc != null && !mirrorProc.HasExited;
            if (!want && alive) { try { mirrorProc.Kill(); } catch { } mirrorProc = null; Log.Write("INFO", "desktop mirror stopped"); }
            else if (want && !alive) { mirrorTimer.Start(); }
        }
        void MirrorStart()
        {
            mirrorTimer.Stop();
            if (!(mirror.Checked && headsetConnected && !gameRunning && !closing && !finding) || (mirrorProc != null && !mirrorProc.HasExited)) return;
            if (!File.Exists(Paths.MirrorDesktop)) { footer.Text = Lang.Get("Streamer.MirrorMissing"); footer.ForeColor = Color.Firebrick; return; }
            try
            {
                var p = Process.Start(new ProcessStartInfo(Paths.MirrorDesktop) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Paths.Bin });
                p.EnableRaisingEvents = true;
                mirrorProc = p; mirrorStarted = DateTime.Now;
                p.Exited += (s, e) => BeginInvoke((Action)(() =>
                {
                    if (mirrorProc != p) return;
                    mirrorProc = null;
                    int code = 0; try { code = p.ExitCode; } catch { }
                    bool quick = (DateTime.Now - mirrorStarted).TotalSeconds < 8;
                    Log.Write(code == 0 || !quick ? "INFO" : "WARN", $"desktop mirror ended (code {code})");
                    if (code == 2) { mirrorFails = 3; footer.Text = Lang.Get("Streamer.MirrorCaptureError"); footer.ForeColor = Color.Firebrick; }
                    else if (quick && code != 0) mirrorFails++;
                    else mirrorFails = 0;
                    MirrorSync();
                }));
                Log.Write("INFO", "desktop mirror started");
            }
            catch (Exception e) { Log.Write("ERROR", "desktop mirror: " + e.Message); mirrorFails = 3; }
        }

        void NeedFind(bool on) { findHint.Visible = find.Visible = on; }

        void CheckFirewall()
        {
            if (Ui.ScreenshotPath() != null) return;
            System.Threading.Tasks.Task.Run(() => Firewall.Check(Paths.Host)).ContinueWith(t =>
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        var st = t.Result;
                        var bad = st == FirewallState.Blocked || st == FirewallState.Missing;
                        fwWarn.Visible = fwFix.Visible = bad;
                        fwWarn.Text = st == FirewallState.Blocked ? Lang.Get("Streamer.FwBlocked")
                                    : Lang.Get("Streamer.FwMissing");
                        Log.Write(bad ? "WARN" : "INFO", "firewall: " + st);
                    }));
                }
                catch { }
            });
        }

        void AddKnob(TableLayoutPanel t, int row, string name, int min, int max, double def, Func<double> get, Action<double> set, string tip = null)
        {
            var valLbl = new Label { Text = "0.00", AutoSize = false, Width = 42, Font = Ui.SemiBold, ForeColor = Ui.AccentLight, Margin = new Padding(4, 5, 4, 3), TextAlign = ContentAlignment.MiddleRight };
            var k = new Knob { Name = name, Def = def, Get = get, Set = set, Value = valLbl,
                               Bar = new ModernSlider { Minimum = min, Maximum = max, SmallChange = 5, LargeChange = 10, Width = 120, Height = 24, AccessibleName = name, Margin = new Padding(2, 2, 2, 2) } };
            k.Bar.Value = (int)Math.Max(min, Math.Min(max, Math.Round(get() * 100)));
            k.Value.Text = (k.Bar.Value / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            k.Bar.ValueChanged += (s, e) =>
            {
                var v = k.Bar.Value / 100.0;
                k.Value.Text = v.ToString("0.00", CultureInfo.InvariantCulture);
                k.Set(v);
                settings.Save();
                SendDisplay();
            };
            var label = new Label { Text = name, AutoSize = true, Font = Ui.SemiBold, ForeColor = Ui.DarkText, Margin = new Padding(3, 5, 4, 3) };
            t.Controls.Add(label, 0, row);
            t.Controls.Add(k.Bar, 1, row);
            t.Controls.Add(k.Value, 2, row);
            if (tip != null) { tips.SetToolTip(label, tip); tips.SetToolTip(k.Bar, tip); }
            knobs.Add(k);
        }

        bool heightFromHeadset, heightUiFromCode;
        double reportedHeight;
        Action fgEnableAll;

        static readonly string[] Levels = { "third", "half", "two_thirds" };
        static readonly double[] LevelCap = { 1.0 / 3, 0.5, 2.0 / 3 };
        (string level, string why)? Recommend(string exe, string hs)
        {
            try
            {
                var gd = Games.Load(exe);
                if (!(gd.TryGetValue("levels", out var lv) && lv is Dictionary<string, object> byHeadset && byHeadset.TryGetValue(hs, out var h) && h is Dictionary<string, object> runs)) return null;
                int best = -1; bool bestClean = false; double bestScore = 9.0; Dictionary<string, object> bestRun = null;
                for (int i = 0; i < Levels.Length; i++)
                {
                    if (!(runs.TryGetValue(Levels[i], out var ro) && ro is Dictionary<string, object> run) || D(run, "seconds") < 30) continue;
                    double headsetRep = D(run, "headset_repeated_typ"), hostRep = D(run, "host_repeats_typ");
                    if (double.IsNaN(hostRep)) hostRep = 0;
                    bool clean = headsetRep < 0.05 && hostRep < 0.05;
                    if (clean || (!bestClean && (best < 0 || headsetRep + hostRep < bestScore))) { best = i; bestClean = clean; bestScore = headsetRep + hostRep; bestRun = run; }
                }
                if (best < 0) return null;
                string[] names = { "1/3", "1/2", "2/3" };
                var fps = D(bestRun, "rendered_fps"); var hz = D(bestRun, "display_hz");
                var low = D(bestRun, "rendered_fps_low");
                var text = string.Format(Lang.Get("Streamer.RecommendDetails"), names[best], F(fps, "0"), low > 0 ? string.Format(Lang.Get("Streamer.RecommendSlowTenth"), F(low, "0")) : "", F(D(bestRun, "headset_repeated_typ") * 100, "0.0"), F(D(bestRun, "headset_repeated_p90") * 100, "0.0"), F(D(bestRun, "host_repeats_typ") * 100, "0.0"));
                if (bestClean && best < 2 && hz > 0 && fps >= 0.95 * hz * LevelCap[best] && !runs.ContainsKey(Levels[best + 1]))
                    return (Levels[best + 1], string.Format(Lang.Get("Streamer.RecommendTryNext"), names[best + 1], names[best], F(fps, "0")));
                return (Levels[best], text);
            }
            catch { return null; }
        }

        void ApplyRecommendation(bool apply)
        {
            recommendationApplied = false;
            if (currentExe.Length == 0 || headsetName.Length == 0) { fgEnableAll?.Invoke(); return; }
            if (!frameGen.Checked)
            {
                fgRateText.Text = Lang.Get("Streamer.FgRateDisabled");
                fgEnableAll?.Invoke();
                return;
            }
            var rec = Recommend(currentExe, headsetName);
            if (rec == null)
            {
                fgRateText.Text = string.Format(Lang.Get("Streamer.NoSessionRecorded"), currentExe, headsetName);
            }
            else if (apply)
            {
                var pos = Array.IndexOf(FgRates, rec.Value.level);
                if (pos >= 0 && fgRate.Value != pos) fgRate.Value = pos;
                recommendationApplied = true;
                fgRateText.Text = string.Format(Lang.Get("Streamer.AutoForGame"), currentExe, rec.Value.why);
            }
            else
            {
                fgRateText.Text = string.Format(Lang.Get("Streamer.RecordedForGame"), currentExe, rec.Value.why);
            }
            fgEnableAll?.Invoke();
        }

        void HeightSource(string headsetName)
        {
            if (string.IsNullOrEmpty(headsetName)) return;
            this.headsetName = headsetName;
            var fromHeadset = !headsetName.Contains("Apple");
            if (fromHeadset == heightFromHeadset) return;
            heightFromHeadset = fromHeadset;
            UpdateHeightUi();
            SendDisplay();
        }

        bool HeightAuto => heightFromHeadset || settings.HeightMode == "auto";
        void UpdateHeightUi()
        {
            heightUiFromCode = true;
            heightMode.Enabled = !heightFromHeadset;
            heightMode.Value = HeightAuto ? 0 : 1;
            heightBox.Enabled = !HeightAuto;
            heightReport.Visible = HeightAuto && reportedHeight > 0;
            heightReport.Text = reportedHeight > 0 ? string.Format(Lang.Get("Streamer.HeadsetReportsHeight"), reportedHeight.ToString("0.00", CultureInfo.InvariantCulture)) : "";
            heightUiFromCode = false;
        }

        int liveRecenterId;

        static void WriteLiveFile(string name, string content)
        {
            Directory.CreateDirectory(Paths.Logs);
            File.WriteAllText(Path.Combine(Paths.Logs, name), content);
            if (!Preset.IsActiveDefault)
            {
                string presetLogs = Path.Combine(Preset.ActiveDir, "logs");
                Directory.CreateDirectory(presetLogs);
                File.WriteAllText(Path.Combine(presetLogs, name), content);
            }
        }

        void SendLiveControl()
        {
            try
            {
                int fgOn = settings.FrameGen ? 1 : 0;
                int throttle = settings.FrameGen ? (settings.FrameGenRate == "third" ? 3 : 2) : 1;
                WriteLiveFile("live_control.cfg", $"{fgOn} {throttle} {liveRecenterId}");
            }
            catch { }
        }

        void SendDisplay()
        {
            string f(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
            double effGamma = 1.0, effBr = 0.0, effCo = 0.0, effSa = 0.0, effSh = 0.0;
            if (settings.PostProcessingEnabled)
            {
                double sgBr = 0.0, sgCo = 0.0, sgGamma = 0.0;
                if (settings.SunGlasses == 1) { sgBr = -0.06; sgCo = 0.04; sgGamma = -0.05; }
                else if (settings.SunGlasses == 2) { sgBr = -0.14; sgCo = 0.08; sgGamma = -0.12; }
                else if (settings.SunGlasses == 3) { sgBr = -0.22; sgCo = 0.16; sgGamma = -0.20; }

                effGamma = Math.Max(0.50, Math.Min(2.50, settings.Gamma + sgGamma + settings.Exposure * 0.25 + (settings.Shadows - settings.Highlights) * 0.15));
                effBr = Math.Max(-0.50, Math.Min(0.50, settings.Brightness + sgBr + settings.Exposure * 0.35 + (settings.Shadows + settings.Highlights) * 0.25));
                effCo = Math.Max(-0.50, Math.Min(0.50, settings.Contrast + sgCo + (settings.Highlights - settings.Shadows) * 0.35));
                effSa = Math.Max(-0.80, Math.Min(0.80, settings.Saturation + settings.Vibrance * 0.75));
                effSh = Math.Max(0.00, Math.Min(1.00, settings.Sharpening));
            }
            try
            {
                WriteLiveFile("live_postproc.cfg", $"{f(effGamma)} {f(effSh)} {f(effBr)} {f(effCo)} {f(effSa)}");
            }
            catch { }
            host.Send("gamma " + f(effGamma));
            host.Send("height " + f(HeightAuto ? 0 : settings.Height));
            host.Send($"color {f(effBr)} {f(effCo)} {f(effSa)} {f(effSh)}");
        }

        void StartAudioRoute()
        {
            var s = Session.Load();
            var enabled = Json.Bool(s, "session_settings.audio.game_audio.enabled", true);
            var part = Json.Bool(s, "session_settings.audio.game_audio.content.device.set", false) ? Convert.ToString(Json.Get(s, "session_settings.audio.game_audio.content.device.content.NameSubstring")) : "";
            audio.Report += AudioNote;
            var cfg = new AppSettings();
            var micOn = Json.Bool(s, "session_settings.audio.microphone.enabled", false);
            var process = cfg.AudioCapture == "process" || micOn;
            if (micOn && cfg.AudioCapture != "process") Log.Write("INFO", "audio: the microphone is on, game audio is captured with process loopback (the cable is the microphone's)");
            if (micOn)
            {
                audio.StartMicrophone(true);
                if (AudioRoute.FindMicrophoneCable() == null && !Environment.GetCommandLineArgs().Contains("--test") && Ui.ScreenshotPath() == null) { }
            }
            if (process)
            {
                Environment.SetEnvironmentVariable("AFXR_AUDIO_CAPTURE", "process");
                audio.StartMuteOnly(enabled, cfg.MuteSpeakers);
            }
            else
            {
                Environment.SetEnvironmentVariable("AFXR_AUDIO_CAPTURE", null);
                audio.Start(part, enabled, cfg.MuteSpeakers);
                if (audio.CableMissing && !Environment.GetCommandLineArgs().Contains("--test") && Ui.ScreenshotPath() == null) { }
            }
            var plan = new AppSettings().PowerPlan;
            if (plan.Length > 0) System.Threading.Tasks.Task.Run(() => { var m = PowerPlan.Apply(plan); if (m != null) Log.Write("INFO", m); });
        }

        void AudioNote(string level, string text)
        {
            try { BeginInvoke((Action)(() => { if (!closing) SetAudioLine(Lang.Get("Streamer.AudioPrefix") + text, level == "WARN" ? Color.DarkOrange : Color.DimGray); })); } catch { }
        }

        bool LaunchSteamVr()
        {
            string configDir = Preset.IsActiveDefault ? Paths.Config : Path.Combine(Preset.ActiveDir, "config");
            try
            {
                var psi = new ProcessStartInfo("steam://rungameid/250820") { UseShellExecute = true };
                psi.EnvironmentVariables["AURAFLASHXR_CONFIG_DIR"] = configDir;
                Process.Start(psi);
                Log.Write("INFO", "SteamVR launch requested (steam://rungameid/250820), config " + configDir);
                return true;
            }
            catch { }
            string vrExec = OpenXr.FindSteamVrExecutable();
            if (!string.IsNullOrEmpty(vrExec) && File.Exists(vrExec))
            {
                try
                {
                    var psi = new ProcessStartInfo(vrExec) { UseShellExecute = false };
                    psi.EnvironmentVariables["AURAFLASHXR_CONFIG_DIR"] = configDir;
                    Process.Start(psi);
                    Log.Write("INFO", "SteamVR launched: " + vrExec + ", config " + configDir);
                    return true;
                }
                catch { }
            }
            return false;
        }

        void StartHost()
        {
            SendDisplay();
            SendLiveControl();
            headsetConnected = false; MirrorSync();
            var hs = Session.Headset(Session.Load());
            pairedName = hs?.Item2 ?? "";
            pairedIp = hs?.Item3 ?? "";
            if (!linkTimer.Enabled) { linkTimer.Tick += (s, e) => LinkTick(); linkTimer.Start(); }
            if (Process.GetProcessesByName("vrserver").Length > 0)
            {
                LinkTick();
                return;
            }
            if (OpenXr.IsSteamVrActive())
            {
                steamVrYield = true;
                SetLink(Link.Red, Lang.Get("Streamer.SteamVrStartFirst"));
                footer.Text = Lang.Get("Streamer.SteamVrStartFirst");
                Log.Write("INFO", "SteamVR runtime active: auraflashxr_host.exe not started, SteamVR must run first");
                LaunchSteamVr();
                return;
            }
            SetLink(Link.Red, hs == null ? Lang.Get("Streamer.HeadsetNonePaired") : string.Format(Lang.Get("Streamer.HeadsetNotFoundSearching"), pairedName, pairedIp));
            NeedFind(true);
            if (!Preset.IsActiveDefault) Preset.SyncPairing(Preset.Active);
            var args = $"--install-dir \"{Preset.HostInstallDir}\" --shim --daemon --gui" + (debug.Checked ? " --debug" : "");
            StartAudioRoute();
            var err = host.Start(args);
            if (err != null)
            {
                MessageBox.Show(this, err, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.Write("ERROR", err);
                return;
            }
            footer.Text = Lang.Get("Streamer.StartedWaitingHeadset");
            Log.Write("INFO", "streamer started: auraflashxr_host " + args);
        }

        void FindHeadset(bool autoPick)
        {
            if (finding) return;
            if (gameRunning && !autoPick && MessageBox.Show(this, Lang.Get("Streamer.GameRunningSearchRestart"), Lang.Get("App.Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            finding = true;
            find.Enabled = false;
            closing = true;
            SetLink(Link.Red, Lang.Get("Streamer.HeadsetSearching"));
            host.Stop();
            Log.Write("INFO", "find headset: streamer stopped, searching");
            try
            {
                using (var dlg = new FindHeadsetForm(Session.Headset(Session.Load()), autoPick))
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        var session = Session.Load();
                        Session.Pair(session, dlg.ChosenHost, dlg.ChosenName, dlg.ChosenIp);
                        Session.Save(session);
                        settings.HeadsetHost = dlg.ChosenHost; settings.HeadsetName = dlg.ChosenName; settings.HeadsetIp = dlg.ChosenIp;
                        settings.Save();
                        Log.Write("INFO", $"find headset: paired {dlg.ChosenName} ({dlg.ChosenHost}) at {dlg.ChosenIp}");
                    }
                    else Log.Write("INFO", "find headset: cancelled");
            }
            catch (Exception e) { Log.Write("ERROR", "find headset failed: " + e.Message); }
            closing = false;
            finding = false;
            find.Enabled = true;
            restarts = 0;
            StartHost();
        }

        void HostExited(int code)
        {
            if (closing || steamVrYield || manualStop) { manualStop = false; return; }
            Log.Write("ERROR", $"auraflashxr_host.exe stopped unexpectedly (code {code})");
            headset.Text = Lang.Get("Streamer.StoppedSeeLogs");
            headset.ForeColor = Color.Firebrick;
            if (code == 7)
            {
                headset.Text = Lang.Get("Streamer.ConfigError");
                footer.Text = setupError ?? Lang.Get("Streamer.SetupErrorGeneric");
                footer.ForeColor = Color.Firebrick;
                return;
            }
            if (restarts++ < 3)
            {
                footer.Text = string.Format(Lang.Get("Streamer.StoppedRestarting"), code, restarts);
                var t = new Timer { Interval = 2000 };
                t.Tick += (s, e) => { t.Stop(); t.Dispose(); StartHost(); };
                t.Start();
            }
            else footer.Text = string.Format(Lang.Get("Streamer.StoppedMultipleTimes"), code);
        }

        static string S(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        static double D(Dictionary<string, object> d, string k) { try { return d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : double.NaN; } catch { return double.NaN; } }
        static string F(double v, string fmt) => double.IsNaN(v) ? "  -" : v.ToString(fmt, CultureInfo.InvariantCulture);

        internal void HostEvent(string name, Dictionary<string, object> d)
        {
            switch (name)
            {
                case "setup_error":
                    setupError = S(d, "message");
                    break;
                case "client_connected":
                    HeightSource(S(d, "name"));
                    var n = S(d, "name");
                    var ip = S(d, "ip");
                    SetLink(Link.Green, string.Format(Lang.Get("Streamer.HeadsetConnected"), (n.Length > 0 ? n : pairedName), (ip.Length > 0 ? ip : pairedIp)));
                    NeedFind(false);
                    headsetConnected = true; MirrorSync();
                    if (ip.Length > 0 && ip != settings.HeadsetIp) { settings.HeadsetIp = ip; settings.Save(); }
                    break;
                case "client_disconnected":
                    headsetConnected = false; MirrorSync();
                    NeedFind(true);
                    lastSeen = DateTime.MinValue;
                    SetLink(Link.Red, string.Format(Lang.Get("Streamer.HeadsetDisconnectedSearching"), pairedName, pairedIp));
                    break;
                case "headset_seen":
                    if (link != Link.Green)
                    {
                        lastSeen = DateTime.UtcNow;
                        var seenName = S(d, "hostname"); var seenIp = S(d, "ip");
                        var paired = pairedName.Length > 0 && (seenName == settings.HeadsetHost || seenIp == pairedIp);
                        SetLink(Link.Orange, paired ? string.Format(Lang.Get("Streamer.HeadsetSeenWaiting"), pairedName, seenIp) : string.Format(Lang.Get("Streamer.HeadsetSeenUnpaired"), seenName, seenIp));
                        if (paired && seenIp.Length > 0 && seenIp != settings.HeadsetIp) { settings.HeadsetIp = seenIp; settings.Save(); pairedIp = seenIp; }
                    }
                    break;
                case "nvidia_profile":
                    encInfo.Text = Lang.Get("Streamer.EncoderActive") + S(d, "choice");
                    break;
                case "game_started":
                    if (IsMirrorApp(S(d, "exe")))
                    {
                        mirrorInHost = true;
                        if (frameGen.Checked) host.Send("frame_gen off");
                        break;
                    }
                    game.Text = Lang.Get("Streamer.GameLabel") + S(d, "exe");
                    gameRunning = true; MirrorSync();
                    currentExe = S(d, "exe");
                    lastSession = false;
                    statsBox.Text = Lang.Get("Streamer.BoxLiveStreaming");
                    ApplyRecommendation(fgRecommend.Checked);
                    break;
                case "game_ended":
                    if (IsMirrorApp(S(d, "exe")))
                    {
                        mirrorInHost = false;
                        if (settings.FrameGen) host.Send("frame_gen on");
                        break;
                    }
                    gameRunning = false; MirrorSync();
                    game.Text = string.Format(Lang.Get("Streamer.GameNoneLast"), S(d, "exe"), F(D(d, "seconds") / 60, "0.0"));
                    break;
                case "game_run":
                    if (IsMirrorApp(S(d, "exe"))) break;
                    {
                        lastSession = true;
                        var mins = D(d, "seconds") / 60;
                        statsBox.Text = string.Format(Lang.Get("Streamer.BoxLastSessionFormat"), S(d, "exe"), F(mins, "0.0"), S(d, "headset"));
                        string[] names = { "1/3", "1/2", "2/3" };
                        var lvl = S(d, "level"); var li = Array.IndexOf(FgRates, lvl);
                        var rec = Recommend(S(d, "exe"), S(d, "headset"));
                        var ri = rec == null ? -1 : Array.IndexOf(FgRates, rec.Value.level);
                        var net = d != null && d.TryGetValue("net", out var no) ? no as Dictionary<string, object> : null;
                        var rep = D(d, "headset_repeated_typ") * 100;
                        tGame.Set(F(D(d, "rendered_fps"), "0.0") + " fps", string.Format(Lang.Get("Streamer.AvgSlowTenth"), F(D(d, "rendered_fps_low"), "0.0")));
                        tStreamed.Set(F(D(d, "streamed_fps"), "0.0") + " fps", string.Format(Lang.Get("Streamer.StreamedMinAt"), F(D(d, "seconds") / 60, "0.0"), (li >= 0 ? names[li] : lvl)));
                        tShown.Set(F(D(net, "displayed_fps"), "0.0") + " fps", string.Format(Lang.Get("Streamer.RepeatedPeriods"), F(rep, "0.0")), rep >= 5 ? Color.Firebrick : (Color?)null);
                        tBitrate.Set(F(D(net, "bitrate_mbps"), "0") + " Mbps", Lang.Get("Streamer.Average"));
                        var latBad = Latency.Absurd(D(net, "latency_ms"), D(net, "latency_outliers"), D(net, "latency_samples")) || Latency.Absurd(D(net, "latency_p95_ms"));
                        tLatency.Set(latBad ? "n/a" : F(D(net, "latency_ms"), "0") + " ms", latBad ? Lang.Get("Streamer.TileLatencyUnmeasured") : string.Format(Lang.Get("Streamer.LatencyBreakdownNet"), F(D(net, "encode_ms"), "0.0"), F(D(net, "network_ms"), "0.0"), F(D(net, "decode_ms"), "0.0")), null, latBad ? Latency.Why : null);
                        tLoss.Set(F(D(net, "packets_lost"), "0"), Lang.Get("Streamer.TileLossPackets"), D(net, "packets_lost") > 0 ? Color.Firebrick : (Color?)Color.FromArgb(0x5E, 0xB2, 0x71));
                        stats.Text = string.Format(Lang.Get("Streamer.LastRunRepeatsSkipped"), F(D(d, "host_repeats_typ") * 100, "0.0"), F(D(d, "skipped") * 100, "0.0"), F(D(net, "latency_p95_ms"), "0.0")) + (rec == null ? "" : string.Format(Lang.Get("Streamer.NextTimeRecommend"), (ri >= 0 ? names[ri] : rec.Value.level)));
                    }
                    break;
                case "status":
                    var c = d != null && d.TryGetValue("client", out var cc) ? cc as Dictionary<string, object> : null;
                    if (d != null && d.TryGetValue("headset", out var ho) && ho is Dictionary<string, object> hd) HeightSource(S(hd, "name"));
                    if (d != null && S(d, "eye_res").Length > 0)
                    {
                        // resolution : oeil, image encodee, et taux de compression quand la foveation est active
                        var eyeRes = S(d, "eye_res");
                        var encRes = S(d, "enc_res");
                        var ratio = "";
                        try
                        {
                            var pe = eyeRes.Split('x'); var pc = encRes.Split('x');
                            if (pe.Length == 2 && pc.Length == 2)
                            {
                                double ew = double.Parse(pe[0], CultureInfo.InvariantCulture), eh = double.Parse(pe[1], CultureInfo.InvariantCulture);
                                double cw = double.Parse(pc[0], CultureInfo.InvariantCulture), ch = double.Parse(pc[1], CultureInfo.InvariantCulture);
                                if (ew > 0 && eh > 0 && cw > 0 && ch > 0)
                                    ratio = string.Format("   ·   {0} %", ((cw * ch) / (ew * eh) * 100.0).ToString("0", CultureInfo.InvariantCulture));
                            }
                        }
                        catch { }
                        var fov = S(d, "foveated") == "True";
                        resInfo.Text = string.Format(Lang.Get("Streamer.ResolutionLine"), eyeRes, encRes, fov ? Lang.Get("Streamer.ResolutionFoveated") : Lang.Get("Streamer.ResolutionFull"), ratio);
                    }
                    if (d != null && S(d, "encoder_desc").Length > 0)
                    {
                        // le multi-pass n'est pas une information utile au joueur : retire du texte
                        var desc = System.Text.RegularExpressions.Regex.Replace(S(d, "encoder_desc"), @"\s*,?\s*multi-pass [a-z-]+", "");
                        desc = desc.Replace(", ", "  |  ");   // separateurs | a la place des virgules
                        encInfo.Text = string.Format(Lang.Get("Streamer.EncoderActiveFormat"), desc, F(D(d, "fps_target"), "0"));
                    }
                    if (d != null && d.ContainsKey("fg_rate") && S(d, "fg_on") == "True")
                    {
                        var hostRate = S(d, "fg_rate");
                        var pos = Array.IndexOf(FgRates, hostRate);
                        fgRateFromHost = true;
                        if (fgUnc.Checked != (hostRate == "uncapped")) fgUnc.Checked = hostRate == "uncapped";
                        if (pos >= 0 && fgRate.Value != pos) fgRate.Value = pos;
                        fgRateFromHost = false;
                    }
                    if (gameRunning)
                    {
                        tGame.Set(F(D(d, "app_fps"), "0.0") + " fps", "");
                        tStreamed.Set(F(D(d, "stream_fps"), "0.0") + " fps", Lang.Get("Streamer.NotStreamed") + S(d, "frames_not_streamed"));
                        tShown.Set(F(double.IsNaN(D(c, "displayed_fps")) ? D(c, "client_fps") : D(c, "displayed_fps"), "0.0") + " fps", "");
                        tBitrate.Set(F(D(d, "mbps"), "0") + " Mbps", "");
                        var liveBad = Latency.Absurd(D(c, "total_latency_ms"), D(c, "latency_outliers"), D(c, "samples"));
                        tLatency.Set(liveBad ? "n/a" : F(D(c, "total_latency_ms"), "0") + " ms", liveBad ? Lang.Get("Streamer.TileLatencyUnmeasured") : string.Format(Lang.Get("Streamer.LatencyBreakdownNet"), F(D(c, "encode_ms"), "0.0"), F(D(c, "network_ms"), "0.0"), F(D(c, "decode_ms"), "0.0")), null, liveBad ? Latency.Why : null);
                        tLoss.Set(S(d, "packets_lost"), Lang.Get("Streamer.TileLossPackets"), D(d, "packets_lost") > 0 ? Color.Firebrick : (Color?)Color.FromArgb(0x5E, 0xB2, 0x71));
                        stats.Text = liveBad ? "" : string.Format(Lang.Get("Streamer.LatencyBreakdownFull"), F(D(c, "game_ms"), "0.0"), F(D(c, "server_compositor_ms"), "0.0"), F(D(c, "encode_ms"), "0.0"), F(D(c, "network_ms"), "0.0"), F(D(c, "decode_ms"), "0.0"), F(D(c, "decoder_queue_ms"), "0.0"), F(D(c, "vsync_queue_ms"), "0.0"));
                    }
                    break;
                case "audio_note":
                    SetAudioLine(Lang.Get("Streamer.AudioPrefix") + S(d, "text"), S(d, "level") == "WARN" ? Color.DarkOrange : Color.DimGray);
                    break;
                case "height_calibrated":
                    reportedHeight = D(d, "headset_y"); UpdateHeightUi();
                    break;
                case "encode_error":
                    footer.Text = Lang.Get("Streamer.EncodeErrorPrefix") + S(d, "message");
                    break;
            }
        }

        public readonly List<string> Sent = new List<string>();

        public void UiAction(string name, string value)
        {
            bool on = value == "on";
            void bar(int idx) { if (idx >= 0 && idx < knobs.Count) { var k = knobs[idx]; k.Bar.Value = (int)Math.Round(double.Parse(value, CultureInfo.InvariantCulture) * 100); } }
            switch (name)
            {
                case "frame_gen": frameGen.Checked = on; break;
                case "rate": fgRate.Value = int.Parse(value); break;
                case "auto": fgRecommend.Checked = on; break;
                case "uncapped": fgUnc.Checked = on; break;
                case "recenter": recenter.PerformClick(); break;
                case "height_mode": heightMode.Value = value == "auto" ? 0 : 1; break;
                case "height": heightBox.Value = decimal.Parse(value, CultureInfo.InvariantCulture); break;
                case "gamma": bar(0); break;
                case "sharpening": bar(1); break;
                case "contrast": bar(2); break; case "brightness": bar(3); break; case "exposure": bar(4); break;
                case "saturation": bar(5); break; case "vibrance": bar(6); break; case "highlights": bar(7); break; case "shadows": bar(8); break;
                case "debug": debug.Checked = on; break;
                case "mirror": mirror.Checked = on; break;
                case "advanced":
                    {
                        Button b = null; var todo = new Stack<Control>(); todo.Push(this);
                        while (todo.Count > 0 && b == null) { var c = todo.Pop(); if (c is Button bb && (bb.AccessibleName == Lang.Get("Streamer.BtnAdvancedPicture") || bb.AccessibleName == "Advanced")) b = bb; foreach (Control k in c.Controls) todo.Push(k); }
                        b?.PerformClick();
                        break;
                    }
            }
        }
        public decimal HeightMin => heightBox.Minimum;
        public decimal HeightMax => heightBox.Maximum;

        void SetAudioLine(string text, Color c) { audioLine.Text = text; audioLine.ForeColor = c; audioLine.Visible = text.Length > 0; }
        public void DemoAudio(bool bad) => SetAudioLine(bad ? Lang.Get("Streamer.DemoAudioBad") : Lang.Get("Streamer.DemoAudioGood"), bad ? Color.DarkOrange : Color.DimGray);

        void WithWorstCase(Action doFit)
        {
            var tiles = new[] { tGame, tStreamed, tShown, tBitrate, tLatency, tLoss };
            var labels = new Control[] { headset, game, runtime, encInfo, heightReport, stats, statsBox };
            var shown = new Control[] { find, heightReport };
            var hidden = new Control[] { audioLine, exportStatus, exportLink };
            var hidBack = hidden.Select(c => (c, c.Visible)).ToList();
            var textBack = labels.Select(c => (c, c.Text, c.ForeColor)).ToList();
            var visBack = shown.Select(c => (c, c.Visible)).ToList();
            var tileBack = tiles.Select(t => (t, t.Value, t.Note, t.ValueColor)).ToList();
            try
            {
                headset.Text = Lang.Get("Streamer.HeadsetConnectedSimple");
                game.Text = Lang.Get("Streamer.DemoGameLast");
                encInfo.Text = Lang.Get("Streamer.DemoEncoderActive");
                heightReport.Text = Lang.Get("Streamer.HeightDetected");
                stats.Text = Lang.Get("Streamer.DemoLatencyFull");
                statsBox.Text = Lang.Get("Streamer.DemoLastSession");
                tGame.Set("144.4 fps", Lang.Get("Streamer.AvgSlowTenthSample")); tStreamed.Set("144.4 fps", Lang.Get("Streamer.DemoMinAt")); tShown.Set("144.4 fps", Lang.Get("Streamer.DemoRepeatedPeriods"));
                tBitrate.Set("1000 Mbps", Lang.Get("Streamer.Average")); tLatency.Set("123 ms", Lang.Get("Streamer.DemoLatencyNet")); tLoss.Set("1234", Lang.Get("Streamer.TileLossPackets"));
                foreach (var c in shown) c.Visible = true;
                foreach (var c in hidden) c.Visible = false;
                doFit();
            }
            finally
            {
                foreach (var (c, t, col) in textBack) { c.Text = t; c.ForeColor = col; }
                foreach (var (c, v) in visBack) c.Visible = v;
                foreach (var (c, v) in hidBack) c.Visible = v;
                foreach (var (t, v, n, col) in tileBack) t.Set(v, n, col == Color.Black ? (Color?)null : col);
            }
        }

        public void DemoDebugOn() { debug.Checked = true; settings.Debug = false; settings.Save(); exportStatus.Text = Lang.Get("Streamer.DemoExportSaved"); exportStatus.ForeColor = Color.SeaGreen; exportStatus.Visible = exportLink.Visible = true; }

        public void ApplyDemoState(bool worstCase = true)
        {
            HostEvent("client_connected", new Dictionary<string, object> { { "name", "Apple Vision Pro" }, { "ip", "192.168.1.50" } });
            HostEvent("height_calibrated", new Dictionary<string, object> { { "headset_y", 1.62 } });
            headset.Text = worstCase ? "Headset: connected - Apple Vision Pro of a person with a long name (192.168.100.123)" : "Headset: connected - Apple Vision Pro (192.168.1.50)"; headset.ForeColor = Color.SeaGreen;
            runtime.Text = "GPU: NVIDIA GeForce RTX 5090 (Blackwell, driver 617.42)   \u00B7   OpenXR runtime: AuraFlashXR (this folder)";
            if (!worstCase)
            {
                NeedFind(false);
                game.Text = "Game: Cyberpunk2077.exe";
                tGame.Set("44.8 fps", "average, slowest tenth 38.2"); tStreamed.Set("89.7 fps", "25.0 min at 1/2"); tShown.Set("89.1 fps", "repeated periods 1.0 %");
                tBitrate.Set("120 Mbps", "average"); tLatency.Set("57 ms", "enc 6 + net 9 + dec 14"); tLoss.Set("0", "packets lost");
                stats.Text = ""; frameGen.SetCheckedInstant(true);
                return;
            }
            NeedFind(true);
            fwWarn.Text = Lang.Get("Streamer.FwBlocked"); fwWarn.Visible = fwFix.Visible = true;
            game.Text = "Game: Cyberpunk2077.exe (Cyberpunk 2077) after 123 min";
            tGame.Set("144.4 fps", "average, slowest tenth 120.0"); tStreamed.Set("144.4 fps", "123.4 min at 1/2"); tShown.Set("144.4 fps", "repeated periods 12.3 %", Color.Firebrick);
            tBitrate.Set("1000 Mbps", "average"); tLatency.Set("123 ms", "enc 12 + net 12 + dec 12"); tLoss.Set("1234", "packets lost", Color.Firebrick);
            stats.Text = Lang.Get("Streamer.DemoHostRepeats");
            fgRateText.Text = FgRateTextAt(2);
            footer.Text = Lang.Get("Streamer.SessionJsonMissing");
            footer.ForeColor = Color.Firebrick;
        }

        [STAThread]
        static void Main()
        {
            Lang.Init();
            Log.Component = "gui";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var testing = Ui.ScreenshotPath() != null || Environment.GetCommandLineArgs().Contains("--test");
            using (new System.Threading.Mutex(true, testing ? "AuraFlashXR_Runtime_Window_Test_" + Process.GetCurrentProcess().Id : "AuraFlashXR_Runtime_Window", out bool first))
            {
                if (!first && !testing) { MessageBox.Show(Lang.Get("Streamer.AlreadyRunning"), Lang.Get("App.Title")); return; }
                Log.Write("INFO", $"streamer.exe {Version.Text} start ({Paths.Dir})");
                if (!File.Exists(Paths.Host))
                {
                    MessageBox.Show(Lang.Get("Streamer.HostMissing"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var check = SystemCheck.Run();
                if (check == null || !(check.TryGetValue("ok", out var ok) && ok is bool b && b))
                {
                    var why = check != null && check.TryGetValue("reason", out var r) ? r as string : Lang.Get("Streamer.SystemCheckFailed");
                    Log.Write("ERROR", "refusing to start: " + why);
                    MessageBox.Show(Lang.Get("Streamer.CannotStart") + why, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (Session.Headset(Session.Load()) == null && !testing)
                {
                    if (MessageBox.Show(Lang.Get("Streamer.PromptPairHeadset"), Lang.Get("App.Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        Process.Start(Paths.Configure);
                        return;
                    }
                }
                Application.Run(new RuntimeForm(check, Environment.GetCommandLineArgs().Contains("--find-auto")));
            }
        }
    }
}