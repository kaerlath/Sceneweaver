using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;
namespace CrossFormat.Core;

public sealed record IntonerSceneSnapshot(string Key, string Name, string Kind, string LayoutJson, string CollectionsJson);
public static class IntonerScenes
{
    public static SceneProject Import(IntonerSceneSnapshot snapshot)
    {
        if (snapshot.LayoutJson.Length > ProjectFiles.MaxInputBytes / 2 || snapshot.CollectionsJson.Length > ProjectFiles.MaxInputBytes / 2) throw new InvalidDataException("Intoner scene exceeds import limits.");
        var scene=SceneCodec.Import(snapshot.LayoutJson);
        var collections=JsonNode.Parse(snapshot.CollectionsJson)?.AsArray() ?? new();
        var used=scene.Objects.Select(o=>o.IntonerSource["Object"]?["CollectionId"]?.GetValue<string>() ?? "").Where(id=>id.Length>0).ToHashSet();
        var ids=new Dictionary<string,string>(); long memoryBytes=0;
        foreach(var collection in collections)
        {
            string id=collection!["CollectionId"]!.GetValue<string>(); if(!used.Contains(id)) continue;
            var resources=new JsonObject();
            foreach(var redirect in collection["Redirects"]!.AsArray())
            {
                string requested=ModResources.GamePath(redirect!["RequestedPath"]!.GetValue<string>());
                var replacement=redirect["Replacement"]!;
                string kind=replacement["Kind"]!.ToString(); string path=replacement["Path"]?.GetValue<string>() ?? "";
                ModResourceDefinition resource;
                switch(kind)
                {
                    case "GamePath": case "1": resource=new GameModResourceDefinition { SourceGamePath=ModResources.GamePath(path) }; break;
                    case "LocalFile": case "2": resource=new DiskModResourceDefinition { SourceDiskPath=path }; break;
                    case "Memory": case "3":
                        string encoded=replacement["Data"]!.GetValue<string>();
                        if(encoded.Length>90_000_000) throw new InvalidDataException("Intoner memory resource exceeds import limit.");
                        byte[] bytes=Convert.FromBase64String(encoded); memoryBytes+=bytes.Length;
                        if(bytes.Length==0 || bytes.Length>ModImport.MaxResourceBytes || memoryBytes>128L*1024*1024) throw new InvalidDataException("Intoner memory resources exceed import limits.");
                        resource=new EmbeddedModResourceDefinition { CompressionScheme=ModCompressionScheme.Zlib, CompressedDataBytes=EmbeddedModResourceDefinition.CompressDataBytes(bytes,ModCompressionScheme.Zlib) }; break;
                    default: throw new InvalidDataException("Unsupported Intoner replacement kind: "+kind);
                }
                resources.Add(requested,JsonSerializer.SerializeToNode<ModResourceDefinition>(resource,StageDefinition.StandardSerializerOptions));
            }
            ids.Add(id,ModResources.Attach(scene,new JsonObject { ["DisplayName"]=collection["Name"]?.DeepClone() ?? JsonValue.Create(id), ["ModdedResources"]=resources }));
        }
        foreach(var item in scene.Objects)
        {
            string collection=item.IntonerSource["Object"]?["CollectionId"]?.GetValue<string>() ?? "";
            if(collection.Length==0) continue;
            if(!ids.TryGetValue(collection,out var pack)) throw new InvalidDataException($"{item.Name}: mod collection could not be captured. Nothing was imported.");
            item.StagehandSource["ModpackId"]=pack;
        }
        // Reuse the bounded cache embedding path without converting away Intoner-specific data.
        var copied=ReceivedStages.Import(new(snapshot.Key,snapshot.Name,"Intoner",JsonSerializer.Serialize(scene,ProjectJson.Options),Vector3.Zero,Quaternion.Identity,1), 128L*1024*1024-memoryBytes);
        copied.ImportNotes.RemoveAt(copied.ImportNotes.Count-1);
        copied.ImportNotes.Add($"Independent snapshot of Intoner {snapshot.Kind} '{snapshot.Name}' ({snapshot.Key}). Resources copied from the active Intoner collections. Original collection IDs are retained as metadata; portable mod bindings are available in Sceneweaver and Stagehand exports.");
        return copied;
    }
}
