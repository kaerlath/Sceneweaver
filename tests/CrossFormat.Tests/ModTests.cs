using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;

internal static class ModTests
{
    public static void Run(Action<string, Action> test, string temporary)
    {
        string root = Path.Combine(temporary, "mod-source"); Directory.CreateDirectory(Path.Combine(root, "files"));
        void Write(string name, string value) => File.WriteAllText(Path.Combine(root, name), value);
        void Assert(bool condition) { if (!condition) throw new Exception("Mod assertion failed."); }
        void Reject(Action action) { try { action(); } catch (Exception e) when (e is InvalidDataException or IOException or JsonException) { return; } throw new Exception("Expected mod rejection."); }
        Write("meta.json", """{"Name":"Test mod","Version":"1.0"}""");
        Write("default_mod.json", """{"Files":{"bg/test/model/a.mdl":"files/a.mdl","bg/test/material/a.mtrl":"files/a.mtrl","bg/test/texture/a_d.tex":"files/a.tex","vfx/test/a.avfx":"files/a.avfx"},"FileSwaps":{"bg/test/model/swap.mdl":"bg/game/model/original.mdl"},"Manipulations":[]}""");
        foreach (string name in new[] { "a.mdl", "a.mtrl", "a.tex", "a.avfx", "b.mdl", "c.mdl" }) Write("files/" + name, name);
        string metadata = Path.Combine(root, "meta.json");
        ModImportResult? imported = null;
        test("Installed mod imports model, material, texture, VFX and game swap together", () =>
        {
            imported = ModImport.Open(metadata).Build([]);
            Assert(imported.Resources == 5 && imported.Placeable == 3);
            var pack = imported.Pack.Deserialize<EmbeddedModpackDefinition>(StageDefinition.StandardSerializerOptions)!;
            var data = (EmbeddedModResourceDefinition)pack.ModdedResources["bg/test/texture/a_d.tex"];
            Assert(Encoding.UTF8.GetString(EmbeddedModResourceDefinition.DecompressDataBytes(data.CompressedDataBytes, data.CompressionScheme)) == "a.tex");
        });
        test("Mod bindings and bytes survive canonical and upstream Stagehand round trip", () =>
        {
            var scene = new SceneProject(); string id = ModResources.Attach(scene, imported!.Pack);
            scene.Objects.Add(ModResources.CreateObject(id, "bg/test/model/a.mdl")); scene.Objects.Add(ModResources.CreateObject(id, "vfx/test/a.avfx"));
            var canonical = ProjectJson.Load(ProjectJson.Save(scene));
            var exported = SceneCodec.Export(canonical, SceneFormat.Stagehand);
            var upstream = exported.Document.Deserialize<StageDefinition>(StageDefinition.StandardSerializerOptions)!;
            Assert(upstream.EmbeddedModpacks[id].ModdedResources.Count == 5 && upstream.Objects.Values.All(o => o.ModpackId == id));
            var reimported = SceneCodec.Import(exported.Json);
            Assert(ModResources.Packs(reimported)[id]!["SceneweaverImport"] != null);
            Assert(SceneCodec.Export(canonical, SceneFormat.Intoner).Issues.Count(i => i.Omitted) == 2);
        });
        test("Embedded preview resolution returns exact mod bytes, while swaps use game paths", () =>
        {
            string cache = Path.Combine(temporary, "mod-cache");
            var resolved = ModResources.Resolve(imported!.Pack, "BG/TEST/MODEL/A.MDL", cache);
            Assert(resolved.Disk && File.ReadAllText(resolved.Path) == "a.mdl" && resolved.Path.StartsWith(cache));
            var swapped = ModResources.Resolve(imported.Pack, "bg/test/model/swap.mdl", cache);
            Assert(!swapped.Disk && swapped.Path == "bg/game/model/original.mdl");
            Assert(!ModResources.Resolve(imported.Pack, "bg/game/unchanged.mtrl", cache).Disk);
            var diskPack = new JsonObject { ["ModdedResources"] = new JsonObject { ["bg/test/model/a.mdl"] = new JsonObject { ["$type"] = "Disk", ["SourceDiskPath"] = Path.Combine(root, "files/a.mdl") } } };
            Assert(ModResources.Resolve(diskPack, "bg/test/model/a.mdl", cache).Disk);
        });
        test("Missing mod binding prevents invalid Stagehand export", () =>
        {
            var scene = new SceneProject(); scene.Objects.Add(ModResources.CreateObject("missing", "bg/test/a.mdl"));
            Reject(() => SceneCodec.Export(scene, SceneFormat.Stagehand));
        });
        test("Penumbra single and multi options honor explicit choices and priority", () =>
        {
            Write("group_001.json", """{"Name":"Variant","Type":"Single","Priority":0,"DefaultSettings":1,"Options":[{"Name":"A","Files":{"bg/test/model/a.mdl":"files/a.mdl"}},{"Name":"B","Files":{"bg/test/model/a.mdl":"files/b.mdl"}}]}""");
            Write("group_002.json", """{"Name":"Extra","Type":"Multi","Priority":1,"DefaultSettings":0,"Options":[{"Name":"C","Priority":0,"Files":{"bg/test/model/a.mdl":"files/c.mdl"}}]}""");
            var mod = ModImport.Open(metadata); Assert(mod.Groups[0].Defaults.SequenceEqual(new[] { 1 }));
            var b = mod.Build([[1], []]); var c = mod.Build([[1], [0]]);
            Assert(File.ReadAllText(ModResources.Resolve(b.Pack, "bg/test/model/a.mdl", Path.Combine(temporary, "mod-cache")).Path) == "b.mdl");
            Assert(File.ReadAllText(ModResources.Resolve(c.Pack, "bg/test/model/a.mdl", Path.Combine(temporary, "mod-cache")).Path) == "c.mdl");
        });
        test("PMP package imports without extracting archive entry paths", () =>
        {
            string package = Path.Combine(temporary, "mod.pmp"); ZipFile.CreateFromDirectory(root, package);
            var mod = ModImport.Open(package); var result = mod.Build(mod.Groups.Select(g => g.Defaults).ToArray());
            Assert(result.Resources == 5 && result.Pack["DisplayName"]!.GetValue<string>() == "Test mod");
        });
        test("Mod archive traversal and duplicate entries are rejected", () =>
        {
            foreach (bool duplicate in new[] { false, true })
            {
                string path = Path.Combine(temporary, duplicate ? "duplicate.pmp" : "traversal.pmp");
                using (var archive = ZipFile.Open(path, ZipArchiveMode.Create)) { archive.CreateEntry("meta.json"); archive.CreateEntry(duplicate ? "META.JSON" : "../outside"); }
                Reject(() => ModImport.Open(path));
            }
        });
        test("Installed mod traversal, missing resources and metadata edits fail explicitly", () =>
        {
            foreach (var file in new[] { "../outside", "files/missing.mdl" })
            {
                Write("default_mod.json", new JsonObject { ["Files"] = new JsonObject { ["bg/test/a.mdl"] = file } }.ToJsonString());
                var mod = ModImport.Open(metadata); Reject(() => mod.Build(mod.Groups.Select(g => g.Defaults).ToArray()));
            }
            Write("default_mod.json", """{"Manipulations":[{"Type":"Imc"}]}""");
            var manipulated = ModImport.Open(metadata); Reject(() => manipulated.Build(manipulated.Groups.Select(g => g.Defaults).ToArray()));
            Write("group_003.json", """{"Name":"Complex","Type":"Imc","Options":[]}"""); Reject(() => ModImport.Open(metadata));
        });
        test("Mod resource expansion and invalid game paths are bounded", () =>
        {
            Reject(() => ModResources.ReadBounded(new MemoryStream(new byte[100]), 10));
            Reject(() => ModResources.GamePath("bg/../../outside.mdl"));
            Reject(() => ModResources.GamePath("C:/outside.mdl"));
        });
    }
}
