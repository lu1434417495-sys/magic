using Godot;

public static class GodotSharpCleanup
{
    public static void ClearRuntimeReferences(BattleCommand command)
    {
        if (command == null)
            return;
        command.equipment_instance = null;
    }

    public static void DisposeBatch(BattleEventBatch batch)
    {
        batch?.Dispose();
    }
}
