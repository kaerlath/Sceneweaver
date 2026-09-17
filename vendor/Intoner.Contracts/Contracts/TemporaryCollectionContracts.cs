using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> temporary object collection replacement kind </summary>
public enum TemporaryCollectionReplacementKind
{
    /// <summary> replacement content loaded from a game path </summary>
    GamePath = 1,
    /// <summary> replacement content loaded from a local file </summary>
    LocalFile = 2,
    /// <summary> replacement content supplied as in-memory bytes </summary>
    Memory = 3,
}

/// <summary> one temporary object collection replacement </summary>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporaryCollectionReplacement
{
    /// <summary> replacement kind </summary>
    public TemporaryCollectionReplacementKind Kind { get; init; }

    /// <summary> replacement game path, local file path, or memory resource game path </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary> memory resource bytes for memory replacements </summary>
    public byte[] Data { get; init; } = [];
}

/// <summary> one temporary object collection redirect </summary>
/// <param name="RequestedPath"> requested game path to replace </param>
/// <param name="Replacement"> replacement path or resource </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporaryObjectCollectionRedirect(
    string RequestedPath,
    TemporaryCollectionReplacement Replacement);

/// <summary> one temporary object collection payload </summary>
/// <param name="CollectionId"> source local collection id referenced by temporary objects </param>
/// <param name="Name"> display name </param>
/// <param name="Redirects"> redirect set </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporaryObjectCollection(
    string CollectionId,
    string Name,
    IReadOnlyList<TemporaryObjectCollectionRedirect> Redirects);
