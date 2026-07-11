using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleRuntimeModule — AI turn-trace capture/snapshot projection + score profiles + metrics.
// Pure physical split: same class, no behavior change. See BattleRuntimeModule.cs.
public sealed partial class BattleRuntimeModule
{

    internal void SetAiTraceEnabled(bool enabled)
    {
        _ai_trace_enabled = enabled;
        if (!enabled)
            _ai_turn_traces.Clear();
    }

    internal IReadOnlyList<BattleAiTurnTraceProjection> GetAiTurnTracesTyped() => _ai_turn_traces;

    internal void ClearAiTurnTraces() => _ai_turn_traces.Clear();

    private List<StringName> CollectAiTraceDecisionTargetUnitIds(
        BattleAiDecision decision,
        BattleAiTurnTraceProjection turn_trace
    )
    {
        var unitIds = new List<StringName>();
        if (decision != null && decision.command != null)
        {
            _add_ai_trace_unit_id(unitIds, decision.command.target_unit_id);
            foreach (StringName unitId in decision.command.TargetUnitIdsTyped)
            {
                _add_ai_trace_unit_id(unitIds, unitId);
            }
        }
        _append_ai_trace_score_target_unit_ids(
            unitIds,
            turn_trace?.ScoreInput
        );
        return unitIds;
    }

    private void _append_ai_trace_score_target_unit_ids(
        List<StringName> unit_ids,
        BattleAiScoreInput score
    )
    {
        if (score == null)
            return;
        _add_ai_trace_unit_ids(unit_ids, score.target_unit_ids);
    }

    private void _add_ai_trace_unit_ids(
        List<StringName> unit_ids,
        IEnumerable<StringName> raw_unit_ids
    )
    {
        if (raw_unit_ids == null)
        {
            return;
        }
        foreach (StringName rawUnitId in raw_unit_ids)
            _add_ai_trace_unit_id(unit_ids, rawUnitId);
    }

    private void _add_ai_trace_unit_id(List<StringName> unit_ids, StringName raw_unit_id)
    {
        StringName unitId = ProgressionDataUtils.to_string_name(raw_unit_id);
        if (IsEmpty(unitId) || unit_ids.Contains(unitId))
            return;
        unit_ids.Add(unitId);
    }

    private Dictionary<StringName, BattleAiTraceUnitSnapshotProjection> BuildAiTraceUnitSnapshotMapTyped()
    {
        var snapshots = new Dictionary<StringName, BattleAiTraceUnitSnapshotProjection>();
        if (_state == null)
            return snapshots;
        foreach (BattleUnitState unitState in _state.GetUnitsTyped())
        {
            if (unitState == null)
                continue;
            StringName unitId = ProgressionDataUtils.to_string_name(unitState.unit_id);
            if (!IsEmpty(unitId))
            {
                snapshots[unitId] = BuildAiTraceUnitSnapshotTyped(unitState);
            }
        }
        return snapshots;
    }

    private BattleAiTraceUnitSnapshotProjection BuildAiTraceUnitSnapshotTyped(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return new BattleAiTraceUnitSnapshotProjection();
        int hpMax = 0;
        int mpMax = 0;
        int staminaMax = 0;
        int auraMax = 0;
        if (unit_state.attribute_snapshot != null)
        {
            hpMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax));
            mpMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax));
            staminaMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax));
            auraMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax));
        }
        return new BattleAiTraceUnitSnapshotProjection
        {
            UnitId = unit_state.unit_id.ToString(),
            DisplayName = unit_state.display_name,
            FactionId = unit_state.faction_id.ToString(),
            Coord = _format_ai_trace_coord(unit_state.coord),
            Alive = unit_state.is_alive,
            Hp = unit_state.current_hp,
            HpMax = Math.Max(hpMax, 1),
            Mp = unit_state.current_mp,
            MpMax = Math.Max(mpMax, 0),
            Stamina = unit_state.current_stamina,
            StaminaMax = Math.Max(staminaMax, 0),
            Aura = unit_state.current_aura,
            AuraMax = Math.Max(auraMax, 0),
            Ap = unit_state.current_ap,
            MovePoints = unit_state.current_move_points,
            ShieldHp = unit_state.current_shield_hp,
            ShieldMaxHp = unit_state.shield_max_hp,
        };
    }

    private List<BattleAiTraceUnitSnapshotProjection> BuildAiTraceSnapshotsForUnitIdsTyped(
        IEnumerable<StringName> unit_ids,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> snapshot_map
    )
    {
        var snapshots = new List<BattleAiTraceUnitSnapshotProjection>();
        if (unit_ids == null || snapshot_map == null)
        {
            return snapshots;
        }
        foreach (StringName rawUnitId in unit_ids)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
            if (IsEmpty(unitId))
            {
                continue;
            }
            if (snapshot_map.TryGetValue(unitId, out BattleAiTraceUnitSnapshotProjection snapshot))
            {
                snapshots.Add(CloneAiTraceUnitSnapshot(snapshot));
            }
        }
        return snapshots;
    }

    private BattleAiTraceExecutionResultProjection BuildAiTraceExecutionResultTyped(
        BattleAiDecision decision,
        BattleEventBatch decision_batch,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> unit_snapshots_before,
        IEnumerable<StringName> decision_target_unit_ids
    )
    {
        Dictionary<StringName, BattleAiTraceUnitSnapshotProjection> unitSnapshotsAfter =
            BuildAiTraceUnitSnapshotMapTyped();
        var trackedUnitIds = new List<StringName>();
        foreach (StringName unitId in decision_target_unit_ids ?? Array.Empty<StringName>())
            _add_ai_trace_unit_id(trackedUnitIds, unitId);
        if (decision?.command != null)
            _add_ai_trace_unit_id(trackedUnitIds, decision.command.unit_id);
        if (decision_batch != null)
            _add_ai_trace_unit_ids(trackedUnitIds, decision_batch.ChangedUnitIdsTyped);
        BattleCommand command = decision?.command;
        return new BattleAiTraceExecutionResultProjection
        {
            CommandType = command != null ? command.command_type.ToString() : "",
            SkillId = command != null ? command.skill_id.ToString() : "",
            SkillVariantId = command != null ? command.skill_variant_id.ToString() : "",
            ChangedUnitIds = _ai_trace_stringify_unit_ids(
                decision_batch != null
                    ? decision_batch.ChangedUnitIdsTyped
                    : Array.Empty<StringName>()
            ),
            TrackedUnitIds = _ai_trace_stringify_unit_ids(trackedUnitIds),
            UnitResults = _build_ai_trace_unit_results(
                trackedUnitIds,
                unit_snapshots_before,
                unitSnapshotsAfter
            ),
            LogLines =
                decision_batch != null ? BuildStringArray(decision_batch.LogLinesTyped) : new List<string>(),
            ReportEntries =
                decision_batch != null
                    ? BuildReportEntriesProjection(decision_batch.ReportEntriesTyped)
                    : new List<Dictionary<string, object>>(),
        };
    }

    private List<BattleAiTraceUnitResultProjection> _build_ai_trace_unit_results(
        IEnumerable<StringName> unit_ids,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> unit_snapshots_before,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> unit_snapshots_after
    )
    {
        var results = new List<BattleAiTraceUnitResultProjection>();
        foreach (StringName rawUnitId in unit_ids ?? Array.Empty<StringName>())
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
            if (IsEmpty(unitId))
            {
                continue;
            }
            BattleAiTraceUnitSnapshotProjection before = null;
            BattleAiTraceUnitSnapshotProjection after = null;
            unit_snapshots_before?.TryGetValue(unitId, out before);
            unit_snapshots_after?.TryGetValue(unitId, out after);
            before ??= new BattleAiTraceUnitSnapshotProjection();
            after ??= new BattleAiTraceUnitSnapshotProjection();
            if (IsEmptySnapshot(before) && IsEmptySnapshot(after))
                continue;
            int hpBefore = before.Hp != 0 ? before.Hp : after.Hp;
            int hpAfter = after.Hp != 0 || before.Hp == 0 ? after.Hp : hpBefore;
            int shieldBefore = before.ShieldHp != 0 ? before.ShieldHp : after.ShieldHp;
            int shieldAfter = after.ShieldHp != 0 || before.ShieldHp == 0 ? after.ShieldHp : shieldBefore;
            bool beforeAlive = before.Alive;
            bool afterAlive = after.Alive || !beforeAlive ? after.Alive : beforeAlive;
            string coordBefore = !string.IsNullOrEmpty(before.Coord) ? before.Coord : after.Coord;
            string coordAfter = !string.IsNullOrEmpty(after.Coord) ? after.Coord : coordBefore;
            results.Add(
                new BattleAiTraceUnitResultProjection
                {
                    UnitId = unitId.ToString(),
                    Before = CloneAiTraceUnitSnapshot(before),
                    After = CloneAiTraceUnitSnapshot(after),
                    HpDelta = hpAfter - hpBefore,
                    HpDamage = Math.Max(hpBefore - hpAfter, 0),
                    HpHealing = Math.Max(hpAfter - hpBefore, 0),
                    ShieldDelta = shieldAfter - shieldBefore,
                    ShieldDamage = Math.Max(shieldBefore - shieldAfter, 0),
                    ShieldRestored = Math.Max(shieldAfter - shieldBefore, 0),
                    Killed = beforeAlive && !afterAlive,
                    Revived = !beforeAlive && afterAlive,
                    Moved = coordBefore != coordAfter,
                }
            );
        }
        return results;
    }

    private List<string> _ai_trace_stringify_unit_ids(IEnumerable<StringName> unit_ids)
    {
        var results = new List<string>();
        if (unit_ids == null)
            return results;
        foreach (StringName rawUnitId in unit_ids)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
            if (!IsEmpty(unitId))
                results.Add(unitId.ToString());
        }
        return results;
    }

    private static List<string> BuildStringArray(IEnumerable<string> values)
    {
        var results = new List<string>();
        if (values == null)
            return results;
        foreach (string value in values)
            results.Add(value ?? "");
        return results;
    }

    private static List<IReadOnlyDictionary<string, object>> BuildReportEntriesProjection(
        IEnumerable<IReadOnlyDictionary<string, object>> reportEntries
    )
    {
        var result = new List<IReadOnlyDictionary<string, object>>();
        foreach (
            IReadOnlyDictionary<string, object> reportEntry in
            reportEntries ?? Array.Empty<IReadOnlyDictionary<string, object>>()
        )
        {
            result.Add(RuntimePlainPayload.CloneDictionary(reportEntry));
        }
        return result;
    }

    private static bool IsEmptySnapshot(BattleAiTraceUnitSnapshotProjection snapshot)
    {
        return snapshot == null
            || string.IsNullOrEmpty(snapshot.UnitId)
                && string.IsNullOrEmpty(snapshot.DisplayName)
                && string.IsNullOrEmpty(snapshot.FactionId)
                && string.IsNullOrEmpty(snapshot.Coord)
                && snapshot.Hp == 0
                && snapshot.HpMax == 0
                && snapshot.Mp == 0
                && snapshot.MpMax == 0
                && snapshot.Stamina == 0
                && snapshot.StaminaMax == 0
                && snapshot.Aura == 0
                && snapshot.AuraMax == 0
                && snapshot.Ap == 0
                && snapshot.MovePoints == 0
                && snapshot.ShieldHp == 0
                && snapshot.ShieldMaxHp == 0
                && !snapshot.Alive;
    }

    private static BattleAiTraceUnitSnapshotProjection CloneAiTraceUnitSnapshot(
        BattleAiTraceUnitSnapshotProjection snapshot
    )
    {
        if (snapshot == null)
        {
            return new BattleAiTraceUnitSnapshotProjection();
        }
        return new BattleAiTraceUnitSnapshotProjection
        {
            UnitId = snapshot.UnitId,
            DisplayName = snapshot.DisplayName,
            FactionId = snapshot.FactionId,
            Coord = snapshot.Coord,
            Alive = snapshot.Alive,
            Hp = snapshot.Hp,
            HpMax = snapshot.HpMax,
            Mp = snapshot.Mp,
            MpMax = snapshot.MpMax,
            Stamina = snapshot.Stamina,
            StaminaMax = snapshot.StaminaMax,
            Aura = snapshot.Aura,
            AuraMax = snapshot.AuraMax,
            Ap = snapshot.Ap,
            MovePoints = snapshot.MovePoints,
            ShieldHp = snapshot.ShieldHp,
            ShieldMaxHp = snapshot.ShieldMaxHp,
        };
    }


    internal string _format_ai_trace_coord(Vector2I coord) => $"({coord.X}, {coord.Y})";

    internal BattleMetricsState GetBattleMetricsTyped() => _battle_metrics ?? new BattleMetricsState();
}
