using Godot;

[GlobalClass]
public partial class AscensionTraitResolver : RefCounted
{
    private static readonly StringName CHARGE_KIND_PER_BATTLE = "per_battle";

    private static readonly StringName CHARGE_KIND_PER_TURN = "per_turn";

    public static void apply_to_unit(BattleUnitState unitState, PassiveSourceContext context)
    {
        if (unitState == null || context == null)
            return;

        _apply_identity_def_projection(
            unitState,
            context.bloodline_def,
            unitState.bloodline_trait_ids
        );

        _apply_identity_def_projection(
            unitState,
            context.bloodline_stage_def,
            unitState.bloodline_trait_ids
        );

        _apply_identity_def_projection(
            unitState,
            context.ascension_def,
            unitState.ascension_trait_ids
        );

        _apply_identity_def_projection(
            unitState,
            context.ascension_stage_def,
            unitState.ascension_trait_ids
        );
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        BloodlineDef identityDef,
        Godot.Collections.Array<StringName> traitTarget
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(traitTarget, V(identityDef.trait_ids));

        _initialize_racial_skill_charges(
            unitState,
            V(identityDef.racial_granted_skills)
        );
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        BloodlineStageDef identityDef,
        Godot.Collections.Array<StringName> traitTarget
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(traitTarget, V(identityDef.trait_ids));

        _initialize_racial_skill_charges(
            unitState,
            V(identityDef.racial_granted_skills)
        );
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        AscensionDef identityDef,
        Godot.Collections.Array<StringName> traitTarget
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(traitTarget, V(identityDef.trait_ids));

        _initialize_racial_skill_charges(
            unitState,
            V(identityDef.racial_granted_skills)
        );
    }

    private static void _apply_identity_def_projection(
        BattleUnitState unitState,
        AscensionStageDef identityDef,
        Godot.Collections.Array<StringName> traitTarget
    )
    {
        if (identityDef == null)
            return;

        _append_unique_string_names(traitTarget, V(identityDef.trait_ids));

        _initialize_racial_skill_charges(
            unitState,
            V(identityDef.racial_granted_skills)
        );
    }

    private static void _initialize_racial_skill_charges(
        BattleUnitState unitState,
        Godot.Collections.Array grants
    )
    {
        foreach (var grantValue in grants)
        {
            var grant = grantValue.AsGodotObject() as RacialGrantedSkill;

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
        Godot.Collections.Array values
    )
    {
        foreach (var rawValue in values)
        {
            var value = ProgressionDataUtils.to_string_name(rawValue);

            if (value == "" || target.Contains(value))
                continue;

            target.Add(value);
        }
    }

    private static Godot.Collections.Array V<[MustBeVariant] T>(Godot.Collections.Array<T> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (T value in values)
            result.Add(Variant.From(value));
        return result;
    }
}
