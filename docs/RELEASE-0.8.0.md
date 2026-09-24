# Sceneweaver 0.8.0 — Groups and animated VFX previews

## Stagehand compatibility

Updated the Stagehand definition contracts to 0.5.1. Stagehand files with no format version (legacy) or version 1 are supported; newer versions are rejected with an update message.

Imported groups and their children appear in an expandable object tree. Edit the group's position, rotation, visibility and uniform scale; child transforms remain relative to their group. Use Add group and Parent group to organize objects without moving them. Duplicate copies the entire subtree, Delete group and children removes it, and Ungroup keeps the children and their placement. Existing undo/redo applies to these edits. Multi-selection and world-space gizmos are not included in this release.

Stagehand export preserves nested groups, child keys and unknown source fields. Intoner export flattens the hierarchy, composes group/stage transforms, and applies inherited visibility; its report explains that group structure is retained in the canonical project. Move to my character and Show scene here account for child transforms.

## VFX previews

Select a VFX in Discover assets, Mods or the Scene inspector and choose Preview VFX in game. The actual animated effect plays near your character through Stagehand; restart and stop buttons and distance/scale controls are provided. Automatic preview on selection is optional and off initially. Embedded mod resources are included. Playback-only Intoner settings still do not apply to Stagehand rendering.

The preview owns a separate temporary stage and can coexist with live scene editing. It never adds an object to the saved project. It stops when switching effects or panels, closing/collapsing the editor, switching projects, leaving the location, logging out or unloading the plugin. Failed cleanup is retained for retry. Stagehand must be enabled; use 0.5.1 or newer for grouped scenes. This is an in-game effect, not an animation in the model preview panel.

## Validation

63 automated tests pass, including nested group round trips through Stagehand's serializer, inherited visibility, Intoner transform composition, reparenting, duplicate subtrees, invalid hierarchy/version rejection, preview mod preservation and independent temporary-stage cleanup. Plugin and CLI Release builds pass. Real in-game preview rendering, UI layout and grouped scene placement still need verification in the running game. Package-server vulnerability checks may warn when network access is unavailable.
