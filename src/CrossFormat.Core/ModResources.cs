using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;

namespace CrossFormat.Core;

public readonly record struct ModResourcePath(string Path, bool Disk);
public static class ModResources
{
    public static string GamePath(string path)
    {
        path = path.Replace('\\', '/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(p => p is ".." or "." or "")) throw new InvalidDataException("Invalid game resource path: " + path);
        return path;
    }
    public static AssetKind? Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".mdl" => AssetKind.BgObject, ".avfx" => AssetKind.Vfx, ".scd" => AssetKind.Sound, _ => null };
    public static JsonObject Packs(SceneProject scene) => scene.StagehandRoot["EmbeddedModpacks"] as JsonObject ?? new();
    public static string Attach(SceneProject scene, JsonObject pack)
    {
        _ = pack.Deserialize<EmbeddedModpackDefinition>(StageDefinition.StandardSerializerOptions) ?? throw new InvalidDataException("Invalid modpack.");
        scene.StagehandRoot["EmbeddedModpacks"] ??= new JsonObject();
        string id = Guid.NewGuid().ToString("N");
        scene.StagehandRoot["EmbeddedModpacks"]![id] = pack.DeepClone();
        return id;
    }
    public static string PackId(SceneObject asset) => asset.StagehandSource["ModpackId"]?.GetValue<string>() ?? "";
    public static void Replace(SceneProject scene, string id, JsonObject pack)
    {
        var packs = (JsonObject)Packs(scene).DeepClone();
        var previous = packs[id] as JsonObject ?? throw new InvalidDataException("The mod to update is no longer in this project.");
        _ = pack.Deserialize<EmbeddedModpackDefinition>(StageDefinition.StandardSerializerOptions) ?? throw new InvalidDataException("Invalid modpack.");
        var updated = (JsonObject)previous.DeepClone();
        foreach (var field in pack) updated[field.Key] = field.Value?.DeepClone();
        updated["DisplayName"] = previous["DisplayName"]?.DeepClone();
        packs[id] = updated;
        // Replacing the container also invalidates preview caches. Object bindings retain their IDs.
        scene.StagehandRoot["EmbeddedModpacks"] = packs;
    }
    public static SceneObject CreateObject(string packId, string gamePath) => new()
    {
        Name = Path.GetFileNameWithoutExtension(gamePath), AssetPath = gamePath,
        Kind = Kind(gamePath) ?? throw new InvalidDataException("Choose a model, VFX or sound resource."),
        StagehandSource = new JsonObject { ["ModpackId"] = packId },
    };
    public static ModResourcePath Resolve(JsonObject? pack, string gamePath, string cacheDirectory)
    {
        if (pack == null) return new(gamePath, Path.IsPathRooted(gamePath));
        var resources = pack["ModdedResources"] as JsonObject;
        var node = resources?.FirstOrDefault(p => string.Equals(p.Key, gamePath, StringComparison.OrdinalIgnoreCase)).Value;
        if (node == null) return new(gamePath, false);
        var resource = node.Deserialize<ModResourceDefinition>(StageDefinition.StandardSerializerOptions);
        if (resource is DiskModResourceDefinition disk)
        {
            if (!Path.IsPathFullyQualified(disk.SourceDiskPath) || !File.Exists(disk.SourceDiskPath)) throw new FileNotFoundException("Mod resource is missing: " + disk.SourceDiskPath);
            if (new FileInfo(disk.SourceDiskPath).Length > ModImport.MaxResourceBytes) throw new InvalidDataException("Mod preview resource exceeds 64 MB.");
            return new(disk.SourceDiskPath, true);
        }
        if (resource is GameModResourceDefinition game) return new(GamePath(game.SourceGamePath), false);
        if (resource is not EmbeddedModResourceDefinition embedded) throw new InvalidDataException("Unsupported mod resource type.");
        byte[] bytes;
        using (var input = new MemoryStream(embedded.CompressedDataBytes))
        {
            if (embedded.CompressionScheme == ModCompressionScheme.None) bytes = ReadBounded(input, ModImport.MaxResourceBytes);
            else if (embedded.CompressionScheme == ModCompressionScheme.Zlib) { using var zlib = new ZLibStream(input, CompressionMode.Decompress); bytes = ReadBounded(zlib, ModImport.MaxResourceBytes); }
            else throw new InvalidDataException("Unsupported mod compression.");
        }
        // Only content-addressed files are written; an imported game path is never a disk destination.
        string extension = Path.GetExtension(GamePath(gamePath));
        if (extension.Length > 12 || extension.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.')) extension = ".bin";
        Directory.CreateDirectory(cacheDirectory);
        var destination = Path.Combine(cacheDirectory, Convert.ToHexString(SHA256.HashData(bytes)) + extension);
        if (!File.Exists(destination))
        {
            string temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temp, bytes); try { File.Move(temp, destination); } catch (IOException) when (File.Exists(destination)) { } }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        return new(destination, true);
    }
    public static byte[] ReadBounded(Stream stream, int limit)
    {
        using var output = new MemoryStream(); byte[] buffer = new byte[81920];
        int read; while ((read = stream.Read(buffer)) != 0) { if (output.Length + read > limit) throw new InvalidDataException("Mod resource exceeds its size limit."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
}
