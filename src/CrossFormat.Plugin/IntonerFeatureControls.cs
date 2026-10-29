using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
namespace CrossFormat.Plugin;
public sealed partial class Plugin
{
    private readonly Dictionary<string,string> intonerDrafts = new();
    private JsonObject IntonerPayload(SceneObject item)
    {
        if(item.IntonerSource["Object"] is JsonObject existing) return (JsonObject)existing.DeepClone();
        var copy=JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(item,ProjectJson.Options),ProjectJson.Options)!;
        copy.ParentId=null; copy.StagehandSource=new();
        return (JsonObject)SceneCodec.Export(new SceneProject {Objects=[copy]},SceneFormat.Intoner).Document["Objects"]![0]!["Object"]!.DeepClone();
    }
    private void DrawIntonerFeatures(SceneObject item)
    {
        if(item.Kind is AssetKind.Unknown or AssetKind.Sound or AssetKind.Weapon or AssetKind.Group) return;
        if(!ImGui.CollapsingHeader("Intoner location and collision")) return;
        ImGui.TextWrapped("Saved for Intoner 0.3+. Collision and location rules do not control the Stagehand live preview.");
        var raw=IntonerPayload(item);
        void Save()=>Edit(()=>item.IntonerSource["Object"]=raw.DeepClone());
        if(item.Kind is AssetKind.BgObject or AssetKind.Furniture)
        {
            var model=raw["Model"]![item.Kind.ToString()]!.AsObject();
            var camera=model["CameraCollision"]?.GetValue<bool>() ?? true;
            if(ImGui.Checkbox("Camera collision (Intoner)",ref camera)) {model["CameraCollision"]=camera;Save();}
            if(model["ColliderDefinition"] is JsonObject collider)
            {
                ImGui.TextWrapped("Collider: "+collider["Kind"]+" — "+collider["PcbPath"]);
                ImGui.TextWrapped("Imported collider retained. Bounds and offsets below use model-local coordinates.");
                if(collider["Shape"] is JsonObject shape)
                {
                    foreach(var key in new[]{"Translation","Rotation","Scale","BoundsMin","BoundsMax"})
                    {
                        var node=shape[key]!;
                        var v=new System.Numerics.Vector3(node["X"]!.GetValue<float>(),node["Y"]!.GetValue<float>(),node["Z"]!.GetValue<float>());
                        if(ImGui.DragFloat3(key=="Rotation"?"Collider rotation (radians)":"Collider "+key,ref v,.01f))
                        { shape[key]=new JsonObject {["X"]=v.X,["Y"]=v.Y,["Z"]=v.Z}; Save(); }
                    }
                }
                if(ImGui.Button("Remove assigned collider")) {model["ColliderDefinition"]=null;Save();}
            }
            else ImGui.TextDisabled("No assigned collider. Import an Intoner model with a collider to edit it here.");
        }
        var location=IntonerLocationConverter.Upgrade(raw["CreatedIn"]!).AsObject();
        var worlds=location["Worlds"] as JsonObject;
        var unrestricted=worlds==null || worlds["AllDataCenters"]!.GetValue<bool>();
        ImGui.TextWrapped(worlds==null?"Worlds: unassigned":unrestricted?"Worlds: all data centers":"Worlds: selected data centers and worlds");
        ImGui.TextWrapped("IDs are Intoner location IDs. Press Enter to apply. Housing address fields work together with the world selection.");
        string dc=string.Join(",",worlds?["DataCenterIds"]?.AsArray().Select(n=>n!.ToString()) ?? []);
        string ids=string.Join(",",worlds?["WorldIds"]?.AsArray().Select(n=>n!.ToString()) ?? []);
        dc=intonerDrafts.GetValueOrDefault(item.Id+"dc",dc); ids=intonerDrafts.GetValueOrDefault(item.Id+"worlds",ids);
        bool apply=ImGui.InputText("Data center IDs (comma separated)",ref dc,512,ImGuiInputTextFlags.EnterReturnsTrue);
        apply|=ImGui.InputText("World IDs (comma separated)",ref ids,512,ImGuiInputTextFlags.EnterReturnsTrue);
        intonerDrafts[item.Id+"dc"]=dc; intonerDrafts[item.Id+"worlds"]=ids;
        if(apply) Run(()=>
        {
            uint[] centers=dc.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(uint.Parse).ToArray();
            ushort[] servers=ids.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(ushort.Parse).ToArray();
            var selection=new Intoner.Objects.Api.ObjectWorldSelection(false,System.Collections.Immutable.ImmutableArray.CreateRange(centers),System.Collections.Immutable.ImmutableArray.CreateRange(servers));
            location["Worlds"]=JsonSerializer.SerializeToNode(selection,ProjectJson.Options); raw["CreatedIn"]=location; Save();
        });
        if(ImGui.Button("Allow all worlds")) {location["Worlds"]=new JsonObject {["AllDataCenters"]=true,["DataCenterIds"]=new JsonArray(),["WorldIds"]=new JsonArray()};raw["CreatedIn"]=location;intonerDrafts.Remove(item.Id+"dc");intonerDrafts.Remove(item.Id+"worlds");Save();}
        foreach(var key in new[]{"TerritoryId","DivisionId","WardId","HouseId","RoomId"})
        {
            var value=intonerDrafts.GetValueOrDefault(item.Id+key,location[key]!.GetValue<uint>().ToString());
            if(ImGui.InputText(key+" (Intoner)",ref value,16,ImGuiInputTextFlags.EnterReturnsTrue)) Run(()=>{location[key]=uint.Parse(value);raw["CreatedIn"]=location;Save();});
            intonerDrafts[item.Id+key]=value;
        }
    }
}
