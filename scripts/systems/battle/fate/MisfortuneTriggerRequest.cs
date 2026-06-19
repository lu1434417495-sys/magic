using System.Collections.Generic;
using Godot;

internal readonly record struct MisfortuneTriggerRequest
{
    private MisfortuneTriggerRequest(
        StringName reasonId,
        BattleUnitState unitState,
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds,
        BattleUnitState defeatedUnit,
        IReadOnlyList<BattleUnitState> adjacentUnits,
        StringName memberId,
        StringName phaseId
    )
    {
        ReasonId = ProgressionDataUtils.to_string_name(reasonId);
        UnitState = unitState;
        TargetUnit = targetUnit;
        StatusEffectIds = CopyStringNames(statusEffectIds);
        DefeatedUnit = defeatedUnit;
        AdjacentUnits = CopyUnits(adjacentUnits);
        MemberId = ProgressionDataUtils.to_string_name(memberId);
        PhaseId = ProgressionDataUtils.to_string_name(phaseId);
    }

    public StringName ReasonId { get; }
    public BattleUnitState UnitState { get; }
    public BattleUnitState TargetUnit { get; }
    public IReadOnlyList<StringName> StatusEffectIds { get; }
    public BattleUnitState DefeatedUnit { get; }
    public IReadOnlyList<BattleUnitState> AdjacentUnits { get; }
    public StringName MemberId { get; }
    public StringName PhaseId { get; }

    public static MisfortuneTriggerRequest StrongDebuff(
        BattleUnitState targetUnit,
        IEnumerable<StringName> statusEffectIds
    ) =>
        new(
            "strong_debuff",
            null,
            targetUnit,
            CopyStringNames(statusEffectIds),
            null,
            null,
            default,
            default
        );

    public static MisfortuneTriggerRequest AdjacentAllyDefeated(
        BattleUnitState defeatedUnit,
        IEnumerable<BattleUnitState> adjacentUnits
    ) =>
        new(
            "adjacent_ally_defeated",
            null,
            null,
            null,
            defeatedUnit,
            CopyUnits(adjacentUnits),
            default,
            default
        );

    public static MisfortuneTriggerRequest LowHpTurnEnd(BattleUnitState unitState) =>
        new("low_hp_end_turn", unitState, null, null, null, null, default, default);

    public static MisfortuneTriggerRequest BossPhaseChanged(
        BattleUnitState unitState,
        StringName phaseId = default
    ) =>
        new("boss_phase_changed", unitState, null, null, null, null, default, phaseId);

    public static MisfortuneTriggerRequest OrdinaryMiss(StringName memberId) =>
        new("ordinary_miss", null, null, null, null, null, memberId, default);

    public static MisfortuneTriggerRequest OrdinaryMiss(BattleUnitState unitState) =>
        new("ordinary_miss", unitState, null, null, null, null, default, default);

    public static MisfortuneTriggerRequest CriticalFail(StringName memberId) =>
        new("critical_fail", null, null, null, null, null, memberId, default);

    public static MisfortuneTriggerRequest CriticalFail(BattleUnitState unitState) =>
        new("critical_fail", unitState, null, null, null, null, default, default);

    public static MisfortuneTriggerRequest FromFateEvent(BattleFateEventPayload payload)
    {
        if (payload == null)
        {
            return default;
        }
        StringName eventType = ProgressionDataUtils.to_string_name(payload.EventType);
        if (eventType == "ordinary_miss")
        {
            return OrdinaryMiss(payload.AttackerMemberId);
        }
        if (eventType == "critical_fail")
        {
            return CriticalFail(payload.AttackerMemberId);
        }
        return default;
    }

    private static IReadOnlyList<StringName> CopyStringNames(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "" && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static IReadOnlyList<BattleUnitState> CopyUnits(IEnumerable<BattleUnitState> values)
    {
        var result = new List<BattleUnitState>();
        if (values == null)
        {
            return result;
        }
        foreach (BattleUnitState value in values)
        {
            if (value != null && !result.Contains(value))
            {
                result.Add(value);
            }
        }
        return result;
    }
}
