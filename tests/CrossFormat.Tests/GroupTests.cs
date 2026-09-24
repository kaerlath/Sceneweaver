using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Stagehand.Definitions;
using Stagehand.Definitions.Objects;

internal static class GroupTests
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool b) { if (!b) throw new Exception("Group assertion failed."); }
        void Near(Vector3 a, Vector3 b) => Assert(Vector3.Distance(a, b) < .001f);
        void Reject(Action a) { try { a(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection."); }
        SceneProject Fixture() => SceneCodec.Import("""
        {"FormatVersion":1,"Info":{"Name":"Nested"},"Objects":{
          "parent":{"Type":"Group","DisplayName":"Parent","Position":{"X":10,"Y":0,"Z":0},"RotationPitchYawRollDegrees":{"X":0,"Y":90,"Z":0},"Scale":{"X":2,"Y":2,"Z":2},"IsDisabled":true,"FutureGroup":42,"Objects":{
            "inner":{"Type":"Group","DisplayName":"Inner","Position":{"X":0,"Y":1,"Z":0},"Objects":{
              "effect":{"Type":"VfxObject","DisplayName":"Effect","VfxGamePath":"vfx/test.avfx","Position":{"X":0,"Y":0,"Z":3},"FutureChild":17}
            }}
          }},"effect":{"Type":"VfxObject","DisplayName":"Root effect","VfxGamePath":"vfx/root.avfx"}
        }}
        """);
        test("Stagehand nested groups round-trip keys, children, visibility and unknown data", () =>
        {
            var s = Fixture(); Assert(s.Objects.Count == 4);
            var exported = SceneCodec.Export(ProjectJson.Load(ProjectJson.Save(s)), SceneFormat.Stagehand);
            var upstream = exported.Document.Deserialize<StageDefinition>(StageDefinition.StandardSerializerOptions)!;
            Assert(upstream.Objects.Count == 2 && ((GroupDefinition)upstream.Objects["parent"]).Objects.Count == 1);
            Assert(exported.Document["Objects"]!["parent"]!["FutureGroup"]!.GetValue<int>() == 42);
            Assert(exported.Document["Objects"]!["parent"]!["Objects"]!["inner"]!["Objects"]!["effect"]!["FutureChild"]!.GetValue<int>() == 17);
            Assert(SceneCodec.Import(exported.Json).Objects.Count == 4);
        });
        test("Intoner flattening composes group and stage transforms and inherited visibility", () =>
        {
            var s = Fixture(); var o = s.Objects.Single(n => n.Name == "Effect");
            Near(SceneHierarchy.StageTransform(s, o).Position, new(16, 2, 0));
            s.StageTranslation = new(1, 0, 0); s.StageUniformScale = 3;
            var r = SceneCodec.Import(SceneCodec.Export(s, SceneFormat.Intoner).Json);
            var effect = r.Objects.Single(n => n.Name == "Effect");
            Near(effect.Position, new(49, 6, 0)); Near(effect.Scale, new(6)); Assert(!effect.Visible);
            Assert(r.Objects.Count == 2 && LiveScene.Build(s).VisibleObjects == 1);
        });
        test("Reparent, group duplication and moving a child to the player preserve hierarchy", () =>
        {
            var s = Fixture(); var child = s.Objects.Single(n => n.Name == "Effect");
            var before = SceneHierarchy.StageTransform(s, child);
            var parent = child.ParentId; SceneHierarchy.Reparent(s, child, null);
            Near(child.Position, before.Position); SceneHierarchy.Reparent(s, child, parent);
            Near(SceneHierarchy.StageTransform(s, child).Position, before.Position);
            var target = new Vector3(7, 8, 9); child.Position = SceneHierarchy.ToParentPosition(s, child, target);
            Near(SceneHierarchy.StageTransform(s, child).Position, target);
            var copy = SceneHierarchy.Duplicate(s, s.Objects[0]);
            Assert(SceneHierarchy.Subtree(s, copy).Count == 3); ProjectJson.Validate(s);
            SceneCodec.Export(s, SceneFormat.Stagehand);
        });
        test("Invalid parent chains, group scales and future Stagehand versions fail explicitly", () =>
        {
            var s = Fixture(); s.Objects[0].ParentId = s.Objects[1].Id; Reject(() => ProjectJson.Validate(s));
            s = Fixture(); s.Objects[1].ParentId = Guid.NewGuid(); Reject(() => ProjectJson.Validate(s));
            s = Fixture(); s.Objects[0].Scale = new(1, 2, 1); Reject(() => ProjectJson.Validate(s));
            Reject(() => SceneCodec.Import("""{"FormatVersion":2,"Info":{},"Objects":{}}"""));
        });
        test("VFX preview isolates one effect and keeps mod data without changing the saved scene", () =>
        {
            var s = Fixture(); var effect = s.Objects.Single(n => n.Name == "Effect");
            var id = ModResources.Attach(s, new JsonObject { ["ModdedResources"] = new JsonObject() }); effect.StagehandSource["ModpackId"] = id;
            var before = ProjectJson.Save(s); var preview = VfxPreview.Build(s, effect, new(1, 2, 3), 0, 4, .5f);
            Assert(before == ProjectJson.Save(s)); Near(preview.Translation, new(1, 2, 7)); Assert(preview.Scale == .5f);
            var imported = SceneCodec.Import(preview.Definition);
            Assert(imported.Objects.Count == 1 && imported.Objects[0].Visible && imported.Objects[0].ParentId == null);
            Assert(ModResources.PackId(imported.Objects[0]) == id && ModResources.Packs(imported).ContainsKey(id));
        });
    }
}
