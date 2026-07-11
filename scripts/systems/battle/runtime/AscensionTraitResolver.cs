using Godot;

public static class AscensionTraitResolver
{
    public static void ApplyToUnit(BattleUnitState unitState, PassiveSourceContext context)
    {
        if (unitState == null || context == null)
            return;

        _apply_identity_def_projection(unitState, context.bloodline_def);

        _apply_identity_def_projection(unitState, context.bloodline_stage_def);

        _apply_identity_def_projection(unitState, context.ascension_def);

        _apply_identity_def_projection(unitState, context.ascension_stage_def);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        BloodlineDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.RacialGrantedSkills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        BloodlineStageDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.RacialGrantedSkills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        AscensionDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.RacialGrantedSkills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        AscensionStageDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.RacialGrantedSkills);
    }

    private static void _initialize_racial_skill_charges(
        BattleUnitState unitState,
        System.Collections.Generic.IReadOnlyList<RacialGrantedSkillDefinition> grants
    )
    {
        if (grants == null)
            return;

        foreach (RacialGrantedSkillDefinition grant in grants)
        {
            if (grant == null || grant.SkillId == "")
                continue;

            var chargeKey = new StringName($"racial_skill_{(string)grant.SkillId}");

            if (grant.ChargeKindKind == RacialSkillChargeKind.PerBattle)
            {
                if (!unitState.HasPerBattleChargeTyped(chargeKey))
                    unitState.SetPerBattleChargeTyped(chargeKey, Mathf.Max(grant.Charges, 1));
            }
            else if (grant.ChargeKindKind == RacialSkillChargeKind.PerTurn)
            {
                int chargeCount = Mathf.Max(grant.Charges, 1);

                unitState.SetPerTurnChargeLimitTyped(chargeKey, chargeCount);

                if (!unitState.HasPerTurnChargeTyped(chargeKey))
                    unitState.SetPerTurnChargeTyped(chargeKey, chargeCount);
                else
                {
                    int currentChargeCount = unitState.GetPerTurnChargeTyped(chargeKey);
                    unitState.SetPerTurnChargeTyped(
                        chargeKey,
                        Mathf.Clamp(
                            currentChargeCount,
                            0,
                            chargeCount
                        )
                    );
                }
            }
        }
    }

}
