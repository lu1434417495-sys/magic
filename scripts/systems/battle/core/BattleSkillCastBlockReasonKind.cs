using System;

public enum BattleSkillCastBlockReasonKind
{
    None = 0,
    InvalidSkillOrTarget,
    SkillCastCheckUnbound,
    InvalidCaster,
    Cooldown,
    MpLocked,
    StaminaLocked,
    AuraLocked,
    InsufficientAp,
    InsufficientMp,
    InsufficientStamina,
    Petrified,
    InsufficientAura,
    RacialSkillPerBattleChargeDepleted,
    RacialSkillPerTurnChargeDepleted,
    RacialSkillChargeUninitialized,
    RequiredWeaponFamilyMissing,
    ShieldRequired,
    MeleeWeaponRequired,
    ExcludedWeaponFamily,
    ExcludedWeaponType,
    MainSkillLockedByStatus,
    MisfortuneSidecarMissing,
    MisfortuneBlocked,
    BlackStarGuardLock,
    BlackContractPushVariantMissing,
    BlackContractPushHpCostUnavailable,
    BlackContractPushGuardCostUnavailable,
    BlackContractPushInvalidVariant,
}

internal static class BattleSkillCastBlockReasonKinds
{
    internal static bool IsBlocked(BattleSkillCastBlockReasonKind kind) =>
        kind != BattleSkillCastBlockReasonKind.None;

    internal static string ToTraceKey(BattleSkillCastBlockReasonKind kind) =>
        kind switch
        {
            BattleSkillCastBlockReasonKind.None => "",
            BattleSkillCastBlockReasonKind.InvalidSkillOrTarget => "invalid_skill_or_target",
            BattleSkillCastBlockReasonKind.SkillCastCheckUnbound => "skill_cast_check_unbound",
            BattleSkillCastBlockReasonKind.InvalidCaster => "invalid_caster",
            BattleSkillCastBlockReasonKind.Cooldown => "cooldown",
            BattleSkillCastBlockReasonKind.MpLocked => "mp_locked",
            BattleSkillCastBlockReasonKind.StaminaLocked => "stamina_locked",
            BattleSkillCastBlockReasonKind.AuraLocked => "aura_locked",
            BattleSkillCastBlockReasonKind.InsufficientAp => "insufficient_ap",
            BattleSkillCastBlockReasonKind.InsufficientMp => "insufficient_mp",
            BattleSkillCastBlockReasonKind.InsufficientStamina => "insufficient_stamina",
            BattleSkillCastBlockReasonKind.Petrified => "petrified",
            BattleSkillCastBlockReasonKind.InsufficientAura => "insufficient_aura",
            BattleSkillCastBlockReasonKind.RacialSkillPerBattleChargeDepleted =>
                "racial_skill_per_battle_charge_depleted",
            BattleSkillCastBlockReasonKind.RacialSkillPerTurnChargeDepleted =>
                "racial_skill_per_turn_charge_depleted",
            BattleSkillCastBlockReasonKind.RacialSkillChargeUninitialized =>
                "racial_skill_charge_uninitialized",
            BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing =>
                "required_weapon_family_missing",
            BattleSkillCastBlockReasonKind.ShieldRequired => "shield_required",
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired => "melee_weapon_required",
            BattleSkillCastBlockReasonKind.ExcludedWeaponFamily => "excluded_weapon_family",
            BattleSkillCastBlockReasonKind.ExcludedWeaponType => "excluded_weapon_type",
            BattleSkillCastBlockReasonKind.MainSkillLockedByStatus =>
                "main_skill_locked_by_status",
            BattleSkillCastBlockReasonKind.MisfortuneSidecarMissing =>
                "misfortune_sidecar_missing",
            BattleSkillCastBlockReasonKind.MisfortuneBlocked => "misfortune_blocked",
            BattleSkillCastBlockReasonKind.BlackStarGuardLock => "black_star_guard_lock",
            BattleSkillCastBlockReasonKind.BlackContractPushVariantMissing =>
                "black_contract_push_variant_missing",
            BattleSkillCastBlockReasonKind.BlackContractPushHpCostUnavailable =>
                "black_contract_push_hp_cost_unavailable",
            BattleSkillCastBlockReasonKind.BlackContractPushGuardCostUnavailable =>
                "black_contract_push_guard_cost_unavailable",
            BattleSkillCastBlockReasonKind.BlackContractPushInvalidVariant =>
                "black_contract_push_invalid_variant",
            _ => Enum.GetName(typeof(BattleSkillCastBlockReasonKind), kind) ?? "unknown",
        };
}
