internal interface IBattleCounterattackChanceRoller
{
    int RollInclusive1To100();
}

internal sealed class BattleCounterattackChanceRoller
    : IBattleCounterattackChanceRoller
{
    internal static BattleCounterattackChanceRoller Instance { get; } =
        new();

    private BattleCounterattackChanceRoller() { }

    public int RollInclusive1To100() =>
        TrueRandomSeedService.RandiRange(1, 100);
}
