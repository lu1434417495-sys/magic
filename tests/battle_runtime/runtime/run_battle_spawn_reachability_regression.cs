using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_spawn_reachability_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestServiceIsPlainTypedCSharp();
        TestResultPublicApiStaysTyped();
        TestRuntimeSetupIndexesSkillDefsForReachability();
        TestDeepWaterSplitMarksEnemySpawnInvalid();
        TestBidirectionalDeepWaterSplitMarksPlayerSpawnInvalid();
        TestFlatFieldMarksEnemySpawnValid();
        TestBidirectionalFlatFieldMarksBothSidesValid();
        TestWeaponRequiredSkillWithoutWeaponIsNotReachable();
        TestResultProjectionBoundary();

        if (_failures.Count == 0)
        {
            GD.Print("Battle spawn reachability regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle spawn reachability regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceIsPlainTypedCSharp()
    {
        Type serviceType = typeof(BattleSpawnReachabilityService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleSpawnReachabilityService 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(serviceType, "GlobalClassAttribute"),
            "BattleSpawnReachabilityService 不应注册 GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("validate_state") == null,
            "BattleSpawnReachabilityService 不应保留 GDScript validate_state wrapper。"
        );
        AssertTrue(
            serviceType.GetMethod("ValidateState") == null,
            "BattleSpawnReachabilityService 不应保留 Godot Dictionary ValidateState wrapper。"
        );

        var validateMethod = serviceType.GetMethod(nameof(BattleSpawnReachabilityService.ValidateStateTyped));
        AssertTrue(validateMethod != null, "BattleSpawnReachabilityService 应保留 typed C# 入口。");
        if (validateMethod == null)
            return;

        Type skillDefParamType = validateMethod.GetParameters()[2].ParameterType;
        AssertEq(
            skillDefParamType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "出生可达性验证应消费 typed skill-def index。"
        );
    }

    private void TestResultPublicApiStaysTyped()
    {
        AssertResultTypeIsPlainTyped(
            typeof(BattleSpawnReachabilityResult),
            "BattleSpawnReachabilityResult"
        );
        AssertResultTypeIsPlainTyped(
            typeof(BattleSpawnReachabilityUnitResult),
            "BattleSpawnReachabilityUnitResult"
        );
    }

    private void AssertResultTypeIsPlainTyped(Type type, string typeName)
    {
        AssertTrue(type.IsSealed, $"{typeName} 应保持 sealed typed DTO。");
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(type),
            $"{typeName} 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(type, "GlobalClassAttribute"),
            $"{typeName} 不应注册 GlobalClass。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(type, typeName);
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type, string typeName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            AssertFalse(
                IsGodotCollectionOrVariant(property.PropertyType),
                $"{typeName}.{property.Name} 不应公开 Godot Dictionary/Array/Variant 属性。"
            );
        }
        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (method.IsSpecialName)
                continue;
            AssertFalse(
                IsGodotCollectionOrVariant(method.ReturnType),
                $"{typeName}.{method.Name} 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsGodotCollectionOrVariant(parameter.ParameterType),
                    $"{typeName}.{method.Name} 不应公开接收 Godot Dictionary/Array/Variant 参数 {parameter.Name}。"
                );
            }
        }
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
                SetCellTerrain(state, new Vector2I(x, y), BattleCellState.TERRAIN_DEEP_WATER());
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
        AssertFalse(result.Valid, "深水完全隔断敌人与玩家时，出生可达性应判定为 invalid。");
        AssertTrue(
            StringNameListHas(result.InvalidEnemyUnitIds, enemy.unit_id),
            "深水隔断回归应在 InvalidEnemyUnitIds 中包含敌方单位。"
        );
        AssertTrue(
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
            new Godot.Collections.Dictionary { [skillId] = skillDef },
            new Godot.Collections.Dictionary(),
            new Godot.Collections.Dictionary()
        );

        IReadOnlyDictionary<StringName, SkillDef> skillDefIndex = runtime.GetSkillDefIndexTyped();
        AssertTrue(
            skillDefIndex.TryGetValue(skillId, out SkillDef indexedSkillDef)
                && ReferenceEquals(indexedSkillDef, skillDef),
            "BattleRuntimeModule.setup 应把 Godot skill_defs 边界物化为 typed skill-def index。"
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
        AssertTrue(result.Valid, "平地直连时，敌方出生点应能抵达可攻击玩家的位置。");
        AssertFalse(
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
                SetCellTerrain(state, new Vector2I(x, y), BattleCellState.TERRAIN_DEEP_WATER());
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
        AssertFalse(
            result.Valid,
            "双向验证开启时，深水完全隔断玩家与敌人应判定为 invalid。"
        );
        AssertTrue(
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
        AssertTrue(result.Valid, "双向验证开启时，平地直连应允许双方抵达可攻击位置。");
        AssertFalse(
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
        AssertFalse(
            result.Valid,
            "缺少需求武器时，出生可达性不应把武器技能当作可攻击方案。"
        );
        AssertTrue(
            StringNameListHas(result.InvalidEnemyUnitIds, enemy.unit_id),
            "缺少需求武器时，应标记对应敌方出生单位 invalid。"
        );
    }

    private void TestResultProjectionBoundary()
    {
        BattleSpawnReachabilityResult result = BattleSpawnReachabilityResult.Invalid("missing_state_or_grid");
        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Array details = payload["details"].AsGodotArray();

        AssertFalse(payload["valid"].AsBool(), "出生可达性 result 应投影 valid。");
        AssertTrue(
            payload["invalid_enemy_unit_ids"].AsGodotArray<StringName>().Count == 0,
            "出生可达性 result 应投影 invalid enemy id array。"
        );
        AssertTrue(details.Count == 1, "出生可达性 result 应投影 details array。");
        AssertEq(
            details[0].AsGodotDictionary()["reason"].AsString(),
            "missing_state_or_grid",
            "出生可达性 detail 应投影 reason。"
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
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.recalculate_runtime_values();
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static void SetCellTerrain(BattleState state, Vector2I coord, StringName terrain)
    {
        if (!state.TryGetCellTyped(coord, out BattleCellState cell))
            return;
        cell.base_terrain = terrain;
        cell.recalculate_runtime_values();
        state.cell_columns[coord] = BattleCellState.build_stacked_cells_from_surface_cell(cell);
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
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 30);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.AURA_MAX_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 6);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        if (!IsEmpty(skillId))
        {
            unit.known_active_skill_ids.Clear();
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map = new Godot.Collections.Dictionary { [skillId] = 1 };
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
        state.units[unit.unit_id] = unit;
        if (isEnemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        bool placed = gridService.place_unit(state, unit, unit.coord, true);
        AssertTrue(placed, $"测试单位 {unit.unit_id} 应能放入测试战场。");
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

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
                return true;
        }
        return false;
    }

    private static bool IsGodotCollectionOrVariant(Type type)
    {
        if (type == typeof(Variant))
            return true;
        Type genericDefinition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return genericDefinition == typeof(Godot.Collections.Dictionary)
            || genericDefinition == typeof(Godot.Collections.Array)
            || type.Namespace == "Godot.Collections";
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private sealed class ServiceFixture
    {
        internal BattleSpawnReachabilityService Service { get; init; }
        internal BattleGridService GridService { get; init; }
        internal Dictionary<StringName, SkillDef> SkillDefs { get; init; }
        internal StringName SkillId { get; init; }
        internal SkillDef SkillDef { get; init; }
    }
}
