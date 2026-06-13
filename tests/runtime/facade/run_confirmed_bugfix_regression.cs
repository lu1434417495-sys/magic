using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_confirmed_bugfix_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedStatusFieldsReplaceLegacyStatusBoolHelpers();
        TestAttackDispositionRespectsNaturalRollFlags();
        TestWorldFootprintReRegisterClearsOldCells();
        TestMissingItemDefDoesNotTrapEquippedInstance();

        Quit(_test.Finish("Confirmed bugfix regression"));
    }

    private void TestTypedStatusFieldsReplaceLegacyStatusBoolHelpers()
    {
        BattleDamageResolver damageResolver = new();
        BattleUnitState source = BuildUnit("incoming_multiplier_source");
        BattleUnitState legacyTarget = BuildUnit("incoming_multiplier_legacy_target");
        SetStatusParams(
            legacyTarget,
            "test_incoming_multiplier",
            new GDictionary { [new StringName("incoming_damage_multiplier")] = 1.5 }
        );
        GDictionary legacyDamageResult = damageResolver.ResolveEffects(
            source,
            legacyTarget,
            new GArray { BuildDamageEffect(10) },
            new GDictionary()
        );
        _test.Eq(
            DictInt(legacyDamageResult, "damage", -1),
            10,
            "legacy incoming_damage_multiplier params 不应继续驱动正式伤害倍率。"
        );

        BattleUnitState formalTarget = BuildUnit("incoming_multiplier_formal_target");
        SetTypedStatus(
            formalTarget,
            "test_incoming_multiplier",
            incomingDamageMultiplier: 1.5
        );
        GDictionary formalDamageResult = damageResolver.ResolveEffects(
            source,
            formalTarget,
            new GArray { BuildDamageEffect(10) },
            new GDictionary()
        );
        _test.Eq(
            DictInt(formalDamageResult, "damage", -1),
            15,
            "typed incoming_damage_multiplier 字段必须驱动正式伤害倍率。"
        );

        _test.False(
            typeof(BattleHitResolver).GetMethod(
                "_get_status_param_bool",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            ) != null,
            "BattleHitResolver 不应继续保留 legacy status bool helper。"
        );

        BattleUnitState unit = BuildUnit("test_crit_lock_unit");
        SetTypedStatus(unit, "test_crit_lock", lockCrit: true);
        _test.True(
            BattleFateAttackRules.IsAttackCritLocked(unit),
            "命运攻击规则应读取 typed lock_crit 状态字段。"
        );
    }

    private void TestAttackDispositionRespectsNaturalRollFlags()
    {
        BattleHitResolver hitResolver = new();
        AttackCheckInput forcedHitCheck = new(
            requiredRoll: 1,
            naturalOneAutoMiss: false,
            naturalTwentyAutoHit: false
        );
        StringName disposition = hitResolver.ResolveAttackRollDispositionForCheck(
            1,
            forcedHitCheck
        );
        _test.Eq(
            disposition,
            new StringName("threshold_hit"),
            "关闭 natural_one_auto_miss 后，d20=1 且 required_roll=1 应按普通命中处理。"
        );
    }

    private void TestWorldFootprintReRegisterClearsOldCells()
    {
        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(new Vector2I(3, 3), Vector2I.One);

        _test.True(
            gridSystem.RegisterFootprint("camp", new Vector2I(0, 0), new Vector2I(2, 1)),
            "初次注册 footprint 应成功。"
        );
        _test.True(
            gridSystem.RegisterFootprint("camp", new Vector2I(1, 1), Vector2I.One),
            "同 entity_id 重新注册 footprint 应成功。"
        );
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(0, 0)), "", "重新注册后旧 footprint 占用应被清理。");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "camp", "重新注册后新 footprint 应可读取。");
        _test.False(
            gridSystem.RegisterFootprint("camp", new Vector2I(9, 9), Vector2I.One),
            "越界重新注册应失败。"
        );
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "camp", "越界注册失败后旧 footprint 应被恢复。");
    }

    private void TestMissingItemDefDoesNotTrapEquippedInstance()
    {
        PartyState partyState = new();
        PartyMemberState memberState = new()
        {
            member_id = "member_a",
        };
        memberState.progression.unit_base_attributes.SetAttributeValue("storage_space", 1);
        partyState.SetMemberState(memberState);

        GStringNameArray occupiedSlots = new() { "main_hand" };
        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "missing_sword",
            "eq_missing_sword"
        );
        _test.True(
            memberState.equipment_state.SetEquippedEntry(
                "main_hand",
                "missing_sword",
                occupiedSlots,
                instance
            ),
            "卸装回归前置：应能写入缺定义装备实例。"
        );

        PartyEquipmentService equipmentService = new();
        equipmentService.Setup(partyState, new Dictionary<StringName, ItemDef>());
        var result = equipmentService.UnequipItemTyped("member_a", "main_hand");
        _test.True(result.Success, "缺失 item_def 的已装备实例仍应可卸下。");
        _test.Eq(
            memberState.equipment_state.GetEquippedItemId("main_hand"),
            new StringName(""),
            "卸下后装备槽应为空。"
        );
        _test.Eq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped().Count,
            1,
            "卸下的坏配置装备实例应回到仓库，不能丢失。"
        );
    }

    private static void SetStatusParams(
        BattleUnitState unit,
        StringName statusId,
        GDictionary @params
    )
    {
        BattleStatusEffectState statusEffect = new()
        {
            status_id = statusId,
            source_unit_id = "test_source",
            power = 1,
            @params = @params != null ? (GDictionary)@params.Duplicate(true) : new GDictionary(),
            stacks = 1,
        };
        unit.SetStatusEffect(statusEffect);
    }

    private static void SetTypedStatus(
        BattleUnitState unit,
        StringName statusId,
        bool lockCrit = false,
        double? incomingDamageMultiplier = null
    )
    {
        BattleStatusEffectState statusEffect = new()
        {
            status_id = statusId,
            source_unit_id = "test_source",
            power = 1,
            stacks = 1,
            lock_crit = lockCrit,
            incoming_damage_multiplier = incomingDamageMultiplier,
        };
        unit.SetStatusEffect(statusEffect);
    }

    private static CombatEffectDef BuildDamageEffect(int power)
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            power = power,
            damage_tag = "physical_slash",
            @params = new GDictionary(),
        };
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = 2,
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            current_hp = 100,
            current_mp = 4,
            current_stamina = 4,
            current_aura = 0,
            is_alive = true,
        };
        unit.SetAnchorCoord(Vector2I.Zero);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 4);
        unit.attribute_snapshot.SetValue("action_points", 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus), 0);
        return unit;
    }

    private static int DictInt(GDictionary dictionary, string key, int defaultValue)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return defaultValue;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : defaultValue;
    }

    private static bool DictBool(GDictionary dictionary, string key, bool defaultValue)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return defaultValue;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : defaultValue;
    }
}
