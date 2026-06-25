using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_score_context_adapter_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSkillScoreInputUsesTypedIndexAndStripsSkillResource();
            TestPayloadGuardRejectsRuntimeLikeCommandAndPreviewPayload();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI score context adapter regression"));
    }

    private void TestSkillScoreInputUsesTypedIndexAndStripsSkillResource()
    {
        Fixture fixture = BuildFixture();
        var adapter = new BattleAiScoreContextAdapter();
        adapter.Setup(
            new BattleAiScoreService(),
            fixture.State,
            fixture.Actor,
            fixture.GridService,
            fixture.SkillDefs
        );

        IBattleAiScoreContext scoreContext = adapter;
        _test.True(
            scoreContext.skill_defs.Count == 1,
            "IBattleAiScoreContext 应直接暴露 typed skill_defs 视图。"
        );

        BattleCommand command = new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = fixture.Actor.unit_id,
            skill_id = fixture.Skill.skill_id,
            target_coord = new Vector2I(2, 1),
        };
        BattlePreview preview = new()
        {
            allowed = true,
            resolved_anchor_coord = fixture.Actor.coord,
        };
        preview.AddTargetCoord(command.target_coord);

        BattleAiScoreInput scoreInput = adapter.BuildSkillScoreInput(
            null,
            fixture.Skill.skill_id,
            command,
            preview,
            Array.Empty<CombatEffectDef>(),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["runtime_action_metadata"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["generated"] = true,
                    ["action_id"] = "adapter_regression",
                },
            }
        );

        _test.True(scoreInput != null, "有效 skill command 应生成 BattleAiScoreInput。");
        if (scoreInput == null)
        {
            return;
        }

        _test.Eq(
            scoreInput.skill_id,
            fixture.Skill.skill_id,
            "score input 离开适配器前只保留 skill_id，不持有 SkillDef live resource。"
        );
        _test.Eq(scoreInput.command, command, "score input 应保留 command value object。");
        _test.Eq(scoreInput.preview, preview, "score input 应保留 preview value object。");
        _test.Eq(scoreInput.action_kind, new StringName("skill"), "默认 action_kind 应为 skill。");
        _test.Eq(scoreInput.ap_cost, 2, "适配器应通过 typed skill index 解析 SkillDef 并计算 AP cost。");
        _test.Eq(scoreInput.mp_cost, 3, "适配器应通过 typed skill index 解析 SkillDef 并计算 MP cost。");
        _test.Eq(
            scoreInput.runtime_action_metadata?.skill_id ?? new StringName(""),
            fixture.Skill.skill_id,
            "runtime_action_metadata 应带上 skill_id，替代 live skill_def。"
        );
        _test.True(
            BattleAiPayloadGuard.ScoreInputHasNoLiveState(scoreInput),
            "score input 应满足 AI payload guard 的 no-live-state 合约。"
        );
    }

    private void TestPayloadGuardRejectsRuntimeLikeCommandAndPreviewPayload()
    {
        BattleAiFailurePolicy.Reset();
        bool originalFailLoudAbort = BattleAiPayloadGuard.FailLoudProcessAbortEnabled;
        BattleAiPayloadGuard.FailLoudProcessAbortEnabled = false;
        try
        {
            BattleCommand command = new()
            {
                command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
                unit_id = "actor",
            };
            command.target_unit_ids = new Godot.Collections.Array<StringName>
            {
                new StringName("<Object:fake_target>"),
            };
            _test.True(
                !BattleAiPayloadGuard.CommandIsValueObject(command),
                "BattleAiPayloadGuard 应拒绝 command target_unit_ids 里的 runtime-like payload 文本。"
            );

            BattleAiFailurePolicy.Reset();

            BattlePreview preview = new()
            {
                allowed = true,
                hit_preview = new AttackPreviewData
                {
                    SummaryText = "<Callable:fake_preview>",
                },
            };
            preview.AddTargetUnitId("safe_target");
            _test.True(
                !BattleAiPayloadGuard.PreviewHasNoLiveState(preview),
                "BattleAiPayloadGuard 应拒绝 preview.hit_preview 里的 runtime-like payload 文本。"
            );
        }
        finally
        {
            BattleAiPayloadGuard.FailLoudProcessAbortEnabled = originalFailLoudAbort;
            BattleAiFailurePolicy.Reset();
        }
    }

    private Fixture BuildFixture()
    {
        BattleState state = BuildFlatState(new Vector2I(4, 3));
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit("adapter_actor", "AI", "enemy", new Vector2I(1, 1));
        state.SetUnit(actor);
        state.enemy_unit_ids.Add(actor.unit_id);
        bool placed = gridService.PlaceUnit(state, actor, actor.coord, true);
        _test.True(placed, "测试单位应能放入测试战场。");

        SkillDef skill = BuildSkill();
        Dictionary<StringName, SkillDef> skillDefs = new() { [skill.skill_id] = skill };

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Skill = skill,
            SkillDefs = skillDefs,
        };
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_score_context_adapter_regression",
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
            control_mode = "ai",
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
        unit.known_active_skill_ids.Add("adapter_skill");
        unit.known_skill_level_map["adapter_skill"] = 1;
        return unit;
    }

    private static SkillDef BuildSkill()
    {
        return TestResourceOwnership.Own(
            new SkillDef
            {
                skill_id = "adapter_skill",
                display_name = "Adapter Skill",
                combat_profile = new CombatSkillDef
                {
                    skill_id = "adapter_skill",
                    ap_cost = 2,
                    mp_cost = 3,
                    stamina_cost = 0,
                    cooldown_tu = 0,
                },
            },
            "BattleAiScoreContextAdapter.BuildSkill"
        );
    }

    private static bool IsGodotDynamicBoundaryType(Type type) =>
        type == typeof(Godot.Collections.Dictionary)
        || type == typeof(Godot.Collections.Array)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private static StringName DictStringName(Godot.Collections.Dictionary source, string key)
    {
        if (source == null)
        {
            return "";
        }
        StringName exactKey = new(key);
        return source.ContainsKey(exactKey)
            ? ProgressionDataUtils.to_string_name(source[exactKey])
            : new StringName("");
    }

    private sealed class Fixture
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public SkillDef Skill;
        internal IReadOnlyDictionary<StringName, SkillDef> SkillDefs;
    }
}
