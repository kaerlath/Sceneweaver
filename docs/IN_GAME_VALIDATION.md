# In-game validation still to perform

The local build and tests are complete; the following checks require loading the development DLL inside Dalamud.

1. Add the Release DLL as a development plugin and load it. Confirm `/sceneweaver` opens without exceptions in the Dalamud log. Close/reopen the window and unload/reload the plugin.
2. Start with an empty scene. Confirm **Discover assets** lists the game catalog; browse folders and search models without importing a scene. Select a huge building and a small prop, check fit/orbit/zoom/pan, wireframe and auto-rotate. Resize the columns and window. Confirm selecting does not add scene/world objects and **Add to scene** adds exactly one editor object. Check missing assets, VFX/sound notices and favorites after reload. Use native **Open Stagehand**, **Open Intoner**, **Open project** and save dialogs; check configured/default folders, remembered folders and cancellation. Open a copy of a real Stagehand JSON and confirm the inspector shows all objects. Verify the unsaved-scene replacement guard and assigned PNG previews.
3. Edit a model's position, rotation, scale, color and opacity. Undo and redo. Edit a light's flags/shape/angles. Verify a locked object cannot be edited or deleted.
4. Export to three new files. Import the Intoner layout and load the Stagehand definition separately. Confirm the shared objects align using the same stage placement and compare VFX tint/light behavior visually.
5. Import an Intoner furniture layout. Verify Stagehand omissions appear clearly and require acknowledgment. Reopen the canonical file and verify furniture, folder colors, locks and source-specific settings remain.
6. In Mods, import a Penumbra `.pmp` and an installed `meta.json`. Select different Single/Multi options, prepare and add the mod. Check its models use modded geometry and color textures, including two mods replacing the same game path. Add a mod model and VFX; apply a texture-only mod to a Scene object. Undo/redo, remove a binding, reopen the project, and confirm missing-resource errors are clear. Export to Stagehand and compare the appearance there. Confirm bound objects are reported as omitted from Intoner and preserved in canonical/Stagehand output. Open an existing Stagehand modpack stage and repeat with embedded, disk and game-file replacements.
7. Edit a destination file externally after review. Confirm the save is rejected. Verify `.bak` recovery files on successful overwrites and recovery.cross.json after normal unload with unsaved changes.

Do not treat a successful compile or the standalone mesh thumbnail as proof that all in-game rendering behavior has been validated.

## Live display (0.5.0)

Enable Stagehand. From an empty Sceneweaver project, select a known small model and click **Place in game at my character**. Confirm it appears near your character. Move/rotate/scale it in Scene, toggle Visible, undo/redo, duplicate and delete it. Check that modded models and VFX render through Stagehand. Use **Move to my character** with a rotated/scaled stage and verify world alignment. Show an imported scene with **Show scene here**, then confirm **Show saved placement** uses the saved transform.

Close/reopen the editor (the live scene should remain), hide it, switch projects, change zones and housing rooms, log out and unload Sceneweaver (its live objects should disappear). Confirm unrelated local/temporary Stagehand scenes remain untouched. Disable Stagehand during live display, re-enable it, and check that cleanup finishes and display does not restart until requested. Check incomplete/unsupported objects are listed in live-display details. Inspect Dalamud logs for IPC errors; automated tests use a fake backend and do not prove the runtime integration.
