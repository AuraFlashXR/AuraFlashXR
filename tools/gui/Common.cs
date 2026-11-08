// Shared by AuraFlashXR.exe (runtime) and configure.exe: install layout, JSON, the general log, headset discovery, the host
// process, the system check, OpenXR registration and the common window header. .NET Framework 4.8 (part of Windows 10/11).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

// What Visual Studio adds to every project: WinForms' per-monitor DPI support (app.config, bin\*.config) only applies to an assembly that says it targets 4.7 or newer.
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName=".NET Framework 4.8")]

namespace AuraFlashXR
{
    public static class Version
    {
        /// <summary>Version affichée dans l'UI (CompactHeader, À propos, logs). Source unique : modifier ici uniquement.</summary>
        public const string AppVersion = "V0";
        public const string Number = "Pre-Release";
        /// "Pre-Release", plus " (debug build)" for a package built with the per-frame telemetry (bin\flavour.txt, written by the packaging step; docs/DEVELOPMENT.md)
        public static readonly string Text = Number + (IsDebugBuild() ? " (debug build)" : "");
        static bool IsDebugBuild()
        {
            try { return File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flavour.txt")).Trim() == "debug"; }
            catch { return false; }   // a developer run (uitest, a bare compile): no file, nothing said
        }
    }

    /// The portable folder: everything next to the exe, config/ and logs/ inside it.
    public static class Paths
    {
        /// The package root. Everything but the launcher lives in bin\ (AuraFlashXR.exe, the launcher, is the only file in the root): their folder's parent is the root.
        public static readonly string Dir = RootOf(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
        static string RootOf(string d) => string.Equals(Path.GetFileName(d), "bin", StringComparison.OrdinalIgnoreCase) ? (Path.GetDirectoryName(d) ?? d) : d;
        public static string Bin => Path.Combine(Dir, "bin");
        public static string Config => Path.Combine(Dir, "config");
        public static string Logs => Path.Combine(Dir, "logs");
        public static string Docs => Path.Combine(Dir, "docs");
        public static string Session => Path.Combine(Config, "session.json");
        public static string SessionDefault => Path.Combine(Config, "session.default.json");
        public static string Settings
        {
            get
            {
                var auraflash = Path.Combine(Config, "auraflashxr.json");
                return File.Exists(auraflash) || !File.Exists(Path.Combine(Config, "auraflashxr.json")) ? auraflash : Path.Combine(Config, "auraflashxr.json");
            }
        }
        public static string GeneralLog
        {
            get
            {
                var auraflash = Path.Combine(Logs, "AuraFlashXR.log");
                return File.Exists(auraflash) || !File.Exists(Path.Combine(Logs, "AuraFlashXR.log")) ? auraflash : Path.Combine(Logs, "AuraFlashXR.log");
            }
        }
        public static string Host
        {
            get
            {
                var auraflash = Path.Combine(Bin, "auraflashxr_host.exe");
                return File.Exists(auraflash) ? auraflash : Path.Combine(Bin, "auraflashxr_host.exe");
            }
        }
        public static string MirrorDesktop => Path.Combine(Bin, "mirrordesktop.exe");
        public static string Manifest => Path.Combine(Bin, "virtualdesktop-openxr.json");
        /// <summary>The OpenXR registration script: embedded in the exes as a resource (no .ps1 in the package), written to %TEMP%\AuraFlashXR on first use.</summary>
        public static string RegisterScript
        {
            get
            {
                var dir = Path.Combine(Path.GetTempPath(), "AuraFlashXR");
                var path = Path.Combine(dir, "register_openxr_runtime.ps1");
                try
                {
                    byte[] bytes;
                    using (var rs = typeof(Paths).Assembly.GetManifestResourceStream("register_openxr_runtime.ps1"))
                    using (var ms = new MemoryStream())
                    {
                        if (rs == null) return path;   // resource absent (should not happen): powershell reports the missing file
                        rs.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                    if (!File.Exists(path) || !bytes.SequenceEqual(File.ReadAllBytes(path)))
                    {
                        Directory.CreateDirectory(dir);
                        var tmp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
                        File.WriteAllBytes(tmp, bytes);
                        File.Copy(tmp, path, true);   // the exes may extract at once: same content, last copy wins
                        File.Delete(tmp);
                    }
                }
                catch { }   // %TEMP% unwritable or the script locked by another instance: an existing copy still works; powershell reports a missing file otherwise
                return path;
            }
        }
        public static string Configure => Path.Combine(Bin, "configure.exe");
        public static string Runtime => Path.Combine(Bin, "streamer.exe");
        public static string AdbForwarder
        {
            get
            {
                var p1 = Path.Combine(Dir, @"adbforwarder\ADBForwarder.exe");
                if (File.Exists(p1)) return p1;
                var p2 = Path.Combine(Dir, @"tools\adbforwarder\ADBForwarder.exe");
                if (File.Exists(p2)) return p2;
                var p3 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"adbforwarder\ADBForwarder.exe");
                if (File.Exists(p3)) return p3;
                return p1;
            }
        }
    }

    /// config\games\<exe name without .exe>.json, one file per game, written by the streamer (tools/host/src/games.rs documents the layout).
    public static class Games
    {
        public static string Dir => Path.Combine(Paths.Config, "games");
        public static string Stem(string exe)
        {
            var n = Path.GetFileName(exe ?? "");
            if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            foreach (var c in "<>:\"/\\|?*") n = n.Replace(c, '_');
            return n.Length == 0 ? "unknown" : n;
        }
        public static string FileOf(string exe) => Path.Combine(Dir, Stem(exe) + ".json");
        public static Dictionary<string, object> Load(string exe) => Json.Load(FileOf(exe));
        public static IEnumerable<string> Files() => Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.json") : new string[0];
        /// The desktop mirror is a game to the engine (it keeps its record and log) but not to the person: no list, label, advice or clean-up shows it.
        public static bool IsHidden(string exe) => string.Equals(Path.GetFileName(exe ?? ""), "mirrordesktop.exe", StringComparison.OrdinalIgnoreCase);
        /// The same for a record file name (config\games\mirrordesktop.json).
        public static bool IsHiddenFile(string file) => IsHidden(Path.GetFileNameWithoutExtension(file) + ".exe");
    }

    /// One zip for a bug report: a game's record (when given), the general log, the newest debug run, the DLL install record, info.txt. No dialog of its own and
    /// nothing is started: the caller shows where the file went.
    public static class LogExport
    {
        public static string DefaultName(string stem) => $"AuraFlashXR-{(string.IsNullOrEmpty(stem) ? "logs" : stem)}-{DateTime.Now:yyyyMMdd-HHmm}.zip";

        /// Writes the zip to `path` (replacing a file there) and returns the entry names.
        public static List<string> Write(string path, string stem)
        {
            var names = new List<string>();
            if (File.Exists(path)) File.Delete(path);
            using (var z = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            {
                void add(string file, string name)
                {
                    if (!File.Exists(file)) return;
                    // the logs are being written by the streamer: read with sharing
                    using (var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var dst = z.CreateEntry(name).Open()) src.CopyTo(dst);
                    names.Add(name);
                }
                if (!string.IsNullOrEmpty(stem)) add(Games.FileOf(stem), "game/" + stem + ".json");
                add(Paths.GeneralLog, Path.GetFileName(Paths.GeneralLog));
                var dbg = Path.Combine(Paths.Logs, "debug");
                if (Directory.Exists(dbg))
                {
                    var newest = new DirectoryInfo(dbg).GetDirectories().OrderByDescending(d => d.LastWriteTime).FirstOrDefault();
                    if (newest != null) foreach (var f in newest.GetFiles()) add(f.FullName, "debug/" + newest.Name + "/" + f.Name);
                }
                using (var w = new StreamWriter(z.CreateEntry("info.txt").Open())) w.WriteLine($"AuraFlashXR {Version.Text}\n{DateTime.Now:s}\n{Environment.OSVersion}");
                names.Add("info.txt");
            }
            Log.Write("INFO", "logs exported to " + path);
            return names;
        }

        /// Save dialog (disposed before the work starts), the export, and the inline result in `status`: "Saved to <file>" with an Open folder link. Never starts Explorer itself.
        public static void Run(IWin32Window owner, string stem, Label status, LinkLabel open)
        {
            string path;
            using (var dlg = new SaveFileDialog { Filter = "Zip|*.zip", FileName = DefaultName(stem) })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            try
            {
                Write(path, stem);
                status.ForeColor = Color.SeaGreen; status.Text = "Saved to " + path;
                open.Tag = path; open.Visible = true;
            }
            catch (Exception ex) { status.ForeColor = Color.Firebrick; status.Text = "Export failed: " + ex.Message; open.Visible = false; Log.Write("ERROR", "log export failed: " + ex.Message); }
            status.Visible = true;
        }

        public static void Reveal(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
        }
    }

    /// The DLSS motion-vector extractor (one DLL) that releases up to 0.6 copied next to a game's exe on request. Releases no longer ship or install it; what
    /// is left is the removal of the copies AuraFlashXR made. config\mv_dll.json recorded each install (file, folder, MD5), and only a file that still has the
    /// recorded MD5 is deleted: anything else in the game folder (someone else's proxy DLL, a copy replaced since) is left alone.
    public static class MvDll
    {
        static string Record => Path.Combine(Paths.Config, "mv_dll.json");

        static string Md5(string path)
        {
            try { using (var m = System.Security.Cryptography.MD5.Create()) using (var f = File.OpenRead(path)) return BitConverter.ToString(m.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }
            catch { return ""; }
        }

        static Dictionary<string, object> Installs(Dictionary<string, object> r)
        {
            if (!(r.TryGetValue("installs", out var o) && o is Dictionary<string, object> d)) { d = new Dictionary<string, object>(); r["installs"] = d; }
            return d;
        }
        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v as string ?? "" : "";

        /// The file name of the copy recorded for this game, or null when AuraFlashXR installed nothing there.
        public static string Installed(string stem)
        {
            var e = Installs(Json.Load(Record)).TryGetValue(stem, out var o) ? o as Dictionary<string, object> : null;
            return e != null && Str(e, "file").Length > 0 ? Str(e, "file") : null;
        }

        /// Deletes the recorded copy when it is still the file AuraFlashXR put there, and forgets the record either way. Returns a message for the user.
        public static string Uninstall(string stem)
        {
            var r = Json.Load(Record);
            var all = Installs(r);
            if (!(all.TryGetValue(stem, out var o) && o is Dictionary<string, object> e)) return "Nothing recorded as installed for this game by AuraFlashXR.";
            string name = Str(e, "file"), dir = Str(e, "dir");
            var target = name.Length > 0 && dir.Length > 0 ? Path.Combine(dir, name) : null;
            string msg;
            if (target == null || !File.Exists(target)) msg = "The file is no longer in the game folder.";
            else if (Md5(target) != Str(e, "md5")) msg = $"{name} in {dir} has changed since AuraFlashXR installed it; it was left in place.";
            else if (!DeleteMaybeElevated(target)) return "The file was not removed (administrator permission refused?).";
            else msg = $"Removed {name} from {dir}.";
            all.Remove(stem);
            Json.Save(Record, r);
            Log.Write("INFO", $"motion-vector DLL record removed for {stem}: {msg}");
            return msg;
        }

        static bool DeleteMaybeElevated(string path)
        {
            try { File.Delete(path); return true; } catch (UnauthorizedAccessException) { } catch (IOException) { }
            return Elevated($"/c del /f /q \"{path}\"") && !File.Exists(path);
        }
        static bool Elevated(string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo("cmd.exe", args) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                p.WaitForExit(30000);
                return true;
            }
            catch { return false; }   // the user refused the prompt
        }
    }

    /// JSON as Dictionary / object[] trees (JavaScriptSerializer), written back indented so session.json stays hand-editable.
    public static class Json
    {
        static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 1000 };

        public static Dictionary<string, object> Parse(string text) => Ser.DeserializeObject(text) as Dictionary<string, object> ?? new Dictionary<string, object>();

        public static Dictionary<string, object> Load(string path)
        {
            try { return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, object>(); }
            catch (Exception e) { Log.Write("WARN", $"cannot read {path}: {e.Message}"); return new Dictionary<string, object>(); }
        }

        public static void Save(string path, Dictionary<string, object> root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            Write(sb, root, 0);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false)); // no BOM: AuraFlashXR's serde_json rejects it
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }

        static void Write(StringBuilder sb, object v, int ind)
        {
            string pad(int n) => new string(' ', n * 2);
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: sb.Append(Ser.Serialize(s)); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case Dictionary<string, object> d:
                    if (d.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    int i = 0;
                    foreach (var kv in d)
                    {
                        sb.Append(pad(ind + 1)).Append(Ser.Serialize(kv.Key)).Append(": ");
                        Write(sb, kv.Value, ind + 1);
                        sb.Append(++i < d.Count ? ",\n" : "\n");
                    }
                    sb.Append(pad(ind)).Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    var items = list.Cast<object>().ToList();
                    if (items.Count == 0) { sb.Append("[]"); break; }
                    sb.Append("[\n");
                    for (int k = 0; k < items.Count; k++)
                    {
                        sb.Append(pad(ind + 1));
                        Write(sb, items[k], ind + 1);
                        sb.Append(k + 1 < items.Count ? ",\n" : "\n");
                    }
                    sb.Append(pad(ind)).Append(']');
                    break;
                case double dd: sb.Append(Num(dd)); break;
                case float ff: sb.Append(Num(ff)); break;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); break;
                default: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        static string Num(double d) => (d == Math.Floor(d) && Math.Abs(d) < 1e15) ? d.ToString("0.0", CultureInfo.InvariantCulture) : d.ToString("R", CultureInfo.InvariantCulture);

        public static object Get(Dictionary<string, object> root, string path)
        {
            object cur = root;
            foreach (var k in path.Split('.'))
            {
                if (!(cur is Dictionary<string, object> d) || !d.TryGetValue(k, out cur)) return null;
            }
            return cur;
        }

        public static void Set(Dictionary<string, object> root, string path, object value)
        {
            var keys = path.Split('.');
            var cur = root;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (!(cur.TryGetValue(keys[i], out var next) && next is Dictionary<string, object> nd))
                {
                    nd = new Dictionary<string, object>();
                    cur[keys[i]] = nd;
                }
                cur = nd;
            }
            cur[keys[keys.Length - 1]] = value;
        }

        /// Removes the key (a setting back at its default stays out of the file); true when there was one.
        public static bool Remove(Dictionary<string, object> root, string path)
        {
            var keys = path.Split('.');
            object cur = root;
            for (int i = 0; i < keys.Length - 1; i++) if (!(cur is Dictionary<string, object> d && d.TryGetValue(keys[i], out cur))) return false;
            return cur is Dictionary<string, object> last && last.Remove(keys[keys.Length - 1]);
        }

        public static double Num(Dictionary<string, object> root, string path, double def)
        {
            var v = Get(root, path);
            try { return v == null || v is string || v is bool ? def : Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        public static string Str(Dictionary<string, object> root, string path, string def) => Get(root, path) as string ?? def;
        public static bool Bool(Dictionary<string, object> root, string path, bool def) => Get(root, path) is bool b ? b : def;

        // The files are written by hand (Set/Write) and read by serde_json on the host: a key the schema does not know
        // makes the whole load fail and the host silently keeps its defaults. These two keep a file loadable.
        /// The schema's field names for a settings struct (the keys the host accepts at this path), or null when the
        /// schema is not known here (the caller then leaves the struct alone).
        public static HashSet<string> SchemaFields(string path)
        {
            HashSet<string> Set(params string[] names) => new HashSet<string>(names, StringComparer.Ordinal);
            switch (path)
            {
                case "session_settings.video.foveated_encoding.content":
                    return Set("force_enable", "center_size_x", "center_size_y", "center_shift_x", "center_shift_y", "edge_ratio_x", "edge_ratio_y");
                case "session_settings.video.bitrate.mode":
                    return Set("ConstantMbps", "Adaptive", "variant");
                case "session_settings.video.bitrate.mode.Adaptive":
                    return Set("saturation_multiplier", "max_throughput_mbps", "min_throughput_mbps", "max_network_latency_ms", "encoder_latency_limiter", "decoder_latency_limiter");
                case "session_settings.video.bitrate":
                    return Set("mode", "adapt_to_framerate", "history_size", "image_corruption_fix");
                case "session_settings.video.bitrate.adapt_to_framerate":
                    return Set("enabled", "content");
                case "session_settings.video.bitrate.adapt_to_framerate.content":
                    return Set("framerate_reset_threshold_multiplier");
                case "session_settings.video.encoder_config":
                    return Set("quality_preset", "enable_vbaq", "rate_control_mode", "h264_profile", "entropy_coding", "filler_data", "use_10bit", "server_overrides_use_10bit", "use_full_range", "server_overrides_use_full_range", "encoding_gamma", "server_overrides_encoding_gamma", "hdr", "nvenc", "amf", "software");
                case "session_settings.video.encoder_config.nvenc":
                    return Set("quality_preset", "tuning_preset", "multi_pass", "adaptive_quantization_mode", "low_delay_key_frame_scale", "refresh_rate", "enable_intra_refresh", "intra_refresh_period", "intra_refresh_count", "max_num_ref_frames", "gop_length", "p_frame_strategy", "rate_control_mode", "rc_buffer_size", "rc_initial_delay", "rc_max_bitrate", "rc_average_bitrate", "enable_weighted_prediction");
                case "openvr_config":
                    return OpenvrFields;
            }
            return null;
        }

        /// The keys of the host's openvr_config (the shim reads this struct with picojson; a key it does not know is ignored
        /// there, but keeping the list exact makes the two files comparable).
        static readonly HashSet<string> OpenvrFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "_client_core_debug", "_client_gfx_debug", "_client_impl_debug", "_connection_debug", "_controller_profile", "_decoder_debug", "_encoder_debug", "_server_core_debug", "_server_gfx_debug", "_server_impl_debug", "_sockets_debug",
            "adapter_index", "amd_bitrate_corruption_fix", "amf_preproc_sigma", "amf_preproc_tor", "body_tracking_has_legs", "body_tracking_vive_enabled", "brightness", "capture_frame_dir", "clamp_hdr_extended_range", "codec", "contrast", "controller_is_tracker", "controllers_enabled",
            "enable_amf_hmqb", "enable_amf_pre_analysis", "enable_color_correction", "enable_foveated_encoding", "enable_hdr", "enable_intra_refresh", "enable_vbaq", "enable_vive_tracker_proxy", "encoder_quality_preset", "encoding_gamma", "entropy_coding", "eye_resolution_height", "eye_resolution_width",
            "filler_data", "force_hdr_srgb_correction", "force_sw_encoding", "foveation_center_shift_x", "foveation_center_shift_y", "foveation_center_size_x", "foveation_center_size_y", "foveation_edge_ratio_x", "foveation_edge_ratio_y", "gamma", "gop_length", "h264_profile",
            "intra_refresh_count", "intra_refresh_period", "linux_async_compute", "linux_async_reprojection", "max_num_ref_frames", "minimum_idr_interval_ms", "nvenc_adaptive_quantization_mode", "nvenc_enable_weighted_prediction", "nvenc_low_delay_key_frame_scale", "nvenc_multi_pass",
            "nvenc_quality_preset", "nvenc_rate_control_mode", "nvenc_qp_map", "nvenc_refresh_rate", "nvenc_slice_count", "nvenc_split_encode_mode", "nvenc_strict_vbv", "nvenc_tuning_preset", "p_frame_strategy", "rate_control_mode", "rc_average_bitrate", "rc_buffer_size", "rc_initial_delay", "rc_max_bitrate",
            "refresh_rate", "saturation", "sharpening", "sw_thread_count", "target_eye_resolution_height", "target_eye_resolution_width", "tracking_ref_only", "use_10bit_encoder", "use_amf_preproc", "use_full_range_encoding", "use_separate_hand_trackers",
        };

        /// Removes from the struct at `path` the keys the schema does not declare; returns how many went. A struct the
        /// schema is not known for is left as it is.
        public static int Prune(Dictionary<string, object> root, string path)
        {
            var fields = SchemaFields(path);
            if (fields == null) return 0;
            if (!(Get(root, path) is Dictionary<string, object> d)) return 0;
            int removed = 0;
            foreach (var k in new List<string>(d.Keys))
            {
                // a hand-written value may carry a "variant" the schema does not declare (the enum lives on the parent)
                if (d[k] is Dictionary<string, object> sub && !fields.Contains(k + ".variant")) sub.Remove("variant");
                if (!fields.Contains(k)) { d.Remove(k); removed++; }
            }
            return removed;
        }
        public static string Serialize(object o) => Ser.Serialize(o);
    }

    /// The general log logs\AuraFlashXR.log, shared with the host and the shim (one appended write per line).
    public static class Log
    {
        public static string Component = "gui";
        static readonly object Lock = new object();

        public static void Write(string level, string msg)
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(Paths.Logs);
                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Component}] {level,-5} {msg}\r\n";
                    using (var fs = new FileStream(Paths.GeneralLog, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                    {
                        var b = Encoding.UTF8.GetBytes(line);
                        fs.Write(b, 0, b.Length);
                    }
                }
            }
            catch { }
        }
    }

    /// config\auraflashxr.json: AuraFlashXR's own settings (the AuraFlashXR ones stay in session.json).
    public class AppSettings
    {
        public Dictionary<string, object> Root = Json.Load(Paths.Settings);

        /// Recharge auraflashxr.json depuis le disque (preset changé sous nos pieds : configure/launcher
        /// rechargent via LoadProfile, le streamer appelle ceci depuis son poll de preset.cfg).
        public void Reload() { Root = Json.Load(Paths.Settings); }
        public string HeadsetHost { get => Json.Str(Root, "headset.hostname", ""); set => Json.Set(Root, "headset.hostname", value); }
        public string HeadsetName { get => Json.Str(Root, "headset.name", ""); set => Json.Set(Root, "headset.name", value); }
        public string HeadsetIp { get => Json.Str(Root, "headset.ip", ""); set => Json.Set(Root, "headset.ip", value); }
        public string EncodeProfile { get => Json.Str(Root, "video.encode_profile", "foveated"); set => Json.Set(Root, "video.encode_profile", value); }
        /// NVENC split-frame encoding ("3" = one strip per engine, up to 3; "off"): on by default, the reason the project exists on a multi-engine GPU
        /// the Logs window deletes the records of games whose exe no longer exists (on by default)
        public bool AutoDeleteGameLogs { get => Json.Bool(Root, "logs.auto_delete_uninstalled", true); set => Json.Set(Root, "logs.auto_delete_uninstalled", value); }
        /// the wizard asked the chroma-key question once (headsets other than the Vision Pro): the answer is in session.json, this only stops the repeat
        public bool ChromaAsked { get => Json.Bool(Root, "chroma.asked", false); set => Json.Set(Root, "chroma.asked", value); }
        /// the key that recentres while playing (the streamer polls it): End by default; "off" disables
        public string RecenterKey { get => Json.Str(Root, "controls.recenter_key", "End"); set => Json.Set(Root, "controls.recenter_key", value); }
        /// fond du launcher (menu BACKGROUND) : "" = fond par défaut, sinon le nom de fichier choisi
        public string Background { get => Json.Str(Root, "launcher.background", ""); set => Json.Set(Root, "launcher.background", value); }
        /// police du launcher (menu POLICE) et sa graisse
        public string FontFamily { get => Json.Str(Root, "launcher.font", "Orbitron"); set => Json.Set(Root, "launcher.font", value); }
        public string FontWeight { get => Json.Str(Root, "launcher.font_weight", "bold"); set => Json.Set(Root, "launcher.font_weight", value); }
        /// the Windows desktop on a virtual screen between games, the right hand (pinch = click) as the mouse; on by default while chroma-key passthrough is off (the idle frame is the room otherwise), until the user chooses
        public bool MirrorDesktop { get => Json.Get(Root, "display.mirror_desktop") != null ? Json.Bool(Root, "display.mirror_desktop", false) : !Chroma.Enabled(Session.Load()); set => Json.Set(Root, "display.mirror_desktop", value); }
        public bool SplitEncode { get => Json.Str(Root, "video.split_encode", "3") != "off"; set => Json.Set(Root, "video.split_encode", value ? "3" : "off"); }
        public string IdleRgb { get => Json.Str(Root, "video.idle_rgb", Chroma.DefaultRgb); set => Json.Set(Root, "video.idle_rgb", value); }
        /// frame generation (docs/FRAME_GENERATION.md): the game renders every other frame, the streamer interpolates the one in between (off by default)
        public bool FrameGen { get => Json.Bool(Root, "video.frame_gen", false); set => Json.Set(Root, "video.frame_gen", value); }
        // frame generation rate: "third" | "half" (default) | "two_thirds" (the game's rate as a fraction of the display rate, the streamer generates the
        // frames in between; docs/FRAME_GENERATION.md), "uncapped" (experimental, from the Advanced settings; docs/FRAME_GENERATION_UNCAPPED.md)
        public string FrameGenRate { get => Json.Str(Root, "video.frame_gen_rate", "half"); set => Json.Set(Root, "video.frame_gen_rate", value); }
        /// uncapped mode's minimum spacing of the game's releases in fps (0 = none) and its content lag in ms (0 = automatic); the streamer takes a change live
        public double FrameGenCapFps { get => Json.Num(Root, "video.frame_gen_cap_fps", 0); set => Json.Set(Root, "video.frame_gen_cap_fps", value); }
        public double FrameGenLagMs { get => Json.Num(Root, "video.frame_gen_lag_ms", 0); set => Json.Set(Root, "video.frame_gen_lag_ms", value); }
        // docs/FRAME_GENERATION.md "Game profiles": the launcher sets the rate from the game's recorded runs on the connected headset (on by default)
        public bool FrameGenRecommended { get => Json.Bool(Root, "video.frame_gen_recommended", true); set => Json.Set(Root, "video.frame_gen_recommended", value); }
        /// arrival margin of the phase lock in ms (host default 7; the host reads it at start) and the GPU keepalive duty in % (0 = off); 0 = not set
        public double PhaseMarginMs { get => Json.Num(Root, "video.phase_margin_ms", 0); set => Json.Set(Root, "video.phase_margin_ms", value); }
        public int GpuKeepalive { get => (int)Json.Num(Root, "video.gpu_keepalive", 0); set => Json.Set(Root, "video.gpu_keepalive", (double)value); }
        /// the Windows power plan the streamer switches to while it runs (a GUID; "" = leave it alone); the plan that was active comes back when the streamer closes
        public string PowerPlan { get => Json.Str(Root, "system.power_plan", ""); set => Json.Set(Root, "system.power_plan", value); }
        /// the PC's own output is muted while the streamer routes the game audio to the virtual cable (on by default)
        public bool MuteSpeakers { get => Json.Bool(Root, "audio.mute_pc", true); set => Json.Set(Root, "audio.mute_pc", value); }
        /// how the game audio is captured: "process" = Windows' process loopback (no virtual cable, the default output stays as it is), "device" = the VB-CABLE path
        /// (the streamer makes the cable the default output, AuraFlashXR captures it)
        public string AudioCapture { get => Json.Str(Root, "audio.capture", "process") == "device" ? "device" : "process"; set => Json.Set(Root, "audio.capture", value == "device" ? "device" : "process"); }
        public bool Debug { get => Json.Bool(Root, "debug", false); set => Json.Set(Root, "debug", value); }
        /// statistics refresh interval in the streamer's live box (s, 1..10; default 10)
        public int StatsRefreshSecs { get => (int)Json.Num(Root, "stats.refresh_secs", 10); set => Json.Set(Root, "stats.refresh_secs", (double)value); }
        /// display (live in AuraFlashXR.exe): gamma 1.2 prefilled (users' AVP experience with AuraFlashXR), the rest neutral
        public const double DefaultGamma = 1.2;
        public bool PostProcessingEnabled { get => Json.Bool(Root, "display.post_processing", true); set => Json.Set(Root, "display.post_processing", value); }
        /// Sun Glasses mode (inspired by OpenXR Toolkit): 0 = Off, 1 = Light, 2 = Dark, 3 = TruNite
        public int SunGlasses { get => (int)Json.Num(Root, "display.sun_glasses", 0); set => Json.Set(Root, "display.sun_glasses", (double)value); }
        public double Gamma { get => Json.Num(Root, "display.gamma", DefaultGamma); set => Json.Set(Root, "display.gamma", value); }
        public double Brightness { get => Json.Num(Root, "display.brightness", 0); set => Json.Set(Root, "display.brightness", value); }
        public double Contrast { get => Json.Num(Root, "display.contrast", 0); set => Json.Set(Root, "display.contrast", value); }
        public double Saturation { get => Json.Num(Root, "display.saturation", 0); set => Json.Set(Root, "display.saturation", value); }
        public double Sharpening { get => Json.Num(Root, "display.sharpening", 0); set => Json.Set(Root, "display.sharpening", value); }
        public double Exposure { get => Json.Num(Root, "display.exposure", 0); set => Json.Set(Root, "display.exposure", value); }
        public double Vibrance { get => Json.Num(Root, "display.vibrance", 0); set => Json.Set(Root, "display.vibrance", value); }
        public double Highlights { get => Json.Num(Root, "display.highlights", 0); set => Json.Set(Root, "display.highlights", value); }
        public double Shadows { get => Json.Num(Root, "display.shadows", 0); set => Json.Set(Root, "display.shadows", value); }
        public bool HasDisplay => Json.Get(Root, "display.gamma") != null;
        /// headset height above the floor handed to games (m), re-anchored live; 1.3 = a seated adult. 0 = the headset's floor estimate
        public const double DefaultHeight = 1.3;
        public double Height { get => Json.Num(Root, "headset.height_m", DefaultHeight); set => Json.Set(Root, "headset.height_m", value); }
        /// "auto" = the headset's own floor estimate (height 0 to the engine), "manual" = Height. Someone who already set a height stays on manual; a new user starts on auto.
        public string HeightMode
        {
            get { var m = Json.Str(Root, "display.height_mode", ""); return m == "auto" || m == "manual" ? m : (Json.Get(Root, "headset.height_m") == null || Json.Num(Root, "headset.height_m", 0) <= 0 ? "auto" : "manual"); }
            set => Json.Set(Root, "display.height_mode", value);
        }
        public void Save()
        {
            Json.Save(Paths.Settings, Root);
        }

        /// Saves into the ACTIVE preset's settings file (config\<preset>\config\auraflashxr.json), the one the
        /// host reads with --install-dir. Saving only to Paths.Settings left the preset's own file untouched, so
        /// the settings that are not part of session.json came back to their old values when the preset was reloaded.
        public void SaveActivePreset()
        {
            Json.Save(Paths.Settings, Root);
            if (Preset.IsActiveDefault) return;
            var f = System.IO.Path.Combine(Preset.ActiveDir, "config", "auraflashxr.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f));
            Json.Save(f, Root);
        }

        // PyroWave codec (Vulkan compute wavelet, intra-only, packet-loss resilient; MIT, Themaister).
        // Settings live in auraflashxr.json -> video.pyrowave; the host and the Vulkan client read them at start.
        public bool PyrowaveEnabled { get => Json.Bool(Root, "video.pyrowave.enabled", false); set => Json.Set(Root, "video.pyrowave.enabled", value); }
        public string PyrowaveChroma { get => Json.Str(Root, "video.pyrowave.chroma", "420") == "444" ? "444" : "420"; set => Json.Set(Root, "video.pyrowave.chroma", value == "444" ? "444" : "420"); }
        public int PyrowaveMaxKb { get => (int)Json.Num(Root, "video.pyrowave.maximum_bitstream_size_kb", 400); set => Json.Set(Root, "video.pyrowave.maximum_bitstream_size_kb", (double)value); }
        public int PyrowavePacketBytes { get => (int)Json.Num(Root, "video.pyrowave.packet_boundary_bytes", 1200); set => Json.Set(Root, "video.pyrowave.packet_boundary_bytes", (double)value); }
        /// One call for the settings page: writes the four PyroWave fields.
        public void SetPyrowave(bool enabled, string chroma, int maxKb, int packetBytes)
        {
            PyrowaveEnabled = enabled;
            PyrowaveChroma = chroma;
            PyrowaveMaxKb = maxKb;
            PyrowavePacketBytes = packetBytes;
        }
    }

    /// What the hardware encoder can take: HEVC frames of at most 8192 pixels across, the two eyes side by side (4096 per eye), and 8192 high. A larger size makes
    /// the streamer exit at start (2026-10-24: an eye of 4288 pixels), so the settings refuse it with the reason.
    public static class Nvenc
    {
        public const int MaxEyeWidth = 4096, MaxEyeHeight = 8192;
        /// null when the per-eye size fits the encoder, else what to tell the user
        public static string CheckEye(int eyeW, int eyeH, string codec = null)
        {
            return null;
        }
    }

    /// Chroma-key passthrough: the headset turns the idle colour into the room. It is optional and off by default; the key colour lives in session.json (what the AuraFlashXR
    /// client keys), the same colour is what the streamer shows between games. Off: the streamer's idle frame is black, and the desktop mirror is on unless chosen otherwise.
    public static class Chroma
    {
        public const string DefaultRgb = "00ff00";
        const string P = "session_settings.video.passthrough.";
        public static bool Enabled(Dictionary<string, object> s) => Json.Bool(s, P + "enabled", false);
        /// the key colour as "rrggbb"
        public static string Rgb(Dictionary<string, object> s) => $"{(int)Json.Num(s, P + "content.RgbChromaKey.red", 0):x2}{(int)Json.Num(s, P + "content.RgbChromaKey.green", 255):x2}{(int)Json.Num(s, P + "content.RgbChromaKey.blue", 0):x2}";
        /// Sets passthrough on or off in `s` (not saved) and the idle colour in `st` (not saved): the key colour when on, black when off. `rgb` = "rrggbb".
        public static void Apply(Dictionary<string, object> s, AppSettings st, bool on, string rgb)
        {
            rgb = (rgb ?? DefaultRgb).TrimStart('#').ToLowerInvariant();
            if (rgb.Length != 6) rgb = DefaultRgb;
            Json.Set(s, P + "enabled", on);
            Json.Set(s, P + "content.variant", "RgbChromaKey");
            Json.Set(s, P + "content.RgbChromaKey.red", Convert.ToInt32(rgb.Substring(0, 2), 16));
            Json.Set(s, P + "content.RgbChromaKey.green", Convert.ToInt32(rgb.Substring(2, 2), 16));
            Json.Set(s, P + "content.RgbChromaKey.blue", Convert.ToInt32(rgb.Substring(4, 2), 16));
            st.IdleRgb = on ? rgb : "000000";
        }
    }

    /// Windows power plans through powercfg (what startauraflashxr.bat did): the configured plan is set while the streamer runs and the previous one is put back at the end.
    /// The plan to restore is written to logs\power_plan.txt first, so a streamer that crashed is undone at the next start.
    public static class PowerPlan
    {
        public sealed class Plan { public string Guid, Name; public bool Active; }
        static string StateFile => Path.Combine(Paths.Logs, "power_plan.txt");
        static readonly System.Text.RegularExpressions.Regex Line = new System.Text.RegularExpressions.Regex(@"([0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\s+\((.*?)\)\s*(\*)?");
        static string previous;

        static string Powercfg(string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo("powercfg.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.Default });
                var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(5000); return p.ExitCode == 0 ? o : null;
            }
            catch { return null; }
        }

        public static List<Plan> List()
        {
            var res = new List<Plan>();
            foreach (var l in (Powercfg("/list") ?? "").Split('\n'))
            {
                var m = Line.Match(l);
                if (m.Success) res.Add(new Plan { Guid = m.Groups[1].Value.ToLowerInvariant(), Name = m.Groups[2].Value, Active = m.Groups[3].Success });
            }
            return res;
        }

        /// Sets `wanted` (a GUID or a plan name) and remembers the plan that was active; returns the log line.
        public static string Apply(string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted)) return null;
            Restore();   // a leftover from a streamer that did not close cleanly
            var plans = List();
            var want = plans.FirstOrDefault(x => string.Equals(x.Guid, wanted.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, wanted.Trim(), StringComparison.OrdinalIgnoreCase));
            if (want == null) return "power plan \"" + wanted + "\" not found; left as it is";
            var cur = plans.FirstOrDefault(x => x.Active);
            if (cur != null && cur.Guid == want.Guid) return $"power plan {want.Name} is already active";
            if (Powercfg("/setactive " + want.Guid) == null) return $"could not set the power plan {want.Name}";
            previous = cur?.Guid;
            if (previous != null) { try { Directory.CreateDirectory(Paths.Logs); File.WriteAllText(StateFile, previous); } catch { } }
            return $"power plan {want.Name} set (was {cur?.Name ?? "unknown"}, restored when the streamer closes)";
        }

        /// Puts the remembered plan back (this run's, or the one a crashed run left in the file); returns the log line, null when there was nothing to restore.
        public static string Restore()
        {
            var g = previous;
            previous = null;
            try { if (g == null && File.Exists(StateFile)) g = File.ReadAllText(StateFile).Trim(); } catch { }
            try { File.Delete(StateFile); } catch { }
            if (string.IsNullOrEmpty(g)) return null;
            return Powercfg("/setactive " + g) != null ? "power plan restored" : "could not restore the power plan " + g;
        }
    }

    /// The session (AuraFlashXR settings, paired headset). Created from session.default.json on first use.
    /// A button that stays pressed: Checked / CheckedChanged like a check box, the look of one Segmented segment.
    public class ToggleButton : Button
    {
        bool on, hover;
        public event EventHandler CheckedChanged;
        public ToggleButton()
        {
            AutoSize = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Padding = new Padding(10, 4, 10, 4);
            MinimumSize = new Size(56, 28);
            UseVisualStyleBackColor = false;
            BackColor = Color.Transparent;   // le parent peint le fond : les coins arrondis ne gardent aucun carre sombre
            ForeColor = Ui.DarkMuted;
            Font = Ui.SemiBold;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }
        public bool Checked
        {
            get { return on; }
            set { if (on == value) return; on = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Ui.DarkBg);
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            Color fill = !Enabled ? Color.FromArgb(0x13, 0x1A, 0x26) : (on ? Ui.Accent : (hover ? Ui.DarkHover : Color.FromArgb(0x1A, 0x24, 0x36)));
            Color border = !Enabled ? Color.FromArgb(0x20, 0x2B, 0x3D) : (on ? Ui.AccentLight : (hover ? Color.FromArgb(0x3A, 0x50, 0x72) : Ui.DarkBorder));
            Color txt = !Enabled ? Color.FromArgb(0x55, 0x64, 0x78) : (on ? Color.White : (hover ? Ui.DarkText : Ui.DarkMuted));
            using (var path = Ui.RoundRect(rect, 6))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(border, on ? 1.5f : 1f)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, on ? Ui.Bold : Ui.SemiBold, new Rectangle(0, 0, Width, Height), txt, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// A segmented choice: a row of modern pill buttons, one selected (Value = its index). Enabled greys the whole row.
    public class Segmented : FlowLayoutPanel
    {
        readonly List<RadioButton> items = new List<RadioButton>();
        public event EventHandler ValueChanged;
        /// Teinte des pastilles (contour et fond a l'etat actif) ; defaut : accent orange.
        public Color? Tint
        {
            get => items.Count > 0 ? ((SegmentedPill)items[0]).Tint : null;
            set { foreach (var i in items) ((SegmentedPill)i).Tint = value; }
        }
        public Segmented(params string[] texts)
        {
            AutoSize = true; WrapContents = false; Margin = new Padding(0); BackColor = Color.Transparent;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            for (int i = 0; i < texts.Length; i++)
            {
                var r = new SegmentedPill(texts[i], i == 0, i == texts.Length - 1) { AccessibleName = texts[i], Tag = i };
                r.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) ValueChanged?.Invoke(this, EventArgs.Empty); };
                items.Add(r); Controls.Add(r);
            }
        }
        public int Value
        {
            get { for (int i = 0; i < items.Count; i++) if (items[i].Checked) return i; return -1; }
            set { if (value >= 0 && value < items.Count) items[value].Checked = true; }
        }
        sealed class SegmentedPill : RadioButton
        {
            bool hover;
            readonly bool first, last;
            public Color? Tint;
            public SegmentedPill(string text, bool first, bool last)
            {
                this.first = first; this.last = last;
                Text = text;
                Appearance = Appearance.Button;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                AutoSize = true;
                MinimumSize = new Size(48, 28);
                Margin = new Padding(0, 0, 3, 0);
                Padding = new Padding(10, 4, 10, 4);
                TextAlign = ContentAlignment.MiddleCenter;
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
                Font = Ui.SemiBold;
            }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // fond du panneau derriere la pastille (un parent transparent laisserait un rectangle sombre)
                var bg = Parent?.BackColor ?? Ui.DarkCard;
                if (bg.A < 255) bg = Ui.DarkCard;
                g.Clear(bg);
                var rect = new Rectangle(1, 1, Width - 3, Height - 3);
                // meme style que les boutons de profils (ModernButton) : fond sombre leger, contour teinte, texte clair
                Color tint = Tint ?? Ui.AccentLight;
                Color fill = !Enabled ? Color.FromArgb(0x13, 0x1A, 0x26) : (Checked ? Color.FromArgb(45, tint.R, tint.G, tint.B) : (hover ? Color.FromArgb(30, tint.R, tint.G, tint.B) : Color.FromArgb(0x1A, 0x24, 0x36)));
                Color border = !Enabled ? Color.FromArgb(0x20, 0x2B, 0x3D) : (Checked ? tint : (hover ? tint : Color.FromArgb(140, tint.R, tint.G, tint.B)));
                Color txt = !Enabled ? Color.FromArgb(0x55, 0x64, 0x78) : (Checked || hover ? Color.White : Ui.DarkText);
                using (var path = Ui.RoundRect(rect, 8))
                {
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var p = new Pen(border, Checked ? 1.5f : 1f)) g.DrawPath(p, path);
                }
                TextRenderer.DrawText(g, Text, Checked ? Ui.Bold : Ui.SemiBold, new Rectangle(0, 0, Width, Height), txt, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    /// Modern pill dropdown button matching the Segmented / ModernButton bubble styling.
    /// Opens a sleek dark dropdown popup on click.
    public class ModernDropdownButton : Button
    {
        bool hover, menuOpen;
        readonly ContextMenuStrip menu;
        int selectedIndex = -1;
        readonly List<string> items = new List<string>();
        public event EventHandler SelectedIndexChanged;
        public event EventHandler ValueChanged { add { SelectedIndexChanged += value; } remove { SelectedIndexChanged -= value; } }

        public ModernDropdownButton()
        {
            AutoSize = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            MinimumSize = new Size(115, 28);
            Padding = new Padding(12, 4, 12, 4);
            BackColor = Color.Transparent;   // le parent peint le fond : les coins arrondis ne gardent aucun carre sombre
            ForeColor = Ui.DarkText;
            Font = Ui.SemiBold;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                BackColor = Color.FromArgb(0x14, 0x1C, 0x2B),
                ForeColor = Ui.DarkText,
                Font = Ui.SemiBold,
                DropShadowEnabled = true,
                Renderer = new ModernMenuRenderer()
            };
            menu.Closed += (s, e) => { menuOpen = false; Invalidate(); };
        }

        public void SetItems(IEnumerable<string> newItems, int initialIndex = 0)
        {
            items.Clear();
            menu.Items.Clear();
            if (newItems != null)
            {
                foreach (var item in newItems)
                {
                    items.Add(item);
                    var capturedIdx = items.Count - 1;
                    var mi = new ToolStripMenuItem(item)
                    {
                        AutoSize = true,
                        Padding = new Padding(14, 6, 18, 6),
                        ForeColor = Ui.DarkText,
                        Font = Ui.SemiBold
                    };
                    mi.Click += (s, e) => SelectedIndex = capturedIdx;
                    menu.Items.Add(mi);
                }
            }
            SelectedIndex = (initialIndex >= 0 && initialIndex < items.Count) ? initialIndex : (items.Count > 0 ? 0 : -1);
        }

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (value >= 0 && value < items.Count)
                {
                    selectedIndex = value;
                    Text = items[value] + "  \u25BE";
                    Invalidate();
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int Value
        {
            get => SelectedIndex;
            set => SelectedIndex = value;
        }

        public string SelectedItem => (selectedIndex >= 0 && selectedIndex < items.Count) ? items[selectedIndex] : "";

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (menu.Items.Count > 0)
            {
                menuOpen = true;
                Invalidate();
                menu.Show(this, new Point(0, Height + 2));
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Ui.DarkBg);
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);

            Color fill = !Enabled ? Color.FromArgb(0x13, 0x1A, 0x26) : ((hover || menuOpen) ? Ui.DarkHover : Color.FromArgb(0x1A, 0x24, 0x36));
            Color border = !Enabled ? Color.FromArgb(0x20, 0x2B, 0x3D) : ((hover || menuOpen) ? Ui.AccentLight : Ui.DarkBorder);
            Color txt = !Enabled ? Color.FromArgb(0x55, 0x64, 0x78) : ((hover || menuOpen) ? Color.White : Ui.DarkText);

            using (var path = Ui.RoundRect(rect, 6))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(border, (hover || menuOpen) ? 1.5f : 1f)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, (hover || menuOpen) ? Ui.Bold : Ui.SemiBold, new Rectangle(0, 0, Width, Height), txt, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernMenuRenderer() : base(new ModernMenuColors()) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected)
                {
                    var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                    using (var path = Ui.RoundRect(rect, 4))
                    using (var b = new SolidBrush(Ui.Accent))
                        e.Graphics.FillPath(b, path);
                }
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(0x14, 0x1C, 0x2B)))
                        e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
                }
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var p = new Pen(Ui.DarkBorder, 1.5f))
                    e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        sealed class ModernMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(0x14, 0x1C, 0x2B);
            public override Color MenuBorder => Ui.DarkBorder;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => Ui.Accent;
        }
    }

    /// Modern custom-painted button with rounded corners, hover transition, and optional active/accent tint.
    public class ModernButton : Button
    {
        bool hover, pressed;
        Color? customAccent;
        bool isActiveState;
        public Color? AccentTint { get => customAccent; set { customAccent = value; Invalidate(); } }
        public bool ActiveState { get => isActiveState; set { isActiveState = value; Invalidate(); } }
        /// Dessin « verre » : pas de bordure, fond translucide arrondi laissant voir le parent.
        public bool Glass { get; set; }
        public ModernButton()
        {
            AutoSize = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            MinimumSize = new Size(76, 30);
            Padding = new Padding(12, 4, 12, 4);
            // SetStyle AVANT BackColor : sans SupportsTransparentBackColor, Affecter Transparent leve une exception
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;   // le parent peint le fond : les coins arrondis ne gardent aucun carre sombre
            ForeColor = Ui.DarkText;
            Font = Ui.SemiBold;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            DoubleBuffered = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { pressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { pressed = false; Invalidate(); base.OnMouseUp(mevent); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // fond réel du parent (un parent transparent donnerait du noir dans les coins arrondis)
            var parentBg = Parent?.BackColor ?? Ui.DarkBg;
            if (parentBg.A < 255) parentBg = Ui.DarkCard;
            g.Clear(parentBg);
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            Color baseFill = Color.FromArgb(0x1C, 0x26, 0x38);
            Color baseBorder = Color.FromArgb(0x2D, 0x3E, 0x58);
            Color txt = !Enabled ? Color.FromArgb(0x55, 0x64, 0x78) : ForeColor;

            if (isActiveState && customAccent.HasValue)
            {
                var ac = customAccent.Value;
                baseFill = Color.FromArgb(hover ? 65 : 45, ac.R, ac.G, ac.B);
                baseBorder = ac;
                txt = Color.White;
            }
            else if (customAccent.HasValue)
            {
                var ac = customAccent.Value;
                baseFill = hover ? Color.FromArgb(45, ac.R, ac.G, ac.B) : Color.FromArgb(24, ac.R, ac.G, ac.B);
                baseBorder = hover ? ac : Color.FromArgb(140, ac.R, ac.G, ac.B);
            }
            else if (pressed)
            {
                baseFill = Ui.Accent;
                baseBorder = Ui.AccentLight;
                txt = Color.White;
            }
            else if (hover && Enabled)
            {
                baseFill = Color.FromArgb(0x25, 0x34, 0x4D);
                baseBorder = Ui.AccentLight;
                txt = Color.White;
            }

            if (Glass && !pressed && !isActiveState && !customAccent.HasValue)
            {
                baseFill = hover ? Color.FromArgb(70, 0xFF, 0xFF, 0xFF) : Color.FromArgb(28, 0xFF, 0xFF, 0xFF);
                baseBorder = Color.Transparent;
            }
            using (var path = Ui.RoundRect(rect, 8))
            {
                using (var b = new SolidBrush(baseFill)) g.FillPath(b, path);
                if (baseBorder.A > 0)
                    using (var p = new Pen(baseBorder, (isActiveState || hover) ? 1.4f : 1f)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, isActiveState ? Ui.Bold : Font, new Rectangle(0, 0, Width, Height), txt, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    /// Modern dark card replacing legacy 3D Win32 GroupBox: rounded border, subtle dark card background, and an accent bar next to the section title.
    public class ModernGroupBox : GroupBox
    {
        /// Optional control drawn at the right end of the title bar (a small dropdown): the title stops before it.
        public Control HeaderControl { get; set; }
        public ModernGroupBox()
        {
            BackColor = Ui.DarkCard;
            ForeColor = Ui.DarkText;
            Font = Ui.SectionTitle;
            Padding = new Padding(12, 28, 12, 10);
            DoubleBuffered = true;
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            var hc = HeaderControl;
            if (hc != null)
            {
                hc.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                hc.Location = new Point(Width - 14 - hc.Width, 6);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Ui.DarkBg);
            var rect = new Rectangle(1, 2, Width - 3, Height - 4);
            using (var path = Ui.RoundRect(rect, 8))
            {
                using (var b = new SolidBrush(Ui.DarkCard)) g.FillPath(b, path);
                // contour 1 px en degrade orange -> jaune -> rouge
                using (var bp = new System.Drawing.Drawing2D.LinearGradientBrush(rect, Color.FromArgb(0xFF, 0x7A, 0x00), Color.FromArgb(0xE8, 0x44, 0x2A), System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                using (var p = new Pen(bp, 1f)) g.DrawPath(p, path);
            }
            // Left accent pill + section title
            using (var accentBrush = new SolidBrush(Ui.AccentLight))
            using (var pillPath = Ui.RoundRect(new Rectangle(12, 10, 4, 15), 2))
                g.FillPath(accentBrush, pillPath);

            int titleRight = HeaderControl != null ? HeaderControl.Left - 10 : Width - 14;
            TextRenderer.DrawText(g, Text, Ui.SectionTitle, new Rectangle(22, 8, Math.Max(0, titleRight - 22), 18), Color.FromArgb(0xE6, 0xF0, 0xFF), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            using (var sep = new Pen(Color.FromArgb(0x20, 0x2D, 0x42), 1f))
                g.DrawLine(sep, 12, 30, Width - 14, 30);
        }
    }

    /// Modern custom-painted slider with a filled accent bar, center tick mark for bipolar sliders (-50..50), and glowing thumb.
    /// Drop-in replacement for TrackBar so all existing .Minimum, .Maximum, .Value, .ValueChanged code works unchanged.
    public class ModernSlider : TrackBar
    {
        bool hover, dragging;
        public ModernSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TickStyle = TickStyle.None;
            AutoSize = false;
            Height = 26;
            Width = 165;
            Cursor = Cursors.Hand;
            BackColor = Ui.DarkCard;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                SetFromMouse(e.X);
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left) SetFromMouse(e.X);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Invalidate();
            base.OnMouseUp(e);
        }
        void SetFromMouse(int x)
        {
            int pad = 10;
            int trackW = Math.Max(1, Width - pad * 2);
            double ratio = Math.Max(0.0, Math.Min(1.0, (double)(x - pad) / trackW));
            int v = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
            if (Value != v) Value = Math.Max(Minimum, Math.Min(Maximum, v));
            Invalidate();
        }
        protected override void OnValueChanged(EventArgs e) { Invalidate(); base.OnValueChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Ui.DarkCard);

            int pad = 10;
            int trackW = Math.Max(1, Width - pad * 2);
            int cy = Height / 2;
            int trackH = 5;

            // Background track
            var trackRect = new Rectangle(pad, cy - trackH / 2, trackW, trackH);
            using (var path = Ui.RoundRect(trackRect, 3))
            using (var b = new SolidBrush(Enabled ? Color.FromArgb(0x0D, 0x13, 0x1E) : Color.FromArgb(0x12, 0x18, 0x24)))
            {
                g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(0x26, 0x35, 0x4D), 1f)) g.DrawPath(p, path);
            }

            double range = Math.Max(1, Maximum - Minimum);
            double ratio = (Value - Minimum) / range;
            int thumbX = pad + (int)Math.Round(ratio * trackW);

            // Bipolar slider (-50..50) fills from 0 center; unipolar fills from left
            int originX = (Minimum < 0 && Maximum > 0) ? (pad + (int)Math.Round((0.0 - Minimum) / range * trackW)) : pad;
            int fillLeft = Math.Min(originX, thumbX);
            int fillW = Math.Abs(thumbX - originX);
            if (fillW > 1)
            {
                var fillRect = new Rectangle(fillLeft, cy - trackH / 2, fillW, trackH);
                using (var b = new SolidBrush(Enabled ? Ui.AccentLight : Color.FromArgb(0x3A, 0x50, 0x70)))
                using (var path = Ui.RoundRect(fillRect, 2))
                    g.FillPath(b, path);
            }

            // Center tick for bipolar sliders
            if (Minimum < 0 && Maximum > 0)
            {
                using (var p = new Pen(Color.FromArgb(0x45, 0x5B, 0x7A), 1f))
                    g.DrawLine(p, originX, cy - 5, originX, cy + 5);
            }

            // Thumb circle
            int r = (hover || dragging) ? 7 : 6;
            var thumbRect = new Rectangle(thumbX - r, cy - r, r * 2, r * 2);
            if (Enabled && (hover || dragging))
            {
                using (var glow = new SolidBrush(Color.FromArgb(55, Ui.AccentLight.R, Ui.AccentLight.G, Ui.AccentLight.B)))
                    g.FillEllipse(glow, thumbX - r - 3, cy - r - 3, (r + 3) * 2, (r + 3) * 2);
            }
            using (var b = new SolidBrush(Enabled ? (hover || dragging ? Color.White : Ui.AccentLight) : Color.FromArgb(0x55, 0x64, 0x78)))
                g.FillEllipse(b, thumbRect);
            if (Enabled)
            {
                using (var p = new Pen(hover || dragging ? Ui.AccentLight : Color.White, 1.5f))
                    g.DrawEllipse(p, thumbRect);
            }
        }
    }

    /// Brings a working AuraFlashXR session.json into config\: the user points at their old AuraFlashXR folder (or the file), we look for session.json around it, check it is an AuraFlashXR session,
    /// keep a backup of the current one and copy it over.
    public static class AuraflashxrImport
    {
        public static string Guess()
        {
            foreach (var d in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\AuraFlashXR", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\AuraFlashXR",
                                      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\AuraFlashXR", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads" })
                if (Directory.Exists(d)) return d;
            return "";
        }

        /// session.json files found in `path` (a file or a folder): the folder itself, config\, its parent, then two levels down.
        public static List<string> Find(string path)
        {
            var r = new List<string>();
            if (File.Exists(path)) { r.Add(path); return r; }
            if (!Directory.Exists(path)) return r;
            foreach (var c in new[] { Path.Combine(path, "session.json"), Path.Combine(path, "config", "session.json"), Path.Combine(path, "..", "session.json") })
                if (File.Exists(c)) r.Add(Path.GetFullPath(c));
            if (r.Count == 0)
                try
                {
                    foreach (var d1 in Directory.GetDirectories(path))
                    {
                        var f1 = Path.Combine(d1, "session.json"); if (File.Exists(f1)) r.Add(f1);
                        foreach (var d2 in Directory.GetDirectories(d1)) { var f2 = Path.Combine(d2, "session.json"); if (File.Exists(f2)) r.Add(f2); }
                    }
                }
                catch { }   // an unreadable folder: what was found so far stands
            return r.Distinct(StringComparer.OrdinalIgnoreCase).Where(f => !f.StartsWith(Paths.Config, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static string Check(string file)
        {
            var d = Json.Load(file);
            return d.ContainsKey("session_settings") ? null : "That file is not an AuraFlashXR session (no session_settings in it).";
        }

        /// Backs up config\session.json and replaces it. Returns the backup path, or null with `error`.
        public static string Import(string file, out string error)
        {
            error = Check(file);
            if (error != null) return null;
            try
            {
                Directory.CreateDirectory(Paths.Config);
                string bak = null;
                if (File.Exists(Paths.Session)) { bak = Paths.Session + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); File.Copy(Paths.Session, bak, true); }
                File.Copy(file, Paths.Session, true);
                return bak ?? "";
            }
            catch (Exception e) { error = "Cannot import: " + e.Message; return null; }
        }

        /// The whole dialog flow: ask for the AuraFlashXR folder, pick the file, confirm. True when config\session.json was replaced.
        public static bool Run(IWin32Window owner)
        {
            string pick;
            using (var dlg = new FolderBrowserDialog { Description = "Choose your AuraFlashXR folder (where AuraFlashXR is installed, or any folder holding the session.json that works for you).", ShowNewFolderButton = false })
            {
                var g = Guess(); if (g.Length > 0) dlg.SelectedPath = g;
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                pick = dlg.SelectedPath;
            }
            var found = Find(pick);
            if (found.Count == 0)
            {
                MessageBox.Show(owner, "No session.json found in that folder or just below it.\nIf you know the file, choose it directly in the next window.", "Import AuraFlashXR settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                using (var f = new OpenFileDialog { Title = "Choose session.json", Filter = "session.json|session.json|JSON (*.json)|*.json", CheckFileExists = true })
                {
                    if (f.ShowDialog(owner) != DialogResult.OK) return false;
                    found.Add(f.FileName);
                }
            }
            var file = found[0];
            if (found.Count > 1)
            {
                var lines = string.Join("\n", found.Select((x, i) => (i + 1) + ". " + x + "  (" + File.GetLastWriteTime(x).ToString("yyyy-MM-dd HH:mm") + ")"));
                if (MessageBox.Show(owner, "Several session.json files were found:\n\n" + lines + "\n\nUse the most recent one?\n" + found.OrderByDescending(File.GetLastWriteTime).First(), "Import AuraFlashXR settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
                file = found.OrderByDescending(File.GetLastWriteTime).First();
            }
            if (MessageBox.Show(owner, "Import these AuraFlashXR settings?\n\n" + file + "\n\nYour current config\\session.json is kept as a backup next to it.", "Import AuraFlashXR settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
            var bak = Import(file, out var err);
            if (bak == null) { MessageBox.Show(owner, err, "Import AuraFlashXR settings", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            Log.Write("INFO", "imported AuraFlashXR session " + file + (bak.Length > 0 ? ", backup " + bak : ""));
            return true;
        }
    }

    public static class Session
    {
        public static Dictionary<string, object> Load()
        {
            // lire la session du PRESET ACTIF (coherent avec le host, qui recoit
            // --install-dir du preset) au lieu de la config de base.
            var sessionFile = Preset.IsActiveDefault ? Paths.Session : Path.Combine(Preset.ActiveDir, "config", "session.json");
            if (!File.Exists(sessionFile))
            {
                // preset sans session (rare): retomber sur la base, jamais echouer
                sessionFile = Paths.Session;
                if (!File.Exists(sessionFile) && File.Exists(Paths.SessionDefault)) File.Copy(Paths.SessionDefault, sessionFile);
            }
            return Json.Load(sessionFile);
        }

        /// The session file of the ACTIVE preset: base config\session.json for MON PRESET,
        /// config\<preset>\config\session.json otherwise — the file the host actually reads.
        public static string FilePath()
        {
            return Preset.IsActiveDefault ? Paths.Session : Path.Combine(Preset.ActiveDir, "config", "session.json");
        }

        /// Saves the session into the active preset's file. All GUI writes go through this,
        /// so what configure/launcher/wizard save is what the host (--install-dir) reads back.
        public static void Save(Dictionary<string, object> s)
        {
            var f = FilePath();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f));
            Json.Save(f, s);
        }

        /// (hostname, display name, ip) of the trusted headset, or null.
        public static Tuple<string, string, string> Headset(Dictionary<string, object> s)
        {
            if (!(Json.Get(s, "client_connections") is Dictionary<string, object> cc)) return null;
            foreach (var kv in cc)
            {
                if (!(kv.Value is Dictionary<string, object> c) || !(c.TryGetValue("trusted", out var t) && t is bool tb && tb)) continue;
                var ip = c.TryGetValue("current_ip", out var cur) && cur is string cs && cs.Length > 0 ? cs
                       : (c.TryGetValue("manual_ips", out var m) && m is object[] a && a.Length > 0 ? a[0] as string : "");
                return Tuple.Create(kv.Key, c.TryGetValue("display_name", out var n) ? n as string ?? kv.Key : kv.Key, ip ?? "");
            }
            return null;
        }

        /// Pairs exactly one headset: trusted, dialled at `ip` (AuraFlashXR still re-finds it by name if its address changes).
        public static void Pair(Dictionary<string, object> s, string hostname, string name, string ip)
        {
            s["client_connections"] = new Dictionary<string, object>
            {
                [hostname] = new Dictionary<string, object>
                {
                    ["display_name"] = name, ["current_ip"] = ip, ["manual_ips"] = new object[] { ip }, ["trusted"] = true,
                    ["connection_state"] = "Disconnected",
                },
            };
        }
    }

    /// Configuration presets. "MON PRESET" is the base config\ folder the user edits in configure.exe; every other preset is a
    /// subfolder of config\ laid out as a mini-install (config\<name>\config\*.json + config\<name>\logs\). Loading a preset NEVER writes
    /// to the base config\: the host is started with --install-dir "<preset folder>", so it reads the preset's own .json files and keeps
    /// its logs in the preset's logs\ folder. The active preset is remembered in config\preset.cfg (a single line with the folder name,
    /// empty or "MON PRESET" = base config).
    public static class Preset
    {
        public const string DefaultName = "MON PRESET";

        static string MarkerFile => Path.Combine(Paths.Config, "preset.cfg");

        /// The folder of the active preset: Paths.Config itself for MON PRESET, config\<name> otherwise.
        public static string ActiveDir => IsActiveDefault ? Paths.Config : Path.Combine(Paths.Config, Active);

        /// The --install-dir value to give the host: the package root for MON PRESET, the preset's folder otherwise.
        public static string HostInstallDir => IsActiveDefault ? Paths.Dir : Path.Combine(Paths.Config, Active);

        /// True when the active preset is MON PRESET (base config\, the one configure.exe edits).
        public static bool IsActiveDefault => string.IsNullOrWhiteSpace(Active);

        /// Extracts the real preset folder name from a display item. Display items are built as
        /// "<folder>  \u00B7  <codec>  \u00B7  <gpu>" - this strips everything from the first "  \u00B7  " onward.
        /// Also trims and collapses whitespace so the result is a valid folder name.
        public static string CleanName(string displayOrFolder)
        {
            if (string.IsNullOrWhiteSpace(displayOrFolder)) return "";
            var v = displayOrFolder.Trim();
            // Strip display suffix: everything from "  \u00B7  " (double-space middle-dot double-space)
            int dotIdx = v.IndexOf("  \u00B7  ", StringComparison.Ordinal);
            if (dotIdx >= 0) v = v.Substring(0, dotIdx).TrimEnd();
            // Also handle single-space variants just in case
            dotIdx = v.IndexOf(" \u00B7 ", StringComparison.Ordinal);
            if (dotIdx >= 0) v = v.Substring(0, dotIdx).TrimEnd();
            // Collapse multiple consecutive spaces into one
            while (v.Contains("  ")) v = v.Replace("  ", " ");
            return v.Trim();
        }

        /// The active preset's folder name ("" or "MON PRESET" = the base config).
        public static string Active
        {
            get
            {
                try
                {
                    if (!File.Exists(MarkerFile)) return "";
                    var v = File.ReadAllText(MarkerFile).Trim();
                    return v.Length == 0 || v.Equals(DefaultName, StringComparison.OrdinalIgnoreCase) ? "" : v;
                }
                catch { return ""; }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(Paths.Config);
                    // clean the value - strip display suffix and sanitize for filesystem use
                    var raw = CleanName(value);
                    var v = string.IsNullOrWhiteSpace(raw) || raw.Equals(DefaultName, StringComparison.OrdinalIgnoreCase) ? "" : raw;
                    File.WriteAllText(MarkerFile, v, new UTF8Encoding(false));
                }
                catch (Exception e) { Log.Write("WARN", "cannot save the selected preset: " + e.Message); }
            }
        }

        /// Every preset the launcher's dropdown offers: MON PRESET first, then each config\ subfolder that holds a config\session.json.
        public static List<string> List()
        {
            var res = new List<string> { DefaultName };
            try
            {
                if (Directory.Exists(Paths.Config))
                    foreach (var d in Directory.GetDirectories(Paths.Config))
                    {
                        var name = Path.GetFileName(d);
                        if (name.Equals("games", StringComparison.OrdinalIgnoreCase)) continue;   // per-game records, not a preset
                        if (!File.Exists(Path.Combine(d, "config", "session.json"))) continue;    // not a preset: no session to load
                        res.Add(name);
                    }
            }
            catch { }
            return res;
        }

        /// The encoder families, one built-in preset each (OPTI H.264 / OPTI HEVC / OPTI AV1), in menu order.
        public static readonly string[] CodecFamilies = { "H.264", "HEVC", "AV1" };

        /// The encoder family a preset name belongs to ("LOW HEVC 10-bit" -> "HEVC 10-bit"), or null when the preset
        /// is not one of the per-encoder ones (MON PRESET, user presets).
        public static string CodecFamilyOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var v = CleanName(name).ToUpperInvariant();
            // longest family first: "HEVC 10-BIT" must win over "HEVC"
            for (int i = CodecFamilies.Length - 1; i >= 0; i--)
                if (v.EndsWith(CodecFamilies[i].ToUpperInvariant(), StringComparison.Ordinal)) return CodecFamilies[i];
            return null;
        }

        /// The quality tier of a per-encoder preset ("OPTI HEVC" -> "OPTI"), or null.
        public static string CodecTierOf(string name)
        {
            if (CodecFamilyOf(name) == null) return null;
            var v = CleanName(name).ToUpperInvariant();
            foreach (var tier in new[] { "OPTI" })
                if (v.StartsWith(tier + " ", StringComparison.Ordinal)) return tier;
            return null;
        }

        /// Copies the trusted-headset list from the base session.json into the preset's own session.json, so a headset paired in
        /// MON PRESET connects without re-pairing. Only the preset's file is written; the base config is never touched.
        public static void SyncPairing(string presetName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(presetName)) return;
                var dest = Path.Combine(Paths.Config, presetName, "config", "session.json");
                if (!File.Exists(dest)) return;
                var cc = Json.Get(Session.Load(), "client_connections");
                if (cc == null) return;
                var ps = Json.Load(dest);
                ps["client_connections"] = cc;
                Json.Save(dest, ps);
                Log.Write("INFO", "preset '" + presetName + "': headset pairing synced from the base config");
            }
            catch (Exception e) { Log.Write("WARN", "preset pairing sync failed: " + e.Message); }
        }

        /// Creates config\<name>\config\ with a snapshot of the base session.json, session.default.json, auraflashxr.json and
        /// auraflashxr_views.json. Returns null on success, an error message otherwise.
        public static string Save(string name) => Save(name, Paths.Config);

        /// <summary>
        /// Creates config\<name>\config\ with a snapshot of the .json files from sourceDir.
        /// Used when a read-only built-in preset is loaded: the preset copies
        /// the read-only profile's files so the user gets an editable copy under a new name.
        /// Returns null on success, an error code otherwise ("empty", "reserved", "exists", or exception message).
        /// </summary>
        public static string Save(string name, string sourceDir)
        {
            try
            {
                // use CleanName to strip display suffixes, then sanitize for filesystem
                var clean = CleanName(name ?? "");
                if (clean.Length == 0) return "empty";
                foreach (var c in Path.GetInvalidFileNameChars()) clean = clean.Replace(c, '_');
                // Replace common display-only characters that are valid in filenames but problematic
                clean = clean.Replace('\u00B7', '-').Replace('\u2014', '-').Replace('\u2013', '-'); // middle dot, em dash, en dash
                while (clean.Contains("--")) clean = clean.Replace("--", "-"); // collapse double dashes
                clean = clean.Trim(' ', '-', '_'); // trim separators from edges
                if (clean.Length == 0) return "empty";
                if (clean.Equals(DefaultName, StringComparison.OrdinalIgnoreCase)) return "reserved";
                var dir = Path.Combine(Paths.Config, clean, "config");
                if (Directory.Exists(dir)) return "exists";
                Directory.CreateDirectory(dir);
                foreach (var f in new[] { "session.json", "session.default.json", "auraflashxr.json", "auraflashxr_views.json" })
                {
                    var src = Path.Combine(sourceDir, f);
                    if (File.Exists(src)) File.Copy(src, Path.Combine(dir, f));
                }
                Log.Write("INFO", "preset '" + clean + "' saved from " + sourceDir);
                return null;
            }
            catch (Exception e) { Log.Write("ERROR", "preset save failed: " + e.Message); return e.Message; }
        }
        
        /// <summary>
        /// Overwrites an existing preset (config\<name>\config\) with a fresh snapshot of the base config files.
        /// Clears read-only attributes before deleting, then recreates the folder and copies the current base .json files.
        /// Returns null on success, an error code otherwise ("empty", "reserved", "notfound", or exception message).
        /// </summary>
        public static string Overwrite(string name)
        {
            try
            {
                // use CleanName to strip display suffixes, then sanitize for filesystem
                var clean = CleanName(name ?? "");
                if (clean.Length == 0) return "empty";
                foreach (var c in Path.GetInvalidFileNameChars()) clean = clean.Replace(c, '_');
                // Replace common display-only characters that are valid in filenames but problematic
                clean = clean.Replace('\u00B7', '-').Replace('\u2014', '-').Replace('\u2013', '-'); // middle dot, em dash, en dash
                while (clean.Contains("--")) clean = clean.Replace("--", "-"); // collapse double dashes
                clean = clean.Trim(' ', '-', '_'); // trim separators from edges
                if (clean.Length == 0) return "empty";
                if (clean.Equals(DefaultName, StringComparison.OrdinalIgnoreCase)) return "reserved";
                
                var presetDir = Path.Combine(Paths.Config, clean);
                var dir = Path.Combine(presetDir, "config");
                if (!Directory.Exists(dir)) return "notfound";
                
                // Clear read-only attributes on all files before deleting (in case the folder was set read-only)
                foreach (var f in Directory.GetFiles(presetDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.IsReadOnly) fi.IsReadOnly = false;
                    }
                    catch { /* ignore individual file errors */ }
                }
                // Also clear read-only on directories
                foreach (var d in Directory.GetDirectories(presetDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var di = new DirectoryInfo(d);
                        if ((di.Attributes & FileAttributes.ReadOnly) != 0)
                            di.Attributes &= ~FileAttributes.ReadOnly;
                    }
                    catch { /* ignore */ }
                }
                
                // Delete and recreate the preset folder
                Directory.Delete(presetDir, true);
                Directory.CreateDirectory(dir);
                
                // Copy current base config files
                foreach (var f in new[] { "session.json", "session.default.json", "auraflashxr.json", "auraflashxr_views.json" })
                {
                    var src = Path.Combine(Paths.Config, f);
                    if (File.Exists(src)) File.Copy(src, Path.Combine(dir, f));
                }
                
                Log.Write("INFO", "preset '" + clean + "' overwritten from the current configuration");
                return null;
            }
            catch (Exception e) { Log.Write("ERROR", "preset overwrite failed: " + e.Message); return e.Message; }
        }
        /// <summary>
        /// True for the presets shipped with the app: the per-encoder ones (LOW/MID/HIGH/OPTI x H.264 / HEVC /
        /// HEVC 10-bit / AV1) and the legacy LOW/MID/HIGH/OPTI CONFIG names.
        /// </summary>
        public static bool IsBuiltInPreset(string name)
        {
            if (CodecFamilyOf(name) != null) return true;
            var v = CleanName(name ?? "").ToUpperInvariant();
            return v == "LOW CONFIG" || v == "MID CONFIG" || v == "HIGH CONFIG" || v == "OPTI CONFIG";
        }

        /// <summary>
        /// returns true when the preset can be deleted by the user (not MON PRESET, not a built-in profile).
        /// Built-in profiles are: the per-encoder presets, LOW/MID/HIGH/OPTI CONFIG and DEFAULT (USINE).
        /// </summary>
        public static bool Deletable(string name)
        {
            var clean = CleanName(name ?? "");
            if (clean.Length == 0) return false;
            if (clean.Equals(DefaultName, StringComparison.OrdinalIgnoreCase)) return false;
            if (IsBuiltInPreset(clean)) return false;
            if (clean.Equals("DEFAULT (USINE)", StringComparison.OrdinalIgnoreCase)) return false;
            var dir = Path.Combine(Paths.Config, clean);
            return Directory.Exists(Path.Combine(dir, "config"));
        }

        /// <summary>
        /// Deletes a user preset (config\<name>\ recursively). Refuses MON PRESET and built-in profiles.
        /// If the deleted preset was the active one, preset.cfg is reset to MON PRESET (base config).
        /// Returns null on success, an error code otherwise ("empty", "reserved", "notfound", or exception message).
        /// </summary>
        public static string Delete(string name)
        {
            try
            {
                var clean = CleanName(name ?? "");
                if (clean.Length == 0) return "empty";
                foreach (var c in Path.GetInvalidFileNameChars()) clean = clean.Replace(c, '_');
                clean = clean.Replace('\u00B7', '-').Replace('\u2014', '-').Replace('\u2013', '-');
                while (clean.Contains("--")) clean = clean.Replace("--", "-");
                clean = clean.Trim(' ', '-', '_');
                if (clean.Length == 0) return "empty";
                if (clean.Equals(DefaultName, StringComparison.OrdinalIgnoreCase)) return "reserved";
                if (IsBuiltInPreset(clean)) return "reserved";
                if (clean.Equals("DEFAULT (USINE)", StringComparison.OrdinalIgnoreCase)) return "reserved";

                var presetDir = Path.Combine(Paths.Config, clean);
                if (!Directory.Exists(presetDir)) return "notfound";

                // Clear read-only attributes before deleting
                foreach (var f in Directory.GetFiles(presetDir, "*", SearchOption.AllDirectories))
                {
                    try { var fi = new FileInfo(f); if (fi.IsReadOnly) fi.IsReadOnly = false; } catch { }
                }
                foreach (var d in Directory.GetDirectories(presetDir, "*", SearchOption.AllDirectories))
                {
                    try { var di = new DirectoryInfo(d); if ((di.Attributes & FileAttributes.ReadOnly) != 0) di.Attributes &= ~FileAttributes.ReadOnly; } catch { }
                }

                Directory.Delete(presetDir, true);
                Log.Write("INFO", "preset '" + clean + "' deleted");

                // If the deleted preset was active, reset to MON PRESET (base config)
                if (!IsActiveDefault && Active.Equals(clean, StringComparison.OrdinalIgnoreCase))
                {
                    Active = "";
                    Log.Write("INFO", "active preset was deleted, reset to MON PRESET");
                }
                return null;
            }
            catch (Exception e) { Log.Write("ERROR", "preset delete failed: " + e.Message); return e.Message; }
        }
    }

    /// "Meta Quest over USB": pure UI. A Quest on a USB cable is not on the network; ADBForwarder (AuraFlashXR's console app) forwards the AuraFlashXR ports to it
    /// through adb, and the Quest then reaches the PC as 127.0.0.1 (nothing is broadcast, so it has to be entered by address). Nothing here touches adb.
    public static class QuestUsb
    {
        public const string Address = "127.0.0.1";
        public const string DownloadUrl = "https://github.com/auraflashxr-org/ADBForwarder";

        /// A small dialog with the steps and the download link; true when the user chose to use 127.0.0.1 as the headset address.
        /// The "Meta Quest on a USB cable?" link for the bottom of a headset page: hidden, shown by `Arm` when nothing was found after a few seconds.
        public static LinkLabel Hint(Action onClick)
        {
            var l = new LinkLabel { Text = "Meta Quest on a USB cable?", AutoSize = true, Font = Ui.Body, Margin = new Padding(3, 8, 3, 3), Visible = false };
            l.LinkClicked += (s, e) => onClick();
            return l;
        }

        /// Shows `hint` after `seconds` unless `seen()` says a headset was found by then.
        public static void Arm(LinkLabel hint, Func<bool> seen, int seconds = 8)
        {
            hint.Visible = false;
            var t = new System.Windows.Forms.Timer { Interval = seconds * 1000 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (!hint.IsDisposed) hint.Visible = !seen(); };
            t.Start();
        }

        public static bool Ask(IWin32Window owner)
        {
            using (var f = new DpiForm())
            {
                f.Text = "AuraFlashXR par Pre-Release \u2013 Meta Quest over USB";
                f.Icon = Ui.AppIcon();
                f.AutoScaleDimensions = new SizeF(96F, 96F);
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.Font = Ui.Body;
                f.BackColor = Ui.DarkBg;
                f.ForeColor = Ui.DarkText;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.AutoSize = true;
                f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(12), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
                var title = Ui.Line("Meta Quest over a USB cable", Ui.Big);
                title.ForeColor = Ui.Accent;
                p.Controls.Add(title);
                p.Controls.Add(Ui.Line("A Quest on a USB cable is not on the network, so it is not found by itself. Two ways:"));
                p.Controls.Add(Ui.Line("1. USB tethering (Quest: Settings, Network). The headset then has a network address like any other: use Search again."));
                p.Controls.Add(Ui.Line("2. ADBForwarder (a small program from the AuraFlashXR project): download it, run it with the Quest plugged in and Developer Mode on, " +
                                       "open the AuraFlashXR app on the headset, then use " + Address + " as the headset address (button below)."));
                var link = new LinkLabel { Text = DownloadUrl, AutoSize = true, Font = Ui.Body, Margin = new Padding(3) };
                link.LinkClicked += (s, e) => { try { System.Diagnostics.Process.Start(DownloadUrl); } catch { } };
                p.Controls.Add(link);
                var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
                var cancel = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
                var use = new Button { Text = "Use " + Address + " as the headset address", AutoSize = true, Font = Ui.Bold, DialogResult = DialogResult.OK };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(use);
                p.Controls.Add(buttons);
                f.Controls.Add(p);
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }

    /// Headset discovery, done by auraflashxr_host.exe (`--discover`, AuraFlashXR's own code: mDNS/Bonjour for the Vision Pro, which never
    /// broadcasts on UDP 9943, and the UDP 9943 broadcast of Quest-style clients). The GUI only shows the `headset_seen` events.
    public class Discovery : IDisposable
    {
        public class Found { public string Hostname, Ip, Protocol, ServerProtocol, Via; public bool Compatible; public DateTime Seen; }
        public event Action<Found> OnFound;
        public event Action<string> OnError;
        HostProcess host;

        public string Start(double seconds = 3600)
        {
            host = new HostProcess();
            host.OnEvent += (name, d) =>
            {
                if (name == "headset_seen" && d != null)
                    OnFound?.Invoke(new Found
                    {
                        Hostname = Str(d, "hostname"), Ip = Str(d, "ip"), Protocol = Str(d, "protocol"), ServerProtocol = Str(d, "server_protocol"),
                        Via = Str(d, "via"), Compatible = d.TryGetValue("compatible", out var c) && c is bool b && b, Seen = DateTime.Now,
                    });
                else if (name == "discovery_error" && d != null) OnError?.Invoke(Str(d, "message"));
            };
            return host.Start($"--install-dir \"{Paths.Dir}\" --discover {seconds.ToString(CultureInfo.InvariantCulture)}");
        }

        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";

        public void Dispose() { try { host?.Stop(1000); } catch { } }
    }

    /// auraflashxr_host.exe as a child process: JSON events from its stdout, commands to its stdin (end of stdin = it quits).
    public class HostProcess
    {
        public event Action<string, Dictionary<string, object>> OnEvent;
        public event Action<int> OnExit;
        public event Action<string> OnStderr;
        Process p;
        public bool Running => p != null && !p.HasExited;

        /// Stop auraflashxr_host.exe gracefully (launcher + streamer): the host is a windowless console process
        /// (CreateNoWindow), so CloseMainWindow() does nothing; the real graceful path is "quit" on stdin (or
        /// closing it: EOF -> the host drops its context and un-mutes the PC speakers, vision.rs). Kill() only as a last resort.
        public static void StopGracefully(Process hp)
        {
            if (hp == null) return;
            try
            {
                // The host reads stdin when started with --gui; a redirected stdin lets us send "quit".
                // For a host we did not start ourselves (no redirected stdin), fall through to Kill.
                if (hp.StartInfo != null && !hp.StartInfo.UseShellExecute && hp.StartInfo.RedirectStandardInput)
                {
                    try { hp.StandardInput.WriteLine("quit"); hp.StandardInput.Flush(); } catch { }
                    if (hp.WaitForExit(2000)) return;
                }
            }
            catch { }
            try { if (!hp.HasExited) hp.Kill(); } catch { }
        }

        public string Start(string args)
        {
            if (!File.Exists(Paths.Host)) return Path.GetFileName(Paths.Host) + " is missing from " + Paths.Dir;
            var psi = new ProcessStartInfo(Paths.Host, args)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = true, WorkingDirectory = Paths.Dir, StandardOutputEncoding = Encoding.UTF8,
            };
            p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null || !e.Data.StartsWith("{")) return;
                try
                {
                    var d = Json.Parse(e.Data);
                    OnEvent?.Invoke(d.TryGetValue("event", out var n) ? n as string : "", d.TryGetValue("data", out var x) ? x as Dictionary<string, object> : null);
                }
                catch { }
            };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) OnStderr?.Invoke(e.Data); };
            p.Exited += (s, e) => OnExit?.Invoke(SafeExitCode());
            try { p.Start(); } catch (Exception e) { return "cannot start " + Path.GetFileName(Paths.Host) + ": " + e.Message; }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return null;
        }

        int SafeExitCode() { try { return p.ExitCode; } catch { return -1; } }

        public event Action<string> OnSend;   // every command, running or not (harness/streamer_events.py)
        public void Send(string line) { OnSend?.Invoke(line); try { if (Running) { p.StandardInput.WriteLine(line); p.StandardInput.Flush(); } } catch { } }

        /// Asks the host to quit (it un-mutes the PC speakers on the way out); kills it after the timeout.
        public void Stop(int timeoutMs = 6000)
        {
            if (!Running) return;
            Send("quit");
            try { p.StandardInput.Close(); } catch { }
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } }
        }
    }

    /// `auraflashxr_host --system-check`: the GPU (NVML architecture: Ada = 8, Blackwell = 10) and AuraFlashXR's protocol id.
    public static class SystemCheck
    {
        public static Dictionary<string, object> Run()
        {
            try
            {
                var psi = new ProcessStartInfo(Paths.Host, "--system-check") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using (var p = Process.Start(psi))
                {
                    var outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(15000);
                    foreach (var line in outp.Split('\n'))
                    {
                        if (!line.TrimStart().StartsWith("{")) continue;
                        var d = Json.Parse(line);
                        if (d.TryGetValue("data", out var x) && x is Dictionary<string, object> data) return data;
                    }
                }
            }
            catch (Exception e) { Log.Write("ERROR", "system check failed: " + e.Message); }
            return null;
        }
    }

    /// HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime == this folder's manifest? Registration runs the script elevated.
    /// OpenXR API layers (the Khronos loader's registry lists): an enabled *implicit* layer is loaded into every OpenXR application, ours included, and
    /// some (hand-tracking and input tools) capture or remap the controllers before any runtime sees them. Reading the lists follows the loader's own
    /// format (see also FredEmmott/openxr-api-layers-gui, MIT, which shows the same lists): ...\Khronos\OpenXR\1\ApiLayers\Implicit|Explicit under HKLM and
    /// HKCU, in the 64-bit and the 32-bit view; a value's name is the layer manifest's path, its DWORD 0 = enabled, anything else = disabled.
    /// Disabling sets the DWORD to 1 (reversible; what AuraFlashXR disabled is kept in config\layers_disabled.json so it can be put back).
    public class XrLayer
    {
        public string Hive, Section, Path, Name = "", Description = "", Library = "";
        public int View;           // 64 or 32
        public bool Enabled;
        public bool MayCaptureInput;
        public string KeyPath => @"SOFTWARE\" + (View == 32 ? @"WOW6432Node\" : "") + @"Khronos\OpenXR\1\ApiLayers\" + Section;
        public string Where => $"{Hive} {View}-bit, {Section.ToLowerInvariant()}";
    }

    public static class XrLayers
    {
        static readonly string[] InputHints = { "ultraleap", "leap", "toolkit", "openkneeboard", "neck", "motioncompensation", "hand", "controller", "input", "steamvr", "oculus", "vive" };
        public static string DisabledFile => System.IO.Path.Combine(Paths.Config, "layers_disabled.json");

        public static List<XrLayer> Enumerate()
        {
            var all = new List<XrLayer>();
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    foreach (var section in new[] { "Implicit", "Explicit" })
                    {
                        var l = new XrLayer { Hive = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU", View = view == RegistryView.Registry64 ? 64 : 32, Section = section };
                        try
                        {
                            using (var k = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(l.KeyPath.Replace(@"WOW6432Node\", "")))
                            {
                                if (k == null) continue;
                                foreach (var name in k.GetValueNames())
                                {
                                    var e = new XrLayer { Hive = l.Hive, View = l.View, Section = section, Path = name, Enabled = Convert.ToInt32(k.GetValue(name) ?? 1) == 0 };
                                    try
                                    {
                                        var j = Json.Load(name);
                                        e.Name = Json.Str(j, "api_layer.name", ""); e.Description = Json.Str(j, "api_layer.description", ""); e.Library = Json.Str(j, "api_layer.library_path", "");
                                    }
                                    catch { }
                                    if (e.Name.Length == 0) e.Name = System.IO.Path.GetFileNameWithoutExtension(name);
                                    var hay = (e.Name + " " + e.Description + " " + e.Library + " " + name).ToLowerInvariant();
                                    e.MayCaptureInput = InputHints.Any(h => hay.Contains(h));
                                    all.Add(e);
                                }
                            }
                        }
                        catch { }
                    }
            // the 64-bit and 32-bit views of one key are the same list on most PCs: a layer is shown once per (hive, section, path)
            return all.GroupBy(x => x.Hive + "|" + x.Section + "|" + x.Path.ToLowerInvariant()).Select(g => g.First()).ToList();
        }

        /// Enabled implicit layers: the ones that load into every OpenXR application.
        public static List<XrLayer> ActiveImplicit() => Enumerate().Where(l => l.Enabled && l.Section == "Implicit").ToList();

        static string RegArgs(XrLayer l, int value) =>
            $"reg add \"{l.Hive}\\{l.KeyPath}\" /v \"{l.Path}\" /t REG_DWORD /d {value} /f /reg:{l.View}";

        /// Sets the layers' DWORD (1 = disabled, 0 = enabled): HKCU directly, HKLM through one UAC prompt. True when every one took effect.
        public static bool Set(IEnumerable<XrLayer> layers, bool enable)
        {
            var list = layers.ToList();
            var machine = list.Where(l => l.Hive == "HKLM").ToList();
            foreach (var l in list.Where(l => l.Hive == "HKCU"))
                try { using (var k = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, l.View == 64 ? RegistryView.Registry64 : RegistryView.Registry32).CreateSubKey(l.KeyPath.Replace(@"WOW6432Node\", ""))) k.SetValue(l.Path, enable ? 0 : 1, RegistryValueKind.DWord); } catch { }
            if (machine.Count > 0)
            {
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe", "/c " + string.Join(" & ", machine.Select(l => RegArgs(l, enable ? 0 : 1)))) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                    using (var p = Process.Start(psi)) p.WaitForExit(60000);
                }
                catch (System.ComponentModel.Win32Exception) { return false; }   // UAC declined
            }
            var now = Enumerate();
            bool ok = list.All(l => now.Any(n => n.Hive == l.Hive && n.Section == l.Section && string.Equals(n.Path, l.Path, StringComparison.OrdinalIgnoreCase) && n.Enabled == enable));
            if (ok)
            {
                Remember(list, enable);
                Log.Write("INFO", $"OpenXR layers {(enable ? "enabled" : "disabled")}: " + string.Join(", ", list.Select(l => l.Name)));
            }
            return ok;
        }

        // what AuraFlashXR disabled, so the runtime toggle (and the layers dialog) can put it back
        static void Remember(List<XrLayer> layers, bool enabled)
        {
            var root = Json.Load(DisabledFile);
            var cur = (root.TryGetValue("layers", out var o) && o is object[] a ? a.OfType<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>>());
            foreach (var l in layers)
            {
                cur.RemoveAll(d => (d["path"] as string) == l.Path && (d["hive"] as string) == l.Hive && (d["section"] as string) == l.Section);
                if (!enabled) cur.Add(new Dictionary<string, object> { { "hive", l.Hive }, { "view", l.View }, { "section", l.Section }, { "path", l.Path }, { "name", l.Name } });
            }
            root["layers"] = cur.Cast<object>().ToArray();
            Directory.CreateDirectory(Paths.Config);
            Json.Save(DisabledFile, root);
        }

        /// The layers AuraFlashXR disabled earlier and that are still disabled.
        public static List<XrLayer> DisabledByUs()
        {
            var root = Json.Load(DisabledFile);
            var now = Enumerate();
            var r = new List<XrLayer>();
            if (root.TryGetValue("layers", out var o) && o is object[] a)
                foreach (var d in a.OfType<Dictionary<string, object>>())
                {
                    var m = now.FirstOrDefault(n => n.Hive == (d["hive"] as string) && n.Section == (d["section"] as string) && string.Equals(n.Path, d["path"] as string, StringComparison.OrdinalIgnoreCase));
                    if (m != null && !m.Enabled) r.Add(m);
                }
            return r;
        }
    }

    /// The layers dialog: every OpenXR API layer, the ones that load into every application ticked when they may capture input; Disable / Enable.
    public class LayersForm : DpiForm
    {
        readonly ListView list = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, Height = 230, Width = 820, HeaderStyle = ColumnHeaderStyle.Nonclickable, BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
        List<XrLayer> layers;
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(820, 0), Margin = new Padding(3, 6, 3, 6), ForeColor = Ui.DarkMuted };
        public LayersForm()
        {
            Text = Lang.Get("XrLayers.Title");
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Body; Icon = Ui.AppIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;
            var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
            col.Controls.Add(new Label { Text = Lang.Get("XrLayers.Subtitle"), AutoSize = true, Font = Ui.Big, ForeColor = Ui.Accent });
            col.Controls.Add(new Label { Text = Lang.Get("XrLayers.Explain"), AutoSize = true, MaximumSize = new Size(820, 0), Margin = new Padding(3, 4, 3, 6) });
            string[] cols = { Lang.Get("XrLayers.ColLayer"), Lang.Get("XrLayers.ColLoading"), Lang.Get("XrLayers.ColLocation"), Lang.Get("XrLayers.ColNote") }; int[] w = { 290, 95, 170, 230 };
            for (int i = 0; i < cols.Length; i++) Ui.Col(list, cols[i], w[i]);
            col.Controls.Add(list);
            col.Controls.Add(status);
            var row = new FlowLayoutPanel { AutoSize = true };
            var dis = new Button { Text = Lang.Get("XrLayers.BtnDisableSelection"), AutoSize = true, Font = Ui.Bold, MinimumSize = new Size(150, 30) };
            var en = new Button { Text = Lang.Get("XrLayers.BtnEnableSelection"), AutoSize = true, MinimumSize = new Size(140, 30) };
            var close = new Button { Text = Lang.Get("XrLayers.BtnClose"), AutoSize = true, DialogResult = DialogResult.OK, MinimumSize = new Size(100, 30) };
            dis.Click += (s, e) => Apply(false);
            en.Click += (s, e) => Apply(true);
            row.Controls.Add(dis); row.Controls.Add(en); row.Controls.Add(close);
            col.Controls.Add(row);
            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.DarkBg };
            col.Dock = DockStyle.None; col.Location = Point.Empty;
            scroller.Controls.Add(col);
            Controls.Add(scroller);
            Ui.AutoFitWindow(this, null, col);
            CancelButton = close;
            Fill();
        }

        void Fill()
        {
            layers = XrLayers.Enumerate().OrderByDescending(l => l.Enabled && l.Section == "Implicit").ThenByDescending(l => l.MayCaptureInput).ThenBy(l => l.Name).ToList();
            list.Items.Clear();
            foreach (var l in layers)
            {
                var loads = l.Enabled ? (l.Section == "Implicit" ? Lang.Get("XrLayers.LoadAllGames") : Lang.Get("XrLayers.LoadIfRequested")) : Lang.Get("XrLayers.LoadDisabled");
                var note = l.Enabled && l.Section == "Implicit" && l.MayCaptureInput ? Lang.Get("XrLayers.NoteMayCapture") : (l.Enabled && l.Section == "Implicit" ? Lang.Get("XrLayers.NoteLoadedAllGames") : "");
                var it = new ListViewItem(new[] { l.Name, loads, l.Where, note }) { Checked = l.Enabled && l.Section == "Implicit" && l.MayCaptureInput, ToolTipText = l.Path + "\n" + l.Description };
                if (l.Enabled && l.Section == "Implicit" && l.MayCaptureInput) it.ForeColor = Color.Firebrick;
                list.Items.Add(it);
            }
            var n = layers.Count(l => l.Enabled && l.Section == "Implicit");
            Ui.FitColumns(list);
            status.Text = layers.Count == 0 ? Lang.Get("XrLayers.NoLayers") : string.Format(Lang.Get("XrLayers.StatusCounts"), n, layers.Count(l => l.Enabled && l.Section == "Implicit" && l.MayCaptureInput));
        }

        void Apply(bool enable)
        {
            var picked = list.CheckedItems.Cast<ListViewItem>().Select(i => layers[i.Index]).ToList();
            if (picked.Count == 0) { status.Text = Lang.Get("XrLayers.SelectFirst"); return; }
            var ok = XrLayers.Set(picked, enable);
            Fill();
            status.Text = (ok ? (enable ? Lang.Get("XrLayers.EnabledDone") : Lang.Get("XrLayers.DisabledDone")) : Lang.Get("XrLayers.NotChanged")) + status.Text;
        }
    }

    public static class OpenXr
    {
        public static string ActiveRuntime()
        {
            try
            {
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1"))
                    return k?.GetValue("ActiveRuntime") as string ?? "";
            }
            catch { return ""; }
        }

        public static bool IsOurs() => string.Equals(ActiveRuntime(), Paths.Manifest, StringComparison.OrdinalIgnoreCase);

        /// SteamVR is the active OpenXR runtime: its vrserver.exe then owns the discovery socket through driver_auraflashxr.dll.
        public static bool IsSteamVrActive()
        {
            string active = ActiveRuntime();
            return active.Length > 0 && active.IndexOf("steamxr", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// The runtime that was active before this folder took over (saved by register_openxr_runtime.ps1; falls back to SteamVR or another registered runtime when empty).
        public static string Previous()
        {
            try
            {
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\AuraFlashXR"))
                {
                    var p = k?.GetValue("PreviousActiveRuntime") as string ?? "";
                    if (p.Length > 0 && !string.Equals(p, Paths.Manifest, StringComparison.OrdinalIgnoreCase)) return p;
                }
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1\AvailableRuntimes"))
                {
                    if (k != null)
                        foreach (var name in k.GetValueNames())
                            if (!string.Equals(name, Paths.Manifest, StringComparison.OrdinalIgnoreCase) && File.Exists(name))
                                return name;
                }
                const string steamvr = @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json";
                if (File.Exists(steamvr)) return steamvr;
            }
            catch { }
            return "";
        }

        /// A runtime's name for people: the manifest's "runtime.name", else the folder it is in.
        public static string Name(string manifest)
        {
            if (string.IsNullOrEmpty(manifest)) return "none";
            if (string.Equals(manifest, Paths.Manifest, StringComparison.OrdinalIgnoreCase)) return "AuraFlashXR";
            try
            {
                var j = Json.Load(manifest);
                var n = Json.Str(j, "runtime.name", "");
                if (n.Length > 0) return n;
            }
            catch { }
            try { return Path.GetFileName(Path.GetDirectoryName(manifest)); } catch { return manifest; }
        }

        /// UAC prompt, then register_openxr_runtime.ps1 -Unregister: the previous runtime is the active one again. True if this folder is no longer active.
        public static bool Unregister()
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{Paths.RegisterScript}\" -InstallDir \"{Paths.Dir}\" -Unregister -Quiet")
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                using (var p = Process.Start(psi)) p.WaitForExit(60000);
            }
            catch (System.ComponentModel.Win32Exception) { return false; }
            var ok = !IsOurs();
            Log.Write(ok ? "INFO" : "ERROR", ok ? "OpenXR runtime: back to " + ActiveRuntime() : "unregistering the OpenXR runtime did not take effect");

            // Ensure AuraFlashXR SteamVR driver is registered in SteamVR and stock AuraFlashXR is removed to avoid port conflicts
            try
            {
                EnsureSteamVrDriver();
            }
            catch { }

            return ok;
        }

        public static void EnsureSteamVrDriver()
        {
            string steamvrDir = FindSteamVrDirectory();
            string vrpathreg = !string.IsNullOrEmpty(steamvrDir) ? Path.Combine(steamvrDir, @"bin\win64\vrpathreg.exe") : null;
            if (string.IsNullOrEmpty(vrpathreg) || !File.Exists(vrpathreg))
            {
                vrpathreg = @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win64\vrpathreg.exe";
                if (!File.Exists(vrpathreg)) return;
            }

            string driverDir = Path.Combine(Paths.Dir, @"tools\auraflashxr");
            if (!Directory.Exists(driverDir)) return;

            // Automatically clean any conflicting third-party AuraFlashXR drivers or obsolete AuraFlashXR paths from openvrpaths.vrpath
            try
            {
                string vrpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"openvr\openvrpaths.vrpath");
                if (File.Exists(vrpath))
                {
                    string content = File.ReadAllText(vrpath);
                    var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""([^""]+)""");
                    var driversToRemove = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        string val = m.Groups[1].Value.Replace(@"\\", @"\");
                        if ((val.IndexOf("auraflashxr", StringComparison.OrdinalIgnoreCase) >= 0 || val.IndexOf("auraflashxr", StringComparison.OrdinalIgnoreCase) >= 0)
                            && !string.Equals(val.TrimEnd('\\', '/'), driverDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                        {
                            driversToRemove.Add(val);
                        }
                    }

                    foreach (var drv in driversToRemove)
                    {
                        try
                        {
                            var psiRem = new ProcessStartInfo(vrpathreg, $"removedriver \"{drv}\"") { UseShellExecute = false, CreateNoWindow = true };
                            using (var p = Process.Start(psiRem)) p.WaitForExit(3000);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // Register our current AuraFlashXR driver
            try
            {
                var psiAdd = new ProcessStartInfo(vrpathreg, $"adddriver \"{driverDir}\"") { UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psiAdd)) p.WaitForExit(5000);
            }
            catch { }
        }

        /// UAC prompt, then register_openxr_runtime.ps1. True if this folder is the active runtime afterwards.
        public static bool Register(IWin32Window owner)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{Paths.RegisterScript}\" -InstallDir \"{Paths.Dir}\" -Quiet")
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                using (var p = Process.Start(psi)) p.WaitForExit(60000);
            }
            catch (System.ComponentModel.Win32Exception) { return false; } // UAC declined
            var ok = IsOurs();
            Log.Write(ok ? "INFO" : "ERROR", ok ? "OpenXR runtime registered: " + Paths.Manifest : "OpenXR registration did not take effect");
            return ok;
        }

        public static bool SetSteamVr()
        {
            string steamvr = @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json";
            if (!File.Exists(steamvr)) return false;
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c reg add \"HKLM\\SOFTWARE\\Khronos\\OpenXR\\1\" /v ActiveRuntime /t REG_SZ /d \"{steamvr}\" /f")
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                using (var p = Process.Start(psi)) p.WaitForExit(10000);
            }
            catch { return false; }
            EnsureSteamVrDriver();
            return string.Equals(ActiveRuntime(), steamvr, StringComparison.OrdinalIgnoreCase);
        }

        /// Locates SteamVR installation path dynamically and universally across Windows registry, openvrpaths, and standard directories.
        public static string FindSteamVrDirectory()
        {
            try
            {
                // 1. Check openvrpaths.vrpath (official OpenVR registry)
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string vrpath = Path.Combine(localApp, @"openvr\openvrpaths.vrpath");
                if (File.Exists(vrpath))
                {
                    var j = Json.Load(vrpath);
                    if (j.TryGetValue("runtime", out var r) && r is object[] arr)
                    {
                        foreach (var item in arr)
                        {
                            string p = item as string;
                            if (!string.IsNullOrEmpty(p) && Directory.Exists(p) && File.Exists(Path.Combine(p, @"bin\win64\vrserver.exe")))
                                return p;
                        }
                    }
                }
            }
            catch { }

            try
            {
                // 2. Check Steam App 250820 uninstall key (SteamVR)
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 250820"))
                {
                    string loc = k?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc) && File.Exists(Path.Combine(loc, @"bin\win64\vrserver.exe")))
                        return loc;
                }
            }
            catch { }

            try
            {
                // 3. Check Steam installation path from registry
                string steamDir = null;
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default).OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    steamDir = k?.GetValue("SteamPath") as string;
                }
                if (string.IsNullOrEmpty(steamDir))
                {
                    using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        steamDir = k?.GetValue("InstallPath") as string;
                    }
                }
                if (!string.IsNullOrEmpty(steamDir))
                {
                    string candidate = Path.Combine(steamDir, @"steamapps\common\SteamVR");
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, @"bin\win64\vrserver.exe")))
                        return candidate;
                }
            }
            catch { }

            // 4. Standard fallback paths across all drives
            List<string> fallbacks = new List<string> {
                @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR",
                @"C:\Program Files\Steam\steamapps\common\SteamVR"
            };
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        string libPath = Path.Combine(drive.RootDirectory.FullName, @"SteamLibrary\steamapps\common\SteamVR");
                        if (!fallbacks.Contains(libPath)) fallbacks.Add(libPath);
                    }
                }
            }
            catch { }

            foreach (var fb in fallbacks)
            {
                if (Directory.Exists(fb) && File.Exists(Path.Combine(fb, @"bin\win64\vrserver.exe")))
                    return fb;
            }

            return null;
        }

        public static string FindSteamVrExecutable()
        {
            string dir = FindSteamVrDirectory();
            if (string.IsNullOrEmpty(dir)) return null;
            string vrStartup = Path.Combine(dir, @"bin\win64\vrstartup.exe");
            if (File.Exists(vrStartup)) return vrStartup;
            string vrServer = Path.Combine(dir, @"bin\win64\vrserver.exe");
            if (File.Exists(vrServer)) return vrServer;
            return null;
        }
    }

    public enum FirewallState { Allowed, Blocked, Missing, Unknown }

    /// One inbound/outbound rule of Windows Firewall that names an auraflashxr_host.exe, with what the review makes of it.
    public class FwRule
    {
        public string Name = "", DisplayName = "", Direction = "", Action = "", Profile = "", Protocol = "", Program = "";
        public bool Enabled = true, ProgramExists = true;
        public string Status = "";      // plain words for the list
        public bool Propose;            // ticked by default in the review: it can go
        public bool InPlace;            // the rule that lets this folder's auraflashxr_host.exe in
    }

    public class FwReport
    {
        public FirewallState State = FirewallState.Unknown;
        public List<FwRule> Rules = new List<FwRule>();
        public string Headline = "";
        public int Clutter => Rules.Count(r => r.Propose);
    }

    /// Windows Firewall rules of auraflashxr_host.exe. The rule belongs to the exact exe path: a moved folder needs its own rule, and Windows leaves the old ones
    /// behind (and adds a TCP and a UDP rule at every "Allow" prompt). Read through PowerShell's NetSecurity cmdlets, whose values are English whatever
    /// the Windows language is (netsh's are not), without administrator rights; changing anything asks for them.
    public static class Firewall
    {
        const string ReadScript = "$ErrorActionPreference='SilentlyContinue'; " +
            "$rules = @(Get-NetFirewallApplicationFilter | Where-Object { $_.Program -match '(auraflashxr_host|auraflashxr_host)\\.exe$' } | ForEach-Object { $r = $_ | Get-NetFirewallRule; $p = $r | Get-NetFirewallPortFilter; " +
            "[pscustomobject]@{ Name=$r.Name; DisplayName=$r.DisplayName; Enabled=[string]$r.Enabled; Direction=[string]$r.Direction; Action=[string]$r.Action; Profile=[string]$r.Profile; Protocol=[string]$p.Protocol; Program=$_.Program } }); " +
            "$profiles = @(Get-NetConnectionProfile | ForEach-Object { [string]$_.NetworkCategory }); " +
            "ConvertTo-Json -Depth 4 -Compress -InputObject ([pscustomobject]@{ profiles = $profiles; rules = $rules })";

        static string Ps(string script, int ms = 40000)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8 };
            using (var p = System.Diagnostics.Process.Start(psi)) { var t = p.StandardOutput.ReadToEnd(); p.WaitForExit(ms); return t; }
        }

        static IEnumerable<Dictionary<string, object>> Items(object o)
        {
            if (o is object[] a) return a.OfType<Dictionary<string, object>>();
            if (o is Dictionary<string, object> d) return new[] { d };
            return new Dictionary<string, object>[0];
        }

        static bool ProfileApplies(string rule, string[] active)
        {
            if (string.IsNullOrEmpty(rule) || rule.Equals("Any", StringComparison.OrdinalIgnoreCase) || rule.Equals("All", StringComparison.OrdinalIgnoreCase) || active.Length == 0) return true;
            return active.Any(c => rule.IndexOf(c.StartsWith("Domain") ? "Domain" : c, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// Every rule that names an auraflashxr_host.exe, judged against this folder's exe: the one that lets it in, the ones that block it, and what is just clutter.
        public static FwReport Analyze(string exe)
        {
            var rep = new FwReport();
            try
            {
                var j = Json.Parse(Ps(ReadScript).Trim());
                if (j.Count == 0) { rep.Headline = "The firewall could not be read."; return rep; }
                string[] active = j.TryGetValue("profiles", out var po0) ? (po0 is object[] pa ? pa.Select(x => Convert.ToString(x)).ToArray() : po0 is string one ? new[] { one } : new string[0]) : new string[0];
                foreach (var d in Items(j.TryGetValue("rules", out var ro) ? ro : null))
                {
                    string S(string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v) : "";
                    var r = new FwRule { Name = S("Name"), DisplayName = S("DisplayName"), Direction = S("Direction"), Action = S("Action"), Profile = S("Profile"), Protocol = S("Protocol"), Program = S("Program"), Enabled = S("Enabled").Equals("True", StringComparison.OrdinalIgnoreCase) };
                    // a program on a drive that is not there (a removed USB disk) is not proven gone
                    r.ProgramExists = r.Program.Length == 0 || File.Exists(r.Program) || !Directory.Exists(Path.GetPathRoot(r.Program));
                    rep.Rules.Add(r);
                }
                Func<FwRule, bool> effective = r => r.Enabled && ProfileApplies(r.Profile, active);
                bool Mine(FwRule r) => r.Direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase) && string.Equals(r.Program, exe, StringComparison.OrdinalIgnoreCase);
                var mine = rep.Rules.Where(Mine).ToList();
                bool block = mine.Any(r => r.Action.Equals("Block", StringComparison.OrdinalIgnoreCase) && effective(r));
                var allows = mine.Where(r => r.Action.Equals("Allow", StringComparison.OrdinalIgnoreCase) && effective(r)).ToList();
                rep.State = block ? FirewallState.Blocked : (allows.Count > 0 ? FirewallState.Allowed : FirewallState.Missing);
                // the rule that counts: an any-protocol one if there is one, else the first
                var keeper = allows.FirstOrDefault(r => r.Protocol.Equals("Any", StringComparison.OrdinalIgnoreCase)) ?? allows.FirstOrDefault();
                foreach (var r in rep.Rules)
                {
                    if (Mine(r))
                    {
                        if (r.Action.Equals("Block", StringComparison.OrdinalIgnoreCase)) { r.Status = "BLOCKS this folder"; r.Propose = r.Enabled; }
                        else if (!r.Enabled) { r.Status = "this folder, switched off"; }
                        else if (!ProfileApplies(r.Profile, active)) { r.Status = "this folder, but not for the current network (" + string.Join("/", active) + ")"; }
                        else if (r == keeper) { r.Status = "in place: lets this folder in"; r.InPlace = true; }
                        else if (keeper != null && keeper.Protocol.Equals("Any", StringComparison.OrdinalIgnoreCase)) { r.Status = "redundant: the rule above already covers all protocols"; r.Propose = true; }
                        else if (allows.Any(o => o != r && o.Protocol.Equals(r.Protocol, StringComparison.OrdinalIgnoreCase) && allows.IndexOf(o) < allows.IndexOf(r))) { r.Status = "duplicate of an earlier " + r.Protocol + " rule"; r.Propose = true; }
                        else r.Status = "this folder (" + r.Protocol + ")";
                    }
                    else if (!r.ProgramExists) { r.Status = "stale: the file is gone (" + r.Program + ")"; r.Propose = true; }
                    else r.Status = "another copy of auraflashxr_host.exe (left alone)";
                }
                int clutter = rep.Clutter;
                rep.Headline = rep.State == FirewallState.Allowed
                    ? (clutter == 0 ? "Windows Firewall lets this folder's host in. Nothing to clean up." : $"Windows Firewall lets this folder's host in. {clutter} leftover rule(s) can be removed.")
                    : rep.State == FirewallState.Blocked ? "Windows Firewall BLOCKS this folder's host: the headset will not be found and cannot connect."
                    : "Windows Firewall has no rule for this folder's host (a rule belongs to the exact folder): discovery will find nothing until it is allowed.";
            }
            catch (Exception e) { Log.Write("WARN", "firewall check: " + e.Message); rep.State = FirewallState.Unknown; rep.Headline = "The firewall could not be read."; }
            return rep;
        }

        public static FirewallState Check(string program) => Analyze(program).State;

        static FwReport cached; static string cachedFor; static DateTime cachedAt;
        /// The same analysis, reused for a minute (a window that shows the verdict many times, the test harness): PowerShell takes a few seconds.
        public static FwReport AnalyzeCached(string exe)
        {
            if (cached == null || cachedFor != exe || (DateTime.Now - cachedAt).TotalSeconds > 60) { cached = Analyze(exe); cachedFor = exe; cachedAt = DateTime.Now; }
            return cached;
        }

        /// Removes every rule of this program (a block rule beats an allow rule) and adds one inbound allow rule, through a UAC prompt. False: declined or failed.
        public static bool Allow(string program)
        {
            try
            {
                var args = $"/c netsh advfirewall firewall delete rule name=all program=\"{program}\" & netsh advfirewall firewall add rule name=\"AuraFlashXR Host\" dir=in action=allow program=\"{program}\" enable=yes profile=any";
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", args) { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                using (var p = System.Diagnostics.Process.Start(psi)) { p.WaitForExit(30000); }
                Log.Write("INFO", $"firewall: allow rule for {program} requested");
                return true;
            }
            catch (Exception e) { Log.Write("WARN", "firewall rule not added: " + e.Message); return false; }
        }

        /// Deletes these rules (by their internal name) through one UAC prompt.
        public static bool Remove(IEnumerable<FwRule> rules)
        {
            var names = rules.Select(r => "'" + r.Name.Replace("'", "''") + "'").ToList();
            if (names.Count == 0) return true;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Remove-NetFirewallRule -Name " + string.Join(",", names).Replace("\"", "\\\"") + "\"")
                { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                using (var p = System.Diagnostics.Process.Start(psi)) { p.WaitForExit(60000); }
                Log.Write("INFO", "firewall: removed " + names.Count + " leftover rule(s) of auraflashxr_host.exe");
                return true;
            }
            catch (Exception e) { Log.Write("WARN", "firewall rules not removed: " + e.Message); return false; }
        }
    }

    /// The firewall review: every rule that names an auraflashxr_host.exe, what each is, the clutter ticked, one click to remove it or to allow this folder.
    public class FirewallForm : DpiForm
    {
        readonly ListView list = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, Height = 240, Width = 880, HeaderStyle = ColumnHeaderStyle.Nonclickable, BackColor = Ui.DarkCard, ForeColor = Ui.DarkText };
        readonly Label headline = new Label { AutoSize = true, MaximumSize = new Size(880, 0), Font = Ui.Bold, Margin = new Padding(3, 6, 3, 6), ForeColor = Ui.DarkText };
        readonly Button allow = new Button { Text = Lang.Get("Fw.BtnCleanAllow"), AutoSize = true, Font = Ui.Bold, MinimumSize = new Size(130, 30) };
        readonly Button remove = new Button { Text = Lang.Get("Fw.BtnRemoveChecked"), AutoSize = true, MinimumSize = new Size(130, 30) };
        FwReport report;
        readonly string exe;
        public FirewallForm(FwReport demo = null)
        {
            exe = Paths.Host;
            Text = Lang.Get("Fw.Title");
            Font = Ui.Body; Icon = Ui.AppIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;
            var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
            col.Controls.Add(new Label { Text = Lang.Get("Fw.Subtitle"), AutoSize = true, Font = Ui.Big, ForeColor = Ui.Accent });
            col.Controls.Add(new Label { Text = Lang.Get("Fw.Explain"), AutoSize = true, MaximumSize = new Size(880, 0), Margin = new Padding(3, 4, 3, 4) });
            col.Controls.Add(headline);
            foreach (var c in new[] { (Lang.Get("Fw.ColRule"), 190), (Lang.Get("Fw.ColAction"), 70), (Lang.Get("Fw.ColProtocol"), 80), (Lang.Get("Fw.ColNetwork"), 100), (Lang.Get("Fw.ColEnabled"), 40), (Lang.Get("Fw.ColDescription"), 400) }) Ui.Col(list, c.Item1, c.Item2);
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            var refresh = new Button { Text = Lang.Get("Fw.BtnRefresh"), AutoSize = true, MinimumSize = new Size(100, 30) };
            var close = new Button { Text = Lang.Get("Fw.BtnClose"), AutoSize = true, DialogResult = DialogResult.OK, MinimumSize = new Size(100, 30) };
            // one action: the rule that is missing is added, the leftovers are removed (the rules themselves are in the details below)
            allow.Click += (s, e) =>
            {
                if (report != null && (report.State == FirewallState.Missing || report.State == FirewallState.Blocked)) Firewall.Allow(exe);
                Firewall.Remove(list.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => (FwRule)i.Tag));
                Load(null);
            };
            remove.Click += (s, e) => { Firewall.Remove(list.CheckedItems.Cast<ListViewItem>().Select(i => (FwRule)i.Tag)); Load(null); };
            refresh.Click += (s, e) => Load(null);
            var det = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var detRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            detRow.Controls.Add(remove); detRow.Controls.Add(refresh);
            det.Controls.Add(list); det.Controls.Add(detRow);
            row.Controls.Add(allow); row.Controls.Add(close);
            col.Controls.Add(row);
            col.Controls.Add(Ui.Expander(Lang.Get("Fw.DetailsExpander"), det, demo != null));
            AcceptButton = allow;
            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.DarkBg };
            col.Dock = DockStyle.None; col.Location = Point.Empty;
            scroller.Controls.Add(col);
            Controls.Add(scroller);
            Ui.AutoFitWindow(this, null, col);
            CancelButton = close;
            if (demo != null) Fill(demo); else Shown += (s, e) => Load(null);
        }

        void Load(object _)
        {
            headline.Text = Lang.Get("Fw.Analyzing"); headline.ForeColor = Color.DimGray;
            allow.Enabled = remove.Enabled = false;
            System.Threading.Tasks.Task.Run(() => Firewall.Analyze(exe)).ContinueWith(t => { try { BeginInvoke((Action)(() => Fill(t.Result))); } catch { } });
        }

        void Fill(FwReport r)
        {
            report = r;
            headline.Text = r.Headline;
            headline.ForeColor = r.State == FirewallState.Allowed ? Color.SeaGreen : r.State == FirewallState.Unknown ? Color.DimGray : Color.Firebrick;
            list.Items.Clear();
            foreach (var x in r.Rules.OrderByDescending(x => x.InPlace).ThenBy(x => x.Propose).ThenBy(x => x.Program))
            {
                var it = new ListViewItem(new[] { x.DisplayName.Length > 0 ? x.DisplayName : x.Name, x.Action == "Allow" ? Lang.Get("Fw.ActionAllow") : Lang.Get("Fw.ActionBlock"), x.Protocol, x.Profile, x.Enabled ? Lang.Get("Fw.Yes") : Lang.Get("Fw.No"), x.Status }) { Tag = x, Checked = x.Propose, ToolTipText = x.Program };
                if (x.Propose) it.ForeColor = Color.Firebrick; else if (x.InPlace) it.ForeColor = Color.SeaGreen;
                list.Items.Add(it);
            }
            Ui.FitColumns(list);
            allow.Enabled = r.State == FirewallState.Missing || r.State == FirewallState.Blocked || r.Rules.Any(x => x.Propose);
            remove.Enabled = r.Rules.Count > 0;
        }
    }

    /// An iOS-style toggle: a pill whose knob slides, green when on. Click or Space flips it. For the one setting that should be readable
    /// from across the room (frame generation in the streamer window); the rest of the GUI keeps plain checkboxes.
    public class ToggleSwitch : Control
    {
        bool on;
        float pos;
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 15 };
        public event EventHandler CheckedChanged;
        public bool Checked
        {
            get => on;
            set { if (on == value) return; on = value; anim.Start(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(54, 30);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            TabStop = true;
            anim.Tick += (s, e) =>
            {
                pos = Math.Max(0f, Math.Min(1f, pos + (on ? 0.18f : -0.18f)));
                if (pos <= 0f || pos >= 1f) anim.Stop();
                Invalidate();
            };
        }
        /// Sets the state without the slide (a screenshot, the initial state).
        public void SetCheckedInstant(bool v) { on = v; pos = v ? 1f : 0f; Invalidate(); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Checked = !Checked; base.OnKeyDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? BackColor);
            int h = Height - 4, w = Width - 4;
            var off = Color.FromArgb(0x1C, 0x26, 0x38);
            var onC = Color.FromArgb(0x28, 0xC7, 0x6F);
            var c = Color.FromArgb((int)(off.R + (onC.R - off.R) * pos), (int)(off.G + (onC.G - off.G) * pos), (int)(off.B + (onC.B - off.B) * pos));
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                int r = h;
                path.AddArc(2, 2, r, r, 90, 180);
                path.AddArc(2 + w - r, 2, r, r, 270, 180);
                path.CloseFigure();
                using (var b = new SolidBrush(c)) g.FillPath(b, path);
                var borderC = on ? Color.FromArgb(0x48, 0xDF, 0x8B) : Ui.DarkBorder;
                using (var p = new Pen(Focused ? Ui.AccentLight : borderC, 1.2f)) g.DrawPath(p, path);
            }
            int d = h - 6;
            int x = 5 + (int)Math.Round(pos * (w - d - 6));
            using (var sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillEllipse(sh, x, 6, d, d);
            using (var thumbBrush = new SolidBrush(on ? Color.White : Color.FromArgb(0xC8, 0xD6, 0xE8)))
                g.FillEllipse(thumbBrush, x, 5, d, d);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// The wizard's chroma-key step, for every headset but the Vision Pro (its App Store app has a fixed key colour and range that the streamer cannot change, so there
    /// the chroma stays off): transparent background yes or no, with the recommended colour. The answer is applied through Chroma.Apply.
    public class ChromaStepForm : DpiForm
    {
        public bool Transparent => yes.Checked;
        public string Rgb => $"{pick.BackColor.R:x2}{pick.BackColor.G:x2}{pick.BackColor.B:x2}";
        readonly RadioButton yes = new RadioButton { Text = Lang.Get("Chroma.YesPassthrough"), AutoSize = true, Checked = true };
        readonly RadioButton no = new RadioButton { Text = Lang.Get("Chroma.NoPassthrough"), AutoSize = true };
        readonly Button pick = new Button { Width = 90, Height = 24, FlatStyle = FlatStyle.Flat };
        public ChromaStepForm()
        {
            Text = Lang.Get("Chroma.Title");
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Body; Icon = Ui.AppIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;
            var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14), BackColor = Ui.DarkBg, ForeColor = Ui.DarkText };
            col.Controls.Add(new Label { Text = Lang.Get("Chroma.Question"), AutoSize = true, Font = Ui.Big, ForeColor = Ui.Accent });
            col.Controls.Add(new Label { Text = Lang.Get("Chroma.Explain"),
                AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(3, 6, 3, 6) });
            yes.Margin = new Padding(3, 6, 3, 3); no.Margin = new Padding(3);
            col.Controls.Add(yes);
            var colour = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(22, 0, 0, 4) };
            colour.Controls.Add(new Label { Text = Lang.Get("Chroma.KeyColor"), AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
            SetColour(ColorTranslator.FromHtml("#" + Chroma.DefaultRgb));
            pick.Click += (s, e) => { using (var cd = new ColorDialog { Color = pick.BackColor, FullOpen = true }) if (cd.ShowDialog(this) == DialogResult.OK) SetColour(cd.Color); };
            var reset = new Button { Text = Lang.Get("Chroma.Recommended"), AutoSize = true };
            reset.Click += (s, e) => SetColour(ColorTranslator.FromHtml("#" + Chroma.DefaultRgb));
            colour.Controls.Add(pick); colour.Controls.Add(reset);
            col.Controls.Add(colour);
            col.Controls.Add(no);
            yes.CheckedChanged += (s, e) => colour.Enabled = yes.Checked;
            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            var go = new Button { Text = Lang.Get("Chroma.Continue"), AutoSize = true, DialogResult = DialogResult.OK, Font = Ui.Bold, MinimumSize = new Size(100, 30) };
            row.Controls.Add(go);
            col.Controls.Add(row);
            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Ui.DarkBg };
            col.Dock = DockStyle.None; col.Location = Point.Empty;
            scroller.Controls.Add(col);
            Controls.Add(scroller);
            Ui.AutoFitWindow(this, null, col);
            AcceptButton = go;
        }
        void SetColour(Color c) { pick.BackColor = c; pick.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; pick.ForeColor = c.GetBrightness() > 0.5 ? Color.Black : Color.White; }
    }

    /// A latency the engine could not measure is shown as n/a, not as a number: above one second, or when over 10 % of the run's client samples were dropped
    /// by the engine's aggregator as garbage (`latency_outliers` of `samples`; docs/PLAN_0.5.md U7).
    public static class Latency
    {
        public const string Why = "n/a: the headset's latency samples were not usable (a duration that wrapped, or too many dropped). The other numbers are fine.";
        public static bool Absurd(double ms, double outliers = 0, double samples = 0) =>
            ms < 0 || ms > 1000 || (samples > 0 && !double.IsNaN(outliers) && outliers / samples > 0.10);
        /// AuraFlashXR's own breakdown of the streamer's share of the latency: encoder + transport (network) + decoder, in ms; null when any part is missing or not believable.
        public static double? Streamer(double enc, double net, double dec, double outliers = 0, double samples = 0) =>
            double.IsNaN(enc) || double.IsNaN(net) || double.IsNaN(dec) || Absurd(enc, outliers, samples) || Absurd(net, outliers, samples) || Absurd(dec, outliers, samples) ? (double?)null : enc + net + dec;
        /// "-" when nothing was measured, "n/a" when it was and is not believable, else the number.
        public static string Text(double ms, double outliers = 0, double samples = 0, string fmt = "0") =>
            double.IsNaN(ms) ? "-" : Absurd(ms, outliers, samples) ? "n/a" : ms.ToString(fmt, CultureInfo.InvariantCulture);
    }

    public class Tile : TableLayoutPanel
    {
        readonly Label value, note;
        public Tile(string title, string v, string n)
        {
            // design values at 96 dpi: scaled with the window, or by Ui.LateScale when the tile is created after it loaded
            AutoSize = true; ColumnCount = 1; BackColor = Color.FromArgb(0x11, 0x18, 0x24);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); Padding = new Padding(12, 8, 12, 8); Margin = new Padding(0, 0, 10, 8); MinimumSize = new Size(116, 62);
            DoubleBuffered = true;
            // contenu centre dans la case, texte en 13 pt
            Controls.Add(new Label { Text = title.ToUpperInvariant(), AutoSize = false, ForeColor = Ui.DarkMuted, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 2), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top });
            value = new Label { Text = v, AutoSize = false, ForeColor = Ui.DarkText, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), Margin = new Padding(0), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top };
            note = new Label { Text = n, AutoSize = false, ForeColor = Ui.DarkMuted, Font = new Font("Segoe UI", 13f), Margin = new Padding(0, 1, 0, 0), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top };
            Controls.Add(value); Controls.Add(note);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundRect(rect, 7))
            {
                using (var b = new SolidBrush(Color.FromArgb(0x11, 0x18, 0x24))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(0x23, 0x32, 0x48), 1f)) g.DrawPath(p, path);
            }
            // Subtle top accent line
            using (var topPen = new Pen(Color.FromArgb(120, Ui.AccentLight.R, Ui.AccentLight.G, Ui.AccentLight.B), 2f))
                g.DrawLine(topPen, 8, 1, Width - 9, 1);
            base.OnPaint(e);
        }
        static readonly ToolTip tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        public string Value => value.Text;
        public string Note => note.Text;
        public Color ValueColor => value.ForeColor;
        public void Set(string v, string n, Color? color = null, string tooltip = null)
        {
            value.Text = v; note.Text = n; value.ForeColor = color ?? Ui.DarkText;
            foreach (Control c in new Control[] { this, value, note }) tip.SetToolTip(c, tooltip);
        }
    }

    /// Every window of the GUI: the designer pattern WinForms' own scaling needs. Built suspended with AutoScaleMode.Dpi (design values at 96 dpi), laid out once
    /// at load, where the framework scales the form, its docked panels and every control that is already in it to the monitor's dpi. Controls created later
    /// (a wizard page) are not scaled by it: Ui.LateScale. With bin\*.config (PerMonitorV2) it follows the window from monitor to monitor.
    public class DpiForm : Form
    {
        public DpiForm()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            SuspendLayout();
        }
        protected override void OnLoad(EventArgs e)
        {
            Ui.ApplyModernTheme(this);
            ResumeLayout(false);
            PerformLayout();
            base.OnLoad(e);
        }
    }

    public static class Ui
    {
        /// Test harness only (uitest.exe): pretend the display is this many dpi. 0 = the real one.
        public static int DpiOverride;
        /// Test harness only: the pretend work area (device pixels) the window-fitting code uses instead of the real screen's.
        public static Rectangle? WorkAreaOverride;
        public static Rectangle WorkArea(Control c) => WorkAreaOverride ?? Screen.FromControl(c).WorkingArea;
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, Action> refits = new System.Runtime.CompilerServices.ConditionalWeakTable<Form, Action>();
        /// Test harness: runs a window's own fitting code again (after the pretend scale and work area changed).
        public static void Refit(Form f) { if (refits.TryGetValue(f, out var a)) a(); }

        /// A window with a fixed design size (a wizard, a list window): never bigger than the work area, its minimum size too. Sizes set in code are
        /// design values at 96 dpi (the framework scales them at load).
        public static void FitToWorkArea(Form f)
        {
            bool placed = false;
            Action fit = () =>
            {
                var wa = WorkArea(f);
                f.MinimumSize = new Size(Math.Min(f.MinimumSize.Width, wa.Width), Math.Min(f.MinimumSize.Height, wa.Height));
                f.Size = new Size(Math.Min(f.Width, wa.Width), Math.Min(f.Height, wa.Height));
                // the size was set after CenterScreen placed the window: centre it again (once), and keep it inside the work area
                int x = f.Left, y = f.Top;
                if (!placed) { placed = true; if (f.StartPosition == FormStartPosition.CenterScreen) { x = wa.Left + (wa.Width - f.Width) / 2; y = wa.Top + (wa.Height - f.Height) / 2; } }
                f.Location = new Point(Math.Max(wa.Left, Math.Min(x, wa.Right - f.Width)), Math.Max(wa.Top, Math.Min(y, wa.Bottom - f.Height)));
            };
            f.Load += (s, e) => fit();
            refits.Remove(f); refits.Add(f, fit);
        }

        /// A window exactly as big as its content: the header (docked at the top) plus the body, which sits in a scrolling host. When that is more
        /// than the work area allows (1080p at 150 %, a long message) the window stops at the work area and the body scrolls instead of being cut.
        ///
        /// refitOnChange false (the streamer): the window is sized at load and by Ui.Refit only, so a notice appearing or a number getting longer never moves or resizes it.
        /// `measure` (optional) gets the sizing action and runs it with the window's fullest content showing, so the fixed size has room for everything that can appear.
        public static void AutoFitWindow(Form f, Control header, Control body, bool refitOnChange = true, Action<Action> measure = null)
        {
            if (body is Panel bp) bp.AutoSizeMode = AutoSizeMode.GrowAndShrink;   // the default GrowOnly keeps the size the framework scaled it to: empty space below the content
            bool placed = false;
            Action size = () =>
            {
                var wa = WorkArea(f);
                var chrome = new Size(f.Width - f.ClientSize.Width, f.Height - f.ClientSize.Height);
                var bs = body.PreferredSize;
                body.PerformLayout();
                int cw = Math.Max(bs.Width, body.Width) + 4, ch = (header != null ? header.Height : 0) + body.Height + 2;   // the body is AutoSize: its real height after a layout is the content
                int maxW = wa.Width - chrome.Width, maxH = wa.Height - chrome.Height;
                if (ch > maxH) { ch = maxH; cw += SystemInformation.VerticalScrollBarWidth; }
                if (cw > maxW) { cw = maxW; ch = Math.Min(maxH, ch + SystemInformation.HorizontalScrollBarHeight); }
                f.ClientSize = new Size(cw, ch);
                // CenterScreen centred the window at its design size, before this fit made it bigger: centre it once now, later fits only keep it on screen
                int x = f.Left, y = f.Top;
                if (!placed)
                {
                    placed = true;
                    var around = f.StartPosition == FormStartPosition.CenterParent && f.Owner != null ? f.Owner.Bounds : (f.StartPosition == FormStartPosition.CenterScreen ? wa : Rectangle.Empty);
                    if (around != Rectangle.Empty) { x = around.Left + (around.Width - f.Width) / 2; y = around.Top + (around.Height - f.Height) / 2; }
                }
                f.Location = new Point(Math.Max(wa.Left, Math.Min(x, wa.Right - f.Width)), Math.Max(wa.Top, Math.Min(y, wa.Bottom - f.Height)));
            };
            Action fit = measure == null ? size : () => measure(size);
            f.Load += (s, e) => fit();
            if (refitOnChange) body.SizeChanged += (s, e) => { if (f.IsHandleCreated) fit(); };
            refits.Remove(f); refits.Add(f, fit);
        }
        /// A size designed at 96 dpi, in pixels at the control's monitor dpi (for what the framework does not scale itself: list columns). The test
        /// harness overrides the dpi.
        public static int Px(Control c, int designPx) => (int)Math.Round(designPx * (DpiOverride > 0 ? DpiOverride : c.DeviceDpi) / 96.0);
        /// The reverse: pixels measured on screen (a width taken from a parent) as design units, for controls that LateScale will scale up.
        public static int Design(Control c, int px) => (int)Math.Round(px * 96.0 / c.DeviceDpi);
        /// A details-view column whose width is a 96-dpi design value (a starting width: FitColumns sizes the columns from their content).
        public static void Col(ListView lv, string header, int designPx) => lv.Columns.Add(header, Px(lv, designPx));

        /// Controls built after the window loaded (a wizard page, a rebuilt pane) are not scaled by the framework: give them the monitor's scale.
        public static T LateScale<T>(T sub, Control owner) where T : Control
        {
            ApplyModernTheme(sub);
            var k = owner.DeviceDpi / 96f;
            if (Math.Abs(k - 1f) > 0.01f) sub.Scale(new SizeF(k, k));
            return sub;
        }

        /// Automatically styles standard WinForms controls (Button, NumericUpDown, ComboBox, TextBox, ListView) with our modern dark theme
        /// across all windows (launcher.exe, configure.exe, performance.exe, dialogs) while leaving custom controls (ModernButton, ModernSlider, etc.) untouched.
        public static void ApplyModernTheme(Control root)
        {
            if (root == null) return;
            if (root is Button b && !(root is ModernButton) && !(root is ToggleButton))
            {
                if (!Equals(b.Tag, "modern-styled"))
                {
                    b.Tag = "modern-styled";
                    bool isLinkLike = b.BackColor == Color.Transparent && b.FlatAppearance.BorderSize == 0 && b.Padding.Left == 0;
                    bool isColorPicker = b.Text != null && b.Text.StartsWith("#") && b.Text.Length == 7;
                    if (!isLinkLike && !isColorPicker)
                    {
                        bool isPrimary = b.Font.Bold || b.BackColor == Accent || b.BackColor == Color.FromArgb(14, 98, 178);
                        b.FlatStyle = FlatStyle.Flat;
                        b.FlatAppearance.BorderSize = 0;
                        b.UseVisualStyleBackColor = false;
                        b.Cursor = Cursors.Hand;
                        if (b.MinimumSize.Height < 28) b.MinimumSize = new Size(Math.Max(b.MinimumSize.Width, 68), 28);
                        if (b.Padding.Left < 8) b.Padding = new Padding(10, 4, 10, 4);
                        bool hover = false, down = false;
                        b.MouseEnter += (s, e) => { hover = true; b.Invalidate(); };
                        b.MouseLeave += (s, e) => { hover = false; down = false; b.Invalidate(); };
                        b.MouseDown += (s, e) => { down = true; b.Invalidate(); };
                        b.MouseUp += (s, e) => { down = false; b.Invalidate(); };
                        b.Paint += (s, e) =>
                        {
                            var g = e.Graphics;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                            g.Clear(b.Parent?.BackColor ?? DarkBg);
                            var rect = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                            bool navSelected = b.BackColor == Color.FromArgb(14, 98, 178) && b.TextAlign == ContentAlignment.MiddleLeft;
                            bool navButton = b.TextAlign == ContentAlignment.MiddleLeft && b.Width >= 180;
                            Color fill, border, txt;
                            if (navButton)
                            {
                                fill = navSelected ? Color.FromArgb(55, AccentLight.R, AccentLight.G, AccentLight.B) : (hover ? DarkHover : Color.FromArgb(0x13, 0x1B, 0x28));
                                border = navSelected ? AccentLight : (hover ? Color.FromArgb(0x30, 0x44, 0x62) : Color.Transparent);
                                txt = navSelected ? Color.White : (hover ? DarkText : DarkMuted);
                            }
                            else if (!b.Enabled)
                            {
                                fill = Color.FromArgb(0x13, 0x1A, 0x26);
                                border = Color.FromArgb(0x20, 0x2B, 0x3D);
                                txt = Color.FromArgb(0x55, 0x64, 0x78);
                            }
                            else if (down)
                            {
                                fill = Accent;
                                border = AccentLight;
                                txt = Color.White;
                            }
                            else if (isPrimary)
                            {
                                fill = hover ? AccentLight : Accent;
                                border = AccentLight;
                                txt = Color.White;
                            }
                            else
                            {
                                fill = hover ? Color.FromArgb(0x25, 0x34, 0x4D) : Color.FromArgb(0x1C, 0x26, 0x38);
                                border = hover ? AccentLight : Color.FromArgb(0x2D, 0x3E, 0x58);
                                txt = hover ? Color.White : DarkText;
                            }
                            using (var path = RoundRect(rect, 6))
                            {
                                using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                                if (border != Color.Transparent)
                                    using (var pen = new Pen(border, (isPrimary || hover || navSelected) ? 1.5f : 1f)) g.DrawPath(pen, path);
                            }
                            if (navSelected)
                            {
                                using (var pill = RoundRect(new Rectangle(4, 8, 4, b.Height - 16), 2))
                                using (var br = new SolidBrush(AccentLight))
                                    g.FillPath(br, pill);
                            }
                            var flags = (navButton ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                            var textRect = navButton ? new Rectangle(12, 0, b.Width - 16, b.Height) : new Rectangle(0, 0, b.Width, b.Height);
                            TextRenderer.DrawText(g, b.Text, (isPrimary || navSelected) ? Bold : SemiBold, textRect, txt, flags);
                        };
                    }
                }
            }
            else if (root is NumericUpDown nud)
            {
                nud.BackColor = Color.FromArgb(0x10, 0x17, 0x24);
                nud.ForeColor = DarkText;
                nud.BorderStyle = BorderStyle.FixedSingle;
                nud.Font = SemiBold;
            }
            else if (root is ComboBox cb)
            {
                cb.BackColor = Color.FromArgb(0x14, 0x1D, 0x2B);
                cb.ForeColor = DarkText;
                cb.FlatStyle = FlatStyle.Flat;
                cb.Font = SemiBold;
                if (cb.DropDownStyle == ComboBoxStyle.DropDownList && cb.DrawMode == DrawMode.Normal)
                {
                    cb.DrawMode = DrawMode.OwnerDrawFixed;
                    cb.ItemHeight = 20;
                    cb.DrawItem += (s, e) =>
                    {
                        if (e.Index < 0 || e.Index >= cb.Items.Count) return;
                        bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected && (e.State & DrawItemState.ComboBoxEdit) == 0;
                        var bg = sel ? Accent : Color.FromArgb(0x14, 0x1D, 0x2B);
                        using (var br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), SemiBold, new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, e.Bounds.Width - 6, e.Bounds.Height), sel ? Color.White : DarkText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    };
                }
            }
            else if (root is TextBox tb)
            {
                tb.BackColor = Color.FromArgb(0x10, 0x17, 0x24);
                tb.ForeColor = DarkText;
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.Font = SemiBold;
            }
            else if (root is ListView lv)
            {
                lv.BackColor = Color.FromArgb(0x11, 0x18, 0x24);
                lv.ForeColor = DarkText;
                lv.BorderStyle = BorderStyle.FixedSingle;
                if (!lv.OwnerDraw && !lv.CheckBoxes)
                {
                    lv.OwnerDraw = true;
                    lv.DrawColumnHeader += (s, e) =>
                    {
                        var fillRect = new Rectangle(e.Bounds.Left, e.Bounds.Top, lv.Width - e.Bounds.Left, e.Bounds.Height);
                        using (var br = new SolidBrush(Color.FromArgb(0x18, 0x23, 0x36))) e.Graphics.FillRectangle(br, fillRect);
                        using (var pen = new Pen(DarkBorder, 1f))
                        {
                            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, lv.Width, e.Bounds.Bottom - 1);
                            e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
                        }
                        TextRenderer.DrawText(e.Graphics, e.Header.Text, Bold, new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height), AccentLight, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    };
                    lv.DrawItem += (s, e) => { };
                    lv.DrawSubItem += (s, e) =>
                    {
                        bool sel = e.Item.Selected;
                        var bg = sel ? Color.FromArgb(65, AccentLight.R, AccentLight.G, AccentLight.B) : (e.ItemIndex % 2 == 0 ? Color.FromArgb(0x11, 0x18, 0x24) : Color.FromArgb(0x14, 0x1C, 0x2B));
                        using (var br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
                        var fg = e.Item.ForeColor != Color.Empty && e.Item.ForeColor != Color.Black && e.Item.ForeColor != DarkText ? e.Item.ForeColor : (sel ? Color.White : DarkText);
                        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, sel ? Bold : Body, new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height), fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    };
                }
            }
            foreach (Control child in root.Controls) ApplyModernTheme(child);
        }

        /// Column widths from what they hold (header and cells), then the widest shrunk so the list never needs sideways scrolling. Fonts and dpi
        /// change what fits, which fixed pixel widths cannot follow. Re-run when the items change; hooked to the list's resize once.
        public static void FitColumns(ListView lv)
        {
            if (lv.View != View.Details || lv.Columns.Count == 0) return;
            if (!lv.Tag?.Equals("fit-hooked") ?? true) { lv.Tag = "fit-hooked"; lv.SizeChanged += (s, e) => FitColumns(lv); lv.FontChanged += (s, e) => FitColumns(lv); lv.ParentChanged += (s, e) => FitColumns(lv); }   // the font it inherits is only known once it has a parent
            int pad = Px(lv, 16);                                  // the list's own padding around a header or a cell
            int n = lv.Columns.Count;
            var want = new int[n]; var floor = new int[n];
            for (int i = 0; i < n; i++)
            {
                floor[i] = want[i] = TextRenderer.MeasureText(lv.Columns[i].Text, lv.Font).Width + pad;
                foreach (ListViewItem it in lv.Items)
                    if (i < it.SubItems.Count) want[i] = Math.Max(want[i], TextRenderer.MeasureText(it.SubItems[i].Text, lv.Font).Width + pad + (i == 0 ? Px(lv, 6) : 0));
            }
            int avail = lv.ClientSize.Width - 2;
            if (avail > 0)
                while (want.Sum() > avail)
                {
                    int widest = 0;
                    for (int i = 1; i < n; i++) if (want[i] - floor[i] > want[widest] - floor[widest]) widest = i;
                    int cut = Math.Min(want.Sum() - avail, want[widest] - floor[widest]);
                    if (cut <= 0) break;                           // only headers left: sideways scrolling is all that remains
                    want[widest] -= cut;
                }
            lv.BeginUpdate();
            for (int i = 0; i < n; i++) if (lv.Columns[i].Width != want[i]) lv.Columns[i].Width = want[i];
            lv.EndUpdate();
        }
        public static Font Body = BrandFont(10.5f, FontStyle.Bold);
        public static Font SemiBold = BrandFont(10.5f, FontStyle.Bold);
        public static Font Bold = BrandFont(10.5f, FontStyle.Bold);
        public static Font Regular = BrandFont(10.5f, FontStyle.Regular);
        public static Font SectionTitle = BrandFont(11f, FontStyle.Bold);
        public static Font Big = BrandFont(12.5f, FontStyle.Bold);
        // DESIGN 2026 (banner.jpg) : obsidienne profonde, accents jaune électrique → orange flamboyant → rouge,
        // textes blanc pur, bordures fines lumineuses rouge/orange.
        public static readonly Color Accent = Color.FromArgb(0xE8, 0x44, 0x2A);      // Rouge flamboyant (accent principal)
        public static readonly Color AccentLight = Color.FromArgb(0xFF, 0x7A, 0x00); // Orange flamboyant (hover/bordures)
        public static readonly Color GlowYellow = Color.FromArgb(0xFF, 0xD5, 0x00);  // Jaune électrique (titres/lueur)
        public static readonly Color GlowOrange = Color.FromArgb(0xFF, 0x7A, 0x00);  // Orange flamboyant (dégradé bas)
        public static readonly Color GlowRed = Color.FromArgb(0xE8, 0x44, 0x2A);     // Rouge contrasté (contours)
        public static readonly Color DarkBg = Color.FromArgb(0x0B, 0x0E, 0x14);      // Fond principal ultra profond
        public static readonly Color DarkCard = Color.FromArgb(0x12, 0x17, 0x22);    // Surface des cartes
        public static readonly Color DarkCardTop = Color.FromArgb(0x16, 0x1C, 0x28); // Haut du dégradé des cartes
        public static readonly Color DarkHeroTop = Color.FromArgb(0x18, 0x1E, 0x2C); // Haut du dégradé de la carte principale
        public static readonly Color DarkHover = Color.FromArgb(0x1C, 0x24, 0x33);   // Surface survolée
        public static readonly Color DarkText = Color.FromArgb(0xF8, 0xFA, 0xFC);    // Texte primaire
        public static readonly Color DarkMuted = Color.FromArgb(0x94, 0xA3, 0xB8);   // Texte secondaire
        public static readonly Color DarkBorder = Color.FromArgb(0x23, 0x2D, 0x3F);  // Bordure au repos
        public static readonly Color Gold = Color.FromArgb(0xFF, 0xA8, 0x00);        // Or électrique
        public static readonly Color Emerald = Color.FromArgb(0x10, 0xB9, 0x81);     // Validation / actif
        public static readonly Color BadgeAmber = Color.FromArgb(0x26, 0xFF, 0x7A, 0x00); // Badge d'icône (ambre 15 %)

        /// Dark renderer for the preset dropdowns (launcher, configure): the submenus inherit it, so they get the same
        /// dark background and border as the parent menu instead of the default light toolstrip look.
        /// Rendu du menu POLICE : chaque entree est dessinee dans la police de son `Tag` (aperçu du menu).
        public sealed class FontPreviewRenderer : ToolStripProfessionalRenderer
        {
            // rendu systeme par defaut (menu clair) : le fond et le survol restent ceux de Windows
            public FontPreviewRenderer() : base() { }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                var family = e.Item?.Tag as string;
                if (!string.IsNullOrEmpty(family))
                {
                    var f = PreviewFont(family, "bold", 10.5f);
                    var rect = e.TextRectangle;
                    rect.Offset(0, (rect.Height - f.Height) / 2);
                    TextRenderer.DrawText(e.Graphics, e.Text, f, rect, e.TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    return;
                }
                base.OnRenderItemText(e);
            }
        }

        public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkMenuColors()) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected)
                {
                    var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                    using (var path = RoundRect(rect, 4))
                    using (var b = new SolidBrush(Accent))
                        e.Graphics.FillPath(b, path);
                }
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(0x14, 0x1C, 0x2B)))
                        e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
                }
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var p = new Pen(DarkBorder, 1.5f))
                    e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
            // A submenu (ToolStripDropDownMenu) paints its own background through OnRenderToolStripBackground; without
            // this override it kept the default light strip, drawn as a detached panel next to the dark parent menu.
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var b = new SolidBrush(Color.FromArgb(0x14, 0x1C, 0x2B)))
                    e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.ToolStrip.Size));
            }
        }

        sealed class DarkMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(0x14, 0x1C, 0x2B);
            public override Color ImageMarginGradientBegin => Color.FromArgb(0x14, 0x1C, 0x2B);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(0x14, 0x1C, 0x2B);
            public override Color ImageMarginGradientEnd => Color.FromArgb(0x14, 0x1C, 0x2B);
            public override Color MenuBorder => DarkBorder;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuItemSelected => Accent;
        }

        public static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Image Resource(string name)
        {
            using (var s = typeof(Ui).Assembly.GetManifestResourceStream(name)) return s == null ? null : Image.FromStream(s);
        }

        public static Icon AppIcon() { try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { return null; } }

        /// Police embarquée (ex. Orbitron) chargée en mémoire, sans installation système.
        static FontFamily _brandFamily;
        static PrivateFontCollection _brandPfc;   // garde la collection vivante (sinon GDI+ perd la police)
        public static FontFamily BrandFamily()
        {
            if (_brandFamily != null) return _brandFamily;
            try
            {
                using (var s = typeof(Ui).Assembly.GetManifestResourceStream("Orbitron.ttf"))
                    if (s != null)
                    {
                        var pfc = new PrivateFontCollection();
                        var buf = new byte[s.Length];
                        s.Read(buf, 0, buf.Length);
                        var h = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try { pfc.AddMemoryFont(h.AddrOfPinnedObject(), buf.Length); }
                        finally { h.Free(); }
                        // famille « Orbitron Black » (graisse la plus épaisse) si présente, sinon la première
                        _brandFamily = pfc.Families.FirstOrDefault(f => f.Name.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (_brandFamily == null && pfc.Families.Length > 0) _brandFamily = pfc.Families[0];
                        _brandPfc = pfc;
                    }
            }
            catch { }
            if (_brandFamily == null) _brandFamily = new FontFamily("Segoe UI");
            return _brandFamily;
        }

        // ---- menu POLICE : familles embarquées (res\fonts) + graisses, choix mémorisé dans config\auraflashxr.json ----
        /// Familles proposées par le menu POLICE et le fichier de chaque graisse (ressource embarquée).
        public static readonly string[] FontFamilies = { "Orbitron", "Montserrat", "CAS Astro Voyager", "Flymax", "Savore", "Yotsuba", "Arial" };
        public static readonly string[] WeightKeys = { "regular", "semibold", "bold", "extrabold" };
        static readonly Dictionary<string, string[]> _fontFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Orbitron", new[] { "fonts/Orbitron-Regular.ttf", "fonts/Orbitron-SemiBold.ttf", "fonts/Orbitron-Bold.ttf", "fonts/Orbitron-ExtraBold.ttf" } },
            { "Montserrat", new[] { "fonts/Montserrat-Regular.otf", "fonts/Montserrat-SemiBold.otf", "fonts/Montserrat-Bold.otf", "fonts/Montserrat-ExtraBold.otf" } },
            { "CAS Astro Voyager", new[] { "fonts/CAS-Astro-Voyager.otf", "fonts/CAS-Astro-Voyager.otf", "fonts/CAS-Astro-Voyager.otf", "fonts/CAS-Astro-Voyager.otf" } },
            { "Flymax", new[] { "fonts/Flymax.ttf", "fonts/Flymax.ttf", "fonts/Flymax.ttf", "fonts/Flymax.ttf" } },
            { "Savore", new[] { "fonts/Savore.ttf", "fonts/Savore.ttf", "fonts/Savore.ttf", "fonts/Savore.ttf" } },
            { "Yotsuba", new[] { "fonts/Yotsuba.ttf", "fonts/Yotsuba.ttf", "fonts/Yotsuba.ttf", "fonts/Yotsuba.ttf" } },
        };
        static readonly Dictionary<string, PrivateFontCollection> _fontCache = new Dictionary<string, PrivateFontCollection>(StringComparer.OrdinalIgnoreCase);
        static string _fontFamily = "Orbitron", _fontWeight = "bold";

        /// Famille chargée depuis un fichier embarqué (une collection par fichier : les graisses d'une même
        /// famille partagent le même nom GDI+, seule la collection d'origine distingue le fichier).
        static readonly Dictionary<string, FontFamily> _fileFamily = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);
        static FontFamily FamilyFromFile(string file)
        {
            FontFamily fam;
            if (_fileFamily.TryGetValue(file, out fam)) return fam;
            try
            {
                using (var s = typeof(Ui).Assembly.GetManifestResourceStream(file))
                    if (s != null)
                    {
                        var pfc = new PrivateFontCollection();
                        var buf = new byte[s.Length];
                        int read = 0;
                        while (read < buf.Length) { int n = s.Read(buf, read, buf.Length - read); if (n <= 0) break; read += n; }
                        var h = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try { pfc.AddMemoryFont(h.AddrOfPinnedObject(), buf.Length); }
                        finally { h.Free(); }
                        if (pfc.Families.Length > 0) fam = pfc.Families[0];
                        _fontCache[file] = pfc;   // garde la collection vivante
                    }
            }
            catch { }
            if (fam == null) fam = new FontFamily("Arial");
            _fileFamily[file] = fam;
            return fam;
        }

        /// Famille GDI+ de la graisse demandée ; repli sur la première famille du fichier.
        static FontFamily FamilyOf(string family, string weight)
        {
            // police système (Arial) : pas de fichier embarqué
            if (!_fontFiles.ContainsKey(family))
            {
                try { return new FontFamily(string.IsNullOrEmpty(family) ? "Arial" : family); }
                catch { return new FontFamily("Arial"); }
            }
            int idx = Array.IndexOf(WeightKeys, weight);
            if (idx < 0) idx = 2;
            string[] files;
            if (_fontFiles.TryGetValue(family, out files) && files.Length > 0)
                return FamilyFromFile(files[Math.Min(idx, files.Length - 1)]);
            return BrandFamily();
        }

        /// Police de l'interface : famille + graisse choisies dans le menu POLICE.
        public static Font BrandFont(float size, FontStyle style) => BrandFont(size, style, _fontFamily, _fontWeight);
        public static Font BrandFont(float size, FontStyle requested, string family, string weight)
        {
            try
            {
                var fam = FamilyOf(string.IsNullOrEmpty(family) ? "Orbitron" : family, string.IsNullOrEmpty(weight) ? "bold" : weight);
                return new Font(fam, size, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch { return new Font("Segoe UI", size, requested, GraphicsUnit.Point); }
        }

        /// Aperçu du menu POLICE : la police demandée dans la graisse demandée.
        public static Font PreviewFont(string family, string weight, float size)
        {
            // Arial : police système, la graisse passe par FontStyle (Regular / Bold)
            if (!_fontFiles.ContainsKey(family))
            {
                try { return new Font(family, size, weight == "regular" ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Point); }
                catch { return new Font("Arial", size, FontStyle.Regular, GraphicsUnit.Point); }
            }
            return BrandFont(size, FontStyle.Bold, family, weight);
        }

        /// Charge le choix enregistré (appelé au démarrage du launcher, avant toute création de contrôle).
        public static void LoadFontChoice()
        {
            try { var st = new AppSettings(); SetFontChoice(st.FontFamily, st.FontWeight); } catch { }
        }

        /// Applique une police et une graisse ; "" = défaut (Orbitron / gras). Ne fait rien si le choix est identique.
        public static void SetFontChoice(string family, string weight)
        {
            var f = string.IsNullOrEmpty(family) ? "Orbitron" : family;
            var w = string.IsNullOrEmpty(weight) ? "bold" : weight.ToLowerInvariant();
            if (Array.IndexOf(WeightKeys, w) < 0) w = "bold";
            if (f == _fontFamily && w == _fontWeight) return;
            _fontFamily = f; _fontWeight = w;
            Body = BrandFont(10.5f, FontStyle.Bold); SemiBold = BrandFont(10.5f, FontStyle.Bold); Bold = BrandFont(10.5f, FontStyle.Bold);
            Regular = BrandFont(10.5f, FontStyle.Regular);
            SectionTitle = BrandFont(11f, FontStyle.Bold); Big = BrandFont(12.5f, FontStyle.Bold);
        }

        public static Icon ResourceIcon(string name)
        {
            try
            {
                using (var s = typeof(Ui).Assembly.GetManifestResourceStream(name))
                    return s == null ? null : new Icon(s, new Size(256, 256));
            }
            catch { return null; }
        }

        /// A short header (small logo and the window's name with the version) for windows that must fit a laptop screen.
        /// Layout of the launcher's header: logo and name on the left, PRE-RELEASE and the window's name in pills, the
        /// window buttons on the right; the bar drags the window (the form is borderless).
        public static Control CompactHeader(Form form, string subtitle)
        {
            const int H = 64;   // header plus haut pour le logo agrandi (jamais rogne)
            var p = new Panel { Dock = DockStyle.Top, Height = H, BackColor = DarkBg };
            p.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                // logo à gauche, à sa taille naturelle (jamais rogné), centré verticalement
                // logo allonge du streamer (nouveau visuel), repli sur logo_640
                var logo = Resource("Logo-allonger.png") ?? Resource("logo_640.png");
                int x = 14;
                if (logo != null)
                {
                    float lh = (H - 8) * 1.44f, lw = logo.Width * (lh / logo.Height);   // +20 % sur la taille deja agrandie
                    g.DrawImage(logo, x, (H - lh) / 2f, lw, lh);
                    x += (int)lw + 10;
                }
                // pastilles à droite : PRE-RELEASE puis le nom de la fenêtre · V0
                using (var bf = BrandFont(10.5f, FontStyle.Bold))
                {
                    var labels = new[] { Version.Number.ToUpperInvariant(), subtitle + "  ·  " + Version.AppVersion };
                    var sizes = new int[labels.Length];
                    int total = 0;
                    for (int i = 0; i < labels.Length; i++) { sizes[i] = (int)g.MeasureString(labels[i], bf).Width + 30; total += sizes[i] + (i > 0 ? 10 : 0); }
                    float bx = p.Width - 76 - total;
                    for (int li = 0; li < labels.Length; li++)
                    {
                        var r = new Rectangle((int)bx, (H - 30) / 2, sizes[li], 30);
                        using (var path = RoundRect(r, 15))
                        {
                            for (int i = 4; i >= 1; i--)
                                using (var glow = new Pen(Color.FromArgb(10 + (5 - i) * 5, 0xFF, 0x8A, 0x1E), i * 2.6f))
                                using (var gp = RoundRect(Rectangle.Inflate(r, i, i), 15 + i))
                                    g.DrawPath(glow, gp);
                            using (var fill = new SolidBrush(Color.FromArgb(204, 0x10, 0x1A, 0x30))) g.FillPath(fill, path);
                            using (var bp = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(0xFF, 0xC7, 0x4A), Color.FromArgb(0xF5, 0x7C, 0x10), System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                            using (var pen = new Pen(bp, 1.4f)) g.DrawPath(pen, path);
                        }
                        TextRenderer.DrawText(g, labels[li], bf, r, Color.FromArgb(0xFF, 0x9E, 0x3C), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        bx = r.Right + 10;
                    }
                }
                // liseré dégradé en bas du header
                using (var glow = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, H - 3, p.Width, 2), GlowYellow, GlowRed, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                    g.FillRectangle(glow, 0, H - 3, p.Width, 2);
            };
            // boutons Fenêtre (réduire / fermer), dessinés par le header
            var min = new CaptionGlyph(false) { Bounds = new Rectangle(0, 0, 24, 24), TabStop = false, Cursor = Cursors.Hand };
            var close = new CaptionGlyph(true) { Bounds = new Rectangle(0, 0, 24, 24), TabStop = false, Cursor = Cursors.Hand };
            min.Click += (s, e) => form.WindowState = FormWindowState.Minimized;
            close.Click += (s, e) => form.Close();
            p.Controls.Add(min);
            p.Controls.Add(close);
            Action place = () =>
            {
                close.Left = p.Width - 14 - close.Width;
                close.Top = (H - close.Height) / 2;
                min.Left = close.Left - 8 - min.Width;
                min.Top = close.Top;
            };
            p.Resize += (s, e) => { place(); p.Invalidate(); };
            place();
            // barre de titre : glisser pour déplacer la fenêtre
            Point dragStart = Point.Empty;
            bool dragging = false;
            p.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; } };
            p.MouseMove += (s, e) => { if (dragging) form.Location = new Point(form.Location.X + e.X - dragStart.X, form.Location.Y + e.Y - dragStart.Y); };
            p.MouseUp += (s, e) => dragging = false;
            return p;
        }

        /// Bouton Fenêtre dessiné (réduire / fermer) pour les fenêtres sans bordure.
        sealed class CaptionGlyph : Button
        {
            readonly bool isClose;
            bool hover;
            public CaptionGlyph(bool close)
            {
                isClose = close;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
            }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(DarkBg);
                var c = hover ? Color.White : Color.FromArgb(0xC8, 0xD2, 0xE0);
                using (var pen = new Pen(c, 1.5f))
                {
                    int cx = Width / 2, cy = Height / 2;
                    if (isClose) { g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5); g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5); }
                    else g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                }
            }
        }

        /// DESIGN 2026 : un Label dont le texte est peint avec un dégradé jaune électrique → orange flamboyant (effet néon).
        public class GradientTitleLabel : Label
        {
            public GradientTitleLabel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Ui.DarkCard;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(BackColor);
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, GlowYellow, GlowOrange, 0f))
                    g.DrawString(Text, Font, brush, new PointF(-2, -2));
            }
            protected override void OnPaintBackground(PaintEventArgs e) { }
        }

        /// Testing aid: `--screenshot <png>` renders the window into a file once it is shown, then closes it.
        public static string ScreenshotPath()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 1; i + 1 < a.Length; i++) if (a[i] == "--screenshot") return a[i + 1];
            return null;
        }

        public static void ScreenshotWhenShown(Form f, int delayMs, Action before = null, string suffix = "")
        {
            var path = ScreenshotPath();
            if (path == null) return;
            path += suffix;
            var t = new System.Windows.Forms.Timer { Interval = delayMs };
            t.Tick += (s, e) =>
            {
                t.Stop();
                before?.Invoke();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                f.Close();
            };
            f.Shown += (s, e) => t.Start();
        }

        /// A collapsed section: a flat "> title" button that shows or hides its content. For what most people never need (progressive disclosure).
        public static Control Expander(string title, Control content, bool open = false, Action onToggle = null)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            var b = new Button { FlatStyle = FlatStyle.Flat, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = Bold, Cursor = Cursors.Hand, Margin = new Padding(0), BackColor = Color.Transparent, TabStop = true, AccessibleName = title };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xE6, 0xEA, 0xF2);
            Action paint = () => { b.Text = (content.Visible ? "\u25BE  " : "\u25B8  ") + title; };
            content.Visible = open; paint();
            b.Click += (s, e) => { content.Visible = !content.Visible; paint(); onToggle?.Invoke(); };
            p.Controls.Add(b); p.Controls.Add(content);
            return p;
        }

        /// A statistics tile: small title, a big value, a one-line note. Sizes from its text (no fixed width), so it follows the display scale.
        public static Tile MakeTile(string title, string value = "-", string note = "", Color? color = null) { var t = new Tile(title, value, note); if (color != null) t.Set(value, note, color); return t; }

        /// A small coloured tag next to a label (instead of a suffix in its text).
        public static Label Badge(string text) => new Label { Text = text, AutoSize = true, ForeColor = Color.DarkOrange, Font = new Font(Body.FontFamily, 8.5f, FontStyle.Bold), Margin = new Padding(0, 6, 3, 0) };

        public static Label Line(string text, Font f = null) =>
            new Label { Text = text, AutoSize = true, Font = f ?? Body, ForeColor = DarkText, Margin = new Padding(3, 3, 3, 3), MaximumSize = new Size(760, 0) }; // long lines wrap (narrower than the page: it may also have a scroll bar)

        /// DESIGN 2026 : une bulle de statut arrondie — pastille de couleur d'état à gauche, texte complet dedans.
        /// Remplace les lignes de texte brut (état du casque, jeu, runtime, encodeur) : plus lisible, zéro texte nu.
        public class StatusBubble : FlowLayoutPanel
        {
            readonly Label dot = new Label { AutoSize = false, Size = new Size(8, 8), Margin = new Padding(0, 3, 6, 0) };
            readonly Label text = new Label { AutoSize = true, ForeColor = DarkText, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0), BackColor = Color.Transparent, MaximumSize = new Size(700, 0) };
            /// badge optionnel à droite (ex : « LOW CONFIG · LECTURE SEULE »), caché tant qu'il est vide
            readonly Label badge = new Label { AutoSize = true, Visible = false, Margin = new Padding(10, 2, 0, 0), Padding = new Padding(8, 2, 8, 3), BackColor = Color.FromArgb(66, 44, 10), ForeColor = Color.FromArgb(255, 200, 100), Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            Color dotColor = AccentLight;

            public StatusBubble(string initial = "", Color? state = null)
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = DarkCard;
                Paint += PaintBubble;
                FlowDirection = FlowDirection.LeftToRight;
                WrapContents = false;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Padding = new Padding(10, 1, 12, 1);
                Margin = new Padding(0, 1, 0, 1);
                dot.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(dotColor)) g.FillEllipse(b, 0, 1, 7, 7);
                };
                Controls.Add(dot);
                Controls.Add(text);
                Controls.Add(badge);
                if (initial.Length > 0) text.Text = initial;
                if (state != null) dotColor = state.Value;
            }

            /// Affiche (ou masque si vide) le badge à droite de la bulle (ex : preset chargé).
            public void SetBadge(string t)
            {
                badge.Text = t;
                badge.Visible = t.Length > 0;
                Invalidate();
            }

            public override string Text { get => text.Text; set { text.Text = value; Invalidate(); } }
            public override Color ForeColor { get => text.ForeColor; set { text.ForeColor = value; Invalidate(); } }

            /// Largeur maximale du texte : au-dela, il revient a la ligne (la bulle reste dans sa case).
            public int TextMaxWidth { get => text.MaximumSize.Width; set { text.MaximumSize = new Size(value, 0); } }

            /// Change la couleur de la pastille d'état (vert/orange/rouge) sans toucher au texte.
            public void SetState(Color color) { dotColor = color; dot.Invalidate(); Invalidate(); }

            /// Texte + couleur d'état en un appel (miroir de SetLink).
            public void Set(string message, Color color) { text.Text = message; dotColor = color; dot.Invalidate(); Invalidate(); }

            // le fond arrondi est peint par l'event Paint (un FlowLayoutPanel ne passe pas par OnPaint d'une classe dérivée quand les styles UserPaint sont absents)
            void PaintBubble(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = RoundRect(rect, 7))
                {
                    using (var b = new SolidBrush(DarkCard)) g.FillPath(b, path);
                    using (var p = new Pen(DarkBorder, 1f)) g.DrawPath(p, path);
                }
            }
        }


        /// <summary>Small hint text line displayed below a card, full width, muted color.</summary>
        public static Label HintLine(string text) =>
            new Label { Text = text, AutoSize = false, Font = new Font(Body.FontFamily, 8f, FontStyle.Italic), ForeColor = Color.FromArgb(140, 160, 180), Margin = new Padding(14, 0, 14, 8), Padding = new Padding(0), Height = 18, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
    }
}