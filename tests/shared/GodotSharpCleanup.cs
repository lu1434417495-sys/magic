using System;
using Godot;

[GlobalClass]
public partial class GodotSharpCleanup : RefCounted
{
    // Headless tests that exercise many C# Variant boundaries can leave
    // Godot.Variant finalizers queued until after Godot native shutdown.
    // Drain them before SceneTree.quit() so godotsharp_variant_destroy still
    // runs while the native runtime is alive.
    public static void collect_pending_finalizers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
