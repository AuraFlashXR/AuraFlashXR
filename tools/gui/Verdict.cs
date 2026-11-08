// The verdict of a run of the tests: the measurements against the targets for the stream that is sent, then what to do about the poor ones.
// Shared by the setup wizard (results page) and its --diagnose mode.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AuraFlashXR
{
    public static class Verdict
    {
        public static FlowLayoutPanel Build(Handshake hs, TestContext ctx, HmdProfile profile, Proposal proposal, double ctxFps, double ctxScale, bool ctxFoveation)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            var r = ctx?.R ?? new Results();
            var prof = hs.Profile;
            // what the setup is judged against: rules of thumb scaled to the stream that is sent (the encoded size when it was measured)
            var rate = proposal != null ? proposal.Fps : ctxFps;
            var ew = r.EncW > 0 ? r.EncW : (int)(hs.PanelW * ctxScale * 2 * (hs.Foveation ? 0.35 : 1.0));
            var eh = r.EncH > 0 ? r.EncH : (int)(hs.PanelH * ctxScale * (hs.Foveation ? 0.52 : 1.0));
            var tg = Targets.For(ew, eh, ctxFps);
            var list = new ListView { View = View.Details, FullRowSelect = true, Width = 790, Height = 350, ShowItemToolTips = true };
            Ui.Col(list, Lang.Get("Verdict.ColMeasure"), 220); Ui.Col(list, Lang.Get("Verdict.ColYourValue"), 190); Ui.Col(list, Lang.Get("Verdict.ColTarget"), 280); Ui.Col(list, Lang.Get("Verdict.ColVerdict"), 90);
            var bad = new List<string>();
            void add(string name, string val, string target, Rating rt, string advice = null)
            {
                var it = new ListViewItem(new[] { name, val, target, rt == Rating.Unknown ? "-" : rt.ToString() }) { ToolTipText = name + ": " + val + "\n" + target };
                it.ForeColor = rt == Rating.Good ? Color.SeaGreen : rt == Rating.Ok ? Color.DarkOrange : rt == Rating.Poor ? Color.Firebrick : Color.Gray;
                list.Items.Add(it);
                if (rt == Rating.Poor && advice != null) bad.Add(advice);
            }
            var frame = r.BudgetMs > 0 ? r.BudgetMs : tg.FrameMs;
            add(Lang.Get("Verdict.EncodeP95"), r.EncodeP95 > 0 ? $"{r.EncodeP95:0.0} ms (trame {frame:0.0} ms)" : "-", string.Format(Lang.Get("Verdict.EncodeTarget"), Reference.Get("encode_ok") * 100), Reference.EncodeBudget(r.EncodeP95, frame, tg),
                Lang.Get("Verdict.EncodeAdvice"));
            if (profile != null)
            {
                var cfgMbps = ctx != null ? ctx.ConfigMbps : 0;
                add("Network at your configured bitrate", cfgMbps <= 0 ? "-" : (r.CleanMbps > 0 ? $"{cfgMbps:0} Mbps: clean" : $"{cfgMbps:0} Mbps: NOT clean"), "no packet loss, the headset keeps its frame rate, the latency does not rise (one step at the configured value, nothing is searched)",
                    cfgMbps <= 0 ? Rating.Unknown : (r.CleanMbps > 0 ? Rating.Good : Rating.Poor), $"Your link did not carry {cfgMbps:0} Mbps cleanly: choose a lower network variant or bitrate in the setup, or improve the Wi-Fi: {prof.WifiAdvice}.");
                if (profile.SplitEncode > 0)
                    add("Split encoding", r.Slices <= 0 ? "-" : (r.Engines >= 2 && r.Slices >= 1.5 ? $"engaged ({r.Slices:0.#} strips, {r.Engines} engines)" : $"not engaged ({r.Engines} engine(s))"), "engaged when the GPU has two or more NVENC engines",
                        r.Slices <= 0 ? Rating.Unknown : (r.Engines >= 2 && r.Slices >= 1.5 ? Rating.Good : Rating.Ok));
            }
            else
            add("Clean network bitrate", r.CleanMbps > 0 ? $"{r.CleanMbps:0} Mbps" : "-", $"{tg.MinMbps:0} Mbps minimum, {tg.GoodMbps:0}+ comfortable ({ew}x{eh} at {ctxFps:0} Hz, HEVC)", Reference.Bitrate(r.CleanMbps, tg),
                $"The link carries less than the stream needs ({tg.MinMbps:0} Mbps): {prof.WifiAdvice}.");
            add("Packets lost per second", r.LossPerS >= 0 && r.CleanMbps > 0 ? $"{r.LossPerS:0.00}" : "-", "0 is clean; up to 1 is tolerable", Reference.Loss(r.CleanMbps > 0 ? r.LossPerS : -1), $"The wireless link drops packets: {prof.WifiAdvice}.");
            add("Total latency (AuraFlashXR's own)", r.TotalMs > 0 ? $"{r.TotalMs:0.0} ms" : "-", $"under {tg.LatencyGoodMs:0} ms good, {tg.LatencyOkMs:0} ms acceptable", Reference.Latency(r.TotalMs, tg));
            add("   network", r.NetworkMs > 0 ? $"{r.NetworkMs:0.0} ms" : "-", $"about {tg.NetworkGoodMs:0} ms", Reference.Network(r.NetworkMs, tg));
            add("   headset decoder", r.DecodeMs > 0 ? $"{r.DecodeMs:0.0} ms" : "-", $"5-{tg.DecodeGoodMs:0} ms at high bitrates", Reference.Decode(r.DecodeMs, tg));
            add("   encoder (AuraFlashXR's view)", r.EncoderMs > 0 ? $"{r.EncoderMs:0.0} ms" : "-", "", Rating.Unknown);
            add("   headset queues", r.DecoderQueueMs + r.VsyncQueueMs > 0 ? $"{r.DecoderQueueMs + r.VsyncQueueMs:0.0} ms" : "-", "", Rating.Unknown);
            add("Frames shown / refresh rate", r.DisplayedFps > 0 ? $"{r.DisplayedFps:0} of {ctxFps:0} fps" : "-", $"{Reference.Get("fps_good_share") * 100:0}% or more", Reference.FpsShare(r.DisplayedFps > 0 ? r.DisplayedFps / ctxFps : 0));
            if (r.SceneRan)
            {
                add("Head tracking (scene test)", $"{r.HeadSamplesPerS:0}/s, gaps up to {r.HeadGapP95Ms:0} ms, turned {r.HeadYawRangeDeg:0} deg", $"about {ctxFps:0} samples per second, no long gaps; the scene saw your movement", r.HeadSamplesPerS >= 0.8 * ctxFps && (r.HeadYawRangeDeg >= 5 || r.HeadMoveCm >= 3) ? Rating.Good : Rating.Ok);
                add("Repeated head samples", $"{r.SameTsPct:0.#}% of frames", "a frame sharing the previous frame's head sample is not shown as a new picture; a few percent is normal", r.SameTsPct < 5 ? Rating.Good : r.SameTsPct < 15 ? Rating.Ok : Rating.Poor);
            }
            add("Video codec", hs.Enc10 ? "HEVC 10-bit" : "HEVC 8-bit", prof.CodecAdvice, hs.Enc10 || !prof.Tuned ? Rating.Good : Rating.Ok);
            // OpenXR API layers that load into every game: some capture the controllers before any runtime sees them
            var layersOn = XrLayers.ActiveImplicit();
            var risky = layersOn.Where(l => l.MayCaptureInput).ToList();
            add("OpenXR layers (loaded into every game)", layersOn.Count == 0 ? "none" : string.Join(", ", layersOn.Select(l => l.Name)), "none: a layer can capture the controllers before the runtime sees them",
                risky.Count > 0 ? Rating.Poor : layersOn.Count > 0 ? Rating.Ok : Rating.Good,
                "OpenXR layers that may capture the controllers are enabled (" + string.Join(", ", risky.Select(l => l.Name)) + "): hands and controllers may do nothing in games. Review them with the button below; switching one off is reversible.");
            // Windows Firewall: one rule that lets this folder's auraflashxr_host.exe in; a moved folder or an old install leaves clutter
            var fw = Firewall.AnalyzeCached(Paths.Host);
            var fwClean = fw.State == FirewallState.Allowed && fw.Clutter == 0;
            add(Lang.Get("Verdict.Firewall"), fw.State == FirewallState.Allowed ? (fw.Clutter == 0 ? Lang.Get("Verdict.FwAllowed") : string.Format(Lang.Get("Verdict.FwAllowedClutter"), fw.Clutter)) : fw.State == FirewallState.Blocked ? Lang.Get("Verdict.FwBlocked") : fw.State == FirewallState.Missing ? Lang.Get("Verdict.FwNoRule") : Lang.Get("Verdict.FwNotRead"),
                Lang.Get("Verdict.FirewallTarget"),
                fwClean ? Rating.Good : fw.State == FirewallState.Allowed ? Rating.Ok : fw.State == FirewallState.Unknown ? Rating.Unknown : Rating.Poor,
                fw.Headline + " Review the rules with the button below.");
            add("GPU", string.IsNullOrEmpty(r.GpuName) ? "-" : r.GpuName, $"{r.Engines} hardware encoder engine(s), {r.VramMb / 1024.0:0} GB", Rating.Unknown);
            var details = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
            details.Controls.Add(list);
            Ui.FitColumns(list);
            if (!fwClean && fw.State != FirewallState.Unknown)
            {
                var fwReview = new Button { Text = Lang.Get("Verdict.BtnReviewFw"), AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
                fwReview.Click += (s2, e2) => { using (var f = new FirewallForm()) f.ShowDialog(fwReview.FindForm()); };
                details.Controls.Add(fwReview);
            }
            if (layersOn.Count > 0)
            {
                var review = new Button { Text = Lang.Get("Verdict.BtnReviewLayers"), AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
                review.Click += (s2, e2) => { using (var f = new LayersForm()) f.ShowDialog(review.FindForm()); };
                details.Controls.Add(review);
            }
            var count = new Func<string, int>(v => list.Items.Cast<ListViewItem>().Count(x => x.SubItems[3].Text == v));
            var verdict = count("Poor") > 0 ? Lang.Get("Verdict.SummaryPoor") : (count("Ok") > 0 ? Lang.Get("Verdict.SummaryOk") : (count("Good") > 0 ? Lang.Get("Verdict.SummaryGood") : Lang.Get("Verdict.SummaryNotEnough")));
            var vl = Ui.Line(verdict, Ui.Big);
            vl.ForeColor = count("Poor") > 0 ? Color.Firebrick : count("Ok") > 0 ? Color.DarkOrange : Color.SeaGreen;
            p.Controls.Add(vl);
            if (profile == null && r.CleanMbps >= 2 * tg.GoodMbps && r.CleanMbps > 0)
                p.Controls.Add(Ui.Line(string.Format(Lang.Get("Verdict.LinkFaster"), r.CleanMbps, tg.GoodMbps)));
            foreach (var a in bad.Distinct()) p.Controls.Add(Ui.Line("\u2022 " + a));
            details.Controls.Add(Ui.Line(string.Format(Lang.Get("Verdict.WifiAdviceFor"), prof.Label, prof.WifiAdvice)));
            // suggestions from docs/PERFORMANCE_KNOBS.md: this run's measurements, and the logs when there are any
            try
            {
                var settings = new AppSettings();
                var facts = LogFacts.Read(Paths.Logs, Games.Dir);
                p.Controls.Add(Advice.Panel(Advice.From(r, hs, ctxFps, settings.Debug, facts, () => { settings.Debug = false; settings.Save(); }), 3));
            }
            catch (Exception e) { Log.Write("WARN", "suggestions: " + e.Message); }
            var note = Ui.Line(Lang.Get("Verdict.NoteTargets"));
            note.ForeColor = Color.DimGray;
            details.Controls.Add(note);
            p.Controls.Add(Ui.Expander(Lang.Get("Verdict.ExpanderDetails"), details));
            Lang.TranslateControl(p); return p;
        }
    }
}
