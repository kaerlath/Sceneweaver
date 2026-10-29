using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
namespace CrossFormat.Plugin;
public sealed partial class Plugin
{
    private void DrawDisplayStatus()
    {
        ImGui.TextWrapped(live.Enabled ? "Sceneweaver editing display: ON (via Stagehand). This is a separate local copy." : "Sceneweaver editing display: OFF.");
        if(live.Enabled) { ImGui.SameLine(); if(ImGui.Button("Hide in game")) live.Stop(); }
        if(live.CleanupPending) {ImGui.TextWrapped(live.Status);if(ImGui.Button("Retry hiding scene")) live.Stop();}
        ImGui.TextDisabled("Other local and synced stages are controlled in Stagehand, Intoner or your sync client.");
    }
    private SceneObject? DestinationSelection()
    {
        var item=scene.Objects.FirstOrDefault(o=>o.Id==selected);
        if(ImGui.BeginCombo("Selected object (shared with Scene)",item?.Name ?? "Choose an object"))
        {
            foreach(var candidate in scene.Objects)
                if(ImGui.Selectable(candidate.Name+" ["+candidate.Kind+"]##"+candidate.Id,candidate.Id==selected)) {selected=candidate.Id;item=candidate;}
            ImGui.EndCombo();
        }
        if(item==null) ImGui.TextWrapped("Select an object here or in Scene to edit its destination settings.");
        else if(item.Locked) ImGui.TextDisabled("Object is locked. Unlock it in Scene to edit settings.");
        return item;
    }
    private void DrawStagehandWorkspace()
    {
        ImGui.TextDisabled("STAGEHAND SETTINGS");
        ImGui.TextWrapped("The same scene and selection are used in every tab. Opening this tab does not show or hide a stage.");
        if(!ImGui.BeginTabBar("stagehand-sections")) return;
        if(ImGui.BeginTabItem("Live display & placement"))
        {
            ImGui.TextWrapped("This starts a separate local editing display. Pause or hide the matching stage in Stagehand or your sync client first to avoid duplicate scenery. It does not publish changes to others.");
            DrawLiveControls(); DrawStagePlacement(); ImGui.EndTabItem();
        }
        if(ImGui.BeginTabItem("Object settings"))
        {
            var item=DestinationSelection();
            if(item!=null)
            {
                ImGui.BeginDisabled(item.Locked);
                if(item.Kind is AssetKind.Group or AssetKind.Unknown or AssetKind.Furniture)
                    ImGui.TextWrapped("No Stagehand mod binding controls for this object type. Unsupported types are reported in Save & export.");
                else
                {
                    if(item.Kind!=AssetKind.Light)
                    {
                    var id=ModResources.PackId(item); var packs=ModResources.Packs(scene);
                    if(ImGui.BeginCombo("Embedded modpack (Stagehand only)",id.Length==0?"Game resources":packs[id]?["DisplayName"]?.GetValue<string>() ?? "Missing modpack"))
                    {
                        if(ImGui.Selectable("Game resources",id.Length==0)) Edit(()=>item.StagehandSource["ModpackId"]="");
                        foreach(var pack in packs)
                            if(ImGui.Selectable((pack.Value?["DisplayName"]?.GetValue<string>() ?? pack.Key)+"##"+pack.Key,pack.Key==id)) Edit(()=>item.StagehandSource["ModpackId"]=pack.Key);
                        ImGui.EndCombo();
                    }
                    ImGui.TextWrapped("Import mod files in Embedded mods, then bind the matching pack here. The asset path in Scene must match a resource provided by that pack.");
                    }
                    if(item.Kind==AssetKind.Light)
                    {
                        var light=LightSettings.Read(item);bool changed=false;
                        DrawLightModBinding(item,light,ref changed);
                        if(changed)Edit(()=>LightSettings.Write(item,light));
                    }
                }
                ImGui.EndDisabled();
            }
            ImGui.EndTabItem();
        }
        if(ImGui.BeginTabItem("Embedded mods")) {DrawMods();ImGui.EndTabItem();}
        if(ImGui.BeginTabItem("Received stages")) {DrawReceivedStages();ImGui.EndTabItem();}
        ImGui.EndTabBar();
    }
    private void DrawIntonerWorkspace()
    {
        ImGui.TextDisabled("INTONER SETTINGS");
        ImGui.TextWrapped("These settings are saved for Intoner 0.3+. They do not start an Intoner display or change the Stagehand preview's collision and playback behavior.");
        if(!ImGui.BeginTabBar("intoner-sections")) return;
        if(ImGui.BeginTabItem("Object settings"))
        {
            var item=DestinationSelection();
            if(item!=null)
            {
                ImGui.BeginDisabled(item.Locked);
                DrawIntonerFeatures(item);
                if(item.Kind==AssetKind.Vfx)DrawVfxPlayback(item);
                if(item.Kind==AssetKind.Furniture)ImGui.TextWrapped("Intoner furniture payloads and material settings are retained on import/export. Shared placement and opacity are edited in Scene.");
                if(item.Kind==AssetKind.Light)ImGui.TextWrapped("Light parameters and projected texture paths are shared with Stagehand and edited in Scene. Intoner needs matching resources for modded textures.");
                if(item.Kind is AssetKind.Group or AssetKind.Sound or AssetKind.Weapon or AssetKind.Unknown)ImGui.TextWrapped("No editable Intoner-specific controls for this entry. See Save & export for conversion details.");
                ImGui.EndDisabled();
            }
            ImGui.EndTabItem();
        }
        if(ImGui.BeginTabItem("Layouts & sources")){DrawIntonerScenes();ImGui.EndTabItem();}
        ImGui.EndTabBar();
    }
}
