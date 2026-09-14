using System;
using System.Collections.Generic;
using Godot;

public partial class run_combat_effect_heal_floor_target_limiter_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSchemaAcceptsOnlyTypedFloorAndDeterministicLimiter();
            TestGroundPreviewExecutionAndAiShareLimitedTargets();
            TestHealFloorRoundsUpAndDoesNotOverhealFloor();
            TestMissingHpPercentHealingUsesCanonicalMathAndHealingReduction();
            TestGroundTargetPlanFreezesBeforeEarlierEffectsChangeHp();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(
            _test.Finish("Combat effect heal floor and target limiter regression")
        );
    }

    private void TestSchemaAcceptsOnlyTypedFloorAndDeterministicLimiter()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        var validEffect = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "ally",
            heal_to_hp_percent_floor = 50,
            max_affected_targets = 4,
            exclude_source = true,
            target_order = "lowest_hp_percent_then_unit_id",
        };
        var validErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            validErrors,
            "typed_floor_valid",
            validEffect,
            "test_effect"
        );
        _test.Eq(validErrors.Count, 0, "合法 typed floor + limiter 应通过正式 validator。");

        var invalidOrder = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "ally",
            heal_to_hp_percent_floor = 50,
            max_affected_targets = 4,
            target_order = "grid_order",
        };
        var invalidOrderErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            invalidOrderErrors,
            "typed_floor_bad_order",
            invalidOrder,
            "test_effect"
        );
        _test.True(
            ContainsError(invalidOrderErrors, "target_order"),
            "未知 target_order 必须被正式 validator 拒绝。"
        );

        var mixedHeal = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "ally",
            heal_to_hp_percent_floor = 50,
            power = 3,
        };
        var mixedHealErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            mixedHealErrors,
            "typed_floor_mixed_heal",
            mixedHeal,
            "test_effect"
        );
        _test.True(
            ContainsError(mixedHealErrors, "cannot be combined"),
            "百分比生命地板不得与 power/dice 治疗混配。"
        );

        var wrongKind = new CombatEffectDef
        {
            effect_type = "damage",
            effect_target_team_filter = "enemy",
            heal_to_hp_percent_floor = 50,
            power = 3,
        };
        var wrongKindErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            wrongKindErrors,
            "typed_floor_wrong_kind",
            wrongKind,
            "test_effect"
        );
        _test.True(
            ContainsError(wrongKindErrors, "only supported on heal"),
            "非 heal effect 不得声明 heal_to_hp_percent_floor。"
        );

        var validMissingHp = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "self",
            heal_missing_hp_percent = 60,
        };
        var validMissingHpErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            validMissingHpErrors,
            "typed_missing_hp_valid",
            validMissingHp,
            "test_effect"
        );
        _test.Eq(validMissingHpErrors.Count, 0, "合法损失生命百分比治疗应通过正式 validator。");

        var mixedPercentageHeal = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "self",
            heal_to_hp_percent_floor = 50,
            heal_missing_hp_percent = 60,
        };
        var mixedPercentageErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            mixedPercentageErrors,
            "typed_missing_hp_mixed",
            mixedPercentageHeal,
            "test_effect"
        );
        _test.True(
            ContainsError(mixedPercentageErrors, "mutually exclusive"),
            "生命地板与损失生命百分比治疗不得混配。"
        );

        var invalidMissingHp = new CombatEffectDef
        {
            effect_type = "damage",
            effect_target_team_filter = "enemy",
            heal_missing_hp_percent = 101,
        };
        var invalidMissingHpErrors = new Godot.Collections.Array<string>();
        validator.AppendEffectValidationErrors(
            invalidMissingHpErrors,
            "typed_missing_hp_invalid",
            invalidMissingHp,
            "test_effect"
        );
        _test.True(
            ContainsError(invalidMissingHpErrors, "between 0 and 100"),
            "损失生命治疗百分比必须限制在0到100。"
        );
        _test.True(
            ContainsError(invalidMissingHpErrors, "only supported on heal"),
            "非heal effect不得声明损失生命百分比治疗。"
        );

        validEffect.Dispose();
        invalidOrder.Dispose();
        mixedHeal.Dispose();
        wrongKind.Dispose();
        validMissingHp.Dispose();
        mixedPercentageHeal.Dispose();
        invalidMissingHp.Dispose();
    }

    private void TestGroundPreviewExecutionAndAiShareLimitedTargets()
    {
        using Fixture fixture = new("effect_limiter_shared_paths", 6);
        CombatEffectDefinition effect = BuildEffect(
            healToHpPercentFloor: 50,
            maxAffectedTargets: 4,
            excludeSource: true,
            targetOrder: "lowest_hp_percent_then_unit_id"
        );
        SkillDefinition skill = BuildGroundSkill("limited_group_heal", effect);
        BattleUnitState source = fixture.AddUnit("source", 5, 100, 0);
        BattleUnitState unitC = fixture.AddUnit("unit_c", 10, 100, 1);
        BattleUnitState unitB = fixture.AddUnit("unit_b", 20, 100, 2);
        BattleUnitState unitA = fixture.AddUnit("unit_a", 20, 100, 3);
        BattleUnitState unitE = fixture.AddUnit("unit_e", 40, 100, 4);
        BattleUnitState unitD = fixture.AddUnit("unit_d", 30, 100, 5);
        IReadOnlyList<Vector2I> effectCoords = fixture.AllCoords();

        IReadOnlyList<StringName> previewIds =
            fixture.Runtime.CollectGroundPreviewUnitIdsTyped(
                new BattleUnitReadView(source),
                skill,
                new[] { effect },
                effectCoords
            );
        _test.Eq(previewIds.Count, 4, "ground preview 不得高估 max_affected_targets。");
        _test.Eq(previewIds[0], new StringName("unit_c"), "最低生命百分比应排第一。");
        _test.Eq(previewIds[1], new StringName("unit_a"), "同百分比应按 unit_id ordinal 排序。");
        _test.Eq(previewIds[2], new StringName("unit_b"), "同百分比的第二个 unit_id 应稳定排序。");
        _test.Eq(previewIds[3], new StringName("unit_d"), "只应保留排序前四名。");

        BattleAiScoreInput score = BuildAiScore(
            fixture,
            source,
            skill,
            effect,
            new[]
            {
                source.unit_id,
                unitE.unit_id,
                unitD.unit_id,
                unitA.unit_id,
                unitB.unit_id,
                unitC.unit_id,
            },
            effectCoords
        );
        _test.True(score != null, "受限群体治疗应生成 AI score input。");
        if (score != null)
        {
            _test.Eq(score.target_count, 4, "AI target_count 必须自行截断过宽候选池。");
            _test.Eq(score.ally_target_count, 4, "AI 不得把第五名或施法者计为治疗目标。");
            _test.Eq(score.estimated_ally_healing, 120, "AI 应按四个冻结目标估算生命地板差值。");
        }

        using var batch = new BattleEventBatch();
        BattleGroundUnitEffectsResult result =
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, batch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                source,
                skill,
                null,
                new[] { effect },
                effectCoords,
                batch,
                effectCoords
            )
            );
        _test.True(result.Applied, "受限群体治疗应在正式 ground runtime 应用。");
        _test.Eq(result.AffectedUnitCount, 4, "runtime affected count 必须等于冻结的前四名。");
        _test.Eq(result.Healing, 120, "runtime healing 应等于四个生命地板正差值之和。");
        _test.Eq(source.GetCurrentHp(), 5, "exclude_source 必须排除施法者。");
        _test.Eq(unitC.GetCurrentHp(), 50, "第一名应治疗到 50%。");
        _test.Eq(unitA.GetCurrentHp(), 50, "unit_id tie-break 第一名应被治疗。");
        _test.Eq(unitB.GetCurrentHp(), 50, "unit_id tie-break 第二名应被治疗。");
        _test.Eq(unitD.GetCurrentHp(), 50, "第四名应被治疗。");
        _test.Eq(unitE.GetCurrentHp(), 40, "排序第五名不得受到该 effect。");
    }

    private void TestHealFloorRoundsUpAndDoesNotOverhealFloor()
    {
        using Fixture fixture = new("heal_floor_rounding", 2);
        CombatEffectDefinition effect = BuildEffect(
            healToHpPercentFloor: 50,
            excludeSource: true
        );
        SkillDefinition skill = BuildGroundSkill("rounding_heal", effect);
        BattleUnitState source = fixture.AddUnit("round_source", 100, 100, 0);
        BattleUnitState target = fixture.AddUnit("round_target", 40, 101, 1);
        IReadOnlyList<Vector2I> effectCoords = fixture.AllCoords();

        using var firstBatch = new BattleEventBatch();
        BattleGroundUnitEffectsResult first =
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, firstBatch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                source,
                skill,
                null,
                new[] { effect },
                effectCoords,
                firstBatch,
                effectCoords
            )
            );
        _test.Eq(target.GetCurrentHp(), 51, "101 max HP 的 50% 地板必须向上取整到 51。");
        _test.Eq(first.Healing, 11, "治疗量必须是 ceil(max*pct/100)-current 的正差值。");

        using var secondBatch = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, secondBatch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
            source,
            skill,
            null,
            new[] { effect },
            effectCoords,
            secondBatch,
            effectCoords
        )
            );
        _test.Eq(target.GetCurrentHp(), 51, "已达到生命地板时不得继续治疗。");
    }

    private void TestMissingHpPercentHealingUsesCanonicalMathAndHealingReduction()
    {
        using Fixture fixture = new("missing_hp_percent_heal", 2);
        CombatEffectDefinition effect = BuildEffect(
            healMissingHpPercent: 60,
            excludeSource: true
        );
        SkillDefinition skill = BuildGroundSkill("missing_hp_percent_heal", effect);
        BattleUnitState source = fixture.AddUnit("missing_source", 100, 100, 0);
        BattleUnitState target = fixture.AddUnit("missing_target", 20, 100, 1);
        IReadOnlyList<Vector2I> effectCoords = fixture.AllCoords();

        BattleAiScoreInput score = BuildAiScore(
            fixture,
            source,
            skill,
            effect,
            new[] { target.unit_id },
            effectCoords
        );
        _test.True(score != null, "损失生命百分比治疗应生成AI score input。");
        if (score != null)
            _test.Eq(score.estimated_ally_healing, 48, "AI应估算80点已损失生命的60%。");

        using var firstBatch = new BattleEventBatch();
        BattleGroundUnitEffectsResult first =
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, firstBatch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                source,
                skill,
                null,
                new[] { effect },
                effectCoords,
                firstBatch,
                effectCoords
            )
            );
        _test.Eq(first.Healing, 48, "20/100生命应恢复48点。");
        _test.Eq(target.GetCurrentHp(), 68, "20/100生命使用60%损失生命治疗后应为68。");

        target.SetCurrentHp(20);
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "missing_hp_heal_reduction",
                stacks = 1,
                duration = 60,
                heal_multiplier_percent = 50,
            }
        );
        using var reducedBatch = new BattleEventBatch();
        BattleGroundUnitEffectsResult reduced =
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, reducedBatch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                source,
                skill,
                null,
                new[] { effect },
                effectCoords,
                reducedBatch,
                effectCoords
            )
            );
        _test.Eq(reduced.Healing, 24, "治疗削减50%应作用于48点基础治疗。");
        _test.Eq(target.GetCurrentHp(), 44, "治疗削减后的实际生命应为44。");

        target.EraseStatusEffect("missing_hp_heal_reduction");
        target.SetCurrentHp(1);
        _test.Eq(
            BattleCombatEffectTargetRules.ResolveHealMissingHpPercentAmount(target, 60),
            59,
            "1/100生命应恢复floor(99*60%)=59。"
        );
        target.SetCurrentHp(50);
        _test.Eq(
            BattleCombatEffectTargetRules.ResolveHealMissingHpPercentAmount(target, 60),
            30,
            "50/100生命应恢复30。"
        );
    }

    private void TestGroundTargetPlanFreezesBeforeEarlierEffectsChangeHp()
    {
        using Fixture fixture = new("effect_limiter_frozen_plan", 3);
        CombatEffectDefinition preHeal = BuildEffect(
            power: 90,
            requiredTargetStatusId: "preheal_only"
        );
        CombatEffectDefinition limitedFloor = BuildEffect(
            healToHpPercentFloor: 50,
            maxAffectedTargets: 1,
            excludeSource: true,
            targetOrder: "lowest_hp_percent_then_unit_id"
        );
        SkillDefinition skill = BuildGroundSkill(
            "frozen_target_plan",
            preHeal,
            limitedFloor
        );
        BattleUnitState source = fixture.AddUnit("freeze_source", 100, 100, 0);
        BattleUnitState first = fixture.AddUnit("freeze_first", 10, 100, 1);
        BattleUnitState second = fixture.AddUnit("freeze_second", 20, 100, 2);
        first.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "preheal_only",
                power = 1,
                stacks = 1,
                duration = 10,
            }
        );
        IReadOnlyList<Vector2I> effectCoords = fixture.AllCoords();

        using var batch = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, batch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
            source,
            skill,
            null,
            new[] { preHeal, limitedFloor },
            effectCoords,
            batch,
            effectCoords
        )
            );
        _test.Eq(first.GetCurrentHp(), 100, "第一个 effect 应先把原最低血量目标治疗满。");
        _test.Eq(
            second.GetCurrentHp(),
            20,
            "第二个 effect 必须沿用提交前冻结候选，不得在前置治疗后改选目标。"
        );
    }

    private static CombatEffectDefinition BuildEffect(
        int power = 0,
        int healToHpPercentFloor = 0,
        int healMissingHpPercent = 0,
        int maxAffectedTargets = 0,
        bool excludeSource = false,
        StringName targetOrder = default,
        StringName requiredTargetStatusId = default
    )
    {
        var resource = new CombatEffectDef
        {
            effect_type = "heal",
            effect_target_team_filter = "ally",
            power = power,
            heal_to_hp_percent_floor = healToHpPercentFloor,
            heal_missing_hp_percent = healMissingHpPercent,
            max_affected_targets = maxAffectedTargets,
            exclude_source = excludeSource,
            target_order = targetOrder,
            required_target_status_id = requiredTargetStatusId,
        };
        CombatEffectDefinition definition = CombatEffectDefinition.FromDiagnosticFixture(
            resource,
            "test://combat_effect_heal_floor_target_limiter"
        );
        resource.Dispose();
        return definition;
    }

    private static SkillDefinition BuildGroundSkill(
        StringName skillId,
        params CombatEffectDefinition[] effects
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: "ground",
                targetTeamFilter: "ally",
                rangePattern: "radius",
                areaPattern: "radius",
                areaValue: 6
            )
        );
    }

    private static BattleAiScoreInput BuildAiScore(
        Fixture fixture,
        BattleUnitState source,
        SkillDefinition skill,
        CombatEffectDefinition effect,
        IReadOnlyList<StringName> previewIds,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        var preview = new BattlePreview { allowed = true };
        foreach (StringName previewId in previewIds)
        {
            preview.AddTargetUnitId(previewId);
        }
        foreach (Vector2I coord in effectCoords)
        {
            preview.AddTargetCoord(coord);
        }
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = source.unit_id,
            skill_id = skill.SkillId,
            target_coord = effectCoords[0],
        };
        command.AddTargetCoord(effectCoords[0]);
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = source,
            grid_service = fixture.Runtime._grid_service,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skill.SkillId] = skill,
            }
        );
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup(new BattleDamageResolver());
        return scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            new[] { effect },
            new Dictionary<string, object>(StringComparer.Ordinal)
        );
    }

    private static bool ContainsError(
        Godot.Collections.Array<string> errors,
        string fragment
    )
    {
        foreach (string error in errors ?? new Godot.Collections.Array<string>())
        {
            if (error?.Contains(fragment, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }
        return false;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly BattleRuntimeModule Runtime = new();
        internal readonly BattleState State;
        private readonly int _width;

        internal Fixture(string battleId, int width)
        {
            _width = width;
            Runtime.setup();
            State = new BattleState
            {
                battle_id = battleId,
                phase = "unit_acting",
                active_unit_id = "source",
                map_size = new Vector2I(width, 1),
                timeline = new BattleTimelineState(),
            };
            for (int x = 0; x < width; x++)
            {
                Vector2I coord = new(x, 0);
                State.SetCell(coord, new BattleCellState { coord = coord, passable = true });
            }
            State.RebuildCellColumns();
            Runtime.SetupStateForTests(State);
        }

        internal BattleUnitState AddUnit(
            StringName unitId,
            int currentHp,
            int maxHp,
            int x
        )
        {
            var unit = new BattleUnitState
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = "player",
            }.WithCombatResourcesForTest(hp: currentHp, isAlive: true);
            unit.SetAnchorCoord(new Vector2I(x, 0));
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.HpMax),
                maxHp
            );
            State.SetUnit(unit);
            State.ally_unit_ids.Add(unit.unit_id);
            if (!Runtime._grid_service.PlaceUnit(State, unit, unit.GetAnchorCoord(), true))
            {
                throw new InvalidOperationException($"Failed to place {unitId}.");
            }
            return unit;
        }

        internal IReadOnlyList<Vector2I> AllCoords()
        {
            var result = new List<Vector2I>(_width);
            for (int x = 0; x < _width; x++)
            {
                result.Add(new Vector2I(x, 0));
            }
            return result;
        }

        public void Dispose()
        {
            State.ClearUnits();
            State.ClearCells();
            Runtime.SetupStateForTests(null);
            Runtime.dispose();
        }
    }
}
