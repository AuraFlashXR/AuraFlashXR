# 🚧 Prochaine Version : v0.4-ALPHA

**Date** : 27 Octobre 2026
**Statut** : La branche `main` et la release [`v0.3-alpha`](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi/releases/tag/v0.3-alpha) sont **figées** — toute modification à partir de maintenant est destinée à la **v0.4-ALPHA**.

---

## 1. Avertissement

> [!IMPORTANT]
> **Toutes les modifications effectuées après le 27/10/2026 07:30 seront intégrées à la v0.4-ALPHA.**
> La v0.3-ALPHA est publiée et ne recevra plus de nouveaux binaires (correctifs critiques uniquement, si nécessaire).

## 2. Périmètre prévu pour la v0.4

Les chantiers suivants sont les candidats naturels pour la prochaine itération (issus des analyses des docs 26-27/10) :

- **Activation effective du transport QUIC** (`quinn 0.11` + `tokio`) dans la boucle réseau du client Quest — l'architecture `NetworkDataBridge` lock-free (`crossbeam-channel`) est déjà en place dans `client_core/src/quic_network.rs`
- **Pipelining décodeur asynchrone** — passage de `AMediaCodec` en mode callback (`AMediaCodecCallback`) au lieu du polling synchrone, et `max_images = 2` sur l'`ImageReader` (gain 2 à 4 ms de latence)
- **Foveated rendering dynamique** — activation de `XR_FB_foveation` avec profil adaptatif côté client
- **Zero-Copy vidéo complet** — liaison `AHardwareBuffer` → `VkImage` (`VK_ANDROID_external_memory_android_hardware_buffer`) pour le build Vulkan, `EGL_ANDROID_image_native_buffer` pour le build OpenGL
- **Serveur PC en QUIC** — mise à jour de `server_core` pour parler QUIC au client (actuellement UDP shard-interleaved)

## 3. Règles de gestion des versions

| Version | Statut | Usage |
|---|---|---|
| `v0.1-ALPHA` | Archivée | Historique |
| `v0.2-ALPHA` | Archivée | Historique |
| `v0.3-ALPHA` | **Release courante** | Téléchargement recommandé |
| `v0.4-ALPHA` | **En développement** | Toute nouvelle modification |
