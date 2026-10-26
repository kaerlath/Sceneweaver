# Sceneweaver 0.15.0 — Insert saved assemblies and New scene dialog

Open your destination project, choose **Insert scene as group...**, and select a saved Sceneweaver, Stagehand, or Intoner JSON file. All entries are added under a selected group named after the source scene. Move, rotate, or scale this group to position the whole assembly. Undo removes the insertion. The destination project name and save locations stay unchanged.

The assembly is centered on your character when logged in, or the stage origin otherwise. Its pivot is the average of its object origins, not its geometry bounds. Source world orientation, relative placement and size are preserved. Existing nested groups remain nested. Each insertion gets new object and modpack IDs, so a saved assembly can be inserted repeatedly. Disk mod resources are embedded; missing resources or invalid references abort insertion before changing the destination. Copy limits remain 64 MB per resource, 128 MB combined copied bytes, and 256 MB final project size. Large imports can briefly pause the editor.

Canonical saves preserve source object metadata and archive source-level metadata. Known Intoner group/furniture references are remapped. Unknown ID references cannot be remapped automatically; unsupported types and native export limitations still appear in conversion reports. Stagehand retains grouped assemblies; Intoner exports may flatten groups according to its format capabilities.

**New** now displays a prominent modal when the current scene has unsaved changes. Choose Keep editing to save first, or Discard changes and continue to start a blank project. Starting a new scene clears the previous save paths, stops Sceneweaver's live display and previews, and drops pending browser imports. Previously the confirmation appeared inline and was easy to miss.

Validation: 92 automated tests pass, including a 71-piece assembly under transformed stage roots, repeated insertion, nested group/furniture references, portable mod resources and failure without destination mutation. Plugin and CLI release builds checked. In-game picker, group controls and modal interaction still require verification.
