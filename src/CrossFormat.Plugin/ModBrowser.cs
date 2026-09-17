using System.Numerics;
using System.Text.Json.Nodes;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private Task<ModImport>? modOpenTask;
    private Task<ModImportResult>? modBuildTask;
    private ModImport? modImport;
    private ModImportResult? modBuilt;
    private List<int[]> modSelections = [];
    private string selectedModId = "", modSearch = "";
    private SceneObject? modPreview;

    private void DrawMods()
    {
        if (modOpenTask is { IsCompleted: true })
        {
            var done = modOpenTask; modOpenTask = null;
            Run(() => { modImport = done.GetAwaiter().GetResult(); modSelections = modImport.Groups.Select(g => g.Defaults.ToArray()).ToList(); modBuilt = null; });
        }
        if (modBuildTask is { IsCompleted: true })
        {
            var done = modBuildTask; modBuildTask = null;
            Run(() => { modBuilt = done.GetAwaiter().GetResult(); status = "Mod prepared. Review the resource counts and add it to this project."; });
        }
        ImGui.TextColored(ImGui.ColorConvertFloat4ToU32(new(.4f, .85f, .95f, 1)), "MOD LIBRARY");
        ImGui.TextWrapped("Import a Penumbra .pmp or choose meta.json in an installed mod folder. Its selected files are embedded in the project and Stagehand export.");
        ImGui.BeginDisabled(filePicker != null || modOpenTask != null || modBuildTask != null);
        if (ImGui.Button("Import mod...")) Pick("Mod", false, "", path => { modImport = null; modBuilt = null; modOpenTask = Task.Run(() => ModImport.Open(path)); });
        ImGui.EndDisabled();
        if (modOpenTask != null || modBuildTask != null) { ImGui.SameLine(); ImGui.TextDisabled("Reading mod files..."); }
        if (modImport != null)
        {
            ImGui.Separator(); ImGui.TextUnformatted(modImport.Name);
            ImGui.BeginDisabled(modBuildTask != null);
            if (ImGui.BeginChild("mod-options", new(0, Math.Min(180, 50 + modImport.Groups.Count * 45)), true))
            {
                for (int g = 0; g < modImport.Groups.Count; g++)
                {
                    var group = modImport.Groups[g]; ImGui.PushID(g);
                    if (group.Multiple)
                    {
                        if (ImGui.TreeNode(group.Name))
                        {
                            for (int i = 0; i < group.Options.Length; i++)
                            {
                                bool enabled = modSelections[g].Contains(i);
                                if (ImGui.Checkbox(group.Options[i] + "##" + i, ref enabled)) { modSelections[g] = enabled ? modSelections[g].Append(i).ToArray() : modSelections[g].Where(n => n != i).ToArray(); modBuilt = null; }
                            }
                            ImGui.TreePop();
                        }
                    }
                    else if (group.Options.Length > 0)
                    {
                        int option = modSelections[g].FirstOrDefault();
                        if (ImGui.Combo(group.Name, ref option, string.Join('\0', group.Options) + "\0")) { modSelections[g] = [option]; modBuilt = null; }
                    }
                    ImGui.PopID();
                }
                if (modImport.Groups.Count == 0) ImGui.TextDisabled("No selectable options; use the mod's default files.");
            }
            ImGui.EndChild();
            if (ImGui.Button("Prepare selected files")) { var import = modImport; var settings = modSelections.Select(s => s.ToArray()).ToArray(); modBuilt = null; modBuildTask = Task.Run(() => import.Build(settings)); }
            ImGui.SameLine(); if (ImGui.Button("Cancel import")) { modImport = null; modBuilt = null; }
            ImGui.EndDisabled();
            if (modBuilt != null)
            {
                ImGui.TextUnformatted($"{modBuilt.Resources} resources, {modBuilt.Placeable} models / effects / sounds. Materials and textures are included.");
                if (ImGui.Button("Add mod to this project")) Run(() =>
                {
                    var built = modBuilt;
                    Edit(() => selectedModId = ModResources.Attach(scene, built.Pack));
                    modImport = null; modBuilt = null; modPreview = null;
                    status = "Mod added. Select a resource below to preview it or add it to your scene.";
                });
            }
        }
        ImGui.Separator();
        var packs = ModResources.Packs(scene);
        if (packs.Count == 0) { ImGui.TextWrapped("No mods in this project. Modpacks from opened Stagehand saves also appear here."); return; }
        if (!packs.ContainsKey(selectedModId)) { selectedModId = packs.First().Key; modPreview = null; }
        string PackName(string id) => packs[id]?["DisplayName"]?.GetValue<string>() ?? id;
        ImGui.SetNextItemWidth(350);
        if (ImGui.BeginCombo("Project mod", PackName(selectedModId)))
        {
            foreach (var p in packs) if (ImGui.Selectable(PackName(p.Key) + "##" + p.Key, selectedModId == p.Key)) { selectedModId = p.Key; modPreview = null; }
            ImGui.EndCombo();
        }
        var selectedObject = scene.Objects.FirstOrDefault(o => o.Id == selected);
        ImGui.BeginDisabled(selectedObject == null || selectedObject.Locked);
        if (ImGui.Button("Apply mod to selected scene object")) Edit(() => selectedObject!.StagehandSource["ModpackId"] = selectedModId);
        ImGui.EndDisabled();
        ImGui.SameLine(); ImGui.TextDisabled(selectedObject?.Name ?? "Select an object in Scene first");
        ImGui.TextWrapped("Stagehand export keeps these bindings. Intoner export reports mod-bound objects as unsupported. Effects can be added, but animated VFX previews are not available yet.");
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##modsearch", "Search this mod's resource paths", ref modSearch, 256);
        if (!ImGui.BeginTable("mod-browser", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Resources", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthFixed, 380);
        float height = Math.Max(240, ImGui.GetContentRegionAvail().Y - 65);
        ImGui.TableNextColumn(); ImGui.BeginChild("mod-resource-list", new(0, height), false);
        if (packs[selectedModId]?["ModdedResources"] is JsonObject resources)
        {
            var placeable = resources.Where(p => ModResources.Kind(p.Key) != null && p.Key.Contains(modSearch, StringComparison.OrdinalIgnoreCase)).ToArray();
            ImGui.TextDisabled($"{placeable.Length} placeable resources / {resources.Count} total files");
            foreach (var p in placeable.Take(500))
                if (ImGui.Selectable(p.Key, modPreview?.AssetPath == p.Key)) { modPreview = ModResources.CreateObject(selectedModId, p.Key); previews.ResetView(); }
            if (placeable.Length > 500) ImGui.TextWrapped("Showing 500 results. Narrow the search to see others.");
            if (placeable.Length == 0) ImGui.TextWrapped("No matching model, effect or sound. For texture-only mods, select a Scene object and apply this mod above.");
        }
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("mod-resource-preview", new(0, height), false);
        if (modPreview != null)
        {
            ImGui.TextWrapped(modPreview.Name);
            previews.Draw(modPreview, new(Math.Max(100, ImGui.GetContentRegionAvail().X), 260), true);
            previews.DrawViewOptions();
            if (ImGui.Button("Add mod asset to scene", new(-1, 32))) Run(() => AddSceneAsset(modPreview, live.Enabled));
            if (ImGui.Button("Place in game at my character", new(-1, 0))) Run(() => AddSceneAsset(modPreview, true));
        }
        else ImGui.TextWrapped("Select a resource to inspect it. Previewing does not spawn it in the room.");
        ImGui.EndChild(); ImGui.EndTable();
    }
}
