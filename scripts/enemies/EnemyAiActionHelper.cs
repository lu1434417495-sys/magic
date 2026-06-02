using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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
            command_type = BattleCommand.TYPE_WAIT(),
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
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = unitState.unit_id,
            target_coord = targetCoord,
        };
    }

    internal static BattleCommand BuildUnitSkillCommand(
        BattleAiContext context,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName skillVariantId = default
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null || targetUnit == null)
        {
            return null;
        }
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = unitState.unit_id,
            skill_id = skillId,
            skill_variant_id = skillVariantId,
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.coord,
        };
    }

    internal static BattleCommand BuildGroundSkillCommand(
        BattleAiContext context,
        StringName skillId,
        StringName skillVariantId,
        GArray targetCoords
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
        {
            return null;
        }
        Godot.Collections.Array<Vector2I> sortedCoords = SortCoords(targetCoords);
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = unitState.unit_id,
            skill_id = skillId,
            skill_variant_id = skillVariantId,
            target_coords = sortedCoords,
        };
        if (command.target_coords.Count > 0)
        {
            command.target_coord = command.target_coords[0];
        }
        return command;
    }

    internal static Godot.Collections.Array<Vector2I> SortCoords(GArray coords)
    {
        List<Vector2I> coordList = ReadVector2ICoords(coords);
        coordList.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
        var sortedCoords = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I coord in coordList)
        {
            sortedCoords.Add(coord);
        }
        return sortedCoords;
    }

    internal static string CoordSetKey(GArray coords)
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
        GDictionary metadata = null
    )
    {
        StringName traceId = context != null ? context.next_action_trace_id(actionId) : actionId;
        return new AiActionTrace(
            traceId,
            actionId.ToString(),
            scoreBucketId.ToString(),
            TraceDictionaryProjection.FromDictionary(metadata)
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
        GDictionary extra = null
    )
    {
        return AiCandidateSummary.Create(
            label,
            command,
            scoreInput,
            TraceDictionaryProjection.FromDictionary(extra)
        );
    }

    internal static string FormatSkillVariantLabel(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (skillDef == null)
        {
            return "";
        }
        if (castVariant == null || castVariant.display_name.Length == 0)
        {
            return skillDef.display_name;
        }
        return $"{skillDef.display_name}·{castVariant.display_name}";
    }

    internal static AiCommandSummary BuildCommandSummary(BattleCommand command) =>
        AiCommandSummary.FromCommand(command);

    private static List<Vector2I> ReadVector2ICoords(GArray coords)
    {
        var result = new List<Vector2I>();
        if (coords == null)
        {
            return result;
        }
        foreach (Variant rawValue in coords)
        {
            if (rawValue.VariantType == Variant.Type.Vector2I)
            {
                result.Add(rawValue.AsVector2I());
            }
        }
        return result;
    }
}
