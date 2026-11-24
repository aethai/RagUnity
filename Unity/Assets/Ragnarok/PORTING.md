# Ragnarok Unity Port

This project is a fresh Unity 6.3 client for Ragnarok Online-style content.

## Direction

- Build a new client architecture instead of reviving the old UnityRO app.
- Require local GRF/data files for copyrighted game content, similar to how ROM-backed fan clients work.
- Use rAthena or Hercules as the server target, with protocol code written for the new client.
- Treat `UnityRO` as a reference for file formats, rendering behavior, and packet research only.

## Current Imported Reference Layer

`Assets/Ragnarok/ROIO` contains the old low-level RO file IO/loaders as a temporary bootstrap layer:

- GRF reading
- RO file models: RSW, GND, GAT, RSM, SPR, ACT, STR
- texture/audio/table loaders

This layer should be stabilized behind new APIs before gameplay, UI, networking, and rendering systems depend on it directly.

New code should depend on `Ragnarok.Assets.RoAssetDatabase`, not `ROIO.FileManager`. `LoadLegacy<T>` exists only as a bridge while loaders are being rewritten behind engine-owned DTOs.

## Current Milestone Map

1. Asset source configuration: locate GRF files, validate them, and load a simple known asset. Started with `RagnarokContentSettings`, `RagnarokContentService`, and `RoAssetDatabase`.
2. Map data pipeline: load RSW/GND/GAT through `RoMapDataLoader` and keep legacy ROIO parsing behind `RoAssetDatabase`.
3. Unity map renderer: render baseline maps through `RoRuntimeMapRenderer` and `RoRuntimeMapController`; `RoMapSmokeTest` remains a dev/test harness.
4. Camera and click-to-move prototype. Current camera settings are accepted and should not be retuned without an explicit visual parity review.
5. rAthena login/char/map server protocol layer.
6. Character sprite rendering from SPR/ACT.
7. Minimal playable loop: login, select char, enter map, move.
