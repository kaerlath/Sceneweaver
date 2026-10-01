using System.Numerics;

namespace CrossFormat.Core;

public sealed record WorldPick(string Key, string ResourcePath, AssetKind Kind, Vector3 Position,
    Quaternion Rotation, Vector3 Scale, Vector4 Color, float Opacity)
{
    public bool CanUse => !string.IsNullOrWhiteSpace(ResourcePath) && !ResourcePath.Contains(':') && !ResourcePath.Contains('|')
        && !ResourcePath.StartsWith('/') && !ResourcePath.Contains('\\') && !ResourcePath.Split('/').Any(p => p is ".." or "." or "")
        && (Kind == AssetKind.BgObject && ResourcePath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase) || Kind == AssetKind.Vfx && ResourcePath.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase));
    public SceneObject CreateAsset() => CanUse ? new SceneObject { Name = Path.GetFileNameWithoutExtension(ResourcePath), Kind = Kind,
        AssetPath = ResourcePath, Color = Color, Opacity = Opacity } : throw new InvalidDataException("This resource uses a mod-specific path. Import the source mod through Mods to retain its resources.");
    public SceneObject CopyAtPlacement(SceneProject scene)
    {
        var result = CreateAsset();
        result.Position = LiveScene.LocalPosition(scene, Position);
        result.RotationDegrees = TransformMath.Degrees(Quaternion.Inverse(TransformMath.Rotation(scene.StageRotationDegrees)) * Rotation);
        result.Scale = Scale / scene.StageUniformScale;
        return result;
    }
}

public sealed class WorldPickHistory
{
    public List<WorldPick> Items { get; } = [];
    public void Add(WorldPick item)
    {
        Items.RemoveAll(p => p.Key == item.Key); Items.Insert(0, item);
        if (Items.Count > 50) Items.RemoveRange(50, Items.Count - 50);
    }
}
