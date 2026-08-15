using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_battle_ground_effect_typed_sets_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestGroundUnitEffectsDoNotPushUnitsOutsideArea();
        TestGroundUnitEffectsMoveAffectedUnitsFarToNear();
        TestSpecialForcedMoveUsesTypedContextDirection();
        TestGroundApplicationResultsProjectInternalBoundary();
        TestSquare2GroundEffectCoordsExpandAndSort();
        TestDuplicateWeaponAttackEffectDamagesGroundTargetOnce();
        TestEdgeClearAppliesThroughGroundTerrainEffectPath();
        RequestTestExit(_test.Finish("Battle ground effect typed sets regression"));
    }

    private void TestGroundUnitEffectsDoNotPushUnitsOutsideArea()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundUnitEffectsResult result =
            fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                fixture.Source,
                fixture.Skill,
                null,
                new[] { fixture.WindPushEffect },
                new List<Vector2I> { new Vector2I(1, 0) },
                batch,
                new List<Vector2I> { new Vector2I(1, 0) }
            );

        _test.False(result.Applied, "锥形外阻挡单位不得被 wind push 递归带动。");
        _test.Eq(result.AffectedUnitCount, 0, "零位移不得报告 affected unit。");
        _test.Eq(fixture.Front.GetAnchorCoord(), new Vector2I(1, 0), "范围内目标应被锥形外单位挡住。");
        _test.Eq(fixture.Back.GetAnchorCoord(), new Vector2I(2, 0), "锥形外阻挡单位必须保持原位。");
        CleanupFixture(fixture, batch);
    }

    private void TestGroundUnitEffectsMoveAffectedUnitsFarToNear()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundUnitEffectsResult result =
            fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                fixture.Source,
                fixture.Skill,
                null,
                new[] { fixture.WindPushEffect },
                new List<Vector2I> { new Vector2I(1, 0), new Vector2I(2, 0) },
                batch,
                new List<Vector2I> { new Vector2I(1, 0) }
            );

        _test.True(result.Applied, "同一风区内的相邻目标应能按风向整体后移。");
        _test.Eq(result.AffectedUnitCount, 2, "每个实际移动的风区目标都应计入 affected set。");
        _test.Eq(fixture.Front.GetAnchorCoord(), new Vector2I(2, 0), "近端目标应在远端目标腾空后移动。");
        _test.Eq(fixture.Back.GetAnchorCoord(), new Vector2I(3, 0), "远端目标必须先沿风向移动。");
        CleanupFixture(fixture, batch);
    }

    private void TestSpecialForcedMoveUsesTypedContextDirection()
    {
        Fixture fixture = BuildForcedMoveContextFixture();
        var batch = new BattleEventBatch();
        BattleSpecialSkillResult result = fixture.Runtime.ApplyUnitSkillSpecialEffectsResult(
            fixture.Source,
            fixture.Front,
            fixture.Skill,
            null,
            new[] { fixture.WindPushEffect },
            batch,
            BattleForcedMoveContext.FromDirection(Vector2I.Right)
        );

        _test.True(result.Applied, "typed forced move context 应触发 wind_push。");
        _test.Eq(result.MovedSteps, 1, "typed forced move context 应记录移动步数。");
        _test.Eq(
            fixture.Front.GetAnchorCoord(),
            new Vector2I(3, 0),
            "typed context direction 应覆盖 source->target fallback 方向。"
        );
        CleanupFixture(fixture, batch);
    }

    private void TestGroundApplicationResultsProjectInternalBoundary()
    {
        Godot.Collections.Dictionary unitPayload =
            BattleGroundEffectApplicationResultProjection.ProjectUnitEffects(
                new BattleGroundUnitEffectsResult(true, 3, 14, 2, 1)
            );
        _test.True(ReadBool(unitPayload, "applied"), "ground unit result 应投影 applied。");
        _test.Eq(
            ReadInt(unitPayload, "affected_unit_count"),
            3,
            "ground unit result 应投影 affected count。"
        );
        _test.Eq(ReadInt(unitPayload, "damage"), 14, "ground unit result 应投影 damage。");
        _test.Eq(ReadInt(unitPayload, "healing"), 2, "ground unit result 应投影 healing。");
        _test.Eq(ReadInt(unitPayload, "kill_count"), 1, "ground unit result 应投影 kill count。");

        Godot.Collections.Dictionary terrainPayload =
            BattleGroundEffectApplicationResultProjection.ProjectTerrainEffects(
                new BattleGroundTerrainEffectsResult(true)
            );
        _test.True(ReadBool(terrainPayload, "applied"), "ground terrain result 应投影 applied。");

        Godot.Collections.Dictionary windPayload =
            BattleGroundEffectApplicationResultProjection.ProjectWindPush(
                new BattleGroundWindPushResult(
                    true,
                    new[] { new StringName("front"), new StringName("back") }
                )
            );
        _test.True(ReadBool(windPayload, "applied"), "wind push result 应投影 applied。");
        Godot.Collections.Array affectedIds = windPayload["affected_unit_ids"].AsGodotArray();
        _test.Eq(
            ProgressionDataUtils.to_string_name(affectedIds[0]),
            new StringName("front"),
            "wind push result 应投影第一个 affected id。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(affectedIds[1]),
            new StringName("back"),
            "wind push result 应投影第二个 affected id。"
        );
    }

    private void TestSquare2GroundEffectCoordsExpandAndSort()
    {
        Fixture fixture = BuildGroundEffectCoordsFixture();
        try
        {
            CombatCastVariantDefinition castVariant = TestSkillDefinitionProjection.BuildCastVariant(
                "square2_probe",
                minSkillLevel: 0,
                effects: Array.Empty<CombatEffectDefinition>(),
                parameters: new Dictionary<string, object> { ["square2_corner"] = "top_left" }
            );
            IReadOnlyList<Vector2I> typedCoords = fixture.Runtime._ground_effect_service
                .BuildGroundEffectCoords(
                    null,
                    new List<Vector2I> { new Vector2I(1, 1) },
                    new Vector2I(-1, -1),
                    null,
                    castVariant
                );

            _test.Eq(typedCoords.Count, 4, "typed ground effect coords 应展开 square2。");
            _test.Eq(typedCoords[0], new Vector2I(1, 1), "typed ground effect coords 应按 Y/X 排序。");
            _test.Eq(typedCoords[3], new Vector2I(2, 2), "typed ground effect coords 应包含右下角。");
        }
        finally
        {
            CleanupFixture(fixture, null);
        }
    }

    private void TestDuplicateWeaponAttackEffectDamagesGroundTargetOnce()
    {
        Fixture fixture = BuildWindPushFixture();
        try
        {
            CombatEffectDefinition weaponDamage = TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 6,
                damageTag: "force",
                resolveAsWeaponAttack: true
            );
            fixture.Source.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
            fixture.Front.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
            fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
            int hpBefore = fixture.Front.GetCurrentHp();
            using var batch = new BattleEventBatch();
            AttackEffectResolutionResult result = fixture
                .Runtime
                ._ground_effect_service
                .ResolveGroundUnitEffectResult(
                    fixture.Source,
                    fixture.Front,
                    fixture.Skill,
                    new[] { weaponDamage, weaponDamage },
                    batch
                );

            _test.True(result.AttackSuccess, "ground weapon-attack 路径应完成真实命中结算。");
            _test.Eq(result.Damage, 6, "重复引用同一伤害效果时，结算结果只能包含一次伤害。");
            _test.Eq(
                fixture.Front.GetCurrentHp(),
                hpBefore - 6,
                "重复效果实例不得让 ground 目标被扣血两次。"
            );
        }
        finally
        {
            CleanupFixture(fixture, null);
        }
    }

    private void TestEdgeClearAppliesThroughGroundTerrainEffectPath()
    {
        CombatEffectDefinition edgeClearEffect =
            TestSkillDefinitionProjection.BuildEffect("edge_clear");
        CombatEffectDefinition directDamageEffect =
            TestSkillDefinitionProjection.BuildEffect("damage");
        Fixture fixture = BuildEdgeClearFixture(
            new[] { edgeClearEffect, directDamageEffect }
        );
        try
        {
            IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions =
                fixture.Runtime.CollectGroundTerrainEffectDefinitionsTyped(
                    fixture.Skill,
                    null,
                    fixture.Source
                );
            _test.Eq(
                terrainEffectDefinitions.Count,
                1,
                "正式 ground terrain effect collection 应只收集 edge_clear，不应混入 unit damage。"
            );
            _test.True(
                terrainEffectDefinitions.Contains(edgeClearEffect),
                "edge_clear 必须被 IsGroundPayloadEffect 分类并进入正式 terrain effect collection。"
            );
            _test.False(
                terrainEffectDefinitions.Contains(directDamageEffect),
                "direct damage 不应进入正式 terrain effect collection。"
            );

            AssertEdgeClearOutcome(
                fixture,
                terrainEffectDefinitions,
                "wall",
                new Vector2I(0, 0),
                new Vector2I(1, 0),
                expectedApplied: true
            );
            AssertEdgeClearOutcome(
                fixture,
                terrainEffectDefinitions,
                "door",
                new Vector2I(0, 0),
                new Vector2I(0, 1),
                expectedApplied: true
            );
            AssertEdgeClearOutcome(
                fixture,
                terrainEffectDefinitions,
                "gate",
                new Vector2I(0, 0),
                new Vector2I(1, 0),
                expectedApplied: true
            );
            AssertEdgeClearOutcome(
                fixture,
                terrainEffectDefinitions,
                "non_clearable_kind",
                new Vector2I(0, 0),
                new Vector2I(0, 1),
                expectedApplied: false
            );
        }
        finally
        {
            CleanupFixture(fixture, null);
        }
    }

    private void AssertEdgeClearOutcome(
        Fixture fixture,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        StringName featureKind,
        Vector2I first,
        Vector2I second,
        bool expectedApplied
    )
    {
        Vector2I direction = second - first;
        _test.True(
            fixture.Runtime._grid_service.SetEdgeFeature(
                fixture.State,
                first,
                direction,
                BuildBlockingEdgeFeature(featureKind)
            ),
            $"edge_clear 前置边缘应能写入：{featureKind} direction={direction}"
        );

        using BattleEventBatch batch = new();
        BattleGroundTerrainEffectsResult result =
            fixture.Runtime.ApplyGroundTerrainEffectsResultTyped(
                fixture.Source,
                fixture.Skill,
                terrainEffectDefinitions,
                new[] { first, second },
                batch
            );
        BattleEdgeFeatureState remainingFeature = fixture
            .Runtime
            ._grid_service
            .GetCellState(fixture.State, first)
            ?.GetEdgeFeature(direction);

        _test.Eq(
            result.Applied,
            expectedApplied,
            $"edge_clear 正式地形效果入口对 {featureKind} 的 applied 结果应符合默认白名单。"
        );
        if (expectedApplied)
        {
            _test.True(
                remainingFeature != null && remainingFeature.IsEmpty(),
                $"默认 edge_clear 应保留格子并把 {featureKind} 边缘规范化为空状态。"
            );
            _test.Eq(
                batch.ChangedCoordsTyped.Count,
                2,
                $"移除 {featureKind} 后应把边缘两端都标记为变化坐标。"
            );
            _test.True(
                batch.ChangedCoordsTyped.Contains(first)
                && batch.ChangedCoordsTyped.Contains(second),
                $"移除 {featureKind} 后 changed coords 应包含边缘两端。"
            );
        }
        else
        {
            _test.True(
                remainingFeature != null && remainingFeature.feature_kind == featureKind,
                $"默认 edge_clear 不应移除非白名单边缘 {featureKind}。"
            );
            _test.Eq(
                batch.ChangedCoordsTyped.Count,
                0,
                $"拒绝移除 {featureKind} 时不应报告变化坐标。"
            );
        }
    }

    private Fixture BuildEdgeClearFixture(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();

        StringName skillId = "typed_edge_clear_skill";
        BattleState state = BuildState(new Vector2I(2, 2));
        BattleUnitState source = BuildUnit(
            "typed_edge_clear_source",
            "player",
            new Vector2I(0, 0)
        );
        source.AddKnownActiveSkill(skillId);
        source.SetKnownSkillLevelTyped(skillId, 1);
        state.active_unit_id = source.unit_id;
        AddUnit(runtime, state, source);
        runtime.SetupStateForTests(state);

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Skill = TestSkillDefinitionProjection.BuildSkill(
                skillId,
                combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                    skillId,
                    effects: effectDefinitions,
                    targetMode: "ground"
                )
            ),
        };
    }

    private static BattleEdgeFeatureState BuildBlockingEdgeFeature(StringName featureKind)
    {
        return new BattleEdgeFeatureState
        {
            feature_kind = featureKind,
            render_kind = "wall",
            render_layers = 1,
            blocks_move = true,
            blocks_occupancy = true,
            blocks_los = true,
        };
    }

    private Fixture BuildWindPushFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();

        BattleState state = BuildState(new Vector2I(5, 1));
        BattleUnitState source = BuildUnit("typed_wind_source", "player", new Vector2I(0, 0));
        BattleUnitState front = BuildUnit("typed_wind_front", "enemy", new Vector2I(1, 0));
        BattleUnitState back = BuildUnit("typed_wind_back", "enemy", new Vector2I(2, 0));
        AddUnit(runtime, state, source);
        AddUnit(runtime, state, front);
        AddUnit(runtime, state, back);
        runtime.SetupStateForTests(state);

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Front = front,
            Back = back,
            Skill = TestSkillDefinitionProjection.BuildSkill(
                "typed_wind_push_skill",
                combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                    "typed_wind_push_skill",
                    targetTeamFilter: "enemy"
                )
            ),
            WindPushEffect = BuildWindPushEffect()
        };
    }

    private Fixture BuildForcedMoveContextFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();

        BattleState state = BuildState(new Vector2I(5, 1));
        BattleUnitState source = BuildUnit(
            "typed_forced_context_source",
            "player",
            new Vector2I(4, 0)
        );
        BattleUnitState front = BuildUnit(
            "typed_forced_context_target",
            "enemy",
            new Vector2I(2, 0)
        );
        AddUnit(runtime, state, source);
        AddUnit(runtime, state, front);
        runtime.SetupStateForTests(state);

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Front = front,
            Skill = TestSkillDefinitionProjection.BuildSkill(
                "typed_forced_context_skill",
                combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                    "typed_forced_context_skill",
                    targetTeamFilter: "enemy"
                )
            ),
            WindPushEffect = BuildWindPushEffect()
        };
    }

    private Fixture BuildGroundEffectCoordsFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        BattleState state = BuildState(new Vector2I(4, 4));
        runtime.SetupStateForTests(state);
        return new Fixture { Runtime = runtime, State = state };
    }

    private static void CleanupFixture(Fixture fixture, BattleEventBatch batch)
    {
        if (fixture == null)
        {
            return;
        }
        fixture.Runtime?._state?.ClearUnits();
        fixture.Runtime?._state?.ClearCells();
        if (fixture.Runtime != null)
        {
            fixture.Runtime.SetupStateForTests(null);
            fixture.Runtime.dispose();
        }
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "battle_ground_effect_typed_sets_regression",
            phase = "unit_acting",
            active_unit_id = "typed_wind_source",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(coord, new BattleCellState { coord = coord, passable = true });
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static CombatEffectDefinition BuildWindPushEffect()
    {
        return new CombatEffectDefinition(
            effectType: "forced_move",
            effectTargetTeamFilter: "enemy",
            statusId: default,
            saveFailureStatusId: default,
            terrainEffectId: default,
            terrainReplaceTo: default,
            heightDelta: 0,
            requiresWeapon: false,
            addWeaponDice: false,
            preventRepeatTarget: false,
            forcedMoveMode: "wind_push",
            minSkillLevel: 0,
            maxSkillLevel: -1,
            damageTag: default,
            damageRatioPercent: 100,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: default,
            hpRatioThresholdPercent: 0,
            damageCategory: default,
            drBypassTag: default,
            diceCount: 0,
            diceSides: 0,
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
            effectTags: Array.Empty<StringName>(),
            forcedMoveDistance: 1
        );
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
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        return unit;
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        if (unit.faction_id == new StringName("player"))
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true),
            $"单位应能放入测试棋盘：{unit.unit_id}"
        );
    }

    private static bool ReadBool(Godot.Collections.Dictionary source, string key) =>
        source != null
        && source.ContainsKey(key)
        && source[key].AsBool();

    private static int ReadInt(Godot.Collections.Dictionary source, string key) =>
        source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleState State;
        public BattleUnitState Source;
        public BattleUnitState Front;
        public BattleUnitState Back;
        public SkillDefinition Skill;
        public CombatEffectDefinition WindPushEffect;
    }
}
