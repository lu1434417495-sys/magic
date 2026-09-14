using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiMultiUnitSkillEvaluator
{
    private static readonly StringName EmptyStringName = "";

    /// <summary>
    /// Hard ceiling on enumerated target groups, independent of the authored candidate_group_limit.
    /// Combination enumeration is exponential in pool size, and the layered-barrier path defers
    /// both the pool limit and the group limit to the canonical preview.
    /// </summary>
    private const int MaxEnumeratedTargetGroups = 256;

    /// <summary>
    /// Effect kinds whose outcome on a target depends on that target alone. This is an allow-list
    /// on purpose: a kind that is missing here only costs enumeration, while a coupling kind
    /// wrongly treated as independent silently degrades target choice with nothing to notice.
    /// Anything that moves units, chains between them, repeats off a whole-cast preview, counts
    /// kills across the group, or writes the board is deliberately absent.
    /// </summary>
    private static readonly HashSet<BattleEffectKind> SeparableEffectKinds =
        new()
        {
            BattleEffectKind.Damage,
            BattleEffectKind.Heal,
            BattleEffectKind.HealFatal,
            BattleEffectKind.StaminaRestore,
            BattleEffectKind.Shield,
            BattleEffectKind.Status,
            BattleEffectKind.ApplyStatus,
            BattleEffectKind.EraseStatus,
            BattleEffectKind.CleanseHarmful,
            BattleEffectKind.EquipmentDurabilityDamage,
        };

    private readonly BattleAiTypedActionHelper _helper = new();

    internal BattleAiDecision Evaluate(
        UseMultiUnitSkillActionDefinition action,
        BattleAiContext context
    )
    {
        if (action == null || context == null || !HasExplicitDistanceContract(action))
            return null;

        BattleUnitState actor = context.unit_state;
        if (actor == null || context.state == null)
            return null;

        AiActionTrace actionTrace = context.trace_enabled
            ? BeginActionTrace(
                action,
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "multi_unit_skill",
                    ["target_selector"] = action.TargetSelector.ToString(),
                    ["distance_reference"] = action.DistanceReference.ToString(),
                    ["desired_min_distance"] = action.DesiredMinDistance,
                    ["desired_max_distance"] = action.DesiredMaxDistance,
                    ["candidate_pool_limit"] = action.CandidatePoolLimit,
                    ["candidate_group_limit"] = action.CandidateGroupLimit,
                }
            )
            : null;

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        foreach (BattleAvailableSkillEntry skillEntry in _helper.ResolveAvailableSkillEntries(context, action.SkillIds))
        {
            StringName skillId = skillEntry.EntryRef.SkillId;
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, skillEntry);
            if (!IsMultiUnitSkill(skillDefinition))
            {
                TraceAddBlockReason(
                    actionTrace,
                    skillDefinition == null ? "missing_skill_definition" : "non_multi_unit_skill"
                );
                continue;
            }

            BattleSkillCastBlockReasonKind blockReason = _helper.GetSkillCastBlockReason(
                context,
                skillDefinition
            );
            if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
            {
                TraceAddBlockReason(
                    actionTrace,
                    BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                );
                continue;
            }

            List<BattleUnitState> sortedTargets = _helper.SortTargetUnits(
                context,
                skillDefinition.CombatProfile.TargetTeamFilter,
                action.TargetSelector
            );
            if (sortedTargets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            foreach (CombatCastVariantDefinition castVariant in GetMultiUnitCastVariants(
                context,
                skillDefinition,
                skillEntry.SkillLevel
            ))
            {
                if (castVariant != null && IsChargeOption(castVariant))
                    continue;

                List<List<BattleUnitState>> targetGroups = BuildTargetGroups(
                    context,
                    action,
                    skillEntry,
                    skillDefinition,
                    castVariant,
                    sortedTargets
                );
                if (targetGroups.Count == 0)
                {
                    TraceAddBlockReason(actionTrace, "no_valid_target_groups");
                    continue;
                }

                bool deferGroupLimitUntilCanonicalPreview =
                    HasLayeredBarrier(context)
                    || BattleTargetSlotCostRules.UsesOrderedTargetSlots(
                        skillDefinition
                    );
                int canonicalValidGroupCount = 0;
                foreach (List<BattleUnitState> targetGroup in targetGroups)
                {
                    if (
                        deferGroupLimitUntilCanonicalPreview
                        && canonicalValidGroupCount >= action.CandidateGroupLimit
                    )
                    {
                        break;
                    }
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    BattleCommand command = BuildMultiUnitSkillCommand(
                        context,
                        skillEntry,
                        castVariant,
                        targetGroup
                    );
                    BattlePreview fastPreview = BuildFastUnitSkillPreview(
                        context,
                        skillDefinition,
                        command
                    );
                    BattlePreview preview = _helper.ResolveBarrierAwareUnitSkillPreview(
                        context,
                        command,
                        fastPreview,
                        skillEntry
                    );
                    if (preview?.allowed != true)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }
                    if (preview.TargetUnitIdsTyped.Count == 0)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        TraceAddBlockReason(actionTrace, "barrier_blocked_all_targets");
                        continue;
                    }
                    canonicalValidGroupCount += 1;

                    List<BattleUnitState> effectiveTargetGroup = ResolvePreviewTargetGroup(
                        targetGroup,
                        preview
                    );
                    Dictionary<string, object> positionMetadata = BuildPositionMetadata(
                        context,
                        action,
                        effectiveTargetGroup,
                        skillDefinition
                    );
                    string optionLabel = EnemyAiActionHelper.FormatSkillVariantLabel(
                        skillDefinition,
                        castVariant
                    );
                    positionMetadata["action_label"] = optionLabel;
                    BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                        action,
                        context,
                        skillDefinition,
                        command,
                        preview,
                        _helper.CollectUnitSkillEffectDefinitions(
                            skillDefinition,
                            castVariant,
                            skillEntry.SkillLevel
                        ),
                        positionMetadata
                    );
                    int targetCount = preview.TargetUnitIdsTyped.Count;

                    if (actionTrace != null)
                    {
                        var candidateExtra = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["skill_id"] = skillId.ToString(),
                            ["target_count"] = targetCount,
                        };
                        TraceOfferCandidate(
                            actionTrace,
                            EnemyAiActionHelper.BuildCandidateSummary(
                                optionLabel,
                                command,
                                scoreInput,
                                candidateExtra
                            )
                        );
                    }

                    if (scoreInput == null)
                    {
                        fallbackDecision ??= EnemyAiActionHelper.CreateDecision(
                            action.ActionId,
                            action.ScoreBucketId,
                            command,
                            $"{actor.display_name} 准备用 {skillDefinition.DisplayName} 锁定 {targetCount} 个单位。"
                        );
                        continue;
                    }
                    if (!BattleAiDecisionEngine.IsBetterScoreInputTyped(scoreInput, bestScoreInput))
                        continue;

                    bestScoreInput = scoreInput;
                    bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                        action.ActionId,
                        action.ScoreBucketId,
                        command,
                        scoreInput,
                        $"{actor.display_name} 准备用 {skillDefinition.DisplayName} 锁定 {targetCount} 个单位（评分 {scoreInput.total_score}）。"
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private static bool IsMultiUnitSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile != null
        && skillDefinition.CombatProfile.TargetSelectionModeKind
            == BattleTargetSelectionMode.MultiUnit;

    private static bool HasExplicitDistanceContract(UseMultiUnitSkillActionDefinition action)
    {
        return action.DesiredMinDistance >= 0
            && action.DesiredMaxDistance >= action.DesiredMinDistance
            && (
                action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit
                || action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
            );
    }

    private static bool IsChargeOption(CombatCastVariantDefinition castVariant)
    {
        if (castVariant == null)
            return false;
        foreach (CombatEffectDefinition effect in castVariant.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect != null && effect.EffectKind == BattleEffectKind.Charge)
                return true;
        }
        return false;
    }

    private static List<CombatCastVariantDefinition> GetMultiUnitCastVariants(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        var result = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return result;
        if (combatProfile.CastVariants.Count == 0)
        {
            result.Add(null);
            return result;
        }
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                result.Add(castVariant);
        }
        return result;
    }

    private List<List<BattleUnitState>> BuildTargetGroups(
        BattleAiContext context,
        UseMultiUnitSkillActionDefinition action,
        BattleAvailableSkillEntry skillEntry,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<BattleUnitState> sortedTargets
    )
    {
        var groups = new List<List<BattleUnitState>>();
        List<BattleUnitState> pool = BuildCandidatePool(
            context,
            action,
            skillEntry,
            skillDefinition,
            castVariant,
            sortedTargets
        );
        if (pool.Count == 0)
            return groups;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        int skillLevel = skillEntry?.SkillLevel ?? GetSkillLevel(context.unit_state, skillDefinition.SkillId);
        int minCount = Mathf.Max(combatProfile.MinTargetCount, 1);
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        int maxCount = Mathf.Max(effectiveDefinition.MaxTargetCount, minCount);
        bool usesOrderedTargetSlots =
            BattleTargetSlotCostRules.UsesOrderedTargetSlots(skillDefinition);
        if (!usesOrderedTargetSlots)
            maxCount = Mathf.Min(maxCount, pool.Count);
        if (!usesOrderedTargetSlots && pool.Count < minCount)
            return groups;

        bool deferGroupLimitUntilCanonicalPreview =
            HasLayeredBarrier(context) || usesOrderedTargetSlots;
        var seen = new HashSet<string>();
        if (usesOrderedTargetSlots)
        {
            AppendOrderedTargetSlotGroups(
                groups,
                seen,
                pool,
                minCount,
                maxCount
            );
            return groups;
        }
        IReadOnlyList<CombatEffectDefinition> castEffectDefinitions =
            _helper.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                skillEntry?.SkillLevel ?? skillLevel
            );
        if (IsSeparableTargetSelection(context, skillDefinition, castEffectDefinitions))
        {
            AppendTopKTargetGroups(
                groups,
                seen,
                context,
                skillEntry,
                skillDefinition,
                castVariant,
                pool,
                castEffectDefinitions,
                minCount,
                maxCount
            );
            return groups;
        }

        // Even when the caller defers the authored group limit to the canonical preview, full
        // combination enumeration is 2^pool; the pool limit is deferred on that same path, so an
        // absolute ceiling is the only thing standing between a layered-barrier field and a
        // combinatorial blow-up.
        int groupBudget = deferGroupLimitUntilCanonicalPreview
            ? MaxEnumeratedTargetGroups
            : Mathf.Min(action.CandidateGroupLimit, MaxEnumeratedTargetGroups);
        // Every target count gets a share of the budget, so a two-target combination stays
        // reachable even when the larger counts could exhaust the budget on their own.
        int remainingCounts = maxCount - minCount + 1;
        for (int count = maxCount; count >= minCount; count--, remainingCounts--)
        {
            if (groups.Count >= groupBudget)
                return groups;
            int countBudget = Mathf.Min(
                groups.Count + Mathf.Max((groupBudget - groups.Count) / Mathf.Max(remainingCounts, 1), 1),
                groupBudget
            );
            if (count == 1)
            {
                foreach (BattleUnitState target in pool)
                {
                    AppendTargetGroup(groups, seen, new List<BattleUnitState> { target });
                    if (groups.Count >= countBudget)
                        break;
                }
                continue;
            }
            AppendCombinationGroups(groups, seen, pool, count, countBudget);
        }
        return groups;
    }

    private static void AppendOrderedTargetSlotGroups(
        List<List<BattleUnitState>> groups,
        HashSet<string> seen,
        IReadOnlyList<BattleUnitState> pool,
        int minCount,
        int maxCount
    )
    {
        for (int count = maxCount; count >= minCount; count--)
        {
            foreach (BattleUnitState focusTarget in pool)
            {
                var focused = new List<BattleUnitState>();
                for (int slot = 0; slot < count; slot++)
                    focused.Add(focusTarget);
                AppendTargetGroup(groups, seen, focused);
            }

            int spreadStartCount = Math.Min(pool.Count, 3);
            for (int start = 0; start < spreadStartCount; start++)
            {
                var spread = new List<BattleUnitState>();
                for (int slot = 0; slot < count; slot++)
                    spread.Add(pool[(start + slot) % pool.Count]);
                AppendTargetGroup(groups, seen, spread);
            }
        }
    }

    private List<BattleUnitState> BuildCandidatePool(
        BattleAiContext context,
        UseMultiUnitSkillActionDefinition action,
        BattleAvailableSkillEntry skillEntry,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IEnumerable<BattleUnitState> sortedTargets
    )
    {
        var pool = new List<BattleUnitState>();
        int minCount = Mathf.Max(skillDefinition.CombatProfile.MinTargetCount, 1);
        bool deferPoolLimitUntilCanonicalPreview = minCount > 1 && HasLayeredBarrier(context);
        foreach (BattleUnitState target in sortedTargets ?? Array.Empty<BattleUnitState>())
        {
            if (
                !deferPoolLimitUntilCanonicalPreview
                && pool.Count >= action.CandidatePoolLimit
            )
                break;
            if (target == null)
                continue;
            if (minCount <= 1)
            {
                BattleCommand singleCommand = BuildMultiUnitSkillCommand(
                    context,
                    skillEntry,
                    castVariant,
                    new List<BattleUnitState> { target }
                );
                BattlePreview singlePreview = BuildFastUnitSkillPreview(
                    context,
                    skillDefinition,
                    singleCommand,
                    target
                );
                BattlePreview barrierAwarePreview = _helper.ResolveBarrierAwareUnitSkillPreview(
                    context,
                    singleCommand,
                    singlePreview,
                    skillEntry
                );
                if (
                    singlePreview?.allowed != true
                    || (
                        !BattleTargetSlotCostRules.UsesOrderedTargetSlots(
                            skillDefinition
                        )
                        && (
                            barrierAwarePreview?.allowed != true
                            || !barrierAwarePreview.ContainsTargetUnitId(target.unit_id)
                        )
                    )
                )
                {
                    continue;
                }
            }
            pool.Add(target);
        }
        return pool;
    }

    private static bool HasLayeredBarrier(BattleAiContext context) =>
        context?.state?.LayeredBarrierFieldCount > 0;

    /// <summary>
    /// Whether a group's value is the sum of its members' values. When it is, the best group of
    /// size k is simply the k highest-scoring targets, so enumerating combinations is wasted work
    /// and top-k is both cheaper and exactly optimal. Everything listed here breaks that
    /// assumption by making one target's outcome depend on which others share the cast; the
    /// predicate is deliberately conservative, since guessing separable wrongly silently degrades
    /// target choice while guessing non-separable only costs enumeration.
    /// </summary>
    private static bool IsSeparableTargetSelection(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        // Barrier layers absorb across the whole cast, so who gets through depends on the set.
        if (HasLayeredBarrier(context))
            return false;
        // Ordered slots repeat targets and bill per slot; they have their own enumeration path.
        if (BattleTargetSlotCostRules.UsesOrderedTargetSlots(skillDefinition))
            return false;
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        // Damage falls off by the target's index in the sequence.
        if (
            combatProfile.DirectionalPiercing != null
            || combatProfile.LineThroughAttack != null
            || combatProfile.SequentialLineHit != null
        )
        {
            return false;
        }
        // Special resolution profiles (meteor swarm and friends) resolve the set as a unit.
        if (combatProfile.SpecialResolutionProfileId != EmptyStringName)
            return false;
        if (effectDefinitions == null || effectDefinitions.Count == 0)
            return false;
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (effectDefinition == null)
                return false;
            // MaxAffectedTargets keeps only the lowest-HP N of the group, so adding a target can
            // push another out of the effect entirely.
            if (effectDefinition.MaxAffectedTargets > 0)
                return false;
            if (!SeparableEffectKinds.Contains(effectDefinition.EffectKind))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Top-k groups for a separable skill: rank each pool target by the payoff it contributes on
    /// its own, then take the k best for every legal k. Exactly optimal per size, and it builds
    /// one group per size instead of C(pool, k).
    /// </summary>
    private void AppendTopKTargetGroups(
        List<List<BattleUnitState>> groups,
        HashSet<string> seen,
        BattleAiContext context,
        BattleAvailableSkillEntry skillEntry,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<BattleUnitState> pool,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int minCount,
        int maxCount
    )
    {
        List<BattleUnitState> ranked = RankSeparableTargets(
            context,
            skillEntry,
            skillDefinition,
            castVariant,
            pool,
            effectDefinitions
        );
        for (int count = maxCount; count >= minCount; count--)
        {
            if (count > ranked.Count)
                continue;
            var targetGroup = new List<BattleUnitState>(count);
            for (int index = 0; index < count; index++)
                targetGroup.Add(ranked[index]);
            AppendTargetGroup(groups, seen, targetGroup);
        }
    }

    /// <summary>
    /// Pool targets ordered by their standalone payoff, best first. The single-target score input
    /// is a ranking probe, never a command we issue, so it is fine to build one even for skills
    /// whose min_target_count is above one. Ties keep the incoming pool order, which is the
    /// action's own target selector ordering.
    /// </summary>
    private List<BattleUnitState> RankSeparableTargets(
        BattleAiContext context,
        BattleAvailableSkillEntry skillEntry,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<BattleUnitState> pool,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        var scored = new List<(BattleUnitState Target, int Payoff, int PoolIndex)>(pool.Count);
        for (int poolIndex = 0; poolIndex < pool.Count; poolIndex++)
        {
            BattleUnitState target = pool[poolIndex];
            var singleGroup = new List<BattleUnitState> { target };
            BattleCommand probeCommand = BuildMultiUnitSkillCommand(
                context,
                skillEntry,
                castVariant,
                singleGroup
            );
            BattlePreview probePreview = BuildFastUnitSkillPreview(
                context,
                skillDefinition,
                probeCommand
            );
            // int.MinValue means the preview says this target lands nothing, which is the only
            // reason to drop it. A missing score input means scoring is unavailable, not that the
            // target is useless, so those rank neutrally and keep the pool's selector order.
            int payoff = int.MinValue;
            if (probePreview?.allowed == true && probePreview.TargetUnitIdsTyped.Count > 0)
            {
                payoff = 0;
                // Straight to the score service: the ranking probe only reads hit_payoff_score,
                // which does not depend on the action's bucket or intent metadata.
                BattleAiScoreInput probeScore = context.BuildSkillScoreInputTyped(
                    skillDefinition,
                    probeCommand,
                    probePreview,
                    effectDefinitions,
                    null
                );
                // hit_payoff_score is the part of total_score that varies per target; the rest is
                // either constant for the cast or a function of slot count alone.
                if (probeScore != null)
                    payoff = probeScore.hit_payoff_score;
            }
            scored.Add((target, payoff, poolIndex));
        }
        scored.Sort(
            (left, right) =>
            {
                if (left.Payoff != right.Payoff)
                    return right.Payoff.CompareTo(left.Payoff);
                return left.PoolIndex.CompareTo(right.PoolIndex);
            }
        );
        var ranked = new List<BattleUnitState>(scored.Count);
        foreach ((BattleUnitState target, int payoff, int _) in scored)
        {
            // A target that previews as landing nothing can only ever pad the group.
            if (payoff == int.MinValue)
                continue;
            ranked.Add(target);
        }
        return ranked;
    }

    /// <summary>
    /// Index combinations of <paramref name="count"/> targets in lexicographic order. The pool is
    /// already sorted by target priority, so the first combinations are the highest-priority ones
    /// and truncation degrades gracefully. Contiguous windows used to make pairs like
    /// (top target, third target) unreachable no matter how well they scored.
    /// </summary>
    private static void AppendCombinationGroups(
        List<List<BattleUnitState>> groups,
        HashSet<string> seen,
        IReadOnlyList<BattleUnitState> pool,
        int count,
        int countBudget
    )
    {
        if (count > pool.Count)
            return;
        var indices = new int[count];
        for (int slot = 0; slot < count; slot++)
            indices[slot] = slot;
        while (true)
        {
            var targetGroup = new List<BattleUnitState>(count);
            for (int slot = 0; slot < count; slot++)
                targetGroup.Add(pool[indices[slot]]);
            AppendTargetGroup(groups, seen, targetGroup);
            if (groups.Count >= countBudget)
                return;

            int advanceSlot = count - 1;
            while (advanceSlot >= 0 && indices[advanceSlot] == pool.Count - count + advanceSlot)
                advanceSlot--;
            if (advanceSlot < 0)
                return;
            indices[advanceSlot]++;
            for (int slot = advanceSlot + 1; slot < count; slot++)
                indices[slot] = indices[slot - 1] + 1;
        }
    }

    private static void AppendTargetGroup(
        List<List<BattleUnitState>> groups,
        HashSet<string> seen,
        List<BattleUnitState> targetGroup
    )
    {
        if (targetGroup.Count == 0)
            return;
        string key = TargetGroupKey(targetGroup);
        if (key.Length == 0 || !seen.Add(key))
            return;
        groups.Add(targetGroup);
    }

    private static string TargetGroupKey(IReadOnlyList<BattleUnitState> targetGroup)
    {
        var parts = new List<string>();
        foreach (BattleUnitState target in targetGroup)
        {
            if (target != null)
                parts.Add(target.unit_id.ToString());
        }
        return string.Join("|", parts);
    }

    private static BattleCommand BuildMultiUnitSkillCommand(
        BattleAiContext context,
        BattleAvailableSkillEntry skillEntry,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<BattleUnitState> targetGroup
    )
    {
        if (context?.unit_state == null || skillEntry == null)
            return null;
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_entry_id = skillEntry.EntryRef.SkillEntryId,
            skill_id = skillEntry.EntryRef.SkillId,
            skill_variant_id = castVariant?.VariantId ?? EmptyStringName,
        };
        foreach (BattleUnitState target in targetGroup ?? Array.Empty<BattleUnitState>())
        {
            if (target == null)
                continue;
            command.AddTargetUnitId(target.unit_id);
            if (command.target_coord == new Vector2I(-1, -1))
                command.target_coord = target.GetAnchorCoord();
        }
        return command;
    }

    private static BattlePreview BuildFastUnitSkillPreview(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitState targetUnit = null
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleState state = context?.state;
        if (actor == null || state == null || skillDefinition?.CombatProfile == null || command == null)
            return preview;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        var targetIds = new List<StringName>();
        if (BattleTargetSlotCostRules.UsesOrderedTargetSlots(skillDefinition))
        {
            foreach (StringName id in command.TargetUnitIdsTyped)
            {
                StringName normalized = ProgressionDataUtils.to_string_name(id);
                if (normalized != "")
                    targetIds.Add(normalized);
            }
            if (targetIds.Count == 0)
            {
                StringName fallbackTargetId = ProgressionDataUtils.to_string_name(
                    command.target_unit_id != ""
                        ? command.target_unit_id
                        : targetUnit?.unit_id ?? ""
                );
                if (fallbackTargetId != "")
                    targetIds.Add(fallbackTargetId);
            }
        }
        else
        {
            AddUniqueTargetId(targetIds, targetUnit?.unit_id ?? "");
            AddUniqueTargetId(targetIds, command.target_unit_id);
            foreach (StringName id in command.TargetUnitIdsTyped)
                AddUniqueTargetId(targetIds, id);
        }
        if (targetIds.Count == 0)
            return preview;

        bool isMultiTarget =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit;
        if (!isMultiTarget && targetIds.Count != 1)
            return preview;

        int skillLevel = GetSkillLevel(actor, skillDefinition.SkillId);
        SkillEffectiveCombatDefinition effectiveDefinition =
            context.skill_catalog?.GetEffectiveCombatDefinition(
                skillDefinition.SkillId,
                skillLevel
            ) ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        CombatSkillResourceCosts selectedCosts =
            effectiveDefinition.GetResourceCostsForTargetSlots(targetIds.Count);
        if (
            actor.GetCurrentAp() < selectedCosts.ApCost
            || actor.GetCurrentMp() < selectedCosts.MpCost
            || actor.GetCurrentStamina() < selectedCosts.StaminaCost
            || actor.GetCurrentAura() < selectedCosts.AuraCost
        )
        {
            return preview;
        }

        foreach (StringName targetId in targetIds)
        {
            BattleUnitState candidate = state.TryGetUnitTyped(targetId, out BattleUnitState found)
                ? found
                : null;
            if (
                candidate == null
                || !candidate.IsAlive()
                || !MatchesTargetFilter(context, candidate, combatProfile.TargetTeamFilter)
                || !IsFastUnitSkillTargetInRange(context, actor, candidate, skillDefinition)
            )
            {
                return preview;
            }
            preview.AddTargetUnitId(candidate.unit_id);
            foreach (Vector2I coord in candidate.GetOccupiedCoordsReadViewTyped())
            {
                if (!preview.ContainsTargetCoord(coord))
                    preview.AddTargetCoord(coord);
            }
        }

        preview.allowed = preview.TargetUnitIdsTyped.Count > 0;
        preview.resolved_anchor_coord =
            preview.TargetCoordsTyped.Count > 0
                ? preview.TargetCoordsTyped[0]
                : new Vector2I(-1, -1);
        return preview;
    }

    private static List<BattleUnitState> ResolvePreviewTargetGroup(
        IReadOnlyList<BattleUnitState> targetGroup,
        BattlePreview preview
    )
    {
        var effectiveTargetIds = new HashSet<StringName>(
            preview?.TargetUnitIdsTyped ?? Array.Empty<StringName>()
        );
        var result = new List<BattleUnitState>();
        foreach (BattleUnitState target in targetGroup ?? Array.Empty<BattleUnitState>())
        {
            if (target != null && effectiveTargetIds.Contains(target.unit_id))
                result.Add(target);
        }
        return result;
    }

    private Dictionary<string, object> BuildPositionMetadata(
        BattleAiContext context,
        UseMultiUnitSkillActionDefinition action,
        IReadOnlyList<BattleUnitState> targetGroup,
        SkillDefinition skillDefinition
    )
    {
        Dictionary<string, object> metadata = ResolveDesiredDistanceContract(
            context,
            action,
            skillDefinition
        );
        if (action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit)
        {
            BattleUnitState primaryTarget = targetGroup.Count > 0 ? targetGroup[0] : null;
            if (primaryTarget != null)
                metadata["position_target_unit_id"] = primaryTarget.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else if (action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontline = ResolveEnemyFrontlineUnit(context);
            if (frontline != null)
                metadata["position_target_unit_id"] = frontline.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private static Dictionary<string, object> ResolveDesiredDistanceContract(
        BattleAiContext context,
        UseMultiUnitSkillActionDefinition action,
        SkillDefinition skillDefinition
    )
    {
        int configuredMinDistance = action?.DesiredMinDistance ?? 0;
        int configuredMaxDistance = action?.DesiredMaxDistance ?? 0;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDefinition);
        int resolvedMaxDistance =
            effectiveAttackRange >= 0 ? effectiveAttackRange : configuredMaxDistance;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMinDistance,
            ["desired_max_distance"] = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ["configured_desired_min_distance"] = configuredMinDistance,
            ["configured_desired_max_distance"] = configuredMaxDistance,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    private BattleUnitState ResolveEnemyFrontlineUnit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _helper.SortTargetUnits(context, "enemy", "nearest_enemy");
        return targets.Count > 0 ? targets[0] : null;
    }

    private static int ResolveEffectiveAttackRange(BattleAiContext context, SkillDefinition skillDefinition)
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || skillDefinition == null)
            return -1;
        return BattleRangeService.GetEffectiveSkillDistanceContractRange(
            actor,
            skillDefinition,
            context.skill_catalog
        );
    }

    private static List<CombatEffectDefinition> CollectMultiUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var result = new List<CombatEffectDefinition>();
        if (skillDefinition?.CombatProfile != null)
        {
            foreach (CombatEffectDefinition effect in skillDefinition.CombatProfile.EffectDefinitions)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }
        if (castVariant != null)
        {
            foreach (CombatEffectDefinition effect in castVariant.EffectDefinitions)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }
        return result;
    }

    private static BattleAiScoreInput BuildSkillScoreInput(
        UseMultiUnitSkillActionDefinition action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        if (context == null || skillDefinition == null)
            return null;

        Dictionary<string, object> scoringMetadata = CloneMetadata(metadata);
        scoringMetadata["score_bucket_id"] = action.ScoreBucketId;
        scoringMetadata["action_kind"] = ReadMetadataStringName(
            scoringMetadata,
            "action_kind",
            new StringName("skill")
        );
        StringName defaultActionIntent =
            BattleAiActionIntent.IsValid(action.ActionIntent)
            && action.ActionIntent != BattleAiActionIntent.Positioning
                ? action.ActionIntent
                : BattleAiActionIntent.InferForSkill(skillDefinition, effectDefinitions);
        scoringMetadata["action_intent"] = ReadMetadataStringName(
            scoringMetadata,
            "action_intent",
            defaultActionIntent
        );
        scoringMetadata["action_label"] = ReadMetadataString(
            scoringMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDefinition.DisplayName)
                ? skillDefinition.DisplayName
                : action.ActionId.ToString()
        );
        scoringMetadata = context.MergeCurrentActionMetadataTyped(scoringMetadata);
        scoringMetadata["score_bucket_id"] = ReadMetadataStringName(
            scoringMetadata,
            "score_bucket_id",
            action.ScoreBucketId
        );
        return context.BuildSkillScoreInputTyped(
            skillDefinition,
            command,
            preview,
            effectDefinitions,
            scoringMetadata
        );
    }

    private static bool MatchesTargetFilter(
        BattleAiContext context,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || targetUnit == null)
            return false;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            actor,
            targetUnit,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
            )
        );
    }

    private static bool IsFastUnitSkillTargetInRange(
        BattleAiContext context,
        BattleUnitState actor,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        if (context?.grid_service == null || actor == null || targetUnit == null || skillDefinition == null)
            return false;
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
            actor,
            skillDefinition,
            context.skill_catalog
        );
        if (context.grid_service.GetDistanceBetweenUnits(actor, targetUnit) > effectiveRange)
            return false;
        return true;
    }

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return 0;
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.KnowsActiveSkill(skillId)
                ? 1
                : 0;
    }

    private static void AddUniqueTargetId(List<StringName> targetIds, StringName unitId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        if (targetIds == null || normalized == "" || targetIds.Contains(normalized))
            return;
        targetIds.Add(normalized);
    }

    private static AiActionTrace BeginActionTrace(
        UseMultiUnitSkillActionDefinition action,
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        Dictionary<string, object> traceMetadata =
            context != null ? context.MergeCurrentActionMetadataTyped(metadata) : CloneMetadata(metadata);
        StringName scoreBucketId = ReadMetadataStringName(
            traceMetadata,
            "score_bucket_id",
            action?.ScoreBucketId ?? EmptyStringName
        );
        return EnemyAiActionHelper.BeginActionTrace(
            action?.ActionId ?? EmptyStringName,
            scoreBucketId,
            context,
            traceMetadata
        );
    }

    private static void TraceCountIncrement(
        AiActionTrace actionTrace,
        string key,
        int amount = 1
    ) => EnemyAiActionHelper.TraceCountIncrement(actionTrace, key, amount);

    private static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey) =>
        EnemyAiActionHelper.TraceAddBlockReason(actionTrace, reasonKey);

    private static void TraceOfferCandidate(
        AiActionTrace actionTrace,
        AiCandidateSummary candidateSummary
    ) => EnemyAiActionHelper.TraceOfferCandidate(actionTrace, candidateSummary, 5);

    private static Dictionary<string, object> CloneMetadata(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static StringName ReadMetadataStringName(
        IReadOnlyDictionary<string, object> source,
        string key,
        StringName fallback = default
    )
    {
        if (
            source == null
            || string.IsNullOrEmpty(key)
            || !source.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }
        return value switch
        {
            StringName stringName => stringName,
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String =>
                new StringName(variant.AsString()),
            _ => fallback,
        };
    }

    private static string ReadMetadataString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        if (
            source == null
            || string.IsNullOrEmpty(key)
            || !source.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }
        return value switch
        {
            string text => text,
            StringName stringName => stringName.ToString(),
            Variant variant when variant.VariantType == Variant.Type.String => variant.AsString(),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName().ToString(),
            _ => value.ToString() ?? fallback,
        };
    }
}
