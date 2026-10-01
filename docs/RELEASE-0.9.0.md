# Sceneweaver 0.9.0 — World picker

Open the World picker tab to browse loaded background models and VFX near your character. Nearby is sorted by distance; Recent keeps up to 50 selected snapshots for the current location/session. Filter by resource path, inspect placement, copy the path, preview supported resources or save them as favorites.

Choose Pick in world and click a circle at an object's origin. Escape, right-click, leaving the tab or closing the editor cancels picking. Markers are not mesh-accurate hit tests: they can show through walls, and large objects may have origins far from their visible surfaces. Use Nearby for ambiguous or overlapping objects. At most 500 filtered markers are drawn.

Add copy at original placement accounts for the editor's stage transform and preserves the captured rotation, scale, tint and opacity. Place copy at my character uses the existing live Stagehand placement action. Picking and previewing alone never add an object to the project.

The scanner reads on the framework thread, copies values into managed snapshots and retains no native object pointers between scans. It limits traversal to 50,000 visits/128 levels and keeps up to 2,000 objects within 200 m. Scanning is suspended during loading transitions and while the picker is closed. It refreshes every three seconds, or every half-second during picking; automatic refresh can be disabled. Location changes/logout clear the selection/history.

This does not import another plugin's mod resources. Redirected, embedded-memory and disk resource paths are shown for inspection but blocked from direct copy/preview. Even ordinary game paths may have been modified by another plugin: use Mods to import/bind the original package for matching appearance.

Validation: 66 automated tests pass, including copying world placement through rotated/scaled stages, resource-path rejection, and bounded recent history. Plugin and CLI Release builds pass. Native scene traversal, origin-marker selection, mouse capture and transitions still require in-game validation; automated tests do not exercise the running game's memory. The VFX resource chain follows Stagehand 0.5.2's inspected layout.
