using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace CrossFormat.Plugin;

public sealed partial class Plugin : IDalamudPlugin
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
    private string inputPath = "", projectPath = "", intonerPath = "", stagehandPath = "", filter = "";
    private string libraryPath;
    private SavePlan? pending;
    private Action? pendingDestructive;
    private int assetPage;
    private bool focusScene;
    private string? operationError;
    private bool showOperationError;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IDataManager data, ITextureProvider textures, IClientState clientState, IObjectTable objectTable, IFramework framework, IPluginLog log)
    {
        pi = pluginInterface; commands = commandManager; previews = new(data, textures);
        this.clientState = clientState; this.objectTable = objectTable; this.framework = framework; this.log = log;
        liveBackend = new(pi, clientState, objectTable); live = new(liveBackend);
        var folder = pi.GetPluginConfigDirectory();
        previews.ModCacheDirectory = Path.Combine(folder, "mod-preview-cache");
        InitializeLocations(folder);
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
        framework.Update += TickLive;
        clientState.TerritoryChanged += OnTerritoryChanged;
    }
    private void Open() => open = true;
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e)
        {
            status = operationError = e.Message;
            showOperationError = true;
            log.Error(e, "Sceneweaver operation failed");
        }
    }
    private void Edit(Action action)
    {
        undo.Push(JsonSerializer.Serialize(scene, ProjectJson.Options));
        // Embedded mod resources can make snapshots large. Bound undo memory as well as count.
        long snapshotBytes = 0;
        var bounded = undo.TakeWhile((value, index) => { snapshotBytes += value.Length * 2L; return index == 0 || snapshotBytes <= 128 * 1024 * 1024; }).Reverse().ToArray();
        undo.Clear(); foreach (var value in bounded) undo.Push(value);
        if (undo.Count > 100) { var recent = undo.Take(100).Reverse().ToArray(); undo.Clear(); foreach (var snapshot in recent) undo.Push(snapshot); }
        redo.Clear(); action(); scene.Revision++; dirty = true; pending = null;
        live.MarkChanged();
    }
    private void ReplaceScene(SceneProject value)
    {
        live.Stop("Live display hidden while switching projects.");
        modOpenTask = null; modBuildTask = null; modImport = null; modBuilt = null; modPreview = null;
        updateModId = ""; restoreModChoices = null;
        scene = value; selected = scene.Objects.FirstOrDefault()?.Id ?? Guid.Empty;
        undo.Clear(); redo.Clear(); pending = null; dirty = false; focusScene = true;
    }
    private void GuardReplace(Action action) { if (dirty) pendingDestructive = action; else action(); }

    private void Draw()
    {
        CompletePicker();
        if (!open) return;
        previews.SetModpacks(scene.StagehandRoot["EmbeddedModpacks"] as JsonObject);
        ImGui.SetNextWindowSize(new(1280, 860), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new(980, 650), new(float.MaxValue, float.MaxValue));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(20, 16));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 8));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.055f, .071f, .095f, 1));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.073f, .094f, .122f, 1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.11f, .23f, .30f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.16f, .35f, .43f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.12f, .31f, .38f, 1));
        if (ImGui.Begin("Sceneweaver###Sceneweaver", ref open))
        {
            ImGui.TextColored(ImGui.ColorConvertFloat4ToU32(new(.4f, .85f, .95f, 1)), "SCENEWEAVER");
            ImGui.SameLine(); ImGui.TextDisabled("One scene. Multiple stages.");
            DrawImportButtons();
            ImGui.TextWrapped(status);
            if (showOperationError) { ImGui.OpenPopup("Sceneweaver could not complete the action"); showOperationError = false; }
            if (ImGui.BeginPopupModal("Sceneweaver could not complete the action", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 560);
                ImGui.TextUnformatted(operationError ?? "Unknown error.");
                ImGui.PopTextWrapPos();
                if (ImGui.Button("Close")) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
            if (ImGui.Button("New")) GuardReplace(() => ReplaceScene(new()));
            ImGui.SameLine();
            ImGui.BeginDisabled(undo.Count == 0);
            if (ImGui.Button("Undo")) { redo.Push(JsonSerializer.Serialize(scene, ProjectJson.Options)); scene = JsonSerializer.Deserialize<SceneProject>(undo.Pop(), ProjectJson.Options)!; dirty = true; pending = null; live.MarkChanged(); }
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.BeginDisabled(redo.Count == 0);
            if (ImGui.Button("Redo")) { undo.Push(JsonSerializer.Serialize(scene, ProjectJson.Options)); scene = JsonSerializer.Deserialize<SceneProject>(redo.Pop(), ProjectJson.Options)!; dirty = true; pending = null; live.MarkChanged(); }
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.TextDisabled($"{scene.Name}  /  {scene.Objects.Count} objects  /  {(dirty ? "Unsaved changes" : "No unsaved changes")}");
            ImGui.Separator();
            DrawLiveControls();
            if (pendingDestructive != null)
            {
                ImGui.TextWrapped("This will replace your unsaved scene. Save it first or discard the changes below.");
                if (ImGui.Button("Discard changes and continue")) { var action = pendingDestructive; pendingDestructive = null; Run(action); }
                ImGui.SameLine(); if (ImGui.Button("Keep editing")) pendingDestructive = null;
                ImGui.Separator();
            }
            if (ImGui.BeginTabBar("workspace"))
            {
                if (ImGui.BeginTabItem("Discover assets")) { DrawAssets(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Scene", focusScene ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
                { focusScene = false; DrawScene(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Mods")) { DrawMods(); ImGui.EndTabItem(); }
                if (ImGui.BeginTabItem("Save & export")) { DrawFiles(); ImGui.EndTabItem(); }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
        ImGui.PopStyleColor(5); ImGui.PopStyleVar(4);
    }

    private void DrawScene()
    {
        var name = scene.Name;
        if (ImGui.InputText("Project name", ref name, 512)) Edit(() => scene.Name = name);
        if (ImGui.Button("Add model")) Edit(() => { var o = new SceneObject { Position = NewObjectPosition() }; scene.Objects.Add(o); selected = o.Id; });
        ImGui.SameLine(); if (ImGui.Button("Add VFX")) Edit(() => { var o = new SceneObject { Kind = AssetKind.Vfx, Name = "New VFX", Position = NewObjectPosition() }; scene.Objects.Add(o); selected = o.Id; });
        ImGui.SameLine(); if (ImGui.Button("Add light")) Edit(() => { var o = new SceneObject { Kind = AssetKind.Light, Name = "New light", Position = NewObjectPosition(), Light = JsonSerializer.SerializeToNode(new Stagehand.Definitions.Objects.LightDefinition(), ProjectJson.Options)!.AsObject() }; scene.Objects.Add(o); selected = o.Id; });
        var sceneHeight = Math.Max(240, ImGui.GetContentRegionAvail().Y - 160);
        ImGui.BeginChild("objects", new(300, sceneHeight), true);
        ImGui.TextDisabled("SCENE OBJECTS"); ImGui.Separator();
        if (scene.Objects.Count == 0) ImGui.TextWrapped("Your scene is empty. Discover assets to preview models and add what you need, or open an existing layout above.");
        foreach (var o in scene.Objects)
        {
            if (ImGui.Selectable($"{o.Name} [{o.Kind}]##{o.Id}", selected == o.Id)) selected = o.Id;
        }
        ImGui.EndChild(); ImGui.SameLine(); ImGui.BeginChild("inspector", new(0, sceneHeight), true);
        var item = scene.Objects.FirstOrDefault(o => o.Id == selected);
        if (item != null) DrawInspector(item);
        else ImGui.TextWrapped("Select an object to edit its transform and appearance.");
        ImGui.EndChild();
        if (ImGui.CollapsingHeader("Stage placement for live display / Intoner export"))
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
        ImGui.TextDisabled("OBJECT INSPECTOR");
        ImGui.BeginDisabled(o.Locked);
        if (ImGui.Button("Move to my character")) Run(() =>
        {
            var position = LiveScene.LocalPosition(scene, PlayerPosition());
            Edit(() => o.Position = position);
        });
        ImGui.EndDisabled();
        if (ModResources.PackId(o).Length > 0)
        {
            ImGui.TextWrapped("Mod binding: " + (ModResources.Packs(scene)[ModResources.PackId(o)]?["DisplayName"]?.GetValue<string>() ?? "Missing modpack"));
            ImGui.BeginDisabled(o.Locked);
            if (ImGui.Button("Remove mod binding")) Edit(() => o.StagehandSource["ModpackId"] = "");
            ImGui.EndDisabled();
        }
        previews.Draw(o, new(Math.Max(180, ImGui.GetContentRegionAvail().X), 220), true);
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
        if (o.Kind == AssetKind.Vfx) DrawVfxPlayback(o);
        if (o.Kind is AssetKind.BgObject or AssetKind.Furniture)
            if (ImGui.SliderFloat("Opacity", ref opacity, 0, 1)) Edit(() => o.Opacity = opacity);
        if (ImGui.InputText("Preview PNG/JPG", ref image, 4096)) Edit(() => o.PreviewImagePath = image);
        ImGui.BeginDisabled(filePicker != null);
        if (ImGui.Button("Choose preview image...")) Pick("Preview image", false, "", file => Edit(() => o.PreviewImagePath = file), true);
        ImGui.EndDisabled();
        if (o.Kind == AssetKind.Light && ImGui.CollapsingHeader("Light parameters"))
        {
            DrawLight(o);
        }
        if (ImGui.Button("Duplicate")) Edit(() => { var copy = JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(o, ProjectJson.Options), ProjectJson.Options)!; copy.Id = Guid.NewGuid(); copy.StagehandId = ""; copy.Name += " copy"; scene.Objects.Add(copy); selected = copy.Id; });
        ImGui.SameLine(); if (ImGui.Button("Delete")) Edit(() => scene.Objects.Remove(o));
        ImGui.EndDisabled();
        ImGui.BeginDisabled(ModResources.PackId(o).Length > 0);
        if (ImGui.Button("Save as favorite")) Run(() =>
        {
            assets.Add(JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(o, ProjectJson.Options), ProjectJson.Options)!);
            SaveLibrary(); lastQuery = null; status = "Asset saved to favorites.";
        });
        ImGui.EndDisabled();
        if (ModResources.PackId(o).Length > 0) ImGui.TextDisabled("Mod assets are kept in this project's Mods library.");
    }

    private void DrawVfxPlayback(SceneObject o)
    {
        if (!ImGui.CollapsingHeader("VFX playback (Intoner)", ImGuiTreeNodeFlags.DefaultOpen)) return;
        ImGui.TextWrapped("Saved in your project and Intoner exports. Stagehand exports and in-game display do not apply these playback settings.");
        var playback = VfxPlayback.For(o);
        var speed = playback.Speed; var paused = playback.Paused; var fade = playback.FadeInSeconds;
        var replay = playback.ReplayOnTransform; var loop = playback.Loop; var interval = playback.LoopIntervalSeconds;
        bool changed = false;
        if (ImGui.SliderFloat("Playback speed", ref speed, 0, 4, "%.2fx")) { playback = playback with { Speed = Math.Clamp(speed, 0, 4) }; changed = true; }
        if (ImGui.Checkbox("Paused", ref paused)) { playback = playback with { Paused = paused }; changed = true; }
        if (ImGui.SliderFloat("Fade-in (seconds)", ref fade, 0, 60, "%.2f")) { playback = playback with { FadeInSeconds = Math.Clamp(fade, 0, 60) }; changed = true; }
        if (ImGui.Checkbox("Replay when moved, rotated or scaled", ref replay)) { playback = playback with { ReplayOnTransform = replay }; changed = true; }
        if (ImGui.Checkbox("Loop / replay on a timer", ref loop)) { playback = playback with { Loop = loop }; changed = true; }
        ImGui.BeginDisabled(!loop);
        if (ImGui.SliderInt("Replay interval (seconds)", ref interval, 1, 60)) { playback = playback with { LoopIntervalSeconds = Math.Clamp(interval, 1, 60) }; changed = true; }
        ImGui.EndDisabled();
        if (ImGui.Button("Reset playback")) { playback = new(); changed = true; }
        if (changed) Edit(() => o.Playback = playback);
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

    private void SaveLibrary() => ProjectFiles.Commit(new SavePlan([new(libraryPath, JsonSerializer.Serialize(assets, ProjectJson.Options))], []));

    private void DrawFiles()
    {
        ImGui.TextColored(ImGui.ColorConvertFloat4ToU32(new(.4f, .85f, .95f, 1)), "SAVE ONCE, EXPORT BOTH");
        ImGui.TextWrapped("Keep the Sceneweaver project for continued editing. Choose where each plugin's copy should be saved below.");
        if (inputPath.Length > 0) ImGui.TextWrapped("Opened: " + inputPath);
        ImGui.BeginDisabled(filePicker != null);
        if (ImGui.Button("Open Stagehand autosave...")) Pick("Stagehand autosave", false, "", ImportFile);
        ImGui.EndDisabled();
        ImGui.Separator();
        Destination("Sceneweaver", projectPath, value => projectPath = value);
        Destination("Intoner", intonerPath, value => intonerPath = value);
        Destination("Stagehand", stagehandPath, value => stagehandPath = value);
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
        ImGui.TextWrapped("Both exports are reviewed before writing. Unsupported data stays in your Sceneweaver project, and existing files receive recovery backups.");
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
        framework.Update -= TickLive; clientState.TerritoryChanged -= OnTerritoryChanged;
        live.Stop("Sceneweaver unloaded.");
        if (live.CleanupPending) log.Warning("Sceneweaver could not remove its temporary Stagehand scene on unload: {Status}", live.Status);
        pi.UiBuilder.Draw -= Draw; pi.UiBuilder.OpenMainUi -= Open; pi.UiBuilder.OpenConfigUi -= Open;
        foreach (var command in new[] { "/sceneweaver", "/swedit", "/crossedit" }) commands.RemoveHandler(command);
        previews.Dispose();
        if (dirty) { try { ProjectFiles.Commit(new SavePlan([new(Path.Combine(pi.GetPluginConfigDirectory(), "recovery.cross.json"), ProjectJson.Save(scene))], [])); } catch { /* Host unload must still complete. */ } }
    }
}
