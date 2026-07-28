using System;
using System.Collections.Generic;

internal sealed class BattleAttackActionCoordinator : IDisposable
{
    private readonly BattleEffectExecutionContextService _effectContext;
    private readonly BattleReactionBoundaryLimits _limits;
    private readonly List<BoundaryFrame> _boundaries = new();
    private readonly List<LogicalFrame> _logicalAttacks = new();

    private IBattleReactionDrainOwner _drainOwner;
    private BattleEventBatch _rootBatch;
    private long _activeRootBoundaryId;
    private long _nextRootBoundaryId = 1;
    private long _nextActionId = 1;
    private long _nextScopeId = 1;
    private long _generation = 1;
    private int _workItemCount;
    private bool _accepting = true;
    private bool _rootFailed;

    internal BattleAttackActionCoordinator(
        BattleEffectExecutionContextService effectContext,
        BattleReactionBoundaryLimits limits
    )
    {
        _effectContext = effectContext
            ?? throw new ArgumentNullException(nameof(effectContext));
        if (
            limits.MaxNestedBoundaryDepth <= 0
            || limits.MaxWorkItems <= 0
        )
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }
        _limits = limits;
    }

    internal bool HasActiveBoundary => _boundaries.Count > 0;

    internal void BindDrainOwner(
        IBattleReactionDrainOwner drainOwner
    )
    {
        ArgumentNullException.ThrowIfNull(drainOwner);
        if (ReferenceEquals(_drainOwner, drainOwner))
            return;
        if (HasActiveBoundary)
        {
            throw new InvalidOperationException(
                "cannot bind drain owner in a boundary"
            );
        }
        if (_drainOwner != null)
        {
            throw new InvalidOperationException(
                "reaction drain owner is already bound"
            );
        }
        _drainOwner = drainOwner;
    }

    internal BattleReactionBoundaryScope BeginReactionBoundary(
        BattleEventBatch batch
    )
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!_accepting)
        {
            throw new ObjectDisposedException(
                nameof(BattleAttackActionCoordinator)
            );
        }
        if (_rootFailed)
        {
            throw new InvalidOperationException(
                "active reaction root has failed"
            );
        }

        int nextDepth = checked(_boundaries.Count + 1);
        if (nextDepth > _limits.MaxNestedBoundaryDepth)
        {
            FailRoot();
            throw new InvalidOperationException(
                $"reaction boundary depth {nextDepth} exceeds "
                + $"{_limits.MaxNestedBoundaryDepth}"
            );
        }

        if (_boundaries.Count == 0)
        {
            _rootBatch = batch;
            _activeRootBoundaryId =
                checked(_nextRootBoundaryId++);
            _workItemCount = 0;
            _rootFailed = false;
        }
        else
        {
            RequireActiveRootBatch(batch);
        }

        long scopeId = checked(_nextScopeId++);
        long generation = _generation;
        _boundaries.Add(
            new BoundaryFrame(
                scopeId,
                _effectContext.Depth,
                _logicalAttacks.Count
            )
        );
        return new BattleReactionBoundaryScope(
            this,
            scopeId,
            _activeRootBoundaryId,
            generation
        );
    }

    internal BattleLogicalAttackScope BeginLogicalAttack(
        BattleAttackDeliveryKind deliveryKind
    )
    {
        RequireActiveBoundary();
        if (_rootFailed)
        {
            throw new InvalidOperationException(
                "active reaction root has failed"
            );
        }
        if (deliveryKind == BattleAttackDeliveryKind.Unknown)
        {
            throw new ArgumentException(
                "attack delivery kind is required"
            );
        }
        ConsumeWorkItem(BattleReactionWorkItemKind.LogicalAttack);

        BattleEffectOrigin currentOrigin =
            _effectContext.RequireCurrentForAttack();
        var context = new BattleAttackActionContext(
            new BattleAttackActionId(checked(_nextActionId++)),
            _activeRootBoundaryId,
            currentOrigin,
            deliveryKind
        );

        long scopeId = checked(_nextScopeId++);
        long generation = _generation;
        _logicalAttacks.Add(new LogicalFrame(scopeId, context));
        return new BattleLogicalAttackScope(
            this,
            scopeId,
            generation,
            context
        );
    }

    internal void RequireActiveRootBatch(BattleEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        RequireActiveBoundary();
        if (!ReferenceEquals(_rootBatch, batch))
        {
            FailRoot();
            throw new InvalidOperationException(
                "reaction boundary received a different event batch"
            );
        }
    }

    internal void RequireActiveBoundary()
    {
        if (_boundaries.Count == 0 || _rootBatch == null)
        {
            throw new InvalidOperationException(
                "no active reaction boundary"
            );
        }
    }

    internal void RequireActiveLogicalAttack(
        BattleAttackActionId actionId,
        long rootBoundaryId,
        BattleEffectOrigin origin,
        BattleAttackDeliveryKind deliveryKind
    )
    {
        RequireActiveBoundary();
        if (_rootFailed)
        {
            throw new InvalidOperationException(
                "active reaction root has failed"
            );
        }
        if (_logicalAttacks.Count == 0)
        {
            FailRoot();
            throw new InvalidOperationException(
                "attack fact was published without an active logical attack"
            );
        }
        BattleAttackActionContext current =
            _logicalAttacks[^1].Context;
        if (
            !actionId.IsValid
            || rootBoundaryId != _activeRootBoundaryId
            || current.ActionId != actionId
            || current.RootBoundaryId != rootBoundaryId
            || !ReferenceEquals(current.Origin, origin)
            || current.DeliveryKind != deliveryKind
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "attack fact does not match the active logical attack"
            );
        }
    }

    internal void ConsumeWorkItem(
        BattleReactionWorkItemKind kind
    )
    {
        RequireActiveBoundary();
        int nextCount = checked(_workItemCount + 1);
        if (nextCount > _limits.MaxWorkItems)
        {
            FailRoot();
            throw new InvalidOperationException(
                $"reaction root {_activeRootBoundaryId} exceeded "
                + $"{_limits.MaxWorkItems} work items before {kind}; "
                + $"depth={_boundaries.Count}, "
                + $"consumed={_workItemCount}"
            );
        }
        _workItemCount = nextCount;
    }

    internal void AbortActiveBoundary()
    {
        if (!HasActiveBoundary)
            return;
        FailRoot();
    }

    internal void StopAcceptingAndAbort()
    {
        _accepting = false;
        _rootFailed = true;
        _drainOwner?.AbortBoundary();
        _logicalAttacks.Clear();
        _boundaries.Clear();
        _generation = checked(_generation + 1);
        ResetRootFields();
    }

    internal void ResetForBattle()
    {
        if (
            _boundaries.Count != 0
            || _logicalAttacks.Count != 0
        )
        {
            throw new InvalidOperationException(
                "cannot reset reaction coordinator with active scopes"
            );
        }
        _drainOwner?.AbortBoundary();
        ResetRootFields();
        _generation = checked(_generation + 1);
        _nextRootBoundaryId = 1;
        _nextActionId = 1;
        _nextScopeId = 1;
        _accepting = true;
        _rootFailed = false;
    }

    public void Dispose()
    {
        StopAcceptingAndAbort();
        _drainOwner = null;
    }

    internal void CompleteBoundary(
        long scopeId,
        long rootBoundaryId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        BoundaryFrame frame = RequireTopBoundary(
            scopeId,
            rootBoundaryId
        );
        if (frame.Completed)
        {
            throw new InvalidOperationException(
                "reaction boundary completed twice"
            );
        }
        if (_rootFailed)
        {
            throw new InvalidOperationException(
                "failed reaction root cannot complete"
            );
        }
        if (
            _logicalAttacks.Count
            != frame.LogicalDepthAtEntry
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "logical attack scope escaped its reaction boundary"
            );
        }

        if (_boundaries.Count == 1)
        {
            IBattleReactionDrainOwner drainOwner = _drainOwner
                ?? throw new InvalidOperationException(
                    "reaction drain owner is not bound"
                );
            try
            {
                drainOwner.Drain(_rootBatch);
            }
            catch
            {
                FailRoot();
                throw;
            }
            if (_logicalAttacks.Count != 0)
            {
                FailRoot();
                throw new InvalidOperationException(
                    "logical attack stack is not empty after reaction drain"
                );
            }
        }
        frame.Completed = true;
    }

    internal void DisposeBoundary(
        long scopeId,
        long rootBoundaryId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        BoundaryFrame frame = RequireTopBoundary(
            scopeId,
            rootBoundaryId
        );
        bool rootAlreadyFailed = _rootFailed;
        bool originDepthMismatch =
            _effectContext.Depth != frame.OriginDepthAtEntry;
        bool logicalDepthMismatch =
            _logicalAttacks.Count
            != frame.LogicalDepthAtEntry;
        bool failed =
            !frame.Completed
            || originDepthMismatch
            || logicalDepthMismatch;
        if (failed)
            FailRoot();

        _boundaries.RemoveAt(_boundaries.Count - 1);
        bool exitedRoot = _boundaries.Count == 0;
        if (exitedRoot)
        {
            _logicalAttacks.Clear();
            ResetRootFields();
        }

        if (rootAlreadyFailed && !frame.Completed)
            return;
        if (originDepthMismatch || logicalDepthMismatch)
        {
            throw new InvalidOperationException(
                "reaction boundary did not restore origin/action depth"
            );
        }
        if (!frame.Completed)
        {
            throw new InvalidOperationException(
                "reaction boundary disposed without Complete()"
            );
        }
    }

    internal void CompleteLogicalAttack(
        long scopeId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        LogicalFrame frame = RequireTopLogical(scopeId);
        if (frame.Completed)
        {
            throw new InvalidOperationException(
                "logical attack completed twice"
            );
        }
        frame.Completed = true;
    }

    internal void DisposeLogicalAttack(
        long scopeId,
        long generation
    )
    {
        if (generation != _generation)
            return;
        LogicalFrame frame = RequireTopLogical(scopeId);
        bool rootAlreadyFailed = _rootFailed;
        bool completed = frame.Completed;
        _logicalAttacks.RemoveAt(
            _logicalAttacks.Count - 1
        );
        if (completed)
            return;
        FailRoot();
        if (rootAlreadyFailed)
            return;
        throw new InvalidOperationException(
            "logical attack disposed without Complete()"
        );
    }

    private BoundaryFrame RequireTopBoundary(
        long scopeId,
        long rootBoundaryId
    )
    {
        RequireActiveBoundary();
        if (
            rootBoundaryId != _activeRootBoundaryId
            || _boundaries[^1].ScopeId != scopeId
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "reaction boundaries must complete/dispose in LIFO order"
            );
        }
        return _boundaries[^1];
    }

    private LogicalFrame RequireTopLogical(long scopeId)
    {
        if (
            _logicalAttacks.Count == 0
            || _logicalAttacks[^1].ScopeId != scopeId
        )
        {
            FailRoot();
            throw new InvalidOperationException(
                "logical attacks must complete/dispose in LIFO order"
            );
        }
        return _logicalAttacks[^1];
    }

    private void FailRoot()
    {
        _rootFailed = true;
        _drainOwner?.AbortBoundary();
    }

    private void ResetRootFields()
    {
        _rootBatch = null;
        _activeRootBoundaryId = 0;
        _workItemCount = 0;
        _rootFailed = false;
    }

    private sealed class BoundaryFrame
    {
        internal BoundaryFrame(
            long scopeId,
            int originDepthAtEntry,
            int logicalDepthAtEntry
        )
        {
            ScopeId = scopeId;
            OriginDepthAtEntry = originDepthAtEntry;
            LogicalDepthAtEntry = logicalDepthAtEntry;
        }

        internal long ScopeId { get; }
        internal int OriginDepthAtEntry { get; }
        internal int LogicalDepthAtEntry { get; }
        internal bool Completed { get; set; }
    }

    private sealed class LogicalFrame
    {
        internal LogicalFrame(
            long scopeId,
            BattleAttackActionContext context
        )
        {
            ScopeId = scopeId;
            Context = context;
        }

        internal long ScopeId { get; }
        internal BattleAttackActionContext Context { get; }
        internal bool Completed { get; set; }
    }
}

internal sealed class BattleReactionBoundaryScope : IDisposable
{
    private BattleAttackActionCoordinator _owner;
    private readonly long _scopeId;
    private readonly long _rootBoundaryId;
    private readonly long _generation;

    internal BattleReactionBoundaryScope(
        BattleAttackActionCoordinator owner,
        long scopeId,
        long rootBoundaryId,
        long generation
    )
    {
        _owner = owner;
        _scopeId = scopeId;
        _rootBoundaryId = rootBoundaryId;
        _generation = generation;
    }

    internal void Complete()
    {
        BattleAttackActionCoordinator owner = _owner
            ?? throw new ObjectDisposedException(GetType().Name);
        owner.CompleteBoundary(
            _scopeId,
            _rootBoundaryId,
            _generation
        );
    }

    public void Dispose()
    {
        BattleAttackActionCoordinator owner = _owner;
        if (owner == null)
            return;
        _owner = null;
        owner.DisposeBoundary(
            _scopeId,
            _rootBoundaryId,
            _generation
        );
    }
}

internal sealed class BattleLogicalAttackScope : IDisposable
{
    private BattleAttackActionCoordinator _owner;
    private readonly long _scopeId;
    private readonly long _generation;

    internal BattleLogicalAttackScope(
        BattleAttackActionCoordinator owner,
        long scopeId,
        long generation,
        BattleAttackActionContext context
    )
    {
        _owner = owner;
        _scopeId = scopeId;
        _generation = generation;
        Context = context
            ?? throw new ArgumentNullException(nameof(context));
    }

    internal BattleAttackActionContext Context { get; }

    internal void Complete()
    {
        BattleAttackActionCoordinator owner = _owner
            ?? throw new ObjectDisposedException(GetType().Name);
        owner.CompleteLogicalAttack(_scopeId, _generation);
    }

    public void Dispose()
    {
        BattleAttackActionCoordinator owner = _owner;
        if (owner == null)
            return;
        _owner = null;
        owner.DisposeLogicalAttack(_scopeId, _generation);
    }
}
