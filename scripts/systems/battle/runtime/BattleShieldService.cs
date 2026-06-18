using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

internal readonly record struct BattleShieldApplyResult(
    bool Applied,
    int CurrentShieldHp,
    int ShieldMaxHp,
    int ShieldDuration,
    StringName ShieldFamily
);

internal sealed class BattleShieldService
{
    private static readonly StringName Empty = "";
    private static readonly StringName ShieldEffect = "shield";
    private static readonly StringName ShieldFallbackFamily = "shield";
    private static readonly StringName Constitution = "constitution";
    private static readonly StringName Willpower = "willpower";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
    }

    internal BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        IEnumerable<CombatEffectDef> effect_defs,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        BattleShieldApplyResult result = DefaultShieldResult(target_unit, false, true);
        if (target_unit == null || effect_defs == null)
        {
            return result;
        }

        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        foreach (CombatEffectDef effectDef in effect_defs)
        {
            if (effectDef == null || effectDef.EffectKind != BattleEffectKind.Shield)
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

    internal BattleShieldApplyResult ApplyShieldEffectToTargetResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        BattleShieldApplyResult result = DefaultShieldResult(target_unit, false, false);
        if (target_unit == null || effect_def == null)
        {
            return result;
        }

        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        int shieldHp = ResolveShieldHp(source_unit, effect_def, rollContext);
        shieldHp = BattleStatusModifierRules.ApplyShieldGainMultiplier(target_unit, shieldHp);
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
        StringName shieldSourceUnitId = source_unit != null ? source_unit.unit_id : Empty;
        StringName shieldSourceSkillId = skill_def != null ? skill_def.skill_id : Empty;

        target_unit.NormalizeShieldState();
        if (!target_unit.HasShield())
        {
            _write_unit_shield(
                target_unit,
                shieldHp,
                shieldDuration,
                shieldFamily,
                shieldSourceUnitId,
                shieldSourceSkillId
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
            target_unit.NormalizeShieldState();
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
            shieldSourceSkillId
        );
        return BuildUnitShieldResult(target_unit, true);
    }

    internal void _write_unit_shield(
        BattleUnitState target_unit,
        int shield_hp,
        int shield_duration,
        StringName shield_family,
        StringName shield_source_unit_id,
        StringName shield_source_skill_id
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
        target_unit.NormalizeShieldState();
    }

    private BattleShieldApplyResult BuildUnitShieldResult(BattleUnitState target_unit, bool applied)
    {
        return DefaultShieldResult(target_unit, applied, false);
    }

    internal int _resolve_shield_hp(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        Godot.Collections.Dictionary shield_roll_context = null
    )
    {
        Dictionary<long, int> rollContext = ReadRollContext(shield_roll_context);
        int shieldHp = ResolveShieldHp(source_unit, effect_def, rollContext);
        WriteRollContext(shield_roll_context, rollContext);
        return shieldHp;
    }

    internal int ResolveShieldHp(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        if (effect_def == null)
        {
            return 0;
        }

        int fallbackShieldHp = Math.Max(effect_def.power, 0);
        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        if (_has_shield_attribute_scaled_dice_config(effect_def))
        {
            return RollShieldHpWithAttributeScaledDice(
                source_unit,
                effect_def,
                rollContext
            );
        }
        if (!_has_shield_dice_config(effect_def))
        {
            return fallbackShieldHp;
        }

        long cacheKey = _get_shield_roll_cache_key(effect_def);
        if (rollContext.TryGetValue(cacheKey, out int cachedShieldHp))
        {
            return Math.Max(cachedShieldHp, 0);
        }

        int rolledShieldHp = _roll_shield_hp(effect_def);
        rollContext[cacheKey] = rolledShieldHp;
        return Math.Max(rolledShieldHp, 0);
    }

    internal int _roll_shield_hp(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return 0;
        }

        int shieldHp = Math.Max(effect_def.power, 0);
        int diceCount = Math.Max(effect_def.dice_count, 0);
        int diceSides = Math.Max(effect_def.dice_sides, 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return shieldHp;
        }

        shieldHp += effect_def.dice_bonus;
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        return Math.Max(shieldHp, 0);
    }

    internal bool _has_shield_dice_config(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        return (effect_def.dice_count > 0 && effect_def.dice_sides > 0)
            || _has_shield_attribute_scaled_dice_config(effect_def);
    }

    internal bool _has_shield_attribute_scaled_dice_config(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        return effect_def.dice_count > 0 && effect_def.dice_sides_base > 0;
    }

    internal int _roll_shield_hp_with_attribute_scaled_dice(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        Godot.Collections.Dictionary shield_roll_context = null
    )
    {
        Dictionary<long, int> rollContext = ReadRollContext(shield_roll_context);
        int shieldHp = RollShieldHpWithAttributeScaledDice(
            source_unit,
            effect_def,
            rollContext
        );
        WriteRollContext(shield_roll_context, rollContext);
        return shieldHp;
    }

    private int RollShieldHpWithAttributeScaledDice(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        if (effect_def == null)
        {
            return 0;
        }
        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        long cacheKey = _get_shield_roll_cache_key(effect_def);
        if (rollContext.TryGetValue(cacheKey, out int cachedShieldHp))
        {
            return Math.Max(cachedShieldHp, 0);
        }

        int diceCount = Math.Max(effect_def.dice_count, 1);
        int baseSides = Math.Max(effect_def.dice_sides_base, 0);
        int conModSides = Math.Max(effect_def.dice_sides_per_constitution_mod, 0);
        int willModSides = Math.Max(effect_def.dice_sides_per_willpower_mod, 0);
        int diceSides = Math.Max(baseSides, 4);
        AttributeSnapshot attributeSnapshot = source_unit?.attribute_snapshot;
        if (attributeSnapshot != null)
        {
            int conScore = attributeSnapshot.GetValue(Constitution);
            int conMod = (int)Math.Floor((conScore - 10) / 2.0);
            int willScore = attributeSnapshot.GetValue(Willpower);
            int willMod = (int)Math.Floor((willScore - 10) / 2.0);
            long diceSidesRaw =
                (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
            diceSides = (int)Math.Clamp(diceSidesRaw, 4L, int.MaxValue);
        }

        int shieldHp = Math.Max(effect_def.power, 0) + effect_def.dice_bonus;
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        rollContext[cacheKey] = Math.Max(shieldHp, 1);
        return Math.Max(shieldHp, 1);
    }

    internal long _get_shield_roll_cache_key(CombatEffectDef effect_def)
    {
        return effect_def != null ? unchecked((long)effect_def.GetInstanceId()) : 0L;
    }

    internal int _roll_battle_effect_die(int dice_sides)
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

        return TrueRandomSeedService.RandiRange(1, dice_sides);
    }

    internal int _resolve_shield_duration_tu(CombatEffectDef effect_def)
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
        Godot.Collections.Dictionary parameters = effect_def.@params;
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

    internal StringName _resolve_shield_family(SkillDef skill_def, CombatEffectDef effect_def)
    {
        if (effect_def != null)
        {
            Godot.Collections.Dictionary parameters = effect_def.@params;
            if (parameters.Count > 0)
            {
                StringName explicitFamily = GetStringName(parameters, "shield_family");
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

    internal static Dictionary<long, int> ReadRollContext(Godot.Collections.Dictionary source)
    {
        var result = new Dictionary<long, int>();
        if (source == null)
        {
            return result;
        }
        foreach (var rawKey in source.Keys)
        {
            string keyText = rawKey.AsString();
            if (!long.TryParse(keyText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long key))
            {
                continue;
            }
            result[key] = source[rawKey].AsInt32();
        }
        return result;
    }

    internal static void WriteRollContext(
        Godot.Collections.Dictionary target,
        Dictionary<long, int> source
    )
    {
        if (target == null)
        {
            return;
        }
        target.Clear();
        if (source == null)
        {
            return;
        }
        foreach (KeyValuePair<long, int> entry in source)
        {
            target[entry.Key.ToString(CultureInfo.InvariantCulture)] = entry.Value;
        }
    }

    private static List<CombatEffectDef> ReadCombatEffectDefs(Godot.Collections.Array source)
    {
        var result = new List<CombatEffectDef>();
        if (source == null)
        {
            return result;
        }
        foreach (var effectValue in source)
        {
            CombatEffectDef effectDef = effectValue.As<CombatEffectDef>();
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
        return result;
    }

    private static int GetInt(Godot.Collections.Dictionary source, string key, int fallback = 0)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return fallback;
        }
        return source[key].AsInt32();
    }

    private static StringName GetStringName(Godot.Collections.Dictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return Empty;
        }
        return ProgressionDataUtils.to_string_name(source[key]);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }
}
