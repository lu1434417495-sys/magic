using System.Collections.Generic;
using Godot;

public sealed class CombatLineThroughAttackDefinition
{
    public CombatLineThroughAttackDefinition(
        int maximumWeaponRange,
        int intermediateWeaponDiceMultiplier,
        IReadOnlyList<int> primaryWeaponDiceMultiplierCurve,
        IReadOnlyList<int> primaryAttackRollBonusCurve,
        int successfulIntermediateHitBonusWeaponDice,
        int successfulIntermediateHitAttackRollBonus,
        IReadOnlyList<int> successfulIntermediateHitBonusCapCurve
    )
    {
        MaximumWeaponRange = Mathf.Max(maximumWeaponRange, 1);
        IntermediateWeaponDiceMultiplier = Mathf.Max(intermediateWeaponDiceMultiplier, 1);
        PrimaryWeaponDiceMultiplierCurve = SkillDefinitionCollectionFreeze.List(
            primaryWeaponDiceMultiplierCurve
        );
        PrimaryAttackRollBonusCurve = SkillDefinitionCollectionFreeze.List(
            primaryAttackRollBonusCurve
        );
        SuccessfulIntermediateHitBonusWeaponDice = Mathf.Max(
            successfulIntermediateHitBonusWeaponDice,
            0
        );
        SuccessfulIntermediateHitAttackRollBonus = Mathf.Max(
            successfulIntermediateHitAttackRollBonus,
            0
        );
        SuccessfulIntermediateHitBonusCapCurve = SkillDefinitionCollectionFreeze.List(
            successfulIntermediateHitBonusCapCurve
        );
    }

    public int MaximumWeaponRange { get; }
    public int IntermediateWeaponDiceMultiplier { get; }
    public IReadOnlyList<int> PrimaryWeaponDiceMultiplierCurve { get; }
    public IReadOnlyList<int> PrimaryAttackRollBonusCurve { get; }
    public int SuccessfulIntermediateHitBonusWeaponDice { get; }
    public int SuccessfulIntermediateHitAttackRollBonus { get; }
    public IReadOnlyList<int> SuccessfulIntermediateHitBonusCapCurve { get; }

    public int GetPrimaryWeaponDiceMultiplier(int skillLevel) =>
        Mathf.Max(ReadCurveValue(PrimaryWeaponDiceMultiplierCurve, skillLevel, 1), 1);

    public int GetPrimaryAttackRollBonus(int skillLevel) =>
        Mathf.Max(ReadCurveValue(PrimaryAttackRollBonusCurve, skillLevel, 0), 0);

    public int GetSuccessfulIntermediateHitBonusCap(int skillLevel) =>
        Mathf.Max(ReadCurveValue(SuccessfulIntermediateHitBonusCapCurve, skillLevel, 0), 0);

    public int GetPrimaryWeaponDiceMultiplier(int skillLevel, int successfulIntermediateHits)
    {
        int countedHits = Mathf.Min(
            Mathf.Max(successfulIntermediateHits, 0),
            GetSuccessfulIntermediateHitBonusCap(skillLevel)
        );
        return GetPrimaryWeaponDiceMultiplier(skillLevel)
            + countedHits * SuccessfulIntermediateHitBonusWeaponDice;
    }

    public int GetPrimaryAttackRollBonus(int skillLevel, int successfulIntermediateHits)
    {
        int countedHits = Mathf.Min(
            Mathf.Max(successfulIntermediateHits, 0),
            GetSuccessfulIntermediateHitBonusCap(skillLevel)
        );
        return GetPrimaryAttackRollBonus(skillLevel)
            + countedHits * SuccessfulIntermediateHitAttackRollBonus;
    }

    internal static CombatLineThroughAttackDefinition FromResource(
        CombatLineThroughAttackDef source
    ) =>
        source == null
            ? null
            : new CombatLineThroughAttackDefinition(
                source.maximum_weapon_range,
                source.intermediate_weapon_dice_multiplier,
                source.primary_weapon_dice_multiplier_curve,
                source.primary_attack_roll_bonus_curve,
                source.successful_intermediate_hit_bonus_weapon_dice,
                source.successful_intermediate_hit_attack_roll_bonus,
                source.successful_intermediate_hit_bonus_cap_curve
            );

    private static int ReadCurveValue(
        IReadOnlyList<int> curve,
        int skillLevel,
        int fallback
    )
    {
        if (curve == null || curve.Count == 0)
            return fallback;
        int index = Mathf.Clamp(skillLevel, 0, curve.Count - 1);
        return curve[index];
    }
}
