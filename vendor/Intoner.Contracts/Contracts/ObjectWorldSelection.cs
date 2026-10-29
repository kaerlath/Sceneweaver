using System.Collections.Immutable;
using MessagePack;

namespace Intoner.Objects.Api;

/// <summary> selected data centers and individual worlds where an object can appear </summary>
[MessagePackObject(keyAsPropertyName: true)]
public sealed record ObjectWorldSelection
{
    /// <summary> includes every world. the individual selections must be empty! </summary>
    public bool AllDataCenters { get; }

    /// <summary> includes every world in each listed data center </summary>
    public ImmutableArray<uint> DataCenterIds { get; }

    /// <summary> additional individual worlds to include </summary>
    public ImmutableArray<ushort> WorldIds { get; }

    /// <summary> creates an immutable, sorted selection with no duplicate ids </summary>
    [SerializationConstructor]
    public ObjectWorldSelection(bool allDataCenters, ImmutableArray<uint> dataCenterIds = default, ImmutableArray<ushort> worldIds = default)
    {
        ImmutableArray<uint> dataCenters = dataCenterIds.IsDefault ? [] : dataCenterIds;
        ImmutableArray<ushort> worlds = worldIds.IsDefault ? [] : worldIds;
        bool hasSelection = !dataCenters.IsEmpty || !worlds.IsEmpty;
        bool valid = allDataCenters ? !hasSelection : hasSelection;

        if (!valid || dataCenters.Contains(0u) || worlds.Contains((ushort)0))
        {
            throw new ArgumentException("Select all data centers, or at least one nonzero data center or world ID.");
        }

        AllDataCenters = allDataCenters;
        DataCenterIds = dataCenters.Length < 2 ? dataCenters : dataCenters.Distinct().Order().ToImmutableArray();
        WorldIds = worlds.Length < 2 ? worlds : worlds.Distinct().Order().ToImmutableArray();
    }

    /// <summary> checks a resolved current world against the selection </summary>
    public bool Matches(ushort worldId, uint dataCenterId)
        => worldId != 0 && dataCenterId != 0 && (AllDataCenters
            || DataCenterIds.BinarySearch(dataCenterId) >= 0 || WorldIds.BinarySearch(worldId) >= 0);

    /// <inheritdoc />
    public bool Equals(ObjectWorldSelection? other)
        => other is not null && AllDataCenters == other.AllDataCenters
            && DataCenterIds.AsSpan().SequenceEqual(other.DataCenterIds.AsSpan())
            && WorldIds.AsSpan().SequenceEqual(other.WorldIds.AsSpan());

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(AllDataCenters);
        hash.Add(DataCenterIds.Length);
        foreach (uint dataCenterId in DataCenterIds)
        {
            hash.Add(dataCenterId);
        }

        foreach (ushort worldId in WorldIds)
        {
            hash.Add(worldId);
        }

        return hash.ToHashCode();
    }
}
