using System.Collections.Generic;
using Godot;

public partial class run_equipment_drop_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestRollDropRarityHitsAllThresholdTiers();
        TestRollDropRarityAcceptsCallerClampedExtremes();
        TestRollDropsKeepsEmptyMainPathStable();
        TestRollItemInstancesReturnsTypedList();
        TestEquipmentDropServiceIsPlainService();

        if (_failures.Count == 0)
        {
            GD.Print("Equipment drop service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Equipment drop service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestRollDropRarityHitsAllThresholdTiers()
    {
        AssertRarityRoll(
            "COMMON 档位上界应落在 9",
            new[] { 3, 3, 3 },
            0,
            EquipmentInstanceState.RARITY_TIER_COMMON()
        );
        AssertRarityRoll(
            "UNCOMMON 档位门槛应落在 10",
            new[] { 4, 3, 3 },
            0,
            EquipmentInstanceState.RARITY_TIER_UNCOMMON()
        );
        AssertRarityRoll(
            "RARE 档位门槛应落在 13",
            new[] { 5, 4, 4 },
            0,
            EquipmentInstanceState.RARITY_TIER_RARE()
        );
        AssertRarityRoll(
            "EPIC 档位门槛应落在 16",
            new[] { 6, 5, 5 },
            0,
            EquipmentInstanceState.RARITY_TIER_EPIC()
        );
        AssertRarityRoll(
            "LEGENDARY 档位门槛应落在 18",
            new[] { 6, 6, 6 },
            0,
            EquipmentInstanceState.RARITY_TIER_LEGENDARY()
        );
    }

    private void TestRollDropRarityAcceptsCallerClampedExtremes()
    {
        AssertRarityRoll(
            "最低 drop_luck=-6 应直接参与 3d6 结果",
            new[] { 6, 6, 6 },
            -6,
            EquipmentInstanceState.RARITY_TIER_UNCOMMON()
        );
        AssertRarityRoll(
            "最高 drop_luck=+5 应直接参与 3d6 结果",
            new[] { 1, 1, 1 },
            5,
            EquipmentInstanceState.RARITY_TIER_COMMON()
        );
    }

    private void TestRollDropsKeepsEmptyMainPathStable()
    {
        EquipmentDropService service = new();
        FixedRollRng rng = new(new[] { 6, 6, 6 });
        service.SetRollRangeForTesting(rng.RollRange);

        List<object> drops = service.RollDrops("starter_equipment", 0);

        AssertTrue(drops != null, "RollDrops 当前应返回稳定的 typed list。");
        AssertEq(drops?.Count ?? -1, 0, "正式掉落表尚未接入前，RollDrops 应返回空列表。");
    }

    private void TestRollItemInstancesReturnsTypedList()
    {
        EquipmentDropService service = new();
        FixedRollRng rng = new(new[] { 6, 6, 6 });
        service.SetRollRangeForTesting(rng.RollRange);

        List<EquipmentInstanceState> instances = service.RollItemInstances("iron_sword", 2, 0);

        AssertEq(instances.Count, 2, "RollItemInstances 应返回 typed 装备实例列表。");
        AssertEq(
            instances[0].rarity,
            EquipmentInstanceState.RARITY_TIER_LEGENDARY(),
            "装备实例稀有度应由 typed 掷骰结果写入。"
        );
        AssertEq(
            instances[0].current_durability,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                EquipmentInstanceState.RARITY_TIER_LEGENDARY()
            ),
            "装备实例耐久应由 typed durability 规则写入。"
        );
    }

    private void TestEquipmentDropServiceIsPlainService()
    {
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(typeof(EquipmentDropService)),
            "EquipmentDropService 不应继承 RefCounted。"
        );
        AssertTrue(
            typeof(EquipmentDropService).GetCustomAttributes(typeof(GlobalClassAttribute), false)
                .Length == 0,
            "EquipmentDropService 不应注册为 Godot GlobalClass。"
        );
    }

    private void AssertRarityRoll(
        string label,
        IEnumerable<int> rolls,
        int dropLuck,
        int expectedRarity
    )
    {
        EquipmentDropService service = new();
        FixedRollRng rng = new(rolls);
        service.SetRollRangeForTesting(rng.RollRange);

        int actualRarity = service.RollDropRarity(dropLuck);
        AssertEq(actualRarity, expectedRarity, $"{label}。");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private sealed class FixedRollRng
    {
        private readonly List<int> _rolls;
        private int _cursor;

        public FixedRollRng(IEnumerable<int> rolls)
        {
            _rolls = new List<int>(rolls);
        }

        public int RollRange(int minValue, int maxValue)
        {
            if (_cursor >= _rolls.Count)
            {
                return minValue;
            }

            int value = _rolls[_cursor];
            _cursor += 1;
            return Mathf.Clamp(value, minValue, maxValue);
        }
    }
}
