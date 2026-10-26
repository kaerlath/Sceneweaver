using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace CrossFormat.Core;

public sealed record SceneInsertion(SceneProject Project, Guid GroupId, int ObjectCount);
public static class SceneMerge
{
    private static SceneProject Clone(SceneProject s)=>ProjectJson.Load(JsonSerializer.Serialize(s,ProjectJson.Options));
    public static SceneInsertion Insert(SceneProject destination,SceneProject source,Vector3 destinationLocalPosition)
    {
        ProjectJson.Validate(destination); ProjectJson.Validate(source);
        if(source.Objects.Count==0) throw new InvalidDataException("The selected scene has no objects to insert.");
        var result=Clone(destination);
        var incoming=ReceivedStages.Import(new(source.Id.ToString(),source.Name,"",JsonSerializer.Serialize(source,ProjectJson.Options),source.StageTranslation,TransformMath.Rotation(source.StageRotationDegrees),source.StageUniformScale));
        incoming.ImportNotes=source.ImportNotes.ToList();
        var ids=incoming.Objects.ToDictionary(o=>o.Id,_=>Guid.NewGuid());
        var packIds=new Dictionary<string,string>();
        foreach(var pack in ModResources.Packs(incoming))
            packIds.Add(pack.Key,ModResources.Attach(result,pack.Value!.AsObject()));
        var points=incoming.Objects.Where(o=>o.Kind is not (AssetKind.Group or AssetKind.Unknown)).Select(o=>TransformMath.ToWorld(incoming,o).Position).ToArray();
        var pivot=points.Length==0?Vector3.Zero:points.Aggregate(Vector3.Zero,(a,b)=>a+b)/points.Length;
        var group=new SceneObject { Kind=AssetKind.Group,Name=source.Name,Position=destinationLocalPosition,
            RotationDegrees=TransformMath.Degrees(Quaternion.Inverse(TransformMath.Rotation(destination.StageRotationDegrees))),Scale=new(1/destination.StageUniformScale) };
        // Keep the source's arrangement and world orientation, centered on its object origins.
        var rootTransforms=incoming.Objects.Where(o=>o.ParentId==null).ToDictionary(o=>o.Id,o=>TransformMath.ToWorld(incoming,o));
        foreach(var item in incoming.Objects)
        {
            var oldId=item.Id; var oldParent=item.ParentId;
            if(oldParent==null)
            {
                var t=rootTransforms[oldId]; item.Position=t.Position-pivot; item.RotationDegrees=t.Rotation; item.Scale=t.Scale;
            }
            item.Id=ids[oldId]; item.ParentId=oldParent is Guid p?ids[p]:group.Id; item.StagehandId="";
            var pack=ModResources.PackId(item);
            if(pack.Length>0)
            {
                if(!packIds.TryGetValue(pack,out var mapped)) throw new InvalidDataException($"{item.Name}: source modpack is missing.");
                item.StagehandSource["ModpackId"]=mapped;
                if(item.Light?.ContainsKey("ModpackId")==true) item.Light["ModpackId"]=mapped;
            }
            Remap(item.IntonerSource,"GroupId",ids);
            if(item.IntonerSource["Group"] is JsonObject rawGroup) Remap(rawGroup,"Id",ids);
            if(item.IntonerSource["Object"] is JsonObject raw)
            {
                Remap(raw,"Id",ids);
                if(raw["Model"]?["Furniture"] is JsonObject furniture) Remap(furniture,"AttachmentParentId",ids);
            }
        }
        result.Objects.Add(group); result.Objects.AddRange(incoming.Objects);
        // Preserve source-only metadata without replacing destination-level settings.
        result.StagehandRoot["SceneweaverInsertedSources"] ??= new JsonObject();
        var stageMetadata=(JsonObject)incoming.StagehandRoot.DeepClone(); stageMetadata.Remove("Objects"); stageMetadata.Remove("EmbeddedModpacks");
        var intonerMetadata=(JsonObject)incoming.IntonerRoot.DeepClone(); intonerMetadata.Remove("Objects"); intonerMetadata.Remove("Groups");
        result.StagehandRoot["SceneweaverInsertedSources"]![group.Id.ToString()]=new JsonObject { ["Stagehand"]=stageMetadata,["Intoner"]=intonerMetadata };
        if(incoming.Objects.Any(o=>o.IntonerSource["Group"]!=null)) result.IntonerRoot["FormatVersion"]=3;
        result.ImportNotes.Add($"Inserted '{source.Name}' as a group ({source.Objects.Count} entries). Pivot is the mean of object origins. Opaque source metadata is preserved; unknown ID references cannot be remapped automatically.");
        result.ImportNotes.AddRange(source.ImportNotes);
        ProjectJson.Validate(result);
        if(JsonSerializer.SerializeToUtf8Bytes(result,ProjectJson.Options).Length>ProjectFiles.MaxInputBytes) throw new InvalidDataException("Combined project exceeds the 256 MB save limit.");
        return new(result,group.Id,source.Objects.Count);
    }
    private static void Remap(JsonObject value,string key,Dictionary<Guid,Guid> ids)
    {
        if(value[key] is JsonValue node && node.TryGetValue<Guid>(out var id) && ids.TryGetValue(id,out var mapped)) value[key]=mapped;
    }
}
