using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_spawn_reachability_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestRuntimeSetupIndexesSkillDefsForReachability();
        TestDeepWaterSplitMarksEnemySpawnInvalid();
        TestBidirectionalDeepWaterSplitMarksPlayerSpawnInvalid();
        TestFlatFieldMarksEnemySpawnValid();
        TestBidirectionalFlatFieldMarksBothSidesValid();
        TestWeaponRequiredSkillWithoutWeaponIsNotReachable();
        TestResultProjectionBoundary();
        TestStartFailureSnapshotProjectionBoundary();
        Quit(_test.Finish("Battle spawn reachability regression"));
    }

    private void TestDeepWaterSplitMarksEnemySpawnInvalid()
    {
        ServiceFixture fixture = BuildServiceFixture();
        int skillRange = GetSkillRange(fixture.SkillDef);
        int barrierStart = 3;
        int barrierWidth = skillRange + 2;
        Vector2I mapSize = new(barrierStart + barrierWidth + 4, 5);
        BattleState state = BuildFlatState(mapSize);
        for (int x = barrierStart; x < barrierStart + barrierWidth; x++)
        {
            for (int y = 0; y < mapSize.Y; y++)
            {
                SetCellTerrain(state, new Vector2I(x, y), BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater));
            }
        }

        BattleUnitState enemy = BuildUnit("split_enemy", "enemy", new Vector2I(1, 2), fixture.SkillId);
        BattleUnitState player = BuildUnit(
            "split_player",
            "player",
            new Vector2I(barrierStart + barrierWidth + 1, 2),
            ""
        );
        AddUnitToState(fixture.GridService, state, enemy, true);
        AddUnitToState(fixture.GridService, state, player, false);

        BattleSpawnReachabilityResult result = fixture.Service.ValidateStateTyped(
            state,
            fixture.GridService,
            fixture.SkillDefs
        );
        _test.False(result.Valid, "深水完全隔断敌人与玩家时，出生可达性应判定为 invalid。");
        _test.True(
            StringNameListHas(result.InvalidEnemyUnitIds, enemy.unit_id),
            "深水隔断回归应在 InvalidEnemyUnitIds 中包含敌方单位。"
        );
        _test.True(
            DetailExistsForUnit(result.Details, enemy.unit_id),
            "深水隔断回归应为无效敌方单位返回 details，便于定位出生点问题。"
        );
    }

    private void TestRuntimeSetupIndexesSkillDefsForReachability()
    {
        StringName skillId = "spawn_reachability_runtime_index_skill";
        SkillDef skillDef = BuildUnitSkill(skillId, range: 3);
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDef> { [skillId] = skillDef }
        );

        IReadOnlyDictionary<StringName, SkillDef> skillDefIndex = runtime.GetSkillDefIndexTyped();
        _test.True(
            skillDefIndex.TryGetValue(skillId, out SkillDef indexedSkillDef)
                && ReferenceEquals(indexedSkillDef, skillDef),
            "BattleRuntimeModule.setup 应直接消费 typed skill-def index。"
        );
    }

    private void TestFlatFieldMarksEnemySpawnValid()
    {
        ServiceFixture fixture = BuildServiceFixture();
        int skillRange = GetSkillRange(fixture.SkillDef);
        BattleState state = BuildFlatState(new Vector2I(skillRange + 6, 3));
        BattleUnitState enemy = BuildUnit("flat_enemy", "enemy", new Vector2I(1, 1), fixture.SkillId);
        BattleUnitState player = BuildUnit(
            "flat_player",
            "player",
            new Vector2I(skillRange + 4, 1),
            ""
        );
        AddUnitToState(fixture.GridService, state, enemy, true);
        AddUnitToState(fixture.GridService, state, player, false);

        BattleSpawnReachabilityResult result = fixture.Service.ValidateStateTyped(
            state,
            fixture.GridService,
            fixture.SkillDefs
        );
        _test.True(result.Valid, "平地直连时，敌方出生点应能抵达可攻击玩家的位置。");
        _test.False(
            StringNameListHas(result.InvalidEnemyUnitIds, enemy.unit_id),
            "平地直连回归不应把敌方单位列入 InvalidEnemyUnitIds。"
        );
    }

    private void TestBidirectionalDeepWaterSplitMarksPlayerSpawnInvalid()
    {
        ServiceFixture fixture = BuildServiceFixture();
        int skillRange = GetSkillRange(fixture.SkillDef);
        int barrierStart = 3;
        int barrierWidth = skillRange + 2;
        Vector2I mapSize = new(barrierStart + barrierWidth + 4, 5);
        BattleState state = BuildFlatState(mapSize);
        for (int x = barrierStart; x < barrierStart + barrierWidth; x++)
        {
            for (int y = 0; y < mapSize.Y; y++)
            {
                SetCellTerrain(state, new Vector2I(x, y), BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater));
            }
        }

        BattleUnitState enemy = BuildUnit("split_enemy", "enemy", new Vector2I(1, 2), fixture.SkillId);
        BattleUnitState player = BuildUnit(
            "split_player",
            "player",
            new Vector2I(barrierStart + barrierWidth + 1, 2),
            fixture.SkillId
        );
        AddUnitToState(fixture.GridService, state, enemy, true);
        AddUnitToState(fixture.GridService, state, player, false);

        BattleSpawnReachabilityResult result = fixture.Service.ValidateStateTyped(
            state,
            fixture.GridService,
            fixture.SkillDefs,
            new BattleSpawnReachabilityOptions(validatePlayerToEnemy: true)
        );
        _test.False(
            result.Valid,
            "双向验证开启时，深水完全隔断玩家与敌人应判定为 invalid。"
        );
        _test.True(
            StringNameListHas(result.InvalidPlayerUnitIds, player.unit_id),
            "双向深水隔断回归应在 InvalidPlayerUnitIds 中包含玩家单位。"
        );
    }

    private void TestBidirectionalFlatFieldMarksBothSidesValid()
    {
        ServiceFixture fixture = BuildServiceFixture();
        int skillRange = GetSkillRange(fixture.SkillDef);
        BattleState state = BuildFlatState(new Vector2I(skillRange + 6, 3));
        BattleUnitState enemy = BuildUnit("flat_enemy", "enemy", new Vector2I(1, 1), fixture.SkillId);
        BattleUnitState player = BuildUnit(
            "flat_player",
            "player",
            new Vector2I(skillRange + 4, 1),
            fixture.SkillId
        );
        AddUnitToState(fixture.GridService, state, enemy, true);
        AddUnitToState(fixture.GridService, state, player, false);

        BattleSpawnReachabilityResult result = fixture.Service.ValidateStateTyped(
            state,
            fixture.GridService,
            fixture.SkillDefs,
            new BattleSpawnReachabilityOptions(validatePlayerToEnemy: true)
        );
        _test.True(result.Valid, "双向验证开启时，平地直连应允许双方抵达可攻击位置。");
        _test.False(
            StringNameListHas(result.InvalidPlayerUnitIds, player.unit_id),
            "双向平地直连回归不应把玩家单位列入 InvalidPlayerUnitIds。"
        );
    }

    private void TestWeaponRequiredSkillWithoutWeaponIsNotReachable()
    {
        var service = new BattleSpawnReachabilityService();
        var gridService = new BattleGridService();
        StringName skillId = "spawn_reachability_requires_sword";
        SkillDef skillDef = BuildUnitSkill(skillId, range: 3, requiredWeaponFamily: "sword");
        var skillDefs = new Dictionary<StringName, SkillDef> { [skillId] = skillDef };
        BattleState state = BuildFlatState(new Vector2I(5, 3));
        BattleUnitState enemy = BuildUnit("weaponless_enemy", "enemy", new Vector2I(1, 1), skillId);
        BattleUnitState player = BuildUnit("weaponless_player", "player", new Vector2I(3, 1), "");
        AddUnitToState(gridService, state, enemy, true);
        AddUnitToState(gridService, state, player, false);

        BattleSpawnReachabilityResult result = service.ValidateStateTyped(
            state,
            gridService,
            skillDefs
        );
        _test.False(
            result.Valid,
            "缺少需求武器时，出生可达性不应把武器技能当作可攻击方案。"
        );
        _test.True(
            StringNameListHas(result.InvalidEnemyUnitIds, enemy.unit_id),
            "缺少需求武器时，应标记对应敌方出生单位 invalid。"
        );
    }

    private void TestResultProjectionBoundary()
    {
        BattleSpawnReachabilityResult result = BattleSpawnReachabilityResult.Invalid("missing_state_or_grid");
        Godot.Collections.Dictionary payload = BattleSpawnReachabilityProjection.Project(result);
        Godot.Collections.Array details = payload["details"].AsGodotArray();

        _test.False(payload["valid"].AsBool(), "出生可达性 result 应投影 valid。");
        _test.True(
            payload["invalid_enemy_unit_ids"].AsGodotArray<StringName>().Count == 0,
            "出生可达性 result 应投影 invalid enemy id array。"
        );
        _test.True(details.Count == 1, "出生可达性 result 应投影 details array。");
        _test.Eq(
            details[0].AsGodotDictionary()["reason"].AsString(),
            "missing_state_or_grid",
            "出生可达性 detail 应投影 reason。"
        );
    }

    private void TestStartFailureSnapshotProjectionBoundary()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["reason"] = "spawn_reachability",
            ["ally_unit_count"] = 2,
            ["enemy_unit_count"] = 3,
            ["placement_attempt"] = 1,
            ["reachability"] = new Godot.Collections.Dictionary
            {
                ["valid"] = false,
            },
        };

        BattleStartFailureSnapshot snapshot = BattleStartFailureSnapshot.FromDictionary(payload);
        Godot.Collections.Dictionary projected = BattleSpawnReachabilityProjection.Project(snapshot);

        _test.Eq(snapshot.Reason, "spawn_reachability", "battle start failure snapshot 应解析 reason。");
        _test.Eq(snapshot.AllyUnitCount, 2, "battle start failure snapshot 应解析 ally unit count。");
        _test.Eq(snapshot.EnemyUnitCount, 3, "battle start failure snapshot 应解析 enemy unit count。");
        _test.Eq(projected["reason"].AsString(), "spawn_reachability", "battle start failure snapshot 应投影 reason。");
        _test.Eq(
            projected["reachability"].AsGodotDictionary()["valid"].AsBool(),
            false,
            "battle start failure snapshot 应保留 reachability payload。"
        );
    }

    private static ServiceFixture BuildServiceFixture()
    {
        StringName skillId = "spawn_reachability_test_bolt";
        SkillDef skillDef = BuildUnitSkill(skillId, range: 3);
        return new ServiceFixture
        {
            Service = new BattleSpawnReachabilityService(),
            GridService = new BattleGridService(),
            SkillDefs = new Dictionary<StringName, SkillDef> { [skillId] = skillDef },
            SkillId = skillId,
            SkillDef = skillDef,
        };
    }

    private static SkillDef BuildUnitSkill(
        StringName skillId,
        int range,
        StringName requiredWeaponFamily = default
    )
    {
        var skillDef = new SkillDef
        {
            skill_id = skillId,
            skill_type = "active",
        };
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            target_mode = "unit",
            target_team_filter = "enemy",
            range_value = range,
        };
        if (!IsEmpty(requiredWeaponFamily))
            combatProfile.required_weapon_families.Add(requiredWeaponFamily);
        skillDef.combat_profile = combatProfile;
        return skillDef;
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "battle_spawn_reachability_regression",
            phase = "timeline_running",
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
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static void SetCellTerrain(BattleState state, Vector2I coord, StringName terrain)
    {
        if (!state.TryGetCellTyped(coord, out BattleCellState cell))
            return;
        cell.base_terrain = terrain;
        cell.RecalculateRuntimeValues();
        state.PutCellColumnPayload(coord, BattleCellState.BuildStackedCellsFromSurfaceCell(cell));
    }

    private BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        StringName skillId
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = factionId == (StringName)"enemy" ? "ai" : "manual",
            current_hp = 30,
            current_mp = 10,
            current_stamina = 10,
            current_aura = 10,
            current_ap = 2,
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 6);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        if (!IsEmpty(skillId))
        {
            unit.known_active_skill_ids.Clear();
            unit.known_active_skill_ids.Add(skillId);
            unit.SetKnownSkillLevelsTyped(new Dictionary<StringName, int> { [skillId] = 1 });
        }
        return unit;
    }

    private void AddUnitToState(
        BattleGridService gridService,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        if (isEnemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        bool placed = gridService.PlaceUnit(state, unit, unit.coord, true);
        _test.True(placed, $"测试单位 {unit.unit_id} 应能放入测试战场。");
    }

    private static int GetSkillRange(SkillDef skillDef)
    {
        if (skillDef == null || skillDef.combat_profile == null)
            return 1;
        return Math.Max(skillDef.combat_profile.range_value, 1);
    }

    private static bool StringNameListHas(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static bool DetailExistsForUnit(
        IReadOnlyList<BattleSpawnReachabilityUnitResult> details,
        StringName unitId
    )
    {
        foreach (BattleSpawnReachabilityUnitResult detail in details)
        {
            if (detail.UnitId == unitId)
                return true;
        }
        return false;
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";

    private sealed class ServiceFixture
    {
        internal BattleSpawnReachabilityService Service { get; init; }
        internal BattleGridService GridService { get; init; }
        internal Dictionary<StringName, SkillDef> SkillDefs { get; init; }
        internal StringName SkillId { get; init; }
        internal SkillDef SkillDef { get; init; }
    }
}
