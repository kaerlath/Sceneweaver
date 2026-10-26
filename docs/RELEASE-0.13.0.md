# Sceneweaver 0.13.0 — Received stages browser

Open /sceneweaver and choose **Received stages**. With PlayerSync and Stagehand loaded and a subscribed stage displayed, click **Refresh received stages**, select a stage, and choose **Import as new project**. Save the independent copy through Save & export.

The browser matches PlayerSync active-stage IDs to Stagehand temporary definitions. Ordinary game furniture and Sceneweaver's own temporary stages are excluded. Import takes a fresh snapshot, preserves stage placement and author metadata, assigns a new project ID, and embeds resolved disk mod resources. Missing files or size-limit failures abort without replacing the current project. Unsaved work still prompts before replacement. Import does not display another copy automatically or modify the synced stage.

This is an experimental read-only managed reflection adapter, inspected against PlayerSync 1.15.5.9, Stagehand 0.5.5.0, and Dalamud 15.0.3.9. These plugins do not expose the needed received-stage data through public IPC. Updates may break the adapter; failures appear in the browser instead of attempting native memory access. No network interception, server downloads, authentication access, cache writes, or foreign stage mutation is used.

Only currently loaded, non-hidden PlayerSync stages are included. Imports preserve the received appearance, including compressed substitutes, rather than reconstructing unused options in an original PMP. Limits: 100 stages, 64 million UTF-16 code units of definitions, 64 MB per copied mod file, 128 MB total copied mod bytes, existing 256 MB project save limit. Refresh and serialization occur on the framework thread; disk copying and compression run in the background. Very large definitions can briefly delay refresh.

Validation: 82 automated tests passed, including cache eviction independence, resource extraction, Stagehand export/import, placement preservation, and failure handling for missing/relative/oversized resources and invalid rotation. Release plugin build succeeded. Live plugin discovery and received-stage import still require in-game verification; no claim of in-game validation is made.
