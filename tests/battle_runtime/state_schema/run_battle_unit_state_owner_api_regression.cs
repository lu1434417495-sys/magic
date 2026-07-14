using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_unit_state_owner_api_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHpApiSynchronizesAliveState();
        TestResourceApiClampsNegativeValues();
        TestSkillCostApiOwnsResourceMutation();
        TestKnownSkillApiFiltersInternalCollection();
        TestStatusApiCapturesAndClearsOwnedDictionary();

        RequestTestExit(_test.Finish("Battle unit state owner API regression"));
    }

    private void TestHpApiSynchronizesAliveState()
    {
        BattleUnitState unit = BuildUnit();

        unit.SetCurrentHp(-10);
        _test.Eq(unit.GetCurrentHp(), 0, "SetCurrentHp 应 clamp 到 0。");
        _test.False(unit.IsAlive(), "HP 为 0 时 BattleUnitState 应同步 is_alive=false。");

        unit.ReviveWithHp(99, 30);
        _test.Eq(unit.GetCurrentHp(), 30, "ReviveWithHp 应按 hpMax clamp。");
        _test.True(unit.IsAlive(), "ReviveWithHp 应同步 is_alive=true。");

        int damage = unit.ApplyHpDamage(12);
        _test.Eq(damage, 12, "ApplyHpDamage 应返回实际伤害。");
        _test.Eq(unit.GetCurrentHp(), 18, "ApplyHpDamage 应扣减 HP。");

        int healing = unit.ApplyHealing(100, 25);
        _test.Eq(healing, 7, "ApplyHealing 应返回实际治疗量。");
        _test.Eq(unit.GetCurrentHp(), 25, "ApplyHealing 不应超过 hpMax。");

        unit.MarkDead();
        _test.Eq(unit.GetCurrentHp(), 0, "MarkDead 应清空 HP。");
        _test.False(unit.IsAlive(), "MarkDead 应同步 is_alive=false。");
    }

    private void TestResourceApiClampsNegativeValues()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetCombatResources(-1, -2, -3, -4, -5, -6);

        _test.Eq(unit.GetCurrentHp(), 0, "SetCombatResources 应 clamp HP。");
        _test.Eq(unit.GetCurrentMp(), 0, "SetCombatResources 应 clamp MP。");
        _test.Eq(unit.GetCurrentStamina(), 0, "SetCombatResources 应 clamp stamina。");
        _test.Eq(unit.GetCurrentAura(), 0, "SetCombatResources 应 clamp aura。");
        _test.Eq(unit.GetCurrentAp(), 0, "SetCombatResources 应 clamp AP。");
        _test.Eq(unit.GetCurrentMovePoints(), 0, "SetCombatResources 应 clamp move points。");

        unit.SetCombatResources(99, 99, 99, 99, 99, 99);
        unit.ClampCombatResources(new BattleResourceCaps(30, 10, 20, 3, 2, 6));
        _test.Eq(unit.GetCurrentHp(), 30, "ClampCombatResources 应 clamp HP 上限。");
        _test.Eq(unit.GetCurrentMp(), 10, "ClampCombatResources 应 clamp MP 上限。");
        _test.Eq(unit.GetCurrentStamina(), 20, "ClampCombatResources 应 clamp stamina 上限。");
        _test.Eq(unit.GetCurrentAura(), 3, "ClampCombatResources 应 clamp aura 上限。");
        _test.Eq(unit.GetCurrentAp(), 2, "ClampCombatResources 应 clamp AP 上限。");
        _test.Eq(unit.GetCurrentMovePoints(), 6, "ClampCombatResources 应 clamp move points 上限。");
    }

    private void TestSkillCostApiOwnsResourceMutation()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetCombatResources(30, 12, 20, 4, 3, 2);

        var costs = new SkillCostTransaction
        {
            SkillId = "meteor",
            ApCost = 2,
            MpCost = 5,
            StaminaCost = 7,
            AuraCost = 3,
            CooldownTurns = 15,
        };
        unit.SpendSkillCosts(costs);

        _test.Eq(unit.GetCurrentAp(), 1, "SpendSkillCosts 应扣 AP。");
        _test.Eq(unit.GetCurrentMp(), 7, "SpendSkillCosts 应扣 MP。");
        _test.Eq(unit.GetCurrentStamina(), 13, "SpendSkillCosts 应扣 stamina。");
        _test.Eq(unit.GetCurrentAura(), 1, "SpendSkillCosts 应扣 aura。");
        _test.Eq(unit.GetCooldownTyped("meteor", -1), 15, "SpendSkillCosts 应写入 cooldown。");

        unit.RefundSkillCosts(costs, new BattleResourceCaps(30, 10, 18, 2, 3, 2));
        _test.Eq(unit.GetCurrentMp(), 10, "RefundSkillCosts 应按 cap 返还 MP。");
        _test.Eq(unit.GetCurrentStamina(), 18, "RefundSkillCosts 应按 cap 返还 stamina。");
        _test.Eq(unit.GetCurrentAura(), 2, "RefundSkillCosts 应按 cap 返还 aura。");
    }

    private void TestKnownSkillApiFiltersInternalCollection()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetKnownActiveSkillIds(
            new[]
            {
                new StringName("slash"),
                new StringName(""),
                new StringName("slash"),
                new StringName("guard"),
            }
        );

        _test.True(unit.KnowsActiveSkill("slash"), "KnowsActiveSkill 应识别已知主动技能。");
        _test.True(unit.KnowsActiveSkill("guard"), "SetKnownActiveSkillIds 应保留有效技能。");
        _test.Eq(unit.GetKnownActiveSkillIdsTyped().Count, 2, "SetKnownActiveSkillIds 应过滤空值和重复值。");

        unit.SetKnownSkillLevelTyped("slash", 3);
        _test.Eq(unit.GetKnownSkillLevelTyped("slash", -1), 3, "SetKnownSkillLevelTyped 应写入等级。");
        unit.SetKnownSkillLevelTyped("slash", 0);
        _test.False(unit.HasKnownSkillLevelTyped("slash"), "0 级应移除技能等级。");
    }

    private void TestStatusApiCapturesAndClearsOwnedDictionary()
    {
        BattleUnitState unit = BuildUnit();
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "burning",
                source_unit_id = "caster",
                power = 2,
                stacks = 1,
            }
        );

        var captured = unit.CaptureStatusEffectsTyped();
        _test.Eq(captured.Count, 1, "CaptureStatusEffectsTyped 应返回有效状态。");
        _test.True(captured.ContainsKey("burning"), "CaptureStatusEffectsTyped 应按 status_id 建索引。");

        captured[new StringName("burning")].power = 99;
        _test.Eq(unit.GetStatusEffect("burning")?.power ?? -1, 2, "CaptureStatusEffectsTyped 不应共享状态引用。");

        unit.ClearStatusEffects();
        _test.False(unit.HasStatusEffect("burning"), "ClearStatusEffects 应清空 owner 状态字典。");
    }

    private static BattleUnitState BuildUnit()
    {
        BattleUnitState unit = new()
        {
            unit_id = "unit",
            display_name = "Unit",
            faction_id = "player",
        };
        unit.SetAnchorCoord(new Vector2I(1, 1));
        unit.SetCurrentHp(30);
        return unit;
    }
}
