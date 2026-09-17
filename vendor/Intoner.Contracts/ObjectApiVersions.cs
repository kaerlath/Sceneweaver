namespace Intoner.Objects.Api;

/// <summary> public object api version constants </summary>
public static class ObjectApiVersions
{
    /// <summary> breaking version number </summary>
    public const int Breaking = 1;
    /// <summary> nonbreaking feature version number </summary>
    public const int Feature = 0;

    /// <summary> current intoner API version </summary>
    public static ObjectApiVersion Current
        => new(Breaking, Feature);
}
