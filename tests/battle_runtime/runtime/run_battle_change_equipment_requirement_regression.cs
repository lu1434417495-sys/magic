using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_change_equipment_requirement_regression : LifecycleTestSceneTree
{
    private static readonly StringName RestrictedHelmId = "requirement_test_restricted_helm";
    private static readonly StringName RestrictedHelmInstanceId =
        "requirement_test_restricted_helm_001";
    private static readonly StringName StrengthBoostHelmId = "requirement_test_strength_boost_helm";
    private static readonly StringName StrengthBoostHelmInstanceId =
        "requirement_test_strength_boost_helm_001";
    private static readonly StringName RequirementAscensionId = "requirement_test_ascension";
    private static readonly StringName RequirementAscensionStageId =
        "requirement_test_ascension_stage";
    private static readonly StringName DuplicateHelmId = "duplicate_test_helm";
    private static readonly StringName DuplicateHelmCommonInstanceId =
        "duplicate_test_helm_common_001";
    private static readonly StringName DuplicateHelmRareInstanceId =
        "duplicate_test_helm_rare_001";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestBattleChangeEquipmentEnforcesItemRequirement();
        TestBattleRequirementIgnoresTemporaryAndDisplacedAttributes();
        TestDuplicateSameItemBattleEquipAndUnequipPreservesInstance();
        TestChangeEquipmentRejectsInactiveCommandUnitWithTypedReport();
        RequestTestExit(_test.Finish("Battle change equipment requirement regression"));
    }

    private void TestBattleChangeEquipmentEnforcesItemRequirement()
    {
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            [RestrictedHelmId] = BuildRestrictedHelmItem(RestrictedHelmId),
        };
        PartyState party = BuildParty("requirement_hero", 2);
        PartyMemberState member = party.GetMemberState("requirement_hero");
        member.progression.unit_base_attributes.SetAttributeValue("strength", 17);
        _test.True(
            member.SetAscension(
                RequirementAscensionId,
                RequirementAscensionStageId,
                0,
                member.race_id
            ),
            "测试成员应能写入正式升华身份。"
        );
        var runtime = BuildRuntime(party, itemDefs, out CharacterManagementModule gateway);
        _test.Eq(
            gateway.GetMemberAttributeSnapshotForEquipmentView(
                member.member_id,
                member.equipment_state
            ).GetValue("strength"),
            20,
            "基础力量 17 与永久升华 +3 应形成稳定力量 20。"
        );
        BattleState state = BuildState("change_equipment_requirement_regression");
        BattleUnitState unit = BuildUnit("requirement_hero", Vector2I.Zero, 2);
        unit.source_member_id = "requirement_hero";
        unit.SetEquipmentView(member.equipment_state);
        BattleUnitState enemy = BuildUnit("requirement_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, unit, enemy);
        state.GetPartyBackpackView().equipment_instances = new()
        {
            MakeEquipmentInstance(RestrictedHelmInstanceId, RestrictedHelmId),
        };
        runtime.SetupStateForTests(state);

        BattleCommand command = BuildEquipCommand(
            unit.unit_id,
            "head",
            RestrictedHelmInstanceId,
            RestrictedHelmId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, "需求不满足时战斗换装 preview 应失败。");
        _test.True(
            preview != null && preview.log_lines.Count > 0,
            $"需求不满足时 preview 应回传失败反馈。 log={JoinLines(preview?.log_lines)}"
        );

        string[] backpackBefore = BackpackInstanceIdSignature(state.GetPartyBackpackView());
        BattleEventBatch blockedBatch = runtime.IssueCommand(command);
        IReadOnlyDictionary<string, object> blockedReport = FindChangeEquipmentReport(blockedBatch.report_entries);
        AssertFormalChangeEquipmentReportShape(blockedReport, "需求失败 report");
        _test.Eq(DictString(blockedReport, "error_code", ""), "item_not_equippable", "需求失败应只暴露泛化错误码。");
        _test.True(!blockedReport.ContainsKey("blockers"), "需求失败 report 不应透出隐藏 blocker 列表。");
        _test.Eq(unit.GetCurrentAp(), 2, "需求失败不应扣 AP。");
        _test.Eq(
            unit.GetEquipmentView().GetEquippedInstanceId("head").ToString(),
            "",
            "需求失败不应写入 battle-local 装备 view。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.GetPartyBackpackView()),
            backpackBefore,
            "需求失败不应移动背包实例。"
        );

        member.body_size = 3;
        member.progression.SetProfessionProgress(
            new UnitProfessionProgress
            {
                profession_id = "helmet_training",
                rank = 1,
            }
        );
        BattlePreview allowedPreview = runtime.PreviewCommand(command);
        _test.True(
            allowedPreview != null && allowedPreview.allowed,
            $"成员满足需求后同一 battle-local 装备 preview 应通过。 log={JoinLines(allowedPreview?.log_lines)}"
        );
        BattleEventBatch successBatch = runtime.IssueCommand(command);
        IReadOnlyDictionary<string, object> successReport = FindChangeEquipmentReport(successBatch.report_entries);
        AssertFormalChangeEquipmentReportShape(successReport, "需求满足 report");
        _test.True(DictBool(successReport, "ok", false), $"成员满足需求后换装应成功。 report={successReport}");
        _test.Eq(unit.GetCurrentAp(), 0, "需求满足后成功换装应扣 2 AP。");
        _test.Eq(
            unit.GetEquipmentView().GetEquippedInstanceId("head").ToString(),
            RestrictedHelmInstanceId.ToString(),
            "需求满足后应写入 battle-local 装备 view。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.GetPartyBackpackView()),
            Array.Empty<string>(),
            "需求满足后应从 battle-local 背包移除实例。"
        );
        runtime.Dispose();
        gateway.Dispose();
    }

    private void TestBattleRequirementIgnoresTemporaryAndDisplacedAttributes()
    {
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            [RestrictedHelmId] = BuildStrengthRestrictedHelmItem(RestrictedHelmId),
            [StrengthBoostHelmId] = BuildStrengthBoostHelmItem(StrengthBoostHelmId),
        };
        PartyState party = BuildParty("detached_requirement_hero", 3);
        PartyMemberState member = party.GetMemberState("detached_requirement_hero");
        member.progression.unit_base_attributes.SetAttributeValue("strength", 17);
        var runtime = BuildRuntime(party, itemDefs, out CharacterManagementModule gateway);
        BattleState state = BuildState("change_equipment_detached_requirement_regression");
        BattleUnitState unit = BuildUnit("detached_requirement_hero", Vector2I.Zero, 2);
        unit.source_member_id = member.member_id;
        unit.SetEquipmentView(new EquipmentState());
        unit.GetEquipmentView().SetEquippedEntry(
            "head",
            StrengthBoostHelmId,
            new[] { new StringName("head") },
            MakeEquipmentInstance(StrengthBoostHelmInstanceId, StrengthBoostHelmId)
        );
        unit.attribute_snapshot.SetValue("strength", 99);
        BattleUnitState enemy = BuildUnit("detached_requirement_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, unit, enemy);
        state.GetPartyBackpackView().equipment_instances = new()
        {
            MakeEquipmentInstance(RestrictedHelmInstanceId, RestrictedHelmId),
        };
        runtime.SetupStateForTests(state);

        _test.Eq(
            gateway.GetMemberAttributeSnapshotForEquipmentView(
                member.member_id,
                unit.GetEquipmentView()
            ).GetValue("strength"),
            20,
            "被替换头盔应是当前稳定力量达到 20 的唯一来源。"
        );
        BattleCommand command = BuildEquipCommand(
            unit.unit_id,
            "head",
            RestrictedHelmInstanceId,
            RestrictedHelmId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(
            preview != null && !preview.allowed,
            "战斗 preview 应先移除被替换头盔，且不得读取临时 battle snapshot 的力量 99。"
        );
        string[] backpackBefore = BackpackInstanceIdSignature(state.GetPartyBackpackView());
        BattleEventBatch batch = runtime.IssueCommand(command);
        IReadOnlyDictionary<string, object> report = FindChangeEquipmentReport(batch.report_entries);
        _test.Eq(
            DictString(report, "error_code", ""),
            "item_not_equippable",
            "战斗执行应与 detached requirement preview 同样拒绝。"
        );
        _test.Eq(unit.GetCurrentAp(), 2, "门槛失败不应扣除 AP。");
        _test.Eq(
            unit.GetEquipmentView().GetEquippedInstanceId("head").ToString(),
            StrengthBoostHelmInstanceId.ToString(),
            "门槛失败应保留原有增力头盔。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.GetPartyBackpackView()),
            backpackBefore,
            "门槛失败不应移动候选装备实例。"
        );
        runtime.Dispose();
        gateway.Dispose();
    }

    private void TestDuplicateSameItemBattleEquipAndUnequipPreservesInstance()
    {
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            [DuplicateHelmId] = BuildPlainHelmItem(DuplicateHelmId),
        };
        PartyState party = BuildParty("duplicate_hero", 2);
        PartyMemberState member = party.GetMemberState("duplicate_hero");
        var runtime = BuildRuntime(party, itemDefs, out CharacterManagementModule gateway);
        BattleState state = BuildState("change_equipment_duplicate_regression");
        BattleUnitState unit = BuildUnit("duplicate_hero", Vector2I.Zero, 4);
        unit.source_member_id = "duplicate_hero";
        unit.SetEquipmentView(member.equipment_state);
        BattleUnitState enemy = BuildUnit("duplicate_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, unit, enemy);
        EquipmentInstanceState commonInstance = MakeEquipmentInstance(
            DuplicateHelmCommonInstanceId,
            DuplicateHelmId
        );
        commonInstance.rarity = (int)EquipmentInstanceState.RarityTier.COMMON;
        commonInstance.current_durability = 12;
        EquipmentInstanceState rareInstance = MakeEquipmentInstance(
            DuplicateHelmRareInstanceId,
            DuplicateHelmId
        );
        rareInstance.rarity = (int)EquipmentInstanceState.RarityTier.RARE;
        rareInstance.current_durability = 29;
        state.GetPartyBackpackView().equipment_instances = new()
        {
            commonInstance,
            rareInstance,
        };
        runtime.SetupStateForTests(state);

        BattleCommand missingInstanceCommand = BuildEquipCommand(unit.unit_id, "head", "", DuplicateHelmId);
        BattleEventBatch missingInstanceBatch = runtime.IssueCommand(missingInstanceCommand);
        IReadOnlyDictionary<string, object> missingReport = FindChangeEquipmentReport(missingInstanceBatch.report_entries);
        AssertFormalChangeEquipmentReportShape(missingReport, "缺少 instance_id report");
        _test.Eq(
            DictString(missingReport, "error_code", ""),
            "equipment_instance_required",
            "战斗换装正式命令缺少 instance_id 应拒绝。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.GetPartyBackpackView()),
            new[] { DuplicateHelmCommonInstanceId.ToString(), DuplicateHelmRareInstanceId.ToString() },
            "缺少 instance_id 失败后两个重复实例都应留在背包。"
        );

        BattleCommand equipCommand = BuildEquipCommand(
            unit.unit_id,
            "head",
            DuplicateHelmRareInstanceId,
            DuplicateHelmId
        );
        BattleEventBatch equipBatch = runtime.IssueCommand(equipCommand);
        IReadOnlyDictionary<string, object> equipReport = FindChangeEquipmentReport(equipBatch.report_entries);
        AssertFormalChangeEquipmentReportShape(equipReport, "指定 instance_id 装备 report");
        _test.True(DictBool(equipReport, "ok", false), $"指定 rare instance_id 的 battle-local 装备应成功。 report={equipReport}");
        _test.Eq(
            unit.GetEquipmentView().GetEquippedInstanceId("head").ToString(),
            DuplicateHelmRareInstanceId.ToString(),
            "battle-local 装备位应写入指定 rare instance_id。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.GetPartyBackpackView()),
            new[] { DuplicateHelmCommonInstanceId.ToString() },
            "装备 rare 后 common 实例应留在背包。"
        );
        EquipmentInstanceState equippedInstance = unit.GetEquipmentView().GetEquippedInstance("head");
        _test.True(equippedInstance != null, "battle-local 装备位应保留完整 rare 实例。");
        if (equippedInstance != null)
        {
            _test.Eq(
                equippedInstance.rarity,
                (int)EquipmentInstanceState.RarityTier.RARE,
                "battle-local 装备位应保留 rare 品质。"
            );
            _test.Eq(equippedInstance.current_durability, 29, "battle-local 装备位应保留 rare 耐久。");
        }

        unit.SetCurrentAp(2);
        BattleCommand unequipCommand = BuildUnequipCommand(
            unit.unit_id,
            "head",
            DuplicateHelmRareInstanceId
        );
        BattleEventBatch unequipBatch = runtime.IssueCommand(unequipCommand);
        IReadOnlyDictionary<string, object> unequipReport = FindChangeEquipmentReport(unequipBatch.report_entries);
        AssertFormalChangeEquipmentReportShape(unequipReport, "指定 instance_id 卸装 report");
        _test.True(DictBool(unequipReport, "ok", false), $"指定 rare instance_id 的 battle-local 卸装应成功。 report={unequipReport}");
        _test.Eq(
            unit.GetEquipmentView().GetEquippedInstanceId("head").ToString(),
            "",
            "卸装后 head 槽应清空。"
        );
        AssertSequenceEq(
            BackpackInstanceIdSignature(state.GetPartyBackpackView()),
            new[] { DuplicateHelmCommonInstanceId.ToString(), DuplicateHelmRareInstanceId.ToString() },
            "卸装后 common 与 rare 实例都应在背包。"
        );
        EquipmentInstanceState returnedInstance = FindBackpackInstance(
            state.GetPartyBackpackView(),
            DuplicateHelmRareInstanceId
        );
        _test.True(returnedInstance != null, "卸回背包后应能按 instance_id 找到 rare 实例。");
        if (returnedInstance != null)
        {
            _test.Eq(
                returnedInstance.rarity,
                (int)EquipmentInstanceState.RarityTier.RARE,
                "卸回背包的 rare 实例应保留品质。"
            );
            _test.Eq(returnedInstance.current_durability, 29, "卸回背包的 rare 实例应保留耐久。");
        }
        runtime.Dispose();
        gateway.Dispose();
    }

    private void TestChangeEquipmentRejectsInactiveCommandUnitWithTypedReport()
    {
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            [DuplicateHelmId] = BuildPlainHelmItem(DuplicateHelmId),
        };
        PartyState party = BuildParty("active_hero", 2);
        var runtime = BuildRuntime(party, itemDefs, out CharacterManagementModule gateway);
        BattleState state = BuildState("change_equipment_inactive_command_unit");
        BattleUnitState activeUnit = BuildUnit("active_hero", Vector2I.Zero, 4);
        BattleUnitState otherUnit = BuildUnit("other_hero", new Vector2I(1, 0), 4);
        BattleUnitState enemy = BuildUnit("inactive_enemy", new Vector2I(2, 0), 0);
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, activeUnit, enemy, otherUnit);
        state.active_unit_id = activeUnit.unit_id;
        state.GetPartyBackpackView().equipment_instances = new()
        {
            MakeEquipmentInstance(DuplicateHelmCommonInstanceId, DuplicateHelmId),
        };
        runtime.SetupStateForTests(state);

        BattleCommand command = BuildEquipCommand(
            otherUnit.unit_id,
            "head",
            DuplicateHelmCommonInstanceId,
            DuplicateHelmId
        );
        BattleEventBatch batch = runtime.IssueCommand(command);
        IReadOnlyDictionary<string, object> report = FindChangeEquipmentReport(batch.report_entries);
        AssertFormalChangeEquipmentReportShape(report, "非当前行动单位 report");
        _test.True(!DictBool(report, "ok", true), "非当前行动单位发起换装应失败。");
        _test.Eq(DictString(report, "error_code", ""), "target_not_self", "非当前行动单位 report 应保持 target_not_self。");
        _test.Eq(
            DictString(report, "target_unit_id", ""),
            otherUnit.unit_id.ToString(),
            "非当前行动单位 report 应记录命令目标单位。"
        );
        runtime.Dispose();
        gateway.Dispose();
    }

    private static BattleRuntimeModule BuildRuntime(
        PartyState party,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        out CharacterManagementModule gateway
    )
    {
        gateway = new CharacterManagementModule();
        gateway.setup(
            party,
            item_defs: itemDefinitions,
            progression_identity_catalog: BuildRequirementIdentityCatalog()
        );
        var runtime = new BattleRuntimeModule();
        runtime.setup(gateway, item_defs: itemDefinitions);
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
        state.SetUnit(ally);
        state.ally_unit_ids.Add(ally.unit_id);
        if (extraAlly != null)
        {
            state.SetUnit(extraAlly);
            state.ally_unit_ids.Add(extraAlly.unit_id);
        }
        state.SetUnit(enemy);
        state.enemy_unit_ids.Add(enemy.unit_id);
        state.active_unit_id = ally.unit_id;
        _test.True(runtime._grid_service.PlaceUnit(state, ally, ally.GetAnchorCoord(), true), "测试友方应能放入战场。");
        if (extraAlly != null)
        {
            _test.True(
                runtime._grid_service.PlaceUnit(state, extraAlly, extraAlly.GetAnchorCoord(), true),
                "额外测试友方应能放入战场。"
            );
        }
        _test.True(runtime._grid_service.PlaceUnit(state, enemy, enemy.GetAnchorCoord(), true), "测试敌方应能放入战场。");
    }

    private static ItemDefinition BuildRestrictedHelmItem(StringName itemId)
    {
        return BuildHelmItem(
            itemId,
            "Requirement Test Helm",
            new EquipmentRequirementDefinition(
                new[] { "helmet_training" },
                3,
                0,
                new[] { new EquipmentAttributeRequirementDefinition("strength", 20) }
            )
        );
    }

    private static ItemDefinition BuildStrengthRestrictedHelmItem(StringName itemId) =>
        BuildHelmItem(
            itemId,
            "Strength Requirement Test Helm",
            new EquipmentRequirementDefinition(
                Array.Empty<string>(),
                0,
                0,
                new[] { new EquipmentAttributeRequirementDefinition("strength", 20) }
            )
        );

    private static ItemDefinition BuildStrengthBoostHelmItem(StringName itemId) =>
        BuildHelmItem(
            itemId,
            "Strength Boost Test Helm",
            null,
            new[]
            {
                new AttributeModifierDefinition(
                    "strength",
                    AttributeModifier.ToStringName(AttributeModifierMode.Flat),
                    3,
                    0,
                    "equipment",
                    "requirement_test_strength_boost"
                ),
            }
        );

    private static ItemDefinition BuildPlainHelmItem(StringName itemId) =>
        BuildHelmItem(itemId, "Duplicate Test Helm", null);

    private static ItemDefinition BuildHelmItem(
        StringName itemId,
        string displayName,
        EquipmentRequirementDefinition requirement,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers = null
    )
    {
        return new ItemDefinition(
            itemId,
            "",
            displayName,
            "",
            "",
            false,
            0,
            0,
            0,
            true,
            1,
            ItemDefinition.ToStringName(ItemCategoryKind.Equipment),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitRollGroupDefinition>(),
            new[] { "head" },
            attributeModifiers ?? Array.Empty<AttributeModifierDefinition>(),
            "",
            Array.Empty<string>(),
            requirement,
            ItemDefinition.ToStringName(ItemEquipmentTypeKind.Armor),
            null,
            -1
        );
    }

    private static ProgressionIdentityCatalogData BuildRequirementIdentityCatalog()
    {
        AscensionDefinition ascension = new(
            RequirementAscensionId,
            "Requirement Test Ascension",
            "",
            new[] { RequirementAscensionStageId },
            Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<string>(),
            false,
            false
        );
        AscensionStageDefinition stage = new(
            RequirementAscensionStageId,
            RequirementAscensionId,
            "Requirement Test Ascension Stage",
            "",
            new[]
            {
                new AttributeModifierDefinition(
                    "strength",
                    AttributeModifier.ToStringName(AttributeModifierMode.Flat),
                    3,
                    0,
                    "ascension",
                    RequirementAscensionStageId
                ),
            },
            Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(),
            "",
            Array.Empty<string>()
        );
        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition>(),
            new Dictionary<StringName, SubraceDefinition>(),
            new Dictionary<StringName, AgeProfileDefinition>(),
            new Dictionary<StringName, BloodlineDefinition>(),
            new Dictionary<StringName, BloodlineStageDefinition>(),
            new Dictionary<StringName, AscensionDefinition>
            {
                [RequirementAscensionId] = ascension,
            },
            new Dictionary<StringName, AscensionStageDefinition>
            {
                [RequirementAscensionStageId] = stage,
            },
            new Dictionary<StringName, StageAdvancementDefinition>()
        );
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
        party.SetMemberState(member);
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
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, int currentAp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
        }.WithCombatResourcesForTest(
            hp: 20,
            ap: currentAp,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        unit.SetAnchorCoord(coord);
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
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.ChangeEquipment),
            unit_id = unitId,
            target_unit_id = unitId,
            equipment_operation = BattleTypedNames.ToStringName(BattleEquipmentOperationKind.Equip),
            equipment_slot_id = slotId,
            equipment_item_id = itemId,
            equipment_instance_id = instanceId,
            equipment_instance = MakeEquipmentInstance(instanceId, itemId),
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
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.ChangeEquipment),
            unit_id = unitId,
            target_unit_id = unitId,
            equipment_operation = BattleTypedNames.ToStringName(BattleEquipmentOperationKind.Unequip),
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

    private static IReadOnlyDictionary<string, object> FindChangeEquipmentReport(
        IEnumerable<IReadOnlyDictionary<string, object>> reportEntries
    )
    {
        foreach (
            IReadOnlyDictionary<string, object> entry in
            reportEntries ?? Array.Empty<IReadOnlyDictionary<string, object>>()
        )
        {
            if (DictString(entry, "type", "") == "change_equipment")
                return entry;
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private void AssertFormalChangeEquipmentReportShape(
        IReadOnlyDictionary<string, object> report,
        string context
    )
    {
        _test.Eq(DictString(report, "type", ""), "change_equipment", $"{context} 应使用正式 type 字段。");
        _test.True(!report.ContainsKey("entry_type"), $"{context} 不应再输出旧 entry_type 字段。");
        _test.True(!report.ContainsKey("reason_id"), $"{context} 不应再输出旧 reason_id 字段。");
        _test.True(!report.ContainsKey("current_ap"), $"{context} 不应再输出旧 current_ap 字段。");
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

    private static string JoinLines(IEnumerable<string> lines)
    {
        return string.Join(" | ", lines ?? Array.Empty<string>());
    }

    private static bool DictBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        bool fallback
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        object value = dictionary[key];
        return value is bool flag ? flag : fallback;
    }

    private static string DictString(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        string fallback
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        object value = dictionary[key];
        return value?.ToString() ?? fallback;
    }

    private void AssertSequenceEq(string[] actual, string[] expected, string message)
    {
        if (actual.Length != expected.Length)
        {
            _test.Fail(
                $"{message} | actual=[{string.Join(",", actual)}] expected=[{string.Join(",", expected)}]"
            );
            return;
        }
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                _test.Fail(
                    $"{message} | actual=[{string.Join(",", actual)}] expected=[{string.Join(",", expected)}]"
                );
                return;
            }
        }
    }
}
