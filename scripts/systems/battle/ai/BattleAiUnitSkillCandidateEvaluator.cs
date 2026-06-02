using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleAiUnitSkillCandidateEvaluator
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName TargetModeUnit = "unit";
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    private enum DistanceReference
    {
        None,
        TargetUnit,
        EnemyFrontline,
    }

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    public BattleAiDecision Evaluate(UseUnitSkillAction action, BattleAiContext context)
    {
        if (action == null || context == null)
            return null;

        StringName distanceReference = action.distance_reference;
        int desiredMinDistance = action.desired_min_distance;
        int desiredMaxDistance = action.desired_max_distance;
        if (!HasExplicitDistanceContract(distanceReference, desiredMinDistance, desiredMaxDistance))
            return null;

        BattleUnitState actor = context.unit_state;
        if (actor == null || context.state == null)
            return null;

        string actorDisplayName = actor.display_name;
        bool traceEnabled = context.trace_enabled;
        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            traceEnabled,
            new GDictionary
            {
                ["action_kind"] = "unit_skill",
                ["target_selector"] = action.target_selector.ToString(),
                ["minimum_effective_target_count"] = action.minimum_effective_target_count,
                ["maximum_friendly_fire_target_count"] =
                    action.maximum_friendly_fire_target_count,
                ["allow_friendly_lethal"] = action.allow_friendly_lethal,
                ["distance_reference"] = distanceReference.ToString(),
                ["desired_min_distance"] = desiredMinDistance,
                ["desired_max_distance"] = desiredMaxDistance,
            }
        );

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        List<StringName> knownSkillIds = _helper.ResolveKnownSkillIds(context, action.skill_ids);

        foreach (StringName skillId in knownSkillIds)
        {
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDef skillDef = _helper.GetSkillDef(context, skillId);
            CombatSkillDef combatProfile = skillDef?.combat_profile;
            if (skillDef == null || combatProfile == null)
            {
                TraceAddBlockReason(actionTrace, "missing_skill_def");
                continue;
            }
            if (combatProfile.target_mode != TargetModeUnit)
            {
                TraceAddBlockReason(actionTrace, "non_unit_skill");
                continue;
            }

            string blockReason = _helper.GetSkillCastBlockReason(context, skillDef);
            if (!string.IsNullOrEmpty(blockReason))
            {
                TraceAddBlockReason(actionTrace, blockReason);
                continue;
            }

            List<BattleUnitState> targets = _helper.SortTargetUnits(
                context,
                combatProfile.target_team_filter,
                action.target_selector
            );
            if (targets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            List<CombatCastVariantDef> castVariants = _helper.GetUnitCastVariants(
                context,
                skillDef
            );
            if (castVariants.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_unlocked_unit_options");
                continue;
            }

            foreach (CombatCastVariantDef castVariant in castVariants)
            {
                StringName optionId = castVariant?.variant_id ?? EmptyStringName;
                string optionLabel = FormatSkillVariantLabel(skillDef, castVariant);

                foreach (BattleUnitState target in targets)
                {
                    if (target == null)
                        continue;
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    BattleCommand command = _helper.BuildUnitSkillCommand(
                        context,
                        skillId,
                        target,
                        optionId
                    );
                    BattlePreview preview = BuildFastUnitSkillPreview(
                        context,
                        skillDef,
                        combatProfile,
                        command,
                        target
                    );
                    if (preview == null || !preview.allowed)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }

                    GDictionary positionMetadata = _helper.BuildPositionMetadata(
                        action,
                        context,
                        target,
                        skillDef
                    );
                    positionMetadata["action_label"] = optionLabel;
                    List<CombatEffectDef> effectDefs = _helper.CollectUnitSkillEffectDefs(
                        skillDef,
                        castVariant,
                        actor
                    );
                    BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                        action,
                        context,
                        skillDef,
                        command,
                        preview,
                        effectDefs,
                        positionMetadata
                    );
                    GDictionary candidateExtra = BuildCandidateExtra(
                        skillId,
                        optionId,
                        skillDef,
                        castVariant,
                        target
                    );

                    if (scoreInput == null)
                    {
                        fallbackDecision ??= _helper.CreateDecision(
                            action,
                            command,
                            $"{actorDisplayName} 选择对 {target.display_name} 使用 {optionLabel}。"
                        );
                        OfferCandidate(
                            actionTrace,
                            traceEnabled,
                            optionLabel,
                            target,
                            command,
                            null,
                            candidateExtra
                        );
                        continue;
                    }

                    if (scoreInput.effective_target_count < action.minimum_effective_target_count)
                    {
                        TraceAddBlockReason(actionTrace, "minimum_effective_target_count");
                        continue;
                    }
                    if (!PassesFriendlyFireLimits(action, scoreInput))
                    {
                        TraceAddBlockReason(actionTrace, "friendly_fire_limit");
                        continue;
                    }

                    OfferCandidate(
                        actionTrace,
                        traceEnabled,
                        optionLabel,
                        target,
                        command,
                        scoreInput,
                        candidateExtra
                    );
                    if (!_scoreOrdering.IsBetterScoreInput(scoreInput, bestScoreInput))
                        continue;

                    bestScoreInput = scoreInput;
                    bestDecision = _helper.CreateScoredDecision(
                        action,
                        command,
                        scoreInput,
                        $"{actorDisplayName} 选择对 {target.display_name} 使用 {optionLabel}（评分 {scoreInput.total_score}）。"
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        FinalizeActionTrace(context, actionTrace, resolvedDecision, traceEnabled);
        return resolvedDecision;
    }

    private static bool HasExplicitDistanceContract(
        StringName distanceReference,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        return desiredMinDistance >= 0
            && desiredMaxDistance >= desiredMinDistance
            && ParseDistanceReference(distanceReference) != DistanceReference.None;
    }

    private static DistanceReference ParseDistanceReference(StringName distanceReference)
    {
        return distanceReference.ToString() switch
        {
            "target_unit" => DistanceReference.TargetUnit,
            "enemy_frontline" => DistanceReference.EnemyFrontline,
            _ => DistanceReference.None,
        };
    }

    private static string FormatSkillVariantLabel(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (skillDef == null)
            return "";
        string optionName = castVariant?.display_name ?? "";
        return string.IsNullOrEmpty(optionName)
            ? skillDef.display_name
            : $"{skillDef.display_name}·{optionName}";
    }

    private static BattleAiScoreInput BuildSkillScoreInput(
        UseUnitSkillAction action,
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDef> effectDefs,
        GDictionary metadata
    )
    {
        if (context == null)
            return null;

        GDictionary scoringMetadata = metadata?.Duplicate(true) ?? new GDictionary();
        scoringMetadata["score_bucket_id"] = action.score_bucket_id;
        scoringMetadata["action_kind"] = DictStringName(
            scoringMetadata,
            "action_kind",
            new StringName("skill")
        );
        scoringMetadata["action_label"] = DictString(
            scoringMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDef?.display_name)
                ? skillDef.display_name
                : action.action_id.ToString()
        );
        scoringMetadata = context.merge_current_action_metadata(scoringMetadata);
        scoringMetadata["score_bucket_id"] = DictStringName(
            scoringMetadata,
            "score_bucket_id",
            action.score_bucket_id
        );
        return context.build_skill_score_input(
            skillDef,
            command,
            preview,
            ToEffectArray(effectDefs),
            scoringMetadata
        );
    }

    private static GArray ToEffectArray(IReadOnlyList<CombatEffectDef> effectDefs)
    {
        var result = new GArray();
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null)
                result.Add(effectDef);
        }
        return result;
    }

    private GDictionary BuildCandidateExtra(
        StringName skillId,
        StringName optionId,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState target
    )
    {
        return new GDictionary
        {
            ["skill_id"] = skillId.ToString(),
            ["skill_variant_id"] = optionId.ToString(),
            ["skill_variant_target_mode"] = _helper
                .GetCastVariantTargetMode(skillDef, castVariant)
                .ToString(),
            ["target_unit_id"] = target?.unit_id.ToString() ?? "",
        };
    }

    private static bool PassesFriendlyFireLimits(
        UseUnitSkillAction action,
        BattleAiScoreInput scoreInput
    )
    {
        if (scoreInput == null || action == null)
            return false;
        if (
            scoreInput.estimated_friendly_fire_target_count
            > action.maximum_friendly_fire_target_count
        )
        {
            return false;
        }
        return action.allow_friendly_lethal
            || scoreInput.estimated_friendly_lethal_target_count <= 0;
    }

    private static BattlePreview BuildFastUnitSkillPreview(
        BattleAiContext context,
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        BattleCommand command,
        BattleUnitState target
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleGridService grid = context?.grid_service;
        if (
            actor == null
            || grid == null
            || skillDef == null
            || combatProfile == null
            || command == null
            || target == null
            || !target.is_alive
        )
        {
            return preview;
        }
        if (
            !BattleTargetTeamRules.IsUnitValidForFilter(
                actor,
                target,
                combatProfile.target_team_filter,
                new BattleTargetTeamRules.TargetFilterOptions(
                    MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
                )
            )
        )
        {
            return preview;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(actor, skillDef);
        if (grid.get_distance_between_units(actor, target) > effectiveRange)
        {
            return preview;
        }

        preview.allowed = true;
        preview.target_unit_ids.Add(target.unit_id);
        target.refresh_footprint();
        foreach (Vector2I coord in target.occupied_coords)
        {
            if (!preview.target_coords.Contains(coord))
            {
                preview.target_coords.Add(coord);
            }
        }
        preview.resolved_anchor_coord =
            preview.target_coords.Count > 0 ? preview.target_coords[0] : new Vector2I(-1, -1);
        return preview;
    }

    private static AiActionTrace BeginActionTrace(
        UseUnitSkillAction action,
        BattleAiContext context,
        bool traceEnabled,
        GDictionary metadata
    )
    {
        GDictionary traceMetadata = metadata?.Duplicate(true) ?? new GDictionary();
        if (context != null)
            traceMetadata = context.merge_current_action_metadata(traceMetadata);
        StringName scoreBucketId = DictStringName(
            traceMetadata,
            "score_bucket_id",
            action?.score_bucket_id ?? EmptyStringName
        );
        StringName actionId = action?.action_id ?? EmptyStringName;
        StringName traceId = context != null ? context.next_action_trace_id(actionId) : actionId;
        return new AiActionTrace(
            traceId,
            actionId.ToString(),
            scoreBucketId.ToString(),
            TraceDictionaryProjection.FromDictionary(traceMetadata)
        );
    }

    private static void TraceCountIncrement(AiActionTrace actionTrace, string key, int amount = 1)
    {
        actionTrace?.Increment(key, amount);
    }

    private static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey)
    {
        actionTrace?.AddBlockReason(reasonKey);
    }

    private static void OfferCandidate(
        AiActionTrace actionTrace,
        bool traceEnabled,
        string optionLabel,
        BattleUnitState target,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        GDictionary candidateExtra
    )
    {
        if (actionTrace == null)
            return;
        if (!traceEnabled)
        {
            actionTrace.Increment("candidate_count", 1);
            return;
        }

        AiCandidateSummary candidateSummary = AiCandidateSummary.Create(
            $"{optionLabel}->{target?.display_name ?? ""}",
            command,
            scoreInput,
            TraceDictionaryProjection.FromDictionary(candidateExtra)
        );
        actionTrace.OfferCandidate(candidateSummary, 5);
    }

    private static void FinalizeActionTrace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision,
        bool traceEnabled
    )
    {
        if (actionTrace == null || actionTrace.IsEmpty())
            return;
        if (bestDecision != null)
            bestDecision.action_trace_id = actionTrace.TraceId;
        if (!traceEnabled)
            return;

        actionTrace.ApplyBestDecision(bestDecision);
        context?.RecordActionTrace(actionTrace);
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return new GArray();
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotArray();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].AsGodotArray()
            : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return new GDictionary();
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotDictionary();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].AsGodotDictionary()
            : new GDictionary();
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        string key,
        StringName fallback = default
    )
    {
        string text = DictString(dictionary, key);
        return !string.IsNullOrEmpty(text)
            ? new StringName(text)
            : fallback ?? EmptyStringName;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].ToString();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].ToString()
            : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsInt32();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].AsInt32()
            : fallback;
    }
}
