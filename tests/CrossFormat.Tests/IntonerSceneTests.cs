using System.Numerics;
using System.Text.Json.Nodes;
using CrossFormat.Core;
internal static class IntonerSceneTests
{
    public static void Run(Action<string,Action> test,string directory)
    {
        void Assert(bool v) { if(!v) throw new Exception("Intoner scene assertion failed."); }
        void Reject(Action a) { try {a();} catch {return;} throw new Exception("Expected rejection"); }
        JsonObject Layout()
        {
            var scene=new SceneProject {Name="Intoner test"};
            scene.Objects.Add(new SceneObject {Name="Stone",Kind=AssetKind.BgObject,AssetPath="bg/test.mdl",Position=new(12,4,0)});
            var root=SceneCodec.Export(scene,SceneFormat.Intoner).Document; root["FormatVersion"]=3;
            var id=Guid.NewGuid();
            root["Groups"]=new JsonArray(new JsonObject { ["Id"]=id,["Name"]="Group",["FolderPath"]="Folder",["Locked"]=true,["Visible"]=true,
                ["CreatedAtUtc"]=DateTime.UtcNow,["CreatedIn"]=root["Objects"]![0]!["Object"]!["CreatedIn"]!.DeepClone(),
                ["Transform"]=new JsonObject { ["Position"]=new JsonObject { ["X"]=10,["Y"]=0,["Z"]=0 },["RotationDegrees"]=new JsonObject { ["X"]=0,["Y"]=90,["Z"]=0 },["Scale"]=new JsonObject { ["X"]=2,["Y"]=2,["Z"]=2 } },["FutureGroupField"]=42 });
            root["Objects"]![0]!["GroupId"]=id; return root;
        }
        test("Intoner v3 groups preserve world pose, pivot, lock and metadata across both formats",()=> {
            var root=Layout(); var scene=SceneCodec.Import(root.ToJsonString());
            var item=scene.Objects.Single(o=>o.Kind==AssetKind.BgObject); var group=scene.Objects.Single(o=>o.Kind==AssetKind.Group);
            Assert(item.ParentId==group.Id && group.Locked);
            Assert(Vector3.Distance(TransformMath.ToWorld(scene,item).Position,new(12,4,0))<.001f);
            group.Position+=new Vector3(7,0,0);
            var output=SceneCodec.Export(scene,SceneFormat.Intoner); Assert(output.Document["FormatVersion"]!.GetValue<int>()==3);
            Assert(output.Document["Groups"]![0]!["FutureGroupField"]!.GetValue<int>()==42);
            var copy=SceneCodec.Import(output.Json); var obj=copy.Objects.Single(o=>o.Kind==AssetKind.BgObject);
            Assert(Vector3.Distance(TransformMath.ToWorld(copy,obj).Position,new(19,4,0))<.001f);
            var stage=SceneCodec.Import(SceneCodec.Export(scene,SceneFormat.Stagehand).Json);
            Assert(Vector3.Distance(TransformMath.ToWorld(stage,stage.Objects.Single(o=>o.Kind==AssetKind.BgObject)).Position,new(19,4,0))<.001f);
        });
                test("Intoner v3 hidden groups do not permanently hide their members",()=> {
            var root=Layout(); root["Groups"]![0]!["Visible"]=false;
            var scene=SceneCodec.Import(root.ToJsonString());
            var copy=SceneCodec.Import(SceneCodec.Export(scene,SceneFormat.Intoner).Json);
            var item=copy.Objects.Single(o=>o.Kind==AssetKind.BgObject); Assert(item.Visible && !SceneHierarchy.Visible(copy,item));
            copy.Objects.Single(o=>o.Kind==AssetKind.Group).Visible=true; Assert(SceneHierarchy.Visible(copy,item));
        });
        test("Intoner v3 rejects orphan and duplicate groups",()=> {
            var root=Layout(); root["Objects"]![0]!["GroupId"]=Guid.NewGuid(); Reject(()=>SceneCodec.Import(root.ToJsonString()));
            root=Layout(); root["Groups"]!.AsArray().Add(root["Groups"]![0]!.DeepClone()); Reject(()=>SceneCodec.Import(root.ToJsonString()));
        });
        test("Intoner v3 preserves empty groups and accepts omitted folders",()=> {
            var root=Layout(); root["Objects"]=new JsonArray(); root.Remove("Folders");
            var scene=SceneCodec.Import(root.ToJsonString()); Assert(scene.Objects.Count==1);
            Assert(SceneCodec.Export(scene,SceneFormat.Intoner).Document["Groups"]!.AsArray().Count==1);
        });
        JsonArray Collections(JsonObject replacement)=>new(new JsonObject { ["CollectionId"]="mod",["Name"]="Memory mod",["Redirects"]=new JsonArray(new JsonObject { ["RequestedPath"]="bg/test.mdl",["Replacement"]=replacement }) });
        test("Intoner memory mod snapshots stay portable and retain group organization",()=> {
            var root=Layout(); root["Objects"]![0]!["Object"]!["CollectionId"]="mod";
            var resources=Collections(new JsonObject { ["Kind"]="Memory",["Path"]="memory-resource",["Data"]=Convert.ToBase64String([1,3,5,7]) });
            var scene=IntonerScenes.Import(new("source","Test","temporary scene",root.ToJsonString(),resources.ToJsonString()));
            var item=scene.Objects.Single(o=>o.Kind==AssetKind.BgObject); Assert(item.ParentId!=null);
            var pack=ModResources.Packs(scene)[ModResources.PackId(item)]!.AsObject();
            var path=ModResources.Resolve(pack,"bg/test.mdl",Path.Combine(directory,"intoner-memory")).Path;
            Assert(File.ReadAllBytes(path).SequenceEqual(new byte[]{1,3,5,7}));
            Assert(SceneCodec.Export(scene,SceneFormat.Stagehand).Document["EmbeddedModpacks"]!.AsObject().Count==1);
        });
        test("Intoner missing collections and empty memory resources abort import",()=> {
            var root=Layout(); root["Objects"]![0]!["Object"]!["CollectionId"]="missing";
            Reject(()=>IntonerScenes.Import(new("source","Test","temporary",root.ToJsonString(),"[]")));
            root["Objects"]![0]!["Object"]!["CollectionId"]="mod";
            Reject(()=>IntonerScenes.Import(new("source","Test","temporary",root.ToJsonString(),Collections(new JsonObject { ["Kind"]=3,["Data"]="" }).ToJsonString())));
        });
    }
}
