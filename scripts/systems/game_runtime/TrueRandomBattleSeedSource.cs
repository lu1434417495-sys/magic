internal sealed class TrueRandomBattleSeedSource : IBattleSeedSource
{
    public int NextSeed(EncounterAnchorData encounterAnchor) =>
        (int)TrueRandomSeedService.GenerateSeed();
}
