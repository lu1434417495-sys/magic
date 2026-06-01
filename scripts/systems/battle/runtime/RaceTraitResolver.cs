using Godot;

[GlobalClass]
public partial class RaceTraitResolver : RefCounted
{
    private static readonly StringName CHARGE_KIND_PER_BATTLE = "per_battle";

    private static readonly StringName CHARGE_KIND_PER_TURN = "per_turn";

    public static void apply_to_unit(BattleUnitState unitState, PassiveSourceContext context)
    {
        if (unitState == null || context == null)
            return;

        _apply_identity_def_projection(unitState, context.race_def, unitState.race_trait_ids);

        _apply_identity_def_projection(unitState, context.subrace_def, unitState.subrace_trait_ids);
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        RaceDef identityDef,
        Godot.Collections.Array<StringName> traitTarget
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(traitTarget, identityDef.trait_ids);

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
        SubraceDef identityDef,
        Godot.Collections.Array<StringName> traitTarget
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(traitTarget, identityDef.trait_ids);

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

            if (grant.charge_kind == CHARGE_KIND_PER_BATTLE)
            {
                if (!unitState.per_battle_charges.ContainsKey(chargeKey))
                    unitState.per_battle_charges[chargeKey] = Mathf.Max(grant.charges, 1);
            }
            else if (grant.charge_kind == CHARGE_KIND_PER_TURN)
            {
                int chargeCount = Mathf.Max(grant.charges, 1);

                unitState.per_turn_charge_limits[chargeKey] = chargeCount;

                if (!unitState.per_turn_charges.ContainsKey(chargeKey))
                    unitState.per_turn_charges[chargeKey] = chargeCount;
                else
                {
                    int currentChargeCount = unitState.per_turn_charges[chargeKey].AsInt32();
                    unitState.per_turn_charges[chargeKey] = Mathf.Clamp(
                        currentChargeCount,
                        0,
                        chargeCount
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
        Godot.Collections.Dictionary target,
        Godot.Collections.Dictionary values
    )
    {
        foreach (var rawKey in values.Keys)
        {
            var damageTag = ProgressionDataUtils.to_string_name(rawKey);

            var mitigationTier = ProgressionDataUtils.to_string_name(values[rawKey]);

            if (damageTag == "" || mitigationTier == "")
                continue;

            target[damageTag] = mitigationTier;
        }
    }

}
