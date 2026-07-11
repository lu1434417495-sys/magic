using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_validation_result_projection_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestSkillExecutionOrchestratorUsesTypedSkillLevelAccessor();
        TestUnitSkillValidationProjectsTypedLists();
        TestGroundSkillValidationParsesAndProjectsStringKeyPayload();
        TestAttackEffectResolutionReaderRequiresTypedCritLock();
        TestRuntimeUnitSkillEffectTypedProjectionPreservesCritLock();
        TestRuntimeChainDamageInternalHelperStillExecutesTypedChainEffects();
        TestTargetCollectionSortsAndProjectsCoords();
        RequestTestExit(_test.Finish("Battle validation result projection regression"));
    }

    private void TestSkillExecutionOrchestratorUsesTypedSkillLevelAccessor()
    {
        var runtime = new BattleRuntimeModule();
        SkillDefinition lockedZeroSkill = TestSkillDefinitionProjection.BuildSkill(
            "locked_zero_skill",
            maxLevel: 0
        );
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                [lockedZeroSkill.SkillId] = lockedZeroSkill,
            }
        );

        var orchestrator = new BattleSkillExecutionOrchestrator();
        BattleUnitState activeOnlyUnit = null;
        BattleUnitState lockedZeroUnit = null;
        try
        {
            orchestrator.Setup(runtime);
            activeOnlyUnit = new BattleUnitState
            {
                known_active_skill_ids = new GStringNameArray { "active_only_skill" },
            };
            _test.Eq(
                orchestrator._get_unit_skill_level(activeOnlyUnit, "active_only_skill"),
                1,
                "只有 active skill id、没有显式等级时应继续按 1 级处理。"
            );

            lockedZeroUnit = new BattleUnitState
            {
                known_active_skill_ids = new GStringNameArray { "locked_zero_skill" },
            };
            lockedZeroUnit.known_skill_level_map[new StringName("locked_zero_skill")] = 0;
            _test.Eq(
                orchestrator._get_unit_skill_level(lockedZeroUnit, "locked_zero_skill"),
                0,
                "静态 0 级技能即使出现在 active skill ids 中，也应继续保持 0 级。"
            );

            var unsortedCoords = new Godot.Collections.Array<Vector2I>
            {
                new Vector2I(2, 3),
                new Vector2I(1, 1),
                new Vector2I(0, 1),
            };
            var sortedCoords = orchestrator._sort_coords(unsortedCoords);
            _test.Eq(sortedCoords[0], new Vector2I(0, 1), "typed 排序入口应先按 Y 再按 X 排序。");
            _test.Eq(sortedCoords[1], new Vector2I(1, 1), "typed 排序入口排序结果应稳定。");
            _test.Eq(sortedCoords[2], new Vector2I(2, 3), "typed 排序入口应保留较大的坐标在后。");
        }
        finally
        {
            orchestrator.DisposeRuntime();
            BattleTestFixture.DisposeBattleUnit(activeOnlyUnit);
            BattleTestFixture.DisposeBattleUnit(lockedZeroUnit);
            BattleTestFixture.DisposeRuntime(runtime);
        }
    }

    private void TestUnitSkillValidationProjectsTypedLists()
    {
        var targetUnit = new BattleUnitState { unit_id = "target_1" };
        try
        {
            BattleUnitSkillValidationResult result = BattleUnitSkillValidationResult.AllowedResult(
                new[] { new StringName("target_1") },
                new[] { targetUnit },
                new[] { new StringName("chain_1") },
                new[] { new Vector2I(2, 3) },
                "ok"
            );

            Godot.Collections.Dictionary payload =
                BattleValidationResultProjection.ProjectUnitSkill(result);

            _test.True(payload["allowed"].AsBool(), "单位技能 validation 应投影 allowed。");
            _test.Eq(payload["message"].AsString(), "ok", "单位技能 validation 应投影 message。");
            _test.Eq(
                payload["target_unit_ids"].AsGodotArray<StringName>()[0],
                new StringName("target_1"),
                "单位技能 validation 应投影目标 id。"
            );
            Godot.Collections.Dictionary projectedTargetUnit =
                payload["target_units"].AsGodotArray()[0].AsGodotDictionary();
            _test.Eq(
                projectedTargetUnit["unit_id"].AsString(),
                targetUnit.unit_id.ToString(),
                "单位技能 validation 应投影目标 unit。"
            );
            _test.Eq(
                payload["random_chain_candidate_unit_ids"].AsGodotArray<StringName>()[0],
                new StringName("chain_1"),
                "单位技能 validation 应投影随机连锁候选。"
            );
            _test.Eq(
                payload["preview_coords"].AsGodotArray<Vector2I>()[0],
                new Vector2I(2, 3),
                "单位技能 validation 应投影 preview coord。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(targetUnit);
        }
    }

    private void TestGroundSkillValidationParsesAndProjectsStringKeyPayload()
    {
        var source = new Godot.Collections.Dictionary
        {
            ["allowed"] = true,
            ["message"] = "cast",
            ["target_coords"] = new Godot.Collections.Array<Vector2I>
            {
                new(4, 5),
            },
            ["preview_coords"] = new Godot.Collections.Array<Vector2I>
            {
                new(4, 5),
                new(5, 5),
            },
            ["direction"] = Vector2I.Right,
            ["distance"] = 2,
            ["resolved_anchor_coord"] = new Vector2I(6, 5),
        };

        BattleGroundSkillValidationResult result = BattleGroundSkillValidationResult.FromDictionary(
            source
        );
        Godot.Collections.Dictionary payload =
            BattleValidationResultProjection.ProjectGroundSkill(result);

        _test.True(result.Allowed, "地面技能 validation 应从 string-key payload 解析 allowed。");
        _test.Eq(result.Message, "cast", "地面技能 validation 应解析 message。");
        _test.Eq(result.TargetCoords[0], new Vector2I(4, 5), "地面技能 validation 应解析目标格。");
        _test.Eq(result.PreviewCoords[1], new Vector2I(5, 5), "地面技能 validation 应解析 preview 格。");
        _test.Eq(result.Direction, Vector2I.Right, "地面技能 validation 应解析方向。");
        _test.Eq(result.Distance, 2, "地面技能 validation 应解析距离。");
        _test.Eq(
            payload["resolved_anchor_coord"].AsVector2I(),
            new Vector2I(6, 5),
            "地面技能 validation 应投影 resolved anchor。"
        );
    }

    private void TestAttackEffectResolutionReaderRequiresTypedCritLock()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["applied"] = true,
            ["attack_success"] = true,
            ["attack_resolution"] = "hit",
            ["crit_locked"] = true,
            ["critical_hit"] = false,
            ["skill_id"] = "black_contract_push",
        };

        AttackEffectResolutionResult result = AttackEffectResolutionResultReader.ReadResolverResult(
            payload,
            new AttackCheckInput(skillId: "black_contract_push")
        );
        Godot.Collections.Dictionary projected = AttackEffectResolutionResultReader.BuildGodotPayload(
            result
        );

        _test.False(
            result.AttackCheck.CritLocked,
            "AttackEffectResolutionResultReader 不应从 payload 回填 crit_locked。"
        );
        _test.False(
            projected.GetValueOrDefault("crit_locked", false).AsBool(),
            "AttackEffectResolutionResult payload projection 应只反映 typed AttackCheckInput。"
        );

        AttackEffectResolutionResult typedResult = AttackEffectResolutionResultReader.ReadResolverResult(
            payload,
            new AttackCheckInput(skillId: "black_contract_push", critLocked: true)
        );
        Godot.Collections.Dictionary typedProjected =
            AttackEffectResolutionResultReader.BuildGodotPayload(typedResult);
        _test.True(
            typedResult.AttackCheck.CritLocked,
            "AttackEffectResolutionResultReader 应保留正式 typed AttackCheckInput 的 crit_locked=true。"
        );
        _test.True(
            typedProjected.GetValueOrDefault("crit_locked", false).AsBool(),
            "AttackEffectResolutionResult payload projection 应输出正式 typed crit_locked=true。"
        );
    }

    private void TestTargetCollectionSortsAndProjectsCoords()
    {
        BattleTargetCollectionResult result = BattleTargetCollectionResult.HandledResult(
            new[] { new Vector2I(3, 2), new Vector2I(1, 1), new Vector2I(2, 1) }
        );

        Godot.Collections.Dictionary payload =
            BattleValidationResultProjection.ProjectTargetCollection(result);

        _test.True(result.Handled, "目标收集结果应保留 handled。");
        _test.Eq(result.TargetCoords[0], new Vector2I(1, 1), "目标收集结果应按 y/x 排序。");
        _test.Eq(result.TargetCoords[1], new Vector2I(2, 1), "目标收集结果排序应稳定按 x。");
        _test.Eq(
            payload["target_coords"].AsGodotArray<Vector2I>()[2],
            new Vector2I(3, 2),
            "目标收集结果应投影排序后的 coords。"
        );
    }

    private void TestRuntimeUnitSkillEffectTypedProjectionPreservesCritLock()
    {
        var runtime = new BattleRuntimeModule();
        ProgressionContentRegistry progressionContent = new(new TestContentResourceLoader());
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            progressionContent.GetSkillDefinitionsTyped();
        runtime.setup(
            null,
            skillDefinitions,
            new Dictionary<StringName, EnemyTemplateDef>(),
            new Dictionary<StringName, EnemyAiBrainDef>(),
            null,
            null,
            new Dictionary<StringName, ItemDefinition>(),
            null
        );
        BattleTestFixture.ConfigureDamageResolverForTests(runtime, new DeterministicBattleDamageResolver());
        BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));

        BattleState state = null;
        BattleUnitState source = null;
        BattleUnitState target = null;
        SkillDefinition skillDefinition = null;
        CombatCastVariantDefinition castVariant = null;
        List<CombatEffectDefinition> effects = null;
        Godot.Collections.Dictionary projected = null;
        try
        {
            state = new BattleState { map_size = new Vector2I(4, 3) };
            source = new BattleUnitState
            {
                unit_id = "source",
                faction_id = "ally",
                coord = new Vector2I(0, 0),
                current_hp = 40,
                current_ap = 2,
                known_active_skill_ids = new GStringNameArray { "black_contract_push" },
            };
            source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
            source.attribute_snapshot.SetValue("action_points", 2);
            source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
            source.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 12);
            source.known_skill_level_map["black_contract_push"] = 1;
            target = new BattleUnitState
            {
                unit_id = "target",
                faction_id = "enemy",
                coord = new Vector2I(1, 0),
                current_hp = 40,
            };
            target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
            target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 999);
            skillDefinition = skillDefinitions["black_contract_push"];
            castVariant = runtime._skill_resolution_rules.ResolveUnitCastVariantDefinition(
                skillDefinition,
                source,
                "action_tithe"
            );
            effects = runtime._skill_resolution_rules.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                source
            );
            state.SetUnit(source);
            state.SetUnit(target);
            state.ally_unit_ids = new GStringNameArray { source.unit_id };
            state.enemy_unit_ids = new GStringNameArray { target.unit_id };
            state.active_unit_id = source.unit_id;
            runtime.SetupStateForTests(state);

            BattleSkillExecutionOrchestrator.UnitSkillEffectResolution typed = runtime
                ._skill_orchestrator
                .ResolveUnitSkillEffectResult(
                    source,
                    target,
                    skillDefinition,
                    effects
                );
            projected = AttackEffectResolutionResultReader.BuildGodotPayload(
                typed.Result
            );
            if (typed.CustomLogLines.Count != 0)
            {
                var customLines = new Godot.Collections.Array();
                foreach (string line in typed.CustomLogLines)
                {
                    customLines.Add(line);
                }
                projected["custom_log_lines"] = customLines;
            }

            _test.True(projected.ContainsKey("damage"), "runtime unit skill effect typed projection 应输出 damage。");
            _test.Eq(
                projected.ContainsKey("custom_log_lines"),
                typed.CustomLogLines.Count != 0,
                "runtime unit skill effect typed projection 应按 custom log lines 决定 payload presence。"
            );
            _test.True(
                typed.Result.AttackCheck.CritLocked && typed.Result.AttackCheck.ForceHitNoCrit,
                "force_hit_no_crit 技能的 typed attack check 应保留 crit_locked/force_hit_no_crit 语义。"
            );
            _test.True(
                projected.GetValueOrDefault("crit_locked", false).AsBool(),
                "runtime unit skill effect typed projection 应输出 force_hit_no_crit 的 crit_locked=true。"
            );
        }
        finally
        {
            projected = null;
            effects = null;
            castVariant = null;
            skillDefinition = null;
            skillDefinitions = null;
            BattleTestFixture.DisposeBattleFixture(runtime, state);
            progressionContent.Dispose();
        }
    }

    private void TestRuntimeChainDamageInternalHelperStillExecutesTypedChainEffects()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        BattleTestFixture.ConfigureDamageResolverForTests(runtime, new DeterministicBattleDamageResolver());

        BattleState state = null;
        BattleUnitState source = null;
        BattleUnitState primary = null;
        BattleUnitState chained = null;
        SkillDefinition skill = null;
        List<CombatEffectDefinition> effectDefs = null;
        BattleEventBatch batch = null;
        try
        {
            state = BuildFlatBattleState(new Vector2I(4, 3));

            source = MakeChainTestUnit("source", "ally", new Vector2I(0, 1));
            primary = MakeChainTestUnit("primary", "enemy", new Vector2I(1, 1));
            chained = MakeChainTestUnit("chained", "enemy", new Vector2I(2, 1));
            int chainedHpBefore = chained.current_hp;

            state.SetUnit(source);
            state.SetUnit(primary);
            state.SetUnit(chained);
            runtime._grid_service.PlaceUnit(state, source, source.coord, true);
            runtime._grid_service.PlaceUnit(state, primary, primary.coord, true);
            runtime._grid_service.PlaceUnit(state, chained, chained.coord, true);
            runtime.SetupStateForTests(state);

            skill = BuildChainTestSkill();
            effectDefs = new List<CombatEffectDefinition>
            {
                BuildChainDamagePayloadEffect(),
                TestSkillDefinitionProjection.BuildEffect("chain_damage"),
            };
            batch = new BattleEventBatch();

            runtime._skill_orchestrator._apply_chain_damage_effects(
                source,
                primary,
                skill,
                effectDefs,
                new AttackEffectResolutionResult { Applied = true, SkillId = skill.SkillId },
                batch,
                "连锁测试"
            );

            _test.Eq(
                chained.current_hp,
                chainedHpBefore - 1,
                "runtime chain damage wrapper 应继续通过 typed primary result 解析命中次级目标。"
            );
            _test.True(
                batch.changed_unit_ids.Contains(chained.unit_id),
                "runtime chain damage wrapper 应继续记录次级目标 changed_unit_id。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state);
            GodotSharpCleanup.DisposeBatch(batch);
        }
    }

    private static BattleState BuildFlatBattleState(Vector2I mapSize)
    {
        BattleState state = new() { map_size = mapSize };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState MakeChainTestUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 30,
            current_ap = 2,
            current_stamina = 20,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        return unit;
    }

    private static SkillDefinition BuildChainTestSkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "chain_arc_projection",
            displayName: "Chain Arc Projection",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "chain_arc_projection",
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangePattern: "fixed",
                rangeValue: 4
            )
        );
    }

    private static CombatEffectDefinition BuildChainDamagePayloadEffect()
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            power: 1
        );
    }
}
