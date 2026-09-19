using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrossFormat.Core;

public enum SceneFormat { Intoner, Stagehand }
public enum AssetKind { BgObject, Vfx, Light, Furniture, Weapon, Sound, Unknown }

public sealed class SceneProject
{
    public string DocumentKind { get; set; } = "cross-format-project";
    public int FormatVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled scene";
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<SceneObject> Objects { get; set; } = [];
    public JsonObject IntonerRoot { get; set; } = new();
    public JsonObject StagehandRoot { get; set; } = new();
    public List<string> ImportNotes { get; set; } = [];
    // Stagehand definitions are local to a stage; Intoner layouts use world space.
    // Identity is the explicit default, never the current player's location.
    public Vector3 StageTranslation { get; set; }
    public Vector3 StageRotationDegrees { get; set; }
    public float StageUniformScale { get; set; } = 1;
}

public sealed class SceneObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StagehandId { get; set; } = "";
    public string Name { get; set; } = "New object";
    public AssetKind Kind { get; set; }
    public bool Visible { get; set; } = true;
    public string Folder { get; set; } = "";
    public bool Locked { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 RotationDegrees { get; set; }
    public Vector3 Scale { get; set; } = Vector3.One;
    public string AssetPath { get; set; } = "";
    public float Opacity { get; set; } = 1;
    public Vector4 Color { get; set; } = Vector4.One;
    public JsonObject Light { get; set; } = new();
    public VfxPlayback? Playback { get; set; }
    public JsonObject IntonerSource { get; set; } = new();
    public JsonObject StagehandSource { get; set; } = new();
    public uint IconId { get; set; }
    public string PreviewImagePath { get; set; } = "";
}

public sealed record VfxPlayback(float Speed = 1, bool Paused = false, float FadeInSeconds = 0,
    bool ReplayOnTransform = false, bool Loop = false, int LoopIntervalSeconds = 5)
{
    // Older canonical projects keep playback only in the preserved Intoner payload.
    public static VfxPlayback For(SceneObject o) => o.Playback
        ?? o.IntonerSource["Object"]?["Model"]?["Vfx"]?.Deserialize<VfxPlayback>(ProjectJson.Options)
        ?? new();
}

public sealed record ConversionIssue(string ObjectName, string Message, bool Omitted = false);
public sealed record ExportResult(JsonObject Document, List<ConversionIssue> Issues)
{
    public string Json => Document.ToJsonString(ProjectJson.Options);
}

public static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, IncludeFields = true,
        Converters = { new JsonStringEnumConverter() },
        AllowDuplicateProperties = false,
    };
    public static string Save(SceneProject scene) { Validate(scene); return JsonSerializer.Serialize(scene, Options); }
    public static SceneProject Load(string json)
    {
        var scene = JsonSerializer.Deserialize<SceneProject>(json, Options) ?? throw new InvalidDataException("Empty project.");
        if (scene.DocumentKind != "cross-format-project" || scene.FormatVersion != 1)
            throw new InvalidDataException("Unsupported canonical project version.");
        Validate(scene);
        return scene;
    }
    public static void Validate(SceneProject scene)
    {
        if (scene.Id == Guid.Empty || string.IsNullOrWhiteSpace(scene.Name) || scene.Revision <= 0 || scene.CreatedAtUtc == default) throw new InvalidDataException("Project needs an ID, name, positive revision and creation timestamp.");
        if (!float.IsFinite(scene.StageUniformScale) || scene.StageUniformScale <= 0) throw new InvalidDataException("Stage scale must be positive and finite.");
        Check(scene.StageTranslation); Check(scene.StageRotationDegrees);
        var ids = new HashSet<Guid>();
        var stageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in scene.Objects)
        {
            if (o.Id == Guid.Empty || !ids.Add(o.Id)) throw new InvalidDataException("Duplicate or empty object ID.");
            if (!stageIds.Add(string.IsNullOrEmpty(o.StagehandId) ? o.Id.ToString() : o.StagehandId)) throw new InvalidDataException("Duplicate Stagehand object key.");
            Check(o.Position); Check(o.RotationDegrees); Check(o.Scale);
            if (o.Kind == AssetKind.Vfx)
            {
                var playback = VfxPlayback.For(o);
                if (!float.IsFinite(playback.Speed) || !float.IsFinite(playback.FadeInSeconds))
                    throw new InvalidDataException($"{o.Name}: VFX playback values must be finite.");
            }
            if (!float.IsFinite(o.Opacity) || !float.IsFinite(o.Color.X) || !float.IsFinite(o.Color.Y) || !float.IsFinite(o.Color.Z) || !float.IsFinite(o.Color.W)) throw new InvalidDataException("Non-finite color or opacity.");
        }
    }
    private static void Check(Vector3 v) { if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z)) throw new InvalidDataException("Non-finite transform."); }
}

public static class TransformMath
{
    public static Quaternion Rotation(Vector3 degrees) => Quaternion.CreateFromYawPitchRoll(degrees.Y * MathF.PI / 180, degrees.X * MathF.PI / 180, degrees.Z * MathF.PI / 180);
    public static Vector3 Degrees(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        var sin = Math.Clamp(2 * (q.W * q.X - q.Y * q.Z), -1, 1);
        var pitch = MathF.Asin(sin);
        float yaw, roll;
        if (MathF.Abs(sin) > 0.999999f) { yaw = 2 * MathF.Atan2(q.Y, q.W); roll = 0; }
        else { yaw = MathF.Atan2(2 * (q.W * q.Y + q.X * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y)); roll = MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.X * q.X + q.Z * q.Z)); }
        return new Vector3(pitch, yaw, roll) * (180 / MathF.PI);
    }
    public static (Vector3 Position, Vector3 Rotation, Vector3 Scale) ToWorld(SceneProject s, SceneObject o)
    {
        if (s.StageTranslation == Vector3.Zero && s.StageRotationDegrees == Vector3.Zero && s.StageUniformScale == 1) return (o.Position, o.RotationDegrees, o.Scale);
        var root = Rotation(s.StageRotationDegrees);
        return (Vector3.Transform(o.Position * s.StageUniformScale, root) + s.StageTranslation,
            Degrees(root * Rotation(o.RotationDegrees)), o.Scale * s.StageUniformScale);
    }
}
