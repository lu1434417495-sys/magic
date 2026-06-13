using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_unit_spec_defaults_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDefaultAttackBonusAndAcAreInitialized();
        TestBaseAttributesUseFormalAttributeService();
        TestAttributeOverridesCanReplaceAttackBonusWithoutFinalAc();
        TestFormalBaseAttributesUseAcComponents();
        TestBaseAttributeOverridesUseFormalActionThreshold();
        Quit(_test.Finish("Battle sim unit spec defaults regression"));
    }

    private void TestDefaultAttackBonusAndAcAreInitialized()
    {
        var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "sim_default",
            display_name = "默认模拟单位",
            current_hp = 30,
            current_ap = 1,
        };
        BattleUnitState unitState = unitSpec.ToBattleUnitState("player", "manual");
        _test.Eq(
            unitState.attribute_snapshot.GetValue(AttributeService.ATTACK_BONUS),
            4,
            "BattleSimUnitSpec 默认应初始化 +4 攻击加值，避免 simulation 中退化到仅天然 20 命中。"
        );
        _test.False(
            unitState.attribute_snapshot.HasValue(AttributeService.ARMOR_CLASS),
            "BattleSimUnitSpec 空单位不应再隐式初始化 AC；simulation 应提供 base_attributes。"
        );
    }

    private void TestBaseAttributesUseFormalAttributeService()
    {
        var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "sim_formal_base",
            display_name = "正式属性模拟单位",
            current_hp = 99,
            current_stamina = 110,
            current_ap = 2,
            action_threshold = 120,
            base_attributes = BaseAttributes(10, 16, 12, 14, 8, 10),
        };
        BattleUnitState unitState = unitSpec.ToBattleUnitState("player", "ai");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.HP_MAX), 16, "BattleSimUnitSpec 有 base_attributes 时应使用正式 0 级初始 HP 公式。");
        _test.Eq(unitState.current_hp, 16, "BattleSimUnitSpec 当前 HP 应按正式 HP 上限 clamp。");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.STAMINA_MAX), 110, "BattleSimUnitSpec 有 base_attributes 时应通过 AttributeService 派生体力。");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.ACTION_POINTS), 2, "BattleSimUnitSpec 有 base_attributes 时应通过 AttributeService 派生 AP。");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS), 11, "BattleSimUnitSpec 有 base_attributes 时 AC 应来自正式 AttributeService。");
        _test.Eq(unitState.action_threshold, AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD, "BattleSimUnitSpec 有 base_attributes 时 action_threshold 应来自正式属性快照。");
    }

    private void TestAttributeOverridesCanReplaceAttackBonusWithoutFinalAc()
    {
        var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "sim_override",
            display_name = "覆盖模拟单位",
            current_hp = 30,
            current_ap = 1,
            attribute_overrides = new GDictionary
            {
                ["attack_bonus"] = 6,
                ["armor_class"] = 17,
            },
        };
        BattleUnitState unitState = unitSpec.ToBattleUnitState("hostile", "ai");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.ATTACK_BONUS), 6, "BattleSimUnitSpec 应允许 attribute_overrides 覆盖默认攻击加值。");
        _test.False(unitState.attribute_snapshot.HasValue(AttributeService.ARMOR_CLASS), "BattleSimUnitSpec 不应接受无 base_attributes 的最终 armor_class 兼容路径。");
    }

    private void TestFormalBaseAttributesUseAcComponents()
    {
        var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "sim_formal_ac_components",
            display_name = "正式 AC 组件模拟单位",
            current_hp = 30,
            current_ap = 1,
            base_attributes = BaseAttributes(10, 10, 10, 10, 10, 10),
            attribute_overrides = new GDictionary
            {
                ["armor_class"] = 99,
                ["armor_ac_bonus"] = 2,
                ["deflection_bonus"] = 4,
            },
        };
        BattleUnitState unitState = unitSpec.ToBattleUnitState("hostile", "ai");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS), 14, "BattleSimUnitSpec 有 base_attributes 时应忽略最终 armor_class，改用基础 8 + AC 组件。");
    }

    private void TestBaseAttributeOverridesUseFormalActionThreshold()
    {
        var unitSpec = new BattleSimUnitSpec
        {
            unit_id = "sim_threshold_override",
            display_name = "行动阈值覆盖单位",
            current_hp = 30,
            current_stamina = 120,
            current_ap = 2,
            action_threshold = 120,
            base_attributes = BaseAttributes(14, 12, 14, 10, 8, 10),
            attribute_overrides = new GDictionary
            {
                ["hp_max"] = 36,
                ["action_threshold"] = 47,
            },
        };
        BattleUnitState unitState = unitSpec.ToBattleUnitState("hostile", "ai");
        _test.Eq(unitState.attribute_snapshot.GetValue(AttributeService.HP_MAX), 36, "base attribute 模拟单位的 hp_max 覆盖应通过正式属性快照生效。");
        _test.Eq(unitState.action_threshold, 45, "base attribute 模拟单位的 action_threshold 覆盖应经过 AttributeService 的 5 TU 归一。");
    }

    private static GDictionary BaseAttributes(
        int strength,
        int agility,
        int constitution,
        int perception,
        int intelligence,
        int willpower
    ) =>
        new()
        {
            ["strength"] = strength,
            ["agility"] = agility,
            ["constitution"] = constitution,
            ["perception"] = perception,
            ["intelligence"] = intelligence,
            ["willpower"] = willpower,
        };
}
