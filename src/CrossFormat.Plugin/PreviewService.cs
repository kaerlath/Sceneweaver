using System.Collections.Concurrent;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using CrossFormat.Core;
using CrossFormat.Assets;

namespace CrossFormat.Plugin;

internal sealed class PreviewService(IDataManager data, ITextureProvider textures) : IDisposable
{
    private readonly ConcurrentDictionary<string, Task<MeshPreview>> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim workers = new(2);
    private volatile bool disposed;
    private float yaw = .65f, pitch = -.35f, zoom = 1;
    private Vector2 pan;
    private bool autoRotate, wireframe, showColors = true;
    private string lastAsset = "";
    private MeshPreview? projectedMesh;
    private float projectedYaw = float.NaN, projectedPitch = float.NaN;
    private Triangle[] projected = [];
    private sealed record Triangle(Vector2 A, Vector2 B, Vector2 C, uint Color, uint LitWhite, float Depth, int Index, TextureTriangle[] TextureParts);

    public void ResetView() { yaw = .65f; pitch = -.35f; zoom = 1; pan = Vector2.Zero; }
    public void DrawViewOptions()
    {
        ImGui.Checkbox("Auto rotate", ref autoRotate);
        ImGui.SameLine(); ImGui.Checkbox("Wireframe", ref wireframe);
        ImGui.Checkbox("Show colors", ref showColors);
    }

    public void Draw(SceneObject asset, Vector2 size, bool interactive = false)
    {
        size = Vector2.Max(size, new Vector2(30));
        if (lastAsset != asset.AssetPath) { lastAsset = asset.AssetPath; ResetView(); }
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
        bool hovered = ImGui.IsItemHovered();
        if (interactive)
        {
            var io = ImGui.GetIO();
            if (autoRotate) yaw += Math.Min(io.DeltaTime, .1f) * .35f;
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 0))
            { yaw += io.MouseDelta.X * .01f; pitch = Math.Clamp(pitch + io.MouseDelta.Y * .01f, -1.55f, 1.55f); }
            if (hovered)
            {
                zoom = Math.Clamp(zoom * MathF.Exp(io.MouseWheel * .12f), .15f, 10);
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Right, 0)) pan += io.MouseDelta;
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) ResetView();
            }
        }
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, 0xFF211910, 8);
        draw.PushClipRect(origin, origin + size, true);
        try
        {
            for (int n = 1; n < 8; n++)
            {
                float x = size.X * n / 8, y = size.Y * n / 8;
                draw.AddLine(origin + new Vector2(x, 0), origin + new Vector2(x, size.Y), 0x222B9CAB);
                draw.AddLine(origin + new Vector2(0, y), origin + new Vector2(size.X, y), 0x222B9CAB);
            }
            if (asset.Kind == AssetKind.Light)
            {
                var color = asset.Light["Color"]?.Deserialize<Vector3>(ProjectJson.Options) ?? Vector3.One;
                var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(Vector3.Clamp(color, Vector3.Zero, Vector3.One), 1));
                draw.AddCircleFilled(origin + size / 2, Math.Min(size.X, size.Y) * .16f, tint);
                return;
            }
            if (!asset.AssetPath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
            {
                draw.AddText(origin + new Vector2(16, size.Y / 2 - 20), 0xFFC6C6C6,
                    asset.Kind == AssetKind.Vfx ? "VFX preview is not yet available" : asset.Kind == AssetKind.Sound ? "Audio preview is not yet available" : "No model geometry for this asset");
                return;
            }
            if (cache.Count >= 24 && !cache.ContainsKey(asset.AssetPath))
                foreach (var key in cache.Where(p => p.Value.IsCompleted).Select(p => p.Key).Take(8)) cache.TryRemove(key, out _);
            if (cache.Count >= 32 && !cache.ContainsKey(asset.AssetPath)) { draw.AddText(origin + new Vector2(16, 16), 0xFFC6C6C6, "Waiting for earlier previews..."); return; }
            var task = cache.GetOrAdd(asset.AssetPath, path => Task.Run(async () =>
            {
                await workers.WaitAsync();
                try { return disposed ? new([], [], "Preview closed") : Load(path); }
                finally { workers.Release(); }
            }));
            if (!task.IsCompletedSuccessfully) { draw.AddText(origin + new Vector2(16, 16), 0xFFC6C6C6, "Loading model from game data..."); return; }
            var mesh = task.Result;
            if (mesh.Error != null)
            {
                draw.AddText(origin + new Vector2(16, 16), 0xFF8DAFED, "Preview unavailable");
                draw.AddText(origin + new Vector2(16, 44), 0xFFC6C6C6, "Hover for details");
                if (hovered) ImGui.SetTooltip(mesh.Error);
                return;
            }
            if (projectedMesh != mesh || projectedYaw != yaw || projectedPitch != pitch)
            {
                projectedMesh = mesh; projectedYaw = yaw; projectedPitch = pitch;
                var rotation = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
                var vertices = mesh.Vertices.Select(v => Vector3.Transform(v, rotation)).ToArray();
                List<Triangle> triangles = new(mesh.Indices.Length / 3);
                for (int t = 0; t + 2 < mesh.Indices.Length; t += 3)
                {
                    var a = vertices[mesh.Indices[t]]; var b = vertices[mesh.Indices[t + 1]]; var c = vertices[mesh.Indices[t + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    if (normal.LengthSquared() < 1e-12f) continue;
                    float shade = .24f + .72f * MathF.Abs(Vector3.Dot(Vector3.Normalize(normal), Vector3.Normalize(new Vector3(-1, 2, -3))));
                    var parts = mesh.TextureTiles[t / 3];
                    triangles.Add(new(new(a.X, -a.Y), new(b.X, -b.Y), new(c.X, -c.Y),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(shade * .8f, shade * .9f, shade, 1)),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(new Vector3(.55f + shade * .45f), 1)), (a.Z + b.Z + c.Z) / 3, t, parts));
                }
                projected = triangles.OrderBy(t => t.Depth).ToArray();
            }
            Vector2 Screen(Vector2 v) => origin + size / 2 + pan + v * (MathF.Min(size.X, size.Y) * .43f * zoom);
            // Resolve once per material per frame; the host owns/caches these texture wrappers.
            var handles = new ImTextureID[mesh.Materials.Length];
            int loadedColors = 0;
            if (showColors && !wireframe)
                for (int m = 0; m < mesh.Materials.Length; m++)
                {
                    if (mesh.Materials[m].TexturePath is not { } texturePath) continue;
                    try
                    {
                        var wrap = textures.GetFromGame(texturePath).GetWrapOrDefault();
                        if (wrap != null) { handles[m] = wrap.Handle; loadedColors++; }
                    }
                    catch { /* Texture failure leaves the geometry visible. */ }
                }
            foreach (var triangle in projected)
            {
                if (wireframe) draw.AddTriangle(Screen(triangle.A), Screen(triangle.B), Screen(triangle.C), 0xFFDAC083, 1);
                else
                {
                    int t = triangle.Index;
                    int material = mesh.TriangleMaterials[t / 3];
                    if (showColors && triangle.TextureParts.Length > 0 && !handles[material].Equals(default(ImTextureID)))
                    {
                        // A degenerate second triangle lets ImGui render a textured triangle safely.
                        Vector2 Point(TextureVertex v) => Screen(triangle.A + (triangle.B - triangle.A) * v.Position.X + (triangle.C - triangle.A) * v.Position.Y);
                        foreach (var part in triangle.TextureParts)
                            draw.AddImageQuad(handles[material], Point(part.A), Point(part.B), Point(part.C), Point(part.C), part.A.UV, part.B.UV, part.C.UV, part.C.UV, triangle.LitWhite);
                    }
                    else draw.AddTriangleFilled(Screen(triangle.A), Screen(triangle.B), Screen(triangle.C), triangle.Color);
                }
            }
            if (showColors && !wireframe)
            {
                draw.AddText(origin + new Vector2(10, 10), 0xFFE2D3BC, $"Color textures: {loadedColors}/{mesh.Materials.Length}");
                if (hovered && mesh.Materials.Any(m => m.Note != null))
                    ImGui.SetTooltip(string.Join("\n", mesh.Materials.Select(m => m.Note).Where(n => n != null).Distinct()));
            }
            draw.AddText(origin + new Vector2(10, size.Y - 24), 0xFFE2D3BC,
                $"{mesh.Size.X:0.#} x {mesh.Size.Y:0.#} x {mesh.Size.Z:0.#} game units" + (mesh.Simplified ? "  |  simplified" : ""));
        }
        finally { draw.PopClipRect(); }
    }

    private MeshPreview Load(string path)
    {
        try
        {
            var file = Path.IsPathRooted(path) ? data.GameData.GetFileFromDisk<MdlFile>(path) : data.GetFile<MdlFile>(path);
            return file == null ? new([], [], "This catalog path is not available in the installed game data.")
                : ModelGeometry.Decode(file, p => data.GetFile<MtrlFile>(p), path);
        }
        catch (Exception e) { return new([], [], e.Message); }
    }
    public void Dispose() { disposed = true; cache.Clear(); projected = []; projectedMesh = null; }
}
