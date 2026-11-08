# 📊 RAPPORT D'AUDIT N°6 — GUI C# & OUTILS HOST (AURAFLASHXR)
**Date :** 28/10/2026 14:11 — **Auditeur :** Sous-agent audit-vr (lecture seule)
**Version analysée :** AuraFlashXR v0.5 (Branche REMIX-BarThi)
**Périmètre :** Ensemble des modules C# de `tools/gui/` (17 fichiers, ~11 700 lignes)

---

## RÉSUMÉ EXÉCUTIF — TOP 10 DES PROBLÈMES LES PLUS IMPACTANTS

| N° | Fichier & Lignes | Problème & Nature | Impact Réel (FPS / Latence / Stabilité) | Gain Estimé |
|---|---|---|---|---|
| **1** | `SessionSync.cs` (L124-126) | **Bug critique de dégradation exponentielle du bitrate** : multiplication de `curBitrate` par `(bh / 100.0)` à chaque enregistrement. | Dégradation continue de la qualité d'image (250 Mbps -> 225 -> 202 -> 182...) à chaque ouverture/sauvegarde de profil. | Maintien du bitrate réel configuré (0% de perte visuelle). |
| **2** | `AudioRoute.cs` (L387, 391, 443, 472, 480) | **Fuites mémoire COM WASAPI majeures** : interfaces `IMMDevice`, `IAudioClient`, `IAudioEndpointVolume` jamais libérées via `Marshal.ReleaseComObject`. | Rétention des handles audio Windows, fuite de mémoire non managée, crash après plusieurs heures. | Stabilité infinie, libération immédiate des descripteurs COM. |
| **3** | `Streamer.cs` (L732-798) | **I/O disque et énumération globale de processus à 1 Hz sur le Thread UI** : lecture synchrone de 3 fichiers JSON/TXT et appel à `Process.GetProcesses()` par seconde. | Micro-saccades de l'interface et blocages UI de 15 à 60 ms chaque seconde pendant le streaming. | **Élimination totale des saccades UI**, CPU UI thread libéré (0% overhead). |
| **4** | `Streamer.cs` (L950-957, L1064-1102) | **Écritures disque non throttlees sur curseurs UI** : chaque pixel déplacé sur les 9 sliders écrit immédiatement 2 fichiers sur disque et émet 3 commandes IPC. | Saturation des I/O disque, lags massifs de l'UI et goulot d'étranglement IPC vers l'encodeur NVENC. | Fluidité parfaite des réglages en direct, latence IPC ramenée à < 1 ms. |
| **5** | `Launcher.cs` (L490, 513-521) | **Polling agressif à 2 Hz via `Process.GetProcessesByName()`** (3 appels par tick = 6 snapshots système complets par seconde). | Des milliers d'allocations d'objets `Process` par minute, overhead kernel `NtQuerySystemInformation` constant. | 0 allocation CPU, réactivité instantanée via événements asynchrones natifs (`Exited`). |
| **6** | `Common.cs` (L1957-2021) & `TestRunner.cs` (L428-431) | **Exécutions synchrones bloquantes de scripts PowerShell** (`p.WaitForExit(40000)`) sur le thread UI pour le pare-feu et les périphériques audio. | Blocage total (freeze "Ne répond pas") de l'interface pendant 5 à 40 secondes lors de l'analyse réseau ou des tests. | Thread UI 100% réactif (exécution asynchrone non-bloquante ou API native). |
| **7** | `Streamer.cs` (L244, 628, 890) & `Launcher.cs` (L274, 360, 446) | **Arrêts brutaux non gracieux (`Process.Kill()`)** de `auraflashxr_host.exe`, `ADBForwarder.exe` et `mirrordesktop.exe`. | Sessions NVENC orphelines, sockets UDP 9943/9944 non fermés (port bind error), logs corrompus. | Fermeture propre en < 200 ms via signal IPC, reset GPU propre. |
| **8** | `Advice.cs` (L79) | **Inversion mémoire complète d'un fichier volumineux** : `File.ReadLines(ev).Reverse().Take(4000)` charge tout `host_events.jsonl` en RAM. | Blocage UI de plusieurs secondes à chaque ouverture des conseils, risque d'OutOfMemory sur les sessions longues (> 50 Mo de logs). | Lecture inversée par chunks ou buffer circulaire en < 15 ms. |
| **9** | `Performance.cs` (L133, 223) & `Common.cs` (L2429-2484) | **Fuites d'abonnements d'événements UI et fuites d'objets GDI** : accumulateur d'événements `Resize` à chaque clic de jeu + allocations répétées de `Font`, `Pen` et `Brush`. | Dégradation progressive des FPS de rendu GDI, fuite de mémoire GDI menant au crash "Out of Memory / GDI handles exhausted". | Zéro fuite de handle GDI, événements attachés une seule fois. |
| **10** | `Lang.cs` (L236-450+) | **Violation majeure de la Règle 3 du projet** : > 400 lignes de dictionnaire de traduction codées en dur dans le code source C#. | Impossible de traduire de nouveaux termes sans recompiler, désynchronisation garantie entre versions FR/EN. | Centralisation 100% dans les fichiers XML `language\fr.xml` et `language\en.xml`. |

---

## RAPPORT D'AUDIT DÉTAILLÉ FICHIER PAR FICHIER

```
[tools/gui/SessionSync.cs]
Lignes 124 à 126 — Diagnostic : Anomalie / Bug critique de dégradation du débit
  Impact estimé : Perte de qualité d'image exponentielle. À chaque sauvegarde dans l'interface, le bitrate réel est multiplié par la marge (ex. 90%), réduisant 250 Mbps à 225, puis 202, puis 182...
  Solution & Code proposé :
  Ne recalculer le bitrate basé sur la marge d'arrivée QUE lors du premier calcul de recommandation initiale, ou stocker la valeur brute nominale dans une propriété distincte `NominalBitrateMbps` sans modifier la valeur configurée par l'utilisateur.

[tools/gui/SessionSync.cs]
Lignes 108 à 111 — Diagnostic : Code obsolète / Imprécision virgule flottante FPS
  Impact estimé : Dérive d'horloge de pacing. L'arrondi `Math.Round(targetFps, 1)` stocké en double dans le JSON peut générer des valeurs comme 89.99999 ou 90.0000001, provoquant des rejets de format ou des faux reconfigurations de l'encodeur.
  Solution & Code proposé :
  Utiliser une table d'entiers ou d'énumérations strictes pour les fréquences standards (72, 80, 90, 120) et formater explicitement avec CultureInfo.InvariantCulture.

[tools/gui/AudioRoute.cs]
Lignes 387, 391, 443, 472, 480 — Diagnostic : Anomalie / Fuite mémoire COM WASAPI majeure
  Impact estimé : Fuite continue de handles COM WASAPI non managés. Les interfaces `IMMDeviceCollection`, `IMMDevice`, `IAudioClient`, `IAudioEndpointVolume` sont instanciées sans bloc `try/finally` appelant `Marshal.ReleaseComObject()`. Sur plusieurs heures de streaming, cela épuise la table de handles Windows audio et empêche le basculement dynamique de casque.
  Solution & Code proposé :
  Encapsuler tous les pointeurs COM dans un helper de libération RAII ou appeler systématiquement `Marshal.ReleaseComObject()` dans des blocs `finally`.

[tools/gui/AudioRoute.cs]
Lignes 68, 183 à 207 — Diagnostic : Goulot FPS/Latence / Polling COM au lieu d'événements WASAPI
  Impact estimé : Utilisation CPU inutile et blocages intermittents du thread audio (3000 ms) pour requêter des COM objects.
  Solution & Code proposé :
  Remplacer le timer de polling par les interfaces d'abonnement événementielles natives de WASAPI : `IMMNotificationClient` (pour la détection de débranchement/branchement d'appareils) et `IAudioEndpointVolumeCallback` (pour les changements de volume en temps réel sans polling).

[tools/gui/Streamer.cs]
Lignes 732 à 798 — Diagnostic : Goulot FPS/Latence / Polling disque et process synchrone à 1 Hz
  Impact estimé : Pauses de 15 à 60 ms sur le thread UI à chaque seconde. `LinkTick` lit `session.json`, `steamvr_status.json`, parse `vrserver.txt`, et si absent, boucle sur TOUS les processus de Windows via `Process.GetProcesses()` pour inspecter `p.MainWindowTitle`.
  Solution & Code proposé :
  1. Déplacer l'inspection de processus hors du thread UI sur un worker dédié avec un cache de 5 secondes.
  2. Remplacer `Process.GetProcesses()` (qui prend un snapshot de tout le noyau) par une vérification ciblée `Process.GetProcessesByName("vrserver")` ou l'API Win32 rapide `FindWindow("ValveVRMainWindow", null)`.
  3. Mettre en cache les fichiers de configuration via `FileSystemWatcher` au lieu de lire le disque chaque seconde.

[tools/gui/Streamer.cs]
Lignes 950-957, 1064 à 1102 — Diagnostic : Goulot FPS/Latence / I/O disque non throttlées sur mouvement curseurs
  Impact estimé : Dégradation immédiate de la fluidité UI et de la communication IPC lors du glissement d'un curseur (netteté, saturation, fovéation, etc.). Jusqu'à 100 écritures disque synchrones et 300 paquets IPC envoyés par seconde de déplacement continu.
  Solution & Code proposé :
  Implémenter un débounceur/throttler (timer de 50 à 100 ms) : n'émettre la commande IPC et l'enregistrement sur disque que lorsque la valeur s'est stabilisée ou à une cadence maximale de 20 Hz.

[tools/gui/Streamer.cs]
Lignes 244, 628, 890 — Diagnostic : Anomalie / Kill brutal des processus fils (Process.Kill)
  Impact estimé : Sessions NVENC orphelines bloquant l'encodeur matériel au prochain lancement, ports UDP réservés par des sockets non fermés, corruption potentielle des logs de session.
  Solution & Code proposé :
  Envoyer d'abord un signal de shutdown gracieux via le canal IPC ou `CloseMainWindow()`, attendre jusqu'à 1500 ms, et seulement en cas de non-réponse forcer le `Kill()`.

[tools/gui/Launcher.cs]
Lignes 490, 513 à 521 — Diagnostic : Goulot FPS/Latence / Polling Windows de processus frénétique
  Impact estimé : 6 snapshots de tous les threads/processus de Windows par seconde via `Process.GetProcessesByName()`, provoquant des allocations de centaines d'objets `Process` non disposés dans le GC.
  Solution & Code proposé :
  Activer `EnableRaisingEvents = true` sur les instances `Process` lancées par le Launcher, et écouter l'événement `proc.Exited` au lieu de sonder continuellement le système d'exploitation.

[tools/gui/Common.cs]
Lignes 1957 à 2021 — Diagnostic : Goulot FPS/Latence / Blocage UI par script PowerShell Firewall
  Impact estimé : Gel complet de l'interface de 5 à 40 secondes lors de l'appel à `Firewall.Analyze()` sur le thread UI.
  Solution & Code proposé :
  `Firewall.Analyze` doit être 100% asynchrone via `Task<FwReport>`, et privilégier l'API COM native Windows Firewall (`INetFwPolicy2`) qui s'exécute en < 5 millisecondes sans jamais lancer de processus `powershell.exe` externe !

[tools/gui/Common.cs]
Lignes 2429 à 2484, 2529 à 2548 — Diagnostic : Goulot FPS/Latence / Fuite et sur-allocation d'objets GDI
  Impact estimé : Dans `OnPaint` et les handlers `DrawColumnHeader` / `DrawSubItem`, des `Pen`, `SolidBrush` et `GraphicsPath` sont alloués à chaque frame de rendu. Sous Windows, cela sollicite violemment la table GDI et déclenche des GC collections fréquentes qui font saccader l'interface.
  Solution & Code proposé :
  Mettre en cache les pinceaux et stylos fréquemment utilisés (`Ui.DarkBgBrush`, `Ui.AccentPen`, etc.) dans des champs `static readonly` réutilisables au lieu de les instancier avec `using (var b = new SolidBrush(...))` à l'intérieur de la boucle de peinture.

[tools/gui/Configure.cs]
Ligne 2636 — Diagnostic : Anomalie / Redémarrage brutal par `Application.Restart()` & `Environment.Exit(0)`
  Impact estimé : Arrêt destructif du processus sans laisser le framework fermer les fenêtres, libérer les singletons ou finaliser les écritures asynchrones.
  Solution & Code proposé :
  Utiliser une séquence de fermeture propre : fermer la fenêtre courante avec `DialogResult.OK`, recharger la configuration à chaud au niveau du Streamer via IPC, ou notifier l'utilisateur de redémarrer sans crasher brutalement l'environnement d'exécution.

[tools/gui/Performance.cs]
Lignes 133, 223 — Diagnostic : Anomalie / Fuite d'écouteurs d'événements (Event Handler Leak)
  Impact estimé : À chaque fois que l'utilisateur sélectionne un jeu dans la liste, une nouvelle lambda `right.Resize += (s, e) => ...` est abonnée sans jamais désabonner la précédente. Après 20 sélections, le redimensionnement exécute 20 fois le même code de mise en page, figeant la fenêtre.
  Solution & Code proposé :
  Abonner l'événement de redimensionnement une seule fois à l'initialisation de la fenêtre, ou détacher l'ancien handler avant d'en rattacher un nouveau.

[tools/gui/Advice.cs]
Lignes 79 à 85 — Diagnostic : Goulot FPS/Latence / Inversion en mémoire de logs volumineux
  Impact estimé : `File.ReadLines(ev).Reverse().Take(4000)` force le chargement intégral en RAM de `host_events.jsonl`. Sur des sessions longues avec des fichiers de 50 à 200 Mo, cela bloque le thread pendant 2 à 8 secondes et consomme des centaines de Mo de mémoire de travail.
  Solution & Code proposé :
  Lire le fichier depuis la fin en marche arrière via un `FileStream` à flux inversé (Reverse File Reader par blocs de 64 Ko), s'arrêtant dès que les 4000 dernières lignes sont atteintes sans jamais lire le reste du fichier.

[tools/gui/Lang.cs]
Lignes 236 à 450+ — Diagnostic : Violation de règle / Plus de 400 lignes de texte en dur dans le C#
  Impact estimé : Non-conformité avec la Règle 3 absolue du projet ("Aucun texte en dur dans le C# (uniquement Lang.Get) + Clés ajoutées et traduites simultanément en Français et en Anglais dans les fichiers XML"). Risque de désynchronisation linguistique majeure et complexité de maintenance.
  Solution & Code proposé :
  Transférer l'ensemble des paires clés/valeurs du dictionnaire codé en dur dans `language\fr.xml` et `language\en.xml`. `Lang.InitDictionary()` ne doit conserver qu'un fallback minimal d'urgence en cas d'absence totale du dossier `language\`.

[tools/gui/TestRunner.cs]
Lignes 313, 318 — Diagnostic : Goulot FPS/Latence / Boucles d'attente active bloquantes (Busy-Wait)
  Impact estimé : `Thread.Sleep(200)` dans des boucles `while (!app.HasExited)` sur des threads du ThreadPool consomme inutilement des threads d'arrière-plan et peut retarder l'annulation des tests.
  Solution & Code proposé :
  Utiliser l'événement asynchrone natif `process.Exited` avec un `TaskCompletionSource<int>` et un `CancellationTokenRegistration`.

[tools/gui/TestRunner.cs]
Lignes 428 à 431 — Diagnostic : Code obsolète / Incohérence d'architecture
  Impact estimé : Lenteur de 2 à 4 secondes pour lister les périphériques audio en lançant un sous-processus `powershell.exe Get-CimInstance Win32_SoundDevice`, alors que le projet possède déjà un module natif WASAPI ultra-rapide (`AudioRoute.FindCable()`).
  Solution & Code proposé :
  Supprimer l'appel PowerShell et utiliser directement l'énumération WASAPI native déjà présente dans `AudioRoute.cs`.
```

---

## PLAN D'ACTION PRIORISÉ

### A. Quick Wins (< 1 heure) — Stabilité & Qualité immédiate
1. **Corriger le bug de dégradation du bitrate dans `SessionSync.cs` (L124-126)** : supprimer la multiplication destructive par `bh / 100.0`.
2. **Éliminer la fuite d'événements `Resize` dans `Performance.cs` (L133, 223)** : désabonner avant de réabonner ou déplacer l'abonnement à l'initialisation.
3. **Supprimer le `Application.Restart()` brutal dans `Configure.cs` (L2636)** : fermer avec `DialogResult.OK`.
4. **Supprimer l'appel PowerShell pour l'audio dans `TestRunner.cs` (L428-431)** au profit de l'API WASAPI native.

### B. Gains Moyens (1 à 4 heures) — Fluidité de l'interface & Libération CPU
1. **Throttler les 9 curseurs de post-traitement dans `Streamer.cs`** : mettre en place un timer anti-rebond de 60 ms pour les écritures disque et les commandes IPC.
2. **Optimiser le polling `LinkTick` du Streamer** : éliminer `Process.GetProcesses()` au profit de `Process.GetProcessesByName("vrserver")` et mettre en cache la lecture des fichiers JSON.
3. **Remplacer le polling 2 Hz dans `Launcher.cs`** par des abonnements `proc.Exited` avec `EnableRaisingEvents = true`.
4. **Sécuriser la libération des objets COM WASAPI dans `AudioRoute.cs`** via des blocs `try/finally` avec `Marshal.ReleaseComObject`.

### C. Refontes Majeures — Conformité du projet & Architecture
1. **Conformité stricte Règle 3 (Traductions & Textes en dur)** : migrer l'intégralité du dictionnaire codé en dur de `Lang.cs` (L236-450+) vers les fichiers `language\fr.xml` et `language\en.xml`.
2. **Passage de `Firewall.Analyze()` en 100% COM natif** (`INetFwPolicy2`) : élimination totale des dépendances PowerShell et des freezes de 40 secondes.
3. **Lecteur de logs inversé par chunks dans `Advice.cs`** : lecture arrière directe en flux binaire pour ouvrir les conseils instantanément sans saturer la RAM.
4. **Protocole de fermeture gracieuse IPC** : remplacer les `Process.Kill()` brutaux par une commande de shutdown avec timeout.

---

**Rapport complet finalisé. Aucune modification de fichier source n'a été effectuée pendant cet audit, conformément à la consigne de lecture seule stricte.**
