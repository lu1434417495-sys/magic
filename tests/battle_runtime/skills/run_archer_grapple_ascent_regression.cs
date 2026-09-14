using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_grapple_ascent_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "archer_grapple_redeploy";
    private static readonly StringName SkillId = "archer_grapple_redeploy";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestLevelCurveAndEffectWindows(skill);
            TestSchemaRejectsInvalidGrappleData();
            TestGridRulesAndReadViewParity(skill);
            TestRuntimePreviewExecutionAndCosts(skill);
            TestRuntimeGatesRejectBeforeCost(skill);
            TestAiRespectsLevelCapAndPrefersHigherLanding();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer grapple ascent regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "索钩登高正式资源与 combat_profile 应可加载。");
        if (combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "技能 ID 应保持 archer_grapple_redeploy。" );
        _test.Eq(skill.DisplayName, "索钩登高", "显示名应表达登高用途。" );
        _test.Eq(skill.MaxLevel, 5, "核心技能等级上限应为5。" );
        _test.Eq(skill.NonCoreMaxLevel, 3, "非核心等级上限应为3。" );
        _test.Eq(skill.MasteryCurve.Count, 5, "熟练度曲线应覆盖五级。" );
        _test.Eq(skill.MasteryCurve[0], 100, "1级熟练度阈值应为100。" );
        _test.Eq(skill.MasteryCurve[4], 1600, "5级熟练度阈值应为1600。" );
        _test.Eq(skill.GrowthTier, new StringName("basic"), "应属于基础成长档。" );
        _test.Eq(ReadGrowth(skill, "agility"), 40, "应提供40点敏捷成长。" );
        _test.Eq(ReadGrowth(skill, "perception"), 20, "应提供20点感知成长。" );
        _test.True(skill.Description.Contains("正交相邻"), "描述应明确只能选择正交相邻格。" );
        _test.True(skill.Description.Contains("至少2层"), "描述应明确最小高差。" );
        _test.True(skill.Description.Contains("不消耗移动力"), "描述应明确不消耗移动力。" );
        _test.True(skill.Description.Contains("不能斜向、平移或下降"), "描述应明确禁止的方向。" );

        _test.Eq(combat.TargetMode, new StringName("ground"), "技能必须选择地格。" );
        _test.Eq(combat.RangeValue, 1, "水平施放距离应固定为1格。" );
        _test.Eq(combat.ApCost, 1, "技能消耗1AP。" );
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "技能只允许一个武器家族。" );
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "武器家族必须为 bow。" );
        _test.False(combat.AllowsNaturalWeapon, "天生武器不得绕过装备弓要求。" );
        BattleUnitState rangeProbe = BuildArcher("grapple_range_probe", "player", Vector2I.Zero, 1);
        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(rangeProbe, skill),
            1,
            "即使装备4格弓，索钩水平射程也必须保持1格。"
        );
        BattleTestFixture.DisposeBattleUnit(rangeProbe);
        _test.Eq(combat.EffectDefinitions.Count, 3, "三个等级窗口都应由正式效果数据承载。" );
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            _test.Eq(effect.EffectKind, BattleEffectKind.ForcedMove, "索钩技能不应混入伤害或状态效果。" );
            _test.Eq(effect.ForcedMoveModeKind, BattleForcedMoveMode.GrappleAscent, "效果必须走 grapple_ascent 类型。" );
            _test.Eq(effect.ForcedMoveDistance, 1, "效果平面距离必须固定为1。" );
        }
    }

    private void TestLevelCurveAndEffectWindows(SkillDefinition skill)
    {
        AssertLevel(skill, 1, stamina: 28, cooldownTu: 80, maxHeightGain: 2);
        AssertLevel(skill, 2, stamina: 26, cooldownTu: 80, maxHeightGain: 2);
        AssertLevel(skill, 3, stamina: 26, cooldownTu: 80, maxHeightGain: 3);
        AssertLevel(skill, 4, stamina: 26, cooldownTu: 70, maxHeightGain: 3);
        AssertLevel(skill, 5, stamina: 26, cooldownTu: 70, maxHeightGain: 4);

        string levelOne = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            1,
            new GDictionary()
        );
        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new GDictionary()
        );
        _test.True(levelOne.Contains("高2至2层"), "1级说明应显示2层上限。" );
        _test.True(levelFive.Contains("高2至4层"), "5级说明应显示4层上限。" );
        _test.True(levelOne.Contains("28体力"), "1级说明应显示28体力。" );
        _test.True(levelFive.Contains("26体力"), "5级说明应显示26体力。" );
    }

    private void TestSchemaRejectsInvalidGrappleData()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using var invalid = new CombatEffectDef
        {
            effect_type = "forced_move",
            forced_move_mode = "grapple_ascent",
            forced_move_distance = 2,
            grapple_max_height_gain = 1,
        };
        using var profile = new CombatSkillDef
        {
            skill_id = "invalid_grapple",
            target_mode = "ground",
            range_value = 1,
        };
        profile.effect_defs.Add(invalid);
        var errors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(errors, "invalid_grapple", profile);
        _test.True(
            ErrorsContain(errors, "forced_move_distance = 1"),
            $"grapple_ascent 平面距离不是1时必须拒绝。errors={string.Join(" | ", errors)}"
        );
        _test.True(
            ErrorsContain(errors, "grapple_max_height_gain >= 2"),
            $"grapple_ascent 最大高差小于2时必须拒绝。errors={string.Join(" | ", errors)}"
        );
    }

    private void TestGridRulesAndReadViewParity(SkillDefinition skill)
    {
        var grid = new BattleGridService();
        BattleState state = BuildFlatState("grapple_grid_rules", new Vector2I(5, 5));
        BattleUnitState unit = BuildArcher("grapple_grid_archer", "player", new Vector2I(2, 2), 2);
        state.SetUnit(unit);
        _test.True(grid.PlaceUnit(state, unit, unit.GetAnchorCoord(), true), "测试弓手应能放入战场。" );

        Vector2I east = new(3, 2);
        Vector2I diagonal = new(3, 1);
        Vector2I twoCellsEast = new(4, 2);
        CombatEffectDefinition levelTwo = FindActiveGrappleEffect(skill, 2);
        CombatEffectDefinition levelThree = FindActiveGrappleEffect(skill, 3);
        CombatEffectDefinition levelFive = FindActiveGrappleEffect(skill, 5);

        SetHeight(grid, state, east, 2);
        _test.True(grid.CanGrappleAscent(state, unit, east, levelTwo), "L2 应允许攀登相邻+2层。" );
        _test.True(
            grid.CanGrappleAscent(state, new BattleUnitReadView(unit), east, levelTwo),
            "只读预览与可变运行态应对+2层得出相同结论。"
        );

        SetHeight(grid, state, east, 1);
        _test.False(grid.CanGrappleAscent(state, unit, east, levelTwo), "+1层低于最低高差，应拒绝。" );
        SetHeight(grid, state, east, 0);
        _test.False(grid.CanGrappleAscent(state, unit, east, levelTwo), "同高平移应拒绝。" );
        SetHeight(grid, state, east, -1);
        _test.False(grid.CanGrappleAscent(state, unit, east, levelTwo), "下降应拒绝。" );

        SetHeight(grid, state, diagonal, 2);
        _test.False(grid.CanGrappleAscent(state, unit, diagonal, levelFive), "斜向目标即使高度合法也应拒绝。" );
        SetHeight(grid, state, twoCellsEast, 2);
        _test.False(grid.CanGrappleAscent(state, unit, twoCellsEast, levelFive), "水平相距2格即使高度合法也应拒绝。" );

        SetHeight(grid, state, east, 3);
        _test.False(grid.CanGrappleAscent(state, unit, east, levelTwo), "L2 必须独立拒绝+3层。" );
        _test.True(grid.CanGrappleAscent(state, unit, east, levelThree), "L3 应允许+3层。" );
        SetHeight(grid, state, east, 4);
        _test.False(grid.CanGrappleAscent(state, unit, east, levelThree), "L3 应拒绝+4层。" );
        _test.True(grid.CanGrappleAscent(state, unit, east, levelFive), "L5 应允许+4层。" );

        _test.True(
            state.PutTemporaryEdgeFeature(
                BuildTemporaryWall(unit.GetAnchorCoord(), Vector2I.Right),
                refreshExisting: false,
                maxActiveEdges: 0
            ),
            "测试前提：临时墙体应可写入。"
        );
        _test.False(grid.CanGrappleAscent(state, unit, east, levelFive), "阻挡移动的墙边应阻挡索钩越过。" );
        state.ReplaceTemporaryEdgeFeaturesTyped(null);

        BattleUnitState blocker = BuildArcher("grapple_grid_blocker", "enemy", east, 1);
        state.SetUnit(blocker);
        _test.True(grid.PlaceUnit(state, blocker, east, true), "阻挡单位应能占据目标格。" );
        _test.False(grid.CanGrappleAscent(state, unit, east, levelFive), "被占据的落点应拒绝。" );

        BattleTestFixture.DisposeBattleFixture(null, state);
    }

    private void TestRuntimePreviewExecutionAndCosts(SkillDefinition skill)
    {
        BattleUnitState caster = BuildArcher("grapple_runtime_caster", "player", new Vector2I(1, 1), 3);
        BattleUnitState enemy = BuildArcher("grapple_runtime_enemy", "enemy", new Vector2I(4, 1), 1);
        using BattleTestFixture fixture = CreateFixture("grapple_runtime", skill, caster, enemy);
        Vector2I landing = new(2, 1);
        SetHeight(fixture.Runtime._grid_service, fixture.State, landing, 3);
        BattleCommand command = BuildCommand(caster, landing);

        int apBefore = caster.GetCurrentAp();
        int staminaBefore = caster.GetCurrentStamina();
        int moveBefore = caster.GetCurrentMovePoints();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"L3 +3层正式预览应允许。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
        );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 1), "预览不得移动施法者。" );
        _test.Eq(caster.GetCurrentAp(), apBefore, "预览不得消耗AP。" );
        _test.Eq(caster.GetCurrentStamina(), staminaBefore, "预览不得消耗体力。" );
        _test.Eq(caster.GetCurrentMovePoints(), moveBefore, "预览不得消耗移动力。" );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetAnchorCoord(), landing, "执行后应落在玩家选择的相邻高地。" );
        _test.Eq(caster.GetCurrentAp(), apBefore - 1, "执行应消耗1AP。" );
        _test.Eq(caster.GetCurrentStamina(), staminaBefore - 26, "L3 执行应消耗26体力。" );
        _test.Eq(caster.GetCurrentMovePoints(), moveBefore, "登高不得消耗移动力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 80, "L3 执行应进入80TU冷却。" );
        _test.False(caster.HasStatusEffect("archer_range_up"), "重做后不得残留旧射程提升状态。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestRuntimeGatesRejectBeforeCost(SkillDefinition skill)
    {
        TestRejectedRuntimeGate(skill, "wrong_weapon", useBow: false, rooted: false);
        TestRejectedRuntimeGate(skill, "rooted", useBow: true, rooted: true);
    }

    private void TestRejectedRuntimeGate(
        SkillDefinition skill,
        string suffix,
        bool useBow,
        bool rooted
    )
    {
        BattleUnitState caster = BuildArcher($"grapple_{suffix}_caster", "player", new Vector2I(1, 1), 1, useBow);
        BattleUnitState enemy = BuildArcher($"grapple_{suffix}_enemy", "enemy", new Vector2I(4, 1), 1);
        if (rooted)
        {
            caster.SetStatusEffect(
                new BattleStatusEffectState
                {
                    status_id = BattleStatusSemanticTable.STATUS_ROOTED,
                    duration = 20,
                    stacks = 1,
                }
            );
        }
        using BattleTestFixture fixture = CreateFixture($"grapple_{suffix}", skill, caster, enemy);
        Vector2I landing = new(2, 1);
        SetHeight(fixture.Runtime._grid_service, fixture.State, landing, 2);
        BattleCommand command = BuildCommand(caster, landing);
        int apBefore = caster.GetCurrentAp();
        int staminaBefore = caster.GetCurrentStamina();

        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.False(preview?.allowed == true, $"{suffix} 应在正式预览阶段拒绝。" );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), apBefore, $"{suffix} 拒绝不得消耗AP。" );
        _test.Eq(caster.GetCurrentStamina(), staminaBefore, $"{suffix} 拒绝不得消耗体力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{suffix} 拒绝不得启动冷却。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 1), $"{suffix} 拒绝不得移动单位。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiRespectsLevelCapAndPrefersHigherLanding()
    {
        TestAiLandingForLevel(2, new Vector2I(2, 1), "L2 AI 应拒绝+3层并选择合法的+2层北侧格。");
        TestAiLandingForLevel(3, new Vector2I(3, 2), "L3 AI 应在同等距离下优先选择+3层东侧格。");
    }

    private void TestAiLandingForLevel(int skillLevel, Vector2I expected, string message)
    {
        using var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            null
        );
        BattleState state = BuildFlatState($"grapple_ai_l{skillLevel}", new Vector2I(6, 5));
        BattleUnitState archer = BuildArcher($"grapple_ai_archer_l{skillLevel}", "hostile", new Vector2I(2, 2), skillLevel);
        archer.control_mode = "ai";
        BattleUnitState threat = BuildArcher($"grapple_ai_threat_l{skillLevel}", "player", new Vector2I(4, 0), 1);
        AddUnitToState(runtime, state, archer, isEnemy: true);
        AddUnitToState(runtime, state, threat, isEnemy: false);
        SetHeight(runtime._grid_service, state, new Vector2I(2, 1), 2);
        SetHeight(runtime._grid_service, state, new Vector2I(3, 2), 3);
        runtime.SetupStateForTests(state);

        UseGroundRepositionSkillActionDefinition action =
            TestEnemyDefinitionFactory.UseGroundRepositionSkill(
                $"grapple_ai_probe_l{skillLevel}",
                new StringName[] { SkillId },
                scoreBucketId: "archer_positioning",
                targetSelector: "nearest_enemy",
                positioningMode: "high_ground",
                minimumSafeDistance: 3,
                safeDistanceMargin: 1,
                desiredMaxDistanceBonus: 1,
                actionBaseScore: 1200,
                highGroundWeight: 180
            );

        BattleAiDecision decision = new BattleAiGroundRepositionActionEvaluator().Evaluate(
            action,
            BuildAiContext(runtime, archer)
        );
        _test.True(decision?.command != null, $"L{skillLevel} AI 应生成索钩登高命令。" );
        _test.Eq(decision?.command?.skill_id ?? new StringName(""), SkillId, "AI 应使用索钩登高。" );
        _test.Eq(decision?.command?.target_coord ?? new Vector2I(-1, -1), expected, message);
        BattlePreview preview = runtime.PreviewCommand(decision?.command);
        _test.True(preview?.allowed == true, $"L{skillLevel} AI 命令必须通过正式预览。" );

        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(decision?.command);
        BattleTestFixture.DisposeBattleFixture(runtime, state);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldownTu,
        int maxHeightGain
    )
    {
        CombatSkillResourceCosts costs = skill.CombatProfile.GetEffectiveResourceCostValues(level);
        CombatEffectDefinition effect = FindActiveGrappleEffect(skill, level);
        _test.Eq(costs.ApCost, 1, $"L{level} 应消耗1AP。" );
        _test.Eq(costs.StaminaCost, stamina, $"L{level} 体力消耗不符。" );
        _test.Eq(costs.CooldownTu, cooldownTu, $"L{level} 冷却不符。" );
        _test.True(effect != null, $"L{level} 应恰好投影一个有效索钩效果。" );
        _test.Eq(effect?.GrappleMaxHeightGain ?? -1, maxHeightGain, $"L{level} 最大攀高差不符。" );
        int activeCount = 0;
        foreach (CombatEffectDefinition candidate in skill.CombatProfile.EffectDefinitions)
        {
            if (candidate.IsUnlockedAtSkillLevel(level))
                activeCount++;
        }
        _test.Eq(activeCount, 1, $"L{level} 不得同时激活多个高度窗口。" );
    }

    private static CombatEffectDefinition FindActiveGrappleEffect(SkillDefinition skill, int level)
    {
        foreach (CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect?.ForcedMoveModeKind == BattleForcedMoveMode.GrappleAscent
                && effect.IsUnlockedAtSkillLevel(level)
            )
            {
                return effect;
            }
        }
        return null;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "archer_grapple_ascent_regression"
        );

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState enemy
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(6, 4),
            new[] { caster },
            new[] { enemy }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        return fixture;
    }

    private static BattleTemporaryEdgeFeatureState BuildTemporaryWall(
        Vector2I originCoord,
        Vector2I direction
    )
    {
        return new BattleTemporaryEdgeFeatureState
        {
            OriginCoord = originCoord,
            Direction = direction,
            BindingId = "grapple_ascent_test_wall",
            ActionId = "grapple_ascent_test_wall",
            CreatedAtTu = 0,
            ExpiresAtTu = 100,
            Feature = BattleEdgeFeatureState.MakeWall(),
        };
    }

    private static BattleState BuildFlatState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "timeline_running",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        for (int x = 0; x < mapSize.X; x++)
        {
            var cell = new BattleCellState
            {
                coord = new Vector2I(x, y),
                base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                base_height = 4,
            };
            cell.RecalculateRuntimeValues();
            state.SetCell(cell.coord, cell);
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildArcher(
        StringName id,
        StringName faction,
        Vector2I coord,
        int skillLevel,
        bool useBow = true
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            id,
            faction,
            coord,
            currentAp: 2,
            currentHp: 100
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.SetCurrentStamina(100);
        unit.SetCurrentMovePoints(3);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina));
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, skillLevel);
        ApplyWeapon(unit, useBow ? (StringName)"bow" : "sword");
        return unit;
    }

    private static void ApplyWeapon(BattleUnitState unit, StringName family)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"grapple_test_{family}",
                weapon_profile_type_id = $"grapple_test_{family}",
                weapon_range_type = family == "bow" ? (StringName)"ranged" : "melee",
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = family == "bow" ? 4 : 1,
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I landing)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = landing,
        };
        command.AddTargetCoord(landing);
        return command;
    }

    private static void SetHeight(
        BattleGridService grid,
        BattleState state,
        Vector2I coord,
        int heightGain
    ) => grid.SetHeightOffset(state, coord, heightGain);

    private void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        (isEnemy ? state.enemy_unit_ids : state.ally_unit_ids).Add(unit.unit_id);
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true),
            $"AI 测试单位 {unit.unit_id} 应能放入战场。"
        );
    }

    private static BattleAiContext BuildAiContext(
        BattleRuntimeModule runtime,
        BattleUnitState unit
    )
    {
        runtime._ensure_ai_action_plan_for_unit(unit);
        runtime.TryGetAiActionPlanForUnit(unit.unit_id, out BattleAiRuntimeActionPlan actionPlan);
        var context = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unit,
            grid_service = runtime._grid_service,
            runtime_action_plan = actionPlan,
            preview_command_callback = runtime.PreviewCommand,
            move_cost_callback = (candidate, target) =>
                runtime._get_ai_move_query_cost(
                    candidate.unit_id,
                    candidate.GetAnchorCoord(),
                    target
                ),
        };
        context.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime._bind_ai_helper_services_for_decision(unit, context);
        return context;
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;

    private static bool ErrorsContain(IEnumerable<string> errors, string fragment)
    {
        foreach (string error in errors ?? Array.Empty<string>())
        {
            if (error?.Contains(fragment, StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }
}
