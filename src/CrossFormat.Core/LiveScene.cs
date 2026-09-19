using System.Numerics;

namespace CrossFormat.Core;

public sealed record LiveSceneSnapshot(string Definition, Vector3 Translation, Quaternion Rotation, float Scale, int VisibleObjects, string[] Notes);
public interface ILiveStageBackend
{
    bool Available { get; }
    string? Location { get; }
    bool Upsert(string id, LiveSceneSnapshot scene);
    bool Show(string id);
    bool Destroy(string id);
}

public static class LiveScene
{
    public static Vector3 LocalPosition(SceneProject scene, Vector3 worldPosition) => Vector3.Transform(worldPosition - scene.StageTranslation, Quaternion.Inverse(TransformMath.Rotation(scene.StageRotationDegrees))) / scene.StageUniformScale;
    public static Vector3 TranslationAt(SceneProject scene, SceneObject? anchor, Vector3 worldPosition) => worldPosition - Vector3.Transform((anchor?.Position ?? Vector3.Zero) * scene.StageUniformScale, TransformMath.Rotation(scene.StageRotationDegrees));
    public static LiveSceneSnapshot Build(SceneProject source)
    {
        var notes = new List<string>();
        var objects = source.Objects.Where(o =>
        {
            if (o.Kind is AssetKind.BgObject or AssetKind.Vfx or AssetKind.Sound && string.IsNullOrWhiteSpace(o.AssetPath))
            { notes.Add(o.Name + ": choose an asset path before showing it in game."); return false; }
            return true;
        }).ToList();
        var live = new SceneProject
        {
            Id = source.Id, Name = source.Name, Revision = source.Revision, CreatedAtUtc = source.CreatedAtUtc,
            StagehandRoot = source.StagehandRoot, Objects = objects,
            StageTranslation = source.StageTranslation, StageRotationDegrees = source.StageRotationDegrees, StageUniformScale = source.StageUniformScale,
        };
        var result = SceneCodec.Export(live, SceneFormat.Stagehand);
        notes.AddRange(result.Issues.Select(i => i.ObjectName + ": " + i.Message));
        int count = result.Document["Objects"]!.AsObject().Count(p => p.Value?["IsDisabled"]?.GetValue<bool>() != true);
        return new(result.Json, source.StageTranslation, TransformMath.Rotation(source.StageRotationDegrees), source.StageUniformScale, count, notes.ToArray());
    }
}

/// <summary>Owns one temporary stage. Tick on the game thread; never touches persistent stages.</summary>
public sealed class LiveSceneSession(ILiveStageBackend backend)
{
    public string StageId { get; } = "Sceneweaver.Live." + Guid.NewGuid().ToString("N");
    public bool Enabled { get; private set; }
    public bool CleanupPending { get; private set; }
    public string Status { get; private set; } = "In-game display is off. Stagehand must be enabled.";
    public string[] Notes { get; private set; } = [];
    private bool owned, changed;
    private string? location;
    private string stoppedStatus = "In-game display hidden.";
    private TimeSpan nextPoll, nextSync;

    public void Start(Func<LiveSceneSnapshot> build, TimeSpan now)
    {
        if (CleanupPending) { Cleanup(); if (CleanupPending) return; }
        try
        {
            if (!backend.Available) throw new InvalidOperationException("Enable Stagehand to display objects in game.");
            location = backend.Location ?? throw new InvalidOperationException("Log into the game before showing the scene.");
            var snapshot = build();
            owned = true; // Retain ownership even if a failed call partially creates the stage.
            if (!backend.Upsert(StageId, snapshot) || !backend.Show(StageId)) throw new InvalidOperationException("Stagehand rejected the live scene.");
            Enabled = true; changed = false; nextSync = now + TimeSpan.FromMilliseconds(350); nextPoll = now;
            UpdateStatus(snapshot);
        }
        catch (Exception e) { Fail(e.Message); }
    }
    public void MarkChanged() { if (Enabled) changed = true; }
    public void Stop(string reason = "In-game display hidden.")
    {
        Enabled = false; changed = false; Notes = []; Status = stoppedStatus = reason;
        Cleanup();
    }
    private void Cleanup()
    {
        if (!owned) { CleanupPending = false; return; }
        try { backend.Destroy(StageId); owned = false; CleanupPending = false; Status = stoppedStatus; }
        catch (Exception e) { CleanupPending = true; Status = stoppedStatus + " Cleanup pending: " + e.Message; }
    }
    private void Fail(string message)
    {
        Stop("Live display stopped: " + message);
    }
    public void Tick(Func<LiveSceneSnapshot> build, TimeSpan now)
    {
        if (now < nextPoll) return;
        nextPoll = now + TimeSpan.FromMilliseconds(200);
        if (!Enabled) { if (CleanupPending) Cleanup(); return; }
        try
        {
            if (!backend.Available) { Stop("Live display stopped: Stagehand is unavailable."); return; }
            string? current = backend.Location;
            if (current == null || current != location) { Stop("Live display stopped after leaving the location. Show it again when ready."); return; }
            if (!changed || now < nextSync) return;
            var snapshot = build();
            if (!backend.Upsert(StageId, snapshot)) throw new InvalidOperationException("Stagehand rejected an update.");
            changed = false; nextSync = now + TimeSpan.FromMilliseconds(350); UpdateStatus(snapshot);
        }
        catch (Exception e) { Fail(e.Message); }
    }
    private void UpdateStatus(LiveSceneSnapshot snapshot)
    {
        Notes = snapshot.Notes;
        Status = $"Live in game via Stagehand: {snapshot.VisibleObjects} enabled objects" + (Notes.Length > 0 ? $" / {Notes.Length} not displayed (details below)." : ".");
    }
}
