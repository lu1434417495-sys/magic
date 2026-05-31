using System.IO;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_meteor_swarm_preview_surface_contract_regression : SceneTree
{
    private readonly GStringArray _failures = new();
    private GDictionary _skillDefsProviderPayload = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestMeteorPreviewUsesDamageResolverPreviewContract();
        TestPreviewHudAndAiShareTypedFacts();
        if (_failures.Count == 0)
        {
            GD.Print("Meteor swarm preview surface contract regression: PASS");
            return 0;
        }
        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Meteor swarm preview surface contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestPreviewHudAndAiShareTypedFacts()
    {
        BattleUnitState enemyCenter = BuildUnit("meteor_surface_enemy_center", "中心敌人", "enemy", new Vector2I(4, 4), 160);
        BattleUnitState allyInner = BuildUnit("meteor_surface_ally_inner", "内圈友军", "player", new Vector2I(5, 4), 160);
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, allyInner });
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        BattleCommand command = BuildCommand(setup.Caster, new Vector2I(4, 4));
        BattlePreview preview = setup.Runtime.preview_command(command);
        AssertTrue(preview != null && preview.allowed, "陨星雨 preview surface 合同前置应可用。");
        AssertTrue(preview.special_profile_preview_facts != null, "preview 必须暴露 special_profile_preview_facts。");
        if (preview == null || preview.special_profile_preview_facts == null)
            return;
        GDictionary factsPayload = preview.special_profile_preview_facts.to_dict();
        string previewFactId = factsPayload.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "";
        AssertTrue(!string.IsNullOrEmpty(previewFactId), "preview facts 必须带稳定 preview_fact_id。");
        AssertEq(preview.hit_preview?.Source ?? "", "special_profile_preview_facts", "preview.hit_preview 应标记 special facts 来源。");
        AssertEq(preview.hit_preview?.Source ?? "", preview.hit_preview?.Source ?? "", "preview source 应稳定。");
        AssertEq(preview.target_coords.Count, 49, "preview surface 必须暴露同一份 7x7 target coords。");
        AssertTrue(
            factsPayload.GetValueOrDefault("target_numeric_summary", new GArray()).AsGodotArray().Count >= 2,
            "preview facts 应携带全目标数值摘要。"
        );
        AssertTrue(
            preview.special_profile_preview_facts.get_friendly_fire_numeric_summary().Count == 1,
            "preview facts 应携带全量友伤数值摘要。"
        );

        var hud = new BattleHudAdapter();
        GDictionary snapshot = hud.build_snapshot(
            setup.Runtime.get_state(),
            new Vector2I(4, 4),
            "mage_meteor_swarm",
            "陨星雨",
            "",
            new GVector2IArray { new Vector2I(4, 4) },
            1,
            new GStringNameArray(),
            "",
            "",
            preview
        );
        var hitPreviewPayload = snapshot.GetValueOrDefault("selected_skill_hit_preview_payload", new Variant()).As<AttackPreviewData>();
        AssertEq(hitPreviewPayload?.Source ?? "", "special_profile_preview_facts", "HUD hit payload 应消费 special facts。");
        GDictionary hudFacts = preview.special_profile_preview_facts.to_dict();
        AssertEq(
            hudFacts.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "",
            previewFactId,
            "HUD 必须和 runtime preview 共用同一 preview_fact_id。"
        );
        AssertEq(
            snapshot.GetValueOrDefault("selected_skill_hit_preview_text", "").As<string>() ?? "",
            preview.hit_preview?.SummaryText ?? "",
            "HUD 应显示 runtime 提供的 summary text。"
        );

        var aiContext = new BattleAiContext();
        aiContext.state = setup.Runtime.get_state();
        aiContext.unit_state = setup.Caster;
        aiContext.grid_service = setup.Runtime.get_grid_service();
        aiContext.skill_defs = setup.SkillDefs;
        var scoreService = new BattleAiScoreService();
        var scoreInput = scoreService.build_skill_score_input(
            aiContext, skillDef, command, preview, new GArray(), new GDictionary
            {
                ["action_kind"] = "ground_skill",
                ["action_label"] = "陨星雨",
            }
        );
        AssertTrue(scoreInput != null, "AI score input 应能消费 special preview facts。");
        if (scoreInput == null)
            return;
        AssertEq(
            scoreInput.special_profile_preview_facts.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "",
            previewFactId,
            "AI 必须和 runtime preview 共用同一 preview_fact_id。"
        );
        AssertEq(scoreInput.target_coords.Count, 49, "AI target coords 必须来自同一份 7x7 preview plan。");
        AssertTrue(scoreInput.enemy_target_count >= 1, "AI 应识别陨星雨敌方目标。");
        AssertTrue(scoreInput.estimated_enemy_damage > 0, "AI 应从 typed numeric summary 估算敌方伤害。");
        AssertTrue(scoreInput.estimated_friendly_fire_target_count == 1, "AI 应从 friendly_fire_numeric_summary 识别友伤目标。");
        AssertTrue(!string.IsNullOrEmpty(scoreInput.friendly_fire_reject_reason), "AI 应把 hard friendly fire 写入 reject reason。");
        AssertTrue(scoreInput.attack_roll_modifier_breakdown.Count >= 1, "AI trace payload 应暴露未来尘土命中修正 breakdown。");
    }

    private Fixture BuildRuntimeFixture(Vector2I mapSize, BattleUnitState[] extraUnits)
    {
        var progressionRegistry = new ProgressionContentRegistry();
        GDictionary skillDefs = progressionRegistry.get_skill_defs();
        var specialRegistry = new BattleSpecialProfileRegistry();
        specialRegistry.rebuild(skillDefs);
        AssertTrue(specialRegistry.validate().Count == 0, "正式 special profile registry 应可用于 preview surface fixture。");
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            skillDefs,
            new GDictionary(),
            new GDictionary(),
            null,
            null,
            new GDictionary(),
            null,
            default,
            specialRegistry.get_snapshot()
        );
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        BattleState state = BuildState(mapSize);
        BattleUnitState caster = BuildUnit("meteor_surface_caster", "陨星术者", "player", new Vector2I(4, 0), 180);
        caster.known_active_skill_ids.Add("mage_meteor_swarm");
        caster.known_skill_level_map[new StringName("mage_meteor_swarm")] = 9;
        caster.current_ap = 4;
        caster.current_mp = 200;
        caster.current_aura = 3;
        caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        state.units[caster.unit_id] = caster;
        state.ally_unit_ids.Add(caster.unit_id);
        foreach (BattleUnitState unit in extraUnits)
        {
            if (unit == null)
                continue;
            state.units[unit.unit_id] = unit;
            if (unit.faction_id == caster.faction_id)
                state.ally_unit_ids.Add(unit.unit_id);
            else
                state.enemy_unit_ids.Add(unit.unit_id);
        }
        state.active_unit_id = caster.unit_id;
        foreach (Variant unitValue in state.units.Values)
        {
            BattleUnitState unitState = unitValue.AsGodotObject() as BattleUnitState;
            AssertTrue(
                runtime._grid_service.place_unit(state, unitState, unitState.coord, true),
                $"单位应能放入 preview surface 棋盘：{unitState?.unit_id}"
            );
        }
        runtime._state = state;
        return new Fixture
        {
            Runtime = runtime,
            Caster = caster,
            SkillDefs = skillDefs,
        };
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "meteor_swarm_preview_surface_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.cells[coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int hp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            coord = coord,
            is_alive = true,
            current_hp = hp,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), hp);
        SeedBaseAttributesAndDeriveAc(unit);
        unit.refresh_footprint();
        return unit;
    }

    private static void SeedBaseAttributesAndDeriveAc(BattleUnitState unit)
    {
        StringName[] baseAttributes =
        {
            "strength",
            "agility",
            "constitution",
            "perception",
            "intelligence",
            "willpower",
        };
        foreach (StringName attributeId in baseAttributes)
        {
            if (!unit.attribute_snapshot.has_value(attributeId))
                unit.attribute_snapshot.set_value(attributeId, 10);
        }
        if (!unit.attribute_snapshot.has_value(AttributeService.ARMOR_CLASS_ID()))
        {
            int agilityModifier = AttributeSnapshot.calculate_score_modifier(
                unit.attribute_snapshot.get_value("agility")
            );
            unit.attribute_snapshot.set_value(
                AttributeService.ARMOR_CLASS_ID(),
                System.Math.Clamp(AttributeService.BASE_ARMOR_CLASS_VALUE() + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I anchorCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = caster.unit_id,
            skill_id = "mage_meteor_swarm",
            target_coord = anchorCoord,
        };
        command.target_coords.Add(anchorCoord);
        return command;
    }

    private void TestMeteorPreviewUsesDamageResolverPreviewContract()
    {
        string source = ReadText("res://scripts/systems/battle/runtime/BattleMeteorSwarmResolver.cs");
        AssertTrue(source.Contains("preview_damage_effect("), "Meteor 友伤数值预览必须调用 BattleDamageResolver.preview_damage_effect。");
        AssertTrue(
            !source.Contains("_resolve_preview_mitigation_tier")
                && !source.Contains("_apply_preview_mitigation")
                && !source.Contains("_estimate_guard_block"),
            "Meteor resolver 不应保留手写抗性 / 固定减伤 / guard 预览 helper，避免和 BattleDamageResolver 漂移。"
        );
    }

    private static string ReadText(string filePath)
    {
        using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
            return "";
        return file.GetAsText();
    }

    private static SkillDef GetSkill(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
            return null;
        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} actual={actual} expected={expected}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleUnitState Caster;
        public GDictionary SkillDefs;
    }
}
