using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_spell_disjunction_equipment_durability_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestDisjunctionFailureDestroysCommonEquipmentAfterTwoHits();
        TestDisjunctionReversedEffectOrderUsesAttackSuccessRequirement();
        TestDisjunctionSuccessLeavesDurabilityUnchanged();
        TestDisjunctionRarityBonusCanPassSave();
        TestEquipmentDurabilityRulesIsPlainStaticHelper();

        if (_failures.Count == 0)
        {
            GD.Print("Spell disjunction equipment durability regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Spell disjunction equipment durability regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestDisjunctionFailureDestroysCommonEquipmentAfterTwoHits()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("caster", "player");
        BattleUnitState target = BuildUnit("target", "enemy");
        EquipInstance(
            target,
            "main_hand",
            "bronze_sword",
            "eq_common_sword",
            EquipmentInstanceState.RARITY_TIER_COMMON(),
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                EquipmentInstanceState.RARITY_TIER_COMMON()
            )
        );

        GDictionary firstResult = resolver.resolve_effects(
            caster,
            target,
            new GArray { FixedDamageEffect(1), DisjunctionEffect(28) },
            new GDictionary { ["save_roll_override"] = 1, ["equipment_slot_override"] = "main_hand" }
        );
        EquipmentInstanceState firstInstance = target
            .get_equipment_view()
            .get_equipped_instance("main_hand");
        AssertEq(DictInt(firstResult, "damage"), 1, "第一次裂解测试应使用固定伤害。");
        AssertTrue(firstInstance != null, "第一次失败后普通装备应仍在装备栏。");
        if (firstInstance != null)
            AssertEq(firstInstance.current_durability, 28, "第一次失败应扣除 28 点耐久。");

        GDictionary secondResult = resolver.resolve_effects(
            caster,
            target,
            new GArray { FixedDamageEffect(1), DisjunctionEffect(28) },
            new GDictionary { ["save_roll_override"] = 1, ["equipment_slot_override"] = "main_hand" }
        );
        GArray events = DictArray(secondResult, "equipment_durability_events");
        AssertEq(
            target.get_equipment_view().get_equipped_item_id("main_hand"),
            new StringName(""),
            "第二次失败后 0 耐久装备应直接从装备栏消失。"
        );
        AssertTrue(events.Count > 0, "第二次失败应记录装备耐久事件。");
        if (events.Count > 0)
            AssertTrue(
                DictBool(events[0].AsGodotDictionary(), "destroyed"),
                "第二次失败的装备耐久事件应标记 destroyed。"
            );
    }

    private void TestDisjunctionReversedEffectOrderUsesAttackSuccessRequirement()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("caster_reversed", "player");
        BattleUnitState target = BuildUnit("target_reversed", "enemy");
        EquipInstance(
            target,
            "main_hand",
            "bronze_sword",
            "eq_reversed_sword",
            EquipmentInstanceState.RARITY_TIER_COMMON(),
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                EquipmentInstanceState.RARITY_TIER_COMMON()
            )
        );

        GDictionary result = resolver.resolve_effects(
            caster,
            target,
            new GArray { DisjunctionEffect(28), FixedDamageEffect(1) },
            new GDictionary
            {
                ["attack_success"] = true,
                ["save_roll_override"] = 1,
                ["equipment_slot_override"] = "main_hand",
            }
        );
        EquipmentInstanceState equippedInstance = target
            .get_equipment_view()
            .get_equipped_instance("main_hand");
        GArray events = DictArray(result, "equipment_durability_events");
        AssertEq(DictInt(result, "damage"), 1, "反向效果顺序仍应结算固定伤害。");
        AssertTrue(
            events.Count > 0,
            "命中元数据存在时，裂解效果排在伤害前也应记录装备耐久事件。"
        );
        AssertTrue(equippedInstance != null, "反向效果顺序失败后普通装备应仍在装备栏。");
        if (equippedInstance != null)
            AssertEq(equippedInstance.current_durability, 28, "反向效果顺序失败应扣除 28 点耐久。");
    }

    private void TestDisjunctionSuccessLeavesDurabilityUnchanged()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("caster_success", "player");
        BattleUnitState target = BuildUnit("target_success", "enemy");
        EquipInstance(
            target,
            "main_hand",
            "bronze_sword",
            "eq_saved_sword",
            EquipmentInstanceState.RARITY_TIER_COMMON(),
            56
        );

        GDictionary result = resolver.resolve_effects(
            caster,
            target,
            new GArray { FixedDamageEffect(1), DisjunctionEffect(28) },
            new GDictionary { ["save_roll_override"] = 20, ["equipment_slot_override"] = "main_hand" }
        );
        EquipmentInstanceState equippedInstance = target
            .get_equipment_view()
            .get_equipped_instance("main_hand");
        AssertTrue(equippedInstance != null, "豁免成功后装备应保留。");
        if (equippedInstance != null)
            AssertEq(equippedInstance.current_durability, 56, "豁免成功不应扣除耐久。");
        GArray events = DictArray(result, "equipment_durability_events");
        AssertTrue(events.Count > 0, "豁免成功也应记录裂解判定事件。");
        if (events.Count > 0)
        {
            GDictionary saveResult = DictDictionary(events[0].AsGodotDictionary(), "save_result");
            AssertTrue(DictBool(saveResult, "success"), "自然 20 的装备裂解豁免应成功。");
        }
    }

    private void TestDisjunctionRarityBonusCanPassSave()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("caster_rare", "player");
        BattleUnitState target = BuildUnit("target_rare", "enemy");
        EquipInstance(
            target,
            "main_hand",
            "bronze_sword",
            "eq_rare_sword",
            EquipmentInstanceState.RARITY_TIER_RARE(),
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                EquipmentInstanceState.RARITY_TIER_RARE()
            )
        );

        GDictionary result = resolver.resolve_effects(
            caster,
            target,
            new GArray { FixedDamageEffect(1), DisjunctionEffect(28) },
            new GDictionary { ["save_roll_override"] = 11, ["equipment_slot_override"] = "main_hand" }
        );
        GArray events = DictArray(result, "equipment_durability_events");
        AssertTrue(events.Count > 0, "稀有度加值豁免应记录裂解事件。");
        if (events.Count == 0)
            return;

        GDictionary saveResult = DictDictionary(events[0].AsGodotDictionary(), "save_result");
        AssertEq(DictInt(saveResult, "equipment_rarity_bonus"), 4, "rare 装备应提供 +4 裂解豁免加值。");
        AssertEq(DictInt(saveResult, "roll_total"), 15, "rare +4 应把 11 点 d20 结果推到 DC 15。");
        AssertTrue(DictBool(saveResult, "success"), "稀有度加值达到 DC 时应完全免除耐久损失。");
        EquipmentInstanceState equippedInstance = target
            .get_equipment_view()
            .get_equipped_instance("main_hand");
        AssertTrue(equippedInstance != null, "稀有度加值成功后装备应保留。");
        if (equippedInstance != null)
            AssertEq(equippedInstance.current_durability, 120, "稀有度加值成功后耐久应保持满值。");
    }

    private void TestEquipmentDurabilityRulesIsPlainStaticHelper()
    {
        var type = typeof(EquipmentDurabilityRules);
        AssertTrue(type.IsAbstract && type.IsSealed, "EquipmentDurabilityRules 应是 C# static helper。");
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(type),
            "EquipmentDurabilityRules 不应再继承 RefCounted。"
        );
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            "EquipmentDurabilityRules 不应再注册为 Godot GlobalClass。"
        );
        foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            string fullName = field.FieldType.FullName ?? "";
            AssertTrue(
                !fullName.StartsWith("Godot.Collections.Dictionary"),
                $"EquipmentDurabilityRules 内部状态不应使用 Godot Dictionary：{field.Name}"
            );
            AssertTrue(
                !fullName.StartsWith("Godot.Collections.Array"),
                $"EquipmentDurabilityRules 内部状态不应使用 Godot Array：{field.Name}"
            );
        }
    }

    private static CombatEffectDef FixedDamageEffect(int power) =>
        new()
        {
            effect_type = "damage",
            power = Mathf.Max(power, 0),
            damage_tag = "magic",
        };

    private static CombatEffectDef DisjunctionEffect(int power) =>
        new()
        {
            effect_type = "equipment_durability_damage",
            power = Mathf.Max(power, 1),
            effect_target_team_filter = "enemy",
            save_dc_mode = "caster_spell",
            save_ability = "willpower",
            save_dc_source_ability = "intelligence",
            save_tag = "equipment_disjunction",
            require_damage_applied = true,
            @params = new GDictionary
            {
                ["max_damaged_items"] = 1,
                ["slot_weight_map"] = new GDictionary { ["main_hand"] = 1 },
                ["target_slots"] = new GStringNameArray { "main_hand" },
            },
        };

    private void EquipInstance(
        BattleUnitState unitState,
        StringName slotId,
        StringName itemId,
        StringName instanceId,
        int rarity,
        int currentDurability
    )
    {
        EquipmentState equipmentState = new();
        EquipmentInstanceState instance = EquipmentInstanceState.create_instance(itemId, instanceId);
        instance.rarity = rarity;
        instance.current_durability = currentDurability;
        bool equipped = equipmentState.set_equipped_entry(
            slotId,
            itemId,
            new GStringNameArray { slotId },
            instance
        );
        AssertTrue(equipped, "测试装备实例应能写入装备栏。");
        unitState.set_equipment_view(equipmentState);
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            is_alive = true,
            current_hp = 30,
            current_mp = 0,
            current_stamina = 0,
            current_aura = 0,
            current_ap = 1,
            attribute_snapshot = new AttributeSnapshot(),
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 30);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.AURA_MAX_ID(), 0);
        unit.attribute_snapshot.set_value("intelligence", 18);
        unit.attribute_snapshot.set_value("willpower", 10);
        unit.attribute_snapshot.set_value(AttributeService.SPELL_PROFICIENCY_BONUS_ID(), 3);
        return unit;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : fallback;

    private static bool DictBool(GDictionary dictionary, string key, bool fallback = false) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsBool() : fallback;

    private static GArray DictArray(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();

    private static GDictionary DictDictionary(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
