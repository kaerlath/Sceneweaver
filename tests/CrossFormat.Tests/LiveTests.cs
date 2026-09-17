using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Stagehand.Definitions;

internal static class LiveTests
{
    private sealed class Backend : ILiveStageBackend
    {
        public bool Available { get; set; } = true;
        public string? Location { get; set; } = "world1/house1/room0";
        public bool RejectUpdate, RejectShow, FailDestroy;
        public int Updates, Shows, Destroys;
        public List<string> Ids = [];
        public LiveSceneSnapshot? Last;
        public bool Upsert(string id, LiveSceneSnapshot scene) { Ids.Add(id); Updates++; Last = scene; return !RejectUpdate; }
        public bool Show(string id) { Ids.Add(id); Shows++; return !RejectShow; }
        public bool Destroy(string id) { Ids.Add(id); Destroys++; if (FailDestroy) throw new IOException("Temporarily unavailable"); return true; }
    }
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool value) { if (!value) throw new Exception("Live scene assertion failed."); }
        void Near(Vector3 a, Vector3 b) => Assert(Vector3.Distance(a, b) < .0001f);
        SceneProject Scene() => new() { Objects = [new SceneObject { Name = "Test", AssetPath = "bg/test.mdl", Position = new(2, 3, 4) }] };
        test("Live placement inverse respects stage rotation and scale", () =>
        {
            var scene = Scene(); scene.StageTranslation = new(40, 50, 60); scene.StageRotationDegrees = new(15, 90, 20); scene.StageUniformScale = 2.5f;
            var world = new Vector3(11, 22, 33); var local = LiveScene.LocalPosition(scene, world);
            Near(TransformMath.ToWorld(scene, new SceneObject { Position = local }).Position, world);
            var original = scene.Objects[0].Position;
            scene.StageTranslation = LiveScene.TranslationAt(scene, scene.Objects[0], world);
            Near(TransformMath.ToWorld(scene, scene.Objects[0]).Position, world); Near(scene.Objects[0].Position, original);
        });
        test("Live definition keeps mods and visibility while reporting unsupported/incomplete objects", () =>
        {
            var scene = Scene(); var id = ModResources.Attach(scene, new JsonObject { ["DisplayName"] = "Test", ["ModdedResources"] = new JsonObject() });
            scene.Objects[0].StagehandSource["ModpackId"] = id;
            scene.Objects.Add(new SceneObject { Name = "Hidden", AssetPath = "bg/hidden.mdl", Visible = false });
            scene.Objects.Add(new SceneObject { Name = "Choose path" });
            scene.Objects.Add(new SceneObject { Name = "Furniture", Kind = AssetKind.Furniture, AssetPath = "bg/furniture.sgb" });
            string before = ProjectJson.Save(scene); var live = LiveScene.Build(scene);
            var definition = JsonSerializer.Deserialize<StageDefinition>(live.Definition, StageDefinition.StandardSerializerOptions)!;
            Assert(definition.Objects.Count == 2 && live.VisibleObjects == 1 && live.Notes.Length == 2);
            Assert(definition.Objects.Values.Any(o => o.ModpackId == id) && definition.EmbeddedModpacks.ContainsKey(id));
            Assert(ProjectJson.Save(scene) == before);
        });
        test("Live session owns a unique temporary ID and does not send unchanged scenes", () =>
        {
            var backend = new Backend(); var live = new LiveSceneSession(backend); var scene = Scene();
            Assert(live.StageId.StartsWith("Sceneweaver.Live.") && live.StageId != new LiveSceneSession(backend).StageId);
            live.Start(() => LiveScene.Build(scene), TimeSpan.Zero);
            live.Tick(() => throw new Exception("Should not rebuild unchanged state"), TimeSpan.FromSeconds(1));
            Assert(live.Enabled && backend.Updates == 1 && backend.Shows == 1);
            live.Stop(); Assert(!live.Enabled && backend.Destroys == 1 && backend.Ids.All(id => id == live.StageId));
        });
        test("Live changes coalesce and deletions replace the full object set", () =>
        {
            var backend = new Backend(); var live = new LiveSceneSession(backend); var scene = Scene();
            live.Start(() => LiveScene.Build(scene), TimeSpan.Zero);
            for (int i = 0; i < 10; i++) { scene.Objects[0].Position = new(i, 0, 0); live.MarkChanged(); }
            live.Tick(() => LiveScene.Build(scene), TimeSpan.FromMilliseconds(100)); Assert(backend.Updates == 1);
            live.Tick(() => LiveScene.Build(scene), TimeSpan.FromMilliseconds(400)); Assert(backend.Updates == 2);
            scene.Objects.Clear(); live.MarkChanged(); live.Tick(() => LiveScene.Build(scene), TimeSpan.FromSeconds(1));
            Assert(backend.Updates == 3 && backend.Last!.VisibleObjects == 0 && JsonNode.Parse(backend.Last.Definition)!["Objects"]!.AsObject().Count == 0);
        });
        test("Live location changes stop display even when the territory is otherwise identical", () =>
        {
            var backend = new Backend(); var live = new LiveSceneSession(backend);
            live.Start(() => LiveScene.Build(Scene()), TimeSpan.Zero); backend.Location = "world1/house1/room1";
            live.MarkChanged(); live.Tick(() => throw new Exception("Must not respawn in a new room"), TimeSpan.FromSeconds(1));
            Assert(!live.Enabled && backend.Destroys == 1 && backend.Updates == 1);
            backend.Location = "world1/house1/room0"; live.Tick(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(2)); Assert(!live.Enabled && backend.Updates == 1);
        });
        test("Logout or missing Stagehand prevents live creation", () =>
        {
            foreach (bool available in new[] { false, true })
            {
                var backend = new Backend { Available = available, Location = null }; var live = new LiveSceneSession(backend);
                live.Start(() => LiveScene.Build(Scene()), TimeSpan.Zero); Assert(!live.Enabled && backend.Updates == 0 && backend.Destroys == 0);
            }
        });
        test("Rejected show and rejected update clean up potentially created objects", () =>
        {
            var backend = new Backend { RejectShow = true }; var live = new LiveSceneSession(backend);
            live.Start(() => LiveScene.Build(Scene()), TimeSpan.Zero); Assert(!live.Enabled && backend.Destroys == 1);
            backend.RejectShow = false; live.Start(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(1));
            backend.RejectUpdate = true; live.MarkChanged(); live.Tick(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(2));
            Assert(!live.Enabled && backend.Destroys == 2);
        });
        test("Failed cleanup retains ownership and retries instead of orphaning a stage", () =>
        {
            var backend = new Backend(); var live = new LiveSceneSession(backend);
            live.Start(() => LiveScene.Build(Scene()), TimeSpan.Zero); backend.FailDestroy = true; live.Stop();
            Assert(live.CleanupPending && !live.Enabled); int updates = backend.Updates;
            live.Start(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(1)); Assert(backend.Updates == updates);
            backend.FailDestroy = false; live.Tick(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(2));
            Assert(!live.CleanupPending && !live.Enabled && backend.Ids.All(id => id == live.StageId));
        });
        test("Live serialization failure and backend unload stop updates", () =>
        {
            var backend = new Backend(); var live = new LiveSceneSession(backend);
            live.Start(() => LiveScene.Build(Scene()), TimeSpan.Zero); live.MarkChanged();
            live.Tick(() => throw new InvalidDataException("Bad transform"), TimeSpan.FromSeconds(1)); Assert(!live.Enabled && backend.Destroys == 1);
            live.Start(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(2)); backend.Available = false;
            live.Tick(() => LiveScene.Build(Scene()), TimeSpan.FromSeconds(3)); Assert(!live.Enabled && backend.Destroys == 2);
        });
    }
}
