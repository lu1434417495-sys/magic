using System;
using System.Collections.Generic;
using Godot;

public class EquipmentDropService
{
    private RandomNumberGenerator _rng;
    private Func<int, int, int> _rollRange;

    public EquipmentDropService()
        : this(null) { }

    public EquipmentDropService(RandomNumberGenerator rng)
    {
        ConfigureRng(rng);
    }

    private void ConfigureRng(RandomNumberGenerator rng)
    {
        if (rng != null)
        {
            _rng = rng;
        }
        else
        {
            var fallbackRng = new RandomNumberGenerator();

            fallbackRng.Randomize();

            _rng = fallbackRng;
        }

        _rollRange = _rng.RandiRange;
    }

    public void SetRngForTesting(RandomNumberGenerator rng)
    {
        ConfigureRng(rng);
    }

    internal void SetRollRangeForTesting(Func<int, int, int> rollRange)
    {
        _rollRange = rollRange ?? _rng.RandiRange;
    }

    public List<object> RollDrops(StringName dropTableId, int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);

        var normalized = ProgressionDataUtils.to_string_name(dropTableId);

        if (normalized == "")
            return new List<object>();

        return new List<object>();
    }

    public int RollDropRarity(int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);

        return _resolve_rarity_from_score(_roll_3d6() + dropLuck);
    }

    public virtual List<EquipmentInstanceState> RollItemInstances(
        StringName itemId,
        int quantity,
        int dropLuck
    )
    {
        _assert_drop_luck_in_range(dropLuck);

        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);

        int resolvedQuantity = Mathf.Max(quantity, 0);

        if (normalizedItemId == "" || resolvedQuantity <= 0)
            return new List<EquipmentInstanceState>();

        var instances = new List<EquipmentInstanceState>();

        for (int i = 0; i < resolvedQuantity; i++)
        {
            var instance = EquipmentInstanceState.CreateTransientInstance(normalizedItemId);
            int rarity = RollDropRarity(dropLuck);

            instance.rarity = rarity;
            instance.current_durability = EquipmentDurabilityRules.GetDefaultCurrentDurability(
                rarity
            );
            instances.Add(instance);
        }

        return instances;
    }

    private int _roll_3d6()
    {
        return _rollRange(1, 6) + _rollRange(1, 6) + _rollRange(1, 6);
    }

    private static int _resolve_rarity_from_score(int rarityScore)
    {
        if (rarityScore >= 18)
            return (int)EquipmentInstanceState.RarityTier.LEGENDARY;
        if (rarityScore >= 16)
            return (int)EquipmentInstanceState.RarityTier.EPIC;
        if (rarityScore >= 13)
            return (int)EquipmentInstanceState.RarityTier.RARE;
        if (rarityScore >= 10)
            return (int)EquipmentInstanceState.RarityTier.UNCOMMON;
        return (int)EquipmentInstanceState.RarityTier.COMMON;
    }

    private static void _assert_drop_luck_in_range(int dropLuck)
    {
        System.Diagnostics.Debug.Assert(
            dropLuck >= -6 && dropLuck <= 5,
            "EquipmentDropService expects caller-clamped drop_luck in [-6, +5]."
        );
    }
}
