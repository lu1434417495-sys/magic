using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗地形效果状态数据。
// 翻译自 battle_terrain_effect_state.gd（2026-05-24，数据层 C# 迁移）。
public partial class BattleTerrainEffectState : RefCounted
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
    public StringName source_unit_id { get; set; } = "";
    public StringName source_skill_id { get; set; } = "";
    public StringName target_team_filter { get; set; } = "any";
    public int power { get; set; }
    public StringName damage_tag { get; set; } = "";
    public int remaining_tu { get; set; }
    public int tick_interval_tu { get; set; }
    public int next_tick_at_tu { get; set; }
    public StringName stack_behavior { get; set; } = "refresh";
    public GDictionary @params { get; set; } = new();

    internal Dictionary<string, object> GetParamsTyped()
    {
        return NormalizeDictionary(BuildParamsProjection());
    }

    internal static GDictionary CopyResidualParams(GDictionary parameters)
    {
        GDictionary residual = parameters?.Duplicate(true) ?? new GDictionary();
        foreach (string key in FormalParamKeys)
        {
            if (residual.ContainsKey(key))
            {
                residual.Remove(key);
            }
        }
        return residual;
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

    internal static Godot.Collections.Array<GDictionary> ToDictionaryArray(GArray effect_states)
    {
        var payloads = new Godot.Collections.Array<GDictionary>();
        foreach (object effectStateValue in effect_states ?? new GArray())
        {
            if (!TryAsObject(effectStateValue, out BattleTerrainEffectState effectState))
            {
                continue;
            }
            payloads.Add(effectState.ToDictionary());
        }
        return payloads;
    }

    internal static Godot.Collections.Array<GDictionary> ToDictionaryArray(
        Godot.Collections.Array<BattleTerrainEffectState> effect_states
    )
    {
        var payloads = new Godot.Collections.Array<GDictionary>();
        foreach (
            BattleTerrainEffectState effectState in effect_states
                ?? new Godot.Collections.Array<BattleTerrainEffectState>()
        )
        {
            if (effectState != null)
            {
                payloads.Add(effectState.ToDictionary());
            }
        }
        return payloads;
    }

    internal static Godot.Collections.Array<BattleTerrainEffectState> FromDictionaryArray(
        Godot.Collections.Array<GDictionary> values
    )
    {
        if (values == null)
        {
            return null;
        }
        var effectStates = new Godot.Collections.Array<BattleTerrainEffectState>();
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

    internal static Godot.Collections.Array<BattleTerrainEffectState> DuplicateArray(
        GArray effect_states
    )
    {
        Godot.Collections.Array<BattleTerrainEffectState> duplicated = FromDictionaryArray(
            ToDictionaryArray(effect_states)
        );
        return duplicated ?? new Godot.Collections.Array<BattleTerrainEffectState>();
    }

    internal static Godot.Collections.Array<BattleTerrainEffectState> DuplicateArray(
        Godot.Collections.Array<BattleTerrainEffectState> effect_states
    )
    {
        Godot.Collections.Array<BattleTerrainEffectState> duplicated = FromDictionaryArray(
            ToDictionaryArray(effect_states)
        );
        return duplicated ?? new Godot.Collections.Array<BattleTerrainEffectState>();
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
        GDictionary projected = @params?.Duplicate(true) ?? new GDictionary();
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
            projected["accuracy_modifier_spec"] = accuracy_modifier_spec.ToPartialDictionary();
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
        return projected;
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

    private static bool TryAsArray(object rawValue, out GArray value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is GArray array)
        {
            value = array;
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
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

    private static Dictionary<string, object> NormalizeDictionary(GDictionary source)
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (source == null)
        {
            return result;
        }
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            result[key] = NormalizeValue(source[rawKey]);
        }
        return result;
    }

    private static List<object> NormalizeArray(GArray source)
    {
        var result = new List<object>();
        if (source == null)
        {
            return result;
        }
        foreach (object rawValue in source)
        {
            result.Add(NormalizeValue(rawValue));
        }
        return result;
    }

    private static object NormalizeValue(object rawValue)
    {
        if (rawValue is Variant variantValue)
        {
            return NormalizeVariant(variantValue);
        }
        if (rawValue is GDictionary dictionaryValue)
        {
            return NormalizeDictionary(dictionaryValue);
        }
        if (rawValue is GArray arrayValue)
        {
            return NormalizeArray(arrayValue);
        }
        return rawValue;
    }

    private static object NormalizeVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => null,
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.Vector2I => value.AsVector2I(),
            Variant.Type.Vector2 => value.AsVector2(),
            Variant.Type.Vector3I => value.AsVector3I(),
            Variant.Type.Vector3 => value.AsVector3(),
            Variant.Type.Dictionary => NormalizeDictionary(value.AsGodotDictionary()),
            Variant.Type.Array => NormalizeArray(value.AsGodotArray()),
            _ => value.Obj,
        };
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
