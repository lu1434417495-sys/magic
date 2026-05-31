using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_hit_preview_contract_regression : SceneTree
{
    private static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    private static readonly StringName ACTION_TITHE_VARIANT_ID = "action_tithe";
    private static readonly StringName WARRIOR_HEAVY_STRIKE_SKILL_ID = "warrior_heavy_strike";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(_Run));
    }

    private async void _Run()
    {
        _TestForceHitSkillRuntimePreviewIsGuaranteed();
        await _TestSingleHitSkillHudSurfacesRuntimePreview();
        if (_failures.Count == 0)
        {
            GD.Print("Battle hit preview contract regression: PASS");
            Quit(0);
            return;
        }
        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle hit preview contract regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void _TestForceHitSkillRuntimePreviewIsGuaranteed()
    {
        GDictionary skillDefs = new ProgressionContentRegistry().get_skill_defs();
        var skillDef = GetObject<SkillDef>(skillDefs, BLACK_CONTRACT_PUSH_SKILL_ID);
        _AssertTrue(skillDef != null && skillDef.combat_profile != null, "黑契推进预览前置：技能定义应存在。");
        if (skillDef == null || skillDef.combat_profile == null)
            return;

        var runtime = new BattleRuntimeModule();
        runtime.setup(null, skillDefs, new GDictionary(), new GDictionary(), null);
        var state = _BuildState("preview_contract_force_hit");
        var caster = _BuildUnit(
            "contract_caster",
            "黑契使徒",
            "player",
            new Vector2I(1, 1),
            new List<StringName> { BLACK_CONTRACT_PUSH_SKILL_ID },
            2
        );
        var target = _BuildUnit(
            "contract_target",
            "高闪避敌人",
            "enemy",
            new Vector2I(2, 1),
            new List<StringName>(),
            2
        );
        target.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 999);
        _AddUnitToRuntimeState(runtime, state, caster, false);
        _AddUnitToRuntimeState(runtime, state, target, true);
        state.phase = new StringName("unit_acting");
        state.active_unit_id = caster.unit_id;
        runtime._state = state;

        BattlePreview preview = runtime.preview_command(_BuildSkillCommand(
            caster.unit_id,
            BLACK_CONTRACT_PUSH_SKILL_ID,
            target,
            ACTION_TITHE_VARIANT_ID
        ));
        _AssertTrue(preview != null && preview.allowed, "黑契推进应能对合法目标生成 preview。");
        AttackPreviewData hitPreview = preview?.hit_preview;
        _AssertEq(hitPreview?.HitRatePercent ?? 0, 100, "黑契推进 hit_rate_percent 应为 100。");
        _AssertEq(hitPreview?.SuccessRatePercent ?? 0, 100, "黑契推进 success_rate_percent 应为 100。");
        _AssertEq(hitPreview?.StageSuccessRates?.Count ?? 0, 1, "黑契推进 stage_success_rates 长度应为 1。");
        if (hitPreview?.StageSuccessRates?.Count >= 1)
            _AssertEq(hitPreview.StageSuccessRates[0], 100, "黑契推进 stage_success_rates[0] 应为 100。");
        _AssertTrue(hitPreview?.ForceHitNoCrit ?? false, "黑契推进 preview 应标记 force_hit_no_crit。");
        string summaryText = hitPreview?.SummaryText ?? "";
        _AssertTrue(
            summaryText.Contains("必定命中") && summaryText.Contains("禁暴击"),
            "黑契推进 preview 文案应说明必定命中且禁暴击。"
        );
        runtime.dispose();
    }

    private async Task _TestSingleHitSkillHudSurfacesRuntimePreview()
    {
        GDictionary skillDefs = new ProgressionContentRegistry().get_skill_defs();
        var skillDef = GetObject<SkillDef>(skillDefs, WARRIOR_HEAVY_STRIKE_SKILL_ID);
        _AssertTrue(skillDef != null && skillDef.combat_profile != null, "重击 HUD 预览前置：技能定义应存在。");
        if (skillDef == null || skillDef.combat_profile == null)
            return;

        GameSession gameSession = await _InstallMockGameSession(skillDefs);
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, skillDefs, new GDictionary(), new GDictionary(), null);
        var trapDamageResolver = new TrapDamageResolver();
        runtime.configure_damage_resolver_for_tests(trapDamageResolver);
        var state = _BuildState("preview_contract_single_hit");
        var attacker = _BuildUnit(
            "heavy_strike_user",
            "重击战士",
            "player",
            new Vector2I(1, 1),
            new List<StringName> { WARRIOR_HEAVY_STRIKE_SKILL_ID },
            3
        );
        attacker.current_stamina = 30;
        attacker.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 80);
        var target = _BuildUnit(
            "heavy_strike_target",
            "高闪避木桩",
            "enemy",
            new Vector2I(2, 1),
            new List<StringName>(),
            2
        );
        target.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 70);
        _AddUnitToRuntimeState(runtime, state, attacker, false);
        _AddUnitToRuntimeState(runtime, state, target, true);
        state.phase = new StringName("unit_acting");
        state.active_unit_id = attacker.unit_id;
        runtime._state = state;

        BattlePreview preview = runtime.preview_command(_BuildSkillCommand(
            attacker.unit_id,
            WARRIOR_HEAVY_STRIKE_SKILL_ID,
            target
        ));
        _AssertTrue(
            preview != null && preview.hit_preview != null && !preview.hit_preview.IsEmpty,
            "重击 runtime preview 应暴露命中摘要。"
        );
        string hitPreviewText = preview?.hit_preview?.SummaryText ?? "";
        _AssertTrue(
            hitPreviewText.Contains("预计命中率") && hitPreviewText.Contains("需 "),
            "重击 runtime preview 应包含命中率与 required roll。"
        );
        _AssertEq(trapDamageResolver.resolve_effects_calls, 0, "runtime preview 不应通过 BattleDamageResolver.resolve_effects() 偷取伤害结果。");
        string damagePreviewText = preview?.damage_preview?.GetValueOrDefault("summary_text", "").AsString() ?? "";
        _AssertEq(damagePreviewText, "伤害 2-10", "runtime preview 应暴露非暴击基础伤害范围。");

        var adapter = new BattleHudAdapter();
        adapter.setup_runtime_context(null, gameSession);
        GDictionary snapshot = adapter.build_snapshot(
            state,
            target.coord,
            WARRIOR_HEAVY_STRIKE_SKILL_ID,
            skillDef.display_name,
            "",
            new Godot.Collections.Array<Vector2I>(),
            1,
            new Godot.Collections.Array<StringName>(),
            new StringName(""),
            "",
            null
        );
        _AssertEq(
            snapshot.GetValueOrDefault("selected_skill_hit_preview_text", "").AsString(),
            hitPreviewText,
            "HUD snapshot 应保留普通单段技能的 runtime 命中摘要。"
        );
        var snapshotStageRates = snapshot.GetValueOrDefault("selected_skill_hit_stage_rates", new GIntArray()).AsGodotArray();
        var previewStageRates = preview?.hit_preview?.StageHitRates ?? new GIntArray();
        _AssertEq(snapshotStageRates.Count, previewStageRates.Count, "HUD snapshot 应保留普通单段技能的阶段命中率数组长度。");
        for (int i = 0; i < Mathf.Min(snapshotStageRates.Count, previewStageRates.Count); i++)
        {
            _AssertEq(snapshotStageRates[i].AsInt32(), previewStageRates[i], $"HUD snapshot stage rate[{i}] 应与 runtime 一致。");
        }
        string skillSubtitle = snapshot.GetValueOrDefault("skill_subtitle", "").AsString();
        _AssertTrue(skillSubtitle.Contains(hitPreviewText), "HUD 副标题应显示普通单段命中摘要。");
        string snapshotDamageText = snapshot.GetValueOrDefault("selected_skill_damage_preview_text", "").AsString();
        _AssertEq(snapshotDamageText, "伤害 2-10", "HUD snapshot 应暴露非暴击基础伤害范围文案。");
        _AssertTrue(skillSubtitle.Contains("伤害 2-10"), "HUD 副标题应显示基础伤害范围。");

        runtime.dispose();
        gameSession.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task<GameSession> _InstallMockGameSession(GDictionary skillDefs)
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == "GameSession")
            {
                child.QueueFree();
            }
        }
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        var gameSession = new GameSession();
        gameSession.Name = "GameSession";
        gameSession._skill_defs = skillDefs;
        Root.AddChild(gameSession);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return gameSession;
    }

    private BattleState _BuildState(StringName battleId)
    {
        var state = new BattleState();
        state.battle_id = battleId;
        state.map_size = new Vector2I(4, 3);
        state.terrain_profile_id = new StringName("default");
        state.timeline = new BattleTimelineState();
        state.cells = new GDictionary();
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                state.cells[new Vector2I(x, y)] = _BuildCell(new Vector2I(x, y));
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        state.units = new GDictionary();
        state.ally_unit_ids = new Godot.Collections.Array<StringName>();
        state.enemy_unit_ids = new Godot.Collections.Array<StringName>();
        return state;
    }

    private BattleCellState _BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState();
        cell.coord = coord;
        cell.stack_layer = 0;
        cell.base_height = 0;
        cell.base_terrain = BattleCellState.TERRAIN_LAND();
        cell.recalculate_runtime_values();
        return cell;
    }

    private BattleUnitState _BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        List<StringName> skillIds,
        int currentAp
    )
    {
        var unit = new BattleUnitState();
        unit.unit_id = unitId;
        unit.display_name = displayName;
        unit.faction_id = factionId;
        unit.control_mode = new StringName("manual");
        unit.current_hp = 40;
        unit.current_mp = 4;
        unit.current_ap = currentAp;
        unit.current_stamina = 30;
        unit.current_aura = 0;
        unit.is_alive = true;
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(new StringName("hp_max"), 40);
        unit.attribute_snapshot.set_value(new StringName("mp_max"), 4);
        unit.attribute_snapshot.set_value(new StringName("stamina_max"), 30);
        unit.attribute_snapshot.set_value(new StringName("action_points"), Mathf.Max(currentAp, 1));
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 12);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.apply_weapon_projection(new GDictionary
        {
            ["weapon_profile_kind"] = "equipped",
            ["weapon_item_id"] = "hit_preview_test_blade",
            ["weapon_profile_type_id"] = "test_blade",
            ["weapon_current_grip"] = "one_handed",
            ["weapon_attack_range"] = 1,
            ["weapon_one_handed_dice"] = new GDictionary { ["dice_count"] = 1, ["dice_sides"] = 4, ["flat_bonus"] = 0 },
            ["weapon_uses_two_hands"] = false,
            ["weapon_physical_damage_tag"] = "physical_slash",
        });
        unit.known_active_skill_ids = new Godot.Collections.Array<StringName>(skillIds);
        foreach (StringName skillId in unit.known_active_skill_ids)
        {
            unit.known_skill_level_map[skillId] = 1;
        }
        return unit;
    }

    private void _AddUnitToRuntimeState(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit, bool isEnemy)
    {
        state.units[unit.unit_id] = unit;
        if (isEnemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        bool placed = runtime._grid_service.place_unit(state, unit, unit.coord, true);
        _AssertTrue(placed, "preview contract 测试单位应成功放入战场。");
    }

    private BattleCommand _BuildSkillCommand(
        StringName unitId,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName variantId = null
    )
    {
        variantId ??= new StringName("");
        var command = new BattleCommand();
        command.command_type = BattleCommand.TYPE_SKILL();
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.skill_variant_id = variantId;
        command.target_unit_id = targetUnit?.unit_id ?? new StringName("");
        command.target_coord = targetUnit?.coord ?? new Vector2I(-1, -1);
        return command;
    }

    private static T GetObject<T>(GDictionary dict, StringName key) where T : GodotObject
    {
        if (dict == null || !dict.ContainsKey(key))
            return null;
        return dict[key].AsGodotObject() as T;
    }

    private void _AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void _AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
