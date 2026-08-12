using System.Collections.Generic;
using Godot;

public partial class run_equipment_drop_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestRollDropRarityHitsAllThresholdTiers();
        TestRollDropRarityAcceptsCallerClampedExtremes();
        TestRollItemInstancesApplyRarityDurabilityAndDeferTraits();

        RequestTestExit(_test.Finish("Equipment drop service regression"));
    }

    private void TestRollDropRarityHitsAllThresholdTiers()
    {
        AssertRarityRoll(
            "COMMON 档位上界应落在 9",
            new[] { 3, 3, 3 },
            0,
            (int)EquipmentInstanceState.RarityTier.COMMON
        );
        AssertRarityRoll(
            "UNCOMMON 档位门槛应落在 10",
            new[] { 4, 3, 3 },
            0,
            (int)EquipmentInstanceState.RarityTier.UNCOMMON
        );
        AssertRarityRoll(
            "RARE 档位门槛应落在 13",
            new[] { 5, 4, 4 },
            0,
            (int)EquipmentInstanceState.RarityTier.RARE
        );
        AssertRarityRoll(
            "EPIC 档位门槛应落在 16",
            new[] { 6, 5, 5 },
            0,
            (int)EquipmentInstanceState.RarityTier.EPIC
        );
        AssertRarityRoll(
            "LEGENDARY 档位门槛应落在 18",
            new[] { 6, 6, 6 },
            0,
            (int)EquipmentInstanceState.RarityTier.LEGENDARY
        );
    }

    private void TestRollDropRarityAcceptsCallerClampedExtremes()
    {
        AssertRarityRoll(
            "最低 drop_luck=-6 应直接参与 3d6 结果",
            new[] { 6, 6, 6 },
            -6,
            (int)EquipmentInstanceState.RarityTier.UNCOMMON
        );
        AssertRarityRoll(
            "最高 drop_luck=+5 应直接参与 3d6 结果",
            new[] { 1, 1, 1 },
            5,
            (int)EquipmentInstanceState.RarityTier.COMMON
        );
    }

    private void TestRollItemInstancesApplyRarityDurabilityAndDeferTraits()
    {
        EquipmentDropService service = new();
        FixedRollRng rng = new(new[] { 6, 6, 6, 1, 1, 1 });
        service.SetRollRangeForTesting(rng.RollRange);

        List<EquipmentInstanceState> instances = service.RollItemInstances("iron_sword", 2, 0);

        _test.Eq(instances.Count, 2, "RollItemInstances 应返回 typed 装备实例列表。");
        if (instances.Count != 2)
            return;

        EquipmentInstanceState legendary = instances[0];
        EquipmentInstanceState common = instances[1];
        _test.Eq(legendary.item_id, new StringName("iron_sword"), "第一件实例应保留 item_id。");
        _test.Eq(common.item_id, new StringName("iron_sword"), "第二件实例应保留 item_id。");
        _test.Eq(
            legendary.rarity,
            (int)EquipmentInstanceState.RarityTier.LEGENDARY,
            "第一组 6/6/6 应生成传奇实例。"
        );
        _test.Eq(
            common.rarity,
            (int)EquipmentInstanceState.RarityTier.COMMON,
            "第二组 1/1/1 应生成普通实例。"
        );
        _test.Eq(
            legendary.current_durability,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.LEGENDARY
            ),
            "传奇实例应写入对应的默认耐久。"
        );
        _test.Eq(
            common.current_durability,
            EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.COMMON
            ),
            "普通实例应写入对应的默认耐久。"
        );
        _test.Eq(
            legendary.trait_instances.Count,
            0,
            "传奇 transient 实例应把 trait rolling 留给稳定仓库 id 分配。"
        );
        _test.Eq(
            common.trait_instances.Count,
            0,
            "普通 transient 实例应把 trait rolling 留给稳定仓库 id 分配。"
        );
        _test.False(
            object.ReferenceEquals(legendary, common),
            "两次掉落实例必须是不同对象，不能复用同一 mutable state。"
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
