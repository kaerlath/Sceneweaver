using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Intoner.Objects.Api;
namespace CrossFormat.Core;

public static class IntonerGroups
{
    public static void Import(SceneProject scene, JsonObject root)
    {
        if (root["Groups"] is not null and not JsonArray) throw new InvalidDataException("Intoner Groups must be an array.");
        foreach(var value in root["Groups"] as JsonArray ?? [])
        {
            var raw=value?.AsObject() ?? throw new InvalidDataException("Invalid Intoner group.");
            var t=raw["Transform"]!.Deserialize<WorldObjectTransform>(ProjectJson.Options)!;
            Vector3 V(ObjectVector3 v)=>new(v.X,v.Y,v.Z);
            scene.Objects.Add(new SceneObject { Id=raw["Id"]!.GetValue<Guid>(), Kind=AssetKind.Group,
                Name=raw["Name"]!.GetValue<string>(), Folder=raw["FolderPath"]!.GetValue<string>(),
                Locked=raw["Locked"]!.GetValue<bool>(), Visible=raw["Visible"]!.GetValue<bool>(),
                Position=V(t.Position), RotationDegrees=V(t.RotationDegrees), Scale=V(t.Scale),
                IntonerSource=new JsonObject { ["Group"]=raw.DeepClone() } });
        }
        ProjectJson.Validate(scene);
        foreach(var item in scene.Objects.Where(o=>o.Kind!=AssetKind.Group))
            if(item.IntonerSource["GroupId"] is JsonValue id)
            {
                var group=id.GetValue<Guid>();
                if(!scene.Objects.Any(o=>o.Id==group && o.Kind==AssetKind.Group)) throw new InvalidDataException("Intoner object references a missing group.");
                SceneHierarchy.Reparent(scene,item,group);
            }
    }
    public static JsonArray Export(SceneProject scene)
    {
        var result=new JsonArray();
        foreach(var group in scene.Objects.Where(o=>o.Kind==AssetKind.Group && o.IntonerSource["Group"] is JsonObject))
        {
            var raw=(JsonObject)group.IntonerSource["Group"]!.DeepClone();
            var t=TransformMath.ToWorld(scene,group);
            ObjectVector3 V(Vector3 v)=>new(v.X,v.Y,v.Z);
            raw["Id"]=group.Id; raw["Name"]=group.Name; raw["FolderPath"]=group.Folder; raw["Locked"]=group.Locked;
            raw["Visible"]=SceneHierarchy.Visible(scene,group);
            raw["Transform"]=JsonSerializer.SerializeToNode(new WorldObjectTransform(V(t.Position),V(t.Rotation),V(t.Scale)),ProjectJson.Options);
            result.Add(raw);
        }
        return result;
    }
}
