using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleShieldService : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName ShieldEffect = "shield";
    private static readonly StringName ShieldFallbackFamily = "shield";
    private static readonly StringName Constitution = "constitution";
    private static readonly Script TrueRandomSeedServiceScript = GD.Load<Script>("res://scripts/utils/true_random_seed_service.gd");

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void setup(GodotObject runtime)
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
        GodotObject skill_def,
        GArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        GDictionary result = DefaultShieldResult(target_unit, false, true);
        if (target_unit == null || effect_defs == null || effect_defs.Count == 0)
        {
            return result;
        }

        GDictionary rollContext = shield_roll_context ?? new GDictionary();
        foreach (Variant effectValue in effect_defs)
        {
            GodotObject effectDef = effectValue.VariantType == Variant.Type.Nil ? null : effectValue.AsGodotObject();
            if (effectDef == null || GdInterop.GetStringName(effectDef, "effect_type") != ShieldEffect)
            {
                continue;
            }

            GDictionary shieldApplyResult = _apply_shield_effect_to_target(
                source_unit,
                target_unit,
                skill_def,
                effectDef,
                rollContext
            );
            if (!GdInterop.GetBool(shieldApplyResult, "applied", false))
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
        GodotObject skill_def,
        GodotObject effect_def,
        GDictionary shield_roll_context = null
    )
    {
        GDictionary result = DefaultShieldResult(target_unit, false, false);
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
        GDictionary shieldParams = DuplicateDictionary(GdInterop.GetDictionary(effect_def, "params"), true);
        shieldParams["resolved_shield_hp"] = shieldHp;
        StringName shieldSourceUnitId = source_unit != null ? source_unit.unit_id : Empty;
        StringName shieldSourceSkillId = skill_def != null ? GdInterop.GetStringName(skill_def, "skill_id") : Empty;

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
            return _build_unit_shield_result(target_unit, true);
        }

        if (target_unit.shield_family == shieldFamily)
        {
            int currentMaxHp = target_unit.shield_max_hp;
            int currentHp = target_unit.current_shield_hp;
            int currentDuration = target_unit.shield_duration;
            int nextShieldMaxHp = Math.Max(currentMaxHp, shieldHp);
            int nextCurrentShieldHp = Math.Max(currentHp, shieldHp);
            int nextShieldDuration = Math.Max(currentDuration, shieldDuration);
            if (nextShieldMaxHp == currentMaxHp
                && nextCurrentShieldHp == currentHp
                && nextShieldDuration == currentDuration)
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
            return _build_unit_shield_result(target_unit, true);
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
        return _build_unit_shield_result(target_unit, true);
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
        return DefaultShieldResult(target_unit, applied, false);
    }

    public int _resolve_shield_hp(BattleUnitState source_unit, GodotObject effect_def, GDictionary shield_roll_context = null)
    {
        if (effect_def == null)
        {
            return 0;
        }

        int fallbackShieldHp = Math.Max(GdInterop.GetInt(effect_def, "power"), 0);
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
            return Math.Max(GdInterop.GetInt(rollContext, cacheKey, fallbackShieldHp), 0);
        }

        int rolledShieldHp = _roll_shield_hp(effect_def);
        rollContext[cacheKey] = rolledShieldHp;
        return Math.Max(rolledShieldHp, 0);
    }

    public int _roll_shield_hp(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return 0;
        }

        int shieldHp = Math.Max(GdInterop.GetInt(effect_def, "power"), 0);
        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        if (parameters.Count == 0)
        {
            return shieldHp;
        }

        int diceCount = Math.Max(GdInterop.GetInt(parameters, "dice_count", 0), 0);
        int diceSides = Math.Max(GdInterop.GetInt(parameters, "dice_sides", 0), 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return shieldHp;
        }

        shieldHp += GdInterop.GetInt(parameters, "dice_bonus", 0);
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        return Math.Max(shieldHp, 0);
    }

    public bool _has_shield_dice_config(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        return parameters.Count > 0
            && GdInterop.GetInt(parameters, "dice_count", 0) > 0
            && GdInterop.GetInt(parameters, "dice_sides", 0) > 0;
    }

    public bool _has_shield_base_sides_config(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        return parameters.Count > 0 && GdInterop.GetInt(parameters, "base_sides", 0) > 0;
    }

    public int _roll_shield_hp_with_base_sides(BattleUnitState source_unit, GodotObject effect_def, GDictionary shield_roll_context = null)
    {
        GDictionary rollContext = shield_roll_context ?? new GDictionary();
        long cacheKey = _get_shield_roll_cache_key(effect_def);
        if (rollContext.ContainsKey(cacheKey))
        {
            return Math.Max(GdInterop.GetInt(rollContext, cacheKey, 0), 0);
        }

        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        int diceCount = Math.Max(GdInterop.GetInt(effect_def, "power"), 1);
        int baseSides = GdInterop.GetInt(parameters, "base_sides", 4);
        int conModSides = GdInterop.GetInt(parameters, "con_mod_sides", 1);
        int diceSides = baseSides;
        GodotObject attributeSnapshot = source_unit != null ? source_unit.attribute_snapshot : null;
        if (attributeSnapshot != null)
        {
            int conScore = attributeSnapshot.Call("get_value", Constitution).AsInt32();
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

    public long _get_shield_roll_cache_key(GodotObject effect_def)
    {
        return effect_def != null ? unchecked((long)effect_def.GetInstanceId()) : 0L;
    }

    public int _roll_battle_effect_die(int dice_sides)
    {
        if (dice_sides <= 0)
        {
            return 0;
        }
        GodotObject runtime = _runtime;
        Variant stateValue = runtime.Get("_state");
        if (stateValue.VariantType == Variant.Type.Nil)
        {
            return 1;
        }

        return TrueRandomSeedServiceScript.Call("randi_range", 1, dice_sides).AsInt32();
    }

    public int _resolve_shield_duration_tu(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return 0;
        }
        int durationTu = GdInterop.GetInt(effect_def, "duration_tu");
        if (durationTu > 0)
        {
            return durationTu;
        }
        GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
        if (parameters.Count == 0)
        {
            return 0;
        }
        if (parameters.ContainsKey("duration_tu"))
        {
            return Math.Max(GdInterop.GetInt(parameters, "duration_tu", 0), 0);
        }
        return 0;
    }

    public StringName _resolve_shield_family(GodotObject skill_def, GodotObject effect_def)
    {
        if (effect_def != null)
        {
            GDictionary parameters = GdInterop.GetDictionary(effect_def, "params");
            if (parameters.Count > 0)
            {
                StringName explicitFamily = ToStringNameLoose(GetVariant(parameters, "shield_family"));
                if (!GdInterop.IsEmpty(explicitFamily))
                {
                    return explicitFamily;
                }
                explicitFamily = ToStringNameLoose(GetVariant(parameters, "family"));
                if (!GdInterop.IsEmpty(explicitFamily))
                {
                    return explicitFamily;
                }
            }
        }
        if (skill_def != null)
        {
            StringName skillId = GdInterop.GetStringName(skill_def, "skill_id");
            if (!GdInterop.IsEmpty(skillId))
            {
                return skillId;
            }
        }
        return ShieldFallbackFamily;
    }

    private static GDictionary DefaultShieldResult(BattleUnitState targetUnit, bool applied, bool useEmptyDefaults)
    {
        return new GDictionary
        {
            ["applied"] = applied,
            ["current_shield_hp"] = targetUnit != null && !useEmptyDefaults ? targetUnit.current_shield_hp : 0,
            ["shield_max_hp"] = targetUnit != null && !useEmptyDefaults ? targetUnit.shield_max_hp : 0,
            ["shield_duration"] = targetUnit != null && !useEmptyDefaults ? targetUnit.shield_duration : -1,
            ["shield_family"] = targetUnit != null && !useEmptyDefaults ? targetUnit.shield_family : Empty,
        };
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
    }

    private static Variant GetVariant(GDictionary dictionary, Variant key)
    {
        return GdInterop.TryGet(dictionary, key, out Variant value) ? value : default;
    }

    private static StringName ToStringNameLoose(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return Empty;
        }

        string text = value.ToString();
        if (text == "<null>")
        {
            return Empty;
        }

        string trimmed = text.Trim();
        return string.IsNullOrEmpty(trimmed) ? Empty : new StringName(trimmed);
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
        {
            return null;
        }
        return target;
    }
}
