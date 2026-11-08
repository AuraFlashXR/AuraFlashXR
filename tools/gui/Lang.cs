using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace AuraFlashXR
{
    public static class Lang
    {
        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> TranslationsFrToEn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static string CurrentCode { get; private set; } = "fr";

        public static string Get(string key, string fallback = null)
        {
            if (Strings.TryGetValue(key, out var val)) return val;
            if (CurrentCode.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                if (fallback != null && TranslationsFrToEn.TryGetValue(fallback.Trim(), out var auto))
                    return auto;
                if (TranslationsFrToEn.TryGetValue(key.Trim(), out var autoKey))
                    return autoKey;
            }
            return fallback ?? key;
        }

        public static string T(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Strings.TryGetValue(text, out var val)) return val;
            if (CurrentCode.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = text.Trim();
                if (TranslationsFrToEn.TryGetValue(trimmed, out var auto))
                {
                    if (text.EndsWith(" :") && !auto.EndsWith(":")) return auto + ":";
                    if (text.EndsWith(":") && !auto.EndsWith(":")) return auto + ":";
                    return auto;
                }
            }
            return text;
        }

        public static string Get(string key, params object[] args)
        {
            var format = Get(key);
            try { return string.Format(format, args); }
            catch { return format; }
        }

        public static List<(string code, string name, string file)> AvailableLanguages()
        {
            var res = new List<(string code, string name, string file)>();
            var dir = GetLanguageDir();
            if (!Directory.Exists(dir)) return res;

            foreach (var f in Directory.GetFiles(dir, "*.xml"))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(f);
                    var root = doc.DocumentElement;
                    if (root != null && root.Name == "Language")
                    {
                        var code = root.GetAttribute("Code");
                        if (string.IsNullOrEmpty(code)) code = Path.GetFileNameWithoutExtension(f);
                        var name = root.GetAttribute("Name");
                        if (string.IsNullOrEmpty(name)) name = code;
                        res.Add((code, name, f));
                    }
                }
                catch { }
            }
            return res;
        }

        public static void Load(string codeOrFile)
        {
            var dir = GetLanguageDir();
            string targetFile = null;

            if (File.Exists(codeOrFile))
            {
                targetFile = codeOrFile;
            }
            else if (Directory.Exists(dir))
            {
                var byName = Path.Combine(dir, codeOrFile + ".xml");
                if (File.Exists(byName))
                {
                    targetFile = byName;
                }
                else
                {
                    foreach (var f in Directory.GetFiles(dir, "*.xml"))
                    {
                        try
                        {
                            var d = new XmlDocument();
                            d.Load(f);
                            if (d.DocumentElement != null && string.Equals(d.DocumentElement.GetAttribute("Code"), codeOrFile, StringComparison.OrdinalIgnoreCase))
                            {
                                targetFile = f;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (targetFile == null)
                {
                    var fr = Path.Combine(dir, "french.xml");
                    if (File.Exists(fr)) targetFile = fr;
                }
            }

            if (targetFile == null || !File.Exists(targetFile)) return;

            try
            {
                var doc = new XmlDocument();
                doc.Load(targetFile);
                var root = doc.DocumentElement;
                if (root != null)
                {
                    Strings.Clear();
                    CurrentCode = root.GetAttribute("Code");
                    if (string.IsNullOrEmpty(CurrentCode)) CurrentCode = "fr";

                    foreach (XmlNode node in root.SelectNodes("String"))
                    {
                        if (node.Attributes?["Key"] != null)
                        {
                            var k = node.Attributes["Key"].Value;
                            var v = node.InnerText;
                            Strings[k] = v;
                        }
                    }
                }
                SavePreference(CurrentCode);
            }
            catch { }
        }

        public static void TranslateControl(Control c)
        {
            if (c == null) return;
            if (!string.IsNullOrEmpty(c.Text))
            {
                var translated = T(c.Text);
                if (!string.Equals(translated, c.Text, StringComparison.Ordinal))
                    c.Text = translated;
            }

            if (c is ListView lv)
            {
                foreach (ColumnHeader ch in lv.Columns)
                {
                    var trans = T(ch.Text);
                    if (!string.Equals(trans, ch.Text, StringComparison.Ordinal))
                        ch.Text = trans;
                }
            }
            else if (c is ComboBox cb)
            {
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    if (cb.Items[i] is string s)
                    {
                        var trans = T(s);
                        if (!string.Equals(trans, s, StringComparison.Ordinal))
                        {
                            bool wasSel = (cb.SelectedIndex == i);
                            cb.Items[i] = trans;
                            if (wasSel) cb.SelectedIndex = i;
                        }
                    }
                }
            }

            foreach (Control sub in c.Controls)
            {
                TranslateControl(sub);
            }
        }

        public static string GetLanguageDir()
        {
            // DESIGN 2026-11-02 : les langues vivent dans tools\language (comme le bench et le driver).
            // Paths helper lives in Common.cs, absent from the stub build (Stub.cs + Lang.cs only):
            // fall back to AppDomain base dir and parent dir instead.
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var p0 = Path.Combine(baseDir, "tools", "language");
            if (Directory.Exists(p0)) return p0;
            var p0b = Path.Combine(baseDir, "..", "tools", "language");
            if (Directory.Exists(p0b)) return p0b;
            var p1 = Path.Combine(baseDir, "language");
            if (Directory.Exists(p1)) return p1;
            var p2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language");
            if (Directory.Exists(p2)) return p2;
            var p3 = Path.Combine(baseDir, "..", "language");
            if (Directory.Exists(p3)) return p3;
            return p0;
        }

        private static string GetPrefPath()
        {
            return Path.Combine(GetLanguageDir(), "selected.cfg");
        }

        public static void Init()
        {
            InitDictionary();
            var pref = "fr";
            var path = GetPrefPath();
            if (File.Exists(path))
            {
                try { pref = File.ReadAllText(path).Trim(); } catch { }
            }
            Load(pref);
        }

        private static void SavePreference(string code)
        {
            try
            {
                var dir = GetLanguageDir();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(GetPrefPath(), code);
            }
            catch { }
        }

        private static void InitDictionary()
        {
            if (TranslationsFrToEn.Count > 0) return;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Réseau & Casque", "Network & Headset" },
                { "?? Réseau & Appairage", "?? Network & Pairing" },
                { "Manettes & Casque", "Controllers & Headset" },
                { "?? Manettes & Casque", "?? Controllers & Headset" },
                { "Vidéo & Encodage", "Video & Encoding" },
                { "? Vidéo & Encodage NVENC", "? Video & NVENC Encoding" },
                { "Amélioration Image", "Image Enhancement" },
                { "?? Qualité d'Image & Clarté", "?? Image Quality & Clarity" },
                { "Cadencement & Pacing", "Pacing & Timing" },
                { "?? Cadencement & Pacing", "?? Frame Pacing & Timing" },
                { "Audio", "Audio" },
                { "?? Audio & Microphone", "?? Audio & Microphone" },
                { "Système Windows", "Windows System" },
                { "??? Système Windows & SteamVR", "??? Windows System & SteamVR" },
                { "Benchmark", "Benchmark" },
                { "?? Benchmarks & Tests", "?? Benchmarks & Tests" },
                { "Détection & Appairage du Casque", "Headset Detection & Pairing" },
                { "Paramètres de Transmission Réseau", "Network Transmission Settings" },
                { "Profils graphiques & Résolution de rendu", "Graphics Profiles & Rendering Resolution" },
                { "Encodage matériel & Fovéation", "Hardware Encoding & Foveation" },
                { "Paramètres NVENC bas-niveau & Intra-Refresh", "Low-Level NVENC Settings & Intra-Refresh" },
                { "Suréchantillonnage & Amélioration de Clarté", "Super-Sampling & Clarity Enhancement" },
                { "Espace Couleur, Contraste & Plage Dynamique", "Color Space, Contrast & Dynamic Range" },
                { "Fovéation avancée & Découpage Fovéal", "Advanced Foveation & Foveated Slices" },
                { "Options d'Incrustation & Passthrough", "Passthrough & Overlay Options" },
                { "Génération d'images (SSW / Motion Reprojection)", "Motion Reprojection & Frame Generation (SSW)" },
                { "Cadencement Serveur & Files d'Attente", "Server Pacing & Processing Queues" },
                { "Pipeline SteamVR, Haptique & Coupure de Vitesse", "SteamVR Pipeline, Haptics & Velocity Cutoff" },
                { "Ajustements de Pose et Décalages (Offsets 3D)", "Pose Adjustments & 3D Offsets" },
                { "Seuils de Pression des Boutons & Trackers Virtuels", "Button Thresholds & Virtual Trackers" },
                { "Routage Audio du Jeu", "Game Audio Routing" },
                { "Microphone & Voix (Passthrough)", "Microphone & Voice (Passthrough)" },
                { "Latence & Tampons Audio", "Audio Latency & Buffers" },
                { "Intégration SteamVR", "SteamVR Integration" },
                { "Alimentation & Énergie Windows", "Windows Power Management" },
                { "Journalisation & Télémétrie Windows", "Windows Telemetry & Logging" },
                { "Test de l'Encodeur Vidéo NVENC (sans casque)", "NVENC Video Encoder Test (without headset)" },
                { "Benchmark de Qualité Graphique (sans casque, ~1.5 min)", "Graphics Quality Benchmark (without headset, ~1.5 min)" },
                { "Test Réseau & Mesures de Latence (avec casque appairé)", "Network Test & Latency Measures (with paired headset)" },
                { "Protocole de transmission (UDP conseillé) :", "Transmission Protocol (UDP recommended):" },
                { "Taille maximale des paquets (octets) :", "Maximum Packet Size (bytes):" },
                { "Priorisation QoS réseau (DSCP EF) :", "Network QoS Priority (DSCP EF):" },
                { "Appairage automatique des casques détectés :", "Auto-Trust Detected Headsets:" },
                { "Éviter les saccades/glitches vidéo :", "Avoid Video Glitches / Stutters:" },
                { "Préréglage de résolution AuraFlashXR :", "AuraFlashXR Resolution Preset:" },
                { "Résolution personnalisée par œil :", "Custom Resolution Per Eye:" },
                { "Fréquence de rafraîchissement (Hz) :", "Refresh Rate (Hz):" },
                { "Débit vidéo (Mbps) :", "Video Bitrate (Mbps):" },
                { "Mode de gestion du débit :", "Bitrate Management Mode:" },
                { "Options de débit adaptatif (Adaptive) :", "Adaptive Bitrate Options:" },
                { "Taille d'historique de débit (History size) :", "Bitrate History Size:" },
                { "Codec vidéo préféré :", "Preferred Video Codec:" },
                { "Préréglage qualité encodeur global :", "Global Encoder Quality Preset:" },
                { "Profil H.264 :", "H.264 Profile:" },
                { "Régulation de débit NVENC :", "NVENC Rate Control Mode:" },
                { "Quantification adaptative (AQ) :", "Adaptive Quantization (AQ):" },
                { "Encodage fractionné (Split Dual-NVENC) :", "Split Encoding (Dual-NVENC):" },
                { "Nombre de tranches de décodage (Slice count) :", "Decoding Slice Count:" },
                { "Codage entropique H.264 :", "H.264 Entropy Coding:" },
                { "Préréglage de performance NVENC :", "NVENC Performance Preset:" },
                { "Optimisation de latence NVENC :", "NVENC Latency Tuning Preset:" },
                { "Analyse multi-passe NVENC :", "NVENC Multi-Pass Analysis:" },
                { "Marge de sécurité débit dynamique (%) :", "Dynamic Bitrate Headroom (%):" },
                { "Priorité d'ordonnancement GPU hôte :", "Host GPU Scheduling Priority:" },
                { "Options d'encodage NVENC avancées :", "Advanced NVENC Encoding Options:" },
                { "Gamma d'encodage & Override serveur :", "Encoding Gamma & Server Override:" },
                { "Échelle clé basse latence (Low delay key scale) :", "Low Delay Key Frame Scale:" },
                { "Rafraîchissement intra (Intra-refresh) :", "Intra-Refresh:" },
                { "Nombre max de trames de référence :", "Max Reference Frames:" },
                { "Longueur de groupe GOP & Stratégie P-frame :", "GOP Length & P-Frame Strategy:" },
                { "Tampon RC & Délai initial (octets) :", "RC Buffer & Initial Delay (bytes):" },
                { "Débit max / moyen régulateur RC (bps) :", "RC Max / Average Bitrate (bps):" },
                { "Prédiction pondérée NVENC :", "NVENC Weighted Prediction:" },
                { "Paramètres d'affichage et couleur HDR :", "HDR Display & Color Settings:" },
                { "Mode d'encodage :", "Encoding Mode:" },
                { "Taille du centre fovéal (X, Y) :", "Foveation Center Size (X, Y):" },
                { "Décalage du centre fovéal (X, Y) :", "Foveation Center Shift (X, Y):" },
                { "Taux de compression périphérique :", "Peripheral Compression Ratio:" },
                { "Poids d'historique de mise en tampon :", "Buffering History Weight:" },
                { "Fovéation côté client (Clientside Foveation) :", "Client-Side Foveation (Quest):" },
                { "Post-traitement sur casque (Sharpening / SS) :", "Headset Post-Processing (Sharpening / SS):" },
                { "Netteté adaptative du contraste (CAS Sharpening) :", "Contrast Adaptive Sharpening (CAS):" },
                { "Correction gamma d'affichage :", "Display Gamma Correction:" },
                { "Traitements d'image avancés :", "Advanced Image Processing:" },
                { "Mode d'incrustation Passthrough :", "Passthrough Overlay Mode:" },
                { "Incrustation chroma-key (Passthrough) :", "Chroma-Key Overlay (Passthrough):" },
                { "Couleur clé transparente :", "Transparent Key Color:" },
                { "Seuil de mélange alpha (Blend threshold) :", "Alpha Blend Threshold:" },
                { "Tolérance de distance (RGB distance) :", "RGB Distance Tolerance:" },
                { "Adoucissement de bordure (Feathering) :", "Border Feathering:" },
                { "Information Chroma-Key :", "Chroma-Key Information:" },
                { "Synchronous Spacewarp (SSW / Frame Gen) :", "Synchronous Spacewarp (SSW / Frame Gen):" },
                { "Anticipation des saccades (Tampon) :", "Stutter Buffer / Smoothing:" },
                { "Mode non plafonné (Uncapped) :", "Uncapped Mode:" },
                { "Plafond fréquence du jeu (FPS Uncapped) :", "Game Framerate Cap (FPS Uncapped):" },
                { "Latence de contenu (Uncapped) :", "Content Latency (Uncapped):" },
                { "Échelle d'entrée du flux optique :", "Optical Flow Input Scale:" },
                { "Préréglage du flux optique :", "Optical Flow Preset:" },
                { "Synthèse des trames générées :", "Generated Frames Synthesis:" },
                { "Maintien de l'âge d'image (Hold frame age) :", "Hold Frame Age:" },
                { "Cadencement strict serveur (Server Frame Pacing) :", "Server Frame Pacing (Smooth Output):" },
                { "Verrouillage de phase (Phase lock) :", "Phase Locking (Phase Lock):" },
                { "Décalage de frontière (ms) :", "Boundary Offset (ms):" },
                { "Marge de départ anticipé (Running Start) :", "Running Start Margin:" },
                { "Porte d'admission hôte (Intake gate) :", "Host Intake Gate:" },
                { "Profondeur de file d'admission (trames) :", "Intake Queue Depth (frames):" },
                { "Politique de sélection de trame :", "Frame Selection Policy:" },
                { "Purger la file décodeur casque (Queue drain) :", "Drain Headset Decoder Queue:" },
                { "Suivre la fréquence réelle du casque :", "Follow Headset Real Refresh Rate:" },
                { "Cadencement d'envoi vers le casque :", "Headset Send Pacing:" },
                { "Porte de début de trame OpenXR (Registry Hook) :", "OpenXR Frame Start Gate (Registry Hook):" },
                { "Mode de recentrage position (Position recentering) :", "Position Recentering Mode:" },
                { "Mode de recentrage rotation (Rotation recentering) :", "Rotation Recentering Mode:" },
                { "Espace de jeu physique (Stage tracking) :", "Physical Play Area (Stage Tracking):" },
                { "Activation et suivi des manettes :", "Controller Activation & Tracking:" },
                { "Modèle de manettes émulé (Emulation mode) :", "Emulated Controller Profile:" },
                { "Modèle de casque émulé (Headset emulation) :", "Emulated Headset Profile:" },
                { "Squelette des mains SteamVR (Hand skeleton) :", "SteamVR Hand Skeleton (Input 2.0):" },
                { "Gestes de mains sans manettes (Hand interaction) :", "Hand Gestures without Controllers:" },
                { "Distances de pincement Pincé / Déclencheur :", "Pinch Touch / Trigger Distances:" },
                { "Distances de courbure Plié / Déclencheur :", "Curl Touch / Trigger Distances:" },
                { "Joystick virtuel mains (Zone morte & Plage) :", "Virtual Hand Joystick (Deadzone & Range):" },
                { "Décalage joystick virtuel (H / V) :", "Virtual Joystick Offset (H / V):" },
                { "Délais d'activation / Désact. / Répétition (ms) :", "Activation / Deact. / Repeat Delays (ms):" },
                { "Trames du pipeline SteamVR (Prediction frames) :", "SteamVR Pipeline Frames (Prediction):" },
                { "Retour haptique des manettes (Haptics) :", "Controller Haptic Feedback:" },
                { "Coupure de vitesse linéaire (Linear velocity cutoff) :", "Linear Velocity Cutoff:" },
                { "Coupure de vitesse angulaire (Angular velocity cutoff) :", "Angular Velocity Cutoff:" },
                { "Décalage Position Manette Gauche (X, Y, Z) :", "Left Controller Position Offset (X, Y, Z):" },
                { "Décalage Rotation Manette Gauche (X, Y, Z) :", "Left Controller Rotation Offset (X, Y, Z):" },
                { "Décalage Position Suivi des Mains (X, Y, Z) :", "Left Hand Tracking Position Offset (X, Y, Z):" },
                { "Décalage Rotation Suivi des Mains (X, Y, Z) :", "Left Hand Tracking Rotation Offset (X, Y, Z):" },
                { "Seuils Clic / Effleurement / Force :", "Click / Touch / Force Thresholds:" },
                { "Trackers, FBT, Visage & Protocole VMC :", "Trackers, FBT, Face & VMC Protocol:" },
                { "Prédiction maximale du casque (Max prediction ms) :", "Max Headset Prediction (ms):" },
                { "Diffuser l'audio du jeu vers le casque :", "Stream Game Audio to Headset:" },
                { "Couper le son des haut-parleurs PC :", "Mute PC Speakers during streaming:" },
                { "Méthode de capture audio :", "Audio Capture Method:" },
                { "Périphérique de capture PC (Jeu / Système) :", "PC Audio Capture Device (Game/System):" },
                { "Activer le microphone du casque pour les jeux :", "Enable Headset Microphone for PC / Games:" },
                { "Sortie du Micro VR (Passthrough) :", "VR Mic Output (Passthrough Sink):" },
                { "Périphérique virtuel AuraFlashXR-MIC :", "Virtual Audio Device AuraFlashXR-MIC:" },
                { "Tampon audio du jeu (ms) :", "Game Audio Buffer (ms):" },
                { "Tampon microphone (ms) :", "Microphone Buffer (ms):" },
                { "Synchroniser SteamVR avec le diffuseur :", "Synchronize SteamVR with Streamer:" },
                { "Mode d'alimentation Windows en jeu :", "Windows Power Plan in game:" },
                { "Note sur le mode d'alimentation :", "Power Plan Note:" },
                { "Journalisation de débogage activée par défaut :", "Debug Logging Enabled by Default:" },
                { "Activer (haute priorité)", "Enable (High Priority)" },
                { "Activer l'appairage automatique", "Enable Auto-Pairing" },
                { "Activer la tolérance aux pertes légères", "Enable Packet Loss Tolerance" },
                { "Activer le microphone du casque pour les jeux / PC", "Enable headset microphone for games / PC" },
                { "Diffuser l'audio du jeu vers le casque", "Stream game audio to headset" },
                { "Couper le son des haut-parleurs du PC pendant la diffusion", "Mute PC speakers during streaming" },
                { "Journalisation de débogage activée par défaut (recommandé pendant l'alpha)", "Debug logging enabled by default (recommended during alpha)" },
                { "Encodage couleur 10-bit HDR (Profil Main10 / P010)", "10-bit HDR Color Encoding (Main10 / P010)" },
                { "Forcer l'override serveur 10-bit", "Force Server 10-bit Override" },
                { "Plage de couleurs complète (Full dynamic range)", "Full Dynamic Color Range" },
                { "Forcer l'override serveur pleine gamme", "Force Server Full Range Override" },
                { "Forcer l'override serveur gamma d'encodage", "Force Server Encoding Gamma Override" },
                { "Données de remplissage CBR (Filler data)", "CBR Filler Data" },
                { "VBAQ (Variance-Based Adaptive Quantization - clarté visuelle)", "VBAQ (Variance-Based Adaptive Quantization)" },
                { "Forcer décodeur logiciel client (si échec décodeur matériel)", "Force Client Software Decoder (hardware fallback)" },
                { "Plafond débit adaptatif (Max throughput)", "Adaptive Max Throughput" },
                { "Débit plancher adaptatif (Min throughput)", "Adaptive Min Throughput" },
                { "Latence réseau max tolérée (ms)", "Max Tolerated Network Latency (ms)" },
                { "Correctif corruption d'image en cas de perte de paquets", "Image Corruption Fix on Packet Loss" },
                { "Adapter dynamiquement le débit au framerate réel", "Dynamically Adapt Bitrate to Framerate" },
                { "Intra Refresh périodique (évite les pics d'images I)", "Periodic Intra-Refresh (prevents I-frame spikes)" },
                { "Tampon VBV et CBR strict (1 trame, évite les pics de latence)", "Strict VBV Buffer & CBR (prevents latency spikes)" },
                { "Carte QP HEVC fovéale (qualité accrue au centre visuel)", "Foveated HEVC QP Map (higher central quality)" },
                { "Verrouillage de phase (aligne l'arrivée sur le rendu du casque)", "Phase Locking (align with headset render)" },
                { "Porte d'admission (vide la file hôte si stagnation)", "Intake Gate (flush queue on stall)" },
                { "Purger la file du décodeur casque si stagnation", "Drain Client Decoder Queue on stall" },
                { "Suivre la fréquence d'affichage déduite du casque", "Follow Headset Inferred Refresh Rate" },
                { "Porte de début de trame OpenXR (Registry Hook)", "OpenXR Frame Start Gate (Registry Hook)" },
                { "Mode non plafonné (Uncapped - le jeu tourne à sa propre fréquence)", "Uncapped Mode (game runs at native rate)" },
                { "Maintenir le régime d'horodatage (évite saut d'image)", "Hold Timestamp Cadence (prevents frame drops)" },
                { "Imposer le cadencement strict serveur (Server Frame Pacing fluide)", "Enforce Server Frame Pacing (smooth cadence)" },
                { "Manettes activées (Controllers enabled)", "Controllers Enabled" },
                { "Suivi actif des manettes dans l'espace (Tracked)", "Active Spatial Tracking" },
                { "Squelette complet des mains (SteamVR Input 2.0 Skeletal Tracking)", "Full Hand Skeleton (SteamVR Input 2.0)" },
                { "SteamVR Input 2.0 (Hand skeleton)", "SteamVR Input 2.0 (Hand skeleton)" },
                { "Prédiction du squelette des mains (Predict)", "Hand Skeleton Prediction" },
                { "Suivi multimodal (Mains + Manettes simultanés)", "Multimodal Tracking (Hands + Controllers)" },
                { "Interaction gestuelle des mains (Pincement, clics virtuels sans manettes)", "Hand Interaction (Pinch & virtual clicks)" },
                { "Interaction : Mode contact uniquement (Only touch)", "Interaction: Touch Only Mode" },
                { "Vibrations haptiques activées (Haptics enabled)", "Haptic Vibrations Enabled" },
                { "Proxy HTC Vive Tracker (compatibilité accessoires)", "HTC Vive Tracker Proxy" },
                { "Suivi complet du corps (Full Body Tracking / FBT via trackers virtuels)", "Full Body Tracking (FBT via virtual trackers)" },
                { "Suivi des expressions du visage et des yeux (Eye / Face OSC pour VRChat)", "Eye & Face Tracking (OSC for VRChat)" },
                { "Diffusion protocole VMC (Virtual Motion Capture OSC)", "VMC Protocol Streaming (Virtual Motion Capture)" },
                { "Démarrer et fermer SteamVR automatiquement avec le diffuseur", "Automatically start & close SteamVR with streamer" },
                { "Activer le streaming HDR (High Dynamic Range)", "Enable HDR Streaming (High Dynamic Range)" },
                { "Forcer l'override serveur HDR", "Force Server HDR Override" },
                { "Correction HDR sRGB forcée", "Forced HDR sRGB Correction" },
                { "Restreindre la plage étendue HDR (Clamp extended range)", "Clamp HDR Extended Range" },
                { "Fovéation côté client (Clientside Foveation Quest)", "Client-Side Foveation (Quest)" },
                { "Post-traitement sur casque (Sharpening & Super Sampling client)", "Headset Post-Processing (Sharpening & Super Sampling)" },
                { "Snapdragon® Game Super Resolution (Suréchantillonnage et clarté sur casque)", "Snapdragon® Game Super Resolution (Headset clarity)" },
                { "Mise en mémoire tampon vidéo (Prévient les saccades et micro-pauses)", "Video Buffering (Prevents micro-stutters)" },
                { "Centrer sur l'espace de jeu physique (Stage Tracking)", "Stage Tracking (Physical Play Area)" },
                { "Suivi actif des manettes et des mains", "Active Controllers & Hands Tracking" },
                { "Éclat et vibrance des couleurs (Correction de saturation pour dalle LCD)", "Color Vibrance (LCD Saturation Boost)" },
                { "Plage vidéo nominale étendue (Noirs intenses et dynamique lumineuse accrue)", "Nominal Extended Video Range (Deep Blacks)" },
                { "Fovéal (encodage fovéal AuraFlashXR ; recommandé)", "Foveated (AuraFlashXR foveated encoding; recommended)" },
                { "Pleine trame + encodage fractionné (3x plus de pixels, nécessite une liaison solide)", "Full frame + split encoding (3x more pixels, requires solid link)" },
                { "Forcer l'activation de l'encodage fovéal (Force enable)", "Force Enable Foveated Encoding" },
                { "Afficher ma pièce là où l'écran a cette couleur", "Show my room where screen matches this color" },
                { "Lancer le test de l'encodeur", "Run Encoder Test" },
                { "Lancer le benchmark de qualité", "Run Quality Benchmark" },
                { "Lancer le test réseau", "Run Network Test" },
                { "Utiliser le débit recommandé", "Apply Recommended Bitrate" },
                { "Mesure la durée d'encodage matériel NVENC par trame pour chaque profil graphique.", "Measures NVENC hardware encoding time per frame for each graphics profile." },
                { "Évalue la scène de test 3D via fovéation dynamique + NVENC sur ~30 configurations d'encodage.", "Evaluates 3D test scene via dynamic foveation + NVENC across ~30 encode presets." },
                { "Scène : \"Littlest Tokyo\" par Glen Fox (glenatron), CC-BY-4.0.", "Scene: \"Littlest Tokyo\" by Glen Fox (glenatron), CC-BY-4.0." },
                { "Envoie des trames à débits croissants pour identifier le débit maximal stable de votre Wi-Fi/câble.", "Streams frames at increasing bitrates to identify stable maximum Wi-Fi/cable throughput." },
                { "Débits cibles (Mbps) :", "Target Bitrates (Mbps):" },
                { "Secondes par palier :", "Seconds per step:" },
                { "Latence globale (end-to-end) = jeu + encodage NVENC + transmission réseau + décodage casque + files VSync.", "Global latency (end-to-end) = game + NVENC encoding + network transmit + headset decoding + VSync queues." },
                { "Test", "Test" },
                { "Mbps", "Mbps" },
                { "fps", "fps" },
                { "perdus/s", "lost/s" },
                { "total ms", "total ms" },
                { "encode ms", "encode ms" },
                { "réseau ms", "network ms" },
                { "décode ms", "decode ms" },
                { "résultat", "result" },
                { "(Périphérique par défaut Windows)", "(Windows Default Device)" },
                { "(Automatique / AuraFlashXR-MIC)", "(Automatic / AuraFlashXR-MIC)" },
                { "Bouclage par processus (aucun câble virtuel requis)", "Process loopback (no virtual cable required)" },
                { "Périphérique audio spécifique (Loopback universel)", "Specific audio device (Universal loopback)" },
                { "Survolez une option pour inspecter son chemin dans session.json", "Hover over an option to inspect its path in session.json" },
                { "Aucune correspondance directe dans session.json", "No direct match in session.json" },
                { "Bascule automatiquement sur ce profil d'alimentation au lancement du streaming et restaure le mode initial à la fermeture.", "Automatically switches to this power plan when streaming starts and restores original mode on exit." },
                { "Installe le pilote audio virtuel et configure automatiquement l'entrée sous le nom AuraFlashXR-MIC.", "Installs virtual audio driver and automatically configures input named AuraFlashXR-MIC." },
                { "Recherche de casques (Bonjour/mDNS et UDP 9943)... Si Windows le demande, autorisez auraflashxr_host.exe sur les réseaux privés.", "Searching for headsets (Bonjour/mDNS & UDP 9943)... If Windows prompts, allow auraflashxr_host.exe on private networks." },
                { "Ouvrez l'application AuraFlashXR sur votre casque (Wi-Fi ou câble USB). Le casque apparaît ci-dessous :", "Open AuraFlashXR app on your headset (Wi-Fi or USB). Headset appears below:" },
                { "Nom d'hôte", "Hostname" },
                { "Adresse IP", "IP Address" },
                { "Protocole / Compatibilité", "Protocol / Compatibility" },
                { "Appairer le casque sélectionné", "Pair Selected Headset" },
                { "Enregistrer les paramètres", "Save Settings" },
                { "Enregistrer et démarrer AuraFlashXR", "Save and Start AuraFlashXR" },
                { "Importer des paramètres...", "Import Settings..." },
                { "Installer / Configurer le périphérique virtuel AuraFlashXR-MIC", "Install / Configure Virtual Device AuraFlashXR-MIC" },


                // Streamer & Runtime GUI translations
                { "Diffuseur (Streamer)", "Streamer" },
                { "⚡ Diffuseur (Streamer)", "⚡ Streamer" },
                { "Diffuseur haute performance pour casques VR/XR", "High-performance streamer for VR/XR headsets" },
                { "AuraFlashXR par Pre-Release – Diffuseur", "AuraFlashXR by Pre-Release – Streamer" },
                { "Démarrer la diffusion haute performance vers votre casque VR.", "Start high-performance VR streaming to your headset." },
                { "Diffusion en direct (mise à jour toutes les 10 s)", "Live streaming (updated every 10 s)" },
                { "Diffusion : dernière session", "Streaming: last session" },
                { "En attente des premières statistiques (toutes les 10 s)...", "Waiting for initial statistics (every 10 s)..." },
                { "Casque : -", "Headset: -" },
                { "Rechercher le casque...", "Search headset..." },
                { "Rechercher un casque", "Search headset" },
                { "Pas de connexion ? Utilisez Rechercher le casque.", "No connection? Use Search headset." },
                { "Pas de connexion ? Rechercher le casque sur le réseau ou en USB.", "No connection? Search headset on network or USB." },
                { "Casque : aucun appairé – cliquez sur Rechercher le casque", "Headset: none paired – click Search headset" },
                { "Casque : recherche sur le réseau...", "Headset: searching network..." },
                { "Casque : non trouvé", "Headset: not found" },
                { "Casque : connecté", "Headset: connected" },
                { "Casque : déconnecté", "Headset: disconnected" },
                { "Casque : détecté", "Headset: detected" },
                { "Jeu : aucun", "Game: none" },
                { "Jeu :", "Game:" },
                { "Jeu", "Game" },
                { "Diffusé", "Streamed" },
                { "Affiché casque", "Displayed headset" },
                { "Débit", "Bitrate" },
                { "Latence", "Latency" },
                { "Pertes", "Loss" },
                { "non mesurée", "not measured" },
                { "paquets perdus", "packets lost" },
                { "Encodeur : initialisation...", "Encoder: initializing..." },
                { "Encodeur actif :", "Active encoder:" },
                { "Activer AuraFlashXR", "Enable AuraFlashXR" },
                { "AuraFlashXR (Actif)", "AuraFlashXR (Active)" },
                { "Activer SteamVR", "Enable SteamVR" },
                { "SteamVR (Actif)", "SteamVR (Active)" },
                { "Activer ADB (USB)", "Enable ADB (USB)" },
                { "Désactiver ADB", "Disable ADB" },
                { "Installer l'APK (Quest)", "Install APK (Quest)" },
                { "Autoriser dans le pare-feu", "Allow in Firewall" },
                { "Le pare-feu Windows bloque le diffuseur : le casque ne peut pas se connecter.", "Windows Firewall blocks the streamer: headset cannot connect." },
                { "Le pare-feu Windows bloque AuraFlashXR : le casque ne peut pas se connecter.", "Windows Firewall blocks AuraFlashXR: headset cannot connect." },
                { "Aucune règle de pare-feu Windows pour AuraFlashXR dans ce dossier : le casque ne pourra pas se connecter.", "No Windows Firewall rule for AuraFlashXR in this folder: headset will not be detected." },
                { "Génération d'images", "Frame generation" },
                { "Génération d'images désactivée (taux natif 1:1 à 120 Hz).", "Frame generation disabled (native 1:1 rate at 120 Hz)." },
                { "Mode non plafonné (expérimental) activé : configuré dans Paramètres avancés", "Uncapped mode (experimental) active: configured in Advanced settings" },
                { "Recentrer", "Recenter" },
                { "Miroir du bureau", "Desktop mirror" },
                { "Hauteur des yeux", "Eye height" },
                { "Qualité d'image", "Image quality" },
                { "Post-Processing", "Post-Processing" },
                { "Actif :", "Active:" },
                { "Lunettes (Sun Glasses) :", "Sun Glasses:" },
                { "Netteté (CAS)", "Sharpening (CAS)" },
                { "Exposition", "Exposure" },
                { "Vibrance", "Vibrance" },
                { "Hautes lumières", "Highlights" },
                { "Ombres (Shadows)", "Shadows" },
                { "Profils :", "Presets:" },
                { "Naturel", "Natural" },
                { "Éclatant (OLED)", "Vivid (OLED)" },
                { "Compétitif (Net+)", "Competitive (Sharp+)" },
                { "Cinéma", "Cinema" },
                { "Position & Espace", "Position & Space" },
                { "Paramètres d'image avancés", "Advanced picture settings" },
                { "Netteté (Sharpening)", "Sharpening" },
                { "Luminosité", "Brightness" },
                { "Contraste", "Contrast" },
                { "Saturation", "Saturation" },
                { "Rétablir par défaut", "Restore defaults" },
                { "Profil équilibré : Gamma 1.10, Netteté 0.15, couleurs neutres.", "Balanced preset: Gamma 1.10, Sharpening 0.15, neutral colors." },
                { "Profil couleurs riches type OLED : Gamma 1.10, Netteté 0.35, Contraste +0.06, Saturation +0.25.", "Rich OLED-like colors: Gamma 1.10, Sharpening 0.35, Contrast +0.06, Saturation +0.25." },
                { "Visibilité maximale à longue distance (FPS / Simu) : Gamma 1.15, Netteté 0.70, Contraste +0.08.", "Maximum long-range visibility (FPS / Sim): Gamma 1.15, Sharpening 0.70, Contrast +0.08." },
                { "Noirs profonds et contraste renforcé : Gamma 1.05, Netteté 0.20, Luminosité -0.02, Contraste +0.12, Saturation +0.15.", "Deep blacks and enhanced contrast: Gamma 1.05, Sharpening 0.20, Brightness -0.02, Contrast +0.12, Saturation +0.15." },
                { "Filtre de netteté adaptatif des contrastes (CAS).", "Contrast adaptive sharpening filter (CAS)." },
                { "Journalisation de débogage", "Debug logging" },
                { "Exporter les journaux...", "Export logs..." },
                { "Ouvrir le dossier des journaux", "Open logs folder" },
                { "Menu principal", "Main menu" },
                { "Ouvrir le dossier", "Open folder" },
                { "Diffuseur démarré. Connectez le casque pour lancer la session.", "Streamer started. Connect headset to start session." },
                { "Diffuseur arrêté (voir les journaux)", "Streamer stopped (see logs)" },
                { "Erreur de configuration : impossible de lancer le diffuseur", "Configuration error: unable to start streamer" },
                { "AuraFlashXR est déjà en cours d'exécution.", "AuraFlashXR is already running." },
                { "Impossible d'ouvrir le menu principal :", "Unable to open main menu:" },
                { "Détail des latences :", "Latency breakdown:" },
                { "Encodage", "Encode" },
                { "Réseau", "Network" },
                { "Décodage", "Decode" },
                { "File décodeur", "Decoder queue" },
                { "VSync casque", "Headset VSync" },
                { "Compositeur PC", "PC compositor" },
                { "moyenne", "average" },
                { "moyenne, décile lent", "average, slowest tenth" },
                { "périodes répétées", "repeated periods" },
                { "Répétitions hôte", "Host repeats" },
                { "sautées", "skipped" },
                { "Latence p95 :", "p95 latency:" },
                { "Prochaine fois :", "Next time:" },
                { "non diffusées :", "not streamed:" },
                { "Luminosité globale de l'image (au-dessus de 1.00 : plus lumineux).", "Overall image brightness (above 1.00: brighter)." },
                { "Filtre de netteté adaptatif des contrastes.", "Contrast adaptive sharpening filter." },
                { "Ajustement fin de la luminosité.", "Fine brightness adjustment." },
                { "Ajustement du contraste dynamique.", "Dynamic contrast adjustment." },
                { "Saturation des couleurs (en dessous de 0 : moins saturé).", "Color saturation (below 0: less saturated)." },
                { "Gamma 1.20, les autres valeurs au neutre.", "Gamma 1.20, other values neutral." },
                { "Entre les jeux, le casque affiche le bureau Windows sur un écran flottant ; votre main sert de souris (pincement = clic). Désactivé par défaut.", "Between games, headset displays Windows desktop on a floating screen; pinch to click. Off by default." },
                { "Auto : la fréquence est choisie selon les sessions enregistrées de ce jeu sur ce casque. Choisissez 1/3, 1/2 ou 2/3 manuellement pour quitter Auto.", "Auto: rate chosen from recorded runs of this game on this headset. Select 1/3, 1/2 or 2/3 manually to exit Auto." },
                { "La direction actuelle devient l'avant et votre position l'origine. Effectué à chaque lancement de jeu ; la touche de recentrage (End par défaut) fait de même en jeu.", "Current heading becomes forward and position becomes origin. Done on every game launch; recenter key does the same in-game." },
                { "Auto : estimation du sol par le casque. Manuel : valeur saisie (environ 1.30 m assis).", "Auto: headset floor estimate. Manual: typed value (~1.30 m seated)." },
                { "Hauteur des yeux au-dessus du sol, de 0.5 à 2.2 m.", "Eye height above floor, 0.5 to 2.2 m." },
                { "Écrit des fichiers dans logs\\debug\\ (coûte ~4.6 ms par trame) : activez-le pour signaler un problème, puis désactivez-le.", "Writes debug files to logs\\debug\\ (costs ~4.6 ms per frame): enable to diagnose, then turn off." },
                { "Ouvrir la fenêtre principale (paramètres, paramètres avancés, statistiques des jeux). Le diffuseur continue de tourner.", "Open main window (settings, advanced settings, game stats). Streamer keeps running." },
                { "1/3 : deux trames générées pour une trame rendue (jeux à 34-49 fps).", "1/3: two frames generated per rendered frame (games at 34-49 fps)." },
                { "1/2 : une trame générée pour une trame rendue (par défaut).", "1/2: one frame generated per rendered frame (default)." },
                { "2/3 : chaque trame envoyée est interpolée (jeux au-dessus de 67 fps).", "2/3: each sent frame is interpolated (games above 67 fps)." },
                { "Preset :", "Preset:" },
                { "ENREGISTRER LE PRESET", "SAVE PRESET" },
                { "Sauvegarder le preset", "Save preset" },
                { "Nom du preset (créé dans config\\, sélectionnable dans le lanceur) :", "Preset name (created in config\\, selectable in the launcher):" },
                { "Créer", "Create" },
                { "Annuler", "Cancel" },
                { "Preset '{0}' créé dans config\\{0}\\config\\. Sélectionnez-le dans le menu déroulant du lanceur.", "Preset '{0}' created in config\\{0}\\config\\. Select it in the launcher's dropdown menu." },
                { "Un preset nommé '{0}' existe déjà. Choisissez un autre nom.", "A preset named '{0}' already exists. Choose another name." },
                { "Ce nom est réservé : MON PRESET désigne la configuration de base.", "This name is reserved: MON PRESET is the base configuration." },
                { "Le nom du preset ne peut pas être vide.", "The preset name cannot be empty." },
                { "Impossible de créer le preset :", "Cannot create the preset:" },
                { "Un jeu est en cours. Changer de preset redémarre le diffuseur : le jeu perdra le casque quelques secondes. Continuer ?", "A game is running. Switching presets restarts the streamer: the game loses the headset for a few seconds. Continue?" }

            };

            foreach (var kv in map)
            {
                TranslationsFrToEn[kv.Key] = kv.Value;
            }
        }
    }
}