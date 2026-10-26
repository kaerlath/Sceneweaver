using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;
internal static class SceneMergeTests
{
    public static void Run(Action<string,Action> test,string directory)
    {
        void Assert(bool b) {if(!b) throw new Exception("Insertion assertion failed.");}
        void Near(Vector3 a,Vector3 b) {Assert(Vector3.Distance(a,b)<.002f);}
        void Reject(Action a) {try {a();} catch{return;} throw new Exception("Expected rejection");}
        SceneProject Ship()
        {
            var s=new SceneProject {Name="Ship",StageTranslation=new(50,20,10),StageRotationDegrees=new(0,37,0),StageUniformScale=2};
            for(int i=0;i<71;i++) s.Objects.Add(new SceneObject {Name="Part "+i,AssetPath="bg/part.mdl",Position=new(i, i%3,0)});
            return s;
        }
        test("Insert 71-piece assembly preserves world arrangement and destination identity",()=>{
            var source=Ship(); var dest=new SceneProject {Name="Cave",StageTranslation=new(100,0,0),StageRotationDegrees=new(0,81,0),StageUniformScale=3};
            dest.Objects.Add(new SceneObject {Name="Existing",AssetPath="bg/existing.mdl"});
            string before=JsonSerializer.Serialize(dest,ProjectJson.Options); string oldSource=JsonSerializer.Serialize(source,ProjectJson.Options);
            var inserted=SceneMerge.Insert(dest,source,new(2,3,4)); var p=inserted.Project;
            Assert(p.Id==dest.Id && p.Name==dest.Name && p.Objects.Count==73 && inserted.ObjectCount==71);
            Assert(before==JsonSerializer.Serialize(dest,ProjectJson.Options) && oldSource==JsonSerializer.Serialize(source,ProjectJson.Options));
            var a=p.Objects.Single(o=>o.Name=="Part 0"); var b=p.Objects.Single(o=>o.Name=="Part 70");
            Near(TransformMath.ToWorld(p,b).Position-TransformMath.ToWorld(p,a).Position,TransformMath.ToWorld(source,source.Objects[70]).Position-TransformMath.ToWorld(source,source.Objects[0]).Position);
            Near(TransformMath.ToWorld(p,a).Scale,TransformMath.ToWorld(source,source.Objects[0]).Scale);
            Assert(a.ParentId==inserted.GroupId && !source.Objects.Any(o=>o.Id==a.Id));
            var second=SceneMerge.Insert(p,source,Vector3.Zero); Assert(second.Project.Objects.Count==145);
            Assert(second.Project.Objects.Select(o=>o.Id).Distinct().Count()==145);
            var restored=ProjectJson.Load(before); Assert(restored.Objects.Count==1); // Same full-project snapshot used by Undo.
        });
        test("Insertion remaps nested parents and furniture links",()=>{
            var s=Ship(); var parent=new SceneObject {Kind=AssetKind.Group,Name="Nested",Position=new(4,1,2)}; s.Objects.Add(parent);
            s.Objects[0].ParentId=parent.Id;
            s.Objects[1].IntonerSource=new JsonObject { ["Object"]=new JsonObject { ["Id"]=s.Objects[1].Id,["Model"]=new JsonObject { ["Furniture"]=new JsonObject { ["AttachmentParentId"]=s.Objects[0].Id } } } };
            var p=SceneMerge.Insert(new(),s,Vector3.Zero).Project;
            var first=p.Objects.Single(o=>o.Name=="Part 0"); var other=p.Objects.Single(o=>o.Name=="Part 1");
            Assert(first.ParentId==p.Objects.Single(o=>o.Name=="Nested").Id);
            Assert(other.IntonerSource["Object"]!["Model"]!["Furniture"]!["AttachmentParentId"]!.GetValue<Guid>()==first.Id);
        });
        test("Insertion embeds disk mods and separates colliding modpack IDs",()=>{
            string path=Path.Combine(directory,"ship-mod.bin"); File.WriteAllBytes(path,[2,4,6]);
            var source=Ship(); source.StagehandRoot["EmbeddedModpacks"]=new JsonObject { ["same"]=new JsonObject { ["DisplayName"]="Ship mod",["ModdedResources"]=new JsonObject { ["bg/part.mdl"]=JsonSerializer.SerializeToNode<ModResourceDefinition>(new DiskModResourceDefinition {SourceDiskPath=path},StageDefinition.StandardSerializerOptions) } } };
            source.Objects[0].StagehandSource["ModpackId"]="same";
            var dest=new SceneProject(); dest.StagehandRoot["EmbeddedModpacks"]=new JsonObject { ["same"]=new JsonObject { ["DisplayName"]="Existing mod",["ModdedResources"]=new JsonObject() } };
            var p=SceneMerge.Insert(dest,source,Vector3.Zero).Project; File.Delete(path);
            var item=p.Objects.Single(o=>o.Name=="Part 0"); string id=ModResources.PackId(item); Assert(id!="same");
            Assert(ModResources.Packs(p)["same"]!["DisplayName"]!.GetValue<string>()=="Existing mod");
            var resolved=ModResources.Resolve(ModResources.Packs(p)[id]!.AsObject(),"bg/part.mdl",Path.Combine(directory,"ship-resources")); Assert(File.ReadAllBytes(resolved.Path).SequenceEqual(new byte[]{2,4,6}));
        });
        test("Invalid insertions leave destination unchanged",()=>{
            var d=new SceneProject(); string before=JsonSerializer.Serialize(d,ProjectJson.Options);
            Reject(()=>SceneMerge.Insert(d,new(),Vector3.Zero));
            var s=Ship(); s.Objects[0].StagehandSource["ModpackId"]="missing"; Reject(()=>SceneMerge.Insert(d,s,Vector3.Zero));
            Assert(before==JsonSerializer.Serialize(d,ProjectJson.Options));
        });
    }
}
