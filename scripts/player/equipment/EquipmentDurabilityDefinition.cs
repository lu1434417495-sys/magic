internal static class EquipmentDurabilityDefinition
{
    internal static int GetMaxDurabilityForRarity(int rarity) =>
        rarity switch
        {
            1 => 84,
            2 => 120,
            3 => 160,
            4 => 200,
            _ => 56,
        };

    internal static bool IsValidCurrentDurability(int value, int rarity) =>
        value >= 1 && value <= GetMaxDurabilityForRarity(rarity);
}
