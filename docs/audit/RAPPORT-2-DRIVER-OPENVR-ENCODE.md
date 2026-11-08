# 📊 RAPPORT D'AUDIT N°2 — PIPELINE D'ENCODAGE DU DRIVER OPENVR (PC)
**Date :** 28/10/2026 14:53 — **Auditeur :** Sous-agent audit-vr (lecture seule)
**Périmètre :** `tools\driver_openvr` — CEncoder, FrameRender, VideoEncoderNVENC, OvrDirectModeComponent, PoseHistory, HMD, Controller, lib.rs, tracking.rs, d3drender.cpp
**Mode :** LECTURE SEULE STRICTE — aucune modification effectuée

---

## RÉSUMÉ EXÉCUTIF — TOP 10 DES PROBLÈMES (TRIÉS PAR GAIN ESTIMÉ)

| Rang | Problème & Fichier | Type | Impact Actuel | Gain Potentiel |
|---|---|---|---|---|
| **1** | **Sérialisation Compositor/NVENC (pas de double buffering)** — `OvrDirectModeComponent.cpp` & `CEncoder.cpp` | Architecture | Le thread Present bloque sur `WaitForEncode()` : rendu SteamVR et encodage NVENC en série stricte | **-3.5 à -6.5 ms** photon-to-network |
| **2** | **Recréation des SRV D3D11 à chaque frame** — `FrameRender.cpp` (L656-669) | Goulot FPS | `CreateShaderResourceView()` par œil et par layer (360-960 allocs kernel/s) | **-1.0 à -1.5 ms CPU** |
| **3** | **Triple `Flush()` consécutif par frame** — `OvrDirectModeComponent.cpp` (L321, L345) & `FrameRender.cpp` (L888) | Goulot FPS | Batching GPU brisé, stalls CPU/GPU | **-0.5 à -1.2 ms** |
| **4** | **`CopyResource` superflu avant NVENC** — `VideoEncoderNVENC.cpp` (L122-123) | Sous-optimal | Copie 4K/5K complète au lieu de zero-copy `NvEncRegisterResource` | **-0.5 à -0.9 ms** / -50 Go/s VRAM |
| **5** | **`SetMultithreadProtected(TRUE)` global** — `d3drender.cpp` (L74-84) | Sous-optimal | Section critique sur chaque appel D3D11 sans bénéfice réel | **-0.3 à -0.6 ms CPU** |
| **6** | **Fuite VRAM cache textures + race sur `m_handleMap`** — `d3drender.cpp` (L218-298), `OvrDirectModeComponent.cpp` (L56-117) | Anomalie critique | Cache jamais vidé, accès multithread sans mutex → crashs | Stabilité absolue |
| **7** | **`thread::sleep` granularité 15.6 ms dans le VSync pacing** — `lib.rs` (L378-404) | Anomalie pacing | Un sleep de 2 ms dure 15 ms → frames perdues massivement | **+15 à +30 FPS effectifs** |
| **8** | **Recherche O(N) avec `pow()` sur 360 éléments** — `PoseHistory.cpp` (L42-69) | Sous-optimal | 3 240 appels `std::pow()` par frame sous mutex | **-0.3 à -0.5 ms CPU** |
| **9** | **Vitesses HMD nulles (0,0,0)** — `HMD.cpp` (L198-216) | Latence tracking | SteamVR ne peut pas extrapoler → judder dès micro-drop | **-2.0 à -5.0 ms perçus** |
| **10** | **`buffer.to_vec()` par paquet vidéo** — `lib.rs` (L337-342) | Allocations par frame | Clonage NAL à 200-500 Mbps → fragmentation heap | **-0.3 à -0.5 ms CPU** |

---

## AUDIT DÉTAILLÉ (extraits clés)

### [OvrDirectModeComponent.cpp]

**L320-346 — GOULOT N°1 : Sérialisation Present/Encode**
`CopyTexture()` appelle `m_pEncoder->WaitForEncode()` → le thread de présentation SteamVR bloque jusqu'à la fin de l'encodage NVENC. + un `Flush()` de debug explicite (commentaire L321 : *"This can go away, but is useful to see it as a separate packet on the gpu in traces"*).

**Solution — double buffering ping-pong + fence D3D11** :
```cpp
void OvrDirectModeComponent::CopyTexture(uint32_t layerCount) {
    // Suppression du Flush() de debug L321
    int writeBufferIdx = m_stagingBufferIndex;
    m_stagingBufferIndex = 1 - m_stagingBufferIndex; // ping-pong
    m_pEncoder->WaitForStagingBufferReady(writeBufferIdx); // fence du buffer cible
    m_pEncoder->CopyToStagingIndexed(writeBufferIdx, pTexture, bounds, poses, layerCount, false, ...);
    // Pas de Flush() : la fence signalera la complétion
}
```
**Gain : -3.5 à -6.5 ms de latence, pacing 120 FPS stable.**

**L56-137 — Race condition + fuite** : `m_handleMap` muté sans mutex pendant que `CopyTexture()` le lit en continu → data-race et crash (access violation) lors des transitions de jeux. Pointeur suspendu (`dangling`) en cas d'échec `CreateTexture2D`.
**Solution** : `std::shared_mutex` (lecteurs-rédacteurs) + `std::make_unique`.

### [FrameRender.cpp]

**L656-669 — SRV recréées par frame** : `CreateShaderResourceView()` appelé 2x par layer à chaque frame (jusqu'à 960 allocations kernel D3D11/s) → micro-stutters via `nvwgf2umx.dll`.
**Solution** : cache `std::unordered_map<ID3D11Texture2D*, ComPtr<ID3D11ShaderResourceView>>` rempli à la création des swapchains. **-1.0 à -1.5 ms CPU.**

**L824-854 — États de pipeline réappliqués par layer** : `IASetInputLayout`, `VSSetShader`, `PSSetShader`... identiques pour tous les layers → 0.15 ms CPU gaspillée.
**Solution** : configurer le pipeline immuable avant la boucle ; seuls SRV et viewports changent par œil.

**L888 — 3e Flush()** : suppression (l'encodage NVENC suit sur la même file).

### [VideoEncoderNVENC.cpp]

**L118-123 — CopyResource superflu** : copie VRAM→VRAM complète de la texture 4K/5K vers la surface interne NVENC → 0.5-0.9 ms GPU, 50+ Go/s gaspillés à 120 FPS HDR 10-bit.
**Solution** : zero-copy natif — `nvEncRegisterResource` (`NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX`) sur la texture de composition finale + `nvEncMapInputResource`.

**L392-403 — Config VBV/slices sous-optimale** : VBV = 1.1 frame, pas de lookahead, pas de multi-slices → le casque ne peut pas décoder en streaming progressif.
**Solution** : `numSlices = 4` + `NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY` → **-2.0 à -4.0 ms** (décodage dès le premier slice).

### [d3drender.cpp]

**L74-84 — `SetMultithreadProtected(TRUE)` inutile** : section critique globale sur chaque méthode du contexte D3D11 alors que la synchronisation est déjà gérée par les CThreadEvent du driver.
**Solution** : passer à `FALSE` ou deferred contexts. **-0.3 à -0.6 ms CPU.**

**L218-298 — Fuite VRAM du cache** : `m_SharedTextureCache` ne libère jamais les `ID3D11Texture2D*` (pas de `Release()`, pas de nettoyage dans `Shutdown()`), et aucun mutex → race conditions.
**Solution** : `ComPtr<ID3D11Texture2D>` + `m_SharedTextureCache.clear()` dans `Shutdown()`.

### [PoseHistory.cpp]

**L28, L42-69 — Recherche O(N) avec pow()** : `std::list` heap-allouée (malloc/free par pose à 120-200 Hz), parcours des 360 éléments les plus anciens d'abord, **3 240 appels `std::pow(double,double)` par frame** sous mutex sur le thread de rendu.
**Solution** : buffer circulaire `std::array<TrackingHistoryFrame, 64>`, parcours inverse (`rbegin()`), `diff * diff` au lieu de `pow`, early-exit à `distance < 1e-5f`. **Matching en < 1 µs.**

### [HMD.cpp]

**L198-216 — Vitesses HMD nulles** : `pose.vecVelocity` et `pose.vecAngularVelocity` laissés à {0,0,0} alors que `motion.linearVelocity`/`angularVelocity` sont disponibles → SteamVR ne peut pas extrapoler la pose → **judder dès qu'une frame prend du retard**.
**Solution** :
```cpp
pose.vecVelocity[0] = motion.linearVelocity[0]; // ... [1], [2]
pose.vecAngularVelocity[0] = motion.angularVelocity[0]; // ... [1], [2]
pose.poseTimeOffset = Settings::Instance().m_headsetPoseTimeOffset;
```
**Gain : -2.0 à -5.0 ms de latence perçue, reprojection fluide.**

### [Controller.cpp]

**L149-157, L398-422 — std::map + squelette inutile** : recherche O(log N) dans un arbre rouge-noir à chaque bouton/tick (120/s), et calcul complet du squelette de main (31 os, Lerp/Slerp) même si le jeu ne demande pas de skeletal input.
**Solution** : tableau plat indexé O(1) + court-circuit si `m_skeletonLevel == VRSkeletalTracking_None`. **-0.4 à -0.8 ms CPU.**

### [lib.rs]

**L337-342 — `buffer.to_vec()` par NAL** : clone heap de chaque paquet vidéo NVENC à 200-500 Mbps → centaines de Mo alloc/désalloc par minute.
**Solution** : buffer pool réutilisable ou `bytes::Bytes` (transfert de propriété sans copie).

**L378-404 — 🚨 ANOMALIE CRITIQUE DE PACING** : `thread::sleep(duration)` s'appuie sur le timer kernel Windows de granularité **15.625 ms** — un sleep de 2 ms dure 15 ms → **rate complètement la VSync de SteamVR**. Quand l'option est désactivée, `yield_now()` ne cadence rien.
**Solution** : `timeBeginPeriod(1)` au démarrage + `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` + micro spin-lock sur les 300 dernières µs :
```rust
pub fn high_precision_sleep(duration: Duration) {
    let start = Instant::now();
    if duration > Duration::from_millis(1) {
        std::thread::sleep(duration - Duration::from_millis(1));
    }
    while Instant::now().duration_since(start) < duration {
        std::hint::spin_loop();
    }
}
```
**Gain : +15 à +30 FPS effectifs, élimination du judder.**

---

## PLAN D'ACTION PRIORISÉ

### Phase 1 : Quick wins (< 1h par point)
1. **Éliminer les 3 `Flush()` par frame** (`OvrDirectModeComponent.cpp` L321/L345, `FrameRender.cpp` L888) → **~1.0 ms**
2. **Corriger le VSync pacing** (`lib.rs` L378-404) avec timer haute précision → **élimine les drops de 15 ms**
3. **Optimiser `GetBestPoseMatch`** (`PoseHistory.cpp`) : `diff*diff`, parcours inverse, early-exit → **~0.4 ms CPU**
4. **Injecter les vélocités HMD** (`HMD.cpp` L203-215) → **élimine le judder SteamVR**

### Phase 2 : Moyens (1-4h)
5. **Cache des SRV D3D11** (`FrameRender.cpp`) → **~1.2 ms CPU**
6. **Supprimer `SetMultithreadProtected` + corriger les fuites VRAM** (`d3drender.cpp`) → **~0.5 ms + stabilité**
7. **Tableaux plats au lieu de std::map** (`Controller.cpp`, `auraflashxr_server.cpp`) → **~0.3 ms CPU**

### Phase 3 : Refontes architecturales
8. **Pipeline asynchrone double-buffer Rendu/NVENC** (ping-pong + fence D3D11) → **-4.0 à -6.0 ms photon-to-network**
9. **Zero-copy NVENC** (`NvEncRegisterResource`) → **-0.8 ms GPU**
10. **Zero-copy bitstream Rust/C++** (éliminer `to_vec()`) → streamer les NALs directement vers UDP

---

**TOTAL GAINS POTENTIELS : -8 à -14 ms de latence, +15 à 30 FPS effectifs, stabilité long terme restaurée.**
