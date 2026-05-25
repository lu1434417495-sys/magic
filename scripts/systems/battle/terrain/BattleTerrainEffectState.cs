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

    public static BattleTerrainEffectState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary typedData = data.AsGodotDictionary();
        if (!HasExactSerializedFields(typedData))
        {
            return null;
        }

        foreach (string fieldName in RequiredNonEmptyStringFields)
        {
            Variant value = Get(typedData, fieldName);
            if (!IsStringLike(value))
            {
                return null;
            }
            if (string.IsNullOrEmpty(value.AsString()))
            {
                return null;
            }
        }
        foreach (string fieldName in OptionalStringFields)
        {
            if (!IsStringLike(Get(typedData, fieldName)))
            {
                return null;
            }
        }

        StringName targetTeamFilter = ToStringName(Get(typedData, "target_team_filter"));
        if (!CombatTargetTeamContentRules.is_valid_skill_target_team_filter(targetTeamFilter))
        {
            return null;
        }

        foreach (string fieldName in IntegerFields)
        {
            if (Get(typedData, fieldName).VariantType != Variant.Type.Int)
            {
                return null;
            }
        }
        foreach (string fieldName in NonNegativeIntegerFields)
        {
            if (Get(typedData, fieldName).AsInt32() < 0)
            {
                return null;
            }
        }
        Variant rawParams = Get(typedData, "params");
        if (rawParams.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        return new BattleTerrainEffectState
        {
            field_instance_id = ToStringName(Get(typedData, "field_instance_id")),
            effect_id = ToStringName(Get(typedData, "effect_id")),
            effect_type = ToStringName(Get(typedData, "effect_type")),
            source_unit_id = ToStringName(Get(typedData, "source_unit_id")),
            source_skill_id = ToStringName(Get(typedData, "source_skill_id")),
            target_team_filter = targetTeamFilter,
            power = Get(typedData, "power").AsInt32(),
            damage_tag = ToStringName(Get(typedData, "damage_tag")),
            remaining_tu = Get(typedData, "remaining_tu").AsInt32(),
            tick_interval_tu = Get(typedData, "tick_interval_tu").AsInt32(),
            next_tick_at_tu = Get(typedData, "next_tick_at_tu").AsInt32(),
            stack_behavior = ToStringName(Get(typedData, "stack_behavior")),
            @params = rawParams.AsGodotDictionary().Duplicate(true),
        };
    }

    public static Godot.Collections.Array<GDictionary> to_dict_array(GArray effect_states)
    {
        var payloads = new Godot.Collections.Array<GDictionary>();
        foreach (Variant effectStateVariant in effect_states ?? new GArray())
        {
            var effectState = effectStateVariant.AsGodotObject() as BattleTerrainEffectState;
            if (effectState == null)
            {
                continue;
            }
            payloads.Add(effectState.to_dict());
        }
        return payloads;
    }

    public static Godot.Collections.Array<GDictionary> to_dict_array(Godot.Collections.Array<BattleTerrainEffectState> effect_states)
    {
        var payloads = new Godot.Collections.Array<GDictionary>();
        foreach (BattleTerrainEffectState effectState in effect_states ?? new Godot.Collections.Array<BattleTerrainEffectState>())
        {
            if (effectState != null)
            {
                payloads.Add(effectState.to_dict());
            }
        }
        return payloads;
    }

    public static Godot.Collections.Array<BattleTerrainEffectState> from_dict_array(Variant values)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        var effectStates = new Godot.Collections.Array<BattleTerrainEffectState>();
        foreach (Variant value in values.AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary)
            {
                return null;
            }
            BattleTerrainEffectState effectState = from_dict(value);
            if (effectState == null)
            {
                return null;
            }
            effectStates.Add(effectState);
        }
        return effectStates;
    }

    public static Godot.Collections.Array<BattleTerrainEffectState> duplicate_array(GArray effect_states)
    {
        Godot.Collections.Array<BattleTerrainEffectState> duplicated = from_dict_array(to_dict_array(effect_states));
        return duplicated ?? new Godot.Collections.Array<BattleTerrainEffectState>();
    }

    public static Godot.Collections.Array<BattleTerrainEffectState> duplicate_array(Godot.Collections.Array<BattleTerrainEffectState> effect_states)
    {
        Godot.Collections.Array<BattleTerrainEffectState> duplicated = from_dict_array(to_dict_array(effect_states));
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

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static StringName ToStringName(Variant value)
    {
        return IsStringLike(value) ? new StringName(value.AsString()) : "";
    }

    private static Variant Get(GDictionary payload, string key)
    {
        return payload.ContainsKey(key) ? payload[key] : default;
    }
}
