using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_equipment_text_command_regression : LifecycleTestSceneTree
{
    private static readonly StringName VersatileTestWeaponId = "wpndice_versatile_longsword";
    private static readonly StringName OffhandTestItemId = "wpndice_offhand_focus";
    private static readonly StringName RestrictedTestHelmId = "wpndice_restricted_helm";
    private static readonly StringName StringKeyOnlyTestHelmId = "wpndice_string_key_only_helm";
    private static readonly StringName StringKeyOnlyTestHelmInstanceId =
        "wpndice_string_key_only_helm_001";
    private static readonly StringName DuplicateTestCharmId = "wpndice_duplicate_charm";
    private static readonly StringName DuplicateTestCharmCommonInstanceId =
        "wpndice_duplicate_charm_common_001";
    private static readonly StringName DuplicateTestCharmRareInstanceId =
        "wpndice_duplicate_charm_rare_001";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();

        InstallBattleEquipmentTestItems(runner);
        RunCommand(runner, "game new test");
        RunCommand(runner, "warehouse capacity 10");
        RunCommand(runner, "warehouse add bronze_sword 1");
        RunCommand(runner, "warehouse add leather_cap 1");
        RunCommand(runner, "warehouse add leather_jerkin 1");
        RunCommand(runner, "warehouse add iron_greatsword 1");
        RunCommand(runner, $"warehouse add {VersatileTestWeaponId} 1");
        RunCommand(runner, $"warehouse add {OffhandTestItemId} 1");
        RunCommand(runner, $"warehouse add {RestrictedTestHelmId} 1");
        RunCommand(runner, "battle start settlement");
        RunCommand(runner, "battle confirm");
        AdvanceToManualBattleTurn(runner);
        BattleUnitState activeUnitState = GetActiveUnitState(runner);
        string activeMemberId = activeUnitState?.source_member_id.ToString() ?? "";
        _test.True(!string.IsNullOrEmpty(activeMemberId), "战斗换装回归前置：手动单位应关联队伍成员。");

        InstallStringKeyOnlyBattleItemInstance(runner);
        PrimeActiveUnitAp(runner, 2);
        GameTextCommandResult stringKeyResult = RunCommandExpectFail(
            runner,
            $"battle equip head {StringKeyOnlyTestHelmId} instance_id={StringKeyOnlyTestHelmInstanceId}"
        );
        AssertBattleEquipStringKeyOnlyItemNotFound(
            stringKeyResult.SnapshotTyped,
            stringKeyResult.snapshot_text
        );

        PrimeActiveUnitAp(runner, 2);
        GameTextCommandResult requirementResult = RunCommandExpectFail(
            runner,
            $"battle equip head {RestrictedTestHelmId}"
        );
        AssertBattleEquipRequirementFailure(
            requirementResult.SnapshotTyped,
            requirementResult.snapshot_text
        );

        InstallDuplicateBattleItemInstances(runner);
        PrimeActiveUnitAp(runner, 4);
        GameTextCommandResult duplicateItemOnlyResult = RunCommandExpectFail(
            runner,
            $"battle equip necklace {DuplicateTestCharmId}"
        );
        AssertBattleDuplicateItemOnlyRequiresInstance(duplicateItemOnlyResult);

        PrimeActiveUnitAp(runner, 4);
        GameTextCommandResult duplicateExplicitResult = RunCommand(
            runner,
            $"battle equip necklace {DuplicateTestCharmId} instance_id={DuplicateTestCharmRareInstanceId}"
        );
        AssertBattleDuplicateExplicitEquip(duplicateExplicitResult.SnapshotTyped);

        PrimeActiveUnitAp(runner, 4);
        GameTextCommandResult duplicateUnequipResult = RunCommand(
            runner,
            $"battle unequip necklace instance_id={DuplicateTestCharmRareInstanceId}"
        );
        AssertBattleDuplicateUnequipRoundTrip(duplicateUnequipResult.SnapshotTyped, runner);

        PrimeActiveUnitAp(runner, 4);
        GameTextCommandResult equipResult = RunCommand(runner, "battle equip head leather_cap");
        AssertSuccessfulBattleEquip(equipResult.SnapshotTyped, equipResult.snapshot_text);

        PrimeActiveUnitAp(runner, 1);
        GameTextCommandResult apResult = RunCommandExpectFail(
            runner,
            "battle equip main_hand bronze_sword"
        );
        AssertBattleEquipApFailure(apResult.SnapshotTyped, apResult.snapshot_text);

        PrimeActiveUnitAp(runner, 2);
        string otherUnitId = FindOtherBattleUnitId(
            runner.GetSession().BuildSnapshotPlain()
        );
        GameTextCommandResult targetResult = RunCommandExpectFail(
            runner,
            $"battle equip main_hand bronze_sword target_unit_id={otherUnitId}"
        );
        AssertBattleEquipSelfOnlyFailure(targetResult.SnapshotTyped, targetResult.snapshot_text);

        RunCommand(runner, "warehouse capacity 1");
        PrimeActiveUnitAp(runner, 2);
        GameTextCommandResult fullResult = RunCommandExpectFail(runner, "battle unequip head");
        AssertBattleUnequipBackpackFullFailure(
            fullResult.SnapshotTyped,
            fullResult.snapshot_text
        );

        RunCommand(runner, "warehouse capacity 10");
        PrimeActiveUnitAp(runner, 6);
        GameTextCommandResult twoHandedResult = RunCommand(
            runner,
            "battle equip main_hand iron_greatsword"
        );
        AssertBattleTwoHandedWeaponLinkage(twoHandedResult.SnapshotTyped);

        PrimeActiveUnitAp(runner, 6);
        GameTextCommandResult versatileResult = RunCommand(
            runner,
            $"battle equip main_hand {VersatileTestWeaponId}"
        );
        AssertBattleVersatileFreeOffhandLinkage(versatileResult.SnapshotTyped);

        PrimeActiveUnitAp(runner, 6);
        GameTextCommandResult offhandResult = RunCommand(
            runner,
            $"battle equip off_hand {OffhandTestItemId}"
        );
        AssertBattleOffhandForcesVersatileOneHanded(offhandResult.SnapshotTyped);
        AssertPartyEquipmentNotUpdatedDuringBattle(offhandResult.SnapshotTyped, activeMemberId);

        PrimeActiveUnitAp(runner, 4);
        GameTextCommandResult bodyArmorEquipResult = RunCommand(
            runner,
            "battle equip body leather_jerkin"
        );
        AssertBattleBodyArmorEquippedWithoutHpBonus(bodyArmorEquipResult.SnapshotTyped);
        IReadOnlyDictionary<string, object> bodyArmorEquipReport =
            FindLatestChangeEquipmentReport(
            bodyArmorEquipResult.SnapshotTyped
        );
        PrimeActiveUnitHpAndAp(runner, DictInt(bodyArmorEquipReport, "hp_max_after"), 2);
        GameTextCommandResult bodyArmorUnequipResult = RunCommand(runner, "battle unequip body");
        AssertBattleUnequipBodyArmorTurnEndWithoutHpClamp(
            bodyArmorUnequipResult.SnapshotTyped,
            bodyArmorUnequipResult.snapshot_text
        );

        GameTextCommandResult finishResult = RunCommand(runner, "battle finish player");
        AssertPartyEquipmentWrittenBackAfterBattle(finishResult.SnapshotTyped, activeMemberId);

        runner.Dispose(true);
        RequestTestExit(_test.Finish("Battle equipment text command regression"));
    }

    private void AdvanceToManualBattleTurn(GameTextCommandRunner runner, int maxTicks = 64)
    {
        for (int index = 0; index < maxTicks; index++)
        {
            IReadOnlyDictionary<string, object> snapshot =
                runner.GetSession().BuildSnapshotPlain();
            IReadOnlyDictionary<string, object> battleSnapshot = Dict(snapshot, "battle");
            bool battleActive = DictBool(battleSnapshot, "active", false);
            string activeUnitId = DictString(battleSnapshot, "active_unit_id");
            IReadOnlyDictionary<string, object> activeUnit = FindBattleUnit(
                battleSnapshot,
                activeUnitId
            );
            bool manualTurn = DictString(activeUnit, "control_mode") == "manual";
            if (!battleActive)
                break;
            if (manualTurn)
                return;
            RunCommand(runner, "battle tick 1");
        }
        _test.Fail("战斗换装文本回归未能进入手动单位回合。");
    }

    private void PrimeActiveUnitAp(GameTextCommandRunner runner, int currentAp)
    {
        BattleUnitState activeUnit = GetActiveUnitState(runner);
        _test.True(activeUnit != null, "战斗换装文本回归前置：应存在当前行动单位。");
        if (activeUnit == null)
            return;
        activeUnit.current_ap = currentAp;
        activeUnit.attribute_snapshot?.SetValue("action_points", Math.Max(currentAp, 1));
    }

    private void PrimeActiveUnitHpAndAp(GameTextCommandRunner runner, int currentHp, int currentAp)
    {
        BattleUnitState activeUnit = GetActiveUnitState(runner);
        _test.True(activeUnit != null, "HP clamp 文本回归前置：应存在当前行动单位。");
        if (activeUnit == null)
            return;
        activeUnit.current_hp = currentHp;
        activeUnit.current_ap = currentAp;
    }

    private static BattleUnitState GetActiveUnitState(GameTextCommandRunner runner)
    {
        GameRuntimeFacade runtime = runner.GetSession().GetRuntimeFacade();
        BattleState battleState = runtime?.GetBattleState();
        if (battleState == null || battleState.active_unit_id == new StringName(""))
            return null;
        return battleState.ContainsUnit(battleState.active_unit_id)
            ? battleState.GetUnit(battleState.active_unit_id)
            : null;
    }

    private void InstallBattleEquipmentTestItems(GameTextCommandRunner runner)
    {
        GameSession gameSession = runner.GetSession().GetGameSession();
        _test.True(gameSession != null, "战斗换装回归前置：应存在 GameSession。");
        if (gameSession == null)
            return;
        _test.Eq(
            (Error)gameSession.InstallItemDefinitionForTests(
                BuildVersatileTestWeaponDef().ToDefinition()
            ),
            Error.Ok,
            "应能注册战斗换装测试用 versatile 武器。"
        );
        _test.Eq(
            (Error)gameSession.InstallItemDefinitionForTests(
                BuildOffhandTestItemDef().ToDefinition()
            ),
            Error.Ok,
            "应能注册战斗换装测试用副手物品。"
        );
        _test.Eq(
            (Error)gameSession.InstallItemDefinitionForTests(
                BuildRestrictedTestHelmDef().ToDefinition()
            ),
            Error.Ok,
            "应能注册战斗换装测试用受限头盔。"
        );
        _test.Eq(
            (Error)gameSession.InstallItemDefinitionForTests(
                BuildDuplicateTestCharmDef().ToDefinition()
            ),
            Error.Ok,
            "应能注册战斗换装测试用重复饰品。"
        );
    }

    private void InstallStringKeyOnlyBattleItemInstance(GameTextCommandRunner runner)
    {
        BattleState battleState = runner.GetSession().GetRuntimeFacade()?.GetBattleState();
        _test.True(battleState != null, "String-key-only 战斗换装回归前置：应存在战斗状态。");
        if (battleState == null)
            return;
        WarehouseState backpack = battleState.GetPartyBackpackView();
        _test.True(backpack != null, "String-key-only 战斗换装回归前置：应存在 battle-local 背包。");
        backpack?.AddEquipmentInstance(
            EquipmentInstanceState.CreateInstance(
                StringKeyOnlyTestHelmId,
                StringKeyOnlyTestHelmInstanceId
            )
        );
    }

    private void InstallDuplicateBattleItemInstances(GameTextCommandRunner runner)
    {
        BattleState battleState = runner.GetSession().GetRuntimeFacade()?.GetBattleState();
        _test.True(battleState != null, "重复实例战斗换装回归前置：应存在战斗状态。");
        if (battleState == null)
            return;
        WarehouseState backpack = battleState.GetPartyBackpackView();
        _test.True(backpack != null, "重复实例战斗换装回归前置：应存在 battle-local 背包。");
        if (backpack == null)
            return;

        EquipmentInstanceState commonInstance = EquipmentInstanceState.CreateInstance(
            DuplicateTestCharmId,
            DuplicateTestCharmCommonInstanceId
        );
        commonInstance.rarity = (int)EquipmentInstanceState.RarityTier.COMMON;
        commonInstance.current_durability = 10;
        EquipmentInstanceState rareInstance = EquipmentInstanceState.CreateInstance(
            DuplicateTestCharmId,
            DuplicateTestCharmRareInstanceId
        );
        rareInstance.rarity = (int)EquipmentInstanceState.RarityTier.RARE;
        rareInstance.current_durability = 24;
        backpack.AddEquipmentInstance(commonInstance);
        backpack.AddEquipmentInstance(rareInstance);
    }

    private static ItemDef BuildVersatileTestWeaponDef()
    {
        var itemDef = new ItemDef
        {
            item_id = VersatileTestWeaponId,
            display_name = "WPNDICE Versatile Longsword",
            is_stackable = false,
            item_category = "equipment",
            equipment_type_id = "weapon",
            equipment_slot_ids = new GStringArray { "main_hand" },
            tags = new GStringNameArray { "weapon", "melee", "versatile", "test" },
        };

        var profile = new WeaponProfileDef
        {
            weapon_type_id = "wpndice_longsword",
            damage_tag = "physical_slash",
            attack_range = 1,
            one_handed_dice = BuildWeaponDice(1, 8, 0),
            two_handed_dice = BuildWeaponDice(1, 10, 0),
            properties_mode = (int)WeaponProfileDef.PropertyMergeMode.REPLACE,
            properties = new GStringNameArray { "versatile" },
        };
        itemDef.weapon_profile = profile;
        return itemDef;
    }

    private static ItemDef BuildOffhandTestItemDef()
    {
        return new ItemDef
        {
            item_id = OffhandTestItemId,
            display_name = "WPNDICE Offhand Focus",
            is_stackable = false,
            item_category = "equipment",
            equipment_type_id = "accessory",
            equipment_slot_ids = new GStringArray { "off_hand" },
            tags = new GStringNameArray { "off_hand", "focus", "test" },
        };
    }

    private static ItemDef BuildDuplicateTestCharmDef()
    {
        return new ItemDef
        {
            item_id = DuplicateTestCharmId,
            display_name = "WPNDICE Duplicate Charm",
            is_stackable = false,
            item_category = "equipment",
            equipment_type_id = "accessory",
            equipment_slot_ids = new GStringArray { "necklace" },
            tags = new GStringNameArray { "accessory", "test" },
        };
    }

    private static ItemDef BuildRestrictedTestHelmDef()
    {
        return new ItemDef
        {
            item_id = RestrictedTestHelmId,
            display_name = "WPNDICE Restricted Helm",
            is_stackable = false,
            item_category = "equipment",
            equipment_type_id = "armor",
            equipment_slot_ids = new GStringArray { "head" },
            tags = new GStringNameArray { "head", "armor", "test" },
            equip_requirement = new EquipmentRequirement { min_body_size = 99 },
        };
    }

    private static WeaponDamageDiceDef BuildWeaponDice(int diceCount, int diceSides, int flatBonus)
    {
        return new WeaponDamageDiceDef
        {
            dice_count = diceCount,
            dice_sides = diceSides,
            flat_bonus = flatBonus,
        };
    }

    private void AssertSuccessfulBattleEquip(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(report.Count > 0, "成功换装后快照应包含 change_equipment report。");
        _test.True(DictBool(report, "ok", false), "成功换装 report 应标记 ok=true。");
        _test.Eq(DictString(report, "operation"), "equip", "成功换装 report 应记录 equip 操作。");
        _test.Eq(DictString(report, "slot_id"), "head", "成功换装 report 应记录 head 槽。");
        _test.Eq(DictString(report, "item_id"), "leather_cap", "成功换装 report 应记录装备物品。");
        _test.Eq(DictInt(report, "ap_before"), 4, "成功换装 report 应记录换装前 AP。");
        _test.Eq(DictInt(report, "ap_after"), 2, "成功换装 report 应记录换装后 AP。");
        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        IReadOnlyDictionary<string, object> equipped = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "head"
        );
        _test.Eq(DictString(equipped, "item_id"), "leather_cap", "成功换装后单位 battle-local 装备快照应显示 head 皮革护帽。");
        _test.Eq(CountBattleBackpackItem(snapshot, "leather_cap"), 0, "成功换装后 battle-local 背包中不应残留该装备。");
        _test.True(textSnapshot.Contains("report=change_equipment | ok=true"), "文本快照应渲染成功换装 report。");
        _test.True(textSnapshot.Contains("head:leather_cap"), "文本快照应渲染单位 battle-local 装备。");
        _test.True(textSnapshot.Contains("backpack_used_slots="), "文本快照应渲染 battle-local 背包摘要。");
    }

    private void AssertBattleEquipApFailure(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.False(DictBool(report, "ok", true), "AP 不足时 change_equipment report 应标记失败。");
        _test.Eq(DictString(report, "error_code"), "ap_insufficient", "AP 不足时 report 应暴露稳定错误码。");
        _test.Eq(DictInt(report, "ap_after", -1), 1, "AP 不足失败时不应扣 AP。");
        _test.Eq(CountBattleBackpackItem(snapshot, "bronze_sword"), 1, "AP 不足失败时装备实例应留在 battle-local 背包。");
        _test.True(textSnapshot.Contains("error=ap_insufficient"), "文本快照应渲染 AP 不足失败原因。");
    }

    private void AssertBattleEquipSelfOnlyFailure(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.False(DictBool(report, "ok", true), "指定其他目标时 change_equipment report 应标记失败。");
        _test.Eq(DictString(report, "error_code"), "target_not_self", "指定其他目标时应暴露 self-only 错误码。");
        _test.Eq(DictInt(report, "ap_after", -1), 2, "self-only 失败时不应扣 AP。");
        _test.Eq(CountBattleBackpackItem(snapshot, "bronze_sword"), 1, "self-only 失败时装备实例应留在 battle-local 背包。");
        _test.True(textSnapshot.Contains("只能为当前行动单位自己换装"), "文本快照应保留 self-only 失败文案。");
    }

    private void AssertBattleEquipRequirementFailure(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.False(DictBool(report, "ok", true), "装备需求失败时 change_equipment report 应标记失败。");
        _test.Eq(DictString(report, "error_code"), "item_not_equippable", "装备需求失败应只暴露泛化错误码。");
        _test.False(report.ContainsKey("blockers"), "装备需求失败不应在 report 中暴露隐藏 blocker。");
        _test.Eq(DictInt(report, "ap_after", -1), 2, "装备需求失败时不应扣 AP。");
        _test.Eq(CountBattleBackpackItem(snapshot, RestrictedTestHelmId.ToString()), 1, "装备需求失败时实例应留在 battle-local 背包。");
        IReadOnlyDictionary<string, object> hudEntry = FindBattleHudBackpackEntry(
            snapshot,
            RestrictedTestHelmId.ToString()
        );
        _test.True(hudEntry.Count > 0, "HUD 快照应包含需求受限装备。");
        _test.False(DictBool(hudEntry, "can_equip", true), "HUD 应把需求不满足的装备标记为不可装备。");
        _test.True(
            DictString(hudEntry, "disabled_reason").Contains("当前无法装备"),
            $"HUD 禁用原因应来自 runtime preview 的泛化文案。 entry={hudEntry}"
        );
        _test.True(textSnapshot.Contains("error=item_not_equippable"), "文本快照应渲染泛化装备失败原因。");
        _test.False(textSnapshot.Contains("body_size_too_small"), "文本快照不应泄露体型 blocker。");
        _test.False(textSnapshot.Contains("missing_profession"), "文本快照不应泄露职业 blocker。");
        _test.False(textSnapshot.Contains("体型过小"), "文本快照不应泄露体型要求。");
    }

    private void AssertBattleEquipStringKeyOnlyItemNotFound(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.False(DictBool(report, "ok", true), "String-key-only 装备定义不应允许战斗换装。");
        _test.Eq(DictString(report, "error_code"), "item_not_found", "String-key-only 装备定义不应被恢复为正式 item_def。");
        _test.Eq(DictInt(report, "ap_after", -1), 2, "String-key-only 装备定义失败时不应扣 AP。");
        _test.Eq(
            CountBattleBackpackItem(snapshot, StringKeyOnlyTestHelmId.ToString()),
            1,
            "String-key-only 装备定义失败后实例应留在 battle-local 背包。"
        );
        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        _test.True(FindEquippedEntry(ArrayValue(unit, "equipment"), "head").Count == 0, "String-key-only 装备定义失败后 head 槽应保持清空。");
        _test.True(textSnapshot.Contains("error=item_not_found"), "文本快照应渲染 String-key-only 装备定义缺失原因。");
    }

    private void AssertBattleDuplicateItemOnlyRequiresInstance(GameTextCommandResult result)
    {
        _test.True(result.message.Contains("请指定 instance_id"), $"同 item_id 多 battle-local 实例的 headless 便利命令应要求 instance_id。 message={result.message}");
        _test.Eq(CountBattleBackpackItem(result.SnapshotTyped, DuplicateTestCharmId.ToString()), 2, "item_id-only 失败后两个重复实例都应留在 battle-local 背包。");
    }

    private void AssertBattleDuplicateExplicitEquip(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "指定 duplicate rare instance_id 的战斗装备应成功。");
        _test.Eq(DictString(report, "item_id"), DuplicateTestCharmId.ToString(), "duplicate explicit equip report 应记录测试饰品。");
        _test.Eq(DictString(report, "instance_id"), DuplicateTestCharmRareInstanceId.ToString(), "duplicate explicit equip report 应记录指定实例。");
        _test.Eq(CountBattleBackpackItem(snapshot, DuplicateTestCharmId.ToString()), 1, "装备指定实例后 battle-local 背包中应只剩另一个重复实例。");
        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        IReadOnlyDictionary<string, object> equipped = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "necklace"
        );
        _test.Eq(DictString(equipped, "instance_id"), DuplicateTestCharmRareInstanceId.ToString(), "battle-local 饰品槽应写入指定 rare instance_id。");
    }

    private void AssertBattleDuplicateUnequipRoundTrip(
        IReadOnlyDictionary<string, object> snapshot,
        GameTextCommandRunner runner
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "指定 duplicate rare instance_id 的战斗卸装应成功。");
        _test.Eq(DictString(report, "operation"), "unequip", "duplicate round-trip report 应来自卸装。");
        _test.Eq(DictString(report, "instance_id"), DuplicateTestCharmRareInstanceId.ToString(), "duplicate unequip report 应记录指定实例。");
        _test.Eq(CountBattleBackpackItem(snapshot, DuplicateTestCharmId.ToString()), 2, "卸装后两个重复实例都应回到 battle-local 背包。");
        BattleState battleState = runner.GetSession().GetRuntimeFacade()?.GetBattleState();
        EquipmentInstanceState returnedInstance = FindBattleBackpackInstance(
            battleState?.GetPartyBackpackView(),
            DuplicateTestCharmRareInstanceId
        );
        _test.True(returnedInstance != null, "卸装 round-trip 后应能在 battle-local 背包找到 rare 实例。");
        if (returnedInstance != null)
        {
            _test.Eq(returnedInstance.rarity, (int)EquipmentInstanceState.RarityTier.RARE, "卸装 round-trip 后 rare 品质应保留。");
            _test.Eq(returnedInstance.current_durability, 24, "卸装 round-trip 后 rare 耐久应保留。");
        }
    }

    private void AssertBattleUnequipBackpackFullFailure(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.False(DictBool(report, "ok", true), "背包满卸装时 change_equipment report 应标记失败。");
        _test.Eq(DictString(report, "error_code"), "backpack_capacity_exceeded", "背包满卸装应暴露容量错误码。");
        _test.Eq(DictInt(report, "ap_after", -1), 2, "背包满卸装失败时不应扣 AP。");
        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        IReadOnlyDictionary<string, object> equipped = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "head"
        );
        _test.Eq(DictString(equipped, "item_id"), "leather_cap", "背包满卸装失败后 head 装备应保持不变。");
        _test.Eq(CountBattleBackpackItem(snapshot, "leather_cap"), 0, "背包满卸装失败后装备不应进入 battle-local 背包。");
        _test.True(textSnapshot.Contains("error=backpack_capacity_exceeded"), "文本快照应渲染背包满失败原因。");
    }

    private void AssertBattleTwoHandedWeaponLinkage(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "双手武器战斗换装应成功。");
        _test.Eq(DictString(report, "operation"), "equip", "双手武器 report 应来自装备操作。");
        _test.Eq(DictString(report, "slot_id"), "main_hand", "双手武器入口槽应为 main_hand。");
        _test.Eq(DictString(report, "item_id"), "iron_greatsword", "双手武器 report 应记录铁制大剑。");
        _test.Eq(DictString(report, "weapon_item_id"), "iron_greatsword", "双手武器投影应使用铁制大剑。");
        _test.Eq(DictString(report, "weapon_current_grip"), "two_handed", "双手武器投影应切到 two_handed grip。");
        _test.True(DictBool(report, "weapon_uses_two_hands", false), "双手武器投影应标记占用双手。");

        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        IReadOnlyDictionary<string, object> mainEntry = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "main_hand"
        );
        _test.Eq(DictString(mainEntry, "item_id"), "iron_greatsword", "双手武器装备后 battle-local 主手应显示铁制大剑。");
        _test.True(ArrayHasString(mainEntry.ContainsKey("occupied_slot_ids") ? mainEntry["occupied_slot_ids"] : default, "off_hand"), "双手武器 battle-local 装备 entry 应联动占用副手。");
    }

    private void AssertBattleVersatileFreeOffhandLinkage(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "versatile 武器战斗换装应成功。");
        _test.Eq(DictString(report, "item_id"), VersatileTestWeaponId.ToString(), "versatile report 应记录测试武器。");
        _test.Eq(DictString(report, "weapon_item_id"), VersatileTestWeaponId.ToString(), "versatile 投影应使用测试武器。");
        _test.Eq(DictString(report, "weapon_current_grip"), "two_handed", "副手空闲时 versatile 应自动使用双手握法。");
        _test.True(DictBool(report, "weapon_uses_two_hands", false), "副手空闲时 versatile 应标记双手使用。");

        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        IReadOnlyDictionary<string, object> mainEntry = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "main_hand"
        );
        IReadOnlyDictionary<string, object> offhandEntry = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "off_hand"
        );
        _test.Eq(DictString(mainEntry, "item_id"), VersatileTestWeaponId.ToString(), "versatile 装备后 battle-local 主手应显示测试武器。");
        _test.True(offhandEntry.Count == 0, "副手空闲时 versatile 不应写入独立 off_hand 装备 entry。");
    }

    private void AssertBattleOffhandForcesVersatileOneHanded(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "副手装备战斗换装应成功。");
        _test.Eq(DictString(report, "slot_id"), "off_hand", "副手装备 report 应记录 off_hand 槽。");
        _test.Eq(DictString(report, "item_id"), OffhandTestItemId.ToString(), "副手装备 report 应记录测试副手物品。");
        _test.Eq(DictString(report, "weapon_item_id"), VersatileTestWeaponId.ToString(), "装备副手后主手武器投影仍应来自 versatile 武器。");
        _test.Eq(DictString(report, "weapon_current_grip"), "one_handed", "副手被占用时 versatile 应自动回到单手握法。");
        _test.False(DictBool(report, "weapon_uses_two_hands", true), "副手被占用时 versatile 不应继续标记双手使用。");

        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        IReadOnlyDictionary<string, object> mainEntry = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "main_hand"
        );
        IReadOnlyDictionary<string, object> offhandEntry = FindEquippedEntry(
            ArrayValue(unit, "equipment"),
            "off_hand"
        );
        _test.Eq(DictString(mainEntry, "item_id"), VersatileTestWeaponId.ToString(), "副手装备后 battle-local 主手应保留 versatile 武器。");
        _test.Eq(DictString(offhandEntry, "item_id"), OffhandTestItemId.ToString(), "副手装备后 battle-local 副手应显示测试副手物品。");
    }

    private void AssertPartyEquipmentNotUpdatedDuringBattle(
        IReadOnlyDictionary<string, object> snapshot,
        string memberId
    )
    {
        IReadOnlyDictionary<string, object> member = FindPartyMember(
            ArrayValue(Dict(snapshot, "party"), "members"),
            memberId
        );
        _test.True(member.Count > 0, "战斗中 party 快照应包含当前行动成员。");
        IReadOnlyDictionary<string, object> partyMain = FindEquippedEntry(
            ArrayValue(member, "equipment"),
            "main_hand"
        );
        IReadOnlyDictionary<string, object> partyOffhand = FindEquippedEntry(
            ArrayValue(member, "equipment"),
            "off_hand"
        );
        _test.True(DictString(partyMain, "item_id") != VersatileTestWeaponId.ToString(), "战斗中换装不应直接写入 party 主手。");
        _test.True(DictString(partyOffhand, "item_id") != OffhandTestItemId.ToString(), "战斗中换装不应直接写入 party 副手。");

        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            Dict(snapshot, "battle"),
            DictString(report, "unit_id")
        );
        _test.Eq(DictString(FindEquippedEntry(ArrayValue(unit, "equipment"), "main_hand"), "item_id"), VersatileTestWeaponId.ToString(), "同一快照中 battle-local 主手应已经换成 versatile 武器。");
        _test.Eq(DictString(FindEquippedEntry(ArrayValue(unit, "equipment"), "off_hand"), "item_id"), OffhandTestItemId.ToString(), "同一快照中 battle-local 副手应已经换成测试副手物品。");
    }

    private void AssertPartyEquipmentWrittenBackAfterBattle(
        IReadOnlyDictionary<string, object> snapshot,
        string memberId
    )
    {
        _test.False(DictBool(Dict(snapshot, "battle"), "active", false), "战斗结算后 battle 应退出 active 状态。");
        IReadOnlyDictionary<string, object> member = FindPartyMember(
            ArrayValue(Dict(snapshot, "party"), "members"),
            memberId
        );
        _test.True(member.Count > 0, "战后 party 快照应包含当前行动成员。");
        IReadOnlyDictionary<string, object> mainEntry = FindEquippedEntry(
            ArrayValue(member, "equipment"),
            "main_hand"
        );
        IReadOnlyDictionary<string, object> offhandEntry = FindEquippedEntry(
            ArrayValue(member, "equipment"),
            "off_hand"
        );
        IReadOnlyDictionary<string, object> headEntry = FindEquippedEntry(
            ArrayValue(member, "equipment"),
            "head"
        );
        IReadOnlyDictionary<string, object> bodyEntry = FindEquippedEntry(
            ArrayValue(member, "equipment"),
            "body"
        );
        _test.Eq(DictString(mainEntry, "item_id"), VersatileTestWeaponId.ToString(), "战后 party 主手才应写回 battle-local versatile 武器。");
        _test.Eq(DictString(offhandEntry, "item_id"), OffhandTestItemId.ToString(), "战后 party 副手才应写回 battle-local 副手物品。");
        _test.Eq(DictString(headEntry, "item_id"), "leather_cap", "战后 party 头部应写回 battle-local 皮革护帽。");
        _test.True(bodyEntry.Count == 0, "卸装后战后 party body 槽应保持清空。");
    }

    private void AssertBattleBodyArmorEquippedWithoutHpBonus(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "身体护甲换装应成功。");
        _test.Eq(DictString(report, "item_id"), "leather_jerkin", "身体护甲换装 report 应记录皮革短甲。");
        _test.Eq(DictInt(report, "hp_max_after"), DictInt(report, "hp_max_before", -1), "皮革短甲不应提高 HP 上限。");
    }

    private void AssertBattleUnequipBodyArmorTurnEndWithoutHpClamp(
        IReadOnlyDictionary<string, object> snapshot,
        string textSnapshot
    )
    {
        IReadOnlyDictionary<string, object> battleSnapshot = Dict(snapshot, "battle");
        IReadOnlyDictionary<string, object> report = FindLatestChangeEquipmentReport(snapshot);
        _test.True(DictBool(report, "ok", false), "身体护甲卸装应成功。");
        _test.Eq(DictString(report, "operation"), "unequip", "卸装 report 应来自卸装。");
        _test.False(DictBool(report, "hp_clamped", false), "卸下无生命加成护甲时不应标记 hp_clamped。");
        _test.Eq(DictInt(report, "hp_before"), DictInt(report, "hp_after", -1), "卸下无生命加成护甲时当前 HP 不应变化。");
        _test.Eq(DictString(battleSnapshot, "phase"), "timeline_running", "AP 归零后战斗阶段应回到 timeline_running。");
        _test.Eq(DictString(battleSnapshot, "active_unit_id"), "", "AP 归零后应清空 active_unit_id。");
        IReadOnlyDictionary<string, object> unit = FindBattleUnit(
            battleSnapshot,
            DictString(report, "unit_id")
        );
        _test.True(FindEquippedEntry(ArrayValue(unit, "equipment"), "body").Count == 0, "身体护甲卸装后 body 槽应清空。");
        _test.Eq(CountBattleBackpackItem(snapshot, "leather_jerkin"), 1, "身体护甲卸装后应回到 battle-local 背包。");
        _test.False(textSnapshot.Contains("hp_clamped=true"), "文本快照不应渲染不存在的 HP clamp。");
        _test.True(textSnapshot.Contains("active_unit_id="), "文本快照应渲染行动结束后的 active_unit_id。");
    }

    private static IReadOnlyDictionary<string, object> FindLatestChangeEquipmentReport(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyList<object> reports = ArrayValue(Dict(snapshot, "battle"), "report_entries");
        for (int index = reports.Count - 1; index >= 0; index--)
        {
            if (reports[index] is not IReadOnlyDictionary<string, object> report)
                continue;
            if (DictString(report, "type") == "change_equipment")
                return report;
        }
        return new Dictionary<string, object>();
    }

    private static IReadOnlyDictionary<string, object> FindBattleUnit(
        IReadOnlyDictionary<string, object> battleSnapshot,
        string unitId
    )
    {
        foreach (object unitValue in ArrayValue(battleSnapshot, "units"))
        {
            if (unitValue is not IReadOnlyDictionary<string, object> unit)
                continue;
            if (DictString(unit, "unit_id") == unitId)
                return unit;
        }
        return new Dictionary<string, object>();
    }

    private static IReadOnlyDictionary<string, object> FindBattleHudBackpackEntry(
        IReadOnlyDictionary<string, object> snapshot,
        string itemId
    )
    {
        IReadOnlyDictionary<string, object> equipmentPanel = Dict(
            Dict(Dict(snapshot, "battle"), "hud"),
            "equipment_panel"
        );
        foreach (object entryValue in ArrayValue(equipmentPanel, "backpack_entries"))
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (DictString(entry, "item_id") == itemId)
                return entry;
        }
        return new Dictionary<string, object>();
    }

    private static string FindOtherBattleUnitId(
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        IReadOnlyDictionary<string, object> battleSnapshot = Dict(snapshot, "battle");
        string activeUnitId = DictString(battleSnapshot, "active_unit_id");
        foreach (object unitValue in ArrayValue(battleSnapshot, "units"))
        {
            if (unitValue is not IReadOnlyDictionary<string, object> unit)
                continue;
            string unitId = DictString(unit, "unit_id");
            if (!string.IsNullOrEmpty(unitId) && unitId != activeUnitId)
                return unitId;
        }
        return "not_current_unit";
    }

    private static IReadOnlyDictionary<string, object> FindPartyMember(
        IReadOnlyList<object> entries,
        string memberId
    )
    {
        foreach (object entryValue in entries)
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (DictString(entry, "member_id") == memberId)
                return entry;
        }
        return new Dictionary<string, object>();
    }

    private static IReadOnlyDictionary<string, object> FindEquippedEntry(
        IReadOnlyList<object> entries,
        string slotId
    )
    {
        foreach (object entryValue in entries)
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (DictString(entry, "slot_id") == slotId)
                return entry;
        }
        return new Dictionary<string, object>();
    }

    private static bool ArrayHasString(object value, string expected)
    {
        if (value is not IReadOnlyList<object> values)
            return false;
        foreach (object entry in values)
        {
            if (
                entry is string stringValue && stringValue == expected
                || entry is StringName stringNameValue && stringNameValue.ToString() == expected
            )
                return true;
        }
        return false;
    }

    private static int CountBattleBackpackItem(
        IReadOnlyDictionary<string, object> snapshot,
        string itemId
    )
    {
        IReadOnlyDictionary<string, object> backpack = Dict(
            Dict(snapshot, "battle"),
            "party_backpack"
        );
        int total = 0;
        foreach (object stackValue in ArrayValue(backpack, "stacks"))
        {
            if (stackValue is not IReadOnlyDictionary<string, object> stack)
                continue;
            if (DictString(stack, "item_id") == itemId)
                total += DictInt(stack, "quantity");
        }
        foreach (object instanceValue in ArrayValue(backpack, "equipment_instances"))
        {
            if (instanceValue is not IReadOnlyDictionary<string, object> instance)
                continue;
            if (DictString(instance, "item_id") == itemId)
                total += 1;
        }
        return total;
    }

    private static EquipmentInstanceState FindBattleBackpackInstance(
        WarehouseState backpackView,
        StringName instanceId
    )
    {
        if (backpackView == null)
            return null;
        foreach (EquipmentInstanceState instance in backpackView.equipment_instances)
        {
            if (instance != null && instance.instance_id == instanceId)
                return instance;
        }
        return null;
    }

    private GameTextCommandResult RunCommand(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return result;
        if (!result.ok)
        {
            GD.Print(result.Render());
            _test.Fail($"命令失败：{commandText} | {result.message}");
        }
        return result;
    }

    private GameTextCommandResult RunCommandExpectFail(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
        {
            _test.Fail($"命令被跳过，无法验证失败：{commandText}");
            return result;
        }
        if (result.ok)
        {
            GD.Print(result.Render());
            _test.Fail($"命令应失败但成功：{commandText}");
        }
        return result;
    }

    private static IReadOnlyList<object> ArrayValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is IReadOnlyList<object> array
                ? array
                : System.Array.Empty<object>();
    }

    private static IReadOnlyDictionary<string, object> Dict(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is IReadOnlyDictionary<string, object> nested
                ? nested
                : new Dictionary<string, object>();
    }

    private static bool DictBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        bool fallback
    )
    {
        return dictionary != null && dictionary.TryGetValue(key, out object value)
            ? value is bool boolValue
                ? boolValue
                : fallback
            : fallback;
    }

    private static int DictInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback = 0
    )
    {
        return dictionary != null && dictionary.TryGetValue(key, out object value)
            ? value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                _ => fallback,
            }
            : fallback;
    }

    private static string DictString(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null && dictionary.TryGetValue(key, out object value)
            ? value switch
            {
                string stringValue => stringValue,
                StringName stringNameValue => stringNameValue.ToString(),
                _ => "",
            }
            : "";
    }
}
