using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

public static class SkillLevelDescriptionFormatter
{
    public static string BuildLevelDescription(
        SkillDefinition skillDefinition,
        int level,
        Godot.Collections.Dictionary runtimeContext = null
    )
    {
        if (
            skillDefinition == null
            || skillDefinition.LevelDescriptionTemplate.Length == 0
        )
            return "";
        var config = new Dictionary<string, Variant>(StringComparer.Ordinal);
        if (skillDefinition.LevelDescriptionConfigs.TryGetValue(level, out var levelConfig))
            MergeVariantMap(config, levelConfig, overwrite: true);
        _merge_matching_effect_params(config, skillDefinition, level);
        _merge_matching_effect_typed_fields(config, skillDefinition, level);
        _merge_level_overrides(config, skillDefinition, level);
        _resolve_charge_distance(config, level);
        if (runtimeContext != null)
            MergeVariantMap(config, runtimeContext, overwrite: true);
        _apply_description_derived_fields(config);
        if (config.Count == 0)
            return "";
        return RenderTemplate(skillDefinition.LevelDescriptionTemplate, config);
    }

    public static string RenderTemplate(string template, Godot.Collections.Dictionary config)
    {
        return RenderTemplate(template, ToVariantMap(config));
    }

    private static string RenderTemplate(string template, Dictionary<string, Variant> config)
    {
        string result = template;
        var condRegex = new Regex(@"\{\{\?([^}]+)\}\}(.*?)\{\{/\1\}\}", RegexOptions.Singleline);
        while (true)
        {
            var m = condRegex.Match(result);
            if (!m.Success)
                break;
            string key = m.Groups[1].Value.Trim();
            string inner = m.Groups[2].Value;
            result =
                config.ContainsKey(key) && _is_optional_value_visible(config, key)
                    ? result.Substring(0, m.Index) + inner + result.Substring(m.Index + m.Length)
                    : result.Substring(0, m.Index) + result.Substring(m.Index + m.Length);
        }
        var exprRegex = new Regex(@"\{=([^}]+)\}");
        while (true)
        {
            var m = exprRegex.Match(result);
            if (!m.Success)
                break;
            result =
                result.Substring(0, m.Index)
                + _eval_expression(m.Groups[1].Value.Trim(), config)
                + result.Substring(m.Index + m.Length);
        }
        var varRegex = new Regex(@"\{([^}]+)\}");
        while (true)
        {
            var m = varRegex.Match(result);
            if (!m.Success)
                break;
            string key = m.Groups[1].Value.Trim();
            string value = config.TryGetValue(key, out Variant rawValue) ? rawValue.AsString() : "";
            result = result.Substring(0, m.Index) + value + result.Substring(m.Index + m.Length);
        }
        return result;
    }

    private static Dictionary<string, Variant> ToVariantMap(Godot.Collections.Dictionary source)
    {
        var result = new Dictionary<string, Variant>(StringComparer.Ordinal);
        MergeVariantMap(result, source, overwrite: true);
        return result;
    }

    private static void MergeVariantMap(
        Dictionary<string, Variant> target,
        IReadOnlyDictionary<string, Variant> source,
        bool overwrite
    )
    {
        if (source == null)
            return;
        foreach ((string key, Variant value) in source)
        {
            if (!overwrite && target.ContainsKey(key))
                continue;
            target[key] = value;
        }
    }

    private static void MergeVariantMap(
        Dictionary<string, Variant> target,
        Godot.Collections.Dictionary source,
        bool overwrite
    )
    {
        if (source == null)
            return;
        foreach (var rawKey in source.Keys)
        {
            string key = rawKey.AsString();
            if (!overwrite && target.ContainsKey(key))
                continue;
            target[key] = source[rawKey];
        }
    }

    private static bool _is_optional_value_visible(Dictionary<string, Variant> config, string key)
    {
        var v = config[key];
        var t = v.VariantType;
        if (t == Variant.Type.Nil)
            return false;
        if (t == Variant.Type.Bool)
            return v.AsBool();
        if (t == Variant.Type.Int)
            return v.AsInt32() != 0;
        if (t == Variant.Type.Float)
        {
            double f = v.AsDouble();
            return !double.IsNaN(f) && !Mathf.IsEqualApprox((float)f, 0f);
        }
        if (t == Variant.Type.String || t == Variant.Type.StringName)
            return v.AsString().StripEdges().Length > 0;
        return v.AsString().StripEdges().Length > 0;
    }

    private static void _merge_matching_effect_params(
        Dictionary<string, Variant> config,
        SkillDefinition skillDefinition,
        int level
    )
    {
        foreach (CombatEffectDefinition effectDefinition in _collect_level_effect_defs(
            skillDefinition,
            level
        ))
        {
            if (effectDefinition?.Parameters == null)
                continue;
            foreach ((string paramKey, Variant value) in effectDefinition.Parameters)
            {
                if (!config.ContainsKey(paramKey))
                    config[paramKey] = value;
            }
        }
    }

    private static void _merge_matching_effect_typed_fields(
        Dictionary<string, Variant> config,
        SkillDefinition skillDefinition,
        int level
    )
    {
        foreach (var ed in _collect_level_effect_defs(skillDefinition, level))
        {
            if (ed == null)
                continue;
            BattleEffectKind effectKind = ed.EffectKind;
            if (effectKind == BattleEffectKind.Damage)
                _merge_damage_effect_typed_fields(config, ed);
            else if (
                effectKind == BattleEffectKind.Heal
                || effectKind == BattleEffectKind.StaminaRestore
                || effectKind == BattleEffectKind.Shield
            )
                _merge_attribute_scaled_dice_effect_typed_fields(config, ed);
            else if (
                effectKind == BattleEffectKind.Status
                || effectKind == BattleEffectKind.ApplyStatus
            )
                _merge_status_effect_typed_fields(config, ed);
            else if (effectKind == BattleEffectKind.ForcedMove)
            {
                if (ed.ForcedMoveModeKind != BattleForcedMoveMode.Unknown)
                    _set_if_missing(config, "forced_move_mode", (string)ed.ForcedMoveMode);
                if (ed.ForcedMoveDistance > 0)
                    _set_if_missing(config, "forced_move_distance", ed.ForcedMoveDistance);
            }
        }
    }

    private static List<CombatEffectDefinition> _collect_level_effect_defs(
        SkillDefinition skillDefinition,
        int level
    )
    {
        var r = new List<CombatEffectDefinition>();
        CombatSkillDefinition profile = skillDefinition?.CombatProfile;
        if (profile == null)
            return r;
        _append_level_effect_defs(r, profile.EffectDefinitions, level);
        foreach (var cv in profile.GetUnlockedCastVariants(level))
        {
            if (cv != null)
                _append_level_effect_defs(r, cv.EffectDefinitions, level);
        }
        return r;
    }

    private static void _append_level_effect_defs(
        List<CombatEffectDefinition> output,
        IReadOnlyList<CombatEffectDefinition> effectDefs,
        int level
    )
    {
        if (effectDefs == null)
            return;
        foreach (var ed in effectDefs)
        {
            if (ed != null && _effect_unlocked_at_level(ed, level))
                output.Add(ed);
        }
    }

    private static bool _effect_unlocked_at_level(CombatEffectDefinition ed, int level)
    {
        if (ed == null)
            return false;
        if (level < Mathf.Max(ed.MinSkillLevel, 0))
            return false;
        return ed.MaxSkillLevel < 0 || level <= ed.MaxSkillLevel;
    }

    private static void _merge_damage_effect_typed_fields(
        Dictionary<string, Variant> config,
        CombatEffectDefinition ed
    )
    {
        if (ed.Power != 0)
            _set_if_missing(config, "damage_power", ed.Power);
        if (ed.DamageRatioPercent != 100)
            _set_if_missing(config, "damage_ratio_percent", ed.DamageRatioPercent);
        if (ed.DamageTag != "")
            _set_if_missing(config, "damage_tag", (string)ed.DamageTag);
        _merge_save_fields(config, "damage", ed);
    }

    private static void _merge_attribute_scaled_dice_effect_typed_fields(
        Dictionary<string, Variant> config,
        CombatEffectDefinition ed
    )
    {
        if (ed.DiceCount > 0)
        {
            _set_if_missing(config, "dice_count", ed.DiceCount);
            if (ed.EffectKind == BattleEffectKind.Heal)
                _set_if_missing(config, "heal", ed.DiceCount);
        }
        if (ed.DiceSidesBase > 0)
            _set_if_missing(config, "dice_sides_base", ed.DiceSidesBase);
        if (ed.DiceSidesPerConstitutionMod > 0)
            _set_if_missing(
                config,
                "dice_sides_per_constitution_mod",
                ed.DiceSidesPerConstitutionMod
            );
        if (ed.DiceSidesPerWillpowerMod > 0)
            _set_if_missing(
                config,
                "dice_sides_per_willpower_mod",
                ed.DiceSidesPerWillpowerMod
            );
        if (ed.DiceSides > 0)
            _set_if_missing(config, "dice_sides", ed.DiceSides);
        if (ed.DiceBonus != 0)
            _set_if_missing(config, "dice_bonus", ed.DiceBonus);
    }

    private static void _merge_status_effect_typed_fields(
        Dictionary<string, Variant> config,
        CombatEffectDefinition ed
    )
    {
        string statusId = (string)ed.StatusId;
        if (statusId.Length == 0)
            return;
        string label = _format_status_label(ed.StatusId);
        _set_if_missing(config, "status_id", statusId);
        _set_if_missing(config, "status_display_name", label);
        if (ed.DurationTu > 0)
            _set_if_missing(config, "status_duration_tu", ed.DurationTu);
        if (ed.Power != 0)
            _set_if_missing(config, "status_power", ed.Power);
        _set_if_missing(config, $"{statusId}_status_id", statusId);
        _set_if_missing(config, $"{statusId}_display_name", label);
        if (ed.DurationTu > 0)
            _set_if_missing(config, $"{statusId}_duration_tu", ed.DurationTu);
        if (ed.Power != 0)
            _set_if_missing(config, $"{statusId}_power", ed.Power);
        _merge_save_fields(config, "status", ed);
        _merge_save_fields(config, statusId, ed);
    }

    private static void _merge_save_fields(
        Dictionary<string, Variant> config,
        string prefix,
        CombatEffectDefinition ed
    )
    {
        if (prefix.Length == 0 || ed?.SaveAbility == "")
            return;
        string saveAbility = (string)ed.SaveAbility;
        string saveLabel = _format_attribute_label(ed.SaveAbility);
        _set_if_missing(config, $"{prefix}_save_ability", saveAbility);
        _set_if_missing(config, $"{prefix}_save_ability_label", saveLabel);
        _set_if_missing(config, $"{prefix}_save_text", _format_save_text(ed, saveLabel));
    }

    private static string _format_save_text(
        CombatEffectDefinition ed,
        string saveLabel
    )
    {
        if (ed == null)
            return "";
        if (ed.EffectKind == BattleEffectKind.Damage && ed.SavePartialOnSuccess)
            return $"{saveLabel}豁免成功时伤害减半";
        if (
            (
                ed.EffectKind == BattleEffectKind.Status
                || ed.EffectKind == BattleEffectKind.ApplyStatus
            )
            && ed.StatusId != ""
        )
            return $"{saveLabel}豁免失败时附加{_format_status_label(ed.StatusId)}";
        return $"{saveLabel}豁免";
    }

    private static string _format_attribute_label(StringName attrId)
    {
        string a = (string)attrId;
        if (a == "strength")
            return "力量";
        if (a == "agility")
            return "敏捷";
        if (a == "constitution")
            return "体质";
        if (a == "perception")
            return "感知";
        if (a == "intelligence")
            return "智力";
        if (a == "willpower")
            return "意志";
        return a;
    }

    private static string _format_status_label(StringName sid)
    {
        string s = (string)sid;
        if (s == "shocked")
            return "感电";
        if (s == "burning")
            return "燃烧";
        if (s == "frozen")
            return "冻结";
        if (s == "slow")
            return "迟缓";
        if (s == "blind" || s == "blinded")
            return "失明";
        if (s == "rooted")
            return "定身";
        if (s == "staggered")
            return "踉跄";
        return s;
    }

    private static void _set_if_missing(Dictionary<string, Variant> config, string key, int value)
    {
        if (!config.ContainsKey(key))
            config[key] = Variant.From(value);
    }

    private static void _set_if_missing(
        Dictionary<string, Variant> config,
        string key,
        string value
    )
    {
        if (!config.ContainsKey(key))
            config[key] = Variant.From(value);
    }

    private static void _merge_level_overrides(
        Dictionary<string, Variant> config,
        SkillDefinition skillDefinition,
        int level
    )
    {
        if (skillDefinition?.CombatProfile == null)
            return;
        CombatSkillDefinition profile = skillDefinition.CombatProfile;
        CombatSkillResourceCosts costs = profile.GetEffectiveResourceCostValues(level);
        var fields = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "ap_cost", costs.ApCost },
            { "mp_cost", costs.MpCost },
            { "stamina_cost", costs.StaminaCost },
            { "cooldown_tu", costs.CooldownTu },
            { "attack_roll_bonus", profile.GetEffectiveAttackRollBonus(level) },
            { "aura_cost", costs.AuraCost },
            { "range_value", profile.GetEffectiveRangeValue(level) },
            { "area_value", profile.GetEffectiveAreaValue(level) },
        };
        foreach ((string fieldKey, int value) in fields)
        {
            if (!config.ContainsKey(fieldKey))
                config[fieldKey] = Variant.From(value);
        }
    }

    private static void _resolve_charge_distance(Dictionary<string, Variant> config, int level)
    {
        if (config.ContainsKey("distance"))
            return;
        if (!config.ContainsKey("base_distance") && !config.ContainsKey("distance_by_level"))
            return;
        int baseDist = config.TryGetValue("base_distance", out Variant baseDistance)
            ? baseDistance.AsInt32()
            : 0;
        Variant dbl = config.TryGetValue("distance_by_level", out Variant distanceByLevel)
            ? distanceByLevel
            : default;
        if (dbl.VariantType != Variant.Type.Dictionary)
        {
            config["distance"] = Variant.From(baseDist);
            return;
        }
        int dist = baseDist;
        var keys = new List<int>();
        foreach (var k in dbl.AsGodotDictionary().Keys)
            keys.Add(k.AsString().ToInt());
        keys.Sort();
        foreach (int k in keys)
        {
            if (k > level)
                break;
            dist = dbl.AsGodotDictionary()[k.ToString()].AsInt32();
        }
        config["distance"] = Variant.From(dist);
    }

    private static void _apply_description_derived_fields(Dictionary<string, Variant> config)
    {
        if (config.ContainsKey("dice_sides_base"))
        {
            int bs = config["dice_sides_base"].AsInt32();
            int cm = config.ContainsKey("con_mod") ? config["con_mod"].AsInt32() : 0;
            int wm = config.ContainsKey("will_mod") ? config["will_mod"].AsInt32() : 0;
            int cms = config.ContainsKey("dice_sides_per_constitution_mod")
                ? config["dice_sides_per_constitution_mod"].AsInt32()
                : 0;
            int wms = config.ContainsKey("dice_sides_per_willpower_mod")
                ? config["dice_sides_per_willpower_mod"].AsInt32()
                : 0;
            config["dice_sides"] = Variant.From(Mathf.Max(bs + cm * cms + wm * wms, 4));
        }
    }

    private static string _eval_expression(string exprStr, Dictionary<string, Variant> variables)
    {
        var expr = new Godot.Expression();
        var inputNames = new List<string>();
        var inputValues = new Godot.Collections.Array();
        foreach ((string key, Variant value) in variables)
        {
            inputNames.Add(key);
            if (value.VariantType == Variant.Type.String)
            {
                string vs = value.AsString();
                inputValues.Add(
                    vs.IsValidInt()
                        ? Variant.From(vs.ToInt())
                        : (vs.IsValidFloat() ? Variant.From(vs.ToFloat()) : value)
                );
            }
            else
                inputValues.Add(value);
        }
        if (expr.Parse(exprStr, inputNames.ToArray()) != Error.Ok)
            return "{=" + exprStr + "}";
        var er = expr.Execute(inputValues);
        if (expr.HasExecuteFailed())
            return "{=" + exprStr + "}";
        if (
            er.VariantType == Variant.Type.Float
            && er.AsDouble() == System.Math.Floor(er.AsDouble())
        )
            return ((int)er.AsDouble()).ToString();
        return er.AsString();
    }
}
