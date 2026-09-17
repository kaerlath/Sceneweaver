using System.Numerics;
using System.Runtime.InteropServices;
using CrossFormat.Assets;
using Lumina;
using Lumina.Data.Files;

internal static class ColorPreviewSmoke
{
    public static void Run(GameData game, string output)
    {
        // Negative and repeated UVs must preserve area and remain inside the UI sampler's unit tile.
        var parts = TextureTiling.Split(new(Vector2.Zero, new(-.5f, -.5f)), new(Vector2.UnitX, new(2.5f, -.5f)), new(Vector2.UnitY, new(-.5f, 2.5f)));
        float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        float area = parts.Sum(p => MathF.Abs(Cross(p.B.Position - p.A.Position, p.C.Position - p.A.Position)) / 2);
        if (MathF.Abs(area - .5f) > .0001f || parts.SelectMany(p => new[] { p.A, p.B, p.C }).Any(v => v.UV.X < 0 || v.UV.Y < 0 || v.UV.X > 1 || v.UV.Y > 1)) throw new Exception("Texture tiling changed geometry or escaped tile bounds.");
        if (TextureTiling.Split(new(Vector2.Zero, Vector2.Zero), new(Vector2.UnitX, new(1000, 0)), new(Vector2.UnitY, new(0, 1000))).Length != 0) throw new Exception("Tiling safety limit failed.");
        if (MaterialColors.IsBaseColor("test_n.tex") || MaterialColors.IsBaseColor("test_s.tex") || !MaterialColors.IsBaseColor("test_d.tex")) throw new Exception("Non-color texture classification failed.");
        var failedMaterial = MaterialColors.Resolve("bg/model/a.mdl", "/missing.mtrl", _ => throw new IOException("missing"));
        if (failedMaterial.TexturePath != null) throw new Exception("Missing material fallback failed.");
        var strings = System.Text.Encoding.UTF8.GetBytes("bg/test_n.tex\0bg/test_color.tex\0");
        var synthetic = new MtrlFile
        {
            Strings = strings,
            TextureOffsets = [new() { Offset = 0 }, new() { Offset = 14 }],
            Samplers = [new() { SamplerId = 0x115306BE, TextureIndex = 1 }],
        };
        var resolved = MaterialColors.Resolve("bg/test/model/a.mdl", "/a.mtrl", p => p == "bg/test/material/a.mtrl" ? synthetic : null);
        if (resolved.TexturePath != "bg/test_color.tex") throw new Exception("Standard diffuse sampler or relative material lookup failed.");
        Console.WriteLine("Texture tests passed: UV area/negative tiling, safety budget, map classification and missing-material fallback.");

        const string path = "bg/ex2/01_gyr_g3/twn/g3t1/bgparts/g3t1_p0_tre4c.mdl";
        var mesh = ModelGeometry.Decode(game.GetFile<MdlFile>(path)!, p => game.GetFile<MtrlFile>(p), path);
        if (mesh.Materials.All(m => m.TexturePath == null) || mesh.UVs.Length != mesh.Vertices.Length || mesh.TriangleMaterials.Length != mesh.Indices.Length / 3) throw new Exception("Real model's color/UV mapping was not loaded.");
        var maps = mesh.Materials.Select(m => m.TexturePath == null ? null : game.GetFile<TexFile>(m.TexturePath)).ToArray();
        if (maps.All(m => m == null)) throw new Exception("No real diffuse texture could be decoded.");
        var pixelsByMaterial = maps.Select(m => m?.ImageData).ToArray(); // Lumina ImageData is BGRA32.
        const int size = 600;
        var pixels = Enumerable.Repeat(unchecked((int)0xFF101923), size * size).ToArray();
        var rotation = Matrix4x4.CreateRotationY(.65f) * Matrix4x4.CreateRotationX(-.35f);
        var vertices = mesh.Vertices.Select(v => Vector3.Transform(v, rotation)).ToArray();
        var triangles = Enumerable.Range(0, mesh.Indices.Length / 3).OrderBy(t => vertices[mesh.Indices[t*3]].Z + vertices[mesh.Indices[t*3+1]].Z + vertices[mesh.Indices[t*3+2]].Z);
        Vector2 Screen(Vector3 v) => new(size / 2 + v.X * size * .43f, size / 2 - v.Y * size * .43f);
        foreach (int t in triangles)
        {
            var a = vertices[mesh.Indices[t*3]]; var b = vertices[mesh.Indices[t*3+1]]; var c = vertices[mesh.Indices[t*3+2]];
            var normal = Vector3.Cross(b - a, c - a); if (normal.LengthSquared() < 1e-12f) continue;
            float shade = .55f + .45f * (.24f + .72f * MathF.Abs(Vector3.Dot(Vector3.Normalize(normal), Vector3.Normalize(new Vector3(-1, 2, -3)))));
            int material = mesh.TriangleMaterials[t]; var tex = maps[material]; var bytes = pixelsByMaterial[material];
            if (tex == null || bytes == null) continue;
            foreach (var tile in mesh.TextureTiles[t])
            {
                Vector2 Point(TextureVertex v) => Screen(a + (b - a) * v.Position.X + (c - a) * v.Position.Y);
                var pa = Point(tile.A); var pb = Point(tile.B); var pc = Point(tile.C);
                float determinant = Cross(pb - pa, pc - pa); if (MathF.Abs(determinant) < .00001f) continue;
                int x0 = Math.Clamp((int)MathF.Floor(MathF.Min(pa.X, MathF.Min(pb.X, pc.X))), 0, size - 1);
                int y0 = Math.Clamp((int)MathF.Floor(MathF.Min(pa.Y, MathF.Min(pb.Y, pc.Y))), 0, size - 1);
                int x1 = Math.Clamp((int)MathF.Ceiling(MathF.Max(pa.X, MathF.Max(pb.X, pc.X))), 0, size - 1);
                int y1 = Math.Clamp((int)MathF.Ceiling(MathF.Max(pa.Y, MathF.Max(pb.Y, pc.Y))), 0, size - 1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float v = Cross(p - pa, pc - pa) / determinant, w = Cross(pb - pa, p - pa) / determinant, u = 1 - v - w;
                    if (u < 0 || v < 0 || w < 0) continue;
                    var uv = tile.A.UV * u + tile.B.UV * v + tile.C.UV * w;
                    int tx = Math.Clamp((int)(uv.X * tex.Header.Width), 0, tex.Header.Width - 1), ty = Math.Clamp((int)(uv.Y * tex.Header.Height), 0, tex.Header.Height - 1);
                    int at = (ty * tex.Header.Width + tx) * 4; float alpha = bytes[at + 3] / 255f;
                    int old = pixels[y * size + x];
                    int Blend(int channel, int shift) => Math.Clamp((int)(channel * shade * alpha + ((old >> shift) & 255) * (1 - alpha)), 0, 255);
                    pixels[y * size + x] = unchecked((int)0xFF000000) | Blend(bytes[at+2],16) << 16 | Blend(bytes[at+1],8) << 8 | Blend(bytes[at],0);
                }
            }
        }
        if (pixels.Distinct().Count() < 100) throw new Exception("Textured raster did not contain expected color variation.");
        using var image = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var bits = image.LockBits(new(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try { Marshal.Copy(pixels, 0, bits.Scan0, pixels.Length); } finally { image.UnlockBits(bits); }
        image.Save(Path.Combine(output, "color-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Real textured preview: {mesh.Materials.Count(m => m.TexturePath != null)}/{mesh.Materials.Length} base-color maps; {mesh.TextureTiles.Sum(p => p.Length)} tiled triangles. Color preview PNG written.");
    }
}
