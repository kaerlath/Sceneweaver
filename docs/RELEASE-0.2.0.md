# Sceneweaver 0.2.0 — Asset discovery and Windows file browsing

- A redesigned workspace separates asset discovery, scene editing and saving. Resizable folder, result and preview columns replace the scene-only asset list.
- Browse 117,677 indexed game resources from Stagehand's bundled catalog, even with an empty project. Filter models, effects or sounds, search path fragments, navigate folders and save favorites.
- Selecting a model loads its geometry into an isolated side preview. Large models fit automatically. Drag to orbit, right-drag to pan, scroll to zoom, or use fit, auto-rotate and wireframe controls. Nothing is spawned in the room.
- Open/import and save destination buttons use native Windows file dialogs. Stagehand's configured library and Intoner's layouts directory are the initial locations; subsequent selections remember folders by format. Stagehand autosaves have their own open button.
- Existing project formats, raw source preservation, conversion reports, reviewed dual-save, backups and undo/redo remain supported.

Previews show static, untextured geometry. Very dense meshes are sampled and marked simplified. Animated VFX, audio playback, furniture shared-group previews and skeletal animation are not implemented. Some catalog paths may not exist in the installed game version.

Local validation: Release build, 32 automated tests, and a real five-object scene round-trip passed. A real game model's normalized fit and original bounds passed. Native file-dialog structure layout was checked against Windows x64. Revised UI/dialog interaction still needs in-game testing; standalone UI rendering could not run without Dalamud initialization.

Install/update through the existing custom repository:

`https://raw.githubusercontent.com/kaerlath/Sceneweaver/main/repo.json`

Open with `/sceneweaver` or `/swedit`. This remains a development prerelease. Corresponding source and third-party notices accompany the release.
