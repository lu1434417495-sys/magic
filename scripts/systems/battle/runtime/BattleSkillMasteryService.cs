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

    private readonly GArray _resolutionEvents = new();

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
        if (!_IsSkillMasteryQualifyingResult(result, skillDef))
            return;
        int amount = _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDef);
        if (amount <= 0)
            return;
        _resolutionEvents.Add(
            new GDictionary
            {
                ["target_unit_id"] = targetUnit.unit_id,
                ["amount"] = amount,
                ["critical_hit"] = result.GetValueOrDefault("critical_hit", false).AsBool(),
                ["skill_damage_dice_is_max"] = _ResultHasSkillDamageDieEvent(result),
                ["weapon_damage_dice_is_max"] = _ResultHasWeaponDiceMaxEvent(result),
            }
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
            new GDictionary
            {
                ["target_unit_id"] = targetUnit.unit_id,
                ["amount"] = amount,
                ["critical_hit"] = result.CriticalHit,
                ["skill_damage_dice_is_max"] = _ResultHasSkillDamageDieEvent(result),
                ["weapon_damage_dice_is_max"] = _ResultHasWeaponDiceMaxEvent(result),
            }
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
            new GDictionary { ["skill_id"] = skillDef.skill_id, ["amount"] = amount }
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
            new GDictionary { ["skill_id"] = skillDef.skill_id, ["amount"] = amount }
        );
    }

    public void record_mastery_amount(SkillDef skill_def, int amount)
    {
        RecordMasteryAmount(skill_def, amount);
    }

    public int ResolveActiveSkillMasteryAmount()
    {
        int total = 0;
        foreach (var eventValue in _resolutionEvents)
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            total += Mathf.Max(
                eventValue.AsGodotDictionary().GetValueOrDefault("amount", 0).AsInt32(),
                0
            );
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
        if (targetUnit.source_member_id == "" || !targetUnit.is_alive)
            return new GDictionary();
        if (sourceUnit.faction_id.ToString() == targetUnit.faction_id.ToString())
            return new GDictionary();
        var statusEntry = targetUnit.get_status_effect(StatusVajraBody);
        if (statusEntry == null)
            return new GDictionary();
        var masterySourceIds = _CollectVajraBodyMasterySourceIds(sourceUnit, skillDef, result);
        var masterySourceId = _ResolveFirstAllowedSkillMasterySource(
            VajraBodySkillId,
            masterySourceIds,
            skillDefs
        );
        if (masterySourceId == "")
            return new GDictionary();
        int qualifyingHits = _CountVajraBodyMasteryHits(result);
        if (qualifyingHits <= 0)
            return new GDictionary();
        int multiplier = _ResolveVajraBodyMasteryMultiplier(sourceUnit, targetUnit);
        int masteryAmount = qualifyingHits * multiplier;
        if (masteryAmount <= 0)
            return new GDictionary();
        return new GDictionary
        {
            ["member_id"] = targetUnit.source_member_id,
            ["skill_id"] = VajraBodySkillId,
            ["amount"] = masteryAmount,
            ["source_type"] = masterySourceId,
            ["source_label"] = "战斗受击",
            ["reason_text"] = "金刚不坏：承受重击或高威胁命中",
            ["allow_unlocks"] = true,
            ["record_near_death_unbroken_manual"] = _IsVajraBodyLowHpTrainingWindow(targetUnit),
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
        if (!result.GetValueOrDefault("attack_success", false).AsBool())
            return new GDictionary();
        if (result.GetValueOrDefault("damage", 0).AsInt32() <= 0)
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
        GDictionary stats,
        int score,
        string ratingLabel
    )
    {
        int masteryAmount = ResolveBattleRatingMasteryAmount(score);
        if (masteryAmount <= 0)
            return new GArray();
        var rewardEntries = new GArray();
        var castCounts = stats
            .GetValueOrDefault("cast_counts", new GDictionary())
            .AsGodotDictionary();
        foreach (var skillKey in castCounts.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            if (skillId == "" || castCounts[skillKey].AsInt32() <= 0)
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
        return BuildBattleRatingMasteryRewardEntries(stats, score, rating_label);
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

    private bool _IsSkillMasteryQualifyingResult(GDictionary result, SkillDef skillDef)
    {
        if (result == null || result.Count == 0)
            return false;
        var triggerMode = _GetSkillMasteryTriggerMode(skillDef);
        switch ((string)triggerMode)
        {
            case "weapon_attack_quality":
                return result.GetValueOrDefault("attack_success", false).AsBool()
                    && (
                        result.GetValueOrDefault("critical_hit", false).AsBool()
                        || _ResultHasWeaponDiceMaxEvent(result)
                    );
            case "damage_dealt":
                return _ResultHasEffectiveDamageOrAbsorb(result);
            case "status_applied":
                return _ResultHasStatusApplied(result);
            case "effect_applied":
                return result.GetValueOrDefault("applied", false).AsBool();
            case "incoming_physical_hit":
                return false;
            case "secondary_hit":
                return result.GetValueOrDefault("secondary_hit_success", false).AsBool();
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

    private bool _ResultHasEffectiveDamageOrAbsorb(GDictionary result)
    {
        return result.GetValueOrDefault("damage", 0).AsInt32() > 0
            || result.GetValueOrDefault("shield_absorbed", 0).AsInt32() > 0;
    }

    private bool _ResultHasEffectiveDamageOrAbsorb(AttackEffectResolutionResult result)
    {
        return result.Damage > 0 || result.ShieldAbsorbed > 0;
    }

    private bool _ResultHasStatusApplied(GDictionary result)
    {
        var statusEffectIds = result.GetValueOrDefault("status_effect_ids", new GArray());
        return statusEffectIds.VariantType == Variant.Type.Array
            && statusEffectIds.AsGodotArray().Count > 0;
    }

    private bool _ResultHasStatusApplied(AttackEffectResolutionResult result)
    {
        return result.StatusEffectIds != null && result.StatusEffectIds.Count > 0;
    }

    private bool _ResultHasSkillDamageDieEvent(GDictionary result)
    {
        if (result.GetValueOrDefault("skill_damage_dice_is_max", false).AsBool())
            return true;
        var damageEvents = result.GetValueOrDefault("damage_events", new GArray());
        if (damageEvents.VariantType != Variant.Type.Array)
            return false;
        foreach (var eventValue in damageEvents.AsGodotArray())
        {
            if (
                eventValue.VariantType == Variant.Type.Dictionary
                && eventValue
                    .AsGodotDictionary()
                    .GetValueOrDefault("skill_damage_dice_is_max", false)
                    .AsBool()
            )
                return true;
        }
        return false;
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

    private bool _ResultHasWeaponDiceMaxEvent(GDictionary result)
    {
        var damageEvents = result.GetValueOrDefault("damage_events", new GArray());
        if (damageEvents.VariantType != Variant.Type.Array)
            return false;
        foreach (var eventValue in damageEvents.AsGodotArray())
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            var evt = eventValue.AsGodotDictionary();
            if (
                evt.GetValueOrDefault("weapon_damage_dice_is_max", false).AsBool()
                && ProgressionDataUtils.to_string_name(
                    evt.GetValueOrDefault("weapon_damage_dice_is_max_reason", "")
                ) == "weapon_dice_max"
            )
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
            if (effectValue.AsGodotObject() == null)
                continue;
            var effectDef = effectValue.AsGodotObject() as CombatEffectDef;
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
        GDictionary result
    )
    {
        var sourceIds = new GArray();
        if (!_ResultHasVajraBodyMasteryEvent(result))
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

    private int _CountVajraBodyMasteryHits(GDictionary result)
    {
        var damageEvents = result.GetValueOrDefault("damage_events", new GArray());
        if (damageEvents.VariantType != Variant.Type.Array)
            return 0;
        int count = 0;
        foreach (var eventValue in damageEvents.AsGodotArray())
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;
            var evt = eventValue.AsGodotDictionary();
            if (_IsVajraBodyMasteryEvent(evt))
                count++;
        }
        return count;
    }

    private bool _ResultHasVajraBodyMasteryEvent(GDictionary result)
    {
        if (result == null)
            return false;
        var damageEvents = result.GetValueOrDefault("damage_events", new GArray());
        if (damageEvents.VariantType != Variant.Type.Array)
            return false;
        foreach (var eventValue in damageEvents.AsGodotArray())
        {
            if (
                eventValue.VariantType == Variant.Type.Dictionary
                && _IsVajraBodyMasteryEvent(eventValue.AsGodotDictionary())
            )
                return true;
        }
        return false;
    }

    private bool _IsVajraBodyMasteryEvent(GDictionary evt)
    {
        return evt.GetValueOrDefault("damage_dice_high_total_roll", false).AsBool()
            && evt.GetValueOrDefault("hp_damage", 0).AsInt32() > 0;
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

}
