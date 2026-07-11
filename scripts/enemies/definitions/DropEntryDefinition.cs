using Godot;

internal sealed record DropEntryDefinition(
    StringName DropEntryId,
    StringName DropType,
    StringName ItemId,
    int Quantity
);
