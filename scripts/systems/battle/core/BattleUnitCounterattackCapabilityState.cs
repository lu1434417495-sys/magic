using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal readonly record struct BattleUnitCounterattackCapabilitySnapshot(
    bool OwnerPresent,
    IReadOnlyList<BattleCounterattackCapability> Values
)
{
    internal static BattleUnitCounterattackCapabilitySnapshot MissingOwner =>
        new(false, Array.Empty<BattleCounterattackCapability>());
}

internal sealed class BattleUnitCounterattackCapabilityState
{
    private BattleCounterattackCapability[] _ordered =
        Array.Empty<BattleCounterattackCapability>();
    private Dictionary<StringName, BattleCounterattackCapability> _byId =
        new();
    private Dictionary<
        BattleCounterattackTriggerKind,
        IReadOnlyList<BattleCounterattackCapability>
    > _byTrigger = new();

    internal void ReplaceAll(
        IReadOnlyList<BattleCounterattackCapability> values
    )
    {
        var byId =
            new Dictionary<StringName, BattleCounterattackCapability>();
        foreach (
            BattleCounterattackCapability value
                in values ?? Array.Empty<BattleCounterattackCapability>()
        )
        {
            if (
                value.InstanceId == new StringName("")
                || value.WeaponActionDefinitionId == new StringName("")
                || !Enum.IsDefined(value.TriggerKind)
                || value.ChancePercent < 0
                || value.ChancePercent > 100
            )
            {
                throw new ArgumentException(
                    "counterattack capability is invalid",
                    nameof(values)
                );
            }
            if (!byId.TryAdd(value.InstanceId, value))
            {
                throw new ArgumentException(
                    $"duplicate counterattack capability {value.InstanceId}",
                    nameof(values)
                );
            }
        }

        BattleCounterattackCapability[] ordered =
            byId.Values.ToArray();
        Array.Sort(
            ordered,
            static (left, right) =>
            {
                int priorityOrder =
                    right.SelectionPriority.CompareTo(
                        left.SelectionPriority
                    );
                return priorityOrder != 0
                    ? priorityOrder
                    : string.Compare(
                        left.InstanceId.ToString(),
                        right.InstanceId.ToString(),
                        StringComparison.Ordinal
                    );
            }
        );
        var byTrigger = new Dictionary<
            BattleCounterattackTriggerKind,
            IReadOnlyList<BattleCounterattackCapability>
        >();
        foreach (
            BattleCounterattackTriggerKind triggerKind
                in Enum.GetValues<BattleCounterattackTriggerKind>()
        )
        {
            byTrigger[triggerKind] = Array.AsReadOnly(
                ordered
                    .Where(value => value.TriggerKind == triggerKind)
                    .ToArray()
            );
        }
        _byId = byId;
        _ordered = ordered;
        _byTrigger = byTrigger;
    }

    internal bool TryGetCapability(
        StringName instanceId,
        out BattleCounterattackCapability capability
    )
    {
        if (instanceId == new StringName(""))
        {
            capability = default;
            return false;
        }
        return _byId.TryGetValue(instanceId, out capability);
    }

    internal IReadOnlyList<BattleCounterattackCapability> GetCandidates(
        BattleCounterattackTriggerKind triggerKind
    )
    {
        if (!Enum.IsDefined(triggerKind))
            return Array.Empty<BattleCounterattackCapability>();
        return _byTrigger.TryGetValue(
            triggerKind,
            out IReadOnlyList<BattleCounterattackCapability> candidates
        )
            ? candidates
            : Array.Empty<BattleCounterattackCapability>();
    }

    internal BattleUnitCounterattackCapabilitySnapshot CaptureRaw() =>
        new(true, Array.AsReadOnly(_ordered.ToArray()));

    internal void RestoreRaw(
        BattleUnitCounterattackCapabilitySnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            throw new ArgumentException(
                "missing-owner snapshot must be restored by BattleUnitState"
            );
        }
        ReplaceAll(snapshot.Values);
    }

    internal BattleUnitCounterattackCapabilityState DuplicateState()
    {
        var duplicate = new BattleUnitCounterattackCapabilityState();
        duplicate.ReplaceAll(_ordered);
        return duplicate;
    }
}
