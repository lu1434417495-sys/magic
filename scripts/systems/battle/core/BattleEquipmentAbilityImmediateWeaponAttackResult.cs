using Godot;

internal sealed class BattleEquipmentAbilityImmediateWeaponAttackResult
{
    public StringName BindingId { get; init; } = "";
    public StringName ActionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public bool Applied { get; init; }
    public int Damage { get; init; }
}
