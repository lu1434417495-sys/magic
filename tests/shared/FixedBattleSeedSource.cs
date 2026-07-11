internal sealed class FixedBattleSeedSource : IBattleSeedSource
{
    private readonly int _seed;

    internal FixedBattleSeedSource(int seed)
    {
        _seed = seed;
    }

    public int NextSeed(EncounterAnchorData encounterAnchor) => _seed;
}
