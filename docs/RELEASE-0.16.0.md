# Sceneweaver 0.16.0 — Intoner 0.3 compatibility and editing

Reads Intoner layout formats 1, 2 and 3. All Intoner exports now use format 3 and API 2 location data, targeting Intoner 0.3+. Older single-world locations migrate to the same individual-world restriction; new world/data-center selections survive canonical and native round trips. Mixed or missing location schemas and invalid world selections are rejected rather than silently widened. Existing canonical projects with legacy location payloads remain readable.

Intoner BG collider definitions and BG/furniture camera-collision flags survive native exports. In **Intoner > Object settings**, select an object and expand **Intoner location and collision** to edit world/data-center IDs and housing location fields, toggle camera collision, inspect/remove an imported collider, or adjust imported analytic collider bounds, offsets and scale. Collider rotation is in radians. Collider discovery/creation from game assets is not included. These settings apply in Intoner; Stagehand live display does not gain Intoner collision behavior.

Projected light texture paths now import/export through Intoner as well as Stagehand. Use the existing Light parameters controls. Embedded Stagehand texture packs are not automatically installed into Intoner; conversion reports explain this resource-binding limitation.

Intoner library prefab JSON versions 1 and 2 can be opened or inserted using **Insert scene as group...**. Saved world transforms and supported BG/furniture/VFX/light payloads are converted to layout objects; furniture attachment IDs are retained and remapped on insertion. Missing transforms and invalid attachment references stop import. Original prefab metadata remains in the canonical project. Saves produce layouts, not Intoner library prefab files. Inserted assemblies use Sceneweaver's mean-origin pivot, which differs from Intoner's prefab placement anchor.

The scene browser's existing API endpoints remain unchanged. Checked installed Intoner 0.3.0.0 for the session scope, temporary-source store and managed resource lookup used by the read-only collection adapter. Runtime capture still requires in-game verification.

Source contracts updated from Intoner.Api b5d3f0c; implementation compared with Intoner v0.3.0.0. Validation: 99 automated tests pass, covering legacy/current world selections, malformed locations, collision preservation, projected textures, prefab import and insertion, and existing save/conversion behavior. Plugin and CLI builds and the original failing location fixture checked. In-game UI, mod capture, collision behavior and projected-light rendering remain unverified.

## Destination workspaces

Shared placement, appearance and light controls remain in **Scene**. **Stagehand** contains live display and stage placement, embedded mod bindings, the mod library and received stages. **Intoner** contains location, collision and VFX playback settings, plus the layouts/source browser. All tabs edit the same scene and share the selected object; opening a destination tab does not start its stage display.

The top bar now explicitly reports whether Sceneweaver's separate local editing display is on and offers **Hide in game**. Other plugin and sync displays remain controlled by their respective plugins. This helps distinguish editing previews from already visible stages. The reorganized UI requires in-game visual verification.
