# Sceneweaver 0.1.0 — development preview

One scene. Multiple stages.

Sceneweaver is a Dalamud API 15 editor for importing Intoner layouts and Stagehand definitions, editing a shared scene, and saving both formats alongside a canonical project.

This prerelease includes scene editing, conversion reports, preservation of source-specific data, dual-save with recovery backups, an asset library, static model thumbnails, and a standalone converter.

## Installation

Extract `Sceneweaver-0.1.0.zip` into a dedicated folder and keep its DLLs together. Add `Sceneweaver.dll` through Dalamud's development-plugin loading settings, load it, and run `/sceneweaver` or `/swedit`.

## Validation and limitations

The Release build passed locally with zero warnings or errors. All 26 conversion/save tests passed. A real five-object stage converted without omissions, and a model thumbnail was checked against installed game data.

The plugin has not yet been loaded and visually verified in-game. This is a development preview, not a validated stable release. Furniture, weapons, sounds, modpack bindings and future object types have conversion limits documented in the README. Keep the canonical `.cross.json` project to preserve source data. Model thumbnails are untextured.

Source is provided under AGPL-3.0-or-later, including the attributed upstream contracts. See `THIRD_PARTY.md`.
