using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleShieldApplyResult(
    bool Applied,
    int CurrentShieldHp,
    int ShieldMaxHp,
    int ShieldDuration,
    StringName ShieldFamily
)
{
    public GDictionary ToDictionary() =>
        new()
        {
            ["applied"] = Applied,
            ["current_shield_hp"] = CurrentShieldHp,
            ["shield_max_hp"] = ShieldMaxHp,
            ["shield_duration"] = ShieldDuration,
            ["shield_family"] = ShieldFamily,
        };
}

[GlobalClass]
public partial class BattleShieldService : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName ShieldEffect = "shield";
    private static readonly StringName ShieldFallbackFamily = "shield";
    private static readonly StringName Constitution = "constitution";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    public void setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public GDictionary _apply_unit_shield_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        return ApplyUnitShieldEffectsResult(
                source_unit,
                target_unit,
                skill_def,
                effect_defs,
                shield_roll_context
            )
            .ToDictionary();
    }

    public BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        BattleShieldApplyResult result = DefaultShieldResult(target_unit, false, true);
        if (target_unit == null || effect_defs == null || effect_defs.Count == 0)
        {
            return result;
        }

        GDictionary rollContext = shield_roll_context ?? new GDictionary();
        foreach (var effectValue in effect_defs)
        {
            CombatEffectDef effectDef =
                effectValue.VariantType == Variant.Type.Nil
                    ? null
                    : effectValue.AsGodotObject() as CombatEffectDef;
            if (effectDef == null || effectDef.effect_type != ShieldEffect)
            {
                continue;
            }

            BattleShieldApplyResult shieldApplyResult = ApplyShieldEffectToTargetResult(
                source_unit,
                target_unit,
                skill_def,
                effectDef,
                rollContext
            );
            if (!shieldApplyResult.Applied)
            {
                continue;
            }
            result = shieldApplyResult;
        }
        return result;
    }

    public GDictionary _apply_shield_effect_to_target(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        GDictionary shield_roll_context = null
    )
    {
        return ApplyShieldEffectToTargetResult(
                source_unit,
                target_unit,
                skill_def,
                effect_def,
                shield_roll_context
            )
            .ToDictionary();
    }

    public BattleShieldApplyResult ApplyShieldEffectToTargetResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        GDictionary shield_roll_context = null
    )
    {
        BattleShieldApplyResult result = DefaultShieldResult(target_unit, false, false);
        if (target_unit == null || effect_def == null)
        {
            return result;
        }

        GDictionary rollContext = shield_roll_context ?? new GDictionary();
        int shieldHp = _resolve_shield_hp(source_unit, effect_def, rollContext);
        if (shieldHp <= 0)
        {
            return result;
        }
        int shieldDuration = _resolve_shield_duration_tu(effect_def);
        if (shieldDuration <= 0)
        {
            return result;
        }

        StringName shieldFamily = _resolve_shield_family(skill_def, effect_def);
        GDictionary shieldParams = DuplicateDictionary(
            effect_def.@params,
            true
        );
        shieldParams["resolved_shield_hp"] = shieldHp;
        StringName shieldSourceUnitId = source_unit != null ? source_unit.unit_id : Empty;
        StringName shieldSourceSkillId = skill_def != null ? skill_def.skill_id : Empty;

        target_unit.normalize_shield_state();
        if (!target_unit.has_shield())
        {
            _write_unit_shield(
                target_unit,
                shieldHp,
                shieldDuration,
                shieldFamily,
                shieldSourceUnitId,
                shieldSourceSkillId,
                shieldParams
            );
            return BuildUnitShieldResult(target_unit, true);
        }

        if (target_unit.shield_family == shieldFamily)
        {
            int currentMaxHp = target_unit.shield_max_hp;
            int currentHp = target_unit.current_shield_hp;
            int currentDuration = target_unit.shield_duration;
            int nextShieldMaxHp = Math.Max(currentMaxHp, shieldHp);
            int nextCurrentShieldHp = Math.Max(currentHp, shieldHp);
            int nextShieldDuration = Math.Max(currentDuration, shieldDuration);
            if (
                nextShieldMaxHp == currentMaxHp
                && nextCurrentShieldHp == currentHp
                && nextShieldDuration == currentDuration
            )
            {
                return result;
            }

            target_unit.shield_max_hp = nextShieldMaxHp;
            target_unit.current_shield_hp = nextCurrentShieldHp;
            target_unit.shield_duration = nextShieldDuration;
            target_unit.shield_source_unit_id = shieldSourceUnitId;
            target_unit.shield_source_skill_id = shieldSourceSkillId;
            target_unit.shield_params = DuplicateDictionary(shieldParams, true);
            target_unit.normalize_shield_state();
            return BuildUnitShieldResult(target_unit, true);
        }

        bool shouldReplace = false;
        int targetCurrentShieldHp = target_unit.current_shield_hp;
        if (shieldHp > targetCurrentShieldHp)
        {
            shouldReplace = true;
        }
        else if (shieldHp == targetCurrentShieldHp)
        {
            shouldReplace = shieldDuration > target_unit.shield_duration;
        }

        if (!shouldReplace)
        {
            return result;
        }

        _write_unit_shield(
            target_unit,
            shieldHp,
            shieldDuration,
            shieldFamily,
            shieldSourceUnitId,
            shieldSourceSkillId,
            shieldParams
        );
        return BuildUnitShieldResult(target_unit, true);
    }

    public void _write_unit_shield(
        BattleUnitState target_unit,
        int shield_hp,
        int shield_duration,
        StringName shield_family,
        StringName shield_source_unit_id,
        StringName shield_source_skill_id,
        GDictionary shield_params
    )
    {
        if (target_unit == null)
        {
            return;
        }

        target_unit.current_shield_hp = Math.Max(shield_hp, 0);
        target_unit.shield_max_hp = Math.Max(shield_hp, 0);
        target_unit.shield_duration = shield_duration;
        target_unit.shield_family = shield_family;
        target_unit.shield_source_unit_id = shield_source_unit_id;
        target_unit.shield_source_skill_id = shield_source_skill_id;
        target_unit.shield_params = DuplicateDictionary(shield_params, true);
        target_unit.normalize_shield_state();
    }

    public GDictionary _build_unit_shield_result(BattleUnitState target_unit, bool applied)
    {
        return BuildUnitShieldResult(target_unit, applied).ToDictionary();
    }

    public BattleShieldApplyResult BuildUnitShieldResult(BattleUnitState target_unit, bool applied)
    {
        return DefaultShieldResult(target_unit, applied, false);
    }

    public int _resolve_shield_hp(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        GDictionary shield_roll_context = null
    )
    {
        if (effect_def == null)
        {
            return 0;
        }

        int fallbackShieldHp = Math.Max(effect_def.power, 0);
        GDictionary rollContext = shield_roll_context ?? new GDictionary();
        if (_has_shield_base_sides_config(effect_def))
        {
            return _roll_shield_hp_with_base_sides(source_unit, effect_def, rollContext);
        }
        if (!_has_shield_dice_config(effect_def))
        {
            return fallbackShieldHp;
        }

        long cacheKey = _get_shield_roll_cache_key(effect_def);
        if (rollContext.ContainsKey(cacheKey))
        {
            return Math.Max(GetInt(rollContext, cacheKey, fallbackShieldHp), 0);
        }

        int rolledShieldHp = _roll_shield_hp(effect_def);
        rollContext[cacheKey] = rolledShieldHp;
        return Math.Max(rolledShieldHp, 0);
    }

    public int _roll_shield_hp(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return 0;
        }

        int shieldHp = Math.Max(effect_def.power, 0);
        GDictionary parameters = effect_def.@params;
        if (parameters.Count == 0)
        {
            return shieldHp;
        }

        int diceCount = Math.Max(GetInt(parameters, "dice_count", 0), 0);
        int diceSides = Math.Max(GetInt(parameters, "dice_sides", 0), 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return shieldHp;
        }

        shieldHp += GetInt(parameters, "dice_bonus", 0);
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        return Math.Max(shieldHp, 0);
    }

    public bool _has_shield_dice_config(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        GDictionary parameters = effect_def.@params;
        return parameters.Count > 0
            && GetInt(parameters, "dice_count", 0) > 0
            && GetInt(parameters, "dice_sides", 0) > 0;
    }

    public bool _has_shield_base_sides_config(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        GDictionary parameters = effect_def.@params;
        return parameters.Count > 0 && GetInt(parameters, "base_sides", 0) > 0;
    }

    public int _roll_shield_hp_with_base_sides(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        GDictionary shield_roll_context = null
    )
    {
        GDictionary rollContext = shield_roll_context ?? new GDictionary();
        long cacheKey = _get_shield_roll_cache_key(effect_def);
        if (rollContext.ContainsKey(cacheKey))
        {
            return Math.Max(GetInt(rollContext, cacheKey, 0), 0);
        }

        GDictionary parameters = effect_def.@params;
        int diceCount = Math.Max(effect_def.power, 1);
        int baseSides = GetInt(parameters, "base_sides", 4);
        int conModSides = GetInt(parameters, "con_mod_sides", 1);
        int diceSides = baseSides;
        AttributeSnapshot attributeSnapshot = source_unit?.attribute_snapshot;
        if (attributeSnapshot != null)
        {
            int conScore = attributeSnapshot.get_value(Constitution);
            int conMod = (int)Math.Floor((conScore - 10) / 2.0);
            diceSides = Math.Max(baseSides + conMod * conModSides, 4);
        }

        int shieldHp = 0;
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        rollContext[cacheKey] = Math.Max(shieldHp, 1);
        return Math.Max(shieldHp, 1);
    }

    public long _get_shield_roll_cache_key(CombatEffectDef effect_def)
    {
        return effect_def != null ? unchecked((long)effect_def.GetInstanceId()) : 0L;
    }

    public int _roll_battle_effect_die(int dice_sides)
    {
        if (dice_sides <= 0)
        {
            return 0;
        }
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null || runtime._state == null)
        {
            return 1;
        }

        return TrueRandomSeedService.randi_range(1, dice_sides);
    }

    public int _resolve_shield_duration_tu(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return 0;
        }
        int durationTu = effect_def.duration_tu;
        if (durationTu > 0)
        {
            return durationTu;
        }
        GDictionary parameters = effect_def.@params;
        if (parameters.Count == 0)
        {
            return 0;
        }
        if (parameters.ContainsKey("duration_tu"))
        {
            return Math.Max(GetInt(parameters, "duration_tu", 0), 0);
        }
        return 0;
    }

    public StringName _resolve_shield_family(SkillDef skill_def, CombatEffectDef effect_def)
    {
        if (effect_def != null)
        {
            GDictionary parameters = effect_def.@params;
            if (parameters.Count > 0)
            {
                StringName explicitFamily = GetStringName(parameters, "shield_family");
                if (!IsEmpty(explicitFamily))
                {
                    return explicitFamily;
                }
                explicitFamily = GetStringName(parameters, "family");
                if (!IsEmpty(explicitFamily))
                {
                    return explicitFamily;
                }
            }
        }
        if (skill_def != null)
        {
            StringName skillId = skill_def.skill_id;
            if (!IsEmpty(skillId))
            {
                return skillId;
            }
        }
        return ShieldFallbackFamily;
    }

    private static BattleShieldApplyResult DefaultShieldResult(
        BattleUnitState targetUnit,
        bool applied,
        bool useEmptyDefaults
    )
    {
        return new BattleShieldApplyResult(
            applied,
            targetUnit != null && !useEmptyDefaults ? targetUnit.current_shield_hp : 0,
            targetUnit != null && !useEmptyDefaults ? targetUnit.shield_max_hp : 0,
            targetUnit != null && !useEmptyDefaults ? targetUnit.shield_duration : -1,
            targetUnit != null && !useEmptyDefaults ? targetUnit.shield_family : Empty
        );
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.Bool => value.AsBool() ? 1 : 0,
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed)
                ? parsed
                : fallback,
            Variant.Type.StringName
                => int.TryParse(value.AsStringName().ToString(), out int parsed)
                    ? parsed
                    : fallback,
            _ => fallback,
        };
    }

    private static StringName GetStringName(GDictionary source, object key)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return Empty;
        }
        return ProgressionDataUtils.to_string_name(value);
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out BattleRuntimeModule target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
