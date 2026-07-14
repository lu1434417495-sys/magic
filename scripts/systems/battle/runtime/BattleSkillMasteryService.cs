using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleSkillMasteryService : IDisposable
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

    internal void Clear()
    {
        _resolutionEvents.Clear();
    }

    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    public void RecordTargetResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        GDictionary result,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions = null
    )
    {
        if (sourceUnit == null || targetUnit == null || result == null)
            return;
        if (sourceUnit.source_member_id == "")
            return;
        var resultSnapshot = SkillMasteryResultSnapshot.FromDictionary(result);
        if (!_IsSkillMasteryQualifyingResult(resultSnapshot, skillDefinition))
            return;
        int amount = _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDefinition);
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
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions = null,
        bool additionalEffectApplied = false
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return;
        if (sourceUnit.source_member_id == "")
            return;
        if (
            !_IsSkillMasteryQualifyingResult(
                result,
                skillDefinition,
                additionalEffectApplied
            )
        )
            return;
        int amount = _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDefinition);
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

    public void RecordBonus(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        int baseAmount
    )
    {
        if (baseAmount <= 0 || sourceUnit == null || targetUnit == null || skillDefinition == null)
            return;
        int amount =
            baseAmount * _ResolveSkillMasteryTargetAmount(sourceUnit, targetUnit, skillDefinition);
        if (amount <= 0)
            return;
        _resolutionEvents.Add(
            SkillMasteryResolutionEvent.ForSkillAmount(skillDefinition.SkillId, amount)
        );
    }

    public void RecordMasteryAmount(StringName skillId, int amount)
    {
        if (skillId == "" || amount <= 0)
            return;
        _resolutionEvents.Add(
            SkillMasteryResolutionEvent.ForSkillAmount(skillId, amount)
        );
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

    internal IReadOnlyList<BattleSkillMasteryGrant> BuildSourceBoundWeaponBonusMasteryGrants(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackEffectResolutionResult result,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (sourceUnit == null || targetUnit == null || result.DamageEvents == null)
            return Array.Empty<BattleSkillMasteryGrant>();
        if (sourceUnit.source_member_id == "")
            return Array.Empty<BattleSkillMasteryGrant>();

        var amountsBySkillId = new Dictionary<StringName, int>();
        foreach (DamageEventResult damageEvent in result.DamageEvents)
        {
            if (damageEvent.HpDamage <= 0)
                continue;
            foreach (StringName rawSkillId in damageEvent.SourceBoundWeaponBonusSkillIds ?? Array.Empty<StringName>())
            {
                StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
                if (
                    skillId == ""
                    || !UnitHasLearnedActiveSkill(sourceUnit, skillId)
                    || !TryGetSkillDefinition(skillDefinitions, skillId, out SkillDefinition skillDefinition)
                    || _GetSkillMasteryTriggerMode(skillDefinition)
                        != CombatSkillMasteryTriggerMode.SourceBoundWeaponBonusDamage
                )
                {
                    continue;
                }

                int amount = _ResolveSkillMasteryTargetAmount(
                    sourceUnit,
                    targetUnit,
                    skillDefinition
                );
                if (amount <= 0)
                    continue;
                amountsBySkillId.TryGetValue(skillId, out int existingAmount);
                amountsBySkillId[skillId] = existingAmount + amount;
            }
        }

        if (amountsBySkillId.Count == 0)
            return Array.Empty<BattleSkillMasteryGrant>();

        var grants = new List<BattleSkillMasteryGrant>();
        foreach (KeyValuePair<StringName, int> entry in amountsBySkillId)
        {
            grants.Add(
                new BattleSkillMasteryGrant
                {
                    MemberId = sourceUnit.source_member_id,
                    SkillId = entry.Key,
                    Amount = entry.Value,
                    SourceType = "battle",
                    SourceLabel = "战斗",
                    ReasonText = "来源绑定武器追加伤害",
                }
            );
        }
        return grants;
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
            weaponKind == BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed)
            || weaponKind == BattleUnitState.ToStringName(BattleWeaponProfileKind.Natural)
        )
            return UnarmedTrainingSkillId;
        return normalizedSkillId;
    }

    internal BattleSkillMasteryGrant BuildVajraBodyMasteryGrantTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        return BuildVajraBodyMasteryGrantTyped(
            sourceUnit,
            targetUnit,
            skillDefinition,
            SkillMasteryResultSnapshot.FromResult(result),
            skillDefinitions
        );
    }

    private BattleSkillMasteryGrant BuildVajraBodyMasteryGrantTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        SkillMasteryResultSnapshot resultSnapshot,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return null;
        if (targetUnit.source_member_id == "" || !targetUnit.is_alive)
            return null;
        if (sourceUnit.faction_id.ToString() == targetUnit.faction_id.ToString())
            return null;
        var statusEntry = targetUnit.GetStatusEffect(StatusVajraBody);
        if (statusEntry == null)
            return null;
        var masterySourceIds = _CollectVajraBodyMasterySourceIds(
            sourceUnit,
            skillDefinition,
            resultSnapshot
        );
        var masterySourceId = _ResolveFirstAllowedSkillMasterySource(
            VajraBodySkillId,
            masterySourceIds,
            skillDefinitions
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

    internal BattleSkillMasteryGrant BuildGuardMasteryGrantFromIncomingHitTyped(
        BattleUnitState attackerUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        AttackEffectResolutionResult result,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (
            attackerUnit == null
            || targetUnit == null
            || effectDefinitions == null
            || effectDefinitions.Count == 0
        )
            return null;
        if (targetUnit.source_member_id == "")
            return null;
        if (!targetUnit.HasStatusEffect("guarding"))
            return null;
        if (!_EffectDefsHavePhysicalDamage(effectDefinitions))
            return null;
        if (!result.AttackSuccess)
            return null;
        if (result.Damage <= 0)
            return null;
        SkillDefinition guardDefinition =
            skillDefinitions != null
            && skillDefinitions.TryGetValue(
                WarriorGuardSkillId,
                out SkillDefinition resolvedGuardDefinition
            )
                ? resolvedGuardDefinition
                : null;
        if (guardDefinition == null)
            return null;
        if (
            _GetSkillMasteryTriggerMode(guardDefinition)
            != CombatSkillMasteryTriggerMode.IncomingPhysicalHit
        )
            return null;
        int amount = _ResolveIncomingSkillMasterySourceAmount(
            attackerUnit,
            targetUnit,
            guardDefinition
        );
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

    internal List<PendingCharacterRewardEntry> BuildBattleRatingMasteryRewardEntries(
        BattleRatingMemberStats stats,
        int score,
        string ratingLabel
    )
    {
        int masteryAmount = ResolveBattleRatingMasteryAmount(score);
        if (masteryAmount <= 0 || stats == null)
            return new List<PendingCharacterRewardEntry>();
        var rewardEntries = new List<PendingCharacterRewardEntry>();
        foreach (KeyValuePair<StringName, int> castCount in stats.cast_counts)
        {
            StringName skillId = castCount.Key;
            if (skillId == "" || castCount.Value <= 0)
                continue;
            rewardEntries.Add(
                new PendingCharacterRewardEntry
                {
                    EntryKind = PendingCharacterRewardEntryKind.SkillMastery,
                    target_id = skillId,
                    amount = masteryAmount,
                    reason_text = $"战斗评分 {score} · {ratingLabel}",
                }
            );
        }
        return rewardEntries;
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

    private bool _IsSkillMasteryQualifyingResult(
        SkillMasteryResultSnapshot result,
        SkillDefinition skillDefinition
    )
    {
        var triggerMode = _GetSkillMasteryTriggerMode(skillDefinition);
        switch (triggerMode)
        {
            case CombatSkillMasteryTriggerMode.WeaponAttackQuality:
                return result.AttackSuccess
                    && (result.CriticalHit || result.HasWeaponDiceMaxEvent);
            case CombatSkillMasteryTriggerMode.DamageDealt:
                return result.HasEffectiveDamageOrAbsorb;
            case CombatSkillMasteryTriggerMode.StatusApplied:
                return result.HasStatusApplied;
            case CombatSkillMasteryTriggerMode.EffectApplied:
                return result.Applied;
            case CombatSkillMasteryTriggerMode.IncomingPhysicalHit:
                return false;
            case CombatSkillMasteryTriggerMode.SecondaryHit:
                return result.SecondaryHitSuccess;
            case CombatSkillMasteryTriggerMode.SourceBoundWeaponBonusDamage:
                return false;
            case CombatSkillMasteryTriggerMode.SkillDamageDiceMax:
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
        SkillDefinition skillDefinition,
        bool additionalEffectApplied = false
    )
    {
        var triggerMode = _GetSkillMasteryTriggerMode(skillDefinition);
        switch (triggerMode)
        {
            case CombatSkillMasteryTriggerMode.WeaponAttackQuality:
                return result.AttackSuccess
                    && (result.CriticalHit || _ResultHasWeaponDiceMaxEvent(result));
            case CombatSkillMasteryTriggerMode.DamageDealt:
                return _ResultHasEffectiveDamageOrAbsorb(result);
            case CombatSkillMasteryTriggerMode.StatusApplied:
                return _ResultHasStatusApplied(result);
            case CombatSkillMasteryTriggerMode.EffectApplied:
                return result.Applied || additionalEffectApplied;
            case CombatSkillMasteryTriggerMode.IncomingPhysicalHit:
                return false;
            case CombatSkillMasteryTriggerMode.SecondaryHit:
                return result.SecondaryHitSuccess;
            case CombatSkillMasteryTriggerMode.SourceBoundWeaponBonusDamage:
                return false;
            case CombatSkillMasteryTriggerMode.SkillDamageDiceMax:
                if (!_ResultHasEffectiveDamageOrAbsorb(result))
                    return false;
                return _ResultHasSkillDamageDieEvent(result);
            default:
                if (!_ResultHasEffectiveDamageOrAbsorb(result))
                    return false;
                return _ResultHasSkillDamageDieEvent(result);
        }
    }

    private CombatSkillMasteryTriggerMode _GetSkillMasteryTriggerMode(
        SkillDefinition skillDefinition
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return combatProfile?.MasteryTriggerModeKind
            ?? CombatSkillMasteryTriggerMode.SkillDamageDiceMax;
    }

    private CombatSkillMasteryAmountMode _GetSkillMasteryAmountMode(
        SkillDefinition skillDefinition
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return combatProfile?.MasteryAmountModeKind
            ?? CombatSkillMasteryAmountMode.PerTargetRank;
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

    private bool _EffectDefsHavePhysicalDamage(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions ?? System.Array.Empty<CombatEffectDefinition>())
        {
            if (effectDefinition == null || effectDefinition.EffectKind != BattleEffectKind.Damage)
                continue;
            var tag = ProgressionDataUtils.to_string_name(effectDefinition.DamageTag);
            if (tag == "physical_slash" || tag == "physical_pierce" || tag == "physical_blunt")
                return true;
        }
        return false;
    }

    private static bool UnitHasLearnedActiveSkill(BattleUnitState unit, StringName skillId)
    {
        if (unit == null || skillId == "")
            return false;
        return unit.KnowsActiveSkill(skillId) || unit.GetKnownSkillLevelTyped(skillId, 0) > 0;
    }

    private static bool TryGetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId,
        out SkillDefinition skillDefinition
    )
    {
        skillDefinition = null;
        return skillId != ""
            && skillDefinitions != null
            && skillDefinitions.TryGetValue(skillId, out skillDefinition)
            && skillDefinition != null;
    }

    private GArray _CollectVajraBodyMasterySourceIds(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        SkillMasteryResultSnapshot result
    )
    {
        var sourceIds = new GArray();
        if (!result.HasVajraBodyMasteryEvent)
            return sourceIds;
        if (_IsVajraBodyHeavyHitSkill(skillDefinition))
            sourceIds.Add(MasterySourceHeavyHitTaken);
        sourceIds.Add(MasterySourceMaxDamageDieTaken);
        if (_IsEliteOrBossTarget(sourceUnit))
            sourceIds.Add(MasterySourceEliteOrBossDamageTaken);
        return sourceIds;
    }

    private StringName _ResolveFirstAllowedSkillMasterySource(
        StringName skillId,
        GArray sourceIds,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (skillId == "" || sourceIds == null || sourceIds.Count == 0)
            return "";
        var skillDefinition =
            skillDefinitions != null
            && skillDefinitions.TryGetValue(skillId, out SkillDefinition resolvedSkillDefinition)
                ? resolvedSkillDefinition
                : null;
        if (skillDefinition == null)
            return "";
        foreach (var sourceIdValue in sourceIds)
        {
            var sourceId = ProgressionDataUtils.to_string_name(sourceIdValue);
            if (sourceId == "")
                continue;
            if (
                skillDefinition.MasterySources.Count == 0
                || HasStringName(skillDefinition.MasterySources, sourceId)
            )
                return sourceId;
        }
        return "";
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        foreach (StringName value in values)
        {
            if (value == target)
                return true;
        }
        return false;
    }

    private bool _IsVajraBodyHeavyHitSkill(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null)
            return false;
        if (skillDefinition.SkillId.ToString().Contains("heavy"))
            return true;
        if ((skillDefinition.DisplayName ?? "").Contains("重击"))
            return true;
        return skillDefinition.HasTag("heavy");
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
        int hpMax = Mathf.Max(unitState.attribute_snapshot.GetValue(HpMax), 1);
        return unitState.current_hp > 0 && unitState.current_hp * 3 < hpMax;
    }

    private int _ResolveSkillMasteryTargetAmount(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return 0;
        var amountMode = _GetSkillMasteryAmountMode(skillDefinition);
        switch (amountMode)
        {
            case CombatSkillMasteryAmountMode.PerCastHpRatio:
            {
                if (sourceUnit.attribute_snapshot == null)
                    return 0;
                int hpMax = Mathf.Max(
                    sourceUnit.attribute_snapshot.GetValue(HpMax),
                    1
                );
                int currentHp = sourceUnit.current_hp;
                if (currentHp * 4 < hpMax)
                    return 4;
                if (currentHp * 2 < hpMax)
                    return 2;
                return 1;
            }
            case CombatSkillMasteryAmountMode.PerTargetRank:
            {
                if (sourceUnit.faction_id == targetUnit.faction_id)
                {
                    if (!_IsSameFactionSupportMasteryTarget(skillDefinition))
                        return 0;
                    int baseAmount = 1;
                    CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
                    if (combatProfile != null && targetUnit.attribute_snapshot != null)
                    {
                        int multiplier = Mathf.Max(
                            combatProfile.MasteryLowHpBonusMultiplier,
                            1
                        );
                        int thresholdPercent = Mathf.Clamp(
                            combatProfile.MasteryLowHpThresholdPercent,
                            1,
                            100
                        );
                        if (multiplier > 1)
                        {
                            int hpMax = Mathf.Max(
                                targetUnit.attribute_snapshot.GetValue(HpMax),
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
            default:
                return 0;
        }
    }

    private bool _IsSameFactionSupportMasteryTarget(SkillDefinition skillDefinition)
    {
        var triggerMode = _GetSkillMasteryTriggerMode(skillDefinition);
        if (
            triggerMode != CombatSkillMasteryTriggerMode.StatusApplied
            && triggerMode != CombatSkillMasteryTriggerMode.EffectApplied
        )
            return false;
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        var targetFilter = ProgressionDataUtils.to_string_name(combatProfile.TargetTeamFilter);
        return targetFilter == "ally" || targetFilter == "self";
    }

    private int _ResolveIncomingSkillMasterySourceAmount(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return 0;
        if (
            _GetSkillMasteryAmountMode(skillDefinition)
            != CombatSkillMasteryAmountMode.PerTargetRank
        )
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
            && unitState.attribute_snapshot.GetValue(FortuneMarkTargetStatId)
                > 0;
    }

    private bool _IsBossTarget(BattleUnitState unitState)
    {
        return unitState != null
            && unitState.attribute_snapshot != null
            && (
                unitState.attribute_snapshot.GetValue(BossTargetStatId) > 0
                || unitState.attribute_snapshot.GetValue(FortuneMarkTargetStatId)
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

        internal static SkillMasteryResultSnapshot FromDictionary(GDictionary source)
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

        internal static SkillMasteryResultSnapshot FromResult(
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
                            ReadStringNameField(evt, "weapon_damage_dice_is_max_reason")
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

        private static StringName ReadStringNameField(GDictionary dictionary, string key)
        {
            if (dictionary == null || !dictionary.ContainsKey(key))
                return "";
            return ProgressionDataUtils.to_string_name(dictionary[key]);
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
