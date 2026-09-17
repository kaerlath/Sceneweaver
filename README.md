# Sceneweaver

**One scene. Multiple stages.**

A Dalamud API 15 development plugin for editing a canonical scene and exporting both Intoner layouts and Stagehand definitions. Open with `/sceneweaver`.

Shortcut: `/swedit`. The earlier `/crossedit` command remains an alias. Existing canonical project files retain their original format identifier and stay compatible.

## What is implemented

- Import Intoner `object-layout` JSON versions 1 and 2, Stagehand definition JSON, and canonical `.cross.json` projects.
- Edit names, visibility, position, pitch/yaw/roll in degrees, nonuniform scale, paths, folders, locks, model opacity/color, VFX tint and light parameters. Add models, VFX and lights; duplicate/delete objects; undo/redo up to 100 edits.
- Convert background models, VFX and all mapped light fields both ways. Preserve other source data in the canonical project.
- Dual-save the canonical project and both exports after showing a conversion report. Unsupported objects require acknowledgment before export.
- Stage placement controls compose Stagehand local transforms into Intoner world transforms.
- Asset library from imported scenes and local directory scans, searchable with paginated thumbnails. `.mdl` files use actual game/disk geometry, without spawning a game object. PNG/JPG previews and game icons are supported; light previews are schematic.
- CLI conversion and automated tests outside the game.

## Build and load

Requires .NET 10 and a current Dalamud API 15 installation. On Windows, the build looks in `%APPDATA%/XIVLauncher/addon/Hooks/dev`; set `DALAMUD_HOME` to override it.

```powershell
./build.ps1
```

The development DLL is `src/CrossFormat.Plugin/bin/Release/Sceneweaver.dll`. The distributable bundle is `dist/Sceneweaver-0.1.0.zip`. Keep all DLLs in the bundle together. Add the development DLL through Dalamud's development-plugin loading settings, load it, then use `/sceneweaver`. The plugin is not installed or enabled automatically by this build.

## Workflow

1. Open **Open & save**, enter a layout or stage JSON path, and choose **Open / import**.
2. Edit the scene. **Collect assets from scene** adds its objects to the asset browser; **Scan folder** reads local asset files. A `.sgb` scan entry needs an imported Intoner furniture payload before it can be placed. Place adds to the editor project, not the live world.
3. Set **Stage placement for Intoner export** if the Stagehand stage is positioned away from the identity origin. This placement remains separate from Stagehand's local definition file.
4. Choose three distinct output paths. Prefer new files while validating a scene. Choose **Review dual-save**, inspect omissions, and **Write reviewed files**.
5. Keep editing the canonical `.cross.json` file. It contains source fields that cannot be stored in the other plugin's format. Reopening only a native export cannot recover those fields.
6. Import the generated layout into Intoner / load the generated definition in Stagehand. Placement, auto-load conditions and world visibility remain controlled by those plugins.

CLI report (no writes by default):

```powershell
dotnet run --project src/CrossFormat.Cli -- input.json converted
dotnet run --project src/CrossFormat.Cli -- input.json converted --write
```

Use `--allow-omissions` only after reviewing omissions, and `--overwrite` when replacing existing outputs.

## Compatibility boundaries

| Data | Behavior |
|---|---|
| Background `.mdl`, VFX `.avfx`, lights | Shared fields convert both ways. Actual game resources must exist. |
| Intoner furniture `.sgb` | Preserved and editable in canonical/Intoner output; omitted from Stagehand. No SGB expansion implemented. |
| Stagehand weapons and sounds | Preserved in canonical/Stagehand output; transforms and names editable. Omitted from Intoner. |
| Stagehand embedded modpacks | Preserved in Stagehand/canonical files. Bound objects omitted from Intoner because resource bindings cannot be safely translated. |
| Intoner collections | IDs and source metadata preserved; collection files/resources are not bundled or converted. |
| Intoner rain, VFX playback, location, folder and lock data | Preserved in canonical/Intoner. No fabricated Stagehand equivalent. |
| Unknown JSON fields | Full raw source documents retained canonically. Stagehand extra fields retained when permitted by its serializer. Intoner exports contain only its strict current schema. |
| Future/unknown object types | Preserved canonically, omitted with a report from current native exports. |
| Local disk assets | Previewable. Omitted from Stagehand export until a complete modpack binding is provided; a disk filename is not a valid game resource path. |
| Native clipboard formats / Intoner autosaves / library prefabs | Not imported by this version. Export a normal layout first. |

Thumbnails are untextured static geometry, capped at 6,000 triangles. They do not render materials, VFX playback, furniture shared groups or weapon animation. Non-model assets can use an assigned image. No live IPC scene manipulation, automatic plugin reload, or file watching is implemented.

## Saving and recovery

All three outputs are serialized and staged before replacement. Existing files receive uniquely named `.bak` backups. Ordinary replacement failures roll back earlier replacements. A review is invalidated by edits, destination changes, or detected external file changes. Cross-directory saves cannot be a single filesystem transaction: a power loss/process crash can leave a partially updated set, recoverable from the canonical file and backups. Backups are retained until you remove them.

An unsaved project is written to `recovery.cross.json` in the plugin configuration directory when the plugin unloads normally. This is not a continuous autosave or crash-recovery guarantee.

## Validation status

The Release build and automated conversion/save tests pass locally. A real five-object Stagehand autosave was converted with zero object omissions; Stagehand round-trip positions, rotations, scales, paths and colors matched. A real tree model was decoded from the installed game data (1,534 vertices, 1,612 triangles), and its generated thumbnail was visually inspected.

The DLL has **not** been loaded into the running game, and exports have **not** been visually compared in Intoner/Stagehand. Compilation and schema tests do not prove in-game behavior. The next validation step is the checklist in `docs/IN_GAME_VALIDATION.md`.

## Source and license

AGPL-3.0-or-later. Vendored upstream contract files retain their source and licenses. See `THIRD_PARTY.md` and `docs/FORMAT_RESEARCH.md` for pinned revisions, API findings and format mapping. Private converted user scenes live outside this source repository.
