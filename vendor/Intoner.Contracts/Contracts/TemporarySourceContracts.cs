using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> complete desired state for one temporary source </summary>
/// <param name="SourceId"> caller local source id </param>
/// <param name="SessionId"> nonempty source session id </param>
/// <param name="Name"> source display name </param>
/// <param name="Revision"> strictly increasing positive source revision </param>
/// <param name="Objects"> complete object set </param>
/// <param name="Collections"> complete collection set referenced by the objects </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceApplyRequest(
    string SourceId,
    Guid SessionId,
    string Name,
    long Revision,
    IReadOnlyList<WorldObject> Objects,
    IReadOnlyList<TemporaryObjectCollection> Collections);

/// <summary> one temporary object change </summary>
/// <param name="Kind"> change kind </param>
/// <param name="Object"> object payload for upsert changes </param>
/// <param name="ObjectId"> source object id for remove and patch changes </param>
/// <param name="Patch"> partial update payload for patch changes </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporaryObjectChange(
    TemporaryObjectChangeKind Kind,
    WorldObject? Object = null,
    Guid ObjectId = default,
    WorldObjectPatch? Patch = null);

/// <summary> ordered temporary object changes for one source </summary>
/// <param name="SourceId"> caller local source id </param>
/// <param name="SessionId"> nonempty source session id </param>
/// <param name="Name"> source display name, or empty to keep the current name </param>
/// <param name="Revision"> strictly increasing positive source revision </param>
/// <param name="Changes"> ordered object changes committed as one source update </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporaryObjectChangeSet(
    string SourceId,
    Guid SessionId,
    string Name,
    long Revision,
    IReadOnlyList<TemporaryObjectChange> Changes);

/// <summary> full temporary source removal </summary>
/// <param name="SourceId"> caller local source id </param>
/// <param name="SessionId"> current source session id </param>
/// <param name="Revision"> strictly increasing positive source revision </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceRemoveRequest(
    string SourceId,
    Guid SessionId,
    long Revision);

/// <summary> caller owned temporary source currently loaded by intoner </summary>
/// <param name="SourceId"> caller local source id </param>
/// <param name="SessionId"> current source session id </param>
/// <param name="Name"> source display name </param>
/// <param name="Revision"> current source revision </param>
/// <param name="UpdatedAtUtc"> last accepted update time </param>
/// <param name="Objects"> complete source object set using caller local object and collection ids </param>
/// <param name="Collections"> complete source collection set using caller local collection ids </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceInfo(
    string SourceId,
    Guid SessionId,
    string Name,
    long Revision,
    DateTime UpdatedAtUtc,
    IReadOnlyList<WorldObject> Objects,
    IReadOnlyList<TemporaryObjectCollection> Collections);

/// <summary> result of a temporary source mutation </summary>
/// <param name="Status"> typed mutation status </param>
/// <param name="SourceRevision"> authoritative source revision after the request </param>
/// <param name="IsAccepted"> whether the requested source revision was committed </param>
/// <param name="SceneRevision"> composed scene revision after an accepted change </param>
/// <param name="Message"> short diagnostic intended for logs </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record TemporarySourceMutationResult(
    TemporarySourceMutationStatus Status,
    long SourceRevision,
    bool IsAccepted,
    long SceneRevision = 0,
    string Message = "")
{
    /// <summary> whether the operation completed successfully or was already applied </summary>
    [IgnoreMember]
    public bool IsSuccess
        => Status is TemporarySourceMutationStatus.Success or TemporarySourceMutationStatus.AlreadyApplied;
}

