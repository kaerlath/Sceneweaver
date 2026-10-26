using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;

internal static class ReceivedStageTests
{
    public static void Run(Action<string, Action> test, string directory)
    {
        void Assert(bool value) { if (!value) throw new Exception("Received stage assertion failed."); }
        void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected import rejection."); }
        string path = Path.Combine(directory, "received-mod.bin");
        Directory.CreateDirectory(directory); File.WriteAllBytes(path, [1, 2, 3, 4]);
        string Definition(string file)
        {
            var resource = JsonSerializer.SerializeToNode<ModResourceDefinition>(new DiskModResourceDefinition { SourceDiskPath = file }, StageDefinition.StandardSerializerOptions);
            return new JsonObject {
                ["FormatVersion"] = 2, ["Info"] = new JsonObject { ["Name"] = "Shared", ["AuthorName"] = "Author" },
                ["Objects"] = new JsonObject { ["stone"] = new JsonObject { ["Type"] = "BgObject", ["ModelGamePath"] = "bg/test.mdl", ["ModpackId"] = "mod", ["Position"] = new JsonObject { ["X"] = 3, ["Y"] = 2, ["Z"] = 1 } } },
                ["FutureField"] = "keep", ["EmbeddedModpacks"] = new JsonObject { ["mod"] = new JsonObject { ["DisplayName"] = "Test", ["ModdedResources"] = new JsonObject { ["bg/test.mdl"] = resource } } }
            }.ToJsonString();
        }
        var snapshot = new ReceivedStageSnapshot("SID", "Shared", "Author", Definition(path), new(10, 20, 30), Quaternion.CreateFromAxisAngle(Vector3.UnitY, .5f), 2);
        test("Received stage embeds cache files, preserves layout and isolates source", () => {
            var imported = ReceivedStages.Import(snapshot);
            Assert(imported.Name == "Shared (copy)" && imported.StageTranslation == snapshot.Translation && imported.StageUniformScale == 2);
            Assert(Math.Abs(Quaternion.Dot(TransformMath.Rotation(imported.StageRotationDegrees), snapshot.Rotation)) > .9999f);
            Assert(imported.StagehandRoot["FutureField"]!.GetValue<string>() == "keep");
            var pack = ModResources.Packs(imported)["mod"]!.AsObject();
            Assert(pack["ModdedResources"]!["bg/test.mdl"]!.Deserialize<ModResourceDefinition>(StageDefinition.StandardSerializerOptions) is EmbeddedModResourceDefinition);
            File.Delete(path);
            var resolved = ModResources.Resolve(pack, "bg/test.mdl", Path.Combine(directory, "received-extracted"));
            Assert(File.ReadAllBytes(resolved.Path).SequenceEqual(new byte[] { 1, 2, 3, 4 }));
            Assert(snapshot.Definition.Contains("SourceDiskPath"));
            Assert(SceneCodec.Import(SceneCodec.Export(imported, SceneFormat.Stagehand).Document.ToJsonString()).Objects.Count == 1);
        });
        test("Received stage rejects missing cache without modifying source", () => Throws(() => ReceivedStages.Import(snapshot)));
        test("Received stage rejects invalid root transforms", () => Throws(() => ReceivedStages.Import(snapshot with { Rotation = default })));
        test("Received stage rejects relative cache paths", () => Throws(() => ReceivedStages.Import(snapshot with { Definition = Definition("relative.bin") })));
        test("Received stage rejects oversized cache resources", () => {
            string big = Path.Combine(directory, "received-large.bin");
            try { using (var f = File.Create(big)) f.SetLength(ModImport.MaxResourceBytes + 1L); Throws(() => ReceivedStages.Import(snapshot with { Definition = Definition(big) })); }
            finally { File.Delete(big); }
        });
    }
}
