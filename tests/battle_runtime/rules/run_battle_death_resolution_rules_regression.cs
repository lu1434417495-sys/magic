using System;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_death_resolution_rules_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestRuleTypeIsPlainStaticCSharp();
        TestPowerWordKillContextIsTyped();
        TestNormalFatalContextIsNotPowerWordKill();

        if (_failures.Count == 0)
        {
            GD.Print("Battle death resolution rules regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle death resolution rules regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRuleTypeIsPlainStaticCSharp()
    {
        Type ruleType = typeof(BattleDeathResolutionRules);
        AssertTrue(ruleType.IsAbstract && ruleType.IsSealed, "死亡来源规则应是 plain static C# class。");
        AssertFalse(typeof(RefCounted).IsAssignableFrom(ruleType), "死亡来源规则不应继承 RefCounted。");
        AssertFalse(HasAttributeNamed(ruleType, "GlobalClassAttribute"), "死亡来源规则不应注册 GlobalClass。");

        foreach (
            MethodInfo method in ruleType.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly
            )
        )
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                string typeName = parameter.ParameterType.FullName ?? "";
                AssertFalse(
                    typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal),
                    $"{method.Name} 不应公开 Godot Dictionary 参数。"
                );
            }
        }
    }

    private void TestPowerWordKillContextIsTyped()
    {
        DeathResolutionContext context =
            BattleDeathResolutionRules.PowerWordKillExecuteContext();

        AssertTrue(context.HasDeathSource, "Power Word Kill execute context 应带有死亡来源。");
        AssertStringNameEq(
            context.DeathSource,
            BattleDeathResolutionRules.PowerWordKillExecuteDeathSource,
            "Power Word Kill execute context 应使用稳定死亡来源 ID。"
        );
        AssertEq(
            context.DeathSourcePriority,
            BattleDeathResolutionRules.DeathPriorityExecuteFatal,
            "Power Word Kill execute context 应使用 execute fatal 优先级。"
        );
        AssertTrue(
            BattleDeathResolutionRules.IsPowerWordKillExecute(context),
            "typed Power Word Kill context 应被识别为 Power Word Kill execute。"
        );
    }

    private void TestNormalFatalContextIsNotPowerWordKill()
    {
        DeathResolutionContext context = BattleDeathResolutionRules.NormalFatalContext();

        AssertStringNameEq(
            context.DeathSource,
            BattleDeathResolutionRules.DamageDeathSource,
            "普通致死 context 应使用 damage 死亡来源。"
        );
        AssertEq(
            context.DeathSourcePriority,
            BattleDeathResolutionRules.DeathPriorityNormalFatal,
            "普通致死 context 应使用普通致死优先级。"
        );
        AssertFalse(
            BattleDeathResolutionRules.IsPowerWordKillExecute(context),
            "普通致死 context 不应被识别为 Power Word Kill execute。"
        );
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
            {
                return true;
            }
        }
        return false;
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
        AssertTrue(!condition, message);
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertStringNameEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
