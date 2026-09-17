using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrossFormat.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly PreviewService previews;
    private SceneProject scene = new();
    private readonly Stack<string> undo = new();
    private readonly Stack<string> redo = new();
    private readonly List<SceneObject> assets = [];
    private readonly Dictionary<string, string> knownHashes = new(StringComparer.OrdinalIgnoreCase);
    private Guid selected;
    private bool open, dirty, acceptOmissions;
    private string status = "Open an Intoner layout, a Stagehand definition, or a canonical project.";
    private string inputPath = "", projectPath, intonerPath, stagehandPath, assetDirectory = "", filter = "";
    private string libraryPath;
    private Task<SceneObject[]>? scan;
    private SavePlan? pending;
    private Action? pendingDestructive;
    private int assetPage;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IDataManager data, ITextureProvider textures)
    {
        pi = pluginInterface; commands = commandManager; previews = new(data, textures);
        var folder = pi.GetPluginConfigDirectory();
        projectPath = Path.Combine(folder, "scene.cross.json"); intonerPath = Path.Combine(folder, "scene.intoner.json"); stagehandPath = Path.Combine(folder, "scene.stagehand.json");
        libraryPath = Path.Combine(folder, "asset-library.json");
        if (File.Exists(libraryPath))
        {
            try { assets.AddRange(JsonSerializer.Deserialize<List<SceneObject>>(ProjectFiles.Read(libraryPath), ProjectJson.Options) ?? []); }
            catch (Exception e) { status = "Could not load asset library: " + e.Message; }
        }
        foreach (var command in new[] { "/sceneweaver", "/swedit", "/crossedit" })
            commands.AddHandler(command, new CommandInfo((_, _) => open = !open) { HelpMessage = "Open Sceneweaver, the Intoner / Stagehand scene editor." });
        pi.UiBuilder.Draw += Draw;
        pi.UiBuilder.OpenMainUi += Open;
        pi.UiBuilder.OpenConfigUi += Open;
    }
    private void Open() => open = true;
    private void Run(Action action) { try { action(); } catch (Exception e) { status = e.Message; } }
    private void Edit(Action action)
    {
        undo.Push(JsonSerializer.Serialize(scene, ProjectJson.Options));
        if (undo.Count > 100) { var recent = undo.Take(100).Reverse().ToArray(); undo.Clear(); foreach (var snapshot in recent) undo.Push(snapshot); }
        redo.Clear(); action(); scene.Revision++; dirty = true; pending = null;
    }
    private void ReplaceScene(SceneProject value)
    {
        scene = value; selected = Guid.Empty; undo.Clear(); redo.Clear(); pending = null; dirty = false;
    }
    private void GuardReplace(Action action) { if (dirty) pendingDestructive = action; else action(); }

    private void Draw()
    {
        if (!open) return;
        ImGui.SetNextWindowSize(new(1120, 750), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Sceneweaver###Sceneweaver", ref open))
        {
            if (ImGui.Button("New")) GuardReplace(() => ReplaceScene(new()));
            ImGui.SameLine();
            ImGui.BeginDisabled(undo.Count == 0);
            if (ImGui.Button("Undo")) { redo.Push(JsonSerializer.Serialize(scene, ProjectJson.Options)); scene = JsonSerializer.Deserialize<SceneProject>(undo.Pop(), ProjectJson.Options)!; dirty = true; pending = null; }
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.BeginDisabled(redo.Count == 0);
            if (ImGui.Button("Redo")) { undo.Push(JsonSerializer.Serialize(scene, ProjectJson.Options)); scene = JsonSerializer.Deserialize<SceneProject>(redo.Pop(), ProjectJson.Options)!; dirty = true; pending = null; }
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.TextUnformatted(dirty ? "Unsaved changes" : "Saved / unchanged");
            ImGui.Separator();
            if (ImGui.BeginTabBar("workspace"))
            {
                if (ImGui.BeginTabItem("Scene")) { DrawScene(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Asset browser")) { DrawAssets(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Open & save")) { DrawFiles(); ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
            ImGui.Separator(); ImGui.TextWrapped(status);
            if (pendingDestructive != null)
            {
                ImGui.TextWrapped("This will replace your unsaved scene. Save it first or discard the changes below.");
                if (ImGui.Button("Discard changes and continue")) { var action = pendingDestructive; pendingDestructive = null; Run(action); }
                ImGui.SameLine(); if (ImGui.Button("Keep editing")) pendingDestructive = null;
            }
        }
        ImGui.End();
    }

    private void DrawScene()
    {
        var name = scene.Name;
        if (ImGui.InputText("Project name", ref name, 512)) Edit(() => scene.Name = name);
        if (ImGui.Button("Add model")) Edit(() => { var o = new SceneObject(); scene.Objects.Add(o); selected = o.Id; });
        ImGui.SameLine(); if (ImGui.Button("Add VFX")) Edit(() => { var o = new SceneObject { Kind = AssetKind.Vfx, Name = "New VFX" }; scene.Objects.Add(o); selected = o.Id; });
        ImGui.SameLine(); if (ImGui.Button("Add light")) Edit(() => { var o = new SceneObject { Kind = AssetKind.Light, Name = "New light", Light = JsonSerializer.SerializeToNode(new Stagehand.Definitions.Objects.LightDefinition(), ProjectJson.Options)!.AsObject() }; scene.Objects.Add(o); selected = o.Id; });
        ImGui.BeginChild("objects", new(300, 440), true);
        foreach (var o in scene.Objects)
        {
            if (ImGui.Selectable($"{o.Name} [{o.Kind}]##{o.Id}", selected == o.Id)) selected = o.Id;
        }
        ImGui.EndChild(); ImGui.SameLine(); ImGui.BeginChild("inspector", new(0, 440), true);
        var item = scene.Objects.FirstOrDefault(o => o.Id == selected);
        if (item != null) DrawInspector(item);
        else ImGui.TextWrapped("Select an object to edit its transform and appearance.");
        ImGui.EndChild();
        if (ImGui.CollapsingHeader("Stage placement for Intoner export"))
        {
            ImGui.TextWrapped("Stagehand uses local positions. Intoner output bakes this placement into world positions. Identity is the default; these values are saved in the canonical project.");
            var p = scene.StageTranslation; var r = scene.StageRotationDegrees; var z = scene.StageUniformScale;
            if (ImGui.DragFloat3("Stage translation", ref p, .05f)) Edit(() => scene.StageTranslation = p);
            if (ImGui.DragFloat3("Stage rotation (degrees)", ref r, .2f)) Edit(() => scene.StageRotationDegrees = r);
            if (ImGui.DragFloat("Stage uniform scale", ref z, .01f, .001f, 1000)) Edit(() => scene.StageUniformScale = z);
        }
    }

    private void DrawInspector(SceneObject o)
    {
        previews.Draw(o, new(140, 140));
        var locked = o.Locked; if (ImGui.Checkbox("Locked", ref locked)) Edit(() => o.Locked = locked);
        ImGui.BeginDisabled(o.Locked || o.Kind == AssetKind.Unknown);
        string name = o.Name, path = o.AssetPath, folder = o.Folder, image = o.PreviewImagePath;
        bool visible = o.Visible; var p = o.Position; var r = o.RotationDegrees; var scale = o.Scale; var color = o.Color; var opacity = o.Opacity;
        if (ImGui.InputText("Name", ref name, 512)) Edit(() => o.Name = name);
        if (ImGui.InputText("Asset path", ref path, 4096)) Edit(() => o.AssetPath = path);
        if (ImGui.InputText("Folder", ref folder, 512)) Edit(() => o.Folder = folder);
        if (ImGui.Checkbox("Visible", ref visible)) Edit(() => o.Visible = visible);
        if (ImGui.DragFloat3("Position", ref p, .01f)) Edit(() => o.Position = p);
        if (ImGui.DragFloat3("Pitch / yaw / roll", ref r, .2f)) Edit(() => o.RotationDegrees = r);
        if (ImGui.DragFloat3("Scale", ref scale, .01f)) Edit(() => o.Scale = scale);
        if (o.Kind is AssetKind.BgObject or AssetKind.Vfx)
            if (ImGui.ColorEdit4("Color", ref color)) Edit(() => o.Color = color);
        if (o.Kind is AssetKind.BgObject or AssetKind.Furniture)
            if (ImGui.SliderFloat("Opacity", ref opacity, 0, 1)) Edit(() => o.Opacity = opacity);
        if (ImGui.InputText("Preview PNG/JPG", ref image, 4096)) Edit(() => o.PreviewImagePath = image);
        if (o.Kind == AssetKind.Light && ImGui.CollapsingHeader("Light parameters"))
        {
            DrawLight(o);
        }
        if (ImGui.Button("Duplicate")) Edit(() => { var copy = JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(o, ProjectJson.Options), ProjectJson.Options)!; copy.Id = Guid.NewGuid(); copy.StagehandId = ""; copy.Name += " copy"; scene.Objects.Add(copy); selected = copy.Id; });
        ImGui.SameLine(); if (ImGui.Button("Delete")) Edit(() => scene.Objects.Remove(o));
        ImGui.EndDisabled();
        if (ImGui.Button("Add to asset library")) Run(() =>
        {
            assets.Add(JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(o, ProjectJson.Options), ProjectJson.Options)!);
            SaveLibrary(); status = "Asset saved to the browser library.";
        });
    }

    private void DrawLight(SceneObject o)
    {
        var light = o.Light.Deserialize<Stagehand.Definitions.Objects.LightDefinition>(ProjectJson.Options) ?? new();
        bool changed = false;
        var shape = (int)light.Shape; if (ImGui.Combo("Shape", ref shape, "Ambient\0Point\0Spot\0Flat\0")) { light.Shape = (Stagehand.Definitions.Objects.LightShape)shape; changed = true; }
        var falloff = (int)light.FalloffFunction; if (ImGui.Combo("Falloff", ref falloff, "Linear\0Quadratic\0Cubic\0")) { light.FalloffFunction = (Stagehand.Definitions.Objects.LightFalloffFunction)falloff; changed = true; }
        var color = light.Color; if (ImGui.ColorEdit3("Light color", ref color, ImGuiColorEditFlags.Hdr | ImGuiColorEditFlags.Float)) { light.Color = color; changed = true; }
        void Number(string label, float value, Action<float> set, float min = 0, float max = 10000) { if (ImGui.DragFloat(label, ref value, .05f, min, max)) { set(value); changed = true; } }
        void Flag(string label, bool value, Action<bool> set) { if (ImGui.Checkbox(label, ref value)) { set(value); changed = true; } }
        Number("Intensity", light.Intensity, v => light.Intensity = v);
        Number("Range", light.Range, v => light.Range = v);
        Number("Falloff factor", light.FalloffFactor, v => light.FalloffFactor = v);
        Number("Spot angle", light.SpotLightAngleDegrees, v => light.SpotLightAngleDegrees = v, 0, 180);
        Number("Angular falloff", light.AngularFalloffDegrees, v => light.AngularFalloffDegrees = v, 0, 180);
        var skew = light.FlatLightSkewAngleDegrees; if (ImGui.DragFloat2("Flat light skew", ref skew, .1f, -89, 89)) { light.FlatLightSkewAngleDegrees = skew; changed = true; }
        Number("Shadow near plane", light.ShadowPlaneNear, v => light.ShadowPlaneNear = v, .001f);
        Number("Shadow far plane", light.ShadowPlaneFar, v => light.ShadowPlaneFar = v, .001f);
        Number("Character shadow range", light.CharacterShadowRange, v => light.CharacterShadowRange = v);
        Flag("Specular highlights", light.EnableSpecularHighlights, v => light.EnableSpecularHighlights = v);
        Flag("Dynamic shadows", light.EnableDynamicShadows, v => light.EnableDynamicShadows = v);
        Flag("Character shadows", light.EnableCharacterShadows, v => light.EnableCharacterShadows = v);
        Flag("Object shadows", light.EnableObjectShadows, v => light.EnableObjectShadows = v);
        if (changed) Edit(() => o.Light = JsonSerializer.SerializeToNode(light, ProjectJson.Options)!.AsObject());
    }

    private void DrawAssets()
    {
        ImGui.TextWrapped("Browse imported scene assets or scan a local folder for .mdl, .avfx and .sgb files. Models have untextured geometry thumbnails; other assets can use a matching preview image.");
        ImGui.InputText("Asset folder", ref assetDirectory, 4096);
        ImGui.BeginDisabled(scan != null);
        if (ImGui.Button("Scan folder"))
        {
            var dir = assetDirectory;
            scan = Task.Run(() => Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Where(f => new[] { ".mdl", ".avfx", ".sgb" }.Contains(Path.GetExtension(f).ToLowerInvariant())).Take(10000).Select(f => new SceneObject
            { Name = Path.GetFileNameWithoutExtension(f), AssetPath = f, Kind = Path.GetExtension(f).ToLowerInvariant() switch { ".avfx" => AssetKind.Vfx, ".sgb" => AssetKind.Furniture, _ => AssetKind.BgObject }, PreviewImagePath = File.Exists(Path.ChangeExtension(f, ".png")) ? Path.ChangeExtension(f, ".png") : "" }).ToArray());
        }
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Collect assets from scene")) Run(() => { assets.AddRange(scene.Objects.Select(o => JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(o, ProjectJson.Options), ProjectJson.Options)!)); SaveLibrary(); });
        if (scan?.IsCompleted == true) { var done = scan; scan = null; Run(() => { assets.AddRange(done.GetAwaiter().GetResult()); SaveLibrary(); status = $"Library contains {assets.Count} assets."; }); }
        if (scan != null) ImGui.TextUnformatted("Scanning...");
        if (ImGui.InputText("Search", ref filter, 256)) assetPage = 0;
        var matches = assets.Where(o => (o.Name + " " + o.AssetPath).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        assetPage = Math.Clamp(assetPage, 0, Math.Max(0, (matches.Length - 1) / 12));
        if (ImGui.Button("Previous") && assetPage > 0) assetPage--;
        ImGui.SameLine(); if (ImGui.Button("Next") && (assetPage + 1) * 12 < matches.Length) assetPage++;
        ImGui.SameLine(); ImGui.TextUnformatted($"{matches.Length} assets | page {assetPage + 1}");
        ImGui.BeginChild("assetcards", new(0, 410), true);
        if (ImGui.BeginTable("cards", 4))
        {
            int cardIndex = 0;
            foreach (var asset in matches.Skip(assetPage * 12).Take(12))
            {
                ImGui.TableNextColumn(); ImGui.PushID(cardIndex++); previews.Draw(asset, new(115, 100));
                ImGui.TextWrapped(asset.Name);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(asset.AssetPath);
                ImGui.BeginDisabled(asset.Kind == AssetKind.Furniture && asset.IntonerSource.Count == 0);
                if (ImGui.Button("Place in project")) Edit(() => { var copy = JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(asset, ProjectJson.Options), ProjectJson.Options)!; copy.Id = Guid.NewGuid(); copy.StagehandId = ""; copy.Position = Vector3.Zero; scene.Objects.Add(copy); selected = copy.Id; status = "Asset added to the project. No world object was spawned."; });
                ImGui.EndDisabled(); ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }

    private void SaveLibrary() => ProjectFiles.Commit(new SavePlan([new(libraryPath, JsonSerializer.Serialize(assets, ProjectJson.Options))], []));

    private void DrawFiles()
    {
        ImGui.InputText("Open file", ref inputPath, 4096);
        if (ImGui.Button("Open / import"))
        {
            var path = inputPath;
            Run(() => { var imported = SceneCodec.Import(ProjectFiles.Read(path)); GuardReplace(() => { ReplaceScene(imported); knownHashes[Path.GetFullPath(path)] = ProjectFiles.Hash(path); status = $"Imported {scene.Objects.Count} objects."; }); });
        }
        ImGui.Separator();
        if (ImGui.InputText("Canonical project", ref projectPath, 4096)) pending = null;
        if (ImGui.InputText("Intoner layout", ref intonerPath, 4096)) pending = null;
        if (ImGui.InputText("Stagehand definition", ref stagehandPath, 4096)) pending = null;
        if (ImGui.Button("Review dual-save")) Run(() =>
        {
            pending = ProjectFiles.PlanDualSave(scene, projectPath, intonerPath, stagehandPath);
            foreach (var t in pending.Targets)
                if (knownHashes.TryGetValue(Path.GetFullPath(t.Path), out var hash) && hash != ProjectFiles.Hash(t.Path)) { pending = null; throw new IOException("A previously opened/saved file was changed externally. Reopen it or select a new output path."); }
            acceptOmissions = false;
        });
        ImGui.SameLine(); if (ImGui.Button("Save canonical only")) Run(() =>
        {
            var full = Path.GetFullPath(projectPath);
            if (knownHashes.TryGetValue(full, out var hash) && hash != ProjectFiles.Hash(full)) throw new IOException("Project changed externally. Reopen or choose another path.");
            ProjectFiles.Commit(new SavePlan([new(full, ProjectJson.Save(scene), ProjectFiles.Hash(full))], []));
            knownHashes[full] = ProjectFiles.Hash(full); dirty = false; status = "Canonical project saved, including all preserved source fields.";
        });
        ImGui.TextWrapped("Dual-save writes all three files. Keep the canonical project for future editing; plugin exports alone cannot retain fields that their formats do not support. Existing files receive dated-by-ID recovery backups.");
        if (pending != null)
        {
            ImGui.BeginChild("report", new(0, 240), true);
            foreach (var t in pending.Targets) ImGui.TextWrapped((File.Exists(t.Path) ? "Replace: " : "Create: ") + t.Path);
            foreach (var issue in pending.Issues) ImGui.TextWrapped($"{(issue.Omitted ? "OMITTED" : "NOTE")} — {issue.ObjectName}: {issue.Message}");
            ImGui.EndChild();
            bool loss = pending.Issues.Any(i => i.Omitted);
            if (loss) ImGui.Checkbox("Export supported objects; preserve omitted objects in canonical project", ref acceptOmissions);
            ImGui.BeginDisabled(loss && !acceptOmissions);
            if (ImGui.Button("Write reviewed files")) Run(() => { ProjectFiles.Commit(pending); foreach (var t in pending.Targets) knownHashes[Path.GetFullPath(t.Path)] = ProjectFiles.Hash(t.Path); pending = null; dirty = false; status = "Saved canonical project and both plugin formats."; });
            ImGui.EndDisabled();
        }
        foreach (var note in scene.ImportNotes) ImGui.TextWrapped(note);
    }

    public void Dispose()
    {
        pi.UiBuilder.Draw -= Draw; pi.UiBuilder.OpenMainUi -= Open; pi.UiBuilder.OpenConfigUi -= Open;
        foreach (var command in new[] { "/sceneweaver", "/swedit", "/crossedit" }) commands.RemoveHandler(command);
        previews.Dispose();
        if (dirty) { try { ProjectFiles.Commit(new SavePlan([new(Path.Combine(pi.GetPluginConfigDirectory(), "recovery.cross.json"), ProjectJson.Save(scene))], [])); } catch { /* Host unload must still complete. */ } }
    }
}
