using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
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
        SkillDefinition skill_definition,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        BattleShieldApplyResult result = DefaultShieldResult(target_unit, false, true);
        if (target_unit == null || effect_definitions == null)
        {
            return result;
        }

        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        foreach (CombatEffectDefinition effectDefinition in effect_definitions)
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.Shield
            )
            {
                continue;
            }

            BattleShieldApplyResult shieldApplyResult = ApplyShieldEffectToTargetResult(
                source_unit,
                target_unit,
                skill_definition,
                effectDefinition,
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
        SkillDefinition skill_definition,
        CombatEffectDefinition effect_definition,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        BattleShieldApplyResult result = DefaultShieldResult(target_unit, false, false);
        if (target_unit == null || effect_definition == null)
        {
            return result;
        }

        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        int shieldHp = ResolveShieldHp(source_unit, effect_definition, rollContext);
        shieldHp = BattleStatusModifierRules.ApplyShieldGainMultiplier(target_unit, shieldHp);
        if (shieldHp <= 0)
        {
            return result;
        }
        int shieldDuration = _resolve_shield_duration_tu(effect_definition);
        if (shieldDuration <= 0)
        {
            return result;
        }

        StringName shieldFamily = _resolve_shield_family(skill_definition, effect_definition);
        StringName shieldSourceUnitId = source_unit != null ? source_unit.unit_id : Empty;
        StringName shieldSourceSkillId =
            skill_definition != null ? skill_definition.SkillId : Empty;

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

        BattleUnitShieldSnapshot currentShield = target_unit.GetShieldStateTyped();
        if (currentShield.Family == shieldFamily)
        {
            int currentMaxHp = currentShield.MaxHp;
            int currentHp = currentShield.CurrentHp;
            int currentDuration = currentShield.Duration;
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

            target_unit.ReplaceShieldStateTyped(
                nextCurrentShieldHp,
                nextShieldMaxHp,
                nextShieldDuration,
                currentShield.Family,
                shieldSourceUnitId,
                shieldSourceSkillId
            );
            return BuildUnitShieldResult(target_unit, true);
        }

        bool shouldReplace = false;
        int targetCurrentShieldHp = currentShield.CurrentHp;
        if (shieldHp > targetCurrentShieldHp)
        {
            shouldReplace = true;
        }
        else if (shieldHp == targetCurrentShieldHp)
        {
            shouldReplace = shieldDuration > currentShield.Duration;
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

        int normalizedHp = Math.Max(shield_hp, 0);
        target_unit.ReplaceShieldStateTyped(
            normalizedHp,
            normalizedHp,
            shield_duration,
            shield_family,
            shield_source_unit_id,
            shield_source_skill_id
        );
    }

    private BattleShieldApplyResult BuildUnitShieldResult(BattleUnitState target_unit, bool applied)
    {
        return DefaultShieldResult(target_unit, applied, false);
    }

    internal int _resolve_shield_hp(
        BattleUnitState source_unit,
        CombatEffectDefinition effect_definition,
        Godot.Collections.Dictionary shield_roll_context = null
    )
    {
        Dictionary<long, int> rollContext = ReadRollContext(shield_roll_context);
        int shieldHp = ResolveShieldHp(source_unit, effect_definition, rollContext);
        WriteRollContext(shield_roll_context, rollContext);
        return shieldHp;
    }

    internal int ResolveShieldHp(
        BattleUnitState source_unit,
        CombatEffectDefinition effect_definition,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        if (effect_definition == null)
        {
            return 0;
        }

        int fallbackShieldHp = Math.Max(effect_definition.Power, 0);
        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        if (_has_shield_attribute_scaled_dice_config(effect_definition))
        {
            return RollShieldHpWithAttributeScaledDice(
                source_unit,
                effect_definition,
                rollContext
            );
        }
        if (!_has_shield_dice_config(effect_definition))
        {
            return fallbackShieldHp;
        }

        long cacheKey = _get_shield_roll_cache_key(effect_definition);
        if (rollContext.TryGetValue(cacheKey, out int cachedShieldHp))
        {
            return Math.Max(cachedShieldHp, 0);
        }

        int rolledShieldHp = _roll_shield_hp(effect_definition);
        rollContext[cacheKey] = rolledShieldHp;
        return Math.Max(rolledShieldHp, 0);
    }

    internal int _roll_shield_hp(CombatEffectDefinition effect_definition)
    {
        if (effect_definition == null)
        {
            return 0;
        }

        int shieldHp = Math.Max(effect_definition.Power, 0);
        int diceCount = Math.Max(effect_definition.DiceCount, 0);
        int diceSides = Math.Max(effect_definition.DiceSides, 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return shieldHp;
        }

        shieldHp += effect_definition.DiceBonus;
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        return Math.Max(shieldHp, 0);
    }

    internal bool _has_shield_dice_config(CombatEffectDefinition effect_definition)
    {
        if (effect_definition == null)
        {
            return false;
        }
        return (effect_definition.DiceCount > 0 && effect_definition.DiceSides > 0)
            || _has_shield_attribute_scaled_dice_config(effect_definition);
    }

    internal bool _has_shield_attribute_scaled_dice_config(
        CombatEffectDefinition effect_definition
    )
    {
        if (effect_definition == null)
        {
            return false;
        }
        return effect_definition.DiceCount > 0 && effect_definition.DiceSidesBase > 0;
    }

    private int RollShieldHpWithAttributeScaledDice(
        BattleUnitState source_unit,
        CombatEffectDefinition effect_definition,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        if (effect_definition == null)
        {
            return 0;
        }
        Dictionary<long, int> rollContext = shield_roll_context ?? new Dictionary<long, int>();
        long cacheKey = _get_shield_roll_cache_key(effect_definition);
        if (rollContext.TryGetValue(cacheKey, out int cachedShieldHp))
        {
            return Math.Max(cachedShieldHp, 0);
        }

        int diceCount = Math.Max(effect_definition.DiceCount, 1);
        int baseSides = Math.Max(effect_definition.DiceSidesBase, 0);
        int conModSides = Math.Max(effect_definition.DiceSidesPerConstitutionMod, 0);
        int willModSides = Math.Max(effect_definition.DiceSidesPerWillpowerMod, 0);
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

        int shieldHp = Math.Max(effect_definition.Power, 0) + effect_definition.DiceBonus;
        for (int rollIndex = 0; rollIndex < diceCount; rollIndex++)
        {
            shieldHp += _roll_battle_effect_die(diceSides);
        }
        rollContext[cacheKey] = Math.Max(shieldHp, 1);
        return Math.Max(shieldHp, 1);
    }

    internal long _get_shield_roll_cache_key(CombatEffectDefinition effect_definition)
    {
        return effect_definition != null
            ? unchecked((long)RuntimeHelpers.GetHashCode(effect_definition))
            : 0L;
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

    internal int _resolve_shield_duration_tu(CombatEffectDefinition effect_definition)
    {
        if (effect_definition == null)
        {
            return 0;
        }
        int durationTu = effect_definition.DurationTu;
        if (durationTu > 0)
        {
            return durationTu;
        }
        return Math.Max(effect_definition.GetIntParamTyped("duration_tu", 0), 0);
    }

    internal StringName _resolve_shield_family(
        SkillDefinition skill_definition,
        CombatEffectDefinition effect_definition
    )
    {
        if (effect_definition != null)
        {
            StringName explicitFamily = effect_definition.GetStringNameParamTyped(
                "shield_family",
                Empty
            );
            if (!IsEmpty(explicitFamily))
            {
                return explicitFamily;
            }
        }
        if (skill_definition != null)
        {
            StringName skillId = skill_definition.SkillId;
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
        BattleUnitShieldSnapshot shieldState =
            targetUnit?.GetShieldStateTyped()
            ?? BattleUnitShieldSnapshot.MissingOwner;
        return new BattleShieldApplyResult(
            applied,
            targetUnit != null && !useEmptyDefaults ? shieldState.CurrentHp : 0,
            targetUnit != null && !useEmptyDefaults ? shieldState.MaxHp : 0,
            targetUnit != null && !useEmptyDefaults ? shieldState.Duration : -1,
            targetUnit != null && !useEmptyDefaults ? shieldState.Family : Empty
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
