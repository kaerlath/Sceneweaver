using System.Numerics;
using Lumina.Data.Files;
using Lumina.Models.Models;

namespace CrossFormat.Assets;

public sealed record MeshPreview(Vector3[] Vertices, int[] Indices, string? Error = null, Vector3 Size = default, bool Simplified = false)
{
    public Vector2[] UVs { get; init; } = [];
    public int[] TriangleMaterials { get; init; } = [];
    public PreviewMaterial[] Materials { get; init; } = [];
    public TextureTriangle[][] TextureTiles { get; init; } = [];
}
public static class ModelGeometry
{
    public static MeshPreview Decode(MdlFile file, Func<string, MtrlFile?>? loadMaterial = null, string modelPath = "")
    {
        var model = new Model(file, Model.ModelLod.High, 1);
        List<Vector3> vertices = []; List<int> indices = [];
        List<Vector2> uvs = []; List<int> triangleMaterials = []; List<PreviewMaterial> materials = [];
        var materialIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Sample throughout very large models, never truncate the first meshes.
        int totalTriangles = model.Meshes.Sum(m => m.Indices.Length / 3);
        int step = Math.Max(1, (int)Math.Ceiling(totalTriangles / 40000d));
        int triangleIndex = 0;
        foreach (var mesh in model.Meshes)
        {
            var name = mesh.Material?.MaterialPath ?? "";
            if (!materialIndices.TryGetValue(name, out var materialIndex))
            {
                materialIndex = materials.Count; materialIndices[name] = materialIndex;
                materials.Add(MaterialColors.Resolve(modelPath, name, loadMaterial));
            }
            if (vertices.Count + mesh.Vertices.Length > 1_000_000) throw new InvalidDataException("Model exceeds preview vertex limit.");
            int offset = vertices.Count;
            foreach (var v in mesh.Vertices)
            {
                var p = v.Position ?? Vector4.Zero;
                if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) throw new InvalidDataException("Invalid model position.");
                vertices.Add(new(p.X, p.Y, p.Z));
                var uv = v.UV ?? Vector4.Zero;
                uvs.Add(float.IsFinite(uv.X) && float.IsFinite(uv.Y) ? new(uv.X, uv.Y) : Vector2.Zero);
            }
            for (int i = 0; i + 2 < mesh.Indices.Length; i += 3, triangleIndex++)
            {
                if (triangleIndex % step != 0) continue;
                if (mesh.Indices[i] < mesh.Vertices.Length && mesh.Indices[i + 1] < mesh.Vertices.Length && mesh.Indices[i + 2] < mesh.Vertices.Length)
                { indices.Add(offset + mesh.Indices[i]); indices.Add(offset + mesh.Indices[i + 1]); indices.Add(offset + mesh.Indices[i + 2]); triangleMaterials.Add(materialIndex); }
            }
        }
        if (vertices.Count == 0 || indices.Count == 0) return new([], [], "No renderable geometry.");
        var min = vertices.Aggregate(Vector3.Min); var max = vertices.Aggregate(Vector3.Max); var center = (min + max) / 2;
        float radius = MathF.Max(.001f, vertices.Max(v => Vector3.Distance(v, center)));
        var tiles = new TextureTriangle[triangleMaterials.Count][];
        int tileBudget = 120_000;
        for (int t = 0; t < tiles.Length; t++)
        {
            // Store barycentric positions: tile boundaries do not change when the view rotates.
            tiles[t] = tileBudget <= 0 || materials[triangleMaterials[t]].TexturePath == null ? [] : TextureTiling.Split(
                new(Vector2.Zero, uvs[indices[t * 3]]), new(Vector2.UnitX, uvs[indices[t * 3 + 1]]), new(Vector2.UnitY, uvs[indices[t * 3 + 2]]));
            tileBudget -= tiles[t].Length;
        }
        return new(vertices.Select(v => (v - center) / radius).ToArray(), indices.ToArray(), Size: max - min, Simplified: step > 1)
        { UVs = uvs.ToArray(), TriangleMaterials = triangleMaterials.ToArray(), Materials = materials.ToArray(), TextureTiles = tiles };
    }
}
