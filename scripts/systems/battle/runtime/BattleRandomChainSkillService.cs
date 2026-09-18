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
        BattleAttackActionContext actionContext,
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
        string skillLabel = BattleSkillTargetPlanRules._format_skill_variant_label(skillDefinition, castVariantDefinition);
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        while (attemptCount < maxAttempts)
        {
            List<BattleUnitState> chainPool = _targetValidationService.BuildRandomChainTargetPool(
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
                        actionContext,
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
                    actionContext,
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
                    batch,
                    actionContext
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
