using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;

namespace CrossFormat.Core;

public sealed record ReceivedStageSnapshot(string Id, string Name, string Author, string Definition,
    Vector3 Translation, Quaternion Rotation, float Scale);

public static class ReceivedStages
{
    // Never keep cache references in the imported project: they may be evicted at any time.
    public static SceneProject Import(ReceivedStageSnapshot snapshot)
    {
        if (snapshot.Definition.Length > ProjectFiles.MaxInputBytes / 2) throw new InvalidDataException("Received definition is too large.");
        var scene = SceneCodec.Import(snapshot.Definition);
        scene.Id = Guid.NewGuid(); scene.Name = string.IsNullOrWhiteSpace(snapshot.Name) ? scene.Name + " (copy)" : snapshot.Name + " (copy)";
        var q = snapshot.Rotation;
        if (!float.IsFinite(q.LengthSquared()) || q.LengthSquared() < 0.000001f) throw new InvalidDataException("Invalid received stage rotation.");
        scene.StageTranslation = snapshot.Translation; scene.StageRotationDegrees = TransformMath.Degrees(Quaternion.Normalize(q)); scene.StageUniformScale = snapshot.Scale;
        ProjectJson.Validate(scene);
        long total = 0;
        var embeddedByPath = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in ModResources.Packs(scene))
        {
            if (pack.Value?["ModdedResources"] is not JsonObject resources) continue;
            foreach (var entry in resources.ToArray())
            {
                if (entry.Value?.Deserialize<ModResourceDefinition>(StageDefinition.StandardSerializerOptions) is not DiskModResourceDefinition disk) continue;
                if (!Path.IsPathFullyQualified(disk.SourceDiskPath)) throw new InvalidDataException("Received mod path is not absolute.");
                string path = Path.GetFullPath(disk.SourceDiskPath);
                if (!embeddedByPath.TryGetValue(path, out var embedded))
                {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (file.Length > ModImport.MaxResourceBytes || total + file.Length > 128L * 1024 * 1024) throw new InvalidDataException("Received mods exceed the import limit (64 MB per file / 128 MB total).");
                    var bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes); total += bytes.Length;
                    embedded = JsonSerializer.SerializeToNode<ModResourceDefinition>(new EmbeddedModResourceDefinition
                    { CompressionScheme = ModCompressionScheme.Zlib, CompressedDataBytes = EmbeddedModResourceDefinition.CompressDataBytes(bytes, ModCompressionScheme.Zlib) }, StageDefinition.StandardSerializerOptions)!;
                    embeddedByPath[path] = embedded;
                }
                resources[entry.Key] = embedded.DeepClone();
            }
        }
        scene.ImportNotes.Add($"Snapshot of received PlayerSync stage {snapshot.Id}. Author: {snapshot.Author}. Imported {DateTime.UtcNow:O}; independent of future sync updates. Cached assets embedded as displayed, possibly compressed alternatives.");
        ProjectJson.Validate(scene);
        if (JsonSerializer.SerializeToUtf8Bytes(scene, ProjectJson.Options).Length > ProjectFiles.MaxInputBytes) throw new InvalidDataException("Imported project exceeds the save size limit.");
        return scene;
    }
}
