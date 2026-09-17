using System.Numerics;

namespace CrossFormat.Assets;

public readonly record struct TextureVertex(Vector2 Position, Vector2 UV);
public readonly record struct TextureTriangle(TextureVertex A, TextureVertex B, TextureVertex C);

/// <summary>Clips repeating UVs to unit tiles so rendering is independent of the UI's clamp sampler.</summary>
public static class TextureTiling
{
    public static TextureTriangle[] Split(TextureVertex a, TextureVertex b, TextureVertex c)
    {
        var min = Vector2.Min(a.UV, Vector2.Min(b.UV, c.UV));
        var max = Vector2.Max(a.UV, Vector2.Max(b.UV, c.UV));
        if (!float.IsFinite(min.X + min.Y + max.X + max.Y) || Vector2.Abs(min).Length() > 10000 || Vector2.Abs(max).Length() > 10000) return [];
        int x0 = (int)MathF.Floor(min.X), y0 = (int)MathF.Floor(min.Y);
        int x1 = Math.Max(x0, (int)MathF.Ceiling(max.X) - 1), y1 = Math.Max(y0, (int)MathF.Ceiling(max.Y) - 1);
        if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > 64) return [];
        List<TextureTriangle> output = [];
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
        {
            List<TextureVertex> polygon = [a, b, c];
            polygon = Clip(polygon, true, x, true); polygon = Clip(polygon, true, x + 1, false);
            polygon = Clip(polygon, false, y, true); polygon = Clip(polygon, false, y + 1, false);
            TextureVertex Local(TextureVertex v) => v with { UV = Vector2.Clamp(v.UV - new Vector2(x, y), Vector2.Zero, Vector2.One) };
            for (int n = 1; n + 1 < polygon.Count; n++) output.Add(new(Local(polygon[0]), Local(polygon[n]), Local(polygon[n + 1])));
        }
        return output.ToArray();
    }

    private static List<TextureVertex> Clip(List<TextureVertex> input, bool horizontal, float edge, bool greater)
    {
        List<TextureVertex> output = [];
        if (input.Count == 0) return output;
        float Coord(TextureVertex v) => horizontal ? v.UV.X : v.UV.Y;
        bool Inside(TextureVertex v) => greater ? Coord(v) >= edge : Coord(v) <= edge;
        var previous = input[^1]; bool previousInside = Inside(previous);
        foreach (var current in input)
        {
            bool inside = Inside(current);
            if (inside != previousInside)
            {
                float t = (edge - Coord(previous)) / (Coord(current) - Coord(previous));
                output.Add(new(Vector2.Lerp(previous.Position, current.Position, t), Vector2.Lerp(previous.UV, current.UV, t)));
            }
            if (inside) output.Add(current);
            previous = current; previousInside = inside;
        }
        return output;
    }
}
