using System.Numerics;
using System.Text.Json;

namespace CrossFormat.Core;

public static class SceneHierarchy
{
    public static IEnumerable<SceneObject> Ancestors(SceneProject scene, SceneObject item)
    {
        var seen = new HashSet<Guid> { item.Id };
        while (item.ParentId is Guid parent)
        {
            if (!seen.Add(parent) || seen.Count > 64) throw new InvalidDataException("Group hierarchy is cyclic or exceeds 63 levels.");
            item = scene.Objects.FirstOrDefault(o => o.Id == parent && o.Kind == AssetKind.Group)
                ?? throw new InvalidDataException("An object's parent group is missing.");
            yield return item;
        }
    }
    public static bool Visible(SceneProject s, SceneObject o) => o.Visible && Ancestors(s, o).All(p => p.Visible);
    public static (Vector3 Position, Vector3 Rotation, Vector3 Scale) StageTransform(SceneProject s, SceneObject o)
    {
        var p = o.Position; var r = TransformMath.Rotation(o.RotationDegrees); var scale = o.Scale;
        foreach (var parent in Ancestors(s, o))
        {
            var q = TransformMath.Rotation(parent.RotationDegrees);
            p = Vector3.Transform(p * parent.Scale.X, q) + parent.Position;
            r = q * r; scale *= parent.Scale.X;
        }
        return (p, o.ParentId == null ? o.RotationDegrees : TransformMath.Degrees(r), scale);
    }
    public static Vector3 ToParentPosition(SceneProject s, SceneObject o, Vector3 stagePosition)
    {
        foreach (var parent in Ancestors(s, o).Reverse())
            stagePosition = Vector3.Transform(stagePosition - parent.Position, Quaternion.Inverse(TransformMath.Rotation(parent.RotationDegrees))) / parent.Scale.X;
        return stagePosition;
    }
    public static List<SceneObject> Subtree(SceneProject s, SceneObject o) => s.Objects.Where(n => n.Id == o.Id || Ancestors(s, n).Any(p => p.Id == o.Id)).ToList();
    public static void Reparent(SceneProject s, SceneObject o, Guid? parent)
    {
        var world = StageTransform(s, o);
        var group = parent == null ? null : s.Objects.Single(n => n.Id == parent && n.Kind == AssetKind.Group);
        if (group != null && Subtree(s, o).Contains(group)) throw new InvalidDataException("A group cannot be its own descendant.");
        var target = group == null ? (Position: Vector3.Zero, Rotation: Vector3.Zero, Scale: Vector3.One) : StageTransform(s, group);
        o.Position = Vector3.Transform(world.Position - target.Position, Quaternion.Inverse(TransformMath.Rotation(target.Rotation))) / target.Scale.X;
        o.RotationDegrees = TransformMath.Degrees(Quaternion.Inverse(TransformMath.Rotation(target.Rotation)) * TransformMath.Rotation(world.Rotation));
        o.Scale = world.Scale / target.Scale.X; o.ParentId = parent; o.StagehandId = "";
    }
    public static SceneObject Duplicate(SceneProject s, SceneObject o)
    {
        var copies = JsonSerializer.Deserialize<List<SceneObject>>(JsonSerializer.Serialize(Subtree(s, o), ProjectJson.Options), ProjectJson.Options)!;
        var ids = copies.ToDictionary(n => n.Id, _ => Guid.NewGuid());
        foreach (var n in copies) { n.Id = ids[n.Id]; n.StagehandId = ""; if (n.ParentId is Guid id && ids.TryGetValue(id, out var mapped)) n.ParentId = mapped; }
        var result = copies.Single(n => n.Id == ids[o.Id]); result.Name += " copy"; s.Objects.AddRange(copies); return result;
    }
}
