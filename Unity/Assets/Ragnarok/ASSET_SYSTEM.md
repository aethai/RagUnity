# RO Asset System

## Goal

The Unity client must treat user-provided RO files as external content. Runtime systems should ask one content layer for data instead of reaching into GRF, loose files, or ROIO globals directly.

`RoAssetDatabase` is that boundary.

## Current Shape

- `RoAssetDatabase` owns source setup, manifest building, path lookup, texture cache, disk cache access, and missing asset diagnostics.
- `GrfAssetSource` wraps a GRF archive.
- `LooseFileAssetSource` wraps files under the configured content root.
- Runtime map loading, ground atlases, model textures, water textures, and grid selector loading go through `RoAssetDatabase`.
- Runtime texture loading uses an in-memory cache first. GRF-backed textures also read/write the profile-scoped disk cache. Loose-file textures intentionally skip disk cache so local developer overrides cannot be hidden by stale cached PNGs.
- ROIO `FileManager` is still initialized internally for legacy ROIO table behavior and fallback compatibility. Renderers should not call it directly.

## Source Priority

Content settings define a root folder and an ordered GRF list.

The database registers sources in this order:

1. Loose files under the content root.
2. Configured GRF files from top to bottom.

First source wins in the manifest. To test a different content profile, change the order in `RagnarokContentSettings`, not renderer code.

This conflict rule is intentional:

- Loose files override everything. This is for development and local fixes.
- A custom GRF must be placed above base GRFs if it should replace files with the same internal path.
- Base GRFs should stay lower in the list.
- Duplicate paths are not an error. They are an override feature.
- Diagnostics should report which source won when a path is inspected.

## Path Rules

Every lookup must pass through `RoAssetPath`.

Required behavior:

- Normalize slashes.
- Lowercase lookup keys.
- Preserve RO-style internal paths.
- Decode GRF file table paths from legacy Korean/CP949 bytes to Unicode manifest keys.
- Generate fallback candidates in both directions for legacy Korean/CP949 path edge cases.
- Keep diagnostics grouped by requested asset, expected type, and lookup candidates.

Do not add one-off path fixes in map, model, sprite, audio, or UI code.

## Disk Cache

The disk cache is intentionally a separate layer from the manifest.

`RoAssetDiskCache` provides:

- A content profile hash based on root path, configured archive order, archive size, and archive write time.
- A cache format version. Bump this when serialized cache data changes incompatibly.
- Deterministic cache paths per asset category and source path.
- Atomic-ish byte writes through a temporary file.
- Safe cache clearing limited to the app cache folder.

Initial categories:

- `Raw`
- `Texture`
- `SpriteAtlas`
- `Map`
- `Audio`
- `Effect`
- `Manifest`

Runtime texture loading uses the `Texture` cache category for GRF-backed textures. The first load decodes the source texture, stores it in memory, and writes a cached PNG. Later loads in the same content profile can read that PNG before touching the GRF texture bytes.

The cache is not yet used by model/map/sprite/audio/effect artifact loading. That is deliberate; those builders should be added only after each runtime format is stable.

## Cache Invalidation

Cache invalidation is profile-based, not destructive.

The cache profile changes when:

- the content root path changes,
- configured GRF order changes,
- configured GRF file size changes,
- configured GRF write time changes,
- the cache format version changes.

When the profile changes, the client uses a different cache folder. It does not automatically delete old profiles. This avoids deleting useful developer artifacts and keeps startup deterministic.

Current behavior:

- `Clear Disk Cache` in Content Diagnostics clears only the current profile.
- Old profiles may remain on disk until manually pruned.
- Loose-file textures are not read from or written to disk cache. Reinitialize/reload the database to pick up loose-file changes, and clear memory cache if the same database instance remains alive.
- A future launcher/settings tool can add profile pruning.

## Editor Hot Reload

`RoAssetDatabase` does not watch GRFs or loose files automatically yet.

Current development flow:

- Change GRF order, GRF files, or loose files.
- Click `Initialize / Reload` in Content Diagnostics or recreate the runtime content service.
- Clear memory/disk cache if the test requires a clean read.

Planned behavior:

- add explicit editor reload commands for map/content tools,
- optionally add file watching for loose files,
- avoid watching large GRF files continuously unless profiling proves it is safe.

## Thread Safety

`RoAssetDatabase` should be treated as main-thread owned for now.

Rules:

- Do not call GRF reads from multiple worker threads through the same database instance.
- Unity object creation such as `Texture2D` and `AudioClip` must stay on the Unity main thread.
- Background builders may be added later, but they need their own source/session rules.
- Async map loading must serialize unsafe legacy ROIO calls behind a lock or queue.

This rule is temporary but explicit. We can relax it later after the GRF source layer and ROIO loader calls are audited.

## Builder Plan

The cache builder should be added in small steps:

1. Manifest export: write a compact source manifest for diagnostics and future launcher validation. Implemented.
2. Texture cache: decode BMP/TGA/PNG/JPG once and write Unity-friendly cached PNG or raw texture metadata. Probe builder exists; GRF-backed runtime texture loading is wired to cached PNGs.
3. Map cache: convert GND/RSW/GAT into stable mesh/material metadata after renderer rules are finalized.
4. Sprite cache: convert SPR/ACT plus palettes into atlases and animation clips.
5. Effect cache: parse STR and animated texture dependencies.
6. Audio cache: validate WAV/MP3 availability and keep map/entity references.

## Rules For New Systems

- Audio, sprites, palettes, STR effects, fonts, UI tables, and networking metadata must request content through `RoAssetDatabase`.
- Current entry points: WAV loads as `AudioClip`; MP3/OGG/font/LUB assets are raw bytes; PAL is raw bytes; SPR/ACT/STR load typed; TXT/XML/Lua load as text.
- STR effect textures must resolve through the asset database texture path, not through direct `FileManager` calls.
- If a system needs a new format, add a loader entry point to the asset layer first.
- If an asset is missing, report it through `RoAssetDiagnostics`.
- If a path looks encoded or map-specific, fix normalization once in `RoAssetPath`.
- Do not hide missing assets with visual fallbacks unless the fallback logs one clear diagnostic.

## Future Work

- Add model/map/sprite/audio/effect artifact caches after those runtime formats stabilize.
- Add optional editor hot reload/file watching for loose files if manual reload becomes too slow.
- Add launcher/settings support for old cache profile pruning.
- Keep renderer changes separate from cache changes.
