using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleSimOverrideApplier
{
    internal BattleSimOverrideApplyResult ApplyProfileTyped(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
        BattleSimProfileDefinition profile
    )
    {
        var patchedSkills = skillDefinitions == null
            ? new Dictionary<StringName, SkillDefinition>()
            : new Dictionary<StringName, SkillDefinition>(skillDefinitions);
        var patchedBrains = enemyAiBrains == null
            ? new Dictionary<StringName, EnemyAiBrainDefinition>()
            : new Dictionary<StringName, EnemyAiBrainDefinition>(enemyAiBrains);
        BattleAiScoreProfileDefinition scoreProfile =
            profile?.AiScoreProfile ?? BattleAiScoreProfileDefinition.Default;
        var factionProfiles = new Dictionary<StringName, BattleAiScoreProfileDefinition>();
        var errors = new List<string>();

        if (profile != null)
        {
            foreach (BattleSimOverridePatchDefinition patch in profile.OverridePatches)
            {
                if (patch == null)
                {
                    errors.Add(
                        $"Battle sim profile {profile.ProfileId} contains a null override patch."
                    );
                    continue;
                }
                ApplyPatch(
                    patchedSkills,
                    patchedBrains,
                    ref scoreProfile,
                    factionProfiles,
                    patch,
                    errors
                );
            }
        }

        foreach (string error in errors)
            GameLog.Error(error, "battlesim.override.failed", "battlesim");

        return new BattleSimOverrideApplyResult(
            patchedSkills,
            patchedBrains,
            scoreProfile,
            factionProfiles,
            errors
        );
    }

    private static void ApplyPatch(
        Dictionary<StringName, SkillDefinition> skills,
        Dictionary<StringName, EnemyAiBrainDefinition> brains,
        ref BattleAiScoreProfileDefinition scoreProfile,
        Dictionary<StringName, BattleAiScoreProfileDefinition> factionProfiles,
        BattleSimOverridePatchDefinition patch,
        ICollection<string> errors
    )
    {
        if (string.IsNullOrEmpty(patch.Path))
        {
            errors.Add(
                $"Battle sim override patch for target_type={patch.TargetType} is missing path."
            );
            return;
        }

        switch (patch.TargetType)
        {
            case "skill":
                ApplySkillPatch(skills, patch, errors);
                return;
            case "brain":
                ApplyBrainPatch(brains, patch, errors);
                return;
            case "action":
                ApplyActionPatch(brains, patch, errors);
                return;
            case "ai_score_profile":
                if (!TryPatchScoreProfile(scoreProfile, patch.Path, patch.Value, out var patched, out string scoreError))
                    errors.Add(scoreError);
                else
                    scoreProfile = patched;
                return;
            case "faction_ai_score_profile":
                if (patch.TargetId == "")
                {
                    errors.Add(
                        $"Battle sim faction_ai_score_profile patch is missing target_id for path {patch.Path}."
                    );
                    return;
                }
                BattleAiScoreProfileDefinition factionProfile = factionProfiles.TryGetValue(
                    patch.TargetId,
                    out BattleAiScoreProfileDefinition existing
                )
                    ? existing
                    : scoreProfile;
                if (!TryPatchScoreProfile(factionProfile, patch.Path, patch.Value, out var patchedFaction, out string factionError))
                    errors.Add(factionError);
                else
                    factionProfiles[patch.TargetId] = patchedFaction;
                return;
            default:
                errors.Add(
                    $"Battle sim override patch uses unsupported target_type {patch.TargetType} for path {patch.Path}."
                );
                return;
        }
    }

    private static void ApplySkillPatch(
        IDictionary<StringName, SkillDefinition> skills,
        BattleSimOverridePatchDefinition patch,
        ICollection<string> errors
    )
    {
        if (!skills.TryGetValue(patch.TargetId, out SkillDefinition skill) || skill == null)
        {
            errors.Add(
                $"Battle sim override patch target skill {patch.TargetId} was not found for path {patch.Path}."
            );
            return;
        }
        if (patch.Path != "combat_profile.stamina_cost")
        {
            errors.Add(
                $"Battle sim override skill patch path {patch.Path} is unsupported; supported skill patch paths: combat_profile.stamina_cost."
            );
            return;
        }
        if (skill.CombatProfile == null)
        {
            errors.Add(
                $"Battle sim override patch target skill {patch.TargetId} has no combat_profile for path {patch.Path}."
            );
            return;
        }
        if (!TryInt(patch.Value, out int value))
        {
            errors.Add(
                $"Battle sim override skill patch {patch.TargetId}.{patch.Path} requires an int value."
            );
            return;
        }
        skills[patch.TargetId] = skill.WithCombatProfile(
            skill.CombatProfile.WithStaminaCost(value)
        );
    }

    private static void ApplyBrainPatch(
        IDictionary<StringName, EnemyAiBrainDefinition> brains,
        BattleSimOverridePatchDefinition patch,
        ICollection<string> errors
    )
    {
        if (!brains.TryGetValue(patch.TargetId, out EnemyAiBrainDefinition brain) || brain == null)
        {
            errors.Add(
                $"Battle sim override patch target brain {patch.TargetId} was not found for path {patch.Path}."
            );
            return;
        }
        if (!TryInt(patch.Value, out int value))
        {
            errors.Add(
                $"Battle sim override brain patch {patch.TargetId}.{patch.Path} requires an int value."
            );
            return;
        }

        string[] segments = patch.Path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (
            segments.Length != 5
            || segments[0] != "transition_rules"
            || !int.TryParse(segments[1], out int ruleIndex)
            || segments[2] != "conditions"
            || !int.TryParse(segments[3], out int conditionIndex)
            || (segments[4] != "basis_points" && segments[4] != "max_distance")
            || ruleIndex < 0
            || ruleIndex >= brain.TransitionRules.Count
            || conditionIndex < 0
            || conditionIndex >= brain.TransitionRules[ruleIndex].Conditions.Count
        )
        {
            errors.Add(
                $"Battle sim override brain patch path {patch.Path} is unsupported or out of range."
            );
            return;
        }

        var rules = new List<EnemyAiTransitionRuleDefinition>(brain.TransitionRules);
        EnemyAiTransitionRuleDefinition rule = rules[ruleIndex];
        var conditions = new List<EnemyAiTransitionConditionDefinition>(rule.Conditions);
        EnemyAiTransitionConditionDefinition condition = conditions[conditionIndex];
        conditions[conditionIndex] = new EnemyAiTransitionConditionDefinition(
            condition.Predicate,
            segments[4] == "basis_points" ? value : condition.BasisPoints,
            segments[4] == "max_distance" ? value : condition.MaxDistance,
            condition.StateIds,
            condition.Affordances
        );
        rules[ruleIndex] = new EnemyAiTransitionRuleDefinition(
            rule.RuleId,
            rule.Order,
            rule.FromStateIds,
            rule.TargetStateId,
            conditions,
            rule.DesignerNote
        );
        brains[patch.TargetId] = new EnemyAiBrainDefinition(
            brain.BrainId,
            brain.DefaultStateId,
            brain.ScoreProfile,
            brain.StateOrder,
            rules
        );
    }

    private static void ApplyActionPatch(
        IDictionary<StringName, EnemyAiBrainDefinition> brains,
        BattleSimOverridePatchDefinition patch,
        ICollection<string> errors
    )
    {
        if (!brains.TryGetValue(patch.TargetId, out EnemyAiBrainDefinition brain) || brain == null)
        {
            errors.Add(
                $"Battle sim override patch target action was not found for path {patch.Path}: brain={patch.TargetId}."
            );
            return;
        }
        if (!TryInt(patch.Value, out int value))
        {
            errors.Add(
                $"Battle sim override action patch {patch.ActionId}.{patch.Path} requires an int value."
            );
            return;
        }

        var states = new List<EnemyAiStateDefinition>(brain.StateOrder);
        bool matched = false;
        for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
        {
            EnemyAiStateDefinition state = states[stateIndex];
            if (patch.StateId != "" && state.StateId != patch.StateId)
                continue;
            var actions = new List<EnemyAiActionDefinition>(state.Actions);
            bool stateChanged = false;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                EnemyAiActionDefinition action = actions[actionIndex];
                if (patch.ActionId != "" && action.ActionId != patch.ActionId)
                    continue;
                if (!TryPatchAction(action, patch.Path, value, out EnemyAiActionDefinition patched, out string error))
                {
                    errors.Add(error);
                    return;
                }
                actions[actionIndex] = patched;
                matched = true;
                stateChanged = true;
                break;
            }
            if (stateChanged)
            {
                states[stateIndex] = new EnemyAiStateDefinition(
                    state.StateId,
                    actions,
                    state.GenerationSlots
                );
            }
        }
        if (matched)
        {
            brains[brain.BrainId] = new EnemyAiBrainDefinition(
                brain.BrainId,
                brain.DefaultStateId,
                brain.ScoreProfile,
                states,
                brain.TransitionRules
            );
            return;
        }
        errors.Add(
            $"Battle sim override patch target action was not found for path {patch.Path}: brain={patch.TargetId}, state={patch.StateId}, action={patch.ActionId}."
        );
    }

    private static bool TryPatchAction(
        EnemyAiActionDefinition action,
        string path,
        int value,
        out EnemyAiActionDefinition patched,
        out string error
    )
    {
        patched = action;
        error = "";
        switch (action)
        {
            case UseUnitSkillActionDefinition unit
                when path is "desired_min_distance" or "desired_max_distance":
                patched = new UseUnitSkillActionDefinition(
                    unit.ActionId,
                    unit.ScoreBucketId,
                    unit.ActionIntent,
                    unit.SkillIds,
                    unit.TargetSelector,
                    unit.MinimumEffectiveTargetCount,
                    unit.MaximumFriendlyFireTargetCount,
                    unit.AllowFriendlyLethal,
                    path == "desired_min_distance" ? value : unit.DesiredMinDistance,
                    path == "desired_max_distance" ? value : unit.DesiredMaxDistance,
                    unit.DistanceReference
                );
                return true;
            case MoveToRangeActionDefinition move when path is "desired_min_distance" or "desired_max_distance":
                patched = new MoveToRangeActionDefinition(
                    move.ActionId,
                    move.ScoreBucketId,
                    move.ActionIntent,
                    move.AiEvaluationMode,
                    move.TargetSelector,
                    path == "desired_min_distance" ? value : move.DesiredMinDistance,
                    path == "desired_max_distance" ? value : move.DesiredMaxDistance,
                    move.RangeSkillIds,
                    move.ScreeningMode,
                    move.EnableAoeSetupPositioning,
                    move.AoeSetupMinTargetCount,
                    move.AoeSetupTargetCountWeight,
                    move.AoeSetupImprovementWeight,
                    move.AoeSetupFriendlyFirePenalty,
                    move.ScreeningMinHpBasisPoints,
                    move.ScreeningAllyMinAttackRange,
                    move.ScreeningEnemyMaxContactRange,
                    move.ScreeningThreatDistanceBuffer,
                    move.ScreeningPathBonus
                );
                return true;
            case UseGroundSkillActionDefinition ground when path is "minimum_hit_count" or "desired_min_distance" or "desired_max_distance":
                patched = new UseGroundSkillActionDefinition(
                    ground.ActionId,
                    ground.ScoreBucketId,
                    ground.ActionIntent,
                    ground.SkillIds,
                    path == "minimum_hit_count" ? value : ground.MinimumHitCount,
                    ground.AllowEmptyGroundControl,
                    ground.AllowGroundControlSupplementPartialHits,
                    ground.MinimumGroundControlScore,
                    ground.MinimumAllyThreatHitCount,
                    ground.MaximumFriendlyFireTargetCount,
                    ground.AllowFriendlyLethal,
                    ground.ThreatMinimumSafeDistance,
                    ground.ThreatSafeDistanceMargin,
                    path == "desired_min_distance" ? value : ground.DesiredMinDistance,
                    path == "desired_max_distance" ? value : ground.DesiredMaxDistance,
                    ground.DistanceReference
                );
                return true;
            case UseGroundRepositionSkillActionDefinition reposition
                when path
                    is "min_survival_margin_gain_to_escape"
                        or "minimum_safe_distance"
                        or "desired_max_distance_bonus":
                patched = new UseGroundRepositionSkillActionDefinition(
                    reposition.ActionId,
                    reposition.ScoreBucketId,
                    reposition.ActionIntent,
                    reposition.SkillIds,
                    reposition.TargetSelector,
                    path == "minimum_safe_distance" ? value : reposition.MinimumSafeDistance,
                    reposition.SafeDistanceMargin,
                    path == "desired_max_distance_bonus"
                        ? value
                        : reposition.DesiredMaxDistanceBonus,
                    reposition.ActionBaseScore,
                    path == "min_survival_margin_gain_to_escape"
                        ? value
                        : reposition.MinSurvivalMarginGainToEscape
                );
                return true;
            case RetreatActionDefinition retreat when path == "minimum_safe_distance":
                patched = new RetreatActionDefinition(
                    retreat.ActionId,
                    retreat.ScoreBucketId,
                    retreat.ActionIntent,
                    retreat.TargetSelector,
                    value,
                    retreat.UseDynamicThreatSafeDistance,
                    retreat.SafeDistanceMargin
                );
                return true;
            case MoveToAdvantagePositionActionDefinition advantage
                when path
                    is "desired_min_distance"
                        or "desired_max_distance"
                        or "minimum_safe_distance"
                        or "min_survival_margin_gain_to_escape":
                patched = new MoveToAdvantagePositionActionDefinition(
                    advantage.ActionId,
                    advantage.ScoreBucketId,
                    advantage.ActionIntent,
                    advantage.TargetSelector,
                    path == "desired_min_distance" ? value : advantage.DesiredMinDistance,
                    path == "desired_max_distance" ? value : advantage.DesiredMaxDistance,
                    advantage.RangeSkillIds,
                    path == "minimum_safe_distance" ? value : advantage.MinimumSafeDistance,
                    advantage.SafeDistanceMargin,
                    path == "min_survival_margin_gain_to_escape"
                        ? value
                        : advantage.MinSurvivalMarginGainToEscape,
                    advantage.MinDistanceProgressWhenBeyondBand,
                    advantage.PositioningMode,
                    advantage.HighGroundWeight,
                    advantage.SafetyWeight,
                    advantage.DistanceBandWeight,
                    advantage.CandidateLimit
                );
                return true;
        }
        error =
            $"Battle sim override action patch path {path} is unsupported for {action.Kind}.";
        return false;
    }

    private static bool TryPatchScoreProfile(
        BattleAiScoreProfileDefinition source,
        string path,
        object rawValue,
        out BattleAiScoreProfileDefinition patched,
        out string error
    )
    {
        source ??= BattleAiScoreProfileDefinition.Default;
        patched = source;
        if (!TryInt(rawValue, out int value))
        {
            error = $"Battle sim override score patch {path} requires an int value.";
            return false;
        }

        const string ActionScoresPrefix = "action_base_scores.";
        if (path.StartsWith(ActionScoresPrefix, StringComparison.Ordinal))
        {
            StringName key = path[ActionScoresPrefix.Length..];
            if (key == "")
            {
                error = $"Battle sim override score patch {path} is missing a score key.";
                return false;
            }
            var scores = new Dictionary<StringName, int>(source.ActionBaseScores) { [key] = value };
            patched = source.WithActionBaseScores(scores);
            error = "";
            return true;
        }

        const string BucketPrefix = "bucket_priorities.";
        if (path.StartsWith(BucketPrefix, StringComparison.Ordinal))
        {
            StringName key = path[BucketPrefix.Length..];
            if (key == "")
            {
                error = $"Battle sim override score patch {path} is missing a bucket key.";
                return false;
            }
            var priorities = new Dictionary<StringName, int>(source.BucketPriorities)
            {
                [key] = value,
            };
            patched = source.WithBucketPriorities(priorities);
            error = "";
            return true;
        }

        if (!source.TryWithScalar(path, value, out patched))
        {
            error = $"Battle sim override score patch path {path} is unsupported.";
            return false;
        }
        error = "";
        return true;
    }

    private static bool TryInt(object value, out int result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                result = (int)longValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case byte byteValue:
                result = byteValue;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
