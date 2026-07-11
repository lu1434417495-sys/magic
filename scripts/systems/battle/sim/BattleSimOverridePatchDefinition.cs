using Godot;

internal sealed record BattleSimOverridePatchDefinition(
    string TargetType,
    StringName TargetId,
    StringName StateId,
    StringName ActionId,
    string Path,
    object Value
);
