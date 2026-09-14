using Godot;

internal sealed record EnemyBattleEquipmentDefinition(
    StringName SlotId,
    StringName ItemId,
    int Rarity,
    int CurrentDurability
);
