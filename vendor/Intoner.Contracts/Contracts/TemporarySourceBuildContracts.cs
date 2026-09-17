using MessagePack;
using System.Text.Json;

namespace Intoner.Objects.Api;

/// <summary> temporary source build result status </summary>
public enum TemporarySourceBuildStatus
{
    /// <summary> a complete source payload was produced </summary>
    Success = 0,
    /// <summary> a complete source payload was produced with nonfatal diagnostics </summary>
    CompletedWithWarnings = 1,
    /// <summary> the request could not produce a source payload </summary>
    InvalidRequest = 2,
    /// <summary> required resources could not be resolved and no source payload was produced </summary>
    ResolutionFailed = 3,
}

/// <summary> temporary source build diagnostic severity </summary>
public enum TemporarySourceBuildDiagnosticSeverity
{
    /// <summary> informational build detail </summary>
    Info = 1,
    /// <summary> nonfatal build issue </summary>
    Warning = 2,
    /// <summary> build issue that prevented a complete payload </summary>
    Error = 3,
}

/// <summary> request to build temporary source and collection payloads from local objects </summary>
/// <param name="SourceId"> caller local source id used by the consumer </param>
/// <param name="SessionId"> nonempty source session id used by the consumer </param>
/// <param name="Name"> temporary source display name </param>
/// <param name="Revision"> strictly increasing positive source revision </param>
/// <param name="Objects"> local objects to include in the temporary source payload </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceBuildRequest(
    string SourceId,
    Guid SessionId,
    string Name,
    long Revision,
    IReadOnlyList<WorldObject> Objects);

/// <summary> one diagnostic from temporary source payload construction </summary>
/// <param name="ObjectId"> object id associated with the diagnostic, or empty when global </param>
/// <param name="CollectionId"> authored collection id associated with the diagnostic </param>
/// <param name="Severity"> diagnostic severity </param>
/// <param name="Message"> diagnostic text </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceBuildDiagnostic(
    Guid ObjectId,
    string CollectionId,
    TemporarySourceBuildDiagnosticSeverity Severity,
    string Message);

/// <summary> temporary source and collection payloads built from local objects </summary>
/// <param name="Status"> build status </param>
/// <param name="Message"> summary message </param>
/// <param name="Source"> complete temporary source payload with generated collection ids, or null when the build failed </param>
/// <param name="LocalFilePaths"> local files referenced by the generated collection redirects </param>
/// <param name="Diagnostics"> build diagnostics </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceBuildResult(
    TemporarySourceBuildStatus Status,
    string Message,
    TemporarySourceApplyRequest? Source,
    IReadOnlyList<string> LocalFilePaths,
    IReadOnlyList<TemporarySourceBuildDiagnostic> Diagnostics)
{
    /// <summary> whether a complete source payload was produced </summary>
    [IgnoreMember]
    public bool IsSuccess
        => Status is TemporarySourceBuildStatus.Success or TemporarySourceBuildStatus.CompletedWithWarnings;
}

internal static class TemporarySourceBuildResultWire
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IgnoreReadOnlyProperties = true,
    };

    public static byte[] Serialize(TemporarySourceBuildResult result)
        => JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);

    public static TemporarySourceBuildResult Deserialize(byte[] payload)
        => JsonSerializer.Deserialize<TemporarySourceBuildResult>(payload, JsonOptions)
            ?? throw new InvalidDataException("Intoner returned an empty temporary source build result");
}
