using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗地形效果状态数据。
// 翻译自 battle_terrain_effect_state.gd（2026-05-24，数据层 C# 迁移）。
[GlobalClass]
public partial class BattleTerrainEffectState : RefCounted
{
    private static readonly string[] SerializedFieldNames =
    {
        "field_instance_id",
        "effect_id",
        "effect_type",
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
        "source_unit_id",
        "source_skill_id",
        "damage_tag",
    };

    private static readonly string[] IntegerFields =
    {
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

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["field_instance_id"] = field_instance_id.ToString(),
            ["effect_id"] = effect_id.ToString(),
            ["effect_type"] = effect_type.ToString(),
            ["source_unit_id"] = source_unit_id.ToString(),
            ["source_skill_id"] = source_skill_id.ToString(),
            ["target_team_filter"] = target_team_filter.ToString(),
            ["power"] = power,
            ["damage_tag"] = damage_tag.ToString(),
            ["remaining_tu"] = remaining_tu,
            ["tick_interval_tu"] = tick_interval_tu,
            ["next_tick_at_tu"] = next_tick_at_tu,
            ["stack_behavior"] = stack_behavior.ToString(),
            ["params"] = @params.Duplicate(true),
        };
    }

    public static BattleTerrainEffectState from_dict(GDictionary typedData)
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
        if (!CombatTargetTeamContentRules.is_valid_skill_target_team_filter(targetTeamFilter))
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
            source_unit_id = GetStringName(typedData, "source_unit_id"),
            source_skill_id = GetStringName(typedData, "source_skill_id"),
            target_team_filter = targetTeamFilter,
            power = GetInt(typedData, "power"),
            damage_tag = GetStringName(typedData, "damage_tag"),
            remaining_tu = GetInt(typedData, "remaining_tu"),
            tick_interval_tu = GetInt(typedData, "tick_interval_tu"),
            next_tick_at_tu = GetInt(typedData, "next_tick_at_tu"),
            stack_behavior = GetStringName(typedData, "stack_behavior"),
            @params = parameters.Duplicate(true),
        };
    }

    public static Godot.Collections.Array<GDictionary> to_dict_array(GArray effect_states)
    {
        var payloads = new Godot.Collections.Array<GDictionary>();
        foreach (object effectStateValue in effect_states ?? new GArray())
        {
            if (!TryAsObject(effectStateValue, out BattleTerrainEffectState effectState))
            {
                continue;
            }
            payloads.Add(effectState.to_dict());
        }
        return payloads;
    }

    public static Godot.Collections.Array<GDictionary> to_dict_array(
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
                payloads.Add(effectState.to_dict());
            }
        }
        return payloads;
    }

    public static Godot.Collections.Array<BattleTerrainEffectState> from_dict_array(
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
            BattleTerrainEffectState effectState = from_dict(value);
            if (effectState == null)
            {
                return null;
            }
            effectStates.Add(effectState);
        }
        return effectStates;
    }

    public static Godot.Collections.Array<BattleTerrainEffectState> duplicate_array(
        GArray effect_states
    )
    {
        Godot.Collections.Array<BattleTerrainEffectState> duplicated = from_dict_array(
            to_dict_array(effect_states)
        );
        return duplicated ?? new Godot.Collections.Array<BattleTerrainEffectState>();
    }

    public static Godot.Collections.Array<BattleTerrainEffectState> duplicate_array(
        Godot.Collections.Array<BattleTerrainEffectState> effect_states
    )
    {
        Godot.Collections.Array<BattleTerrainEffectState> duplicated = from_dict_array(
            to_dict_array(effect_states)
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

    private static string GetString(GDictionary payload, string key)
    {
        return TryGetStringLike(payload, key, out string value) ? value : "";
    }

    private static StringName GetStringName(GDictionary payload, string key)
    {
        return new StringName(GetString(payload, key));
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
}
