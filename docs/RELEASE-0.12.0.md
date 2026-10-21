# Sceneweaver 0.12.0 — Nearby scene capture

World picker now includes Capture nearby as scene. Choose a radius from 1 to 200 game units and Scan for capture. A fresh scan on the framework thread records the character's position and freezes nearby model/VFX snapshots for review. Selection uses a three-dimensional radius around object origins, not camera visibility or mesh boundaries.

Supported entries are selected by default. Filter paths, select/deselect matching entries, or check individual objects. Click a row to inspect it with the existing picker details/highlight. Mod-specific paths are shown as unsupported and cannot be included. Scanning still has a 2,000-entry / 50,000-node traversal cap; busy areas may be incomplete. The capture does not identify ownership or distinguish map props from shared stages.

Create new scene from selection constructs a canonical project preserving positions relative to the capture origin, rotations, scales, colors and opacity. Existing unsaved work uses the usual discard/keep-editing prompt. New captures are marked unsaved and previous save destinations are cleared. Save the canonical project and export through Save & export.

Use Place stage origin here to move the entire arrangement to your current character position. Unlike Show scene here, this anchors the stage origin rather than the selected object. Captured projects retain the original world translation for Show saved placement / Intoner output; Stagehand exports store local coordinates as before.

The review snapshot is cleared on project/location changes and logout: create and save the scene before traveling. Capturing alone does not spawn copies. Creating the project stops the previous Sceneweaver live stage; displaying the capture remains an explicit action.

This reconstructs supported loaded visuals only. Terrain/collision, NPCs, area weather/lighting, original labels/groups, and embedded mod dependencies are not captured. VFX restart on loading. Standard game paths can also be redirected by other mods, so matching appearance is not guaranteed.

Validation: 77 automated tests pass, including frozen radius selection, unsupported-path rejection, save/export placement and relocation. Plugin and CLI Release builds pass. Capture scanning, review UI and live placement still require in-game verification.
