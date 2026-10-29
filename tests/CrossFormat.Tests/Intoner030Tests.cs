using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
internal static class Intoner030Tests
{
    public static void Run(Action<string,Action> test)
    {
        void Assert(bool value) {if(!value)throw new Exception("Intoner 0.3 assertion failed");}
        void Reject(Action action) {try{action();}catch{return;}throw new Exception("Expected rejection");}
        JsonObject Layout()
        {
            var s=new SceneProject {Name="Compatibility"};
            s.Objects.Add(new SceneObject {Name="Rock",Kind=AssetKind.BgObject,AssetPath="bg/test.mdl"});
            return SceneCodec.Export(s,SceneFormat.Intoner).Document;
        }
        JsonNode? Camel(JsonNode? n)=>n switch { JsonObject o=>new JsonObject(o.Select(p=>new KeyValuePair<string,JsonNode?>(p.Key.StartsWith('$')?p.Key:char.ToLowerInvariant(p.Key[0])+p.Key[1..],Camel(p.Value)))),JsonArray a=>new JsonArray(a.Select(Camel).ToArray()),_=>n?.DeepClone() };
        JsonObject Worlds()=>new(){["AllDataCenters"]=false,["DataCenterIds"]=new JsonArray(1u,2u),["WorldIds"]=new JsonArray((ushort)21,(ushort)22)};
        test("Intoner 0.3 world selections survive canonical and native round trips",()=>{
            var input=Layout(); input["Objects"]![0]!["Object"]!["CreatedIn"]!["Worlds"]=Worlds();
            var scene=ProjectJson.Load(ProjectJson.Save(SceneCodec.Import(input.ToJsonString())));
            var output=SceneCodec.Export(scene,SceneFormat.Intoner).Document;
            Assert(output["FormatVersion"]!.GetValue<int>()==3);
            Assert(JsonNode.DeepEquals(Worlds(),output["Objects"]![0]!["Object"]!["CreatedIn"]!["Worlds"]));
            Assert(SceneCodec.Import(output.ToJsonString()).Objects.Count==1);
        });
        test("Legacy locations upgrade without broadening world restrictions",()=>{
            foreach(int version in new[]{1,2}) {
                var input=Layout(); input["FormatVersion"]=version;
                if(version==1)input["FolderColors"]=new JsonObject();
                input["Folders"]=new JsonArray();
                var location=input["Objects"]![0]!["Object"]!["CreatedIn"]!.AsObject(); location.Remove("Worlds");location["WorldId"]=21;location["WorldName"]="Old";
                var output=SceneCodec.Export(SceneCodec.Import(input.ToJsonString()),SceneFormat.Intoner).Document;
                Assert(output["Objects"]![0]!["Object"]!["CreatedIn"]!["Worlds"]!["WorldIds"]![0]!.GetValue<int>()==21);
            }
        });
        test("Ambiguous and malformed world selections are rejected",()=>{
            var input=Layout(); var location=input["Objects"]![0]!["Object"]!["CreatedIn"]!.AsObject();
            location["WorldId"]=21;Reject(()=>SceneCodec.Import(input.ToJsonString()));location.Remove("WorldId");
            var w=Worlds();w["AllDataCenters"]=true;location["Worlds"]=w;Reject(()=>SceneCodec.Import(input.ToJsonString()));
            location.Remove("Worlds");Reject(()=>SceneCodec.Import(input.ToJsonString()));
        });
        test("Camera collision and collider payload survive editing and export",()=>{
            var input=Layout(); var model=input["Objects"]![0]!["Object"]!["Model"]!["BgObject"]!;
            model["CameraCollision"]=false; model["ColliderDefinition"]=new JsonObject {["ModelPath"]="bg/test.mdl",["SourcePath"]="bg/source.sgb",["Kind"]="Mesh",["PcbPath"]="bg/test.pcb",["MaterialId"]=123ul,["MaterialMask"]=456ul,["Shape"]=null};
            var scene=SceneCodec.Import(input.ToJsonString());scene.Objects[0].Position=new(1,2,3);
            var output=SceneCodec.Export(scene,SceneFormat.Intoner).Document["Objects"]![0]!["Object"]!["Model"]!["BgObject"]!;
            Assert(output["CameraCollision"]!.GetValue<bool>()==false); Assert(JsonNode.DeepEquals(output["ColliderDefinition"],model["ColliderDefinition"]));
        });
        test("Intoner prefab versions import and insert as movable assemblies",()=>{
            foreach(int version in new[]{1,2}) {
                var source=Layout(); var obj=source["Objects"]![0]!["Object"]!; var model=(JsonObject)obj["Model"]!["BgObject"]!.DeepClone();model["$type"]="bgobject";
                var captured=obj["CreatedIn"]!.DeepClone(); if(version==1){captured.AsObject().Remove("Worlds");captured["WorldId"]=21;captured["WorldName"]="Old";}
                var input=new JsonObject {["version"]=version,["id"]=Guid.NewGuid(),["name"]="Prefab",["createdAtUtc"]=DateTime.UtcNow,["capturedIn"]=captured,
                    ["entries"]=new JsonArray(new JsonObject {["id"]=Guid.NewGuid(),["name"]="Rock",["createdAtUtc"]=DateTime.UtcNow,["worldTransform"]=obj["Transform"]!.DeepClone(),["preset"]=new JsonObject {["visible"]=true,["model"]=model}})};
                var scene=SceneCodec.Import(Camel(input)!.ToJsonString()); Assert(scene.Objects.Count==1 && scene.IntonerRoot["SceneweaverOriginalPrefab"]!=null);
                var merged=SceneMerge.Insert(new(),scene,new(4,5,6)); Assert(merged.Project.Objects.Count==2);
                input["entries"]![0]!["worldTransform"]=null;Reject(()=>SceneCodec.Import(input.ToJsonString()));
            }
        });
    }
}
