# Sceneweaver 0.15.2 — Installed Penumbra mod browser dependency fix

Fixes Browse installed Penumbra mods / Refresh failing with a missing Microsoft.Extensions.Logging.Abstractions, Version=9.0.0.0 error. The bundled Penumbra.Api Luna helper references that library without bringing its dependency into our package automatically.

Explicitly includes Microsoft.Extensions.Logging.Abstractions 9.0.10 and its DependencyInjection.Abstractions dependency. Both provide assembly version 9.0.0.0. Adds build checks requiring both DLLs in the plugin output. Existing Penumbra IPC calls and mod import behavior are unchanged.

Validation: 94 core tests, release builds, and packaged dependency identity checks. In-game Refresh and collection-settings verification remain pending. Update Sceneweaver and retry Refresh with Penumbra enabled.
