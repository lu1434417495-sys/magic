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
            source.AddKnownActiveSkill(skillId);
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
            TestNonContiguousTargetPairIsReachable();
            TestSeparableSkillPicksTopPayoffTargets();
            TestOrderedTargetSlotsEnumerateRepeatedTargetsAndScoreActualCost();
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
        source.AddKnownActiveSkill(skillId);
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
        // The fully blocked pair {blockedA, blockedB} must not consume the group budget and win.
        // Which single blocked target pads the winning pair is not asserted: combination
        // enumeration makes {blockedA, valid} reachable alongside {blockedB, valid}, and the score
        // model rates them identically because both land exactly one effective target.
        _test.True(
            !CommandContainsTargetUnitId(command, blockedTargetA.unit_id)
                || !CommandContainsTargetUnitId(command, blockedTargetB.unit_id),
            "the fully blocked candidate group should not consume the canonical group limit"
        );
    }

    private void TestNonContiguousTargetPairIsReachable()
    {
        StringName skillId = "non_contiguous_probe";
        var source = _runtimeScope.OwnWrapper(
            BuildUnit("non_contiguous_actor", "hostile", new Vector2I(0, 0)),
            "non-contiguous-source"
        );
        // Nearest-first pool order is [first, skipped, third, last]. Only the first and third are
        // effective, so the best pair is non-contiguous and a sliding window can never build it.
        var firstTarget = _runtimeScope.OwnWrapper(
            BuildUnit("non_contiguous_first", "player", new Vector2I(1, 0)),
            "non-contiguous-first"
        );
        var skippedTarget = _runtimeScope.OwnWrapper(
            BuildUnit("non_contiguous_skipped", "player", new Vector2I(2, 0)),
            "non-contiguous-skipped"
        );
        var thirdTarget = _runtimeScope.OwnWrapper(
            BuildUnit("non_contiguous_third", "player", new Vector2I(3, 0)),
            "non-contiguous-third"
        );
        var lastTarget = _runtimeScope.OwnWrapper(
            BuildUnit("non_contiguous_last", "player", new Vector2I(4, 0)),
            "non-contiguous-last"
        );
        source.AddKnownActiveSkill(skillId);
        source.SetKnownSkillLevelTyped(skillId, 1);

        BattleState state = _runtimeScope.OwnWrapper(
            new BattleState
            {
                battle_id = "multi_unit_non_contiguous_pair",
                phase = "unit_acting",
                map_size = new Vector2I(8, 2),
                active_unit_id = source.unit_id,
                timeline = new BattleTimelineState(),
            },
            "non-contiguous-state"
        );
        state.SetUnit(source);
        state.SetUnit(firstTarget);
        state.SetUnit(skippedTarget);
        state.SetUnit(thirdTarget);
        state.SetUnit(lastTarget);
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "non_contiguous_field",
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
            // Only the non-contiguous pair lands anything. Every other pair previews as
            // fully blocked, so the evaluator can only produce a command if enumeration
            // actually builds {first, third}.
            preview_command_callback = command =>
            {
                var preview = new BattlePreview { allowed = true };
                if (
                    CommandContainsTargetUnitId(command, firstTarget.unit_id)
                    && CommandContainsTargetUnitId(command, thirdTarget.unit_id)
                )
                {
                    preview.AddTargetUnitId(firstTarget.unit_id);
                    preview.AddTargetUnitId(thirdTarget.unit_id);
                }
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
            "non_contiguous_action",
            "test",
            BattleAiActionIntent.Positioning,
            new[] { skillId },
            "nearest_enemy",
            0,
            6,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit),
            4,
            12
        );

        BattleAiDecision decision = new BattleAiMultiUnitSkillEvaluator().Evaluate(action, context);
        BattleCommand command = decision?.command;
        _test.True(command != null, "a non-contiguous target pair should still produce a command");
        if (command == null)
            return;
        _test.Eq(
            command.TargetUnitIdsTyped.Count,
            2,
            "min_target_count=2 should preserve a legal command shape"
        );
        var chosenIds = new List<string>();
        foreach (StringName targetId in command.TargetUnitIdsTyped)
            chosenIds.Add(targetId.ToString());
        _test.Eq(
            string.Join(",", chosenIds),
            $"{firstTarget.unit_id},{thirdTarget.unit_id}",
            "the two effective targets are not adjacent in the pool, so only combination "
                + "enumeration can pair them; a sliding window caps out at one effective target"
        );
    }

    private void TestSeparableSkillPicksTopPayoffTargets()
    {
        RunTopPayoffScenario("damage", expectSeparable: true, idPrefix: "separable");
        // A coupling effect kind is not on the allow-list, so the same board must fall back to
        // enumeration - whose group budget runs out before the best pair.
        RunTopPayoffScenario("chain_damage", expectSeparable: false, idPrefix: "coupled");
    }

    private void RunTopPayoffScenario(string effectType, bool expectSeparable, string idPrefix)
    {
        StringName skillId = $"{idPrefix}_probe";
        var source = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_actor", "hostile", new Vector2I(0, 0)),
            $"{idPrefix}-source"
        );
        // Six targets in nearest-first order, with the only two worthwhile ones last. Pairs are
        // C(6,2)=15 but the group budget is 12, so lexicographic combination enumeration stops
        // before reaching {index 4, index 5}. Ranking by payoff finds it directly.
        var poor0 = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_poor_0", "player", new Vector2I(1, 0)),
            $"{idPrefix}-poor-0"
        );
        var poor1 = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_poor_1", "player", new Vector2I(2, 0)),
            $"{idPrefix}-poor-1"
        );
        var poor2 = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_poor_2", "player", new Vector2I(3, 0)),
            $"{idPrefix}-poor-2"
        );
        var poor3 = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_poor_3", "player", new Vector2I(4, 0)),
            $"{idPrefix}-poor-3"
        );
        var goodA = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_good_a", "player", new Vector2I(5, 0)),
            $"{idPrefix}-good-a"
        );
        var goodB = _runtimeScope.OwnWrapper(
            BuildUnit($"{idPrefix}_good_b", "player", new Vector2I(6, 0)),
            $"{idPrefix}-good-b"
        );
        source.AddKnownActiveSkill(skillId);
        source.SetKnownSkillLevelTyped(skillId, 1);

        BattleState state = _runtimeScope.OwnWrapper(
            new BattleState
            {
                battle_id = $"multi_unit_{idPrefix}_top_k",
                phase = "unit_acting",
                map_size = new Vector2I(8, 2),
                active_unit_id = source.unit_id,
                timeline = new BattleTimelineState(),
            },
            $"{idPrefix}-state"
        );
        state.SetUnit(source);
        state.SetUnit(poor0);
        state.SetUnit(poor1);
        state.SetUnit(poor2);
        state.SetUnit(poor3);
        state.SetUnit(goodA);
        state.SetUnit(goodB);

        using var scoreService = new BattleAiScoreService();
        var context = new BattleAiContext
        {
            state = state,
            unit_state = source,
            grid_service = new BattleGridService(),
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            preview_command_callback = command =>
            {
                var preview = new BattlePreview { allowed = true };
                foreach (StringName targetUnitId in command.TargetUnitIdsTyped)
                    preview.AddTargetUnitId(targetUnitId);
                return preview;
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skillId] = BuildMultiUnitSkill(
                    skillId,
                    2,
                    2,
                    withDamageEffect: true,
                    effectType: effectType
                ),
            }
        );
        context.skill_score_input_callback = (
            aiContext,
            skillDefinition,
            command,
            preview,
            effects,
            metadata,
            facts
        ) =>
        {
            if (command != null && preview != null)
            {
                bool hitsOnlyGoodTargets =
                    !CommandContainsTargetUnitId(command, poor0.unit_id)
                    && !CommandContainsTargetUnitId(command, poor1.unit_id)
                    && !CommandContainsTargetUnitId(command, poor2.unit_id)
                    && !CommandContainsTargetUnitId(command, poor3.unit_id);
                preview.hit_preview = BuildHitPreview(hitsOnlyGoodTargets ? 95 : 5);
            }
            return scoreService.BuildSkillScoreInput(
                aiContext,
                skillDefinition,
                command,
                preview,
                effects,
                metadata,
                facts
            );
        };

        var action = new UseMultiUnitSkillActionDefinition(
            $"{idPrefix}_action",
            "test",
            BattleAiActionIntent.Positioning,
            new[] { skillId },
            "nearest_enemy",
            0,
            6,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit),
            6,
            12
        );

        BattleAiDecision decision = new BattleAiMultiUnitSkillEvaluator().Evaluate(action, context);
        BattleCommand command2 = decision?.command;
        _test.True(command2 != null, "a separable multi-unit skill should produce a command");
        if (command2 == null)
            return;
        bool pickedBestPair =
            CommandContainsTargetUnitId(command2, goodA.unit_id)
            && CommandContainsTargetUnitId(command2, goodB.unit_id);
        _test.Eq(
            pickedBestPair,
            expectSeparable,
            expectSeparable
                ? "ranking by payoff should reach the best pair even though it sits past the "
                    + "point where combination enumeration exhausts its group budget"
                : "a coupling effect kind must not be treated as separable; enumeration cannot "
                    + "reach this pair, and silently taking the top-k path would hide that"
        );
        _test.Eq(
            command2.TargetUnitIdsTyped.Count,
            2,
            "min and max target count are both two"
        );
    }

    private static CombatEffectDefinition BuildDamageEffect(string effectType = "damage") =>
        new(
            effectType: effectType,
            effectTargetTeamFilter: "enemy",
            statusId: default,
            saveFailureStatusId: default,
            terrainEffectId: default,
            terrainReplaceTo: default,
            heightDelta: 0,
            requiresWeapon: false,
            addWeaponDice: false,
            preventRepeatTarget: false,
            forcedMoveMode: default,
            minSkillLevel: 0,
            maxSkillLevel: -1,
            damageTag: default,
            damageRatioPercent: 100,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: default,
            hpRatioThresholdPercent: 0,
            damageCategory: default,
            drBypassTag: default,
            diceCount: 2,
            diceSides: 6,
            diceBonus: 0,
            bonusDamageDiceCount: 0,
            bonusDamageDiceSides: 0,
            bonusDamageDiceBonus: 0,
            saveDc: 0,
            saveDcMode: default,
            saveDcSourceAbility: default,
            saveAbility: default,
            savePartialOnSuccess: false,
            saveTag: default,
            thresholdBaseValue: 0,
            thresholdLevelAnchor: 0,
            thresholdLevelBonusPerDelta: 0,
            thresholdMaxHpRatioPercent: 0,
            thresholdCapMaxHpRatioPercent: 0,
            soulFractureDurationTu: 0,
            healMultiplierPercent: 0,
            shieldGainMultiplierPercent: 0,
            appliedStatusDurationTu: 0,
            durationTu: 0,
            tickIntervalTu: 0,
            effectTags: Array.Empty<StringName>()
        );

    private static AttackPreviewData BuildHitPreview(int successRate) =>
        new()
        {
            Stages = new List<AttackPreviewStage>
            {
                new(successRate, successRate, successRate, 0, 0, ""),
            },
            HitRatePercent = successRate,
            SuccessRatePercent = successRate,
            BaseHitRatePercent = successRate,
        };

    private void TestOrderedTargetSlotsEnumerateRepeatedTargetsAndScoreActualCost()
    {
        SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "mage_arcane_missile",
            "enemy_multi_unit_skill_ordered_slots"
        );
        BattleUnitState source = _runtimeScope.OwnWrapper(
            BuildUnit("ordered_slot_actor", "hostile", new Vector2I(0, 0)),
            "ordered-slot-source"
        );
        BattleUnitState targetA = _runtimeScope.OwnWrapper(
            BuildUnit("ordered_slot_target_a", "player", new Vector2I(2, 0)),
            "ordered-slot-target-a"
        );
        BattleUnitState targetB = _runtimeScope.OwnWrapper(
            BuildUnit("ordered_slot_target_b", "player", new Vector2I(3, 0)),
            "ordered-slot-target-b"
        );
        source.AddKnownActiveSkill(skill.SkillId);
        source.SetKnownSkillLevelTyped(skill.SkillId, 10, preserveZero: true);
        source.SetCurrentMp(100);
        source.SetCurrentStamina(100);

        BattleState state = _runtimeScope.OwnWrapper(
            new BattleState
            {
                battle_id = "ordered_target_slot_ai",
                phase = "unit_acting",
                map_size = new Vector2I(8, 2),
                active_unit_id = source.unit_id,
                timeline = new BattleTimelineState(),
            },
            "ordered-slot-state"
        );
        state.SetUnit(source);
        state.SetUnit(targetA);
        state.SetUnit(targetB);
        var context = new BattleAiContext
        {
            state = state,
            unit_state = source,
            grid_service = new BattleGridService(),
            skill_cast_block_reason_callback = (_, _) =>
                BattleSkillCastBlockReasonKind.None,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skill.SkillId] = skill,
            }
        );
        var action = new UseMultiUnitSkillActionDefinition(
            "ordered_slot_action",
            "test",
            BattleAiActionIntent.Offense,
            new[] { skill.SkillId },
            "nearest_enemy",
            0,
            5,
            EnemyAiDistanceReferences.ToStringName(
                EnemyAiDistanceReference.TargetUnit
            ),
            2,
            12
        );

        BattleAiDecision decision = new BattleAiMultiUnitSkillEvaluator().Evaluate(
            action,
            context
        );
        BattleCommand command = decision?.command;
        _test.True(command != null, "ordered_slots AI 应枚举出可用候选。" );
        if (command == null)
            return;
        _test.Eq(command.TargetUnitIdsTyped.Count, 6, "10级AI候选应允许编排6发。" );
        _test.True(
            command.TargetUnitIdsTyped[0] == command.TargetUnitIdsTyped[1],
            "AI必须枚举向同一目标重复分配飞弹的集中火力候选。"
        );

        var preview = new BattlePreview { allowed = true };
        foreach (StringName targetUnitId in command.TargetUnitIdsTyped)
            preview.AddTargetUnitId(targetUnitId);
        using var scoreService = new BattleAiScoreService();
        BattleAiScoreInput scoreInput = scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            skill.CombatProfile.EffectDefinitions,
            new Dictionary<string, object>(StringComparer.Ordinal)
        );
        _test.True(scoreInput != null, "ordered_slots 应能进入通用AI评分。" );
        _test.Eq(scoreInput?.mp_cost ?? -1, 30, "AI评分必须按6发计算30法力。" );
        _test.Eq(
            scoreInput?.stamina_cost ?? -1,
            42,
            "AI评分必须按6发计算42体力。"
        );
        _test.Eq(scoreInput?.target_count ?? -1, 6, "AI评分必须保留6个有序伤害槽位。" );

        StringName singleTargetId = command.TargetUnitIdsTyped[0];
        var singleCommand = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = source.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skill.SkillId),
            skill_id = skill.SkillId,
            target_unit_id = singleTargetId,
        };
        singleCommand.SetTargetUnitIds(new[] { singleTargetId });
        var singlePreview = new BattlePreview { allowed = true };
        singlePreview.AddTargetUnitId(singleTargetId);
        BattleAiScoreInput singleScoreInput = scoreService.BuildSkillScoreInput(
            context,
            skill,
            singleCommand,
            singlePreview,
            skill.CombatProfile.EffectDefinitions,
            new Dictionary<string, object>(StringComparer.Ordinal)
        );
        _test.True(
            singleScoreInput != null
                && scoreInput != null
                && scoreInput.estimated_enemy_damage
                    == singleScoreInput.estimated_enemy_damage * 6,
            "AI伤害评分必须按每个有序飞弹槽位线性累计，重复目标不得被去重。"
        );
        BattleTestFixture.DisposeBattlePreview(singlePreview);
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: 20,
            mp: 10,
            stamina: 10,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        unit.SetAnchorCoord(coord);
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
        int maxTargetCount = 2,
        bool withDamageEffect = false,
        string effectType = "damage"
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
            withDamageEffect
                ? new[] { BuildDamageEffect(effectType) }
                : Array.Empty<CombatEffectDefinition>(),
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
            new Dictionary<int, SkillDescriptionVariables>(),
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
                new Dictionary<int, CombatSkillLevelOverrideImportModel>(),
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
