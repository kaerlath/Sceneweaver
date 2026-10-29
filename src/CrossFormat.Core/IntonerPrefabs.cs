using System.Text.Json;
using System.Text.Json.Nodes;
using Intoner.Objects.Api;
namespace CrossFormat.Core;
public static class IntonerPrefabs
{
    private static JsonNode? Pascal(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(p=>new KeyValuePair<string,JsonNode?>(p.Key.StartsWith('$')?p.Key:char.ToUpperInvariant(p.Key[0])+p.Key[1..],Pascal(p.Value)))),
        JsonArray arr => new JsonArray(arr.Select(Pascal).ToArray()), _ => node?.DeepClone()
    };
    public static SceneProject Import(JsonObject input)
    {
        var root=Pascal(input)!.AsObject();
        if(root["Version"]!.GetValue<int>() is not (1 or 2)) throw new InvalidDataException("Unsupported Intoner prefab version.");
        var entries=root["Entries"]!.AsArray();
        if(entries.Count==0) throw new InvalidDataException("Intoner prefab has no entries.");
        var ids=entries.Select(e=>e!["Id"]!.GetValue<Guid>()).ToHashSet();
        if(ids.Count!=entries.Count || ids.Contains(Guid.Empty)) throw new InvalidDataException("Invalid prefab identities.");
        var parents=entries.ToDictionary(e=>e!["Id"]!.GetValue<Guid>(),e=>e!["AttachmentParentEntryId"]?.GetValue<Guid>());
        foreach(var id in ids)
        {
            var visited=new HashSet<Guid>(); var cursor=id;
            while(parents[cursor] is Guid parent)
            {
                if(!ids.Contains(parent) || !visited.Add(cursor)) throw new InvalidDataException("Prefab attachments contain a missing parent or cycle.");
                cursor=parent;
            }
        }
        var wrappers=new JsonArray();
        foreach(var entry in entries)
        {
            var preset=entry!["Preset"]!; var model=preset["Model"]!.AsObject();
            string kind=model["$type"]?.GetValue<string>() switch {"bgobject"=>"BgObject","vfx"=>"Vfx","light"=>"Light","furniture"=>"Furniture",_=>throw new InvalidDataException("Unsupported prefab model type.")};
            var payload=(JsonObject)model.DeepClone(); payload.Remove("$type");
            if(kind=="Furniture")
            {
                var parent=entry["AttachmentParentEntryId"]?.GetValue<Guid>();
                if(parent is Guid p && !ids.Contains(p)) throw new InvalidDataException("Prefab attachment references a missing entry.");
                payload["AttachmentParentId"]=parent is Guid owner?JsonValue.Create(owner):null;
            }
            if(entry["WorldTransform"] is not JsonObject transform) throw new InvalidDataException("Prefab entry lacks its saved world transform.");
            wrappers.Add(new JsonObject {["FolderPath"]="",["Locked"]=false,["GroupId"]=null,["Object"]=new JsonObject {
                ["Id"]=entry["Id"]!.DeepClone(),["Name"]=entry["Name"]!.DeepClone(),["Kind"]=kind,
                ["Visible"]=preset["Visible"]!.DeepClone(),["Transform"]=transform.DeepClone(),
                ["CreatedAtUtc"]=entry["CreatedAtUtc"]!.DeepClone(),["CreatedIn"]=root["CapturedIn"]!.DeepClone(),["CollectionId"]="",
                ["Model"]=new JsonObject {[kind]=payload}}});
        }
        var document=new JsonObject {["DocumentKind"]="object-layout",["FormatVersion"]=3,["Id"]=root["Id"]!.DeepClone(),["Name"]=root["Name"]!.DeepClone(),
            ["Revision"]=1,["CreatedAtUtc"]=root["CreatedAtUtc"]!.DeepClone(),["UpdatedAtUtc"]=root["CreatedAtUtc"]!.DeepClone(),["ExportedAtUtc"]=root["CreatedAtUtc"]!.DeepClone(),
            ["Folders"]=new JsonArray(),["Groups"]=new JsonArray(),["Objects"]=wrappers};
        var scene=SceneCodec.Import(document.ToJsonString());
        scene.IntonerRoot["SceneweaverOriginalPrefab"]=input.DeepClone();
        scene.ImportNotes.Add("Imported Intoner prefab with saved world placement. Insert as group to relocate the assembly. Original prefab metadata is retained; saves export a layout, not an Intoner library prefab.");
        return scene;
    }
}
