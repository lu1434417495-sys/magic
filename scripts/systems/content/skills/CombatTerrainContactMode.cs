using Godot;

internal enum CombatTerrainContactMode
{
    Unknown = 0,
    None,
    InterruptMovementOnFailedSave,
}

internal static class CombatTerrainContactModeRules
{
    private static readonly StringName InterruptMovementOnFailedSave =
        "interrupt_movement_on_failed_save";

    internal static CombatTerrainContactMode ToMode(StringName value)
    {
        if (value == null || value == "")
        {
            return CombatTerrainContactMode.None;
        }
        return value == InterruptMovementOnFailedSave
            ? CombatTerrainContactMode.InterruptMovementOnFailedSave
            : CombatTerrainContactMode.Unknown;
    }

    internal static StringName ToStringName(CombatTerrainContactMode value) =>
        value == CombatTerrainContactMode.InterruptMovementOnFailedSave
            ? InterruptMovementOnFailedSave
            : new StringName("");
}
