using System.Text.Json.Nodes;
using CrossFormat.Assets;
using CrossFormat.Core;
using Lumina;
using Lumina.Data.Files;

internal static class ModPreviewSmoke
{
    public static void Run(GameData game, string output)
    {
        const string original = "bg/ex2/01_gyr_g3/twn/g3t1/bgparts/g3t1_p0_tre4c.mdl";
        const string target = "bg/sceneweaver-test/overridden.mdl";
        var file = game.GetFile<MdlFile>(original)!;
        var baseline = ModelGeometry.Decode(file, p => game.GetFile<MtrlFile>(p), original);
        string source = Path.Combine(output, "private-mod-fixture"); Directory.CreateDirectory(source);
        var files = new JsonObject();
        void Store(string gamePath, byte[] bytes)
        {
            string local = $"resource-{files.Count}" + Path.GetExtension(gamePath);
            File.WriteAllBytes(Path.Combine(source, local), bytes); files[gamePath] = local;
        }
        Store(target, file.Data);
        foreach (var material in baseline.Materials)
        {
            if (files.ContainsKey(material.Name)) continue;
            var mtrl = game.GetFile<MtrlFile>(material.Name)!; Store(material.Name, mtrl.Data);
            foreach (var offset in mtrl.TextureOffsets)
            {
                int end = Array.IndexOf(mtrl.Strings, (byte)0, offset.Offset);
                string path = System.Text.Encoding.UTF8.GetString(mtrl.Strings, offset.Offset, end - offset.Offset);
                if (files.ContainsKey(path)) continue;
                var texture = game.GetFile<TexFile>(path); if (texture != null) Store(path, texture.Data);
            }
        }
        File.WriteAllText(Path.Combine(source, "meta.json"), """{"Name":"Private local game-data smoke fixture"}""");
        File.WriteAllText(Path.Combine(source, "default_mod.json"), new JsonObject { ["Files"] = files }.ToJsonString());
        var imported = ModImport.Open(Path.Combine(source, "meta.json")).Build([]);
        var scene = new SceneProject(); string id = ModResources.Attach(scene, imported.Pack);
        scene.Objects.Add(ModResources.CreateObject(id, target));
        var roundTrip = SceneCodec.Import(SceneCodec.Export(ProjectJson.Load(ProjectJson.Save(scene)), SceneFormat.Stagehand).Json);
        var pack = (JsonObject)ModResources.Packs(roundTrip)[id]!;
        string cache = Path.Combine(output, "private-mod-cache");
        var modelPath = ModResources.Resolve(pack, target, cache);
        if (!modelPath.Disk || game.FileExists(target)) throw new Exception("Mod test did not exercise an isolated replacement.");
        var modModel = game.GetFileFromDisk<MdlFile>(modelPath.Path)!;
        int materialLoads = 0;
        var modMesh = ModelGeometry.Decode(modModel, p =>
        {
            var materialPath = ModResources.Resolve(pack, p, cache);
            if (!materialPath.Disk) throw new Exception("Preview silently fell back to a vanilla material.");
            materialLoads++; return game.GetFileFromDisk<MtrlFile>(materialPath.Path);
        }, target);
        if (materialLoads == 0 || modMesh.Indices.Length != baseline.Indices.Length || modMesh.Size != baseline.Size) throw new Exception("Mod preview geometry/material mismatch.");
        foreach (var material in modMesh.Materials.Where(m => m.TexturePath != null))
        {
            var texture = ModResources.Resolve(pack, material.TexturePath!, cache);
            if (!texture.Disk || game.GetFileFromDisk<TexFile>(texture.Path)!.ImageData.Length == 0) throw new Exception("Mod texture did not resolve and decode.");
        }
        Console.WriteLine($"Real mod preview passed: model at a nonexistent game path, {materialLoads} embedded materials and all selected color textures resolved after canonical/Stagehand round trip.");
    }
}
