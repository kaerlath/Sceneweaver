using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stagehand.Definitions;
using Stagehand.Definitions.ModResources;

namespace CrossFormat.Core;

public sealed record ModOptionGroup(string Name, bool Multiple, string[] Options, int[] Defaults, JsonObject Raw);
public sealed record ModImportResult(JsonObject Pack, int Resources, int Placeable);

/// <summary>Reads Penumbra's file replacement data without installing or activating the mod.</summary>
public sealed class ModImport
{
    private readonly string source;
    private readonly JsonObject metadata, defaults;
    public string Name => metadata["Name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(source);
    public List<ModOptionGroup> Groups { get; } = [];
    private ModImport(string source, JsonObject metadata, JsonObject defaults) { this.source = source; this.metadata = metadata; this.defaults = defaults; }
    public const int MaxResourceBytes = 64 * 1024 * 1024;

    public static ModImport Open(string path)
    {
        path = Path.GetFullPath(path);
        using var files = new SourceFiles(path);
        int metadataBudget = 16 * 1024 * 1024;
        JsonObject Read(string name)
        {
            var bytes = files.Read(name, Math.Min(metadataBudget, 4 * 1024 * 1024)); metadataBudget -= bytes.Length;
            return JsonNode.Parse(bytes)?.AsObject() ?? throw new InvalidDataException("Empty mod metadata.");
        }
        if (!files.Names.Contains("meta.json", StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a Penumbra .pmp package or the meta.json in an installed mod folder.");
        var result = new ModImport(path, Read("meta.json"), files.Names.Contains("default_mod.json", StringComparer.OrdinalIgnoreCase) ? Read("default_mod.json") : new());
        foreach (var name in files.Names.Where(n => n.StartsWith("group_", StringComparison.OrdinalIgnoreCase) && !n.Contains('/') && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase))
        {
            var raw = Read(name); string type = raw["Type"]?.GetValue<string>() ?? "";
            if (type is not ("Single" or "Multi")) throw new InvalidDataException($"Option group {name} uses {type}; only Single and Multi groups are supported. Export a simple Penumbra package first.");
            var options = raw["Options"]?.AsArray() ?? throw new InvalidDataException($"Missing options in {name}.");
            if (options.Count > 64) throw new InvalidDataException("Mod groups with more than 64 options are not supported.");
            ulong settings = raw["DefaultSettings"]?.GetValue<ulong>() ?? 0;
            if (type == "Single" && options.Count > 0 && settings >= (ulong)options.Count) throw new InvalidDataException("Mod group contains an invalid default option.");
            int[] selected = type == "Single" ? (options.Count == 0 ? [] : [(int)Math.Min(settings, (ulong)options.Count - 1)]) : Enumerable.Range(0, options.Count).Where(i => (settings & (1UL << i)) != 0).ToArray();
            result.Groups.Add(new(raw["Name"]?.GetValue<string>() ?? name, type == "Multi", options.Select(o => o?["Name"]?.GetValue<string>() ?? "Unnamed option").ToArray(), selected, raw));
        }
        // Penumbra group priority determines which group wins overlapping replacements.
        var ordered = result.Groups.OrderBy(g => g.Raw["Priority"]?.GetValue<int>() ?? 0).ToArray();
        result.Groups.Clear(); result.Groups.AddRange(ordered);
        return result;
    }

    public ModImportResult Build(IReadOnlyList<int[]> selections)
    {
        if (selections.Count != Groups.Count) throw new InvalidDataException("Choose settings for every group.");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var swaps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Apply(JsonObject data)
        {
            if (data["Manipulations"] is { } manipulations && manipulations is not JsonArray { Count: 0 }) throw new InvalidDataException("The selected options contain metadata manipulations. Stagehand resource packs cannot represent them; choose another option or export a files-only mod.");
            if (data["Files"] is JsonObject replacements) foreach (var f in replacements) { var key = ModResources.GamePath(f.Key); files[key] = f.Value!.GetValue<string>(); swaps.Remove(key); }
            if (data["FileSwaps"] is JsonObject replacements2) foreach (var f in replacements2) { var key = ModResources.GamePath(f.Key); swaps[key] = ModResources.GamePath(f.Value!.GetValue<string>()); files.Remove(key); }
        }
        Apply(defaults);
        for (int g = 0; g < Groups.Count; g++)
        {
            var group = Groups[g]; var options = group.Raw["Options"]!.AsArray();
            if ((!group.Multiple && selections[g].Length != 1 && options.Count > 0) || selections[g].Distinct().Count() != selections[g].Length || selections[g].Any(i => i < 0 || i >= options.Count)) throw new InvalidDataException("Invalid mod option selection.");
            foreach (int i in selections[g].OrderBy(i => options[i]?["Priority"]?.GetValue<int>() ?? i)) Apply(options[i]!.AsObject());
        }
        if (files.Count + swaps.Count > 8192) throw new InvalidDataException("Mod exceeds the 8192-resource limit.");
        if (files.Count + swaps.Count == 0) throw new InvalidDataException("The selected options contain no file replacements or game-file swaps.");
        var pack = new EmbeddedModpackDefinition { DisplayName = Name, PenumbraSourceModDirectory = source.EndsWith(".pmp", StringComparison.OrdinalIgnoreCase) ? "" : new DirectoryInfo(Path.GetDirectoryName(source)!).Name, PenumbraSourceModVersion = metadata["Version"]?.GetValue<string>() ?? "" };
        using var sourceFiles = new SourceFiles(source);
        long total = 0;
        foreach (var f in files)
        {
            var bytes = sourceFiles.Read(f.Value, MaxResourceBytes - (int)total); total += bytes.Length;
            pack.ModdedResources[f.Key] = new EmbeddedModResourceDefinition { CompressionScheme = ModCompressionScheme.Zlib, CompressedDataBytes = EmbeddedModResourceDefinition.CompressDataBytes(bytes, ModCompressionScheme.Zlib) };
        }
        foreach (var f in swaps) pack.ModdedResources[f.Key] = new GameModResourceDefinition { SourceGamePath = f.Value };
        var rawPack = JsonSerializer.SerializeToNode(pack, StageDefinition.StandardSerializerOptions)!.AsObject();
        // Source option definitions are kept canonically for inspection, not executed or installed.
        rawPack["SceneweaverImport"] = new JsonObject { ["Metadata"] = metadata.DeepClone(), ["Defaults"] = defaults.DeepClone(), ["Groups"] = new JsonArray(Groups.Select(g => (JsonNode)g.Raw.DeepClone()).ToArray()), ["Selections"] = JsonSerializer.SerializeToNode(selections) };
        return new(rawPack, pack.ModdedResources.Count, pack.ModdedResources.Keys.Count(p => ModResources.Kind(p) != null));
    }

    private sealed class SourceFiles : IDisposable
    {
        private readonly ZipArchive? archive;
        private readonly string root;
        private readonly Dictionary<string, ZipArchiveEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        public string[] Names { get; }
        public SourceFiles(string source)
        {
            root = Path.GetDirectoryName(source)!;
            if (source.EndsWith(".pmp", StringComparison.OrdinalIgnoreCase))
            {
                archive = ZipFile.OpenRead(source);
                if (archive.Entries.Count > 32768) { archive.Dispose(); throw new InvalidDataException("Mod archive contains too many files."); }
                try { foreach (var entry in archive.Entries) if (!entry.FullName.EndsWith('/')) { var name = Relative(entry.FullName); if (!entries.TryAdd(name, entry)) throw new InvalidDataException("Duplicate paths in mod archive."); } }
                catch { archive.Dispose(); throw; }
                Names = entries.Keys.ToArray();
            }
            else Names = Directory.GetFiles(root, "*.json").Select(Path.GetFileName).OfType<string>().ToArray();
        }
        private static string Relative(string name)
        {
            name = name.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(p => p is ".." or "." or "")) throw new InvalidDataException("Mod contains an unsafe relative file path.");
            return name;
        }
        public byte[] Read(string name, int limit)
        {
            name = Relative(name);
            if (limit <= 0) throw new InvalidDataException("Selected mod files exceed the 64 MB import budget.");
            if (archive != null)
            {
                if (!entries.TryGetValue(name, out var entry)) throw new FileNotFoundException("Mod file is missing: " + name);
                if (entry.Length > limit) throw new InvalidDataException("Mod file exceeds the import budget: " + name);
                using var stream = entry.Open(); return ModResources.ReadBounded(stream, limit);
            }
            string full = Path.GetFullPath(Path.Combine(root, name));
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("File escapes the mod directory.");
            for (string? check = full; check != null && check.Length >= root.Length; check = Path.GetDirectoryName(check))
                if ((File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked mod files must be copied into the mod directory before importing.");
            using var input = File.OpenRead(full); return ModResources.ReadBounded(input, limit);
        }
        public void Dispose() => archive?.Dispose();
    }
}
