using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_change_equipment_requirement_regression : SceneTree
{
    private static readonly StringName RestrictedHelmId = "requirement_test_restricted_helm";
    private static readonly StringName RestrictedHelmInstanceId =
        "requirement_test_restricted_helm_001";
    private static readonly StringName DuplicateHelmId = "duplicate_test_helm";
    private static readonly StringName DuplicateHelmCommonInstanceId =
        "duplicate_test_helm_common_001";
    private static readonly StringName DuplicateHelmRareInstanceId =
        "duplicate_test_helm_rare_001";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestBattleChangeEquipmentEnforcesItemRequirement();
        TestDuplicateSameItemBattleEquipAndUnequipPreservesInstance();
        TestChangeEquipmentRejectsInactiveCommandUnitWithTypedReport();

        if (_failures.Count == 0)
        {
            GD.Print("Battle change equipment requirement regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle change equipment requirement regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestBattleChangeEquipmentEnforcesItemRequirement()
    {
        var itemDefs = new GDictionary
        {
            [RestrictedHelmId] = BuildRestrictedHelmItem(RestrictedHelmId),
        };
        PartyState party = BuildParty("requirement_hero", 2);
        PartyMemberState member = party.get_member_state("requirement_hero");
        var runtime = BuildRuntime(party, itemDefs);
        BattleState state = BuildState("change_equipment_requirement_regression");
        BattleUnitState unit = BuildUnit("requirement_hero", Vector2I.Zero, 2);
        unit.source_member_id = "requirement_hero";
        unit.set_equipment_view(member.equipment_state);
        BattleUnitState enemy = BuildUnit("requirement_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, unit, enemy);
        state.get_party_backpack_view().equipment_instances = new()
        {
            MakeEquipmentInstance(RestrictedHelmInstanceId, RestrictedHelmId),
        };
        runtime._state = state;

        BattleCommand command = BuildEquipCommand(
            unit.unit_id,
            "head",
            RestrictedHelmInstanceId,
            RestrictedHelmId
        );
        BattlePreview preview = runtime.preview_command(command);
        AssertTrue(preview != null && !preview.allowed, "需求不满足时战斗换装 preview 应失败。");
        AssertTrue(
            preview != null && AnyLineContains(preview.log_lines, "当前无法装备"),
            $"需求不满足时 preview 应显示泛化失败原因。 log={JoinLines(preview?.log_lines)}"
        );
        AssertTrue(
            preview != null
                && !JoinLines(preview.log_lines).Contains("missing_profession")
                && !JoinLines(preview.log_lines).Contains("body_size_too_small")
                && !JoinLines(preview.log_lines).Contains("缺少所需职业")
                && !JoinLines(preview.log_lines).Contains("体型过小"),
            $"需求不满足时 preview 不应泄露隐藏需求。 log={JoinLines(preview?.log_lines)}"
        );

        string[] backpackBefore = BackpackInstanceIdSignature(state.get_party_backpack_view());
        BattleEventBatch blockedBatch = runtime.issue_command(command);
        GDictionary blockedReport = FindChangeEquipmentReport(blockedBatch.report_entries);
        AssertEq(DictString(blockedReport, "error_code", ""), "item_not_equippable", "需求失败应只暴露泛化错误码。");
        AssertTrue(!blockedReport.ContainsKey("blockers"), "需求失败 report 不应透出隐藏 blocker 列表。");
        AssertEq(unit.current_ap, 2, "需求失败不应扣 AP。");
        AssertEq(
            unit.get_equipment_view().get_equipped_instance_id("head").ToString(),
            "",
            "需求失败不应写入 battle-local 装备 view。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.get_party_backpack_view()),
            backpackBefore,
            "需求失败不应移动背包实例。"
        );

        member.body_size = 3;
        member.progression.set_profession_progress(
            new UnitProfessionProgress
            {
                profession_id = "helmet_training",
                rank = 1,
            }
        );
        BattlePreview allowedPreview = runtime.preview_command(command);
        AssertTrue(
            allowedPreview != null && allowedPreview.allowed,
            $"成员满足需求后同一 battle-local 装备 preview 应通过。 log={JoinLines(allowedPreview?.log_lines)}"
        );
        BattleEventBatch successBatch = runtime.issue_command(command);
        GDictionary successReport = FindChangeEquipmentReport(successBatch.report_entries);
        AssertTrue(DictBool(successReport, "ok", false), $"成员满足需求后换装应成功。 report={successReport}");
        AssertEq(unit.current_ap, 0, "需求满足后成功换装应扣 2 AP。");
        AssertEq(
            unit.get_equipment_view().get_equipped_instance_id("head").ToString(),
            RestrictedHelmInstanceId.ToString(),
            "需求满足后应写入 battle-local 装备 view。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.get_party_backpack_view()),
            Array.Empty<string>(),
            "需求满足后应从 battle-local 背包移除实例。"
        );
    }

    private void TestDuplicateSameItemBattleEquipAndUnequipPreservesInstance()
    {
        var itemDefs = new GDictionary { [DuplicateHelmId] = BuildPlainHelmItem(DuplicateHelmId) };
        PartyState party = BuildParty("duplicate_hero", 2);
        PartyMemberState member = party.get_member_state("duplicate_hero");
        var runtime = BuildRuntime(party, itemDefs);
        BattleState state = BuildState("change_equipment_duplicate_regression");
        BattleUnitState unit = BuildUnit("duplicate_hero", Vector2I.Zero, 4);
        unit.source_member_id = "duplicate_hero";
        unit.set_equipment_view(member.equipment_state);
        BattleUnitState enemy = BuildUnit("duplicate_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, unit, enemy);
        EquipmentInstanceState commonInstance = MakeEquipmentInstance(
            DuplicateHelmCommonInstanceId,
            DuplicateHelmId
        );
        commonInstance.rarity = EquipmentInstanceState.RARITY_TIER_COMMON();
        commonInstance.current_durability = 12;
        EquipmentInstanceState rareInstance = MakeEquipmentInstance(
            DuplicateHelmRareInstanceId,
            DuplicateHelmId
        );
        rareInstance.rarity = EquipmentInstanceState.RARITY_TIER_RARE();
        rareInstance.current_durability = 29;
        state.get_party_backpack_view().equipment_instances = new()
        {
            commonInstance,
            rareInstance,
        };
        runtime._state = state;

        BattleCommand missingInstanceCommand = BuildEquipCommand(unit.unit_id, "head", "", DuplicateHelmId);
        BattleEventBatch missingInstanceBatch = runtime.issue_command(missingInstanceCommand);
        GDictionary missingReport = FindChangeEquipmentReport(missingInstanceBatch.report_entries);
        AssertEq(
            DictString(missingReport, "error_code", ""),
            "equipment_instance_required",
            "战斗换装正式命令缺少 instance_id 应拒绝。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.get_party_backpack_view()),
            new[] { DuplicateHelmCommonInstanceId.ToString(), DuplicateHelmRareInstanceId.ToString() },
            "缺少 instance_id 失败后两个重复实例都应留在背包。"
        );

        BattleCommand equipCommand = BuildEquipCommand(
            unit.unit_id,
            "head",
            DuplicateHelmRareInstanceId,
            DuplicateHelmId
        );
        BattleEventBatch equipBatch = runtime.issue_command(equipCommand);
        GDictionary equipReport = FindChangeEquipmentReport(equipBatch.report_entries);
        AssertTrue(DictBool(equipReport, "ok", false), $"指定 rare instance_id 的 battle-local 装备应成功。 report={equipReport}");
        AssertEq(
            unit.get_equipment_view().get_equipped_instance_id("head").ToString(),
            DuplicateHelmRareInstanceId.ToString(),
            "battle-local 装备位应写入指定 rare instance_id。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.get_party_backpack_view()),
            new[] { DuplicateHelmCommonInstanceId.ToString() },
            "装备 rare 后 common 实例应留在背包。"
        );
        EquipmentInstanceState equippedInstance = unit.get_equipment_view().get_equipped_instance("head");
        AssertTrue(equippedInstance != null, "battle-local 装备位应保留完整 rare 实例。");
        if (equippedInstance != null)
        {
            AssertEq(
                equippedInstance.rarity,
                EquipmentInstanceState.RARITY_TIER_RARE(),
                "battle-local 装备位应保留 rare 品质。"
            );
            AssertEq(equippedInstance.current_durability, 29, "battle-local 装备位应保留 rare 耐久。");
        }

        unit.current_ap = 2;
        BattleCommand unequipCommand = BuildUnequipCommand(
            unit.unit_id,
            "head",
            DuplicateHelmRareInstanceId
        );
        BattleEventBatch unequipBatch = runtime.issue_command(unequipCommand);
        GDictionary unequipReport = FindChangeEquipmentReport(unequipBatch.report_entries);
        AssertTrue(DictBool(unequipReport, "ok", false), $"指定 rare instance_id 的 battle-local 卸装应成功。 report={unequipReport}");
        AssertEq(
            unit.get_equipment_view().get_equipped_instance_id("head").ToString(),
            "",
            "卸装后 head 槽应清空。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.get_party_backpack_view()),
            new[] { DuplicateHelmCommonInstanceId.ToString(), DuplicateHelmRareInstanceId.ToString() },
            "卸装后 common 与 rare 实例都应在背包。"
        );
        EquipmentInstanceState returnedInstance = FindBackpackInstance(
            state.get_party_backpack_view(),
            DuplicateHelmRareInstanceId
        );
        AssertTrue(returnedInstance != null, "卸回背包后应能按 instance_id 找到 rare 实例。");
        if (returnedInstance != null)
        {
            AssertEq(
                returnedInstance.rarity,
                EquipmentInstanceState.RARITY_TIER_RARE(),
                "卸回背包的 rare 实例应保留品质。"
            );
            AssertEq(returnedInstance.current_durability, 29, "卸回背包的 rare 实例应保留耐久。");
        }
    }

    private void TestChangeEquipmentRejectsInactiveCommandUnitWithTypedReport()
    {
        var itemDefs = new GDictionary { [DuplicateHelmId] = BuildPlainHelmItem(DuplicateHelmId) };
        PartyState party = BuildParty("active_hero", 2);
        var runtime = BuildRuntime(party, itemDefs);
        BattleState state = BuildState("change_equipment_inactive_command_unit");
        BattleUnitState activeUnit = BuildUnit("active_hero", Vector2I.Zero, 4);
        BattleUnitState otherUnit = BuildUnit("other_hero", new Vector2I(1, 0), 4);
        BattleUnitState enemy = BuildUnit("inactive_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, activeUnit, enemy, otherUnit);
        state.active_unit_id = activeUnit.unit_id;
        state.get_party_backpack_view().equipment_instances = new()
        {
            MakeEquipmentInstance(DuplicateHelmCommonInstanceId, DuplicateHelmId),
        };
        runtime._state = state;

        BattleCommand command = BuildEquipCommand(
            otherUnit.unit_id,
            "head",
            DuplicateHelmCommonInstanceId,
            DuplicateHelmId
        );
        BattleEventBatch batch = runtime.issue_command(command);
        GDictionary report = FindChangeEquipmentReport(batch.report_entries);
        AssertTrue(!DictBool(report, "ok", true), "非当前行动单位发起换装应失败。");
        AssertEq(DictString(report, "error_code", ""), "target_not_self", "非当前行动单位 report 应保持 target_not_self。");
        AssertEq(
            DictString(report, "target_unit_id", ""),
            otherUnit.unit_id.ToString(),
            "非当前行动单位 report 应记录命令目标单位。"
        );
    }

    private static BattleRuntimeModule BuildRuntime(PartyState party, GDictionary itemDefs)
    {
        var gateway = new CharacterManagementModule();
        gateway.setup(party, new GDictionary(), new GDictionary(), new GDictionary(), itemDefs);
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            gateway,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            null,
            itemDefs
        );
        return runtime;
    }

    private void InstallUnits(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState ally,
        BattleUnitState enemy,
        BattleUnitState extraAlly = null
    )
    {
        state.units[ally.unit_id] = ally;
        state.ally_unit_ids.Add(ally.unit_id);
        if (extraAlly != null)
        {
            state.units[extraAlly.unit_id] = extraAlly;
            state.ally_unit_ids.Add(extraAlly.unit_id);
        }
        state.units[enemy.unit_id] = enemy;
        state.enemy_unit_ids.Add(enemy.unit_id);
        state.active_unit_id = ally.unit_id;
        AssertTrue(runtime._grid_service.place_unit(state, ally, ally.coord, true), "测试友方应能放入战场。");
        if (extraAlly != null)
        {
            AssertTrue(
                runtime._grid_service.place_unit(state, extraAlly, extraAlly.coord, true),
                "额外测试友方应能放入战场。"
            );
        }
        AssertTrue(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "测试敌方应能放入战场。");
    }

    private static ItemDef BuildRestrictedHelmItem(StringName itemId)
    {
        var itemDef = BuildPlainHelmItem(itemId);
        var requirement = new EquipmentRequirement
        {
            required_profession_ids = new GStringArray { "helmet_training" },
            min_body_size = 3,
        };
        itemDef.display_name = "Requirement Test Helm";
        itemDef.equip_requirement = requirement;
        return itemDef;
    }

    private static ItemDef BuildPlainHelmItem(StringName itemId)
    {
        return new ItemDef
        {
            item_id = itemId,
            display_name = "Duplicate Test Helm",
            item_category = "equipment",
            equipment_type_id = "armor",
            equipment_slot_ids = new GStringArray { "head" },
            is_stackable = false,
            max_stack = 1,
        };
    }

    private static PartyState BuildParty(StringName memberId, int bodySize)
    {
        var party = new PartyState();
        var member = new PartyMemberState
        {
            member_id = memberId,
            display_name = "Requirement Hero",
            body_size = bodySize,
            current_hp = 20,
            current_mp = 5,
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = "Requirement Hero",
                unit_base_attributes = new UnitBaseAttributes(),
            },
        };
        member.progression.unit_base_attributes.custom_stats["storage_space"] = 4;
        party.set_member_state(member);
        party.active_member_ids = new GStringNameArray { memberId };
        party.leader_member_id = memberId;
        party.main_character_member_id = memberId;
        return party;
    }

    private static BattleState BuildState(StringName battleId)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = new Vector2I(3, 1),
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                };
                cell.recalculate_runtime_values();
                state.cells[coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, int currentAp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = currentAp,
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            current_hp = 20,
            is_alive = true,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 20);
        unit.set_anchor_coord(coord);
        return unit;
    }

    private static BattleCommand BuildEquipCommand(
        StringName unitId,
        StringName slotId,
        StringName instanceId,
        StringName itemId
    )
    {
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_CHANGE_EQUIPMENT(),
            unit_id = unitId,
            target_unit_id = unitId,
            equipment_operation = BattleCommand.EQUIPMENT_OPERATION_EQUIP(),
            equipment_slot_id = slotId,
            equipment_item_id = itemId,
            equipment_instance_id = instanceId,
            equipment_instance = new GDictionary
            {
                ["instance_id"] = instanceId.ToString(),
                ["item_id"] = itemId.ToString(),
            },
        };
    }

    private static BattleCommand BuildUnequipCommand(
        StringName unitId,
        StringName slotId,
        StringName instanceId
    )
    {
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_CHANGE_EQUIPMENT(),
            unit_id = unitId,
            target_unit_id = unitId,
            equipment_operation = BattleCommand.EQUIPMENT_OPERATION_UNEQUIP(),
            equipment_slot_id = slotId,
            equipment_instance_id = instanceId,
        };
    }

    private static EquipmentInstanceState MakeEquipmentInstance(StringName instanceId, StringName itemId)
    {
        return new EquipmentInstanceState
        {
            instance_id = instanceId,
            item_id = itemId,
        };
    }

    private static GDictionary FindChangeEquipmentReport(GArray reportEntries)
    {
        foreach (Variant entryValue in reportEntries ?? new GArray())
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictString(entry, "type", DictString(entry, "entry_type", "")) == "change_equipment")
            {
                return entry;
            }
        }
        return new GDictionary();
    }

    private static string[] BackpackInstanceIdSignature(WarehouseState backpackView)
    {
        var result = new List<string>();
        if (backpackView == null)
        {
            return result.ToArray();
        }
        foreach (EquipmentInstanceState instance in backpackView.equipment_instances)
        {
            if (instance == null)
            {
                continue;
            }
            result.Add(instance.instance_id.ToString());
        }
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    private static EquipmentInstanceState FindBackpackInstance(
        WarehouseState backpackView,
        StringName instanceId
    )
    {
        if (backpackView == null)
        {
            return null;
        }
        foreach (EquipmentInstanceState instance in backpackView.equipment_instances)
        {
            if (instance != null && instance.instance_id == instanceId)
            {
                return instance;
            }
        }
        return null;
    }

    private static bool AnyLineContains(GArray lines, string fragment)
    {
        foreach (Variant lineValue in lines ?? new GArray())
        {
            string line = lineValue.ToString();
            if (line.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string JoinLines(GArray lines)
    {
        var values = new List<string>();
        foreach (Variant lineValue in lines ?? new GArray())
        {
            values.Add(lineValue.ToString());
        }
        return string.Join(" | ", values);
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertSequenceEq(string[] actual, string[] expected, string message)
    {
        if (actual.Length != expected.Length)
        {
            _failures.Add(
                $"{message} | actual=[{string.Join(",", actual)}] expected=[{string.Join(",", expected)}]"
            );
            return;
        }
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                _failures.Add(
                    $"{message} | actual=[{string.Join(",", actual)}] expected=[{string.Join(",", expected)}]"
                );
                return;
            }
        }
    }
}
