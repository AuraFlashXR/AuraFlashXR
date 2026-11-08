// performance.exe: the games that have run (left) and what was stored for the selected one (right): the last session as tiles, the recent
// sessions, the removal of a motion-vector DLL an earlier release installed, and an export of the logs. Data: config\games\<exe>.json written by the streamer.
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
    public class PerformanceForm : DpiForm
    {
        readonly AppSettings settings = new AppSettings();
        readonly ListBox games = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, Font = Ui.Body, BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
        readonly FlowLayoutPanel right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12, 8, 12, 8), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
        readonly List<string> stems = new List<string>();   // parallel to the list box rows
        readonly Dictionary<string, Dictionary<string, object>> data = new Dictionary<string, Dictionary<string, object>>();

        public PerformanceForm(string select)
        {
            Lang.Init();
            Text = Lang.Get("Perf.Title");
            Icon = Ui.AppIcon();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Body;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;
            Width = 1180; Height = 780;   // design values at 96 dpi
            MinimumSize = new Size(760, 520);
            Ui.FitToWorkArea(this);

            // troubleshooting and the one preference, folded away: most people never open them
            var debug = new CheckBox { Text = Lang.Get("Perf.DebugLogging"), AutoSize = true, Checked = settings.Debug, Margin = new Padding(3, 4, 3, 2), ForeColor = Ui.DarkText };
            debug.CheckedChanged += (s, e) => { settings.Debug = debug.Checked; settings.Save(); Log.Write("INFO", "debug logging " + (debug.Checked ? "on" : "off") + " (from the logs window)"); };
            var autoDel = new CheckBox { Text = Lang.Get("Perf.AutoDeleteUninstalled"), AutoSize = true, Checked = settings.AutoDeleteGameLogs, Margin = new Padding(3, 2, 3, 2), ForeColor = Ui.DarkText };
            autoDel.CheckedChanged += (s, e) => { settings.AutoDeleteGameLogs = autoDel.Checked; settings.Save(); if (autoDel.Checked) { Scan(); Fill(null); } };
            var open = new Button { Text = Lang.Get("Perf.BtnOpenLogsFolder"), AutoSize = true, Margin = new Padding(3, 4, 3, 4) };
            open.Click += (s, e) => { Directory.CreateDirectory(Paths.Logs); Process.Start("explorer.exe", Paths.Logs); };
            var trouble = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            trouble.Controls.Add(debug); trouble.Controls.Add(autoDel); trouble.Controls.Add(open);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12, 6, 12, 6), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            bottom.Controls.Add(Ui.Expander(Lang.Get("Perf.TroubleshootingTitle"), trouble));

            var left = new Panel { Dock = DockStyle.Left, Width = 300, Padding = new Padding(12, 14, 12, 10), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            var leftHdr = new Label { Text = Lang.Get("Perf.RecordedGamesHeader"), AutoSize = false, Height = 32, Dock = DockStyle.Top, Font = Ui.Big, ForeColor = Ui.AccentLight, Margin = new Padding(0, 0, 0, 8), TextAlign = ContentAlignment.MiddleLeft };
            left.Controls.Add(games);
            left.Controls.Add(leftHdr);
            games.BringToFront();
            games.ItemHeight = 28;
            games.DrawMode = DrawMode.OwnerDrawFixed;
            games.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= games.Items.Count) return;
                bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                var bg = sel ? Ui.Accent : Ui.DarkCard;
                var fg = sel ? Color.White : Ui.DarkText;
                using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
                var text = games.Items[e.Index].ToString();
                using (var fb = new SolidBrush(fg))
                {
                    var rect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 4, e.Bounds.Width - 12, e.Bounds.Height - 8);
                    e.Graphics.DrawString(text, sel ? Ui.Bold : Ui.Body, fb, rect);
                }
            };
            games.BackColor = left.BackColor;
            games.ForeColor = Ui.DarkText;
            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(bottom);
            games.SelectedIndexChanged += (s, e) => Render();
            if (settings.AutoDeleteGameLogs) Scan();
            Fill(select);
            Load += (s, e) => { ActiveControl = games; right.AutoScrollPosition = Point.Empty; };   // focus on the list of games: a control lower in the right pane would scroll it
            Ui.ScreenshotWhenShown(this, 1500);
        }

        static string S(Dictionary<string, object> d, string k, string def = "") => d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : def;
        static double D(Dictionary<string, object> d, string k) { try { return d != null && d.TryGetValue(k, out var v) && v != null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : double.NaN; } catch { return double.NaN; } }
        static Dictionary<string, object> O(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        static string N(double v, string fmt = "0.0") => double.IsNaN(v) ? "-" : v.ToString(fmt, CultureInfo.InvariantCulture);

        // a record whose game has recorded paths and none of them exists any more: the game was uninstalled
        void Scan()
        {
            foreach (var f in Games.Files().Where(f => !Games.IsHiddenFile(f)).ToList())
            {
                var g = Json.Load(f);
                if (g.TryGetValue("paths", out var p) && p is object[] arr && arr.Length > 0 && !arr.Any(o => File.Exists(o as string ?? "")))
                {
                    try { File.Delete(f); Log.Write("INFO", "logs: deleted the record of an uninstalled game: " + Path.GetFileName(f)); } catch { }
                }
            }
        }

        void Fill(string select)
        {
            games.Items.Clear(); stems.Clear(); data.Clear();
            var rows = new List<(string stem, string label, long when)>();
            foreach (var f in Games.Files().Where(f => !Games.IsHiddenFile(f)))
            {
                var g = Json.Load(f);
                var stem = Path.GetFileNameWithoutExtension(f);
                data[stem] = g;
                var name = S(g, "name"); if (name.Length == 0) name = S(g, "exe", stem);
                long when = 0;
                if (g.TryGetValue("sessions", out var so) && so is object[] ss && ss.Length > 0 && ss[ss.Length - 1] is Dictionary<string, object> last) when = (long)(D(last, "updated") is double u && !double.IsNaN(u) ? u : 0);
                rows.Add((stem, name, when));
            }
            foreach (var r in rows.OrderByDescending(r => r.when).ThenBy(r => r.label)) { stems.Add(r.stem); games.Items.Add(r.label); }
            if (stems.Count == 0) { Render(); return; }
            var i = select != null ? stems.IndexOf(Games.Stem(select)) : -1;
            games.SelectedIndex = i >= 0 ? i : 0;
        }

        void Render()
        {
            right.SuspendLayout();
            right.Controls.Clear();
            if (games.SelectedIndex < 0 || games.SelectedIndex >= stems.Count)
            {
                right.Controls.Add(Ui.Line(Lang.Get("Perf.NoGamesYet"), Ui.Body));
                right.ResumeLayout(); return;
            }
            var stem = stems[games.SelectedIndex];
            var g = data[stem];
            var exe = S(g, "exe", stem);
            right.Controls.Add(new Label { Text = games.SelectedItem.ToString(), AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), Margin = new Padding(3, 0, 3, 0), ForeColor = Ui.DarkText });
            var paths = g.TryGetValue("paths", out var po) && po is object[] pa ? pa.Select(x => x as string).Where(x => !string.IsNullOrEmpty(x)).ToArray() : new string[0];
            right.Controls.Add(new Label { Text = exe + (paths.Length > 0 ? "   " + paths[0] : ""), AutoSize = true, ForeColor = Ui.DarkMuted, MaximumSize = new Size(Math.Max(400, Ui.Design(right, right.ClientSize.Width - 40)), 0), Margin = new Padding(3, 0, 3, 8) });

            var sessions = g.TryGetValue("sessions", out var so) && so is object[] sa ? sa.OfType<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>>();
            if (sessions.Count == 0)
                right.Controls.Add(Ui.Line(Lang.Get("Perf.NoSessionsForGame"), Ui.Body));
            else
            {
                var last = sessions[sessions.Count - 1];
                right.Controls.Add(new Label { Text = string.Format(Lang.Get("Perf.LastSessionHeader"), When(last), N(D(last, "seconds") / 60), S(last, "headset"), Mode(S(last, "level"))), AutoSize = true, Font = Ui.Bold, Margin = new Padding(3, 0, 3, 4), ForeColor = Ui.DarkText });
                right.Controls.Add(Tiles(last));
                right.Controls.Add(new Label { Text = string.Format(Lang.Get("Perf.SessionsCountHeader"), sessions.Count), AutoSize = true, Font = Ui.Bold, Margin = new Padding(3, 8, 3, 4), ForeColor = Ui.DarkText });
                right.Controls.Add(SessionList(sessions));
            }
            if (MvDll.Installed(stem) != null) right.Controls.Add(DllBox(stem));
            var tools = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            var export = new Button { Text = Lang.Get("Perf.BtnExportLog"), AutoSize = true };
            var exported = new Label { AutoSize = true, Visible = false, Margin = new Padding(3, 4, 3, 0), MaximumSize = new Size(Math.Max(400, Ui.Design(right, right.ClientSize.Width - 40)), 0) };
            var reveal = new LinkLabel { Text = Lang.Get("Perf.LinkOpenFolder"), AutoSize = true, Visible = false, Margin = new Padding(3, 4, 3, 0) };
            reveal.LinkClicked += (s, e) => LogExport.Reveal(reveal.Tag as string);
            export.Click += (s, e) => LogExport.Run(this, stem, exported, reveal);
            var del = new Button { Text = Lang.Get("Perf.BtnForgetGame"), AutoSize = true };
            del.Click += (s, e) =>
            {
                if (MessageBox.Show(this, string.Format(Lang.Get("Perf.ConfirmForgetGame"), games.SelectedItem), Lang.Get("App.Title"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                try { File.Delete(Games.FileOf(exe)); } catch { }
                Fill(null);
            };
            tools.Controls.Add(export);
            tools.Controls.Add(del);
            right.Controls.Add(tools);
            var result = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            result.Controls.Add(exported); result.Controls.Add(reveal);
            right.Controls.Add(result);
            foreach (Control c in right.Controls) Ui.LateScale(c, this);   // rebuilt after the window loaded; widths above are design units
            right.ResumeLayout();
            right.AutoScrollPosition = Point.Empty;   // a rebuilt pane starts at its top, whatever the last one was scrolled to
        }

        static string Mode(string level) => level == "off" ? Lang.Get("Perf.ModeGenOff") : level == "third" ? Lang.Get("Perf.ModeGenThird") : level == "half" ? Lang.Get("Perf.ModeGenHalf") : level == "two_thirds" ? Lang.Get("Perf.ModeGenTwoThirds") : level;
        static string When(Dictionary<string, object> ses)
        {
            var t = D(ses, "start");
            return double.IsNaN(t) ? "?" : DateTimeOffset.FromUnixTimeSeconds((long)t).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        Control Tiles(Dictionary<string, object> s)
        {
            var net = O(s, "net");
            int maxW = Math.Max(400, Ui.Design(right, right.ClientSize.Width - 40));
            var main = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(maxW, 0), Margin = new Padding(0) };
            var more = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(maxW, 0), Margin = new Padding(0) };
            var rep = D(s, "headset_repeated_typ") * 100; var hostRep = D(s, "host_repeats_typ") * 100;
            // the four numbers that answer "how did it go"; the rest is one click away
            main.Controls.Add(Ui.MakeTile(Lang.Get("Streamer.TileGame"), N(D(s, "rendered_fps")) + " fps", Lang.Get("Perf.SlowestTenth") + N(D(s, "rendered_fps_low"))));
            if (net != null) main.Controls.Add(Ui.MakeTile(Lang.Get("Streamer.TileShown"), N(D(net, "displayed_fps")) + " fps", string.Format(Lang.Get("Perf.RepeatedPeriodsPct"), N(rep)), rep >= 5 ? Color.Firebrick : (Color?)null));
            if (net != null) {
                var bad = Latency.Absurd(D(net, "latency_ms"), D(net, "latency_outliers"), D(net, "latency_samples")) || Latency.Absurd(D(net, "latency_p95_ms"));
                var lt = Ui.MakeTile(Lang.Get("Streamer.TileLatency"), bad ? "n/d" : N(D(net, "latency_ms"), "0") + " ms", bad ? Lang.Get("Streamer.TileLatencyUnmeasured") : $"p95 {N(D(net, "latency_p95_ms"), "0")} ; enc {N(D(net, "encode_ms"), "0")} + {Lang.Get("Perf.NetAbbr")} {N(D(net, "network_ms"), "0")} + {Lang.Get("Perf.DecAbbr")} {N(D(net, "decode_ms"), "0")}");
                if (bad) lt.Set("n/d", Lang.Get("Streamer.TileLatencyUnmeasured"), null, Latency.Why);
                main.Controls.Add(lt);
            }
            more.Controls.Add(Ui.MakeTile(Lang.Get("Streamer.TileStreamed"), N(D(s, "streamed_fps")) + " fps", Lang.Get("Perf.DisplayHzLabel") + N(D(s, "display_hz"), "0") + " Hz"));
            more.Controls.Add(Ui.MakeTile(Lang.Get("Perf.TileHostRepeats"), N(hostRep) + " %", Lang.Get("Perf.OfOutputs")));
            if (net != null)
            {
                more.Controls.Add(Ui.MakeTile(Lang.Get("Streamer.TileBitrate"), N(D(net, "bitrate_mbps"), "0") + " Mbps", Lang.Get("Perf.SentThroughput") + N(D(net, "throughput_mbps"), "0")));
                var lost = D(net, "packets_lost");
                more.Controls.Add(Ui.MakeTile(Lang.Get("Perf.TileLostPackets"), N(lost, "0"), Lang.Get("Perf.DuringSession"), lost > 0 ? Color.Firebrick : (Color?)null));
            }
            var all = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            all.Controls.Add(main);
            if (net != null)
            {
                // AuraFlashXR's own stats line, to tell a slow encoder or decoder from a slow network: streamer latency = encoder + transport + decoder
                double enc = D(net, "encode_ms"), tr = D(net, "network_ms"), dec = D(net, "decode_ms");
                var st = Latency.Streamer(enc, tr, dec, D(net, "latency_outliers"), D(net, "latency_samples"));
                all.Controls.Add(new Label { AutoSize = true, Font = Ui.Body, Margin = new Padding(3, 0, 3, 4),
                    Text = st == null ? Lang.Get("Perf.StreamerLatencyNd") : string.Format(Lang.Get("Perf.StreamerLatencyFormula"), N(st.Value, "0"), N(enc, "0"), N(tr, "0"), N(dec, "0")) });
            } all.Controls.Add(Ui.Expander(Lang.Get("Perf.MoreMetricsExpander"), more));
            return all;
        }

        Control SessionList(List<Dictionary<string, object>> sessions)
        {
            var lv = new ListView { View = View.Details, FullRowSelect = true, GridLines = false, Height = 150, Width = Math.Max(400, Ui.Design(right, right.ClientSize.Width - 40)), HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(3), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
            string[] cols = { Lang.Get("Perf.ColDate"), Lang.Get("Perf.ColHeadset"), Lang.Get("Perf.ColGeneration"), Lang.Get("Perf.ColMinutes"), Lang.Get("Perf.ColGameFps"), Lang.Get("Perf.ColShownFps"), Lang.Get("Perf.ColRepeatedPct"), Lang.Get("Perf.ColLatencyMs"), Lang.Get("Perf.ColEncNetDecMs") };
            int[] w = { 125, 125, 55, 45, 55, 60, 75, 65, 90 };
            for (int i = 0; i < cols.Length; i++) Ui.Col(lv, cols[i], w[i]);
            right.Resize += (s, e) => lv.Width = Math.Max(400, right.ClientSize.Width - 40);
            foreach (var s in Enumerable.Reverse(sessions))
            {
                var net = O(s, "net");
                lv.Items.Add(new ListViewItem(new[] { When(s), S(s, "headset"), Mode(S(s, "level")).Replace("generation ", ""), N(D(s, "seconds") / 60), N(D(s, "rendered_fps")), net != null ? N(D(net, "displayed_fps")) : "-",
                    N(D(s, "headset_repeated_typ") * 100), net != null ? Latency.Text(D(net, "latency_ms"), D(net, "latency_outliers"), D(net, "latency_samples")) : "-",
                    net != null ? (Latency.Streamer(D(net, "encode_ms"), D(net, "network_ms"), D(net, "decode_ms"), D(net, "latency_outliers"), D(net, "latency_samples")) is double sl ? N(sl, "0") : "n/a") : "-" }));
            }
            Ui.FitColumns(lv);
            return lv;
        }

        // only for a game where an earlier release installed the motion-vector DLL: one line and the button that removes AuraFlashXR's copy
        Control DllBox(string stem)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(3, 10, 3, 0), MaximumSize = new Size(Math.Max(400, Ui.Design(right, right.ClientSize.Width - 40)), 0), BackColor = Ui.DarkCard, ForeColor = Ui.DarkText, Padding = new Padding(6) };
            row.Controls.Add(new Label { Text = Lang.Get("Perf.DlssDllHeader"), AutoSize = true, Font = Ui.Bold, Margin = new Padding(0, 6, 4, 0), ForeColor = Ui.DarkText });
            row.Controls.Add(new Label { Text = MvDll.Installed(stem) + " " + Lang.Get("Perf.DllInstalledEarlier"), AutoSize = true, Margin = new Padding(0, 6, 12, 0), ForeColor = Ui.DarkMuted });
            var b = new Button { Text = Lang.Get("Perf.BtnRemoveDll"), AutoSize = true, Margin = new Padding(0) };
            b.Click += (s, e) => { MessageBox.Show(this, MvDll.Uninstall(stem), Lang.Get("App.Title")); Render(); };
            row.Controls.Add(b);
            return row;
        }

        [STAThread]
        static void Main()
        {
            Lang.Init();
            Log.Component = "performance";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var a = Environment.GetCommandLineArgs();
            Application.Run(new PerformanceForm(a.Length > 2 && a[1] == "--game" ? a[2] : null));
        }
    }
}
