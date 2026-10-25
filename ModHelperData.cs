namespace SnappyCoop;

public static class ModHelperData
{
    public const string WorksOnVersion = "55.0";
    public const string Version = "0.2.0";
    public const string RepoOwner = "OliverMarcusson";
    public const string RepoName = "SnappyCoop";
    public const string Name = "Snappy Coop";

    public const string Description =
        """
        Smoother co-op: signs off simulation steps 5x as often, so other players stall less waiting on you.
        Same network protocol, so it works alongside players without the mod (everyone having it is best).

        Optional "Trim Delay" setting cuts up to 0.2 s off your action delay (2 players, stable connections only).
        Logs co-op network stats to the MelonLoader log every 30 s, or on F8.
        """;
}
