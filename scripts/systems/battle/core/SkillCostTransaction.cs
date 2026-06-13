using Godot;

internal sealed class SkillCostTransaction
{
    internal StringName SkillId { get; set; } = "";
    internal int SkillLevel { get; set; } = 1;
    internal int ApCost { get; set; }
    internal int MpCost { get; set; }
    internal int StaminaCost { get; set; }
    internal int AuraCost { get; set; }
    internal int CooldownTurns { get; set; }
    internal int PrecastDamage { get; set; }

    internal bool HasResourceCost => MpCost > 0 || StaminaCost > 0 || AuraCost > 0;

    internal SkillCostTransaction Clone()
    {
        return new SkillCostTransaction
        {
            SkillId = SkillId,
            SkillLevel = SkillLevel,
            ApCost = ApCost,
            MpCost = MpCost,
            StaminaCost = StaminaCost,
            AuraCost = AuraCost,
            CooldownTurns = CooldownTurns,
            PrecastDamage = PrecastDamage,
        };
    }

    internal static SkillCostTransaction CooldownOnly(
        StringName skillId,
        int skillLevel,
        int cooldownTurns
    )
    {
        return new SkillCostTransaction
        {
            SkillId = skillId,
            SkillLevel = skillLevel,
            CooldownTurns = Mathf.Max(0, cooldownTurns),
        };
    }
}
