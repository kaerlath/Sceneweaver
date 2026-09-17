# Sceneweaver 0.5.0 — In-game scene display

Sceneweaver can now show and update an editor scene in the room through Stagehand's temporary-stage IPC API. **Stagehand must be installed and enabled.** Saved Stagehand scenes are never edited by this feature.

- **Place in game at my character** in Discover assets and Mods creates the selected model/effect at your character's position and starts live display.
- **Show scene here** moves the scene's stage placement so the selected object's origin (or the first visible object's origin) is at your character. This placement is part of the canonical project and is used when exporting to Intoner.
- **Show saved placement** displays the scene at its existing stage transform without moving it.
- While live display is enabled, additions appear at your character and edits, visibility changes, deletion, undo and redo update the temporary stage. Updates are throttled while dragging controls.
- **Move to my character** in the inspector moves the selected object, accounting for stage rotation and scale.
- **Hide in game** removes Sceneweaver's temporary stage. Closing the editor window keeps the live scene visible; switching projects, changing areas/rooms, logging out, or unloading Sceneweaver stops it. Failed cleanup retains the temporary stage ID and retries while the plugin is running.

Models, VFX, lights, sounds, weapons and Stagehand-compatible mod bindings use the Stagehand renderer. Intoner-only furniture and unknown types are reported in live-display details. Unfinished objects with empty paths are skipped until a path is chosen. This release does not implement an Intoner live backend or world-space dragging gizmos; use the Scene transform controls to position objects.

Validation: 50 automated tests pass, including live placement math, updates/deletions, visibility/mod preservation, throttling, temporary-stage ownership, unavailable backend, rejected updates, cleanup retry and location changes. IPC endpoint names and signatures were checked against Stagehand's source and the installed Stagehand.Api generated consumer (Stagehand 0.4.14.0). Plugin/CLI Release builds pass. Actual IPC spawning and cleanup inside the running game still need validation.
