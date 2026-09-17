using System.Numerics;
using System.Text.Json;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private readonly Task<GameAssetCatalog> catalogTask = Task.Run(GameAssetCatalog.LoadBundled);
    private GameAssetCatalog? catalog;
    private GameAsset[] results = [];
    private string catalogFolder = "";
    private string? lastQuery;
    private int assetType, assetSource;
    private SceneObject? previewAsset;
    private const int ResultsPerPage = 100;
    private string catalogError = "";

    private void DrawAssets()
    {
        if (catalog == null && catalogTask.IsCompleted && catalogError.Length == 0)
        {
            try { catalog = catalogTask.GetAwaiter().GetResult(); }
            catch (Exception e) { catalogError = e.Message; }
        }
        ImGui.TextColored(ImGui.ColorConvertFloat4ToU32(new(.4f, .85f, .95f, 1)), "DISCOVER GAME ASSETS");
        ImGui.SameLine(); ImGui.TextDisabled(catalog == null ? "Loading catalog..." : $"{catalog.Assets.Count:N0} indexed resources");
        ImGui.TextDisabled("Select to inspect. Add to your scene only when you choose.");
        ImGui.SetNextItemWidth(180);
        if (ImGui.Combo("##source", ref assetSource, "Game catalog\0Saved favorites\0")) lastQuery = null;
        ImGui.SameLine(); ImGui.SetNextItemWidth(140);
        if (ImGui.Combo("##assettype", ref assetType, "Models\0Visual effects\0Sounds\0All types\0")) lastQuery = null;
        ImGui.SameLine(); ImGui.SetNextItemWidth(Math.Max(140, ImGui.GetContentRegionAvail().X));
        if (ImGui.InputTextWithHint("##assetsearch", "Search name or path — e.g. gyr tree, bgparts, furniture", ref filter, 256)) lastQuery = null;
        if (catalogError.Length > 0) { ImGui.TextWrapped("Catalog could not load: " + catalogError); return; }
        if (catalog == null) return;
        RefreshResults();
        var height = Math.Max(260, ImGui.GetContentRegionAvail().Y - 45);
        if (!ImGui.BeginTable("asset-workspace", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Folders", ImGuiTableColumnFlags.WidthFixed, 210);
        ImGui.TableSetupColumn("Assets", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthFixed, 380);
        ImGui.TableNextColumn(); ImGui.BeginChild("catalog-folders", new(0, height), false);
        ImGui.TextDisabled("GAME FOLDERS"); ImGui.Separator();
        ImGui.BeginDisabled(assetSource != 0);
        if (ImGui.Selectable("All game assets", catalogFolder.Length == 0)) SetFolder("");
        if (catalogFolder.Length > 0)
        {
            if (ImGui.Button("< Parent folder", new(-1, 0))) SetFolder(catalogFolder.Contains('/') ? catalogFolder[..catalogFolder.LastIndexOf('/')] : "");
            ImGui.TextWrapped(catalogFolder);
        }
        foreach (var folder in catalog.Children(catalogFolder))
        {
            var name = folder[(folder.LastIndexOf('/') + 1)..];
            var label = folder switch { "bg" => "Environment / zones", "bgcommon" => "Shared props / housing", "chara" => "Characters / equipment", "vfx" => "Visual effects", "sound" => "Sounds", _ => name };
            if (ImGui.Selectable(label + " >##" + folder)) { SetFolder(folder); break; }
        }
        ImGui.EndDisabled();
        ImGui.Separator(); ImGui.TextWrapped("These are indexed game paths, independent of the objects in your room. Some catalog entries may be absent in your installed game version.");
        ImGui.EndChild();

        ImGui.TableNextColumn(); ImGui.BeginChild("catalog-results", new(0, height), false);
        ImGui.TextDisabled($"{results.Length:N0} results");
        if (ImGui.Button("<##prev") && assetPage > 0) assetPage--;
        ImGui.SameLine(); ImGui.TextUnformatted($"{assetPage + 1} / {Math.Max(1, (results.Length + ResultsPerPage - 1) / ResultsPerPage)}");
        ImGui.SameLine(); if (ImGui.Button(">##next") && (assetPage + 1) * ResultsPerPage < results.Length) assetPage++;
        ImGui.Separator();
        ImGui.BeginChild("result-scroll", new(0, 0), false);
        int index = assetPage * ResultsPerPage;
        foreach (var entry in results.Skip(index).Take(ResultsPerPage))
        {
            ImGui.PushID(index++);
            var current = previewAsset?.AssetPath == entry.Path;
            if (ImGui.Selectable(entry.Name, current))
            {
                previewAsset = assetSource == 1 ? assets.FirstOrDefault(o => o.AssetPath == entry.Path) ?? entry.CreateObject() : entry.CreateObject();
                previews.ResetView();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(entry.Path);
            ImGui.TextDisabled(entry.Path.Length > 72 ? "..." + entry.Path[^69..] : entry.Path);
            ImGui.Spacing(); ImGui.PopID();
        }
        if (results.Length == 0) ImGui.TextWrapped("No matching assets. Try a shorter search, another type, or All game assets.");
        ImGui.EndChild(); ImGui.EndChild();

        ImGui.TableNextColumn(); ImGui.BeginChild("catalog-preview", new(0, height), false);
        ImGui.TextDisabled("ISOLATED PREVIEW"); ImGui.Separator();
        if (previewAsset == null)
        {
            ImGui.Dummy(new(0, 60)); ImGui.TextWrapped("Choose an asset from the list to inspect it here.");
            ImGui.TextWrapped("Large models are automatically fitted to this panel. Browsing never places objects in your room.");
        }
        else
        {
            ImGui.TextWrapped(previewAsset.Name);
            var width = Math.Max(100, ImGui.GetContentRegionAvail().X);
            previews.Draw(previewAsset, new(width, Math.Clamp(height * .53f, 220, 480)), true);
            if (previewAsset.Kind == AssetKind.BgObject)
            {
                ImGui.TextDisabled("Drag to orbit  |  Wheel to zoom");
                if (ImGui.Button("Fit model")) previews.ResetView();
                ImGui.SameLine(); previews.DrawViewOptions();
                ImGui.TextWrapped("Base-color textures with preview lighting. Special shaders and animation may look different in game.");
            }
            else ImGui.TextWrapped(previewAsset.Kind == AssetKind.Vfx
                ? "This effect is indexed and can be added, but animated VFX cannot yet be rendered in this panel. No effect is spawned."
                : "Sound resource. Audio preview is not available; no sound is played.");
            ImGui.Spacing();
            ImGui.BeginDisabled(previewAsset.Kind == AssetKind.Furniture && previewAsset.IntonerSource.Count == 0);
            if (ImGui.Button("Add to scene", new(-1, 34)))
            {
                var asset = previewAsset;
                Edit(() => { var copy = JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(asset, ProjectJson.Options), ProjectJson.Options)!; copy.Id = Guid.NewGuid(); copy.StagehandId = ""; copy.Position = Vector3.Zero; scene.Objects.Add(copy); selected = copy.Id; });
                status = $"Added {asset.Name} to the project. Edit it in the Scene tab.";
            }
            ImGui.EndDisabled();
            bool favorite = assets.Any(a => a.AssetPath == previewAsset.AssetPath);
            ImGui.BeginDisabled(favorite);
            if (ImGui.Button(favorite ? "Saved to favorites" : "Save favorite", new(-1, 0))) Run(() =>
            {
                assets.Add(JsonSerializer.Deserialize<SceneObject>(JsonSerializer.Serialize(previewAsset, ProjectJson.Options), ProjectJson.Options)!);
                SaveLibrary(); lastQuery = null;
            });
            ImGui.EndDisabled();
            ImGui.Separator(); ImGui.TextDisabled("GAME PATH"); ImGui.TextWrapped(previewAsset.AssetPath);
            if (ImGui.Button("Copy path")) ImGui.SetClipboardText(previewAsset.AssetPath);
            if (previewAsset.Kind == AssetKind.Sound) ImGui.TextWrapped("Stagehand only: Intoner has no sound object type. Export will report this.");
        }
        ImGui.EndChild(); ImGui.EndTable();
    }

    private void SetFolder(string folder) { catalogFolder = folder; lastQuery = null; RefreshResults(); }
    private void RefreshResults()
    {
        if (catalog == null) return;
        var key = $"{assetSource}|{assetType}|{catalogFolder}|{filter}";
        if (lastQuery == key) return;
        lastQuery = key; assetPage = 0;
        AssetKind? kind = assetType switch { 0 => AssetKind.BgObject, 1 => AssetKind.Vfx, 2 => AssetKind.Sound, _ => null };
        results = assetSource == 0 ? catalog.Search(filter, catalogFolder, kind)
            : assets.Where(a => (!kind.HasValue || a.Kind == kind) && (a.Name + " " + a.AssetPath).Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(a => new GameAsset(a.AssetPath, a.Kind)).DistinctBy(a => a.Path).ToArray();
    }
}
