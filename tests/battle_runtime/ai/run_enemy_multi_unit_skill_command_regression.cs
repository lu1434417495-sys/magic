using Godot;
using System;
using System.Collections.Generic;

public partial class run_enemy_multi_unit_skill_command_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly GodotTransientResourceScope _runtimeScope =
        new("enemy_multi_unit_skill_command");

    public override void _Initialize()
    {
        try
        {
            var source = _runtimeScope.OwnWrapper(
                BuildUnit("enemy_1", "hostile", new Vector2I(0, 0)),
                "source"
            );
            var targetA = _runtimeScope.OwnWrapper(
                BuildUnit("hero_1", "player", new Vector2I(2, 0)),
                "target-a"
            );
            var targetB = _runtimeScope.OwnWrapper(
                BuildUnit("hero_2", "player", new Vector2I(3, 0)),
                "target-b"
            );
            StringName skillId = "enemy_chain";
            source.known_active_skill_ids.Add(skillId);
            BattleState state = _runtimeScope.OwnWrapper(
                new BattleState
                {
                    battle_id = "enemy_multi_unit_skill_command",
                    phase = "unit_acting",
                    map_size = new Vector2I(8, 2),
                    active_unit_id = source.unit_id,
                    timeline = new BattleTimelineState(),
                },
                "state"
            );
            state.SetUnit(source);
            state.SetUnit(targetA);
            state.SetUnit(targetB);
            var context = new BattleAiContext
            {
                state = state,
                unit_state = source,
                grid_service = new BattleGridService(),
                skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            };
            context.SetSkillDefinitions(
                new Dictionary<StringName, SkillDefinition>
                {
                    [skillId] = BuildMultiUnitSkill(skillId),
                }
            );
            UseMultiUnitSkillAction action = TestResourceOwnership.Own(
                new UseMultiUnitSkillAction
                {
                    action_id = "multi_unit_command_regression",
                    score_bucket_id = "test",
                    target_selector = "nearest_enemy",
                    desired_min_distance = 0,
                    desired_max_distance = 6,
                    distance_reference = "target_unit",
                },
                "multi-unit-action"
            );
            action.skill_ids.Add(skillId);

            BattleAiDecision decision = new BattleAiMultiUnitSkillEvaluator().Evaluate(
                (UseMultiUnitSkillActionDefinition)action.ToDefinition(),
                context
            );
            BattleCommand command = decision?.command;
            _test.True(command != null, "command was null");
            if (command != null)
            {
                _test.Eq(command.skill_id, skillId, "skill id was preserved");
                _test.Eq(command.skill_variant_id, new StringName("multi"), "variant id was preserved");
                _test.Eq(command.TargetUnitIdsTyped.Count, 2, "expected 2 target ids");
                _test.Eq(
                    command.TargetUnitIdsTyped[0],
                    new StringName("hero_1"),
                    "first target id was preserved"
                );
                _test.Eq(
                    command.TargetUnitIdsTyped[1],
                    new StringName("hero_2"),
                    "second target id was preserved"
                );
            }

            TestBarrierBlockedCandidateDoesNotConsumePoolLimit();
        }
        catch (Exception exception)
        {
            _test.Fail(exception.ToString());
        }
        finally
        {
            _runtimeScope.Close();
        }

        RequestTestExit(_test.Finish("enemy multi-unit skill candidate pool regression"));
    }

    private void TestBarrierBlockedCandidateDoesNotConsumePoolLimit()
    {
        StringName skillId = "barrier_pool_probe";
        var source = _runtimeScope.OwnWrapper(
            BuildUnit("barrier_pool_actor", "hostile", new Vector2I(0, 0)),
            "barrier-pool-source"
        );
        var blockedTargetA = _runtimeScope.OwnWrapper(
            BuildUnit("barrier_pool_blocked_a", "player", new Vector2I(1, 0)),
            "barrier-pool-blocked-target-a"
        );
        var blockedTargetB = _runtimeScope.OwnWrapper(
            BuildUnit("barrier_pool_blocked_b", "player", new Vector2I(2, 0)),
            "barrier-pool-blocked-target-b"
        );
        var validTarget = _runtimeScope.OwnWrapper(
            BuildUnit("barrier_pool_valid", "player", new Vector2I(3, 0)),
            "barrier-pool-valid-target"
        );
        source.known_active_skill_ids.Add(skillId);
        source.SetKnownSkillLevelTyped(skillId, 1);

        BattleState state = _runtimeScope.OwnWrapper(
            new BattleState
            {
                battle_id = "multi_unit_barrier_candidate_pool",
                phase = "unit_acting",
                map_size = new Vector2I(8, 2),
                active_unit_id = source.unit_id,
                timeline = new BattleTimelineState(),
            },
            "barrier-pool-state"
        );
        state.SetUnit(source);
        state.SetUnit(blockedTargetA);
        state.SetUnit(blockedTargetB);
        state.SetUnit(validTarget);
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "barrier_pool_field",
            ProfileId = "prismatic_sphere",
            RemainingTu = 40,
        };
        barrier.SetLayers(
            new[]
            {
                new BattleBarrierLayerState
                {
                    LayerId = "indigo",
                    DisplayName = "Indigo",
                    Order = 6,
                },
            }
        );
        state.PutLayeredBarrierField(barrier.BarrierInstanceId, barrier);

        var context = new BattleAiContext
        {
            state = state,
            unit_state = source,
            grid_service = new BattleGridService(),
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            preview_command_callback = command =>
            {
                var preview = new BattlePreview { allowed = true };
                if (CommandContainsTargetUnitId(command, validTarget.unit_id))
                    preview.AddTargetUnitId(validTarget.unit_id);
                return preview;
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skillId] = BuildMultiUnitSkill(skillId, 2, 2),
            }
        );
        var action = new UseMultiUnitSkillActionDefinition(
            "barrier_pool_action",
            "test",
            BattleAiActionIntent.Positioning,
            new[] { skillId },
            "nearest_enemy",
            0,
            6,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit),
            2,
            1
        );

        BattleAiDecision decision = new BattleAiMultiUnitSkillEvaluator().Evaluate(action, context);
        BattleCommand command = decision?.command;
        _test.True(
            command != null,
            "a valid target behind the blocked pool candidate should remain selectable"
        );
        if (command == null)
            return;
        _test.Eq(
            command.TargetUnitIdsTyped.Count,
            2,
            "min_target_count=2 should preserve a legal command shape"
        );
        _test.True(
            CommandContainsTargetUnitId(command, validTarget.unit_id),
            "the later barrier-valid target should enter after the blocked pool prefix"
        );
        _test.True(
            !CommandContainsTargetUnitId(command, blockedTargetA.unit_id),
            "the first fully blocked candidate group should not consume the canonical group limit"
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        var unit = new BattleUnitState
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

    private static bool CommandContainsTargetUnitId(BattleCommand command, StringName targetUnitId)
    {
        foreach (
            StringName candidateUnitId in command?.TargetUnitIdsTyped
                ?? Array.Empty<StringName>()
        )
        {
            if (candidateUnitId == targetUnitId)
                return true;
        }
        return false;
    }

    private static SkillDefinition BuildMultiUnitSkill(
        StringName skillId,
        int minTargetCount = 2,
        int maxTargetCount = 2
    )
    {
        var castVariant = new CombatCastVariantDefinition(
            "multi",
            "Multi",
            "",
            0,
            "unit",
            "single",
            1,
            Array.Empty<StringName>(),
            Array.Empty<CombatEffectDefinition>(),
            new Dictionary<string, object>()
        );
        return new SkillDefinition(
            skillId,
            "Enemy Chain",
            "",
            "",
            "active",
            1,
            0,
            "",
            0,
            0,
            Array.Empty<int>(),
            Array.Empty<StringName>(),
            "book",
            Array.Empty<StringName>(),
            "standard",
            Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            false,
            "",
            Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            new CombatSkillDefinition(
                skillId,
                "unit",
                "enemy",
                "single",
                6,
                "single",
                0,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "",
                0,
                "",
                0,
                new Dictionary<int, IReadOnlyDictionary<string, object>>(),
                "",
                "",
                "",
                "",
                0,
                Array.Empty<int>(),
                0,
                "",
                "",
                0,
                "",
                "",
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                "",
                "multi_unit",
                minTargetCount,
                maxTargetCount,
                false,
                1,
                "",
                Array.Empty<CombatEffectDefinition>(),
                Array.Empty<CombatEffectDefinition>(),
                new[] { castVariant },
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                false,
                0,
                0
            )
        );
    }
}
