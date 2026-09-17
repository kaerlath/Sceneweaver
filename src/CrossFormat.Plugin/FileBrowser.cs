using System.Text.Json;
using CrossFormat.Core;
using Dalamud.Bindings.ImGui;

namespace CrossFormat.Plugin;

public sealed partial class Plugin
{
    private SaveLocations locations = null!;
    private Task<string?>? filePicker;
    private Action<string>? pickedFile;
    private Dictionary<string, string> recentFolders = [];
    private string preferencesPath = "";
    private string activePickerKind = "";

    private void InitializeLocations(string configurationDirectory)
    {
        locations = SaveLocations.Discover(configurationDirectory, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        preferencesPath = Path.Combine(configurationDirectory, "browser-folders.json");
        try { if (File.Exists(preferencesPath)) recentFolders = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(preferencesPath)) ?? []; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { status = "Browser locations reset: " + e.Message; }
        projectPath = Path.Combine(locations.Projects, "scene.cross.json");
        intonerPath = Path.Combine(locations.Intoner, "scene.intoner.json");
        stagehandPath = Path.Combine(locations.Stagehand, "scene.stagehand.json");
    }

    private void Pick(string kind, bool save, string currentPath, Action<string> accept, bool image = false)
    {
        if (filePicker != null) return;
        var home = kind switch { "Stagehand" => locations.Stagehand, "Intoner" => locations.Intoner, "Stagehand autosave" => locations.StagehandAutosave, _ => locations.Projects };
        var initial = SaveLocations.ExistingDirectory(recentFolders.GetValueOrDefault(kind) ?? "", save ? Path.GetDirectoryName(currentPath) ?? "" : home, home,
            kind == "Stagehand" ? locations.StagehandAutosave : "", pi.GetPluginConfigDirectory());
        pickedFile = accept; activePickerKind = kind;
        filePicker = NativeFilePicker.Show(save ? $"Save {kind} file" : $"Open {kind} file", initial, save, save ? Path.GetFileName(currentPath) : "", image);
    }

    private void CompletePicker()
    {
        if (filePicker is not { IsCompleted: true }) return;
        var task = filePicker; var accept = pickedFile; filePicker = null; pickedFile = null;
        Run(() =>
        {
            string? path = task.GetAwaiter().GetResult();
            if (path == null) return;
            recentFolders[activePickerKind] = Path.GetDirectoryName(path)!;
            // A preference write failure must not lose the user's selection.
            try { File.WriteAllText(preferencesPath, JsonSerializer.Serialize(recentFolders)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            accept?.Invoke(path);
        });
    }

    private void ImportFile(string path)
    {
        var imported = SceneCodec.Import(ProjectFiles.Read(path));
        var hash = ProjectFiles.Hash(path);
        GuardReplace(() =>
        {
            ReplaceScene(imported); inputPath = path; knownHashes[Path.GetFullPath(path)] = hash;
            if (Path.GetFileName(path).EndsWith(".cross.json", StringComparison.OrdinalIgnoreCase)) projectPath = path;
            status = $"Opened {Path.GetFileName(path)} — {scene.Objects.Count} objects.";
        });
    }

    private void DrawImportButtons()
    {
        ImGui.BeginDisabled(filePicker != null);
        if (ImGui.Button("Open Stagehand...")) Pick("Stagehand", false, "", ImportFile);
        ImGui.SameLine(); if (ImGui.Button("Open Intoner...")) Pick("Intoner", false, "", ImportFile);
        ImGui.SameLine(); if (ImGui.Button("Open project...")) Pick("Sceneweaver", false, "", ImportFile);
        ImGui.EndDisabled();
        if (filePicker != null) { ImGui.SameLine(); ImGui.TextDisabled("Windows file picker is open..."); }
    }

    private void Destination(string label, string current, Action<string> set)
    {
        ImGui.PushID(label);
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - 110));
        string path = current;
        if (ImGui.InputText("##path", ref path, 4096)) { set(path); pending = null; }
        ImGui.SameLine(); ImGui.BeginDisabled(filePicker != null);
        if (ImGui.Button("Browse...", new(96, 0))) Pick(label, true, current, value => { set(value); pending = null; });
        ImGui.EndDisabled(); ImGui.PopID();
    }
}
