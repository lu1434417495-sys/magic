using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattlePreparedChainDamage
{
    internal static BattlePreparedChainDamage Empty { get; } =
        new(null, Array.Empty<CombatEffectDefinition>(), BattleChainDamagePlan.Empty);

    internal BattlePreparedChainDamage(
        CombatEffectDefinition chainEffect,
        IReadOnlyList<CombatEffectDefinition> targetEffects,
        BattleChainDamagePlan plan
    )
    {
        ChainEffect = chainEffect;
        TargetEffects = targetEffects ?? Array.Empty<CombatEffectDefinition>();
        Plan = plan ?? BattleChainDamagePlan.Empty;
    }

    internal CombatEffectDefinition ChainEffect { get; }
    internal IReadOnlyList<CombatEffectDefinition> TargetEffects { get; }
    internal BattleChainDamagePlan Plan { get; }
    internal bool IsConfigured =>
        ChainEffect?.ChainDamage != null && TargetEffects.Count > 0;
}

internal sealed class BattleChainDamageService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;
    private BattleSkillPreviewService _skillPreviewService;

    private BattleRuntimeModule _runtime
    {
        get =>
            _runtimeRef != null
            && _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime)
                ? runtime
                : null;
        set =>
            _runtimeRef =
                value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private BattleRuntimeModule Runtime => _runtime;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleSkillExecutionOrchestrator owner,
        BattleSkillPreviewService skillPreviewService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _skillPreviewService = skillPreviewService;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _skillPreviewService = null;
    }

    internal BattlePreparedChainDamage BuildPreparedPlan(
        BattleUnitState sourceUnit,
        BattleUnitState primaryTarget,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        bool backlashTriggered
    )
    {
        if (sourceUnit == null || primaryTarget == null || skillDefinition == null)
            return BattlePreparedChainDamage.Empty;
        CombatEffectDefinition chainEffect = FindChainEffect(effectDefinitions);
        if (chainEffect?.ChainDamage == null)
            return BattlePreparedChainDamage.Empty;
        IReadOnlyList<CombatEffectDefinition> targetEffects =
            BuildChainTargetEffectDefinitions(effectDefinitions, chainEffect);
        if (targetEffects.Count == 0)
            return BattlePreparedChainDamage.Empty;

        StringName targetFilter = _owner.ResolveEffectTargetFilter(
            skillDefinition,
            chainEffect
        );
        if (BattleSkillExecutionOrchestrator.StringNameIsEmpty(targetFilter))
            return BattlePreparedChainDamage.Empty;
        BattleState state = _owner.RtState();
        if (state == null)
            return BattlePreparedChainDamage.Empty;
        BattleChainDamagePlan plan = BattleChainDamageRules.BuildPlan(
            state.AsReadView(),
            primaryTarget,
            chainEffect.ChainDamage,
            backlashTriggered,
            candidate =>
                _owner._is_unit_valid_for_effect(
                    sourceUnit,
                    candidate.UnsafeUnitForReadOnlyRules,
                    targetFilter
                )
        );
        return new BattlePreparedChainDamage(chainEffect, targetEffects, plan);
    }

    internal BattlePreparedChainDamage BuildPreparedPreviewPlan(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView primaryTarget,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        bool backlashTriggered
    )
    {
        return BuildPreparedPlan(
            sourceUnit.UnsafeUnitForReadOnlyRules,
            primaryTarget.UnsafeUnitForReadOnlyRules,
            skillDefinition,
            effectDefinitions,
            backlashTriggered
        );
    }

    internal void _apply_chain_damage_effects(
        BattleUnitState sourceUnit,
        BattleUnitState primaryTarget,
        SkillDefinition skillDefinition,
        BattlePreparedChainDamage preparedChain,
        AttackEffectResolutionResult primaryResolution,
        BattleEventBatch batch,
        string skillSubject,
        CombatCastVariantDefinition castVariantDefinition = null
    )
    {
        if (
            !primaryResolution.Applied
            || preparedChain == null
            || !preparedChain.IsConfigured
            || preparedChain.Plan.Hops.Count == 0
        )
            return;
        ArgumentNullException.ThrowIfNull(skillDefinition);
        if (skillDefinition.SkillId == new StringName(""))
        {
            throw new ArgumentException(
                "applied chain damage requires a non-empty skill id",
                nameof(skillDefinition)
            );
        }
        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        BattleRatingSystem ratingSystem = Runtime?._battle_rating_system;
        BattleState state = _owner.RtState();
        foreach (BattleChainDamageHopPlan hop in preparedChain.Plan.Hops)
        {
            BattleUnitState chainTarget = state?.GetAliveUnit(hop.TargetUnitId);
            if (chainTarget == null)
                break;
            BattleBarrierInteractionResult barrierResult =
                Runtime?._layered_barrier_service?.ResolveSkillBarrierInteractionBetweenCoordsResult(
                    sourceUnit,
                    hop.OriginCoord,
                    chainTarget,
                    hop.TargetCoord,
                    skillDefinition,
                    preparedChain.TargetEffects,
                    batch,
                    castVariantDefinition
                ) ?? new BattleBarrierInteractionResult(false, false);
            if (barrierResult.Blocked)
                break;

            AttackEffectResolutionResult chainResolution =
                damageResolver?.ResolveEffects(
                    sourceUnit,
                    chainTarget,
                    preparedChain.TargetEffects,
                    DamageResolutionContext
                        .ForSkill(skillDefinition?.SkillId ?? new StringName(""))
                        .WithBattleState(Runtime?.GetState())
                        .WithSourceSkillLevel(
                            Math.Max(
                                sourceUnit.GetKnownSkillLevelTyped(
                                    skillDefinition?.SkillId ?? new StringName(""),
                                    fallback: 1
                                ),
                                1
                            )
                        )
                        .WithDamageOriginKind(
                            BattleDamageOriginContentRules.ResolveProducerOrigin(
                                BattleDamageOriginKind.MainDirectEffect,
                                sourceUnit,
                                chainTarget
                            )
                        )
                        .WithDamageApplicationHookContext(
                            batch,
                            Runtime?.CurrentEffectOriginForContingency
                                ?? BattleEffectOrigin.PlayerCommand()
                        )
                ) ?? new AttackEffectResolutionResult
                {
                    AttackCheck = new AttackCheckInput(
                        skillId: skillDefinition?.SkillId ?? new StringName("")
                    ),
                };
            skillMasteryService?.RecordTargetResult(
                sourceUnit,
                chainTarget,
                skillDefinition,
                chainResolution
            );
            _owner.MarkAppliedStatusesForTurnTiming(
                chainTarget,
                chainResolution.StatusEffectIds
            );
            if (!chainResolution.Applied)
                continue;

            _owner._append_changed_unit_id(batch, sourceUnit.unit_id);
            _owner._append_changed_unit_id(batch, chainTarget.unit_id);
            _owner._append_changed_unit_coords(batch, chainTarget);
            _owner.append_result_source_status_effects(batch, sourceUnit, chainResolution);
            _skillPreviewService.AppendDamageResultLogLines(
                batch,
                $"{skillSubject} 的连锁闪电",
                chainTarget.display_name,
                chainResolution
            );
            foreach (StringName statusId in chainResolution.StatusEffectIds)
                batch.AddLogLine($"{chainTarget.display_name} 获得状态 {statusId}。");

            int chainDamage = chainResolution.Damage;
            int chainHealing = chainResolution.Healing;
            bool causedChainDefeat = !chainTarget.IsAlive();
            if (causedChainDefeat)
            {
                Runtime?._apply_on_kill_gain_resources_effects(
                    sourceUnit,
                    chainTarget,
                    skillDefinition,
                    preparedChain.TargetEffects,
                    batch
                );
                Runtime?.HandleUnitDefeatedByRuntimeEffect(
                    chainTarget,
                    sourceUnit,
                    batch,
                    $"{chainTarget.display_name} 被击倒。",
                    new BattleDefeatHandlingOptions(
                        recordEnemyDefeatedAchievement: true,
                        killProvenance: BattleSkillExecutionOrchestrator.BuildWeaponAttackKillProvenance(
                            sourceUnit,
                            chainResolution,
                            skillDefinition?.SkillId ?? new StringName("")
                        )
                    )
                );
            }
            _owner._record_effect_metrics(
                sourceUnit,
                chainTarget,
                chainDamage,
                chainHealing,
                causedChainDefeat ? 1 : 0
            );
            ratingSystem?.RecordContributionFromUnits(
                sourceUnit,
                chainTarget,
                chainDamage,
                chainHealing,
                causedChainDefeat,
                new StringName("skill"),
                skillDefinition?.SkillId ?? new StringName("")
            );
        }
    }

    internal static CombatEffectDefinition FindChainEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.ChainDamage
                && effectDefinition.ChainDamage != null
            )
                return effectDefinition;
        }
        return null;
    }

    internal static IReadOnlyList<CombatEffectDefinition> BuildChainTargetEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        CombatEffectDefinition chainEffect
    )
    {
        var chainTargetEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition == chainEffect
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
                continue;
            chainTargetEffects.Add(effectDefinition);
        }
        return chainTargetEffects;
    }
}
