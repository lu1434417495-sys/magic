using System;
using Godot;

[GlobalClass]
public partial class GodotSharpCleanup : RefCounted
{
    // Headless tests that exercise many C# Variant boundaries can leave
    // Godot.Variant finalizers queued until after Godot native shutdown.
    // Drain them before SceneTree.quit() so godotsharp_variant_destroy still
    // runs while the native runtime is alive.
    public static void CollectPendingFinalizers()
    {
        GodotObjectLifecycle.CollectPendingFinalizers();
    }

    public static void DisposeGodotObject(GodotObject owned)
    {
        GodotObjectLifecycle.DisposeGodotObject(owned);
    }

    public static void DisposeGodotObject(BattleCommand command)
    {
        if (command == null)
            return;
        DisposeGodotObject(command.equipment_instance);
        command.equipment_instance = null;
    }

    public static void DisposeGodotObject(BattleEventBatch batch)
    {
        batch?.Dispose();
    }
}
