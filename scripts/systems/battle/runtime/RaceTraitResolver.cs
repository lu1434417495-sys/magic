using Godot;

public static class RaceTraitResolver
{
    public static void ApplyToUnit(BattleUnitState unitState, PassiveSourceContext context)
    {
        if (unitState == null || context == null)
            return;

        _apply_identity_def_projection(unitState, context.race_def);

        _apply_identity_def_projection(unitState, context.subrace_def);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        RaceDef identityDef
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(unitState.vision_tags, identityDef.vision_tags);

        _append_unique_string_names(
            unitState.proficiency_tags,
            identityDef.proficiency_tags
        );

        _append_unique_string_names(
            unitState.save_advantage_tags,
            identityDef.save_advantage_tags
        );

        _merge_damage_resistances(
            unitState.damage_resistances,
            identityDef.damage_resistances
        );

        _initialize_racial_skill_charges(unitState, identityDef.racial_granted_skills);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        SubraceDef identityDef
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(unitState.vision_tags, identityDef.vision_tags);

        _append_unique_string_names(
            unitState.proficiency_tags,
            identityDef.proficiency_tags
        );

        _append_unique_string_names(
            unitState.save_advantage_tags,
            identityDef.save_advantage_tags
        );

        _merge_damage_resistances(
            unitState.damage_resistances,
            identityDef.damage_resistances
        );

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
                        Mathf.Clamp(currentChargeCount, 0, chargeCount)
                    );
                }
            }
        }
    }

    private static void _append_unique_string_names(
        Godot.Collections.Array<StringName> target,
        Godot.Collections.Array<StringName> values
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

    private static void _merge_damage_resistances(
        BattleStringNameMap target,
        Godot.Collections.Dictionary values
    )
    {
        foreach (var rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            Variant rawValue = values[rawKey];
            if (rawValue.VariantType != Variant.Type.StringName)
                continue;
            var damageTag = rawKey.AsStringName();
            var mitigationTier = rawValue.AsStringName();

            if (damageTag == "" || mitigationTier == "")
                continue;

            target.Put(damageTag, mitigationTier);
        }
    }

}
