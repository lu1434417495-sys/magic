using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：攻击元数据解析与攻击/法术控制事件 payload 及 tag 构建。按阶段拆出，不改逻辑。
public partial class BattleDamageResolver
{
    private AttackResolutionMetadata ResolveAttackMetadata(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackCheckInput attackCheck,
        AttackContext attackContext
    )
    {
        _hit_resolver ??= new BattleHitResolver();
        return _hit_resolver.ResolveAttackMetadata(
            sourceUnit,
            targetUnit,
            attackCheck,
            attackContext
        );
    }

    private BattleSpellControlMetadata ResolveSpellControlMetadata(
        BattleUnitState sourceUnit,
        SpellControlCheckContext context
    )
    {
        _hit_resolver ??= new BattleHitResolver();
        return _hit_resolver.ResolveSpellControlMetadataTyped(
            sourceUnit,
            context.ToAttackContext()
        );
    }

    private AttackEffectResolutionResult ApplyAttackMetadataResult(
        AttackEffectResolutionResult result,
        AttackResolutionMetadata attackMetadata
    )
    {
        attackMetadata ??= new AttackResolutionMetadata();
        result.AttackResolution = AttackEffectResolutionResultReader.ParseAttackResolution(
            attackMetadata.AttackResolution
        );
        result.AttackSuccess = attackMetadata.AttackSuccess;
        result.CriticalHit = attackMetadata.CriticalHit;
        result.CriticalFail = attackMetadata.CriticalFail;
        result.CriticalSource = AttackEffectResolutionResultReader.ParseCriticalSource(
            ResolveCriticalSource(attackMetadata)
        );
        result.ReverseFateDowngraded = attackMetadata.ReverseFateDowngraded;
        result.SecondaryHitSuccess = attackMetadata.SecondaryHitSuccess;
        result.CritGateDie = attackMetadata.CritGateDie;
        result.CritGateRoll = attackMetadata.CritGateRoll;
        result.HitRoll = attackMetadata.HitRoll;
        result.RequiredRoll = attackMetadata.RequiredRoll;
        result.DisplayRequiredRoll = attackMetadata.DisplayRequiredRoll;
        result.HitRatePercent = attackMetadata.HitRatePercent;
        result.SuccessRatePercent = attackMetadata.SuccessRatePercent;
        result.SkillId = attackMetadata.SkillId;
        result.TraitTriggerResults = BuildTraitTriggerResultsTyped(attackMetadata);
        return result;
    }

    private GDictionary BuildAttackEffectContext(AttackResolutionMetadata attackMetadata)
    {
        attackMetadata ??= new AttackResolutionMetadata();
        return new GDictionary
        {
            ["attack_resolution"] = attackMetadata.AttackResolution,
            ["attack_success"] = attackMetadata.AttackSuccess,
            ["critical_hit"] = attackMetadata.CriticalHit,
            ["critical_fail"] = attackMetadata.CriticalFail,
            ["ordinary_miss"] = attackMetadata.OrdinaryMiss,
            ["critical_source"] = ResolveCriticalSource(attackMetadata),
            ["is_disadvantage"] = attackMetadata.IsDisadvantage,
            ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
            ["effective_luck"] = attackMetadata.EffectiveLuck,
            ["crit_locked"] = attackMetadata.CritLocked,
            ["crit_gate_die"] = attackMetadata.CritGateDie,
            ["crit_gate_roll"] = attackMetadata.CritGateRoll,
            ["hit_roll"] = attackMetadata.HitRoll,
            ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
            ["crit_threshold"] = attackMetadata.CritThreshold,
            ["required_roll"] = attackMetadata.RequiredRoll,
            ["display_required_roll"] = attackMetadata.DisplayRequiredRoll,
            ["hit_rate_percent"] = attackMetadata.HitRatePercent,
            ["success_rate_percent"] = attackMetadata.SuccessRatePercent,
            ["reverse_fate_downgraded"] = attackMetadata.ReverseFateDowngraded,
            ["secondary_hit_success"] = attackMetadata.SecondaryHitSuccess,
            ["skill_id"] = attackMetadata.SkillId,
            ["trait_trigger_results"] = BuildTraitTriggerResultsArray(attackMetadata),
        };
    }

    private static GArray BuildTraitTriggerResultsArray(AttackResolutionMetadata attackMetadata)
    {
        var results = new GArray();
        if (attackMetadata?.TraitTriggerResults == null)
        {
            return results;
        }
        foreach (AttackTraitTriggerResult triggerResult in attackMetadata.TraitTriggerResults)
        {
            if (!triggerResult.Triggered)
            {
                continue;
            }
            results.Add(
                new GDictionary
                {
                    ["triggered"] = triggerResult.Triggered,
                    ["event"] = triggerResult.Event,
                    ["trait_id"] = triggerResult.TraitId,
                    ["effect_type"] = triggerResult.EffectType,
                    ["original_roll"] = triggerResult.OriginalRoll,
                    ["reroll_die"] = triggerResult.RerollDie,
                    ["rerolled_roll"] = triggerResult.RerolledRoll,
                    ["die_size"] = triggerResult.DieSize,
                    ["charge_key"] = triggerResult.ChargeKey,
                    ["charges_remaining"] = triggerResult.ChargesRemaining,
                }
            );
        }
        return results;
    }

    private static TraitTriggerEventResult[] BuildTraitTriggerResultsTyped(
        AttackResolutionMetadata attackMetadata
    )
    {
        var results = new List<TraitTriggerEventResult>();
        if (attackMetadata?.TraitTriggerResults == null)
        {
            return results.ToArray();
        }
        foreach (AttackTraitTriggerResult triggerResult in attackMetadata.TraitTriggerResults)
        {
            if (!triggerResult.Triggered)
            {
                continue;
            }
            results.Add(
                new TraitTriggerEventResult
                {
                    Triggered = triggerResult.Triggered,
                    Event = triggerResult.Event,
                    TraitId = triggerResult.TraitId,
                    EffectType = triggerResult.EffectType,
                    OriginalRoll = triggerResult.OriginalRoll,
                    RerollDie = triggerResult.RerollDie,
                    RerolledRoll = triggerResult.RerolledRoll,
                    DieSize = triggerResult.DieSize,
                    ChargeKey = triggerResult.ChargeKey,
                    ChargesRemaining = triggerResult.ChargesRemaining,
                }
            );
        }
        return results.ToArray();
    }

    private void AttachAttackReportEntry(
        GDictionary result,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata
    )
    {
        if (result == null || result.Count == 0)
        {
            return;
        }
        GDictionary reportEntry = _report_formatter.BuildAttackReportEntry(
            sourceUnit,
            targetUnit,
            attackMetadata,
            ResolveCriticalSource(attackMetadata),
            BuildAttackEventTags(attackMetadata)
        );
        if (reportEntry.Count > 0)
        {
            result["report_entry"] = DuplicateDictionary(reportEntry);
        }
    }

    private AttackEffectResolutionResult AttachAttackReportEntry(
        AttackEffectResolutionResult result,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata
    )
    {
        GDictionary reportEntry = _report_formatter.BuildAttackReportEntry(
            sourceUnit,
            targetUnit,
            attackMetadata,
            ResolveCriticalSource(attackMetadata),
            BuildAttackEventTags(attackMetadata)
        );
        if (reportEntry.Count <= 0)
        {
            return result;
        }
        result.ReportEntry = BattleReportEntryPayload.ReadPayload(reportEntry);
        result.HasReportEntry = true;
        return result;
    }

    private void DispatchAttackResolutionEvents(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext
    )
    {
        if (attackMetadata == null)
        {
            return;
        }
        GDictionary payload = BuildAttackEventPayload(
            sourceUnit,
            targetUnit,
            attackMetadata,
            attackContext
        );
        foreach (StringName eventType in BuildAttackEventTags(attackMetadata))
        {
            _fate_event_bus.dispatch(eventType, payload);
        }
    }

    private void DispatchSpellControlResolutionEvents(
        BattleUnitState sourceUnit,
        BattleSpellControlMetadata controlMetadata,
        SpellControlCheckContext context
    )
    {
        if (controlMetadata == null || !controlMetadata.HasResolutionMetadata)
        {
            return;
        }
        GDictionary payload = BuildSpellControlEventPayload(
            sourceUnit,
            controlMetadata,
            context
        );
        foreach (StringName eventType in BuildSpellControlEventTags(controlMetadata))
        {
            _fate_event_bus.dispatch(eventType, payload);
        }
    }

    private GDictionary BuildAttackEventPayload(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext
    )
    {
        BattleState battleState = attackContext?.BattleState;
        attackMetadata ??= new AttackResolutionMetadata();
        return new GDictionary
        {
            ["battle_id"] = battleState != null ? battleState.battle_id : new StringName(""),
            ["attacker_id"] = sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            ["attacker_member_id"] =
                sourceUnit != null ? sourceUnit.source_member_id : new StringName(""),
            ["attacker_low_hp_hardship"] = IsLowHpHardship(sourceUnit),
            ["attacker_strong_attack_debuff_ids"] = GetStrongAttackDebuffIds(sourceUnit),
            ["defender_id"] = targetUnit != null ? targetUnit.unit_id : new StringName(""),
            ["defender_member_id"] =
                targetUnit != null ? targetUnit.source_member_id : new StringName(""),
            ["defender_is_elite_or_boss"] = IsEliteOrBoss(targetUnit),
            ["attack_resolution"] = attackMetadata.AttackResolution,
            ["critical_source"] = ResolveCriticalSource(attackMetadata),
            ["is_disadvantage"] = attackMetadata.IsDisadvantage,
            ["crit_gate_die"] = attackMetadata.CritGateDie,
            ["crit_gate_roll"] = attackMetadata.CritGateRoll,
            ["hit_roll"] = attackMetadata.HitRoll,
            ["luck_snapshot"] = BuildAttackLuckSnapshot(attackMetadata),
        };
    }

    private GDictionary BuildSpellControlEventPayload(
        BattleUnitState sourceUnit,
        BattleSpellControlMetadata controlMetadata,
        SpellControlCheckContext context
    )
    {
        controlMetadata ??= BattleSpellControlMetadata.Empty();
        return new GDictionary
        {
            ["battle_id"] =
                context.BattleState != null ? context.BattleState.battle_id : new StringName(""),
            ["attacker_id"] = sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            ["attacker_member_id"] =
                sourceUnit != null ? sourceUnit.source_member_id : new StringName(""),
            ["attacker_low_hp_hardship"] = IsLowHpHardship(sourceUnit),
            ["attacker_strong_attack_debuff_ids"] = GetStrongAttackDebuffIds(sourceUnit),
            ["defender_id"] = new StringName(""),
            ["defender_member_id"] = new StringName(""),
            ["defender_is_elite_or_boss"] = false,
            ["attack_resolution"] = controlMetadata.AttackResolution,
            ["spell_control_resolution"] = controlMetadata.SpellControlResolution,
            ["critical_source"] = ResolveCriticalSource(controlMetadata),
            ["is_disadvantage"] = controlMetadata.IsDisadvantage,
            ["crit_gate_die"] = controlMetadata.CritGateDie,
            ["crit_gate_roll"] = controlMetadata.CritGateRoll,
            ["hit_roll"] = controlMetadata.HitRoll,
            ["luck_snapshot"] = BuildAttackLuckSnapshot(controlMetadata),
            ["event_family"] = "spell_control",
            ["skill_id"] = context.SkillId,
        };
    }

    private static GDictionary BuildAttackLuckSnapshot(AttackResolutionMetadata attackMetadata)
    {
        attackMetadata ??= new AttackResolutionMetadata();
        return new GDictionary
        {
            ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
            ["effective_luck"] = attackMetadata.EffectiveLuck,
            ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
            ["crit_threshold"] = attackMetadata.CritThreshold,
        };
    }

    private static GDictionary BuildAttackLuckSnapshot(BattleSpellControlMetadata attackMetadata)
    {
        attackMetadata ??= BattleSpellControlMetadata.Empty();
        return new GDictionary
        {
            ["hidden_luck_at_birth"] = attackMetadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = attackMetadata.FaithLuckBonus,
            ["effective_luck"] = attackMetadata.EffectiveLuck,
            ["fumble_low_end"] = attackMetadata.FumbleLowEnd,
            ["crit_threshold"] = attackMetadata.CritThreshold,
        };
    }

    private static StringName ResolveCriticalSource(AttackResolutionMetadata attackMetadata)
    {
        return attackMetadata == null || !attackMetadata.CriticalHit ? new StringName("")
            : IsHighThreatCriticalHit(attackMetadata) ? new StringName("high_threat")
            : new StringName("gate_die");
    }

    private static StringName ResolveCriticalSource(BattleSpellControlMetadata attackMetadata)
    {
        return attackMetadata == null || !attackMetadata.CriticalHit ? new StringName("")
            : IsHighThreatCriticalHit(attackMetadata) ? new StringName("high_threat")
            : new StringName("gate_die");
    }

    private static bool IsHighThreatCriticalHit(AttackResolutionMetadata attackMetadata)
    {
        return attackMetadata != null
            && attackMetadata.CriticalHit
            && attackMetadata.CritGateDie == NaturalHitRoll;
    }

    private static bool IsHighThreatCriticalHit(BattleSpellControlMetadata attackMetadata)
    {
        return attackMetadata != null
            && attackMetadata.CriticalHit
            && attackMetadata.CritGateDie == NaturalHitRoll;
    }

    private static bool IsLowHpHardship(BattleUnitState unitState)
    {
        int maxHp = GetAttributeValue(unitState, AttributeService.ToStringName(AttributeIdKind.HpMax));
        return unitState != null
            && maxHp > 0
            && unitState.current_hp * 100
                <= maxHp * BattleState.LowHpAttackDisadvantagePercent;
    }

    private static GStringNameArray GetStrongAttackDebuffIds(BattleUnitState unitState)
    {
        var strongStatusIds = new GStringNameArray();
        if (unitState == null)
        {
            return strongStatusIds;
        }
        foreach (StringName statusId in BattleState.StrongAttackDisadvantageStatusIdsTyped())
        {
            if (statusId != "" && unitState.HasStatusEffect(statusId))
            {
                strongStatusIds.Add(statusId);
            }
        }
        return strongStatusIds;
    }

    private static bool IsEliteOrBoss(BattleUnitState unitState)
    {
        return GetAttributeValue(unitState, FortuneMarkTargetStatId) > 0;
    }

    private static GStringNameArray BuildAttackEventTags(AttackResolutionMetadata attackMetadata)
    {
        var tags = new GStringNameArray();
        if (attackMetadata == null)
        {
            return tags;
        }
        if (attackMetadata.CriticalFail)
            tags.Add("critical_fail");
        if (IsHighThreatCriticalHit(attackMetadata))
            tags.Add("high_threat_critical_hit");
        if (attackMetadata.CriticalHit && attackMetadata.IsDisadvantage)
            tags.Add("critical_success_under_disadvantage");
        if (attackMetadata.OrdinaryMiss)
            tags.Add("ordinary_miss");
        if (
            attackMetadata.AttackSuccess
            && attackMetadata.IsDisadvantage
            && !attackMetadata.CriticalHit
        )
            tags.Add("hardship_survival");
        return tags;
    }

    private static GStringNameArray BuildSpellControlEventTags(
        BattleSpellControlMetadata controlMetadata
    )
    {
        var tags = new GStringNameArray();
        if (controlMetadata == null)
        {
            return tags;
        }
        if (controlMetadata.CriticalFail)
            tags.Add("critical_fail");
        if (IsHighThreatCriticalHit(controlMetadata))
            tags.Add("high_threat_critical_hit");
        if (controlMetadata.CriticalHit && controlMetadata.IsDisadvantage)
            tags.Add("critical_success_under_disadvantage");
        return tags;
    }
}
