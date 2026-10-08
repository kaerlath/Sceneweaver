using System.Text.Json;
using System.Text.Json.Nodes;
using Stagehand.Definitions.Objects;

namespace CrossFormat.Core;

public static class LightSettings
{
    public static LightDefinition Read(SceneObject o)
    {
        var light = o.Light.Deserialize<LightDefinition>(ProjectJson.Options) ?? new();
        // Older canonical projects may have kept this property only in the source payload.
        if (!o.Light.ContainsKey(nameof(light.ProjectedTextureGamePath)))
            light.ProjectedTextureGamePath = o.StagehandSource[nameof(light.ProjectedTextureGamePath)]?.GetValue<string>() ?? "";
        return light;
    }

    public static void Write(SceneObject o, LightDefinition light)
    {
        var merged = (JsonObject)o.Light.DeepClone();
        foreach (var p in JsonSerializer.SerializeToNode(light, ProjectJson.Options)!.AsObject())
            merged[p.Key] = p.Value?.DeepClone();
        o.Light = merged;
    }

    public static string TexturePath(string value) => string.IsNullOrWhiteSpace(value) ? "" :
        Path.GetExtension(value.Trim()).Equals(".tex", StringComparison.OrdinalIgnoreCase)
            ? ModResources.GamePath(value.Trim()) : throw new InvalidDataException("Choose a game texture path ending in .tex.");
}
