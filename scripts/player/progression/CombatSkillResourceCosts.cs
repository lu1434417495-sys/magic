public readonly record struct CombatSkillResourceCosts(
    int ApCost,
    int MpCost,
    int StaminaCost,
    int AuraCost,
    int CooldownTu
)
{
    public static readonly CombatSkillResourceCosts Zero = new(0, 0, 0, 0, 0);
}
