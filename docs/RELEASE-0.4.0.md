# Sceneweaver 0.4.0 — Mod assets

The new **Mods** tab imports Penumbra `.pmp` packages or an installed mod's `meta.json`. Review its Single/Multi option groups, prepare the selected files, then add the mod to the project. No Penumbra installation or activation is performed.

- Models, effects and sounds appear in the mod resource list. Select a model for an isolated textured preview, then explicitly add it to the scene.
- Materials, textures and other file replacements are included alongside placeable resources. The selected files are compressed and embedded, so Stagehand export does not depend on the original package remaining on disk.
- Apply a mod to an existing selected scene object, including texture-only mods. Remove its binding in the Scene inspector.
- Modpacks from opened Stagehand saves appear in the same library. Preview loading supports their embedded, disk and vanilla-game resource replacements, with distinct caches for modded and unmodified assets.
- Canonical and Stagehand saves retain the resource bindings and source metadata. Intoner export continues to report mod-bound objects as omitted; their data remains in the canonical project.

Supported imports: Penumbra file replacements, file swaps, Single/Multi options and priorities. Advanced groups and metadata manipulations are rejected explicitly. `.ttmp`/`.ttmp2` archives are not imported directly: install them in Penumbra first, then choose the generated `meta.json`, subject to the same supported-option limits. VFX can be added/exported, but animated effect previews are still unavailable. Color/shader limitations from 0.3.0 still apply.

Selected resource data is limited to 64 MB uncompressed per import and 8192 resource mappings. Project reads/writes are limited to 256 MB; undo snapshots have a 128 MB budget, retaining at least the newest snapshot. Embedded preview files use content-addressed names under the plugin's `mod-preview-cache` directory. This disposable cache may be cleared while the plugin is unloaded.

Validation: 41 automated tests pass, covering package/folder imports, dependencies, selections, bindings, round trips, missing files, unsupported groups, path traversal and bounded expansion. A real model/material/texture chain was embedded at a nonexistent game path, exported/reimported, resolved from the modpack and decoded successfully. Color preview and real-scene conversion checks still pass. The new Mods UI and loading the resulting modded stage inside Stagehand still require in-game testing.
