using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Stagehand.Definitions;
using Stagehand.Definitions.Objects;

internal static class EditorUpdateTests
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool value) { if (!value) throw new Exception("Editor update assertion failed."); }
        void Near(Vector3 a, Vector3 b) => Assert(Vector3.Distance(a, b) < .001f);
        void RotationNear(Vector3 a, Vector3 b) => Assert(MathF.Abs(Quaternion.Dot(TransformMath.Rotation(a), TransformMath.Rotation(b))) > .99999f);
        void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection."); }
        SceneProject LightFixture() => SceneCodec.Import("""
        {"FormatVersion":2,"Info":{"Name":"Projected light"},"Objects":{
          "group":{"Type":"Group","Objects":{
            "light":{"Type":"Light","Shape":"Spot","ModpackId":"pack","ProjectedTextureGamePath":"bg/test/mask.tex","FutureLight":17}
          }}
        },"EmbeddedModpacks":{"pack":{"DisplayName":"Mask","ModdedResources":{
          "bg/test/mask.tex":{"$type":"Game","SourceGamePath":"bg/example.tex"}
        }}}}
        """);
        test("Version 2 projected lights retain groups, mod resources and unknown fields after edits", () =>
        {
            var scene = LightFixture(); var light = scene.Objects.Single(o => o.Kind == AssetKind.Light);
            var values = LightSettings.Read(light); Assert(values.ProjectedTextureGamePath == "bg/test/mask.tex");
            values.Intensity = 8; LightSettings.Write(light, values);
            scene = ProjectJson.Load(ProjectJson.Save(scene));
            var output = SceneCodec.Export(scene, SceneFormat.Stagehand);
            Assert(output.Document["FormatVersion"]!.GetValue<int>() == 2);
            var raw = output.Document["Objects"]!["group"]!["Objects"]!["light"]!;
            Assert(raw["ProjectedTextureGamePath"]!.GetValue<string>() == "bg/test/mask.tex");
            Assert(raw["ModpackId"]!.GetValue<string>() == "pack" && raw["FutureLight"]!.GetValue<int>() == 17);
            Assert(raw["Intensity"]!.GetValue<float>() == 8);
            Assert(JsonNode.DeepEquals(output.Document["EmbeddedModpacks"], scene.StagehandRoot["EmbeddedModpacks"]));
            Assert(StageDefinition.TryParseDefinitionString(output.Json, out _));
            Assert(SceneCodec.Import(LiveScene.Build(scene).Definition).Objects.Count == 2);
        });
        test("Intoner export reports projected texture loss while preserving canonical data", () =>
        {
            var scene = LightFixture(); var before = ProjectJson.Save(scene);
            var result = SceneCodec.Export(scene, SceneFormat.Intoner);
            Assert(result.Issues.Any(i => i.Message.Contains("Projected light textures")));
            Assert(SceneCodec.Import(result.Json).Objects.Single().Kind == AssetKind.Light);
            Assert(ProjectJson.Save(scene) == before);
        });
        test("Legacy texture metadata migrates and explicitly clearing it stays cleared", () =>
        {
            var o = new SceneObject { Kind = AssetKind.Light, StagehandSource = new() { ["ProjectedTextureGamePath"] = "bg/legacy.tex" } };
            Assert(LightSettings.Read(o).ProjectedTextureGamePath == "bg/legacy.tex");
            var light = LightSettings.Read(o); light.ProjectedTextureGamePath = ""; LightSettings.Write(o, light);
            Assert(LightSettings.Read(o).ProjectedTextureGamePath == "");
            var s = new SceneProject(); s.Objects.Add(o);
            Assert(SceneCodec.Export(s, SceneFormat.Stagehand).Document["Objects"]![o.Id.ToString()]!["ProjectedTextureGamePath"]!.GetValue<string>() == "");
            Reject(() => LightSettings.TexturePath("C:/mod/test.tex"));
            Reject(() => LightSettings.TexturePath("bg/../test.tex"));
            Reject(() => LightSettings.TexturePath("bg/test.mdl"));
        });
        test("Old Stagehand versions still import and future versions fail", () =>
        {
            for (int version = 0; version <= 2; version++)
                Assert(SceneCodec.Import($$$"""{"FormatVersion":{{{version}}},"Info":{"Name":"Test"},"Objects":{}}""").Name == "Test");
            Reject(() => SceneCodec.Import("""{"FormatVersion":3,"Info":{},"Objects":{}}"""));
        });
        test("Stagehand-compatible transform clipboard is precise and rejects malformed input", () =>
        {
            var vector = new Vector3(-1.125f, 90, .001f);
            Near(TransformEditing.Paste(TransformEditing.Copy(vector)), vector);
            Near(TransformEditing.Paste("""{"Type":"Vector3DataTransferFragment","Value":{"X":1,"Y":2,"Z":3}}"""), new(1,2,3));
            Reject(() => TransformEditing.Paste("{}"));
            Reject(() => TransformEditing.Paste("not json"));
            Reject(() => TransformEditing.Paste("""{"Type":"Vector3DataTransferFragment","Value":{"X":1,"Y":2}}"""));
            Reject(() => TransformEditing.Paste(TransformEditing.Copy(new(1,2,3)), true));
            Reject(() => TransformEditing.Paste(TransformEditing.Copy(new(-1)), true));
            Reject(() => TransformEditing.Paste("""{"Type":"Vector3DataTransferFragment","Value":{"X":1e100,"Y":2,"Z":3}}"""));
        });
        test("World manipulation reverses nested rotated and scaled parents plus stage placement", () =>
        {
            var s = new SceneProject { StageTranslation = new(4,5,6), StageRotationDegrees = new(20,30,40), StageUniformScale = 3 };
            var parent = new SceneObject { Kind = AssetKind.Group, Position = new(7,8,9), RotationDegrees = new(10,50,15), Scale = new(2) };
            var inner = new SceneObject { Kind = AssetKind.Group, ParentId = parent.Id, Position = new(2,1,3), RotationDegrees = new(5,15,25), Scale = new(.5f) };
            var child = new SceneObject { ParentId = inner.Id }; s.Objects.AddRange([parent, inner, child]);
            var p = new Vector3(-8, 10, 16); var r = new Vector3(12, 42, -31); var z = new Vector3(3,6,9);
            TransformEditing.SetWorld(s, child, p, TransformMath.Rotation(r), z);
            var actual = TransformMath.ToWorld(s, child); Near(actual.Position, p); Near(actual.Scale, z); RotationNear(actual.Rotation, r);
            var before = child.Position;
            Reject(() => TransformEditing.SetWorld(s, child, new(float.NaN), Quaternion.Identity, Vector3.One)); Near(child.Position, before);
        });
        test("Manipulating group world pose preserves child offsets and rejects nonuniform group scale", () =>
        {
            var s = new SceneProject(); var group = new SceneObject { Kind = AssetKind.Group };
            var child = new SceneObject { ParentId = group.Id, Position = new(0,0,1) }; s.Objects.AddRange([group,child]);
            TransformEditing.SetWorld(s, group, new(1,2,3), TransformMath.Rotation(new(0,90,0)), new(2));
            Near(TransformMath.ToWorld(s, child).Position, new(3,2,3)); Near(child.Position, new(0,0,1));
            Reject(() => TransformEditing.SetWorld(s, group, Vector3.Zero, Quaternion.Identity, new(1,2,3)));
            Near(group.Position, new(1,2,3));
        });
    }
}
