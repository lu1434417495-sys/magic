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
        RequestTestExit(_test.Finish("Battle ground effect typed sets regression"));
    }

    private void TestGroundUnitEffectsDoNotPushUnitsOutsideArea()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundUnitEffectsResult result =
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, batch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                    fixture.Source, fixture.Skill, null,
                    new[] { fixture.WindPushEffect },
                    new[] { new Vector2I(1, 0) }, batch,
                    new[] { new Vector2I(1, 0) }
                )
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
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, batch,
                () => fixture.Runtime.ApplyGroundUnitEffectsResultTyped(
                fixture.Source,
                fixture.Skill,
                null,
                new[] { fixture.WindPushEffect },
                new List<Vector2I> { new Vector2I(1, 0), new Vector2I(2, 0) },
                batch,
                new List<Vector2I> { new Vector2I(1, 0) }
            )
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
            var cases = new[]
            {
                (Wire: "top_left", Kind: CombatCastSquare2CornerKind.TopLeft,
                    Coords: new[] { new Vector2I(1, 1), new Vector2I(2, 1), new Vector2I(1, 2), new Vector2I(2, 2) },
                    Edge: new Vector2I(3, 3)),
                (Wire: "top_right", Kind: CombatCastSquare2CornerKind.TopRight,
                    Coords: new[] { new Vector2I(0, 1), new Vector2I(1, 1), new Vector2I(0, 2), new Vector2I(1, 2) },
                    Edge: new Vector2I(0, 3)),
                (Wire: "bottom_left", Kind: CombatCastSquare2CornerKind.BottomLeft,
                    Coords: new[] { new Vector2I(1, 0), new Vector2I(2, 0), new Vector2I(1, 1), new Vector2I(2, 1) },
                    Edge: new Vector2I(3, 0)),
                (Wire: "bottom_right", Kind: CombatCastSquare2CornerKind.BottomRight,
                    Coords: new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(1, 1) },
                    Edge: new Vector2I(0, 0)),
            };
            foreach (var testCase in cases)
            {
                CombatCastVariantDefinition variant = ImportSquare2Variant(testCase.Wire);
                _test.Eq(variant.Square2Corner, testCase.Kind,
                    $"{testCase.Wire} 应由 JSON 保真投影为 typed corner。");
                AssertGroundCoords(fixture, variant, new[] { new Vector2I(1, 1) },
                    testCase.Coords, $"{testCase.Wire} 四格展开");
                AssertGroundCoords(fixture, variant, new[] { testCase.Edge },
                    new[] { testCase.Edge }, $"{testCase.Wire} 地图角落裁剪");
            }

            CombatCastVariantDefinition topLeft = ImportSquare2Variant("top_left");
            AssertGroundCoords(fixture, topLeft, new[] { new Vector2I(3, 1) },
                new[] { new Vector2I(3, 1), new Vector2I(3, 2) }, "边缘裁剪保留两格");
            AssertGroundCoords(fixture, topLeft, Array.Empty<Vector2I>(),
                Array.Empty<Vector2I>(), "空目标不展开");
            AssertGroundCoords(fixture, topLeft,
                new[] { new Vector2I(2, 2), new Vector2I(0, 1) },
                new[] { new Vector2I(0, 1), new Vector2I(2, 2) }, "显式多格目标只排序不再展开");

            CombatCastVariantDefinition omitted = ImportSquare2Variant(null);
            _test.False(omitted.Square2Corner.HasValue, "省略 corner 必须保留未配置语义。");
            AssertGroundCoords(fixture, omitted, new[] { new Vector2I(1, 1) },
                new[] { new Vector2I(1, 1) }, "未配置 corner 不触发局部展开");

            bool invalidCornerRejected = false;
            try
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "invalid_corner", 0, Array.Empty<CombatEffectDefinition>(),
                    square2Corner: (CombatCastSquare2CornerKind)(-1)
                );
            }
            catch (ArgumentOutOfRangeException)
            {
                invalidCornerRejected = true;
            }
            _test.True(invalidCornerRejected, "Definition 不得接收未定义的 corner 枚举值。");
        }
        finally
        {
            CleanupFixture(fixture, null);
        }
    }

    private CombatCastVariantDefinition ImportSquare2Variant(string corner)
    {
        string payload = corner == null
            ? ""
            : $",\"payload\":{{\"square2_corner\":\"{corner}\"}}";
        ContentImportStageResult<SkillImportModel> import = SkillJsonImportParser.Parse(
            new JsonContentEntryContext("skills", "square2_probe", "fixture.json#square2_probe", "/entries/0"),
            "{\"skill_id\":\"square2_probe\",\"display_name\":\"Square\","
                + "\"combat_profile\":{\"skill_id\":\"square2_probe\",\"cast_variants\":["
                + "{\"variant_id\":\"square\",\"footprint_pattern\":\"square2\",\"required_coord_count\":1"
                + payload + "}]}}"
        );
        if (!import.HasValue)
            throw new InvalidOperationException(string.Join(" | ", import.Diagnostics.Select(d => d.RuleId)));
        return SkillDefinitionProjector.Project(import.Value).CombatProfile.CastVariants[0];
    }

    private void AssertGroundCoords(
        Fixture fixture,
        CombatCastVariantDefinition variant,
        IReadOnlyList<Vector2I> targets,
        IReadOnlyList<Vector2I> expected,
        string label
    )
    {
        BattleGroundEffectService service = fixture.Runtime._ground_effect_service;
        IReadOnlyList<Vector2I> stateCoords = service.BuildGroundEffectCoords(
            null, targets, new Vector2I(-1, -1), (BattleUnitState)null, variant
        );
        IReadOnlyList<Vector2I> viewCoords = service.BuildGroundEffectCoords(
            null, targets, new Vector2I(-1, -1), default(BattleUnitReadView), variant
        );
        _test.True(stateCoords.SequenceEqual(expected),
            $"{label}：state 入口坐标应正确且按 Y/X 排序；actual={string.Join(",", stateCoords)}。");
        _test.True(viewCoords.SequenceEqual(expected),
            $"{label}：read-view 入口坐标应与 state 入口一致；actual={string.Join(",", viewCoords)}。");
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
            BattleTestFixture.ConfigureDamageResolverForTests(
                fixture.Runtime,
                new FixedHitMaxDamageResolver()
            );
            BattleTestFixture.ConfigureHitResolverForTests(
                fixture.Runtime,
                new FixedHitResolver()
            );
            int hpBefore = fixture.Front.GetCurrentHp();
            using var batch = new BattleEventBatch();
            AttackEffectResolutionResult result = default;
            BattleReactionRootTestHelper.ExecuteLogicalAttack(
                fixture.Runtime, batch, fixture.Source, new[] { weaponDamage, weaponDamage },
                actionContext => result = fixture
                .Runtime
                ._ground_effect_service
                .ResolveGroundUnitEffectResult(
                    fixture.Source,
                    fixture.Front,
                    fixture.Skill,
                    new[] { weaponDamage, weaponDamage },
                    batch,
                    actionContext
                )
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
