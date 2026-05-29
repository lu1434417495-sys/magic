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

    private sealed class CandidateTraceSummary
    {
        public GDictionary Payload = new();
        public int TotalScore;

        public GDictionary ToDictionary() => Payload.Duplicate(true);

        public static CandidateTraceSummary Create(
            string label,
            BattleCommand command,
            BattleAiScoreInput scoreInput,
            GDictionary extra
        )
        {
            var payload = new GDictionary
            {
                ["label"] = label,
                ["command"] = BuildCommandSummary(command),
                ["total_score"] = scoreInput?.total_score ?? DictInt(extra, "total_score"),
                ["score_input"] = scoreInput != null ? scoreInput.to_dict() : new GDictionary(),
            };
            foreach (CandidateTraceExtraField field in ReadCandidateTraceExtraFields(extra))
            {
                payload[field.Key] = field.Value;
            }
            return new CandidateTraceSummary
            {
                Payload = payload,
                TotalScore = GdInterop.GetInt(payload, "total_score", -999999),
            };
        }

        public static CandidateTraceSummary FromDictionary(GDictionary source)
        {
            source ??= new GDictionary();
            return new CandidateTraceSummary
            {
                Payload = source.Duplicate(true),
                TotalScore = GdInterop.GetInt(source, "total_score", -999999),
            };
        }
    }

    private readonly struct CandidateTraceExtraField
    {
        public CandidateTraceExtraField(string key, Variant value)
        {
            Key = key ?? "";
            Value = value;
        }

        public string Key { get; }

        public Variant Value { get; }
    }

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    public BattleAiDecision evaluate(UseUnitSkillAction action, BattleAiContext context)
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
        GDictionary actionTrace = BeginActionTrace(
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
                    if (!_scoreOrdering.is_better_score_input(scoreInput, bestScoreInput))
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
            !BattleTargetTeamRules.is_unit_valid_for_filter(
                actor,
                target,
                combatProfile.target_team_filter,
                new BattleTargetTeamRules.TargetFilterOptions(
                    MadnessTargetAnyTeam: GdInterop.GetBool(
                        actor.ai_blackboard,
                        "madness_target_any_team"
                    )
                )
            )
        )
        {
            return preview;
        }
        int effectiveRange = BattleRangeService.get_effective_skill_range(actor, skillDef);
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

    private static GDictionary BeginActionTrace(
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
        return new GDictionary
        {
            ["trace_id"] = traceId,
            ["action_id"] = actionId.ToString(),
            ["score_bucket_id"] = scoreBucketId.ToString(),
            ["metadata"] = traceMetadata,
            ["evaluation_count"] = 0,
            ["blocked_count"] = 0,
            ["preview_reject_count"] = 0,
            ["candidate_count"] = 0,
            ["block_reasons"] = new GDictionary(),
            ["top_candidates"] = traceEnabled ? new GArray() : new GArray(),
            ["chosen"] = false,
        };
    }

    private static void TraceCountIncrement(GDictionary actionTrace, string key, int amount = 1)
    {
        if (actionTrace == null || actionTrace.Count == 0 || string.IsNullOrEmpty(key))
            return;
        actionTrace[key] = DictInt(actionTrace, key) + amount;
    }

    private static void TraceAddBlockReason(GDictionary actionTrace, string reasonKey)
    {
        if (actionTrace == null || actionTrace.Count == 0 || string.IsNullOrEmpty(reasonKey))
            return;
        TraceCountIncrement(actionTrace, "blocked_count", 1);
        GDictionary blockReasons = DictDictionary(actionTrace, "block_reasons");
        blockReasons[reasonKey] = DictInt(blockReasons, reasonKey) + 1;
        actionTrace["block_reasons"] = blockReasons;
    }

    private static void OfferCandidate(
        GDictionary actionTrace,
        bool traceEnabled,
        string optionLabel,
        BattleUnitState target,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        GDictionary candidateExtra
    )
    {
        if (actionTrace == null || actionTrace.Count == 0)
            return;
        TraceCountIncrement(actionTrace, "candidate_count", 1);
        if (!traceEnabled)
            return;

        CandidateTraceSummary candidateSummary = CandidateTraceSummary.Create(
            $"{optionLabel}->{target?.display_name ?? ""}",
            command,
            scoreInput,
            candidateExtra
        );
        GArray topCandidates = DictArray(actionTrace, "top_candidates");
        topCandidates.Add(candidateSummary.ToDictionary());
        List<CandidateTraceSummary> sorted = ReadCandidateSummaries(topCandidates);
        sorted.Sort((left, right) => right.TotalScore.CompareTo(left.TotalScore));
        var trimmed = new GArray();
        for (int i = 0; i < Math.Min(sorted.Count, 5); i++)
            trimmed.Add(sorted[i].ToDictionary());
        actionTrace["top_candidates"] = trimmed;
    }

    private static List<CandidateTraceSummary> ReadCandidateSummaries(GArray topCandidates)
    {
        var result = new List<CandidateTraceSummary>();
        foreach (var candidateValue in topCandidates ?? new GArray())
        {
            if (TryAsDictionary(candidateValue, out GDictionary candidate))
                result.Add(CandidateTraceSummary.FromDictionary(candidate));
        }
        return result;
    }

    private static List<CandidateTraceExtraField> ReadCandidateTraceExtraFields(GDictionary extra)
    {
        var result = new List<CandidateTraceExtraField>();
        if (extra == null)
        {
            return result;
        }
        foreach (var rawKey in extra.Keys)
        {
            string key = ReadDictionaryKey(rawKey);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            result.Add(new CandidateTraceExtraField(key, GetValue(extra, key)));
        }
        return result;
    }

    private static string ReadDictionaryKey(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.String => rawKey.AsString(),
            Variant.Type.StringName => rawKey.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => rawKey.ToString(),
        };
    }

    private static GDictionary BuildCommandSummary(BattleCommand command)
    {
        if (command == null)
            return new GDictionary();
        return new GDictionary
        {
            ["command_type"] = command.command_type.ToString(),
            ["unit_id"] = command.unit_id.ToString(),
            ["skill_id"] = command.skill_id.ToString(),
            ["skill_variant_id"] = command.skill_variant_id.ToString(),
            ["target_unit_id"] = command.target_unit_id.ToString(),
            ["target_unit_ids"] = command.target_unit_ids.Duplicate(),
            ["target_coord"] = command.target_coord,
            ["target_coords"] = command.target_coords.Duplicate(),
        };
    }

    private static void FinalizeActionTrace(
        BattleAiContext context,
        GDictionary actionTrace,
        BattleAiDecision bestDecision,
        bool traceEnabled
    )
    {
        if (actionTrace == null || actionTrace.Count == 0)
            return;
        StringName traceId = DictStringName(actionTrace, "trace_id");
        if (bestDecision != null)
            bestDecision.action_trace_id = traceId;
        if (!traceEnabled)
            return;

        if (bestDecision != null)
        {
            actionTrace["best_reason_text"] = bestDecision.reason_text;
            actionTrace["best_command"] = BuildCommandSummary(bestDecision.command);
            BattleAiScoreInput scoreInput =
                bestDecision.score_input ?? bestDecision.skill_score_input;
            actionTrace["best_score_input"] =
                scoreInput != null ? scoreInput.to_dict() : new GDictionary();
        }
        context?.record_action_trace(actionTrace);
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null)
            return new GArray();
        return TryAsArray(GetValue(dictionary, key), out GArray value) ? value : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        if (dictionary == null)
            return new GDictionary();
        return TryAsDictionary(GetValue(dictionary, key), out GDictionary value)
            ? value
            : new GDictionary();
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        string key,
        StringName fallback = default
    )
    {
        return TryAsStringName(GetValue(dictionary, key), out StringName value)
            ? value
            : fallback ?? EmptyStringName;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback = "")
    {
        var value = GetValue(dictionary, key);
        return IsNil(value) ? fallback : value.ToString();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        return TryAsInt(GetValue(dictionary, key), out int value) ? value : fallback;
    }

    private static Variant GetValue(GDictionary dictionary, string key)
    {
        if (dictionary == null)
            return default;
        if (dictionary.ContainsKey(key))
            return dictionary[key];
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey) ? dictionary[stringNameKey] : default;
    }

    private static bool TryAsStringName(Variant rawValue, out StringName value)
    {
        if (rawValue.VariantType == Variant.Type.StringName)
        {
            value = rawValue.AsStringName();
            return true;
        }
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = new StringName(rawValue.AsString());
            return true;
        }
        value = EmptyStringName;
        return false;
    }

    private static bool TryAsInt(Variant rawValue, out int value)
    {
        if (rawValue.VariantType == Variant.Type.Nil)
        {
            value = 0;
            return false;
        }
        value = rawValue.AsInt32();
        return true;
    }

    private static bool TryAsArray(Variant rawValue, out GArray value)
    {
        if (rawValue.VariantType == Variant.Type.Array)
        {
            value = rawValue.AsGodotArray();
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsDictionary(Variant rawValue, out GDictionary value)
    {
        if (rawValue.VariantType == Variant.Type.Dictionary)
        {
            value = rawValue.AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool IsNil(Variant rawValue)
    {
        return rawValue.VariantType == Variant.Type.Nil;
    }
}
