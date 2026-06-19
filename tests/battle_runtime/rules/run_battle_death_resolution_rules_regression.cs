using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_death_resolution_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestPowerWordKillContextIsTyped();
        TestNormalFatalContextIsNotPowerWordKill();
        TestDeathProtectionPriorityComparison();

        Quit(_test.Finish("Battle death resolution rules regression"));
    }

    private void TestPowerWordKillContextIsTyped()
    {
        DeathResolutionContext context =
            BattleDeathResolutionRules.PowerWordKillExecuteContext();

        _test.True(context.HasDeathSource, "Power Word Kill execute context 应带有死亡来源。");
        _test.Eq(
            context.DeathSource,
            BattleDeathResolutionRules.PowerWordKillExecuteDeathSource,
            "Power Word Kill execute context 应使用稳定死亡来源 ID。"
        );
        _test.Eq(
            context.DeathSourcePriority,
            900,
            "Power Word Kill execute context 应使用 execute fatal 优先级。"
        );
        _test.True(
            BattleDeathResolutionRules.IsPowerWordKillExecute(context),
            "typed Power Word Kill context 应被识别为 Power Word Kill execute。"
        );
    }

    private void TestNormalFatalContextIsNotPowerWordKill()
    {
        DeathResolutionContext context = BattleDeathResolutionRules.NormalFatalContext();

        _test.Eq(
            context.DeathSource,
            BattleDeathResolutionRules.DamageDeathSource,
            "普通致死 context 应使用 damage 死亡来源。"
        );
        _test.Eq(
            context.DeathSourcePriority,
            100,
            "普通致死 context 应使用普通致死优先级。"
        );
        _test.False(
            BattleDeathResolutionRules.IsPowerWordKillExecute(context),
            "普通致死 context 不应被识别为 Power Word Kill execute。"
        );
    }

    private void TestDeathProtectionPriorityComparison()
    {
        DeathResolutionContext normal = BattleDeathResolutionRules.NormalFatalContext();
        DeathResolutionContext pwk = BattleDeathResolutionRules.PowerWordKillExecuteContext();

        _test.True(
            BattleDeathResolutionRules.CanDeathPreventionBlock(normal, 100),
            "priority 100 protection blocks normal fatal damage."
        );
        _test.False(
            BattleDeathResolutionRules.CanDeathPreventionBlock(pwk, 100),
            "priority 100 protection does not block PWK priority 900."
        );
        _test.True(
            BattleDeathResolutionRules.CanDeathPreventionBlock(pwk, 900),
            "priority 900 protection blocks PWK."
        );
    }

}
