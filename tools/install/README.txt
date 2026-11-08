AuraFlashXR v0 - PC streamer for the AuraFlashXR app on Meta Quest (OpenXR/SteamVR games, Vulkan & OpenGL APK)
ALPHA SOFTWARE - TEST AT YOUR OWN RISK. Tested on a real headset by one person on one PC. Games may crash or show a broken
image, the stream may stutter. It registers this folder as your OpenXR runtime and adds a firewall rule (undone by
the launcher's "Switch back" button). Take the headset off if the image freezes or judders. Provided "as is", without warranty
(LICENSE.txt).

Made for: Windows + NVIDIA GeForce RTX 40 series or newer + the AuraFlashXR app on Meta Quest (Quest 3 / Quest 3S / Quest 2). Other AuraFlashXR headsets
may connect (same protocol) but are not tested or supported: use AuraFlashXR itself for them.

1. Unzip this folder anywhere you like (it stays self-contained: config\ and logs\ are created inside it).
2. Double-click AuraFlashXR.exe (the launcher: the only file in this folder, everything else is in bin\ and the other folders).
3. In the OpenXR runtime row click "Switch to AuraFlashXR" (asks for administrator rights): this folder becomes the Windows OpenXR
   runtime and auraflashxr_host.exe is allowed through the firewall. The same button reads "Switch back to ..." afterwards and puts your
   previous runtime back. Moved the folder? Click the switch again from the new place.
4. Click Launch on "Setup wizard": open the AuraFlashXR app on the Meta Quest, pair it, check the settings. On the Meta Quest set the AuraFlashXR
   app's chroma key to the "colour when no game runs" (the idle frame is #107C10, the App Store app's own key colour: just turn the chroma key on), as the wizard shows.
5. Click Launch on "Streamer" and keep it open while you play. It connects to the headset by itself; start any OpenXR game.
   Close SteamVR and AuraFlashXR's own launcher/dashboard first: they use the same network ports.
"Diagnose" re-checks your setup when something feels wrong; "Logs and performance" shows how each game went.

Logs: logs\AuraFlashXR.log (always), logs\debug\<date-time>\ (when debug logging is on). Send both when reporting a problem.

Credits: the benchmark scene bench\littlest_tokyo.vab is "Littlest Tokyo" by Glen Fox (glenatron,
https://sketchfab.com/glenatron), licensed CC-BY-4.0 (https://creativecommons.org/licenses/by/4.0/), from the three.js
examples. Changed: converted to AuraFlashXR's own format (meshes decompressed, simplified shading) and shown inside a room
tiled with the scene's own texture. Its music bench\sakura_breeze.mp3 is "Sakura Breeze" by Roa Music
(https://soundcloud.com/roa_music1031), Royalty Free Music by https://www.free-stock-music.com, Creative Commons / Attribution 3.0
Unported License (CC BY 3.0, https://creativecommons.org/licenses/by/3.0/deed.en_US); changed: the first 75 seconds, re-encoded. stb_image (public domain / MIT) decodes its textures.
Third-party licences: docs\THIRD_PARTY_NOTICES.md and docs\licenses\.
