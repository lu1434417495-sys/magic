using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_confirmed_bugfix_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestStatusParamLookupAcceptsStringNameKeys();
        TestAttackDispositionRespectsNaturalRollFlags();
        TestWorldFootprintReRegisterClearsOldCells();
        TestMissingItemDefDoesNotTrapEquippedInstance();

        if (_failures.Count == 0)
        {
            GD.Print("Confirmed bugfix regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Confirmed bugfix regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestStatusParamLookupAcceptsStringNameKeys()
    {
        BattleDamageResolver damageResolver = new();
        BattleUnitState source = BuildUnit("incoming_multiplier_source");
        BattleUnitState target = BuildUnit("incoming_multiplier_target");
        SetStatusParams(
            target,
            "test_incoming_multiplier",
            new GDictionary { [new StringName("incoming_damage_multiplier")] = 1.5 }
        );
        GDictionary damageResult = damageResolver.resolve_effects(
            source,
            target,
            new GArray { BuildDamageEffect(10) },
            new GDictionary()
        );
        AssertEq(
            DictInt(damageResult, "damage", -1),
            15,
            "伤害解析器应能读取 StringName key 的状态参数。"
        );

        BattleHitResolver hitResolver = new();
        GDictionary hitParams = new() { [new StringName("lock_crit")] = true };
        AssertTrue(
            hitResolver._get_status_param_bool(hitParams, "lock_crit", false),
            "命中解析器应能读取 StringName key 的状态参数。"
        );

        BattleFateAttackRules fateRules = new();
        BattleUnitState unit = BuildUnit("test_crit_lock_unit");
        SetStatusParams(
            unit,
            "test_crit_lock",
            new GDictionary { [new StringName("lock_crit")] = true }
        );
        AssertTrue(
            fateRules.is_attack_crit_locked(unit),
            "命运攻击规则应能读取 StringName key 的状态参数。"
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
        StringName disposition = hitResolver._resolve_attack_roll_disposition_for_check(
            1,
            forcedHitCheck
        );
        AssertEq(
            disposition,
            new StringName("threshold_hit"),
            "关闭 natural_one_auto_miss 后，d20=1 且 required_roll=1 应按普通命中处理。"
        );
    }

    private void TestWorldFootprintReRegisterClearsOldCells()
    {
        WorldMapGridSystem gridSystem = new();
        gridSystem.setup(new Vector2I(3, 3), Vector2I.One);

        AssertTrue(
            gridSystem.register_footprint("camp", new Vector2I(0, 0), new Vector2I(2, 1)),
            "初次注册 footprint 应成功。"
        );
        AssertTrue(
            gridSystem.register_footprint("camp", new Vector2I(1, 1), Vector2I.One),
            "同 entity_id 重新注册 footprint 应成功。"
        );
        AssertEq(gridSystem.get_occupant_root(new Vector2I(0, 0)), "", "重新注册后旧 footprint 占用应被清理。");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "camp", "重新注册后新 footprint 应可读取。");
        AssertFalse(
            gridSystem.register_footprint("camp", new Vector2I(9, 9), Vector2I.One),
            "越界重新注册应失败。"
        );
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "camp", "越界注册失败后旧 footprint 应被恢复。");
    }

    private void TestMissingItemDefDoesNotTrapEquippedInstance()
    {
        PartyState partyState = new();
        PartyMemberState memberState = new()
        {
            member_id = "member_a",
        };
        memberState.progression.unit_base_attributes.set_attribute_value("storage_space", 1);
        partyState.set_member_state(memberState);

        GStringNameArray occupiedSlots = new() { "main_hand" };
        EquipmentInstanceState instance = EquipmentInstanceState.create_instance(
            "missing_sword",
            "eq_missing_sword"
        );
        AssertTrue(
            memberState.equipment_state.set_equipped_entry(
                "main_hand",
                "missing_sword",
                occupiedSlots,
                instance
            ),
            "卸装回归前置：应能写入缺定义装备实例。"
        );

        PartyEquipmentService equipmentService = new();
        equipmentService.setup(partyState, new GDictionary());
        GDictionary result = equipmentService.unequip_item("member_a", "main_hand");
        AssertTrue(DictBool(result, "success", false), "缺失 item_def 的已装备实例仍应可卸下。");
        AssertEq(
            memberState.equipment_state.get_equipped_item_id("main_hand"),
            new StringName(""),
            "卸下后装备槽应为空。"
        );
        AssertEq(
            partyState.warehouse_state.get_non_empty_instances().Count,
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
            @params = @params?.Duplicate(true).AsGodotDictionary() ?? new GDictionary(),
            stacks = 1,
        };
        unit.set_status_effect(statusEffect);
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
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            current_hp = 100,
            current_mp = 4,
            current_stamina = 4,
            current_aura = 0,
            is_alive = true,
        };
        unit.set_anchor_coord(Vector2I.Zero);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 100);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 4);
        unit.attribute_snapshot.set_value("action_points", 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.DODGE_BONUS_ID(), 0);
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected))
        {
            return;
        }
        _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
