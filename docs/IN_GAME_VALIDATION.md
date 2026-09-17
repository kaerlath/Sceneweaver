# In-game validation still to perform

The local build and tests are complete; the following checks require loading the development DLL inside Dalamud.

1. Add the Release DLL as a development plugin and load it. Confirm `/sceneweaver` opens without exceptions in the Dalamud log. Close/reopen the window and unload/reload the plugin.
2. Open a copy of a real Stagehand JSON. Confirm the inspector shows all objects. Collect its assets and confirm the model thumbnails appear without spawning world objects. Check missing assets and assigned PNG previews.
3. Edit a model's position, rotation, scale, color and opacity. Undo and redo. Edit a light's flags/shape/angles. Verify a locked object cannot be edited or deleted.
4. Export to three new files. Import the Intoner layout and load the Stagehand definition separately. Confirm the shared objects align using the same stage placement and compare VFX tint/light behavior visually.
5. Import an Intoner furniture layout. Verify Stagehand omissions appear clearly and require acknowledgment. Reopen the canonical file and verify furniture, folder colors, locks and source-specific settings remain.
6. Test a Stagehand modpack stage. Confirm bound objects are omitted from Intoner and preserved in canonical/Stagehand output.
7. Edit a destination file externally after review. Confirm the save is rejected. Verify `.bak` recovery files on successful overwrites and recovery.cross.json after normal unload with unsaved changes.

Do not treat a successful compile or the standalone mesh thumbnail as proof that all in-game rendering behavior has been validated.
