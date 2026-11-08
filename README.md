<div align="center">

# AuraFlashXR

**Streaming PCVR ultra basse latence vers Meta Quest — double architecture native : OpenXR Direct et driver SteamVR.**

**⚡ PRE-RELEASE V0 DISPONIBLE ⚡**

</div>

---

## La Pre-Release V0 est en ligne

La première version publique d'AuraFlashXR est publiée. Elle est pensée pour une seule chose : repousser ce que le streaming PCVR peut donner sur Meta Quest. Pas d'habillage, pas de couche inutile — de la performance brute, un frame pacing stable et une image sans compromis.

Téléchargement : [Releases — AuraFlashXR V0](https://github.com/AuraFlashXR/AuraFlashXR/releases).

---

## Ce qu'embarque la V0

### Double architecture native

Deux chemins d'exécution, un seul objectif : supprimer les intermédiaires.

- **Runtime OpenXR Direct** — l'application parle directement à l'API OpenXR du casque, sans couche de traduction. Le chemin d'exécution est le plus court possible.
- **Driver SteamVR natif** — `driver_auraflashxr.dll` s'intègre directement à `vrserver` : SteamVR voit le Quest comme un casque PCVR natif, sans protocole intermédiaire ni runtime externe. Cette intégration native permet de jouer aux titres protégés par Anti-Cheat / BattlEye.

### Streaming haut débit, latence ultra-basse

Le pipeline NVENC est réglé pour tenir la cadence, pas pour économiser la bande passante.

- **H.264 et HEVC jusqu'à près de 500 Mbps** en mode P1 low-latency.
- **120 FPS verrouillés** avec un frame pacing calé sur le rafraîchissement des dalles.
- **Latence compositeur PC réduite au dixième de milliseconde.**
- Contrairement aux solutions fermées bridées autour de 350 Mbps, la configuration est entièrement pilotée depuis le launcher.

### Dashboard Streamer et Toolbox AFXR

Une interface Bento unifiée, lisible d'un coup d'œil.

- **Télémétrie en temps réel** : FPS Jeu / Diffusé / Casque, débit, décomposition fine des latences (Encodage / Réseau / Décodage / VSync) et suivi des pertes de paquets.
- **Toolbox rapide** : bascule **OpenXR / SteamVR** en un clic, gestion du pont USB **ADB Forwarder** et installation directe de l'**APK Quest**.

### Post-processing temps réel et Frame Generation

- **Génération d'images** à la volée (`Auto`, `1/3`, `1/2`, `2/3`) avec profilage automatique par jeu.
- **Suite de post-processing en direct** : Gamma, Netteté (CAS), Contraste, Luminosité, Exposition, Saturation, Vibrance, Ombres et hautes lumières.
- **Mode Sun Glasses** — `Light`, `Dark`, `TruNite` pour des noirs profonds.
- **Presets instantanés** : Naturel, Éclatant OLED, Compétitif, Cinéma.

### Confort et audio automatisé

- **Routage audio automatique** via `AuraFlashXR-CABLE` et capture process loopback.
- **Miroir du bureau** intégré entre les sessions.

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
| Runtime PC | OpenXR Direct, SteamVR |
| Système | Windows 10 / 11, GPU NVIDIA |

---

## Crédits

AuraFlashXR s'appuie sur le travail de la communauté open source VR :

- **AirFrameXR** (jfbobier) — architecture de runtime OpenXR direct, intégration VDXR, pipeline d'encodage NVENC split-frame.
- **ALVR — Air Light VR** (alvr-org) — moteur de streaming PC-vers-casque, cœur du driver OpenVR, protocole réseau et écosystème client.

Les licences et mentions tierces complètes sont dans `LICENSE` et `THIRD_PARTY_NOTICES.md` de l'archive.
