// Standalone settings editor (configure.exe). Wraps SettingsPanel in a window and dispatches CLI modes (--switches, --settings, --diagnose, wizard).
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
    public class ConfigureForm : DpiForm
    {
        readonly AppSettings settings = new AppSettings();
        Dictionary<string, object> session = Session.Load();
        // tracks which config profile is loaded in the editor (MON PRESET by default).
        // Used to show a popup when a config is loaded and to know where to save.
        string loadedProfileName = Preset.DefaultName;
        bool isReadOnlyProfile = false;
        // last active-preset value seen in config\preset.cfg ("" = MON PRESET); the poll timer
        // compares against it to detect preset changes made in the launcher CONFIGURATION dropdown.
        string lastSyncedActive = "";
        // bottom-bar bubble showing which configuration is currently loaded
        // (replaces the old "Importer des parametres" button).
        readonly Label configBubble = new Label { AutoSize = true, Font = Ui.Bold, Padding = new Padding(10, 7, 10, 7), Margin = new Padding(0, 4, 8, 4), BackColor = Color.FromArgb(14, 50, 38), ForeColor = Color.FromArgb(120, 255, 180), Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
        readonly Dictionary<string, object> check;
        Discovery discovery;
        readonly ListView found = new ListView { View = View.Details, FullRowSelect = true, Height = 160, Width = 680, HideSelection = false };
        // la derniere colonne du ListView remplit la largeur restante (aucune zone vide dans l'en-tete)
        Action fillLastColumn = () => { };
        readonly Label paired = Ui.Line("", Ui.Bold);
        readonly Label discStatus = Ui.Line("");
        readonly Dictionary<string, DateTime> seen = new Dictionary<string, DateTime>();

        readonly NumericUpDown eyeW = Num(256, Nvenc.MaxEyeWidth, 32), eyeH = Num(256, Nvenc.MaxEyeHeight, 32), bitrate = Num(10, 1000, 10);
        readonly ComboBox fps = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        readonly RadioButton profFov = new RadioButton { Text = Lang.Get("Settings.Check.Foveated"), AutoSize = true };
        readonly RadioButton profFull = new RadioButton { Text = Lang.Get("Settings.Check.FullSplit"), AutoSize = true };
        readonly NumericUpDown fcx = Dec(0.1m, 1m), fcy = Dec(0.1m, 1m), fsx = Dec(-1m, 1m), fsy = Dec(-1m, 1m), frx = Dec(1m, 10m), fry = Dec(1m, 10m);
        readonly Button idle = new Button { Width = 90, Height = 24, FlatStyle = FlatStyle.Flat };
        readonly CheckBox chromaOn = new CheckBox { Text = Lang.Get("Settings.Check.ChromaKey"), AutoSize = true };
        readonly CheckBox audio = new CheckBox { Text = Lang.Get("Settings.Check.GameAudio"), AutoSize = true };
        readonly CheckBox mute = new CheckBox { Text = Lang.Get("Settings.Check.MuteSpeakers"), AutoSize = true };
        readonly ComboBox captureBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        readonly ComboBox audioPlaybackDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
        readonly CheckBox mic = new CheckBox { Text = Lang.Get("Settings.Check.MicEnabled"), AutoSize = true };
        readonly ComboBox audioMicDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
        readonly Button btnInstallMic = new Button
        {
            Text = Lang.Get("Settings.Audio.InstallMic"),
            AutoSize = false,
            Size = new Size(420, 36),
            BackColor = Ui.Accent,
            ForeColor = Color.White,
            Font = Ui.Bold,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 4, 0, 4)
        };
        readonly Button btnUninstallMic = new Button
        {
            Text = Lang.Get("Settings.Audio.UninstallMic"),
            AutoSize = false,
            Size = new Size(420, 36),
            BackColor = Color.FromArgb(178, 34, 34),
            ForeColor = Color.White,
            Font = Ui.Bold,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 4, 0, 4)
        };
        readonly ComboBox planBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        List<PowerPlan.Plan> plans = new List<PowerPlan.Plan>();
        readonly ComboBox proto = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        readonly NumericUpDown packet = Num(500, 4000, 100);
        readonly NumericUpDown haptics = Dec(0m, 5m);
        readonly CheckBox debugDefault = new CheckBox { Text = Lang.Get("Settings.Check.DebugLog"), AutoSize = true };

        // Nouvelles options Réseau
        readonly CheckBox netDscp = new CheckBox { Text = Lang.Get("Settings.Check.Dscp"), AutoSize = true };
        readonly CheckBox netAutoTrust = new CheckBox { Text = Lang.Get("Settings.Check.AutoTrust"), AutoSize = true };
        readonly CheckBox netAvoidGlitching = new CheckBox { Text = Lang.Get("Settings.Check.AvoidGlitching"), AutoSize = true };

        // Nouvelles options Vid\u00E9o & Encodage NVENC
        readonly ComboBox preferredCodec = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        // PyroWave codec (Vulkan compute, intra-only, packet-loss resilient) - settings live in auraflashxr.json -> video.pyrowave
        readonly CheckBox pyroEnabled = new CheckBox { AutoSize = true };
        readonly ComboBox pyroChroma = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly NumericUpDown pyroMaxKb = Num(50, 2000, 50);
        readonly NumericUpDown pyroPacketSize = Num(512, 65000, 256);
        readonly CheckBox use10Bit = new CheckBox { Text = Lang.Get("Settings.Check.Use10Bit"), AutoSize = true };
        readonly CheckBox serverOverrides10Bit = new CheckBox { Text = Lang.Get("Settings.Check.Server10Bit"), AutoSize = true };
        readonly CheckBox useFullRange = new CheckBox { Text = Lang.Get("Settings.Check.FullRange"), AutoSize = true };
        readonly CheckBox serverOverridesFullRange = new CheckBox { Text = Lang.Get("Settings.Check.ServerFullRange"), AutoSize = true };
        readonly NumericUpDown encodingGamma = Dec(0.1m, 5.0m);
        readonly CheckBox serverOverridesEncodingGamma = new CheckBox { Text = Lang.Get("Settings.Check.ServerGamma"), AutoSize = true };
        readonly CheckBox fillerData = new CheckBox { Text = Lang.Get("Settings.Check.FillerData"), AutoSize = true };
        readonly ComboBox encoderQualityPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };

        // Paramètres Bitrate & Débit adaptatif
        readonly ComboBox bitrateMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        readonly NumericUpDown adaptiveMaxThroughput = Num(10, 1000, 10);
        readonly CheckBox adaptiveMaxThroughputEnabled = new CheckBox { Text = Lang.Get("Settings.Check.MaxThroughput"), AutoSize = true };
        readonly NumericUpDown adaptiveMinThroughput = Num(1, 100, 5);
        readonly CheckBox adaptiveMinThroughputEnabled = new CheckBox { Text = Lang.Get("Settings.Check.MinThroughput"), AutoSize = true };
        readonly NumericUpDown adaptiveMaxLatency = Num(1, 100, 1);
        readonly CheckBox adaptiveMaxLatencyEnabled = new CheckBox { Text = Lang.Get("Settings.Check.MaxLatency"), AutoSize = true };
        readonly NumericUpDown adaptiveHistorySize = Num(32, 2048, 32);
        readonly CheckBox adaptiveImageCorruptionFix = new CheckBox { Text = Lang.Get("Settings.Check.CorruptionFix"), AutoSize = true };
        readonly CheckBox adaptToFramerate = new CheckBox { Text = Lang.Get("Settings.Check.AdaptFramerate"), AutoSize = true };
        readonly NumericUpDown maxBufferingFrames = Dec(0.5m, 10.0m);
        readonly NumericUpDown bufferingHistoryWeight = Dec(0.1m, 1.0m);

        // Fovéation & Décodage
        readonly CheckBox foveatedForceEnable = new CheckBox { Text = Lang.Get("Settings.Check.ForceFoveated"), AutoSize = true };
        readonly ComboBox h264Profile = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly ComboBox rateControlMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly ComboBox nvencAqMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        readonly CheckBox enableVbaq = new CheckBox { Text = Lang.Get("Settings.Check.Vbaq"), AutoSize = true };
        readonly CheckBox forceSoftwareDecoder = new CheckBox { Text = Lang.Get("Settings.Check.SwDecoder"), AutoSize = true };

        // Options NVENC avancées
        readonly NumericUpDown nvencLowDelayKeyFrameScale = Num(-1, 100, 1);
        readonly CheckBox nvencEnableIntraRefresh = new CheckBox { Text = Lang.Get("Settings.Check.IntraRefresh"), AutoSize = true };
        readonly NumericUpDown nvencIntraRefreshPeriod = Num(-1, 500, 1);
        readonly NumericUpDown nvencIntraRefreshCount = Num(-1, 500, 1);
        readonly NumericUpDown nvencMaxNumRefFrames = Num(-1, 16, 1);
        readonly NumericUpDown nvencGopLength = Num(-1, 500, 1);
        readonly NumericUpDown nvencPFrameStrategy = Num(-1, 5, 1);
        readonly NumericUpDown nvencRcBufferSize = Num(-1, 100000000, 10000);
        readonly NumericUpDown nvencRcInitialDelay = Num(-1, 100000000, 10000);
        readonly NumericUpDown nvencRcMaxBitrate = Num(-1, 1000000000, 10000);
        readonly NumericUpDown nvencRcAverageBitrate = Num(-1, 1000000000, 10000);
        readonly CheckBox nvencWeightedPrediction = new CheckBox { Text = Lang.Get("Settings.Check.WeightedPrediction"), AutoSize = true };

        // HDR
        readonly CheckBox hdrEnabled = new CheckBox { Text = Lang.Get("Settings.Check.HdrEnabled"), AutoSize = true };
        readonly CheckBox hdrServerOverrides = new CheckBox { Text = Lang.Get("Settings.Check.HdrServer"), AutoSize = true };
        readonly CheckBox hdrForceSrgb = new CheckBox { Text = Lang.Get("Settings.Check.HdrSrgb"), AutoSize = true };
        readonly CheckBox hdrClampRange = new CheckBox { Text = Lang.Get("Settings.Check.HdrClamp"), AutoSize = true };

        // Fovéation côté client & Post-traitement casque
        readonly CheckBox clientFoveationEnabled = new CheckBox { Text = Lang.Get("Settings.Check.ClientFoveation"), AutoSize = true };
        readonly ComboBox clientFoveationMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly ComboBox clientFoveationLevel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        readonly NumericUpDown clientFoveationOffset = Dec(-45m, 45m);
        readonly CheckBox clientPostProcessingEnabled = new CheckBox { Text = Lang.Get("Settings.Check.ClientPost"), AutoSize = true };
        readonly ComboBox clientPostSuperSampling = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly ComboBox clientPostSharpening = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };

        // Passthrough / Incrustation étendue
        readonly ComboBox passthroughMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly NumericUpDown passthroughBlendThreshold = Dec(0.0m, 1.0m);
        readonly NumericUpDown passthroughDistanceThreshold = Num(0, 500, 5);
        readonly NumericUpDown passthroughFeathering = Dec(0.0m, 2.0m);

        // Options avancées intégrées (provenant de Paramètres avancés)
        readonly ComboBox splitEncode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly NumericUpDown sliceCount = Num(1, 8, 1);
        readonly ComboBox entropyCoding = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly ComboBox nvencQualityPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly ComboBox nvencTuningPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        readonly ComboBox nvencMultiPass = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        readonly CheckBox vbvStrict = new CheckBox { Text = Lang.Get("Settings.Check.VbvStrict"), AutoSize = true };
        readonly CheckBox qpMap = new CheckBox { Text = Lang.Get("Settings.Check.QpMap"), AutoSize = true };
        readonly NumericUpDown gpuSchedClass = Num(0, 5, 1);
        readonly NumericUpDown bitrateHeadroomPct = Num(50, 100, 5);

        // Options avancées de Cadencement & Pacing (provenant de Paramètres avancés)
        readonly CheckBox phaseLock = new CheckBox { Text = Lang.Get("Settings.Check.PhaseLock"), AutoSize = true };
        readonly NumericUpDown boundaryOffsetMs = Dec(-20m, 20m);
        readonly NumericUpDown runningStartMs = Dec(0m, 10m);
        readonly CheckBox intakeGate = new CheckBox { Text = Lang.Get("Settings.Check.IntakeGate"), AutoSize = true };
        readonly NumericUpDown intakeDepth = Num(1, 3, 1);
        readonly ComboBox pickPolicy = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly CheckBox clientQueueDrain = new CheckBox { Text = Lang.Get("Settings.Check.QueueDrain"), AutoSize = true };
        readonly CheckBox followClientRate = new CheckBox { Text = Lang.Get("Settings.Check.FollowRate"), AutoSize = true };
        readonly ComboBox sendPacing = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly CheckBox frameStartGate = new CheckBox { Text = Lang.Get("Settings.Check.FrameStartGate"), AutoSize = true };
        const string GateKey = @"SOFTWARE\VirtualDesktop-OpenXR", GateValue = "ovrshim_vdxr_running_start";

        // Options avancées de Génération d'images SSW / Uncapped
        readonly CheckBox uncappedMode = new CheckBox { Text = Lang.Get("Settings.Check.Uncapped"), AutoSize = true };
        readonly NumericUpDown frameGenCapFps = Num(0, 240, 5);
        readonly NumericUpDown frameGenLagMs = Num(0, 100, 1);
        readonly NumericUpDown frameGenFlowScale = Dec(0.1m, 1.0m);
        readonly ComboBox frameGenPerf = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly ComboBox frameGenSynthesis = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly CheckBox frameGenHoldAge = new CheckBox { Text = Lang.Get("Settings.Check.HoldAge"), AutoSize = true };

        // Nouvelles options Cadencement & Suivi
        readonly CheckBox serverFramePacing = new CheckBox { Text = Lang.Get("Settings.Check.ServerPacing"), AutoSize = true };

        // Options Onglet Manettes & Casque (Contrôleurs, Haptique, Offsets, Squelette, Recenter, Tracking)
        readonly ComboBox posRecenteringMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly NumericUpDown posRecenterViewHeight = Dec(0.5m, 2.5m);
        readonly ComboBox rotRecenteringMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };

        readonly CheckBox ctrlEnabled = new CheckBox { Text = Lang.Get("Settings.Check.Controllers"), AutoSize = true };
        readonly CheckBox ctrlTracked = new CheckBox { Text = Lang.Get("Settings.Check.ControllersTracked"), AutoSize = true };
        readonly CheckBox handSkeleton = new CheckBox { Text = Lang.Get("Settings.Check.HandSkeleton"), AutoSize = true };
        readonly CheckBox handSkeletonInput2 = new CheckBox { Text = Lang.Get("Settings.Check.HandSkeletonInput2"), AutoSize = true };
        readonly CheckBox handSkeletonPredict = new CheckBox { Text = Lang.Get("Settings.Check.HandSkeletonPredict"), AutoSize = true };
        readonly CheckBox multimodalTracking = new CheckBox { Text = Lang.Get("Settings.Check.Multimodal"), AutoSize = true };

        readonly CheckBox handInteraction = new CheckBox { Text = Lang.Get("Settings.Check.HandInteraction"), AutoSize = true };
        readonly CheckBox handOnlyTouch = new CheckBox { Text = Lang.Get("Settings.Check.HandOnlyTouch"), AutoSize = true };
        readonly NumericUpDown pinchTouchDist = Dec(0.0m, 5.0m);
        readonly NumericUpDown pinchTriggerDist = Dec(0.0m, 5.0m);
        readonly NumericUpDown curlTouchDist = Dec(0.0m, 5.0m);
        readonly NumericUpDown curlTriggerDist = Dec(0.0m, 5.0m);
        readonly NumericUpDown joyDeadzone = Dec(0.0m, 100.0m);
        readonly NumericUpDown joyOffsetH = Dec(-1.0m, 1.0m);
        readonly NumericUpDown joyOffsetV = Dec(-1.0m, 1.0m);
        readonly NumericUpDown joyRange = Dec(0.1m, 2.0m);
        readonly NumericUpDown actDelay = Num(0, 500, 10);
        readonly NumericUpDown deactDelay = Num(0, 500, 10);
        readonly NumericUpDown repDelay = Num(0, 500, 10);

        readonly NumericUpDown steamvrPipelineFrames = Dec(0.0m, 5.0m);
        readonly CheckBox hapticsEnabled = new CheckBox { Text = Lang.Get("Settings.Check.Haptics"), AutoSize = true };
        readonly NumericUpDown hapticsCurve = Dec(0.1m, 5.0m);
        readonly NumericUpDown hapticsMinDuration = Dec(0.001m, 0.5m);

        readonly ComboBox controllerEmulation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        readonly NumericUpDown linearVelocityCutoff = Dec(0.0001m, 1.0m);
        readonly NumericUpDown angularVelocityCutoff = Dec(0.01m, 50.0m);

        // Offsets Manette Gauche (Position & Rotation)
        readonly NumericUpDown ctrlPosX = Dec(-1.0m, 1.0m), ctrlPosY = Dec(-1.0m, 1.0m), ctrlPosZ = Dec(-1.0m, 1.0m);
        readonly NumericUpDown ctrlRotX = Dec(-180m, 180m), ctrlRotY = Dec(-180m, 180m), ctrlRotZ = Dec(-180m, 180m);

        // Offsets Suivi des mains Gauche (Position & Rotation)
        readonly NumericUpDown handPosX = Dec(-1.0m, 1.0m), handPosY = Dec(-1.0m, 1.0m), handPosZ = Dec(-1.0m, 1.0m);
        readonly NumericUpDown handRotX = Dec(-180m, 180m), handRotY = Dec(-180m, 180m), handRotZ = Dec(-180m, 180m);

        // Configuration des boutons et seuils de pression
        readonly NumericUpDown clickThreshold = Dec(0.01m, 1.0m);
        readonly NumericUpDown touchThreshold = Dec(0.01m, 1.0m);
        readonly NumericUpDown forceThreshold = Dec(0.01m, 1.0m);

        // Casque, Vive Tracker Proxy, Body/Face Tracking, VMC
        readonly ComboBox headsetEmulation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        readonly CheckBox trackingRefOnly = new CheckBox { Text = Lang.Get("Settings.Check.TrackingRefOnly"), AutoSize = true };
        readonly CheckBox enableViveTrackerProxy = new CheckBox { Text = Lang.Get("Settings.Check.ViveTrackerProxy"), AutoSize = true };
        readonly CheckBox bodyTracking = new CheckBox { Text = Lang.Get("Settings.Check.BodyTracking"), AutoSize = true };
        readonly CheckBox faceTracking = new CheckBox { Text = Lang.Get("Settings.Check.FaceTracking"), AutoSize = true };
        readonly CheckBox vmcEnabled = new CheckBox { Text = Lang.Get("Settings.Check.Vmc"), AutoSize = true };
        readonly NumericUpDown maxPredictionMs = Num(0, 200, 5);

        // Nouvelles options Audio & Système
        readonly CheckBox steamvrLifecycle = new CheckBox { Text = Lang.Get("Settings.Check.SteamvrLifecycle"), AutoSize = true };
        readonly NumericUpDown gameAudioBufferMs = Num(10, 200, 5);
        readonly NumericUpDown micAudioBufferMs = Num(10, 200, 5);

        // Options inspirées du style Virtual Desktop & optimisation d'image de pointe
        readonly ComboBox vdQuality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
        readonly ComboBox sswMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly CheckBox vdSnapdragonSuperRes = new CheckBox { Text = Lang.Get("Settings.Check.SnapdragonSR"), AutoSize = true };
        readonly CheckBox vdVideoBuffering = new CheckBox { Text = Lang.Get("Settings.Check.VideoBuffering"), AutoSize = true };
        readonly CheckBox vdStageTracking = new CheckBox { Text = Lang.Get("Settings.Check.StageTracking"), AutoSize = true };
        readonly CheckBox vdTrackControllers = new CheckBox { Text = Lang.Get("Settings.Check.TrackControllers"), AutoSize = true };
        readonly CheckBox vdColorVibrance = new CheckBox { Text = Lang.Get("Settings.Check.ColorVibrance"), AutoSize = true };
        readonly CheckBox vdNominalRange = new CheckBox { Text = Lang.Get("Settings.Check.NominalRange"), AutoSize = true };
        readonly TrackBar vdSharpening = new ModernSlider { Minimum = 0, Maximum = 100, Width = 220, Height = 26 };
        readonly Label vdSharpeningVal = Ui.Line("75 %", Ui.Bold);
        readonly TrackBar vdGamma = new ModernSlider { Minimum = 50, Maximum = 200, Width = 220, Height = 26 };
        readonly Label vdGammaVal = Ui.Line("1.00", Ui.Bold);
        readonly CheckBox upscalingEdgeDirection = new CheckBox { Text = Lang.Get("Settings.Check.UpscaleEdgeDir"), AutoSize = true };
        readonly NumericUpDown upscalingEdgeThreshold = Num(1, 16, 1);
        readonly NumericUpDown upscalingEdgeSharpness = Dec(1.0m, 2.0m);
        readonly NumericUpDown upscalingFactor = Dec(1.0m, 3.0m);
        readonly NumericUpDown colorBrightness = Dec(-1.0m, 1.0m);
        readonly NumericUpDown colorContrast = Dec(-1.0m, 1.0m);
        readonly NumericUpDown colorSaturation = Dec(-1.0m, 1.0m);

        readonly ListView benchList = new ListView { View = View.Details, FullRowSelect = true, Height = 220, Width = 720 };
        readonly Label benchStatus = Ui.Line("");
        readonly TextBox steps = new TextBox { Text = "100,150,200,250,300,350,400,500", Width = 240 };
        readonly NumericUpDown stepSecs = Num(3, 30, 1);
        readonly Button applyBitrate = new Button { Text = Lang.Get("Settings.Bench.ApplyBitrate"), AutoSize = true, Enabled = false };
        readonly Label sessionKeyLabel = new Label { AutoSize = true, ForeColor = Color.FromArgb(0, 255, 180), Font = Ui.BrandFont(9.5f, FontStyle.Bold), Margin = new Padding(3, 4, 3, 2), Text = Lang.Get("Settings.Inspector.Hover") };
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300 };
        HostProcess bench;
        int recommended;
        const string V = "session_settings.video.";
        // new fields from official AuraFlashXR settings.rs
        readonly NumericUpDown adapterIndex = Num(0, 16, 1);
        readonly NumericUpDown bitrateSaturationMultiplier = Dec(0.5m, 5.0m);
        readonly CheckBox amfEnableHmqb = new CheckBox { Text = Lang.Get("Settings.Label.AmfHmqb"), AutoSize = true };
        readonly CheckBox amfUsePreproc = new CheckBox { Text = Lang.Get("Settings.Label.AmfPreproc"), AutoSize = true };
        readonly NumericUpDown amfPreprocSigma = Num(0, 10, 1);
        readonly NumericUpDown amfPreprocTor = Num(0, 10, 1);
        readonly CheckBox amfEnablePreAnalysis = new CheckBox { Text = Lang.Get("Settings.Label.AmfPreAnalysis"), AutoSize = true };
        readonly CheckBox swForceEncoding = new CheckBox { Text = Lang.Get("Settings.Label.SwForceEncoding"), AutoSize = true };
        readonly NumericUpDown swThreadCount = Num(0, 64, 1);
        readonly NumericUpDown colorGamma = Dec(0.0m, 5.0m);
        readonly NumericUpDown colorSharpening = Dec(-1.0m, 5.0m);
        readonly NumericUpDown streamPort = Num(1024, 65535, 1);
        readonly NumericUpDown webServerPort = Num(1024, 65535, 1);
        readonly NumericUpDown oscLocalPort = Num(1024, 65535, 1);
        readonly NumericUpDown maxQueuedVideoFrames = Num(1, 4096, 1);
        readonly NumericUpDown statisticsHistorySize = Num(1, 4096, 1);
        readonly NumericUpDown minimumIdrIntervalMs = Num(5, 1000, 5);
        readonly CheckBox enableOnConnectScript = new CheckBox { Text = Lang.Get("Settings.Label.OnConnectScript"), AutoSize = true };
        readonly CheckBox enableOnDisconnectScript = new CheckBox { Text = Lang.Get("Settings.Label.OnDisconnectScript"), AutoSize = true };
        readonly CheckBox allowUntrustedHttp = new CheckBox { Text = Lang.Get("Settings.Label.AllowUntrustedHttp"), AutoSize = true };
        // nouveaux champs pour l'onglet CONNECTION (AuraFlashXR connection settings)
        readonly CheckBox clientDiscoveryEnabled = new CheckBox { Text = Lang.Get("Settings.Label.ClientDiscovery"), AutoSize = true };
        readonly ComboBox wiredClientType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        readonly CheckBox wiredClientAutolaunch = new CheckBox { Text = Lang.Get("Settings.Label.WiredClientAutolaunch"), AutoSize = true };
        readonly ComboBox streamerSendBufferMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        readonly NumericUpDown streamerSendBufferBytes = Num(0, 100000000, 1000);
        readonly ComboBox streamerRecvBufferMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        readonly NumericUpDown streamerRecvBufferBytes = Num(0, 100000000, 1000);
        readonly ComboBox clientSendBufferMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        readonly NumericUpDown clientSendBufferBytes = Num(0, 100000000, 1000);
        readonly ComboBox clientRecvBufferMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        readonly NumericUpDown clientRecvBufferBytes = Num(0, 100000000, 1000);
        readonly ComboBox dscpMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        readonly CheckBox muteWhenStreaming = new CheckBox { Text = Lang.Get("Settings.Label.MuteWhenStreaming"), AutoSize = true };
        readonly NumericUpDown gameAudioBatchMs = Num(1, 50, 1);
        readonly NumericUpDown micAudioBatchMs = Num(1, 50, 1);
        readonly NumericUpDown velocitiesMultiplier = Dec(0.1m, 10.0m);
        static NumericUpDown Num(int min, int max, int inc) => new NumericUpDown { Minimum = min, Maximum = max, Increment = inc, Width = 80 };
        static NumericUpDown Dec(decimal min, decimal max) => new NumericUpDown { Minimum = min, Maximum = max, Increment = 0.05m, DecimalPlaces = 2, Width = 80 };

        public ConfigureForm(Dictionary<string, object> check)
        {
            this.check = check;
            Lang.Init();
            Text = Lang.Get("Settings.Title");
            Icon = Ui.AppIcon();
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Ui.Body;
            BackColor = Ui.DarkBg;
            ForeColor = Ui.DarkText;
            Width = 1280; Height = 720;
            Ui.FitToWorkArea(this);

            var tabPages = new List<(string Name, string Title, Panel Page)>
            {
                ("Appairage", Lang.Get("Settings.Tab.Pairing"), HeadsetTab()),
                ("Connexion", Lang.Get("Settings.Tab.Connection"), ConnectionTab()),
                ("Manettes & Casque", Lang.Get("Settings.Tab.Controllers"), ControllerHeadsetTab()),
                ("Vid\u00E9o & Encodage", Lang.Get("Settings.Tab.Video"), VideoTab()),
                ("Am\u00E9lioration Image", Lang.Get("Settings.Tab.Image"), ImageEnhanceTab()),
                ("Cadencement & Pacing", Lang.Get("Settings.Tab.Pacing"), PacingAndTrackingTab()),
                ("Audio", Lang.Get("Settings.Tab.Audio"), AudioTab()),
                ("Syst\u00E8me Windows", Lang.Get("Settings.Tab.System"), SystemTab()),
                ("Benchmark", Lang.Get("Settings.Tab.Benchmark"), BenchTab())
            };

            var mainContainer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Ui.DarkBg, Margin = new Padding(0) };
            mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 223));
            mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(16, 22, 28), Padding = new Padding(12, 18, 12, 10), Margin = new Padding(0) };
            var contentContainer = new Panel { Dock = DockStyle.Fill, BackColor = Ui.DarkBg, Padding = new Padding(14, 10, 14, 10), AutoScroll = true };

            var navButtons = new List<Button>();
            void SelectTab(int index)
            {
                contentContainer.SuspendLayout();
                contentContainer.Controls.Clear();

                // Espace inférieur dédié pour garantir que le défilement dépasse largement la dernière ligne
                var bottomSpacer = new Panel { Dock = DockStyle.Top, Height = 140, BackColor = Color.Transparent };
                contentContainer.Controls.Add(bottomSpacer);

                var page = tabPages[index].Page;
                page.Dock = DockStyle.Top;
                // re-ajuster la largeur de la page quand le panneau de droite est redimensionne
                // (sinon en anglais les textes longs ne forcent pas le retour a la ligne et debordent)
                contentContainer.Resize -= ResizePageToFit;
                contentContainer.Resize += ResizePageToFit;
                void ResizePageToFit(object s, EventArgs e)
                {
                    if (contentContainer.Controls.Count > 1 && contentContainer.Controls[1] is Panel pg)
                    {
                        int availW = contentContainer.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - contentContainer.Padding.Left - contentContainer.Padding.Right - 8;
                        if (availW > 100) pg.Width = availW;
                    }
                }
                page.Width = contentContainer.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - contentContainer.Padding.Left - contentContainer.Padding.Right - 8;
                contentContainer.Controls.Add(page);
                Lang.TranslateControl(page);
                Ui.ApplyModernTheme(page);

                for (int i = 0; i < navButtons.Count; i++)
                {
                    bool active = (i == index);
                    navButtons[i].BackColor = active ? Ui.Accent : Color.Transparent;
                    navButtons[i].ForeColor = active ? Color.White : Color.FromArgb(160, 180, 190);
                    navButtons[i].Font = active ? Ui.Bold : Ui.Body;
                    navButtons[i].Invalidate();
                }
                contentContainer.AutoScrollPosition = new Point(0, 0);
                contentContainer.ResumeLayout(true);
            }

            for (int i = 0; i < tabPages.Count; i++)
            {
                var idx = i;
                var item = tabPages[i];
                var btn = new Button
                {
                    Text = item.Title,
                    Width = 202,
                    Height = 44,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 0, 8),
                    BackColor = Color.Transparent,
                    ForeColor = Ui.DarkMuted,
                    UseMnemonic = false
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
                btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
                bool hover = false;
                btn.MouseEnter += (s, e) => { hover = true; btn.Invalidate(); };
                btn.MouseLeave += (s, e) => { hover = false; btn.Invalidate(); };
                btn.Paint += (s, e) =>
                {
                    var b = (Button)s;
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(b.Parent?.BackColor ?? Color.FromArgb(16, 22, 28));
                    bool active = b.BackColor == Ui.Accent;
                    var rect = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                    Color fill, border, txt;
                    if (active)
                    {
                        fill = Color.FromArgb(55, Ui.AccentLight.R, Ui.AccentLight.G, Ui.AccentLight.B);
                        border = Ui.AccentLight;
                        txt = Color.White;
                    }
                    else if (hover)
                    {
                        fill = Ui.DarkHover;
                        border = Color.FromArgb(0x30, 0x44, 0x62);
                        txt = Ui.DarkText;
                    }
                    else
                    {
                        fill = Color.FromArgb(0x13, 0x1B, 0x28);
                        border = Ui.DarkBorder;
                        txt = Ui.DarkMuted;
                    }
                    using (var path = Ui.RoundRect(rect, 8))
                    {
                        using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                        using (var pen = new Pen(border, active ? 1.5f : 1f)) g.DrawPath(pen, path);
                    }
                    if (active)
                    {
                        using (var pill = Ui.RoundRect(new Rectangle(5, 10, 4, b.Height - 20), 2))
                        using (var br = new SolidBrush(Ui.AccentLight))
                            g.FillPath(br, pill);
                    }
                    var navFont = Ui.BrandFont(8.5f, FontStyle.Bold);
                    var textRect = new Rectangle(14, 0, b.Width - 18, b.Height);
                    TextRenderer.DrawText(g, b.Text, navFont, textRect, txt,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                    navFont.Dispose();
                };
                btn.Click += (s, e) => SelectTab(idx);
                sidebar.Controls.Add(btn);
                navButtons.Add(btn);
            }

            mainContainer.Controls.Add(sidebar, 0, 0);
            mainContainer.Controls.Add(contentContainer, 1, 0);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(14, 10, 14, 10), BackColor = Color.FromArgb(16, 22, 28) };

            var startBtn = new Button { Text = Lang.Get("Settings.Btn.Start"), AutoSize = true, BackColor = Color.FromArgb(32, 45, 60), ForeColor = Color.White, Font = Ui.Bold, Padding = new Padding(10, 6, 10, 6), FlatStyle = FlatStyle.Flat };
            startBtn.FlatAppearance.BorderSize = 0;
            startBtn.Click += (s, e) => { if (SaveSettings(false)) { try { Process.Start(Paths.Runtime); } catch { } Close(); } };

            // ENREGISTRER LA CONFIG : sauvegarde un instantané de la configuration actuelle (celle de MON PRESET, la config\ de base)
            // dans config\<nom>\config\ ; la config apparaît ensuite dans le menu déroulant du lanceur. Les .json de base ne sont jamais modifiés.
            var presetBtn = new Button { Text = Lang.Get("Settings.Btn.SaveConfig"), AutoSize = true, BackColor = Color.FromArgb(32, 45, 60), ForeColor = Color.White, Font = Ui.Bold, Padding = new Padding(10, 6, 10, 6), FlatStyle = FlatStyle.Flat };
            presetBtn.FlatAppearance.BorderSize = 0;

            // APPLIQUER LE PRESET : enregistre les réglages affichés dans le preset actif, puis le marque
            // comme preset actif (config\preset.cfg) pour que le launcher, le streamer et le host le prennent en compte.
            var applyPresetBtn = new Button { Text = Lang.Get("Settings.Btn.ApplyPreset"), AutoSize = true, BackColor = Ui.Accent, ForeColor = Color.White, Font = Ui.Bold, Padding = new Padding(10, 6, 10, 6), FlatStyle = FlatStyle.Flat };
            applyPresetBtn.FlatAppearance.BorderSize = 0;
            applyPresetBtn.Click += (s, e) =>
            {
                // The preset is made active FIRST: SaveSettings() and settings.Save() write into the active
                // preset's folder (config\<name>\config), so saving before marking it wrote to the wrong place.
                var name = loadedProfileName ?? Preset.DefaultName;
                Preset.Active = name;
                lastSyncedActive = Preset.Active;
                if (!SaveSettings(false)) return;
                Log.Write("INFO", "preset applied from configure.exe: '" + name + "' is now the active preset");
                MessageBox.Show(this, string.Format(Lang.Get("Settings.ApplyPreset.Done"), name), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            presetBtn.Click += (s, e) =>
            {
                // when a read-only profile is loaded, we still allow saving as a NEW preset (copy under a new name).
                string presetSourceDir = isReadOnlyProfile ? Path.Combine(Paths.Config, loadedProfileName, "config") : Paths.Config;
                using (var ask = new Form
                {
                    Text = Lang.Get("Preset.Dialog.Title"),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedSingle,
                    MinimizeBox = false,
                    MaximizeBox = false,
                    ClientSize = new Size(520, 170),
                    BackColor = Color.FromArgb(16, 22, 28),
                    ForeColor = Color.White,
                    Font = Ui.Body
                })
                {
                    var info = new Label { Text = string.Format(Lang.Get(isReadOnlyProfile ? "Preset.Dialog.NamePromptReadOnly" : "Preset.Dialog.NamePrompt"), isReadOnlyProfile ? loadedProfileName : ""), Location = new Point(14, 16), AutoSize = false, Width = 492, Height = 40, ForeColor = isReadOnlyProfile ? Color.FromArgb(255, 200, 100) : Color.FromArgb(180, 200, 210) };
                    var nameBox = new TextBox { Location = new Point(14, 66), Width = 492, Font = Ui.Body, BackColor = Color.FromArgb(24, 32, 42), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                    var btnRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, BackColor = Color.FromArgb(16, 22, 28), Padding = new Padding(10, 8, 10, 8) };
                    var okBtn = new Button { Text = Lang.Get("Preset.Dialog.Ok"), DialogResult = DialogResult.OK, BackColor = Ui.Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Padding = new Padding(12, 4, 12, 4), Font = Ui.Body, UseVisualStyleBackColor = false, AutoSize = true };
                    var cancelBtn = new Button { Text = Lang.Get("Preset.Dialog.Cancel"), DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(32, 45, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Padding = new Padding(12, 4, 12, 4), Font = Ui.Body, UseVisualStyleBackColor = false, AutoSize = true };
                    okBtn.FlatAppearance.BorderSize = 0; cancelBtn.FlatAppearance.BorderSize = 0;
                    btnRow.Controls.Add(okBtn);
                    btnRow.Controls.Add(cancelBtn);
                    ask.AcceptButton = okBtn;
                    ask.CancelButton = cancelBtn;
                    ask.Controls.Add(btnRow);
                    ask.Controls.Add(nameBox);
                    ask.Controls.Add(info);
                    if (ask.ShowDialog(this) == DialogResult.OK)
                    {
                        var presetName = nameBox.Text.Trim();
                        // bloque la sauvegarde si le nom est un profil read-only built-in
                        if (IsBuiltInProfile(presetName))
                        {
                            MessageBox.Show(this, string.Format(Lang.Get("Preset.ReadOnlyExists"), presetName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        var err = Preset.Save(presetName, presetSourceDir);
                        if (err == null)
                        {
                            MessageBox.Show(this, string.Format(Lang.Get(isReadOnlyProfile ? "Preset.SavedFromReadOnly" : "Preset.Saved"), isReadOnlyProfile ? loadedProfileName : presetName, presetName), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            ShowSaveReminderPopup(); // rappel d'enregistrer les parametres
                        }
                        else if (err == "exists")
                        {
                            // propose d'écraser le preset perso existant (les profils built-in sont déjà bloqués par IsBuiltInProfile au-dessus)
                            if (!IsBuiltInProfile(presetName) &&
                                MessageBox.Show(this, string.Format(Lang.Get("Preset.OverwriteConfirm"), presetName), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                var oerr = Preset.Overwrite(presetName);
                                if (oerr == null)
                                {
                                    MessageBox.Show(this, string.Format(Lang.Get("Preset.Overwritten"), presetName), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    ShowSaveReminderPopup();
                                }
                                else
                                {
                                    MessageBox.Show(this, Lang.Get("Preset.OverwriteError") + "\n" + oerr, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            else if (IsBuiltInProfile(presetName))
                            {
                                MessageBox.Show(this, string.Format(Lang.Get("Preset.Exists"), presetName), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            // sinon : l'utilisateur a refusé d'écraser -> on ne fait rien
                        }
                        else if (err == "reserved")
                            MessageBox.Show(this, Lang.Get("Preset.Reserved"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else if (err == "empty")
                            MessageBox.Show(this, Lang.Get("Preset.EmptyName"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else
                            MessageBox.Show(this, Lang.Get("Preset.Error") + "\n" + err, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            
            // CHARGER CONFIG : ouvre une fenêtre avec un menu déroulant listant toutes les configurations
            // (DEFAULT (USINE), MON PRESET, OPTI CONFIG, et chaque dossier config\<nom>\config\). Le chargement est en temps réel :
            // LoadProfile() charge session.json + auraflashxr.json et rafraîchit l'éditeur sans modifier les .json de base.
            // Un popup confirme le chargement et demande de rafraîchir la fenêtre.
            var loadConfigBtn = new Button { Text = Lang.Get("Settings.Btn.LoadConfig"), AutoSize = true, BackColor = Color.FromArgb(24, 32, 42), ForeColor = Color.FromArgb(180, 200, 210), Font = Ui.Bold, Padding = new Padding(10, 6, 10, 6), FlatStyle = FlatStyle.Flat };
            loadConfigBtn.FlatAppearance.BorderSize = 0;
            loadConfigBtn.Click += (s, e) =>
            {
                // menu déroulant preset - sélection (checkmark) + suppression, style sombre
                // built-in presets: one per encoder (OPTI H.264 / OPTI HEVC / OPTI AV1), listed flat
                var configs = new List<string>();
                foreach (var family in Preset.CodecFamilies)
                    configs.Add("OPTI " + family);
                foreach (var p in Preset.List())
                    if (!configs.Contains(p, StringComparer.OrdinalIgnoreCase))
                        configs.Add(p);

                var menu = new ContextMenuStrip
                {
                    ShowImageMargin = false,
                    BackColor = Color.FromArgb(0x14, 0x1C, 0x2B),
                    ForeColor = Color.White,
                    Font = Ui.SemiBold,
                    DropShadowEnabled = true,
                    Renderer = new Ui.DarkMenuRenderer()   // inherited by the per-encoder submenus (same dark look)
                };

                // Header sélection
                var selHeader = new ToolStripMenuItem(Lang.Get("Preset.Menu.SelectHeader"))
                {
                    Enabled = false,
                    ForeColor = Color.FromArgb(120, 140, 160),
                    Font = Ui.Body,
                    BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                };
                menu.Items.Add(selHeader);

                string activePreset = Preset.IsActiveDefault ? Preset.DefaultName : Preset.Active;
                foreach (var cfg in configs)
                {
                    var capturedCfg = cfg;
                    string cfgUpper = cfg.ToUpperInvariant();
                    string codecLabel;
                    string gpuLabel;
                    bool hasGpu = true;
                    var codecFamily = Preset.CodecFamilyOf(cfg);
                    if (codecFamily != null) { codecLabel = codecFamily; gpuLabel = ""; hasGpu = false; }
                    else if (cfgUpper.Contains("LOW")) { codecLabel = Lang.Get("Launcher.Preset.Codec.H264"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.Low"); }
                    else if (cfgUpper.Contains("MID")) { codecLabel = Lang.Get("Launcher.Preset.Codec.Hevc8"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.Mid"); }
                    else if (cfgUpper.Contains("OPTI")) { codecLabel = Lang.Get("Launcher.Preset.Codec.Hevc10Split"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.High"); }
                    else if (cfgUpper.Contains("HIGH")) { codecLabel = Lang.Get("Launcher.Preset.Codec.Hevc10Dual"); gpuLabel = Lang.Get("Launcher.Preset.Gpu.High"); }
                    else { codecLabel = Lang.Get("Launcher.Preset.Tooltip.Custom"); gpuLabel = ""; hasGpu = false; }
                    string displayText = hasGpu
                        ? cfg + "  \u00B7  " + codecLabel + "  \u00B7  " + Lang.Get("Launcher.Preset.GpuMinPrefix") + gpuLabel
                        : cfg + "  \u00B7  " + codecLabel;
                    var item = new ToolStripMenuItem(displayText)
                    {
                        AutoSize = true,
                        Padding = new Padding(14, 6, 18, 6),
                        ForeColor = Color.White,
                        Font = Ui.SemiBold,
                        Checked = cfg.Equals(activePreset, StringComparison.OrdinalIgnoreCase),
                        CheckOnClick = false,
                        BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                    };
                    item.Click += (ms, me) =>
                    {
                        try
                        {
                            bool readOnly;
                            string configDir;
                            if (capturedCfg == Preset.DefaultName)
                            {
                                configDir = Paths.Config;
                                readOnly = false;
                            }
                            else
                            {
                                configDir = Path.Combine(Paths.Config, capturedCfg, "config");
                                readOnly = false;   // presets intégrés modifiables (plus de lecture seule)
                            }
                            LoadProfile(capturedCfg, configDir, readOnly);
                            MessageBox.Show(this, string.Format(Lang.Get("Config.LoadedPopup"), capturedCfg), Lang.Get("Config.Dialog.Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, Lang.Get("Config.LoadError") + ex.Message, Lang.Get("Config.Dialog.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    };
                    // One preset per encoder: it is listed flat, under its own name (no per-encoder submenu).
                    menu.Items.Add(item);
                }

                // Separator + section suppression
                menu.Items.Add(new ToolStripSeparator { BackColor = Color.FromArgb(0x14, 0x1C, 0x2B) });
                var delHeader = new ToolStripMenuItem(Lang.Get("Preset.Menu.DeleteHeader"))
                {
                    Enabled = false,
                    ForeColor = Color.FromArgb(120, 140, 160),
                    Font = Ui.Body,
                    BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                };
                menu.Items.Add(delHeader);

                int delCount = 0;
                foreach (var dp in configs)
                {
                    if (dp == Preset.DefaultName || !Preset.Deletable(dp)) continue;
                    delCount++;
                    var capturedDel = dp;
                    var delItem = new ToolStripMenuItem("\u2715 " + dp)
                    {
                        AutoSize = true,
                        Padding = new Padding(14, 6, 18, 6),
                        ForeColor = Color.FromArgb(255, 130, 130),
                        Font = Ui.SemiBold,
                        BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                    };
                    delItem.Click += (ms, me) =>
                    {
                        var confirmMsg = string.Format(Lang.Get("Preset.Menu.DeleteConfirm"), capturedDel);
                        if (MessageBox.Show(this, confirmMsg, Lang.Get("App.Title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return;
                        var err = Preset.Delete(capturedDel);
                        if (err == null)
                        {
                            MessageBox.Show(this, string.Format(Lang.Get("Preset.Menu.DeleteSuccess"), capturedDel), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                            // Si on supprime le profil actuellement charge, recharger MON PRESET
                            if (loadedProfileName != null && loadedProfileName.Equals(capturedDel, StringComparison.OrdinalIgnoreCase))
                            {
                                LoadProfile(Preset.DefaultName, Paths.Config, false);
                            }
                        }
                        else if (err == "reserved")
                        {
                            MessageBox.Show(this, Lang.Get("Preset.Menu.DeleteReserved"), Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            MessageBox.Show(this, Lang.Get("Preset.Menu.DeleteError") + " " + err, Lang.Get("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    };
                    menu.Items.Add(delItem);
                }
                if (delCount == 0)
                {
                    var noDel = new ToolStripMenuItem(Lang.Get("Preset.Menu.NoPresets"))
                    {
                        Enabled = false,
                        ForeColor = Color.FromArgb(120, 140, 160),
                        Font = Ui.Body,
                        BackColor = Color.FromArgb(0x14, 0x1C, 0x2B)
                    };
                    menu.Items.Add(noDel);
                }

                menu.Show(loadConfigBtn, new Point(0, loadConfigBtn.Height + 2));
            };
            
            bottom.Controls.Add(startBtn);
            bottom.Controls.Add(presetBtn);
            bottom.Controls.Add(applyPresetBtn);
            bottom.Controls.Add(loadConfigBtn);
            // the bubble is added to its own bottom-left row below (not in the button bar)

            var inspectorPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(12, 17, 24), Padding = new Padding(14, 7, 14, 7) };
            inspectorPanel.Controls.Add(sessionKeyLabel);

            // Ordre d'ajout dans Controls : le container principal occupe le centre, les barres restent ancrées
            // bottom-left bubble row showing the loaded configuration
            var bubbleRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(14, 0, 14, 8), BackColor = Color.FromArgb(16, 22, 28) };
            bubbleRow.Controls.Add(configBubble);

            Controls.Add(mainContainer);
            Controls.Add(inspectorPanel);
            Controls.Add(bottom);
            Controls.Add(bubbleRow); // added last = docked under the bottom bar (bottom-left)

            SelectTab(0);
            Lang.TranslateControl(this);
            LoadActiveProfileAtStartup(); // open on the preset marked active in preset.cfg (launcher sync)
            Shown += (s, e) => StartDiscovery();
            // live sync with the launcher - poll config\preset.cfg so a preset changed in the
            // launcher CONFIGURATION dropdown reloads here automatically (both windows stay in sync).
            var presetPoll = new System.Windows.Forms.Timer { Interval = 1500 };
            presetPoll.Tick += (s, e) => PollActivePreset();
            presetPoll.Start();
            FormClosed += (s, e) => presetPoll.Stop();
            var sp = Ui.ScreenshotPath() ?? "";
            var spLow = Path.GetFileName(sp).ToLowerInvariant();
            if (spLow.Contains("tab1") || spLow.Contains("controllers")) Shown += (s, e) => SelectTab(1);
            else if (spLow.Contains("tab2") || spLow.Contains("video") || spLow.Contains("settings")) Shown += (s, e) => SelectTab(2);
            else if (spLow.Contains("tab3") || spLow.Contains("image")) Shown += (s, e) => SelectTab(3);
            else if (spLow.Contains("tab4") || spLow.Contains("pacing")) Shown += (s, e) => SelectTab(4);
            else if (spLow.Contains("tab5") || spLow.Contains("audio")) Shown += (s, e) => SelectTab(5);
            else if (spLow.Contains("tab6") || spLow.Contains("system")) Shown += (s, e) => SelectTab(6);
            else if (spLow.Contains("tab7") || spLow.Contains("bench")) Shown += (s, e) => SelectTab(7);
            if (sp.Length > 0) Ui.ScreenshotWhenShown(this, 2500);
            FormClosing += (s, e) => { discovery?.Dispose(); bench?.Stop(); };
        }

        Panel HeadsetTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            found.BackColor = Color.FromArgb(20, 27, 36);
            found.ForeColor = Ui.DarkText;
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            section(Lang.Get("Settings.Section.Detection"));

            // Carte d'appairage contenant la liste et le bouton dans un conteneur propre
            var pairCard = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.FromArgb(20, 27, 36),
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 0, 14)
            };
            pairCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Ui.RoundRect(new Rectangle(0, 0, pairCard.Width - 1, pairCard.Height - 1), 7))
                using (var pen = new Pen(Ui.DarkBorder, 1f))
                    e.Graphics.DrawPath(pen, path);
            };
            StretchChildren(pairCard);

            var infoLabel = Ui.Line(Lang.Get("Settings.Headset.Info"));
            infoLabel.Margin = new Padding(0, 0, 0, 8);
            pairCard.Controls.Add(infoLabel);

            found.Columns.Clear();
            Ui.Col(found, Lang.Get("Settings.Headset.ColHost"), 220);
            Ui.Col(found, Lang.Get("Settings.Headset.ColIp"), 140);
            Ui.Col(found, Lang.Get("Settings.Headset.ColProto"), 320);
            fillLastColumn = () =>
            {
                if (found.Columns.Count < 3) return;
                int others = found.Columns[0].Width + found.Columns[1].Width;
                found.Columns[2].Width = Math.Max(160, found.ClientSize.Width - others - 4);
            };
            found.Resize += (s, e) => fillLastColumn();
            // pairCard est un FlowLayoutPanel TopDown : le ListView s'etire en largeur
            // -> on hook aussi pairCard.Resize (sinon la derniere colonne ne suit pas
            // et la zone vide blanche reapparait a droite de l'en-tete).
            pairCard.Resize += (s, e) => fillLastColumn();
            found.VisibleChanged += (s, e) => { if (found.Visible) fillLastColumn(); };
            found.Margin = new Padding(0, 8, 0, 10);
            pairCard.Controls.Add(found);
            fillLastColumn();

            var pairBtn = new Button { Text = Lang.Get("Settings.Headset.BtnPair"), AutoSize = true, Font = Ui.Bold, BackColor = Ui.Accent, ForeColor = Color.White, Padding = new Padding(14, 8, 14, 8), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 10, 0, 8) };
            pairBtn.FlatAppearance.BorderSize = 0;
            pairBtn.Click += (s, e) => PairSelected();
            found.DoubleClick += (s, e) => PairSelected();

            var btnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 4) };
            btnRow.Controls.Add(pairBtn);
            pairCard.Controls.Add(btnRow);

            discStatus.Margin = new Padding(0, 4, 0, 4);
            pairCard.Controls.Add(discStatus);

            paired.Margin = new Padding(0, 4, 0, 4);
            pairCard.Controls.Add(paired);

            g.Controls.Add(pairCard);

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            ShowPaired();
            return t;
        }

        Panel ConnectionTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            // Section: Protocole & Decouverte
            g.Controls.Add(SectionHeader(Lang.Get("Settings.Section.ConnectionProtocol")));

            proto.Items.Clear();
            proto.Items.AddRange(new object[] { "Udp", "Tcp" });
            SetupRow(g, Lang.Get("Settings.Label.Protocol"), proto, "session_settings.connection.stream_protocol.variant");

            SetupRow(g, Lang.Get("Settings.Label.ClientDiscovery"), clientDiscoveryEnabled, "session_settings.connection.client_discovery.enabled");

            wiredClientType.Items.Clear();
            wiredClientType.Items.AddRange(new object[] { Lang.Get("Settings.Label.Store"), Lang.Get("Settings.Label.Github") });
            SetupRow(g, Lang.Get("Settings.Label.WiredClientType"), wiredClientType, "session_settings.connection.wired_client_type.variant");

            SetupRow(g, Lang.Get("Settings.Label.WiredClientAutolaunch"), wiredClientAutolaunch, "session_settings.connection.wired_client_autolaunch");

            // Section: Scripts & Securite
            g.Controls.Add(SectionHeader(Lang.Get("Settings.Section.ConnectionScripts")));

            SetupRow(g, Lang.Get("Settings.Label.OnConnectScript"), enableOnConnectScript, "session_settings.connection.enable_on_connect_script");

            SetupRow(g, Lang.Get("Settings.Label.OnDisconnectScript"), enableOnDisconnectScript, "session_settings.connection.enable_on_disconnect_script");

            SetupRow(g, Lang.Get("Settings.Label.AllowUntrustedHttp"), allowUntrustedHttp, "session_settings.connection.allow_untrusted_http");

            SetupRow(g, Lang.Get("Settings.Label.AutoTrust"), netAutoTrust, "session_settings.connection.client_discovery.content.auto_trust_clients");

            SetupRow(g, Lang.Get("Settings.Label.AvoidGlitching"), netAvoidGlitching, "session_settings.connection.avoid_video_glitching");

            // Section: Reseau & Ports
            g.Controls.Add(SectionHeader(Lang.Get("Settings.Section.ConnectionNetwork")));

            SetupRow(g, Lang.Get("Settings.Label.PacketSize"), packet, "session_settings.connection.packet_size");

            SetupRow(g, Lang.Get("Settings.Label.StreamPort"), streamPort, "session_settings.connection.stream_port");

            SetupRow(g, Lang.Get("Settings.Label.WebServerPort"), webServerPort, "session_settings.connection.web_server_port");

            SetupRow(g, Lang.Get("Settings.Label.OscPort"), oscLocalPort, "session_settings.connection.osc_local_port");

            // Section: Tampons (Buffer sizes)
            g.Controls.Add(SectionHeader(Lang.Get("Settings.Section.ConnectionBuffers")));

            // Streamer send buffer
            streamerSendBufferMode.Items.Clear();
            streamerSendBufferMode.Items.AddRange(new object[] { Lang.Get("Settings.Label.BufferDefault"), Lang.Get("Settings.Label.BufferMaximum"), Lang.Get("Settings.Label.BufferCustom") });
            SetupRow(g, Lang.Get("Settings.Label.StreamerSendBuffer"), FlowWrap(streamerSendBufferMode, streamerSendBufferBytes), "session_settings.connection.server_send_buffer_bytes");

            // Streamer receive buffer
            streamerRecvBufferMode.Items.Clear();
            streamerRecvBufferMode.Items.AddRange(new object[] { Lang.Get("Settings.Label.BufferDefault"), Lang.Get("Settings.Label.BufferMaximum"), Lang.Get("Settings.Label.BufferCustom") });
            SetupRow(g, Lang.Get("Settings.Label.StreamerReceiveBuffer"), FlowWrap(streamerRecvBufferMode, streamerRecvBufferBytes), "session_settings.connection.server_recv_buffer_bytes");

            // Client send buffer
            clientSendBufferMode.Items.Clear();
            clientSendBufferMode.Items.AddRange(new object[] { Lang.Get("Settings.Label.BufferDefault"), Lang.Get("Settings.Label.BufferMaximum"), Lang.Get("Settings.Label.BufferCustom") });
            SetupRow(g, Lang.Get("Settings.Label.ClientSendBuffer"), FlowWrap(clientSendBufferMode, clientSendBufferBytes), "session_settings.connection.client_send_buffer_bytes");

            // Client receive buffer
            clientRecvBufferMode.Items.Clear();
            clientRecvBufferMode.Items.AddRange(new object[] { Lang.Get("Settings.Label.BufferDefault"), Lang.Get("Settings.Label.BufferMaximum"), Lang.Get("Settings.Label.BufferCustom") });
            SetupRow(g, Lang.Get("Settings.Label.ClientReceiveBuffer"), FlowWrap(clientRecvBufferMode, clientRecvBufferBytes), "session_settings.connection.client_recv_buffer_bytes");

            // Section: Performance & QoS
            g.Controls.Add(SectionHeader(Lang.Get("Settings.Section.ConnectionPerformance")));

            SetupRow(g, Lang.Get("Settings.Label.MaxQueuedFrames"), maxQueuedVideoFrames, "session_settings.connection.max_queued_server_video_frames");

            SetupRow(g, Lang.Get("Settings.Label.StatsHistory"), statisticsHistorySize, "session_settings.connection.statistics_history_size");

            SetupRow(g, Lang.Get("Settings.Label.MinIdrInterval"), minimumIdrIntervalMs, "session_settings.connection.minimum_idr_interval_ms");

            // DSCP
            dscpMode.Items.Clear();
            dscpMode.Items.AddRange(new object[] { Lang.Get("Settings.Label.DscpDefault"), Lang.Get("Settings.Label.DscpSet") });
            SetupRow(g, Lang.Get("Settings.Label.QosDscp"), dscpMode, "session_settings.connection.dscp");

            t.Controls.Add(g);
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        void ShowPaired()
        {
            var h = Session.Headset(session);
            paired.Text = h != null ? Lang.Get("Settings.Headset.Paired", h.Item2, h.Item1, h.Item3) : Lang.Get("Settings.Headset.None");
            paired.ForeColor = h != null ? Color.SeaGreen : Color.DarkOrange;
        }

        void StartDiscovery()
        {
            discovery?.Dispose();
            discovery = new Discovery();
            discovery.OnFound += f => BeginInvoke(new Action(() => AddFound(f)));
            discovery.OnError += m => BeginInvoke(new Action(() => { discStatus.Text = Lang.Get("Settings.Headset.DiscFailed") + m; discStatus.ForeColor = Color.Firebrick; }));
            discovery.Start();
            discStatus.Text = Lang.Get("Settings.Headset.Searching");
            discStatus.ForeColor = Ui.DarkMuted;
        }

        void AddFound(Discovery.Found f)
        {
            var key = f.Hostname + "|" + f.Ip;
            if (seen.ContainsKey(key) && (f.Seen - seen[key]).TotalSeconds < 2) return;
            seen[key] = f.Seen;
            foreach (ListViewItem existing in found.Items)
                if (existing.Tag is Discovery.Found ef && ef.Hostname == f.Hostname && ef.Ip == f.Ip) return;
            var ver = f.Compatible ? string.Format(Lang.Get("Settings.Headset.Compatible"), f.Protocol) : string.Format(Lang.Get("Settings.Headset.Incompatible"), f.Protocol, f.ServerProtocol);
            var it = new ListViewItem(new[] { f.Hostname, f.Ip, ver }) { Tag = f };
            if (!f.Compatible) it.ForeColor = Color.Firebrick;
            found.Items.Add(it);
            fillLastColumn();
            if (found.Items.Count == 1) it.Selected = true;
        }

        void PairSelected()
        {
            if (found.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.SelectFirst"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var f = (Discovery.Found)found.SelectedItems[0].Tag;
            var name = f.Hostname.EndsWith(".client.auraflashxr") ? "Meta Quest / Casque VR" : f.Hostname;
            Session.Pair(session, f.Hostname, name, f.Ip);
            Session.Save(session);
            settings.HeadsetHost = f.Hostname;
            settings.HeadsetName = name;
            settings.HeadsetIp = f.Ip;
            settings.Save();
            Log.Write("INFO", $"paired headset {name} ({f.Hostname}) at {f.Ip}");
            ShowPaired();
        }

        void AttachInspector(Control c, string path, Control labelOrRow = null)
        {
            if (c == null) return;
            var text = string.IsNullOrEmpty(path) ? Lang.Get("Settings.Inspector.NoMatch") : "session.json \u2192 " + path;
            void onEnter(object s, EventArgs e)
            {
                sessionKeyLabel.Text = "\u00E0 la cl\u00E9 : " + text;
                if (labelOrRow != null) labelOrRow.ForeColor = Color.FromArgb(0, 255, 180);
            }
            void onLeave(object s, EventArgs e)
            {
                sessionKeyLabel.Text = Lang.Get("Settings.Inspector.Hover");
                if (labelOrRow != null) labelOrRow.ForeColor = Ui.DarkText;
            }
            c.MouseEnter += onEnter;
            c.MouseLeave += onLeave;
            void AttachRecursive(Control parent)
            {
                foreach (Control sub in parent.Controls)
                {
                    sub.MouseEnter += onEnter;
                    sub.MouseLeave += onLeave;
                    AttachRecursive(sub);
                }
            }
            AttachRecursive(c);
        }

        static Control SectionHeader(string title)
        {
            const string HEADER_TAG = "AFXR_SECTION_HEADER";
            var panel = new Panel
            {
                Height = 30,
                Margin = new Padding(0, 16, 0, 8),
                BackColor = Color.Transparent,
                Tag = HEADER_TAG,
                Cursor = Cursors.Hand
            };

            var lbl = new Label
            {
                Text = Lang.T(title).ToUpperInvariant(),
                AutoSize = true,
                Font = Ui.BrandFont(10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 210, 190),
                Padding = new Padding(4, 0, 4, 0),
                BackColor = Color.Transparent,
                Location = new Point(4, 6),
                Cursor = Cursors.Hand
            };

            var arrow = new Label
            {
                Text = "\u25BC",
                AutoSize = true,
                Font = new Font("Segoe UI Symbol", 10f),
                ForeColor = Color.FromArgb(0, 210, 190),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            panel.Controls.Add(lbl);
            panel.Controls.Add(arrow);

            // Positionner la fleche juste apres le texte du titre
            Action repositionArrow = () =>
            {
                arrow.Location = new Point(lbl.Right + 8, 6);
            };
            lbl.LocationChanged += (s, e) => repositionArrow();
            panel.Resize += (s, e) => repositionArrow();
            repositionArrow();

            bool isToggling = false;
            Action toggleSection = () =>
            {
                if (isToggling) return;
                var parent = panel.Parent as FlowLayoutPanel;
                if (parent == null) return;
                isToggling = true;
                try
                {
                    int startIndex = parent.Controls.GetChildIndex(panel);
                    bool collapse = arrow.Text == "\u25BC";
                    arrow.Text = collapse ? "\u25B6" : "\u25BC";
                    parent.SuspendLayout();
                    for (int i = startIndex + 1; i < parent.Controls.Count; i++)
                    {
                        var c = parent.Controls[i];
                        if (c.Tag as string == HEADER_TAG) break;
                        c.Visible = !collapse;
                    }
                    // opening a section collapses every other section in the same panel,
                    // so the right pane always shows a single expanded sub-category at a time.
                    if (!collapse)
                    {
                        for (int i = 0; i < parent.Controls.Count; i++)
                        {
                            var h = parent.Controls[i];
                            if (h == panel || !(h.Tag as string == HEADER_TAG)) continue;
                            // find this header's own arrow (second label child) and collapse its section if it is open
                            var otherArrow = h.Controls.Count > 1 ? h.Controls[1] as Label : null;
                            if (otherArrow == null || otherArrow.Text != "\u25BC") continue;   // already collapsed
                            otherArrow.Text = "\u25B6";
                            for (int j = i + 1; j < parent.Controls.Count; j++)
                            {
                                var c = parent.Controls[j];
                                if (c.Tag as string == HEADER_TAG) break;
                                c.Visible = false;
                            }
                        }
                    }
                    // ResumeLayout(true) force le recalcul AutoSize complet du FlowLayoutPanel
                    // et propage aux parents (t -> contentContainer AutoScroll) pour que toutes les cards apparaissent
                    parent.ResumeLayout(true);
                }
                finally
                {
                    isToggling = false;
                }
            };

            panel.Click += (s, e) => toggleSection();
            lbl.Click += (s, e) => toggleSection();
            arrow.Click += (s, e) => toggleSection();

            return panel;
        }

        static Control CreateCard(string label, Control control, string sessionPath = null, ToolTip tips = null, Action<Control, string, Control> attachInspector = null)
        {
            // Hauteur de base de la card
            const int CARD_HEIGHT = 58;
            
            // Calculer la hauteur ncessaire en fonction du contenu
            int actualHeight = CARD_HEIGHT;
            if (control != null)
            {
                // Forcer le calcul de la taille prfre
                control.PerformLayout();
                int controlPreferredHeight = control.PreferredSize.Height;
                
                // Si le contrle a besoin de plus de hauteur (ex: FlowLayoutPanel TopDown multi-lignes)
                if (controlPreferredHeight > CARD_HEIGHT - 8)
                {
                    actualHeight = controlPreferredHeight + 16; // +16 pour le padédiéng vertical (4+4) + marge
                }
            }
            
            var card = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = false,
                Height = actualHeight,
                MinimumSize = new Size(0, actualHeight),
                MaximumSize = new Size(int.MaxValue, actualHeight),
                BackColor = Color.FromArgb(20, 27, 36),
                Padding = new Padding(14, 4, 14, 4),
                Margin = new Padding(0, 0, 0, 8)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, actualHeight));
            
            // Label gauche - AutoSize pour s'adapter au texte
            var lbl = new Label
            {
                Text = Lang.T(label),
                AutoSize = false,
                Font = Ui.BrandFont(9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 242, 250),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            
            // Panel de droite simple avec centrage vertical du contrle
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            
            Lang.TranslateControl(control);
            // S'assurer que le contrle garde sa taille prfre et est centr
            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.None;
            control.Margin = new Padding(0);
            rightPanel.Controls.Add(control);
            // ajuster la largeur des FlowLayoutPanel pour permettre le wrap
            if (control is FlowLayoutPanel flp)
            {
                Action adjustWidth = () => {
                    int w = rightPanel.ClientSize.Width - 4;
                    if (w > 100)
                    {
                        flp.Width = w;
                        flp.MaximumSize = new Size(w, 0);
                        AdjustChildFlowWidths(flp, w);
                    }
                };
                rightPanel.Layout += (s, e) => adjustWidth();
                adjustWidth();
            }
            
            // Centrer le contrle verticalement dans le panel
            rightPanel.Resize += (s, e) => {
                if (control.Height > 0 && rightPanel.ClientSize.Height > 0)
                {
                    control.Top = Math.Max(0, (rightPanel.ClientSize.Height - control.Height) / 2);
                    control.Left = 0;
                }
            };
            // ajuster la hauteur du card quand le controle change de taille (wrap)
            control.SizeChanged += (s, e) => {
                int newHeight = control.Height + 16;
                if (newHeight > card.Height)
                {
                    card.Height = newHeight;
                    card.MinimumSize = new Size(0, newHeight);
                    card.MaximumSize = new Size(int.MaxValue, newHeight);
                    card.RowStyles[0] = new RowStyle(SizeType.Absolute, newHeight);
                    control.Top = Math.Max(0, (newHeight - 8 - control.Height) / 2);
                }
            };
            // re-layout diff\u00E9r\u00E9 pour capturer le wrap apr\u00E8s affichage initial
            var reLayoutTimer = new System.Windows.Forms.Timer { Interval = 50 };
            reLayoutTimer.Tick += (s, e) => { reLayoutTimer.Stop(); rightPanel.PerformLayout(); control.PerformLayout(); if (control is FlowLayoutPanel flp2) { int w = rightPanel.ClientSize.Width - 4; if (w > 100) { flp2.Width = w; flp2.MaximumSize = new Size(w, 0); AdjustChildFlowWidths(flp2, w); } } };
            reLayoutTimer.Start();
            // Centrage initial
            control.Top = Math.Max(0, (actualHeight - 8 - control.Height) / 2);
            
            card.Controls.Add(lbl, 0, 0);
            card.Controls.Add(rightPanel, 1, 0);
            // card width is now managed by parent FlowLayoutPanel (MakeFlow adéjàustChildren)
            
            // Bords subtils peints (arrondis)
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Ui.RoundRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 7))
                using (var pen = new Pen(Ui.DarkBorder, 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };
            
            if (!string.IsNullOrEmpty(sessionPath))
            {
                tips?.SetToolTip(control, label + "\r\n[session.json : " + sessionPath + "]");
                tips?.SetToolTip(lbl, label + "\r\n[session.json : " + sessionPath + "]");
                attachInspector?.Invoke(card, sessionPath, lbl);
                attachInspector?.Invoke(control, sessionPath, lbl);
                attachInspector?.Invoke(lbl, sessionPath, lbl);
                attachInspector?.Invoke(rightPanel, sessionPath, lbl);
            }
            
            return card;
        }

        void SetupRow(FlowLayoutPanel container, string label, Control c, string sessionPath = null)
        {
            container.Controls.Add(CreateCard(label, c, sessionPath, tips, AttachInspector));
        }

        static Control FlowWrap(params Control[] cs) { var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), WrapContents = true, FlowDirection = FlowDirection.LeftToRight }; f.Controls.AddRange(cs); return f; }
        // ajuster r\u00E9cursivement la largeur des FlowLayoutPanel imbriqu\u00E9s pour forcer le wrap
        static void AdjustChildFlowWidths(FlowLayoutPanel parent, int width)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is FlowLayoutPanel childFlp)
                {
                    childFlp.Width = width;
                    childFlp.MaximumSize = new Size(width, 0);
                    AdjustChildFlowWidths(childFlp, width);
                }
            }
        }

        // FlowLayoutPanel TopDown avec Dock=Top (seul enfant de t, pas d'inversion).
        // Le handler Resize tire chaque enfant sur toute la largeur du panneau.
        static FlowLayoutPanel MakeFlow()
        {
            var g = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Ui.DarkBg
            };
            Action adjustChildren = () =>
            {
                int w = g.ClientSize.Width - g.Padding.Left - g.Padding.Right;
                if (w < 100) return;
                foreach (Control c in g.Controls)
                {
                    int cw = Math.Max(100, w - c.Margin.Left - c.Margin.Right);
                    c.Width = cw;
                }
            };
            g.Resize += (s, e) => adjustChildren();
            g.ControlAdded += (s, e) => { if (e.Control != null) { int w = g.ClientSize.Width - g.Padding.Left - g.Padding.Right; if (w > 100) e.Control.Width = Math.Max(100, w - e.Control.Margin.Left - e.Control.Margin.Right); } };
            return g;
        }

        // tire les enfants d'un FlowLayoutPanel TopDown sur toute sa largeur
        static void StretchChildren(FlowLayoutPanel panel)
        {
            panel.Resize += (s, e) =>
            {
                int w = panel.ClientSize.Width;
                foreach (Control c in panel.Controls)
                {
                    int cw = Math.Max(0, w - c.Margin.Left - c.Margin.Right);
                    // AutoSize crase Width : MinimumSize est respect mme en AutoSize.
                    c.MinimumSize = new Size(cw, c.MinimumSize.Height);
                    c.Width = cw;
                }
            };
        }

        Panel VideoTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            // 1. Rsolution, Frquence & Débit Vidéo
            section(Lang.Get("Settings.Section.ResolutionBitrate"));
            vdQuality.Items.Clear();
            // Quest 3 native panel per eye (2064x2208) = 100 %, then +10 % per step up to 150 %.
            // Every step is rounded to a multiple of 32 (the encoder's alignment); the native
            // resolution is the one exception, kept exactly as the headset reports it.
            vdQuality.Items.AddRange(new object[] {
                "100 % Native (2064x2208)",
                "110 % (2272x2432)",
                "120 % (2464x2656)",
                "130 % (2688x2880)",
                "140 % (2880x3104)",
                "150 % (3104x3328)"
            });
            vdQuality.SelectedIndexChanged += (s, e) =>
            {
                switch (vdQuality.SelectedIndex)
                {
                    case 0: eyeW.Value = 2064; eyeH.Value = 2208; break; // 100 % Native (Quest 3)
                    case 1: eyeW.Value = 2272; eyeH.Value = 2432; break; // 110 %
                    case 2: eyeW.Value = 2464; eyeH.Value = 2656; break; // 120 %
                    case 3: eyeW.Value = 2688; eyeH.Value = 2880; break; // 130 %
                    case 4: eyeW.Value = 2880; eyeH.Value = 3104; break; // 140 %
                    case 5: eyeW.Value = 3104; eyeH.Value = 3328; break; // 150 %
                }
            };
            SetupRow(g, Lang.Get("Settings.Label.PresetResolution"), vdQuality, "transcoding_view_resolution & openvr_config.eye_resolution_width / height");

            SetupRow(g, Lang.Get("Settings.Label.CustomEyeRes"), FlowWrap(eyeW, Ui.Line(Lang.Get("Settings.Hint.TimesSep")), eyeH), "session_settings.video.transcoding_view_resolution & openvr_config.eye_resolution");
            g.Controls.Add(Ui.HintLine(Lang.Get("Settings.Hint.AuraflashxrDefaultHint", Nvenc.MaxEyeWidth)));

            fps.Items.Clear();
            fps.Items.AddRange(new object[] { "60", "72", "80", "90", "96", "100", "120", "144", "207" });
            SetupRow(g, Lang.Get("Settings.Label.RefreshRate"), fps, "session_settings.video.preferred_fps & openvr_config.refresh_rate");

            // Débit vido (dans la mme section que Rsolution & Frquence)
            SetupRow(g, Lang.Get("Settings.Label.Bitrate"), FlowWrap(bitrate, Ui.Line(Lang.Get("Settings.Hint.Mbps"))), "session_settings.video.bitrate.mode.ConstantMbps");

            bitrateMode.Items.Clear();
            bitrateMode.Items.AddRange(new object[] { "ConstantMbps", "Adaptive" });
            SetupRow(g, Lang.Get("Settings.Label.BitrateMode"), bitrateMode, "session_settings.video.bitrate.mode.variant");

            var adaptFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            adaptFlow.Controls.Add(FlowWrap(adaptiveMaxThroughputEnabled, adaptiveMaxThroughput, Ui.Line("Mbps")));
            adaptFlow.Controls.Add(FlowWrap(adaptiveMinThroughputEnabled, adaptiveMinThroughput, Ui.Line("Mbps")));
            adaptFlow.Controls.Add(FlowWrap(adaptiveMaxLatencyEnabled, adaptiveMaxLatency, Ui.Line("ms")));
            adaptFlow.Controls.Add(adaptToFramerate);
            adaptFlow.Controls.Add(adaptiveImageCorruptionFix);
            SetupRow(g, Lang.Get("Settings.Label.AdaptiveOptions"), adaptFlow, "session_settings.video.bitrate.mode.Adaptive & adapt_to_framerate");
            SetupRow(g, Lang.Get("Settings.Label.HistorySize"), FlowWrap(adaptiveHistorySize, Ui.Line(Lang.Get("Settings.Hint.Samples"))), "session_settings.video.bitrate.history_size");
            SetupRow(g, Lang.Get("Settings.Label.BitrateHeadroom"), FlowWrap(bitrateHeadroomPct, Ui.Line("% (50-100 %)")), "auraflashxr.json -> video.bitrate_headroom_pct");
            SetupRow(g, Lang.Get("Settings.Label.SaturationMultiplier"), bitrateSaturationMultiplier, "session_settings.video.bitrate.mode.Adaptive.saturation_multiplier");

            // 2. Encodeur Matriel NVENC & Codec
            section(Lang.Get("Settings.Section.Codec"));
            preferredCodec.Items.Clear();
            preferredCodec.Items.AddRange(new object[] { "H264", "Hevc", "AV1" });
            SetupRow(g, Lang.Get("Settings.Label.PreferredCodec"), preferredCodec, "session_settings.video.preferred_codec.variant & openvr_config.codec");
            h264Profile.Items.Clear();
            h264Profile.Items.AddRange(new object[] { "High", "Main", "Baseline" });
            SetupRow(g, Lang.Get("Settings.Label.H264Profile"), h264Profile, "session_settings.video.encoder_config.h264_profile.variant");
            SetupRow(g, Lang.Get("Settings.Label.SliceCount"), FlowWrap(sliceCount, Ui.Line(Lang.Get("Settings.Hint.Slices"))), "openvr_config.nvenc_slice_count & auraflashxr.json -> video.slice_count");
            entropyCoding.Items.Clear();
            entropyCoding.Items.AddRange(new object[] { "cavlc", "cabac" });
            SetupRow(g, Lang.Get("Settings.Label.EntropyCoding"), FlowWrap(entropyCoding, Ui.Line(Lang.Get("Settings.Hint.Cavlc"))), "openvr_config.entropy_coding & session_settings.video.encoder_config.entropy_coding");

            section(Lang.Get("Settings.Section.RateControl"));
            rateControlMode.Items.Clear();
            rateControlMode.Items.AddRange(new object[] { "Cbr", "Vbr" });
            SetupRow(g, Lang.Get("Settings.Label.RateControl"), rateControlMode, "session_settings.video.encoder_config.rate_control_mode.variant");
            nvencAqMode.Items.Clear();
            nvencAqMode.Items.AddRange(new object[] { "Spatial", "Temporal", "Disabled" });
            SetupRow(g, Lang.Get("Settings.Label.GopPFrame"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Gop")), nvencGopLength, Ui.Line(Lang.Get("Settings.Hint.PFrame")), nvencPFrameStrategy), "session_settings.video.encoder_config.nvenc (gop_length, p_frame_strategy)");
            var intraFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            intraFlow.Controls.Add(nvencEnableIntraRefresh);
            intraFlow.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Period")), nvencIntraRefreshPeriod, Ui.Line(Lang.Get("Settings.Hint.Count")), nvencIntraRefreshCount));
            SetupRow(g, Lang.Get("Settings.Label.IntraRefresh"), intraFlow, "session_settings.video.encoder_config.nvenc (enable_intra_refresh, period, count)");
            SetupRow(g, Lang.Get("Settings.Label.MaxRefFrames"), FlowWrap(nvencMaxNumRefFrames, Ui.Line("(-1 = auto)")), "session_settings.video.encoder_config.nvenc.max_num_ref_frames");
            SetupRow(g, Lang.Get("Settings.Label.RcBufferDelay"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Buffer")), nvencRcBufferSize, Ui.Line(Lang.Get("Settings.Hint.Delay")), nvencRcInitialDelay), "session_settings.video.encoder_config.nvenc (rc_buffer_size, rc_initial_delay)");
            SetupRow(g, Lang.Get("Settings.Label.RcMaxAvg"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Max")), nvencRcMaxBitrate, Ui.Line(Lang.Get("Settings.Hint.Avg")), nvencRcAverageBitrate), "session_settings.video.encoder_config.nvenc (rc_max_bitrate, rc_average_bitrate)");
            section(Lang.Get("Settings.Section.NvencQuality"));
            SetupRow(g, Lang.Get("Settings.Label.AqMode"), nvencAqMode, "session_settings.video.encoder_config.nvenc.adaptive_quantization_mode.variant");
            encoderQualityPreset.Items.Clear();
            encoderQualityPreset.Items.AddRange(new object[] { "Quality", "Balanced", "Speed" });
            SetupRow(g, Lang.Get("Settings.Label.EncoderQuality"), encoderQualityPreset, "session_settings.video.encoder_config.quality_preset.variant");
            nvencMultiPass.Items.Clear();
            nvencMultiPass.Items.AddRange(new object[] { "disabled", "quarter-resolution", "full-resolution" });
            SetupRow(g, Lang.Get("Settings.Label.NvencMultiPass"), nvencMultiPass, "openvr_config.nvenc_multi_pass & encoder_config.nvenc.multi_pass");
            splitEncode.Items.Clear();
            splitEncode.Items.AddRange(new object[] { "off", "auto", "on" });
            SetupRow(g, Lang.Get("Settings.Label.SplitEncode"), splitEncode, "openvr_config.nvenc_split_encode_mode & auraflashxr.json -> video.split_encode");
            nvencQualityPreset.Items.Clear();
            nvencQualityPreset.Items.AddRange(new object[] { "P1", "P2", "P3", "P4", "P5", "P6", "P7" });
            SetupRow(g, Lang.Get("Settings.Label.NvencPerf"), nvencQualityPreset, "openvr_config.nvenc_quality_preset & encoder_config.nvenc.quality_preset");
            nvencTuningPreset.Items.Clear();
            nvencTuningPreset.Items.AddRange(new object[] { "ultra-low-latency", "low-latency", "high-quality" });
            SetupRow(g, Lang.Get("Settings.Label.NvencTuning"), nvencTuningPreset, "openvr_config.nvenc_tuning_preset & encoder_config.nvenc.tuning_preset");


                        // Options NVENC avancées (suite de la section Encodeur Matriel NVENC & Codec)
            

            section(Lang.Get("Settings.Section.NvencAdvanced"));
            SetupRow(g, Lang.Get("Settings.Label.NvencWeighted"), nvencWeightedPrediction, "session_settings.video.encoder_config.nvenc.enable_weighted_prediction");
            SetupRow(g, Lang.Get("Settings.Label.LowDelayKey"), FlowWrap(nvencLowDelayKeyFrameScale, Ui.Line(Lang.Get("Settings.Hint.KeyScale"))), "session_settings.video.encoder_config.nvenc.low_delay_key_frame_scale");
            SetupRow(g, Lang.Get("Settings.Label.GpuSched"), FlowWrap(gpuSchedClass, Ui.Line(Lang.Get("Settings.Hint.GpuSched"))), "auraflashxr.json -> video.gpu_sched_class");
            var encToggles = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            encToggles.Controls.Add(use10Bit);
            encToggles.Controls.Add(serverOverrides10Bit);
            encToggles.Controls.Add(useFullRange);
            encToggles.Controls.Add(serverOverridesFullRange);
            encToggles.Controls.Add(fillerData);
            encToggles.Controls.Add(enableVbaq);
            encToggles.Controls.Add(vbvStrict);
            encToggles.Controls.Add(qpMap);
            encToggles.Controls.Add(forceSoftwareDecoder);
            SetupRow(g, Lang.Get("Settings.Label.EncoderToggles"), encToggles, "session_settings.video.encoder_config");


            section(Lang.Get("Settings.Section.ColorDisplay"));
            SetupRow(g, Lang.Get("Settings.Label.EncodingGamma"), FlowWrap(encodingGamma, serverOverridesEncodingGamma), "session_settings.video.encoder_config.encoding_gamma");
            // HDR (section Couleur & affichage)
            var hdrFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            hdrFlow.Controls.Add(hdrEnabled);
            hdrFlow.Controls.Add(hdrServerOverrides);
            hdrFlow.Controls.Add(hdrForceSrgb);
            hdrFlow.Controls.Add(hdrClampRange);
            SetupRow(g, Lang.Get("Settings.Label.HdrDisplay"), hdrFlow, "session_settings.video.encoder_config.hdr");
            SetupRow(g, Lang.Get("Settings.Label.AdapterIndex"), adapterIndex, "session_settings.video.adapter_index");

            // PyroWave codec (Vulkan compute wavelet, intra-only). Robust to packet loss (64x64 blocks
            // recover in isolation) and very fast (<0.1 ms encode at 1080p), at the cost of a high bitrate (~200+ Mbps).
            // The host and the Vulkan client decode it; a hardware decoder is not used for this codec.
            section(Lang.Get("Settings.Section.Pyrowave"));
            var pyroFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            pyroFlow.Controls.Add(pyroEnabled);
            pyroFlow.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Hint.PyrowaveExperimental")), pyroChroma, Ui.Line(Lang.Get("Settings.Hint.PyrowaveChroma"))));
            SetupRow(g, Lang.Get("Settings.Label.Pyrowave"), pyroFlow, "auraflashxr.json -> video.pyrowave (enabled)");
            SetupRow(g, Lang.Get("Settings.Label.PyrowaveBitstream"), FlowWrap(pyroMaxKb, Ui.Line(Lang.Get("Settings.Hint.PyrowaveKb"))), "auraflashxr.json -> video.pyrowave.maximum_bitstream_size_kb");
            SetupRow(g, Lang.Get("Settings.Label.PyrowavePacket"), FlowWrap(pyroPacketSize, Ui.Line(Lang.Get("Settings.Hint.PyrowavePacketBytes"))), "auraflashxr.json -> video.pyrowave.packet_boundary_bytes");

            // AMF encoder section
            section(Lang.Get("Settings.Section.AmfSoftware"));
            var amfFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            amfFlow.Controls.Add(amfEnableHmqb);
            amfFlow.Controls.Add(amfUsePreproc);
            amfFlow.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Label.AmfPreprocSigma")), amfPreprocSigma, Ui.Line(Lang.Get("Settings.Label.AmfPreprocTor")), amfPreprocTor));
            amfFlow.Controls.Add(amfEnablePreAnalysis);
            SetupRow(g, Lang.Get("Settings.Label.AmfEncoder"), amfFlow, "session_settings.video.encoder_config.amf");

            // Software encoder section
            var swFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            swFlow.Controls.Add(swForceEncoding);
            swFlow.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Label.SwThreadCount")), swThreadCount));
            SetupRow(g, Lang.Get("Settings.Label.SwEncoder"), swFlow, "session_settings.video.encoder_config.software");

            // 3. Fovéation & Compression Périphérique
            section(Lang.Get("Settings.Section.FoveationPeripheral"));
            var prof = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            prof.Controls.Add(profFov);
            prof.Controls.Add(FlowWrap(profFull, Ui.Badge(Lang.Get("Settings.Badge.Experimental"))));
            prof.Controls.Add(foveatedForceEnable);
            // profFov et profFull ont des parents diffrents (FlowWrap imbriqu) : les RadioButtons
            // ne s'excluent donc pas automatiquement. On force l'exclusion mutuelle manuellement
            // (une seule option slectionnable : l'une OU l'autre).
            bool syncingEncodeMode = false;
            profFov.CheckedChanged += (s, e) =>
            {
                if (syncingEncodeMode || !profFov.Checked) return;
                syncingEncodeMode = true;
                profFull.Checked = false;
                syncingEncodeMode = false;
            };
            profFull.CheckedChanged += (s, e) =>
            {
                if (syncingEncodeMode || !profFull.Checked) return;
                syncingEncodeMode = true;
                profFov.Checked = false;
                syncingEncodeMode = false;
            };
            SetupRow(g, Lang.Get("Settings.Label.EncodeMode"), prof, "session_settings.video.foveated_encoding.enabled & openvr_config.enable_foveated_encoding");
            SetupRow(g, Lang.Get("Settings.Label.FovCenterSize"), FlowWrap(fcx, fcy), "session_settings.video.foveated_encoding.content.center_size_x / y");
            SetupRow(g, Lang.Get("Settings.Label.FovCenterShift"), FlowWrap(fsx, fsy), "session_settings.video.foveated_encoding.content.center_shift_x / y");
            SetupRow(g, Lang.Get("Settings.Label.FovEdgeRatio"), FlowWrap(frx, fry, Ui.Line(Lang.Get("Settings.Hint.EdgeRatio"))), "session_settings.video.foveated_encoding.content.edge_ratio_x / y");

            // Fovéation côté client (dans la section Fovéation & Compression Périphérique)
            clientFoveationMode.Items.Clear();
            clientFoveationMode.Items.AddRange(new object[] { "Dynamic", "Static" });
            clientFoveationLevel.Items.Clear();
            clientFoveationLevel.Items.AddRange(new object[] { "Low", "Medium", "High" });
            var cfovFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            cfovFlow.Controls.Add(clientFoveationEnabled);
            cfovFlow.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Mode")), clientFoveationMode, Ui.Line(Lang.Get("Settings.Hint.Level")), clientFoveationLevel, Ui.Line(Lang.Get("Settings.Hint.VerticalOffset")), clientFoveationOffset));
            SetupRow(g, Lang.Get("Settings.Label.ClientFoveation"), cfovFlow, "session_settings.video.clientside_foveation");

            // 4. Post-Traitement Client & Tampon Vidéo
            section(Lang.Get("Settings.Section.ClientPostBuffer"));
            SetupRow(g, Lang.Get("Settings.Label.BufferHistory"), FlowWrap(bufferingHistoryWeight, Ui.Line(Lang.Get("Settings.Hint.BufferHistory"))), "session_settings.video.buffering_history_weight");

            clientPostSuperSampling.Items.Clear();
            clientPostSuperSampling.Items.AddRange(new object[] { "Quality", "Fast", "Disabled" });
            clientPostSharpening.Items.Clear();
            clientPostSharpening.Items.AddRange(new object[] { "Quality", "Fast", "Disabled" });
            var cpostFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            cpostFlow.Controls.Add(clientPostProcessingEnabled);
            cpostFlow.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Hint.SuperSampling")), clientPostSuperSampling, Ui.Line(Lang.Get("Settings.Hint.Sharpening")), clientPostSharpening));
            SetupRow(g, Lang.Get("Settings.Label.ClientPost"), cpostFlow, "session_settings.video.clientside_post_processing");

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        Panel ImageEnhanceTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            section(Lang.Get("Settings.Section.Sharpness"));
            vdSharpening.ValueChanged += (s, e) => vdSharpeningVal.Text = $"{vdSharpening.Value} %";
            SetupRow(g, Lang.Get("Settings.Label.CasSharpening"), FlowWrap(vdSharpening, vdSharpeningVal), "openvr_config.sharpening & color_correction.content.sharpening");

            vdGamma.ValueChanged += (s, e) => vdGammaVal.Text = (vdGamma.Value / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            SetupRow(g, Lang.Get("Settings.Label.DisplayGamma"), FlowWrap(vdGamma, vdGammaVal), "openvr_config.gamma & color_correction.content.gamma");
            SetupRow(g, Lang.Get("Settings.Label.ColorGamma"), colorGamma, "session_settings.video.color_correction.content.gamma");

            SetupRow(g, Lang.Get("Settings.Label.ColorSharpening"), colorSharpening, "session_settings.video.color_correction.content.sharpening");

            var imgAdv = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            imgAdv.Controls.Add(vdSnapdragonSuperRes);
            imgAdv.Controls.Add(FlowWrap(upscalingEdgeDirection, Ui.Line(Lang.Get("Settings.Label.UpscaleEdgeThreshold")), upscalingEdgeThreshold, Ui.Line(Lang.Get("Settings.Label.UpscaleEdgeSharpness")), upscalingEdgeSharpness, Ui.Line(Lang.Get("Settings.Label.UpscaleFactor")), upscalingFactor));
            imgAdv.Controls.Add(vdColorVibrance);
            imgAdv.Controls.Add(FlowWrap(Ui.Line(Lang.Get("Settings.Label.ColorBrightness")), colorBrightness, Ui.Line(Lang.Get("Settings.Label.ColorContrast")), colorContrast, Ui.Line(Lang.Get("Settings.Label.ColorSaturation")), colorSaturation));
            imgAdv.Controls.Add(vdNominalRange);
            SetupRow(g, Lang.Get("Settings.Label.AdvancedImage"), imgAdv, "session_settings.video (upscaling, color_correction, full range)");

            section(Lang.Get("Settings.Section.ChromaKey"));
            passthroughMode.Items.Clear();
            passthroughMode.Items.AddRange(new object[] { "Blend", "RgbChromaKey", "HsvChromaKey" });
            SetupRow(g, Lang.Get("Settings.Label.PassthroughMode"), passthroughMode, "session_settings.video.passthrough.content.variant");

            idle.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { Color = idle.BackColor, FullOpen = true })
                    if (cd.ShowDialog(this) == DialogResult.OK) SetIdle(cd.Color);
            };
            var idleReset = new Button { Text = Lang.Get("Settings.Btn.RecommendedColor"), AutoSize = true };
            idleReset.Click += (s, e) => SetIdle(ColorTranslator.FromHtml("#00ff00"));
            chromaOn.CheckedChanged += (s, e) => { idle.Enabled = idleReset.Enabled = chromaOn.Checked; };
            SetupRow(g, Lang.Get("Settings.Label.ChromaKey"), chromaOn, "session_settings.video.passthrough & auraflashxr.json -> video.idle_rgb");
            SetupRow(g, Lang.Get("Settings.Label.ChromaColor"), FlowWrap(idle, idleReset), "auraflashxr.json -> video.idle_rgb");
            SetupRow(g, Lang.Get("Settings.Label.BlendThreshold"), FlowWrap(passthroughBlendThreshold, Ui.Line(Lang.Get("Settings.Hint.Blend"))), "session_settings.video.passthrough.content.Blend.threshold");
            SetupRow(g, Lang.Get("Settings.Label.DistanceThreshold"), FlowWrap(passthroughDistanceThreshold, Ui.Line(Lang.Get("Settings.Hint.Distance"))), "session_settings.video.passthrough.content.RgbChromaKey.distance_threshold");
            SetupRow(g, Lang.Get("Settings.Label.Feathering"), FlowWrap(passthroughFeathering, Ui.Line(Lang.Get("Settings.Hint.Feathering"))), "session_settings.video.passthrough.content.RgbChromaKey.feathering");
            var remind = Ui.Line(Lang.Get("Settings.Hint.ChromaKeyInfo"));
            remind.MaximumSize = new Size(560, 0); remind.ForeColor = Ui.DarkMuted;
            SetupRow(g, Lang.Get("Settings.Label.ChromaInfo"), remind);

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        Panel PacingAndTrackingTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            section(Lang.Get("Settings.Section.Ssw"));
            sswMode.Items.Clear();
            sswMode.Items.AddRange(new object[] { Lang.Get("Settings.Combo.SswDisabled"), Lang.Get("Settings.Combo.SswAuto"), Lang.Get("Settings.Combo.SswAlways") });
            SetupRow(g, Lang.Get("Settings.Label.Ssw"), sswMode, "auraflashxr.json -> video.frame_gen");

            SetupRow(g, Lang.Get("Settings.Label.VideoBuffering"), vdVideoBuffering, "session_settings.video.max_buffering_frames");

            SetupRow(g, Lang.Get("Settings.Label.Uncapped"), uncappedMode, "auraflashxr.json -> video.frame_gen_rate");

            SetupRow(g, Lang.Get("Settings.Label.GameFpsCap"), FlowWrap(frameGenCapFps, Ui.Line(Lang.Get("Settings.Hint.FpsCap"))), "auraflashxr.json -> video.frame_gen_cap_fps");
            SetupRow(g, Lang.Get("Settings.Label.ContentLatency"), FlowWrap(frameGenLagMs, Ui.Line("ms (0 = auto)")), "auraflashxr.json -> video.frame_gen_lag_ms");
            SetupRow(g, Lang.Get("Settings.Label.FlowScale"), FlowWrap(frameGenFlowScale, Ui.Line(Lang.Get("Settings.Hint.FlowScale"))), "auraflashxr.json -> video.frame_gen_flow_scale");

            frameGenPerf.Items.Clear();
            frameGenPerf.Items.AddRange(new object[] { "fast", "medium", "slow" });
            SetupRow(g, Lang.Get("Settings.Label.OpticalFlow"), frameGenPerf, "auraflashxr.json -> video.frame_gen_perf");

            frameGenSynthesis.Items.Clear();
            frameGenSynthesis.Items.AddRange(new object[] { "edge", "classic", "edge-blend" });
            SetupRow(g, Lang.Get("Settings.Label.FrameSynthesis"), frameGenSynthesis, "auraflashxr.json -> video.frame_gen_synthesis");

            SetupRow(g, Lang.Get("Settings.Label.HoldFrameAge"), frameGenHoldAge, "auraflashxr.json -> video.frame_gen_hold_age");

            section(Lang.Get("Settings.Section.Pacing"));
            SetupRow(g, Lang.Get("Settings.Label.ServerPacing"), serverFramePacing, "session_settings.video.enforce_server_frame_pacing");

            SetupRow(g, Lang.Get("Settings.Label.PhaseLock"), phaseLock, "auraflashxr.json -> video.phase_lock");

            SetupRow(g, Lang.Get("Settings.Label.BoundaryOffset"), FlowWrap(boundaryOffsetMs, Ui.Line("ms")), "auraflashxr.json -> video.boundary_offset_ms");
            SetupRow(g, Lang.Get("Settings.Label.RunningStart"), FlowWrap(runningStartMs, Ui.Line(Lang.Get("Settings.Hint.RunningStart"))), "auraflashxr.json -> video.running_start_ms");

            SetupRow(g, Lang.Get("Settings.Label.IntakeGate"), intakeGate, "auraflashxr.json -> video.intake_gate");

            SetupRow(g, Lang.Get("Settings.Label.IntakeDepth"), FlowWrap(intakeDepth, Ui.Line(Lang.Get("Settings.Hint.IntakeDepth"))), "auraflashxr.json -> video.intake_depth");

            pickPolicy.Items.Clear();
            pickPolicy.Items.AddRange(new object[] { "drain", "oldest", "newest" });
            SetupRow(g, Lang.Get("Settings.Label.PickPolicy"), pickPolicy, "auraflashxr.json -> video.pick_policy");

            SetupRow(g, Lang.Get("Settings.Label.QueueDrain"), clientQueueDrain, "auraflashxr.json -> video.client_queue_drain");

            SetupRow(g, Lang.Get("Settings.Label.FollowRate"), followClientRate, "auraflashxr.json -> video.follow_client_rate");

            sendPacing.Items.Clear();
            sendPacing.Items.AddRange(new object[] { "asap", "vsync", "phase" });
            SetupRow(g, Lang.Get("Settings.Label.SendPacing"), sendPacing, "auraflashxr.json -> video.send_pacing");

            SetupRow(g, Lang.Get("Settings.Label.FrameStartGate"), frameStartGate, "Registre HKLM\\SOFTWARE\\VirtualDesktop-OpenXR -> ovrshim_vdxr_running_start");
            frameStartGate.CheckedChanged += (s, e) =>
            {
                if (!loadingGate) SetGate(frameStartGate.Checked);
            };

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        Panel ControllerHeadsetTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            // 1. Recentrage & Espace physique
            section(Lang.Get("Settings.Section.Recentering"));
            posRecenteringMode.Items.Clear();
            posRecenteringMode.Items.AddRange(new object[] { "Disabled", "LocalFloor", "Local" });
            SetupRow(g, Lang.Get("Settings.Label.PosRecenter"), FlowWrap(posRecenteringMode, Ui.Line(Lang.Get("Settings.Hint.ViewHeight")), posRecenterViewHeight, Ui.Line("m")), "session_settings.headset.position_recentering_mode");

            rotRecenteringMode.Items.Clear();
            rotRecenteringMode.Items.AddRange(new object[] { "Disabled", "Yaw", "Tilted" });
            SetupRow(g, Lang.Get("Settings.Label.RotRecenter"), rotRecenteringMode, "session_settings.headset.rotation_recentering_mode.variant");

            SetupRow(g, Lang.Get("Settings.Label.StageTracking"), vdStageTracking, "session_settings.headset.stage_tracking");

            // 2. Contrôleurs & Émulation
            section(Lang.Get("Settings.Section.Controllers"));
            var ctrlActivation = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            ctrlActivation.Controls.Add(ctrlEnabled);
            ctrlActivation.Controls.Add(ctrlTracked);
            ctrlActivation.Controls.Add(multimodalTracking);
            SetupRow(g, Lang.Get("Settings.Label.ControllersActivation"), ctrlActivation, "session_settings.headset.controllers (enabled, tracked, multimodal_tracking)");

            controllerEmulation.Items.Clear();
            controllerEmulation.Items.AddRange(new object[] { "Quest3Plus", "QuestPro", "Quest2Touch", "RiftSTouch", "ValveIndex", "Pico4", "ViveWand", "ViveTracker", "Custom" });
            SetupRow(g, Lang.Get("Settings.Label.ControllerEmulation"), controllerEmulation, "session_settings.headset.controllers.content.emulation_mode.variant");

            headsetEmulation.Items.Clear();
            headsetEmulation.Items.AddRange(new object[] { "QuestPro", "Quest2", "RiftS", "Vive", "Custom" });
            SetupRow(g, Lang.Get("Settings.Label.HeadsetEmulation"), headsetEmulation, "session_settings.headset.emulation_mode.variant");

            // 3. Suivi squelettique et gestuel des mains (Hand Skeleton & Interaction)
            section(Lang.Get("Settings.Section.HandTracking"));
            var handSkelFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            handSkelFlow.Controls.Add(handSkeleton);
            handSkelFlow.Controls.Add(handSkeletonInput2);
            handSkelFlow.Controls.Add(handSkeletonPredict);
            SetupRow(g, Lang.Get("Settings.Label.HandSkeleton"), handSkelFlow, "session_settings.headset.controllers.content.hand_skeleton");

            var handIntFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            handIntFlow.Controls.Add(handInteraction);
            handIntFlow.Controls.Add(handOnlyTouch);
            SetupRow(g, Lang.Get("Settings.Label.HandInteraction"), handIntFlow, "session_settings.headset.controllers.content.hand_tracking_interaction");

            SetupRow(g, Lang.Get("Settings.Label.PinchDistances"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Touch")), pinchTouchDist, Ui.Line(Lang.Get("Settings.Hint.Trigger")), pinchTriggerDist), "hand_tracking_interaction (pinch_touch_distance, pinch_trigger_distance)");
            SetupRow(g, Lang.Get("Settings.Label.CurlDistances"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Touch")), curlTouchDist, Ui.Line(Lang.Get("Settings.Hint.Trigger")), curlTriggerDist), "hand_tracking_interaction (curl_touch_distance, curl_trigger_distance)");
            SetupRow(g, Lang.Get("Settings.Label.JoystickZone"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Deadzone")), joyDeadzone, Ui.Line(Lang.Get("Settings.Hint.Range")), joyRange), "hand_tracking_interaction (joystick_deadzone, joystick_range)");
            SetupRow(g, Lang.Get("Settings.Label.JoystickOffset"), FlowWrap(Ui.Line("H :"), joyOffsetH, Ui.Line("V :"), joyOffsetV), "hand_tracking_interaction (joystick_offset_horizontal / vertical)");
            SetupRow(g, Lang.Get("Settings.Label.ActivationDelays"), FlowWrap(actDelay, deactDelay, repDelay), "hand_tracking_interaction (activation_delay, deactivation_delay, repeat_delay)");

            // 4. Cadencement, Pipeline & Haptique
            section(Lang.Get("Settings.Section.PipelineHaptics"));
            SetupRow(g, Lang.Get("Settings.Label.SteamvrPipeline"), FlowWrap(steamvrPipelineFrames, Ui.Line(Lang.Get("Settings.Hint.PipelineFrames"))), "session_settings.headset.controllers.content.steamvr_pipeline_frames");

            var hapFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            hapFlow.Controls.Add(hapticsEnabled);
            var hapValues = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0), WrapContents = true };
            hapValues.Controls.Add(Ui.Line(Lang.Get("Settings.Hint.Intensity")));
            hapValues.Controls.Add(haptics);
            hapValues.Controls.Add(Ui.Line(Lang.Get("Settings.Hint.Curve")));
            hapValues.Controls.Add(hapticsCurve);
            hapValues.Controls.Add(Ui.Line(Lang.Get("Settings.Hint.MinDuration")));
            hapValues.Controls.Add(hapticsMinDuration);
            hapFlow.Controls.Add(hapValues);
            SetupRow(g, Lang.Get("Settings.Label.Haptics"), hapFlow, "session_settings.headset.controllers.content.haptics");

            SetupRow(g, Lang.Get("Settings.Label.LinearCutoff"), FlowWrap(linearVelocityCutoff, Ui.Line(Lang.Get("Settings.Hint.LinearCutoff"))), "session_settings.headset.controllers.content.linear_velocity_cutoff");
            SetupRow(g, Lang.Get("Settings.Label.AngularCutoff"), FlowWrap(angularVelocityCutoff, Ui.Line("\u00B0/s (seuil de rotation)")), "session_settings.headset.controllers.content.angular_velocity_cutoff");

            // 5. Offsets prcis de Position et Rotation (Manettes & Mains)
            section(Lang.Get("Settings.Section.Offsets"));
            SetupRow(g, Lang.Get("Settings.Label.ControllerPosOffset"), FlowWrap(ctrlPosX, ctrlPosY, ctrlPosZ, Ui.Line("m")), "session_settings.headset.controllers.content.left_controller_position_offset");
            SetupRow(g, Lang.Get("Settings.Label.ControllerRotOffset"), FlowWrap(ctrlRotX, ctrlRotY, ctrlRotZ, Ui.Line("\u00B0")), "session_settings.headset.controllers.content.left_controller_rotation_offset");
            SetupRow(g, Lang.Get("Settings.Label.HandPosOffset"), FlowWrap(handPosX, handPosY, handPosZ, Ui.Line("m")), "session_settings.headset.controllers.content.left_hand_tracking_position_offset");
            SetupRow(g, Lang.Get("Settings.Label.HandRotOffset"), FlowWrap(handRotX, handRotY, handRotZ, Ui.Line("\u00B0")), "session_settings.headset.controllers.content.left_hand_tracking_rotation_offset");

            // 6. Seuils de dclenchement des boutons & Trackers
            section(Lang.Get("Settings.Section.ButtonThresholds"));
            SetupRow(g, Lang.Get("Settings.Label.ButtonThresholds"), FlowWrap(Ui.Line(Lang.Get("Settings.Hint.Click")), clickThreshold, Ui.Line(Lang.Get("Settings.Hint.Touch")), touchThreshold, Ui.Line(Lang.Get("Settings.Hint.Force")), forceThreshold), "session_settings.headset.controllers.content.button_mapping_config");

            var advTrackFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0) };
            advTrackFlow.Controls.Add(trackingRefOnly);
            advTrackFlow.Controls.Add(enableViveTrackerProxy);
            advTrackFlow.Controls.Add(bodyTracking);
            advTrackFlow.Controls.Add(faceTracking);
            advTrackFlow.Controls.Add(vmcEnabled);
            SetupRow(g, Lang.Get("Settings.Label.TrackersFbt"), advTrackFlow, "session_settings.headset (tracking_ref_only, enable_vive_tracker_proxy, body_tracking, face_tracking, vmc)");

            SetupRow(g, Lang.Get("Settings.Label.MaxPrediction"), FlowWrap(maxPredictionMs, Ui.Line(Lang.Get("Settings.Hint.MaxPrediction"))), "session_settings.headset.max_prediction_ms");

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        Panel AudioTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            section(Lang.Get("Settings.Section.GameAudio"));
            SetupRow(g, Lang.Get("Settings.Label.GameAudio"), audio, "session_settings.audio.game_audio.enabled");
            SetupRow(g, Lang.Get("Settings.Label.MuteSpeakers"), mute, "auraflashxr.json -> audio.mute_pc");
            SetupRow(g, Lang.Get("Settings.Label.MuteWhenStreaming"), muteWhenStreaming, "session_settings.audio.game_audio.content.mute_when_streaming");

            captureBox.Items.Clear();
            captureBox.Items.AddRange(new object[] { Lang.Get("Settings.Audio.CaptureProcess"), Lang.Get("Settings.Audio.CaptureDevice") });
            SetupRow(g, Lang.Get("Settings.Label.CaptureMethod"), captureBox, "auraflashxr.json -> audio.capture");

            SetupRow(g, Lang.Get("Settings.Label.CaptureDevice"), audioPlaybackDevice, "session_settings.audio.game_audio.content.device");

            section(Lang.Get("Settings.Section.Microphone"));
            SetupRow(g, Lang.Get("Settings.Label.MicEnabled"), mic, "session_settings.audio.microphone.enabled");
            SetupRow(g, Lang.Get("Settings.Label.MicSink"), audioMicDevice, "session_settings.audio.microphone.content.devices.Custom.sink");
            btnInstallMic.Text = Lang.Get("Settings.Audio.InstallMic");
            btnInstallMic.FlatAppearance.BorderSize = 0;
            btnInstallMic.Click += (s, e) => LaunchMicInstallScript();
            btnUninstallMic.Text = Lang.Get("Settings.Audio.UninstallMic");
            btnUninstallMic.FlatAppearance.BorderSize = 0;
            btnUninstallMic.Click += (s, e) => LaunchMicUninstallScript();
            var micPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0), WrapContents = false };
            var micInstallHint = Ui.Line(Lang.Get("Settings.Audio.MicHint"));
            micInstallHint.ForeColor = Ui.DarkMuted;
            micInstallHint.Margin = new Padding(0, 4, 0, 0);
            var micUninstallHint = Ui.Line(Lang.Get("Settings.Audio.MicUninstallHint"));
            micUninstallHint.ForeColor = Ui.DarkMuted;
            micUninstallHint.Margin = new Padding(0, 4, 0, 0);
            micPanel.Controls.Add(btnInstallMic);
            micPanel.Controls.Add(micInstallHint);
            micPanel.Controls.Add(btnUninstallMic);
            micPanel.Controls.Add(micUninstallHint);
            SetupRow(g, Lang.Get("Settings.Label.MicDevice"), micPanel);

            section(Lang.Get("Settings.Section.AudioLatency"));
            SetupRow(g, Lang.Get("Settings.Label.GameAudioBuffer"), FlowWrap(gameAudioBufferMs, Ui.Line("ms (latence tampon audio casque)")), "session_settings.audio.game_audio.content.buffering.average_buffering_ms");
            SetupRow(g, Lang.Get("Settings.Label.MicBuffer"), FlowWrap(micAudioBufferMs, Ui.Line("ms (latence tampon micro)")), "session_settings.audio.microphone.content.buffering.average_buffering_ms");
            SetupRow(g, Lang.Get("Settings.Label.GameAudioBatch"), gameAudioBatchMs, "session_settings.audio.game_audio.content.buffering.batch_ms");

            SetupRow(g, Lang.Get("Settings.Label.MicAudioBatch"), micAudioBatchMs, "session_settings.audio.microphone.content.buffering.batch_ms");

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        Panel SystemTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            section(Lang.Get("Settings.Section.Steamvr"));
            SetupRow(g, Lang.Get("Settings.Label.SteamvrSync"), steamvrLifecycle, "session_settings.extra.steamvr_launcher.open_close_steamvr_with_dashboard");

            section(Lang.Get("Settings.Section.Power"));
            SetupRow(g, Lang.Get("Settings.Label.PowerPlan"), planBox, "auraflashxr.json -> system.power_plan");
            var planNote = Ui.Line(Lang.Get("Settings.Hint.PowerPlan"));
            planNote.ForeColor = Ui.DarkMuted; planNote.MaximumSize = new Size(560, 0);
            SetupRow(g, Lang.Get("Settings.Label.PowerNote"), planNote);

            section(Lang.Get("Settings.Section.Logging"));
            SetupRow(g, Lang.Get("Settings.Label.DebugLog"), debugDefault, "auraflashxr.json -> debug");
            SetupRow(g, Lang.Get("Settings.Label.VelocitiesMultiplier"), velocitiesMultiplier, "session_settings.extra.velocities_multiplier");

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        bool loadingGate;

        static int GateValueNow()
        {
            try { using (var k = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64).OpenSubKey(GateKey)) return k?.GetValue(GateValue) is int v ? v : 0; }
            catch { return 0; }
        }

        void ShowGate()
        {
            var now = GateValueNow();
            var was = loadingGate; loadingGate = true;
            frameStartGate.Checked = now != 1;
            loadingGate = was;
        }

        void SetGate(bool on)
        {
            var args = on ? $"/c reg delete HKLM\\{GateKey} /v {GateValue} /f" : $"/c reg add HKLM\\{GateKey} /v {GateValue} /t REG_DWORD /d 1 /f";
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
                using (var p = Process.Start(psi)) p.WaitForExit(60000);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.GateError") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ShowGate();
        }

        void SetIdle(Color c) { idle.BackColor = c; idle.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}"; idle.ForeColor = c.GetBrightness() > 0.5 ? Color.Black : Color.White; }

        static decimal Clamp(NumericUpDown n, double v) => Math.Max(n.Minimum, Math.Min(n.Maximum, (decimal)v));

        /// <summary>
        /// Loads a full configuration profile (session.json + auraflashxr.json + auraflashxr_views.json)
        /// from a given config folder into the editor. Updates loadedProfileName and isReadOnlyProfile.
        /// </summary>
        /// <param name="profileName">Display name: "MON PRESET", "DEFAULT (USINE)", or a preset folder name</param>
        /// <param name="configDir">The config folder path (e.g. Paths.Config or Paths.Config\OPTI CONFIG\config)</param>
        /// <param name="readOnly">True for DEFAULT (USINE) and OPTI CONFIG (protected profiles)</param>
        void LoadProfile(string profileName, string configDir, bool readOnly)
        {
            try
            {
                // Load session.json
                var sessionPath = Path.Combine(configDir, "session.json");
                if (File.Exists(sessionPath))
                {
                    session = Json.Load(sessionPath);
                }
                else
                {
                    // Fallback to default
                    var defaultPath = Path.Combine(configDir, "session.default.json");
                    if (File.Exists(defaultPath))
                        session = Json.Load(defaultPath);
                    else
                        session = Session.Load();
                }

                // Load auraflashxr.json into settings.Root
                var auraflashPath = Path.Combine(configDir, "auraflashxr.json");
                if (File.Exists(auraflashPath))
                {
                    settings.Root = Json.Load(auraflashPath);
                }

                // Load auraflashxr_views.json if needed (stored in settings or separate)
                // Note: auraflashxr_views.json is typically loaded by the host, not the GUI editor

                loadedProfileName = profileName;
                isReadOnlyProfile = readOnly;

                // Sync with launcher - write the active preset name to preset.cfg so the
                // launcher's CONFIGURATION dropdown shows the last-loaded config on next open.
                if (profileName == Preset.DefaultName)
                    Preset.Active = Preset.DefaultName; // setter converts to empty
                else
                    Preset.Active = profileName;
                lastSyncedActive = Preset.Active; // remember the synced value so the poll timer does not reload
                UpdateConfigBubble();

                // Refresh the UI with the loaded values
                LoadSettings();

                // Update the window title to show the loaded profile
                Text = Lang.Get("Settings.Title") +
                       (readOnly ? " [" + profileName + " \u2014 " + Lang.Get("Config.ReadOnly") + "]" : " [" + profileName + "]");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.Get("Config.LoadError") + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// built-in read-only profiles (per-encoder presets + LOW/MID/HIGH/OPTI CONFIG + DEFAULT (USINE)).
        /// </summary>
        static bool IsBuiltInProfile(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Preset.IsBuiltInPreset(name)
                || name.Equals("DEFAULT (USINE)", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// at startup, loads the preset marked active in config\preset.cfg (written by the launcher
        /// CONFIGURATION dropdown or by CHARGER CONFIG), so configure.exe always opens on the last-used config.
        /// </summary>
        void LoadActiveProfileAtStartup()
        {
            try
            {
                var active = Preset.Active;
                lastSyncedActive = active;
                if (string.IsNullOrEmpty(active))
                {
                    // MON PRESET: the base config was already loaded by the field initializer (Session.Load()).
                    loadedProfileName = Preset.DefaultName;
                    isReadOnlyProfile = false;
                    LoadSettings();
                    UpdateConfigBubble();
                    Text = Lang.Get("Settings.Title") + " [" + Preset.DefaultName + "]";
                    return;
                }
                var dir = Path.Combine(Paths.Config, active, "config");
                if (!File.Exists(Path.Combine(dir, "session.json"))) return;
                bool ro = false;   // presets intégrés modifiables (plus de lecture seule)
                LoadProfile(active, dir, ro);
            }
            catch { }
        }

        /// <summary>
        /// polls config\preset.cfg (every 1.5 s) so a preset changed in the launcher CONFIGURATION
        /// dropdown reloads here automatically - the two windows stay in sync in both directions.
        /// </summary>
        void PollActivePreset()
        {
            try
            {
                var active = Preset.Active;
                if (active == lastSyncedActive) return;
                lastSyncedActive = active;
                if (string.IsNullOrEmpty(active))
                {
                    loadedProfileName = Preset.DefaultName;
                    isReadOnlyProfile = false;
                    LoadSettings();
                    UpdateConfigBubble();
                    Text = Lang.Get("Settings.Title") + " [" + Preset.DefaultName + "]";
                    return;
                }
                var dir = Path.Combine(Paths.Config, active, "config");
                if (!File.Exists(Path.Combine(dir, "session.json"))) return;
                bool ro = false;   // presets intégrés modifiables (plus de lecture seule)
                LoadProfile(active, dir, ro);
            }
            catch { }
        }

        /// <summary>refreshes the bottom-bar bubble showing the currently loaded configuration.</summary>
        void UpdateConfigBubble()
        {
            configBubble.Text = string.Format(Lang.Get("Config.Bubble"), loadedProfileName)
                + (isReadOnlyProfile ? "  \u2022  " + Lang.Get("Config.ReadOnly") : "");
            configBubble.BackColor = isReadOnlyProfile ? Color.FromArgb(66, 44, 10) : Color.FromArgb(14, 50, 38);
            configBubble.ForeColor = isReadOnlyProfile ? Color.FromArgb(255, 200, 100) : Color.FromArgb(120, 255, 180);
        }
        /// <summary>second popup after ENREGISTRER LA CONFIG - reminds the user to save the settings.</summary>
        void ShowSaveReminderPopup()
        {
            MessageBox.Show(this, Lang.Get("Preset.SaveReminder"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void LoadSettings()
        {
            var s = session;
            int curW = (int)Json.Num(s, V + "transcoding_view_resolution.Absolute.width", 2816);
            int curH = (int)Json.Num(s, V + "transcoding_view_resolution.Absolute.height.content", 2944);
            eyeW.Value = Clamp(eyeW, curW);
            eyeH.Value = Clamp(eyeH, curH);

            if (curW >= 3104) vdQuality.SelectedIndex = 5;      // 150 %
            else if (curW >= 2880) vdQuality.SelectedIndex = 4; // 140 %
            else if (curW >= 2688) vdQuality.SelectedIndex = 3; // 130 %
            else if (curW >= 2464) vdQuality.SelectedIndex = 2; // 120 %
            else if (curW >= 2272) vdQuality.SelectedIndex = 1; // 110 %
            else vdQuality.SelectedIndex = 0;                   // 100 % Native

            sswMode.SelectedIndex = settings.FrameGen ? (settings.FrameGenRecommended ? 1 : 2) : 0;
            vdSharpening.Value = (int)Math.Max(0, Math.Min(100, Math.Round(settings.Sharpening * 100)));
            vdSharpeningVal.Text = $"{vdSharpening.Value} %";
            vdGamma.Value = (int)Math.Max(50, Math.Min(200, Math.Round(settings.Gamma * 100)));
            vdGammaVal.Text = (vdGamma.Value / 100.0).ToString("0.00", CultureInfo.InvariantCulture);

            vdSnapdragonSuperRes.Checked = Json.Bool(s, V + "upscaling.enabled", false);
            upscalingEdgeDirection.Checked = Json.Bool(s, V + "upscaling.content.edge_direction", false);
            upscalingEdgeThreshold.Value = Clamp(upscalingEdgeThreshold, Json.Num(s, V + "upscaling.content.edge_threshold", 4));
            upscalingEdgeSharpness.Value = Clamp(upscalingEdgeSharpness, Json.Num(s, V + "upscaling.content.edge_sharpness", 1.5));
            upscalingFactor.Value = Clamp(upscalingFactor, Json.Num(s, V + "upscaling.content.upscale_factor", 1.5));
            vdVideoBuffering.Checked = Json.Num(s, V + "max_buffering_frames", 1.0) >= 2.0;
            vdStageTracking.Checked = Json.Bool(s, "session_settings.headset.stage_tracking", false);
            vdTrackControllers.Checked = Json.Bool(s, "session_settings.headset.controllers.enabled", true);
            vdColorVibrance.Checked = Json.Num(s, V + "color_correction.content.saturation", 0) > 0.1;
            colorBrightness.Value = Clamp(colorBrightness, Json.Num(s, V + "color_correction.content.brightness", 0.0));
            colorContrast.Value = Clamp(colorContrast, Json.Num(s, V + "color_correction.content.contrast", 0.0));
            colorSaturation.Value = Clamp(colorSaturation, Json.Num(s, V + "color_correction.content.saturation", 0.0));
            vdNominalRange.Checked = Json.Bool(s, "openvr_config.use_full_range_encoding", true);
            var f = ((int)Json.Num(s, V + "preferred_fps", 120)).ToString(CultureInfo.InvariantCulture);
            fps.SelectedItem = fps.Items.Contains(f) ? f : "120";
            bitrate.Value = Clamp(bitrate, Json.Num(s, V + "bitrate.mode.ConstantMbps", 250));

            var bMode = Json.Str(s, V + "bitrate.mode.variant", "ConstantMbps");
            bitrateMode.SelectedItem = bitrateMode.Items.Contains(bMode) ? bMode : "ConstantMbps";
            adaptiveMaxThroughputEnabled.Checked = Json.Bool(s, V + "bitrate.mode.Adaptive.max_throughput_mbps.enabled", false);
            adaptiveMaxThroughput.Value = Clamp(adaptiveMaxThroughput, Json.Num(s, V + "bitrate.mode.Adaptive.max_throughput_mbps.content", 100));
            adaptiveMinThroughputEnabled.Checked = Json.Bool(s, V + "bitrate.mode.Adaptive.min_throughput_mbps.enabled", false);
            adaptiveMinThroughput.Value = Clamp(adaptiveMinThroughput, Json.Num(s, V + "bitrate.mode.Adaptive.min_throughput_mbps.content", 5));
            adaptiveMaxLatencyEnabled.Checked = Json.Bool(s, V + "bitrate.mode.Adaptive.max_network_latency_ms.enabled", false);
            adaptiveMaxLatency.Value = Clamp(adaptiveMaxLatency, Json.Num(s, V + "bitrate.mode.Adaptive.max_network_latency_ms.content", 8));
            adaptiveHistorySize.Value = Clamp(adaptiveHistorySize, Json.Num(s, V + "bitrate.history_size", 512));
            adaptiveImageCorruptionFix.Checked = Json.Bool(s, V + "bitrate.image_corruption_fix", false);
            adaptToFramerate.Checked = Json.Bool(s, V + "bitrate.adapt_to_framerate.enabled", false);
            bufferingHistoryWeight.Value = Clamp(bufferingHistoryWeight, Json.Num(s, V + "buffering_history_weight", 0.7));
            bitrateSaturationMultiplier.Value = Clamp(bitrateSaturationMultiplier, Json.Num(s, V + "bitrate.mode.Adaptive.saturation_multiplier", 0.95));

            // Options Vidéo & Codec
            var cdc = Json.Str(s, V + "preferred_codec.variant", "H264");
            preferredCodec.SelectedItem = preferredCodec.Items.Contains(cdc) ? cdc : "H264";
            var eqp = Json.Str(s, V + "encoder_config.quality_preset.variant", "Quality");
            encoderQualityPreset.SelectedItem = encoderQualityPreset.Items.Contains(eqp) ? eqp : "Quality";
            use10Bit.Checked = Json.Bool(s, V + "encoder_config.use_10bit", false);
            serverOverrides10Bit.Checked = Json.Bool(s, V + "encoder_config.server_overrides_use_10bit", false);
            useFullRange.Checked = Json.Bool(s, V + "encoder_config.use_full_range", false);
            serverOverridesFullRange.Checked = Json.Bool(s, V + "encoder_config.server_overrides_use_full_range", false);
            encodingGamma.Value = Clamp(encodingGamma, Json.Num(s, V + "encoder_config.encoding_gamma", 1.0));
            serverOverridesEncodingGamma.Checked = Json.Bool(s, V + "encoder_config.server_overrides_encoding_gamma", false);
            fillerData.Checked = Json.Bool(s, V + "encoder_config.filler_data", false);

            var h264Prof = Json.Str(s, V + "encoder_config.h264_profile.variant", "High");
            h264Profile.SelectedItem = h264Profile.Items.Contains(h264Prof) ? h264Prof : "High";
            var rcMode = Json.Str(s, V + "encoder_config.rate_control_mode.variant", "Cbr");
            rateControlMode.SelectedItem = rateControlMode.Items.Contains(rcMode) ? rcMode : "Cbr";
            var aqMode = Json.Str(s, V + "encoder_config.nvenc.adaptive_quantization_mode.variant", "Spatial");
            nvencAqMode.SelectedItem = nvencAqMode.Items.Contains(aqMode) ? aqMode : "Spatial";
            enableVbaq.Checked = Json.Bool(s, V + "encoder_config.enable_vbaq", false);
            forceSoftwareDecoder.Checked = Json.Bool(s, V + "force_software_decoder", false);

            // NVENC bas-niveau & Intra-refresh
            nvencLowDelayKeyFrameScale.Value = Clamp(nvencLowDelayKeyFrameScale, Json.Num(s, V + "encoder_config.nvenc.low_delay_key_frame_scale", -1));
            nvencEnableIntraRefresh.Checked = Json.Bool(s, V + "encoder_config.nvenc.enable_intra_refresh", false);
            nvencIntraRefreshPeriod.Value = Clamp(nvencIntraRefreshPeriod, Json.Num(s, V + "encoder_config.nvenc.intra_refresh_period", -1));
            nvencIntraRefreshCount.Value = Clamp(nvencIntraRefreshCount, Json.Num(s, V + "encoder_config.nvenc.intra_refresh_count", -1));
            nvencMaxNumRefFrames.Value = Clamp(nvencMaxNumRefFrames, Json.Num(s, V + "encoder_config.nvenc.max_num_ref_frames", -1));
            nvencGopLength.Value = Clamp(nvencGopLength, Json.Num(s, V + "encoder_config.nvenc.gop_length", -1));
            nvencPFrameStrategy.Value = Clamp(nvencPFrameStrategy, Json.Num(s, V + "encoder_config.nvenc.p_frame_strategy", -1));
            nvencRcBufferSize.Value = Clamp(nvencRcBufferSize, Json.Num(s, V + "encoder_config.nvenc.rc_buffer_size", -1));
            nvencRcInitialDelay.Value = Clamp(nvencRcInitialDelay, Json.Num(s, V + "encoder_config.nvenc.rc_initial_delay", -1));
            nvencRcMaxBitrate.Value = Clamp(nvencRcMaxBitrate, Json.Num(s, V + "encoder_config.nvenc.rc_max_bitrate", -1));
            nvencRcAverageBitrate.Value = Clamp(nvencRcAverageBitrate, Json.Num(s, V + "encoder_config.nvenc.rc_average_bitrate", -1));
            nvencWeightedPrediction.Checked = Json.Bool(s, V + "encoder_config.nvenc.enable_weighted_prediction", false);

            // HDR
            hdrEnabled.Checked = Json.Bool(s, V + "encoder_config.hdr.enable_hdr", false);
            hdrServerOverrides.Checked = Json.Bool(s, V + "encoder_config.hdr.server_overrides_enable_hdr", false);
            hdrForceSrgb.Checked = Json.Bool(s, V + "encoder_config.hdr.force_hdr_srgb_correction", false);
            hdrClampRange.Checked = Json.Bool(s, V + "encoder_config.hdr.clamp_hdr_extended_range", false);

            // Fovéation côté client & Post-traitement
            clientFoveationEnabled.Checked = Json.Bool(s, V + "clientside_foveation.enabled", false);
            var cfMode = Json.Str(s, V + "clientside_foveation.content.mode.variant", "Dynamic");
            clientFoveationMode.SelectedItem = clientFoveationMode.Items.Contains(cfMode) ? cfMode : "Dynamic";
            clientFoveationOffset.Value = Clamp(clientFoveationOffset, Json.Num(s, V + "clientside_foveation.content.vertical_offset_deg", 0.0));
            var cfModeVariant = Json.Str(s, V + "clientside_foveation.content.mode.variant", "Dynamic");
            var cfLevelPath = cfModeVariant == "Static"
                ? V + "clientside_foveation.content.mode.Static.level.variant"
                : V + "clientside_foveation.content.mode.Dynamic.max_level.variant";
            var cfLevel = Json.Str(s, cfLevelPath, "High");
            clientFoveationLevel.SelectedItem = clientFoveationLevel.Items.Contains(cfLevel) ? cfLevel : "High";

            clientPostProcessingEnabled.Checked = Json.Bool(s, V + "clientside_post_processing.enabled", false);
            var cpSS = Json.Str(s, V + "clientside_post_processing.content.super_sampling.variant", "Quality");
            clientPostSuperSampling.SelectedItem = clientPostSuperSampling.Items.Contains(cpSS) ? cpSS : "Quality";
            var cpSh = Json.Str(s, V + "clientside_post_processing.content.sharpening.variant", "Quality");
            clientPostSharpening.SelectedItem = clientPostSharpening.Items.Contains(cpSh) ? cpSh : "Quality";

            // Passthrough tendu
            var ptMode = Json.Str(s, V + "passthrough.content.variant", "Blend");
            passthroughMode.SelectedItem = passthroughMode.Items.Contains(ptMode) ? ptMode : "Blend";
            passthroughBlendThreshold.Value = Clamp(passthroughBlendThreshold, Json.Num(s, V + "passthrough.content.Blend.threshold", 0.10));
            passthroughDistanceThreshold.Value = Clamp(passthroughDistanceThreshold, Json.Num(s, V + "passthrough.content.RgbChromaKey.distance_threshold", 175));
            passthroughFeathering.Value = Clamp(passthroughFeathering, Json.Num(s, V + "passthrough.content.RgbChromaKey.feathering", 1.0));
            foveatedForceEnable.Checked = Json.Bool(s, V + "foveated_encoding.content.force_enable", false);

            var spSplit = Json.Str(settings.Root, "video.split_encode", "off");
            splitEncode.SelectedItem = splitEncode.Items.Contains(spSplit) ? spSplit : "off";
            sliceCount.Value = Clamp(sliceCount, Json.Num(s, "openvr_config.nvenc_slice_count", Json.Num(settings.Root, "video.slice_count", 8)));
            var entMode = Json.Str(settings.Root, "video.entropy_coding", "cavlc");
            entropyCoding.SelectedItem = entropyCoding.Items.Contains(entMode) ? entMode : "cavlc";
            // the preset's own session.json is the reference (the host reads it); auraflashxr.json only fills what is missing
            var nqPreset = Json.Str(settings.Root, "video.nvenc_quality_preset", Json.Str(s, V + "encoder_config.nvenc.quality_preset.variant", "P1"));
            nvencQualityPreset.SelectedItem = nvencQualityPreset.Items.Contains(nqPreset) ? nqPreset : "P1";
            var ntArm = Json.Str(s, V + "encoder_config.nvenc.tuning_preset.variant", "LowLatency");
            var ntPreset = Json.Str(settings.Root, "video.nvenc_tuning_preset", ntArm == "HighQuality" ? "high-quality" : (ntArm == "UltraLowLatency" ? "ultra-low-latency" : "low-latency"));
            nvencTuningPreset.SelectedItem = nvencTuningPreset.Items.Contains(ntPreset) ? ntPreset : "low-latency";
            var nmArm = Json.Str(s, V + "encoder_config.nvenc.multi_pass.variant", "Disabled");
            var nmPass = Json.Str(settings.Root, "video.nvenc_multi_pass", nmArm == "FullResolution" ? "full-resolution" : (nmArm == "QuarterResolution" ? "quarter-resolution" : "disabled"));
            nvencMultiPass.SelectedItem = nvencMultiPass.Items.Contains(nmPass) ? nmPass : "disabled";
            vbvStrict.Checked = Json.Bool(settings.Root, "video.vbv_strict", true);
            qpMap.Checked = Json.Bool(settings.Root, "video.qp_map", false);
            gpuSchedClass.Value = Clamp(gpuSchedClass, Json.Num(settings.Root, "video.gpu_sched_class", 5));
            bitrateHeadroomPct.Value = Clamp(bitrateHeadroomPct, Json.Num(settings.Root, "video.bitrate_headroom_pct", 100));

            // Options avancées Cadencement & SSW / Uncapped
            uncappedMode.Checked = settings.FrameGenRate == "uncapped";
            frameGenCapFps.Value = Clamp(frameGenCapFps, Json.Num(settings.Root, "video.frame_gen_cap_fps", 0));
            frameGenLagMs.Value = Clamp(frameGenLagMs, Json.Num(settings.Root, "video.frame_gen_lag_ms", 0));
            frameGenFlowScale.Value = Clamp(frameGenFlowScale, Json.Num(settings.Root, "video.frame_gen_flow_scale", 0.5));
            var fgP = Json.Str(settings.Root, "video.frame_gen_perf", "fast");
            frameGenPerf.SelectedItem = frameGenPerf.Items.Contains(fgP) ? fgP : "fast";
            var fgS = Json.Str(settings.Root, "video.frame_gen_synthesis", "edge");
            frameGenSynthesis.SelectedItem = frameGenSynthesis.Items.Contains(fgS) ? fgS : "edge";
            frameGenHoldAge.Checked = Json.Bool(settings.Root, "video.frame_gen_hold_age", false);

            phaseLock.Checked = Json.Bool(settings.Root, "video.phase_lock", true);
            boundaryOffsetMs.Value = Clamp(boundaryOffsetMs, Json.Num(settings.Root, "video.boundary_offset_ms", 0));
            runningStartMs.Value = Clamp(runningStartMs, Json.Num(settings.Root, "video.running_start_ms", 2));
            intakeGate.Checked = Json.Bool(settings.Root, "video.intake_gate", true);
            intakeDepth.Value = Clamp(intakeDepth, Json.Num(settings.Root, "video.intake_depth", 3));
            var pPolicy = Json.Str(settings.Root, "video.pick_policy", "drain");
            pickPolicy.SelectedItem = pickPolicy.Items.Contains(pPolicy) ? pPolicy : "drain";
            clientQueueDrain.Checked = Json.Bool(settings.Root, "video.client_queue_drain", false);
            followClientRate.Checked = Json.Bool(settings.Root, "video.follow_client_rate", true);
            var sPacing = Json.Str(settings.Root, "video.send_pacing", "asap");
            sendPacing.SelectedItem = sendPacing.Items.Contains(sPacing) ? sPacing : "asap";
            ShowGate();

            (settings.EncodeProfile == "full-split" ? profFull : profFov).Checked = true;
            fcx.Value = Clamp(fcx, Json.Num(s, V + "foveated_encoding.content.center_size_x", 0.45));
            fcy.Value = Clamp(fcy, Json.Num(s, V + "foveated_encoding.content.center_size_y", 0.4));
            fsx.Value = Clamp(fsx, Json.Num(s, V + "foveated_encoding.content.center_shift_x", 0.4));
            fsy.Value = Clamp(fsy, Json.Num(s, V + "foveated_encoding.content.center_shift_y", 0.1));
            frx.Value = Clamp(frx, Json.Num(s, V + "foveated_encoding.content.edge_ratio_x", 4));
            fry.Value = Clamp(fry, Json.Num(s, V + "foveated_encoding.content.edge_ratio_y", 5));
            chromaOn.Checked = Chroma.Enabled(s);
            try { SetIdle(ColorTranslator.FromHtml("#" + Chroma.Rgb(s))); } catch { SetIdle(Color.FromArgb(0, 255, 0)); }
            idle.Enabled = chromaOn.Checked;

            // Audio & Système
            audio.Checked = Json.Bool(s, "session_settings.audio.game_audio.enabled", true);
            mute.Checked = settings.MuteSpeakers;
            captureBox.SelectedIndex = settings.AudioCapture == "device" ? 1 : 0;
            mic.Checked = Json.Bool(s, "session_settings.audio.microphone.enabled", false);
            gameAudioBufferMs.Value = Clamp(gameAudioBufferMs, Json.Num(s, "session_settings.audio.game_audio.content.buffering.average_buffering_ms", 50));
            micAudioBufferMs.Value = Clamp(micAudioBufferMs, Json.Num(s, "session_settings.audio.microphone.content.buffering.average_buffering_ms", 50));
            steamvrLifecycle.Checked = Json.Bool(s, "session_settings.extra.steamvr_launcher.open_close_steamvr_with_dashboard", true);

            // Remplissage dynamique des priphriques Audio Windows (WASAPI / IMMDeviceEnumerator)
            audioPlaybackDevice.Items.Clear();
            audioPlaybackDevice.Items.Add(Lang.Get("Settings.Audio.DefaultWindows"));
            try
            {
                foreach (var d in AudioRoute.Endpoints.Render())
                    audioPlaybackDevice.Items.Add(d.Name);
            }
            catch { }
            string curPlayDevice = Json.Str(s, "session_settings.audio.game_audio.content.device.content.NameSubstring", "");
            int pIdx = 0;
            if (!string.IsNullOrEmpty(curPlayDevice))
            {
                for (int i = 1; i < audioPlaybackDevice.Items.Count; i++)
                {
                    if (audioPlaybackDevice.Items[i].ToString().IndexOf(curPlayDevice, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pIdx = i;
                        break;
                    }
                }
            }
            audioPlaybackDevice.SelectedIndex = pIdx;

            audioMicDevice.Items.Clear();
            audioMicDevice.Items.Add(Lang.Get("Settings.Audio.DefaultMic"));
            try
            {
                foreach (var d in AudioRoute.Endpoints.Capture())
                    audioMicDevice.Items.Add(d.Name);
            }
            catch { }
            string curMicDevice = Json.Str(s, "session_settings.audio.microphone.content.devices.Custom.source.NameSubstring", "");
            if (string.IsNullOrEmpty(curMicDevice))
                curMicDevice = Json.Str(s, "session_settings.audio.microphone.content.devices.Custom.sink.NameSubstring", "");
            int mIdx = 0;
            if (!string.IsNullOrEmpty(curMicDevice))
            {
                for (int i = 1; i < audioMicDevice.Items.Count; i++)
                {
                    if (audioMicDevice.Items[i].ToString().IndexOf(curMicDevice, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mIdx = i;
                        break;
                    }
                }
            }
            audioMicDevice.SelectedIndex = mIdx;

            plans = PowerPlan.List();
            planBox.Items.Clear(); planBox.Items.Add(Lang.Get("Settings.Combo.PowerUnchanged"));
            foreach (var p in plans) planBox.Items.Add(p.Name);
            var cur = plans.FindIndex(p => string.Equals(p.Guid, settings.PowerPlan, StringComparison.OrdinalIgnoreCase));
            planBox.SelectedIndex = cur >= 0 ? cur + 1 : 0;

            // Réseau
            proto.SelectedItem = Json.Str(s, "session_settings.connection.stream_protocol.variant", "Udp") == "Tcp" ? "Tcp" : "Udp";
            packet.Value = Clamp(packet, Json.Num(s, "session_settings.connection.packet_size", 1400));
            netDscp.Checked = Json.Bool(s, "session_settings.connection.dscp.set", true);
            netAutoTrust.Checked = Json.Bool(s, "session_settings.connection.client_discovery.content.auto_trust_clients", false);
            netAvoidGlitching.Checked = Json.Bool(s, "session_settings.connection.avoid_video_glitching", false);

            // Cadencement & Pacing
            serverFramePacing.Checked = Json.Bool(s, V + "enforce_server_frame_pacing", true);

            // Onglet Spcial Manettes & Casque
            var posRecenter = Json.Str(s, "session_settings.headset.position_recentering_mode.variant", "LocalFloor");
            posRecenteringMode.SelectedItem = posRecenteringMode.Items.Contains(posRecenter) ? posRecenter : "LocalFloor";
            posRecenterViewHeight.Value = Clamp(posRecenterViewHeight, Json.Num(s, "session_settings.headset.position_recentering_mode.Local.view_height", 1.5));
            var rotRecenter = Json.Str(s, "session_settings.headset.rotation_recentering_mode.variant", "Yaw");
            rotRecenteringMode.SelectedItem = rotRecenteringMode.Items.Contains(rotRecenter) ? rotRecenter : "Yaw";

            ctrlEnabled.Checked = Json.Bool(s, "session_settings.headset.controllers.enabled", true);
            ctrlTracked.Checked = Json.Bool(s, "session_settings.headset.controllers.content.tracked", true);
            handSkeleton.Checked = Json.Bool(s, "session_settings.headset.controllers.content.hand_skeleton.enabled", false);
            handSkeletonInput2.Checked = Json.Bool(s, "session_settings.headset.controllers.content.hand_skeleton.content.steamvr_input_2_0", false);
            handSkeletonPredict.Checked = Json.Bool(s, "session_settings.headset.controllers.content.hand_skeleton.content.predict", false);
            multimodalTracking.Checked = Json.Bool(s, "session_settings.headset.controllers.content.multimodal_tracking", false);

            handInteraction.Checked = Json.Bool(s, "session_settings.headset.controllers.content.hand_tracking_interaction.enabled", false);
            handOnlyTouch.Checked = Json.Bool(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.only_touch", false);
            pinchTouchDist.Value = Clamp(pinchTouchDist, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.pinch_touch_distance", 0.0));
            pinchTriggerDist.Value = Clamp(pinchTriggerDist, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.pinch_trigger_distance", 0.25));
            curlTouchDist.Value = Clamp(curlTouchDist, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.curl_touch_distance", 2.0));
            curlTriggerDist.Value = Clamp(curlTriggerDist, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.curl_trigger_distance", 2.5));
            joyDeadzone.Value = Clamp(joyDeadzone, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_deadzone", 40.0));
            joyOffsetH.Value = Clamp(joyOffsetH, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_offset_horizontal", 0.0));
            joyOffsetV.Value = Clamp(joyOffsetV, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_offset_vertical", 0.0));
            joyRange.Value = Clamp(joyRange, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_range", 1.0));
            actDelay.Value = Clamp(actDelay, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.activation_delay", 50));
            deactDelay.Value = Clamp(deactDelay, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.deactivation_delay", 100));
            repDelay.Value = Clamp(repDelay, Json.Num(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.repeat_delay", 100));

            steamvrPipelineFrames.Value = Clamp(steamvrPipelineFrames, Json.Num(s, "session_settings.headset.controllers.content.steamvr_pipeline_frames", 2.1));
            hapticsEnabled.Checked = Json.Bool(s, "session_settings.headset.controllers.content.haptics.enabled", true);
            haptics.Value = Clamp(haptics, Json.Num(s, "session_settings.headset.controllers.content.haptics.content.intensity_multiplier", 1.0));
            hapticsCurve.Value = Clamp(hapticsCurve, Json.Num(s, "session_settings.headset.controllers.content.haptics.content.amplitude_curve", 1.0));
            hapticsMinDuration.Value = Clamp(hapticsMinDuration, Json.Num(s, "session_settings.headset.controllers.content.haptics.content.min_duration_s", 0.01));

            var ctrlEmu = Json.Str(s, "session_settings.headset.controllers.content.emulation_mode.variant", "Quest3Plus");
            controllerEmulation.SelectedItem = controllerEmulation.Items.Contains(ctrlEmu) ? ctrlEmu : "Quest3Plus";

            linearVelocityCutoff.Value = Clamp(linearVelocityCutoff, Json.Num(s, "session_settings.headset.controllers.content.linear_velocity_cutoff", 0.05));
            angularVelocityCutoff.Value = Clamp(angularVelocityCutoff, Json.Num(s, "session_settings.headset.controllers.content.angular_velocity_cutoff", 10.0));

            // Offsets Manette (Position & Rotation)
            var cPos = Json.Get(s, "session_settings.headset.controllers.content.left_controller_position_offset.content") as System.Collections.IEnumerable;
            if (cPos != null) { var arr = cPos.Cast<object>().ToArray(); if (arr.Length >= 3) { ctrlPosX.Value = Clamp(ctrlPosX, Convert.ToDouble(arr[0])); ctrlPosY.Value = Clamp(ctrlPosY, Convert.ToDouble(arr[1])); ctrlPosZ.Value = Clamp(ctrlPosZ, Convert.ToDouble(arr[2])); } }
            var cRot = Json.Get(s, "session_settings.headset.controllers.content.left_controller_rotation_offset.content") as System.Collections.IEnumerable;
            if (cRot != null) { var arr = cRot.Cast<object>().ToArray(); if (arr.Length >= 3) { ctrlRotX.Value = Clamp(ctrlRotX, Convert.ToDouble(arr[0])); ctrlRotY.Value = Clamp(ctrlRotY, Convert.ToDouble(arr[1])); ctrlRotZ.Value = Clamp(ctrlRotZ, Convert.ToDouble(arr[2])); } }

            // Offsets Mains (Position & Rotation)
            var hPos = Json.Get(s, "session_settings.headset.controllers.content.left_hand_tracking_position_offset.content") as System.Collections.IEnumerable;
            if (hPos != null) { var arr = hPos.Cast<object>().ToArray(); if (arr.Length >= 3) { handPosX.Value = Clamp(handPosX, Convert.ToDouble(arr[0])); handPosY.Value = Clamp(handPosY, Convert.ToDouble(arr[1])); handPosZ.Value = Clamp(handPosZ, Convert.ToDouble(arr[2])); } }
            var hRot = Json.Get(s, "session_settings.headset.controllers.content.left_hand_tracking_rotation_offset.content") as System.Collections.IEnumerable;
            if (hRot != null) { var arr = hRot.Cast<object>().ToArray(); if (arr.Length >= 3) { handRotX.Value = Clamp(handRotX, Convert.ToDouble(arr[0])); handRotY.Value = Clamp(handRotY, Convert.ToDouble(arr[1])); handRotZ.Value = Clamp(handRotZ, Convert.ToDouble(arr[2])); } }

            clickThreshold.Value = Clamp(clickThreshold, Json.Num(s, "session_settings.headset.controllers.content.button_mapping_config.click_threshold.value", 0.5));
            touchThreshold.Value = Clamp(touchThreshold, Json.Num(s, "session_settings.headset.controllers.content.button_mapping_config.touch_threshold.value", 0.1));
            forceThreshold.Value = Clamp(forceThreshold, Json.Num(s, "session_settings.headset.controllers.content.button_mapping_config.force_threshold", 0.8));

            var hsEmu = Json.Str(s, "session_settings.headset.emulation_mode.variant", "QuestPro");
            headsetEmulation.SelectedItem = headsetEmulation.Items.Contains(hsEmu) ? hsEmu : "QuestPro";

            trackingRefOnly.Checked = Json.Bool(s, "session_settings.headset.tracking_ref_only", false);
            enableViveTrackerProxy.Checked = Json.Bool(s, "session_settings.headset.enable_vive_tracker_proxy", false);
            bodyTracking.Checked = Json.Bool(s, "session_settings.headset.body_tracking.enabled", false);
            faceTracking.Checked = Json.Bool(s, "session_settings.headset.face_tracking.enabled", false);
            vmcEnabled.Checked = Json.Bool(s, "session_settings.headset.vmc.enabled", false);
            maxPredictionMs.Value = Clamp(maxPredictionMs, Json.Num(s, "session_settings.headset.max_prediction_ms", 100));

            haptics.Value = Clamp(haptics, Json.Num(s, "session_settings.headset.controllers.content.haptics.content.intensity_multiplier", 1));
            // load new fields
            adapterIndex.Value = Clamp(adapterIndex, Json.Num(s, V + "adapter_index", 0));
        // PyroWave (auraflashxr.json -> video.pyrowave) - read through the AppSettings properties
        // (they resolve the "video.pyrowave.*" paths; reading Root["pyrowave"] directly would miss the "video" level).
        pyroEnabled.Checked = settings.PyrowaveEnabled;
        pyroChroma.Items.Clear();
        pyroChroma.Items.AddRange(new object[] { "420", "444" });
        pyroChroma.SelectedItem = settings.PyrowaveChroma;
        pyroMaxKb.Value = Clamp(pyroMaxKb, settings.PyrowaveMaxKb);
        pyroPacketSize.Value = Clamp(pyroPacketSize, settings.PyrowavePacketBytes);
            bitrateSaturationMultiplier.Value = Clamp(bitrateSaturationMultiplier, Json.Num(s, V + "bitrate.mode.Adaptive.saturation_multiplier", 1.0));
            amfEnableHmqb.Checked = Json.Bool(s, V + "encoder_config.amf.enable_hmqb", false);
            amfUsePreproc.Checked = Json.Bool(s, V + "encoder_config.amf.use_preproc", false);
            amfPreprocSigma.Value = Clamp(amfPreprocSigma, Json.Num(s, V + "encoder_config.amf.preproc_sigma", 0));
            amfPreprocTor.Value = Clamp(amfPreprocTor, Json.Num(s, V + "encoder_config.amf.preproc_tor", 0));
            amfEnablePreAnalysis.Checked = Json.Bool(s, V + "encoder_config.amf.enable_pre_analysis", false);
            swForceEncoding.Checked = Json.Bool(s, V + "encoder_config.software.force_software_encoding", false);
            swThreadCount.Value = Clamp(swThreadCount, Json.Num(s, V + "encoder_config.software.thread_count", 0));
            colorGamma.Value = Clamp(colorGamma, Json.Num(s, V + "color_correction.content.gamma", 1.0));
            colorSharpening.Value = Clamp(colorSharpening, Json.Num(s, V + "color_correction.content.sharpening", 0.0));
            streamPort.Value = Clamp(streamPort, Json.Num(s, "session_settings.connection.stream_port", 9944));
            webServerPort.Value = Clamp(webServerPort, Json.Num(s, "session_settings.connection.web_server_port", 8082));
            oscLocalPort.Value = Clamp(oscLocalPort, Json.Num(s, "session_settings.connection.osc_local_port", 9942));
            maxQueuedVideoFrames.Value = Clamp(maxQueuedVideoFrames, Json.Num(s, "session_settings.connection.max_queued_server_video_frames", 1024));
            statisticsHistorySize.Value = Clamp(statisticsHistorySize, Json.Num(s, "session_settings.connection.statistics_history_size", 256));
            minimumIdrIntervalMs.Value = Clamp(minimumIdrIntervalMs, Json.Num(s, "session_settings.connection.minimum_idr_interval_ms", 100));
            enableOnConnectScript.Checked = Json.Bool(s, "session_settings.connection.enable_on_connect_script", false);
            enableOnDisconnectScript.Checked = Json.Bool(s, "session_settings.connection.enable_on_disconnect_script", false);
            allowUntrustedHttp.Checked = Json.Bool(s, "session_settings.connection.allow_untrusted_http", false);
            clientDiscoveryEnabled.Checked = Json.Bool(s, "session_settings.connection.client_discovery.enabled", true);
            { string wct = Json.Str(s, "session_settings.connection.wired_client_type.variant", "Store"); wiredClientType.SelectedItem = wct == "Github" ? Lang.Get("Settings.Label.Github") : Lang.Get("Settings.Label.Store"); if (wiredClientType.SelectedIndex < 0) wiredClientType.SelectedIndex = 0; }
            wiredClientAutolaunch.Checked = Json.Bool(s, "session_settings.connection.wired_client_autolaunch", true);
            { string m = Json.Str(s, "session_settings.connection.server_send_buffer_bytes.variant", "Default"); streamerSendBufferMode.SelectedIndex = m == "Maximum" ? 1 : (m == "Custom" ? 2 : 0); streamerSendBufferBytes.Value = Clamp(streamerSendBufferBytes, Json.Num(s, "session_settings.connection.server_send_buffer_bytes.content", 100000)); }
            { string m = Json.Str(s, "session_settings.connection.server_recv_buffer_bytes.variant", "Default"); streamerRecvBufferMode.SelectedIndex = m == "Maximum" ? 1 : (m == "Custom" ? 2 : 0); streamerRecvBufferBytes.Value = Clamp(streamerRecvBufferBytes, Json.Num(s, "session_settings.connection.server_recv_buffer_bytes.content", 100000)); }
            { string m = Json.Str(s, "session_settings.connection.client_send_buffer_bytes.variant", "Default"); clientSendBufferMode.SelectedIndex = m == "Maximum" ? 1 : (m == "Custom" ? 2 : 0); clientSendBufferBytes.Value = Clamp(clientSendBufferBytes, Json.Num(s, "session_settings.connection.client_send_buffer_bytes.content", 100000)); }
            { string m = Json.Str(s, "session_settings.connection.client_recv_buffer_bytes.variant", "Default"); clientRecvBufferMode.SelectedIndex = m == "Maximum" ? 1 : (m == "Custom" ? 2 : 0); clientRecvBufferBytes.Value = Clamp(clientRecvBufferBytes, Json.Num(s, "session_settings.connection.client_recv_buffer_bytes.content", 100000)); }
            { bool dscpSet = Json.Bool(s, "session_settings.connection.dscp.set", true); dscpMode.SelectedIndex = dscpSet ? 1 : 0; netDscp.Checked = dscpSet; }
            muteWhenStreaming.Checked = Json.Bool(s, "session_settings.audio.game_audio.content.mute_when_streaming", false);
            gameAudioBatchMs.Value = Clamp(gameAudioBatchMs, Json.Num(s, "session_settings.audio.game_audio.content.buffering.batch_ms", 20));
            micAudioBatchMs.Value = Clamp(micAudioBatchMs, Json.Num(s, "session_settings.audio.microphone.content.buffering.batch_ms", 20));
            velocitiesMultiplier.Value = Clamp(velocitiesMultiplier, Json.Num(s, "session_settings.extra.velocities_multiplier", 1.0));            debugDefault.Checked = settings.Debug;
        }

        void LaunchMicInstallScript()
        {
            string scriptPath = Path.Combine(Paths.Dir, "tools", "install", "install_auraflashxr_mic.bat");
            if (!File.Exists(scriptPath))
            {
                // Essai alternatif relatif au rpertoire de l'application
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "install", "install_auraflashxr_mic.bat");
            }
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(Paths.Dir, "..", "tools", "install", "install_auraflashxr_mic.bat");
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.MicScriptMissing") + scriptPath, Lang.Get("Settings.Msg.MicScriptMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    Verb = "runas" // Demander les privil\u00E8ges administrateur si n\u00E9cessaire
                };
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit();
                }

                // Recharger la liste des priphriques audio aprs l'installation et le renommage
                ReloadAudioEndpoints();

                MessageBox.Show(this, Lang.Get("Settings.Msg.MicInstalled"), Lang.Get("Settings.Msg.MicInstalledTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.MicRunError") + ex.Message, Lang.Get("Settings.Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void LaunchMicUninstallScript()
        {
            string scriptPath = Path.Combine(Paths.Dir, "tools", "install", "uninstall_auraflashxr_mic.bat");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "install", "uninstall_auraflashxr_mic.bat");
            }
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(Paths.Dir, "..", "tools", "install", "uninstall_auraflashxr_mic.bat");
            }

            if (!File.Exists(scriptPath))
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.MicUninstallScriptMissing") + scriptPath, Lang.Get("Settings.Msg.MicScriptMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show(this, Lang.Get("Settings.Msg.MicUninstallConfirm"), Lang.Get("Settings.Msg.MicUninstallTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit();
                }

                ReloadAudioEndpoints();

                MessageBox.Show(this, Lang.Get("Settings.Msg.MicUninstalled"), Lang.Get("Settings.Msg.MicUninstalledTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.MicRunError") + ex.Message, Lang.Get("Settings.Msg.ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ReloadAudioEndpoints()
        {
            try
            {
                audioMicDevice.Items.Clear();
                audioMicDevice.Items.Add(Lang.Get("Settings.Audio.DefaultMic"));
                int mIdx = 0;
                int idx = 1;
                foreach (var d in AudioRoute.Endpoints.Capture())
                {
                    audioMicDevice.Items.Add(d.Name);
                    if (d.Name.IndexOf("AuraFlashXR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        d.Name.IndexOf("CABLE Output", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mIdx = idx;
                    }
                    idx++;
                }
                audioMicDevice.SelectedIndex = mIdx;

                audioPlaybackDevice.Items.Clear();
                audioPlaybackDevice.Items.Add(Lang.Get("Settings.Audio.DefaultWindows"));
                foreach (var d in AudioRoute.Endpoints.Render())
                {
                    audioPlaybackDevice.Items.Add(d.Name);
                }
                audioPlaybackDevice.SelectedIndex = 0;
            }
            catch { }
        }

        bool SaveSettings(bool notify)
        {
            // Protection ne pas sauvegarder si un profil en lecture seule est charg (DEFAULT (USINE) ou OPTI CONFIG)
            // built-in presets are editable like any other preset (no read-only protection)

            session = Session.Load();
            var s = session;
            // the file is read by serde_json on the host: one key the schema does not know makes the whole load fail
            // (the host then silently keeps its defaults, e.g. no foveation, CBR 300 Mbps). Drop what the schema refuses.
            foreach (var structPath in new[] {
                "session_settings.video.foveated_encoding.content",
                "session_settings.video.bitrate.mode",
                "session_settings.video.bitrate.mode.Adaptive",
                "session_settings.video.bitrate",
                "session_settings.video.bitrate.adapt_to_framerate",
                "session_settings.video.bitrate.adapt_to_framerate.content",
                "session_settings.video.encoder_config",
                "session_settings.video.encoder_config.nvenc" })
            {
                int gone = Json.Prune(s, structPath);
                if (gone > 0) Log.Write("WARN", "session.json: " + gone + " clé(s) hors schéma retirée(s) de " + structPath);
            }
            // width rounded to 32 for the encoder, except the Quest 3 native 2064 kept as is
            int w = (int)eyeW.Value == 2064 ? 2064 : (int)eyeW.Value / 32 * 32, h = (int)eyeH.Value;
            string codecStr = (string)preferredCodec.SelectedItem ?? "H264";
            var err = Nvenc.CheckEye(w, h, codecStr);
            if (err != null) { MessageBox.Show(this, err, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            var rate = double.Parse((string)fps.SelectedItem, CultureInfo.InvariantCulture);
            foreach (var k in new[] { "transcoding_view_resolution", "emulated_headset_view_resolution" })
            {
                Json.Set(s, V + k + ".variant", "Absolute");
                Json.Set(s, V + k + ".Absolute.width", w);
                Json.Set(s, V + k + ".Absolute.height.set", true);
                Json.Set(s, V + k + ".Absolute.height.content", h);
            }
            Json.Set(s, V + "preferred_fps", rate);

            // BitrateConfig is a struct whose "mode" member is the BitrateMode enum: "mode" keeps the enum's variant, the
            // values sit next to it (ConstantMbps) or under it (Adaptive).
            string bVariant = (string)bitrateMode.SelectedItem ?? "ConstantMbps";
            Json.Set(s, V + "bitrate.mode.variant", bVariant);
            Json.Set(s, V + "bitrate.mode.ConstantMbps", (int)bitrate.Value);
            // the Adaptive arm of the enum: its struct sits under bitrate.mode.Adaptive
            Json.Set(s, V + "bitrate.mode.Adaptive.max_throughput_mbps.enabled", adaptiveMaxThroughputEnabled.Checked);
            Json.Set(s, V + "bitrate.mode.Adaptive.max_throughput_mbps.content", (int)adaptiveMaxThroughput.Value);
            Json.Set(s, V + "bitrate.mode.Adaptive.min_throughput_mbps.enabled", adaptiveMinThroughputEnabled.Checked);
            Json.Set(s, V + "bitrate.mode.Adaptive.min_throughput_mbps.content", (int)adaptiveMinThroughput.Value);
            Json.Set(s, V + "bitrate.mode.Adaptive.max_network_latency_ms.enabled", adaptiveMaxLatencyEnabled.Checked);
            Json.Set(s, V + "bitrate.mode.Adaptive.max_network_latency_ms.content", (int)adaptiveMaxLatency.Value);
            Json.Set(s, V + "bitrate.mode.Adaptive.saturation_multiplier", (double)bitrateSaturationMultiplier.Value);
            Json.Set(s, V + "bitrate.history_size", (int)adaptiveHistorySize.Value);
            Json.Set(s, V + "bitrate.image_corruption_fix", adaptiveImageCorruptionFix.Checked);
            Json.Set(s, V + "bitrate.adapt_to_framerate.enabled", adaptToFramerate.Checked);
            Json.Set(s, V + "buffering_history_weight", (double)bufferingHistoryWeight.Value);

            // Switch<FoveatedEncodingConfig>: "enabled" + "content" (the shape the shipped presets use)
            Json.Set(s, V + "foveated_encoding.enabled", profFov.Checked);
            Json.Set(s, V + "foveated_encoding.content.force_enable", foveatedForceEnable.Checked);
            Json.Set(s, V + "foveated_encoding.content.center_size_x", (double)fcx.Value);
            Json.Set(s, V + "foveated_encoding.content.center_size_y", (double)fcy.Value);
            Json.Set(s, V + "foveated_encoding.content.center_shift_x", (double)fsx.Value);
            Json.Set(s, V + "foveated_encoding.content.center_shift_y", (double)fsy.Value);
            Json.Set(s, V + "foveated_encoding.content.edge_ratio_x", (double)frx.Value);
            Json.Set(s, V + "foveated_encoding.content.edge_ratio_y", (double)fry.Value);
            // Virtual Desktop mappings
            settings.FrameGen = sswMode.SelectedIndex > 0;
            settings.FrameGenRecommended = sswMode.SelectedIndex == 1;
            settings.Sharpening = vdSharpening.Value / 100.0;
            settings.Gamma = vdGamma.Value / 100.0;

            Json.Set(s, V + "upscaling.enabled", vdSnapdragonSuperRes.Checked);
            Json.Set(s, V + "upscaling.content.edge_direction", upscalingEdgeDirection.Checked);
            Json.Set(s, V + "upscaling.content.edge_threshold", (int)upscalingEdgeThreshold.Value);
            Json.Set(s, V + "upscaling.content.edge_sharpness", (double)upscalingEdgeSharpness.Value);
            Json.Set(s, V + "upscaling.content.upscale_factor", (double)upscalingFactor.Value);
            Json.Set(s, V + "max_buffering_frames", vdVideoBuffering.Checked ? 2.0 : 1.0);
            Json.Set(s, "session_settings.headset.stage_tracking", vdStageTracking.Checked);
            Json.Set(s, "session_settings.headset.controllers.enabled", vdTrackControllers.Checked);
            Json.Set(s, V + "color_correction.enabled", vdColorVibrance.Checked || colorBrightness.Value != 0m || colorContrast.Value != 0m || colorSaturation.Value != 0m);
            Json.Set(s, V + "color_correction.content.brightness", (double)colorBrightness.Value);
            Json.Set(s, V + "color_correction.content.contrast", (double)colorContrast.Value);
            Json.Set(s, V + "color_correction.content.saturation", vdColorVibrance.Checked ? Math.Max((double)colorSaturation.Value, 0.35) : (double)colorSaturation.Value);
            Json.Set(s, "openvr_config.use_full_range_encoding", vdNominalRange.Checked);

            // Synchronisation openvr_config pour les composants runtime C++ / OpenVR
            Json.Set(s, "openvr_config.eye_resolution_width", w);
            Json.Set(s, "openvr_config.eye_resolution_height", h);
            Json.Set(s, "openvr_config.target_eye_resolution_width", w);
            Json.Set(s, "openvr_config.target_eye_resolution_height", h);
            Json.Set(s, "openvr_config.refresh_rate", (int)rate);

            // Paramètres Vidéo & NVENC
            Json.Set(s, V + "preferred_codec.variant", codecStr);
            Json.Set(s, "openvr_config.codec", codecStr == "Hevc" ? 1 : (codecStr == "AV1" ? 2 : 0));

            Json.Set(s, V + "encoder_config.quality_preset.variant", (string)encoderQualityPreset.SelectedItem ?? "Quality");
            Json.Set(s, V + "encoder_config.use_10bit", use10Bit.Checked);
            Json.Set(s, V + "encoder_config.server_overrides_use_10bit", serverOverrides10Bit.Checked);
            Json.Set(s, "openvr_config.use_10bit_encoder", use10Bit.Checked);
            Json.Set(s, V + "encoder_config.use_full_range", useFullRange.Checked);
            Json.Set(s, V + "encoder_config.server_overrides_use_full_range", serverOverridesFullRange.Checked);
            Json.Set(s, V + "encoder_config.encoding_gamma", (double)encodingGamma.Value);
            Json.Set(s, V + "encoder_config.server_overrides_encoding_gamma", serverOverridesEncodingGamma.Checked);
            Json.Set(s, V + "encoder_config.filler_data", fillerData.Checked);

            // HDR
            Json.Set(s, V + "encoder_config.hdr.enable_hdr", hdrEnabled.Checked);
            Json.Set(s, V + "encoder_config.hdr.server_overrides_enable_hdr", hdrServerOverrides.Checked);
            Json.Set(s, V + "encoder_config.hdr.force_hdr_srgb_correction", hdrForceSrgb.Checked);
            Json.Set(s, V + "encoder_config.hdr.clamp_hdr_extended_range", hdrClampRange.Checked);

            // Clientside Foveation & Post Processing
            Json.Set(s, V + "clientside_foveation.enabled", clientFoveationEnabled.Checked);
            Json.Set(s, V + "clientside_foveation.content.mode.variant", (string)clientFoveationMode.SelectedItem ?? "Dynamic");
            Json.Set(s, V + "clientside_foveation.content.vertical_offset_deg", (double)clientFoveationOffset.Value);
            var cfModeSave = (string)clientFoveationMode.SelectedItem ?? "Dynamic";
            var cfLevelSave = (string)clientFoveationLevel.SelectedItem ?? "High";
            if (cfModeSave == "Static")
                Json.Set(s, V + "clientside_foveation.content.mode.Static.level.variant", cfLevelSave);
            else
                Json.Set(s, V + "clientside_foveation.content.mode.Dynamic.max_level.variant", cfLevelSave);

            Json.Set(s, V + "clientside_post_processing.enabled", clientPostProcessingEnabled.Checked);
            Json.Set(s, V + "clientside_post_processing.content.super_sampling.variant", (string)clientPostSuperSampling.SelectedItem ?? "Quality");
            Json.Set(s, V + "clientside_post_processing.content.sharpening.variant", (string)clientPostSharpening.SelectedItem ?? "Quality");

            // Passthrough tendu
            Json.Set(s, V + "passthrough.content.variant", (string)passthroughMode.SelectedItem ?? "Blend");
            Json.Set(s, V + "passthrough.content.Blend.threshold", (double)passthroughBlendThreshold.Value);
            Json.Set(s, V + "passthrough.content.RgbChromaKey.distance_threshold", (int)passthroughDistanceThreshold.Value);
            Json.Set(s, V + "passthrough.content.RgbChromaKey.feathering", (double)passthroughFeathering.Value);

            Json.Set(s, V + "encoder_config.h264_profile.variant", (string)h264Profile.SelectedItem ?? "High");
            Json.Set(s, "openvr_config.h264_profile", (string)h264Profile.SelectedItem == "Main" ? 1 : ((string)h264Profile.SelectedItem == "Baseline" ? 2 : 0));

            Json.Set(s, V + "encoder_config.rate_control_mode.variant", (string)rateControlMode.SelectedItem ?? "Cbr");
            Json.Set(s, "openvr_config.rate_control_mode", (string)rateControlMode.SelectedItem == "Vbr" ? 1 : 0);

            Json.Set(s, V + "encoder_config.nvenc.adaptive_quantization_mode.variant", (string)nvencAqMode.SelectedItem ?? "Spatial");
            Json.Set(s, "openvr_config.nvenc_adaptive_quantization_mode", (string)nvencAqMode.SelectedItem == "Temporal" ? 2 : ((string)nvencAqMode.SelectedItem == "Disabled" ? 0 : 1));

            Json.Set(s, V + "encoder_config.enable_vbaq", enableVbaq.Checked);
            Json.Set(s, "openvr_config.enable_vbaq", enableVbaq.Checked);

            Json.Set(s, V + "force_software_decoder", forceSoftwareDecoder.Checked);
            Json.Set(s, V + "enforce_server_frame_pacing", serverFramePacing.Checked);

            // NVENC bas-niveau
            Json.Set(s, V + "encoder_config.nvenc.low_delay_key_frame_scale", (int)nvencLowDelayKeyFrameScale.Value);
            Json.Set(s, V + "encoder_config.nvenc.enable_intra_refresh", nvencEnableIntraRefresh.Checked);
            Json.Set(s, V + "encoder_config.nvenc.intra_refresh_period", (int)nvencIntraRefreshPeriod.Value);
            Json.Set(s, V + "encoder_config.nvenc.intra_refresh_count", (int)nvencIntraRefreshCount.Value);
            Json.Set(s, V + "encoder_config.nvenc.max_num_ref_frames", (int)nvencMaxNumRefFrames.Value);
            Json.Set(s, V + "encoder_config.nvenc.gop_length", (int)nvencGopLength.Value);
            Json.Set(s, V + "encoder_config.nvenc.p_frame_strategy", (int)nvencPFrameStrategy.Value);
            Json.Set(s, V + "encoder_config.nvenc.rc_buffer_size", (int)nvencRcBufferSize.Value);
            Json.Set(s, V + "encoder_config.nvenc.rc_initial_delay", (int)nvencRcInitialDelay.Value);
            Json.Set(s, V + "encoder_config.nvenc.rc_max_bitrate", (int)nvencRcMaxBitrate.Value);
            Json.Set(s, V + "encoder_config.nvenc.rc_average_bitrate", (int)nvencRcAverageBitrate.Value);
            Json.Set(s, V + "encoder_config.nvenc.enable_weighted_prediction", nvencWeightedPrediction.Checked);

            // Synchronisation des options avancées NVENC (auraflashxr.json + session.json)
            string spMode = (string)splitEncode.SelectedItem ?? "off";
            Json.Set(settings.Root, "video.split_encode", spMode);
            Json.Set(s, "openvr_config.nvenc_split_encode_mode", spMode == "on" ? 2 : (spMode == "auto" ? 1 : 0));

            int slices = (int)sliceCount.Value;
            Json.Set(settings.Root, "video.slice_count", slices);
            Json.Set(s, "openvr_config.nvenc_slice_count", slices);

            string ent = (string)entropyCoding.SelectedItem ?? "cavlc";
            Json.Set(settings.Root, "video.entropy_coding", ent);
            Json.Set(s, "openvr_config.entropy_coding", ent == "cabac" ? 0 : 1);
            Json.Set(s, V + "encoder_config.entropy_coding.variant", ent == "cabac" ? "Cabac" : "Cavlc");

            string nqp = (string)nvencQualityPreset.SelectedItem ?? "P1";
            Json.Set(settings.Root, "video.nvenc_quality_preset", nqp);
            int nqpInt = 1;
            if (int.TryParse(nqp.TrimStart('P'), out int parsedP)) nqpInt = parsedP;
            Json.Set(s, "openvr_config.nvenc_quality_preset", nqpInt);
            Json.Set(s, V + "encoder_config.nvenc.quality_preset.variant", nqp);

            string ntp = (string)nvencTuningPreset.SelectedItem ?? "ultra-low-latency";
            Json.Set(settings.Root, "video.nvenc_tuning_preset", ntp);
            Json.Set(s, "openvr_config.nvenc_tuning_preset", ntp == "high-quality" ? 1 : (ntp == "low-latency" ? 2 : 3));
            Json.Set(s, V + "encoder_config.nvenc.tuning_preset.variant", ntp == "high-quality" ? "HighQuality" : (ntp == "low-latency" ? "LowLatency" : "UltraLowLatency"));

            string nmp = (string)nvencMultiPass.SelectedItem ?? "disabled";
            Json.Set(settings.Root, "video.nvenc_multi_pass", nmp);
            Json.Set(s, "openvr_config.nvenc_multi_pass", nmp == "full-resolution" ? 2 : (nmp == "quarter-resolution" ? 1 : 0));
            Json.Set(s, V + "encoder_config.nvenc.multi_pass.variant", nmp == "full-resolution" ? "FullResolution" : (nmp == "quarter-resolution" ? "QuarterResolution" : "Disabled"));

            Json.Set(settings.Root, "video.vbv_strict", vbvStrict.Checked);
            Json.Set(s, "openvr_config.nvenc_strict_vbv", vbvStrict.Checked);

            Json.Set(settings.Root, "video.qp_map", qpMap.Checked);
            Json.Set(settings.Root, "video.gpu_sched_class", (int)gpuSchedClass.Value);
            Json.Set(settings.Root, "video.bitrate_headroom_pct", (int)bitrateHeadroomPct.Value);

            // Synchronisation des options avancées Cadencement & SSW / Uncapped
            if (uncappedMode.Checked)
            {
                var cur = settings.FrameGenRate;
                if (cur != "uncapped") Json.Set(settings.Root, "video.frame_gen_rate_before_uncapped", cur);
                settings.FrameGenRate = "uncapped";
            }
            else if (settings.FrameGenRate == "uncapped")
            {
                var b = Json.Str(settings.Root, "video.frame_gen_rate_before_uncapped", "half");
                settings.FrameGenRate = b == "uncapped" ? "half" : b;
            }
            Json.Set(settings.Root, "video.frame_gen_cap_fps", (double)frameGenCapFps.Value);
            Json.Set(settings.Root, "video.frame_gen_lag_ms", (double)frameGenLagMs.Value);
            Json.Set(settings.Root, "video.frame_gen_flow_scale", (double)frameGenFlowScale.Value);
            Json.Set(settings.Root, "video.frame_gen_perf", (string)frameGenPerf.SelectedItem ?? "fast");
            Json.Set(settings.Root, "video.frame_gen_synthesis", (string)frameGenSynthesis.SelectedItem ?? "edge");
            Json.Set(settings.Root, "video.frame_gen_hold_age", frameGenHoldAge.Checked);

            Json.Set(settings.Root, "video.phase_lock", phaseLock.Checked);
            Json.Set(settings.Root, "video.boundary_offset_ms", (double)boundaryOffsetMs.Value);
            Json.Set(settings.Root, "video.running_start_ms", (double)runningStartMs.Value);
            Json.Set(settings.Root, "video.intake_gate", intakeGate.Checked);
            Json.Set(settings.Root, "video.intake_depth", (int)intakeDepth.Value);
            Json.Set(settings.Root, "video.pick_policy", (string)pickPolicy.SelectedItem ?? "drain");
            Json.Set(settings.Root, "video.client_queue_drain", clientQueueDrain.Checked);
            Json.Set(settings.Root, "video.follow_client_rate", followClientRate.Checked);
            Json.Set(settings.Root, "video.send_pacing", (string)sendPacing.SelectedItem ?? "asap");

            // Onglet Spcial Manettes & Casque (Sauvegarde complte de session_settings.headset)
            Json.Set(s, "session_settings.headset.position_recentering_mode.variant", (string)posRecenteringMode.SelectedItem ?? "LocalFloor");
            Json.Set(s, "session_settings.headset.position_recentering_mode.Local.view_height", (double)posRecenterViewHeight.Value);
            Json.Set(s, "session_settings.headset.rotation_recentering_mode.variant", (string)rotRecenteringMode.SelectedItem ?? "Yaw");

            Json.Set(s, "session_settings.headset.controllers.enabled", ctrlEnabled.Checked);
            Json.Set(s, "openvr_config.controllers_enabled", ctrlEnabled.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.tracked", ctrlTracked.Checked);

            Json.Set(s, "session_settings.headset.controllers.content.hand_skeleton.enabled", handSkeleton.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.hand_skeleton.content.steamvr_input_2_0", handSkeletonInput2.Checked);
            Json.Set(s, "openvr_config.use_separate_hand_trackers", handSkeletonInput2.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.hand_skeleton.content.predict", handSkeletonPredict.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.multimodal_tracking", multimodalTracking.Checked);

            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.enabled", handInteraction.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.only_touch", handOnlyTouch.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.pinch_touch_distance", (double)pinchTouchDist.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.pinch_trigger_distance", (double)pinchTriggerDist.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.curl_touch_distance", (double)curlTouchDist.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.curl_trigger_distance", (double)curlTriggerDist.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_deadzone", (double)joyDeadzone.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_offset_horizontal", (double)joyOffsetH.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_offset_vertical", (double)joyOffsetV.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.joystick_range", (double)joyRange.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.activation_delay", (int)actDelay.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.deactivation_delay", (int)deactDelay.Value);
            Json.Set(s, "session_settings.headset.controllers.content.hand_tracking_interaction.content.repeat_delay", (int)repDelay.Value);

            Json.Set(s, "session_settings.headset.controllers.content.steamvr_pipeline_frames", (double)steamvrPipelineFrames.Value);
            Json.Set(s, "session_settings.headset.controllers.content.haptics.enabled", hapticsEnabled.Checked);
            Json.Set(s, "session_settings.headset.controllers.content.haptics.content.intensity_multiplier", (double)haptics.Value);
            Json.Set(s, "session_settings.headset.controllers.content.haptics.content.amplitude_curve", (double)hapticsCurve.Value);
            Json.Set(s, "session_settings.headset.controllers.content.haptics.content.min_duration_s", (double)hapticsMinDuration.Value);

            string cEmu = (string)controllerEmulation.SelectedItem ?? "Quest3Plus";
            Json.Set(s, "session_settings.headset.controllers.content.emulation_mode.variant", cEmu);
            int cProf = 2; // Quest3Plus
            if (cEmu == "RiftSTouch") cProf = 0;
            else if (cEmu == "Quest2Touch") cProf = 1;
            else if (cEmu == "QuestPro") cProf = 3;
            else if (cEmu == "Pico4") cProf = 10;
            else if (cEmu == "ValveIndex") cProf = 20;
            else if (cEmu == "ViveWand") cProf = 40;
            else if (cEmu == "ViveTracker") cProf = 41;
            Json.Set(s, "openvr_config._controller_profile", cProf);

            Json.Set(s, "session_settings.headset.controllers.content.linear_velocity_cutoff", (double)linearVelocityCutoff.Value);
            Json.Set(s, "session_settings.headset.controllers.content.angular_velocity_cutoff", (double)angularVelocityCutoff.Value);

            Json.Set(s, "session_settings.headset.controllers.content.left_controller_position_offset.content", new[] { (double)ctrlPosX.Value, (double)ctrlPosY.Value, (double)ctrlPosZ.Value });
            Json.Set(s, "session_settings.headset.controllers.content.left_controller_rotation_offset.content", new[] { (double)ctrlRotX.Value, (double)ctrlRotY.Value, (double)ctrlRotZ.Value });
            Json.Set(s, "session_settings.headset.controllers.content.left_hand_tracking_position_offset.content", new[] { (double)handPosX.Value, (double)handPosY.Value, (double)handPosZ.Value });
            Json.Set(s, "session_settings.headset.controllers.content.left_hand_tracking_rotation_offset.content", new[] { (double)handRotX.Value, (double)handRotY.Value, (double)handRotZ.Value });

            Json.Set(s, "session_settings.headset.controllers.content.button_mapping_config.click_threshold.value", (double)clickThreshold.Value);
            Json.Set(s, "session_settings.headset.controllers.content.button_mapping_config.touch_threshold.value", (double)touchThreshold.Value);
            Json.Set(s, "session_settings.headset.controllers.content.button_mapping_config.force_threshold", (double)forceThreshold.Value);

            string hEmu = (string)headsetEmulation.SelectedItem ?? "QuestPro";
            Json.Set(s, "session_settings.headset.emulation_mode.variant", hEmu);

            Json.Set(s, "session_settings.headset.tracking_ref_only", trackingRefOnly.Checked);
            Json.Set(s, "openvr_config.tracking_ref_only", trackingRefOnly.Checked);
            Json.Set(s, "session_settings.headset.enable_vive_tracker_proxy", enableViveTrackerProxy.Checked);
            Json.Set(s, "openvr_config.enable_vive_tracker_proxy", enableViveTrackerProxy.Checked);
            Json.Set(s, "session_settings.headset.body_tracking.enabled", bodyTracking.Checked);
            Json.Set(s, "openvr_config.body_tracking_vive_enabled", bodyTracking.Checked);
            Json.Set(s, "session_settings.headset.face_tracking.enabled", faceTracking.Checked);
            Json.Set(s, "session_settings.headset.vmc.enabled", vmcEnabled.Checked);
            Json.Set(s, "session_settings.headset.max_prediction_ms", (int)maxPredictionMs.Value);

            // R\u00E9seau & Casque
            Json.Set(s, "session_settings.connection.dscp.set", netDscp.Checked);
            Json.Set(s, "session_settings.connection.client_discovery.content.auto_trust_clients", netAutoTrust.Checked);
            Json.Set(s, "session_settings.connection.avoid_video_glitching", netAvoidGlitching.Checked);

            // Audio & Système
            Json.Set(s, "session_settings.audio.game_audio.content.buffering.average_buffering_ms", (int)gameAudioBufferMs.Value);
            Json.Set(s, "session_settings.audio.microphone.content.buffering.average_buffering_ms", (int)micAudioBufferMs.Value);
            Json.Set(s, "session_settings.extra.steamvr_launcher.open_close_steamvr_with_dashboard", steamvrLifecycle.Checked);

            Json.Set(s, "session_settings.audio.game_audio.enabled", audio.Checked);
            settings.MuteSpeakers = mute.Checked;
            settings.AudioCapture = captureBox.SelectedIndex == 1 ? "device" : "process";
            Json.Set(s, "session_settings.audio.microphone.enabled", mic.Checked);

            // Configuration universelle des priphriques de lecture et micro dans session.json
            if (audioPlaybackDevice.SelectedIndex > 0)
            {
                string selPlay = audioPlaybackDevice.SelectedItem.ToString();
                Json.Set(s, "session_settings.audio.game_audio.content.device.set", true);
                Json.Set(s, "session_settings.audio.game_audio.content.device.content.variant", "NameSubstring");
                Json.Set(s, "session_settings.audio.game_audio.content.device.content.NameSubstring", selPlay);
            }
            else
            {
                Json.Set(s, "session_settings.audio.game_audio.content.device.set", false);
                Json.Set(s, "session_settings.audio.game_audio.content.device.content.NameSubstring", "");
            }

            if (audioMicDevice.SelectedIndex > 0)
            {
                string selMic = audioMicDevice.SelectedItem.ToString();
                // resolve the render (playback) side of the selected capture device dynamically -
                // e.g. AuraFlashXR-MIC / CABLE Output -> AuraFlashXR-CABLE / CABLE Input - no hardcoded device name: the sink
                // always exists on this machine (a sink name that exists nowhere fails the host's handshake).
                string sinkName = AudioRoute.FindCable(selMic);
                if (string.IsNullOrEmpty(sinkName)) sinkName = AudioRoute.FindCable();
                if (string.IsNullOrEmpty(sinkName))
                {
                    // No playback device at all: let AuraFlashXR decide (Automatic never fails the handshake).
                    Json.Set(s, "session_settings.audio.microphone.content.devices.variant", "Automatic");
                }
                else
                {
                Json.Set(s, "session_settings.audio.microphone.content.devices.variant", "Custom");
                Json.Set(s, "session_settings.audio.microphone.content.devices.Custom.sink.variant", "NameSubstring");
                Json.Set(s, "session_settings.audio.microphone.content.devices.Custom.sink.NameSubstring", sinkName);
                Json.Set(s, "session_settings.audio.microphone.content.devices.Custom.source.variant", "NameSubstring");
                Json.Set(s, "session_settings.audio.microphone.content.devices.Custom.source.NameSubstring", selMic);
                }
            }
            else
            {
                Json.Set(s, "session_settings.audio.microphone.content.devices.variant", "Automatic");
            }
            settings.PowerPlan = planBox.SelectedIndex > 0 && planBox.SelectedIndex <= plans.Count ? plans[planBox.SelectedIndex - 1].Guid : (planBox.SelectedIndex < 0 ? settings.PowerPlan : "");
            Json.Set(s, "session_settings.connection.stream_protocol.variant", (string)proto.SelectedItem);
            Json.Set(s, "session_settings.connection.packet_size", (int)packet.Value);
            Json.Set(s, "session_settings.headset.controllers.content.haptics.content.intensity_multiplier", (double)haptics.Value);
            // save new fields
            Json.Set(s, V + "adapter_index", (int)adapterIndex.Value);
        // PyroWave -> auraflashxr.json -> video.pyrowave (own settings file, not session.json)
        settings.SetPyrowave(pyroEnabled.Checked, (string)pyroChroma.SelectedItem, (int)pyroMaxKb.Value, (int)pyroPacketSize.Value);
            Json.Set(s, V + "encoder_config.amf.enable_hmqb", amfEnableHmqb.Checked);
            Json.Set(s, V + "encoder_config.amf.use_preproc", amfUsePreproc.Checked);
            Json.Set(s, V + "encoder_config.amf.preproc_sigma", (int)amfPreprocSigma.Value);
            Json.Set(s, V + "encoder_config.amf.preproc_tor", (int)amfPreprocTor.Value);
            Json.Set(s, V + "encoder_config.amf.enable_pre_analysis", amfEnablePreAnalysis.Checked);
            Json.Set(s, V + "encoder_config.software.force_software_encoding", swForceEncoding.Checked);
            Json.Set(s, V + "encoder_config.software.thread_count", (int)swThreadCount.Value);
            Json.Set(s, V + "color_correction.content.gamma", (double)colorGamma.Value);
            Json.Set(s, V + "color_correction.content.sharpening", (double)colorSharpening.Value);
            Json.Set(s, "session_settings.connection.stream_port", (int)streamPort.Value);
            Json.Set(s, "session_settings.connection.web_server_port", (int)webServerPort.Value);
            Json.Set(s, "session_settings.connection.osc_local_port", (int)oscLocalPort.Value);
            Json.Set(s, "session_settings.connection.max_queued_server_video_frames", (int)maxQueuedVideoFrames.Value);
            Json.Set(s, "session_settings.connection.statistics_history_size", (int)statisticsHistorySize.Value);
            Json.Set(s, "session_settings.connection.minimum_idr_interval_ms", (int)minimumIdrIntervalMs.Value);
            Json.Set(s, "session_settings.connection.enable_on_connect_script", enableOnConnectScript.Checked);
            Json.Set(s, "session_settings.connection.enable_on_disconnect_script", enableOnDisconnectScript.Checked);
            Json.Set(s, "session_settings.connection.allow_untrusted_http", allowUntrustedHttp.Checked);
            Json.Set(s, "session_settings.connection.client_discovery.enabled", clientDiscoveryEnabled.Checked);
            Json.Set(s, "session_settings.connection.wired_client_type.variant", wiredClientType.SelectedItem?.ToString() == Lang.Get("Settings.Label.Github") ? "Github" : "Store");
            Json.Set(s, "session_settings.connection.wired_client_autolaunch", wiredClientAutolaunch.Checked);
            // socket buffers: the size is written whatever the mode, so a typed value is never lost when the row is not on Custom
            void SaveBuffer(string key, ComboBox mode, NumericUpDown bytes)
            {
                string variant = mode.SelectedIndex == 1 ? "Maximum" : (mode.SelectedIndex == 2 ? "Custom" : "Default");
                Json.Set(s, "session_settings.connection." + key + ".variant", variant);
                Json.Set(s, "session_settings.connection." + key + ".content", (int)bytes.Value);
            }
            SaveBuffer("server_send_buffer_bytes", streamerSendBufferMode, streamerSendBufferBytes);
            SaveBuffer("server_recv_buffer_bytes", streamerRecvBufferMode, streamerRecvBufferBytes);
            SaveBuffer("client_send_buffer_bytes", clientSendBufferMode, clientSendBufferBytes);
            SaveBuffer("client_recv_buffer_bytes", clientRecvBufferMode, clientRecvBufferBytes);
            Json.Set(s, "session_settings.connection.dscp.set", dscpMode.SelectedIndex == 1);
            Json.Set(s, "session_settings.audio.game_audio.content.mute_when_streaming", muteWhenStreaming.Checked);
            Json.Set(s, "session_settings.audio.game_audio.content.buffering.batch_ms", (int)gameAudioBatchMs.Value);
            Json.Set(s, "session_settings.audio.microphone.content.buffering.batch_ms", (int)micAudioBatchMs.Value);
            Json.Set(s, "session_settings.extra.velocities_multiplier", (double)velocitiesMultiplier.Value);            Chroma.Apply(s, settings, chromaOn.Checked, $"{idle.BackColor.R:x2}{idle.BackColor.G:x2}{idle.BackColor.B:x2}");
            try { Session.Save(s); }
            catch (Exception e) { MessageBox.Show(this, Lang.Get("Settings.Msg.SaveError") + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
            settings.EncodeProfile = profFull.Checked ? "full-split" : "foveated";
            settings.Debug = debugDefault.Checked;
            settings.SaveActivePreset();
            Log.Write("INFO", string.Format("settings saved: {0}x{1}/eye @ {2} Hz, {3} Mbps, {4}, idle #{5}, audio {6}, {7}, debug {8}", w, h, fps.SelectedItem, bitrate.Value, settings.EncodeProfile, settings.IdleRgb, audio.Checked, proto.SelectedItem, settings.Debug));
            if (notify)
            {
                MessageBox.Show(this, Lang.Get("Settings.Saved.Restart"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Restart();
                Environment.Exit(0);
            }
            return true;
        }

        Panel BenchTab()
        {
            var t = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Ui.DarkBg, ForeColor = Ui.DarkText, Padding = new Padding(12) };
            var g = MakeFlow();

            void section(string title) { g.Controls.Add(SectionHeader(title)); }

            // 1. Test encodeur
            section(Lang.Get("Settings.Section.EncoderTest"));
            var encCard = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.FromArgb(20, 27, 36),
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 0, 12)
            };
            encCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Ui.RoundRect(new Rectangle(0, 0, encCard.Width - 1, encCard.Height - 1), 7))
                using (var pen = new Pen(Ui.DarkBorder, 1f))
                    e.Graphics.DrawPath(pen, path);
            };
            StretchChildren(encCard);

            var encDesc = Ui.Line(Lang.Get("Settings.Bench.EncoderDesc"));
            encDesc.Margin = new Padding(0, 0, 0, 8);
            encCard.Controls.Add(encDesc);

            var encBtn = new Button { Text = "Lancer le test de l'encodeur", AutoSize = true, Font = Ui.Bold, BackColor = Ui.Accent, ForeColor = Color.White, Padding = new Padding(14, 8, 14, 8), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 8, 0, 0) };
            encBtn.Text = Lang.Get("Settings.Bench.RunEncoder");
            encBtn.FlatAppearance.BorderSize = 0;
            encBtn.Click += (s, e) => RunEncoderTest();

            var encBtnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
            encBtnRow.Controls.Add(encBtn);
            encCard.Controls.Add(encBtnRow);
            g.Controls.Add(encCard);

            // 2. Benchmark de qualit
            section(Lang.Get("Settings.Section.QualityBench"));
            var qualCard = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.FromArgb(20, 27, 36),
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 0, 12)
            };
            qualCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Ui.RoundRect(new Rectangle(0, 0, qualCard.Width - 1, qualCard.Height - 1), 7))
                using (var pen = new Pen(Ui.DarkBorder, 1f))
                    e.Graphics.DrawPath(pen, path);
            };
            StretchChildren(qualCard);

            var qualDesc = Ui.Line(Lang.Get("Settings.Bench.QualityDesc"));
            qualDesc.Margin = new Padding(0, 0, 0, 4);
            qualCard.Controls.Add(qualDesc);

            var sceneNote = Ui.Line(Lang.Get("Settings.Bench.SceneNote"));
            sceneNote.ForeColor = Ui.DarkMuted;
            sceneNote.Margin = new Padding(0, 0, 0, 8);
            qualCard.Controls.Add(sceneNote);

            var qualBtn = new Button { Text = Lang.Get("Settings.Bench.RunQuality"), AutoSize = true, Font = Ui.Bold, BackColor = Color.FromArgb(32, 45, 60), ForeColor = Color.White, Padding = new Padding(14, 8, 14, 8), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 8, 0, 0) };
            qualBtn.FlatAppearance.BorderSize = 0;
            qualBtn.Click += (s, e) => RunQualityTest();

            var qualBtnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
            qualBtnRow.Controls.Add(qualBtn);
            qualCard.Controls.Add(qualBtnRow);
            g.Controls.Add(qualCard);

            // 3. Test rseau et rsultats
            section(Lang.Get("Settings.Section.NetworkBench"));
            var netCard = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.FromArgb(20, 27, 36),
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 0, 12)
            };
            netCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = Ui.RoundRect(new Rectangle(0, 0, netCard.Width - 1, netCard.Height - 1), 7))
                using (var pen = new Pen(Ui.DarkBorder, 1f))
                    e.Graphics.DrawPath(pen, path);
            };
            StretchChildren(netCard);

            var netDesc = Ui.Line(Lang.Get("Settings.Bench.NetDesc"));
            netDesc.Margin = new Padding(0, 0, 0, 8);
            netCard.Controls.Add(netDesc);

            stepSecs.Value = 8;
            var netParams = FlowWrap(Ui.Line(Lang.Get("Settings.Bench.NetTargets")), steps, Ui.Line(Lang.Get("Settings.Bench.NetSeconds")), stepSecs);
            netParams.Margin = new Padding(0, 0, 0, 8);
            netCard.Controls.Add(netParams);

            var netBtn = new Button { Text = Lang.Get("Settings.Bench.RunNet"), AutoSize = true, Font = Ui.Bold, BackColor = Ui.Accent, ForeColor = Color.White, Padding = new Padding(14, 8, 14, 8), FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 8, 0, 10) };
            netBtn.FlatAppearance.BorderSize = 0;
            netBtn.Click += (s, e) => RunNetworkTest();

            var netBtnRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 10) };
            netBtnRow.Controls.Add(netBtn);
            netCard.Controls.Add(netBtnRow);

            benchList.BackColor = Color.FromArgb(16, 22, 28);
            benchList.ForeColor = Ui.DarkText;
            benchList.Height = 220;
            benchList.Columns.Clear();
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColTest"), 160);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColMbps"), 65);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColFps"), 55);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColLost"), 75);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColTotalMs"), 75);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColEncodeMs"), 85);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColNetMs"), 85);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColDecodeMs"), 85);
            Ui.Col(benchList, Lang.Get("Settings.Bench.ColResult"), 130);
            netCard.Controls.Add(benchList);
            Ui.FitColumns(benchList);

            benchStatus.Margin = new Padding(0, 8, 0, 8);
            netCard.Controls.Add(benchStatus);

            applyBitrate.Text = Lang.Get("Settings.Bench.ApplyBitrate");
            applyBitrate.ForeColor = Color.White;
            applyBitrate.Font = Ui.Bold;
            applyBitrate.FlatStyle = FlatStyle.Flat;
            applyBitrate.FlatAppearance.BorderSize = 0;
            applyBitrate.Padding = new Padding(12, 6, 12, 6);
            applyBitrate.Click += (s, e) =>
            {
                bitrate.Value = Clamp(bitrate, recommended);
                SaveSettings(false);
                MessageBox.Show(this, string.Format(Lang.Get("Settings.Msg.BitrateSet"), recommended), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var applyRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 6, 0, 8) };
            applyRow.Controls.Add(applyBitrate);
            netCard.Controls.Add(applyRow);

            var latNote = Ui.Line(Lang.Get("Settings.Bench.LatNote"));
            latNote.ForeColor = Ui.DarkMuted;
            latNote.Margin = new Padding(0, 4, 0, 0);
            netCard.Controls.Add(latNote);

            g.Controls.Add(netCard);

            t.Controls.Add(g);
            // sync g width with t client width so FlowLayoutPanel lays out correctly
            Action syncGWidth = () => { int gw = t.ClientSize.Width - t.Padding.Left - t.Padding.Right; if (gw > 100) g.Width = gw; };
            syncGWidth();
            t.ClientSizeChanged += (s, e) => syncGWidth();
            return t;
        }

        static string Fmt(Dictionary<string, object> d, string k, string fmt)
        {
            if (d == null || !d.TryGetValue(k, out var v) || v == null) return "-";
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture).ToString(fmt, CultureInfo.InvariantCulture); }
            catch { return "-"; }
        }

        bool BenchBusy()
        {
            if (bench != null && bench.Running)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.BenchBusy"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            return false;
        }

        void RunEncoderTest()
        {
            if (BenchBusy()) return;
            discovery?.Dispose();
            var profiles = new Queue<string>(new[] { "foveated", "full-split" });
            benchStatus.Text = Lang.Get("Settings.Bench.EncoderRunning");
            void next()
            {
                if (profiles.Count == 0)
                {
                    if (!benchStatus.Text.StartsWith("\u00C9chec"))
                        benchStatus.Text = Lang.Get("Settings.Bench.EncoderDone");
                    StartDiscovery();
                    return;
                }
                var prof = profiles.Dequeue();
                bench = new HostProcess();
                bench.OnEvent += (n, d) => BeginInvoke(new Action(() =>
                {
                    if (n == "setup_error")
                    {
                        benchStatus.Text = string.Format(Lang.Get("Settings.Bench.EncoderFailed"), (d.ContainsKey("message") ? d["message"] : ""));
                        profiles.Clear();
                        return;
                    }
                    if (n != "bench") return;
                    var fits = d.TryGetValue("fits", out var fv) && fv is bool b && b;
                    var it = new ListViewItem(new[]
                    {
                        string.Format(Lang.Get("Settings.Bench.EncoderStep"), prof, (d.ContainsKey("width") ? d["width"] : ""), (d.ContainsKey("height") ? d["height"] : "")),
                        "", Fmt(d, "fps", "0"), "", "", $"{Fmt(d, "p50_ms", "0.0")}/{Fmt(d, "p95_ms", "0.0")}", "", "", fits ? Lang.Get("Settings.Bench.Fits") : Lang.Get("Settings.Bench.TooSlow")
                    }) { ForeColor = fits ? Color.SeaGreen : Color.Firebrick };
                    benchList.Items.Add(it);
                }));
                bench.OnExit += c => BeginInvoke(new Action(next));
                var err = bench.Start($"--install-dir \"{Paths.Dir}\" --live --bench 300 --noise-temporal --encode-profile {prof}");
                if (err != null)
                {
                    benchStatus.Text = err;
                    StartDiscovery();
                }
            }
            next();
        }

        static Dictionary<string, object> Sub(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;

        void RunQualityTest()
        {
            if (BenchBusy()) return;
            if (!File.Exists(Path.Combine(Paths.Dir, "tools", "bench", "littlest_tokyo.vab")))
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.BenchFileMissing"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!SaveSettings(false)) return;
            discovery?.Dispose();
            benchStatus.Text = Lang.Get("Settings.Bench.QualityStarting");
            int count = 0;
            bench = new HostProcess();
            bench.OnEvent += (ev, d) => BeginInvoke(new Action(() =>
            {
                if (ev == "bq_start")
                    benchStatus.Text = string.Format(Lang.Get("Settings.Bench.QualityGpu"), (d.ContainsKey("gpu") ? d["gpu"] : ""), Fmt(d, "bitrate_mbps", "0"));
                else if (ev == "bq_result")
                {
                    count++;
                    var rt = d.TryGetValue("realtime", out var rv) && rv is bool b && b;
                    var psnr = Sub(d, "psnr");
                    var enc = Sub(d, "encode_ms");
                    var err = d.TryGetValue("error", out var evObj) && evObj != null ? evObj.ToString() : "";
                    var it = new ListViewItem(new[]
                    {
                        $"{(d.ContainsKey("label") ? d["label"] : "")}",
                        Fmt(d, "produced_mbps", "0"), "", "", "", $"{Fmt(enc, "p50", "0.0")}/{Fmt(enc, "p95", "0.0")}", "", "",
                        !string.IsNullOrEmpty(err) ? Lang.Get("Settings.Bench.NotSupported") : $"{Fmt(psnr, "weighted", "0.0")} dB {(rt ? Lang.Get("Settings.Bench.Realtime") : Lang.Get("Settings.Bench.TooSlow"))}"
                    }) { ForeColor = !string.IsNullOrEmpty(err) || !rt ? Color.Firebrick : Color.SeaGreen };
                    benchList.Items.Add(it);
                    benchList.EnsureVisible(benchList.Items.Count - 1);
                    benchStatus.Text = string.Format(Lang.Get("Settings.Bench.QualityCount"), count);
                }
                else if (ev == "bq_summary")
                {
                    var pr = Sub(d, "proposals");
                    var gc = Sub(d, "gpu_clocks");
                    var stable = gc != null && gc.TryGetValue("stable", out var sv) && sv is bool sb && sb;
                    string line(string key, string title)
                    {
                        var sub = Sub(pr, key);
                        if (sub == null) return title + " : " + Lang.Get("Settings.Bench.None");
                        return string.Format(Lang.Get("Settings.Bench.LineFormat"), title, sub.ContainsKey("label") ? sub["label"] : "", Fmt(Sub(sub, "psnr"), "weighted", "0.0"), Fmt(Sub(sub, "encode_ms"), "p95", "0.0"));
                    }
                    benchStatus.Text = string.Join("\n", new[]
                    {
                        line("lowest_latency", Lang.Get("Settings.Bench.LowestLatency")),
                        line("recommended", Lang.Get("Settings.Bench.Recommended")),
                        line("best_quality", Lang.Get("Settings.Bench.BestQuality")),
                        !stable ? Lang.Get("Settings.Bench.GpuClockWarn") : Lang.Get("Settings.Bench.SavedFile")
                    });
                    Log.Write("INFO", "quality benchmark: " + benchStatus.Text.Replace("\n", " | "));
                }
                else if (ev == "bq_error" || ev == "setup_error")
                {
                    benchStatus.Text = string.Format(Lang.Get("Settings.Bench.QualityFailed"), (d.ContainsKey("message") ? d["message"] : ""));
                }
            }));
            bench.OnExit += c => BeginInvoke(new Action(() =>
            {
                if (c != 0 && !benchStatus.Text.StartsWith("\u00C9chec"))
                    benchStatus.Text = string.Format(Lang.Get("Settings.Bench.QualityDone"), c);
                StartDiscovery();
            }));
            var startErr = bench.Start($"--install-dir \"{Paths.Dir}\" --benchmark-quality");
            if (startErr != null)
            {
                benchStatus.Text = startErr;
                StartDiscovery();
            }
        }

        void RunNetworkTest()
        {
            if (BenchBusy()) return;
            if (Session.Headset(Session.Load()) == null)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.PairFirst"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var list = string.Join(",", steps.Text.Split(',').Select(s => s.Trim()).Where(s => int.TryParse(s, out _)));
            if (list.Length == 0)
            {
                MessageBox.Show(this, Lang.Get("Settings.Msg.EnterBitrates"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!SaveSettings(false)) return;
            discovery?.Dispose();
            applyBitrate.Enabled = false;
            benchStatus.Text = Lang.Get("Settings.Bench.WaitingHeadset");
            bench = new HostProcess();
            bench.OnEvent += (n, d) => BeginInvoke(new Action(() =>
            {
                if (n == "client_connected") benchStatus.Text = Lang.Get("Settings.Bench.HeadsetConnected");
                else if (n == "setup_error") benchStatus.Text = string.Format(Lang.Get("Settings.Bench.NetworkFailed"), (d.ContainsKey("message") ? d["message"] : ""));
                else if (n == "bench_step_start") benchStatus.Text = string.Format(Lang.Get("Settings.Bench.StepStart"), (d.ContainsKey("target_mbps") ? d["target_mbps"] : ""), Convert.ToInt32(d["step"]));
                else if (n == "bench_step")
                {
                    var it = new ListViewItem(new[]
                    {
                        string.Format(Lang.Get("Settings.Bench.NetworkStep"), Fmt(d, "target_mbps", "0")),
                        Fmt(d, "actual_mbps", "0"), Fmt(d, "client_fps", "0"), Fmt(d, "packets_lost_per_s", "0.0"),
                        Fmt(d, "total_latency_ms", "0.0"), Fmt(d, "encode_ms", "0.0"), Fmt(d, "network_ms", "0.0"), Fmt(d, "decode_ms", "0.0"), ""
                    });
                    benchList.Items.Add(it);
                }
                else if (n == "bench_result")
                {
                    var rec = d.TryGetValue("recommended_mbps", out var rv) && rv != null ? Convert.ToInt32(rv) : 0;
                    if (rec > 0)
                    {
                        recommended = rec;
                        applyBitrate.Enabled = true;
                        benchStatus.Text = string.Format(Lang.Get("Settings.Bench.RecommendedBitrate"), recommended);
                    }
                    else
                    {
                        benchStatus.Text = Lang.Get("Settings.Bench.NoStepValid") + (d.ContainsKey("note") && d["note"] != null ? d["note"].ToString() : "");
                    }
                    Log.Write("INFO", "network benchmark: " + benchStatus.Text);
                }
            }));
            bench.OnExit += c => BeginInvoke(new Action(() =>
            {
                if (!applyBitrate.Enabled && !benchStatus.Text.StartsWith("Aucun") && !benchStatus.Text.StartsWith("\u00C9chec"))
                    benchStatus.Text = string.Format(Lang.Get("Settings.Bench.NetworkDone"), c);
                StartDiscovery();
            }));
            var err = bench.Start($"--install-dir \"{Paths.Dir}\" --benchmark-network {list} --bench-step-s {(int)stepSecs.Value} --connect-timeout 120 --seconds 900 --encode-profile {(profFull.Checked ? "full-split" : "foveated")}");
            if (err != null)
            {
                benchStatus.Text = err;
                StartDiscovery();
            }
        }

        [STAThread]
        static void Main()
        {
            Log.Component = "configure";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Lang.Init();
            var args = Environment.GetCommandLineArgs();
            Log.Write("INFO", "configure.exe " + Version.Text + " start (" + Paths.Dir + ")");
            var check = File.Exists(Paths.Host) ? SystemCheck.Run() : null;
            if (args.Contains("--settings") || args.Contains("--advanced") || args.Contains("--switches"))
            {
                Application.Run(new ConfigureForm(check));
                return;
            }
            Application.Run(new WizardForm(check, args.Contains("--demo")));
        }
    }
}