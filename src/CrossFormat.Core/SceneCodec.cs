using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Intoner.Objects.Api;
using Stagehand.Definitions;
using Stagehand.Definitions.Objects;

namespace CrossFormat.Core;

public static class SceneCodec
{
    private static readonly JsonSerializerOptions IntonerOptions = new(ProjectJson.Options)
    { RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true };
    private static JsonObject Copy(JsonObject value) => (JsonObject)value.DeepClone();
    private static JsonObject Node<T>(T value) => JsonSerializer.SerializeToNode(value, ProjectJson.Options)!.AsObject();
    private static Vector3 V(ObjectVector3 v) => new(v.X, v.Y, v.Z);
    private static Vector4 V(ObjectVector4 v) => new(v.X, v.Y, v.Z, v.W);
    private static ObjectVector3 I(Vector3 v) => new(v.X, v.Y, v.Z);
    private static ObjectVector4 I(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    public static SceneProject Import(string json)
    {
        using var check = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Expected a JSON object.");
        if (root["DocumentKind"]?.GetValue<string>() == "cross-format-project") return ProjectJson.Load(json);
        SceneProject s;
        if (root["DocumentKind"]?.GetValue<string>() == "object-layout") s = ImportIntoner(root);
        else if (root["Info"] is JsonObject && root["Objects"] is JsonObject) s = ImportStagehand(root);
        else throw new InvalidDataException("Not a canonical project, Intoner object-layout, or Stagehand stage definition.");
        ProjectJson.Validate(s);
        return s;
    }

    private static SceneProject ImportIntoner(JsonObject root)
    {
        int version = root["FormatVersion"]?.GetValue<int>() ?? 0;
        if (version is not (1 or 2)) throw new InvalidDataException($"Unsupported Intoner version {version}.");
        var s = new SceneProject { IntonerRoot = Copy(root), Name = root["Name"]!.GetValue<string>(), Id = root["Id"]!.GetValue<Guid>(), Revision = root["Revision"]!.GetValue<long>(), CreatedAtUtc = root["CreatedAtUtc"]!.GetValue<DateTime>() };
        if (s.Revision <= 0 || s.CreatedAtUtc == default || root["Folders"] is not JsonArray) throw new InvalidDataException("Invalid Intoner layout metadata.");
        _ = root["ExportedAtUtc"]!.GetValue<DateTime>(); _ = root["UpdatedAtUtc"]!.GetValue<DateTime>();
        if (version == 1)
        {
            var colors = root["FolderColors"] as JsonObject ?? throw new InvalidDataException("Version 1 needs FolderColors.");
            s.IntonerRoot["Folders"] = new JsonArray(root["Folders"]!.AsArray().Select(n => (JsonNode)new JsonObject { ["Path"] = n!.GetValue<string>(), ["Color"] = colors[n.GetValue<string>()]?.DeepClone() }).ToArray());
            s.IntonerRoot.Remove("FolderColors"); s.IntonerRoot["FormatVersion"] = 2;
        }
        foreach (var entry in root["Objects"]!.AsArray())
        {
            var wrapper = entry?.AsObject() ?? throw new InvalidDataException("Null Intoner object.");
            var raw = wrapper["Object"]!.AsObject();
            var kind = raw["Kind"]?.GetValue<string>();
            if (kind is not ("BgObject" or "Vfx" or "Light" or "Furniture"))
            {
                s.Objects.Add(new SceneObject { Name = raw["Name"]?.GetValue<string>() ?? "Unknown", Kind = AssetKind.Unknown, IntonerSource = Copy(wrapper) });
                continue;
            }
            var w = raw.Deserialize<WorldObject>(IntonerOptions) ?? throw new InvalidDataException("Invalid Intoner object.");
            if (w.Id == Guid.Empty || w.CreatedAtUtc == default) throw new InvalidDataException("Intoner object lacks persistent identity.");
            var o = new SceneObject
            {
                Id = w.Id, Name = w.Name, Kind = Enum.Parse<AssetKind>(kind), Visible = w.Visible,
                Position = V(w.Transform.Position), RotationDegrees = V(w.Transform.RotationDegrees), Scale = V(w.Transform.Scale),
                Folder = wrapper["FolderPath"]!.GetValue<string>(), Locked = wrapper["Locked"]!.GetValue<bool>(), IntonerSource = Copy(wrapper),
            };
            switch (o.Kind)
            {
                case AssetKind.BgObject:
                    var b = w.Model.BgObject ?? throw new InvalidDataException("Missing BgObject payload.");
                    o.AssetPath = b.ModelPath; o.Opacity = b.Transparency; o.Color = V(b.DyeColor); break;
                case AssetKind.Vfx:
                    var v = w.Model.Vfx ?? throw new InvalidDataException("Missing Vfx payload.");
                    o.AssetPath = v.VfxPath; o.Color = V(v.Color); break;
                case AssetKind.Furniture:
                    var f = w.Model.Furniture ?? throw new InvalidDataException("Missing Furniture payload.");
                    o.AssetPath = f.SharedGroupPath; o.Opacity = f.Transparency; o.Color = V(f.Color.CustomColor); break;
                case AssetKind.Light:
                    o.Light = Node(ToStageLight(w.Model.Light ?? throw new InvalidDataException("Missing Light payload."))); break;
            }
            s.Objects.Add(o);
        }
        s.ImportNotes.Add("Intoner world coordinates imported with an identity Stagehand placement. Set Stage placement only when needed.");
        return s;
    }

    private static SceneProject ImportStagehand(JsonObject root)
    {
        var s = new SceneProject { StagehandRoot = Copy(root), Name = root["Info"]?["Name"]?.GetValue<string>() ?? "Imported stage" };
        if (string.IsNullOrWhiteSpace(s.Name)) s.Name = "Imported stage";
        foreach (var (key, value) in root["Objects"]!.AsObject())
        {
            var raw = value?.AsObject() ?? throw new InvalidDataException("Null Stagehand object.");
            var type = raw["Type"]?.GetValue<string>() ?? throw new InvalidDataException("Stagehand object lacks Type.");
            var kind = type switch { "BgObject" => AssetKind.BgObject, "VfxObject" => AssetKind.Vfx, "Light" => AssetKind.Light, "Sound" => AssetKind.Sound, "Weapon" => AssetKind.Weapon, _ => AssetKind.Unknown };
            var o = new SceneObject { StagehandId = key, Kind = kind, StagehandSource = Copy(raw) };
            if (kind == AssetKind.Unknown) { o.Name = raw["DisplayName"]?.GetValue<string>() ?? key; s.Objects.Add(o); continue; }
            // Discriminator must precede other properties for upstream's serializer.
            var ordered = new JsonObject { ["Type"] = type };
            foreach (var p in raw.Where(p => p.Key != "Type")) ordered[p.Key] = p.Value?.DeepClone();
            var d = ordered.Deserialize<ObjectDefinition>(StageDefinition.StandardSerializerOptions) ?? throw new InvalidDataException("Invalid Stagehand object.");
            o.Name = d.DisplayName; o.Visible = !d.IsDisabled; o.Position = d.Position; o.RotationDegrees = d.RotationPitchYawRollDegrees; o.Scale = d.Scale;
            switch (d)
            {
                case BgObjectDefinition b: o.AssetPath = b.ModelGamePath; o.Opacity = b.Opacity; o.Color = b.DyeColor; break;
                case VfxObjectDefinition v: o.AssetPath = v.VfxGamePath; o.Color = v.Color; break;
                case LightDefinition l: o.Light = Node(l); break;
                case SoundObjectDefinition a: o.AssetPath = a.SoundGamePath; break;
            }
            s.Objects.Add(o);
        }
        s.ImportNotes.Add("Stagehand saves local coordinates. Use Stage placement to set the world origin for Intoner exports; identity is the default.");
        return s;
    }

    public static ExportResult Export(SceneProject s, SceneFormat format)
    {
        ProjectJson.Validate(s);
        foreach (var o in s.Objects)
            if (o.Kind is AssetKind.BgObject or AssetKind.Vfx or AssetKind.Furniture or AssetKind.Sound && string.IsNullOrWhiteSpace(o.AssetPath))
                throw new InvalidDataException($"{o.Name}: choose an asset path before exporting.");
        return format == SceneFormat.Intoner ? ExportIntoner(s) : ExportStagehand(s);
    }

    private static ExportResult ExportStagehand(SceneProject s)
    {
        var root = Copy(s.StagehandRoot);
        root["Info"] ??= new JsonObject(); root["Info"]!["Name"] = s.Name;
        root["EmbeddedModpacks"] ??= new JsonObject();
        var objects = new JsonObject(); root["Objects"] = objects;
        List<ConversionIssue> issues = [];
        foreach (var o in s.Objects)
        {
            var modId = ModResources.PackId(o);
            if (modId.Length > 0 && !ModResources.Packs(s).ContainsKey(modId)) throw new InvalidDataException($"{o.Name}: the referenced modpack is missing. Reimport it or remove the binding before Stagehand export.");
            if (o.Kind is AssetKind.Furniture or AssetKind.Unknown) { issues.Add(new(o.Name, "No supported Stagehand object type; retained in canonical project.", true)); continue; }
            if (Path.IsPathRooted(o.AssetPath)) { issues.Add(new(o.Name, "A disk asset needs a Stagehand modpack with material/texture bindings; omitted rather than writing an invalid game path.", true)); continue; }
            ObjectDefinition d = o.Kind switch
            {
                AssetKind.BgObject => new BgObjectDefinition { ModelGamePath = o.AssetPath, Opacity = o.Opacity, DyeColor = o.Color },
                AssetKind.Vfx => new VfxObjectDefinition { VfxGamePath = o.AssetPath, Color = o.Color },
                AssetKind.Light => o.Light.Deserialize<LightDefinition>(ProjectJson.Options) ?? new(),
                AssetKind.Sound => new SoundObjectDefinition { SoundGamePath = o.AssetPath },
                AssetKind.Weapon => new WeaponDefinition(),
                _ => throw new InvalidDataException("Unsupported object kind."),
            };
            // Keep every source-specific field, replacing only canonical editable fields.
            var generated = JsonSerializer.SerializeToNode(d, StageDefinition.StandardSerializerOptions)!.AsObject();
            var raw = new JsonObject { ["Type"] = generated["Type"]!.DeepClone() };
            foreach (var p in o.StagehandSource.Where(p => p.Key != "Type")) raw[p.Key] = p.Value?.DeepClone();
            foreach (var p in generated)
                if (p.Key != "Type" && (o.Kind is not (AssetKind.Sound or AssetKind.Weapon) || !raw.ContainsKey(p.Key))) raw[p.Key] = p.Value?.DeepClone();
            // Preserve ModpackId, including when Light's canonical data has a default empty ID.
            raw["ModpackId"] = o.StagehandSource["ModpackId"]?.DeepClone() ?? JsonValue.Create("");
            raw["DisplayName"] = o.Name; raw["IsDisabled"] = !o.Visible;
            raw["Position"] = Node(o.Position); raw["RotationPitchYawRollDegrees"] = Node(o.RotationDegrees); raw["Scale"] = Node(o.Scale);
            if (o.Kind == AssetKind.Sound) raw["SoundGamePath"] = o.AssetPath;
            objects[string.IsNullOrEmpty(o.StagehandId) ? o.Id.ToString() : o.StagehandId] = raw;
            if (o.IntonerSource.Count > 0) issues.Add(new(o.Name, "Intoner location, collection, folder, lock, rain and playback-only settings remain in the canonical project."));
        }
        if (s.StageTranslation != Vector3.Zero || s.StageRotationDegrees != Vector3.Zero || s.StageUniformScale != 1)
            issues.Add(new(s.Name, "Stagehand file stores local coordinates. Apply the canonical Stage placement in Stagehand when loading; placement is baked only into Intoner output."));
        // Prove emitted current-type documents can be read by the upstream definition serializer.
        _ = root.Deserialize<StageDefinition>(StageDefinition.StandardSerializerOptions) ?? throw new InvalidDataException("Stagehand export validation failed.");
        return new(root, issues);
    }

    private static ExportResult ExportIntoner(SceneProject s)
    {
        List<ConversionIssue> issues = [];
        var now = DateTime.UtcNow;
        var root = new JsonObject
        {
            ["DocumentKind"] = "object-layout", ["FormatVersion"] = 2, ["Id"] = s.Id,
            ["Name"] = s.Name, ["Revision"] = s.Revision, ["CreatedAtUtc"] = s.CreatedAtUtc,
            ["UpdatedAtUtc"] = now, ["ExportedAtUtc"] = now,
        };
        var objects = new JsonArray(); root["Objects"] = objects;
        var folders = new JsonArray(); root["Folders"] = folders;
        var folderPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in s.IntonerRoot["Folders"] as JsonArray ?? [])
        {
            var path = f?["Path"]?.GetValue<string>() ?? "";
            if (folderPaths.Add(path)) folders.Add(new JsonObject { ["Path"] = path, ["Color"] = f?["Color"]?.DeepClone() });
        }
        foreach (var o in s.Objects)
        {
            if (o.Kind is AssetKind.Weapon or AssetKind.Sound or AssetKind.Unknown) { issues.Add(new(o.Name, "No supported Intoner object type; retained in canonical project.", true)); continue; }
            if (o.StagehandSource["ModpackId"] is JsonValue mod && !string.IsNullOrEmpty(mod.GetValue<string>()))
            {
                issues.Add(new(o.Name, "Stagehand modpack resource bindings cannot be resolved by Intoner; object omitted, complete data retained in canonical project.", true)); continue;
            }
            var source = o.IntonerSource["Object"]?.Deserialize<WorldObject>(IntonerOptions);
            var model = source?.Model ?? new WorldObjectModelData();
            switch (o.Kind)
            {
                case AssetKind.BgObject: model = model with { BgObject = new(o.AssetPath, o.Opacity, I(o.Color), model.BgObject?.IsCoveredFromRain ?? false) }; break;
                case AssetKind.Vfx: model = model with { Vfx = (model.Vfx ?? new VfxModelData(o.AssetPath, I(o.Color))) with { VfxPath = o.AssetPath, Color = I(o.Color) } }; break;
                case AssetKind.Furniture:
                    if (model.Furniture == null) { issues.Add(new(o.Name, "Furniture needs an imported Intoner shared-group payload.", true)); continue; }
                    model = model with { Furniture = model.Furniture with { SharedGroupPath = o.AssetPath, Transparency = o.Opacity, Color = model.Furniture.Color with { CustomColor = I(o.Color) } } }; break;
                case AssetKind.Light: model = model with { Light = ToIntonerLight(o.Light.Deserialize<LightDefinition>(ProjectJson.Options) ?? new()) }; break;
            }
            var t = TransformMath.ToWorld(s, o);
            var location = source?.CreatedIn ?? new ObjectLocationData(0, "", (uint)Math.Max(0, s.StagehandRoot["Info"]?["IntendedTerritoryType"]?.GetValue<int>() ?? 0), "", 0, 0, 0, 0);
            var w = new WorldObject(o.Id, o.Name, Enum.Parse<WorldObjectKind>(o.Kind.ToString()), o.Visible,
                new(I(t.Position), I(t.Rotation), I(t.Scale)), source?.CreatedAtUtc ?? s.CreatedAtUtc, location, source?.CollectionId ?? "", model);
            var serialized = Node(w);
            // Validate against the actual upstream DTO with unknown members forbidden.
            var strict = new JsonSerializerOptions(IntonerOptions) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
            _ = serialized.Deserialize<WorldObject>(strict);
            objects.Add(new JsonObject { ["FolderPath"] = o.Folder, ["Locked"] = o.Locked, ["Object"] = serialized });
            if (folderPaths.Add(o.Folder)) folders.Add(new JsonObject { ["Path"] = o.Folder, ["Color"] = null });
            if (o.StagehandSource.Count > 0) issues.Add(new(o.Name, "Stagehand metadata and fields without Intoner equivalents remain in the canonical project."));
            if (o.IntonerSource.Count > 0) issues.Add(new(o.Name, "Intoner export uses the verified schema; any unknown source fields remain in the canonical project."));
        }
        return new(root, issues);
    }

    private static LightDefinition ToStageLight(LightModelData l) => new()
    {
        Shape = (LightShape)((int)l.LightType - 1), FalloffFunction = (LightFalloffFunction)l.FalloffType,
        Color = V(l.Color), Intensity = l.Intensity,
        EnableSpecularHighlights = l.Flags.EnableMaterialReflection, EnableDynamicShadows = l.Flags.EnableDynamicLighting,
        EnableCharacterShadows = l.Flags.EnableCharacterShadow, EnableObjectShadows = l.Flags.EnableObjectShadow,
        Range = l.Shape.Range, FalloffFactor = l.Shape.Falloff, SpotLightAngleDegrees = l.Shape.LightAngle,
        AngularFalloffDegrees = l.Shape.FalloffAngle, FlatLightSkewAngleDegrees = new(l.Shape.AngleDegrees.X, l.Shape.AngleDegrees.Y),
        CharacterShadowRange = l.Shadow.CharacterShadowRange, ShadowPlaneNear = l.Shadow.ShadowPlaneNear, ShadowPlaneFar = l.Shadow.ShadowPlaneFar,
    };
    private static LightModelData ToIntonerLight(LightDefinition l) => new(I(l.Color), (ObjectLightType)((int)l.Shape + 1), (ObjectLightFalloffType)l.FalloffFunction,
        new(l.EnableSpecularHighlights, l.EnableDynamicShadows, l.EnableCharacterShadows, l.EnableObjectShadows), l.Intensity,
        new(l.Range, l.FalloffFactor, l.SpotLightAngleDegrees, l.AngularFalloffDegrees, new(l.FlatLightSkewAngleDegrees.X, l.FlatLightSkewAngleDegrees.Y)),
        new(l.CharacterShadowRange, l.ShadowPlaneNear, l.ShadowPlaneFar));
}
