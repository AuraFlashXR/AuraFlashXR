# Rapport Récapitulatif des Modifications — AuraFlashXR BGXR

Date : 25 octobre 2026  
Projet : **AuraFlashXR (BGXR)**  
Emplacement : `E:\OTHER\VR\BGXR`

---

## 1. Harmonisation Complète de la Marque & Renommage (AuraFlashXR)
- **Remplacement systématique de tous les anciens noms** (`AuraFlashXR`, `auraflashxr_server`, `AuraFlashXR_Host.exe`, `AuraFlashXR`) par la marque officielle **AuraFlashXR**.
- **Correction des doublons de version** : correction de `version-barthi-remix` / `Version-Version-...` en format propre et uniforme : **`BGXR`** dans l'intégralité des binaires (`.exe`, `.dll`), fichiers JSON, manifests et métadonnées.

---

## 2. Sauvegarde & Cohérence GUI ↔ `session.json`
- **Bouton d'enregistrement forcé** : ajout d'un bouton dédié dans l'interface graphique permettant d'appliquer et d'enregistrer instantanément toutes les modifications de réglages avancés.
- **Synchronisation bi-directionnelle propre** :
  - Chaque modification d'option dans les paramètres et paramètres avancés s'écrit et se vérifie directement dans `config/session.json`.
  - Élimination des options fantômes ou inventées pour correspondre strictement aux champs supportés par le schéma de session.
- **Surbrillance dynamique & inspection visuelle (Hover)** :
  - Au survol de la souris sur n'importe quel paramètre ou réglage avancé de l'interface, la clé JSON exacte ciblée dans `session.json` est affichée en surbrillance pour offrir un contrôle total et transparent.

---

## 3. Sécurisation du Mode Diffuseur (Streamer Read-Only)
- **Protection en lecture seule** : le mode Diffuseur a été restreint de manière stricte afin de ne faire que lire la configuration sans permission d'écraser, réinitialiser ou altérer le fichier `session.json` lors de son lancement ou de sa connexion.

---

## 4. Pilote SteamVR (`driver_auraflashxr.dll`) & Fluidification des Mouvements

### A. Problème du blocage à 90Hz & Détection des taux de rafraîchissement
- **Cause identifiée** : SteamVR affichait l'avertissement :  
  *« Votre casque est compatible avec d'autres fréquences de rafraîchissement, mais votre pilote graphique ne les a pas détectées. »*  
  Le driver déclarait supporter les fréquences multiples (`Prop_DisplaySupportsMultipleFramerates_Bool = true`), mais ne renseignait pas le tableau des taux disponibles (`Prop_DisplayAvailableFrameRates_Float_Array` - propriété OpenVR 2080).
- **Correctif apporté** :
  - Enregistrement du vecteur complet des fréquences supportées : `[ 72.0 Hz, 80.0 Hz, 90.0 Hz, 120.0 Hz, 144.0 Hz ]` via `SetPropertyVector` / `Prop_DisplayAvailableFrameRates_Float_Array`.
  - Initialisation dynamique de `Prop_DisplayFrequency_Float` selon la configuration demandée par l'utilisateur (ex: 120 Hz) au lieu de rester figé.
  - Déclaration du support du changement dynamique de fréquence (`Prop_DisplaySupportsRuntimeFramerateChange_Bool`).

### B. Prise en charge de la « Fluidification des mouvements » (Motion Smoothing)
- **Cause du statut "Non pris en charge"** :
  - Dans le code de l'interface SteamVR (`dashboard/chunk~8012d0c89.js`), SteamVR vérifie :
    1. Que le mode direct est actif (`Prop_HasDriverDirectModeComponent_Bool = true`).
    2. Que le système de suivi (`Prop_TrackingSystemName_String`) n'est pas blacklisté (comme l'ancien identifiant `oculus`).
    3. Que les paramètres `motionSmoothing` et `motionSmoothingOverride` sont activés dans `vr::VRSettings()`.
- **Correctif apporté** :
  - Configuration de `Prop_TrackingSystemName_String` sous l'identité native **AuraFlashXR**.
  - Activation des drapeaux d'optimisation et d'interpolation de mouvement SteamVR (`k_pch_SteamVR_MotionSmoothing_Bool = true`, `k_pch_SteamVR_MotionSmoothingOverride_Int32 = 0`).
  - Déclaration de `Prop_Hmd_SupportsAppThrottling_Bool` pour permettre le contrôle de throttling et de lissage par application.
  - Compilation de `driver_auraflashxr.dll` et déploiement dans :  
    `E:\OTHER\VR\BGXR\driver\auraflashxr\bin\win64\driver_auraflashxr.dll`.


---

## 5. Modernisation UI des Paramètres & Résolution du Défilement (Scroll)

- **Architecture en Cartes Distinctes :** Remplacement des listes compactes par des conteneurs isolés en cartes sombres `#141B24` avec bordures `#24303E` et en-têtes luminescents teal `#00D2BE`.
- **Dégagement Inférieur :** Ajout d'une marge de défilement (spacer de 140px) et réorganisation du dock d'ancrage afin que le panneau d'inspection en bas n'occulte plus les dernières options.
- **Préréglages Résolution AuraFlashXR :** Intégration directe des profils de rendu natifs d'AuraFlashXR (Ultra, High, Medium, Low) dans l'onglet Casque.
- **Centralisation Unifiée :** Suppression de la fenêtre redondante de paramètres avancés au profit d'un ensemble cohérent d'onglets (Casque, Vidéo Encodage NVENC, Amélioration d'image, Manette & Casque, Audio & Système).
