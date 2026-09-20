# Sceneweaver 0.7.0 — Penumbra import and mod updates

The Mods tab now browses installed Penumbra mods by name. Refresh the list, select a mod, and optionally copy options from the currently selected Penumbra collection, including inherited/temporary choices reported by Penumbra. These operations only read Penumbra; they do not change its configuration. Package and folder imports remain available without Penumbra.

Single, Multi and Combining option groups are supported. Combining groups select the resource container for the chosen combination. IMC groups and metadata manipulations are explicitly rejected because Stagehand resource packs cannot represent them.

Project mods display their source directory and version. Update from the installed source or select a replacement package/folder, prepare the files, review removed paths and affected scene objects, then apply the update. Existing object bindings and the project mod's display name remain intact; unknown pack fields are preserved. Resources absent from the new import are removed, so dependent game resources may revert to their original appearance. The entire update can be undone. Preview caches and active live display refresh after the update.

Saved option choices are restored by name, including imports made before this release. Changed or missing option names produce an error with instructions to reset and review the defaults. Groups newly added by the mod author use their defaults. Switching projects discards pending imports to prevent applying an old import to the new scene.

Validation: 57 automated tests pass. Added coverage for all two-option Combining selections, malformed containers, named settings, mod replacement, preserved bindings/unknown fields, removed resources, snapshot recovery and upstream Stagehand serialization. Release plugin/CLI builds pass. Uses Penumbra.Api 5.19.1, matching Stagehand 0.4.15; installed-mod browsing and collection copying still need an in-game check.
