using System.Runtime.InteropServices;
using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> object kind </summary>
public enum WorldObjectKind
{
    Light = 1,
    BgObject = 2,
    Furniture = 4,
    Vfx = 8,
}

/// <summary> light type </summary>
public enum ObjectLightType : uint
{
    WorldLight = 1,
    AreaLight = 2,
    SpotLight = 3,
    FlatLight = 4,
}

/// <summary> light falloff type </summary>
public enum ObjectLightFalloffType : uint
{
    Linear = 0,
    Quadratic = 1,
    Cubic = 2,
}

/// <summary> draw object outline color </summary>
public enum ObjectOutlineColor : byte
{
    None = 0,
    Red = 1,
    Green = 2,
    Blue = 3,
    Yellow = 4,
    Orange = 5,
    Magenta = 6,
    Black = 7,
}

/// <summary> loaded layout type </summary>
public enum LoadedObjectLayoutType
{
    /// <summary> the local persistent default layout </summary>
    Default = 1,
    /// <summary> a caller owned temporary source </summary>
    Temporary = 2,
}

/// <summary> runtime object state </summary>
public enum RuntimeObjectStateKind
{
    /// <summary> the object has an active game runtime </summary>
    Active = 1,
    /// <summary> the object belongs to the current location but is not active </summary>
    Inactive = 2,
    /// <summary> the object belongs to another location </summary>
    LocationMismatch = 3,
    /// <summary> runtime creation failed </summary>
    LoadFailed = 4,
}

/// <summary> temporary source mutation status </summary>
public enum TemporarySourceMutationStatus
{
    /// <summary >the requested source revision was applied </summary>
    Success = 0,
    /// <summary> source identity, session, or revision data is invalid </summary>
    InvalidSource = 1,
    /// <summary> one or more object payloads are invalid </summary>
    InvalidObject = 2,
    /// <summary> the requested revision is older than authoritative state </summary>
    StaleRevision = 3,
    /// <summary> the requested source or object does not exist </summary>
    ObjectNotFound = 4,
    /// <summary> the request uses a different source session </summary>
    SourceMismatch = 5,
    /// <summary> source state was accepted but runtime reconciliation failed </summary>
    RuntimeApplyFailed = 6,
    /// <summary> the same source revision was already accepted </summary>
    AlreadyApplied = 7,
    /// <summary> one or more collection payloads are invalid </summary>
    InvalidCollection = 8,
    /// <summary> the source is not owned by the calling plugin </summary>
    OwnershipMismatch = 9,
    /// <summary> one or more runtime object identities are already owned by another scene source </summary>
    IdentityConflict = 10,
}

/// <summary> temporary object change kind </summary>
public enum TemporaryObjectChangeKind
{
    /// <summary> creates or replaces one source object </summary>
    Upsert = 1,
    /// <summary> removes one source object </summary>
    Remove = 2,
    /// <summary> partially updates one source object </summary>
    Patch = 3,
}

/// <summary> object api version </summary>
/// <param name="Breaking"> breaking version for incompatible changes </param>
/// <param name="Feature"> feature version for non breaking changes </param>
[MessagePackObject(keyAsPropertyName: true)]
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ObjectApiVersion(int Breaking, int Feature);

/// <summary> 2d float vector </summary>
/// <param name="X"> x component </param>
/// <param name="Y"> y component </param>
[MessagePackObject(keyAsPropertyName: true)]
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ObjectVector2(float X, float Y);

/// <summary> 3d float vector </summary>
/// <param name="X"> x component </param>
/// <param name="Y"> y component </param>
/// <param name="Z"> z component </param>
[MessagePackObject(keyAsPropertyName: true)]
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ObjectVector3(float X, float Y, float Z);

/// <summary> 4d float vector </summary>
/// <param name="X"> x component </param>
/// <param name="Y"> y component </param>
/// <param name="Z"> z component </param>
/// <param name="W"> w component </param>
[MessagePackObject(keyAsPropertyName: true)]
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ObjectVector4(float X, float Y, float Z, float W);

/// <summary> object transform </summary>
/// <param name="Position"> world position </param>
/// <param name="RotationDegrees"> euler rotation in degrees </param>
/// <param name="Scale"> local scale multiplier </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record WorldObjectTransform(
    ObjectVector3 Position,
    ObjectVector3 RotationDegrees,
    ObjectVector3 Scale);

/// <summary> object world and housing location context </summary>
/// <param name="WorldId"> world id, or zero to use the current world on local create or import </param>
/// <param name="WorldName"> world display name </param>
/// <param name="TerritoryId"> territory id, or zero to use the current territory on local create or import </param>
/// <param name="TerritoryName"> territory display name </param>
/// <param name="DivisionId"> housing division id </param>
/// <param name="WardId"> housing ward id </param>
/// <param name="HouseId"> housing plot or apartment if id is 100 </param>
/// <param name="RoomId"> apartment or housing room id </param>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectLocationData(
    ushort WorldId,
    string WorldName,
    uint TerritoryId,
    string TerritoryName,
    uint DivisionId,
    uint WardId,
    uint HouseId,
    uint RoomId);

