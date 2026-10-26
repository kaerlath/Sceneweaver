# Sceneweaver 0.14.0 — Intoner browser and format 3

Open /sceneweaver > Intoner scenes > Refresh Intoner scenes. Select a saved layout or temporary source, then Import Intoner scene as new project. Imports reread the source, prompt before replacing unsaved work, clear old save destinations, and do not change or display the source scene. Ordinary room objects are not scanned.

Intoner version 3 files now import with editing groups, world-space group pivots converted into Sceneweaver's parent-relative representation, visibility, locks, folders, and preserved raw group metadata. Empty groups survive. Version 3 imports export back to version 3; older layouts and ordinary Stagehand conversions retain version 2 output. Nested Stagehand-only groups still flatten with conversion diagnostics. Unsupported object fields remain in the canonical project.

Saved-layout browsing uses Intoner.Api.Layouts.GetAll/Get. Temporary browsing uses Scene.GetLoadedLayouts. Local mod collections use Sharing.BuildSource; a read-only adapter accesses Intoner's temporary source store and managed memory resource lookup for received collections. It never calls native resource readers or mutates the source. Temporary sources are not assumed to be PlayerSync sources.

File replacements and memory bytes are embedded into Sceneweaver's canonical project and Stagehand modpacks. Game-path swaps remain swaps. Missing collections/resources stop the import. Bounds: 64 MB per copied resource, 128 MB combined copied file/memory bytes, existing 256 MB output limit. Large source serialization can delay refresh/capture. Resource copying/compression occurs outside the framework thread.

Intoner layout JSON does not embed collections. Portable captured mods are retained in Sceneweaver/Stagehand; Intoner export reports unsupported mod bindings rather than silently exporting an unmodded appearance. Furniture and unknown object kinds may lack Stagehand equivalents and produce normal conversion reports.

Source/API reviewed: Intoner 833e73e, Intoner.Api c453ab0. Internal adapter shape checked against installed Intoner 0.2.5.0. The adapter may need updates when Intoner internals change. No upstream code or installed plugin was modified.

Validation: 88 automated tests passed, covering version 3 world pose, group pivot/metadata, cross-format transforms, empty groups, invalid references, embedded memory bytes, and missing-resource failures. Plugin and CLI release builds checked. Live IPC discovery and capture still require in-game verification.
