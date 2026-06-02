using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleSkillMasteryService : RefCounted
{
    private static readonly StringName BattleRatingSourceType = "battle_rating";
    private static readonly StringName BasicAttackSkillId = "basic_attack";
    private static readonly StringName BowTrainingSkillId = "bow_training";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";
    private static readonly StringName StatusVajraBody = "vajra_body";
    private static readonly StringName SwordTrainingSkillId = "sword_training";
    private static readonly StringName UnarmedTrainingSkillId = "unarmed_training";
    private static readonly StringName VajraBodySkillId = "vajra_body";
    private static readonly StringName WarriorGuardSkillId = "warrior_guard";
    private static readonly StringName MasterySourceHeavyHitTaken = "heavy_hit_taken";
    private static readonly StringName MasterySourceMaxDamageDieTaken = "max_damage_die_taken";
    private static readonly StringName MasterySourceEliteOrBossDamageTaken =
        "elite_or_boss_damage_taken";
    private static readonly StringName HpMax = "hp_max";
    private static readonly StringName StaminaMax = "stamina_max";

    private readonly List<SkillMasteryResolutionEvent> _resolutionEvents = new();

    public void Clear()
    {
        _resolutionEvents.Clear();
    }

    public void clear() => Clear();

    public void RecordTargetResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        GDictionary result,
        GArray effectDefs = null
    )
    {
        if (sourceUnit == null || targetUnit == null || result == null)
            return;
        if (sourceUnit.source_member_id == "")
            return;
        var resultSnapshot = SkillMasteryResultSnapshot.FromDictionary(result);
        if (!_IsSkillMasteryQualifyingResult(resultSnapshot, skillDef))
            return;
        int amount = _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDef);
        if (amount <= 0)
            return;
        _resolutionEvents.Add(
            SkillMasteryResolutionEvent.ForTargetResult(
                targetUnit.unit_id,
                amount,
                resultSnapshot.CriticalHit,
                resultSnapshot.HasSkillDamageDieEvent,
                resultSnapshot.HasWeaponDiceMaxEvent
            )
        );
    }

    internal void RecordTargetResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        AttackEffectResolutionResult result,
        GCombatEffectArray effectDefs = null
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return;
        if (sourceUnit.source_member_id == "")
            return;
        if (!_IsSkillMasteryQualifyingResult(result, skillDef))
            return;
        int amount = _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDef);
        if (amount <= 0)
            return;
        _resolutionEvents.Add(
            SkillMasteryResolutionEvent.ForTargetResult(
                targetUnit.unit_id,
                amount,
                result.CriticalHit,
                _ResultHasSkillDamageDieEvent(result),
                _ResultHasWeaponDiceMaxEvent(result)
            )
        );
    }

    public void record_target_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GDictionary result,
        GArray effect_defs = null
    )
    {
        RecordTargetResult(source_unit, target_unit, skill_def, result, effect_defs);
    }

    public void RecordBonus(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        int baseAmount
    )
    {
        if (baseAmount <= 0 || sourceUnit == null || targetUnit == null || skillDef == null)
            return;
        int amount =
            baseAmount * _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDef);
        if (amount <= 0)
            return;
        _resolutionEvents.Add(
            SkillMasteryResolutionEvent.ForSkillAmount(skillDef.skill_id, amount)
        );
    }

    public void record_bonus(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        int base_amount
    )
    {
        RecordBonus(source_unit, target_unit, skill_def, base_amount);
    }

    public void RecordMasteryAmount(SkillDef skillDef, int amount)
    {
        if (skillDef == null || amount <= 0)
            return;
        _resolutionEvents.Add(
            SkillMasteryResolutionEvent.ForSkillAmount(skillDef.skill_id, amount)
        );
    }

    public void record_mastery_amount(SkillDef skill_def, int amount)
    {
        RecordMasteryAmount(skill_def, amount);
    }

    public int ResolveActiveSkillMasteryAmount()
    {
        int total = 0;
        foreach (var resolutionEvent in _resolutionEvents)
        {
            total += Mathf.Max(resolutionEvent.Amount, 0);
        }
        return total;
    }

    public int resolve_active_skill_mastery_amount()
    {
        return ResolveActiveSkillMasteryAmount();
    }

    public StringName ResolveMasteryRewardSkillId(BattleUnitState sourceUnit, StringName skillId)
    {
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (normalizedSkillId != BasicAttackSkillId)
            return normalizedSkillId;
        if (sourceUnit == null)
            return normalizedSkillId;
        var weaponFamily = ProgressionDataUtils.to_string_name(sourceUnit.weapon_family);
        if (weaponFamily == "sword")
            return SwordTrainingSkillId;
        if (weaponFamily == "bow")
            return BowTrainingSkillId;
        if (weaponFamily == "unarmed")
            return UnarmedTrainingSkillId;
        var weaponKind = ProgressionDataUtils.to_string_name(sourceUnit.weapon_profile_kind);
        if (
            weaponKind == BattleUnitState.WEAPON_PROFILE_KIND_UNARMED()
            || weaponKind == BattleUnitState.WEAPON_PROFILE_KIND_NATURAL()
        )
            return UnarmedTrainingSkillId;
        return normalizedSkillId;
    }

    public StringName resolve_mastery_reward_skill_id(
        BattleUnitState source_unit,
        StringName skill_id
    )
    {
        return ResolveMasteryRewardSkillId(source_unit, skill_id);
    }

    public GDictionary BuildVajraBodyMasteryGrant(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        GDictionary result,
        GDictionary skillDefs
    )
    {
        if (sourceUnit == null || targetUnit == null || result == null)
            return new GDictionary();
        BattleSkillMasteryGrant grant = BuildVajraBodyMasteryGrantTyped(
            sourceUnit,
            targetUnit,
            skillDef,
            SkillMasteryResultSnapshot.FromDictionary(result),
            skillDefs
        );
        return grant?.ToDictionary() ?? new GDictionary();
    }

    internal BattleSkillMasteryGrant BuildVajraBodyMasteryGrantTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        AttackEffectResolutionResult result,
        GDictionary skillDefs
    )
    {
        return BuildVajraBodyMasteryGrantTyped(
            sourceUnit,
            targetUnit,
            skillDef,
            SkillMasteryResultSnapshot.FromResult(result),
            skillDefs
        );
    }

    private BattleSkillMasteryGrant BuildVajraBodyMasteryGrantTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        SkillMasteryResultSnapshot resultSnapshot,
        GDictionary skillDefs
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return null;
        if (targetUnit.source_member_id == "" || !targetUnit.is_alive)
            return null;
        if (sourceUnit.faction_id.ToString() == targetUnit.faction_id.ToString())
            return null;
        var statusEntry = targetUnit.get_status_effect(StatusVajraBody);
        if (statusEntry == null)
            return null;
        var masterySourceIds = _CollectVajraBodyMasterySourceIds(
            sourceUnit,
            skillDef,
            resultSnapshot
        );
        var masterySourceId = _ResolveFirstAllowedSkillMasterySource(
            VajraBodySkillId,
            masterySourceIds,
            skillDefs
        );
        if (masterySourceId == "")
            return null;
        int qualifyingHits = resultSnapshot.CountVajraBodyMasteryHits;
        if (qualifyingHits <= 0)
            return null;
        int multiplier = _ResolveVajraBodyMasteryMultiplier(sourceUnit, targetUnit);
        int masteryAmount = qualifyingHits * multiplier;
        if (masteryAmount <= 0)
            return null;
        return new BattleSkillMasteryGrant
        {
            MemberId = targetUnit.source_member_id,
            SkillId = VajraBodySkillId,
            Amount = masteryAmount,
            SourceType = masterySourceId,
            SourceLabel = "战斗受击",
            ReasonText = "金刚不坏：承受重击或高威胁命中",
            AllowUnlocks = true,
            RecordNearDeathUnbrokenManual = _IsVajraBodyLowHpTrainingWindow(targetUnit),
        };
    }

    public GDictionary build_vajra_body_mastery_grant(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GDictionary result,
        GDictionary skill_defs
    )
    {
        return BuildVajraBodyMasteryGrant(source_unit, target_unit, skill_def, result, skill_defs);
    }

    public GDictionary BuildGuardMasteryGrantFromIncomingHit(
        BattleUnitState attackerUnit,
        BattleUnitState targetUnit,
        GArray effectDefs,
        GDictionary result,
        GDictionary skillDefs
    )
    {
        if (
            attackerUnit == null
            || targetUnit == null
            || effectDefs == null
            || effectDefs.Count == 0
            || result == null
        )
            return new GDictionary();
        if (targetUnit.source_member_id == "")
            return new GDictionary();
        if (!targetUnit.status_effects.ContainsKey("guarding"))
            return new GDictionary();
        if (!_EffectDefsHavePhysicalDamage(effectDefs))
            return new GDictionary();
        var resultSnapshot = SkillMasteryResultSnapshot.FromDictionary(result);
        if (!resultSnapshot.AttackSuccess)
            return new GDictionary();
        if (resultSnapshot.Damage <= 0)
            return new GDictionary();
        var guardDef = skillDefs.GetValueOrDefault(WarriorGuardSkillId, default).As<SkillDef>();
        if (guardDef == null)
            return new GDictionary();
        if (_GetSkillMasteryTriggerMode(guardDef) != "incoming_physical_hit")
            return new GDictionary();
        int amount = _ResolveIncomingSkillMasterySourceAmount(attackerUnit, targetUnit, guardDef);
        if (amount <= 0)
            return new GDictionary();
        return new GDictionary
        {
            ["member_id"] = targetUnit.source_member_id,
            ["skill_id"] = WarriorGuardSkillId,
            ["amount"] = amount,
            ["source_type"] = "battle",
            ["source_label"] = "战斗",
            ["reason_text"] = "",
            ["allow_unlocks"] = true,
        };
    }

    internal BattleSkillMasteryGrant BuildGuardMasteryGrantFromIncomingHitTyped(
        BattleUnitState attackerUnit,
        BattleUnitState targetUnit,
        GCombatEffectArray effectDefs,
        AttackEffectResolutionResult result,
        GDictionary skillDefs
    )
    {
        if (
            attackerUnit == null
            || targetUnit == null
            || effectDefs == null
            || effectDefs.Count == 0
        )
            return null;
        if (targetUnit.source_member_id == "")
            return null;
        if (!targetUnit.status_effects.ContainsKey("guarding"))
            return null;
        if (!_EffectDefsHavePhysicalDamage(effectDefs))
            return null;
        if (!result.AttackSuccess)
            return null;
        if (result.Damage <= 0)
            return null;
        var guardDef = skillDefs.GetValueOrDefault(WarriorGuardSkillId, default).As<SkillDef>();
        if (guardDef == null)
            return null;
        if (_GetSkillMasteryTriggerMode(guardDef) != "incoming_physical_hit")
            return null;
        int amount = _ResolveIncomingSkillMasterySourceAmount(attackerUnit, targetUnit, guardDef);
        if (amount <= 0)
            return null;
        return new BattleSkillMasteryGrant
        {
            MemberId = targetUnit.source_member_id,
            SkillId = WarriorGuardSkillId,
            Amount = amount,
            SourceType = "battle",
            SourceLabel = "战斗",
            ReasonText = "",
            AllowUnlocks = true,
        };
    }

    public GDictionary build_guard_mastery_grant_from_incoming_hit(
        BattleUnitState attacker_unit,
        BattleUnitState target_unit,
        GArray effect_defs,
        GDictionary result,
        GDictionary skill_defs
    )
    {
        return BuildGuardMasteryGrantFromIncomingHit(
            attacker_unit,
            target_unit,
            effect_defs,
            result,
            skill_defs
        );
    }

    public GArray BuildBattleRatingMasteryRewardEntries(
        BattleRatingMemberStats stats,
        int score,
        string ratingLabel
    )
    {
        int masteryAmount = ResolveBattleRatingMasteryAmount(score);
        if (masteryAmount <= 0 || stats == null)
            return new GArray();
        var rewardEntries = new GArray();
        foreach (KeyValuePair<StringName, int> castCount in stats.cast_counts)
        {
            StringName skillId = castCount.Key;
            if (skillId == "" || castCount.Value <= 0)
                continue;
            rewardEntries.Add(
                new GDictionary
                {
                    ["entry_type"] = "skill_mastery",
                    ["target_id"] = skillId.ToString(),
                    ["target_label"] = "",
                    ["amount"] = masteryAmount,
                    ["reason_text"] = $"战斗评分 {score} · {ratingLabel}",
                }
            );
        }
        return rewardEntries;
    }

    public GArray build_battle_rating_mastery_reward_entries(
        GDictionary stats,
        int score,
        string rating_label
    )
    {
        return BuildBattleRatingMasteryRewardEntries(
            BattleRatingMemberStats.FromDictionary(stats),
            score,
            rating_label
        );
    }

    public int ResolveBattleRatingMasteryAmount(int score)
    {
        if (score >= 6)
            return 6;
        if (score >= 4)
            return 4;
        if (score >= 2)
            return 2;
        return 0;
    }

    public int resolve_battle_rating_mastery_amount(int score)
    {
        return ResolveBattleRatingMasteryAmount(score);
    }

    private bool _IsSkillMasteryQualifyingResult(
        SkillMasteryResultSnapshot result,
        SkillDef skillDef
    )
    {
        var triggerMode = _GetSkillMasteryTriggerMode(skillDef);
        switch ((string)triggerMode)
        {
            case "weapon_attack_quality":
                return result.AttackSuccess
                    && (result.CriticalHit || result.HasWeaponDiceMaxEvent);
            case "damage_dealt":
                return result.HasEffectiveDamageOrAbsorb;
            case "status_applied":
                return result.HasStatusApplied;
            case "effect_applied":
                return result.Applied;
            case "incoming_physical_hit":
                return false;
            case "secondary_hit":
                return result.SecondaryHitSuccess;
            case "skill_damage_dice_max":
                if (!result.HasEffectiveDamageOrAbsorb)
                    return false;
                return result.HasSkillDamageDieEvent;
            default:
                if (!result.HasEffectiveDamageOrAbsorb)
                    return false;
                return result.HasSkillDamageDieEvent;
        }
    }

    private bool _IsSkillMasteryQualifyingResult(
        AttackEffectResolutionResult result,
        SkillDef skillDef
    )
    {
        var triggerMode = _GetSkillMasteryTriggerMode(skillDef);
        switch ((string)triggerMode)
        {
            case "weapon_attack_quality":
                return result.AttackSuccess
                    && (result.CriticalHit || _ResultHasWeaponDiceMaxEvent(result));
            case "damage_dealt":
                return _ResultHasEffectiveDamageOrAbsorb(result);
            case "status_applied":
                return _ResultHasStatusApplied(result);
            case "effect_applied":
                return result.Applied;
            case "incoming_physical_hit":
                return false;
            case "secondary_hit":
                return result.SecondaryHitSuccess;
            case "skill_damage_dice_max":
                if (!_ResultHasEffectiveDamageOrAbsorb(result))
                    return false;
                return _ResultHasSkillDamageDieEvent(result);
            default:
                if (!_ResultHasEffectiveDamageOrAbsorb(result))
                    return false;
                return _ResultHasSkillDamageDieEvent(result);
        }
    }

    private StringName _GetSkillMasteryTriggerMode(SkillDef skillDef)
    {
        if (skillDef == null || skillDef.combat_profile == null)
            return "skill_damage_dice_max";
        var combatProfile = skillDef.combat_profile as CombatSkillDef;
        if (combatProfile == null)
            return "skill_damage_dice_max";
        var triggerMode = ProgressionDataUtils.to_string_name(combatProfile.mastery_trigger_mode);
        if (triggerMode == "")
            return "skill_damage_dice_max";
        return triggerMode;
    }

    private StringName _GetSkillMasteryAmountMode(SkillDef skillDef)
    {
        if (skillDef == null || skillDef.combat_profile == null)
            return "per_target_rank";
        var combatProfile = skillDef.combat_profile as CombatSkillDef;
        if (combatProfile == null)
            return "per_target_rank";
        var amountMode = ProgressionDataUtils.to_string_name(combatProfile.mastery_amount_mode);
        if (amountMode == "")
            return "per_target_rank";
        return amountMode;
    }

    private bool _ResultHasEffectiveDamageOrAbsorb(AttackEffectResolutionResult result)
    {
        return result.Damage > 0 || result.ShieldAbsorbed > 0;
    }

    private bool _ResultHasStatusApplied(AttackEffectResolutionResult result)
    {
        return result.StatusEffectIds != null && result.StatusEffectIds.Count > 0;
    }

    private bool _ResultHasSkillDamageDieEvent(AttackEffectResolutionResult result)
    {
        if (result.SkillDamageDiceIsMax)
            return true;
        foreach (DamageEventResult damageEvent in result.DamageEvents ?? System.Array.Empty<DamageEventResult>())
        {
            if (damageEvent.SkillDamageDiceIsMax)
                return true;
        }
        return false;
    }

    private bool _ResultHasWeaponDiceMaxEvent(AttackEffectResolutionResult result)
    {
        foreach (DamageEventResult damageEvent in result.DamageEvents ?? System.Array.Empty<DamageEventResult>())
        {
            if (
                damageEvent.WeaponDamageDiceIsMax
                && damageEvent.WeaponDamageDiceIsMaxReason
                    == DamageDiceMaxReasonKind.WeaponDiceMax
            )
                return true;
        }
        return false;
    }

    private bool _EffectDefsHavePhysicalDamage(GArray effectDefs)
    {
        foreach (var effectValue in effectDefs)
        {
            CombatEffectDef effectDef = effectValue.As<CombatEffectDef>();
            if (effectDef == null || effectDef.effect_type != "damage")
                continue;
            var tag = ProgressionDataUtils.to_string_name(effectDef.damage_tag);
            if (tag == "physical_slash" || tag == "physical_pierce" || tag == "physical_blunt")
                return true;
        }
        return false;
    }

    private bool _EffectDefsHavePhysicalDamage(GCombatEffectArray effectDefs)
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? new GCombatEffectArray())
        {
            if (effectDef == null || effectDef.effect_type != "damage")
                continue;
            var tag = ProgressionDataUtils.to_string_name(effectDef.damage_tag);
            if (tag == "physical_slash" || tag == "physical_pierce" || tag == "physical_blunt")
                return true;
        }
        return false;
    }

    private GArray _CollectVajraBodyMasterySourceIds(
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        SkillMasteryResultSnapshot result
    )
    {
        var sourceIds = new GArray();
        if (!result.HasVajraBodyMasteryEvent)
            return sourceIds;
        if (_IsVajraBodyHeavyHitSkill(skillDef))
            sourceIds.Add(MasterySourceHeavyHitTaken);
        sourceIds.Add(MasterySourceMaxDamageDieTaken);
        if (_IsEliteOrBossTarget(sourceUnit))
            sourceIds.Add(MasterySourceEliteOrBossDamageTaken);
        return sourceIds;
    }

    private StringName _ResolveFirstAllowedSkillMasterySource(
        StringName skillId,
        GArray sourceIds,
        GDictionary skillDefs
    )
    {
        if (skillId == "" || sourceIds == null || sourceIds.Count == 0)
            return "";
        var skillDef = skillDefs.GetValueOrDefault(skillId, default).As<SkillDef>();
        if (skillDef == null)
            return "";
        foreach (var sourceIdValue in sourceIds)
        {
            var sourceId = ProgressionDataUtils.to_string_name(sourceIdValue);
            if (sourceId == "")
                continue;
            if (
                skillDef.mastery_sources == null
                || skillDef.mastery_sources.Count == 0
                || skillDef.mastery_sources.Contains(sourceId)
            )
                return sourceId;
        }
        return "";
    }

    private bool _IsVajraBodyHeavyHitSkill(SkillDef skillDef)
    {
        if (skillDef == null)
            return false;
        if (skillDef.skill_id.ToString().Contains("heavy"))
            return true;
        if (skillDef.display_name.Contains("重击"))
            return true;
        return skillDef.tags.Contains("heavy");
    }

    private int _ResolveVajraBodyMasteryMultiplier(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        int multiplier = 1;
        if (_IsBossTarget(sourceUnit))
            multiplier = 3;
        else if (_IsEliteOrBossTarget(sourceUnit))
            multiplier = 2;
        if (_IsVajraBodyLowHpTrainingWindow(targetUnit))
            multiplier *= 2;
        return multiplier;
    }

    private bool _IsVajraBodyLowHpTrainingWindow(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return false;
        int hpMax = Mathf.Max(unitState.attribute_snapshot.get_value(HpMax), 1);
        return unitState.current_hp > 0 && unitState.current_hp * 3 < hpMax;
    }

    private int _ResolveSkillMasteryTargetAmount(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return 0;
        var amountMode = _GetSkillMasteryAmountMode(skillDef);
        switch ((string)amountMode)
        {
            case "per_cast_hp_ratio":
            {
                if (sourceUnit.attribute_snapshot == null)
                    return 0;
                int hpMax = Mathf.Max(
                    sourceUnit.attribute_snapshot.get_value(HpMax),
                    1
                );
                int currentHp = sourceUnit.current_hp;
                if (currentHp * 4 < hpMax)
                    return 4;
                if (currentHp * 2 < hpMax)
                    return 2;
                return 1;
            }
            default:
            {
                if (amountMode != "per_target_rank")
                    return 0;
                if (sourceUnit.faction_id == targetUnit.faction_id)
                {
                    if (!_IsSameFactionSupportMasteryTarget(skillDef))
                        return 0;
                    int baseAmount = 1;
                    var combatProfile = skillDef?.combat_profile as CombatSkillDef;
                    if (combatProfile != null && targetUnit.attribute_snapshot != null)
                    {
                        int multiplier = Mathf.Max(
                            combatProfile.mastery_low_hp_bonus_multiplier,
                            1
                        );
                        int thresholdPercent = Mathf.Clamp(
                            combatProfile.mastery_low_hp_threshold_percent,
                            1,
                            100
                        );
                        if (multiplier > 1)
                        {
                            int hpMax = Mathf.Max(
                                targetUnit.attribute_snapshot.get_value(HpMax),
                                1
                            );
                            if (targetUnit.current_hp * 100 < hpMax * thresholdPercent)
                                baseAmount = multiplier;
                        }
                    }
                    return baseAmount;
                }
                if (!_AreOpposingFactions(sourceUnit, targetUnit))
                    return 0;
                if (_IsBossTarget(targetUnit))
                    return 3;
                if (_IsEliteOrBossTarget(targetUnit))
                    return 2;
                return 1;
            }
        }
    }

    private bool _IsSameFactionSupportMasteryTarget(SkillDef skillDef)
    {
        var triggerMode = _GetSkillMasteryTriggerMode(skillDef);
        if (triggerMode != "status_applied" && triggerMode != "effect_applied")
            return false;
        if (skillDef == null || skillDef.combat_profile == null)
            return false;
        var combatProfile = skillDef.combat_profile as CombatSkillDef;
        if (combatProfile == null)
            return false;
        var targetFilter = ProgressionDataUtils.to_string_name(combatProfile.target_team_filter);
        return targetFilter == "ally" || targetFilter == "self";
    }

    private int _ResolveIncomingSkillMasterySourceAmount(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return 0;
        if (_GetSkillMasteryAmountMode(skillDef) != "per_target_rank")
            return 0;
        if (!_AreOpposingFactions(sourceUnit, targetUnit))
            return 0;
        if (_IsBossTarget(sourceUnit))
            return 3;
        if (_IsEliteOrBossTarget(sourceUnit))
            return 2;
        return 1;
    }

    private bool _AreOpposingFactions(BattleUnitState sourceUnit, BattleUnitState targetUnit)
    {
        if (sourceUnit == null || targetUnit == null)
            return false;
        var sourceFaction = ProgressionDataUtils.to_string_name(sourceUnit.faction_id);
        var targetFaction = ProgressionDataUtils.to_string_name(targetUnit.faction_id);
        return sourceFaction != "" && targetFaction != "" && sourceFaction != targetFaction;
    }

    private bool _IsEliteOrBossTarget(BattleUnitState unitState)
    {
        return unitState != null
            && unitState.attribute_snapshot != null
            && unitState.attribute_snapshot.get_value(FortuneMarkTargetStatId)
                > 0;
    }

    private bool _IsBossTarget(BattleUnitState unitState)
    {
        return unitState != null
            && unitState.attribute_snapshot != null
            && (
                unitState.attribute_snapshot.get_value(BossTargetStatId) > 0
                || unitState.attribute_snapshot.get_value(FortuneMarkTargetStatId)
                    > 1
            );
    }

    private readonly struct SkillMasteryResultSnapshot
    {
        private readonly SkillMasteryDamageEventSnapshot[] _damageEvents;

        public SkillMasteryResultSnapshot(
            bool attackSuccess,
            bool criticalHit,
            bool applied,
            bool secondaryHitSuccess,
            int damage,
            int shieldAbsorbed,
            int statusEffectCount,
            bool skillDamageDiceIsMax,
            SkillMasteryDamageEventSnapshot[] damageEvents
        )
        {
            AttackSuccess = attackSuccess;
            CriticalHit = criticalHit;
            Applied = applied;
            SecondaryHitSuccess = secondaryHitSuccess;
            Damage = damage;
            ShieldAbsorbed = shieldAbsorbed;
            StatusEffectCount = statusEffectCount;
            SkillDamageDiceIsMax = skillDamageDiceIsMax;
            _damageEvents = damageEvents ?? System.Array.Empty<SkillMasteryDamageEventSnapshot>();
        }

        public bool AttackSuccess { get; }
        public bool CriticalHit { get; }
        public bool Applied { get; }
        public bool SecondaryHitSuccess { get; }
        public int Damage { get; }
        public int ShieldAbsorbed { get; }
        public int StatusEffectCount { get; }
        public bool SkillDamageDiceIsMax { get; }

        public bool HasEffectiveDamageOrAbsorb => Damage > 0 || ShieldAbsorbed > 0;
        public bool HasStatusApplied => StatusEffectCount > 0;

        public bool HasSkillDamageDieEvent
        {
            get
            {
                if (SkillDamageDiceIsMax)
                    return true;
                foreach (var damageEvent in _damageEvents ?? System.Array.Empty<SkillMasteryDamageEventSnapshot>())
                {
                    if (damageEvent.SkillDamageDiceIsMax)
                        return true;
                }
                return false;
            }
        }

        public bool HasWeaponDiceMaxEvent
        {
            get
            {
                foreach (var damageEvent in _damageEvents ?? System.Array.Empty<SkillMasteryDamageEventSnapshot>())
                {
                    if (damageEvent.IsWeaponDiceMaxEvent)
                        return true;
                }
                return false;
            }
        }

        public bool HasVajraBodyMasteryEvent => CountVajraBodyMasteryHits > 0;

        public int CountVajraBodyMasteryHits
        {
            get
            {
                int count = 0;
                foreach (var damageEvent in _damageEvents ?? System.Array.Empty<SkillMasteryDamageEventSnapshot>())
                {
                    if (damageEvent.IsVajraBodyMasteryEvent)
                        count++;
                }
                return count;
            }
        }

        public static SkillMasteryResultSnapshot FromDictionary(GDictionary source)
        {
            if (source == null || source.Count == 0)
                return new SkillMasteryResultSnapshot();
            return new SkillMasteryResultSnapshot(
                BooleanField(source, "attack_success"),
                BooleanField(source, "critical_hit"),
                BooleanField(source, "applied"),
                BooleanField(source, "secondary_hit_success"),
                IntegerField(source, "damage"),
                IntegerField(source, "shield_absorbed"),
                ArrayField(source, "status_effect_ids").Count,
                BooleanField(source, "skill_damage_dice_is_max"),
                ReadDamageEvents(source)
            );
        }

        public static SkillMasteryResultSnapshot FromResult(
            AttackEffectResolutionResult result
        )
        {
            return new SkillMasteryResultSnapshot(
                result.AttackSuccess,
                result.CriticalHit,
                result.Applied,
                result.SecondaryHitSuccess,
                result.Damage,
                result.ShieldAbsorbed,
                result.StatusEffectIds?.Count ?? 0,
                result.SkillDamageDiceIsMax,
                ReadDamageEvents(result)
            );
        }

        private static SkillMasteryDamageEventSnapshot[] ReadDamageEvents(GDictionary source)
        {
            var damageEvents = ArrayField(source, "damage_events");
            if (damageEvents.Count == 0)
                return System.Array.Empty<SkillMasteryDamageEventSnapshot>();
            var results = new System.Collections.Generic.List<SkillMasteryDamageEventSnapshot>();
            foreach (var eventValue in damageEvents)
            {
                GDictionary evt = eventValue.AsGodotDictionary();
                results.Add(
                    new SkillMasteryDamageEventSnapshot(
                        BooleanField(evt, "damage_dice_high_total_roll"),
                        BooleanField(evt, "skill_damage_dice_is_max"),
                        BooleanField(evt, "weapon_damage_dice_is_max"),
                        AttackEffectResolutionResultReader.ParseDamageDiceMaxReason(
                            ProgressionDataUtils.to_string_name(
                                evt.GetValueOrDefault("weapon_damage_dice_is_max_reason", "")
                            )
                        ),
                        IntegerField(evt, "hp_damage")
                    )
                );
            }
            return results.ToArray();
        }

        private static SkillMasteryDamageEventSnapshot[] ReadDamageEvents(
            AttackEffectResolutionResult result
        )
        {
            if (result.DamageEvents == null || result.DamageEvents.Length == 0)
                return System.Array.Empty<SkillMasteryDamageEventSnapshot>();
            var results = new System.Collections.Generic.List<SkillMasteryDamageEventSnapshot>();
            foreach (DamageEventResult damageEvent in result.DamageEvents)
            {
                results.Add(
                    new SkillMasteryDamageEventSnapshot(
                        damageEvent.DamageDiceHighTotalRoll,
                        damageEvent.SkillDamageDiceIsMax,
                        damageEvent.WeaponDamageDiceIsMax,
                        damageEvent.WeaponDamageDiceIsMaxReason,
                        damageEvent.HpDamage
                    )
                );
            }
            return results.ToArray();
        }

        private static bool BooleanField(GDictionary dictionary, string key)
        {
            if (dictionary == null || !dictionary.ContainsKey(key))
                return false;
            return dictionary[key].AsBool();
        }

        private static int IntegerField(GDictionary dictionary, string key)
        {
            if (dictionary == null || !dictionary.ContainsKey(key))
                return 0;
            return dictionary[key].AsInt32();
        }

        private static GArray ArrayField(GDictionary dictionary, string key)
        {
            if (dictionary == null || !dictionary.ContainsKey(key))
                return new GArray();
            return dictionary[key].AsGodotArray();
        }
    }

    private readonly struct SkillMasteryResolutionEvent
    {
        private SkillMasteryResolutionEvent(
            StringName targetUnitId,
            StringName skillId,
            int amount,
            bool criticalHit,
            bool skillDamageDiceIsMax,
            bool weaponDamageDiceIsMax
        )
        {
            TargetUnitId = targetUnitId ?? "";
            SkillId = skillId ?? "";
            Amount = amount;
            CriticalHit = criticalHit;
            SkillDamageDiceIsMax = skillDamageDiceIsMax;
            WeaponDamageDiceIsMax = weaponDamageDiceIsMax;
        }

        public StringName TargetUnitId { get; }
        public StringName SkillId { get; }
        public int Amount { get; }
        public bool CriticalHit { get; }
        public bool SkillDamageDiceIsMax { get; }
        public bool WeaponDamageDiceIsMax { get; }

        public static SkillMasteryResolutionEvent ForTargetResult(
            StringName targetUnitId,
            int amount,
            bool criticalHit,
            bool skillDamageDiceIsMax,
            bool weaponDamageDiceIsMax
        )
        {
            return new SkillMasteryResolutionEvent(
                targetUnitId,
                "",
                amount,
                criticalHit,
                skillDamageDiceIsMax,
                weaponDamageDiceIsMax
            );
        }

        public static SkillMasteryResolutionEvent ForSkillAmount(StringName skillId, int amount)
        {
            return new SkillMasteryResolutionEvent("", skillId, amount, false, false, false);
        }
    }

    private readonly struct SkillMasteryDamageEventSnapshot
    {
        public SkillMasteryDamageEventSnapshot(
            bool damageDiceHighTotalRoll,
            bool skillDamageDiceIsMax,
            bool weaponDamageDiceIsMax,
            DamageDiceMaxReasonKind weaponDamageDiceIsMaxReason,
            int hpDamage
        )
        {
            DamageDiceHighTotalRoll = damageDiceHighTotalRoll;
            SkillDamageDiceIsMax = skillDamageDiceIsMax;
            WeaponDamageDiceIsMax = weaponDamageDiceIsMax;
            WeaponDamageDiceIsMaxReason = weaponDamageDiceIsMaxReason;
            HpDamage = hpDamage;
        }

        public bool DamageDiceHighTotalRoll { get; }
        public bool SkillDamageDiceIsMax { get; }
        public bool WeaponDamageDiceIsMax { get; }
        public DamageDiceMaxReasonKind WeaponDamageDiceIsMaxReason { get; }
        public int HpDamage { get; }

        public bool IsWeaponDiceMaxEvent =>
            WeaponDamageDiceIsMax
            && WeaponDamageDiceIsMaxReason == DamageDiceMaxReasonKind.WeaponDiceMax;

        public bool IsVajraBodyMasteryEvent => DamageDiceHighTotalRoll && HpDamage > 0;
    }
}
