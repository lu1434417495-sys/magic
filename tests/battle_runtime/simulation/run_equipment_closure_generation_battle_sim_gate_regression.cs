#nullable enable

using System;
using Godot;

public partial class run_equipment_closure_generation_battle_sim_gate_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunTests);
    }

    private void RunTests()
    {
        try
        {
            AssertFormalSamplingContract();
            RunProductionJsonClosureThroughRealGate();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected equipment closure BattleSim gate exception: {exception}");
        }
        RequestTestExit(_test.Finish("Equipment closure generation BattleSim gate regression"));
    }

    private void AssertFormalSamplingContract()
    {
        var options = new EquipmentClosureGenerationBattleSimOptions();
        _test.Eq(options.Seeds.Count, 12, "formal equipment sampling seed count");
        _test.Eq(
            options.MinimumCompletedSamples,
            12,
            "formal equipment sampling requires all configured seeds"
        );
        _test.Eq(options.MaximumSampledItems, 8, "formal batch sampling cap");
        _test.Eq(
            options.MaximumWinRateDeltaBasisPoints,
            5000,
            "formal high win-rate outlier threshold"
        );
        _test.Eq(
            options.MaximumDamageRatioBasisPoints,
            40000,
            "formal high damage-ratio outlier threshold"
        );
    }

    private void RunProductionJsonClosureThroughRealGate()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNode<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        ContentSnapshot snapshot = coordinator.ContentHost.GetSnapshot();
        var options = new EquipmentClosureGenerationBattleSimOptions(
            seeds: new[] { 84901, 84902 },
            minimumCompletedSamples: 2,
            maximumSampledItems: 1,
            maximumWinRateDeltaBasisPoints: 10000,
            maximumDamageRatioBasisPoints: 100000
        );
        var service = new EquipmentClosureGenerationValidationService(
            snapshot,
            new EquipmentClosureGenerationBattleSimGate(options),
            coordinator.ContentHost.EngineAssets.GetPublishedContentAssetIds<Texture2D>(),
            allowExistingIdReplacement: true
        );
        EquipmentClosureGenerationValidationReport report = service.Validate(
            EquipmentClosureGenerationSourceSet.Production,
            new GodotContentJsonSourceReader()
        );

        _test.True(
            report.Success,
            report.Success
                ? "production JSON closure passes the real four-stage gate"
                : EquipmentClosureGenerationValidationProtocol.FormatJson(report)
        );
        _test.Eq(report.Stages.Count, 4, "real gate reports all four stages");
        if (report.Stages.Count < 4)
            return;
        EquipmentClosureGenerationValidationStageReport simulation = report.Stages[3];
        _test.Eq(
            simulation.Stage,
            EquipmentClosureGenerationValidationStageKind.BattleSimulation,
            "last stage is BattleSim"
        );
        _test.Eq(
            simulation.ValidatedEntryCount,
            1,
            "real gate samples one deterministic production equipment item"
        );
        _test.True(
            simulation.Metrics.ContainsKey("sampled_item_count")
                && simulation.Metrics.ContainsKey("completed_candidate_sample_count"),
            "real gate publishes sampling metrics"
        );
        string protocol = EquipmentClosureGenerationValidationProtocol.FormatJson(report);
        _test.False(
            protocol.Contains(".tres", StringComparison.OrdinalIgnoreCase),
            "generation admission report exposes no TRES source dependency"
        );
    }
}
