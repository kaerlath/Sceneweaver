# Sceneweaver 0.10.0 — Stagehand 2 compatibility and scene tools

Stagehand 0.5.4 and later save format version 2. Sceneweaver now opens and exports this format, while continuing to import older version 0/1 stages. Vendored Stagehand definitions are updated to 0.5.5. Newer, unknown format versions still fail with a clear update message.

Lights now support projected texture paths. Spot/flat lights need 2D textures; point lights need cubemaps; ambient lights do not project textures. Open Light parameters to enter a .tex game path, choose a game example, or select a texture from an imported modpack. Use live display to see the projection. Texture dependencies and unknown source fields remain in canonical and Stagehand saves. Intoner exports the light without its projection and reports this limitation, including when the projection uses a modpack. Other unsupported mod-bound assets continue to be omitted from Intoner with a report. Stagehand 0.5.4+ is required for the new exports and live display.

Right-click Position, Pitch / yaw / roll, or Scale to copy/paste. Clipboard vectors work with Stagehand 0.5.5. Values use parent-relative coordinates; group scale paste requires positive equal components. Invalid clipboard data reports an error without changing the project.

Reveal selected expands ancestor groups and scrolls to the selected object. Groups containing the selected descendant have an asterisk beside their names.

The Scene toolbar now offers Select, Move, Rotate and Scale. Show the scene in game, select an object, and use its on-screen handles. Move and Rotate support Local/World axes and separate optional snapping increments. Scale follows the object's axes; group scaling is always uniform. Group and stage transforms are accounted for, and each continuous drag creates one undo step. Switching away from Scene, hiding live display, closing the editor or entering a loading transition disables the handles. Locked and hidden objects cannot be manipulated. World picker selection remains origin-marker based and separate from these handles.

The world scanner now checks for exactly the loaded resource state before reading model details. Mod-update review also counts lights affected by removed texture paths.

Validation: 73 automated tests pass, including nested textured-light round trips and loss reporting, legacy formats, clipboard rejection, nested world-transform reversal and uniform group transforms. Release plugin and CLI builds pass. The new native handles, snapping, mouse capture, clipboard menus and projected lighting require in-game verification; compilation and automated tests do not validate the running game renderer.
