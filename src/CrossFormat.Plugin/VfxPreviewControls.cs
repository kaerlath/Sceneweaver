using CrossFormat.Core;
using Dalamud.Bindings.ImGui;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private readonly LiveSceneSession vfxPreview;
    private LiveSceneSnapshot? previewSnapshot;
    private string previewSelection = "";
    private bool previewDrawn, autoVfxPreview;
    private float previewDistance = 2, previewScale = 1;

    private void StartVfxPreview(SceneObject asset)
    {
        RequireLiveBackend();
        var player = objectTable.LocalPlayer ?? throw new InvalidOperationException("Log into the game to preview VFX.");
        var snapshot = VfxPreview.Build(scene, asset, player.Position, player.Rotation, previewDistance, previewScale);
        vfxPreview.Stop(); previewSnapshot = snapshot;
        vfxPreview.Start(() => snapshot, liveClock.Elapsed);
    }
    private void DrawVfxPreview(SceneObject asset)
    {
        previewDrawn = true;
        string key = $"{asset.Id}|{ModResources.PackId(asset)}|{asset.AssetPath}";
        if (key != previewSelection)
        {
            vfxPreview.Stop(); previewSelection = key;
            if (autoVfxPreview) Run(() => StartVfxPreview(asset));
        }
        ImGui.TextWrapped("Animated VFX preview in the game. This temporary effect is not added to your scene. Requires Stagehand.");
        if (ImGui.Checkbox("Preview VFX automatically when selected", ref autoVfxPreview))
        { if (autoVfxPreview) Run(() => StartVfxPreview(asset)); else vfxPreview.Stop(); }
        bool changed = ImGui.SliderFloat("Preview distance", ref previewDistance, 0, 30, "%.1f m");
        changed |= ImGui.SliderFloat("Preview scale", ref previewScale, .01f, 10, "%.2fx");
        previewDistance = Math.Clamp(previewDistance, 0, 30); previewScale = Math.Clamp(previewScale, .01f, 10);
        if (changed && vfxPreview.Enabled) Run(() =>
        {
            var player = objectTable.LocalPlayer ?? throw new InvalidOperationException("Log into the game to preview VFX.");
            previewSnapshot = VfxPreview.Build(scene, asset, player.Position, player.Rotation, previewDistance, previewScale);
            vfxPreview.MarkChanged();
        });
        if (ImGui.Button(vfxPreview.Enabled ? "Restart VFX preview" : "Preview VFX in game")) Run(() => StartVfxPreview(asset));
        ImGui.SameLine(); if (ImGui.Button("Stop preview")) vfxPreview.Stop();
        if (vfxPreview.CleanupPending && ImGui.Button("Retry preview cleanup")) vfxPreview.Stop();
        ImGui.TextWrapped(vfxPreview.Enabled ? "VFX preview playing near your character." : vfxPreview.Status);
        foreach (var note in vfxPreview.Notes) ImGui.TextWrapped(note);
    }
}
