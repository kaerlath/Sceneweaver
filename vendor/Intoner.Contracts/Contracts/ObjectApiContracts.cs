using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> features exposed by the current intoner API host </summary>
[Flags]
public enum ObjectApiCapabilities : ulong
{
    /// <summary> no optional capabilities </summary>
    None = 0,
    /// <summary> composed and persistent scene queries </summary>
    SceneQueries = 1UL << 0,
    /// <summary> persistent object mutations </summary>
    PersistentObjects = 1UL << 1,
    /// <summary> saved layout queries and mutations </summary>
    Layouts = 1UL << 2,
    /// <summary> caller owned temporary source state </summary>
    TemporarySources = 1UL << 3,
    /// <summary> incremental temporary object changes </summary>
    TemporaryObjectChanges = 1UL << 4,
    /// <summary> temporary source payload construction </summary>
    SourceBuilder = 1UL << 5,
    /// <summary> live runtime state queries </summary>
    RuntimeState = 1UL << 6,
    /// <summary> composed and persistent scene revision notifications </summary>
    RevisionEvents = 1UL << 7,
    /// <summary> revision checked persistent scene replacement </summary>
    PersistentSceneApply = 1UL << 8,
    /// <summary> saved layout change notifications </summary>
    SavedLayoutEvents = 1UL << 9,
}

/// <summary> current lifecycle state of the Intoner API host </summary>
public enum ObjectApiHostState
{
    /// <summary> the host is ready to accept calls </summary>
    Ready = 1,
    /// <summary> the host is unregistering its IPC providers </summary>
    Disposing = 2,
}

/// <summary> status returned by mutation operations </summary>
public enum ObjectApiResultStatus
{
    /// <summary> the mutation completed successfully </summary>
    Success = 0,
    /// <summary> the request failed validation </summary>
    InvalidRequest = 1,
    /// <summary> the requested object or layout does not exist </summary>
    NotFound = 2,
    /// <summary> the mutation conflicts with current persistent state </summary>
    Conflict = 3,
    /// <summary> persistent state could not be stored and the previous state was restored </summary>
    StorageFailed = 4,
    /// <summary> persistent storage failed and the previous state could not be fully restored </summary>
    RecoveryRequired = 5,
    /// <summary> the active runtime could not fully apply the mutation; inspect IsAccepted </summary>
    RuntimeApplyFailed = 6,
}

/// <summary> current API host identity and supported feature set </summary>
/// <param name="Version"> version </param>
/// <param name="PluginVersion"> loaded plugin version </param>
/// <param name="InstanceId"> identifier that changes whenever the API host is recreated </param>
/// <param name="State"> current host lifecycle state </param>
/// <param name="Capabilities"> supported API features </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectApiInfo(
    ObjectApiVersion Version,
    string PluginVersion,
    Guid InstanceId,
    ObjectApiHostState State,
    ObjectApiCapabilities Capabilities)
{
    /// <summary> checks whether the host supports every requested capability </summary>
    public bool Supports(ObjectApiCapabilities capabilities)
        => (Capabilities & capabilities) == capabilities;
}

/// <summary> API lifecycle notification </summary>
/// <param name="Info"> current host information </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectApiStateChanged(ObjectApiInfo Info);

/// <summary> composed scene revision notification </summary>
/// <param name="InstanceId"> API host instance that produced the revision </param>
/// <param name="SceneRevision"> new composed scene revision </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectSceneChanged(Guid InstanceId, long SceneRevision);

/// <summary> persistent scene revision notification </summary>
/// <param name="InstanceId"> API host instance that produced the revision </param>
/// <param name="PersistentRevision"> new persistent scene revision </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectSceneChanged(Guid InstanceId, long PersistentRevision);

/// <summary> notification sent when the saved layouts change </summary>
/// <param name="InstanceId"> API host instance that produced the revision </param>
/// <param name="SavedLayoutsRevision"> revision that changes whenever a saved layout is added, changed, or removed </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutsChanged(Guid InstanceId, long SavedLayoutsRevision);

/// <summary> result of a revision checked persistent scene replacement </summary>
/// <param name="Status"> typed result status </param>
/// <param name="IsAccepted"> whether the requested persistent state was committed </param>
/// <param name="SceneRevision"> composed scene revision after the request </param>
/// <param name="PersistentRevision"> persistent scene revision after the request </param>
/// <param name="Message"> short diagnostic intended for logs </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectSceneMutationResult(
    ObjectApiResultStatus Status,
    bool IsAccepted,
    long SceneRevision,
    long PersistentRevision,
    string Message = "")
{
    /// <summary> whether the persistent scene was committed and reconciled </summary>
    [IgnoreMember]
    public bool IsSuccess => Status == ObjectApiResultStatus.Success;
}

/// <summary> revision receipt for a successful persistent mutation </summary>
/// <param name="ObjectId"> affected object id when the operation targets an object </param>
/// <param name="SceneRevision"> composed scene revision after the operation </param>
/// <param name="PersistentRevision"> persistent scene revision after the operation </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectMutationReceipt(Guid? ObjectId, long SceneRevision, long PersistentRevision);

/// <summary> result of a persistent object mutation </summary>
/// <param name="Status"> typed result status </param>
/// <param name="IsAccepted"> whether the requested persistent state was committed </param>
/// <param name="Receipt"> mutation receipt when the operation was committed </param>
/// <param name="Message"> short diagnostic intended for logs </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectMutationResult(
    ObjectApiResultStatus Status,
    bool IsAccepted,
    ObjectMutationReceipt? Receipt = null,
    string Message = "")
{
    /// <summary> whether the mutation completed successfully </summary>
    [IgnoreMember]
    public bool IsSuccess => Status == ObjectApiResultStatus.Success;
}

/// <summary> result of a saved layout mutation </summary>
/// <param name="Status"> typed result status </param>
/// <param name="IsAccepted"> whether the requested saved or selected layout state was committed </param>
/// <param name="LayoutId"> affected layout id when available </param>
/// <param name="SavedLayoutsRevision"> revision for all saved layouts after the operation </param>
/// <param name="SceneRevision"> composed scene revision after the operation </param>
/// <param name="PersistentRevision"> persistent scene revision after the operation </param>
/// <param name="Message"> short diagnostic intended for logs </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutMutationResult(
    ObjectApiResultStatus Status,
    bool IsAccepted,
    Guid? LayoutId,
    long SavedLayoutsRevision,
    long SceneRevision,
    long PersistentRevision,
    string Message = "")
{
    /// <summary> whether the mutation completed successfully </summary>
    [IgnoreMember]
    public bool IsSuccess => Status == ObjectApiResultStatus.Success;
}
