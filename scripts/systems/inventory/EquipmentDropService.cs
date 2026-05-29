using Godot;

[GlobalClass]
public partial class EquipmentDropService : RefCounted
{
    private GodotObject _rng;

    public EquipmentDropService()
        : this(null) { }

    public EquipmentDropService(GodotObject rng)
    {
        ConfigureRng(rng);
    }

    private void ConfigureRng(GodotObject rng)
    {
        if (rng != null && rng.HasMethod("randi_range"))
        {
            _rng = rng;
        }
        else
        {
            var fallbackRng = new RandomNumberGenerator();

            fallbackRng.Randomize();

            _rng = fallbackRng;
        }
    }

    public void set_rng_for_testing(GodotObject rng)
    {
        ConfigureRng(rng);
    }

    public Godot.Collections.Array roll_drops(StringName dropTableId, int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);

        var normalized = ProgressionDataUtils.to_string_name(dropTableId);

        if (normalized == "")
            return new Godot.Collections.Array();

        return new Godot.Collections.Array();
    }

    public int roll_drop_rarity(int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);

        return _resolve_rarity_from_score(_roll_3d6() + dropLuck);
    }

    public Godot.Collections.Array roll_item_instances(
        StringName itemId,
        int quantity,
        int dropLuck
    )
    {
        _assert_drop_luck_in_range(dropLuck);

        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);

        int resolvedQuantity = Mathf.Max(quantity, 0);

        if (normalizedItemId == "" || resolvedQuantity <= 0)
            return new Godot.Collections.Array();

        var instances = new Godot.Collections.Array();

        for (int i = 0; i < resolvedQuantity; i++)
        {
            var instance = EquipmentInstanceState.create(normalizedItemId, default);
            int rarity = roll_drop_rarity(dropLuck);

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
        return _rng.Call("randi_range", 1, 6).AsInt32()
            + _rng.Call("randi_range", 1, 6).AsInt32()
            + _rng.Call("randi_range", 1, 6).AsInt32();
    }

    private static int _resolve_rarity_from_score(int rarityScore)
    {
        if (rarityScore >= 18)
            return EquipmentInstanceState.RARITY_TIER_LEGENDARY();
        if (rarityScore >= 16)
            return EquipmentInstanceState.RARITY_TIER_EPIC();
        if (rarityScore >= 13)
            return EquipmentInstanceState.RARITY_TIER_RARE();
        if (rarityScore >= 10)
            return EquipmentInstanceState.RARITY_TIER_UNCOMMON();
        return EquipmentInstanceState.RARITY_TIER_COMMON();
    }

    private static void _assert_drop_luck_in_range(int dropLuck)
    {
        System.Diagnostics.Debug.Assert(
            dropLuck >= -6 && dropLuck <= 5,
            "EquipmentDropService expects caller-clamped drop_luck in [-6, +5]."
        );
    }
}
