using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal readonly record struct BattleStatusTickResult(bool Changed, StringName DefeatSourceUnitId)
{
    internal static BattleStatusTickResult Empty() => new(false, "");
}

internal readonly record struct BattleTurnControlStatusResult(
    bool SkipTurn,
    bool Changed,
    bool AiControlled,
    string AiTargetPolicy,
    bool CleanupOnTurnEnd,
    bool StatusRemoved
)
{
    internal static BattleTurnControlStatusResult Empty() =>
        new(false, false, false, "", false, false);
}

internal partial class BattleRuntimeSkillTurnResolver : RefCounted
{
    private static readonly StringName STATUS_PINNED = "pinned";
    private static readonly StringName STATUS_ROOTED = "rooted";
    private static readonly StringName STATUS_TENDON_CUT = "tendon_cut";
    private static readonly StringName STATUS_STAGGERED = "staggered";
    private static readonly StringName STATUS_METEOR_CONCUSSED = "meteor_concussed";
    private static readonly StringName STATUS_FROZEN = "frozen";
    private static readonly StringName STATUS_PETRIFIED = "petrified";
    private static readonly StringName STATUS_MADNESS = "madness";
    private static readonly StringName STATUS_GUARDING = "guarding";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";
    private static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    private static readonly StringName BLACK_CONTRACT_PUSH_OPTION_BLOOD = "blood_tithe";
    private static readonly StringName BLACK_CONTRACT_PUSH_OPTION_GUARD = "guard_tithe";
    private static readonly StringName BLACK_CONTRACT_PUSH_OPTION_ACTION = "action_tithe";
    private const int DOOM_SHIFT_SELF_DEBUFF_DURATION_TU = 60;
    private const int BLACK_CONTRACT_PUSH_HP_COST = 10;
    private const int TU_GRANULARITY = 5;

    private static readonly StringName Empty = "";
    private static readonly StringName CombatResourceMp = "mp";
    private static readonly StringName CombatResourceStamina = "stamina";
    private static readonly StringName CombatResourceAura = "aura";
    private static readonly StringName UnitTargetMode = "unit";
    private static readonly StringName StatusEffectType = "status";
    private static readonly HashSet<StringName> IdentitySkillLearnSources = new()
    {
        "race",
        "subrace",
        "ascension",
        "bloodline",
    };

    private static readonly HashSet<StringName> DebuffStatusIds = new()
    {
        "armor_break",
        "black_star_brand_elite",
        "black_star_brand_normal",
        "burning",
        "crown_break_blinded_eye",
        "crown_break_broken_fang",
        "crown_break_broken_hand",
        "frozen",
        "hex_of_frailty",
        "marked",
        "meteor_concussed",
        "pinned",
        "petrified",
        "rooted",
        "shocked",
        "slow",
        "staggered",
        "taunted",
        "tendon_cut",
    };

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

    internal BattleTurnControlStatusResult ResolveTurnControlStatusResult(
        BattleUnitState unit_state,
        BattleEventBatch batch
    )
    {
        if (unit_state == null || !unit_state.is_alive)
        {
            return BattleTurnControlStatusResult.Empty();
        }
        if (HasStatus(unit_state, STATUS_PETRIFIED))
        {
            BattleStatusEffectState petrifiedEntry = GetStatusEffect(unit_state, STATUS_PETRIFIED);
            BattleSaveResult petrifiedSave = _resolve_status_self_save_result(
                unit_state,
                petrifiedEntry,
                "constitution",
                "constitution"
            );
            if (petrifiedSave.Success)
            {
                EraseStatusEffect(unit_state, STATUS_PETRIFIED);
                _append_changed_unit(batch, unit_state);
                _append_log(
                    batch,
                    $"{DisplayName(unit_state)} 通过体质检定，解除石化并立刻恢复行动。"
                );
                return new BattleTurnControlStatusResult(
                    false,
                    true,
                    false,
                    "",
                    false,
                    true
                );
            }
            unit_state.SetCurrentAp(0);
            unit_state.SetCurrentMovePoints(0);
            _append_changed_unit(batch, unit_state);
            _append_log(batch, $"{DisplayName(unit_state)} 石化未解除，无法行动。");
            return new BattleTurnControlStatusResult(true, true, false, "", false, false);
        }
        if (HasStatus(unit_state, STATUS_MADNESS))
        {
            BattleStatusEffectState madnessEntry = GetStatusEffect(unit_state, STATUS_MADNESS);
            BattleSaveResult madnessSave = _resolve_status_self_save_result(
                unit_state,
                madnessEntry,
                "willpower",
                "willpower"
            );
            if (madnessSave.Success)
            {
                EraseStatusEffect(unit_state, STATUS_MADNESS);
                ClearTurnAiOverride(unit_state);
                _append_changed_unit(batch, unit_state);
                _append_log(
                    batch,
                    $"{DisplayName(unit_state)} 通过意志检定，摆脱疯狂并立刻恢复行动。"
                );
                return new BattleTurnControlStatusResult(
                    false,
                    true,
                    false,
                    "",
                    false,
                    true
                );
            }
            unit_state.ai_blackboard.madness_ai_control = true;
            unit_state.ai_blackboard.madness_target_any_team = true;
            _append_changed_unit(batch, unit_state);
            _append_log(
                batch,
                $"{DisplayName(unit_state)} 疯狂未解除，本次行动由 AI 接管且不区分敌我。"
            );
            return new BattleTurnControlStatusResult(
                false,
                true,
                true,
                "any_unit",
                true,
                false
            );
        }
        return BattleTurnControlStatusResult.Empty();
    }

    internal bool IsTurnAiOverrideActive(BattleUnitState unit_state)
    {
        return unit_state?.ai_blackboard?.madness_ai_control ?? false;
    }

    internal void ClearTurnAiOverride(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.ai_blackboard.madness_ai_control = false;
        unit_state.ai_blackboard.madness_target_any_team = false;
    }

    internal BattleCommand BuildMadnessFallbackCommand(BattleUnitState unit_state)
    {
        BattleUnitState activeUnit = unit_state;
        BattleState state = _runtime?._state;
        if (_runtime == null || state == null || activeUnit == null)
        {
            return null;
        }
        foreach (StringName skillId in activeUnit.known_active_skill_ids)
        {
            SkillDef skillDef = _runtime.GetSkillDefTyped(skillId);
            CombatSkillDef combatProfile = skillDef?.combat_profile;
            if (skillDef == null || combatProfile == null)
            {
                continue;
            }
            if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
            {
                continue;
            }
            if (BattleSkillCastBlockReasonKinds.IsBlocked(GetSkillCastBlockReason(activeUnit, skillDef)))
            {
                continue;
            }
            BattleUnitState targetUnit = _find_madness_unit_target(activeUnit, skillDef);
            if (targetUnit == null)
            {
                continue;
            }
            BattleCommand command = NewBattleCommand();
            command.CommandKind = BattleCommandKind.Skill;
            command.unit_id = activeUnit.unit_id;
            command.skill_id = skillId;
            command.target_unit_id = targetUnit.unit_id;
            command.target_coord = targetUnit.coord;
            if (_skill_requires_option(skillDef))
            {
                CombatCastVariantDef firstValue = _pick_first_valid_madness_option(activeUnit, skillDef);
                if (firstValue != null)
                {
                    command.skill_variant_id = new StringName(firstValue.variant_id.ToString());
                }
            }
            return command;
        }
        BattleCommand waitCommand = NewBattleCommand();
        waitCommand.CommandKind = BattleCommandKind.Wait;
        waitCommand.unit_id = activeUnit.unit_id;
        return waitCommand;
    }

    internal BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return BattleSkillCastBlockReasonKind.InvalidSkillOrTarget;
        }
        CombatSkillResourceCosts costs = GetEffectiveSkillResourceCosts(
            active_unit,
            skill_def
        );
        int cooldown = active_unit.GetCooldownTyped(skill_def.skill_id);
        if (cooldown > 0)
        {
            return BattleSkillCastBlockReasonKind.Cooldown;
        }
        BattleSkillCastBlockReasonKind lockedResourceBlockReason = GetLockedCombatResourceBlockReasonKind(
            active_unit,
            costs
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(lockedResourceBlockReason))
        {
            return lockedResourceBlockReason;
        }
        if (
            active_unit.current_ap < costs.ApCost
        )
        {
            return BattleSkillCastBlockReasonKind.InsufficientAp;
        }
        if (
            active_unit.current_mp < costs.MpCost
        )
        {
            return BattleSkillCastBlockReasonKind.InsufficientMp;
        }
        if (
            active_unit.current_stamina < costs.StaminaCost
        )
        {
            return BattleSkillCastBlockReasonKind.InsufficientStamina;
        }
        if (active_unit.HasStatusEffect(STATUS_PETRIFIED))
        {
            return BattleSkillCastBlockReasonKind.Petrified;
        }
        if (
            active_unit.current_aura < costs.AuraCost
        )
        {
            return BattleSkillCastBlockReasonKind.InsufficientAura;
        }
        BattleSkillCastBlockReasonKind racialChargeBlockReason = GetRacialSkillChargeBlockReason(
            active_unit,
            skill_def
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(racialChargeBlockReason))
        {
            return racialChargeBlockReason;
        }
        if (
            combatProfile.required_weapon_families.Count > 0
            && !UnitMatchesRequiredWeaponFamilies(
                active_unit,
                combatProfile.required_weapon_families
            )
        )
        {
            return BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing;
        }
        if (
            combatProfile.requires_equipped_shield
            && !UnitHasEquippedShield(active_unit)
        )
        {
            return BattleSkillCastBlockReasonKind.ShieldRequired;
        }
        if (RequiresMeleeWeapon(skill_def) && !UnitHasMeleeWeapon(active_unit))
        {
            return BattleSkillCastBlockReasonKind.MeleeWeaponRequired;
        }
        if (
            combatProfile.excluded_weapon_families.Count > 0
            && combatProfile.excluded_weapon_families.Contains(active_unit.weapon_family)
        )
        {
            return BattleSkillCastBlockReasonKind.ExcludedWeaponFamily;
        }
        if (
            combatProfile.excluded_weapon_type_ids.Count > 0
            && combatProfile.excluded_weapon_type_ids.Contains(active_unit.weapon_profile_type_id)
        )
        {
            return BattleSkillCastBlockReasonKind.ExcludedWeaponType;
        }
        if (IsMainSkillLockedByStatus(active_unit, skill_def))
        {
            return BattleSkillCastBlockReasonKind.MainSkillLockedByStatus;
        }
        string misfortuneBlockReason = GetMisfortuneSkillCastBlockReason(
            active_unit,
            skill_def
        );
        if (!string.IsNullOrEmpty(misfortuneBlockReason))
        {
            return _runtime == null
                ? BattleSkillCastBlockReasonKind.MisfortuneSidecarMissing
                : BattleSkillCastBlockReasonKind.MisfortuneBlocked;
        }
        if (
            active_unit.HasStatusEffect(STATUS_BLACK_STAR_BRAND_NORMAL)
            && _runtime._skill_grants_guarding(skill_def)
        )
        {
            return BattleSkillCastBlockReasonKind.BlackStarGuardLock;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    internal BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleUnitReadView active_unit,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (!active_unit.IsValid || skill_def == null || combatProfile == null)
        {
            return BattleSkillCastBlockReasonKind.InvalidSkillOrTarget;
        }
        CombatSkillResourceCosts costs = GetEffectiveSkillResourceCosts(
            active_unit,
            skill_def
        );
        int cooldown = active_unit.GetCooldown(skill_def.skill_id);
        if (cooldown > 0)
        {
            return BattleSkillCastBlockReasonKind.Cooldown;
        }
        BattleSkillCastBlockReasonKind lockedResourceBlockReason = GetLockedCombatResourceBlockReasonKind(
            active_unit,
            costs
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(lockedResourceBlockReason))
        {
            return lockedResourceBlockReason;
        }
        if (active_unit.CurrentAp < costs.ApCost)
        {
            return BattleSkillCastBlockReasonKind.InsufficientAp;
        }
        if (active_unit.CurrentMp < costs.MpCost)
        {
            return BattleSkillCastBlockReasonKind.InsufficientMp;
        }
        if (active_unit.CurrentStamina < costs.StaminaCost)
        {
            return BattleSkillCastBlockReasonKind.InsufficientStamina;
        }
        if (active_unit.HasStatusEffect(STATUS_PETRIFIED))
        {
            return BattleSkillCastBlockReasonKind.Petrified;
        }
        if (active_unit.CurrentAura < costs.AuraCost)
        {
            return BattleSkillCastBlockReasonKind.InsufficientAura;
        }
        BattleSkillCastBlockReasonKind racialChargeBlockReason = GetRacialSkillChargeBlockReason(
            active_unit,
            skill_def
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(racialChargeBlockReason))
        {
            return racialChargeBlockReason;
        }
        if (
            combatProfile.required_weapon_families.Count > 0
            && !UnitMatchesRequiredWeaponFamilies(
                active_unit,
                combatProfile.required_weapon_families
            )
        )
        {
            return BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing;
        }
        if (
            combatProfile.requires_equipped_shield
            && !UnitHasEquippedShield(active_unit)
        )
        {
            return BattleSkillCastBlockReasonKind.ShieldRequired;
        }
        if (RequiresMeleeWeapon(skill_def) && !UnitHasMeleeWeapon(active_unit))
        {
            return BattleSkillCastBlockReasonKind.MeleeWeaponRequired;
        }
        if (
            combatProfile.excluded_weapon_families.Count > 0
            && combatProfile.excluded_weapon_families.Contains(active_unit.WeaponFamily)
        )
        {
            return BattleSkillCastBlockReasonKind.ExcludedWeaponFamily;
        }
        if (
            combatProfile.excluded_weapon_type_ids.Count > 0
            && combatProfile.excluded_weapon_type_ids.Contains(active_unit.WeaponProfileTypeId)
        )
        {
            return BattleSkillCastBlockReasonKind.ExcludedWeaponType;
        }
        if (IsMainSkillLockedByStatus(active_unit, skill_def))
        {
            return BattleSkillCastBlockReasonKind.MainSkillLockedByStatus;
        }
        if (
            active_unit.HasStatusEffect(STATUS_BLACK_STAR_BRAND_NORMAL)
            && _runtime._skill_grants_guarding(skill_def)
        )
        {
            return BattleSkillCastBlockReasonKind.BlackStarGuardLock;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    internal string FormatSkillCastBlockReason(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleSkillCastBlockReasonKind reason_kind,
        CombatCastVariantDef cast_variant = null
    )
    {
        if (!BattleSkillCastBlockReasonKinds.IsBlocked(reason_kind))
        {
            return "";
        }
        return reason_kind switch
        {
            BattleSkillCastBlockReasonKind.InvalidSkillOrTarget => "技能或目标无效。",
            BattleSkillCastBlockReasonKind.SkillCastCheckUnbound =>
                "正式技能检查未绑定，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InvalidCaster => "技能施放者无效。",
            BattleSkillCastBlockReasonKind.Cooldown =>
                $"{DisplayName(skill_def)} 仍在冷却中（{active_unit?.GetCooldownTyped(skill_def?.skill_id ?? Empty) ?? 0}）。",
            BattleSkillCastBlockReasonKind.MpLocked => "法力尚未解锁，无法施放该技能。",
            BattleSkillCastBlockReasonKind.StaminaLocked => "体力尚未解锁，无法施放该技能。",
            BattleSkillCastBlockReasonKind.AuraLocked => "斗气尚未解锁，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InsufficientAp => "AP不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InsufficientMp => "法力不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InsufficientStamina => "体力不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.Petrified => "当前处于石化状态，无法施放技能。",
            BattleSkillCastBlockReasonKind.InsufficientAura => "斗气不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.RacialSkillPerBattleChargeDepleted =>
                $"{_get_skill_display_name(skill_def)} 的身份技能次数已用尽。",
            BattleSkillCastBlockReasonKind.RacialSkillPerTurnChargeDepleted =>
                $"{_get_skill_display_name(skill_def)} 本回合无法再次使用。",
            BattleSkillCastBlockReasonKind.RacialSkillChargeUninitialized =>
                $"{_get_skill_display_name(skill_def)} 的身份技能次数未初始化。",
            BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing =>
                "需要装备指定武器家族，无法施放该技能。",
            BattleSkillCastBlockReasonKind.ShieldRequired => "需要装备盾牌，无法施放该技能。",
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired =>
                "需要装备有效武器，无法施放该技能。",
            BattleSkillCastBlockReasonKind.ExcludedWeaponFamily =>
                "当前武器类型无法施放该技能。",
            BattleSkillCastBlockReasonKind.ExcludedWeaponType =>
                "当前武器类型无法施放该技能。",
            BattleSkillCastBlockReasonKind.MainSkillLockedByStatus =>
                "厄命宣判压制了主技能，无法施放该技能。",
            BattleSkillCastBlockReasonKind.MisfortuneSidecarMissing =>
                MisfortuneService.GetSkillSidecarMissingMessage(skill_def?.skill_id ?? Empty),
            BattleSkillCastBlockReasonKind.MisfortuneBlocked =>
                GetMisfortuneSkillCastBlockReason(active_unit, skill_def),
            BattleSkillCastBlockReasonKind.BlackStarGuardLock =>
                "黑星烙印封锁了格挡，无法施放该技能。",
            BattleSkillCastBlockReasonKind.BlackContractPushVariantMissing =>
                "黑契推进需要先选择一个代价分支。",
            BattleSkillCastBlockReasonKind.BlackContractPushHpCostUnavailable =>
                "当前生命不足，无法支付血契代价。",
            BattleSkillCastBlockReasonKind.BlackContractPushGuardCostUnavailable =>
                "当前没有 Guard，无法支付护契代价。",
            BattleSkillCastBlockReasonKind.BlackContractPushInvalidVariant =>
                "黑契推进的施法形态无效。",
            _ => "",
        };
    }

    internal string FormatSkillCastBlockReason(
        BattleUnitReadView active_unit,
        SkillDef skill_def,
        BattleSkillCastBlockReasonKind reason_kind,
        CombatCastVariantDef cast_variant = null
    )
    {
        if (!BattleSkillCastBlockReasonKinds.IsBlocked(reason_kind))
        {
            return "";
        }
        return reason_kind switch
        {
            BattleSkillCastBlockReasonKind.InvalidSkillOrTarget => "技能或目标无效。",
            BattleSkillCastBlockReasonKind.SkillCastCheckUnbound =>
                "正式技能检查未绑定，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InvalidCaster => "技能施放者无效。",
            BattleSkillCastBlockReasonKind.Cooldown =>
                $"{DisplayName(skill_def)} 仍在冷却中（{active_unit.GetCooldown(skill_def?.skill_id ?? Empty)}）。",
            BattleSkillCastBlockReasonKind.MpLocked => "法力尚未解锁，无法施放该技能。",
            BattleSkillCastBlockReasonKind.StaminaLocked => "体力尚未解锁，无法施放该技能。",
            BattleSkillCastBlockReasonKind.AuraLocked => "斗气尚未解锁，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InsufficientAp => "AP不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InsufficientMp => "法力不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.InsufficientStamina => "体力不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.Petrified => "当前处于石化状态，无法施放技能。",
            BattleSkillCastBlockReasonKind.InsufficientAura => "斗气不足，无法施放该技能。",
            BattleSkillCastBlockReasonKind.RacialSkillPerBattleChargeDepleted =>
                $"{_get_skill_display_name(skill_def)} 的身份技能次数已用尽。",
            BattleSkillCastBlockReasonKind.RacialSkillPerTurnChargeDepleted =>
                $"{_get_skill_display_name(skill_def)} 本回合无法再次使用。",
            BattleSkillCastBlockReasonKind.RacialSkillChargeUninitialized =>
                $"{_get_skill_display_name(skill_def)} 的身份技能次数未初始化。",
            BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing =>
                "需要装备指定武器家族，无法施放该技能。",
            BattleSkillCastBlockReasonKind.ShieldRequired => "需要装备盾牌，无法施放该技能。",
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired =>
                "需要装备有效武器，无法施放该技能。",
            BattleSkillCastBlockReasonKind.ExcludedWeaponFamily =>
                "当前武器类型无法施放该技能。",
            BattleSkillCastBlockReasonKind.ExcludedWeaponType =>
                "当前武器类型无法施放该技能。",
            BattleSkillCastBlockReasonKind.MainSkillLockedByStatus =>
                "厄命宣判压制了主技能，无法施放该技能。",
            BattleSkillCastBlockReasonKind.MisfortuneSidecarMissing =>
                MisfortuneService.GetSkillSidecarMissingMessage(skill_def?.skill_id ?? Empty),
            BattleSkillCastBlockReasonKind.MisfortuneBlocked =>
                MisfortuneService.GetSkillDefaultBlockMessage(skill_def?.skill_id ?? Empty),
            BattleSkillCastBlockReasonKind.BlackStarGuardLock =>
                "黑星烙印封锁了格挡，无法施放该技能。",
            BattleSkillCastBlockReasonKind.BlackContractPushVariantMissing =>
                "黑契推进需要先选择一个代价分支。",
            BattleSkillCastBlockReasonKind.BlackContractPushHpCostUnavailable =>
                "当前生命不足，无法支付血契代价。",
            BattleSkillCastBlockReasonKind.BlackContractPushGuardCostUnavailable =>
                "当前没有 Guard，无法支付护契代价。",
            BattleSkillCastBlockReasonKind.BlackContractPushInvalidVariant =>
                "黑契推进的施法形态无效。",
            _ => "",
        };
    }

    internal bool UnitHasMeleeWeapon(BattleUnitState active_unit) =>
        BattleRangeService.UnitHasMeleeWeapon(active_unit);

    internal bool UnitHasMeleeWeapon(BattleUnitReadView active_unit) =>
        active_unit.IsValid
        && active_unit.WeaponProfileKind == new StringName("equipped")
        && active_unit.WeaponAttackRange > 0
        && active_unit.WeaponPhysicalDamageTag != "";

    private bool UnitMatchesRequiredWeaponFamilies(
        BattleUnitState active_unit,
        Godot.Collections.Array<StringName> required_weapon_families
    )
    {
        return BattleRangeService.UnitMatchesRequiredWeaponFamilies(
            active_unit,
            required_weapon_families
        );
    }

    private bool UnitMatchesRequiredWeaponFamilies(
        BattleUnitReadView active_unit,
        Godot.Collections.Array<StringName> required_weapon_families
    )
    {
        bool hasRequiredFamily = false;
        if (required_weapon_families == null)
        {
            return true;
        }
        foreach (StringName familyValue in required_weapon_families)
        {
            if (familyValue == "")
            {
                continue;
            }
            hasRequiredFamily = true;
            if (!UnitHasMeleeWeapon(active_unit))
            {
                return false;
            }
            if (active_unit.WeaponFamily == "")
            {
                return false;
            }
            if (familyValue == active_unit.WeaponFamily)
            {
                return true;
            }
        }
        return !hasRequiredFamily;
    }

    private bool UnitHasEquippedShield(BattleUnitState active_unit)
    {
        IReadOnlyDictionary<StringName, ItemDef> itemDefs =
            _runtime != null
                ? _runtime.GetItemDefIndexTyped()
                : new Dictionary<StringName, ItemDef>();
        return BattleEquipmentRequirementRules.UnitHasEquippedShield(active_unit, itemDefs);
    }

    private bool UnitHasEquippedShield(BattleUnitReadView active_unit)
    {
        IReadOnlyDictionary<StringName, ItemDef> itemDefs =
            _runtime != null
                ? _runtime.GetItemDefIndexTyped()
                : new Dictionary<StringName, ItemDef>();
        if (!active_unit.IsValid || itemDefs == null || itemDefs.Count == 0)
        {
            return false;
        }
        EquipmentState equipmentView = active_unit.DuplicateEquipmentView();
        if (equipmentView == null)
        {
            return false;
        }
        StringName offhand = EquipmentRules.ToStringName(EquipmentSlotKind.OffHand);
        StringName itemId = ProgressionDataUtils.to_string_name(
            equipmentView.GetEquippedItemId(offhand)
        );
        return itemId != ""
            && itemDefs.TryGetValue(itemId, out ItemDef itemDef)
            && BattleEquipmentRequirementRules.ItemHasTag(itemDef, new StringName("shield"));
    }

    internal bool _skill_requires_option(SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        return skill_def != null
            && combatProfile != null
            && combatProfile.cast_variants.Count > 0;
    }

    internal CombatCastVariantDef _pick_first_valid_madness_option(
        BattleUnitState unit_state,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        foreach (CombatCastVariantDef castVariant in combatProfile.cast_variants)
        {
            if (castVariant == null)
            {
                continue;
            }
            int skillLevel = _runtime._get_unit_skill_level(
                unit_state,
                skill_def.skill_id
            );
            if (skillLevel < castVariant.min_skill_level)
            {
                continue;
            }
            return castVariant;
        }
        return null;
    }

    internal bool RequiresMeleeWeapon(SkillDef skill_def) =>
        BattleRangeService.RequiresCurrentMeleeWeapon(skill_def);

    internal bool EffectUsesWeaponPhysicalDamageTag(CombatEffectDef effect_def)
    {
        return BattleRangeService.EffectUsesWeaponPhysicalDamageTag(effect_def);
    }

    internal string GetSkillCommandBlockReason(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        BattleSkillCastBlockReasonKind blockReason = GetSkillCastBlockReason(
            active_unit,
            skill_def
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            return FormatSkillCastBlockReason(active_unit, skill_def, blockReason);
        }
        if (_is_black_contract_push_skill(skill_def.skill_id))
        {
            BattleSkillCastBlockReasonKind variantBlockReason =
                GetBlackContractPushVariantBlockReason(active_unit, cast_variant);
            return FormatSkillCastBlockReason(
                active_unit,
                skill_def,
                variantBlockReason,
                cast_variant
            );
        }
        return "";
    }

    internal string GetSkillCommandBlockReason(
        BattleUnitReadView active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        BattleSkillCastBlockReasonKind blockReason = GetSkillCastBlockReason(
            active_unit,
            skill_def
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            return FormatSkillCastBlockReason(active_unit, skill_def, blockReason);
        }
        if (_is_black_contract_push_skill(skill_def.skill_id))
        {
            BattleSkillCastBlockReasonKind variantBlockReason =
                GetBlackContractPushVariantBlockReason(active_unit, cast_variant);
            return FormatSkillCastBlockReason(
                active_unit,
                skill_def,
                variantBlockReason,
                cast_variant
            );
        }
        return "";
    }

    internal string GetMisfortuneSkillCastBlockReason(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        StringName skillId = skill_def?.skill_id ?? Empty;
        if (
            skill_def == null
            || !MisfortuneService.IsMisfortuneGatedSkill(skillId)
        )
        {
            return "";
        }
        if (_runtime == null)
        {
            return MisfortuneService.GetSkillSidecarMissingMessage(skillId);
        }
        return _runtime.GetMisfortuneSkillCastBlockReason(active_unit, skillId);
    }

    internal bool ConsumeSkillCosts(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null,
        BattleEventBatch batch = null
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return false;
        }
        CombatSkillResourceCosts costs = GetEffectiveSkillResourceCosts(
            active_unit,
            skill_def
        );
        BattleSkillCastBlockReasonKind lockedResourceBlockReason =
            GetLockedCombatResourceBlockReasonKind(
                active_unit,
                costs
            );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(lockedResourceBlockReason))
        {
            AppendLog(
                batch,
                FormatSkillCastBlockReason(active_unit, skill_def, lockedResourceBlockReason)
            );
            return false;
        }
        if (
            _is_black_contract_push_skill(skill_def.skill_id)
            && !ConsumeBlackContractPushCast(active_unit, cast_variant, batch)
        )
        {
            return false;
        }
        if (!ConsumeMisfortuneSkillGate(active_unit, skill_def, batch))
        {
            return false;
        }
        if (
            !ConsumeRacialSkillCharge(
                active_unit,
                skill_def,
                batch
            )
        )
        {
            return false;
        }
        active_unit.SetCurrentAp(active_unit.current_ap - costs.ApCost);
        active_unit.SetCurrentMp(active_unit.current_mp - costs.MpCost);
        active_unit.SetCurrentStamina(active_unit.current_stamina - costs.StaminaCost);
        active_unit.SetCurrentAura(active_unit.current_aura - costs.AuraCost);
        int cooldown = Math.Max(costs.CooldownTu, 0);
        if (cooldown > 0)
        {
            active_unit.SetCooldownTyped(skill_def.skill_id, cooldown);
        }
        return true;
    }

    internal SkillCostTransaction BuildSkillCostTransaction(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        if (active_unit == null || skill_def?.combat_profile == null)
        {
            return new SkillCostTransaction();
        }
        CombatSkillResourceCosts costs = GetEffectiveSkillResourceCosts(active_unit, skill_def);
        return new SkillCostTransaction
        {
            SkillId = skill_def.skill_id,
            SkillLevel = _runtime?._get_unit_skill_level(active_unit, skill_def.skill_id) ?? 1,
            ApCost = Math.Max(costs.ApCost, 0),
            MpCost = Math.Max(costs.MpCost, 0),
            StaminaCost = Math.Max(costs.StaminaCost, 0),
            AuraCost = Math.Max(costs.AuraCost, 0),
            CooldownTurns = Math.Max(costs.CooldownTu, 0),
        };
    }

    internal bool ConsumePendingCastResourceCosts(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch,
        out SkillCostTransaction transaction
    )
    {
        transaction = BuildSkillCostTransaction(active_unit, skill_def);
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return false;
        }
        CombatSkillResourceCosts costs = GetEffectiveSkillResourceCosts(active_unit, skill_def);
        BattleSkillCastBlockReasonKind lockedResourceBlockReason =
            GetLockedCombatResourceBlockReasonKind(active_unit, costs);
        if (BattleSkillCastBlockReasonKinds.IsBlocked(lockedResourceBlockReason))
        {
            AppendLog(
                batch,
                FormatSkillCastBlockReason(active_unit, skill_def, lockedResourceBlockReason)
            );
            return false;
        }
        if (
            _is_black_contract_push_skill(skill_def.skill_id)
            && !ConsumeBlackContractPushCast(active_unit, cast_variant, batch)
        )
        {
            return false;
        }
        if (!ConsumeMisfortuneSkillGate(active_unit, skill_def, batch))
        {
            return false;
        }
        if (!ConsumeRacialSkillCharge(active_unit, skill_def, batch))
        {
            return false;
        }
        active_unit.SetCurrentMp(active_unit.current_mp - costs.MpCost);
        active_unit.SetCurrentStamina(active_unit.current_stamina - costs.StaminaCost);
        active_unit.SetCurrentAura(active_unit.current_aura - costs.AuraCost);
        return true;
    }

    internal void ConsumeCastingFailureApCost(
        BattleUnitState active_unit,
        SkillCostTransaction transaction,
        BattleEventBatch batch = null,
        bool criticalFailure = false
    )
    {
        if (active_unit == null)
        {
            return;
        }
        int apCost = criticalFailure
            ? Math.Max((active_unit.current_ap + 1) / 2, 1)
            : Math.Max(transaction?.ApCost ?? 0, 1);
        active_unit.SetCurrentAp(active_unit.current_ap - apCost);
        active_unit.turn_casting_exhausted = true;
        _runtime?._record_action_issued(
            active_unit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            apCost
        );
        _runtime?._append_changed_unit_id(batch, active_unit.unit_id);
    }

    internal void StartSkillCooldownFromTransaction(
        BattleUnitState active_unit,
        SkillCostTransaction transaction,
        BattleEventBatch batch = null
    )
    {
        if (active_unit == null || transaction == null || transaction.SkillId == Empty)
        {
            return;
        }
        int cooldown = Math.Max(transaction.CooldownTurns, 0);
        if (cooldown <= 0)
        {
            return;
        }
        active_unit.SetCooldownTyped(transaction.SkillId, cooldown);
        _runtime?._append_changed_unit_id(batch, active_unit.unit_id);
    }

    internal void RefundSkillCostTransaction(
        BattleUnitState active_unit,
        SkillCostTransaction transaction,
        PendingCastRefundPolicy refundPolicy,
        BattleEventBatch batch = null
    )
    {
        if (active_unit == null || transaction == null || refundPolicy == PendingCastRefundPolicy.None)
        {
            return;
        }
        int mpRefund = transaction.MpCost / 2;
        int staminaRefund = transaction.StaminaCost / 2;
        int auraRefund = transaction.AuraCost / 2;
        bool changed = false;
        if (mpRefund > 0)
        {
            active_unit.SetCurrentMp(Math.Min(active_unit.current_mp + mpRefund, GetUnitMpMax(active_unit)));
            changed = true;
        }
        if (staminaRefund > 0)
        {
            active_unit.SetCurrentStamina(Math.Min(
                active_unit.current_stamina + staminaRefund,
                GetUnitStaminaMax(active_unit)
            ));
            changed = true;
        }
        if (auraRefund > 0)
        {
            active_unit.SetCurrentAura(Math.Min(active_unit.current_aura + auraRefund, GetUnitAuraMax(active_unit)));
            changed = true;
        }
        if (changed)
        {
            _runtime?._append_changed_unit_id(batch, active_unit.unit_id);
        }
    }

    internal bool IsCastInterruptedByStatus(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return false;
        }
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            if (statusEntry != null && BattleStatusSemanticTable.BlocksPendingCast(statusEntry.status_id))
            {
                return true;
            }
        }
        return false;
    }

    private static int GetUnitMpMax(BattleUnitState unit_state)
    {
        int value =
            unit_state?.attribute_snapshot?.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax))
            ?? 0;
        return Math.Max(value, unit_state?.current_mp ?? 0);
    }

    private int GetUnitStaminaMax(BattleUnitState unit_state)
    {
        return Math.Max(_runtime?._get_unit_stamina_max(unit_state) ?? 0, unit_state?.current_stamina ?? 0);
    }

    private static int GetUnitAuraMax(BattleUnitState unit_state)
    {
        return Math.Max(unit_state?.GetAuraMax() ?? 0, unit_state?.current_aura ?? 0);
    }

    private bool ConsumeMisfortuneSkillGate(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch = null
    )
    {
        StringName skillId = skill_def?.skill_id ?? Empty;
        if (
            skill_def == null
            || !MisfortuneService.IsMisfortuneGatedSkill(skillId)
        )
        {
            return true;
        }
        if (_runtime == null)
        {
            AppendLog(
                batch,
                MisfortuneService.GetSkillSidecarMissingMessage(skillId)
            );
            return false;
        }
        MisfortuneSkillCastResult consumeResult = _runtime.ConsumeMisfortuneSkillCastResult(
            active_unit,
            skillId
        );
        if (!consumeResult.Ok)
        {
            AppendLog(
                batch,
                !string.IsNullOrEmpty(consumeResult.Message)
                    ? consumeResult.Message
                    : MisfortuneService.GetSkillDefaultBlockMessage(skillId)
            );
            return false;
        }
        return true;
    }

    private BattleSkillCastBlockReasonKind GetRacialSkillChargeBlockReason(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        if (active_unit == null || !_is_identity_granted_skill(skill_def))
        {
            return BattleSkillCastBlockReasonKind.None;
        }
        StringName chargeKey = GetRacialSkillChargeKey(skill_def.skill_id);
        if (active_unit.HasPerBattleChargeTyped(chargeKey))
        {
            if (active_unit.GetPerBattleChargeTyped(chargeKey) <= 0)
            {
                return BattleSkillCastBlockReasonKind.RacialSkillPerBattleChargeDepleted;
            }
        }
        else if (active_unit.HasPerTurnChargeTyped(chargeKey))
        {
            if (active_unit.GetPerTurnChargeTyped(chargeKey) <= 0)
            {
                return BattleSkillCastBlockReasonKind.RacialSkillPerTurnChargeDepleted;
            }
        }
        else
        {
            return BattleSkillCastBlockReasonKind.RacialSkillChargeUninitialized;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    private BattleSkillCastBlockReasonKind GetRacialSkillChargeBlockReason(
        BattleUnitReadView active_unit,
        SkillDef skill_def
    )
    {
        if (!active_unit.IsValid || !_is_identity_granted_skill(skill_def))
        {
            return BattleSkillCastBlockReasonKind.None;
        }
        StringName chargeKey = GetRacialSkillChargeKey(skill_def.skill_id);
        if (active_unit.HasPerBattleCharge(chargeKey))
        {
            if (active_unit.GetPerBattleCharge(chargeKey) <= 0)
            {
                return BattleSkillCastBlockReasonKind.RacialSkillPerBattleChargeDepleted;
            }
        }
        else if (active_unit.HasPerTurnCharge(chargeKey))
        {
            if (active_unit.GetPerTurnCharge(chargeKey) <= 0)
            {
                return BattleSkillCastBlockReasonKind.RacialSkillPerTurnChargeDepleted;
            }
        }
        else
        {
            return BattleSkillCastBlockReasonKind.RacialSkillChargeUninitialized;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    private bool ConsumeRacialSkillCharge(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch = null
    )
    {
        BattleSkillCastBlockReasonKind blockReason = GetRacialSkillChargeBlockReason(
            active_unit,
            skill_def
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            AppendLog(batch, FormatSkillCastBlockReason(active_unit, skill_def, blockReason));
            return false;
        }
        if (active_unit == null || !_is_identity_granted_skill(skill_def))
        {
            return true;
        }
        StringName chargeKey = GetRacialSkillChargeKey(skill_def.skill_id);
        if (active_unit.HasPerBattleChargeTyped(chargeKey))
        {
            active_unit.SetPerBattleChargeTyped(
                chargeKey,
                active_unit.GetPerBattleChargeTyped(chargeKey) - 1
            );
        }
        if (active_unit.HasPerTurnChargeTyped(chargeKey))
        {
            active_unit.SetPerTurnChargeTyped(
                chargeKey,
                active_unit.GetPerTurnChargeTyped(chargeKey) - 1
            );
        }
        return true;
    }

    private StringName GetRacialSkillChargeKey(StringName skill_id)
    {
        return skill_id == Empty ? Empty : new StringName($"racial_skill_{skill_id}");
    }

    internal CombatSkillResourceCosts GetEffectiveSkillResourceCosts(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return CombatSkillResourceCosts.Zero;
        }
        int skillLevel =
            _runtime != null ? _runtime._get_unit_skill_level(active_unit, skill_def.skill_id) : 0;
        return combatProfile.GetEffectiveResourceCostValues(skillLevel);
    }

    internal CombatSkillResourceCosts GetEffectiveSkillResourceCosts(
        BattleUnitReadView active_unit,
        SkillDef skill_def
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return CombatSkillResourceCosts.Zero;
        }
        int skillLevel = active_unit.GetKnownSkillLevel(skill_def.skill_id);
        return combatProfile.GetEffectiveResourceCostValues(skillLevel);
    }

    internal BattleSkillCastBlockReasonKind GetLockedCombatResourceBlockReasonKind(
        BattleUnitState active_unit,
        CombatSkillResourceCosts costs
    )
    {
        if (active_unit == null)
        {
            return BattleSkillCastBlockReasonKind.InvalidCaster;
        }
        if (
            costs.MpCost > 0
            && !HasCombatResourceUnlocked(active_unit, CombatResourceMp)
        )
        {
            return BattleSkillCastBlockReasonKind.MpLocked;
        }
        // Stamina (and HP) are in CombatResourceIds.DefaultUnlocked, so every
        // unit always has them unlocked — no lock check needed for stamina.
        if (
            costs.AuraCost > 0
            && !HasCombatResourceUnlocked(active_unit, CombatResourceAura)
        )
        {
            return BattleSkillCastBlockReasonKind.AuraLocked;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    internal BattleSkillCastBlockReasonKind GetLockedCombatResourceBlockReasonKind(
        BattleUnitReadView active_unit,
        CombatSkillResourceCosts costs
    )
    {
        if (!active_unit.IsValid)
        {
            return BattleSkillCastBlockReasonKind.InvalidCaster;
        }
        if (
            costs.MpCost > 0
            && !active_unit.HasCombatResourceUnlocked(CombatResourceMp)
        )
        {
            return BattleSkillCastBlockReasonKind.MpLocked;
        }
        if (
            costs.AuraCost > 0
            && !active_unit.HasCombatResourceUnlocked(CombatResourceAura)
        )
        {
            return BattleSkillCastBlockReasonKind.AuraLocked;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    internal bool _is_identity_granted_skill(SkillDef skill_def)
    {
        return skill_def != null
            && IdentitySkillLearnSources.Contains(skill_def.learn_source);
    }

    internal string _get_skill_display_name(SkillDef skill_def)
    {
        if (skill_def == null)
        {
            return "身份技能";
        }
        string displayName = skill_def.display_name.Trim();
        if (!string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }
        StringName skillId = skill_def.skill_id;
        return skillId != Empty ? skillId.ToString() : "身份技能";
    }

    internal BattleSkillCastBlockReasonKind GetBlackContractPushVariantBlockReason(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        if (active_unit == null)
        {
            return BattleSkillCastBlockReasonKind.InvalidCaster;
        }
        if (cast_variant == null)
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushVariantMissing;
        }
        StringName optionId = cast_variant.variant_id;
        if (
            optionId == BLACK_CONTRACT_PUSH_OPTION_BLOOD
            && active_unit.current_hp <= BLACK_CONTRACT_PUSH_HP_COST
        )
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushHpCostUnavailable;
        }
        if (
            optionId == BLACK_CONTRACT_PUSH_OPTION_GUARD
            && !HasStatus(active_unit, STATUS_GUARDING)
        )
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushGuardCostUnavailable;
        }
        if (optionId == BLACK_CONTRACT_PUSH_OPTION_ACTION)
        {
            return BattleSkillCastBlockReasonKind.None;
        }
        if (
            optionId != BLACK_CONTRACT_PUSH_OPTION_BLOOD
            && optionId != BLACK_CONTRACT_PUSH_OPTION_GUARD
        )
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushInvalidVariant;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    internal BattleSkillCastBlockReasonKind GetBlackContractPushVariantBlockReason(
        BattleUnitReadView active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        if (!active_unit.IsValid)
        {
            return BattleSkillCastBlockReasonKind.InvalidCaster;
        }
        if (cast_variant == null)
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushVariantMissing;
        }
        StringName optionId = cast_variant.variant_id;
        if (
            optionId == BLACK_CONTRACT_PUSH_OPTION_BLOOD
            && active_unit.CurrentHp <= BLACK_CONTRACT_PUSH_HP_COST
        )
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushHpCostUnavailable;
        }
        if (
            optionId == BLACK_CONTRACT_PUSH_OPTION_GUARD
            && !active_unit.HasStatusEffect(STATUS_GUARDING)
        )
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushGuardCostUnavailable;
        }
        if (optionId == BLACK_CONTRACT_PUSH_OPTION_ACTION)
        {
            return BattleSkillCastBlockReasonKind.None;
        }
        if (
            optionId != BLACK_CONTRACT_PUSH_OPTION_BLOOD
            && optionId != BLACK_CONTRACT_PUSH_OPTION_GUARD
        )
        {
            return BattleSkillCastBlockReasonKind.BlackContractPushInvalidVariant;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    internal bool ConsumeBlackContractPushCast(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch = null
    )
    {
        BattleSkillCastBlockReasonKind blockReason = GetBlackContractPushVariantBlockReason(
            active_unit,
            cast_variant
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            AppendLog(
                batch,
                FormatSkillCastBlockReason(active_unit, null, blockReason, cast_variant)
            );
            return false;
        }
        if (active_unit == null || cast_variant == null)
        {
            return false;
        }
        StringName optionId = cast_variant.variant_id;
        if (optionId == BLACK_CONTRACT_PUSH_OPTION_BLOOD)
        {
            active_unit.SetCurrentHp(Math.Max(
                active_unit.current_hp - BLACK_CONTRACT_PUSH_HP_COST,
                1
            ));
            AppendLog(
                batch,
                $"{DisplayName(active_unit)} 以血契推进，先失去 {BLACK_CONTRACT_PUSH_HP_COST} 点生命。"
            );
        }
        else if (optionId == BLACK_CONTRACT_PUSH_OPTION_GUARD)
        {
            EraseStatusEffect(active_unit, STATUS_GUARDING);
            AppendLog(batch, $"{DisplayName(active_unit)} 拆解了自己的 Guard，换取这次黑契推进。");
        }
        else if (optionId == BLACK_CONTRACT_PUSH_OPTION_ACTION)
        {
            _runtime._set_runtime_debuff_status_effect(
                active_unit,
                STATUS_STAGGERED,
                DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
                active_unit.unit_id,
                1
            );
            AppendLog(
                batch,
                $"{DisplayName(active_unit)} 透支了下一回合的行动力，换取这次黑契推进。"
            );
        }
        _runtime._append_changed_unit_id(batch, active_unit.unit_id);
        return true;
    }

    internal void EnsureUnitTurnAnchor(BattleUnitState unit_state)
    {
        if (unit_state == null || unit_state.last_turn_tu >= 0)
        {
            return;
        }
        unit_state.last_turn_tu = _runtime?._state?.timeline?.current_tu ?? 0;
    }

    internal bool AdvanceUnitCooldowns(BattleUnitState unit_state, int cooldown_delta)
    {
        if (unit_state == null || cooldown_delta <= 0)
        {
            return false;
        }
        Dictionary<StringName, int> previousCooldowns = unit_state.GetCooldownsTyped();
        var retainedCooldowns = new Dictionary<StringName, int>();
        foreach (KeyValuePair<StringName, int> entry in previousCooldowns)
        {
            int remaining = Math.Max(entry.Value - cooldown_delta, 0);
            if (remaining > 0)
            {
                retainedCooldowns[entry.Key] = remaining;
            }
        }
        unit_state.SetCooldownsTyped(retainedCooldowns);
        return !CooldownMapsEqual(previousCooldowns, retainedCooldowns);
    }

    internal bool ConsumeTurnCooldownDelta(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return false;
        }
        int currentTu = _runtime?._state?.timeline?.current_tu ?? 0;
        if (unit_state.last_turn_tu < 0)
        {
            unit_state.last_turn_tu = currentTu;
            return false;
        }
        int elapsedTu = Math.Max(currentTu - unit_state.last_turn_tu, 0);
        unit_state.last_turn_tu = currentTu;
        if (elapsedTu <= 0)
        {
            return false;
        }
        if (elapsedTu % TU_GRANULARITY != 0)
        {
            GameLog.Error($"Cooldown delta must use {TU_GRANULARITY} TU steps, got {elapsedTu}.", "battle.skill.invalid_cooldown_delta", "battle");
            return false;
        }
        return AdvanceUnitCooldowns(unit_state, elapsedTu);
    }

    internal void AdvanceUnitTurnTimers(BattleUnitState unit_state, BattleEventBatch batch)
    {
        if (unit_state == null)
        {
            return;
        }
        bool changed = ConsumeTurnCooldownDelta(unit_state);
        int previousStatusCount = unit_state.status_effects.Count;
        unit_state.GetStatusEffectsTyped();
        if (unit_state.status_effects.Count != previousStatusCount)
        {
            changed = true;
        }
        if (changed)
        {
            _runtime._append_changed_unit_id(
                batch,
                unit_state.unit_id
            );
        }
    }

    internal BattleStatusTickResult ApplyTurnStartStatusesResult(
        BattleUnitState unit_state,
        BattleEventBatch batch
    )
    {
        if (unit_state == null)
        {
            return BattleStatusTickResult.Empty();
        }
        bool changed = false;
        var penaltyByGroup = new Dictionary<StringName, int>();
        var labelByGroup = new Dictionary<StringName, string>();
        var consumeStatusIds = new List<StringName>();
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            int apPenalty = BattleStatusSemanticTable.GetTurnStartApPenalty(statusEntry);
            if (apPenalty <= 0)
            {
                continue;
            }
            StringName penaltyGroup =
                BattleStatusSemanticTable.GetTurnStartApPenaltyGroup(statusEntry);
            if (penaltyGroup == Empty)
            {
                penaltyGroup = statusEntry.status_id;
            }
            if (
                !penaltyByGroup.TryGetValue(penaltyGroup, out int previousPenalty)
                || apPenalty > previousPenalty
            )
            {
                penaltyByGroup[penaltyGroup] = apPenalty;
                labelByGroup[penaltyGroup] =
                    BattleStatusSemanticTable.GetTurnStartApPenaltyDisplayLabel(
                        statusEntry
                    );
            }
            if (
                BattleStatusSemanticTable.ShouldConsumeAfterTurnStartApPenalty(
                    statusEntry
                )
            )
            {
                consumeStatusIds.Add(statusEntry.status_id);
            }
        }
        var sortedGroupIds = new List<StringName>(penaltyByGroup.Keys);
        sortedGroupIds.Sort(
            (left, right) => StringComparer.Ordinal.Compare(left.ToString(), right.ToString())
        );
        foreach (StringName groupId in sortedGroupIds)
        {
            int groupPenalty = penaltyByGroup[groupId];
            if (groupPenalty <= 0)
            {
                continue;
            }
            int previousAp = unit_state.current_ap;
            unit_state.SetCurrentAp(previousAp - groupPenalty);
            int consumedAp = previousAp - unit_state.current_ap;
            if (consumedAp > 0)
            {
                changed = true;
                AppendLog(
                    batch,
                    $"{DisplayName(unit_state)} 受到{labelByGroup.GetValueOrDefault(groupId, "状态")}影响，本回合少 {consumedAp} 点 AP。"
                );
            }
        }
        foreach (StringName statusId in consumeStatusIds)
        {
            if (HasStatus(unit_state, statusId))
            {
                EraseStatusEffect(unit_state, statusId);
                changed = true;
            }
        }
        if (changed)
        {
            _runtime._append_changed_unit_id(
                batch,
                unit_state.unit_id
            );
        }
        return new BattleStatusTickResult(changed, "");
    }

    internal BattleStatusTickResult ApplyUnitStatusPeriodicTicksResult(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    )
    {
        BattleTimelineState timeline = _runtime?._state?.timeline;
        if (timeline == null || unit_state == null || elapsed_tu <= 0)
        {
            return BattleStatusTickResult.Empty();
        }
        bool changed = false;
        StringName defeatSourceUnitId = Empty;
        int currentTu = timeline.current_tu;
        int previousTu = Math.Max(currentTu - elapsed_tu, 0);
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            if (!unit_state.is_alive)
            {
                break;
            }
            int tickDamage = BattleStatusSemanticTable.GetTimelineTickDamage(statusEntry);
            if (tickDamage <= 0)
            {
                continue;
            }
            if (statusEntry.next_tick_at_tu <= previousTu)
            {
                statusEntry.next_tick_at_tu = previousTu + statusEntry.tick_interval_tu;
                changed = true;
            }
            int tickLimitTu = currentTu;
            if (statusEntry.HasDuration())
            {
                tickLimitTu = Math.Min(
                    tickLimitTu,
                    previousTu + statusEntry.duration
                );
            }
            while (
                unit_state.is_alive
                && statusEntry.next_tick_at_tu > 0
                && statusEntry.next_tick_at_tu <= tickLimitTu
            )
            {
                int previousHp = unit_state.current_hp;
                unit_state.ApplyHpDamage(tickDamage);
                statusEntry.next_tick_at_tu += statusEntry.tick_interval_tu;
                if (unit_state.current_hp != previousHp)
                {
                    changed = true;
                    AppendLog(
                        batch,
                        $"{DisplayName(unit_state)} 受到 {statusEntry.status_id} 持续影响，损失 {previousHp - unit_state.current_hp} 点生命。"
                    );
                    if (
                        !unit_state.is_alive
                        && statusEntry.source_unit_id != Empty
                    )
                    {
                        defeatSourceUnitId = statusEntry.source_unit_id;
                    }
                }
            }
            if (unit_state.is_alive)
            {
                unit_state.SetStatusEffect(statusEntry);
            }
        }
        return new BattleStatusTickResult(changed, defeatSourceUnitId);
    }

    internal bool AdvanceUnitStatusDurations(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null)
        {
            return false;
        }
        bool changed = false;
        var expiredStatusIds = new List<StringName>();
        var expiredStatusEntries = new Dictionary<StringName, BattleStatusEffectState>();
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            BattleStatusDurationAdvanceResult durationResult =
                BattleStatusSemanticTable.AdvanceTimelineDurationResult(
                statusEntry,
                elapsed_tu
            );
            if (durationResult.Expired)
            {
                expiredStatusIds.Add(statusEntry.status_id);
                expiredStatusEntries[statusEntry.status_id] = statusEntry;
                changed = true;
                continue;
            }
            if (durationResult.Changed)
            {
                unit_state.SetStatusEffect(statusEntry);
                changed = true;
            }
        }
        foreach (StringName expiredStatusId in expiredStatusIds)
        {
            expiredStatusEntries.TryGetValue(
                expiredStatusId,
                out BattleStatusEffectState expiredStatusEntry
            );
            bool shouldEraseStatus = true;
            if (_is_body_size_category_override_status(expiredStatusEntry))
            {
                shouldEraseStatus = false;
                if (
                    _restore_body_size_category_override_if_needed(
                        unit_state,
                        expiredStatusEntry,
                        batch
                    )
                )
                {
                    changed = true;
                    shouldEraseStatus = true;
                }
                else if (_body_size_already_matches_previous(unit_state, expiredStatusEntry))
                {
                    shouldEraseStatus = true;
                }
            }
            if (shouldEraseStatus)
            {
                EraseStatusEffect(unit_state, expiredStatusId);
            }
        }
        return changed;
    }

    internal bool _body_size_already_matches_previous(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry
    )
    {
        if (unit_state == null || status_entry == null)
        {
            return false;
        }
        if (!BodySizeContentRules.IsValidBodySizeCategory(status_entry.body_size_category_override))
        {
            return false;
        }
        StringName previousCategory = status_entry.previous_body_size_category;
        if (!BodySizeContentRules.IsValidBodySizeCategory(previousCategory))
        {
            return false;
        }
        return unit_state.body_size_category == previousCategory;
    }

    internal bool _is_body_size_category_override_status(BattleStatusEffectState status_entry)
    {
        return status_entry != null
            && BodySizeContentRules.IsValidBodySizeCategory(
                status_entry.body_size_category_override
            );
    }

    internal bool _restore_body_size_category_override_if_needed(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null || status_entry == null)
        {
            return false;
        }
        if (!BodySizeContentRules.IsValidBodySizeCategory(status_entry.body_size_category_override))
        {
            return false;
        }
        StringName previousCategory = status_entry.previous_body_size_category;
        if (!BodySizeContentRules.IsValidBodySizeCategory(previousCategory))
        {
            return false;
        }
        if (unit_state.body_size_category == previousCategory)
        {
            return false;
        }
        GVector2IArray previousCoords = DuplicateCoordArray(unit_state.occupied_coords);
        StringName currentCategory = unit_state.body_size_category;
        BattleRuntimeModule runtime = _runtime;
        BattleGridService gridService = runtime?.GetGridService();
        BattleState state = runtime?._state;
        if (gridService != null && state != null)
        {
            gridService.ClearUnitOccupancy(state, unit_state);
        }
        unit_state.SetBodySizeCategory(previousCategory);
        if (gridService != null && state != null)
        {
            if (
                !gridService.CanPlaceUnit(
                    state,
                    unit_state,
                    unit_state.coord,
                    true
                )
            )
            {
                unit_state.SetBodySizeCategory(currentCategory);
                gridService.SetOccupantsTyped(state, previousCoords, unit_state.unit_id);
                return false;
            }
            gridService.SetOccupantsTyped(state, unit_state.occupied_coords, unit_state.unit_id);
        }
        if (runtime != null && batch != null)
        {
            runtime._append_changed_coords(batch, previousCoords);
            runtime._append_changed_unit_coords(batch, unit_state);
            runtime._append_changed_unit_id(batch, unit_state.unit_id);
        }
        return true;
    }

    internal int GetEffectiveSkillRange(BattleUnitState active_unit, SkillDef skill_def) =>
        BattleRangeService.GetEffectiveSkillRange(
            active_unit,
            skill_def
        );

    internal int GetEffectiveSkillRange(BattleUnitReadView active_unit, SkillDef skill_def) =>
        BattleRangeService.GetEffectiveSkillRange(
            active_unit,
            skill_def
        );

    internal int ResolveBaseSkillRange(BattleUnitState active_unit, SkillDef skill_def) =>
        BattleRangeService.ResolveBaseSkillRange(
            active_unit,
            skill_def
        );

    internal bool IsWeaponRangeSkill(SkillDef skill_def) =>
        BattleRangeService.IsWeaponRangeSkill(skill_def);

    internal int GetWeaponAttackRange(BattleUnitState active_unit) =>
        BattleRangeService.GetWeaponAttackRange(active_unit);

    internal bool SkillHasTag(SkillDef skill_def, StringName expected_tag)
    {
        if (skill_def == null || expected_tag == Empty)
        {
            return false;
        }
        foreach (StringName tag in skill_def.TagsTyped)
        {
            if (tag == expected_tag)
            {
                return true;
            }
        }
        return false;
    }

    internal bool IsMovementBlocked(BattleUnitState unit_state)
    {
        return HasUnitStatus(unit_state, STATUS_PINNED)
            || HasUnitStatus(unit_state, STATUS_ROOTED)
            || HasUnitStatus(unit_state, STATUS_TENDON_CUT)
            || HasUnitStatus(unit_state, STATUS_PETRIFIED)
            || BattleTemporalStatusService.HasTimeStasis(unit_state);
    }

    // 静滞冻结：冷却 anchor 跟随战场时间前移（静滞时段不计入冷却消耗，
    // 静滞前已流逝的时间保留待解除后惰性消费），其他状态 duration 不推进，
    // 只有 time_stasis 自身按战场时间减少；自然到期添加 time_reverberation。
    internal bool AdvanceTimeStasisFrozenTimers(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null || elapsed_tu <= 0)
        {
            return false;
        }
        int currentTu = _runtime?._state?.timeline?.current_tu ?? 0;
        if (unit_state.last_turn_tu >= 0)
        {
            unit_state.last_turn_tu = Math.Min(
                unit_state.last_turn_tu + elapsed_tu,
                currentTu
            );
        }
        BattleStatusEffectState stasisEntry = GetStatusEffect(
            unit_state,
            BattleStatusSemanticTable.STATUS_TIME_STASIS
        );
        if (stasisEntry == null)
        {
            return false;
        }
        BattleStatusDurationAdvanceResult durationResult =
            BattleStatusSemanticTable.AdvanceTimelineDurationResult(stasisEntry, elapsed_tu);
        if (durationResult.Expired)
        {
            EraseStatusEffect(unit_state, BattleStatusSemanticTable.STATUS_TIME_STASIS);
            BattleTemporalStatusService.HandleTemporalStatusRemoved(
                unit_state,
                BattleStatusSemanticTable.STATUS_TIME_STASIS,
                TemporalStatusReleaseKind.NaturalExpire
            );
            AppendLog(batch, $"{DisplayName(unit_state)} 的时间静滞消散，残留时间余波。");
            return true;
        }
        if (durationResult.Changed)
        {
            unit_state.SetStatusEffect(stasisEntry);
            return true;
        }
        return false;
    }

    internal bool HasUnitStatus(BattleUnitState unit_state, StringName status_id)
    {
        return unit_state != null
            && status_id != Empty
            && HasStatus(unit_state, status_id);
    }

    internal GDictionary _resolve_status_self_save(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry,
        StringName fallback_ability,
        StringName fallback_tag
    )
    {
        return _resolve_status_self_save_result(
                unit_state,
                status_entry,
                fallback_ability,
                fallback_tag
            )
            .ToDictionary();
    }

    internal BattleSaveResult _resolve_status_self_save_result(
        BattleUnitState unit_state,
        BattleStatusEffectState status_entry,
        StringName fallback_ability,
        StringName fallback_tag
    )
    {
        CombatEffectDef effect = new CombatEffectDef();
        effect.effect_type = StatusEffectType;
        effect.save_dc = Math.Max(
            status_entry?.self_save_dc ?? 16,
            1
        );
        effect.SaveDcModeKind = BattleSaveDcMode.Static;
        effect.save_ability =
            status_entry != null && status_entry.self_save_ability != Empty
                ? status_entry.self_save_ability
                : fallback_ability;
        effect.save_tag =
            status_entry != null && status_entry.self_save_tag != Empty
                ? status_entry.self_save_tag
                : fallback_tag;
        BattleSaveContext context = BattleSaveContext.Empty;
        if (status_entry?.self_save_roll_override is int selfSaveRollOverride)
        {
            context = BattleSaveContext.WithSaveRollOverride(selfSaveRollOverride);
        }
        BattleUnitState sourceUnit = null;
        if (
            _runtime != null
            && _runtime._state != null
            && status_entry != null
            && status_entry.source_unit_id != Empty
        )
        {
            _runtime._state.TryGetUnitTyped(status_entry.source_unit_id, out sourceUnit);
        }
        return BattleSaveResolver.ResolveSaveResult(
            sourceUnit,
            unit_state,
            effect,
            context
        );
    }

    internal BattleUnitState _find_madness_unit_target(BattleUnitState unit_state, SkillDef skill_def)
    {
        BattleState state = _runtime?._state;
        if (_runtime == null || state == null || unit_state == null)
        {
            return null;
        }
        BattleUnitState bestUnit = null;
        int bestDistance = 999999;
        int effectiveRange = _runtime._get_effective_skill_range(
            unit_state,
            skill_def
        );
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == unit_state.unit_id
            )
            {
                continue;
            }
            int distance = _runtime.GetGridService().GetDistanceBetweenUnits(
                unit_state,
                candidate
            );
            if (distance > effectiveRange)
            {
                continue;
            }
            if (bestUnit == null || distance < bestDistance)
            {
                bestUnit = candidate;
                bestDistance = distance;
            }
        }
        return bestUnit;
    }

    internal void _append_changed_unit(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (_runtime == null || batch == null || unit_state == null)
        {
            return;
        }
        _runtime._append_changed_unit_id(batch, unit_state.unit_id);
        _runtime._append_changed_unit_coords(batch, unit_state);
    }

    internal void _append_log(BattleEventBatch batch, string line)
    {
        AppendLog(batch, line);
    }

    internal void ConsumeStatusIfPresent(
        BattleUnitState unit_state,
        StringName status_id,
        BattleEventBatch batch = null
    )
    {
        if (unit_state == null || status_id == Empty || !HasStatus(unit_state, status_id))
        {
            return;
        }
        EraseStatusEffect(unit_state, status_id);
        if (batch != null)
        {
            _runtime._append_changed_unit_id(
                batch,
                unit_state.unit_id
            );
        }
    }

    internal bool IsMainSkillLockedByStatus(BattleUnitState active_unit, SkillDef skill_def)
    {
        if (active_unit == null || skill_def == null)
        {
            return false;
        }
        if (active_unit.known_active_skill_ids.Count == 0)
        {
            return false;
        }
        if (active_unit.known_active_skill_ids[0] != skill_def.skill_id)
        {
            return false;
        }
        int requiredDebuffCount = GetMainSkillLockOtherDebuffCount(active_unit);
        if (requiredDebuffCount <= 0)
        {
            return false;
        }
        return CountDebuffStatuses(active_unit) >= requiredDebuffCount;
    }

    internal bool IsMainSkillLockedByStatus(BattleUnitReadView active_unit, SkillDef skill_def)
    {
        if (!active_unit.IsValid || skill_def == null)
        {
            return false;
        }
        if (active_unit.FirstKnownActiveSkillId == "")
        {
            return false;
        }
        if (active_unit.FirstKnownActiveSkillId != skill_def.skill_id)
        {
            return false;
        }
        int requiredDebuffCount = GetMainSkillLockOtherDebuffCount(active_unit);
        if (requiredDebuffCount <= 0)
        {
            return false;
        }
        return CountDebuffStatuses(active_unit) >= requiredDebuffCount;
    }

    internal int CountDebuffStatuses(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int debuffCount = 0;
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            if (StatusCountsAsDebuff(statusEntry.status_id, statusEntry))
            {
                debuffCount += 1;
            }
        }
        return debuffCount;
    }

    internal int CountDebuffStatuses(BattleUnitReadView unitView)
    {
        if (!unitView.IsValid)
        {
            return 0;
        }
        int debuffCount = 0;
        foreach (BattleStatusReadView statusEntry in unitView.StatusEffects())
        {
            if (StatusCountsAsDebuff(statusEntry.StatusId, statusEntry))
            {
                debuffCount += 1;
            }
        }
        return debuffCount;
    }

    internal bool StatusCountsAsDebuff(
        StringName status_id,
        BattleStatusEffectState status_entry
    )
    {
        if (status_entry != null)
        {
            if (status_entry.counts_as_debuff_override)
            {
                return status_entry.counts_as_debuff;
            }
        }
        return DebuffStatusIds.Contains(status_id);
    }

    internal bool StatusCountsAsDebuff(
        StringName status_id,
        BattleStatusReadView status_entry
    )
    {
        if (status_entry.IsValid)
        {
            if (status_entry.CountsAsDebuffOverride)
            {
                return status_entry.CountsAsDebuff;
            }
        }
        return DebuffStatusIds.Contains(status_id);
    }

    internal bool HasCounterattackLockStatus(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return false;
        }
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            if (statusEntry.lock_counterattack)
            {
                return true;
            }
        }
        return false;
    }

    internal int GetMainSkillLockOtherDebuffCount(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int maxValue = 0;
        foreach (BattleStatusEffectState statusEntry in unit_state.GetStatusEffectsTyped())
        {
            maxValue = Math.Max(statusEntry.main_skill_lock_other_debuff_count, maxValue);
        }
        return maxValue;
    }

    internal int GetMainSkillLockOtherDebuffCount(BattleUnitReadView unitView)
    {
        if (!unitView.IsValid)
        {
            return 0;
        }
        int maxValue = 0;
        foreach (BattleStatusReadView statusEntry in unitView.StatusEffects())
        {
            maxValue = Math.Max(statusEntry.MainSkillLockOtherDebuffCount, maxValue);
        }
        return maxValue;
    }

    internal bool _is_black_contract_push_skill(StringName skill_id)
    {
        return skill_id == BLACK_CONTRACT_PUSH_SKILL_ID;
    }

    private static BattleCommand NewBattleCommand()
    {
        return new BattleCommand();
    }

    private static bool HasCombatResourceUnlocked(BattleUnitState unit, StringName resourceId)
    {
        return unit?.HasCombatResourceUnlocked(resourceId) ?? false;
    }

    private static bool HasStatus(BattleUnitState unit, StringName statusId)
    {
        return unit?.HasStatusEffect(statusId) ?? false;
    }

    private static BattleStatusEffectState GetStatusEffect(BattleUnitState unit, StringName statusId)
    {
        return unit?.GetStatusEffect(statusId);
    }

    private static void EraseStatusEffect(BattleUnitState unit, StringName statusId)
    {
        unit?.EraseStatusEffect(statusId);
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.AddLogLine(line);
    }

    private static string DisplayName(BattleUnitState unitState)
    {
        return unitState?.display_name ?? "";
    }

    private static string DisplayName(SkillDef skillDef)
    {
        return skillDef != null && !string.IsNullOrEmpty(skillDef.display_name)
            ? skillDef.display_name
            : skillDef?.skill_id.ToString() ?? "";
    }

    private static StringName ToStringName<TValue>(TValue value) =>
        ProgressionDataUtils.to_string_name(value);

    private static GVector2IArray DuplicateCoordArray(IEnumerable<Vector2I> source)
    {
        var result = new GVector2IArray();
        foreach (Vector2I coord in source ?? Array.Empty<Vector2I>())
        {
            result.Add(coord);
        }
        return result;
    }


    private static bool CooldownMapsEqual(
        IReadOnlyDictionary<StringName, int> left,
        IReadOnlyDictionary<StringName, int> right
    )
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        foreach (KeyValuePair<StringName, int> entry in left)
        {
            if (!right.TryGetValue(entry.Key, out int rightValue) || rightValue != entry.Value)
            {
                return false;
            }
        }
        return true;
    }

    private static BattleRuntimeModule ResolveWeakRef(
        WeakReference<BattleRuntimeModule> weakRef
    )
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }
}
