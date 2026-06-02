public static class EquipmentDurabilityRules
{
    public const int RARITY_COMMON = 0;
    public const int RARITY_UNCOMMON = 1;
    public const int RARITY_RARE = 2;
    public const int RARITY_EPIC = 3;
    public const int RARITY_LEGENDARY = 4;

    public static int GetMaxDurabilityForRarity(int rarity) =>
        rarity switch
        {
            RARITY_UNCOMMON => 84,
            RARITY_RARE => 120,
            RARITY_EPIC => 160,
            RARITY_LEGENDARY => 200,
            _ => 56,
        };

    public static int GetDefaultCurrentDurability(int rarity) => GetMaxDurabilityForRarity(rarity);

    public static int GetDisjunctionSaveBonusForRarity(int rarity) =>
        rarity switch
        {
            RARITY_UNCOMMON => 2,
            RARITY_RARE => 4,
            RARITY_EPIC => 6,
            RARITY_LEGENDARY => 8,
            _ => 0,
        };

    public static bool IsValidCurrentDurability(int value, int rarity) =>
        value >= 1 && value <= GetMaxDurabilityForRarity(rarity);
}
