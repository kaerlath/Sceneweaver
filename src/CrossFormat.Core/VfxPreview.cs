using System.Numerics;
using System.Text.Json;

namespace CrossFormat.Core;

public static class VfxPreview
{
    public static LiveSceneSnapshot Build(SceneProject source, SceneObject asset, Vector3 position, float rotation, float distance, float scale)
    {
        if (asset.Kind != AssetKind.Vfx || string.IsNullOrWhiteSpace(asset.AssetPath)) throw new InvalidDataException("Select a VFX asset to preview.");
        if (!float.IsFinite(distance) || distance < 0 || !float.IsFinite(scale) || scale <= 0 || !float.IsFinite(rotation)) throw new InvalidDataException("Invalid preview placement.");
        var copy = JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(asset, ProjectJson.Options), ProjectJson.Options)!;
        copy.ParentId = null; copy.Visible = true; copy.Position = Vector3.Zero; copy.RotationDegrees = Vector3.Zero; copy.Scale = Vector3.One;
        var packs = new System.Text.Json.Nodes.JsonObject();
        string packId = ModResources.PackId(asset);
        if (packId.Length > 0) packs[packId] = ModResources.Packs(source)[packId]?.DeepClone()
            ?? throw new InvalidDataException("The selected effect's modpack is missing.");
        var project = new SceneProject { Name = "Sceneweaver VFX preview", Objects = [copy], StagehandRoot = new System.Text.Json.Nodes.JsonObject { ["EmbeddedModpacks"] = packs },
            StageTranslation = position + Vector3.Transform(Vector3.UnitZ * distance, Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation)),
            StageUniformScale = scale };
        return LiveScene.Build(project);
    }
}
