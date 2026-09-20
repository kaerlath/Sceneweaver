using System.Text.Json;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private Dictionary<string, string> installedMods = [];
    private string installedFilter = "", importDirectory = "", updateModId = "";
    private Dictionary<string, string[]>? restoreModChoices;
    private bool ModBusy => modOpenTask != null || modBuildTask != null || filePicker != null;

    private void BeginModImport(string path, string directory = "", string updateId = "")
    {
        modImport = null; modBuilt = null; importDirectory = directory; updateModId = updateId;
        restoreModChoices = updateId.Length == 0 ? null : ModResources.Packs(scene)[updateId]?["SceneweaverImport"]?["NamedSelections"]?.Deserialize<Dictionary<string, string[]>>();
        if (restoreModChoices == null && updateId.Length > 0 && ModResources.Packs(scene)[updateId]?["SceneweaverImport"] is System.Text.Json.Nodes.JsonObject old
            && old["Groups"] is System.Text.Json.Nodes.JsonArray groups && old["Selections"] is System.Text.Json.Nodes.JsonArray selections)
        {
            restoreModChoices = [];
            for (int i = 0; i < groups.Count; i++)
                restoreModChoices[groups[i]!["Name"]!.GetValue<string>()] = selections[i]!.AsArray().Select(n => groups[i]!["Options"]![n!.GetValue<int>()]!["Name"]!.GetValue<string>()).ToArray();
        }
        modOpenTask = Task.Run(() => ModImport.Open(path));
    }

    private string InstalledModPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || directory.IndexOfAny(['/', '\\', ':']) >= 0 || directory is "." or "..")
            throw new InvalidDataException("Invalid Penumbra mod directory.");
        if (!new GetEnabledState(pi).Invoke()) throw new InvalidOperationException("Enable Penumbra to browse installed mods.");
        return Path.Combine(new GetModDirectory(pi).Invoke(), directory, "meta.json");
    }

    private void DrawInstalledMods()
    {
        ImGui.BeginDisabled(ModBusy);
        if (ImGui.Button("Browse installed Penumbra mods / Refresh")) Run(() =>
        {
            if (!new GetEnabledState(pi).Invoke()) throw new InvalidOperationException("Enable Penumbra to browse installed mods.");
            installedMods = new GetModList(pi).Invoke();
        });
        if (installedMods.Count > 0)
        {
            ImGui.InputTextWithHint("##installed-filter", "Search installed mods", ref installedFilter, 256);
            if (ImGui.BeginCombo("Installed mod", "Select a mod..."))
            {
                foreach (var mod in installedMods.OrderBy(p => p.Value).Where(p => p.Value.Contains(installedFilter, StringComparison.OrdinalIgnoreCase) || p.Key.Contains(installedFilter, StringComparison.OrdinalIgnoreCase)))
                    if (ImGui.Selectable(mod.Value + "##" + mod.Key)) Run(() => BeginModImport(InstalledModPath(mod.Key), mod.Key));
                ImGui.EndCombo();
            }
        }
        ImGui.EndDisabled();
    }

    private void DrawCollectionSettings()
    {
        if (ImGui.Button("Reset options to defaults")) { modSelections = modImport!.Groups.Select(g => g.Defaults.ToArray()).ToList(); modBuilt = null; }
        ImGui.BeginDisabled(importDirectory.Length == 0);
        if (ImGui.Button("Use current Penumbra collection settings")) Run(() =>
        {
            var collection = new GetCollection(pi).Invoke(ApiCollectionType.Current)
                ?? throw new InvalidOperationException("Select a collection in Penumbra first.");
            var result = new GetCurrentModSettingsWithTemp(pi).Invoke(collection.Id, importDirectory, "", false, false, 0);
            if (result.Item1 != PenumbraApiEc.Success || result.Item2 == null)
                throw new InvalidOperationException("Penumbra could not read settings for this mod in the selected collection.");
            var selections = result.Item2.Value.Item3.ToDictionary(p => p.Key, p => p.Value.ToArray());
            modSelections = modImport!.SelectByName(selections); modBuilt = null;
            status = $"Copied settings from {collection.Name}. Review the choices before preparing files.";
        });
        ImGui.EndDisabled();
    }
}
