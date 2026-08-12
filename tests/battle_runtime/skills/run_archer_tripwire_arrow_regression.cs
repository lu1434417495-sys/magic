using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_tripwire_arrow_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_tripwire_arrow";
    private const string SkillPath =
        "res://data/configs/skills/archer_tripwire_arrow.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                SkillPath,
                "archer_tripwire_arrow_regression"
            );
            TestAuthoredAndLevelContract(skill);
            TestSchemaRejectsMalformedMovementContact();
            TestCanonicalPlacementPreviewAndLegality(skill);
            TestFailedSaveRepeatsAndConsumesLevelFiveCharges(skill);
            TestSuccessfulSaveDoesNotConsumeCharge(skill);
            TestFlyingMovementIgnoresTripwire(skill);
            TestForcedMovementStopsAtTripwire(skill);
            TestChargeStopsAtTripwire(skill);
            TestRecastReplacesAndSourceDeathPreservesField(skill);
            TestAiRouteScoreFacts(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer tripwire arrow regression"));
    }

    private void TestAuthoredAndLevelContract(SkillDefinition skill)
    {
        _test.True(skill != null, "绊索箭正式资源应可投影。" );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "绊索箭必须有战斗配置。" );
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.MaxLevel, 5, "核心等级上限应为5。" );
        _test.Eq(skill.NonCoreMaxLevel, 3, "非核心等级上限应为3。" );
        _test.Eq(skill.GrowthTier, new StringName("basic"), "应属于基础成长档。" );
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Ground, "必须选择地格。" );
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Enemy, "机关只影响敌人。" );
        _test.Eq(
            BattleTypedNames.ToAreaPattern(combat.AreaPattern),
            BattleAreaPattern.Line,
            "机关应为直线三格。"
        );
        _test.Eq(combat.AreaValue, 1, "半径1的直线应产生三格。" );
        _test.Eq(
            combat.AreaDirectionModeKind,
            CombatAreaDirectionMode.TargetVectorPerpendicular,
            "绊索必须垂直于射击方向。"
        );
        _test.True(combat.RequiresLos, "布置应要求视线。" );
        _test.True(combat.GroundEffectRequireFullArea, "地图边缘不得裁剪绊索。" );
        _test.True(combat.GroundEffectRequireEmpty, "绊索三格必须为空。" );
        _test.True(combat.GroundEffectRequireTraversable, "绊索三格必须可通行。" );
        _test.False(combat.AllowsNaturalWeapon, "天生武器不得替代弓。" );
        _test.True(
            combat.RequiredWeaponFamilies.Contains(new StringName("bow")),
            "必须装备弓。"
        );
        _test.Eq(combat.RangeValue, 0, "不得保留固定射程兜底。" );
        _test.Eq(combat.WeaponRangePolicy, new StringName("current_weapon"), "射程应取当前弓。" );
        _test.Eq(
            combat.MasteryTriggerModeKind,
            CombatSkillMasteryTriggerMode.TerrainEffectiveTrigger,
            "只有实际失败拦停才应触发熟练度。"
        );
        BattleBoardTileSourceSpec overlay = BattleBoardRenderProfile
            .ForTerrainProfileId("default")
            .GetSourceSpecs()
            .FirstOrDefault(spec => spec?.Key == new StringName("tripwire_line"));
        _test.True(overlay != null, "绊索必须注册正式地形overlay source。" );
        _test.True(overlay?.AllowGeneratedFallback == true, "缺少专用贴图时绊索仍必须可见。" );

        AssertLevel(skill, 0, 20, 80, 12, 60, 1);
        AssertLevel(skill, 1, 20, 80, 12, 60, 1);
        AssertLevel(skill, 2, 18, 80, 12, 60, 1);
        AssertLevel(skill, 3, 18, 80, 13, 70, 1);
        AssertLevel(skill, 4, 18, 70, 13, 70, 1);
        AssertLevel(skill, 5, 18, 70, 13, 80, 2);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldown,
        int dc,
        int duration,
        int triggers
    )
    {
        CombatSkillResourceCosts costs = skill.CombatProfile.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 1, $"{level}级应消耗1AP。" );
        _test.Eq(costs.StaminaCost, stamina, $"{level}级体力消耗应正确。" );
        _test.Eq(costs.CooldownTu, cooldown, $"{level}级冷却应正确。" );
        BattleUnitState source = BuildArcher($"tripwire_level_{level}", new Vector2I(1, 1), level);
        try
        {
            using var rules = new BattleSkillResolutionRules();
            IReadOnlyList<CombatEffectDefinition> effects =
                rules.CollectGroundTerrainEffectDefinitions(skill, null, source);
            _test.Eq(effects.Count, 1, $"{level}级只应选择一个等级窗口。" );
            if (effects.Count != 1)
                return;
            CombatEffectDefinition effect = effects[0];
            _test.Eq(effect.SaveDc, dc, $"{level}级敏捷豁免DC应正确。" );
            _test.Eq(effect.DurationTu, duration, $"{level}级持续时间应正确。" );
            _test.Eq(effect.TerrainEffectiveTriggerCount, triggers, $"{level}级有效阻挡次数应正确。" );
            _test.True(effect.TerrainRecheckFromInside, "新移动指令从机关内起步应重复判定。" );
            _test.True(effect.TerrainRequiresGroundContact, "飞行单位应被排除。" );
            _test.True(effect.TerrainReplaceExistingFromSource, "同源重施必须替换旧机关。" );
            _test.Eq(effect.TerrainMaxActiveInstancesPerSource, 1, "每个来源只能保留一条。" );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(source);
        }
    }

    private void TestSchemaRejectsMalformedMovementContact()
    {
        using var loader = new TestContentResourceLoader();
        using var registry = new SkillContentRegistry(loader, loadDefaultContent: false);
        var malformed = new CombatEffectDef
        {
            effect_type = "terrain_effect",
            terrain_effect_id = "bad_tripwire",
            terrain_contact_mode = "interrupt_movement_on_failed_save",
            terrain_effective_trigger_count = 0,
            save_dc = 0,
            duration_tu = 20,
            tick_interval_tu = 5,
        };
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(errors, SkillId, malformed, "malformed");
        _test.True(
            errors.Any(error => error.Contains("terrain_effective_trigger_count")),
            "零有效阻挡次数必须在内容加载期失败。"
        );
        _test.True(
            errors.Any(error => error.Contains("positive save_dc")),
            "缺失豁免配置必须在内容加载期失败。"
        );
        malformed.Dispose();
    }

    private void TestCanonicalPlacementPreviewAndLegality(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_preview_archer", new Vector2I(1, 2), 0);
        BattleUnitState enemy = BuildUnit("tripwire_preview_enemy", "enemy", new Vector2I(5, 2));
        using BattleTestFixture fixture = CreateFixture(skill, archer, enemy);
        ForceUnitActing(fixture.State, archer);

        BattleCommand command = BuildSkillCommand(archer, new Vector2I(3, 2));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "弓射程内完整空旷三格应允许布置。" );
        _test.True(preview.TargetCoordsTyped.Contains(new Vector2I(3, 1)), "横向射击应生成竖向上格。" );
        _test.True(preview.TargetCoordsTyped.Contains(new Vector2I(3, 2)), "应包含锚点。" );
        _test.True(preview.TargetCoordsTyped.Contains(new Vector2I(3, 3)), "横向射击应生成竖向下格。" );
        _test.Eq(preview.TargetCoordsTyped.Count, 3, "canonical preview必须精确显示三格。" );
        _test.Eq(preview.TerrainContactPreviewTyped?.SaveDc ?? 0, 12, "preview应公开接触豁免DC。" );
        _test.Eq(preview.TerrainContactPreviewTyped?.EffectiveTriggerCount ?? 0, 1, "preview应公开阻挡次数。" );
        using (GodotProjectionLease<GDictionary> lease = BattlePreviewProjection.BuildLease(preview))
        using (GDictionary contact = lease.Value["terrain_contact_preview"].AsGodotDictionary())
        {
            _test.Eq(contact["save_dc"].AsInt32(), 12, "Godot/UI preview投影应公开DC。" );
            _test.Eq(
                contact["effective_trigger_count"].AsInt32(),
                1,
                "Godot/UI preview投影应公开有效阻挡次数。"
            );
        }

        BattleCommand edge = BuildSkillCommand(archer, new Vector2I(0, 0));
        _test.False(fixture.Runtime.PreviewCommand(edge)?.allowed == true, "地图边缘裁剪成两格时必须拒绝。" );

        BattleCommand occupied = BuildSkillCommand(archer, new Vector2I(5, 2));
        _test.False(
            fixture.Runtime.PreviewCommand(occupied)?.allowed == true,
            "三格范围存在单位时必须拒绝。"
        );
        fixture.Runtime._grid_service.SetEdgeFeature(
            fixture.State,
            new Vector2I(1, 2),
            Vector2I.Right,
            BattleEdgeFeatureState.MakeWall()
        );
        _test.False(
            fixture.Runtime.PreviewCommand(command)?.allowed == true,
            "阻挡视线的边缘必须拒绝布置。"
        );
        BattleTestFixture.DisposeBattleCommand(command);
        BattleTestFixture.DisposeBattleCommand(edge);
        BattleTestFixture.DisposeBattleCommand(occupied);
    }

    private void TestFailedSaveRepeatsAndConsumesLevelFiveCharges(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_repeat_archer", new Vector2I(1, 2), 5);
        archer.source_member_id = "tripwire_repeat_member";
        BattleUnitState enemy = BuildUnit("tripwire_repeat_enemy", "enemy", new Vector2I(2, 2));
        enemy.attribute_snapshot.SetValue("boss_target", 1);
        enemy.SetCurrentMovePoints(6);
        var gateway = new MasteryGatewayStub();
        using BattleTestFixture fixture = CreateFixture(skill, archer, gateway, enemy);
        PlaceTripwire(fixture, archer, new Vector2I(3, 2));
        BattleTerrainEffectState field = FindTripwire(fixture, new Vector2I(3, 2));
        _test.Eq(field?.terrain_remaining_effective_triggers ?? 0, 2, "5级机关初始应有两次有效阻挡。" );
        _test.Eq(archer.GetCurrentAp(), 1, "5级正式布置应支付1AP。" );
        _test.Eq(archer.GetCurrentStamina(), 82, "5级正式布置应支付18体力。" );
        _test.Eq(archer.GetCooldownTyped(SkillId), 70, "5级正式布置应启动70TU冷却。" );

        ForceUnitActing(fixture.State, enemy);
        fixture.Runtime._terrain_effect_system.ConfigureMovementContactSaveRollOverridesForTests(
            new[] { 1, 1 }
        );
        BattleCommand firstMove = BuildMoveCommand(enemy, new Vector2I(5, 2));
        using (BattleEventBatch firstBatch = fixture.Runtime.IssueCommand(firstMove))
        {
            _test.Eq(enemy.GetAnchorCoord(), new Vector2I(3, 2), "第一次失败应停在刚进入的机关格。" );
            _test.Eq(enemy.GetCurrentMovePoints(), 3, "第一次失败仍应支付原三步路径的全部成本。" );
            _test.True(
                firstBatch.LogLinesTyped.Any(line => line.Contains("还可生效 1 次")),
                "日志应公开剩余一次有效阻挡。"
            );
        }
        _test.Eq(
            FindTripwire(fixture, new Vector2I(3, 2))?.terrain_remaining_effective_triggers ?? 0,
            1,
            "第一次失败只应扣除一次阻挡。"
        );
        _test.Eq(gateway.Grants.Count, 1, "第一次实际拦停应授予一次熟练度。" );
        _test.Eq(gateway.Grants[0].MasteryAmount, 3, "Boss拦停应复用标准目标阶级熟练度倍率。" );

        enemy.ResetTurnStateForTurnStartTyped();
        enemy.SetCurrentMovePoints(6);
        ForceUnitActing(fixture.State, enemy);
        BattleCommand secondMove = BuildMoveCommand(enemy, new Vector2I(5, 2));
        using (BattleEventBatch secondBatch = fixture.Runtime.IssueCommand(secondMove))
        {
            _test.Eq(enemy.GetAnchorCoord(), new Vector2I(3, 2), "下一行动从机关内起步应再次失败并原地停下。" );
            _test.Eq(enemy.GetCurrentMovePoints(), 4, "起步失败仍应支付原两步路径的全部成本。" );
            _test.True(
                secondBatch.LogLinesTyped.Any(line => line.Contains("已经耗尽")),
                "第二次失败应耗尽机关。"
            );
        }
        _test.True(FindTripwire(fixture, new Vector2I(3, 2)) == null, "两次失败后整组三格应原子移除。" );
        _test.True(FindTripwire(fixture, new Vector2I(3, 1)) == null, "上格也必须同步移除。" );
        _test.True(FindTripwire(fixture, new Vector2I(3, 3)) == null, "下格也必须同步移除。" );
        _test.Eq(gateway.Grants.Count, 2, "第二次实际拦停应再授予一次熟练度。" );
        _test.Eq(gateway.Grants[1].MasteryAmount, 3, "同一Boss第二次被拦停仍应按目标阶级计熟练度。" );
        BattleTestFixture.DisposeBattleCommand(firstMove);
        BattleTestFixture.DisposeBattleCommand(secondMove);
    }

    private void TestSuccessfulSaveDoesNotConsumeCharge(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_success_archer", new Vector2I(1, 2), 0);
        archer.source_member_id = "tripwire_success_member";
        BattleUnitState enemy = BuildUnit("tripwire_success_enemy", "enemy", new Vector2I(2, 2));
        enemy.SetCurrentMovePoints(6);
        var gateway = new MasteryGatewayStub();
        using BattleTestFixture fixture = CreateFixture(skill, archer, gateway, enemy);
        PlaceTripwire(fixture, archer, new Vector2I(3, 2));
        ForceUnitActing(fixture.State, enemy);
        fixture.Runtime._terrain_effect_system.ConfigureMovementContactSaveRollOverridesForTests(
            new[] { 20 }
        );
        BattleCommand move = BuildMoveCommand(enemy, new Vector2I(5, 2));
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(move);
        _test.Eq(enemy.GetAnchorCoord(), new Vector2I(5, 2), "豁免成功应完成整条移动。" );
        _test.Eq(
            FindTripwire(fixture, new Vector2I(3, 2))?.terrain_remaining_effective_triggers ?? 0,
            1,
            "豁免成功不得消耗有效阻挡次数。"
        );
        _test.Eq(
            batch.LogLinesTyped.Count(line => line.Contains("通过敏捷豁免")),
            1,
            "同一移动指令对同一机关只应判定一次。"
        );
        _test.Eq(gateway.Grants.Count, 0, "豁免成功不得授予熟练度。" );
        BattleTestFixture.DisposeBattleCommand(move);
    }

    private void TestFlyingMovementIgnoresTripwire(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_fly_archer", new Vector2I(1, 2), 0);
        BattleUnitState enemy = BuildUnit("tripwire_fly_enemy", "enemy", new Vector2I(2, 2));
        enemy.AddMovementTagTyped("fly");
        enemy.SetCurrentMovePoints(6);
        using BattleTestFixture fixture = CreateFixture(skill, archer, enemy);
        PlaceTripwire(fixture, archer, new Vector2I(3, 2));
        ForceUnitActing(fixture.State, enemy);
        fixture.Runtime._terrain_effect_system.ConfigureMovementContactSaveRollOverridesForTests(
            new[] { 1 }
        );
        BattleCommand move = BuildMoveCommand(enemy, new Vector2I(5, 2));
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(move);
        _test.Eq(enemy.GetAnchorCoord(), new Vector2I(5, 2), "飞行单位应无视绊索。" );
        _test.False(batch.LogLinesTyped.Any(line => line.Contains("敏捷豁免")), "飞行移动不应进行机关豁免。" );
        _test.Eq(
            FindTripwire(fixture, new Vector2I(3, 2))?.terrain_remaining_effective_triggers ?? 0,
            1,
            "飞行移动不得消耗次数。"
        );
        BattleTestFixture.DisposeBattleCommand(move);
    }

    private void TestForcedMovementStopsAtTripwire(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_forced_archer", new Vector2I(1, 2), 0);
        BattleUnitState enemy = BuildUnit("tripwire_forced_enemy", "enemy", new Vector2I(2, 2));
        using BattleTestFixture fixture = CreateFixture(skill, archer, enemy);
        PlaceTripwire(fixture, archer, new Vector2I(3, 2));
        fixture.Runtime._terrain_effect_system.ConfigureMovementContactSaveRollOverridesForTests(
            new[] { 1 }
        );
        CombatEffectDefinition knockback = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            forcedMoveMode: "knockback",
            forcedMoveDistance: 3
        );
        using var batch = new BattleEventBatch();
        int movedSteps = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            archer,
            enemy,
            knockback,
            batch,
            default
        );
        _test.Eq(movedSteps, 1, "强制位移进入绊索后应停止剩余位移。" );
        _test.Eq(enemy.GetAnchorCoord(), new Vector2I(3, 2), "强制位移失败应停在触发格。" );
        _test.True(FindTripwire(fixture, new Vector2I(3, 2)) == null, "0级绊索阻挡一次后应耗尽。" );
    }

    private void TestChargeStopsAtTripwire(SkillDefinition tripwireSkill)
    {
        SkillDefinition chargeSkill = BuildChargeSkill();
        BattleUnitState archer = BuildArcher("tripwire_charge_archer", new Vector2I(1, 2), 0);
        BattleUnitState charger = BuildUnit("tripwire_charge_enemy", "enemy", new Vector2I(2, 2));
        charger.AddKnownActiveSkill(chargeSkill.SkillId);
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "archer_tripwire_charge",
            new Vector2I(7, 5),
            new[] { archer },
            new[] { charger }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                [SkillId] = tripwireSkill,
                [chargeSkill.SkillId] = chargeSkill,
            }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        PlaceTripwire(fixture, archer, new Vector2I(3, 2));
        ForceUnitActing(fixture.State, charger);
        fixture.Runtime._terrain_effect_system.ConfigureMovementContactSaveRollOverridesForTests(
            new[] { 1 }
        );
        BattleCommand charge = BuildGroundVariantCommand(
            charger,
            chargeSkill.SkillId,
            "charge_line",
            new Vector2I(5, 2)
        );
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(charge);
        _test.Eq(charger.GetAnchorCoord(), new Vector2I(3, 2), "冲锋进入绊索失败后必须停在触发格。" );
        _test.Eq(charger.GetCurrentAp(), 1, "冲锋被绊索中止后仍应支付完整AP。" );
        _test.Eq(charger.GetCurrentStamina(), 90, "冲锋被绊索中止后仍应支付完整体力。" );
        _test.Eq(charger.GetCooldownTyped(chargeSkill.SkillId), 50, "冲锋被绊索中止后仍应启动完整冷却。" );
        _test.True(
            batch.LogLinesTyped.Any(line => line.Contains("绊索")),
            "冲锋中止应由统一地形接触结算留下日志。"
        );
        BattleTestFixture.DisposeBattleCommand(charge);
    }

    private void TestRecastReplacesAndSourceDeathPreservesField(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_replace_archer", new Vector2I(1, 2), 0);
        BattleUnitState enemy = BuildUnit("tripwire_replace_enemy", "enemy", new Vector2I(2, 2));
        using BattleTestFixture fixture = CreateFixture(skill, archer, enemy);
        PlaceTripwire(fixture, archer, new Vector2I(3, 2));

        archer.ResetTurnStateForTurnStartTyped();
        archer.SetCurrentAp(2);
        archer.SetCurrentStamina(100);
        archer.SetCooldownTyped(SkillId, 0);
        PlaceTripwire(fixture, archer, new Vector2I(4, 2));
        _test.True(FindTripwire(fixture, new Vector2I(3, 2)) == null, "同源重施应移除旧绊索。" );
        _test.True(FindTripwire(fixture, new Vector2I(4, 2)) != null, "同源重施应保留新绊索。" );

        archer.MarkDead();
        ForceUnitActing(fixture.State, enemy);
        fixture.Runtime._terrain_effect_system.ConfigureMovementContactSaveRollOverridesForTests(
            new[] { 1 }
        );
        BattleCommand move = BuildMoveCommand(enemy, new Vector2I(5, 2));
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(move);
        _test.Eq(enemy.GetAnchorCoord(), new Vector2I(4, 2), "来源倒下后绊索仍应拦截敌人。" );
        BattleTestFixture.DisposeBattleCommand(move);
    }

    private void TestAiRouteScoreFacts(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("tripwire_ai_archer", new Vector2I(1, 2), 5);
        archer.faction_id = "enemy";
        BattleUnitState player = BuildUnit("tripwire_ai_player", "player", new Vector2I(5, 2));
        player.SetCurrentMovePoints(4);
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "tripwire_ai",
            new Vector2I(7, 5),
            new[] { player },
            new[] { archer }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        ForceUnitActing(fixture.State, archer);
        BattleCommand command = BuildSkillCommand(archer, new Vector2I(3, 2));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        using var scoreService = new BattleAiScoreService();
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = archer,
            grid_service = fixture.Runtime._grid_service,
            preview_command_callback = fixture.Runtime.PreviewCommand,
            trace_enabled = true,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        using var rules = new BattleSkillResolutionRules();
        IReadOnlyList<CombatEffectDefinition> effects =
            rules.CollectGroundTerrainEffectDefinitions(skill, null, archer);
        BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            effects
        );
        _test.True(score != null, "AI应能为合法绊索候选生成typed评分输入。" );
        _test.Eq(score?.estimated_ground_control_cell_count ?? 0, 3, "AI应消费canonical三格preview。" );
        _test.True(
            (score?.estimated_terrain_interrupt_threat_count ?? 0) >= 1,
            "位于敌人接近路线上的绊索应识别至少一个威胁目标。"
        );
        _test.True(
            (score?.estimated_terrain_interrupt_reachable_count ?? 0) >= 1,
            "本回合可触及绊索的敌人应提高路线控制评分。"
        );
        _test.True((score?.ground_control_score ?? 0) > 0, "路线阻挡应产生正的控场评分。" );
        BattleAiScoreInput scoreClone = BattleAiDecisionResult.CloneScoreInput(score);
        _test.Eq(
            scoreClone?.estimated_terrain_interrupt_threat_count ?? 0,
            score?.estimated_terrain_interrupt_threat_count ?? 0,
            "decision clone必须保留路线威胁事实。"
        );
        _test.Eq(
            scoreClone?.estimated_terrain_interrupt_reachable_count ?? 0,
            score?.estimated_terrain_interrupt_reachable_count ?? 0,
            "decision clone必须保留当回合可达事实。"
        );
        fixture.Runtime._bind_ai_helper_services_for_decision(archer, context);
        using var action = new UseGroundSkillAction
        {
            action_id = "tripwire_ai_probe",
            score_bucket_id = "archer_positioning",
            minimum_hit_count = 1,
            allow_empty_ground_control = true,
            minimum_ground_control_score = 1,
            desired_min_distance = 2,
            desired_max_distance = 4,
            distance_reference = "enemy_frontline",
        };
        action.skill_ids.Add(SkillId);
        BattleAiDecision decision = new BattleAiGroundSkillActionEvaluator().Evaluate(
            (UseGroundSkillActionDefinition)action.ToDefinition(),
            context
        );
        AiActionTrace trace = context.GetActionTracesTyped().LastOrDefault();
        _test.True(
            decision?.command != null,
            $"AI必须能枚举并生成合法绊索候选。 evaluations={trace?.EvaluationCount ?? 0} preview_rejects={trace?.PreviewRejectCount ?? 0} reasons={string.Join(",", trace?.BlockReasons ?? new Dictionary<string, int>())}"
        );
        _test.Eq(decision?.command?.skill_id ?? new StringName(""), SkillId, "AI候选应使用绊索箭。" );
        if (decision?.command != null)
        {
            BattlePreview decisionPreview = fixture.Runtime.PreviewCommand(decision.command);
            _test.True(decisionPreview?.allowed == true, "AI候选必须通过同一canonical preview。" );
            _test.True(
                (decision.score_input?.estimated_terrain_interrupt_threat_count ?? 0) >= 1,
                "AI最终候选评分应保留路线威胁事实。"
            );
            BattleTestFixture.DisposeBattlePreview(decisionPreview);
            BattleTestFixture.DisposeBattleCommand(decision.command);
        }
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void PlaceTripwire(
        BattleTestFixture fixture,
        BattleUnitState archer,
        Vector2I anchor
    )
    {
        ForceUnitActing(fixture.State, archer);
        BattleCommand command = BuildSkillCommand(archer, anchor);
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(FindTripwire(fixture, anchor) != null, "正式IssueCommand应布置绊索。" );
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static BattleTerrainEffectState FindTripwire(
        BattleTestFixture fixture,
        Vector2I coord
    )
    {
        BattleCellState cell = fixture.Runtime._grid_service.GetCellState(
            fixture.State,
            coord
        );
        return cell?.timed_terrain_effects?.FirstOrDefault(
            effect => effect?.effect_id == new StringName("tripwire_line")
        );
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState archer,
        params BattleUnitState[] enemies
    ) => CreateFixture(skill, archer, null, enemies);

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState archer,
        IBattleRuntimeCharacterGateway gateway,
        params BattleUnitState[] enemies
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "archer_tripwire_arrow",
            new Vector2I(7, 5),
            new[] { archer },
            enemies
        );
        fixture.Runtime.setup(
            gateway,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static BattleUnitState BuildArcher(StringName id, Vector2I coord, int level)
    {
        BattleUnitState unit = BuildUnit(id, "player", coord);
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"{id}_bow",
                weapon_profile_type_id = "test_longbow",
                weapon_range_type = "ranged",
                weapon_family = "bow",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 5,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        return unit;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        }.WithCombatResourcesForTest(hp: 50, ap: 2, stamina: 100, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 50);
        unit.SetCurrentMovePoints(6);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static BattleCommand BuildSkillCommand(BattleUnitState unit, Vector2I coord)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unit.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = coord,
        };
        command.AddTargetCoord(coord);
        return command;
    }

    private static BattleCommand BuildMoveCommand(BattleUnitState unit, Vector2I coord) =>
        new()
        {
            CommandKind = BattleCommandKind.Move,
            unit_id = unit.unit_id,
            target_coord = coord,
        };

    private static BattleCommand BuildGroundVariantCommand(
        BattleUnitState unit,
        StringName skillId,
        StringName variantId,
        Vector2I coord
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unit.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            skill_variant_id = variantId,
            target_coord = coord,
        };
        command.AddTargetCoord(coord);
        return command;
    }

    private static SkillDefinition BuildChargeSkill()
    {
        StringName skillId = "test_tripwire_charge";
        CombatEffectDefinition chargeEffect = TestSkillDefinitionProjection.BuildEffect("charge");
        CombatCastVariantDefinition variant = TestSkillDefinitionProjection.BuildCastVariant(
            "charge_line",
            0,
            new[] { chargeEffect },
            targetMode: "ground",
            requiredCoordCount: 1
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "绊索冲锋入口测试",
            tags: new[] { new StringName("melee"), new StringName("charge") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                targetMode: "ground",
                targetTeamFilter: "any",
                rangeValue: 3,
                apCost: 1,
                staminaCost: 10,
                cooldownTu: 50,
                castVariants: new[] { variant }
            )
        );
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private sealed class MasteryGatewayStub : IBattleRuntimeCharacterGateway
    {
        internal List<CharacterMasteryChangeFact> Grants { get; } = new();

        public PartyState GetPartyState() => null;
        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDefinition>();
        public bool HasItemDefCatalog() => false;
        public ItemDefinition GetItemDef(StringName itemId) => null;
        public PartyMemberState GetMemberState(StringName memberId) => null;
        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName memberId,
            EquipmentState equipmentView
        ) => null;
        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName memberId,
            EquipmentState equipmentView
        ) => new();
        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
            StringName memberId,
            EquipmentState equipmentView
        ) => BattleEffectiveTraitProjection.Empty;
        public PassiveSourceContext BuildPassiveSourceContext(
            StringName memberId,
            UnitProgress progressionState
        ) => null;
        public CharacterProgressionDelta PromoteProfession(
            StringName memberId,
            StringName professionId,
            PromotionSelectionData selection
        ) => new() { member_id = memberId };
        public BattleResourceCommitResult CommitBattleResources(
            StringName memberId,
            int currentHp,
            int currentMp,
            int currentAura
        ) => BattleResourceCommitResult.Success(memberId);
        public ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(
            StringName memberId,
            IReadOnlyCollection<StringName> consumedSetupIds
        ) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
            StringName memberId,
            IReadOnlyCollection<StringName> consumedSetupIds
        ) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public void CommitBattleDeath(StringName memberId) { }
        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName memberId,
            StringName skillId,
            int amount
        ) => RecordGrant(memberId, skillId, amount, "battle");

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType,
            string sourceLabel,
            string reasonText,
            bool emitAchievementEvent
        ) => RecordGrant(memberId, skillId, amount, sourceType);

        public IReadOnlyList<StringName> RecordAchievementEvent(
            StringName memberId,
            StringName eventType,
            int amount
        ) => Array.Empty<StringName>();

        public IReadOnlyList<StringName> RecordAchievementEvent(
            StringName memberId,
            StringName eventType,
            int amount,
            StringName subjectId,
            GDictionary meta
        ) => Array.Empty<StringName>();

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName memberId,
            StringName sourceType,
            string sourceLabel,
            IEnumerable<PendingCharacterRewardEntry> entryOptions,
            string summaryText
        ) => null;

        private CharacterProgressionDelta RecordGrant(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType
        )
        {
            var change = new CharacterMasteryChangeFact(
                skillId,
                skillId.ToString(),
                amount,
                sourceType,
                sourceType.ToString(),
                ""
            );
            Grants.Add(change);
            var delta = new CharacterProgressionDelta { member_id = memberId };
            delta.AddMasteryChange(change);
            return delta;
        }
    }
}
