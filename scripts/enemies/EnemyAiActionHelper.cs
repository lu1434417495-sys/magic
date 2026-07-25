using System.Collections.Generic;
using Godot;

internal static class EnemyAiActionHelper
{
    internal static BattleAiDecision CreateDecision(
        StringName actionId,
        StringName scoreBucketId,
        BattleCommand command,
        string reasonText = ""
    )
    {
        return new BattleAiDecision
        {
            command = command,
            action_id = actionId,
            reason_text = reasonText,
            score_bucket_id = scoreBucketId,
        };
    }

    internal static BattleAiDecision CreateScoredDecision(
        StringName actionId,
        StringName scoreBucketId,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string reasonText = ""
    )
    {
        BattleAiDecision decision = CreateDecision(actionId, scoreBucketId, command, reasonText);
        decision.skill_score_input = scoreInput;
        decision.score_input = scoreInput;
        return decision;
    }

    internal static BattleCommand BuildWaitCommand(BattleAiContext context)
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
        {
            return null;
        }
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Wait,
            unit_id = unitState.unit_id,
        };
    }

    internal static BattleCommand BuildMoveCommand(BattleAiContext context, Vector2I targetCoord)
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
        {
            return null;
        }
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Move,
            unit_id = unitState.unit_id,
            target_coord = targetCoord,
        };
    }

    internal static BattleCommand BuildUnitSkillCommand(
        BattleAiContext context,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName skillVariantId = default
    ) => BuildUnitSkillCommand(
        context,
        new BattleSkillEntryRef(
            BattleSkillEntryIds.KnownSkill(skillId),
            ProgressionDataUtils.to_string_name(skillId),
            BattleSkillEntrySourceKind.KnownSkill,
            ""
        ),
        targetUnit,
        skillVariantId
    );

    internal static BattleCommand BuildUnitSkillCommand(
        BattleAiContext context,
        BattleAvailableSkillEntry entry,
        BattleUnitState targetUnit,
        StringName skillVariantId = default
    ) => BuildUnitSkillCommand(context, entry?.EntryRef ?? default, targetUnit, skillVariantId);

    internal static BattleCommand BuildUnitSkillCommand(
        BattleAiContext context,
        BattleSkillEntryRef entryRef,
        BattleUnitState targetUnit,
        StringName skillVariantId = default
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null || targetUnit == null || IsEmpty(entryRef.SkillId))
        {
            return null;
        }
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unitState.unit_id,
            skill_entry_id = entryRef.SkillEntryId,
            skill_id = entryRef.SkillId,
            skill_variant_id = skillVariantId,
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.GetAnchorCoord(),
        };
    }

    internal static BattleCommand BuildGroundSkillCommand(
        BattleAiContext context,
        StringName skillId,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    ) => BuildGroundSkillCommand(
        context,
        new BattleSkillEntryRef(
            BattleSkillEntryIds.KnownSkill(skillId),
            ProgressionDataUtils.to_string_name(skillId),
            BattleSkillEntrySourceKind.KnownSkill,
            ""
        ),
        skillVariantId,
        targetCoords
    );

    internal static BattleCommand BuildGroundSkillCommand(
        BattleAiContext context,
        BattleAvailableSkillEntry entry,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    ) => BuildGroundSkillCommand(context, entry?.EntryRef ?? default, skillVariantId, targetCoords);

    internal static BattleCommand BuildGroundSkillCommand(
        BattleAiContext context,
        BattleSkillEntryRef entryRef,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null || IsEmpty(entryRef.SkillId))
        {
            return null;
        }
        List<Vector2I> sortedCoords = SortCoords(targetCoords);
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unitState.unit_id,
            skill_entry_id = entryRef.SkillEntryId,
            skill_id = entryRef.SkillId,
            skill_variant_id = skillVariantId,
            target_coords = new Vector2IList(sortedCoords),
        };
        if (command.TargetCoordsTyped.Count > 0)
        {
            command.target_coord = command.TargetCoordsTyped[0];
        }
        return command;
    }

    internal static List<Vector2I> SortCoords(IEnumerable<Vector2I> coords)
    {
        List<Vector2I> coordList = coords != null ? new List<Vector2I>(coords) : new();
        coordList.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
        return coordList;
    }

    internal static string CoordSetKey(IEnumerable<Vector2I> coords)
    {
        var parts = new List<string>();
        foreach (Vector2I coord in SortCoords(coords))
        {
            parts.Add($"{coord.X}:{coord.Y}");
        }
        return string.Join("|", parts);
    }

    internal static AiActionTrace BeginActionTrace(
        StringName actionId,
        StringName scoreBucketId,
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (context?.trace_enabled != true)
            return null;

        StringName traceId = context != null ? context.NextActionTraceId(actionId) : actionId;
        return new AiActionTrace(
            traceId,
            actionId.ToString(),
            scoreBucketId.ToString(),
            metadata
        );
    }

    internal static void TraceCountIncrement(
        AiActionTrace actionTrace,
        string key,
        int amount = 1
    )
    {
        actionTrace?.Increment(key, amount);
    }

    internal static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey)
    {
        actionTrace?.AddBlockReason(reasonKey);
    }

    internal static void TraceOfferCandidate(
        AiActionTrace actionTrace,
        AiCandidateSummary candidateSummary,
        int keepCount = 5
    )
    {
        actionTrace?.OfferCandidate(candidateSummary, keepCount);
    }

    internal static StringName FinalizeActionTrace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision = null
    )
    {
        if (actionTrace == null || actionTrace.IsEmpty())
        {
            return "";
        }
        actionTrace.ApplyBestDecision(bestDecision);
        context?.RecordActionTrace(actionTrace);
        return actionTrace.TraceId;
    }

    internal static AiCandidateSummary BuildCandidateSummary(
        string label,
        BattleCommand command,
        BattleAiScoreInput scoreInput = null,
        IReadOnlyDictionary<string, object> extra = null
    )
    {
        return AiCandidateSummary.Create(
            label,
            command,
            scoreInput,
            extra
        );
    }

    internal static string FormatSkillVariantLabel(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (skillDefinition == null)
        {
            return "";
        }
        if (castVariant == null || castVariant.DisplayName.Length == 0)
        {
            return skillDefinition.DisplayName;
        }
        return $"{skillDefinition.DisplayName}·{castVariant.DisplayName}";
    }

    internal static AiCommandSummary BuildCommandSummary(BattleCommand command) =>
        AiCommandSummary.FromCommand(command);

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
