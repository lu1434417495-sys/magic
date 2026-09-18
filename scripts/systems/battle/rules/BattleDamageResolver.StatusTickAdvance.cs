using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal enum BattleStatusTickAdvancePreviewMode { Minimum, Average, Maximum }

internal sealed record BattleStatusTickAdvancePreview(
    int TickCount, int SourceCount, int RawDamage, int IncomingDamage,
    int HpDamage, int ShieldAbsorbed, bool TargetDefeated)
{
    internal static BattleStatusTickAdvancePreview Empty { get; } = new(0, 0, 0, 0, 0, 0, false);
}

public partial class BattleDamageResolver
{
    private sealed record StatusTickAdvanceOutcome(
        IReadOnlyList<AppliedDamageResult> Results, int TickCount, int SourceCount, int RawDamage);

    internal IReadOnlyList<BattleStatusTickAdvanceStep> BuildStatusTickAdvancePlanTyped(
        BattleUnitState target, CombatEffectDefinition effect, BattleState state) =>
        BattleStatusTickAdvanceRules.BuildPlan(target, effect,
            state?.timeline?.current_tu ?? 0, _skillDefinitionIndex);

    private StatusTickAdvanceOutcome ResolveStatusTickAdvance(
        BattleUnitState caster, BattleUnitState target,
        CombatEffectDefinition effect, DamageResolutionContext context, bool minimumRoll = false)
    {
        var results = new List<AppliedDamageResult>();
        var sources = new HashSet<BattleStatusSourceIdentity>();
        int currentTu = context.BattleState?.timeline?.current_tu ?? 0;
        int ticks = 0;
        int rawTotal = 0;
        // It remains timeline damage: no second save, spell critical or caster
        // offense/weapon bonus. The action's damage and kill credit belongs to caster.
        var tickContext = context.WithDamageOriginKind(BattleDamageOriginKind.TimelineUpkeep);
        foreach (var step in BuildStatusTickAdvancePlanTyped(target, effect, context.BattleState))
        {
            if (!target.IsAlive())
                break;
            if (!BattleStatusTickAdvanceRules.ConsumeTick(target, effect.StatusId, step, currentTu))
                continue;
            sources.Add(step.Source);
            ticks++;
            int raw = minimumRoll ? step.DiceCount + step.FlatDamage : step.DiceCount > 0
                ? RollDicePool(step.DiceCount, step.DiceSides, step.FlatDamage,
                    "advanced_status_tick", context.DamageRollMode).TotalWithBonus
                : step.FlatDamage;
            rawTotal += raw;
            var input = BuildTimelineDamageInput(target, raw, step.DamageTag,
                caster, context.BattleState, tickContext);
            results.Add(ApplyDamageToTargetResult(target, input, caster, tickContext));
        }
        return new(results, ticks, sources.Count, rawTotal);
    }

    internal BattleStatusTickAdvancePreview PreviewStatusTickAdvanceTyped(
        BattleUnitState caster, BattleUnitState target, CombatEffectDefinition effect,
        BattleState state, BattleStatusTickAdvancePreviewMode rollMode = BattleStatusTickAdvancePreviewMode.Average)
    {
        var workingSet = BattleDamagePreviewWorkingSet.CreateDetached(caster, target, state);
        return PreviewStatusTickAdvanceOnWorkingSetTyped(workingSet, effect, rollMode);
    }

    internal BattleStatusTickAdvancePreview PreviewStatusTickAdvanceOnWorkingSetTyped(
        BattleDamagePreviewWorkingSet workingSet, CombatEffectDefinition effect,
        BattleStatusTickAdvancePreviewMode rollMode = BattleStatusTickAdvancePreviewMode.Average)
    {
        if (workingSet == null || effect == null)
            return BattleStatusTickAdvancePreview.Empty;
        var plan = BuildStatusTickAdvancePlanTyped(workingSet.TargetPreview, effect, workingSet.BattleState);
        var branches = workingSet.HasContinuationState
            ? workingSet.ContinuationBranches.ToArray()
            : new[] { new BattleFatalInterceptPreviewBranch {
                ProbabilityBasisPoints = 10000, SourceUnit = workingSet.SourcePreview,
                TargetUnit = workingSet.TargetPreview, BattleState = workingSet.BattleState } };
        long rawTotal = 0, incomingTotal = 0, hpTotal = 0, shieldTotal = 0;
        int ticks = 0;
        var sources = new HashSet<BattleStatusSourceIdentity>();
        foreach (var step in plan)
        {
            var nextBranches = new List<BattleFatalInterceptPreviewBranch>();
            bool reached = false;
            foreach (var branch in branches)
            {
                var target = branch.TargetUnit;
                int probability = branch.ProbabilityBasisPoints;
                if (target?.IsAlive() != true || probability <= 0
                    || !BattleStatusTickAdvanceRules.ConsumeTick(target, effect.StatusId, step,
                        branch.BattleState.timeline.current_tu))
                {
                    nextBranches.Add(branch);
                    continue;
                }
                reached = true;
                var context = DamageResolutionContext.Empty().WithBattleState(branch.BattleState)
                    .WithDamageOriginKind(BattleDamageOriginKind.TimelineUpkeep).WithDetachedPreviewMode();
                int raw = rollMode == BattleStatusTickAdvancePreviewMode.Minimum ? step.DiceCount + step.FlatDamage
                    : step.DiceCount > 0 ? RollDicePool(step.DiceCount, step.DiceSides, step.FlatDamage,
                        "advanced_status_tick", rollMode == BattleStatusTickAdvancePreviewMode.Maximum ? new StringName("maximum") : new StringName("average")).TotalWithBonus
                    : step.FlatDamage;
                int hpBefore = target.GetCurrentHp();
                var input = BuildTimelineDamageInput(target, raw, step.DamageTag, branch.SourceUnit, branch.BattleState, context);
                var applied = ApplyDamageToTargetResult(target, input, branch.SourceUnit, context);
                rawTotal += (long)raw * probability;
                incomingTotal += (long)applied.Event.ResolvedDamage * probability;
                shieldTotal += (long)applied.ShieldAbsorbed * probability;
                var continuations = applied.FatalInterceptPreview?.ContinuationBranches;
                if (continuations?.Count > 0)
                {
                    // A survival branch consumes this tick too, then receives only
                    // the remaining ticks. Never reuse the pre-intercept target.
                    var scaled = new List<BattleFatalInterceptPreviewBranch>();
                    AppendScaledContinuationBranches(scaled, continuations, probability);
                    nextBranches.AddRange(scaled);
                    foreach (var continuation in scaled)
                        hpTotal += (long)Math.Max(hpBefore - Math.Max(continuation.TargetUnit.GetCurrentHp(), 0), 0)
                            * continuation.ProbabilityBasisPoints;
                }
                else
                {
                    hpTotal += (long)applied.HpDamage * probability;
                    nextBranches.Add(branch);
                }
            }
            if (reached) { ticks++; sources.Add(step.Source); }
            workingSet.ReplaceContinuationBranches(nextBranches);
            branches = workingSet.ContinuationBranches.ToArray();
        }
        return new(ticks, sources.Count, (int)(rawTotal / 10000), (int)(incomingTotal / 10000),
            (int)(hpTotal / 10000), (int)(shieldTotal / 10000),
            branches.Length > 0 && branches.All(b => b.TargetUnit?.IsAlive() == false));
    }
}
