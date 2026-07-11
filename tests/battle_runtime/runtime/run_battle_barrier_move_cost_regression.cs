using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_barrier_move_cost_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestFirstBoundaryInterruptionDoesNotExecuteMovement();
            TestBarrierInterruptedMoveCostUsesReachedAnchors();
            RequestTestExit(_test.Finish("Battle barrier move cost regression"));
        }
        catch (Exception ex)
        {
            GD.PushError($"Battle barrier move cost regression crashed: {ex}");
            RequestTestExit(_test.Finish("Battle barrier move cost regression", 1));
        }
    }

    private void TestFirstBoundaryInterruptionDoesNotExecuteMovement()
    {
        Fixture fixture = BuildRuntimeWithSphere("first_boundary_stop", new Vector2I(5, 2));
        BattleUnitState activeUnit = fixture.Enemy;
        Vector2I origin = activeUnit.coord;
        Vector2I target = new(4, 2);
        MarkLayersBroken(
            fixture.State,
            "red",
            "orange",
            "yellow",
            "green",
            "blue",
            "indigo"
        );
        SetLayerSaveRollOverride(fixture.State, "violet", 1);

        BattleValidatedMoveExecutionResult result =
            fixture.Runtime._movement_service.MoveUnitAlongValidatedPathTyped(
                activeUnit,
                new[] { origin, target },
                target,
                new BattleEventBatch()
            );

        _test.False(result.Executed, "第一步被屏障放逐拦截时，不应把屏障副作用计作已执行移动。");
        _test.True(result.StoppedByBarrier, "屏障中断应记录 stopped_by_barrier。");
        _test.Eq(result.ExecutedPath.Count, 1, "未进入任何新路径锚点时，executed path 应只包含起点。");
        if (result.Executed && result.ExecutedPath.Count <= 1 && activeUnit.coord != origin)
        {
            throw new Exception("executed movement changed coord without recording path");
        }

        fixture.Runtime.dispose();
    }

    private void TestBarrierInterruptedMoveCostUsesReachedAnchors()
    {
        Fixture fixture = BuildRuntimeWithSphere("reached_anchor_stop", new Vector2I(6, 2));
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleUnitState activeUnit = fixture.Enemy;
        Vector2I origin = activeUnit.coord;
        int movePointsBefore = activeUnit.current_move_points;
        MarkLayersBroken(
            fixture.State,
            "red",
            "orange",
            "yellow",
            "green",
            "blue",
            "indigo"
        );
        SetLayerSaveRollOverride(fixture.State, "violet", 1);

        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Move),
            unit_id = activeUnit.unit_id,
            target_coord = new Vector2I(3, 2),
        };
        runtime._movement_service.HandleMoveCommand(activeUnit, command, new BattleEventBatch());

        _test.Eq(activeUnit.current_move_points, movePointsBefore - 1, "屏障前已抵达一格时，只应扣除已抵达锚点的移动力。");
        _test.True(activeUnit.coord != new Vector2I(3, 2), "紫色层放逐应中断移动，不能抵达原目标。");
        if (activeUnit.current_move_points == movePointsBefore && activeUnit.coord != origin)
        {
            throw new Exception("movement changed coord with zero cost");
        }

        runtime.dispose();
    }

    private static Fixture BuildRuntimeWithSphere(StringName battleId, Vector2I enemyCoord)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        BattleState state = BuildState(battleId, new Vector2I(8, 5));
        runtime.SetupStateForTests(state);
        BattleUnitState caster = BuildUnit("caster", "施法者", "player", new Vector2I(2, 2));
        BattleUnitState enemy = BuildUnit("enemy", "敌人", "enemy", enemyCoord);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, enemy, true);
        SkillDefinition skill = BuildSkill("mage_prismatic_sphere", "虹光法球", "mage", "magic");
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            saveDc: 15,
            saveDcMode: "static",
            saveAbility: "willpower",
            saveTag: "magic",
            durationTu: 120,
            parameters: new Dictionary<string, Variant>
            {
                ["area_pattern"] = "diamond",
                ["profile_id"] = "prismatic_sphere",
                ["radius_cells"] = 2,
            }
        );
        runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
            caster,
            caster,
            skill,
            effect,
            new BattleEventBatch()
        );
        return new Fixture(runtime, state, caster, enemy);
    }

    private static BattleState BuildState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
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
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 120,
            current_mp = 120,
            current_stamina = 40,
            current_ap = 2,
            current_move_points = 4,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), AttributeService.BASE_ARMOR_CLASS);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("constitution_modifier", 0);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        return unit;
    }

    private static SkillDefinition BuildSkill(StringName skillId, string displayName, params StringName[] tags) =>
        TestSkillDefinitionProjection.BuildSkill(skillId, displayName, tags: tags);

    private static void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private static BattleBarrierInstanceState FirstBarrier(BattleState state)
    {
        GDictionary barrierFields = state?.ProjectLayeredBarrierFields() ?? new GDictionary();
        foreach (Variant rawKey in barrierFields.Keys)
        {
            return BattleBarrierInstanceState.FromRuntimeDict(
                barrierFields[rawKey].AsGodotDictionary()
            );
        }
        return new BattleBarrierInstanceState();
    }

    private static StringName FirstBarrierKey(BattleState state)
    {
        GDictionary barrierFields = state?.ProjectLayeredBarrierFields() ?? new GDictionary();
        foreach (Variant rawKey in barrierFields.Keys)
        {
            return ProgressionDataUtils.to_string_name(rawKey);
        }
        return "";
    }

    private static void StoreFirstBarrier(BattleState state, BattleBarrierInstanceState barrier)
    {
        StringName key = FirstBarrierKey(state);
        if (state == null || key == "" || barrier == null)
        {
            return;
        }
        state.PutLayeredBarrierFieldPayload(key, barrier.ToRuntimeDict());
    }

    private static void MarkLayersBroken(BattleState state, params StringName[] layerIds)
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var targetLayerIds = new HashSet<StringName>(layerIds ?? Array.Empty<StringName>());
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && targetLayerIds.Contains(layer.LayerId))
            {
                layer.Broken = true;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static void SetLayerSaveRollOverride(
        BattleState state,
        StringName layerId,
        int roll
    )
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && layer.LayerId == layerId)
            {
                layer.HasSaveRollOverride = true;
                layer.SaveRollOverride = roll;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private readonly record struct Fixture(
        BattleRuntimeModule Runtime,
        BattleState State,
        BattleUnitState Caster,
        BattleUnitState Enemy
    );
}
