using Godot;

[GlobalClass]
public partial class EquipmentDropService : RefCounted
{
    private static readonly Script EquipmentInstanceStateScript = GD.Load<Script>("res://scripts/player/warehouse/equipment_instance_state.gd");

    private Variant _rng;

    public EquipmentDropService()
        : this(default(Variant))
    {
    }

    public EquipmentDropService(Variant rng)
    {
        if (rng.VariantType != Variant.Type.Nil && rng.AsGodotObject()?.HasMethod("randi_range") == true)
            _rng = rng;
        else
        {
            var fallbackRng = new RandomNumberGenerator();
            fallbackRng.Randomize();
            _rng = Variant.From(fallbackRng);
        }
    }

    public Godot.Collections.Array roll_drops(StringName dropTableId, int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);
        var normalized = ProgressionDataUtils.to_string_name(dropTableId);
        if (normalized == "") return new Godot.Collections.Array();
        return new Godot.Collections.Array();
    }

    public int roll_drop_rarity(int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);
        return _resolve_rarity_from_score(_roll_3d6() + dropLuck);
    }

    public Godot.Collections.Array roll_item_instances(StringName itemId, int quantity, int dropLuck)
    {
        _assert_drop_luck_in_range(dropLuck);
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        int resolvedQuantity = Mathf.Max(quantity, 0);
        if (normalizedItemId == "" || resolvedQuantity <= 0) return new Godot.Collections.Array();
        var instances = new Godot.Collections.Array();
        for (int i = 0; i < resolvedQuantity; i++)
        {
            var instance = EquipmentInstanceStateScript.Call("create", normalizedItemId).AsGodotObject();
            int rarity = roll_drop_rarity(dropLuck);
            instance.Set("rarity", rarity);
            instance.Set("current_durability", EquipmentDurabilityRules.get_default_current_durability(rarity));
            instances.Add(instance);
        }
        return instances;
    }

    private int _roll_3d6()
    {
        var rngObj = _rng.AsGodotObject();
        return rngObj.Call("randi_range", 1, 6).AsInt32()
             + rngObj.Call("randi_range", 1, 6).AsInt32()
             + rngObj.Call("randi_range", 1, 6).AsInt32();
    }

    private static int _resolve_rarity_from_score(int rarityScore)
    {
        if (rarityScore >= 18) return (int)EquipmentInstanceStateScript.Get("RarityTier.LEGENDARY");
        if (rarityScore >= 16) return (int)EquipmentInstanceStateScript.Get("RarityTier.EPIC");
        if (rarityScore >= 13) return (int)EquipmentInstanceStateScript.Get("RarityTier.RARE");
        if (rarityScore >= 10) return (int)EquipmentInstanceStateScript.Get("RarityTier.UNCOMMON");
        return (int)EquipmentInstanceStateScript.Get("RarityTier.COMMON");
    }

    private static void _assert_drop_luck_in_range(int dropLuck)
    {
        System.Diagnostics.Debug.Assert(dropLuck >= -6 && dropLuck <= 5, "EquipmentDropService expects caller-clamped drop_luck in [-6, +5].");
    }
}
