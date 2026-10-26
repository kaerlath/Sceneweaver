using CrossFormat.Core;
using Dalamud.Bindings.ImGui;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private List<ReceivedStageSnapshot> receivedStages = [];
    private string receivedSelection = "", receivedFilter = "", receivedStatus = "Refresh to find PlayerSync stages currently displayed here.";
    private Task<List<ReceivedStageSnapshot>>? receivedRefresh;
    private Task<SceneProject>? receivedImport;

    private void DrawReceivedStages()
    {
        ImGui.TextWrapped("Received stages — experimental PlayerSync integration");
        ImGui.TextWrapped("Browse displayed PlayerSync stages separately from room furniture. Import creates an independent project with available mod files embedded. The received stage stays untouched. Internal plugin updates may temporarily disable this browser.");
        if (receivedRefresh?.IsCompleted == true)
        {
            var task = receivedRefresh; receivedRefresh = null;
            try { receivedStages = task.GetAwaiter().GetResult(); receivedStatus = $"{receivedStages.Count} displayed stages found."; }
            catch (Exception e) { receivedStages.Clear(); receivedStatus = e.GetBaseException().Message; }
        }
        if (receivedImport?.IsCompleted == true)
        {
            var task = receivedImport; receivedImport = null;
            Run(() =>
            {
                var imported = task.GetAwaiter().GetResult();
                GuardReplace(() =>
                {
                    ReplaceScene(imported); inputPath = projectPath = intonerPath = stagehandPath = "";
                    knownHashes.Clear(); dirty = true;
                    status = "Received stage imported as an independent copy. Choose new destinations in Save & export.";
                });
            });
        }
        bool busy = receivedRefresh != null || receivedImport != null;
        ImGui.BeginDisabled(busy || !clientState.IsLoggedIn);
        if (ImGui.Button("Refresh received stages"))
        {
            receivedRefresh = framework.RunOnFrameworkThread(() => ReceivedStageReader.Read(pi));
            receivedStatus = "Reading displayed stages...";
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(receivedImport != null ? "Copying and embedding mod files..." : receivedStatus);
        ImGui.InputTextWithHint("##received-filter", "Filter by stage name or author", ref receivedFilter, 256);
        foreach (var stage in receivedStages)
        {
            if (!(stage.Name + " " + stage.Author).Contains(receivedFilter, StringComparison.OrdinalIgnoreCase)) continue;
            if (ImGui.Selectable($"{stage.Name} — {stage.Author}##{stage.Id}", receivedSelection == stage.Id)) receivedSelection = stage.Id;
        }
        var selectedStage = receivedStages.FirstOrDefault(s => s.Id == receivedSelection);
        if (selectedStage == null) return;
        ImGui.Separator();
        ImGui.TextWrapped($"Stage ID: {selectedStage.Id}");
        ImGui.TextWrapped("Import uses a fresh snapshot. Missing cache files stop the import rather than silently losing the modded appearance. Large imports are limited to 64 MB per file and 128 MB of copied resources.");
        ImGui.BeginDisabled(busy || !clientState.IsLoggedIn);
        if (ImGui.Button("Import as new project"))
        {
            string id = selectedStage.Id;
            receivedImport = ImportReceivedAsync(id);
        }
        ImGui.EndDisabled();
    }

    private async Task<SceneProject> ImportReceivedAsync(string id)
    {
        var snapshot = await framework.RunOnFrameworkThread(() => ReceivedStageReader.Read(pi).FirstOrDefault(s => s.Id == id)
            ?? throw new InvalidOperationException("This stage is no longer displayed. Refresh the received stages list."));
        return await Task.Run(() => ReceivedStages.Import(snapshot));
    }
}
