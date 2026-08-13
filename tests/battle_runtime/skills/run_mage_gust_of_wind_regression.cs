using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_mage_gust_of_wind_regression : LifecycleTestSceneTree
{
    private const string SkillPath = "res://data/configs/skills/mage_gust_of_wind.tres";
    private static readonly StringName SkillId = "mage_gust_of_wind";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContractAndLevelCurve(skill);
            TestSchemaRejectsIncompleteWindPush();
            TestForcedMoveSaveBodyBossAndTerrainRules(skill);
            TestGroundWindDoesNotRecursivelyPushOutsideArea(skill);
            TestCanonicalPreviewAndEmptyCastCost(skill);
            TestMasteryRankAmounts(skill);
            TestBarrierOnlyMastery(skill);
            TestAiConsumesCanonicalWindPreview(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage gust of wind regression"));
    }

    private void TestAuthoredContractAndLevelCurve(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "强风术正式资源与 combat_profile 应可加载。");
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "技能 ID 应保持 mage_gust_of_wind。");
        _test.Eq(skill.DisplayName, "强风术", "显示名应保持强风术。");
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Ground, "强风术应为地面定向技能。");
        _test.Eq(combat.RangeValue, 1, "必须点击正交相邻格确定风向。");
        _test.Eq(combat.AreaPattern, new StringName("cone"), "影响区必须保持锥形。");
        _test.Eq(combat.MasteryTriggerModeKind, CombatSkillMasteryTriggerMode.EffectApplied, "仅实际生效才应增长熟练度。");
        _test.Eq(combat.MasteryAmountModeKind, CombatSkillMasteryAmountMode.PerTargetRank, "熟练度应按目标阶级结算。");
        _test.Eq(combat.MasteryBaseAmount, 5, "强风术熟练度基础值应为5。");
        _test.False(combat.EffectDefinitions.Any(effect => effect?.EffectKind == BattleEffectKind.Damage), "强风术不得含伤害效果。");
        _test.True(skill.Description.Contains("允许对空范围施放"), "描述必须明确允许空放及其代价。");
        _test.True(skill.Description.Contains("首领身份本身不提供强风免疫"), "描述必须明确 Boss 无额外免疫。");
        _test.True(skill.Description.Contains("不造成伤害"), "描述必须明确纯控制定位。");

        AssertLevel(skill, 0, 100, 240, 2, 1, 2);
        AssertLevel(skill, 1, 100, 240, 2, 1, 2);
        AssertLevel(skill, 2, 95, 240, 2, 1, 2);
        AssertLevel(skill, 3, 95, 240, 2, 2, 2);
        AssertLevel(skill, 4, 95, 230, 2, 2, 2);
        AssertLevel(skill, 5, 95, 230, 3, 2, 2);
        AssertLevel(skill, 6, 95, 220, 3, 2, 2);
        AssertLevel(skill, 7, 95, 220, 3, 3, 4);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int mp,
        int cooldown,
        int area,
        int pushDistance,
        int maximumBodySize
    )
    {
        CombatSkillResourceCosts costs = skill.CombatProfile.GetEffectiveResourceCostValues(level);
        CombatEffectDefinition effect = FindWind(skill, level);
        _test.Eq(costs.ApCost, 1, $"L{level} 应消耗1AP。");
        _test.Eq(costs.MpCost, mp, $"L{level} 法力消耗不符。");
        _test.Eq(costs.CooldownTu, cooldown, $"L{level} 冷却不符。");
        _test.Eq(skill.CombatProfile.GetEffectiveAreaValue(level), area, $"L{level} 锥形列数不符。");
        _test.True(effect != null, $"L{level} 应恰有一个有效 wind_push 效果。");
        if (effect == null)
            return;
        _test.Eq(effect.ForcedMoveDistance, pushDistance, $"L{level} 推动距离不符。");
        _test.Eq(effect.ForcedMoveMaxTargetBodySize, maximumBodySize, $"L{level} 体型上限不符。");
        _test.Eq(effect.SaveDcModeKind, BattleSaveDcMode.CasterSpell, $"L{level} 应使用施法者法术豁免DC。");
        _test.Eq(effect.SaveDcSourceAbility, new StringName("intelligence"), $"L{level} DC 应使用智力。");
        _test.Eq(effect.SaveAbility, new StringName("strength"), $"L{level} 应进行力量豁免。");
        _test.Eq(effect.SaveTag, new StringName("strength"), $"L{level} 豁免标签应为 strength。");
    }

    private void TestSchemaRejectsIncompleteWindPush()
    {
        using var effect = new CombatEffectDef
        {
            effect_type = "forced_move",
            effect_target_team_filter = "enemy",
            forced_move_mode = "wind_push",
            forced_move_distance = 1,
        };
        using var profile = new CombatSkillDef
        {
            skill_id = "invalid_wind_push",
            target_mode = "ground",
            target_team_filter = "enemy",
            range_value = 1,
            area_pattern = "cone",
            area_value = 2,
        };
        profile.effect_defs.Add(effect);
        var errors = new GStringArray();
        new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        ).AppendCombatProfileValidationErrors(errors, profile.skill_id, profile);
        _test.True(ContainsError(errors, "forced_move_max_target_body_size"), "wind_push 缺少体型上限必须被拒绝。");
        _test.True(ContainsError(errors, "requires a saving throw"), "wind_push 缺少豁免必须被拒绝。");
    }

    private void TestForcedMoveSaveBodyBossAndTerrainRules(SkillDefinition skill)
    {
        CombatEffectDefinition levelSevenEffect = FindWind(skill, 7);
        BattleUnitState caster = BuildMage("wind_runtime_caster", new Vector2I(0, 2), 7);
        BattleUnitState target = BuildUnit("wind_runtime_target", "enemy", new Vector2I(2, 2));
        using (BattleTestFixture fixture = CreateFixture("wind_runtime", skill, caster, target))
        {
            AddContactField(fixture, new Vector2I(3, 2), caster, "wind_contact_1", "slow");
            AddContactField(fixture, new Vector2I(4, 2), caster, "wind_contact_2", "burning");
            int hpBefore = target.GetCurrentHp();
            using var batch = new BattleEventBatch();
            int moved = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
                caster,
                target,
                levelSevenEffect,
                batch,
                BattleForcedMoveContext.FromDirection(Vector2I.Right),
                BattleSaveContext.WithSaveRollOverride(1, SkillId)
            );
            _test.Eq(moved, 3, "L7 力量豁免失败应尝试推动3格。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(5, 2), "目标应平行于施法方向移动。");
            _test.Eq(target.GetCurrentHp(), hpBefore, "强风位移不得造成伤害。");
            _test.True(target.HasStatusEffect("slow"), "进入第一格应触发地形接触。");
            _test.True(target.HasStatusEffect("burning"), "进入第二格应继续触发地形接触。");
        }

        AssertDirectMoveResult(skill, level: 7, bodySize: 2, saveRoll: 20, boss: false, expectedSteps: 0, "豁免成功");
        AssertDirectMoveResult(skill, level: 3, bodySize: 3, saveRoll: 1, boss: false, expectedSteps: 0, "L3体型3");
        AssertDirectMoveResult(skill, level: 7, bodySize: 4, saveRoll: 1, boss: true, expectedSteps: 3, "L7四格Boss");
        AssertDirectMoveResult(skill, level: 7, bodySize: 5, saveRoll: 1, boss: true, expectedSteps: 0, "L7体型5Boss");
    }

    private void AssertDirectMoveResult(
        SkillDefinition skill,
        int level,
        int bodySize,
        int saveRoll,
        bool boss,
        int expectedSteps,
        string label
    )
    {
        BattleUnitState caster = BuildMage($"wind_{label}_caster", new Vector2I(0, 2), level);
        BattleUnitState target = BuildUnit($"wind_{label}_target", "enemy", new Vector2I(2, 1));
        target.SetBodySizeProjection(bodySize);
        if (boss)
            target.attribute_snapshot.SetValue("boss_target", 1);
        using BattleTestFixture fixture = CreateFixture($"wind_{label}", skill, caster, target);
        Vector2I before = target.GetAnchorCoord();
        using var batch = new BattleEventBatch();
        int moved = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            caster,
            target,
            FindWind(skill, level),
            batch,
            BattleForcedMoveContext.FromDirection(Vector2I.Right),
            BattleSaveContext.WithSaveRollOverride(saveRoll, SkillId)
        );
        _test.Eq(moved, expectedSteps, $"{label} 的推动步数不符。");
        _test.Eq(
            target.GetAnchorCoord(),
            before + Vector2I.Right * expectedSteps,
            $"{label} 的最终位置不符。"
        );
    }

    private void TestGroundWindDoesNotRecursivelyPushOutsideArea(SkillDefinition skill)
    {
        CombatEffectDefinition noSaveWind = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            effectTargetTeamFilter: "enemy",
            forcedMoveMode: "wind_push",
            forcedMoveDistance: 1,
            forcedMoveMaxTargetBodySize: 2
        );
        BattleUnitState caster = BuildMage("wind_group_caster", new Vector2I(0, 2), 1);
        caster.source_member_id = "wind_group_caster_member";
        BattleUnitState front = BuildUnit("wind_group_front", "enemy", new Vector2I(1, 2));
        BattleUnitState outsideBlocker = BuildUnit("wind_group_blocker", "enemy", new Vector2I(2, 2));
        using (BattleTestFixture fixture = CreateFixture("wind_outside_block", skill, caster, front, outsideBlocker))
        {
            using var batch = new BattleEventBatch();
            BattleGroundUnitEffectsResult result = fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                caster,
                skill,
                null,
                new[] { noSaveWind },
                new[] { front.GetAnchorCoord() },
                batch,
                new[] { caster.GetAnchorCoord() + Vector2I.Right }
            );
            _test.False(result.Applied, "锥形外单位只能阻挡，不能被递归推动。");
            _test.Eq(front.GetAnchorCoord(), new Vector2I(1, 2), "范围内目标应停在最后合法格。");
            _test.Eq(outsideBlocker.GetAnchorCoord(), new Vector2I(2, 2), "范围外阻挡单位必须保持原位。");
            _test.Eq(
                fixture.Runtime._skill_mastery_service.ResolveActiveSkillMasteryAmount(),
                0,
                "未产生实际位移时不得增长熟练度。"
            );
        }

        BattleUnitState groupCaster = BuildMage("wind_group2_caster", new Vector2I(0, 2), 1);
        groupCaster.source_member_id = "wind_group2_caster_member";
        BattleUnitState near = BuildUnit("wind_group2_near", "enemy", new Vector2I(1, 2));
        BattleUnitState far = BuildUnit("wind_group2_far", "enemy", new Vector2I(2, 2));
        using BattleTestFixture groupFixture = CreateFixture("wind_group_move", skill, groupCaster, near, far);
        using var groupBatch = new BattleEventBatch();
        BattleGroundUnitEffectsResult groupResult = groupFixture.Runtime.ApplyGroundUnitEffectsResultTyped(
            groupCaster,
            skill,
            null,
            new[] { noSaveWind },
            new[] { near.GetAnchorCoord(), far.GetAnchorCoord() },
            groupBatch,
            new[] { groupCaster.GetAnchorCoord() + Vector2I.Right }
        );
        _test.True(groupResult.Applied, "风区内相邻目标应由远到近整体移动。");
        _test.Eq(groupResult.AffectedUnitCount, 2, "只应报告实际移动的两个风区目标。");
        _test.Eq(near.GetAnchorCoord(), new Vector2I(2, 2), "近端目标应在远端腾空后移动。");
        _test.Eq(far.GetAnchorCoord(), new Vector2I(3, 2), "远端目标应先移动。");
        _test.Eq(
            groupFixture.Runtime._skill_mastery_service.ResolveActiveSkillMasteryAmount(),
            10,
            "同次施法中两个实际移动的普通目标应各结算一次5点熟练度。"
        );
    }

    private void TestCanonicalPreviewAndEmptyCastCost(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("wind_preview_caster", new Vector2I(1, 3), 7);
        BattleUnitState target = BuildUnit("wind_preview_target", "enemy", new Vector2I(2, 3));
        using (BattleTestFixture fixture = CreateFixture("wind_preview", skill, caster, target))
        {
            BattleCommand command = BuildGroundCommand(caster, new Vector2I(2, 3));
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, $"相邻格应可确定强风方向。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}");
            BattleForcedMovePreviewData forcedMove = preview?.ForcedMovePreviewTyped;
            _test.True(forcedMove != null, "正式预览必须投影 typed wind_push 事实。");
            _test.Eq(forcedMove?.Mode ?? new StringName(""), BattleTypedNames.ForcedMoveWindPush, "预览模式应为 wind_push。");
            _test.True(forcedMove?.Targets.Any(item => item.TargetUnitId == target.unit_id && item.CanMoveOnFailedSave) == true, "预览应列出可移动目标及失败分支落点。");
            _test.True(forcedMove?.Targets.All(item => item.SaveDc > 0) == true, "预览应提供每个目标的力量豁免概率输入。");
            _test.Eq(target.GetAnchorCoord(), new Vector2I(2, 3), "正式预览不得改写战斗状态。");
            using GodotProjectionLease<Godot.Collections.Dictionary> lease = BattlePreviewProjection.BuildLease(preview);
            Godot.Collections.Dictionary projection = lease.Value["forced_move_preview"].AsGodotDictionary();
            _test.True(projection.ContainsKey("targets") && projection["targets"].AsGodotArray().Count > 0, "Godot 边界必须投影逐目标强风预览。");
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }

        BattleUnitState emptyCaster = BuildMage("wind_empty_caster", new Vector2I(1, 2), 1);
        using BattleTestFixture emptyFixture = CreateFixture("wind_empty", skill, emptyCaster);
        BattleCommand emptyCommand = BuildGroundCommand(emptyCaster, new Vector2I(2, 2));
        BattlePreview emptyPreview = emptyFixture.Runtime.PreviewCommand(emptyCommand);
        _test.True(emptyPreview?.allowed == true, "强风术必须允许对空范围施放。");
        _test.Eq(emptyPreview?.TargetUnitIdsTyped.Count ?? -1, 0, "空放预览不应伪造有效目标。");
        using BattleEventBatch emptyBatch = emptyFixture.Runtime.IssueCommand(emptyCommand);
        _test.Eq(emptyCaster.GetCurrentAp(), 1, "空放仍应消耗1AP。");
        _test.Eq(emptyCaster.GetCurrentMp(), 100, "L1 空放仍应消耗100MP。");
        _test.Eq(emptyCaster.GetCooldownTyped(SkillId), 240, "L1 空放仍应启动240TU冷却。");
        BattleTestFixture.DisposeBattlePreview(emptyPreview);
        BattleTestFixture.DisposeBattleCommand(emptyCommand);
    }

    private void TestMasteryRankAmounts(SkillDefinition skill)
    {
        BattleUnitState source = BuildMage("wind_mastery_source", Vector2I.Zero, 7);
        source.source_member_id = "wind_mastery_member";
        AttackEffectResolutionResult empty = BattleDamageResolver.BuildEmptyResolutionResult(SkillId);
        using var mastery = new BattleSkillMasteryService();

        BattleUnitState normal = BuildUnit("wind_mastery_normal", "enemy", Vector2I.Right);
        mastery.RecordTargetResult(source, normal, skill, empty, new[] { FindWind(skill, 7) }, additionalEffectApplied: true);
        _test.Eq(mastery.ResolveActiveSkillMasteryAmount(), 5, "普通目标实际移动应提供5熟练度。");
        mastery.Clear();

        BattleUnitState elite = BuildUnit("wind_mastery_elite", "enemy", Vector2I.Right);
        elite.attribute_snapshot.SetValue("fortune_mark_target", 1);
        mastery.RecordTargetResult(source, elite, skill, empty, new[] { FindWind(skill, 7) }, additionalEffectApplied: true);
        _test.Eq(mastery.ResolveActiveSkillMasteryAmount(), 10, "精英目标实际移动应提供10熟练度。");
        mastery.Clear();

        BattleUnitState boss = BuildUnit("wind_mastery_boss", "enemy", Vector2I.Right);
        boss.attribute_snapshot.SetValue("boss_target", 1);
        mastery.RecordTargetResult(source, boss, skill, empty, new[] { FindWind(skill, 7) }, additionalEffectApplied: true);
        _test.Eq(mastery.ResolveActiveSkillMasteryAmount(), 15, "Boss 目标实际移动应提供15熟练度。");
        mastery.Clear();
    }

    private void TestBarrierOnlyMastery(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("wind_barrier_caster", new Vector2I(6, 2), 1);
        caster.source_member_id = "wind_barrier_caster_member";
        BattleUnitState sphereOwner = BuildUnit("wind_barrier_owner", "enemy", new Vector2I(2, 2));
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "wind_barrier_mastery",
            new Vector2I(9, 5),
            new[] { caster },
            new[] { sphereOwner }
        );
        fixture.Runtime.setup(
            skill_definitions: new Dictionary<StringName, SkillDefinition> { [SkillId] = skill },
            barrier_profile_definitions: BarrierDefinitionTestContent.LoadValidated()
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        ApplyPrismaticSphere(fixture.Runtime, sphereOwner);
        MarkOnlyOrangeLayerActive(fixture.State);

        BattleCommand command = BuildGroundCommand(caster, new Vector2I(5, 2));
        CombatCastVariantDefinition castVariant =
            fixture.Runtime._skill_resolution_rules.ResolveGroundCastVariantDefinition(
                skill,
                caster,
                ""
            );
        IReadOnlyList<Vector2I> rawEffectCoords = fixture.Runtime.BuildGroundEffectCoordsTyped(
            skill,
            new[] { command.target_coord },
            caster.GetAnchorCoord(),
            caster,
            castVariant
        );
        IReadOnlyList<CombatEffectDefinition> unitEffects =
            fixture.Runtime.CollectGroundUnitEffectDefinitionsTyped(skill, castVariant, caster);
        BattleGroundEffectBarrierClipResult clipPreview =
            fixture.Runtime._layered_barrier_service.PreviewGroundEffectBarrierClipResult(
                caster,
                skill,
                unitEffects,
                Array.Empty<CombatEffectDefinition>(),
                rawEffectCoords,
                castVariant
            );
        _test.True(
            clipPreview.Applied,
            $"强风范围应与橙层边界相交。raw={string.Join(",", rawEffectCoords)}"
        );
        BattleGroundSkillValidationResult validation =
            fixture.Runtime.ValidateGroundSkillCommandResultTyped(caster, skill, castVariant, command);
        _test.True(validation.Allowed, $"屏障交互测试施法目标必须合法：{validation.Message}");
        using var batch = new BattleEventBatch();
        bool applied = fixture.Runtime._skill_orchestrator._handle_ground_skill_command(
            caster,
            command,
            skill,
            castVariant,
            batch
        );

        _test.True(applied, $"强风术应能打破虹光法球橙层。logs={string.Join(" | ", batch.LogLinesTyped)}");
        _test.Eq(ActiveBarrierLayerId(fixture.State), new StringName(""), "橙层应被强风术实际打破。");
        _test.Eq(
            fixture.Runtime._skill_mastery_service.ResolveActiveSkillMasteryAmount(),
            5,
            "仅成功打破屏障且未推动单位时应固定获得5熟练度。"
        );
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiConsumesCanonicalWindPreview(SkillDefinition skill)
    {
        BattleUnitState caster = BuildMage("wind_ai_caster", new Vector2I(1, 3), 7);
        BattleUnitState target = BuildUnit("wind_ai_target", "enemy", new Vector2I(2, 3));
        using BattleTestFixture fixture = CreateFixture("wind_ai", skill, caster, target);
        BattleCommand command = BuildGroundCommand(caster, new Vector2I(2, 3));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
            preview_command_callback = fixture.Runtime.PreviewCommand,
        };
        context.SetSkillDefinitions(fixture.Runtime.GetSkillDefinitionIndexTyped());
        using var scoreService = new BattleAiScoreService();
        BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            new[] { FindWind(skill, 7) },
            new Dictionary<string, object>(StringComparer.Ordinal)
        );
        _test.True(score?.forced_move_distance > 0, "AI 应读取按豁免失败概率折算的强风位移距离。");
        _test.True(score?.target_count > 0, "AI 应将可实际推动目标计入候选。");
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(SkillPath, "mage_gust_of_wind_regression");

    private static CombatEffectDefinition FindWind(SkillDefinition skill, int level) =>
        skill?.CombatProfile?.EffectDefinitions.SingleOrDefault(effect =>
            effect?.ForcedMoveModeKind == BattleForcedMoveMode.WindPush
            && effect.IsUnlockedAtSkillLevel(level)
        );

    private BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        BattleUnitState caster,
        params BattleUnitState[] enemies
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(10, 7),
            new[] { caster },
            enemies ?? Array.Empty<BattleUnitState>()
        );
        fixture.Runtime.setup(null, new Dictionary<StringName, SkillDefinition> { [SkillId] = skill });
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        return fixture;
    }

    private static BattleUnitState BuildMage(StringName id, Vector2I coord, int level)
    {
        BattleUnitState unit = BuildUnit(id, "player", coord);
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 200);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 4);
        unit.attribute_snapshot.SetValue("spell_proficiency_bonus", 2);
        unit.SetCurrentMp(200);
        return unit;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(id, faction, coord, currentAp: 2, currentHp: 100);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue("strength_modifier", 0);
        unit.SetBodySizeProjection(1);
        return unit;
    }

    private static BattleCommand BuildGroundCommand(BattleUnitState caster, Vector2I targetCoord) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = targetCoord,
        };

    private static void AddContactField(
        BattleTestFixture fixture,
        Vector2I coord,
        BattleUnitState source,
        StringName fieldId,
        StringName statusId
    )
    {
        BattleCellState cell = fixture.Runtime.GetGridService().GetCellState(fixture.State, coord);
        cell?.timed_terrain_effects.Add(new BattleTerrainEffectState
        {
            field_instance_id = fieldId,
            effect_id = fieldId,
            effect_type = "status",
            source_unit_id = source.unit_id,
            source_skill_id = SkillId,
            target_team_filter = "enemy",
            contact_status_id = statusId,
            contact_status_duration_tu = 30,
            contact_stack_behavior = "add",
            contact_stack_limit = 3,
            remaining_tu = 100,
        });
    }

    private static void ApplyPrismaticSphere(
        BattleRuntimeModule runtime,
        BattleUnitState sphereOwner
    )
    {
        SkillDefinition sphereSkill = TestSkillDefinitionProjection.BuildSkill(
            "mage_prismatic_sphere",
            displayName: "虹光法球",
            tags: new[] { new StringName("mage"), new StringName("magic") }
        );
        CombatEffectDefinition sphereEffect = TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            durationTu: 120,
            saveDc: 15,
            saveDcMode: "static",
            saveAbility: "willpower",
            saveTag: "magic",
            parameters: new Dictionary<string, object>
            {
                ["area_pattern"] = "diamond",
                ["profile_id"] = "prismatic_sphere",
                ["radius_cells"] = 2,
            }
        );
        using var batch = new BattleEventBatch();
        runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
            sphereOwner,
            sphereOwner,
            sphereSkill,
            sphereEffect,
            batch
        );
    }

    private static void MarkOnlyOrangeLayerActive(BattleState state)
    {
        foreach (StringName barrierId in state.LayeredBarrierStore.SortedKeys())
        {
            if (!state.LayeredBarrierStore.TryGet(barrierId, out BattleBarrierInstanceState barrier))
            {
                continue;
            }
            var layers = new List<BattleBarrierLayerState>();
            foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
            {
                if (layer != null && layer.LayerId != new StringName("orange"))
                {
                    layer.Broken = true;
                }
                layers.Add(layer);
            }
            barrier.SetLayers(layers);
            state.LayeredBarrierStore.Put(barrierId, barrier);
        }
    }

    private static StringName ActiveBarrierLayerId(BattleState state)
    {
        foreach (BattleBarrierInstanceState barrier in state.LayeredBarrierStore.ValuesSorted())
        {
            foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
            {
                if (layer != null && !layer.Broken)
                {
                    return layer.LayerId;
                }
            }
        }
        return "";
    }

    private static bool ContainsError(IEnumerable<string> errors, string fragment) =>
        (errors ?? Array.Empty<string>()).Any(error =>
            error?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true
        );
}
