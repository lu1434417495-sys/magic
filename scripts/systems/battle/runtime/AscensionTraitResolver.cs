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
        BloodlineDef identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.racial_granted_skills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        BloodlineStageDef identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.racial_granted_skills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        AscensionDef identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.racial_granted_skills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        AscensionStageDef identityDef
    )
    {
        if (identityDef == null)
            return;

        _initialize_racial_skill_charges(unitState, identityDef.racial_granted_skills);
    }

    private static void _initialize_racial_skill_charges(
        BattleUnitState unitState,
        Godot.Collections.Array<RacialGrantedSkill> grants
    )
    {
        if (grants == null)
            return;

        foreach (RacialGrantedSkill grant in grants)
        {
            if (grant == null || grant.skill_id == "")
                continue;

            var chargeKey = new StringName($"racial_skill_{(string)grant.skill_id}");

            if (grant.ChargeKind == RacialSkillChargeKind.PerBattle)
            {
                if (!unitState.HasPerBattleChargeTyped(chargeKey))
                    unitState.SetPerBattleChargeTyped(chargeKey, Mathf.Max(grant.charges, 1));
            }
            else if (grant.ChargeKind == RacialSkillChargeKind.PerTurn)
            {
                int chargeCount = Mathf.Max(grant.charges, 1);

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
