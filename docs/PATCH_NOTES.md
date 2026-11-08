# Patch Notes — AuraFlashXR V0

Historique complet des modifications depuis la refonte V0, en ordre chronologique (format : `Patch : Nom | Date + Heure` + paragraphe bref). Les nouvelles sections s'écrivent à la suite, en bas du fichier.

## Patch : Harmonisation de la marque AuraFlashXR | 25/10/2026 — 14:00

Renommage systématique de tous les anciens identifiants (exécutables, DLL, fichiers JSON, manifests, métadonnées) vers la marque officielle **AuraFlashXR**, avec correction des doublons de version en format uniforme. Les crédits upstream (alvr-org) sont conservés dans les licences et mentions tierces.

## Patch : Sauvegarde & cohérence GUI ↔ session.json | 25/10/2026 — 15:30

Ajout d'un bouton d'enregistrement forcé dans l'interface : chaque modification de réglage s'écrit et se vérifie directement dans `config/session.json`, éliminant les options fantômes. Au survol d'un paramètre, la clé JSON exacte ciblée est affichée en surbrillance pour un contrôle total et transparent.

## Patch : Sécurisation du mode Diffuseur (lecture seule) | 25/10/2026 — 16:00

Le mode Diffuseur (Streamer) est restreint en lecture stricte : il ne peut plus écraser, réinitialiser ni altérer `session.json` au lancement ou à la connexion.

## Patch : Pilote SteamVR & fluidification des mouvements | 25/10/2026 — 18:00

Correction du blocage à 90 Hz : le pilote déclare désormais le vecteur complet des fréquences supportées (72/80/90/120/144 Hz) et applique la fréquence demandée dynamiquement. La « Fluidification des mouvements » (Motion Smoothing) de SteamVR est prise en charge : identité de suivi native AuraFlashXR, drapeaux d'interpolation activés et support du throttling par application (`driver_auraflashxr.dll` compilé et déployé).

## Patch : Modernisation UI des Paramètres & défilement | 25/10/2026 — 19:00

Les listes compactes deviennent des cartes sombres distinctes avec en-têtes luminescents et bordures nettes. Ajout d'une marge de défilement en bas et réorganisation du dock d'ancrage : le panneau d'inspection n'occulte plus les dernières options.

## Patch : Langues et localisation (fr/en) | 31/10/2026 — 06:30

Moteur de traduction `Lang.cs` en place : chargement des XML (`french.xml`, `english.xml`), langue active mémorisée dans `selected.cfg`, auto-traduction fr→en en secours. Sélecteur de langue intégré au thème sombre dans le launcher.

## Patch : Profils casques & assistanat | 31/10/2026 — 07:00

Ajout des profils communautaires (Quest 2/3/3S/Pro, Pico 4) dans `profiles\`, assistant de configuration (Wizard), panel de réglages structuré et gestion des presets LOW/MID/HIGH/OPTI dans configure.exe.

## Patch : Chaîne de build réparée (toolchain VS 2022) | 31/10/2026 — 18:24

Réparation complète de la chaîne de compilation : sondage multi-chemins Visual Studio (Community/BuildTools) avec suffixe de chemin correct dans 9 scripts stage (`stage2/3/5/6/11/12/14/15/19/20/21`), correction du clone source (`AuraFlashXR-v0.1`, tag v20.14.1, paquet Rust `alvr_server_core`), et `deps.lock.json` mis en cohérence. Premier build complet réussi : `RESULT: OK` sur les 6 cibles (vdxr, host, shim, mirrordesktop, gui, package).

## Patch : install_apk_quest.bat harmonisé | 31/10/2026 — 18:30

Les menus d'installation APK affichent « AuraFlashXR V0 » avec les deux moteurs (OpenGL ES 3.2 / Vulkan) et pointent vers les APK réels du dossier `tools\APK\`. Aucune référence Apple Vision Pro touchée.

## Patch : Correction des 3 bugs du Launcher (GDI+, localisation, alignement) | 31/10/2026 — 20:00

Trois défauts visuels corrigés dans le launcher : (1) superpositions GDI+ et carré noir — fonds opaques forcés (`g.Clear(Ui.DarkCard)`) avant chaque dessin, `OnPaintBackground` neutralisé ; (2) clés de traduction brutes visibles — `Lang.Init()` appelé avant l'initialisation des libellés, `Lang.cs` ajouté aux 5 ensembles de compilation ; (3) alignement/espacement des cartes rétabli (marges et padding cohérents).

## Patch : Script d'enregistrement OpenXR embarqué (zéro .ps1 livré) | 01/11/2026 — 16:00

`register_openxr_runtime.ps1` n'est plus livré dans `bin\` : il est embarqué comme **ressource dans les exécutables** et extrait vers `%TEMP%\AuraFlashXR` à l'utilisation, avec `-InstallDir` explicite. Le packaging échoue désormais si un `.ps1` traîne dans le livrable. Source conservée dans `tools\install\` (jamais livrée, toujours testable via `Test-Install.ps1` qui l'extrait de launcher.exe).

## Patch : Nettoyage des doublons de dossiers | 01/11/2026 — 16:07

Suppression des doublons créés par erreur (`_RELEASE_BUILD`, `_APP_BUILD`, `- RELEASE_BUILD` dans `- SOURCE CODE AFXR`) : un seul `- RELEASE_BUILD` et un seul `- APP BUILD` à la racine `E:\PROJET-AXFR`, comme défini par la cartographie du projet.

## Patch : Toolbox OpenXR réorganisée | 01/11/2026 — 17:30

La bulle « CONFIG CHARGÉE » affiche un libellé propre (plus jamais de JSON brut). Le bouton « CHARGER LE PRESET » rejoint la ligne des actions de la Toolbox avec la bulle centrée dessous ; les boutons sont répartis sur 2 lignes équilibrées, « Activer AuraFlashXR » et « Installer l'APK (Quest) » alignés horizontalement.

## Patch : Presets intégrés restaurés (LOW/MID/HIGH/OPTI) | 01/11/2026 — 17:30

Les quatre presets intégrés sont de retour dans le livrable : `config\LOW\MID\HIGH\OPTI CONFIG\config\session.json`, chacun avec ses réglages calibrés (LOW : H264 72 Hz demi-résolution pour GPU faible ; MID : HEVC 90 Hz trois-quarts de résolution ; OPTI : HEVC 10-bit 120 Hz pleine résolution avec foveation ; HIGH : HEVC 10-bit 120 Hz sans foveation, qualité maximale). Ils apparaissent à nouveau dans le menu CHARGER LE PRESET du launcher et CHARGER CONFIG de configure.exe, en lecture seule, avec les étiquettes codec/GPU.

## Patch : Refonte UI/UX du Launcher (Bento Grid) | 01/11/2026 — 17:45

Refonte intégrale du launcher : disposition **Bento Grid** avec carte Hero « Diffuseur (Streamer) » en haut (bordure dégradée jaune/orange/rouge, icône éclair), grille 2×2 au centre (Statistiques, Paramètres, Diagnostics, Configuration du casque) et Toolbox OpenXR repensée en bas. Espacements resserrés (~8 px) pour un rendu compact, fenêtre à taille fixe 980×640 — plus aucune barre de défilement.

## Patch : Corrections visuelles critiques du Launcher | 01/11/2026 — 17:45

Suppression des émojis cassés (« ▯ ») remplacés par des icônes vectorielles GDI+ (éclair, graphique, engrenage, bouclier, casque) et nettoyage des caractères non-ASCII en tête des chaînes traduites. Header sombre 75 px avec logo, badge « PRE-RELEASE » en pilule bordée orange et sous-titre « Lanceur · V0 » dans une bulle assortie. Footer propre : « ● Prêt » vert, Aide et GitHub en deux boutons fantômes discrets. Contenu des cartes clippé au contour arrondi avec troncature « … » — plus aucun débordement.

## Patch : Liens officiels mis à jour | 01/11/2026 — 18:00

Le bouton **Aide** pointe vers le Discord officiel (`discord.gg/5dg72jKdTR`) et **GitHub** vers le nouveau dépôt (`github.com/AuraFlashXR/AuraFlashXR`). Vérifié : plus aucun lien vers l'ancien dépôt dans les interfaces.

## Patch : Patch notes embarqués au package | 01/11/2026 — 18:10

Ce fichier (`docs/PATCH_NOTES.md`) est désormais copié dans chaque build : présent dans `- APP BUILD` et dans le zip de release GitHub, à côté des licences et du README.

## Patch : Synchronisation des presets entre fenêtres | 01/11/2026 — 19:00

Le changement de preset se propage maintenant à toutes les fenêtres ouvertes : le Streamer surveille `config\preset.cfg` (toutes les 1,5 s, comme le configure.exe) et recharge la session + les réglages du preset à chaud ; les presets intégrés (LOW/MID/HIGH/OPTI CONFIG) embarquent désormais leur `auraflashxr.json` en plus du `session.json`, donc charger un preset met à jour les deux fichiers (session.json + auraflashxr.json) dans toutes les fenêtres — launcher, configure et streamer.

## Patch : Allègement du code source (purge des parasites) | 01/11/2026 — 20:30

Purge complète des parasites après audit par sous-agents : fichiers orphelins retirés (`SessionSync.cs`, `UiTest.cs`, `check_gui.sh`, `emblem.png`, `main.cpp`, `CMakeLists.txt`, 9 scripts stage abandonnés, fichier `-` résiduel — tous archivés dans `_BACKUP/20261101-purge/`), code mort supprimé des fichiers compilés (`Ui.Header`, `Ui.Family`, couleurs inutilisées, 3 usings morts), et ressource `chroma_passthrough.png` (~1 Mo) retirée des 5 exécutables qui l'embarquaient sans jamais la lire. Les exé passent de ~1,4-1,7 Mo à 0,35-0,77 Mo.

## Patch : Changement de preset = redémarrage du host | 02/11/2026 — 00:00

Quand un preset est chargé pendant que le host tourne, le Streamer le redémarre maintenant automatiquement avec la nouvelle configuration (l'encodeur, la résolution et les Hz sont lus par le host à son démarrage — recharger les JSON côté interface ne suffisait pas). Garde SteamVR respectée : si SteamVR tourne, le host n'est pas redémarré (ports UDP 9943/9944 occupés). La méthode d'arrêt propre du host est mutualisée dans `HostProcess.StopGracefully` (partagée launcher + streamer, arrêt par stdin « quit » pour ne pas laisser les haut-parleurs PC coupés).

## Patch : Les presets changent maintenant l'encodeur | 02/11/2026 — 00:30

Cause trouvée : l'encodeur (codec, preset NVENC, tuning, multi-pass) est lu par le host depuis `auraflashxr.json` (`video.*`), pas depuis `session.json` — et les presets intégrés avaient un `auraflashxr.json` sans section `video`. Chaque preset embarque maintenant sa configuration encodeur complète : LOW = H264 P1 low-latency, MID = HEVC P3, OPTI = HEVC P5 multi-engine (3 slices), HIGH = HEVC P7 multi-pass full-resolution. Changer de preset change donc codec ET résolution ET fréquence.

## Patch : auraflashxr.json complet dans les presets | 02/11/2026 — 00:45

Les presets intégrés (LOW/MID/HIGH/OPTI CONFIG) embarquent maintenant un `auraflashxr.json` avec **toutes les sections** (video, audio, system, display, controls, chroma, headset) au lieu de 2 clés — même structure que ceux d'une installation utilisée. Chaque preset garde sa variante encodeur : LOW = H264 P1 low-latency, MID = HEVC P3, OPTI = HEVC P5 + 3 slices multi-engine, HIGH = HEVC P7 full multi-pass.

## Patch : Badge du preset chargé dans le Streamer | 02/11/2026 — 01:00

Le preset actif s'affiche maintenant en badge à droite de la bulle encodeur du Streamer (même style que la bulle CONFIG CHARGÉE de configure.exe) : « CONFIG CHARGÉE : LOW CONFIG · LECTURE SEULE ». Le badge se met à jour en direct au changement de preset (poll 1,5 s).

## Patch : Dossier tools\ livré au package | 02/11/2026 — 01:15

Le package n'embarquait plus le dossier `tools\` dont l'app a besoin (régression du packaging vs v0.3) : bouton « Installer l'APK » et installation micro VB-Cable cassés, ADBForwarder introuvable. Livré maintenant : `tools\adbforwarder\` (Quest en USB, dans tools\ comme attendu par la sonde de chemins), `tools\APK\`, `tools\vbcable\` et `tools\install\` (install_apk_quest.bat + scripts micro). Le pilote SteamVR du v0.3 reste volontairement absent : le mode SteamVR actuel n'utilise pas de driver (OpenXR direct via VDXR, ou SteamVR natif « Compatible BattlEye »).

## Patch : Restructuration du package (tout dans tools\) | 02/11/2026 — 01:30

`language\` et `bench\` rejoignent `tools\` (comme le driver) : `tools\language\` (french.xml, english.xml, selected.cfg), `tools\bench\` (littlest_tokyo.vab, sakura_breeze.mp3, xr_probe.exe), `tools\auraflashxr\` (driver SteamVR). Redirections de chemins mises à jour dans le code : `Lang.GetLanguageDir` (tools\language, fallback conservé), `Configure.cs` + `TestRunner.cs` + `Reference.cs` (tools\bench). Le host Rust lisait déjà `tools\bench` (main.rs) — la scène bench fonctionne enfin des deux côtés. Racine du package désormais : AuraFlashXR.exe + bin\ + config\ + docs\ + profiles\ + tools\.

## Patch : Driver SteamVR AuraFlashXR de retour | 02/11/2026 — 02:30

Le driver SteamVR propre à AuraFlashXR est de retour, rangé dans `tools\auraflashxr\` (comme le bench) : récupéré de l'ancienne version et remis au nom de la marque — wrapper C++ renommé `driver_auraflashxr.dll`, DLL Rust `alvr_server_openvr.dll` (récupéré du build du 29/10, export `HmdDriverFactory`), manifest (nom `auraflashxr`, activation permanente, capture de tous les casques), et la chaîne FFmpeg complète. Le mode SteamVR (Compatible BattlEye) enregistre automatiquement ce driver via `EnsureSteamVrDriver`, dont le chemin a été mis à jour vers `tools\auraflashxr`.

## Patch : Traductions cassées (redirection tools\language) | 02/11/2026 — 02:45

Les fenêtres affichaient les clés brutes (Lang.Get) au lieu du texte traduit. Cause : GetLanguageDir() sondait BaseDirectory (bin\), puis l'ancien layout ..\language — plus aucun chemin ne pointait vers tools\language après le déplacement. Fix : fallback ..\tools\language ajouté en priorité (remonte de bin\ vers la racine du package). Les 5 exé GUI recompilés (cas partagé Lang.cs), package complet regénéré et livré dans - APP BUILD + zip V0 dans - RELEASE_BUILD.

Chaîne de build réparée pour le nouveau dossier de travail (stage16_gui.ps1 lit les sources dans tools\gui et la ressource register_openxr_runtime.ps1 dans tools\install ; setup_builder.ps1 et stage13_package.ps1 pointent vers ce checkout au lieu de l'ancien root). Le stage13 embarque maintenant toujours `config\auraflashxr.json` (réglages lus par le host : codec, idle_rgb, gamma…) — présent dans les presets, absent de la racine config\ du package depuis la refonte. Package complet livré dans `- APP BUILD\AuraFlashXR\` (racine AuraFlashXR.exe seule, bin\ avec les 5 exé + runtime OpenXR, config\ avec session.default.json + 4 presets + auraflashxr.json, profiles\ avec les 7 profils casques, tools\ avec language/bench/driver/adbforwarder/APK/vbcable/install, docs\ avec licences + patch notes). Le package n'est plus jamais assemblé dans out\dist : construit dans out\dist puis copié vers `- APP BUILD` conformément à la cartographie du projet.

## Patch : Presets par encodeur (sous-menus) | 04/11/2026 — 21:00

Le sélecteur de preset du launcher et le menu « CHARGER CONFIG » de configure.exe regroupent désormais les presets par encodeur : un sous-menu par famille (H.264, HEVC, HEVC 10-bit, AV1) contenant les quatre niveaux LOW / MID / HIGH / OPTI. 16 nouveaux presets générés à partir des profils existants (même résolution, fréquence et débit, seul le codec change : `openvr_config.codec` + `use_10bit_encoder` + `video.preferred_codec`), embarqués dans le package par stage13. Les presets personnalisés et MON PRESET restent des entrées simples ; OPTI CONFIG réapparaît dans le menu de configure.exe.

## Patch : Presets par encodeur par défaut | 04/11/2026 — 21:20

Les anciens presets LOW/MID/HIGH/OPTI CONFIG sont supprimés : les 16 presets par encodeur (LOW/MID/HIGH/OPTI × H.264, HEVC, HEVC 10-bit, AV1) deviennent les presets intégrés par défaut du package. Le launcher, configure.exe et le Streamer reconnaissent ces noms comme presets intégrés (lecture seule, non supprimables) ; le menu « CHARGER CONFIG » les liste par famille d'encodeur. Les noms CONFIG restent refusés à la création pour éviter les collisions avec d'anciens dossiers.

## Patch : Style des sous-menus de preset | 04/11/2026 — 21:35

Les sous-menus déroulants par encodeur (launcher et configure.exe) héritent maintenant du renderer sombre du menu parent : fond sombre, bordure et surbrillance identiques, plus de panneau clair décalé à l'ouverture.

## Patch : Fond des sous-menus de preset | 04/11/2026 — 21:50

Les sous-menus d'encodeur utilisaient le panneau clair par défaut de WinForms (marges image/coche), d'où un bloc détaché à côté du menu sombre. Ils sont maintenant des ToolStripDropDownMenu sans marges, peints par le renderer sombre (fond, bordure, surbrillance).

## Patch : Sous-menus de preset (construction dédiée) | 04/11/2026 — 22:10

Les sous-menus d'encodeur sont construits par un helper commun (tools/gui/PresetMenu.cs) : ToolStripDropDownMenu sans marges image/coche, fond sombre, renderer sombre et ombre, affecté au moment de la création du sous-menu. Le launcher et configure.exe utilisent ce helper.

## Patch : Bouton « Appliquer le preset » dans configure.exe | 04/11/2026 — 22:40

Nouveau bouton « APPLIQUER LE PRESET » dans la barre du bas de configure.exe : il enregistre les réglages affichés dans le preset actif puis marque ce preset comme actif (`config\preset.cfg`), pour que le launcher, le streamer et le host le prennent en compte. Sur un preset intégré en lecture seule, il bascule simplement le preset actif sans modifier ses fichiers. Libellés FR/EN ajoutés (`Settings.Btn.ApplyPreset`, `Settings.ApplyPreset.Done`).

## Patch : Presets intégrés modifiables (fin de la lecture seule) | 04/11/2026 — 23:05

Les presets intégrés (LOW/MID/HIGH/OPTI × H.264, HEVC, HEVC 10-bit, AV1) ne sont plus verrouillés : configure.exe les charge en édition normale, « Démarrer AuraFlashXR » et « Appliquer le preset » enregistrent sans blocage. Le badge « LECTURE SEULE » disparaît du launcher, de configure.exe et du streamer ; l'avertissement d'écrasement reste pour les presets intégrés.

## Patch : Installation silencieuse du micro et endpoints VB-CABLE | 04/11/2026 — 23:25

`install_auraflashxr_mic.bat` n'ouvre plus le setup graphique : le pilote s'installe en silence via `pnputil /add-driver … /install`. `rename_mic.ps1` renomme uniquement le câble basique (AuraFlashXR-CABLE / AuraFlashXR-MIC) et désactive les endpoints 16 canaux (DeviceState 0x10000001) au lieu de les renommer ; l'état de chaque endpoint est forcé (basique actif, 16 ch désactivé). `rename_mic.ps1` et les scripts micro sont désormais embarqués dans le package (`tools\install`).

## Patch : Renommage micro élevé et ciblé | 04/11/2026 — 23:40

`rename_mic.ps1` s'élève lui-même en administrateur (les clés `MMDevices` sont sinon en lecture seule : « Accès au registre demandé non autorisé ») et ne touche plus que les endpoints du driver VB-Audio (nom ou driver `VB-Audio`/`VB-Cable`, `CABLE Input`/`CABLE Output`) : un haut-parleur nommé « Speakers » ou « CABLE » n'est plus renommé. L'installation, le propriétaire et les droits des clés sont appliqués séparément pour ne pas masquer une erreur.

## Patch : Désinstallation micro corrigée | 04/11/2026 — 23:55

`uninstall_auraflashxr_mic.ps1` s'élève en administrateur (sinon les clés MMDevices restent en lecture seule) et supprime les endpoints avec `DeleteSubKeyTree` sur le chemin relatif et un seul argument — l'appel précédent passait « HKLM:\… » avec deux arguments, d'où « Impossible de supprimer une sous-clé : elle n'existe pas ».

## Patch : Renommage micro réécrit (PowerShell pur) | 05/11/2026 — 00:10

`rename_mic.ps1` est réécrit sans compilation C# à la volée : élévation administrateur, prise de propriété des clés `MMDevices` puis droits Administrateurs, écriture du nom et de `DeviceState` avec un message d'échec explicite. Seuls les endpoints du driver VB-Audio sont touchés (nom `CABLE Input`/`CABLE Output` ou driver `VB-Audio`) : « Speakers » n'est plus jamais renommé.

## Patch : Renommage micro fonctionnel | 05/11/2026 — 00:30

Cause trouvée : les clés `MMDevices` n'accordent aux Administrateurs que le droit `SetValue` ; l'ouverture en `ReadWriteSubTree` (qui exige aussi `ReadKey`) échouait avec « Accès au registre demandé non autorisé ». `rename_mic.ps1` ouvre désormais les clés avec le seul droit `SetValue` et écrit nom + `DeviceState`. Le câble basique s'appelant « Speakers » chez VB-Audio, le ciblage se fait sur le driver (`VB-Audio Virtual Cable`). Vérifié : `AuraFlashXR-CABLE`, `AuraFlashXR-CABLE in 16ch` (désactivé), `AuraFlashXR-MIC`.

## Patch : Réorganisation de l'onglet Vidéo & Encodage | 05/11/2026 — 00:50

L'onglet « Vidéo & Encodage NVENC » devient « Vidéo & Encodage ». Les options sont regroupées par thème : Résolution/Fréquence/Débit (avec marge de débit et multiplicateur de saturation), Codec & Encodeur NVENC (codec, profils, rate control, split, slices, P1-P7, tuning, multi-pass et options NVENC avancées), nouvelle section Couleur, HDR & Carte graphique (gamma d'encodage, HDR, index GPU), Fovéation, Tampon & Post-traitement client, AMF/Logiciel et PyroWave.

## Patch : Rangement par thème de l'onglet Vidéo & Encodage | 05/11/2026 — 01:10

L'onglet est découpé en sections cohérentes : Résolution/Fréquence/Débit → Codec & cadence (codec préféré, profil H.264, slices, entropie) → Contrôle du débit (rate control, AQ, GOP/P-frame, intra-refresh, refs max, tampon RC, max/avg) → Qualité & vitesse NVENC (preset, P1-P7, tuning, multi-pass, split) → Réglages NVENC avancés (weighted prediction, low-delay key, ordonnancement GPU, options d'encodage) → Couleur, HDR & Carte graphique → Fovéation → Tampon & post-traitement client → AMF/Logiciel → PyroWave.

## Patch : Sections Vidéo par thème réel | 05/11/2026 — 01:30

Nouvelle découpe : « Codec Vidéo & Structure de Flux » (codec préféré, profil H.264, slices, codage entropique — plus rien de générique dans NVENC), « Contrôle du Débit » (rate control, GOP/P-frame, intra-refresh, refs, tampon RC, max/avg), « Qualité & Vitesse NVENC » (AQ, preset global, multi-pass, split, P1-P7, tuning), « Réglages NVENC Avancés » (options d'encodage, weighted prediction, low-delay key, ordonnancement GPU), puis Couleur/HDR/Carte graphique, Fovéation, Tampon client, AMF/Logiciel, PyroWave.

## Patch : Section NVENC vide supprimée | 05/11/2026 — 01:45

La section « Codec & Encodeur NVENC » ne contenait plus aucune option après la réorganisation (son contenu est passé dans « Codec Vidéo & Structure de Flux » et « Qualité & Vitesse NVENC ») : elle est retirée. Les listes déroulantes de codage entropique et du mode AQ sont réinitialisées avant leur ligne, comme les autres.

## Patch : APK unique OpenGL + PyroWave Vulkan | 05/11/2026 — 02:30

L'APK devient un seul paquet `AuraFlashXR-V0.apk` : affichage OpenGL ES 3.2 (inchangé) et décodeur PyroWave en calcul Vulkan (`libpyroclient.so` + `libpyrowave-shared.so`, arm64) embarqués. Nouveau codec `Pyrowave` côté session/client ; `video_decoder/pyrowave.rs` parle au client natif (config 15 octets : taille, chroma 4:4:4, plage complète, transport, échéance, ondelette CDF 9/7 / CDF 5/3 / Haar, chemin de décodage) et rend des AHardwareBuffer importés comme une sortie MediaCodec. `install_apk_quest.bat` installe désormais ce fichier unique (plus de choix OpenGL/Vulkan). Dépendance `settings-schema` pointée sur le dépôt amont `alvr-org/settings-schema-rs` (l'ancien dépôt renommé renvoyait 404).

## Patch : Version unifiée V0 host + APK | 05/11/2026 — 01:30

L'APK annonçait la version 0.1.0 alors que le host était en 20.14.1 : le protocol ID (hash de la version majeure) différait, d'où « Trusted client is incompatible » et l'absence de connexion. Le workspace du host et celui de l'APK sont désormais tous deux en `0.0.0` (V0) ; host et APK sont rebuildés ensemble et l'APK installé est aligné sur le host (même protocol ID, handshake accepté).

## Patch : Version V0 stricte (0.0.0) | 05/11/2026 — 01:45

L'APK embarquait encore la chaîne `20.14.1` (constante de version compilée dans `auraflashxr_common`) : le workspace APK et le workspace host passent tous deux à `0.0.0`, la version majeure 0 donnant le protocol ID de la V0. `server_version` / `hide_while_version` mis à jour dans `session.default.json`, les 16 presets et `tools/install/session.default.json`. Vérifié : aucune occurrence de `20.14` dans `AuraFlashXR-V0.apk` ni dans `auraflashxr_host.exe`.

## Patch : Intervalle de rafraîchissement en menu déroulant | 05/11/2026 — 02:00

La carte « Diffusion en direct » n'affiche plus l'intervalle dans son titre (l'encodage des tuiles « Diffusé » / « Affiché casque » / « Débit » est corrigé). Un menu déroulant 1 s / 2 s / 3 s / 4 s / 5 s / 10 s est placé à droite du titre de la carte (`ModernGroupBox.HeaderControl`) ; il applique l'intervalle au host en direct (`status_secs`) et le mémorise pour les sessions suivantes.

## Patch : « Appliquer le preset » enregistre au bon endroit | 05/11/2026 — 02:12

Deux causes au preset qui ne s'appliquait pas entièrement. 1) Le bouton marquait le preset actif **après** `SaveSettings()`, qui écrit dans le dossier du preset actif : les réglages partaient dans l'ancien preset puis le marqueur pointait vers le nouveau. Le preset est désormais activé avant l'enregistrement. 2) `settings.Save()` n'écrivait que `config\auraflashxr.json` (réglages de base), jamais `config\<preset>\config\auraflashxr.json` que lit le host : nouveau `AppSettings.SaveActivePreset()` qui écrit les deux, utilisé par configure.exe, donc les options hors `session.json` (frame generation, NVENC avancé, PyroWave, etc.) suivent maintenant le preset.

## Patch : Audit des réglages configure.exe ↔ session.json / auraflashxr.json | 05/11/2026 — 02:25

Tour complet des 139 lignes de configure.exe : chaque contrôle est bien sauvegardé, mais trois clés écrites dans `openvr_config` n'existaient dans aucun JSON (`nvenc_slice_count`, `nvenc_split_encode_mode`, `nvenc_strict_vbv`) — ajoutées à `session.default.json` et aux 16 presets. Les quatre tailles de tampons socket ne sont plus écrites seulement en mode Custom (une valeur saisie n'est plus perdue). `video.frame_gen_rate_before_uncapped` documentée dans `auraflashxr.json` et les presets. Vérifié : plus aucune clé lue ou écrite par configure.exe absente des deux JSON.

## Patch : Preset réécrit hors schéma (image en damiers) | 05/11/2026 — 02:40

Cause de l'image brouillée en LOW H.264 : configure.exe réécrivait `session.json` avec des clés que le schéma ne déclare pas (`foveated_encoding.content.gui_collapsed`, `bitrate.mode.Adaptive.saturation_multiplier`). Le host charge ce fichier avec serde_json et, sur une clé inconnue, **refuse tout le fichier** : il gardait alors ses valeurs par défaut (CBR 300 Mbps, pas de fovéation, P1/ultra-low-latency) au lieu du preset. Corrections : `Json.Prune` retire à l'enregistrement les clés hors schéma (fovéation, bitrate, encoder_config, NVENC, openvr_config) ; le multiplicateur de saturation est relu ; les presets NVENC (P1-P7, tuning, multi-pass) sont relus depuis le `session.json` du preset, `auraflashxr.json` ne servant que de repli.

## Patch : Damiers & latence de décodage (client Quest + host) | 05/11/2026 — 03:10

Diagnostic des logs Quest 3 : décodage 13 ms pour une frame de 8,3 ms, file décodeur 3,3 ms, vsync_queue 15 ms, et 1951 avertissements `VideoRenderQualityTracker` (une frame présentée toutes les 8,3 ms alors que le runtime en attend une toutes ~16,6 ms : chaque image restait affichée deux fois, d'où les carrés). Corrections client : `dequeue_frame_blocking` vérifie la file sous le verrou avant d'attendre (une frame arrivée entre le test et l'attente coûtait une frame entière) ; `xrBeginFrame` passe en tête de boucle (la frame lue est celle de la frame en cours) ; présentation à l'heure d'affichage du runtime au lieu de l'horodatage réseau (plus de Early/Tear) ; ImageReader ramené de 10 à 4 tampons (~280 Mo libérés) ; `max_buffering_frames` 1,5 → 1,0 dans `session.default.json` et les 16 presets. Host : `nvenc_slice_count`, `nvenc_strict_vbv` et `nvenc_qp_map` survivent à l'enregistrement de configure.exe (le prune de `openvr_config` les supprimait, le host retombait sur 8 tranches), et `video.split_encode: "off"` est enfin lu par le host.

## Patch : Crash au lancement (XR_ERROR_CALL_ORDER_INVALID) | 05/11/2026 — 03:12

L'APK précédent appelait `xrBeginFrame` avant `xrWaitFrame` : le runtime Meta refuse cet ordre et l'application s'arrêtait aussitôt (SIGABRT, `called Result::unwrap() on an Err value: ERROR_CALL_ORDER_INVALID`). La séquence reprend l'ordre imposé par OpenXR : `xrWaitFrame` puis `xrBeginFrame` puis `xrEndFrame`, le début de boucle ne faisant plus que le traitement des événements.

## Patch : Décodeur configuré en 30 fps (damiers) | 05/11/2026 — 03:25

Le client n'annonçait pas la fréquence du flux au décodeur : `MediaFormat` restait sur `frame-rate = 30`, et le décodeur Qualcomm cadencait ses sorties à 30 Hz (frames présentées toutes les 33,3 ms au lieu de 8,3 ms, `Early=155` côté runtime, image figée par blocs). `VideoDecoderConfig` porte désormais `frame_rate_hz`, rempli avec la fréquence du casque (120 Hz) au moment de créer le décodeur, et `android._video-scaling` est passé à 0 pour laisser le staging faire seul la mise à l'échelle.

## Patch : Content time continu pour le tracker vidéo | 05/11/2026 — 03:35

Le tracker de qualité vidéo du runtime suivait l'horodatage réseau des frames : celui du PC saute de plusieurs secondes quand des frames sont perdues ou à chaque reconnexion, d'où 4526 avertissements « content time jumped ~8,3 s forward » en 11 s et l'image figée par blocs. Le client tient désormais un compteur monotone (`content_time`, +1/fréquence du casque à chaque frame décodée) au lieu de transmettre le timestamp de capture. Le `frame-rate` du décodeur reste à la fréquence du casque.

## Patch : Journal du content time (diagnostic) | 05/11/2026 — 03:45

Le client écrit une ligne par seconde (`[AFXR] content_time … ns, stream timestamp … ns`) pour vérifier sur le casque que le compteur de content time avance bien d'un intervalle de frame et ne saute jamais. Aucun changement de comportement de rendu.

## Patch : Content time présenté au runtime | 05/11/2026 — 03:55

Le compteur de content time était calculé mais jamais transmis : `xrEndFrame` recevait encore l'heure d'affichage, et le tracker vidéo du runtime continuait de lire l'horodatage réseau du PC (sauts de ~8,3 s, image figée). `StreamContext::render` renvoie maintenant le content time monotone (un pas par frame, 1/120 s) et la boucle de rendu l'ancre sur l'heure d'affichage de la première frame avant de le passer à `xrEndFrame`. Les statistiques par frame gardent l'horodatage réel.

## Patch : Unités MediaCodec et validation des NAL H.264 | 05/11/2026 — 03:56

Le client transmettait les timestamps de frames en nanosecondes à `queue_input_buffer` (microsecondes attendues), puis passait les microsecondes de sortie directement à `release_output_buffer_at_time` (nanosecondes attendues), ce qui désynchronisait le temps média de la cadence réelle. Les conversions sont maintenant explicites dans les deux sens. Le parseur Annex-B H.264/HEVC valide désormais les préfixes et la taille avant d’accéder à un NAL, et écarte les paquets malformés au lieu de lire hors limites.

## Patch : Contrôle des prérequis de build host | 05/11/2026 — 04:02

`build.ps1` cherchait `afxr_server_core.dll` sous un ancien nom de dossier et de binaire. Le contrôle déclarait donc les dépendances absentes et relançait le setup VDXR/OpenXR inutilement ; il vérifie désormais le chemin et le nom de DLL réellement générés.

## Patch : Crash au démarrage (requestPermissions) | 05/11/2026 — 16:30

Le client plantait instantanément (SIGABRT, `MethodNotFound { name: "requestPermissions" }`) : la permission micro était demandée par JNI sur le contexte **application** de `ndk_context`, qui n'expose pas cette méthode (seule une Activity la possède). `try_get_permission` ne panique plus : l'appel est tenté, l'échec est journalisé (`warn`) et la fonction rend `Ok` — la fonctionnalité micro se dégrade au lieu d'arrêter l'application.

## Patch : Retour au code APK fonctionnel | 05/11/2026 — 17:10

L'APK est reconstruit depuis le code source restauré (celui où H.264, HEVC et AV1 fonctionnent) : version 0.0.0, boucle OpenXR wait → begin → end d'origine, file décodeur d'origine (polling 500 µs, 10 tampons ImageReader), pas de compteur content time. Le code modifié aujourd'hui (frame-rate, content time, permission JNI) est écarté ; l'analyse des logs continue sur une base saine.

## Patch : Presets réduits à un par encodeur | 05/11/2026 — 17:25

Les 16 presets LOW/MID/HIGH/OPTI × codec sont remplacés par trois presets uniques construits sur l'ancien LOW H.264 : `OPTI H.264`, `OPTI HEVC` et `OPTI AV1` (même résolution 3040×3248, 120 Hz, 500 Mbps, P1 low-latency, fovéation ; seul le codec et l'entropie changent — CAVLC en H.264, CABAC en HEVC/AV1). La liste de packaging (`stage13_package.ps1`) ne livre plus que ces trois dossiers, et les menus du launcher et de configure.exe les affichent à plat, sans sous-menu par encodeur.

## Patch : Préréglages de résolution 100 % → 150 % | 05/11/2026 — 18:10

La liste des résolutions de configure.exe repart de la résolution native du Quest 3 : **100 % Native (2064×2208)**, puis **110 / 120 / 130 / 140 / 150 %** (2272×2432, 2464×2656, 2688×2880, 2880×3104, 3104×3328), chaque palier arrondi à un multiple de 32 comme l'encodeur l'exige — seule la résolution native garde sa largeur exacte de 2064. Les anciens paliers Very Low → Extreme sont supprimés, le libellé perd « Remix » (FR/EN). Les trois presets OPTI sont alignés sur **120 % (2464×2656)**, dans `transcoding_view_resolution` comme dans `openvr_config`.

## Patch : Menu « Charger un preset » nettoyé | 05/11/2026 — 18:20

Le menu de chargement de configure.exe construisait encore les 16 entrées LOW/MID/HIGH/OPTI × quatre familles (dont HEVC 10-bit) au lieu de lire les presets livrés. Il liste désormais uniquement les trois presets existants : OPTI H.264, OPTI HEVC et OPTI AV1, puis les presets personnels de l'utilisateur et MON PRESET.

## Patch : SteamVR ne coupe plus un host qui streame | 05/11/2026 — 18:35

Au démarrage de SteamVR, le streamer arrêtait systématiquement `auraflashxr_host.exe` pour laisser `driver_auraflashxr.dll` prendre les ports 9943/9944. Quand un casque était déjà connecté au host, cet arrêt laissait le driver sans données : SteamVR passait en `NotReady` avec « Casque : Recherche… » et l'affichage tombait. Le host n'est désormais stoppé que s'il ne streame aucun casque (`RuntimeStreaming()` faux) ; sinon il reste en place et le log le signale.

## Patch : Content time monotone (APK) | 05/11/2026 — 18:50

Le code APK a été restauré sans le correctif content time : le runtime recevait l'horodatage réseau du PC, qui saute de 33 ms quand des frames sont perdues (`VideoRenderQualityTracker: content time jumped 33346 ms`), l'image restait figée une frame sur quatre et la session finissait par être coupée (`os error 10053` après 2 min 46 s à ~30 fps). Le client tient de nouveau un compteur monotone (`content_time`, +1/fréquence du casque par frame décodée), ancré sur l'heure d'affichage de la première frame avant d'être passé à `xrEndFrame`.

## Patch : Diagnostics de découverte UDP + driver recompilé | 05/11/2026 — 19:10

Le driver `driver_auraflashxr.dll` est recompilé depuis le workspace APK (il datait du 02/11) et embarque deux logs de diagnostic dans `server_core::handshake_loop` : `discovery socket bound on UDP 9943 (handshake listening)` quand le socket d'écoute est pris, et `discovery: N client(s) found` quand des casques sont découverts. Cela permet de savoir si c'est le host ou le driver (dans vrserver) qui détient réellement le port de découverte. Correction au passage : `web_server.rs` référençait `ServerRequest::RegisterAuraflashxrDriver`, variante renommée `RegisterAuraFlashXRDriver`, ce qui empêchait la compilation du workspace.

## Patch : Ordre de lancement SteamVR | 06/11/2026 — 04:15

Quand le runtime OpenXR actif est SteamVR (`steamxr_win64.json`), `streamer.exe` ne démarre plus `auraflashxr_host.exe` : il affiche « Runtime SteamVR actif : SteamVR démarre en premier, le host reste arrêté » et lance SteamVR (`steam://rungameid/250820`, sinon `vrstartup.exe`). C'est `vrserver.exe` qui doit détenir UDP 9943/9944 via `driver_auraflashxr.dll` ; un host lancé seul se connecte au casque mais SteamVR reste sur « Recherche… ». Nouvelle clé `Streamer.SteamVrStartFirst` (FR/EN) et helper `OpenXr.IsSteamVrActive()`.

## Patch : Panic du logger du driver | 06/11/2026 — 04:26

`server_core::logging_backend` appelait `serde_json::from_str(...).unwrap()` sur tout message commençant par `{` et finissant par `}`. Un dump de session ou de config loggé en clair faisait donc paniquer le driver (`missing field 'id'`), que SteamVR relançait en boucle. Un message qui n'est pas un Event est désormais loggé en texte brut.

## Patch : Le driver suit le preset chargé dans le launcher | 06/11/2026 — 04:32

Le driver lisait `tools/auraflashxr/session.json`, une copie figée qui ne suivait pas le preset choisi dans le launcher. Le launcher et le Streamer exportent maintenant `AURAFLASHXR_CONFIG_DIR` (dossier `config` du preset actif) au lancement de SteamVR, et `filesystem::Layout::new` l'utilise en priorité sous Windows : le driver dans `vrserver.exe` lit donc `config/<preset>/config/session.json` et écrit ses logs dans `config/<preset>/logs/`. Changer de preset dans le launcher suffit, sans recopier de fichier.

## Patch : Télémétrie du preset actif | 06/11/2026 — 04:36

Le driver suivant désormais `AURAFLASHXR_CONFIG_DIR`, il écrit sa télémétrie et lit ses fichiers live dans `config/<preset>/logs/`, alors que le Streamer cherchait encore `logs/` à la racine : les tuiles de statistiques restaient vides quand SteamVR lançait le jeu. Le Streamer lit `steamvr_status.json` dans le dossier de logs du preset actif (repli sur la racine), et `live_control.cfg` / `live_postproc.cfg` sont écrits dans les deux dossiers.
## Patch : Pacing SteamVR activé (jeu bridé à la fréquence du casque) | 06/11/2026 — 05:12

Un jeu léger (Pistol Whip, ~1,5 ms par frame) rendait ~380 fps pour 120 affichées : le GPU et l'encodeur travaillaient pour des frames jamais vues, et le débit montait à plus de 1 Gbps. Le driver ne déclarait pas `Prop_Hmd_SupportsAppThrottling_Bool`, donc SteamVR grisait « Fluidification des mouvements » et le throttling par application : aucun limiteur ne pouvait s'appliquer. Le HMD pose maintenant cette propriété, `OvrDirectModeComponent::PostPresent` honore `Throttling_t::nFramesToThrottle` (une attente de vsync par frame bridée) et `GetFrameTiming` remplit `vr::DriverDirectMode_FrameTiming`. Résultat mesuré : JEU 118,6 fps / AFFICHÉ 120 fps, débit ramené à 469 Mbps. Le limiteur maison côté `Present` (jet de frames puis `sleep_until`) a été essayé puis entièrement retiré : il ne pouvait pas agir, `Present` étant appelé par vrcompositor et non par le jeu.

### État du driver SteamVR au 06/11/2026

- Découverte UDP 9943 : le socket appartient bien à `vrserver.exe` (driver chargé), le casque se connecte et le playspace est reçu.
- Le driver lit le preset actif du launcher via `AURAFLASHXR_CONFIG_DIR` (sinon il retombait sur `tools/auraflashxr/session.json`, une copie par défaut en 800×900 réécrite à chaque démarrage).
- Le logger ne panique plus sur un message JSON qui n'est pas un Event (`missing field 'id'`), ce qui faisait relancer le driver en boucle par SteamVR.
- La fluidification de mouvement (motion smoothing) n'est pas exposée : elle demande la reprojection des textures côté SteamVR, fragile sur un flux encodé à distance. La bonne place est la reprojection côté APK, comme ALVR. Chantier volontairement écarté.

## Patch : Nettoyage des résidus du code source | 06/11/2026 — 05:47

Passage complet sur `SOURCE CODE AFXR` pour retirer les traces des anciennes versions. Les trois occurrences `AirFrameXR` sont corrigées (dont un log visible dans `client_openxr`), les 185 occurrences de la marque `AuraFlashXR-Remix` deviennent `AuraFlashXR` (25 fichiers), les 94 commentaires numérotés `v0.4`/`v0.5` sont supprimés (6 fichiers GUI) et les 64 lignes de commentaires corrompues par un mauvais décodage dans `Configure.cs` sont réécrites en français lisible. Le seul `ALVR` restant est le lien GitHub de crédit upstream, conservé. Les mentions `Apple Vision Pro` et le code de démonstration utilisé par `uitest.exe` n'ont pas été touchés.

## Patch : Refonte visuelle du Launcher (design tokens + Toolbox structurée) | 06/11/2026 — 06:15

La palette passe aux tokens « hardware control center » : fond `#0B0E14`, cartes `#121722`/`#161C28`, survol `#1C2433`, bordures `#232D3F` s'allumant en `#FF7A00`, texte `#F8FAFC`/`#94A3B8`, vert émeraude `#10B981` et or `#FFA800` (nouvelles constantes `Ui.DarkCardTop`, `Ui.DarkHeroTop`, `Ui.Gold`, `Ui.Emerald`, `Ui.BadgeAmber`). Les cartes Bento reçoivent un dégradé vertical de surface, une bordure 1px et une icône logée dans un badge carré arrondi 44×44 (ambre 15 %) au lieu d'une icône flottante ; la carte principale garde son dégradé hero et son titre en 13pt. La Toolbox OpenXR devient un panneau à trois niveaux : en-tête avec titre, état « Prêt » et pill de configuration alignée à droite, filet de séparation, puis deux groupes légendés (RUNTIME / SERVEUR et OUTILS & DÉPLOIEMENT) séparés par un filet vertical, tous les boutons à 34px de haut et 8px d'écart. Le pied de page utilise des ghost buttons homogènes et la pastille verte `Ui.Emerald`. Aucun bouton, texte ni action n'a été retiré.

## Patch : Alignement de la Toolbox et fenêtre du Launcher | 06/11/2026 — 06:18

La refonte précédente débordait : la Toolbox était coupée et les boutons passaient sur deux lignes. La fenêtre passe à 1040×760, la Toolbox prend la hauteur restante jusqu'au pied de page, et chaque ligne de boutons devient une `TableLayoutPanel` à colonnes égales — largeur calculée par `ButtonWidth()` — au lieu d'un `FlowLayoutPanel` qui repliait le contenu. Les deux groupes sont répartis 62 % / 38 %.

## Patch : Toolbox empilée, hauteur ajustée et footer dégagé | 06/11/2026 — 06:22

Les deux groupes de la Toolbox étaient côte à côte (62 % / 38 %) : « Lancer SteamVR » et « CHARGER LE PRESET » sortaient de leur bouton. Ils sont désormais empilés sur toute la largeur (RUNTIME / SERVEUR avec ses 4 boutons, puis OUTILS & DÉPLOIEMENT avec ses 3), et la hauteur du panneau se calcule sur son contenu (`GetPreferredSize`) au lieu d'occuper l'espace jusqu'au pied de page — plus de grand vide noir. Le pied de page passe à 44 px avec 10 px de marge haute pour que « Prêt », « Aide », « GitHub » et le sélecteur de langue soient entièrement visibles.

## Patch : Typographie lisible et rythme vertical du Launcher | 06/11/2026 — 06:26

Les polices de base passent de 9,5 à 10,5 pt (`Body`, `SemiBold`, `Bold`), les titres de section à 11 pt et `Big` à 12,5 pt : les textes étaient illisibles sur un écran haute densité. Le rythme vertical est resserré — cartes de 104 px avec 8 px d'écart, Toolbox collée sous la grille, fenêtre ramenée à 1040×720 — ce qui supprime le grand vide entre la Toolbox et le pied de page. Dans la Toolbox, les légendes de groupe passent à 8,5 pt et s'alignent sur le bord gauche des boutons, les boutons à 36 px de haut avec 10 px d'écart et une largeur mesurée sur le texte. Le sélecteur de langue est ancré au bord droit du pied de page au lieu d'une marge fixe de 480 px.

## Patch : Effets de matière sur le Launcher | 06/11/2026 — 06:30

L'interface était plate : chaque surface reçoit maintenant sa matière. Les boutons ont un dégradé vertical, un reflet haut et un halo qui s'allume au survol ou à l'activation (rayon 8 px). Les cartes Bento ont un voile lumineux sur la moitié haute, ce qui les fait lire comme du verre éclairé. Le panneau Toolbox reprend le même dégradé et le même voile, avec un liseré ambre et un halo extérieur. Le header gagne un halo ambré derrière le logo et sa ligne de séparation passe en dégradé atténué aux deux extrémités.

## Patch : Retour à un rendu plat et net | 06/11/2026 — 06:32

Les effets de matière ajoutés juste avant donnaient des boutons en pilules 3D biseautées (`ControlPaint.Light/Dark` + reflet) qui juraient avec le reste de l'interface. Les boutons repassent en aplat avec bordure 1px et rayon 8 px, la Toolbox retrouve sa surface unie `Ui.DarkCard` avec bordure `Ui.DarkBorder`, et le voile des cartes comme le halo du header sont retirés. Le pied de page passe à 48 px pour que « Prêt », « Aide », « GitHub » et le sélecteur de langue soient entièrement visibles.

## Patch : Launcher aligné sur la maquette | 06/11/2026 — 06:36

Chaque carte prend sa propre teinte (ambre pour le diffuseur, or pour les statistiques, bleu acier pour les paramètres, orange pour les diagnostics, bleu pour le casque) : badge d'icône, bordure et halo de survol suivent cette couleur. La carte du diffuseur gagne en hauteur (112 px) avec un badge 56 px et un titre en dégradé or, la grille 2×2 repasse à 92 px pour laisser respirer la Toolbox. L'en-tête du panneau reçoit une pastille carrée ambre et un titre à 13 pt, et le pied de page voit ses liens Aide / GitHub / langue alignés à droite.

## Patch : Toolbox sur deux lignes avec légendes | 06/11/2026 — 06:40

La Toolbox suit maintenant la maquette : légende « RUNTIME / SERVEUR » puis quatre boutons sur une seule ligne (Activer AuraFlashXR, SteamVR, Hôte, Lancer SteamVR), puis « OUTILS & DÉPLOIEMENT » avec trois boutons (ADB, Installer l'APK, Charger le preset). La pastille d'en-tête devient un carré ambre dégradé et la bulle de configuration reste en haut à droite du panneau.

## Patch : Compilation du Launcher modifié | 06/11/2026 — 06:44

Compilation du `Launcher.cs` modifié manuellement : build Roslyn OK (0 erreur), `launcher.exe` déployé dans `APP BUILD\AuraFlashXR\bin\` et capture de contrôle vérifiée.

## Patch : Launcher version claire (sans badges d'icônes) | 06/11/2026 — 06:47

Les cartes passent en design épuré : plus de badge coloré, icône fine ambre directement posée à gauche, titre blanc et description sur deux lignes. La carte principale garde une bordure dégradée orange→rouge, les autres une bordure grise discrète. L'en-tête descend à 64 px avec logo 46 px et pilules contourées, la grille respire (88 px de haut), le pied de page passe à 40 px avec « Prêt », « Aide » et « GitHub » à gauche, et le sélecteur de langue est retiré du footer.

## Patch : Rendu fidèle au screen détaillé | 06/11/2026 — 06:50

Application point par point de la maquette : surfaces bleu-nuit légèrement plus claires, bordures gris-bleu visibles sur les quatre cartes, bordure dégradée orange→rouge à l'horizontale sur la carte Diffuseur, icônes fines ambre sans badge, descriptions sur deux lignes, pastille carrée ambre vide en tête de la Toolbox et boutons de pied de page gris remplis.

## Patch : Reproduction pixel de la maquette | 06/11/2026 — 06:53

Surfaces des cartes calées sur #1B2029 → #141920, bordure grise #2E3644 sur les quatre cartes et dégradé orange→rouge horizontal sur la carte Diffuseur. Titres en Segoe UI Bold 15,5 pt (hero) / 14 pt, descriptions 11 / 10,5 pt sur deux lignes, icônes 30 px à 22 px du bord. Hero à 100 px de haut, grille à 92 px, en-tête à 64 px avec logo 44 px et pilules contourées.

## Patch : Alignement complet sur la maquette | 06/11/2026 — 06:56

Fenêtre portée à 1360×744, légendes « RUNTIME / SERVEUR » et « OUTILS DÉPLOIEMENT » restaurées au-dessus des groupes de boutons, boutons à 34 px, pilule de configuration en contour vert sur fond translucide, icônes Paramètres (engrenage contour) et Diagnostics (cœur-pouls) redessinées, logo horizontal « AuraFlash » + « XR », fond de fenêtre en dégradé bleu-nuit.

## Patch : Logo d'origine restauré et fenêtre calée | 06/11/2026 — 06:58

Le logo `logo_640.png` d'origine revient dans l'en-tête en 56 px (icône + wordmark complet), avec le texte « AuraFlash XR » replacé juste à côté et les badges PRE-RELEASE / Lanceur · V0 à la suite. Fenêtre passée à 1372×744 et en-tête ramené à 72 px.

## Patch : Header néon fidèle à la maquette | 06/11/2026 — 07:12

Le header reprend la maquette : logo `auraflashxr.ico` à gauche, wordmark « Aura / Flash / XR » en dégradé bleu clair → orange, et les deux badges `PRE-RELEASE` / `Lanceur · V0` en pilules néon (halo orange, contour dégradé ambre→orange, texte orange).

## Patch : Logo agrandi et wordmark néon | 06/11/2026 — 07:20

Le logo `.ico` passe de 44 à 57 px (+30 %) et le wordmark « AuraFlashXR » est redessiné en Segoe UI Black 24 pt avec remplissage dégradé rouge → orange, halo néon et liseré clair de relief.

## Patch : Orbitron embarquée et logo +30 % | 06/11/2026 — 07:30

Le wordmark « AuraFlashXR » utilise désormais la police Orbitron (fichier `Orbitron.ttf` embarqué en ressource, chargé via `PrivateFontCollection` — aucune installation requise) en 19 pt gras avec dégradé rouge → orange et halo néon. Le logo `.ico` passe de 57 à 74 px (+30 %).

## Patch : Wordmark réellement en Orbitron | 06/11/2026 — 07:38

La collection `PrivateFontCollection` est désormais conservée en champ statique (elle était libérée, ce qui faisait retomber GDI+ sur Segoe UI) et la famille « Orbitron Black » est sélectionnée explicitement : le titre s'affiche enfin dans la bonne typo.

## Patch : Wordmark retiré du header | 06/11/2026 — 07:45

Le texte « AuraFlashXR » est supprimé de l'en-tête : il ne reste que le logo à gauche et les deux badges néon, qui se placent désormais juste à droite du logo.

## Patch : Logo `logo_640.png` dans le header | 06/11/2026 — 07:55

Le header affiche désormais le logo horizontal `logo_640.png` (ratio conservé, 54 px de haut) à la place de l'icône `.ico`, les badges néon restant alignés juste à sa droite.

## Patch : Logo agrandi de 40 % | 06/11/2026 — 08:05

Le logo `logo_640.png` du header passe de 54 à 76 px de haut (+40 %), largeur toujours proportionnelle, les badges néon se décalant automatiquement à sa droite.

## Patch : Fond "ciel orageux" + halo central | 06/11/2026 — 07:20

Le fond du Launcher passe d'un dégradé noir à un dégradé bleu ardoise clair (haut #2A3852 → bas #121926) avec deux halos lumineux bleutés superposés au centre (PathGradientBrush), pour un rendu de ciel orageux éclairé.

## Patch : Fond nuit bleu orageuse | 06/11/2026 — 07:35

Fond assombri : dégradé nuit bleue (#1A243A → #0B101A) et halos centraux réduits (#5A86C8 / #8AA8E0) pour un rendu ciel orageux nocturne.

## Patch : Halo central recentré et atténué | 06/11/2026 — 07:50

Halo lumineux ramené au centre exact (50 % de hauteur), rayon réduit (0,40) et opacité abaissée (26 / 30) pour un effet moins marqué.

## Patch : Ciel orageux sur header et footer | 06/11/2026 — 08:10

Le header (72 px) et le footer (40 px) reprennent le même fond que la fenêtre : dégradé nuit bleue (#1A243A → #0B101A) avec halo central discret, pour une continuité visuelle haut/bas.

## Patch : Suppression de la délimitation header/footer | 06/11/2026 — 08:25

Le header et le footer sont désormais `Color.Transparent` ; la fenêtre redessine leur bande de ciel (`PaintSkyBand`) en échantillonnant le même dégradé et le même halo aux coordonnées globales, ce qui efface la couture visible entre les zones.

## Patch : Coins des cartes sans triangle sombre | 06/11/2026 — 09:10

Les coins des cartes reprennent le ciel de la fenetre : la carte repeint le fond parent (PaintSkyBand) avant son trace arrondi, ce qui supprime les triangles noirs sur les bords.

## Patch : Coins de la Toolbox sans triangle sombre | 06/11/2026 — 09:20

Le panneau Toolbox repeint le ciel de la fenetre (PaintSkyBand) avant son cadre arrondi : les coins ne montrent plus de fond sombre derriere les cartes.

## Patch : Coins des cartes nets (sans fond sombre) | 06/11/2026 — 09:35

La carte et le panneau Toolbox sont transparents et leur parent repeint le ciel (PaintSkyBand) : les coins arrondis affichent le degrade de la fenetre, plus aucun triangle sombre.

## Patch : Coins des boutons de la Toolbox | 06/11/2026 — 09:50

ModernButton passe en BackColor transparent (SupportsTransparentBackColor) : les coins arrondis laissent voir le fond du panneau au lieu de garder un carre sombre.

## Patch : Coins des boutons de la Toolbox | 06/11/2026 — 10:05

Les boutons ModernButton de la Toolbox sont transparents : les coins arrondis laissent voir la surface du panneau et plus aucun carre sombre.

## Patch : Transparence ToggleButton et ModernDropdownButton | 06/11/2026 — 10:20

ToggleButton et ModernDropdownButton passent en BackColor transparent (SupportsTransparentBackColor) comme ModernButton : les coins arrondis laissent voir le fond parent dans launcher et configure.

## Patch : Coins noirs des boutons corriges | 06/11/2026 — 10:40

ModernButton remplissait son fond avec Parent.BackColor ; quand le parent est transparent (FlowLayoutPanel de la Toolbox), GDI+ peignait du noir pur dans les coins arrondis. Le fond utilise desormais Ui.DarkCard si la couleur parent est transparente.

## Patch : Effet verre 3D sur les cartes | 06/11/2026 — 11:05

Les cartes Bento recoivent un effet verre : reflet diagonal en haut, liseré lumineux sur larête superieure et ombre interne en bas, pour donner du relief au survol et au repos.

## Patch : Effet verre translucide sur les cartes | 06/11/2026 — 11:25

La surface des cartes est desormais un voile translucide qui laisse voir le ciel de la fenetre, avec reflet en biseau, arete lumineuse, filet lateral, ombre interne et halo diffus : rendu verre 3D au lieu du degrade opaque.

## Patch : Nuage givre sur les cartes | 06/11/2026 — 11:40

La surface des cartes est desormais un nuage opaque au centre (gradient radial PathGradientBrush) qui se dissout vers les bords : le ciel reste visible sur les aretes, le centre est plus opaque et givre comme un verre depoli.

## Patch : Verre moderne epure | 06/11/2026 — 11:55

Suppression du nuage radial : surface translucide uniforme (degrade vertical froid), reflet blanc bleute en haut, liseré 1 px net sur l arete superieure et ombre douce en bas. Effet glass propre et moderne.

## Patch : Correctif ColorBlend cartes | 06/11/2026 — 12:05

Correction de l exception ColorBlend (derniere position a 1.0 au lieu de 0.75) qui faisait afficher les cartes en croix rouges. Rendu verre retabli.

## Patch : Relief des cartes (ombre portee + arete claire) | 06/11/2026 — 12:30

Ajout d une ombre portee exterieure (7-9 couches de degrades vers le bas) et d une arete claire interieure : les cartes se detachent du ciel. Halo ambre diffus autour de la carte principale et au survol.

## Patch : Profondeur premium des cartes | 06/11/2026 — 12:50

Ombre portee renforcee (11-14 couches, decalage 4 px), dome de lumiere froide en haut a gauche (PathGradientBrush) simulant un verre convexe, ombres internes laterales et arete claire interieure plus marquee.

## Patch : Liquid Glass sur les cartes | 06/11/2026 — 13:10

Corps translucide plus clair (le ciel traverse la carte), lentille speculaire PathGradientBrush en haut a gauche, bande de refraction bleu froid en bas, rim light sur tout le pourtour, eclat speculaire et ombre portee 14-18 couches.

## Patch : Correction barre blanche et contours epaissis | 06/11/2026 — 13:25

Suppression de l eclat speculaire rectangulaire (bande blanche), remplacement par un halo diffus fondu. Rim light porte a 2.2 px, bordure coloree a 2.6 px, contour gris-bleu a 2.4 px et arete interne sombre de 1.6 px pour un vrai relief 3D.

## Patch : Retour a un rendu classique des cartes | 06/11/2026 — 13:40

Suppression du style liquid glass (lentille, refraction, halo, rim light epais). Surface pleine #161C28 vers #121722, rayon 10, contour 1.2-1.4 px et ombre portee discrete de 6-8 couches.

## Patch : Fond image back.png | 06/11/2026 — 13:55

Suppression du degrade/halo de ciel orageux. back.png est embarquee en ressource (stage16_gui.ps1) et dessinee en fond de fenetre ; PaintSkyBand reutilise la meme image pour le header, le footer et les coins des cartes.

## Patch : Effet Liquid Glass sur les cartes | 06/11/2026 — 14:05

Cartes en verre translucide (fond visible a travers), lentille speculaire, bande de refraction froide, rim light et ombre de contact ; coins arrondis 14 px et liseré de verre froid sur les cartes secondaires.

## Patch : Liquid Glass plus opaque | 06/11/2026 — 14:20

Opacite du verre des cartes augmentee (0x5E/0x44 -> 0xB0/0x9A, hover 0xC4/0xAE) et lentille speculaire adoucie pour garder le texte lisible.

## Patch : Liquid Glass sur le fond de la Toolbox | 06/11/2026 — 14:40

Le panneau Toolbox OpenXR passe du cadre plein a un verre translucide : fond visible a travers, lentille speculaire, refraction froide, rim light et ombre de contact (helper PaintLiquidGlass dans LauncherForm), coins arrondis 14 px.

## Patch : Verre Liquid Glass plus opaque | 06/11/2026 — 16:35

Opacite du verre augmentee sur les cartes (0xD2 -> 0xBC, hover 0xE2 -> 0xD0) et sur le fond de la Toolbox (0xD2 -> 0xBC), lentille speculaire relevee (0x28, hover 0x38) pour garder le relief.

## Patch : Verre Liquid Glass encore plus opaque | 06/11/2026 — 16:50

Alpha du verre releve sur les cartes (0xEA -> 0xD8, hover 0xF4 -> 0xE6) et sur le fond de la Toolbox (0xEA -> 0xD8) ; le fond reste devine sous une surface presque opaque.

## Patch : Nouveau fond d'ecran backv2 | 06/11/2026 — 17:05

Le fond d'ecran embarque passe de `res/back.png` a `res/backv2.jpg` ; la ressource est toujours injectee sous le nom `back.png` au build et le rendu (fond, bandes header/footer, coins de carte) reste inchange.

## Patch : Effet Liquid Glass renforce | 06/11/2026 — 17:20

Verre plus liquide sur les cartes et la Toolbox : lentille speculaire relevee, frange de refraction froide sur l'arête droite et reflet diagonal oblique traversant le verre ; alpha du fond abaisse (cartes 0xD2/0xC0, survol 0xE0/0xD2, Toolbox 0xD2/0xC0) pour laisser transparaitre le fond.

## Patch : Suppression des effets de verre | 06/11/2026 — 17:35

Retrait complet du Liquid Glass et de l'ombre portée sur les cartes et la Toolbox (erreur ColorBlend) : retour à des surfaces Bento pleines (`Ui.DarkCard`, survol `0x26,0x2C,0x3C`) avec liseré froid.

## Patch : Header logo centre et badges a droite | 06/11/2026 — 17:50

Le logo du header est desormais centre horizontalement, les badges version et « Lanceur » aligns a droite du header.

## Patch : Logo header agrandi a 100px | 06/11/2026 — 18:05

Le logo centre du header passe de 76px a 100px de hauteur pour plus de presence visuelle.

## Patch : Header etendu a 100px | 06/11/2026 — 18:20

Le header passe a 100px de hauteur (logo entier visible, plus de crop) ; la Hero Card et la grille sont decalees de 28px vers le bas.

## Patch : Aura grisee au centre des cartes | 06/11/2026 — 18:35

Ajout d'un halo gris doux (PathGradientBrush centre 0x9A,0xA6,0xB4, alpha 0x18 / 0x22 au survol) centre sur chaque carte et s'eteignant vers les bords.

## Patch : Aura jaune/orangee cartes et Toolbox | 06/11/2026 — 18:50

Halo grise remplace par un halo chaud (0xF2,0x96,0x22, alpha 0x2A / 0x3A au survol) au centre des cartes, et meme aura appliquee a la Toolbox OpenXR.

## Patch : Boutons Toolbox sans rectangle de fond | 06/11/2026 — 19:05

La Toolbox expose PaintToolboxSurface ; le panneau devient transparent et les boutons redessinent carte + aura sous eux, supprimant les blocs rectangulaires gris autour des coins arrondis.

## Patch : Boutons Toolbox reellement transparents | 06/11/2026 — 19:20

PaintToolboxUnder redessine la surface de la Toolbox dans les coordonnees du bouton (reflection sur le form) : les coins arrondis laissent voir la carte et son aura, plus aucun bloc plein.

## Patch : Boutons Toolbox coins transparents | 06/11/2026 — 19:35

Le bouton lit ToolboxControl et PaintToolboxUnder par reflexion sur le formulaire hote (compatible avec les exes qui n'embarquent pas LauncherForm) : les coins arrondis laissent voir la carte et son aura.

## Patch : Methode de build documentee | 07/11/2026 — 05:45

`BUILD_SCRIPT.md` decrit desormais la methode de build validee : chaine 100 % locale sans git (miroir `src\AuraFlashXR-v0.1` depuis `SOURCE CODE APK AuraFlashXR`, `src\VDXR` requis), role de chaque dossier du projet, tableau des etapes setup/afxr_patch/vdxr/host/shim/mirrordesktop/probe/gui/package, points de vigilance (licenses.txt, xr_probe.exe, APK a jour) et commandes de deploiement vers `APP BUILD` et `RELEASE_BUILD`.

## Patch : Menus BACKGROUND et POLICE du launcher | 07/11/2026 — 06:10

Deux menus deroulants rejoignent la Toolbox, a la suite de CHARGER LE PRESET et avec le meme style de bouton (`ModernButton`). BACKGROUND liste les onze fonds de `docs\images\background\launcher` (embarques dans `launcher.exe` en ressources `bg\*`) plus ceux que l'utilisateur depose dans `docs\images\launcher` du package, avec une entree « Dossier des fonds… » ; le choix est memorise (`launcher.background`) et applique immediatement. POLICE propose sept familles embarquees (Orbitron, Montserrat, CAS Astro Voyager, Flymax, Savore, Yotsuba, Zirox Cyberpunk) avec un sous-menu de graisse (Normal, Semi-gras, Gras, Extra-gras) rendu dans sa propre police ; le choix (`launcher.font`, `launcher.font_weight`) reconstruit la fenetre. Les polices viennent de `docs\images\police`, sont copiees dans `tools\gui\res\fonts` et embarquees en ressources `fonts/*`.

## Patch : BACKGROUND et POLICE dans le footer, Zirox retiree, Arial ajoutee | 07/11/2026 — 06:25

Les boutons BACKGROUND et POLICE quittent la Toolbox et prennent place dans le footer, a la suite du selecteur de langue, avec le meme style que CHARGER LE PRESET (`ModernButton` 34 px, `ButtonWidth`). Le selecteur de langue reprend lui aussi ce style (plus de `Glass`, plus de chevron). La police Zirox Cyberpunk est retiree de la liste, de l'embarquement et de `tools\gui\res\fonts` (elle ne s'affichait pas) ; la police systeme Arial est ajoutee et resolue via `FontFamily` sans fichier embarque.

## Patch : Apercu des polices dans un menu sombre | 07/11/2026 — 06:35

Le menu POLICE utilise desormais `Ui.DarkMenuRenderer` (fond sombre, survol accent) au lieu du menu systeme clair : l'apercu de chaque famille et de chaque graisse, rendu dans sa propre police, reste lisible. Pour Arial (police systeme sans fichier embarque), `Ui.PreviewFont` applique la graisse via `FontStyle` (Regular / Gras) au lieu de la famille embarquee.

## Patch : Menu POLICE clair avec apercu | 07/11/2026 — 06:42

Le menu POLICE revient au menu systeme clair (le rendu sombre est retire). L'apercu reste actif : chaque famille s'affiche dans sa propre police et chaque graisse (Normal, Semi-gras, Gras, Extra-gras) dans son epaisseur reelle, y compris les quatre graisses Orbitron et Montserrat.

## Patch : Une collection de polices par fichier | 07/11/2026 — 06:55

Les graisses d'une meme famille partagent le meme nom GDI+ (Orbitron Regular et Bold s'appellent tous deux « Orbitron »), donc une collection unique ne distinguait pas les fichiers et l'apercu du menu POLICE retombait sur la meme graisse. Chaque fichier embarque est desormais charge dans sa propre `PrivateFontCollection` (`FamilyFromFile`, cache par ressource) : l'apercu affiche reellement Normal, Semi-gras, Gras et Extra-gras pour Orbitron et Montserrat, et la police de chaque famille pour les autres.

## Patch : Apercu des familles dans le menu POLICE | 07/11/2026 — 07:05

Le menu systeme ignore `Font` sur les entrees de premier niveau : les noms de familles s'affichaient donc tous dans la police par defaut, seul le sous-menu des graisses suivait. Un renderer dedie (`Ui.FontPreviewRenderer`) dessine chaque entree dont le `Tag` porte un nom de famille dans cette police ; le sous-menu garde son `Font` par graisse.

## Patch : Menu POLICE clair avec apercu des familles | 07/11/2026 — 07:12

Le renderer d'apercu reprend la palette systeme (`ToolStripProfessionalRenderer` par defaut) au lieu de la palette sombre : le menu retrouve son fond blanc et son survol bleu Windows, tout en dessinant chaque nom de famille dans sa propre police.

## Patch : Coins des boutons du footer | 07/11/2026 — 07:22

Les boutons du footer (langue, BACKGROUND, POLICE) sont poses sur un panneau transparent : leur fond de coin etait peint en `DarkCard`, d'ou le contour sombre visible autour des coins arrondis. `ModernButton` repeint desormais la bande de ciel du launcher derriere le bouton (methode `PaintSkyBand`, appelee par reflexion pour rester compilable dans les quatre exe), comme les cartes Bento.

## Patch : Retour du fond des boutons | 07/11/2026 — 07:30

Le repeint du ciel derriere `ModernButton` est retire : les boutons reprennent leur fond `DarkCard` d'origine (rendu precedent).

## Patch : Boutons du footer au style Aide / GitHub en rouge | 07/11/2026 — 07:40

Le selecteur de langue, BACKGROUND et POLICE reprennent le style des boutons Aide / GitHub (pilule arrondie, teinte d'accent, texte teinte, contour 1.3 px) via un helper dedie `FooterChip` en rouge (`E8442A`) ; `Link` n'est pas modifie, Aide et GitHub gardent leur ambre.

## Patch : Coins des boutons du footer sur le ciel | 07/11/2026 — 07:50

`ModernButton` peint le fond du ciel du launcher derriere lui quand son parent est transparent (footer) au lieu de `DarkCard` : les coins arrondis du selecteur de langue, de BACKGROUND et de POLICE ne montrent plus de contour sombre. La methode `PaintSkyBand` du launcher est appelee par reflexion pour rester compilable dans les quatre exe.

## Patch : Retour du fond des boutons du footer | 07/11/2026 — 07:58

Le repeint du ciel derriere `ModernButton` est retire (rendu precedent) ; les boutons du footer gardent leur style rouge et leur fond `DarkCard`.

## Patch : Header du streamer | 07/11/2026 — 08:10

Le header compact (`Ui.CompactHeader`, utilise par le streamer, la configuration et les performances) n'affiche plus « AuraFlashXR · Pre-Release » : il montre le logo agrandi de 50 % (248x96 au lieu de 165x64, header agrandi d'autant pour ne plus rogner le logo), la pastille neon PRE-RELEASE (nouveau `Ui.BadgeLabel`, meme halo et meme contour degrade que le header du launcher) puis le titre « STREAMER · V0 ». `Streamer.HeaderSubtitle` passe a STREAMER (FR/EN).

## Patch : Titre du streamer dans une pastille | 07/11/2026 — 08:18

Le titre du header compact (« STREAMER · V0 », « CONFIGURATION · V0 »...) est dessine dans la meme pastille neon que PRE-RELEASE (`Ui.BadgeLabel`, texte orange) au lieu du titre en degrade.

## Patch : Header du streamer aligne sur le launcher | 07/11/2026 — 08:35

`Ui.CompactHeader(form, subtitle)` reprend la mise en page du header du launcher : logo a sa taille naturelle a gauche (jamais rogne), nom AuraFlashXR en degrade jaune → orange, pastilles PRE-RELEASE et « STREAMER · V0 » a droite, boutons reduire/fermer dessines (`CaptionGlyph`) et barre de titre deplacable. Le streamer et l'assistant utilisent la nouvelle signature.

## Patch : Maquette 2026 du streamer | 07/11/2026 — 09:30

Application du patch `Streamer.cs.maquette-2026.patch` : titre de fenetre « AuraFlashXR », curseurs du Post-Processing sur deux colonnes (`AddKnob(t, row, ...)` avec numeros de ligne), tuile « Pertes » en vert (`5EB271`) quand aucune perte. Le badge PRE-RELEASE ajoute par le patch a ete retire : le header `Ui.CompactHeader` le dessine deja.

## Patch : Grille du statut et bouton du casque | 08/11/2026 — 00:05

Les bulles casque / jeu / GPU / encodeur remplissent exactement leur case du `topBento` (`Dock = Fill` + `RowStyle AutoSize`), la bulle casque et le bouton partagent la meme ligne. Le bouton s'appelle « Trouver mon casque » (FR) / « Find my headset » (EN).

## Patch : Statut du streamer sur deux lignes | 08/11/2026 — 00:20

Le `topBento` passe a deux lignes : a gauche Casque (avec le bouton « Trouver mon casque » dans la meme case) puis Jeu ; a droite GPU puis Encodeur. Chaque colonne est un `TableLayoutPanel` interne, les bulles remplissent leur case.

## Patch : Bouton casque dans la bulle | 08/11/2026 — 00:30

Le bouton « Trouver mon casque » est place dans la bulle Casque elle-meme (a droite du texte) : la bulle s'elargit avec lui au lieu d'occuper une case separee.

## Patch : Bouton casque plus fin | 08/11/2026 — 00:40

Le bouton « Trouver mon casque » de la bulle casque est reduit de 20 % en hauteur et de 10 % en largeur par rapport au bouton standard (22 px de haut, largeur mesuree du texte a 90 % + 14 px).

## Patch : GPU et encodeur dans la largeur de la fenetre | 08/11/2026 — 00:50

Nouvelle propriete `Ui.StatusBubble.TextMaxWidth` : le texte des bulles GPU et Encodeur revient a la ligne dans leur colonne (`52 %` de la largeur cible) au lieu de deborder de la fenetre.

## Patch : Badge CONFIG CHARGE a la suite du texte | 08/11/2026 — 01:00

Le badge de `Ui.StatusBubble` est place dans un `FlowLayoutPanel` (`badgeRow`) apres le texte : il suit la fin du texte encodeur, y compris quand celui-ci revient a la ligne, au lieu de flotter a droite de la bulle.

## Patch : Tableau de statut unifie du streamer | 08/11/2026 — 01:15

Le bloc de statut (Casque, Jeu, GPU, Encodeur) n'affiche plus quatre pilules : un tableau pleine largeur (fond `111620`, bordure `222A38`, trois lignes de 32 px separees par des filets fins) montre l'etat du casque avec le bouton « Trouver mon casque » a droite, puis Jeu | GPU en deux colonnes egales, puis l'encodeur (ellipse si trop long) avec le badge CONFIG CHARGE a droite. Les bulles `headset`, `game`, `runtime`, `encInfo` restent actives : `StatusText` recopie leur texte et leur couleur, et le badge est emprunte a `encInfo` (`BadgeControl`).

## Patch : Retour de la grille de statut en bulles | 08/11/2026 — 01:25

Le tableau unifie est retire : le bloc de statut revient aux bulles en deux colonnes (Casque avec le bouton a l'interieur, Jeu sous Casque, GPU puis Encodeur a droite), avec le retour a la ligne du GPU et de l'encodeur.

## Patch : Bloc de statut rectangulaire | 08/11/2026 — 01:40

Les quatre bulles du haut sont remplacees par un bloc rectangulaire unique (fond `111620`, bordure `222A38`, trois lignes separees par des filets fins, trait vertical au milieu de la deuxieme). Ligne 1 : texte du casque a gauche, bouton « Trouver mon casque » plein orange (`FF9800`, texte fonce) a droite ; ligne 2 : Jeu | GPU ; ligne 3 : encodeur sur une ligne + badge CONFIG CHARGE a droite. Les bulles restent actives et alimentent les textes (`StatusText`).

## Patch : Encodeur en initialisation | 08/11/2026 — 01:50

Tant que le host n'a pas publie d'encodeur, la ligne 3 du bloc de statut affiche « Encodeur : initialisation... » (les textes `Streamer.EncoderActive` et `Streamer.EncoderActiveFormat` sont remplaces) au lieu d'un libelle vide ou d'un ancien encodeur.

## Patch : Bloc de statut unique | 08/11/2026 — 01:10

Les quatre bulles du haut deviennent un seul bloc sombre (`#111620`, bordure `#222A38`, coins 10 px) : ligne 1 casque + bouton orange plein « Trouver mon casque » (coins 6 px, texte noir), ligne 2 jeu | GPU separee par un trait vertical, ligne 3 encodeur + badge du preset. `Ui.StatusBubble.Flat` retire pastille et contour individuels, `TextColor` force le texte blanc ; les separateurs sont peints par le bloc.

## Patch : Bloc statut du streamer en carte 3 lignes | 08/11/2026 — 01:10

Le bloc du haut devient un conteneur sombre `#111620` (rayon 10 px, bordure `#222A38`) en trois lignes separees par des traits : casque a gauche et bouton « Trouver mon casque » (fond orange, texte fonce, coins 6 px) a droite ; jeu | GPU separes par un trait vertical ; encodeur a gauche et badge du preset a droite. Les couleurs des textes ne changent pas.

## Patch : Bloc de statut unifie du streamer | 08/11/2026 — 01:10

Les quatre pilules de statut sont remplacees par un panneau unique pleine largeur (fond `#111620`, bordure `#222A38`, 105 px) a trois lignes separees par des filets fins : ligne 1 casque + bouton « Trouver mon casque » a droite, ligne 2 jeu | GPU (trait vertical au milieu), ligne 3 encodeur (ellipse si trop long) + badge CONFIG CHARGEE encadre a droite. Le texte passe en `Label` (couleur d'etat via `ForeColor`), plus de pastilles rondes.

## Patch : Encodeur affiche dans le streamer | 08/11/2026 — 01:25

La cle `Streamer.EncoderActiveFormat` de `tools\language\french.xml` et `english.xml` avait ete ecrasee par « Encodeur : initialisation... » (sans le format `{0}…{3}`), si bien que la ligne encodeur restait bloquee sur l'initialisation. Les quatre fichiers de langue reprennent le format complet.

## Patch : GPU et casque echanges | 08/11/2026 — 01:35

Ligne 1 du bloc de statut : le GPU remplace le casque (le bouton « Trouver mon casque » reste a droite) ; ligne 2 : jeu a gauche, casque a droite.

## Patch : Couleur GPU, bouton casque et badge preset | 08/11/2026 — 01:45

Le texte GPU passe en vert NVIDIA (`76B900`). Le bouton « Trouver mon casque » rejoint la case du casque (ligne 2, a sa droite) et quitte la ligne GPU. Le badge CONFIG CHARGEE passe sous la ligne encodeur (l'encodeur occupe toute la largeur de la ligne 3).

## Patch : Retour de l'agencement du bloc statut | 08/11/2026 — 01:55

Les deplacements du bouton « Trouver mon casque » et du badge CONFIG CHARGEE sont annules (l'agencement precedent revient) ; seule la couleur verte NVIDIA du texte GPU (`76B900`) est conservee.

## Patch : Bouton casque dans sa case | 08/11/2026 — 02:05

Le bouton « Trouver mon casque » est place dans la case du casque (ligne 2, a droite du texte), la ligne GPU ne garde que le texte GPU.

## Patch : Retour du bouton sur la ligne GPU | 08/11/2026 — 02:12

Le deplacement du bouton « Trouver mon casque » est annule : il revient a droite de la ligne GPU, la ligne casque garde son texte seul.

## Patch : Bouton casque a droite du texte | 08/11/2026 — 02:22

Le bouton « Trouver mon casque » est place a droite du texte « Casque : ... » (ligne 2), le GPU occupe seul toute la ligne 1.

## Patch : Hauteur des cases du bloc statut | 08/11/2026 — 02:30

La case casque (texte + bouton) n'avait pas de `RowStyle` : elle prenait toute la hauteur de la ligne 2 et poussait le texte hors de la case. `RowStyle Percent 100` ajoute sur la case et sur la ligne 2.

## Patch : Oeil sous l'encodeur | 08/11/2026 — 02:40

`Streamer.EncoderActiveFormat` place « Oeil : ... » sur une deuxieme ligne (retour a la ligne dans la chaine, FR/EN), la ligne encodeur garde la resolution et les Hz en dessous.

## Patch : Ligne encodeur compacte et badge colle | 08/11/2026 — 02:52

« Oeil : ... » revient a la suite de l'encodeur. La ligne n'affiche que les infos principales : codec/profil (premier segment de `encoder_desc`, sans les options P1/CABAC/slices), resolution oeil, resolution encodee et frequence. Le badge CONFIG CHARGEE est colle a droite de la case, centre verticalement.

## Patch : Texte complet de l'encodeur | 08/11/2026 — 03:00

La troncature de `encoder_desc` est retiree : la ligne encodeur affiche de nouveau toutes les informations du host.

## Patch : Multi-pass retire et police unifiee | 08/11/2026 — 03:12

L'information « multi-pass ... » est retiree du texte de l'encodeur (regex sur `encoder_desc`). Les quatre lignes du bloc de statut (GPU, jeu, casque, encodeur) utilisent la meme police (`Segoe UI` 9.5 pt).

## Patch : Police du bloc statut agrandie | 08/11/2026 — 03:20

La police des quatre lignes du bloc de statut passe de 9.5 pt a 19.5 pt (+10 px).

## Patch : Police du bloc statut ajustee | 08/11/2026 — 03:26

La police des quatre lignes du bloc de statut passe de 19.5 pt a 14.5 pt (9.5 pt + 5 px).

## Patch : Police du bloc statut a 12 px | 08/11/2026 — 03:32

Les quatre lignes du bloc de statut utilisent une police de 12 px (`Segoe UI` 9 pt).

## Patch : Police du bloc statut a 12 pt | 08/11/2026 — 03:38

Les quatre lignes du bloc de statut utilisent `Segoe UI` 12 pt.

## Patch : Ligne Resolution dans le bloc statut | 08/11/2026 — 03:55

Le bloc de statut passe a 4 lignes (132 px) : une ligne « Resolution » affiche la resolution de l'oeil, la taille de l'image encodee, l'etat de la foveation et le pourcentage de pixels encodes. Le host publie `foveated` et `fov_geom` dans son evenement `status`.

## Patch : Panneau Information en direct | 08/11/2026 — 04:15

Le bloc de statut devient un panneau `ModernGroupBox` titré « INFORMATION EN DIRECT » (meme cadre que « Diffusion en direct »). L'encodeur n'affiche plus les resolutions (codec + Hz). La ligne « Resolution actif » passe en 12 pt jaune, les quatre lignes ont une ombre portee de 1 px, et les separateurs `·` deviennent `|`. Le host publie `foveated`/`fov_geom` et est recompile.

## Patch : Lueur blanche sur le texte du bloc statut | 08/11/2026 — 04:25

L'ombre portee est remplacee par une lueur exterieure blanche de 1 px (quatre passes decalees) autour du texte des quatre lignes.

## Patch : Texte 13 pt sans lueur et cadre resserre | 08/11/2026 — 04:35

La lueur blanche est retiree, les quatre lignes passent en 13 pt. La zone de statut dans le panneau « INFORMATION EN DIRECT » passe de 132 px a 112 px pour reduire le vide.

## Patch : Cadre du panneau Information en direct | 08/11/2026 — 04:45

La zone de statut repasse a 135 px. Le panneau « INFORMATION EN DIRECT » reduit sa marge interieure haute (26 px au lieu de 28) et basse (6 px au lieu de 10), soit environ 5 % de hauteur en moins.

## Patch : Texte semi-gras et separateurs de l'encodeur | 08/11/2026 — 04:52

Les quatre lignes du panneau « INFORMATION EN DIRECT » passent en semi-gras (13 pt Bold). Les virgules de `encoder_desc` sont remplacees par des separateurs `|`.

## Patch : Panneau Diffusion en direct et cases FPS | 08/11/2026 — 05:05

Majuscule au premier mot de chaque segment du bloc statut (apres « : » et « | »). Le panneau « Diffusion en direct » passe en MAJUSCULES. Les cases `Tile` affichent leur contenu centre et en 13 pt, et s'appellent FPS JEU, FPS DIFFUSE, FPS CASQUE (FR) / GAME FPS, STREAMED FPS, HEADSET FPS (EN).

## Patch : Titres en majuscules et panneaux harmonises | 08/11/2026 — 05:20

Tous les panneaux du streamer affichent leur titre en MAJUSCULES et leur contenu en 13 pt. Le panneau « Position & Espace » devient « POSITION/ESPACE & BUREAU VIRTUEL ». La ligne de presets du Post-Processing passe a la ligne (`WrapContents`) au lieu de sortir du panneau.

## Patch : XML de langue repare | 08/11/2026 — 05:30

Le titre « POSITION/ESPACE & BUREAU VIRTUEL » contenait un `&` non echappe : les quatre fichiers de langue n'etaient plus du XML valide, donc plus aucune traduction n'etait chargee. Remplace par `&amp;` dans `french.xml`, `english.xml` et `tools\language\`.

## Patch : Lunettes et profils du Post-Processing | 08/11/2026 — 05:40

Le libelle devient « Lunettes : » (FR) / « Glasses: » (EN), sans la mention (Sun Glasses). Les boutons de profils passent a la ligne apres « Cinema » : « Retablir par defaut » s'aligne sous « Naturel ».

## Patch : Alignement des profils du Post-Processing | 08/11/2026 — 05:48

Le retour a la ligne se fait apres « Competitif » : « Cinema » s'aligne sous « Naturel » et « Retablir par defaut » le suit sur la meme ligne.

## Patch : Profils alignes sous le libelle | 08/11/2026 — 05:58

Le retour a la ligne se fait sur le libelle « Profils : » : « Naturel » et « Cinema » demarrent a la meme abscisse, « Retablir par defaut » suit « Cinema ».

## Patch : Libelle Profils retire | 08/11/2026 — 06:05

Le libelle « Profils : » est retire du panneau Post-Processing ; les boutons commencent la ligne et « Cinema » s'aligne sous « Naturel ».

## Patch : Streamer, pastilles et logos | 08/11/2026 — 06:25

« Cinema » suit « Competitif » sur la meme ligne et le libelle devient « Competitif » (sans Net+). Les pastilles « Lunettes » (Off/Light/Dark/TruNite) reprennent le style des boutons de profils avec une teinte noir leger (`3A4458`). Les nouveaux visuels de `tools\gui\res` sont embarques : le streamer utilise `Logo-allonger.png` (repli `logo_640.png`), `emblem.png` et `backv2.jpg` (restaure) restent embarques.

## Patch : Fonds transparents, header et panneau Audio | 08/11/2026 — 06:45

`ToggleSwitch` et `Segmented` passent en fond transparent : les boutons Lunettes, Hauteur des yeux et Generation d'images n'ont plus de rectangle de fond. Le titre « AuraFlashXR » du header est retire, le logo prend 20 % de plus. Le titre « Generation d'images » est en majuscules et centre. L'onglet Audio est traduit (sans emoji).

## Patch : Streamer ne demarre plus (fond transparent) | 08/11/2026 — 06:58

`ToggleSwitch` posait `BackColor = Transparent` avant `SupportsTransparentBackColor`, ce qui levait `ArgumentException: Le controle ne prend pas en charge les couleurs d'arriere-plan transparentes` au demarrage du streamer. L'ordre est corrige (`SetStyle` d'abord).

## Patch : Pastilles sans fond, titre centre et logo | 08/11/2026 — 07:20

Les pastilles (`Segmented`) peignent le fond du panneau avant le dessin : plus de rectangle derriere Lunettes et Hauteur des yeux. « GENERATION D'IMAGES » est centre horizontalement avec son interrupteur. Le logo du header prend 20 % de plus (header 64 px). L'onglet Audio est traduit.

## Patch : Hauteur du panneau Generation d'images | 08/11/2026 — 07:35

La marge interieure du bloc « GENERATION D'IMAGES » passe de 8 px a 2 px en haut et en bas : sa hauteur est divisee par deux.

## Patch : Marges des panneaux du streamer | 08/11/2026 — 10:25

Les panneaux des colonnes reduisent leur marge basse (8 -> 4 px) et leur marge interieure (haute 28 -> 24, basse 10 -> 4) ; le bloc « GENERATION D'IMAGES » passe a 4 px de marge basse. La fenetre gagne environ 20 px de hauteur.

## Patch : Valeurs des curseurs visibles | 08/11/2026 — 10:40

Les curseurs du Post-Processing passent de 140 a 120 px, la valeur a une largeur fixe de 42 px (marge droite 4 au lieu de 10) et l'ecart entre les deux colonnes tombe de 20 a 6 px : les valeurs ne sont plus coupees a droite.

## Patch : Hauteur du bloc Generation d'images | 08/11/2026 — 10:55

Le bloc « GENERATION D'IMAGES » s'etirait a la hauteur de la colonne de droite (la ligne du `leftColumn` n'avait pas de `RowStyle`). La ligne passe en `AutoSize`, le bloc en `AutoSizeMode.GrowAndShrink` / `Dock = Top` : sa hauteur suit son contenu.

## Patch : Audio traduit et bloc Generation d'images au style Toolbox | 08/11/2026 — 11:10

Les messages du routage audio (cable a 100 %, sortie par defaut, cable introuvable...) passent par `Lang.Get` (cles `Audio.*`, FR/EN) au lieu de textes anglais en dur. Le bloc « GENERATION D'IMAGES » reprend le cadre `ModernGroupBox` de la TOOLBOX AFXR (titre en majuscules, meme bordure) au lieu de son rectangle dessine.

## Patch : Contour orange et hauteurs des panneaux | 08/11/2026 — 11:30

Tous les panneaux (`ModernGroupBox`) ont un contour orange de 2 px. Le bloc « GENERATION D'IMAGES » affiche « Actif : » avant son interrupteur, et les deux colonnes partagent leurs hauteurs de ligne : Generation d'images = Toolbox, Position/Espace = Post-Processing.

## Patch : Contour degrade et panneaux de meme hauteur | 08/11/2026 — 11:50

Le contour des panneaux passe a 1 px en degrade orange -> rouge. La mise en page devient deux rangees de deux panneaux : chaque rangee prend la hauteur de son panneau le plus haut, donc Generation d'images et Toolbox ont la meme hauteur, Position/Espace et Post-Processing aussi (les rangees ne sont pas liees). La ligne GPU n'affiche plus le pilote entre parentheses.

## Patch : Pilote GPU et hauteur copiee | 08/11/2026 — 12:05

La ligne GPU retire le nom de la puce graphique (Blackwell) et garde le numero de pilote. Le bloc « GENERATION D'IMAGES » reprend la hauteur de contenu de la TOOLBOX AFXR (74 px) en dur, sans lien entre les panneaux.

## Patch : Hauteur Toolbox et Generation d'images a 60 px | 08/11/2026 — 12:15

La hauteur de contenu de la TOOLBOX AFXR et du bloc « GENERATION D'IMAGES » passe de 74 a 60 px (valeurs copiees, panneaux independants).

## Patch : Hauteur reelle de la Toolbox | 08/11/2026 — 12:25

Le `MinimumSize` ne changeait rien : la hauteur venait des boutons (32 px x 2 lignes). Les boutons de la TOOLBOX AFXR passent a 24 px (marges 2 px) et le bloc « GENERATION D'IMAGES » est plafonne a 60 px (`MaximumSize`).

## Patch : Lignes centrees et badge CONFIG CHARGEE | 08/11/2026 — 12:45

Les textes des quatre lignes du panneau « INFORMATION EN DIRECT » sont centres horizontalement dans leur case. Le badge CONFIG CHARGEE reprend le style du bouton « AuraFlashXR Active » de la TOOLBOX (pilule arrondie, teinte accent, texte blanc, 24 px) — style copie, aucun lien entre les deux controles.

## Patch : Bloc statut centre verticalement | 08/11/2026 — 12:58

Le bloc de statut remplissait le panneau en haut (`Dock = Top`, 135 px) avec la marge basse du panneau en trop : il passe en `Dock = Fill`, ses quatre lignes occupent toute la hauteur et sont donc centrees verticalement. « Actif » est retire des lignes Encodeur et Resolution.

## Patch : Bloc de statut visible dans son panneau | 08/11/2026 — 13:10

Depuis le passage en `Dock = Fill`, le bloc de statut recouvrait le titre et la bordure du panneau (ajoute avant ses controles, il passait dessous puis masquait tout). Il est place a la zone utile du panneau (`Padding`), ancre sur les quatre cotes et amene au premier plan.

## Patch : Panneau Information en direct revenu a sa taille | 08/11/2026 — 13:20

Le repositionnement manuel du bloc dans le panneau l'a fait grandir jusqu'a remplir la fenetre. Retour au montage precedent : panneau en `Dock = Top` avec `AutoSize`, bloc de statut a hauteur fixe (132 px) dans sa zone utile.

## Patch : Marges du panneau Information en direct | 08/11/2026 — 13:30

La marge interieure basse du panneau passe de 6 a 26 px, egale a la marge haute : l'espace au-dessus de la premiere ligne est le meme qu'en dessous de la derniere.

## Patch : Marges du panneau a 15 px | 08/11/2026 — 13:38

Les marges interieures haute et basse du panneau « INFORMATION EN DIRECT » passent de 26 a 15 px.

## Patch : Marge basse du panneau a 12 px | 08/11/2026 — 13:45

La marge interieure basse du panneau « INFORMATION EN DIRECT » passe de 15 a 12 px (haute inchangee a 15).

## Patch : Finalisation du launcher | 08/11/2026 — 14:00

Le fond par defaut devient `BACK-UNI-BLACK.jpg` (embarque comme `back.png`). Les boutons du footer (langue, BACKGROUND, POLICE) peignent le ciel du launcher derriere eux : plus de rectangle sombre. La fenetre perd 10 % de hauteur, les badges PRE-RELEASE / LANCEUR V0 glissent de 20 % vers la droite, la Toolbox reduit sa hauteur de 10 % et la pastille DEMARRER se decale de 10 % vers la gauche.

## Patch : Positions et transparence du footer | 08/11/2026 — 14:15

Les badges PRE-RELEASE / LANCEUR V0 reviennent de 5 % vers la gauche, la pastille DEMARRER repart de 5 % vers la droite. Les pastilles du footer (langue, BACKGROUND, POLICE, Aide, GitHub) peignent le ciel du launcher avant leur contour : plus de rectangle de fond.

## Patch : Fond du footer reellement transparent | 08/11/2026 — 14:30

`PaintSkyBand` posait son clip AVANT la translation : la bande demandee etait interpretee dans les coordonnees de la fenetre, donc le ciel n'etait jamais peint et les pastilles gardaient leur rectangle. Le clip est applique apres la translation. La pastille DEMARRER repart de 5 % vers la droite.

## Patch : Pastilles du footer sur le ciel | 08/11/2026 — 14:45

Deux causes au rectangle de fond : `ModernButton` affectait `BackColor = Transparent` avant `SupportsTransparentBackColor` (l'affectation etait ignoree) et peignait `DarkCard` quand son parent etait transparent. L'ordre est corrige et le bouton peint desormais le ciel du launcher derriere lui (methode `PaintSkyBand` appelee par reflexion pour rester compilable dans les quatre exe).

## Patch : Retour du fond des boutons du footer | 08/11/2026 — 14:55

Le repeint du ciel dans `ModernButton` et dans les pastilles du footer est retire (rendu precedent) ; le fond du parent est de nouveau utilise.

## Patch : Pastilles du footer autonomes et header sans doublon | 08/11/2026 — 15:10

Le header et le footer n'etaient plus peints deux fois : l'image du fond se dupliquait dans la bande du header (redessin inutile retire). Les pastilles du footer (langue, BACKGROUND, POLICE, Aide, GitHub) utilisent un bouton autonome `PillButton` qui ne peint aucun fond (`OnPaintBackground` vide) : seul le contour et le texte sont dessines, le ciel de la fenetre reste visible. Aucun code partage avec les autres boutons.

## Patch : Pastilles du footer au style PRE-RELEASE | 08/11/2026 — 12:01

Les boutons langue, BACKGROUND, POLICE, Aide et GitHub du footer reprennent a l'identique le rendu de la pastille PRE-RELEASE du header : lueur orange exterieure, fond bleu nuit translucide, contour degrade jaune vers orange et texte orange. Le style est copie dans une classe autonome, sans aucun lien de code avec le header.

## Patch : Boutons du footer refaits a plat | 08/11/2026 — 12:20

Les pastilles dessinees du footer sont supprimees : langue, BACKGROUND, POLICE, Aide et GitHub utilisent de nouveau des boutons plats basiques (rectangle opaque bleu nuit, bordure orange 1 px, texte orange), sans aucun dessin custom ni fond transparent. La classe de pastille et ses helpers sont retires du code.

## Patch : Coins arrondis des boutons du footer | 08/11/2026 — 12:35

Les boutons du footer dessinent eux-memes leurs coins arrondis (rayon 10 px) : le fond de la fenetre est re-echantillonne aux coordonnees globales avant le remplissage, donc plus aucun rectangle noir dans les angles. Aucun `Region` n'est utilise (pas de crenelage) ; survol et clic changent la teinte de remplissage.

## Patch : Couleurs des boutons du footer | 08/11/2026 — 12:45

Chaque bouton du footer a sa propre teinte de bordure et de texte : Aide et GitHub en jaune, selecteur de langue en rouge, BACKGROUND en violet et POLICE en bleu. La fabrique `Flat` accepte desormais une teinte et une couleur de texte en parametres, le rendu arrondi reste identique.
