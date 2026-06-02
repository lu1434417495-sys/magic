using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_trace_summary_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestTraceSummaryTypesArePlainCSharpDtos();
            TestCommandSummaryCopiesBattleCommandAndProjectsToDictionary();
            TestActionTraceProjectsStableDictionaryShape();
            TestEnemyAiActionHelperUsesTypedTraceState();
            TestWaitActionActiveRestUsesTypedProfile();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI trace summary regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI trace summary regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestTraceSummaryTypesArePlainCSharpDtos()
    {
        AssertPlainDto(typeof(AiCommandSummary), "AiCommandSummary");
        AssertPlainDto(typeof(AiCandidateSummary), "AiCandidateSummary");
        AssertPlainDto(typeof(AiActionTrace), "AiActionTrace");
        AssertPublicApiDoesNotExposeGodotTypes(typeof(AiCommandSummary));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(AiCandidateSummary));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(AiActionTrace));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(AiCommandSummary));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(AiCandidateSummary));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(AiActionTrace));

        AssertTrue(
            typeof(AiCommandSummary).GetMethod("FromCommand") != null
                && typeof(AiCommandSummary).GetMethod("from_command") == null,
            "AiCommandSummary should expose FromCommand() and not keep from_command()."
        );
        AssertTrue(
            typeof(AiActionTrace).GetMethod("IsEmpty") != null
                && typeof(AiActionTrace).GetMethod("is_empty") == null,
            "AiActionTrace should expose IsEmpty() and not keep is_empty()."
        );
        AssertTrue(
            typeof(AiActionTrace).GetMethod("to_dict") == null
                && typeof(AiCandidateSummary).GetMethod("to_dict") == null
                && typeof(AiCommandSummary).GetMethod("to_dict") == null,
            "AI trace summary DTOs should not keep GDScript-style to_dict() API."
        );
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
        command.target_unit_ids.Add("target");
        command.target_unit_ids.Add("support");
        command.target_coords.Add(new Vector2I(3, 4));
        command.target_coords.Add(new Vector2I(4, 4));

        AiCommandSummary summary = AiCommandSummary.FromCommand(command);
        command.target_unit_ids.Add("late_mutation");
        command.target_coords.Add(new Vector2I(9, 9));

        AssertEq(summary.CommandType, "skill", "CommandType should copy command_type.");
        AssertEq(summary.UnitId, "caster", "UnitId should copy unit_id.");
        AssertEq(summary.TargetUnitIds.Count, 2, "TargetUnitIds should be copied into a C# list.");
        AssertEq(summary.TargetCoords.Count, 2, "TargetCoords should be copied into a C# list.");

        Godot.Collections.Dictionary payload = summary.ToDictionary();
        AssertEq(payload["command_type"].AsString(), "skill", "Projection should include command_type.");
        AssertEq(
            payload["target_unit_ids"].AsGodotArray().Count,
            2,
            "Projection should preserve copied target unit ids."
        );
        AssertEq(
            payload["target_coords"].AsGodotArray().Count,
            2,
            "Projection should preserve copied target coords."
        );
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

        AssertTrue(!trace.IsEmpty(), "Trace with trace_id should not be empty.");
        Godot.Collections.Dictionary payload = trace.ToDictionary();
        AssertEq(payload["trace_id"].AsString(), "trace_1", "Trace projection should include trace_id.");
        AssertEq(payload["action_id"].AsString(), "cast_bolt", "Trace projection should include action_id.");
        AssertEq(payload["evaluation_count"].AsInt32(), 3, "Trace projection should include evaluation_count.");
        AssertEq(payload["candidate_count"].AsInt32(), 1, "Trace projection should include candidate_count.");
        AssertEq(
            payload["top_candidates"].AsGodotArray().Count,
            1,
            "Trace projection should include candidate summaries."
        );
        AssertEq(
            payload["gate_rejection_reason"].AsString(),
            "unsafe",
            "Trace projection should include gate rejection reason."
        );
    }

    private void TestEnemyAiActionHelperUsesTypedTraceState()
    {
        Type helperType = typeof(EnemyAiActionHelper);
        AssertTrue(helperType.IsAbstract && helperType.IsSealed, "EnemyAiActionHelper should be a static helper.");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(helperType),
            "EnemyAiActionHelper should not inherit GodotObject/RefCounted."
        );
        AssertTrue(
            helperType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "EnemyAiActionHelper should not register as GlobalClass."
        );
        AssertEq(
            helperType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length,
            0,
            "EnemyAiActionHelper should not expose public GDScript-style helper API."
        );

        var context = new BattleAiContext { trace_enabled = true };
        AiActionTrace trace = EnemyAiActionHelper.BeginActionTrace(
            "wait_action",
            "idle",
            context,
            new Godot.Collections.Dictionary { ["generated"] = true }
        );
        EnemyAiActionHelper.TraceCountIncrement(trace, "evaluation_count", 2);
        EnemyAiActionHelper.TraceAddBlockReason(trace, "blocked_by_test");

        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = "actor",
        };
        var scoreInput = new BattleAiScoreInput
        {
            total_score = 12,
            score_bucket_id = "idle",
        };
        AiCandidateSummary candidate = EnemyAiActionHelper.BuildCandidateSummary(
            "wait",
            command,
            scoreInput,
            new Godot.Collections.Dictionary { ["source"] = "test" }
        );
        EnemyAiActionHelper.TraceOfferCandidate(trace, candidate);
        BattleAiDecision decision = EnemyAiActionHelper.CreateScoredDecision(
            "wait_action",
            "idle",
            command,
            scoreInput,
            "selected"
        );

        StringName traceId = EnemyAiActionHelper.FinalizeActionTrace(context, trace, decision);

        AssertEq(traceId.ToString(), "wait_action_1", "Helper should allocate a typed trace id.");
        AssertEq(decision.action_trace_id.ToString(), "wait_action_1", "Finalization should write the decision trace id.");
        AssertEq(context.action_traces.Count, 1, "Context should expose one projected trace.");

        Godot.Collections.Dictionary payload = context.action_traces[0].AsGodotDictionary();
        AssertEq(payload["evaluation_count"].AsInt32(), 2, "Typed trace should keep evaluation count.");
        AssertEq(payload["blocked_count"].AsInt32(), 1, "Typed trace should keep block count.");
        AssertEq(payload["candidate_count"].AsInt32(), 1, "Typed trace should keep candidate count.");
        AssertEq(
            payload["metadata"].AsGodotDictionary()["generated"].AsBool(),
            true,
            "Projected trace should preserve metadata."
        );
        AssertEq(
            payload["best_score_input"].AsGodotDictionary()["total_score"].AsInt32(),
            12,
            "Projected trace should preserve best score input."
        );
    }

    private void TestWaitActionActiveRestUsesTypedProfile()
    {
        Type profileType = typeof(WaitAction).GetNestedType(
            "ActiveRestProfile",
            BindingFlags.NonPublic
        );
        AssertTrue(profileType != null && profileType.IsSealed, "WaitAction active-rest profile should be a private sealed C# type.");
        AssertTrue(
            profileType != null && !typeof(GodotObject).IsAssignableFrom(profileType),
            "WaitAction active-rest profile should not inherit GodotObject/RefCounted."
        );
        MethodInfo profileFactory = typeof(WaitAction).GetMethod(
            "_build_active_rest_profile",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        AssertTrue(
            profileFactory != null && profileFactory.ReturnType == profileType,
            "WaitAction should build a typed active-rest profile instead of a Godot Dictionary."
        );
        if (profileType != null)
        {
            foreach (FieldInfo field in profileType.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                     ))
            {
                AssertTrue(
                    !IsForbiddenPublicApiType(field.FieldType),
                    $"WaitAction active-rest profile field {field.Name} should not use Godot Dictionary/Array/Variant."
                );
            }
        }

        var unit = new BattleUnitState
        {
            unit_id = "wait_actor",
            display_name = "Wait Actor",
            faction_id = "hostile",
            action_threshold = 5,
            current_stamina = 1,
            stamina_recovery_progress = 0,
        };
        unit.attribute_snapshot = new AttributeSnapshot();
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 10);
        unit.attribute_snapshot.set_value(UnitBaseAttributes.CONSTITUTION(), 10);

        var basicAttack = new SkillDef
        {
            skill_id = "basic_attack",
            combat_profile = new CombatSkillDef
            {
                skill_id = "basic_attack",
                stamina_cost = 4,
                target_mode = "unit",
            },
        };

        var context = new BattleAiContext
        {
            trace_enabled = true,
            unit_state = unit,
            skill_defs = new Godot.Collections.Dictionary
            {
                [new StringName("basic_attack")] = basicAttack,
            },
            action_score_input_callback = (
                _,
                actionKind,
                actionLabel,
                scoreBucketId,
                command,
                preview,
                metadata
            ) =>
                new BattleAiScoreInput
                {
                    action_kind = actionKind,
                    action_label = actionLabel,
                    score_bucket_id = scoreBucketId,
                    command = command,
                    runtime_action_metadata = metadata.Duplicate(true),
                    total_score = ReadInt(metadata, "action_base_score", 0),
                },
        };

        var action = new WaitAction
        {
            action_id = "wait_active_rest",
            active_rest_action_base_score = 17,
        };

        BattleAiDecision decision = action.decide(context);
        AssertTrue(decision != null, "WaitAction should return an active-rest decision.");
        AssertTrue(
            decision != null && decision.reason_text.Contains("主动休息", StringComparison.Ordinal),
            "Active-rest decision should keep the active rest reason text."
        );
        AssertTrue(
            ReadBool(decision?.score_input?.runtime_action_metadata, "active_rest"),
            "Active-rest score metadata should still project active_rest at the score boundary."
        );
        AssertEq(
            ReadInt(decision?.score_input?.runtime_action_metadata, "action_base_score", -1),
            17,
            "Active-rest score metadata should preserve action_base_score."
        );
        AssertEq(context.action_traces.Count, 1, "WaitAction should emit one action trace.");
        if (context.action_traces.Count > 0)
        {
            Godot.Collections.Dictionary payload = context.action_traces[0].AsGodotDictionary();
            Godot.Collections.Dictionary metadata = payload["metadata"].AsGodotDictionary();
            AssertTrue(
                ReadBool(metadata, "active_rest"),
                "WaitAction trace metadata should still project active_rest."
            );
            AssertEq(
                ReadInt(metadata, "projected_rest_stamina", -1),
                5,
                "WaitAction trace metadata should preserve projected rest stamina."
            );
            AssertEq(
                payload["best_score_input"].AsGodotDictionary()["total_score"].AsInt32(),
                17,
                "WaitAction candidate summary should keep active-rest score."
            );
        }
    }

    private void AssertPlainDto(Type type, string typeName)
    {
        AssertTrue(type.IsSealed, $"{typeName} should be sealed.");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(type),
            $"{typeName} should be a plain C# DTO, not GodotObject/RefCounted."
        );
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            $"{typeName} should not register as GlobalClass."
        );
        AssertEq(
            type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length,
            0,
            $"{typeName} should expose typed properties/lists, not public mutable fields."
        );
    }

    private void AssertPublicApiDoesNotExposeGodotTypes(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertTrue(
                !IsForbiddenPublicApiType(method.ReturnType),
                $"{type.Name}.{method.Name} should not return Godot Dictionary/Array/Variant."
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsForbiddenPublicApiType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) should not accept Godot Dictionary/Array/Variant."
                );
            }
        }
    }

    private void AssertPublicPropertiesDoNotExposeGodotTypes(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertTrue(
                !IsForbiddenPublicApiType(property.PropertyType),
                $"{type.Name}.{property.Name} should not expose Godot Dictionary/Array/Variant."
            );
        }
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

    private static bool ReadBool(Godot.Collections.Dictionary dictionary, string key)
    {
        return TryReadValue(dictionary, key, out Variant value) && value.AsBool();
    }

    private static int ReadInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
    {
        return TryReadValue(dictionary, key, out Variant value) ? value.AsInt32() : fallback;
    }

    private static bool TryReadValue(
        Godot.Collections.Dictionary dictionary,
        string key,
        out Variant value
    )
    {
        value = default;
        if (dictionary == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        var stringNameKey = new StringName(key);
        if (dictionary.ContainsKey(stringNameKey))
        {
            value = dictionary[stringNameKey];
            return true;
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} Expected {expected}, got {actual}.");
        }
    }
}
