<div align="center">

# AuraFlashXR

**Streaming PCVR ultra basse latence vers Meta Quest — driver SteamVR natif, runtime OpenXR maison.**

</div>

---

## Le principe

AuraFlashXR remplace la chaîne classique du streaming VR par un pipeline direct. Le casque n'est plus vu comme un périphérique réseau par SteamVR : notre driver le présente comme un **casque PCVR natif**, branché en direct.

- **Driver SteamVR propriétaire** — `driver_auraflashxr.dll` déclare le Quest comme un HMD natif : plus de surcouche, plus de protocole intermédiaire, plus de runtime externe.
- **Runtime OpenXR maison** — l'application n'emprunte ni VDXR ni un runtime tiers pour l'affichage : elle parle directement à l'API OpenXR du casque.
- **Configuration débridée** — le driver récupère toute la configuration depuis le launcher. Là où les solutions fermées plafonnent le débit autour de 350 Mbps, AuraFlashXR laisse l'encodeur monter tant que la liaison tient.
- **Frame Pacing SteamVR** — le rythme des images est calé sur le rafraîchissement des dalles du casque, image par image.

---

## Frame Pacing : le métronome

Avoir 120 FPS ne suffit pas. Si le PC envoie les images de façon irrégulière, l'œil perçoit des micro-saccades même à fréquence nominale. Le Frame Pacing impose un rythme fixe à la carte graphique, aligné sur le rafraîchissement du casque.

Résultat mesuré sur **Pistol Whip** :

| Mesure | Valeur |
|---|---|
| FPS jeu / diffusés / casque | **120.0 / 120.0 / 120.0** |
| Paquets perdus | **0** |
| Débit | **484 Mbps** en H.264 |
| Compositeur PC | **0.2 ms** |
| Encodage | **3.7 ms** |
| Réseau | **3.4 ms** |
| CPU VR Server | **2.0 %** |
| RAM VR Server | **91.3 Mo** |
| Encodeur GPU | **11.4 %** |

Synchronisation 1:1, zéro saccade, charge machine minimale : la sensation d'un casque filaire, sans le fil.

---

## Ce que fait l'application

### Launcher
Un poste de pilotage unique : cartes Bento par outil, Toolbox AFXR, réglages visuels en direct.

- Sélection du **fond d'image** parmi les visuels embarqués.
- Sélection de la **police** avec aperçu réel de chaque famille et de chaque graisse.
- **Langue** commutable à chaud (français / anglais).
- Accès direct : Streamer, Performances, Configuration, Diagnostic, Assistant.

### Streamer
Le tableau de bord de diffusion, lisible d'un coup d'œil.

- **INFORMATION EN DIRECT** : GPU et pilote, jeu détecté, casque, encodeur actif, résolution active, runtime OpenXR.
- **FPS JEU / FPS DIFFUSÉ / FPS CASQUE** en tuiles séparées.
- **Génération d'images** : activation, mode et suivi.
- **Post-traitement** : réglages fins, profils (Naturel, Compétitif, Cinéma, Par défaut), modes de lunettes.
- **Position / espace** et **bureau virtuel**.
- Détection du casque en un clic.

### Diffusion
- **NVENC multi-slice** avec résolution d'encodage fovéée (FFR) : l'encodeur travaille sur une zone réduite autour du regard, le casque reçoit la pleine résolution.
- Presets **H.264**, **HEVC**, **AV1** et **PyroWave**.
- **Routage audio** automatique via câble virtuel, avec gestion du mute et de la perte de câble.
- Déploiement **ADB** et APK Quest intégrés.

---

## Installation

1. Télécharger `AuraFlashXR-V0.zip` depuis la page [Releases](https://github.com/AuraFlashXR/AuraFlashXR/releases).
2. Décompresser dans un dossier accessible en écriture.
3. Lancer `AuraFlashXR.exe`.
4. Ouvrir le **Streamer**, suivre l'assistant de première configuration.
5. Connecter le casque en USB ou Wi-Fi, puis lancer la diffusion.

Aucun chemin absolu n'est utilisé : l'application fonctionne depuis n'importe quel dossier.

---

## Compatibilité

| Élément | Prise en charge |
|---|---|
| Casques | Meta Quest 3 / 3S / Pro / 2 |
| Encodeurs | NVENC (H.264, HEVC, AV1), PyroWave |
| Runtime PC | SteamVR, OpenXR |
| Système | Windows 10 / 11, GPU NVIDIA |

---

## Crédits

AuraFlashXR s'appuie sur le travail de la communauté open source VR :

- **AirFrameXR** (jfbobier) — architecture de runtime OpenXR direct, intégration VDXR, pipeline d'encodage NVENC split-frame.
- **ALVR — Air Light VR** (alvr-org) — moteur de streaming PC-vers-casque, cœur du driver OpenVR, protocole réseau et écosystème client.

Les licences et mentions tierces complètes sont dans `LICENSE` et `THIRD_PARTY_NOTICES.md` de l'archive.
