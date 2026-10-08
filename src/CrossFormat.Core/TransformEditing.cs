using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrossFormat.Core;

public static class TransformEditing
{
    public static void Check(Vector3 v)
    {
        if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
            throw new InvalidDataException("Transform values must be finite numbers.");
    }

    // Matches Stagehand's vector clipboard, so values can be exchanged between editors.
    public static string Copy(Vector3 value) => new JsonObject
    {
        ["Type"] = "Vector3DataTransferFragment",
        ["Value"] = JsonSerializer.SerializeToNode(value, ProjectJson.Options)
    }.ToJsonString();

    public static Vector3 Paste(string text, bool uniform = false)
    {
        if (text.Length > 4096) throw new InvalidDataException("Clipboard does not contain a transform vector.");
        try
        {
            var node = JsonNode.Parse(text, documentOptions: new() { AllowDuplicateProperties = false });
            if (node?["Type"]?.GetValue<string>() != "Vector3DataTransferFragment") throw new JsonException();
            var v = node["Value"]!;
            var result = new Vector3(v["X"]!.GetValue<float>(), v["Y"]!.GetValue<float>(), v["Z"]!.GetValue<float>());
            Check(result);
            if (uniform && (result.X <= 0 || result.X != result.Y || result.X != result.Z))
                throw new InvalidDataException("Groups need positive, equal X/Y/Z scale values.");
            return result;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException or FormatException)
        { throw new InvalidDataException("Copy a position, rotation or scale vector in Sceneweaver or Stagehand first.", e); }
    }

    public static void SetWorld(SceneProject scene, SceneObject item, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Check(position); Check(scale);
        if (!float.IsFinite(rotation.LengthSquared()) || rotation.LengthSquared() < .000001f)
            throw new InvalidDataException("Invalid rotation.");
        var parentRotation = TransformMath.Rotation(scene.StageRotationDegrees);
        float parentScale = scene.StageUniformScale;
        foreach (var parent in SceneHierarchy.Ancestors(scene, item).Reverse())
        { parentRotation *= TransformMath.Rotation(parent.RotationDegrees); parentScale *= parent.Scale.X; }
        var localPosition = SceneHierarchy.ToParentPosition(scene, item, LiveScene.LocalPosition(scene, position));
        var localRotation = TransformMath.Degrees(Quaternion.Inverse(parentRotation) * rotation);
        var localScale = scale / parentScale;
        Check(localPosition); Check(localRotation); Check(localScale);
        if (item.Kind == AssetKind.Group && (localScale.X <= 0 || MathF.Abs(localScale.Y - localScale.X) > .0001f || MathF.Abs(localScale.Z - localScale.X) > .0001f))
            throw new InvalidDataException("Groups need positive uniform scale.");
        item.Position = localPosition; item.RotationDegrees = localRotation;
        item.Scale = item.Kind == AssetKind.Group ? new(localScale.X) : localScale;
    }
}
