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
    }
}
