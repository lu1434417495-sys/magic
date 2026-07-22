using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

public static class SkillLevelDescriptionFormatter
{
    private const string InvalidTemplateFieldText = "[描述配置错误]";

    private static readonly Regex ConditionalRegex = new(
        @"\{\{\?([^}]+)\}\}(.*?)\{\{/\1\}\}",
        RegexOptions.Singleline
    );
    private static readonly Regex ExpressionRegex = new(@"\{=([^}]*)\}");
    private static readonly Regex VariableRegex = new(@"\{([^}]+)\}");

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
        var config = new Dictionary<string, object>(StringComparer.Ordinal);
        if (skillDefinition.LevelDescriptionConfigs.TryGetValue(level, out var levelConfig))
            MergePlainMap(config, levelConfig, overwrite: true);
        _merge_matching_effect_params(config, skillDefinition, level);
        _merge_matching_effect_typed_fields(config, skillDefinition, level);
        _merge_level_overrides(config, skillDefinition, level);
        _resolve_charge_distance(config, level);
        if (runtimeContext != null)
            MergePlainMap(
                config,
                ContentValueNormalizer.NormalizeDictionary(
                    runtimeContext,
                    "SkillLevelDescriptionFormatter.runtime_context"
                ),
                overwrite: true
            );
        _apply_description_derived_fields(config);
        if (config.Count == 0)
            return "";
        return RenderTemplate(skillDefinition.LevelDescriptionTemplate, config);
    }

    public static string RenderTemplate(string template, Godot.Collections.Dictionary config)
    {
        return RenderTemplate(
            template,
            new Dictionary<string, object>(
                ContentValueNormalizer.NormalizeDictionary(
                    config,
                    "SkillLevelDescriptionFormatter.template_config"
                ),
                StringComparer.Ordinal
            )
        );
    }

    private static string RenderTemplate(string template, Dictionary<string, object> config)
    {
        string result = template;
        while (true)
        {
            var m = ConditionalRegex.Match(result);
            if (!m.Success)
                break;
            string key = m.Groups[1].Value.Trim();
            string inner = m.Groups[2].Value;
            result =
                config.ContainsKey(key) && _is_optional_value_visible(config, key)
                    ? result.Substring(0, m.Index) + inner + result.Substring(m.Index + m.Length)
                    : result.Substring(0, m.Index) + result.Substring(m.Index + m.Length);
        }
        result = ExpressionRegex.Replace(
            result,
            m =>
            {
                string expressionText = m.Groups[1].Value.Trim();
                if (
                    !_try_evaluate_expression(expressionText, config, out string value)
                    || _contains_template_token(value)
                )
                {
                    return InvalidTemplateFieldText;
                }
                return value;
            }
        );
        result = VariableRegex.Replace(
            result,
            m =>
            {
                string key = m.Groups[1].Value.Trim();
                string value = config.TryGetValue(key, out object rawValue)
                    ? FormatPlainValue(rawValue)
                    : "";
                return _contains_template_token(value) ? InvalidTemplateFieldText : value;
            }
        );
        return result;
    }

    private static bool _contains_template_token(string value) =>
        !string.IsNullOrEmpty(value) && VariableRegex.IsMatch(value);

    private static void MergePlainMap(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> source,
        bool overwrite
    )
    {
        if (source == null)
            return;
        foreach ((string key, object value) in source)
        {
            if (!overwrite && target.ContainsKey(key))
                continue;
            target[key] = value;
        }
    }

    private static bool _is_optional_value_visible(Dictionary<string, object> config, string key)
    {
        object value = config[key];
        return value switch
        {
            null => false,
            bool flag => flag,
            byte number => number != 0,
            short number => number != 0,
            int number => number != 0,
            long number => number != 0,
            float number => !float.IsNaN(number) && !Mathf.IsEqualApprox(number, 0f),
            double number => !double.IsNaN(number) && !Mathf.IsEqualApprox((float)number, 0f),
            string text => text.StripEdges().Length > 0,
            StringName stringName => stringName.ToString().StripEdges().Length > 0,
            IReadOnlyDictionary<string, object> dictionary => dictionary.Count > 0,
            IReadOnlyList<object> list => list.Count > 0,
            _ => false,
        };
    }

    private static void _merge_matching_effect_params(
        Dictionary<string, object> config,
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
            foreach ((string paramKey, object value) in effectDefinition.Parameters)
            {
                if (!config.ContainsKey(paramKey))
                    config[paramKey] = value;
            }
        }
    }

    private static void _merge_matching_effect_typed_fields(
        Dictionary<string, object> config,
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
        Dictionary<string, object> config,
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
        Dictionary<string, object> config,
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
        Dictionary<string, object> config,
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
        Dictionary<string, object> config,
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

    private static void _set_if_missing(Dictionary<string, object> config, string key, int value)
    {
        if (!config.ContainsKey(key))
            config[key] = value;
    }

    private static void _set_if_missing(
        Dictionary<string, object> config,
        string key,
        string value
    )
    {
        if (!config.ContainsKey(key))
            config[key] = value;
    }

    private static void _merge_level_overrides(
        Dictionary<string, object> config,
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
                config[fieldKey] = value;
        }
    }

    private static void _resolve_charge_distance(Dictionary<string, object> config, int level)
    {
        if (config.ContainsKey("distance"))
            return;
        if (!config.ContainsKey("base_distance") && !config.ContainsKey("distance_by_level"))
            return;
        int baseDist = config.TryGetValue("base_distance", out object baseDistance)
            ? ReadPlainInt(baseDistance)
            : 0;
        object distanceMap = config.TryGetValue("distance_by_level", out object distanceByLevel)
            ? distanceByLevel
            : null;
        if (distanceMap is not IReadOnlyDictionary<string, object> distanceByLevelMap)
        {
            config["distance"] = baseDist;
            return;
        }
        int dist = baseDist;
        var keys = new List<int>();
        foreach (string key in distanceByLevelMap.Keys)
            if (int.TryParse(key, out int parsedKey))
                keys.Add(parsedKey);
        keys.Sort();
        foreach (int k in keys)
        {
            if (k > level)
                break;
            if (distanceByLevelMap.TryGetValue(k.ToString(), out object value))
                dist = ReadPlainInt(value, dist);
        }
        config["distance"] = dist;
    }

    private static void _apply_description_derived_fields(Dictionary<string, object> config)
    {
        if (config.ContainsKey("dice_sides_base"))
        {
            int bs = ReadPlainInt(config["dice_sides_base"]);
            int cm = config.ContainsKey("con_mod") ? ReadPlainInt(config["con_mod"]) : 0;
            int wm = config.ContainsKey("will_mod") ? ReadPlainInt(config["will_mod"]) : 0;
            int cms = config.ContainsKey("dice_sides_per_constitution_mod")
                ? ReadPlainInt(config["dice_sides_per_constitution_mod"])
                : 0;
            int wms = config.ContainsKey("dice_sides_per_willpower_mod")
                ? ReadPlainInt(config["dice_sides_per_willpower_mod"])
                : 0;
            config["dice_sides"] = Mathf.Max(bs + cm * cms + wm * wms, 4);
        }
    }

    private static int ReadPlainInt(object value, int fallback = 0)
    {
        return value switch
        {
            sbyte number => number,
            byte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number when number <= int.MaxValue => (int)number,
            long number when number >= int.MinValue && number <= int.MaxValue => (int)number,
            float number when number >= int.MinValue && number <= int.MaxValue => (int)number,
            double number when number >= int.MinValue && number <= int.MaxValue => (int)number,
            _ => fallback,
        };
    }

    private static string FormatPlainValue(object value)
    {
        Variant variant = value switch
        {
            null => default,
            bool flag => Variant.From(flag),
            sbyte number => Variant.From((long)number),
            byte number => Variant.From((long)number),
            short number => Variant.From((long)number),
            ushort number => Variant.From((long)number),
            int number => Variant.From(number),
            uint number => Variant.From((long)number),
            long number => Variant.From(number),
            float number => Variant.From(number),
            double number => Variant.From(number),
            string text => Variant.From(text),
            StringName stringName => Variant.From(stringName),
            Vector2 mathValue => Variant.From(mathValue),
            Vector2I mathValue => Variant.From(mathValue),
            Rect2 mathValue => Variant.From(mathValue),
            Rect2I mathValue => Variant.From(mathValue),
            Vector3 mathValue => Variant.From(mathValue),
            Vector3I mathValue => Variant.From(mathValue),
            Transform2D mathValue => Variant.From(mathValue),
            Vector4 mathValue => Variant.From(mathValue),
            Vector4I mathValue => Variant.From(mathValue),
            Plane mathValue => Variant.From(mathValue),
            Quaternion mathValue => Variant.From(mathValue),
            Aabb mathValue => Variant.From(mathValue),
            Basis mathValue => Variant.From(mathValue),
            Transform3D mathValue => Variant.From(mathValue),
            Projection mathValue => Variant.From(mathValue),
            Color mathValue => Variant.From(mathValue),
            _ => default,
        };
        return variant.VariantType == Variant.Type.Nil ? "" : variant.AsString();
    }

    private static bool _try_evaluate_expression(
        string expressionText,
        Dictionary<string, object> variables,
        out string value
    )
    {
        value = "";
        if (expressionText.Length == 0)
            return false;
        using var expressionScope = new NativeLeaseScope(
            "skill-level-description-expression",
            LifetimeDomain.Request
        );
        Godot.Expression expr = expressionScope.Own(
            new Godot.Expression(),
            "SkillLevelDescriptionFormatter.expression"
        );
        var inputNames = new List<string>();
        var inputValues = new List<object>();
        foreach ((string key, object rawValue) in variables)
        {
            inputNames.Add(key);
            if (rawValue is string text)
            {
                inputValues.Add(
                    text.IsValidInt()
                        ? text.ToInt()
                        : (text.IsValidFloat() ? text.ToFloat() : rawValue)
                );
            }
            else
                inputValues.Add(rawValue);
        }
        if (expr.Parse(expressionText, inputNames.ToArray()) != Error.Ok)
            return false;
        using GodotProjectionLease<Godot.Collections.Array> inputProjection =
            RuntimePlainPayload.ProjectArrayLease(
                inputValues,
                "skill-level-description-expression-inputs",
                LifetimeDomain.Request,
                "SkillLevelDescriptionFormatter.expression_inputs"
            );
        Variant er = expr.Execute(inputProjection.Value, showError: false);
        if (expr.HasExecuteFailed())
            return false;
        if (
            er.VariantType == Variant.Type.Float
            && er.AsDouble() == System.Math.Floor(er.AsDouble())
        )
            value = ((int)er.AsDouble()).ToString();
        else
            value = er.AsString();
        return true;
    }
}
