using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleRandomChainSkillService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;
    private BattleSkillTargetValidationService _targetValidationService;
    private readonly BattleNineEchoFinalHammerResolver _nineEchoFinalHammerResolver = new();

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
        BattleSkillTargetValidationService targetValidationService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _targetValidationService = targetValidationService;
        _nineEchoFinalHammerResolver.Setup(runtime, owner);
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _targetValidationService = null;
        _nineEchoFinalHammerResolver.DisposeRuntime();
    }

    internal bool _handle_random_chain_unit_skill_command(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch,
        IReadOnlyList<CombatEffectDefinition> effect_definitions,
        CombatEffectDefinition repeat_attack_effect,
        BattleSpellControlResult spell_control_context
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        int maxHitsPerTarget = Math.Max(combatProfile?.MaxHitsPerTarget ?? 0, 1);
        var chainSelectionCounts = new Dictionary<StringName, int>();
        var chainSuccessfulHitCounts = new Dictionary<StringName, int>();
        bool applied = false;
        int attemptCount = 0;
        int skillLevel = Runtime?._get_unit_skill_level(
            active_unit,
            skillDefinition?.SkillId ?? new StringName("")
        ) ?? 0;
        int configuredAttackCount =
            combatProfile?.GetEffectiveRandomChainAttackCount(skillLevel) ?? 0;
        int maxAttempts =
            configuredAttackCount > 0
                ? configuredAttackCount
                : Math.Max(
                    (Runtime?._state?.UnitCount ?? 0) * maxHitsPerTarget,
                    1
                );
        bool continueOnMiss = combatProfile?.RandomChainContinueOnMiss == true;
        string skillLabel = _owner._format_skill_variant_label(skillDefinition, castVariantDefinition);
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        while (attemptCount < maxAttempts)
        {
            List<BattleUnitState> chainPool = BuildRandomChainTargetPool(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                chainSelectionCounts,
                maxHitsPerTarget
            );
            if (chainPool.Count == 0)
            {
                break;
            }
            ShuffleRandomChainPool(chainPool);
            BattleUnitState targetUnit = chainPool[0];
            if (targetUnit == null)
            {
                break;
            }
            batch?.AddLogLine(
                $"{active_unit.display_name} 的{skillLabel}锁定了 {targetUnit.display_name}。"
            );
            StringName targetId = targetUnit.unit_id;
            chainSelectionCounts.TryGetValue(targetId, out int targetSelectionCount);
            chainSelectionCounts[targetId] = targetSelectionCount + 1;
            attemptCount += 1;
            bool stageApplied;
            if (repeat_attack_effect != null)
            {
                stageApplied =
                    repeatAttackResolver != null
                    && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                        active_unit,
                        targetUnit,
                        skillDefinition,
                        effect_definitions,
                        repeat_attack_effect,
                        batch,
                        castVariantDefinition
                    );
            }
            else
            {
                stageApplied = _owner._apply_unit_skill_result(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    effect_definitions,
                    batch,
                    spell_control_context
                );
            }
            if (stageApplied)
            {
                applied = true;
                chainSuccessfulHitCounts.TryGetValue(
                    targetId,
                    out int targetSuccessfulHitCount
                );
                targetSuccessfulHitCount += 1;
                chainSuccessfulHitCounts[targetId] = targetSuccessfulHitCount;
                _nineEchoFinalHammerResolver.ApplySuccessfulHitReward(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    effect_definitions,
                    targetSuccessfulHitCount,
                    batch
                );
            }
            else if (!continueOnMiss)
            {
                break;
            }
        }
        if (attemptCount > 0)
        {
            batch?.AddLogLine(
                $"{active_unit.display_name} 的{skillLabel}执行了 {attemptCount} 次攻击链判定。"
            );
        }
        return applied;
    }

    internal List<BattleUnitState> BuildRandomChainTargetPool(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyDictionary<StringName, int> chain_hit_counts,
        int max_hits_per_target
    )
    {
        var chainPool = new List<BattleUnitState>();
        BattleState state = _owner.RtState();
        if (state == null)
        {
            return chainPool;
        }
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || candidate == active_unit
                || !candidate.IsAlive()
            )
            {
                continue;
            }
            StringName candidateId = ProgressionDataUtils.to_string_name(
                candidate.unit_id
            );
            if (
                BattleSkillExecutionOrchestrator.StringNameIsEmpty(candidateId)
                || (
                    chain_hit_counts != null
                    && chain_hit_counts.TryGetValue(candidateId, out int hitCount)
                    && hitCount >= max_hits_per_target
                )
            )
            {
                continue;
            }
            if (!_targetValidationService._can_skill_target_unit(active_unit, candidate, skillDefinition, false, castVariant))
            {
                continue;
            }
            chainPool.Add(candidate);
        }
        return chainPool;
    }

    internal List<BattleUnitReadView> BuildRandomChainTargetPool(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        int max_hits_per_target
    )
    {
        var chainPool = new List<BattleUnitReadView>();
        BattleState state = _owner.RtState();
        if (state == null || !active_unit.IsValid)
        {
            return chainPool;
        }
        foreach (BattleUnitReadView candidate in state.AsReadView().AliveUnits())
        {
            if (
                !candidate.IsValid
                || candidate.UnitId == active_unit.UnitId
                || BattleSkillExecutionOrchestrator.StringNameIsEmpty(candidate.UnitId)
            )
            {
                continue;
            }
            if (max_hits_per_target <= 0)
            {
                continue;
            }
            if (!_targetValidationService._can_skill_target_unit(active_unit, candidate, skillDefinition, false, castVariant))
            {
                continue;
            }
            chainPool.Add(candidate);
        }
        return chainPool;
    }

    internal void _shuffle_random_chain_pool(GArray chain_pool)
    {
        if (chain_pool.Count <= 1)
        {
            return;
        }
        for (int index = chain_pool.Count - 1; index > 0; index--)
        {
            int swapIndex = TrueRandomSeedService.RandiRange(0, index);
            if (swapIndex == index)
            {
                continue;
            }
            var temp = chain_pool[index];
            chain_pool[index] = chain_pool[swapIndex];
            chain_pool[swapIndex] = temp;
        }
    }

    private static void ShuffleRandomChainPool(List<BattleUnitState> chainPool)
    {
        if (chainPool == null || chainPool.Count <= 1)
        {
            return;
        }
        for (int index = chainPool.Count - 1; index > 0; index--)
        {
            int swapIndex = TrueRandomSeedService.RandiRange(0, index);
            if (swapIndex == index)
            {
                continue;
            }
            BattleUnitState temp = chainPool[index];
            chainPool[index] = chainPool[swapIndex];
            chainPool[swapIndex] = temp;
        }
    }
}
