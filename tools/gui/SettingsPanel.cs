// The settings AuraFlashXR actually uses, as one panel: the wizard's last step shows it prefilled with the proposal, and the user may change anything.
// Everything else (AuraFlashXR's advanced options) stays in config\session.json.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace AuraFlashXR
{
    public class SettingsPanel : UserControl
    {
        readonly AppSettings settings;
        Dictionary<string, object> session;
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        readonly Dictionary<Control, Label> reasons = new Dictionary<Control, Label>();

        public readonly NumericUpDown EyeW = Num(256, Nvenc.MaxEyeWidth, 32), EyeH = Num(256, Nvenc.MaxEyeHeight, 32), Bitrate = Num(10, 1000, 10);
        public readonly ComboBox Fps = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        public readonly RadioButton ProfFov = new RadioButton { Text = Lang.Get("SettingsPanel.ProfileFoveated"), AutoSize = true };
        public readonly RadioButton ProfFull = new RadioButton { Text = Lang.Get("SettingsPanel.ProfileFullSplit"), AutoSize = true };
        public readonly CheckBox Split = new CheckBox { Text = Lang.Get("SettingsPanel.SplitEncode"), AutoSize = true };
        public readonly CheckBox FrameGen = new CheckBox { Text = Lang.Get("SettingsPanel.FrameGen"), AutoSize = true };
        public readonly NumericUpDown Fcx = Dec(0.1m, 1m), Fcy = Dec(0.1m, 1m), Fsx = Dec(-1m, 1m), Fsy = Dec(-1m, 1m), Frx = Dec(1m, 10m), Fry = Dec(1m, 10m);
        readonly Button idle = new Button { Width = 90, Height = 24, FlatStyle = FlatStyle.Flat };
        public readonly CheckBox ChromaOn = new CheckBox { Text = Lang.Get("SettingsPanel.ChromaKey"), AutoSize = true };
        public readonly CheckBox Audio = new CheckBox { Text = Lang.Get("SettingsPanel.StreamAudio"), AutoSize = true };
        public readonly CheckBox Mute = new CheckBox { Text = Lang.Get("SettingsPanel.MuteSpeakers"), AutoSize = true };
        public readonly ComboBox Proto = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        public readonly NumericUpDown Packet = Num(500, 4000, 100);
        public readonly NumericUpDown Haptics = Dec(0m, 5m);
        public readonly CheckBox DebugDefault = new CheckBox { Text = Lang.Get("SettingsPanel.DebugLogging"), AutoSize = true };
        public readonly CheckBox Mic = new CheckBox { Text = Lang.Get("SettingsPanel.HeadsetMic"), AutoSize = true };
        public readonly ComboBox CaptureBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        public readonly ComboBox PowerPlanBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        List<PowerPlan.Plan> plans = new List<PowerPlan.Plan>();
        public readonly ComboBox RecenterKey = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 130 };
        const string V = "session_settings.video.";

        static NumericUpDown Num(int min, int max, int inc) => new NumericUpDown { Minimum = min, Maximum = max, Increment = inc, Width = 80 };
        static NumericUpDown Dec(decimal min, decimal max) => new NumericUpDown { Minimum = min, Maximum = max, Increment = 0.05m, DecimalPlaces = 2, Width = 80 };

        public SettingsPanel(AppSettings settings, Dictionary<string, object> session)
        {
            this.settings = settings;
            this.session = session;
            AutoScroll = true;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;
            Font = Ui.Body;
            var g = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.None, Location = Point.Empty, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };   // its natural width: the panel scrolls rather than clipping
            void row(string label, Control c, Control reasonFor = null)
            {
                g.Controls.Add(Ui.Line(label));
                var cell = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
                cell.Controls.Add(c);
                var r = new Label { AutoSize = true, ForeColor = Ui.DarkMuted, Font = new Font("Segoe UI", 8.5f), MaximumSize = new Size(520, 0), Margin = new Padding(3, 0, 3, 3), Visible = false };
                cell.Controls.Add(r);
                g.Controls.Add(cell);
                reasons[reasonFor ?? c] = r;
            }
            void section(string title) { var l = Ui.Line(title, Ui.Big); l.ForeColor = Ui.Accent; l.Margin = new Padding(3, 12, 3, 3); g.Controls.Add(l); g.SetColumnSpan(l, 2); }
            Control flow(params Control[] cs) { var f = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) }; f.Controls.AddRange(cs); return f; }
            void note(Control c) { if (c is Label nl) nl.MaximumSize = new Size(560, 0); g.Controls.Add(new Label()); g.Controls.Add(c); }   // wraps inside the column, not at the page's width

            section(Lang.Get("SettingsPanel.SectionVideo"));
            var res = flow(EyeW, Ui.Line("x"), EyeH);
            row(Lang.Get("SettingsPanel.ResolutionPerEye"), res, EyeW);
            var eyeNote = Ui.Line(string.Format(Lang.Get("SettingsPanel.EyeNote"), Nvenc.MaxEyeWidth, 2 * Nvenc.MaxEyeWidth));
            eyeNote.ForeColor = Ui.DarkMuted; note(eyeNote);
            Fps.Items.AddRange(new object[] { "60", "72", "80", "90", "96", "100", "120", "144", "207" });
            row(Lang.Get("SettingsPanel.RefreshRate"), Fps);
            row(Lang.Get("SettingsPanel.VideoBitrate"), Bitrate);
            var prof = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
            prof.Controls.Add(ProfFov);
            prof.Controls.Add(flow(ProfFull, Ui.Badge(Lang.Get("SettingsPanel.BadgeExperimental"))));
            row(Lang.Get("SettingsPanel.Encoding"), prof, ProfFov);
            row(Lang.Get("SettingsPanel.SplitEncodeLabel"), Split, Split);
            row(Lang.Get("SettingsPanel.FrameGenLabel"), flow(FrameGen, Ui.Badge(Lang.Get("SettingsPanel.BadgeExperimental"))), FrameGen);
            row(Lang.Get("SettingsPanel.FoveationCenterSize"), flow(Fcx, Fcy), Fcx);
            row(Lang.Get("SettingsPanel.FoveationCenterShift"), flow(Fsx, Fsy), Fsx);
            row(Lang.Get("SettingsPanel.FoveationEdgeRatio"), flow(Frx, Fry, Ui.Line(Lang.Get("SettingsPanel.EdgeRatioNote"))), Frx);
            idle.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { Color = idle.BackColor, FullOpen = true })
                    if (cd.ShowDialog(this) == DialogResult.OK) SetIdle(cd.Color);
            };
            var idleReset = new Button { Text = Lang.Get("SettingsPanel.IdleRecommended"), AutoSize = true };
            idleReset.Click += (s, e) => SetIdle(ColorTranslator.FromHtml("#" + Chroma.DefaultRgb));
            ChromaOn.CheckedChanged += (s, e) => { idle.Enabled = idleReset.Enabled = ChromaOn.Checked; };
            row(Lang.Get("SettingsPanel.ChromaMixReality"), ChromaOn, ChromaOn);
            row(Lang.Get("SettingsPanel.ChromaKeyColor"), flow(idle, idleReset), idle);
            var remind = Ui.Line(Lang.Get("SettingsPanel.ChromaReminder"));
            remind.ForeColor = Ui.DarkMuted;
            note(remind);

            section(Lang.Get("SettingsPanel.SectionAudio"));
            note(Audio);
            CaptureBox.Items.AddRange(new object[] { Lang.Get("SettingsPanel.CaptureProcessLoopback"), Lang.Get("SettingsPanel.CaptureVirtualCable") });
            row(Lang.Get("SettingsPanel.AudioCapture"), CaptureBox);
            note(Mic);
            Mic.Click += (s, e) => { if (Mic.Checked && AudioRoute.FindMicrophoneCable() == null && !string.Equals(Environment.GetEnvironmentVariable("AURAFLASHXR_TEST_NO_DIALOG"), "1")) using (var f = new CableForm(true)) { f.ShowDialog(this); if (AudioRoute.FindMicrophoneCable() == null) Mic.Checked = false; } };
            note(Mute);

            section(Lang.Get("SettingsPanel.SectionNetworkControllers"));
            Proto.Items.AddRange(new object[] { "Udp", "Tcp" });
            row(Lang.Get("SettingsPanel.StreamProtocol"), flow(Proto, Ui.Line(Lang.Get("SettingsPanel.UdpNote"))), Proto);
            row(Lang.Get("SettingsPanel.PacketSize"), Packet);
            row(Lang.Get("SettingsPanel.HapticsIntensity"), Haptics);
            RecenterKey.Items.AddRange(new object[] { "End", "Home", "Pause", "F9", "Ctrl+Alt+R", "off" });
            row(Lang.Get("SettingsPanel.RecenterKey"), RecenterKey);
            var keyNote = Ui.Line(Lang.Get("SettingsPanel.RecenterKeyNote"));
            keyNote.ForeColor = Ui.DarkMuted;
            note(keyNote);

            section(Lang.Get("SettingsPanel.SectionSystem"));
            row(Lang.Get("SettingsPanel.PowerPlan"), PowerPlanBox);
            var planNote = Ui.Line(Lang.Get("SettingsPanel.PowerPlanNote"));
            planNote.ForeColor = Ui.DarkMuted;
            note(planNote);

            section(Lang.Get("SettingsPanel.SectionLogging"));
            note(DebugDefault);
            var more = Ui.Line(Lang.Get("SettingsPanel.OtherOptionsNote"));
            more.ForeColor = Ui.DarkMuted;
            note(more);
            Controls.Add(g);
        }

        void SetIdle(Color c) { idle.BackColor = c; idle.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; idle.ForeColor = c.GetBrightness() > 0.5 ? Color.Black : Color.White; }

        static decimal Clamp(NumericUpDown n, double v) => Math.Max(n.Minimum, Math.Min(n.Maximum, (decimal)v));

        /// A short "why" next to a setting (the proposal's reasons), or "" to clear it.
        public void SetReason(Control c, string text)
        {
            if (reasons.TryGetValue(c, out var l)) { l.Text = text ?? ""; l.Visible = !string.IsNullOrEmpty(text); tips.SetToolTip(c, text ?? ""); }
        }

        public void ClearReasons() { foreach (var l in reasons.Values) { l.Text = ""; l.Visible = false; } }

        /// Reads the current values from session.json / auraflashxr.json (what is on disk wins).
        public new void Load()
        {
            session = Session.Load();
            var s = session;
            EyeW.Value = Clamp(EyeW, Json.Num(s, V + "transcoding_view_resolution.Absolute.width", 2496));
            EyeH.Value = Clamp(EyeH, Json.Num(s, V + "transcoding_view_resolution.Absolute.height.content", 2608));
            var f = ((int)Json.Num(s, V + "preferred_fps", 120)).ToString(CultureInfo.InvariantCulture);
            if (!Fps.Items.Contains(f)) Fps.Items.Add(f);
            Fps.SelectedItem = f;
            Bitrate.Value = Clamp(Bitrate, Json.Num(s, V + "bitrate.mode.ConstantMbps", 250));
            (settings.EncodeProfile == "full-split" ? ProfFull : ProfFov).Checked = true;
            Split.Checked = settings.SplitEncode;
            FrameGen.Checked = settings.FrameGen;
            Fcx.Value = Clamp(Fcx, Json.Num(s, V + "foveated_encoding.content.center_size_x", 0.45));
            Fcy.Value = Clamp(Fcy, Json.Num(s, V + "foveated_encoding.content.center_size_y", 0.4));
            Fsx.Value = Clamp(Fsx, Json.Num(s, V + "foveated_encoding.content.center_shift_x", 0.4));
            Fsy.Value = Clamp(Fsy, Json.Num(s, V + "foveated_encoding.content.center_shift_y", 0.1));
            Frx.Value = Clamp(Frx, Json.Num(s, V + "foveated_encoding.content.edge_ratio_x", 4));
            Fry.Value = Clamp(Fry, Json.Num(s, V + "foveated_encoding.content.edge_ratio_y", 5));
            ChromaOn.Checked = Chroma.Enabled(s);
            try { SetIdle(ColorTranslator.FromHtml("#" + Chroma.Rgb(s))); } catch { SetIdle(Color.FromArgb(0, 255, 0)); }
            idle.Enabled = ChromaOn.Checked;
            Audio.Checked = Json.Bool(s, "session_settings.audio.game_audio.enabled", true);
            Mute.Checked = settings.MuteSpeakers;
            Proto.SelectedItem = Json.Str(s, "session_settings.connection.stream_protocol.variant", "Udp") == "Tcp" ? "Tcp" : "Udp";
            Packet.Value = Clamp(Packet, Json.Num(s, "session_settings.connection.packet_size", 1400));
            Haptics.Value = Clamp(Haptics, Json.Num(s, "session_settings.headset.controllers.content.haptics.content.intensity_multiplier", 1));
            DebugDefault.Checked = settings.Debug;
            RecenterKey.Text = settings.RecenterKey;
            CaptureBox.SelectedIndex = settings.AudioCapture == "device" ? 1 : 0;
            Mic.Checked = Json.Bool(s, "session_settings.audio.microphone.enabled", false);
            plans = PowerPlan.List();
            PowerPlanBox.Items.Clear(); PowerPlanBox.Items.Add(Lang.Get("SettingsPanel.PowerPlanLeave"));
            foreach (var p in plans) PowerPlanBox.Items.Add(p.Name);
            var cur = plans.FindIndex(p => string.Equals(p.Guid, settings.PowerPlan, StringComparison.OrdinalIgnoreCase));
            PowerPlanBox.SelectedIndex = cur >= 0 ? cur + 1 : 0;
        }

        /// Puts a proposal into the controls (nothing is written to disk until Save).
        public void Prefill(Proposal p)
        {
            ClearReasons();
            void num(NumericUpDown n, double v, string why) { n.Value = Clamp(n, v); SetReason(n, why); }
            num(EyeW, p.EyeW, p.Why("resolution"));
            EyeH.Value = Clamp(EyeH, p.EyeH);
            var f = ((int)p.Fps).ToString(CultureInfo.InvariantCulture);
            if (!Fps.Items.Contains(f)) Fps.Items.Add(f);
            Fps.SelectedItem = f; SetReason(Fps, p.Why("fps"));
            num(Bitrate, p.BitrateMbps, p.Why("bitrate"));
            (p.Foveated ? ProfFov : ProfFull).Checked = true; SetReason(ProfFov, p.Why("foveation"));
            Split.Checked = true; SetReason(Split, Lang.Get("SettingsPanel.SplitWhy"));
            Audio.Checked = p.Audio; SetReason(Audio, p.Why("audio"));
        }

        public bool Save(out string error)
        {
            error = null;
            session = Session.Load(); // keep anything edited meanwhile (e.g. pairing)
            var s = session;
            int w = (int)EyeW.Value / 32 * 32, h = (int)EyeH.Value; // AuraFlashXR aligns widths to 32
            string codecVariant = Json.Str(s, V + "preferred_codec.variant", "H264");
            error = Nvenc.CheckEye(w, h, codecVariant);
            if (error != null) return false;
            var rate = double.Parse((string)Fps.SelectedItem, CultureInfo.InvariantCulture);
            foreach (var k in new[] { "transcoding_view_resolution", "emulated_headset_view_resolution" })
            {
                Json.Set(s, V + k + ".variant", "Absolute");
                Json.Set(s, V + k + ".Absolute.width", w);
                Json.Set(s, V + k + ".Absolute.height.set", true);
                Json.Set(s, V + k + ".Absolute.height.content", h);
            }
            Json.Set(s, V + "preferred_fps", rate);
            Json.Set(s, V + "bitrate.mode.variant", "ConstantMbps");
            Json.Set(s, V + "bitrate.mode.ConstantMbps", (int)Bitrate.Value);
            Json.Set(s, V + "foveated_encoding.enabled", ProfFov.Checked);
            Json.Set(s, V + "foveated_encoding.content.center_size_x", (double)Fcx.Value);
            Json.Set(s, V + "foveated_encoding.content.center_size_y", (double)Fcy.Value);
            Json.Set(s, V + "foveated_encoding.content.center_shift_x", (double)Fsx.Value);
            Json.Set(s, V + "foveated_encoding.content.center_shift_y", (double)Fsy.Value);
            Json.Set(s, V + "foveated_encoding.content.edge_ratio_x", (double)Frx.Value);
            Json.Set(s, V + "foveated_encoding.content.edge_ratio_y", (double)Fry.Value);
            // AuraFlashXR's negotiated-stream cache: rewritten at every connection, but the encoder tests (no headset) read it as it is
            Json.Set(s, "openvr_config.eye_resolution_width", w);
            Json.Set(s, "openvr_config.eye_resolution_height", h);
            Json.Set(s, "openvr_config.target_eye_resolution_width", w);
            Json.Set(s, "openvr_config.target_eye_resolution_height", h);
            Json.Set(s, "openvr_config.refresh_rate", (int)rate);
            Json.Set(s, "openvr_config.enable_foveated_encoding", ProfFov.Checked);
            Json.Set(s, "session_settings.audio.game_audio.enabled", Audio.Checked);
            Json.Set(s, "session_settings.audio.microphone.enabled", Mic.Checked);
            Json.Set(s, "session_settings.connection.stream_protocol.variant", (string)Proto.SelectedItem);
            Json.Set(s, "session_settings.connection.packet_size", (int)Packet.Value);
            Json.Set(s, "session_settings.headset.controllers.content.haptics.content.intensity_multiplier", (double)Haptics.Value);
            Chroma.Apply(s, settings, ChromaOn.Checked, $"{idle.BackColor.R:x2}{idle.BackColor.G:x2}{idle.BackColor.B:x2}");
            try { Session.Save(s); }
            catch (Exception e) { error = Lang.Get("SettingsPanel.CannotWriteSession") + e.Message; return false; }
            settings.EncodeProfile = ProfFull.Checked ? "full-split" : "foveated";
            settings.SplitEncode = Split.Checked || ProfFull.Checked;   // the full-frame profile forces 3 strips whatever this says
            settings.FrameGen = FrameGen.Checked;
            settings.MuteSpeakers = Mute.Checked;
            settings.AudioCapture = CaptureBox.SelectedIndex == 1 ? "device" : "process";
            settings.Debug = DebugDefault.Checked;
            settings.RecenterKey = RecenterKey.Text.Trim().Length > 0 ? RecenterKey.Text.Trim() : "End";
            settings.PowerPlan = PowerPlanBox.SelectedIndex > 0 && PowerPlanBox.SelectedIndex <= plans.Count ? plans[PowerPlanBox.SelectedIndex - 1].Guid : (PowerPlanBox.SelectedIndex < 0 ? settings.PowerPlan : "");
            settings.Save();
            Log.Write("INFO", $"settings saved: {w}x{h}/eye @ {rate} Hz, {Bitrate.Value} Mbps, {settings.EncodeProfile}, split {settings.SplitEncode}, frame generation {settings.FrameGen}, idle #{settings.IdleRgb}, audio {Audio.Checked}, {Proto.SelectedItem}, debug {settings.Debug}");
            return true;
        }
    }
}
