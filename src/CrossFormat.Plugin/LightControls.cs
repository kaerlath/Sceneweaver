using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Stagehand.Definitions.Objects;
using System.Text.Json.Nodes;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private void DrawLightTexture(SceneObject o, LightDefinition light, ref bool changed)
    {
        if (light.Shape == LightShape.Ambient)
        {
            if (light.ProjectedTextureGamePath.Length > 0) ImGui.TextWrapped("Ambient lights do not project textures. The saved texture is retained if you change the shape back.");
            return;
        }
        string path = light.ProjectedTextureGamePath;
        if (ImGui.InputText("Projected texture (.tex)", ref path, 4096, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            try { light.ProjectedTextureGamePath = LightSettings.TexturePath(path); changed = true; }
            catch (InvalidDataException e) { status = e.Message; }
        }
        ImGui.TextWrapped(light.Shape == LightShape.Point ? "Point lights require a cubemap texture." : "Spot and flat lights require a 2D texture.");
        ImGui.TextDisabled("Press Enter to apply a path. See the projection using Show scene here.");
        if (ImGui.Button("Use game example"))
        {
            light.ProjectedTextureGamePath = light.Shape == LightShape.Point
                ? "bgcommon/hou/dyna/lmp/lp/0018/texture/lmp_s0_m0018_1a_i.tex"
                : "bgcommon/hou/indoor/general/0538/texture/fun_b0_m0538_0a_i.tex";
            changed = true;
        }
        ImGui.SameLine(); if (ImGui.Button("Clear texture")) { light.ProjectedTextureGamePath = ""; changed = true; }
        string packId = ModResources.PackId(o);
        if (ImGui.BeginCombo("Texture modpack", ModResources.Packs(scene)[packId]?["DisplayName"]?.GetValue<string>() ?? "Game textures"))
        {
            if (ImGui.Selectable("Game textures", packId.Length == 0)) Edit(() => o.StagehandSource["ModpackId"] = "");
            foreach (var pack in ModResources.Packs(scene))
                if (ImGui.Selectable((pack.Value?["DisplayName"]?.GetValue<string>() ?? pack.Key) + "##" + pack.Key, packId == pack.Key))
                    Edit(() => o.StagehandSource["ModpackId"] = pack.Key);
            ImGui.EndCombo();
        }
        packId = ModResources.PackId(o);
        if (ModResources.Packs(scene)[packId]?["ModdedResources"] is JsonObject resources && ImGui.BeginCombo("Mod texture", "Choose a texture from this modpack"))
        {
            foreach (var resource in resources.Where(p => p.Key.EndsWith(".tex", StringComparison.OrdinalIgnoreCase)))
                if (ImGui.Selectable(resource.Key, light.ProjectedTextureGamePath == resource.Key))
                { light.ProjectedTextureGamePath = resource.Key; changed = true; }
            ImGui.EndCombo();
        }
        ImGui.TextWrapped("Import a Penumbra mod in Mods first to select its textures here. Projected textures are saved to Stagehand; Intoner exports keep the light without its projection.");
    }
}
