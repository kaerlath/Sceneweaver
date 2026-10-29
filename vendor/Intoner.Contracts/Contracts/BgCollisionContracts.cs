using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> set BG collider shape </summary>
public enum BgCollisionShape
{
    None,
    Mesh,
    Box,
    Sphere,
    Cylinder,
    Plane,
}

/// <summary> set local shape (rotation uses file Euler radians) </summary>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record BgAnalyticShapeData(
    ObjectVector3 Translation,
    ObjectVector3 Rotation,
    ObjectVector3 Scale,
    ObjectVector3 BoundsMin,
    ObjectVector3 BoundsMax,
    uint MaterialId,
    uint MaterialMask);

/// <summary> portable set collider bound to one model </summary>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record BgColliderDefinitionData(
    string ModelPath,
    string SourcePath,
    BgCollisionShape Kind,
    string PcbPath,
    ulong MaterialId,
    ulong MaterialMask,
    BgAnalyticShapeData? Shape);
