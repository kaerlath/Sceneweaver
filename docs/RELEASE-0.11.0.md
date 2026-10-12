# Sceneweaver 0.11.0 — Effects nearby and selection highlighting

Open World picker → Effects nearby to list loaded VFX within the existing 200 m scan. Entries show the resource filename and distance; selecting one shows its full path and snapshot details. Search filters effect paths. Mod-specific paths remain inspectable even when their resources cannot be copied or previewed independently. The scanner keeps up to 2,000 models/effects combined, so this is not a complete inventory of every effect in a busy area. Scene ownership, creator labels and scene names are not available from these snapshots.

Highlight selected origin is enabled by default. A pulsing gold ring, crosshair and filename/distance label mark the selected object's source position in game while World picker is visible. It also works for model selections in Nearby. This overlay does not modify, restart or duplicate the selected object. Existing VFX preview controls remain separate.

The highlight resolves against the latest scan rather than the Recent snapshot. Missing selections are not highlighted, with a message to refresh; moving objects may need to be selected again because snapshots are identified by resource path and origin. With automatic refresh disabled, the UI explicitly states that the highlight uses the last scan. Off-screen origins show a turn-camera message. Closing or collapsing the editor, leaving World picker, logout, loading transitions and location changes suppress the overlay. Disable the checkbox to hide it manually.

Picking in world while Effects nearby is active limits selectable markers to effects. Highlights and markers indicate origins, not effect volumes or mesh boundaries; they may appear through walls or far from visible particles. The list can include another person's shared effects only when those objects are loaded on your client and exposed through the scanned scene tree.

Validation: 75 automated tests pass, including effect filtering with redirected mod paths and rejecting stale/mismatched selection snapshots. Plugin and CLI Release builds pass. Native scanning and the new overlay still require in-game verification.
