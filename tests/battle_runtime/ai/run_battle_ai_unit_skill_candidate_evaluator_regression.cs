using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class run_battle_ai_unit_skill_candidate_evaluator_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestEvaluatorIsPlainCSharpHelper();
            TestEnemyAiActionHelperSkillCommandsCarrySelectedEntryIds();
            TestResolveAvailableSkillEntriesFiltersUnavailablePreferredSkills();
            TestAuthoredEnemyActionsResolveAvailabilityEntriesBeforeBuildingSkillCommands();
            TestEvaluatorGeneratedCommandCarriesAvailableEntryId();
            TestFastPreviewRejectsExposeOutOfRangeCounter();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI unit skill candidate evaluator regression"));
    }

    private void TestEvaluatorIsPlainCSharpHelper()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        _test.True(
            evaluatorType.IsSealed,
            "BattleAiUnitSkillCandidateEvaluator 应是 sealed helper。"
        );
    }

    private void TestEnemyAiActionHelperSkillCommandsCarrySelectedEntryIds()
    {
        StringName skillId = "ai_helper_entry_probe";
        StringName skillEntryId = "equipment:ai_helper_entry_probe";
        BattleUnitState actor = BuildUnit("helper_actor", "hostile", new Vector2I(0, 0));
        BattleUnitState target = BuildUnit("helper_target", "player", new Vector2I(1, 0));
        BattleAiContext context = new()
        {
            unit_state = actor,
        };
        BattleAvailableSkillEntry entry = new()
        {
            EntryRef = new BattleSkillEntryRef(
                skillEntryId,
                skillId,
                BattleSkillEntrySourceKind.EquipmentSkill,
                "probe_source"
            ),
            SkillLevel = 3,
        };

        BattleCommand unitCommand = EnemyAiActionHelper.BuildUnitSkillCommand(
            context,
            entry,
            target,
            "main"
        );
        _test.True(unitCommand != null, "unit skill command should be created.");
        if (unitCommand != null)
        {
            _test.Eq(unitCommand.skill_id, skillId, "unit command should preserve skill_id.");
            _test.Eq(
                unitCommand.skill_entry_id,
                skillEntryId,
                "unit command should carry the selected skill entry id."
            );
        }

        BattleCommand groundCommand = EnemyAiActionHelper.BuildGroundSkillCommand(
            context,
            entry,
            "ground",
            new[] { new Vector2I(3, 1), new Vector2I(2, 1) }
        );
        _test.True(groundCommand != null, "ground skill command should be created.");
        if (groundCommand != null)
        {
            _test.Eq(groundCommand.skill_id, skillId, "ground command should preserve skill_id.");
            _test.Eq(
                groundCommand.skill_entry_id,
                skillEntryId,
                "ground command should carry the selected skill entry id."
            );
        }
    }

    private void TestResolveAvailableSkillEntriesFiltersUnavailablePreferredSkills()
    {
        StringName availableSkillId = "ai_available_skill";
        StringName unavailableSkillId = "ai_unavailable_skill";
        BattleUnitState actor = BuildUnit("availability_actor", "hostile", new Vector2I(0, 0));
        actor.known_active_skill_ids.Add(availableSkillId);
        actor.SetKnownSkillLevelTyped(availableSkillId, 2);

        BattleAiContext context = new()
        {
            unit_state = actor,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [availableSkillId] = BuildUnitSkill(availableSkillId, rangeValue: 4),
                [unavailableSkillId] = BuildUnitSkill(unavailableSkillId, rangeValue: 4),
            }
        );

        List<BattleAvailableSkillEntry> entries = new BattleAiTypedActionHelper()
            .ResolveAvailableSkillEntries(
                context,
                new List<StringName> { unavailableSkillId, availableSkillId }
            );

        _test.Eq(entries.Count, 1, "Unavailable preferred skills should be filtered out.");
        if (entries.Count == 0)
            return;
        _test.Eq(
            entries[0].EntryRef.SkillId,
            availableSkillId,
            "Available preferred skill should be preserved."
        );
        _test.Eq(
            entries[0].EntryRef.SkillEntryId,
            BattleSkillEntryIds.KnownSkill(availableSkillId),
            "Available entry should carry the known-skill entry id."
        );
        _test.Eq(entries[0].SkillLevel, 2, "Available entry should preserve known skill level.");
    }

    private void TestAuthoredEnemyActionsResolveAvailabilityEntriesBeforeBuildingSkillCommands()
    {
        AssertSourceDoesNotContain(
            "scripts/enemies/actions/UseGroundRepositionSkillAction.cs",
            "_resolve_known_skill_ids",
            "UseGroundRepositionSkillAction should resolve BattleAvailableSkillEntry values, not raw known skill ids."
        );
        AssertSourceDoesNotContain(
            "scripts/enemies/actions/UseGroundRepositionSkillAction.cs",
            "_build_typed_ground_skill_command(\n                        context,\n                        sid,",
            "UseGroundRepositionSkillAction should build ground commands from the selected skill entry."
        );
        AssertSourceDoesNotContain(
            "scripts/enemies/actions/WaitAction.cs",
            "foreach (var rsi in us.known_active_skill_ids)",
            "WaitAction should evaluate acting-unit skills through availability entries."
        );
        AssertSourceDoesNotContain(
            "scripts/enemies/actions/WaitAction.cs",
            "_build_unit_skill_command(\n                context,\n                skillDefinition.SkillId,",
            "WaitAction should build preview commands from the selected skill entry."
        );
        AssertSourceDoesNotContain(
            "scripts/enemies/EnemyAiAction.cs",
            "skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId)",
            "EnemyAiAction raw skill-id command helpers should validate through availability before stamping entries."
        );
    }

    private void TestEvaluatorGeneratedCommandCarriesAvailableEntryId()
    {
        StringName skillId = "ai_entry_unit_skill";
        BattleUnitState actor = BuildUnit("entry_actor", "hostile", new Vector2I(0, 0));
        BattleUnitState target = BuildUnit("entry_target", "player", new Vector2I(1, 0));
        actor.known_active_skill_ids.Add(skillId);
        actor.SetKnownSkillLevelTyped(skillId, 1);

        SkillDefinition skill = BuildUnitSkill(skillId, rangeValue: 4);
        BattleState state = new()
        {
            battle_id = "unit_skill_entry_regression",
            phase = "unit_acting",
            map_size = new Vector2I(8, 2),
            timeline = new BattleTimelineState(),
            active_unit_id = actor.unit_id,
        };
        state.SetUnit(actor);
        state.SetUnit(target);

        BattleAiContext context = new()
        {
            state = state,
            unit_state = actor,
            grid_service = new BattleGridService(),
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skill.SkillId] = skill,
            }
        );

        UseUnitSkillActionDefinition action = new(
            "entry_unit_action",
            "test",
            BattleAiActionIntent.Positioning,
            new[] { skillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            4,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            action,
            context
        );
        BattleCommand command = decision?.command;
        _test.True(command != null, "Available unit skill should produce a command.");
        if (command == null)
            return;
        _test.Eq(command.skill_id, skillId, "Candidate command should preserve skill_id.");
        _test.Eq(
            command.skill_entry_id,
            BattleSkillEntryIds.KnownSkill(skillId),
            "Candidate command should carry the selected availability entry id."
        );
    }

    private void TestFastPreviewRejectsExposeOutOfRangeCounter()
    {
        StringName skillId = "ai_preview_range_probe";
        BattleUnitState actor = BuildUnit("preview_range_actor", "hostile", new Vector2I(0, 0));
        BattleUnitState target = BuildUnit("preview_range_target", "player", new Vector2I(5, 0));
        actor.known_active_skill_ids.Add(skillId);

        SkillDefinition skill = BuildUnitSkill(skillId, rangeValue: 1);
        BattleState state = new()
        {
            battle_id = "unit_skill_preview_counter_regression",
            phase = "unit_acting",
            map_size = new Vector2I(8, 2),
            timeline = new BattleTimelineState(),
            active_unit_id = actor.unit_id,
        };
        state.SetUnit(actor);
        state.SetUnit(target);

        BattleAiContext context = new()
        {
            state = state,
            unit_state = actor,
            grid_service = new BattleGridService(),
            trace_enabled = true,
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skill.SkillId] = skill,
            }
        );

        UseUnitSkillActionDefinition action = new(
            "preview_range_action",
            "test",
            BattleAiActionIntent.Positioning,
            new[] { skillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            1,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            action,
            context
        );
        _test.True(decision == null, "超出 fast preview 射程时不应生成 unit-skill 决策。");

        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "trace_enabled 时 evaluator 应记录 action trace。");
        if (traces.Count == 0)
            return;

        AiActionTrace trace = traces[0];
        _test.Eq(trace.EvaluationCount, 1, "单技能单目标应评估一次。");
        _test.Eq(trace.PreviewRejectCount, 1, "fast preview 拒绝仍应计入 preview_reject_count 总数。");
        _test.True(
            trace.CandidateTraceCounters.TryGetValue(
                "fast_preview_reject_out_of_range",
                out int outOfRangeCount
            ),
            "fast preview 射程拒绝应写入细分 counter。"
        );
        _test.Eq(outOfRangeCount, 1, "fast preview 射程拒绝细分 counter 应与总拒绝数一致。");
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            current_hp = 20,
            current_ap = 2,
            current_mp = 10,
            current_stamina = 10,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        unit.RefreshFootprint();
        return unit;
    }

    private void AssertSourceDoesNotContain(
        string virtualPath,
        string forbiddenSnippet,
        string message
    )
    {
        string absolutePath = ProjectSettings.GlobalizePath($"res://{virtualPath}");
        string source = NormalizeNewlines(File.ReadAllText(absolutePath));
        string snippet = NormalizeNewlines(forbiddenSnippet);
        _test.True(!source.Contains(snippet, StringComparison.Ordinal), message);
    }

    private static string NormalizeNewlines(string text) =>
        (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);

    private static SkillDefinition BuildUnitSkill(StringName skillId, int rangeValue) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            skillId.ToString(),
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: rangeValue
            )
        );

}
