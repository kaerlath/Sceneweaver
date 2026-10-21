using System.Numerics;
using CrossFormat.Core;

internal static class WorldPickTests
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool value) { if (!value) throw new Exception("World picker assertion failed."); }
        WorldPick Pick(string path = "bg/test.mdl", AssetKind kind = AssetKind.BgObject) => new("key", path, kind, new(12, 4, 8), TransformMath.Rotation(new(25, 65, 10)), new(2, 3, 4), new(.2f, .4f, .6f, 1), .75f);
        test("World copy preserves position, rotation, scale and appearance under stage placement", () =>
        {
            var scene = new SceneProject { StageTranslation = new(20, 15, 7), StageRotationDegrees = new(10, 30, 20), StageUniformScale = 2.5f };
            var pick = Pick(); var copy = pick.CopyAtPlacement(scene); var world = TransformMath.ToWorld(scene, copy);
            Assert(Vector3.Distance(world.Position, pick.Position) < .001f);
            Assert(Vector3.Distance(world.Scale, pick.Scale) < .001f);
            Assert(Math.Abs(Quaternion.Dot(TransformMath.Rotation(world.Rotation), pick.Rotation)) > .9999f);
            Assert(copy.Opacity == pick.Opacity && copy.Color == pick.Color && scene.Objects.Count == 0);
            Assert(copy.Id != pick.CopyAtPlacement(scene).Id);
        });
        test("World picker blocks mod wrappers, disk paths and unsafe paths without discarding details", () =>
        {
            foreach (var path in new[] { "mem://123/bg/a.mdl", "|mod|bg/a.mdl", "C:/mods/a.mdl", "/outside.mdl", "bg/../a.mdl", "" })
            {
                var pick = Pick(path); Assert(!pick.CanUse && pick.ResourcePath == path);
                bool rejected = false; try { pick.CreateAsset(); } catch (InvalidDataException) { rejected = true; } Assert(rejected);
            }
            Assert(Pick("vfx/test.avfx", AssetKind.Vfx).CanUse);
            Assert(!Pick("vfx/test.avfx", AssetKind.Sound).CanUse);
        });
        test("Recent picks are bounded snapshots with latest selection first", () =>
        {
            var history = new WorldPickHistory();
            for (int i = 0; i < 60; i++) history.Add(Pick() with { Key = i.ToString() });
            Assert(history.Items.Count == 50 && history.Items[0].Key == "59");
            var recent = Pick() with { Key = "20", Position = new(5, 6, 7) }; history.Add(recent);
            Assert(history.Items.Count == 50 && history.Items[0] == recent && history.Items.Count(p => p.Key == "20") == 1);
        });
        test("Capture freezes radius-filtered entries and preserves world placement through save and relocation", () =>
        {
            var origin = new Vector3(10, 4, 8);
            var a = Pick(); var b = Pick("vfx/test.avfx", AssetKind.Vfx) with { Key = "b", Position = origin + new Vector3(0, 3, 0) };
            var far = Pick() with { Key = "far", Position = origin + new Vector3(0, 4, 0) };
            var list = new List<WorldPick> { a, b, far };
            var capture = WorldCapture.Take(list, origin, 3); list.Clear();
            Assert(capture.Items.Length == 2);
            var project = ProjectJson.Load(ProjectJson.Save(capture.Create("Cave", [0, 1])));
            Assert(project.StageTranslation == origin && project.Objects.Count == 2);
            Assert(Vector3.Distance(TransformMath.ToWorld(project, project.Objects[0]).Position, a.Position) < .001f);
            Assert(project.Objects[0].Color == a.Color && project.Objects[0].Opacity == a.Opacity);
            var imported = SceneCodec.Import(SceneCodec.Export(project, SceneFormat.Stagehand).Json);
            Assert(imported.Objects.Count == 2 && imported.Objects[0].Position == a.Position - origin);
            project.StageTranslation = new(100, 20, 30);
            Assert(Vector3.Distance(TransformMath.ToWorld(project, project.Objects[1]).Position, new(100, 23, 30)) < .001f);
        });
        test("Capture selection excludes unsupported paths and rejects invalid or empty requests", () =>
        {
            void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }
            var capture = WorldCapture.Take([Pick(), Pick("mem://effect", AssetKind.Vfx)], Vector3.Zero, 30);
            Assert(capture.Create("Scene", [0, 0]).Objects.Count == 1);
            Reject(() => capture.Create("Scene", [1])); Reject(() => capture.Create("Scene", []));
            Reject(() => capture.Create("Scene", [3])); Reject(() => capture.Create(" ", [0]));
            Reject(() => WorldCapture.Take([], Vector3.Zero, float.NaN));
            Reject(() => WorldCapture.Take([], Vector3.Zero, 201));
            Assert(capture.Create("Scene", [0]).Objects[0].Id != capture.Create("Scene", [0]).Objects[0].Id);
        });
        test("Effects nearby filters by kind and path without excluding mod-specific effects", () =>
        {
            var model = Pick();
            var effect = Pick("vfx/mist.avfx", AssetKind.Vfx) with { Key = "effect" };
            var mod = Pick("|mod|vfx/mist.avfx", AssetKind.Vfx) with { Key = "mod" };
            var items = new[] { model, effect, mod };
            Assert(WorldPickView.Filter(items, "MIST", true).SequenceEqual(new[] { effect, mod }));
            Assert(WorldPickView.Filter(items, "", false).Count() == 3);
            Assert(!WorldPickView.Filter(items, "missing", true).Any());
        });
        test("Selection highlight requires a matching current snapshot and never resolves by name alone", () =>
        {
            var old = Pick("vfx/mist.avfx", AssetKind.Vfx);
            var current = old with { Color = Vector4.One };
            Assert(WorldPickView.Current([current], old) == current);
            Assert(WorldPickView.Current([], old) == null);
            Assert(WorldPickView.Current([current], null) == null);
            Assert(WorldPickView.Current([current with { Key = "other-origin" }], old) == null);
            Assert(WorldPickView.Current([current with { ResourcePath = "vfx/other.avfx" }], old) == null);
            Assert(WorldPickView.Current([current with { Kind = AssetKind.BgObject }], old) == null);
        });
    }
}
