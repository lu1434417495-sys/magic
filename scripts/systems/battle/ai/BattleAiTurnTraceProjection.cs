using System;
using System.Collections.Generic;

public sealed class BattleAiTurnTraceProjection
{
    internal string BattleId { get; set; } = "";
    internal int TurnStartedTu { get; set; } = -1;
    internal string UnitId { get; set; } = "";
    internal string UnitName { get; set; } = "";
    internal string FactionId { get; set; } = "";
    internal string BrainId { get; set; } = "";
    internal string StateId { get; set; } = "";
    internal string ActionId { get; set; } = "";
    internal string ReasonText { get; set; } = "";
    internal AiCommandSummary Command { get; set; } = new();
    internal BattleAiTraceTransitionProjection Transition { get; set; } = new();
    internal BattleAiScoreInput ScoreInput { get; set; }
    internal IReadOnlyList<AiActionTrace> ActionTraces { get; set; } = Array.Empty<AiActionTrace>();
    internal IReadOnlyList<BattleAiTraceUnitSnapshotProjection> DecisionTargetSnapshots { get; set; } =
        Array.Empty<BattleAiTraceUnitSnapshotProjection>();
    internal BattleAiTraceExecutionResultProjection ExecutionResult { get; set; }

    internal BattleAiTurnTraceProjection Clone()
    {
        return new BattleAiTurnTraceProjection
        {
            BattleId = BattleId,
            TurnStartedTu = TurnStartedTu,
            UnitId = UnitId,
            UnitName = UnitName,
            FactionId = FactionId,
            BrainId = BrainId,
            StateId = StateId,
            ActionId = ActionId,
            ReasonText = ReasonText,
            Command = Command?.Clone() ?? new AiCommandSummary(),
            Transition = Transition?.Clone() ?? new BattleAiTraceTransitionProjection(),
            ScoreInput = ScoreInput,
            ActionTraces = CloneActionTraces(ActionTraces),
            DecisionTargetSnapshots = CloneSnapshots(DecisionTargetSnapshots),
            ExecutionResult = ExecutionResult?.Clone(),
        };
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["battle_id"] = BattleId,
            ["turn_started_tu"] = TurnStartedTu,
            ["unit_id"] = UnitId,
            ["unit_name"] = UnitName,
            ["faction_id"] = FactionId,
            ["brain_id"] = BrainId,
            ["state_id"] = StateId,
            ["action_id"] = ActionId,
            ["reason_text"] = ReasonText,
            ["command"] = Command,
            ["transition"] = Transition,
            ["score_input"] = ScoreInput,
            ["action_traces"] = ActionTraces,
        };
        if (DecisionTargetSnapshots.Count > 0)
        {
            result["decision_target_snapshots"] = DecisionTargetSnapshots;
        }
        if (ExecutionResult != null)
        {
            result["execution_result"] = ExecutionResult;
        }
        return result;
    }

    private static IReadOnlyList<AiActionTrace> CloneActionTraces(
        IReadOnlyList<AiActionTrace> traces
    )
    {
        if (traces == null)
            return Array.Empty<AiActionTrace>();
        var result = new List<AiActionTrace>(traces.Count);
        foreach (AiActionTrace trace in traces)
            result.Add(trace);
        return result;
    }

    private static IReadOnlyList<BattleAiTraceUnitSnapshotProjection> CloneSnapshots(
        IReadOnlyList<BattleAiTraceUnitSnapshotProjection> snapshots
    )
    {
        if (snapshots == null)
            return Array.Empty<BattleAiTraceUnitSnapshotProjection>();
        var result = new List<BattleAiTraceUnitSnapshotProjection>(snapshots.Count);
        foreach (BattleAiTraceUnitSnapshotProjection snapshot in snapshots)
            result.Add(snapshot?.Clone() ?? new BattleAiTraceUnitSnapshotProjection());
        return result;
    }
}

public sealed class BattleAiTraceTransitionProjection
{
    internal string PreviousStateId { get; set; } = "";
    internal string StateId { get; set; } = "";
    internal string RuleId { get; set; } = "";
    internal string Reason { get; set; } = "";
    internal IReadOnlyList<BattleAiTraceTransitionConditionProjection> MatchedConditions { get; set; } =
        Array.Empty<BattleAiTraceTransitionConditionProjection>();

    internal BattleAiTraceTransitionProjection Clone()
    {
        var matchedConditions = new List<BattleAiTraceTransitionConditionProjection>();
        foreach (BattleAiTraceTransitionConditionProjection condition in MatchedConditions ?? Array.Empty<BattleAiTraceTransitionConditionProjection>())
            matchedConditions.Add(condition?.Clone() ?? new BattleAiTraceTransitionConditionProjection());
        return new BattleAiTraceTransitionProjection
        {
            PreviousStateId = PreviousStateId,
            StateId = StateId,
            RuleId = RuleId,
            Reason = Reason,
            MatchedConditions = matchedConditions,
        };
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["previous_state_id"] = PreviousStateId,
            ["state_id"] = StateId,
            ["rule_id"] = RuleId,
            ["reason"] = Reason,
            ["matched_conditions"] = MatchedConditions,
        };
    }
}

public sealed class BattleAiTraceTransitionConditionProjection
{
    internal string Predicate { get; set; } = "";
    internal int BasisPoints { get; set; }
    internal int MaxDistance { get; set; }
    internal IReadOnlyList<string> StateIds { get; set; } = Array.Empty<string>();
    internal IReadOnlyList<string> Affordances { get; set; } = Array.Empty<string>();

    internal BattleAiTraceTransitionConditionProjection Clone()
    {
        return new BattleAiTraceTransitionConditionProjection
        {
            Predicate = Predicate,
            BasisPoints = BasisPoints,
            MaxDistance = MaxDistance,
            StateIds = StateIds != null ? new List<string>(StateIds) : Array.Empty<string>(),
            Affordances = Affordances != null ? new List<string>(Affordances) : Array.Empty<string>(),
        };
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["predicate"] = Predicate,
            ["basis_points"] = BasisPoints,
            ["max_distance"] = MaxDistance,
            ["state_ids"] = StateIds,
            ["affordances"] = Affordances,
        };
    }
}

public sealed class BattleAiTraceUnitSnapshotProjection
{
    internal string UnitId { get; set; } = "";
    internal string DisplayName { get; set; } = "";
    internal string FactionId { get; set; } = "";
    internal string Coord { get; set; } = "";
    internal bool Alive { get; set; }
    internal int Hp { get; set; }
    internal int HpMax { get; set; }
    internal int Mp { get; set; }
    internal int MpMax { get; set; }
    internal int Stamina { get; set; }
    internal int StaminaMax { get; set; }
    internal int Aura { get; set; }
    internal int AuraMax { get; set; }
    internal int Ap { get; set; }
    internal int MovePoints { get; set; }
    internal int ShieldHp { get; set; }
    internal int ShieldMaxHp { get; set; }

    internal BattleAiTraceUnitSnapshotProjection Clone()
    {
        return new BattleAiTraceUnitSnapshotProjection
        {
            UnitId = UnitId,
            DisplayName = DisplayName,
            FactionId = FactionId,
            Coord = Coord,
            Alive = Alive,
            Hp = Hp,
            HpMax = HpMax,
            Mp = Mp,
            MpMax = MpMax,
            Stamina = Stamina,
            StaminaMax = StaminaMax,
            Aura = Aura,
            AuraMax = AuraMax,
            Ap = Ap,
            MovePoints = MovePoints,
            ShieldHp = ShieldHp,
            ShieldMaxHp = ShieldMaxHp,
        };
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["unit_id"] = UnitId,
            ["display_name"] = DisplayName,
            ["faction_id"] = FactionId,
            ["coord"] = Coord,
            ["alive"] = Alive,
            ["hp"] = Hp,
            ["hp_max"] = HpMax,
            ["mp"] = Mp,
            ["mp_max"] = MpMax,
            ["stamina"] = Stamina,
            ["stamina_max"] = StaminaMax,
            ["aura"] = Aura,
            ["aura_max"] = AuraMax,
            ["ap"] = Ap,
            ["move_points"] = MovePoints,
            ["shield_hp"] = ShieldHp,
            ["shield_max_hp"] = ShieldMaxHp,
        };
    }
}

public sealed class BattleAiTraceUnitResultProjection
{
    internal string UnitId { get; set; } = "";
    internal BattleAiTraceUnitSnapshotProjection Before { get; set; }
    internal BattleAiTraceUnitSnapshotProjection After { get; set; }
    internal int HpDelta { get; set; }
    internal int HpDamage { get; set; }
    internal int HpHealing { get; set; }
    internal int ShieldDelta { get; set; }
    internal int ShieldDamage { get; set; }
    internal int ShieldRestored { get; set; }
    internal bool Killed { get; set; }
    internal bool Revived { get; set; }
    internal bool Moved { get; set; }

    internal BattleAiTraceUnitResultProjection Clone()
    {
        return new BattleAiTraceUnitResultProjection
        {
            UnitId = UnitId,
            Before = Before?.Clone() ?? new BattleAiTraceUnitSnapshotProjection(),
            After = After?.Clone() ?? new BattleAiTraceUnitSnapshotProjection(),
            HpDelta = HpDelta,
            HpDamage = HpDamage,
            HpHealing = HpHealing,
            ShieldDelta = ShieldDelta,
            ShieldDamage = ShieldDamage,
            ShieldRestored = ShieldRestored,
            Killed = Killed,
            Revived = Revived,
            Moved = Moved,
        };
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["unit_id"] = UnitId,
            ["before"] = Before,
            ["after"] = After,
            ["hp_delta"] = HpDelta,
            ["hp_damage"] = HpDamage,
            ["hp_healing"] = HpHealing,
            ["shield_delta"] = ShieldDelta,
            ["shield_damage"] = ShieldDamage,
            ["shield_restored"] = ShieldRestored,
            ["killed"] = Killed,
            ["revived"] = Revived,
            ["moved"] = Moved,
        };
    }
}

public sealed class BattleAiTraceExecutionResultProjection
{
    internal string CommandType { get; set; } = "";
    internal string SkillId { get; set; } = "";
    internal string SkillVariantId { get; set; } = "";
    internal IReadOnlyList<string> ChangedUnitIds { get; set; } = Array.Empty<string>();
    internal IReadOnlyList<string> TrackedUnitIds { get; set; } = Array.Empty<string>();
    internal IReadOnlyList<BattleAiTraceUnitResultProjection> UnitResults { get; set; } =
        Array.Empty<BattleAiTraceUnitResultProjection>();
    internal IReadOnlyList<string> LogLines { get; set; } = Array.Empty<string>();
    internal IReadOnlyList<IReadOnlyDictionary<string, object>> ReportEntries { get; set; } =
        Array.Empty<IReadOnlyDictionary<string, object>>();

    internal BattleAiTraceExecutionResultProjection Clone()
    {
        var unitResults = new List<BattleAiTraceUnitResultProjection>();
        foreach (BattleAiTraceUnitResultProjection unitResult in UnitResults ?? Array.Empty<BattleAiTraceUnitResultProjection>())
            unitResults.Add(unitResult?.Clone() ?? new BattleAiTraceUnitResultProjection());
        var reportEntries = new List<IReadOnlyDictionary<string, object>>();
        foreach (IReadOnlyDictionary<string, object> reportEntry in ReportEntries ?? Array.Empty<IReadOnlyDictionary<string, object>>())
            reportEntries.Add(reportEntry != null ? new Dictionary<string, object>(reportEntry, StringComparer.Ordinal) : new Dictionary<string, object>(StringComparer.Ordinal));
        return new BattleAiTraceExecutionResultProjection
        {
            CommandType = CommandType,
            SkillId = SkillId,
            SkillVariantId = SkillVariantId,
            ChangedUnitIds = ChangedUnitIds != null ? new List<string>(ChangedUnitIds) : Array.Empty<string>(),
            TrackedUnitIds = TrackedUnitIds != null ? new List<string>(TrackedUnitIds) : Array.Empty<string>(),
            UnitResults = unitResults,
            LogLines = LogLines != null ? new List<string>(LogLines) : Array.Empty<string>(),
            ReportEntries = reportEntries,
        };
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["command_type"] = CommandType,
            ["skill_id"] = SkillId,
            ["skill_variant_id"] = SkillVariantId,
            ["changed_unit_ids"] = ChangedUnitIds,
            ["tracked_unit_ids"] = TrackedUnitIds,
            ["unit_results"] = UnitResults,
            ["log_lines"] = LogLines,
            ["report_entries"] = ReportEntries,
        };
    }
}
