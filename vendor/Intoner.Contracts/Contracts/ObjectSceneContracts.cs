using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> runtime world object </summary>
/// <param name="Id"> object id, or 'Guid.Empty' to generate one on local create or import </param>
/// <param name="Name"> display name </param>
/// <param name="Kind"> object kind and required model branch </param>
/// <param name="Visible"> initial visible state </param>
/// <param name="Transform"> world transform </param>
/// <param name="CreatedAtUtc"> creation time, or the default value to use current utc on local create or import </param>
/// <param name="CreatedIn"> saved creation location </param>
/// <param name="CollectionId"> optional object owned collection id </param>
/// <param name="Model"> kind matching model payload </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record WorldObject(
    Guid Id,
    string Name,
    WorldObjectKind Kind,
    bool Visible,
    WorldObjectTransform Transform,
    DateTime CreatedAtUtc,
    ObjectLocationData CreatedIn,
    string CollectionId,
    WorldObjectModelData Model);

/// <summary> partial runtime object update </summary>
/// <param name="Name"> updated display name, or null to keep the current name </param>
/// <param name="Visible"> updated visible state, or null to keep the current state </param>
/// <param name="Transform"> updated transform, or null to keep the current transform </param>
/// <param name="Model"> updated model patch for the current object kind, or null to keep the current model </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record WorldObjectPatch(
    string? Name = null,
    bool? Visible = null,
    WorldObjectTransform? Transform = null,
    WorldObjectModelPatch? Model = null);

/// <summary> persistent object including editor organization state </summary>
/// <param name="Object"> runtime object state </param>
/// <param name="FolderPath"> folder path, or an empty string when ungrouped </param>
/// <param name="Locked"> whether editing is locked </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObject(
    WorldObject Object,
    string FolderPath,
    bool Locked);

/// <summary> revision checked persistent object write </summary>
/// <param name="ExpectedRevision"> persistent scene revision that must still be current </param>
/// <param name="Object"> object payload for create, import, or update </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectWriteRequest(
    long ExpectedRevision,
    PersistentObject Object);

/// <summary> revision checked persistent object target </summary>
/// <param name="ExpectedRevision"> persistent scene revision that must still be current </param>
/// <param name="ObjectId"> object to remove or duplicate </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectTargetRequest(
    long ExpectedRevision,
    Guid ObjectId);

/// <summary> partial persistent object update </summary>
/// <param name="Object"> partial runtime state update, or null when only editor state changes </param>
/// <param name="FolderPath"> updated placed list folder path, or null to keep the current path </param>
/// <param name="Locked"> updated editor lock state, or null to keep the current state </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectPatch(
    WorldObjectPatch? Object = null,
    string? FolderPath = null,
    bool? Locked = null);

/// <summary> persistent object patch request </summary>
/// <param name="ExpectedRevision"> persistent scene revision that must still be current </param>
/// <param name="ObjectId"> persistent object id </param>
/// <param name="Patch"> partial update payload; null fields keep the current value </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectPatchRequest(
    long ExpectedRevision,
    Guid ObjectId,
    PersistentObjectPatch Patch);

/// <summary> persistent objects and their organization </summary>
/// <param name="Objects"> persistent object set </param>
/// <param name="Folders"> ordered explicit folder paths, including empty folders </param>
/// <param name="FolderColors"> folder colors keyed by folder path using '#RRGGBB' or '#RRGGBBAA'  </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectSet(
    IReadOnlyList<PersistentObject> Objects,
    IReadOnlyList<string> Folders,
    IReadOnlyDictionary<string, string> FolderColors);

/// <summary> identifying information for a saved layout </summary>
/// <param name="Id"> saved layout id </param>
/// <param name="Name"> saved layout name </param>
/// <param name="Revision"> revision for this layout </param>
/// <param name="CreatedAtUtc"> layout creation timestamp </param>
/// <param name="UpdatedAtUtc"> last layout update timestamp </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutInfo(
    Guid Id,
    string Name,
    long Revision,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary> saved layout with its complete content </summary>
/// <param name="Id"> saved layout id </param>
/// <param name="Name"> saved layout name </param>
/// <param name="Revision"> revision for this layout </param>
/// <param name="CreatedAtUtc"> layout creation timestamp </param>
/// <param name="UpdatedAtUtc"> last layout update timestamp </param>
/// <param name="Content"> persistent objects and folder organization </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayout(
    Guid Id,
    string Name,
    long Revision,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    PersistentObjectSet Content);

/// <summary> saved layouts stored by the current API host </summary>
/// <param name="InstanceId"> API host instance that owns the revision </param>
/// <param name="Revision"> revision that changes whenever a saved layout is added, changed, or removed </param>
/// <param name="Layouts"> saved layout information in display order </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutsSnapshot(
    Guid InstanceId,
    long Revision,
    IReadOnlyList<SavedObjectLayoutInfo> Layouts);

/// <summary> revision checked request to save the current persistent scene as a layout </summary>
/// <param name="ExpectedPersistentRevision"> persistent scene revision that must still be current </param>
/// <param name="Name"> requested layout name </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutSaveRequest(
    long ExpectedPersistentRevision,
    string Name);

/// <summary> revision checked request to set the default layout </summary>
/// <param name="ExpectedPersistentRevision"> persistent scene revision that must still be current </param>
/// <param name="ExpectedLayoutRevision"> selected layout revision that must still be current </param>
/// <param name="LayoutId"> layout to select </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutSetDefaultRequest(
    long ExpectedPersistentRevision,
    long ExpectedLayoutRevision,
    Guid LayoutId);

/// <summary> revision checked saved layout deletion </summary>
/// <param name="ExpectedPersistentRevision"> persistent scene revision that must still be current </param>
/// <param name="ExpectedLayoutRevision"> target layout revision that must still be current </param>
/// <param name="LayoutId"> layout to delete </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record SavedObjectLayoutDeleteRequest(
    long ExpectedPersistentRevision,
    long ExpectedLayoutRevision,
    Guid LayoutId);

/// <summary> loaded layout source </summary>
/// <param name="Type"> whether this is the local default layout or a temporary source </param>
/// <param name="LayoutId"> saved layout id for the local default layout </param>
/// <param name="SourceKey"> source key for temporary layouts, or the layout id string for the default layout </param>
/// <param name="SourceSessionId"> source session id for temporary layouts, or 'Guid.Empty' for the local default layout </param>
/// <param name="Name"> loaded layout display name </param>
/// <param name="Revision"> current source revision </param>
/// <param name="UpdatedAtUtc"> last update time for this source </param>
/// <param name="Objects"> full object list for this source </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record LoadedObjectLayout(
    LoadedObjectLayoutType Type,
    Guid? LayoutId,
    string SourceKey,
    Guid SourceSessionId,
    string Name,
    long Revision,
    DateTime UpdatedAtUtc,
    IReadOnlyList<WorldObject> Objects);

/// <summary> runtime state for one object </summary>
/// <param name="Id"> logical object id </param>
/// <param name="State"> current runtime state for this object </param>
/// <param name="FailureCode"> short failure code for 'LoadFailed' states </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record RuntimeObjectState(
    Guid Id,
    RuntimeObjectStateKind State,
    string? FailureCode);

/// <summary> current persistent object scene without caller owned temporary sources </summary>
/// <param name="Revision"> persistent scene revision </param>
/// <param name="DefaultLayout"> current default layout, if one is loaded </param>
/// <param name="Standalone"> persistent objects and organization outside the default layout </param>
/// <param name="CurrentLocation"> current local world and territory context </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectSceneSnapshot(
    long Revision,
    SavedObjectLayout? DefaultLayout,
    PersistentObjectSet Standalone,
    ObjectLocationData CurrentLocation);

/// <summary> desired content for the current default layout </summary>
/// <param name="LayoutId"> current default layout returned by the scene snapshot </param>
/// <param name="Content"> replacement objects and folder organization </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record DefaultLayoutUpdate(
    Guid LayoutId,
    PersistentObjectSet Content);

/// <summary> revision checked replacement of the current persistent scene </summary>
/// <param name="ExpectedRevision"> persistent revision that must still be current </param>
/// <param name="Standalone"> replacement standalone objects and organization </param>
/// <param name="DefaultLayout"> replacement for the current default layout, or null to clear the default selection </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record PersistentObjectSceneApplyRequest(
    long ExpectedRevision,
    PersistentObjectSet Standalone,
    DefaultLayoutUpdate? DefaultLayout);

/// <summary> current composed object scene </summary>
/// <param name="Revision"> local scene revision </param>
/// <param name="PersistentRevision"> local persistent scene revision for standalone objects and the default layout </param>
/// <param name="DefaultLayoutId"> current local default layout id, if one is loaded </param>
/// <param name="StandaloneObjects"> local standalone objects that are not stored inside the default layout </param>
/// <param name="LoadedLayouts"> loaded layout sources, including the default layout and all temporary layouts </param>
/// <param name="RuntimeStates"> runtime state list for the composed scene </param>
/// <param name="CurrentLocation"> current local world and territory context </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectSceneSnapshot(
    long Revision,
    long PersistentRevision,
    Guid? DefaultLayoutId,
    IReadOnlyList<WorldObject> StandaloneObjects,
    IReadOnlyList<LoadedObjectLayout> LoadedLayouts,
    IReadOnlyList<RuntimeObjectState> RuntimeStates,
    ObjectLocationData CurrentLocation);

