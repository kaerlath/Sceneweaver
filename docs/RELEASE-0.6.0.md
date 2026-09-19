# Sceneweaver 0.6.0 — VFX playback controls

Select a VFX object in Scene to edit **VFX playback (Intoner)**:

- Playback speed: 0–4x.
- Pause playback.
- Fade-in: 0–60 seconds.
- Replay after moving, rotating or scaling.
- Timed looping with a 1–60 second replay interval.
- Reset playback to Intoner's defaults.

Settings are editable for imported effects and newly added effects, saved in the canonical project and written to Intoner exports. Older canonical projects recover their settings from preserved Intoner source data. Changes participate in existing undo/redo and respect object locking.

Stagehand's VFX definition has no equivalent playback fields. Its export retains the effect but reports the playback limitation; live-display details report the same limitation. Sceneweaver's Stagehand-backed live display does not preview these playback changes. Keep the canonical project to retain them across editing sessions.

Compatibility reviewed against Intoner 0.2.5.0 (798b3960eaa29e1a9ccf61f6f1bcc9bc703421ae). The API contract and layout format remain unchanged from the previously inspected version.

Validation: 54 automated tests pass, including edited playback round trips, old canonical projects, explicit default overrides, Stagehand/live reporting and non-finite input rejection. Plugin and CLI Release builds pass; controls and playback still need verification in game. NuGet vulnerability checks could not reach the package server during the build.
