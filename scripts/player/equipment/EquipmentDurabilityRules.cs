using Godot;

[GlobalClass]
public partial class EquipmentDurabilityRules : RefCounted
{
    public const int RARITY_COMMON = 0;
    public const int RARITY_UNCOMMON = 1;
    public const int RARITY_RARE = 2;
    public const int RARITY_EPIC = 3;
    public const int RARITY_LEGENDARY = 4;

    private static readonly Godot.Collections.Dictionary MAX_DURABILITY_BY_RARITY = new()
    {
        { RARITY_COMMON, 56 },
        { RARITY_UNCOMMON, 84 },
        { RARITY_RARE, 120 },
        { RARITY_EPIC, 160 },
        { RARITY_LEGENDARY, 200 },
    };

    private static readonly Godot.Collections.Dictionary DISJUNCTION_SAVE_BONUS_BY_RARITY = new()
    {
        { RARITY_COMMON, 0 },
        { RARITY_UNCOMMON, 2 },
        { RARITY_RARE, 4 },
        { RARITY_EPIC, 6 },
        { RARITY_LEGENDARY, 8 },
    };

    public static int GetMaxDurabilityForRarity(int rarity) =>
        MAX_DURABILITY_BY_RARITY.ContainsKey(rarity)
            ? MAX_DURABILITY_BY_RARITY[rarity].AsInt32()
            : 56;

    public static int GetDefaultCurrentDurability(int rarity) => GetMaxDurabilityForRarity(rarity);

    public static int GetDisjunctionSaveBonusForRarity(int rarity) =>
        DISJUNCTION_SAVE_BONUS_BY_RARITY.ContainsKey(rarity)
            ? DISJUNCTION_SAVE_BONUS_BY_RARITY[rarity].AsInt32()
            : 0;

    public static bool IsValidCurrentDurability(int value, int rarity) =>
        value >= 1 && value <= GetMaxDurabilityForRarity(rarity);
}
