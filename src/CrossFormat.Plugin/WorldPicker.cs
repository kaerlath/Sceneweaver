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
    private bool effectsNearby, highlightWorldSelection = true;
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
    private IEnumerable<WorldPick> FilterPicks(IEnumerable<WorldPick> source) => WorldPickView.Filter(source, worldFilter, effectsNearby);
    private void DrawWorldPicker()
    {
        worldPickerDrawn = true;
        ImGui.TextWrapped("Pick a loaded model or VFX. World markers indicate object origins; they are not exact mesh hit tests and may be visible through walls. Recent entries are snapshots, not live objects.");
        if (ImGui.Button(worldPicking ? "Cancel picking" : "Pick in world")) { worldPicking = !worldPicking; refreshWorld = true; nextWorldRefresh = TimeSpan.Zero; }
        ImGui.SameLine(); if (ImGui.Button("Refresh nearby")) { refreshWorld = true; nextWorldRefresh = TimeSpan.Zero; }
        ImGui.SameLine(); ImGui.Checkbox("Refresh automatically", ref autoRefreshWorld);
        ImGui.Checkbox("Highlight selected origin", ref highlightWorldSelection);
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
            if (ImGui.BeginTabItem("Nearby")) { effectsNearby = false; DrawPickList(nearbyObjects); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Effects nearby"))
            {
                effectsNearby = true;
                ImGui.TextWrapped("Loaded VFX on your client, including shared scenes. Names come from resource filenames; ownership and scene names are unknown.");
                DrawPickList(nearbyObjects); ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Recent")) { effectsNearby = false; DrawPickList(pickHistory.Items.ToArray()); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        ImGui.EndChild(); ImGui.TableNextColumn();
        ImGui.BeginChild("world-details", new(0, Math.Max(240, ImGui.GetContentRegionAvail().Y - 20)), false);
        if (pickedObject is { } item)
        {
            ImGui.TextWrapped(item.ResourcePath);
            ImGui.TextWrapped($"{item.Kind} | Position {item.Position} | Scale {item.Scale}");
            var current = WorldPickView.Current(nearbyObjects, item);
            if (current == null) ImGui.TextWrapped("Not found in the latest scan. This snapshot is not highlighted; refresh to check again.");
            else if (highlightWorldSelection)
                ImGui.TextWrapped(gameGui.WorldToScreen(current.Position, out _) ? "The selected origin is marked in gold. This marks its source position, not the full effect's shape." : "The selected origin is off-screen. Turn the camera to locate it.");
            if (!autoRefreshWorld) ImGui.TextDisabled("Highlight uses the last scan. Refresh to update it.");
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
        var matches = FilterPicks(items).ToArray();
        ImGui.TextDisabled($"{matches.Length} matching {(effectsNearby ? "effects" : "objects")}");
        if (matches.Length == 0) ImGui.TextWrapped("No matches in the current scan. Try Refresh nearby or clear the filter. Only loaded objects within the scan limits can appear.");
        foreach (var item in matches)
        {
            float distance = objectTable.LocalPlayer is { } player ? Vector3.Distance(player.Position, item.Position) : 0;
            if (ImGui.Selectable($"{distance:F1} m | {Path.GetFileName(item.ResourcePath)} [{item.Kind}]##{item.Key}", pickedObject?.Key == item.Key)) SelectWorldObject(item);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.ResourcePath);
        }
    }
    private void DrawWorldMarkers()
    {
        DrawWorldSelectionHighlight();
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

    private void DrawWorldSelectionHighlight()
    {
        if (!open || !worldPickerDrawn || !highlightWorldSelection || !clientState.IsLoggedIn || objectTable.LocalPlayer == null
            || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]
            || condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return;
        var item = WorldPickView.Current(nearbyObjects, pickedObject);
        if (item == null || (effectsNearby && item.Kind != AssetKind.Vfx) || !gameGui.WorldToScreen(item.Position, out var screen)) return;
        var draw = ImGui.GetForegroundDrawList();
        const uint gold = 0xFF55DDFF;
        float radius = 16 + 3 * MathF.Sin((float)liveClock.Elapsed.TotalSeconds * 4);
        draw.AddCircle(screen, radius, gold, 32, 3);
        draw.AddLine(screen - new Vector2(10, 0), screen + new Vector2(10, 0), gold, 2);
        draw.AddLine(screen - new Vector2(0, 10), screen + new Vector2(0, 10), gold, 2);
        var name = Path.GetFileName(item.ResourcePath);
        if (name.Length > 72) name = name[..69] + "...";
        string label = $"Selected: {name} | {Vector3.Distance(objectTable.LocalPlayer.Position, item.Position):F1} m";
        var pos = screen + new Vector2(22, -12);
        var size = ImGui.CalcTextSize(label);
        var viewport = ImGui.GetMainViewport();
        var min = viewport.Pos + new Vector2(4);
        var max = Vector2.Max(min, viewport.Pos + viewport.Size - size - new Vector2(4));
        pos = Vector2.Clamp(pos, min, max);
        draw.AddRectFilled(pos - new Vector2(4), pos + size + new Vector2(4), 0xDD17120D, 4);
        draw.AddText(pos, gold, label);
    }
}
