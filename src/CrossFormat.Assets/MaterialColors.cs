using System.Text;
using Lumina.Data.Files;

namespace CrossFormat.Assets;

public sealed record PreviewMaterial(string Name, string? TexturePath, string? Note = null);

/// <summary>Reads base-color maps only; normal, mask and specular maps are not visible surface colors.</summary>
public static class MaterialColors
{
    public static PreviewMaterial Resolve(string modelPath, string name, Func<string, MtrlFile?>? load)
    {
        if (load == null || string.IsNullOrWhiteSpace(name)) return new(name, null, "No material loader.");
        foreach (var candidate in Candidates(modelPath, name))
        {
            try
            {
                var material = load(candidate);
                if (material == null) continue;
                var paths = material.TextureOffsets.Select(t => ReadString(material.Strings, t.Offset)).ToArray();
                // Standard s_diffuse sampler. Background shaders also use multiple named base maps.
                var diffuse = material.Samplers.FirstOrDefault(s => s.SamplerId == 0x115306BE);
                string? texture = material.Samplers.Any(s => s.SamplerId == 0x115306BE) && diffuse.TextureIndex < paths.Length
                    ? paths[diffuse.TextureIndex] : paths.FirstOrDefault(IsBaseColor);
                if (string.IsNullOrWhiteSpace(texture) || !texture.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
                    return new(candidate, null, "No supported base-color texture; showing shape shading.");
                return new(candidate, texture, paths.Count(IsBaseColor) > 1 ? "Layered material: first base-color texture shown." : null);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
            { /* A missing or unsupported material must not prevent the shape preview. */ }
        }
        return new(name, null, "Material not found; showing shape shading.");
    }

    public static bool IsBaseColor(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        return name.EndsWith("_d.tex") || name.EndsWith("_b.tex") || name.Contains("_diff") || name.Contains("_base") || name.StartsWith("base_");
    }

    private static string ReadString(byte[] bytes, int offset)
    {
        if (offset < 0 || offset >= bytes.Length) return "";
        int end = Array.IndexOf(bytes, (byte)0, offset);
        return Encoding.UTF8.GetString(bytes, offset, (end < 0 ? bytes.Length : end) - offset).Replace('\\', '/');
    }

    public static IEnumerable<string> Candidates(string modelPath, string name)
    {
        name = name.Replace('\\', '/').TrimStart('/'); modelPath = modelPath.Replace('\\', '/');
        if (name.Length == 0) yield break;
        yield return name;
        string dir = modelPath.Contains('/') ? modelPath[..modelPath.LastIndexOf('/')] : "";
        string parent = dir.Contains('/') ? dir[..dir.LastIndexOf('/')] : "";
        string leaf = name[(name.LastIndexOf('/') + 1)..];
        foreach (var folder in new[] { dir, parent + "/material", parent + "/material/v0001" })
            if (folder.Length > 0) yield return folder + "/" + leaf;
    }
}
