using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrossFormat.Core;
using Intoner.Objects.Api;
using Stagehand.Definitions;
using Stagehand.Definitions.Objects;

var passed = 0;
var failed = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e); failed++; }
}
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection."); }
SceneProject Fixture()
{
    // Authored against the published Stagehand contract, not our exporter.
    return SceneCodec.Import("""
    {
      "Info": {"Name":"Fixture", "AuthorName":"Test author", "IntendedTerritoryType": 339},
      "UnknownRoot": {"future":[1,2,3]},
      "Objects": {
        "model-key": {"Type":"BgObject", "DisplayName":"Stone", "ModelGamePath":"bg/test/stone.mdl", "Opacity":0.75,
          "DyeColor":{"X":0.1,"Y":0.2,"Z":0.3,"W":1}, "Position":{"X":1,"Y":2,"Z":3},
          "RotationPitchYawRollDegrees":{"X":20,"Y":30,"Z":40}, "Scale":{"X":2,"Y":3,"Z":4}, "IsDisabled":true,
          "FutureValue":{"keep":"yes"}},
        "vfx-key": {"Type":"VfxObject", "DisplayName":"Mist", "VfxGamePath":"vfx/test/mist.avfx", "Color":{"X":0.7,"Y":0.8,"Z":0.9,"W":0.5}},
        "light-key": {"Type":"Light", "DisplayName":"Lamp", "Shape":"Flat", "Color":{"X":2,"Y":3,"Z":4}, "Intensity":7,
          "EnableSpecularHighlights":false,"EnableDynamicShadows":true,"EnableCharacterShadows":false,"EnableObjectShadows":true,
          "FalloffFunction":"Cubic","Range":31,"FalloffFactor":1.7,"SpotLightAngleDegrees":55,"AngularFalloffDegrees":8,
          "FlatLightSkewAngleDegrees":{"X":12,"Y":13},"CharacterShadowRange":44,"ShadowPlaneNear":0.3,"ShadowPlaneFar":66},
        "sound-key": {"Type":"Sound", "DisplayName":"Bell", "SoundGamePath":"sound/test/bell.scd", "Volume":0.3,"Speed":1.2,"SoundIndex":3,"IsPositional":false},
        "weapon-key": {"Type":"Weapon", "DisplayName":"Sword", "ModelSetId":42,"SecondaryId":7,"Variant":3,"PrimaryDye":5,"SecondaryDye":6,"AnimationVariant":2}
      }, "EmbeddedModpacks": {}
    }
    """);
}
string Stage(SceneProject s) => SceneCodec.Export(s, SceneFormat.Stagehand).Json;
string Intoner(SceneProject s) => SceneCodec.Export(s, SceneFormat.Intoner).Json;

Test("Independent Stagehand fixture imports all five types", () => { var s = Fixture(); Assert(s.Objects.Count == 5); Assert(s.Objects[0].Position == new Vector3(1,2,3)); Assert(s.Objects[0].RotationDegrees == new Vector3(20,30,40)); Assert(!s.Objects[0].Visible); });
Test("Stagehand round-trip keeps keys, future fields and metadata", () => { var root = JsonNode.Parse(Stage(Fixture()))!; Assert(root["Objects"]!["model-key"]!["FutureValue"]!["keep"]!.GetValue<string>() == "yes"); Assert(root["UnknownRoot"]!["future"]!.AsArray().Count == 3); Assert(root["Info"]!["AuthorName"]!.GetValue<string>() == "Test author"); });
Test("Stagehand output reads through upstream serializer", () => { Assert(StageDefinition.TryParseDefinitionString(Stage(Fixture()), out var d)); Assert(d!.Objects["model-key"] is BgObjectDefinition); Assert(((SoundObjectDefinition)d.Objects["sound-key"]).SoundIndex == 3); Assert(!((SoundObjectDefinition)d.Objects["sound-key"]).IsPositional); Assert(((WeaponDefinition)d.Objects["weapon-key"]).PrimaryDye == 5); });
Test("Stagehand to Intoner explicitly reports unsupported sound and weapon", () => { var r = SceneCodec.Export(Fixture(), SceneFormat.Intoner); Assert(r.Issues.Count(i => i.Omitted) == 2); Assert(r.Document["Objects"]!.AsArray().Count == 3); });
Test("Intoner output uses strict upstream WorldObject DTOs", () =>
{
    var root = JsonNode.Parse(Intoner(Fixture()))!;
    var strict = new JsonSerializerOptions(ProjectJson.Options) { RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    foreach (var entry in root["Objects"]!.AsArray()) Assert(entry!["Object"]!.Deserialize<WorldObject>(strict) != null);
    Assert(root["DocumentKind"]!.GetValue<string>() == "object-layout"); Assert(root["FormatVersion"]!.GetValue<int>() == 2);
});
Test("Shared transforms, opacity and colors survive conversion both ways", () => { var s = SceneCodec.Import(Intoner(Fixture())); var b = s.Objects[0]; Assert(b.Position == new Vector3(1,2,3)); Assert(b.Scale == new Vector3(2,3,4)); Assert(b.RotationDegrees == new Vector3(20,30,40)); Assert(b.Opacity == .75f); Assert(b.Color == new Vector4(.1f,.2f,.3f,1)); var d = JsonSerializer.Deserialize<StageDefinition>(Stage(s), StageDefinition.StandardSerializerOptions)!; Assert(d.Objects.Values.OfType<BgObjectDefinition>().Single().IsDisabled); });
Test("All light fields survive cross conversion", () =>
{
    var a = JsonSerializer.Deserialize<StageDefinition>(Stage(Fixture()), StageDefinition.StandardSerializerOptions)!.Objects.Values.OfType<LightDefinition>().Single();
    var b = JsonSerializer.Deserialize<StageDefinition>(Stage(SceneCodec.Import(Intoner(Fixture()))), StageDefinition.StandardSerializerOptions)!.Objects.Values.OfType<LightDefinition>().Single();
    var aj = JsonSerializer.SerializeToNode(a, StageDefinition.StandardSerializerOptions)!; var bj = JsonSerializer.SerializeToNode(b, StageDefinition.StandardSerializerOptions)!;
    Assert(JsonNode.DeepEquals(aj,bj));
});
Test("Canonical save retains complete source trees", () => { var s = Fixture(); var restored = ProjectJson.Load(ProjectJson.Save(s)); Assert(JsonNode.DeepEquals(s.StagehandRoot, restored.StagehandRoot)); Assert(JsonNode.DeepEquals(s.Objects[0].StagehandSource, restored.Objects[0].StagehandSource)); Assert(restored.Objects.Count == 5); });
Test("Unknown Intoner fields kept canonically but excluded from strict export", () => { var r = JsonNode.Parse(Intoner(Fixture()))!; r["FutureRoot"] = true; r["Objects"]![0]!["Object"]!["FutureField"] = "preserve"; var s = SceneCodec.Import(r.ToJsonString()); Assert(s.IntonerRoot["FutureRoot"]!.GetValue<bool>()); Assert(s.Objects[0].IntonerSource["Object"]!["FutureField"]!.GetValue<string>() == "preserve"); var output = JsonNode.Parse(Intoner(s))!; Assert(output["Objects"]![0]!["Object"]!["FutureField"] == null); });
Test("Intoner folder colors, locked state and playback data preserved", () =>
{
    var root = JsonNode.Parse(Intoner(Fixture()))!;
    root["Folders"] = new JsonArray(new JsonObject { ["Path"]="Room/Corner", ["Color"]="#FFAA00" });
    root["Objects"]![1]!["FolderPath"] = "Room/Corner"; root["Objects"]![1]!["Locked"] = true;
    root["Objects"]![1]!["Object"]!["Model"]!["Vfx"]!["Speed"] = 2.3f;
    var output = JsonNode.Parse(Intoner(SceneCodec.Import(root.ToJsonString())))!;
    Assert(output["Folders"]![0]!["Color"]!.GetValue<string>() == "#FFAA00"); Assert(output["Objects"]![1]!["Locked"]!.GetValue<bool>()); Assert(output["Objects"]![1]!["Object"]!["Model"]!["Vfx"]!["Speed"]!.GetValue<float>() == 2.3f);
});
Test("Version 1 folders upgrade to version 2", () => { var r = JsonNode.Parse(Intoner(Fixture()))!; r["FormatVersion"] = 1; r["Folders"] = new JsonArray("Folder"); r["FolderColors"] = new JsonObject { ["Folder"]="#123456" }; var s = SceneCodec.Import(r.ToJsonString()); var output = JsonNode.Parse(Intoner(s))!; Assert(output["Folders"]![0]!["Path"]!.GetValue<string>() == "Folder"); Assert(output["Folders"]![0]!["Color"]!.GetValue<string>() == "#123456"); });
Test("Intoner furniture and material metadata survive canonical and native saves", () =>
{
    var r = JsonNode.Parse(Intoner(Fixture()))!;
    var w = r["Objects"]![0]!["Object"]!;
    w["Kind"] = "Furniture";
    w["Model"] = JsonNode.Parse("""
    {"Furniture":{"SharedGroupPath":"bgcommon/hou/indoor/test.sgb","Color":{"StainId":12,"UseCustomColor":false,"CustomColor":{"X":0.2,"Y":0.3,"Z":0.4,"W":1}},"Transparency":0.8,"OutlineColor":"Green","HousingRowId":42,"ItemRowId":99,"AttachmentParentId":null,"MaterialItem":{"Name":"Wallpaper","ItemId":100}}}
    """);
    var s = ProjectJson.Load(ProjectJson.Save(SceneCodec.Import(r.ToJsonString())));
    Assert(s.Objects[0].Kind == AssetKind.Furniture);
    var output = JsonNode.Parse(Intoner(s))!;
    Assert(output["Objects"]![0]!["Object"]!["Model"]!["Furniture"]!["Color"]!["StainId"]!.GetValue<int>() == 12);
    Assert(output["Objects"]![0]!["Object"]!["Model"]!["Furniture"]!["MaterialItem"]!["ItemId"]!.GetValue<int>() == 100);
    Assert(SceneCodec.Export(s,SceneFormat.Stagehand).Issues.Count(i=>i.Omitted)==1);
});
Test("Future object types retained canonically and explicitly omitted", () => { var root = JsonNode.Parse(Stage(Fixture()))!; root["Objects"]!["future"] = new JsonObject { ["Type"]="FutureThing",["Value"]=123 }; var s = SceneCodec.Import(root.ToJsonString()); Assert(s.Objects.Last().Kind == AssetKind.Unknown); Assert(SceneCodec.Export(s, SceneFormat.Stagehand).Issues.Any(i=>i.Omitted)); Assert(ProjectJson.Load(ProjectJson.Save(s)).Objects.Last().StagehandSource["Value"]!.GetValue<int>() == 123); });
Test("Embedded modpacks retained and bound objects omitted from Intoner", () => { var r = JsonNode.Parse(Stage(Fixture()))!; r["Objects"]!["model-key"]!["ModpackId"]="pack"; r["EmbeddedModpacks"]!["pack"] = new JsonObject { ["DisplayName"]="Test", ["ModdedResources"]=new JsonObject() }; var s = SceneCodec.Import(r.ToJsonString()); Assert(SceneCodec.Export(s, SceneFormat.Intoner).Issues.Count(i=>i.Omitted)==3); Assert(JsonNode.Parse(Stage(s))!["Objects"]!["model-key"]!["ModpackId"]!.GetValue<string>()=="pack"); });
Test("Absolute disk paths are not emitted as Stagehand game paths", () => { var s=Fixture(); s.Objects[0].AssetPath=Path.GetFullPath("stone.mdl"); Assert(SceneCodec.Export(s,SceneFormat.Stagehand).Issues.Any(i=>i.Omitted && i.ObjectName=="Stone")); });
Test("Stage placement composes world position and scale", () => { var s=Fixture(); s.StageTranslation=new(10,20,30); s.StageRotationDegrees=new(0,90,0); s.StageUniformScale=2; var (p,r,z)=TransformMath.ToWorld(s,s.Objects[0]); Assert(Vector3.Distance(p,new(16,24,28))<1e-4f); Assert(z==new Vector3(4,6,8)); Assert(MathF.Abs(Quaternion.Dot(TransformMath.Rotation(r),TransformMath.Rotation(s.StageRotationDegrees)*TransformMath.Rotation(s.Objects[0].RotationDegrees)))>.99999f); });
Test("Quaternion conversion covers gimbal lock and randomized rotations", () => { var random=new Random(731); for(int i=0;i<1000;i++){ var v=new Vector3((float)random.NextDouble()*360-180,(float)random.NextDouble()*360-180,(float)random.NextDouble()*360-180); if(i<2) v.X=i==0?90:-90; var q=TransformMath.Rotation(v); var q2=TransformMath.Rotation(TransformMath.Degrees(q)); Assert(MathF.Abs(Quaternion.Dot(q,q2))>.99999f, v.ToString()); } });
Test("Reject malformed, duplicate-key and future-version inputs", () => { Throws(()=>SceneCodec.Import("{}")); Throws(()=>SceneCodec.Import("{bad")); Throws(()=>SceneCodec.Import("{\"Info\":{},\"Info\":{},\"Objects\":{}}")); var r=JsonNode.Parse(Intoner(Fixture()))!; r["FormatVersion"]=99; Throws(()=>SceneCodec.Import(r.ToJsonString())); });
Test("Reject duplicate IDs and non-finite transforms", () => { var s=Fixture(); s.Objects[1].Id=s.Objects[0].Id; Throws(()=>Intoner(s)); s=Fixture(); s.Objects[0].Position=new(float.NaN,0,0); Throws(()=>ProjectJson.Save(s)); });
Test("Reject incomplete assets before export", () => { var s=Fixture(); s.Objects[0].AssetPath=""; Throws(()=>Intoner(s)); });
var testDirectory=Path.Combine(Path.GetTempPath(),"CrossFormatTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(testDirectory);
Test("Dual-save produces three readable files", () => { var p=ProjectFiles.PlanDualSave(Fixture(),Path.Combine(testDirectory,"canonical.json"),Path.Combine(testDirectory,"intoner.json"),Path.Combine(testDirectory,"stagehand.json")); ProjectFiles.Commit(p); Assert(ProjectJson.Load(ProjectFiles.Read(p.Targets[0].Path)).Objects.Count==5); Assert(SceneCodec.Import(ProjectFiles.Read(p.Targets[1].Path)).Objects.Count==3); Assert(SceneCodec.Import(ProjectFiles.Read(p.Targets[2].Path)).Objects.Count==5); });
Test("Dual-save rejects path collisions before writing", () => Throws(()=>ProjectFiles.PlanDualSave(Fixture(),Path.Combine(testDirectory,"a"),Path.Combine(testDirectory,"a"),Path.Combine(testDirectory,"c"))));
Test("External edits invalidate reviewed save", () => { var a=Path.Combine(testDirectory,"external.json"); File.WriteAllText(a,"old"); var p=new SavePlan([new(a,"new",ProjectFiles.Hash(a))],[]); File.WriteAllText(a,"external"); Throws(()=>ProjectFiles.Commit(p)); Assert(File.ReadAllText(a)=="external"); });
Test("Failure on second replacement rolls first file back", () => { var a=Path.Combine(testDirectory,"rollback-a"); var b=Path.Combine(testDirectory,"rollback-b"); File.WriteAllText(a,"old-a"); File.WriteAllText(b,"old-b"); var p=new SavePlan([new(a,"new-a"),new(b,"new-b")],[]); Throws(()=>ProjectFiles.Commit(p,n=>{if(n==1)throw new IOException("Injected failure");})); Assert(File.ReadAllText(a)=="old-a"); Assert(File.ReadAllText(b)=="old-b"); });
Test("Failure after creating new file removes only new file", () => { var a=Path.Combine(testDirectory,"created-a"); var b=Path.Combine(testDirectory,"created-b"); var p=new SavePlan([new(a,"a"),new(b,"b")],[]); Throws(()=>ProjectFiles.Commit(p,n=>{if(n==1)throw new IOException("Injected failure");})); Assert(!File.Exists(a)&&!File.Exists(b)); });
Test("Successful replacement retains recovery backup", () => { var a=Path.Combine(testDirectory,"backup"); File.WriteAllText(a,"old"); ProjectFiles.Commit(new SavePlan([new(a,"new")],[])); Assert(File.ReadAllText(a)=="new"); Assert(Directory.GetFiles(testDirectory,"backup.*.bak").Any(f=>File.ReadAllText(f)=="old")); });
Test("Bundled game catalog is available without any scene objects", () =>
{
    var catalog = GameAssetCatalog.LoadBundled();
    Assert(catalog.Assets.Count > 100000);
    Assert(catalog.Assets.Any(a => a.Kind == AssetKind.Vfx));
    Assert(catalog.Assets.Any(a => a.Kind == AssetKind.Sound));
    Assert(catalog.Children("").Contains("bg"));
    Assert(catalog.Search("g3t1_p0_tre4c", "bg", AssetKind.BgObject).Any());
});
Test("Catalog search combines terms, type and folder boundaries", () =>
{
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
    {"MdlPaths":["bg/room/tree.mdl","bg/rooms/tree.mdl","bg/room/chair.mdl","BG/ROOM/TREE.MDL","../bad.mdl"],"AvfxPaths":["bg/room/tree.avfx"],"ScdPaths":[]}
    """));
    var catalog = GameAssetCatalog.Read(stream);
    Assert(catalog.Assets.Count == 4);
    Assert(catalog.Search("TREE room", "bg/room", AssetKind.BgObject).Single().Path == "bg/room/tree.mdl");
    Assert(catalog.Children("bg").SequenceEqual(new[] { "bg/room", "bg/rooms" }));
});
Test("Browsing catalog and creating an asset does not modify a scene", () =>
{
    var scene = new SceneProject(); var entry = new GameAsset("bg/example.mdl", AssetKind.BgObject);
    var first = entry.CreateObject(); var second = entry.CreateObject();
    Assert(scene.Objects.Count == 0); Assert(first.Id != second.Id); Assert(first.AssetPath == entry.Path);
});
Test("File picker defaults honor Stagehand's configured library and autosave", () =>
{
    var root = Path.Combine(testDirectory, "configs"); Directory.CreateDirectory(root);
    var stage = Path.Combine(testDirectory, "custom stages"); var autosave = Path.Combine(testDirectory, "custom autosave");
    File.WriteAllText(Path.Combine(root, "Stagehand.json"), JsonSerializer.Serialize(new { DefinitionLibraryPath = stage, AutosavePath = autosave }));
    var locations = SaveLocations.Discover(Path.Combine(root, "Sceneweaver"), Path.Combine(testDirectory, "Documents"));
    Assert(locations.Stagehand == stage); Assert(locations.StagehandAutosave == autosave);
    Assert(locations.Intoner == Path.Combine(root, "Intoner", "objects", "layouts"));
});
Test("Missing or malformed Stagehand configuration uses documented defaults", () =>
{
    var root = Path.Combine(testDirectory, "default-configs"); Directory.CreateDirectory(root);
    var docs = Path.Combine(testDirectory, "Documents");
    var locations = SaveLocations.Discover(Path.Combine(root, "Sceneweaver"), docs);
    Assert(locations.Stagehand == Path.Combine(docs, "Stages"));
    File.WriteAllText(Path.Combine(root, "Stagehand.json"), "{bad");
    Assert(SaveLocations.Discover(Path.Combine(root, "Sceneweaver"), docs).Stagehand == locations.Stagehand);
});
Test("Dialog initial folder falls back to an existing library or parent", () =>
{
    var existing = Path.Combine(testDirectory, "existing"); Directory.CreateDirectory(existing);
    Assert(SaveLocations.ExistingDirectory(Path.Combine(testDirectory, "missing"), existing) == existing);
    Assert(SaveLocations.ExistingDirectory(Path.Combine(existing, "missing", "child")) == existing);
});
ModTests.Run(Test, testDirectory);
LiveTests.Run(Test);
Console.WriteLine($"{passed} passed; {failed} failed. Test artifacts: {testDirectory}");
Environment.ExitCode=failed==0?0:1;
