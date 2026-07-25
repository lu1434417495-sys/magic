using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal abstract class SettlementModalContext
{
    private readonly Dictionary<string, object> _snapshot;

    protected SettlementModalContext(IReadOnlyDictionary<string, object> snapshot)
    {
        _snapshot = RuntimePlainPayload.CloneDictionary(snapshot);
    }

    protected SettlementModalContext(GDictionary boundaryPayload, string ownerPath)
    {
        _snapshot = RuntimePlainPayload.NormalizeDictionary(
            boundaryPayload,
            ownerPath
        );
    }

    internal bool IsEmpty => _snapshot.Count == 0;

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_snapshot);

    internal GodotProjectionLease<GDictionary> ProjectLease(
        string ownerId,
        string reason
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            _snapshot,
            ownerId,
            LifetimeDomain.Request,
            reason
        );
}

internal sealed class SettlementContractBoardContext : SettlementModalContext
{
    internal static SettlementContractBoardContext Empty { get; } =
        new((IReadOnlyDictionary<string, object>)null);

    private SettlementContractBoardContext(IReadOnlyDictionary<string, object> snapshot)
        : base(snapshot) { }

    private SettlementContractBoardContext(GDictionary payload)
        : base(payload, "SettlementContractBoardContext") { }

    internal static SettlementContractBoardContext FromBoundaryPayload(GDictionary payload) =>
        payload == null || payload.Count == 0 ? Empty : new(payload);

    internal static SettlementContractBoardContext FromSnapshot(
        IReadOnlyDictionary<string, object> snapshot
    ) => snapshot == null || snapshot.Count == 0 ? Empty : new(snapshot);
}

internal sealed class SettlementShopContext : SettlementModalContext
{
    internal static SettlementShopContext Empty { get; } =
        new((IReadOnlyDictionary<string, object>)null);

    private SettlementShopContext(IReadOnlyDictionary<string, object> snapshot)
        : base(snapshot) { }

    private SettlementShopContext(GDictionary payload)
        : base(payload, "SettlementShopContext") { }

    internal static SettlementShopContext FromBoundaryPayload(GDictionary payload) =>
        payload == null || payload.Count == 0 ? Empty : new(payload);

    internal static SettlementShopContext FromSnapshot(
        IReadOnlyDictionary<string, object> snapshot
    ) => snapshot == null || snapshot.Count == 0 ? Empty : new(snapshot);
}

internal sealed class SettlementForgeContext : SettlementModalContext
{
    internal static SettlementForgeContext Empty { get; } =
        new((IReadOnlyDictionary<string, object>)null);

    private SettlementForgeContext(IReadOnlyDictionary<string, object> snapshot)
        : base(snapshot) { }

    private SettlementForgeContext(GDictionary payload)
        : base(payload, "SettlementForgeContext") { }

    internal static SettlementForgeContext FromBoundaryPayload(GDictionary payload) =>
        payload == null || payload.Count == 0 ? Empty : new(payload);

    internal static SettlementForgeContext FromSnapshot(
        IReadOnlyDictionary<string, object> snapshot
    ) => snapshot == null || snapshot.Count == 0 ? Empty : new(snapshot);
}

internal sealed class SettlementStagecoachContext : SettlementModalContext
{
    internal static SettlementStagecoachContext Empty { get; } =
        new((IReadOnlyDictionary<string, object>)null);

    private SettlementStagecoachContext(IReadOnlyDictionary<string, object> snapshot)
        : base(snapshot) { }

    private SettlementStagecoachContext(GDictionary payload)
        : base(payload, "SettlementStagecoachContext") { }

    internal static SettlementStagecoachContext FromBoundaryPayload(GDictionary payload) =>
        payload == null || payload.Count == 0 ? Empty : new(payload);

    internal static SettlementStagecoachContext FromSnapshot(
        IReadOnlyDictionary<string, object> snapshot
    ) => snapshot == null || snapshot.Count == 0 ? Empty : new(snapshot);
}
