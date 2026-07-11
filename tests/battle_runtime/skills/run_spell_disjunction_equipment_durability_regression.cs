using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_spell_disjunction_equipment_durability_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

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

        RequestTestExit(_test.Finish("Spell disjunction equipment durability regression"));
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
            (int)EquipmentInstanceState.RarityTier.COMMON,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.COMMON
            )
        );

        using GodotProjectionLease<GDictionary> firstResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            caster,
            target,
            new[] { FixedDamageEffect(1), DisjunctionEffect(28) },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 1, ["equipment_slot_override"] = "main_hand" }
            )
        ));
        GDictionary firstResult = firstResultLease.Value;
        EquipmentInstanceState firstInstance = target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        _test.Eq(DictInt(firstResult, "damage"), 1, "第一次裂解测试应使用固定伤害。");
        _test.True(firstInstance != null, "第一次失败后普通装备应仍在装备栏。");
        if (firstInstance != null)
            _test.Eq(firstInstance.current_durability, 28, "第一次失败应扣除 28 点耐久。");

        using GodotProjectionLease<GDictionary> secondResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            caster,
            target,
            new[] { FixedDamageEffect(1), DisjunctionEffect(28) },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 1, ["equipment_slot_override"] = "main_hand" }
            )
        ));
        GDictionary secondResult = secondResultLease.Value;
        GArray events = DictArray(secondResult, "equipment_durability_events");
        _test.Eq(
            target.GetEquipmentView().GetEquippedItemId("main_hand"),
            new StringName(""),
            "第二次失败后 0 耐久装备应直接从装备栏消失。"
        );
        _test.True(events.Count > 0, "第二次失败应记录装备耐久事件。");
        if (events.Count > 0)
            _test.True(
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
            (int)EquipmentInstanceState.RarityTier.COMMON,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.COMMON
            )
        );

        using GodotProjectionLease<GDictionary> resultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            caster,
            target,
            new[] { DisjunctionEffect(28), FixedDamageEffect(1) },
            DamageResolutionContext.FromDictionary(
                new GDictionary
                {
                    ["attack_success"] = true,
                    ["save_roll_override"] = 1,
                    ["equipment_slot_override"] = "main_hand",
                }
            )
        ));
        GDictionary result = resultLease.Value;
        EquipmentInstanceState equippedInstance = target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        GArray events = DictArray(result, "equipment_durability_events");
        _test.Eq(DictInt(result, "damage"), 1, "反向效果顺序仍应结算固定伤害。");
        _test.True(
            events.Count > 0,
            "命中元数据存在时，裂解效果排在伤害前也应记录装备耐久事件。"
        );
        _test.True(equippedInstance != null, "反向效果顺序失败后普通装备应仍在装备栏。");
        if (equippedInstance != null)
            _test.Eq(equippedInstance.current_durability, 28, "反向效果顺序失败应扣除 28 点耐久。");
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
            (int)EquipmentInstanceState.RarityTier.COMMON,
            56
        );

        using GodotProjectionLease<GDictionary> resultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            caster,
            target,
            new[] { FixedDamageEffect(1), DisjunctionEffect(28) },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 20, ["equipment_slot_override"] = "main_hand" }
            )
        ));
        GDictionary result = resultLease.Value;
        EquipmentInstanceState equippedInstance = target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        _test.True(equippedInstance != null, "豁免成功后装备应保留。");
        if (equippedInstance != null)
            _test.Eq(equippedInstance.current_durability, 56, "豁免成功不应扣除耐久。");
        GArray events = DictArray(result, "equipment_durability_events");
        _test.True(events.Count > 0, "豁免成功也应记录裂解判定事件。");
        if (events.Count > 0)
        {
            GDictionary saveResult = DictDictionary(events[0].AsGodotDictionary(), "save_result");
            _test.True(DictBool(saveResult, "success"), "自然 20 的装备裂解豁免应成功。");
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
            (int)EquipmentInstanceState.RarityTier.RARE,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.RARE
            )
        );

        using GodotProjectionLease<GDictionary> resultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(resolver.ResolveEffects(
            caster,
            target,
            new[] { FixedDamageEffect(1), DisjunctionEffect(28) },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 11, ["equipment_slot_override"] = "main_hand" }
            )
        ));
        GDictionary result = resultLease.Value;
        GArray events = DictArray(result, "equipment_durability_events");
        _test.True(events.Count > 0, "稀有度加值豁免应记录裂解事件。");
        if (events.Count == 0)
            return;

        GDictionary saveResult = DictDictionary(events[0].AsGodotDictionary(), "save_result");
        _test.Eq(DictInt(saveResult, "equipment_rarity_bonus"), 4, "rare 装备应提供 +4 裂解豁免加值。");
        _test.Eq(DictInt(saveResult, "roll_total"), 15, "rare +4 应把 11 点 d20 结果推到 DC 15。");
        _test.True(DictBool(saveResult, "success"), "稀有度加值达到 DC 时应完全免除耐久损失。");
        EquipmentInstanceState equippedInstance = target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        _test.True(equippedInstance != null, "稀有度加值成功后装备应保留。");
        if (equippedInstance != null)
            _test.Eq(equippedInstance.current_durability, 120, "稀有度加值成功后耐久应保持满值。");
    }

    private static CombatEffectDefinition FixedDamageEffect(int power) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: Mathf.Max(power, 0),
            damageTag: "magic"
        );

    private static CombatEffectDefinition DisjunctionEffect(int power) =>
        CombatEffectDefinition.FromResource(
            new CombatEffectDef
            {
                effect_type = "equipment_durability_damage",
                power = Mathf.Max(power, 1),
                effect_target_team_filter = "enemy",
                save_dc_mode = "caster_spell",
                save_ability = "willpower",
                save_dc_source_ability = "intelligence",
                save_tag = "equipment_disjunction",
                require_damage_applied = true,
                equipment_durability_slot_weights =
                    new Godot.Collections.Array<CombatEffectSlotWeightDef>
                    {
                        new() { slot_id = "main_hand", weight = 1 },
                    },
                @params = new GDictionary
                {
                    ["max_damaged_items"] = 1,
                    ["target_slots"] = new GStringNameArray { "main_hand" },
                },
            },
            "test.spell_disjunction.equipment_durability_effect"
        );

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
        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(itemId, instanceId);
        instance.rarity = rarity;
        instance.current_durability = currentDurability;
        bool equipped = equipmentState.SetEquippedEntry(
            slotId,
            itemId,
            new GStringNameArray { slotId },
            instance
        );
        _test.True(equipped, "测试装备实例应能写入装备栏。");
        unitState.SetEquipmentView(equipmentState);
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 0);
        unit.attribute_snapshot.SetValue("intelligence", 18);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 3);
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

}
