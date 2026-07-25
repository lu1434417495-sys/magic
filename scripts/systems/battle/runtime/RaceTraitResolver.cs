using Godot;

public static class RaceTraitResolver
{
    public static void ApplyToUnit(BattleUnitState unitState, PassiveSourceContext context)
    {
        if (unitState == null || context == null)
            return;

        var visionTags = new StringNameList();
        var proficiencyTags = new StringNameList();
        _append_identity_capability_projection(
            visionTags,
            proficiencyTags,
            context.race_def
        );
        _append_identity_capability_projection(
            visionTags,
            proficiencyTags,
            context.subrace_def
        );
        unitState.ReplaceVisionProficiencyTagsTyped(
            visionTags,
            proficiencyTags
        );

        _apply_identity_def_projection(unitState, context.race_def);

        _apply_identity_def_projection(unitState, context.subrace_def);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        RaceDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        unitState.AppendSaveTagsTyped(
            identityDef.SaveAdvantageTags,
            identityDef.SaveDisadvantageTags,
            identityDef.SaveImmunityTags
        );

        unitState.MergeDamageResistancesTyped(
            identityDef.DamageResistances
        );

        _initialize_racial_skill_charges(unitState, identityDef.RacialGrantedSkills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        SubraceDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        unitState.AppendSaveTagsTyped(
            identityDef.SaveAdvantageTags,
            identityDef.SaveDisadvantageTags,
            identityDef.SaveImmunityTags
        );

        unitState.MergeDamageResistancesTyped(
            identityDef.DamageResistances
        );

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
                        Mathf.Clamp(currentChargeCount, 0, chargeCount)
                    );
                }
            }
        }
    }

    private static void _append_identity_capability_projection(
        StringNameList visionTags,
        StringNameList proficiencyTags,
        RaceDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(visionTags, identityDef.VisionTags);
        _append_unique_string_names(
            proficiencyTags,
            identityDef.ProficiencyTags
        );
    }

    private static void _append_identity_capability_projection(
        StringNameList visionTags,
        StringNameList proficiencyTags,
        SubraceDefinition identityDef
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(visionTags, identityDef.VisionTags);
        _append_unique_string_names(
            proficiencyTags,
            identityDef.ProficiencyTags
        );
    }

    private static void _append_unique_string_names(
        StringNameList target,
        System.Collections.Generic.IReadOnlyList<StringName> values
    )
    {
        if (target == null || values == null)
            return;

        foreach (StringName value in values)
        {
            if (value == "" || target.Contains(value))
                continue;

            target.Add(value);
        }
    }

}
