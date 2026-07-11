using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_trace_summary_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestCommandSummaryCopiesBattleCommandAndProjectsToDictionary();
            TestBattlePreviewDamagePreviewSetterDecodesProjectedPayload();
            TestActionTraceProjectsStableDictionaryShape();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI trace summary regression"));
    }

    private void TestCommandSummaryCopiesBattleCommandAndProjectsToDictionary()
    {
        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = "caster",
            skill_id = "bolt",
            skill_variant_id = "wide",
            target_unit_id = "target",
            target_coord = new Vector2I(3, 4),
        };
        command.SetTargetUnitIds(new[] { new StringName("target"), new StringName("support") });
        command.SetTargetCoords(new[] { new Vector2I(3, 4), new Vector2I(4, 4) });

        AiCommandSummary summary = AiCommandSummary.FromCommand(command);
        command.target_unit_ids.Add("late_mutation");
        command.target_coords.Add(new Vector2I(9, 9));

        _test.Eq(summary.CommandType, "skill", "CommandType should copy command_type.");
        _test.Eq(summary.UnitId, "caster", "UnitId should copy unit_id.");
        _test.Eq(summary.TargetUnitIds.Count, 2, "TargetUnitIds should be copied into a C# list.");
        _test.Eq(summary.TargetCoords.Count, 2, "TargetCoords should be copied into a C# list.");

        Godot.Collections.Dictionary payload = summary.ToDictionary();
        _test.Eq(payload["command_type"].AsString(), "skill", "Projection should include command_type.");
        _test.Eq(
            payload["target_unit_ids"].AsGodotArray().Count,
            2,
            "Projection should preserve copied target unit ids."
        );
        _test.Eq(
            payload["target_coords"].AsGodotArray().Count,
            2,
            "Projection should preserve copied target coords."
        );
    }

    private void TestBattlePreviewDamagePreviewSetterDecodesProjectedPayload()
    {
        var preview = new BattlePreview();
        preview.damage_preview = new GDictionary
        {
            ["has_damage"] = true,
            ["min_damage"] = 4,
            ["max_damage"] = 9,
        };

        _test.True(
            preview.DamagePreviewTyped.HasValue,
            "BattlePreview.damage_preview setter 应继续解码成 internal typed payload。"
        );
        if (preview.DamagePreviewTyped.HasValue)
        {
            _test.True(preview.DamagePreviewTyped.Value.HasDamage, "BattlePreview damage preview setter 应保留 has_damage。");
            _test.Eq(preview.DamagePreviewTyped.Value.MinDamage, 4, "BattlePreview damage preview setter 应保留 min_damage。");
            _test.Eq(preview.DamagePreviewTyped.Value.MaxDamage, 9, "BattlePreview damage preview setter 应保留 max_damage。");
        }
    }

    private void TestActionTraceProjectsStableDictionaryShape()
    {
        var command = new AiCommandSummary(
            "skill",
            "caster",
            "bolt",
            "",
            "target",
            new[] { new StringName("target") },
            new Vector2I(2, 2),
            new[] { new Vector2I(2, 2) }
        );
        var candidate = new AiCandidateSummary(
            "bolt@target",
            command,
            42,
            new Dictionary<string, object>
            {
                ["score_bucket_id"] = "offense",
                ["target_ids"] = new[] { new StringName("target") },
            }
        );
        var trace = new AiActionTrace(
            "trace_1",
            "cast_bolt",
            "offense",
            new Dictionary<string, object>
            {
                ["generated"] = true,
                ["position"] = new Vector2I(1, 2),
            }
        )
        {
            EvaluationCount = 3,
            CandidateCount = 1,
            BestReasonText = "best",
            BestCommand = command,
            Chosen = true,
            ChosenReasonText = "selected",
            GateRejected = true,
            GateRejectionReason = "unsafe",
        };
        trace.BlockReasons["blocked"] = 2;
        trace.TopCandidates.Add(candidate);
        trace.CandidateTraceCounters["evaluated"] = 3;

        _test.True(!trace.IsEmpty(), "Trace with trace_id should not be empty.");
        Godot.Collections.Dictionary payload = trace.ToDictionary();
        _test.Eq(payload["trace_id"].AsString(), "trace_1", "Trace projection should include trace_id.");
        _test.Eq(payload["action_id"].AsString(), "cast_bolt", "Trace projection should include action_id.");
        _test.Eq(payload["evaluation_count"].AsInt32(), 3, "Trace projection should include evaluation_count.");
        _test.Eq(payload["candidate_count"].AsInt32(), 1, "Trace projection should include candidate_count.");
        _test.Eq(
            payload["top_candidates"].AsGodotArray().Count,
            1,
            "Trace projection should include candidate summaries."
        );
        _test.Eq(
            payload["gate_rejection_reason"].AsString(),
            "unsafe",
            "Trace projection should include gate rejection reason."
        );
    }

    private static bool IsForbiddenPublicApiType(Type type)
    {
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.TryGetValue(key, out object value))
        {
            return false;
        }
        return value is bool boolValue && boolValue;
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback
    )
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.TryGetValue(key, out object value))
        {
            return fallback;
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            _ => fallback,
        };
    }

}
