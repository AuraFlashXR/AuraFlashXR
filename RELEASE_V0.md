# AuraFlashXR V0 — Release

Runtime de streaming PCVR basse latence vers Meta Quest, avec encodeur NVENC multi-slice, driver OpenVR unifié, déploiement ADB en un clic et interface sombre modernisée.

---

## Contenu de l'archive

| Élément | Description |
|---|---|
| `AuraFlashXR.exe` | Lanceur : Toolbox, sélection des outils, réglages visuels |
| `bin\` | Streamer, host, shim OpenXR, driver OpenVR, encodeur |
| `config\` | Configuration et presets d'encodage |
| `profiles\` | Profils de diffusion |
| `tools\` | ADB, VB-Cable, langues, APK Quest |
| `docs\` | Patch notes et documentation embarquée |

---

## Installation

1. Décompresser l'archive dans un dossier accessible en écriture.
2. Lancer `AuraFlashXR.exe`.
3. Ouvrir le **Streamer** et suivre l'assistant de première configuration.
4. Connecter le casque en USB ou Wi-Fi, puis lancer la diffusion.

Aucun chemin absolu n'est utilisé : l'application fonctionne depuis n'importe quel dossier.

---

## Nouveautés de cette version

### Interface Launcher
- Bandeau supérieur compact : logo allongé, pastilles **PRE-RELEASE** et **V0** avec lueur orange, boutons de fenêtre dessinés.
- Toolbox AFXR et cartes Bento en verre liquide, accents par outil.
- Pied de page repensé : langue, **BACKGROUND**, **POLICE**, Aide et GitHub en boutons arrondis à coins anti-crénelés, chacun avec sa teinte (jaune, rouge, violet, bleu).
- Sélecteur de police avec aperçu réel de chaque famille et de chaque graisse.
- Sélecteur de fond d'image parmi les visuels embarqués, **BACK-UNI-BLACK** par défaut.

### Interface Streamer
- Panneau **INFORMATION EN DIRECT** unifié : GPU et pilote, jeu, casque, encodeur actif, résolution active, runtime OpenXR.
- Tuiles **FPS JEU / FPS DIFFUSÉ / FPS CASQUE** et panneau de génération d'images.
- Post-traitement : réglages fins, profils (Naturel, Compétitif, Cinéma, Par défaut) et modes de lunettes.
- Position / espace et bureau virtuel dans un panneau dédié.
- Contours de panneaux en dégradé orange, titres en majuscules, valeurs centrées et lisibles.

### Diffusion
- Encodeur NVENC multi-slice avec résolution d'encodage fovéée (FFR), publiée dans le statut du host.
- Presets H.264, HEVC, AV1 et PyroWave.
- Routage audio localisé (câble virtuel, muting, absence de câble).

### Traductions
- Français et anglais complets, appliqués aux quatre exécutables GUI.

---

## Historique récent

Les 224 entrées détaillées de la refonte V0 sont dans `docs/PATCH_NOTES.md`, embarqué dans l'archive. Les dernières étapes :

| Date | Modification |
|---|---|
| 08/11 12:45 | Couleurs propres par bouton du footer |
| 08/11 12:35 | Coins arrondis anti-crénelage sur les boutons du footer |
| 08/11 12:20 | Boutons du footer refaits à plat, code de pastille supprimé |
| 08/11 15:10 | Header sans doublon d'image |
| 08/11 14:55 | Fond des boutons du footer rétabli |
| 08/11 — | Panneaux du Streamer : typographie, hauteurs, alignements |
| 08/11 — | Panneau INFORMATION EN DIRECT unifié et ligne Résolution active |
| 08/11 — | Ligne GPU en vert NVIDIA, bouton « Trouver mon casque » replacé |

---

## Notes

- Cible unique : **Meta Quest 3 / 3S / Pro / 2**.
- Build 100 % local, reproductible via `build.ps1`.
- Crédits upstream (AirFrameXR, ALVR) conservés dans `LICENSE` et `THIRD_PARTY_NOTICES.md`.
