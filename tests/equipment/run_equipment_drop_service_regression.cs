using System.Collections.Generic;
using Godot;

public partial class run_equipment_drop_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

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

        Quit(_test.Finish("Equipment drop service regression"));
    }

    private void TestRollDropRarityHitsAllThresholdTiers()
    {
        using EquipmentDropService service = new();
        AssertRarityRoll(
            service,
            "COMMON 档位上界应落在 9",
            new[] { 3, 3, 3 },
            0,
            (int)EquipmentInstanceState.RarityTier.COMMON
        );
        AssertRarityRoll(
            service,
            "UNCOMMON 档位门槛应落在 10",
            new[] { 4, 3, 3 },
            0,
            (int)EquipmentInstanceState.RarityTier.UNCOMMON
        );
        AssertRarityRoll(
            service,
            "RARE 档位门槛应落在 13",
            new[] { 5, 4, 4 },
            0,
            (int)EquipmentInstanceState.RarityTier.RARE
        );
        AssertRarityRoll(
            service,
            "EPIC 档位门槛应落在 16",
            new[] { 6, 5, 5 },
            0,
            (int)EquipmentInstanceState.RarityTier.EPIC
        );
        AssertRarityRoll(
            service,
            "LEGENDARY 档位门槛应落在 18",
            new[] { 6, 6, 6 },
            0,
            (int)EquipmentInstanceState.RarityTier.LEGENDARY
        );
    }

    private void TestRollDropRarityAcceptsCallerClampedExtremes()
    {
        using EquipmentDropService service = new();
        AssertRarityRoll(
            service,
            "最低 drop_luck=-6 应直接参与 3d6 结果",
            new[] { 6, 6, 6 },
            -6,
            (int)EquipmentInstanceState.RarityTier.UNCOMMON
        );
        AssertRarityRoll(
            service,
            "最高 drop_luck=+5 应直接参与 3d6 结果",
            new[] { 1, 1, 1 },
            5,
            (int)EquipmentInstanceState.RarityTier.COMMON
        );
    }

    private void TestRollDropsKeepsEmptyMainPathStable()
    {
        using EquipmentDropService service = new();
        FixedRollRng rng = new(new[] { 6, 6, 6 });
        service.SetRollRangeForTesting(rng.RollRange);

        List<object> drops = service.RollDrops("starter_equipment", 0);

        _test.True(drops != null, "RollDrops 当前应返回稳定的 typed list。");
        _test.Eq(drops?.Count ?? -1, 0, "正式掉落表尚未接入前，RollDrops 应返回空列表。");
    }

    private void TestRollItemInstancesReturnsTypedList()
    {
        using EquipmentDropService service = new();
        FixedRollRng rng = new(new[] { 6, 6, 6 });
        service.SetRollRangeForTesting(rng.RollRange);

        List<EquipmentInstanceState> instances = service.RollItemInstances("iron_sword", 2, 0);

        _test.Eq(instances.Count, 2, "RollItemInstances 应返回 typed 装备实例列表。");
        _test.Eq(
            instances[0].rarity,
            (int)EquipmentInstanceState.RarityTier.LEGENDARY,
            "装备实例稀有度应由 typed 掷骰结果写入。"
        );
        _test.Eq(
            instances[0].current_durability,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.LEGENDARY
            ),
            "装备实例耐久应由 typed durability 规则写入。"
        );
        _test.Eq(
            instances[0].trait_instances.Count,
            0,
            "EquipmentDropService should leave transient trait rolling to stable warehouse id assignment."
        );
    }

    private void TestEquipmentDropServiceIsPlainService()
    {
        _test.False(
            typeof(GodotObject).IsAssignableFrom(typeof(EquipmentDropService)),
            "EquipmentDropService 应保持 plain C# service，而不是 GodotObject wrapper。"
        );
    }

    private void AssertRarityRoll(
        EquipmentDropService service,
        string label,
        IEnumerable<int> rolls,
        int dropLuck,
        int expectedRarity
    )
    {
        FixedRollRng rng = new(rolls);
        service.SetRollRangeForTesting(rng.RollRange);

        int actualRarity = service.RollDropRarity(dropLuck);
        _test.Eq(actualRarity, expectedRarity, $"{label}。");
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
