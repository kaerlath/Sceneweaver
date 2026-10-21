using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private WorldCapture? worldCapture;
    private readonly HashSet<int> captureSelection = [];
    private bool captureRequested;
    private float captureRadius = 30;
    private string captureName = "Captured scenery", captureFilter = "";

    private void DrawCaptureControls()
    {
        if (!ImGui.CollapsingHeader("Capture nearby as scene")) return;
        ImGui.TextWrapped("Capture loaded models and VFX, including shared stages and map props. Radius is measured from your character to object origins, including above/below you. Objects behind walls may be included; terrain, NPCs and mod files are not captured.");
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputFloat("Radius (game units)", ref captureRadius)) captureRadius = float.IsFinite(captureRadius) ? Math.Clamp(captureRadius, 1, 200) : 30;
        ImGui.BeginDisabled(captureRequested || !clientState.IsLoggedIn);
        if (ImGui.Button("Scan for capture")) { captureRequested = true; nextWorldRefresh = TimeSpan.Zero; }
        ImGui.EndDisabled();
        if (captureRequested) ImGui.TextDisabled("Waiting for a fresh world scan...");
        if (worldCapture is not { } snapshot) return;
        ImGui.TextWrapped($"Frozen snapshot: {snapshot.Items.Length} entries within {snapshot.Radius:0.#} units; {captureSelection.Count} selected. Origin: {snapshot.Origin}.");
        ImGui.TextWrapped("Scan limit: 2,000 nearby entries / 50,000 visited nodes. This may be incomplete in busy areas. Unsupported mod-specific paths are excluded; even ordinary game paths can have modded appearances.");
        ImGui.InputText("Captured project name", ref captureName, 256);
        ImGui.InputTextWithHint("##capture-filter", "Filter capture paths", ref captureFilter, 256);
        if (ImGui.Button("Select matching supported"))
            for (int i = 0; i < snapshot.Items.Length; i++) if (snapshot.Items[i].CanUse && Matches(snapshot.Items[i])) captureSelection.Add(i);
        ImGui.SameLine(); if (ImGui.Button("Deselect matching"))
            for (int i = 0; i < snapshot.Items.Length; i++) if (Matches(snapshot.Items[i])) captureSelection.Remove(i);
        ImGui.BeginChild("capture-review", new(0, 180), true);
        for (int i = 0; i < snapshot.Items.Length; i++)
        {
            var item = snapshot.Items[i]; if (!Matches(item)) continue;
            ImGui.PushID(i);
            bool include = captureSelection.Contains(i);
            ImGui.BeginDisabled(!item.CanUse);
            if (ImGui.Checkbox("##include", ref include)) { if (include) captureSelection.Add(i); else captureSelection.Remove(i); }
            ImGui.EndDisabled(); ImGui.SameLine();
            if (ImGui.Selectable($"{Vector3.Distance(snapshot.Origin, item.Position):0.0} | {item.Kind} | {Path.GetFileName(item.ResourcePath)}{(item.CanUse ? "" : " [unsupported path]")}", pickedObject == item)) SelectWorldObject(item);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.ResourcePath);
            ImGui.PopID();
        }
        ImGui.EndChild();
        ImGui.BeginDisabled(captureSelection.Count == 0 || string.IsNullOrWhiteSpace(captureName));
        if (ImGui.Button("Create new scene from selection")) Run(() =>
        {
            var captured = snapshot.Create(captureName, captureSelection);
            GuardReplace(() =>
            {
                ReplaceScene(captured);
                inputPath = projectPath = intonerPath = stagehandPath = ""; knownHashes.Clear(); dirty = true;
                status = $"Captured {captured.Objects.Count} objects. Choose new save destinations in Save & export. Use Place stage origin here to relocate the whole arrangement.";
            });
        });
        ImGui.EndDisabled();
        ImGui.SameLine(); if (ImGui.Button("Discard capture")) { worldCapture = null; captureSelection.Clear(); }
        ImGui.Separator();
    }
    private bool Matches(WorldPick item) => item.ResourcePath.Contains(captureFilter, StringComparison.OrdinalIgnoreCase);
}
