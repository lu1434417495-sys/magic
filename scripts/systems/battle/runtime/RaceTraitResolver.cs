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

    private static void _apply_identity_def_projection(BattleUnitState unitState, GodotObject identityDef, Godot.Collections.Array<StringName> traitTarget)
    {
        if (identityDef == null)
            return;
        _append_unique_string_names(traitTarget, _get_array_property(identityDef, "trait_ids"));
        _append_unique_string_names(unitState.vision_tags, _get_array_property(identityDef, "vision_tags"));
        _append_unique_string_names(unitState.proficiency_tags, _get_array_property(identityDef, "proficiency_tags"));
        _append_unique_string_names(unitState.save_advantage_tags, _get_array_property(identityDef, "save_advantage_tags"));
        _merge_damage_resistances(unitState.damage_resistances, _get_dictionary_property(identityDef, "damage_resistances"));
        _initialize_racial_skill_charges(unitState, _get_array_property(identityDef, "racial_granted_skills"));
    }

    private static void _initialize_racial_skill_charges(BattleUnitState unitState, Godot.Collections.Array grants)
    {
        foreach (var grantVariant in grants)
        {
            var grant = grantVariant.AsGodotObject() as RacialGrantedSkill;
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
                    unitState.per_turn_charges[chargeKey] = Mathf.Clamp(
                        unitState.per_turn_charges.GetValueOrDefault(chargeKey, 0).AsInt32(), 0, chargeCount);
            }
        }
    }

    private static void _append_unique_string_names(Godot.Collections.Array<StringName> target, Godot.Collections.Array values)
    {
        foreach (var rawValue in values)
        {
            var value = ProgressionDataUtils.to_string_name(rawValue);
            if (value == "" || target.Contains(value))
                continue;
            target.Add(value);
        }
    }

    private static void _merge_damage_resistances(Godot.Collections.Dictionary target, Godot.Collections.Dictionary values)
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

    private static Godot.Collections.Array _get_array_property(GodotObject source, string propertyName)
    {
        if (source == null)
            return new Godot.Collections.Array();
        var rawValue = source.Get(propertyName);
        return rawValue.VariantType == Variant.Type.Array ? rawValue.AsGodotArray() : new Godot.Collections.Array();
    }

    private static Godot.Collections.Dictionary _get_dictionary_property(GodotObject source, string propertyName)
    {
        if (source == null)
            return new Godot.Collections.Dictionary();
        var rawValue = source.Get(propertyName);
        return rawValue.VariantType == Variant.Type.Dictionary ? rawValue.AsGodotDictionary() : new Godot.Collections.Dictionary();
    }
}
