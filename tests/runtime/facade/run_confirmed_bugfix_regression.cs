using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_confirmed_bugfix_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAttackDispositionRespectsNaturalRollFlags();
        TestAttackMetadataRespectsExplicitCritLock();
        TestMissingItemDefDoesNotTrapEquippedInstance();

        RequestTestExit(_test.Finish("Confirmed bugfix regression"));
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

    private void TestAttackMetadataRespectsExplicitCritLock()
    {
        BattleHitResolver hitResolver = new();
        AttackCheckInput critLockedCheck = new(
            requiredRoll: 21,
            naturalTwentyAutoHit: false,
            critLocked: true
        );
        AttackResolutionMetadata metadata = hitResolver.ResolveAttackMetadata(
            BuildUnit("crit_lock_source"),
            BuildUnit("crit_lock_target"),
            critLockedCheck,
            new AttackContext(new[] { 20 })
        );

        _test.Eq(metadata.HitRoll, 20, "显式禁暴击回归应固定掷出 d20=20。");
        _test.True(metadata.CritLocked, "执行元数据应保留 AttackCheckInput.CritLocked。");
        _test.False(metadata.AttackSuccess, "禁用自然 20 自动命中且门槛为 21 时应未命中。");
        _test.False(metadata.CriticalHit, "显式禁暴击时 d20=20 不得提前判为暴击命中。");
        _test.True(metadata.OrdinaryMiss, "显式禁暴击后的阈值失败应记为普通未命中。");
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
        equipmentService.Setup(partyState, new Dictionary<StringName, ItemDefinition>());
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
        return TestResourceOwnership.Own(
            new CombatEffectDef
            {
                effect_type = "damage",
                power = power,
                damage_tag = "physical_slash",
                @params = new GDictionary(),
            },
            "ConfirmedBugfix.BuildDamageEffect"
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
        }.WithCombatResourcesForTest(
            hp: 100,
            mp: 4,
            stamina: 4,
            aura: 0,
            ap: 2,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
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
