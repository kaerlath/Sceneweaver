using System.Text.Json;

namespace CrossFormat.Core;

public sealed record GameAsset(string Path, AssetKind Kind)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public SceneObject CreateObject() => new() { Name = Name, AssetPath = Path, Kind = Kind };
}

public sealed class GameAssetCatalog
{
    public IReadOnlyList<GameAsset> Assets { get; }
    private readonly Dictionary<string, string[]> childFolders;
    private GameAssetCatalog(List<GameAsset> assets, Dictionary<string, string[]> children)
    { Assets = assets; childFolders = children; }

    public static GameAssetCatalog LoadBundled()
    {
        using var stream = typeof(GameAssetCatalog).Assembly.GetManifestResourceStream("Sceneweaver.GamePaths.json")
            ?? throw new InvalidDataException("Bundled game catalog is missing.");
        return Read(stream);
    }

    public static GameAssetCatalog Read(Stream stream)
    {
        using var document = JsonDocument.Parse(stream);
        List<GameAsset> assets = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (property, kind, extension) in new[] { ("MdlPaths", AssetKind.BgObject, ".mdl"), ("AvfxPaths", AssetKind.Vfx, ".avfx"), ("ScdPaths", AssetKind.Sound, ".scd") })
        {
            if (!document.RootElement.TryGetProperty(property, out var paths)) continue;
            foreach (var item in paths.EnumerateArray())
            {
                var path = item.GetString()?.Replace('\\', '/').Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(path) || path.StartsWith('/') || path.Contains(':') || path.Split('/').Contains("..") || !path.EndsWith(extension, StringComparison.Ordinal) || !seen.Add(path)) continue;
                assets.Add(new(path, kind));
                var parts = path.Split('/');
                string parent = "";
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string folder = parent.Length == 0 ? parts[i] : parent + "/" + parts[i];
                    if (!folders.TryGetValue(parent, out var children)) folders[parent] = children = new(StringComparer.OrdinalIgnoreCase);
                    children.Add(folder); parent = folder;
                }
            }
        }
        assets.Sort((a,b) => StringComparer.Ordinal.Compare(a.Path,b.Path));
        return new(assets, folders.ToDictionary(p => p.Key, p => p.Value.Order(StringComparer.Ordinal).ToArray(), StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> Children(string folder) => childFolders.TryGetValue(folder, out var children) ? children : [];
    public GameAsset[] Search(string query, string folder = "", AssetKind? kind = null)
    {
        var words = query.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var prefix = folder.TrimEnd('/') + "/";
        return Assets.Where(a => (!kind.HasValue || a.Kind == kind)
            && (folder.Length == 0 || a.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            && words.All(w => a.Path.Contains(w, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
