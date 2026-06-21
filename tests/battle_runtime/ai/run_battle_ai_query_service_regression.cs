using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_query_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestQueryServiceConsumesTypedSkillIndexAndBuildsActionScore();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI query service regression"));
    }

    private void TestQueryServiceConsumesTypedSkillIndexAndBuildsActionScore()
    {
        using Fixture fixture = BuildFixture();
        bool callbackCalled = false;
        BattleAiScoreInput callbackScoreInput = null;

        var query = new BattleAiQueryService();
        try
        {
            query.Setup(
                fixture.State,
                fixture.GridService,
                fixture.Actor.unit_id,
                fixture.SkillDefs,
                (service, actionKind, actionLabel, scoreBucketId, command, preview, metadata) =>
                {
                    callbackCalled = service == query;
                    callbackScoreInput = new BattleAiScoreInput
                    {
                        action_kind = actionKind,
                        action_label = actionLabel,
                        score_bucket_id = scoreBucketId,
                        command = command,
                        preview = preview,
                        runtime_action_metadata =
                            BattleAiScoreRuntimeMetadata.FromMetadata(metadata),
                    };
                    return callbackScoreInput;
                },
                null,
                unitId => unitId == fixture.Actor.unit_id
            );

            _test.Eq(query.GetActorId(), fixture.Actor.unit_id, "actor id 应从 Setup 规范化保存。");
            _test.Eq(
                query.GetActorSnapshot()?.unit_id ?? new StringName(""),
                fixture.Actor.unit_id,
                "actor snapshot 应从 battle state 构建。"
            );
            _test.Eq(
                query.GetLivingUnitSnapshotsTyped("enemy").Count,
                1,
                "enemy living snapshots 应基于 actor faction 解析。"
            );
            _test.True(
                query.IsUnitMovementBlocked(fixture.Actor.unit_id),
                "movement blocked callback 应通过 typed StringName 调用。"
            );
            _test.Eq(
                query.DistanceFromAnchorToTarget(
                    fixture.Actor.coord,
                    fixture.Actor.footprint_size,
                    fixture.Target.unit_id
                ),
                2,
                "distance query 应通过 typed snapshot/grid 服务计算。"
            );

            _test.True(
                query.TryGetSkillRecordTyped(fixture.Skill.skill_id, out BattleAiQueryService.SkillRecord record),
                "QueryService 应从 typed skill-def index 生成 SkillRecord。"
            );
            _test.Eq(record.skill_id, fixture.Skill.skill_id, "SkillRecord.skill_id 应来自 typed SkillDef。");
            _test.Eq(record.range_value, 5, "SkillRecord.range_value 应读取有效技能范围。");
            _test.Eq(record.ai_tags.Count, 1, "SkillRecord.ai_tags 应使用 typed List<StringName>。");
            _test.Eq(record.ai_tags[0], new StringName("setup"), "SkillRecord.ai_tags 应保留技能 tag。");

            BattleCommand command = new()
            {
                command_type = BattleTypedNames.ToStringName(BattleCommandKind.Move),
                unit_id = fixture.Actor.unit_id,
                target_coord = new Vector2I(2, 1),
            };
            BattleAiScoreInput scoreInput = query.BuildActionScoreInput(
                "move",
                "query move",
                "positioning",
                command,
                new BattlePreview { move_cost = 1 },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["runtime_action_metadata"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["generated"] = true,
                    },
                }
            );

            _test.True(callbackCalled, "BuildActionScoreInput 应调用 typed C# callback。");
            _test.Eq(scoreInput, callbackScoreInput, "BuildActionScoreInput 应返回 callback score input。");
            _test.Eq(scoreInput?.action_kind ?? new StringName(""), new StringName("move"), "score action_kind 应透传。");
            _test.Eq(scoreInput?.action_label ?? "", "query move", "score action_label 应透传。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleAiScoreInput(callbackScoreInput);
        }
    }

    private Fixture BuildFixture()
    {
        BattleState state = BuildFlatState(new Vector2I(5, 3));
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit("query_actor", "AI", "enemy", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("query_target", "Target", "player", new Vector2I(3, 1));
        AddUnitToState(gridService, state, actor, isEnemy: true);
        AddUnitToState(gridService, state, target, isEnemy: false);

        SkillDef skill = BuildSkill();
        var skillDefs = new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill };

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Target = target,
            Skill = skill,
            SkillDefs = skillDefs,
        };
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_query_service_regression",
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
                    height_offset = 0,
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
            current_hp = 20,
            current_mp = 20,
            current_stamina = 10,
            current_ap = 2,
            current_move_points = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue("hp_max", 20);
        unit.attribute_snapshot.SetValue("mp_max", 20);
        unit.attribute_snapshot.SetValue("stamina_max", 10);
        unit.attribute_snapshot.SetValue("action_points", 2);
        unit.known_active_skill_ids.Add("query_skill");
        unit.known_skill_level_map["query_skill"] = 1;
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
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        _test.True(gridService.PlaceUnit(state, unit, unit.coord, true), $"测试单位 {unit.unit_id} 应能放入测试战场。");
    }

    private static SkillDef BuildSkill()
    {
        return new SkillDef
        {
            skill_id = "query_skill",
            display_name = "Query Skill",
            combat_profile = new CombatSkillDef
            {
                skill_id = "query_skill",
                range_value = 5,
                target_mode = "ground",
                target_team_filter = "enemy",
                area_pattern = "diamond",
                area_value = 1,
                target_selection_mode = "single_unit",
                ai_tags = new Godot.Collections.Array<StringName> { "setup" },
            },
        };
    }

    private static bool IsGodotDynamicBoundaryType(Type type) =>
        type == typeof(Godot.Collections.Dictionary)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private sealed class Fixture : IDisposable
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public BattleUnitState Target;
        public SkillDef Skill;
        public Dictionary<StringName, SkillDef> SkillDefs;

        public void Dispose()
        {
            SkillDefs = null;
            BattleTestFixture.DisposeSkill(Skill);
            BattleTestFixture.DisposeBattleState(State);
            Skill = null;
            State = null;
            Actor = null;
            Target = null;
            GridService = null;
        }
    }
}
