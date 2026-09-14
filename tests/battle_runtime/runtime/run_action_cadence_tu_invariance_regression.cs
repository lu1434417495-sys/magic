using Godot;
using GDictionary = Godot.Collections.Dictionary;

// TU 计价系统对行动阈值的不变性回归。
// 设计来源：docs/design/battle/action_cadence.md §五
//
// P2 实测的核心结论：一场长度 L 的战斗里，技能施放次数（L/cooldown）、状态覆盖时长（duration）、
// 体力回复总量（L × 每 TU 回复）全都不随行动阈值变化，**只有行动次数 L/T 变化**。
// 敏捷买到的是填充行动，不是技能强度或续航强度——这条用例把这个定位钉住。
//
// 会让它失败的改动，正是会推翻 §五 定位的那些：
//   - 把体力回复从「按 TU」改成「按行动次数」→ 高敏 build 会开始体力卡死；
//   - 把冷却从绝对 TU 改成按行动次数计价 → 敏捷会变成爆发属性；
//   - 改动基数 BASE 使主流冷却不再整除 → §三 的零内容迁移前提失效。
public partial class run_action_cadence_tu_invariance_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private const int Base = ActionCadenceContentRules.BaseActionThresholdTu;
    private const int Granularity = ActionCadenceContentRules.ActionThresholdGranularityTu;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStaminaRecoveryIsPerTuNotPerAction();
        TestActionCountIsTheOnlyQuantityThatMoves();
        TestMainstreamCooldownsDivideTheBase();
        RequestTestExit(_test.Finish("Action cadence TU invariance regression"));
    }

    // 同样的 CON、不同的 agility：每 tick 的回复量必须相同，tick 数只看流逝 TU。
    // 也就是说「每 TU 回复多少体力」与行动阈值完全无关。
    private void TestStaminaRecoveryIsPerTuNotPerAction()
    {
        BattleUnitState slow = BuildUnit("cadence_slow", agility: 8);    // 调整值 -1 -> 45 TU
        BattleUnitState fast = BuildUnit("cadence_fast", agility: 20);   // 调整值 +5 -> 25 TU

        _test.Eq(slow.GetActionThresholdTyped(), 45, "agility 8（调整值 -1）应派生 45 TU。");
        _test.Eq(fast.GetActionThresholdTyped(), 25, "agility 20（调整值 +5）应派生 25 TU。");

        int slowPerTick = BattleStaminaRecoveryRules.ResolveProgressGainPerTick(slow, false);
        int fastPerTick = BattleStaminaRecoveryRules.ResolveProgressGainPerTick(fast, false);
        _test.Eq(
            fastPerTick,
            slowPerTick,
            "体力每 tick 回复量只吃 CON，不应因 agility / 行动阈值不同而变化。"
        );

        const int elapsedTu = 900;
        int ticks = BattleStaminaRecoveryRules.ResolveTickCount(elapsedTu);
        _test.Eq(ticks, elapsedTu / Granularity, "tick 数应只由流逝 TU 决定。");

        // 同一段时间里两人回复的体力总量相同——这就是「高敏不会体力卡死」的机制保证：
        // 受体力约束的技能施放次数/TU 与阈值无关，变快只是多出普攻和走位。
        _test.Eq(
            ticks * fastPerTick,
            ticks * slowPerTick,
            "同一段流逝 TU 内，快慢单位回复的体力总量必须相同。"
        );

        // 换算到「每次行动」上就会分叉，且分叉比例恰好是阈值比。
        int slowPerAction = (slow.GetActionThresholdTyped() / Granularity) * slowPerTick;
        int fastPerAction = (fast.GetActionThresholdTyped() / Granularity) * fastPerTick;
        _test.Eq(
            slowPerAction * fast.GetActionThresholdTyped(),
            fastPerAction * slow.GetActionThresholdTyped(),
            "每次行动的体力回复应与阈值成正比——变快的单位每次行动回复更少，是同一条规则的另一面。"
        );
    }

    // 固定战斗长度下：冷却按绝对 TU 递减，只有行动次数随阈值变化。
    private void TestActionCountIsTheOnlyQuantityThatMoves()
    {
        const int battleTu = 1200;
        const int cooldownTu = 80;

        int slowThreshold = ActionCadenceContentRules.ResolveActionThreshold(-1);   // 45
        int fastThreshold = ActionCadenceContentRules.ResolveActionThreshold(5);    // 25

        // 冷却计价单位：BattleUnitCooldownState.AdvanceTo 按流逝 TU 递减，不按行动次数。
        // 这是「冷却的绝对施放次数与阈值无关」的机制根据；若哪天改成按行动次数计价，
        // 敏捷就会从续航属性变成爆发属性（§五），这条会失败。
        StringName skillId = "cadence_probe_skill";
        var cooldowns = new BattleUnitCooldownState();
        cooldowns.Set(skillId, cooldownTu);
        cooldowns.EnsureAnchor(0);
        BattleUnitCooldownAdvanceResult advance = cooldowns.AdvanceTo(
            Granularity * 3,
            Granularity
        );
        _test.Eq(advance.ElapsedTu, Granularity * 3, "冷却推进应按流逝 TU 结算。");
        _test.Eq(
            cooldowns.Get(skillId),
            cooldownTu - Granularity * 3,
            "冷却剩余量应按流逝 TU 递减，与该单位行动了几次无关。"
        );

        int slowActions = battleTu / slowThreshold;
        int fastActions = battleTu / fastThreshold;
        _test.True(
            fastActions > slowActions,
            $"行动次数是唯一随阈值变化的量：{fastThreshold} TU 应比 {slowThreshold} TU 多行动。"
        );
        _test.Eq(fastActions, 48, "1200 TU / 25 TU 应为 48 次行动。");
        _test.Eq(slowActions, 26, "1200 TU / 45 TU 应为 26 次行动。");

        // 施放次数受量化影响：实际循环是 ceil(cooldown / threshold) * threshold。
        // 基数档上 80 TU 冷却零等待，相邻档会有等待——这是量化轴的固有代价（§三）。
        _test.Eq(
            CooldownCycleTu(cooldownTu, Base),
            cooldownTu,
            $"基数 {Base} TU 上，{cooldownTu} TU 冷却应零等待。"
        );
        _test.True(
            CooldownCycleTu(cooldownTu, slowThreshold) > cooldownTu,
            "非整除档位会产生等待，这是已知且接受的量化代价。"
        );
    }

    // §三 的零内容迁移前提：主流冷却在基数上正好落在整数次行动。
    private void TestMainstreamCooldownsDivideTheBase()
    {
        int[] mainstreamCooldowns = { 40, 80, 120, 160 };
        for (int index = 0; index < mainstreamCooldowns.Length; index++)
        {
            int cooldown = mainstreamCooldowns[index];
            _test.Eq(
                cooldown % Base,
                0,
                $"主流冷却 {cooldown} TU 必须被基数 {Base} 整除，否则 §三 的零内容迁移前提失效。"
            );
            _test.Eq(
                CooldownCycleTu(cooldown, Base),
                cooldown,
                $"{cooldown} TU 冷却在基数上应正好是 {cooldown / Base} 次行动。"
            );
        }
    }

    private static int CooldownCycleTu(int cooldownTu, int thresholdTu) =>
        Mathf.CeilToInt((float)cooldownTu / thresholdTu) * thresholdTu;

    private static BattleUnitState BuildUnit(StringName unitId, int agility)
    {
        var spec = new BattleSimTestUnitBuilder
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            current_hp = 40,
            current_ap = 2,
            base_attributes = new GDictionary
            {
                ["strength"] = 10,
                ["agility"] = agility,
                ["constitution"] = 12,
                ["perception"] = 10,
                ["intelligence"] = 10,
                ["willpower"] = 10,
            },
        };
        return spec.ToDefinition("player", "manual").CreateRuntimeState();
    }
}
