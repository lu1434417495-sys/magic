using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleCounterattackSystem
    : IBattleAttackResolutionSink,
        IBattleReactionDrainOwner
{
    private readonly BattleRuntimeModule _runtime;
    private readonly BattleAttackActionCoordinator
        _attackActionCoordinator;
    private readonly BattleEffectExecutionContextService
        _effectExecutionContext;
    private readonly BattleCounterattackQueryService _queryService;
    private readonly BattleImmediateWeaponAttackService
        _immediateWeaponAttackService;
    private readonly IBattleCounterattackChanceRoller _chanceRoller;
    private readonly Queue<BattleCounterattackQueueEntry> _queue =
        new();
    private readonly HashSet<BattleCounterattackDedupeKey> _dedupe =
        new();
    private bool _isDraining;
    private bool _disposed;

    internal int PendingCount => _queue.Count;
    internal int DedupeCount => _dedupe.Count;
    internal bool IsDraining => _isDraining;

    internal BattleCounterattackSystem(
        BattleRuntimeModule runtime,
        BattleAttackActionCoordinator attackActionCoordinator,
        BattleEffectExecutionContextService effectExecutionContext,
        BattleCounterattackQueryService queryService,
        BattleImmediateWeaponAttackService immediateWeaponAttackService,
        IBattleCounterattackChanceRoller chanceRoller
    )
    {
        _runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        _attackActionCoordinator = attackActionCoordinator
            ?? throw new ArgumentNullException(
                nameof(attackActionCoordinator)
            );
        _effectExecutionContext = effectExecutionContext
            ?? throw new ArgumentNullException(
                nameof(effectExecutionContext)
            );
        _queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        _immediateWeaponAttackService = immediateWeaponAttackService
            ?? throw new ArgumentNullException(
                nameof(immediateWeaponAttackService)
            );
        _chanceRoller = chanceRoller
            ?? throw new ArgumentNullException(nameof(chanceRoller));
    }

    internal void DisposeRuntime()
    {
        if (_disposed)
            return;
        AbortBoundaryCore();
        _disposed = true;
    }

    private void RequireUsable()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(BattleCounterattackSystem)
            );
        }
    }

    void IBattleAttackResolutionSink.OnAttackResolved(
        in BattleAttackResolutionFact fact,
        BattleEventBatch batch
    )
    {
        RequireUsable();
        _attackActionCoordinator.RequireActiveRootBatch(batch);
        _attackActionCoordinator.RequireActiveLogicalAttack(
            fact.ActionId,
            fact.RootBoundaryId,
            fact.Origin,
            fact.DeliveryKind
        );
        _attackActionCoordinator.ConsumeWorkItem(
            BattleReactionWorkItemKind.AttackFact
        );
        if (
            !fact.ActionId.IsValid
            || fact.RootBoundaryId <= 0
            || fact.Origin == null
            || fact.AttackerUnitId == new StringName("")
            || fact.DefenderUnitId == new StringName("")
        )
        {
            throw new InvalidOperationException(
                "invalid attack resolution fact"
            );
        }
        if (!fact.Origin.CanTriggerReactions)
            return;
        if (
            !BattleCounterattackRules.TryMapTrigger(
                fact,
                out BattleCounterattackTriggerKind triggerKind
            )
        )
            return;

        BattleState state = _runtime.GetState()
            ?? throw new InvalidOperationException(
                "battle state is not bound"
            );
        if (
            !state.TryGetUnitTyped(
                fact.DefenderUnitId,
                out BattleUnitState defender
            )
            || defender == null
        )
        {
            return;
        }
        IReadOnlyList<BattleCounterattackCapability> candidates =
            defender.GetCounterattackCandidatesTyped(triggerKind);
        if (candidates.Count == 0)
            return;

        BattleCounterattackCapability capability = candidates[0];
        var key = new BattleCounterattackDedupeKey(
            fact.ActionId,
            fact.AttackerUnitId,
            fact.DefenderUnitId
        );
        if (!_dedupe.Add(key))
            return;
        _queue.Enqueue(
            new BattleCounterattackQueueEntry(fact, capability)
        );
    }

    void IBattleReactionDrainOwner.Drain(
        BattleEventBatch batch
    ) => DrainCore(batch);

    private void DrainCore(BattleEventBatch batch)
    {
        RequireUsable();
        _attackActionCoordinator.RequireActiveRootBatch(batch);
        if (_isDraining)
            return;

        _isDraining = true;
        bool completed = false;
        try
        {
            while (_queue.Count > 0)
            {
                _attackActionCoordinator.ConsumeWorkItem(
                    BattleReactionWorkItemKind.CounterattackDequeue
                );
                BattleCounterattackQueueEntry entry =
                    _queue.Dequeue();
                TryExecute(entry, batch);
            }
            completed = true;
        }
        finally
        {
            _isDraining = false;
            if (completed)
            {
                if (_queue.Count != 0)
                {
                    throw new InvalidOperationException(
                        "reaction queue did not drain"
                    );
                }
                _dedupe.Clear();
            }
        }
    }

    void IBattleReactionDrainOwner.AbortBoundary() =>
        AbortBoundaryCore();

    private void AbortBoundaryCore()
    {
        _queue.Clear();
        _dedupe.Clear();
        _isDraining = false;
    }

    private void TryExecute(
        in BattleCounterattackQueueEntry entry,
        BattleEventBatch batch
    )
    {
        RequireUsable();
        using IDisposable originScope =
            _effectExecutionContext.Push(
                BattleEffectOrigin.Counterattack(
                    entry.Fact.ActionId,
                    entry.Capability.InstanceId
                )
            );
        BattleCounterattackQueryResult query =
            _queryService.Build(entry);
        BattleCounterattackEligibility eligibility =
            BattleCounterattackRules.Evaluate(
                query.Evaluation
            );
        if (!eligibility.IsAllowed)
        {
            _runtime._append_report_entry_to_batch(
                batch,
                _runtime._report_formatter
                    .BuildCounterattackBlockedEntry(
                        entry,
                        eligibility,
                        query.Evaluation.AttackAvailability
                    )
            );
            return;
        }

        BattleImmediateWeaponAttackPlan plan =
            query.RequireExecutablePlan();
        if (
            !plan.SourceUnit
                .TryCommitCounterattackAttemptCostTyped(
                    plan.StaminaCost,
                    batch
                )
        )
        {
            throw new InvalidOperationException(
                "counterattack cost changed after read-only eligibility"
            );
        }

        int chancePercent = entry.Capability.ChancePercent;
        int chanceRoll = 0;
        bool chancePassed;
        if (chancePercent <= 0)
        {
            chancePassed = false;
        }
        else if (chancePercent >= 100)
        {
            chancePassed = true;
        }
        else
        {
            chanceRoll = _chanceRoller.RollInclusive1To100();
            if (chanceRoll < 1 || chanceRoll > 100)
            {
                throw new InvalidOperationException(
                    "counterattack chance roller returned out of range"
                );
            }
            chancePassed = chanceRoll <= chancePercent;
        }
        if (!chancePassed)
        {
            _runtime._append_report_entry_to_batch(
                batch,
                _runtime._report_formatter
                    .BuildCounterattackChanceFailedEntry(
                        entry,
                        chancePercent,
                        chanceRoll,
                        plan.StaminaCost
                    )
            );
            return;
        }

        _immediateWeaponAttackService.Execute(plan, batch);
    }
}
