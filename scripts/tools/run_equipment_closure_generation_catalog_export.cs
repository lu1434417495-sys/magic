using System;
using Godot;

public partial class run_equipment_closure_generation_catalog_export : SceneTree
{
    public override void _Initialize() => ProcessFrame += ExportOnFirstFrame;

    private void ExportOnFirstFrame()
    {
        ProcessFrame -= ExportOnFirstFrame;
        try
        {
            ApplicationLifetimeCoordinator coordinator =
                Root.GetNode<ApplicationLifetimeCoordinator>(
                    "ApplicationLifetimeCoordinator"
                );
            EquipmentClosureGenerationCatalog catalog =
                EquipmentClosureGenerationCatalog.Build(
                    coordinator.ContentHost.GetSnapshot(),
                    coordinator.ContentHost.EngineAssets
                );
            ConsoleProcessOutput.WriteStandard(catalog.ToJson().TrimEnd());
            Quit(0);
        }
        catch (Exception exception)
        {
            ConsoleProcessOutput.WriteFailure(
                $"Equipment closure generation catalog export failed: {exception}"
            );
            Quit(1);
        }
    }
}
