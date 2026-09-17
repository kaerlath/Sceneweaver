using System.Numerics;
using Lumina.Data.Files;
using Lumina.Models.Models;

namespace CrossFormat.Assets;

public sealed record MeshPreview(Vector3[] Vertices, int[] Indices, string? Error = null);
public static class ModelGeometry
{
    public static MeshPreview Decode(MdlFile file)
    {
        var model = new Model(file, Model.ModelLod.High, 1);
        List<Vector3> vertices = []; List<int> indices = [];
        foreach (var mesh in model.Meshes)
        {
            if (vertices.Count + mesh.Vertices.Length > 1_000_000) throw new InvalidDataException("Model exceeds preview vertex limit.");
            int offset = vertices.Count;
            foreach (var v in mesh.Vertices)
            {
                var p = v.Position ?? Vector4.Zero;
                if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) throw new InvalidDataException("Invalid model position.");
                vertices.Add(new(p.X, p.Y, p.Z));
            }
            for (int i = 0; i + 2 < mesh.Indices.Length && indices.Count < 18000; i += 3)
                if (mesh.Indices[i] < mesh.Vertices.Length && mesh.Indices[i + 1] < mesh.Vertices.Length && mesh.Indices[i + 2] < mesh.Vertices.Length)
                { indices.Add(offset + mesh.Indices[i]); indices.Add(offset + mesh.Indices[i + 1]); indices.Add(offset + mesh.Indices[i + 2]); }
        }
        if (vertices.Count == 0 || indices.Count == 0) return new([], [], "No renderable geometry.");
        var min = vertices.Aggregate(Vector3.Min); var max = vertices.Aggregate(Vector3.Max); var center = (min + max) / 2;
        float radius = MathF.Max(.001f, vertices.Max(v => Vector3.Distance(v, center)));
        return new(vertices.Select(v => (v - center) / radius).ToArray(), indices.ToArray());
    }
}
