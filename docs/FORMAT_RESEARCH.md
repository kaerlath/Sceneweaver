# Verified APIs and persisted formats

Inspection date: 2026-09-16 local time. Pinned revisions are in `THIRD_PARTY.md`. Live repositories may change independently of this editor.

## Intoner

[Intoner](https://github.com/Abelfreyja/Intoner) places background models, housing furniture, VFX and lights. It has persistent scenes/layouts, folders, collections and caller-owned temporary sources.

[API contracts](https://github.com/Abelfreyja/Intoner.Api/tree/65930f89436faa5167e74d167a94e0bb745f9f00/Objects) report API version **1.0**. IPC labels begin `Intoner.Api.`. `State.GetInfo` exposes compatibility/capabilities; `Layouts.GetAll/Get/Create/SaveCurrent/SetDefault` manage saved layouts; `Scene.GetSnapshot` and `PersistentScene.GetSnapshot/Apply` query or replace state; `Objects.Create/Import/Update/Patch/Remove/Duplicate` handle individual objects. Temporary sources use source identity/session/revision and mutation status contracts. Many writes use expected revisions. These DTOs are not interchangeable with an exported layout root.

`Objects/Api/ObjectLayoutJsonSerializer.cs`, `Contracts/ObjectLayoutFileContracts.cs`, and `Services/Storage/PluginStoragePaths.cs` establish:

- Persistent layout files: `objects/layouts/<guid>.json` in Intoner's configuration directory.
- `DocumentKind: "object-layout"`, `FormatVersion: 2`, GUID `Id`, nonblank `Name`, positive `Revision`, UTC `ExportedAtUtc/CreatedAtUtc/UpdatedAtUtc`, `Objects` array and `Folders` array.
- Each object entry contains `FolderPath`, `Locked` and `Object` (`WorldObject`). The latter requires ID, name, enum kind, visibility, transform, timestamp, location, collection ID and kind-specific model data.
- Version 1 uses string folders plus a `FolderColors` dictionary. Version 2 uses `{Path, Color}` entries.
- Enum values are strings and numeric enum values are not accepted by Intoner's strict serializer. Unknown members and duplicate properties are disallowed. Unknown source data therefore belongs in the canonical project, not in an Intoner export.
- Intoner `object-autosave`, library prefab files and MessagePack clipboard transfers are separate formats, not supported inputs here.

## Stagehand

[API documentation](https://stagehandxiv.com/api/Stagehand.Api) and [source](https://github.com/universalconquistador/Stagehand) describe stages comprising background models, VFX, lights, weapons and sounds, with optional embedded modpacks.

Current source API revision is **1.2**. `StagehandApi.CreateIpcClient(IDalamudPluginInterface)` is generated through HQIPC for `IStagehandApi`. It offers API/location queries, local-stage listing and notifications, and temporary-stage creation/update/visibility/removal. `IStagehandApi.Local.cs` explicitly states that plugins cannot modify local stages or their auto-load conditions through this API. A dual-file editor therefore needs explicit filesystem exports; it must not pretend local-stage persistence is an IPC method.

`Stagehand.Definitions/StageDefinition.cs` is authoritative for JSON:

- Root contains `Info`, `Objects` dictionary keyed by stable arbitrary strings, and `EmbeddedModpacks` dictionary. There is no explicit numeric file-format version in this contract.
- Object discriminator is `Type`: `BgObject`, `VfxObject`, `Light`, `Weapon`, or `Sound`. Emit it first for the upstream polymorphic serializer.
- Common fields: `DisplayName`, `IsDisabled`, `Position`, `RotationPitchYawRollDegrees`, `Scale`, and `ModpackId`.
- Vectors are objects with X/Y/Z/W numeric components, not arrays.
- `ToDefinitionString()` is compact JSON suitable for IPC; it is not a binary/compressed save format. Files use indented JSON with the same definitions.
- Embedded modpacks contain resource maps with game/disk/embedded resource variants. Copying an object path alone does not reproduce these bindings.

## Field mapping and coordinates

Both implementations create rotations with `Quaternion.CreateFromYawPitchRoll(y, x, z)` after converting degrees to radians. Intoner's world `RotationDegrees` and Stagehand's local `RotationPitchYawRollDegrees` have compatible axis/order conventions. Canonical coordinates are local to the editor's optional stage placement; identity is the initial placement.

| Intoner | Stagehand |
|---|---|
| Name / Visible | DisplayName / !IsDisabled |
| BgObject.ModelPath / Transparency / DyeColor | ModelGamePath / Opacity / DyeColor |
| Vfx.VfxPath / Color | VfxGamePath / Color |
| Light WorldLight / AreaLight / SpotLight / FlatLight | Ambient / Point / Spot / Flat |
| Light FalloffType | FalloffFunction (same numeric order) |
| EnableMaterialReflection / EnableDynamicLighting | EnableSpecularHighlights / EnableDynamicShadows |
| EnableCharacterShadow / EnableObjectShadow | EnableCharacterShadows / EnableObjectShadows |
| Shape.Range / Falloff / LightAngle / FalloffAngle | Range / FalloffFactor / SpotLightAngleDegrees / AngularFalloffDegrees |
| Shape.AngleDegrees | FlatLightSkewAngleDegrees |
| Shadow.CharacterShadowRange / ShadowPlaneNear / ShadowPlaneFar | Same fields |

The light flag and angle mappings were checked against both runtime implementations, including conversion of flat-light degrees to engine radians. `Transparency` is an opacity multiplier (1 is opaque), not `1 - Opacity`.

Canonical raw documents and per-object payloads preserve unsupported data. They are intentionally redundant: the canonical object list determines deletion/current state; raw roots preserve source metadata for recovery. Native exports are reconstructed from the current list and the target's recognized fields. See the README compatibility matrix for deliberate omissions.
