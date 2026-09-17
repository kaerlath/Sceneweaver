using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Models.Models;
using CrossFormat.Core;
using CrossFormat.Assets;

namespace CrossFormat.Plugin;

internal sealed class PreviewService(IDataManager data, ITextureProvider textures) : IDisposable
{
    private readonly ConcurrentDictionary<string, Task<MeshPreview>> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim workers = new(2);
    private volatile bool disposed;

    public void Draw(SceneObject asset, Vector2 size)
    {
        if (!string.IsNullOrWhiteSpace(asset.PreviewImagePath) && File.Exists(asset.PreviewImagePath))
        {
            var image = textures.GetFromFile(asset.PreviewImagePath).GetWrapOrDefault();
            if (image != null) { ImGui.Image(image.Handle, size); return; }
        }
        if (asset.IconId > 0)
        {
            var icon = textures.GetFromGameIcon(asset.IconId).GetWrapOrDefault();
            if (icon != null) { ImGui.Image(icon.Handle, size); return; }
        }
        var origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("preview", size);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, 0xFF242020, 6);
        if (asset.Kind == AssetKind.Light)
        {
            var color = asset.Light["Color"]?.Deserialize<Vector3>(ProjectJson.Options) ?? Vector3.One;
            var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(Vector3.Clamp(color, Vector3.Zero, Vector3.One), 1));
            draw.AddCircleFilled(origin + size / 2, size.X * .16f, tint);
            for (int n = 0; n < 8; n++) { var v = new Vector2(MathF.Cos(n * MathF.PI / 4), MathF.Sin(n * MathF.PI / 4)); draw.AddLine(origin + size / 2 + v * size.X * .22f, origin + size / 2 + v * size.X * .35f, tint, 2); }
            return;
        }
        if (!asset.AssetPath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
        {
            draw.AddText(origin + new Vector2(7, 8), 0xFFAFAFAF, asset.Kind.ToString());
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("No model thumbnail for this asset. Assign a PNG/JPG preview in the inspector.");
            return;
        }
        if (cache.Count >= 128 && !cache.ContainsKey(asset.AssetPath))
        {
            foreach (var key in cache.Where(p => p.Value.IsCompleted).Select(p => p.Key).Take(32)) cache.TryRemove(key, out _);
        }
        var task = cache.GetOrAdd(asset.AssetPath, path => Task.Run(async () =>
        {
            await workers.WaitAsync();
            try { return disposed ? new([], [], "Preview closed") : Load(path); }
            finally { workers.Release(); }
        }));
        if (!task.IsCompletedSuccessfully) { draw.AddText(origin + new Vector2(7, 8), 0xFFAFAFAF, "Loading..."); return; }
        var mesh = task.Result;
        if (mesh.Error != null) { draw.AddText(origin + new Vector2(7, 8), 0xFFAFAFAF, "No preview"); if (ImGui.IsItemHovered()) ImGui.SetTooltip(mesh.Error); return; }
        var rotation = Matrix4x4.CreateRotationY(.65f) * Matrix4x4.CreateRotationX(-.35f);
        var vertices = mesh.Vertices.Select(v => Vector3.Transform(v, rotation)).ToArray();
        var triangles = Enumerable.Range(0, mesh.Indices.Length / 3).OrderBy(n => (vertices[mesh.Indices[n * 3]].Z + vertices[mesh.Indices[n * 3 + 1]].Z + vertices[mesh.Indices[n * 3 + 2]].Z) / 3);
        Vector2 Screen(Vector3 v) => origin + size / 2 + new Vector2(v.X, -v.Y) * (MathF.Min(size.X, size.Y) * .43f);
        draw.PushClipRect(origin, origin + size, true);
        foreach (var t in triangles)
        {
            var a = vertices[mesh.Indices[t * 3]]; var b = vertices[mesh.Indices[t * 3 + 1]]; var c = vertices[mesh.Indices[t * 3 + 2]];
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.LengthSquared() < 1e-12f) continue;
            var shade = .3f + .65f * MathF.Abs(Vector3.Dot(Vector3.Normalize(normal), Vector3.Normalize(new Vector3(-1, 2, -3))));
            draw.AddTriangleFilled(Screen(a), Screen(b), Screen(c), ImGui.ColorConvertFloat4ToU32(new Vector4(shade * .8f, shade * .9f, shade, 1)));
        }
        draw.PopClipRect();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Model geometry preview (untextured). No object is spawned in the world.");
    }

    private MeshPreview Load(string path)
    {
        try
        {
            var file = Path.IsPathRooted(path) ? data.GameData.GetFileFromDisk<MdlFile>(path) : data.GetFile<MdlFile>(path);
            if (file == null) return new([], [], "Model not found in game data or on disk.");
            return ModelGeometry.Decode(file);
        }
        catch (Exception e) { return new([], [], e.Message); }
    }
    public void Dispose() { disposed = true; cache.Clear(); }
}
