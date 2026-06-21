using System.Collections.Generic;
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
        TestAttackDispositionRespectsNaturalRollFlags();
        TestWorldFootprintReRegisterClearsOldCells();
        TestMissingItemDefDoesNotTrapEquippedInstance();

        Quit(_test.Finish("Confirmed bugfix regression"));
    }

    private void TestAttackDispositionRespectsNaturalRollFlags()
    {
        using var hitResolver = new BattleHitResolver();
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
        PartyEquipmentService equipmentService = new();
        try
        {
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
            bool equipped = memberState.equipment_state.SetEquippedEntry(
                "main_hand",
                "missing_sword",
                occupiedSlots,
                instance
            );
            if (!equipped)
                GodotRefCountedDisposer.DisposeIfValid(instance);
            _test.True(
                equipped,
                "卸装回归前置：应能写入缺定义装备实例。"
            );

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
        finally
        {
            equipmentService.Dispose();
            GodotRefCountedDisposer.DisposeIfValid(partyState);
        }
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
