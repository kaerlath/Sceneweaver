using System.Text.Json;

namespace CrossFormat.Core;

public sealed record SaveLocations(string Stagehand, string StagehandAutosave, string Intoner, string Projects)
{
    public static SaveLocations Discover(string pluginConfigDirectory, string documentsDirectory)
    {
        var configRoot = Directory.GetParent(Path.GetFullPath(pluginConfigDirectory))!.FullName;
        string stagehand = Path.Combine(documentsDirectory, "Stages");
        string autosave = Path.Combine(configRoot, "Stagehand", "autosave");
        try
        {
            string configuration = Path.Combine(configRoot, "Stagehand.json");
            if (File.Exists(configuration))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(configuration));
                if (json.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a configuration object.");
                if (json.RootElement.TryGetProperty("DefinitionLibraryPath", out var library) && library.ValueKind == JsonValueKind.String && Path.IsPathFullyQualified(library.GetString() ?? "")) stagehand = library.GetString()!;
                if (json.RootElement.TryGetProperty("AutosavePath", out var saved) && saved.ValueKind == JsonValueKind.String && Path.IsPathFullyQualified(saved.GetString() ?? "")) autosave = saved.GetString()!;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { /* Fall back to the plugins' documented defaults. */ }
        return new(stagehand, autosave, Path.Combine(configRoot, "Intoner", "objects", "layouts"), Path.Combine(pluginConfigDirectory, "projects"));
    }

    public static string ExistingDirectory(params string[] candidates)
    {
        foreach (var candidate in candidates) if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate)) return Path.GetFullPath(candidate);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var current = Path.GetFullPath(candidate);
            while (Path.GetDirectoryName(current) is string parent && parent != current)
            { if (Directory.Exists(parent)) return parent; current = parent; }
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
