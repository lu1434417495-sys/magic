using System;
using System.Collections.Generic;

internal readonly record struct BattleRuntimeModuleBorrowerTopologySnapshot(
    string Signature,
    int RegisteredCount,
    int BoundCount,
    int ActiveDependencyCount
);

internal abstract class BattleRuntimeModuleBorrower
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;

    protected BattleRuntimeModule _runtime =>
        _runtimeRef != null
        && _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime)
            ? runtime
            : null;

    internal void Setup(BattleRuntimeModule runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (IsBoundTo(runtime))
            return;
        _runtimeRef = new WeakReference<BattleRuntimeModule>(runtime);
    }

    internal bool IsBoundTo(BattleRuntimeModule runtime) =>
        runtime != null && ReferenceEquals(_runtime, runtime);

    internal int ActiveDependencyCount => _runtime != null ? 1 : 0;

    internal virtual void DisposeRuntime()
    {
        _runtimeRef = null;
    }
}

internal sealed class BattleRuntimeModuleBorrowerSet
{
    private readonly BattleRuntimeModuleBorrower[] _borrowers;
    private readonly string _topologySignature;

    internal BattleSpawnPlacementService SpawnPlacement { get; } = new();
    internal BattleSpecialSkillGateService SpecialSkillGate { get; } = new();
    internal BattleMovementCommandService MovementCommand { get; } = new();
    internal BattleMetricsReportService MetricsReport { get; } = new();
    internal BattleContingencyBridgeService ContingencyBridge { get; } = new();
    internal BattleCommandPreviewService CommandPreview { get; } = new();
    internal BattleAiDecisionBindingService AiDecisionBinding { get; } = new();

    internal BattleRuntimeModuleBorrowerSet()
    {
        // Forward order is dependency-first. Teardown runs this list in reverse,
        // so the AI borrower releases its preview/movement dependencies first.
        _borrowers =
        [
            SpawnPlacement,
            SpecialSkillGate,
            MovementCommand,
            MetricsReport,
            ContingencyBridge,
            CommandPreview,
            AiDecisionBinding,
        ];
        var typeNames = new string[_borrowers.Length];
        for (int index = 0; index < _borrowers.Length; index++)
            typeNames[index] = _borrowers[index].GetType().Name;
        _topologySignature = string.Join(">", typeNames);
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        int setupCount = 0;
        try
        {
            for (; setupCount < _borrowers.Length; setupCount++)
                _borrowers[setupCount].Setup(runtime);
        }
        catch
        {
            Exception cleanupFailure = null;
            for (int index = setupCount - 1; index >= 0; index--)
            {
                BattleRuntimeModule.RunTeardownStep(
                    ref cleanupFailure,
                    _borrowers[index].DisposeRuntime
                );
            }
            throw;
        }
    }

    internal void DisposeRuntime(ref Exception firstFailure)
    {
        for (int index = _borrowers.Length - 1; index >= 0; index--)
        {
            BattleRuntimeModule.RunTeardownStep(
                ref firstFailure,
                _borrowers[index].DisposeRuntime
            );
        }
    }

    internal BattleRuntimeModuleBorrowerTopologySnapshot CaptureTopology(
        BattleRuntimeModule runtime
    )
    {
        int boundCount = 0;
        int activeDependencyCount = 0;
        foreach (BattleRuntimeModuleBorrower borrower in _borrowers)
        {
            if (borrower.IsBoundTo(runtime))
                boundCount++;
            activeDependencyCount += borrower.ActiveDependencyCount;
        }
        return new BattleRuntimeModuleBorrowerTopologySnapshot(
            _topologySignature,
            _borrowers.Length,
            boundCount,
            activeDependencyCount
        );
    }
}
