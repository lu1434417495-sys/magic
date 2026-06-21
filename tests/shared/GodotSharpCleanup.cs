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
        GodotRefCountedDisposer.SuppressKeptBorrowedResourceFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static void DisposeGodotObject(GodotObject owned)
    {
        if (owned == null)
        {
            return;
        }
        GC.SuppressFinalize(owned);
        if (GodotObject.IsInstanceValid(owned))
        {
            DisposeTyped(owned);
        }
    }

    private static void DisposeTyped(GodotObject owned)
    {
        switch (owned)
        {
            case RefCounted refCounted:
                GodotRefCountedDisposer.DisposeIfValid(refCounted);
                return;
            case GameSession gameSession:
                gameSession.Dispose();
                return;
            default:
                owned.Dispose();
                return;
        }
    }
}
