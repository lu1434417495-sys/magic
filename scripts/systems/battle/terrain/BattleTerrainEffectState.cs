using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗地形效果状态数据。
// 翻译自 battle_terrain_effect_state.gd（2026-05-24，数据层 C# 迁移）。
public class BattleTerrainEffectState
{
    private static readonly string[] FormalParamKeys =
    {
        "lifetime_policy",
        "move_cost_delta",
        "render_overlay_id",
        "overlay_priority",
        "display_name",
        "accuracy_modifier_spec",
        "does_not_stack_with_status_id",
        "does_not_stack_with_status_ids",
        "contact_status_id",
        "contact_status_duration_tu",
        "contact_stack_behavior",
        "contact_stack_limit",
        "contact_status_display_label",
        "contact_counts_as_debuff_override",
        "contact_counts_as_debuff",
        "contact_undispellable",
        "contact_dispellable_magic",
        "contact_dispellable_harmful_magic",
        "contact_dispellable_beneficial_magic",
        "contact_save_dc",
        "contact_save_ability",
        "contact_save_tag",
        "contact_apply_on_save_failure",
        "contact_tick_interval_tu",
        "contact_timeline_damage_dice_count",
        "contact_timeline_damage_dice_sides",
        "contact_timeline_damage_flat_bonus",
        "contact_blocked_by_trait_id",
    };

    private static readonly string[] SerializedFieldNames =
    {
        "field_instance_id",
        "effect_id",
        "effect_type",
        "applied_status_id",
        "applied_status_duration_tu",
        "render_overlay_id",
        "overlay_priority",
        "source_unit_id",
        "source_skill_id",
        "target_team_filter",
        "power",
        "damage_tag",
        "remaining_tu",
        "tick_interval_tu",
        "next_tick_at_tu",
        "stack_behavior",
        "params",
    };

    private static readonly string[] RequiredNonEmptyStringFields =
    {
        "field_instance_id",
        "effect_id",
        "effect_type",
        "target_team_filter",
        "stack_behavior",
    };

    private static readonly string[] OptionalStringFields =
    {
        "applied_status_id",
        "render_overlay_id",
        "source_unit_id",
        "source_skill_id",
        "damage_tag",
    };

    private static readonly string[] IntegerFields =
    {
        "applied_status_duration_tu",
        "overlay_priority",
        "power",
        "remaining_tu",
        "tick_interval_tu",
        "next_tick_at_tu",
    };

    private static readonly string[] NonNegativeIntegerFields =
    {
        "remaining_tu",
        "tick_interval_tu",
        "next_tick_at_tu",
    };

    public StringName field_instance_id { get; set; } = "";
    public StringName effect_id { get; set; } = "";
    public StringName effect_type { get; set; } = "damage";
    internal BattleTerrainEffectRuntimeKind RuntimeEffectKind
    {
        get => BattleTypedNames.ToTerrainEffectRuntimeKind(effect_type);
        set => effect_type = BattleTypedNames.ToStringName(value);
    }
    public StringName lifetime_policy { get; set; } = "timed";
    public int move_cost_delta { get; set; }
    public StringName applied_status_id { get; set; } = "";
    public int applied_status_duration_tu { get; set; }
    public StringName render_overlay_id { get; set; } = "";
    public int overlay_priority { get; set; }
    public string display_name { get; set; } = "";
    public BattleAttackRollModifierSpec accuracy_modifier_spec { get; set; }
    public StringName does_not_stack_with_status_id { get; set; } = "";
    public List<StringName> does_not_stack_with_status_ids { get; set; } = new();
    public StringName contact_status_id { get; set; } = "";
    public int contact_status_duration_tu { get; set; }
    public StringName contact_stack_behavior { get; set; } = "refresh";
    public int contact_stack_limit { get; set; }
    public string contact_status_display_label { get; set; } = "";
    public bool contact_counts_as_debuff_override { get; set; }
    public bool contact_counts_as_debuff { get; set; }
    public bool contact_undispellable { get; set; }
    public bool contact_dispellable_magic { get; set; }
    public bool contact_dispellable_harmful_magic { get; set; }
    public bool contact_dispellable_beneficial_magic { get; set; }
    public int contact_save_dc { get; set; }
    public StringName contact_save_ability { get; set; } = "";
    public StringName contact_save_tag { get; set; } = "";
    public bool contact_apply_on_save_failure { get; set; }
    public int contact_tick_interval_tu { get; set; }
    public int contact_timeline_damage_dice_count { get; set; }
    public int contact_timeline_damage_dice_sides { get; set; }
    public int contact_timeline_damage_flat_bonus { get; set; }
    public StringName contact_blocked_by_trait_id { get; set; } = "";
    public StringName source_unit_id { get; set; } = "";
    public StringName source_skill_id { get; set; } = "";
    public StringName target_team_filter { get; set; } = "any";
    public int power { get; set; }
    public StringName damage_tag { get; set; } = "";
    public int remaining_tu { get; set; }
    public int tick_interval_tu { get; set; }
    public int next_tick_at_tu { get; set; }
    public StringName stack_behavior { get; set; } = "refresh";
    private readonly Dictionary<string, object> _params = new(System.StringComparer.Ordinal);
    public GDictionary @params
    {
        get => RuntimePlainPayload.ProjectDictionary(
            _params,
            "BattleTerrainEffectState.@params"
        );
        set => ReplaceParams(value);
    }

    internal Dictionary<string, object> GetParamsTyped()
    {
        return RuntimePlainPayload.NormalizeDictionary(
            BuildParamsProjection(),
            "BattleTerrainEffectState.GetParamsTyped"
        );
    }

    internal static GDictionary CopyResidualParams(GDictionary parameters)
    {
        GDictionary residual = RuntimePayloadCopy.Dictionary(
            parameters,
            "BattleTerrainEffectState.CopyResidualParams"
        );
        RemoveFormalParamKeys(residual);
        return residual;
    }

    internal static GDictionary CopyResidualParams(
        IReadOnlyDictionary<string, Variant> parameters
    )
    {
        var residual = new GDictionary();
        if (parameters != null)
        {
            foreach (KeyValuePair<string, Variant> entry in parameters)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    residual[entry.Key] = entry.Value;
                }
            }
        }
        RemoveFormalParamKeys(residual);
        return residual;
    }

    internal static GDictionary CopyResidualParamsForOwnedTransient(GDictionary parameters)
    {
        GDictionary residual = parameters != null ? (GDictionary)parameters.Duplicate(true) : new GDictionary();
        RemoveFormalParamKeys(residual);
        return residual;
    }

    private static void RemoveFormalParamKeys(GDictionary residual)
    {
        if (residual == null)
            return;
        foreach (string key in FormalParamKeys)
        {
            if (residual.ContainsKey(key))
                residual.Remove(key);
        }
    }

    internal GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["field_instance_id"] = field_instance_id.ToString(),
            ["effect_id"] = effect_id.ToString(),
            ["effect_type"] = effect_type.ToString(),
            ["applied_status_id"] = applied_status_id.ToString(),
            ["applied_status_duration_tu"] = applied_status_duration_tu,
            ["render_overlay_id"] = render_overlay_id.ToString(),
            ["overlay_priority"] = overlay_priority,
            ["source_unit_id"] = source_unit_id.ToString(),
            ["source_skill_id"] = source_skill_id.ToString(),
            ["target_team_filter"] = target_team_filter.ToString(),
            ["power"] = power,
            ["damage_tag"] = damage_tag.ToString(),
            ["remaining_tu"] = remaining_tu,
            ["tick_interval_tu"] = tick_interval_tu,
            ["next_tick_at_tu"] = next_tick_at_tu,
            ["stack_behavior"] = stack_behavior.ToString(),
            ["params"] = BuildParamsProjection(),
        };
    }

    internal static BattleTerrainEffectState FromDictionary(GDictionary typedData)
    {
        if (typedData == null)
            return null;
        if (!HasExactSerializedFields(typedData))
        {
            return null;
        }

        foreach (string fieldName in RequiredNonEmptyStringFields)
        {
            if (!TryGetStringLike(typedData, fieldName, out string value)
                || string.IsNullOrEmpty(value))
            {
                return null;
            }
        }
        foreach (string fieldName in OptionalStringFields)
        {
            if (!TryGetStringLike(typedData, fieldName, out _))
            {
                return null;
            }
        }

        StringName targetTeamFilter = GetStringName(typedData, "target_team_filter");
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(targetTeamFilter))
        {
            return null;
        }

        foreach (string fieldName in IntegerFields)
        {
            if (!TryGetStrictInt(typedData, fieldName, out _))
            {
                return null;
            }
        }
        foreach (string fieldName in NonNegativeIntegerFields)
        {
            if (GetInt(typedData, fieldName) < 0)
            {
                return null;
            }
        }
        if (!TryGetDictionary(typedData, "params", out GDictionary parameters))
        {
            return null;
        }

        return new BattleTerrainEffectState
        {
            field_instance_id = GetStringName(typedData, "field_instance_id"),
            effect_id = GetStringName(typedData, "effect_id"),
            effect_type = GetStringName(typedData, "effect_type"),
            applied_status_id = GetStringName(typedData, "applied_status_id"),
            applied_status_duration_tu = GetInt(typedData, "applied_status_duration_tu"),
            render_overlay_id = GetStringName(typedData, "render_overlay_id"),
            overlay_priority = GetInt(typedData, "overlay_priority"),
            display_name = GetString(parameters, "display_name"),
            accuracy_modifier_spec = ParseAccuracyModifierSpec(parameters),
            does_not_stack_with_status_id = GetStringName(
                parameters,
                "does_not_stack_with_status_id"
            ),
            does_not_stack_with_status_ids = ReadStringNameList(parameters, "does_not_stack_with_status_ids"),
            contact_status_id = GetStringName(parameters, "contact_status_id"),
            contact_status_duration_tu = GetInt(parameters, "contact_status_duration_tu"),
            contact_stack_behavior = GetStringName(parameters, "contact_stack_behavior"),
            contact_stack_limit = GetInt(parameters, "contact_stack_limit"),
            contact_status_display_label = GetString(parameters, "contact_status_display_label"),
            contact_counts_as_debuff_override = GetBool(parameters, "contact_counts_as_debuff_override"),
            contact_counts_as_debuff = GetBool(parameters, "contact_counts_as_debuff"),
            contact_undispellable = GetBool(parameters, "contact_undispellable"),
            contact_dispellable_magic = GetBool(parameters, "contact_dispellable_magic"),
            contact_dispellable_harmful_magic = GetBool(parameters, "contact_dispellable_harmful_magic"),
            contact_dispellable_beneficial_magic = GetBool(parameters, "contact_dispellable_beneficial_magic"),
            contact_save_dc = GetInt(parameters, "contact_save_dc"),
            contact_save_ability = GetStringName(parameters, "contact_save_ability"),
            contact_save_tag = GetStringName(parameters, "contact_save_tag"),
            contact_apply_on_save_failure = GetBool(parameters, "contact_apply_on_save_failure"),
            contact_tick_interval_tu = GetInt(parameters, "contact_tick_interval_tu"),
            contact_timeline_damage_dice_count = GetInt(parameters, "contact_timeline_damage_dice_count"),
            contact_timeline_damage_dice_sides = GetInt(parameters, "contact_timeline_damage_dice_sides"),
            contact_timeline_damage_flat_bonus = GetInt(parameters, "contact_timeline_damage_flat_bonus"),
            contact_blocked_by_trait_id = GetStringName(parameters, "contact_blocked_by_trait_id"),
            source_unit_id = GetStringName(typedData, "source_unit_id"),
            source_skill_id = GetStringName(typedData, "source_skill_id"),
            target_team_filter = targetTeamFilter,
            power = GetInt(typedData, "power"),
            damage_tag = GetStringName(typedData, "damage_tag"),
            remaining_tu = GetInt(typedData, "remaining_tu"),
            tick_interval_tu = GetInt(typedData, "tick_interval_tu"),
            next_tick_at_tu = GetInt(typedData, "next_tick_at_tu"),
            stack_behavior = GetStringName(typedData, "stack_behavior"),
            lifetime_policy = ReadLifetimePolicy(parameters),
            move_cost_delta = GetInt(parameters, "move_cost_delta"),
            @params = CopyResidualParams(parameters),
        };
    }

    internal static Godot.Collections.Array<GDictionary> ToDictionaryArray(
        IEnumerable<BattleTerrainEffectState> effect_states
    )
    {
        var payloads = new Godot.Collections.Array<GDictionary>();
        foreach (BattleTerrainEffectState effectState in effect_states ?? new List<BattleTerrainEffectState>())
        {
            if (effectState != null)
            {
                payloads.Add(effectState.ToDictionary());
            }
        }
        return payloads;
    }

    internal static List<BattleTerrainEffectState> FromDictionaryArray(
        Godot.Collections.Array<GDictionary> values
    )
    {
        if (values == null)
        {
            return null;
        }
        var effectStates = new List<BattleTerrainEffectState>();
        foreach (GDictionary value in values)
        {
            BattleTerrainEffectState effectState = FromDictionary(value);
            if (effectState == null)
            {
                return null;
            }
            effectStates.Add(effectState);
        }
        return effectStates;
    }

    internal static List<BattleTerrainEffectState> DuplicateList(
        IEnumerable<BattleTerrainEffectState> effect_states
    )
    {
        List<BattleTerrainEffectState> duplicated = FromDictionaryArray(
            ToDictionaryArray(effect_states)
        );
        return duplicated ?? new List<BattleTerrainEffectState>();
    }

    private static bool HasExactSerializedFields(GDictionary data)
    {
        if (data.Count != SerializedFieldNames.Length)
        {
            return false;
        }
        foreach (string fieldName in SerializedFieldNames)
        {
            if (!data.ContainsKey(fieldName))
            {
                return false;
            }
        }
        return true;
    }

    private GDictionary BuildParamsProjection()
    {
        GDictionary projected = RuntimePlainPayload.ProjectDictionary(
            _params,
            "BattleTerrainEffectState.BuildParamsProjection"
        );
        projected["lifetime_policy"] = lifetime_policy.ToString();
        projected["move_cost_delta"] = move_cost_delta;
        if (render_overlay_id != "")
        {
            projected["render_overlay_id"] = render_overlay_id.ToString();
        }
        if (overlay_priority != 0)
        {
            projected["overlay_priority"] = overlay_priority;
        }
        if (!string.IsNullOrEmpty(display_name))
        {
            projected["display_name"] = display_name;
        }
        if (accuracy_modifier_spec != null)
        {
            projected["accuracy_modifier_spec"] =
                BattleAttackRollModifierProjection.ProjectPartialSpec(accuracy_modifier_spec);
        }
        if (does_not_stack_with_status_id != "")
        {
            projected["does_not_stack_with_status_id"] = does_not_stack_with_status_id.ToString();
        }
        if (does_not_stack_with_status_ids.Count > 0)
        {
            projected["does_not_stack_with_status_ids"] =
                ProgressionDataUtils.string_name_array_to_string_array(
                    does_not_stack_with_status_ids
                );
        }
        if (contact_status_id != "")
        {
            projected["contact_status_id"] = contact_status_id.ToString();
        }
        if (contact_status_duration_tu > 0)
        {
            projected["contact_status_duration_tu"] = contact_status_duration_tu;
        }
        if (contact_stack_behavior != "")
        {
            projected["contact_stack_behavior"] = contact_stack_behavior.ToString();
        }
        if (contact_stack_limit > 0)
        {
            projected["contact_stack_limit"] = contact_stack_limit;
        }
        if (!string.IsNullOrEmpty(contact_status_display_label))
        {
            projected["contact_status_display_label"] = contact_status_display_label;
        }
        if (contact_counts_as_debuff_override)
        {
            projected["contact_counts_as_debuff_override"] = true;
            projected["contact_counts_as_debuff"] = contact_counts_as_debuff;
        }
        if (contact_undispellable)
        {
            projected["contact_undispellable"] = true;
        }
        if (contact_dispellable_magic)
        {
            projected["contact_dispellable_magic"] = true;
        }
        if (contact_dispellable_harmful_magic)
        {
            projected["contact_dispellable_harmful_magic"] = true;
        }
        if (contact_dispellable_beneficial_magic)
        {
            projected["contact_dispellable_beneficial_magic"] = true;
        }
        if (contact_save_dc > 0)
        {
            projected["contact_save_dc"] = contact_save_dc;
        }
        if (contact_save_ability != "")
        {
            projected["contact_save_ability"] = contact_save_ability.ToString();
        }
        if (contact_save_tag != "")
        {
            projected["contact_save_tag"] = contact_save_tag.ToString();
        }
        if (contact_apply_on_save_failure)
        {
            projected["contact_apply_on_save_failure"] = contact_apply_on_save_failure;
        }
        if (contact_tick_interval_tu > 0)
        {
            projected["contact_tick_interval_tu"] = contact_tick_interval_tu;
        }
        if (contact_timeline_damage_dice_count > 0)
        {
            projected["contact_timeline_damage_dice_count"] =
                contact_timeline_damage_dice_count;
        }
        if (contact_timeline_damage_dice_sides > 0)
        {
            projected["contact_timeline_damage_dice_sides"] =
                contact_timeline_damage_dice_sides;
        }
        if (contact_timeline_damage_flat_bonus > 0)
        {
            projected["contact_timeline_damage_flat_bonus"] =
                contact_timeline_damage_flat_bonus;
        }
        if (contact_blocked_by_trait_id != "")
        {
            projected["contact_blocked_by_trait_id"] = contact_blocked_by_trait_id.ToString();
        }
        return projected;
    }

    private void ReplaceParams(GDictionary values)
    {
        _params.Clear();
        foreach (
            KeyValuePair<string, object> entry in RuntimePlainPayload.NormalizeDictionary(
                values ?? new GDictionary(),
                "BattleTerrainEffectState.@params"
            )
        )
        {
            if (!string.IsNullOrEmpty(entry.Key))
                _params[entry.Key] = entry.Value;
        }
    }

    private static string GetString(GDictionary payload, string key)
    {
        return TryGetStringLike(payload, key, out string value) ? value : "";
    }

    private static StringName GetStringName(GDictionary payload, string key)
    {
        return new StringName(GetString(payload, key));
    }

    private static StringName ReadLifetimePolicy(GDictionary parameters)
    {
        StringName lifetimePolicy = GetStringName(parameters, "lifetime_policy");
        return lifetimePolicy != "" ? lifetimePolicy : new StringName("timed");
    }

    private static BattleAttackRollModifierSpec ParseAccuracyModifierSpec(GDictionary parameters)
    {
        if (!TryGetDictionary(parameters, "accuracy_modifier_spec", out GDictionary rawSpec))
        {
            return null;
        }
        return BattleAttackRollModifierSpec.FromPartialDictionary(rawSpec);
    }

    private static int GetInt(GDictionary payload, string key)
    {
        return TryGetStrictInt(payload, key, out int value) ? value : 0;
    }

    private static bool GetBool(GDictionary payload, string key)
    {
        return TryGetStrictBool(payload, key, out bool value) && value;
    }

    private static bool TryGetStringLike(GDictionary payload, string key, out string value)
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsStringLike(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(GDictionary payload, string key, out int value)
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetStrictBool(GDictionary payload, string key, out bool value)
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsStrictBool(rawValue, out value))
        {
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetDictionary(GDictionary payload, string key, out GDictionary value)
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsDictionary(rawValue, out value))
        {
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsStrictBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryGetExactValue(GDictionary payload, string key, out object value)
    {
        if (payload != null && payload.ContainsKey(key))
        {
            value = payload[key];
            return true;
        }
        value = null;
        return false;
    }

    private static List<StringName> ReadStringNameList(GDictionary payload, string key)
    {
        if (!TryGetExactValue(payload, key, out object rawValue))
        {
            return new List<StringName>();
        }
        GArray values = rawValue switch
        {
            Variant variant when variant.VariantType == Variant.Type.Array => variant.AsGodotArray(),
            GArray array => array,
            _ => null,
        };
        if (values == null)
        {
            return new List<StringName>();
        }
        return BuildStringNameList(ProgressionDataUtils.to_string_name_array(values));
    }

    private static List<StringName> BuildStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }
}
