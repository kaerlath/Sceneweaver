# Sceneweaver 0.3.0 — Color previews

Model previews now load base-color textures referenced by the game's materials. Colors are enabled by default; **Show colors** switches back to shape shading, and wireframe remains available.

Texture coordinates and material assignments survive geometry sampling. Repeating UVs are clipped into tiles before rendering so the UI's texture sampler does not stretch them. Work is bounded for very dense geometry or extreme texture repetition, which falls back to shape shading. Missing materials and textures also retain the shape preview. The preview shows how many material textures are ready.

This is a base-color approximation under neutral preview lighting, not the full game renderer. Layered materials use their first recognized base-color map. Color-set dyes, material blends, normal/specular lighting, glass, glow, skeletal animation and some transparency effects may differ from the game. Local disk models with external materials may remain untextured. No game assets are distributed or spawned in the world.

Validation: Release build and 32 conversion/catalog/save tests passed. Texture smoke checks cover negative/repeating UVs, area preservation, safety limits, diffuse-map selection, relative material lookup and missing-material fallback. An installed game tree's actual model/material/texture chain was decoded and a standalone colored preview was rendered and visually inspected. A real five-object scene still round-trips without omissions. The updated texture rendering inside Dalamud still needs in-game validation.

Publish using `publish.ps1`, then update through the existing Dalamud repository and open `/sceneweaver`.
