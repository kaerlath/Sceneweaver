using System.Numerics;

namespace CrossFormat.Core;

public sealed record WorldCapture(Vector3 Origin, float Radius, WorldPick[] Items)
{
    public static WorldCapture Take(IEnumerable<WorldPick> source, Vector3 origin, float radius)
    {
        TransformEditing.Check(origin);
        if (!float.IsFinite(radius) || radius <= 0 || radius > 200) throw new InvalidDataException("Capture radius must be greater than zero and at most 200 game units.");
        return new(origin, radius, source.Where(p => Vector3.Distance(origin, p.Position) <= radius).ToArray());
    }

    public SceneProject Create(string name, IEnumerable<int> selected)
    {
        var indices = selected.Distinct().Order().ToArray();
        if (indices.Length == 0) throw new InvalidDataException("Select at least one supported object.");
        var scene = new SceneProject { Name = name.Trim(), StageTranslation = Origin };
        foreach (int index in indices)
        {
            if (index < 0 || index >= Items.Length) throw new InvalidDataException("Capture selection is invalid.");
            scene.Objects.Add(Items[index].CopyAtPlacement(scene));
        }
        scene.ImportNotes.Add($"Nearby capture: {indices.Length} objects, radius {Radius} game units. Positions are relative to the character's capture origin {Origin}.");
        scene.ImportNotes.Add("Reconstructed from loaded model/VFX snapshots. No ownership, groups, mod dependencies, terrain collision, area lighting or NPCs were captured. VFX restart when loaded. Even ordinary resource paths may have been altered by mods.");
        ProjectJson.Validate(scene);
        return scene;
    }
}
