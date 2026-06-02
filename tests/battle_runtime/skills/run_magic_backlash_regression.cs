using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_magic_backlash_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestSpellControlMetadataUsesTypedStateAndProjection();
        TestFireballNormalCastHitsFriendAtFullDamageRoute();
        TestFireballBurnAppliesToEveryTeamInArea();
        TestFireballCriticalRefundsMpWithoutBlockingFriendlyFire();
        TestFireballProtectedFumbleConsumesExtraMpAndSkipsBlast();
        TestFireballUnprotectedFumbleDriftsGroundAnchor();

        if (_failures.Count == 0)
        {
            GD.Print("Magic backlash regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Magic backlash regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestSpellControlMetadataUsesTypedStateAndProjection()
    {
        Type metadataType = typeof(BattleSpellControlMetadata);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(metadataType),
            "BattleSpellControlMetadata 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            typeof(BattleSpellControlResult).GetProperty("SpellControl")?.PropertyType
                == typeof(BattleSpellControlMetadata),
            "BattleSpellControlResult 应持有 typed BattleSpellControlMetadata，而不是 Godot Dictionary。"
        );

        var metadata = new BattleSpellControlMetadata
        {
            AttackResolution = "critical_hit",
            SpellControlResolution = "critical_success",
            AttackSuccess = true,
            CriticalHit = true,
            HitRoll = 20,
            EffectiveHitRoll = 20,
        };
        BattleSpellControlResult result =
            BattleSpellControlResult.None(metadata) with { MpRefund = 5 };
        GDictionary payload = result.ToDictionary();
        GDictionary spellControl = payload["spell_control"].AsGodotDictionary();

        AssertEq(payload["mp_refund"].AsInt32(), 5, "spell-control result 应投影 MP 返还。");
        AssertEq(
            spellControl["spell_control_resolution"].AsString(),
            "critical_success",
            "spell-control metadata 只在 ToDictionary 边界投影。"
        );
    }

    private void TestFireballNormalCastHitsFriendAtFullDamageRoute()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(10);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("normal_caster", "player", new Vector2I(0, 0), 1, 200, 0);
        BattleUnitState friend = BuildUnit("normal_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        BattleUnitState enemy = BuildUnit("normal_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        BattleCommand command = BuildFireballCommand(caster.unit_id, friend.coord);
        BattlePreview preview = runtime.preview_command(command);
        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.issue_command(command);

        AssertTrue(preview.allowed, "火球术瞄准友军地格应通过地面目标预览。");
        AssertTrue(
            preview.target_unit_ids.Contains(friend.unit_id),
            "火球术普通预览应把范围内友军列为受影响单位。"
        );
        AssertTrue(
            batch.changed_unit_ids.Contains(friend.unit_id),
            "普通施法应标记被火球波及的友军。"
        );
        AssertTrue(friend.current_hp < beforeFriendHp, "普通施法时范围内友军应受到火球伤害。");
        AssertEq(caster.current_mp, 100, "普通施法只应扣除火球本身 100 法力。");
    }

    private void TestFireballBurnAppliesToEveryTeamInArea()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(10);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("burn_caster", "player", new Vector2I(0, 0), 1, 200, 3);
        BattleUnitState friend = BuildUnit("burn_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        BattleUnitState enemy = BuildUnit("burn_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        BattleEventBatch batch = runtime.issue_command(BuildFireballCommand(caster.unit_id, friend.coord));
        BattleStatusEffectState friendBurning = friend.get_status_effect("burning");
        int beforeBurnTickHp = friend.current_hp;

        AssertTrue(caster.has_status_effect("burning"), "火球术灼烧不应保护范围内施法者。");
        AssertTrue(friendBurning != null, "火球术灼烧应和伤害一样波及友军。");
        AssertTrue(enemy.has_status_effect("burning"), "火球术灼烧仍应作用于范围内敌人。");
        AssertEq(
            friendBurning != null ? friendBurning.tick_interval_tu : -1,
            10,
            "火球术友军灼烧应保留正式 timeline tick。"
        );
        AssertTrue(batch.changed_unit_ids.Contains(friend.unit_id), "友军被灼烧时应标记单位变化。");
        AdvanceTimelineTu(runtime, state, 10);
        AssertTrue(friend.current_hp < beforeBurnTickHp, "火球术友军灼烧应按 timeline tick 造成伤害。");
    }

    private void TestFireballCriticalRefundsMpWithoutBlockingFriendlyFire()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(20);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("crit_caster", "player", new Vector2I(0, 0), 1, 200, 0);
        BattleUnitState friend = BuildUnit("crit_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        BattleUnitState enemy = BuildUnit("crit_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.issue_command(BuildFireballCommand(caster.unit_id, friend.coord));

        AssertTrue(friend.current_hp < beforeFriendHp, "法术控制大成功不应取消范围内友军伤害。");
        AssertEq(caster.current_mp, 150, "火球术大成功应返还本次实际法力消耗的 50%。");
        AssertTrue(LogsContain(batch.log_lines, "返还 50 点法力"), "火球术大成功应写入 MP 返还日志。");
    }

    private void TestFireballProtectedFumbleConsumesExtraMpAndSkipsBlast()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(1);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("protected_caster", "player", new Vector2I(0, 0), 1, 250, 3);
        BattleUnitState friend = BuildUnit(
            "protected_friend",
            "player",
            new Vector2I(1, 0),
            0,
            0,
            0
        );
        BattleUnitState enemy = BuildUnit("protected_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.issue_command(BuildFireballCommand(caster.unit_id, friend.coord));

        AssertEq(friend.current_hp, beforeFriendHp, "受精通保护的大失败不应释放火球爆炸。");
        AssertEq(caster.current_mp, 50, "受保护大失败应在原 100 法力外额外吞噬 100 法力。");
        AssertEq(
            DictInt(caster.fumble_protection_used, new StringName("mage_fireball"), 0),
            1,
            "受保护大失败应消耗一次火球术保护次数。"
        );
        AssertTrue(!batch.changed_unit_ids.Contains(friend.unit_id), "受保护大失败不应标记目标友军变化。");
    }

    private void TestFireballUnprotectedFumbleDriftsGroundAnchor()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(1);
        BattleState state = BuildState(new Vector2I(2, 1));
        BattleUnitState caster = BuildUnit("drift_caster", "player", new Vector2I(0, 0), 1, 200, 0);
        BattleUnitState friend = BuildUnit("drift_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        Activate(runtime, state, caster);

        int beforeCasterHp = caster.current_hp;
        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.issue_command(BuildFireballCommand(caster.unit_id, caster.coord));

        AssertEq(caster.current_hp, beforeCasterHp, "无保护大失败偏移后不应继续结算原落点。");
        AssertTrue(friend.current_hp < beforeFriendHp, "无保护大失败应把火球偏移到唯一候选地格并伤到友军。");
        AssertTrue(LogsContain(batch.log_lines, "偏移到 (1, 0)"), "无保护大失败应写入明确的落点偏移日志。");
    }

    private static BattleRuntimeModule BuildRuntimeWithSpellControlRoll(int roll)
    {
        SkillDef skillDef = ResourceLoader.Load<SkillDef>("res://data/configs/skills/mage_fireball.tres");
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new GDictionary { [skillDef.skill_id] = skillDef },
            new GDictionary(),
            new GDictionary()
        );
        runtime.configure_damage_resolver_for_tests(
            new FixedFailedSaveDamageResolver(new GArray(), new GArray { roll })
        );
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(roll));
        return runtime;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "magic_backlash_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        state.cells = new GDictionary();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.cells[coord] = BuildCell(coord);
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState
        {
            coord = coord,
            base_terrain = BattleCellState.TERRAIN_LAND(),
            base_height = 4,
        };
        cell.recalculate_runtime_values();
        return cell;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentAp,
        int currentMp,
        int fireballLevel
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_ap = currentAp,
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            current_hp = 100,
            current_mp = currentMp,
            current_stamina = 60,
            is_alive = true,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 100);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), Math.Max(currentMp, 200));
        unit.attribute_snapshot.set_value(AttributeService.SPELL_PROFICIENCY_BONUS_ID(), 2);
        unit.attribute_snapshot.set_value("agility", 10);
        unit.attribute_snapshot.set_value("intelligence", 16);
        unit.attribute_snapshot.set_value("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.set_value("faith_luck_bonus", 0);
        unit.attribute_snapshot.set_value(
            AttributeService.ARMOR_CLASS_ID(),
            AttributeService.BASE_ARMOR_CLASS_VALUE()
        );
        unit.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        unit.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        if (fireballLevel >= 0)
        {
            unit.known_active_skill_ids.Add("mage_fireball");
            unit.known_skill_level_map[new StringName("mage_fireball")] = fireballLevel;
        }
        unit.set_anchor_coord(coord);
        return unit;
    }

    private void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.units[unit.unit_id] = unit;
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        AssertTrue(
            runtime._grid_service.place_unit(state, unit, unit.coord, true),
            $"{unit.unit_id} 应能放入战场。"
        );
    }

    private static void Activate(BattleRuntimeModule runtime, BattleState state, BattleUnitState caster)
    {
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
    }

    private static BattleCommand BuildFireballCommand(StringName unitId, Vector2I targetCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = unitId,
            skill_id = "mage_fireball",
            target_coord = targetCoord,
        };
        command.target_coords.Add(targetCoord);
        return command;
    }

    private static bool LogsContain(GStringArray logLines, string needle)
    {
        foreach (string line in logLines)
        {
            if (line.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void AdvanceTimelineTu(BattleRuntimeModule runtime, BattleState state, int totalTu)
    {
        if (runtime == null || state == null || totalTu <= 0)
        {
            return;
        }
        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.tu_per_tick = 5;
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            if (unitState != null)
            {
                unitState.action_threshold = 1_000_000;
            }
        }
        runtime.advance(totalTu / 5);
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private static int DictInt(GDictionary dictionary, StringName key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }
}
