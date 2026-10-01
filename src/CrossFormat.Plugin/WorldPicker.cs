using System.Numerics;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private readonly IGameGui gameGui;
    private readonly ICondition condition;
    private List<WorldPick> nearbyObjects = [];
    private readonly WorldPickHistory pickHistory = new();
    private WorldPick? pickedObject;
    private SceneObject? pickedAsset;
    private bool worldPickerDrawn, worldPicking, refreshWorld = true, autoRefreshWorld = true;
    private TimeSpan nextWorldRefresh;
    private string worldFilter = "", worldReadStatus = "Open Nearby or refresh to scan loaded objects.";

    private void ClearWorldPicker()
    {
        worldPicking = false; nearbyObjects = []; pickHistory.Items.Clear(); pickedObject = null; pickedAsset = null;
        nextWorldRefresh = liveClock.Elapsed + TimeSpan.FromSeconds(2); refreshWorld = true;
    }
    private void TickWorldPicker()
    {
        if (!clientState.IsLoggedIn || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
        { ClearWorldPicker(); return; }
        if (!open || !worldPickerDrawn || objectTable.LocalPlayer == null || liveClock.Elapsed < nextWorldRefresh) return;
        if (!refreshWorld && !autoRefreshWorld && !worldPicking) return;
        nextWorldRefresh = liveClock.Elapsed + TimeSpan.FromSeconds(worldPicking ? .5 : 3);
        refreshWorld = false;
        try { nearbyObjects = WorldSceneReader.Read(objectTable.LocalPlayer.Position); worldReadStatus = $"{nearbyObjects.Count} loaded models/effects within 200 m (up to 2,000)."; }
        catch (Exception e) { worldPicking = false; nearbyObjects = []; worldReadStatus = "Could not read world objects: " + e.Message; log.Error(e, "World picker scan failed"); }
    }
    private void SelectWorldObject(WorldPick item)
    {
        pickedObject = item; pickedAsset = item.CanUse ? item.CreateAsset() : null; pickHistory.Add(item); previews.ResetView();
    }
    private IEnumerable<WorldPick> FilterPicks(IEnumerable<WorldPick> source) => source.Where(p => p.ResourcePath.Contains(worldFilter, StringComparison.OrdinalIgnoreCase));
    private void DrawWorldPicker()
    {
        worldPickerDrawn = true;
        ImGui.TextWrapped("Pick a loaded model or VFX. World markers indicate object origins; they are not exact mesh hit tests and may be visible through walls. Recent entries are snapshots, not live objects.");
        if (ImGui.Button(worldPicking ? "Cancel picking" : "Pick in world")) { worldPicking = !worldPicking; refreshWorld = true; nextWorldRefresh = TimeSpan.Zero; }
        ImGui.SameLine(); if (ImGui.Button("Refresh nearby")) { refreshWorld = true; nextWorldRefresh = TimeSpan.Zero; }
        ImGui.SameLine(); ImGui.Checkbox("Refresh automatically", ref autoRefreshWorld);
        ImGui.TextWrapped(worldReadStatus);
        ImGui.InputTextWithHint("##world-filter", "Filter model / VFX paths", ref worldFilter, 256);
        if (worldPicking) ImGui.TextWrapped("Click a circle in the world to select it. Escape or right-click cancels. Overlapping markers select the closest origin to your character.");
        if (!ImGui.BeginTable("world-picker-layout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Objects", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn();
        ImGui.BeginChild("world-list", new(0, Math.Max(240, ImGui.GetContentRegionAvail().Y - 20)), false);
        if (ImGui.BeginTabBar("world-lists"))
        {
            if (ImGui.BeginTabItem("Nearby")) { DrawPickList(nearbyObjects); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Recent")) { DrawPickList(pickHistory.Items.ToArray()); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        ImGui.EndChild(); ImGui.TableNextColumn();
        ImGui.BeginChild("world-details", new(0, Math.Max(240, ImGui.GetContentRegionAvail().Y - 20)), false);
        if (pickedObject is { } item)
        {
            ImGui.TextWrapped(item.ResourcePath);
            ImGui.TextWrapped($"{item.Kind} | Position {item.Position} | Scale {item.Scale}");
            if (ImGui.Button("Copy resource path")) ImGui.SetClipboardText(item.ResourcePath);
            ImGui.TextWrapped("Picking records resource paths and transforms. It does not capture another plugin's mod files. For modded appearances, import and bind the source mod through Mods.");
            if (pickedAsset is { } asset)
            {
                if (asset.Kind == AssetKind.Vfx) DrawVfxPreview(asset);
                else previews.Draw(asset, new(Math.Max(120, ImGui.GetContentRegionAvail().X), 250), true);
                if (ImGui.Button("Add copy at original placement")) Run(() =>
                {
                    var copy = item.CopyAtPlacement(scene);
                    Edit(() => { scene.Objects.Add(copy); selected = copy.Id; }); status = "Copied the resource and placement into the scene.";
                });
                if (ImGui.Button("Place copy at my character")) Run(() => AddSceneAsset(asset, true));
                if (ImGui.Button("Save as favorite")) Run(() =>
                {
                    if (!assets.Any(a => a.AssetPath == asset.AssetPath)) { assets.Add(item.CreateAsset()); SaveLibrary(); lastQuery = null; }
                    status = "Asset saved to favorites.";
                });
            }
            else ImGui.TextWrapped("This mod-specific resource path cannot be copied reliably by itself. Use Mods to import its model/effect and dependencies.");
        }
        else ImGui.TextWrapped("Choose an object from Nearby, Recent, or a world marker.");
        ImGui.EndChild(); ImGui.EndTable();
    }
    private void DrawPickList(IEnumerable<WorldPick> items)
    {
        foreach (var item in FilterPicks(items))
        {
            float distance = objectTable.LocalPlayer is { } player ? Vector3.Distance(player.Position, item.Position) : 0;
            if (ImGui.Selectable($"{distance:F1} m | {Path.GetFileName(item.ResourcePath)} [{item.Kind}]##{item.Key}", pickedObject?.Key == item.Key)) SelectWorldObject(item);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.ResourcePath);
        }
    }
    private void DrawWorldMarkers()
    {
        if (!worldPicking) return;
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) { worldPicking = false; return; }
        bool overUi = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var draw = ImGui.GetForegroundDrawList(); var mouse = ImGui.GetMousePos(); WorldPick? hover = null;
        foreach (var item in FilterPicks(nearbyObjects).Take(500))
        {
            if (!gameGui.WorldToScreen(item.Position, out var screen)) continue;
            draw.AddCircle(screen, 7, item.Kind == AssetKind.Vfx ? 0xFFFFBBFFu : 0xFF99FF88u, 12, 2);
            if (!overUi && hover == null && Vector2.DistanceSquared(mouse, screen) < 144) hover = item;
        }
        if (!overUi) ImGui.SetNextFrameWantCaptureMouse(true);
        if (hover != null)
        {
            ImGui.SetTooltip(hover.ResourcePath);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) { SelectWorldObject(hover); worldPicking = false; }
        }
    }
}
