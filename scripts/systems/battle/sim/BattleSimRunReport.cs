using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

public sealed class BattleSimRunReport
{
    private BattleSimMetricsSnapshot _metricsSnapshot = BattleSimMetricsSnapshot.Empty();
    private List<Dictionary<string, object>> _finalUnits = new();
    private BattleSimTerminationKind _terminationKind = BattleSimTerminationKind.InvalidRuntime;
    private BattleStartFailureSnapshot _startFailure = new();
    private BattleFinalDecision _finalDecision;
    public string ScenarioId { get; set; } = "";

    public string ProfileId { get; set; } = "";

    public long Seed { get; set; }

    public string BattleId { get; set; } = "";

    public BattleSimTerminationKind TerminationKind
    {
        get => _terminationKind;
        set => _terminationKind = value;
    }

    public bool BattleEnded
    {
        get => _terminationKind == BattleSimTerminationKind.BattleEnded;
        set
        {
            if (value)
                _terminationKind = BattleSimTerminationKind.BattleEnded;
            else if (_terminationKind == BattleSimTerminationKind.BattleEnded)
                _terminationKind = BattleSimTerminationKind.InvalidRuntime;
        }
    }

    public bool Stalled => _terminationKind == BattleSimTerminationKind.IdleStall;

    public BattleStartFailureSnapshot StartFailure
    {
        get => _startFailure;
        set => _startFailure = value ?? new BattleStartFailureSnapshot();
    }

    internal BattleFinalDecision FinalDecision => _finalDecision?.DuplicateState();

    internal bool HasFinalDecision => _finalDecision != null;

    internal bool IsCompletedSample => BattleEnded && HasFinalDecision;

    internal BattleObjectiveMode ObjectiveMode =>
        _finalDecision?.ObjectiveMode ?? BattleObjectiveMode.Unknown;

    internal BattleOutcomeKind Outcome =>
        _finalDecision?.Outcome ?? BattleOutcomeKind.Unknown;

    internal BattleEndReasonKind EndReason =>
        _finalDecision?.EndReason ?? BattleEndReasonKind.None;

    internal int DecisionTu => _finalDecision?.DecisionTu ?? -1;

    public string WinnerFactionId => _finalDecision?.WinnerFactionId.ToString() ?? "";

    internal void SetFinalDecision(BattleFinalDecision finalDecision)
    {
        _finalDecision = finalDecision?.DuplicateState();
    }

    public int FinalTu { get; set; }

    public int Iterations { get; set; }

    public int IdleLoops { get; set; }

    public int TimelineSteps { get; set; }

    public int AllyAlive { get; set; }

    public int EnemyAlive { get; set; }

    public BattleSimMetricsSnapshot MetricsSnapshot
    {
        get => _metricsSnapshot;
        set => _metricsSnapshot = value ?? BattleSimMetricsSnapshot.Empty();
    }

    public IReadOnlyList<BattleAiTurnTraceProjection> AiTurnTraces { get; set; } =
        System.Array.Empty<BattleAiTurnTraceProjection>();

    public IReadOnlyList<Dictionary<string, object>> FinalUnits
    {
        get => CloneFinalUnits(_finalUnits);
        set => _finalUnits = CloneFinalUnits(value);
    }

    private static List<Dictionary<string, object>> CloneFinalUnits(
        IEnumerable<Dictionary<string, object>> source
    )
    {
        var result = new List<Dictionary<string, object>>();
        if (source == null)
            return result;
        foreach (Dictionary<string, object> snapshot in source)
            result.Add(CloneStringObjectMap(snapshot));
        return result;
    }

    private static Dictionary<string, object> CloneStringObjectMap(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach ((string key, object value) in source)
        {
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException(
                    "BattleSimRunReport.FinalUnits does not accept an empty dictionary key."
                );
            result[key] = CloneValue(value);
        }
        return result;
    }

    private static Dictionary<StringName, object> CloneStringNameObjectMap(
        IReadOnlyDictionary<StringName, object> source
    )
    {
        var result = new Dictionary<StringName, object>();
        if (source == null)
            return result;
        foreach ((StringName key, object value) in source)
            result[key] = CloneValue(value);
        return result;
    }

    private static Dictionary<StringName, int> CloneStringNameIntMap(
        IReadOnlyDictionary<StringName, int> source
    ) => source == null ? new() : new(source);

    private static Dictionary<string, int> CloneStringIntMap(
        IReadOnlyDictionary<string, int> source
    ) => source == null ? new(StringComparer.Ordinal) : new(source, StringComparer.Ordinal);

    private static Dictionary<string, float> CloneStringFloatMap(
        IReadOnlyDictionary<string, float> source
    ) => source == null ? new(StringComparer.Ordinal) : new(source, StringComparer.Ordinal);

    private static object CloneValue(object value)
    {
        return value switch
        {
            null => null,
            IReadOnlyDictionary<string, object> map => CloneStringObjectMap(map),
            IReadOnlyDictionary<StringName, object> map => CloneStringNameObjectMap(map),
            IReadOnlyDictionary<StringName, int> map => CloneStringNameIntMap(map),
            IReadOnlyDictionary<string, int> map => CloneStringIntMap(map),
            IReadOnlyDictionary<string, float> map => CloneStringFloatMap(map),
            bool or byte or short or int or long or float or double or string or StringName
                or Vector2I or Vector2 or Vector3I or Vector3 => value,
            Variant or GodotObject or IDisposable or IDictionary => throw UnsupportedValue(value),
            IEnumerable values => CloneEnumerable(values),
            _ => throw UnsupportedValue(value),
        };
    }

    private static List<object> CloneEnumerable(IEnumerable source)
    {
        var result = new List<object>();
        if (source == null)
            return result;
        foreach (object value in source)
            result.Add(CloneValue(value));
        return result;
    }

    private static InvalidOperationException UnsupportedValue(object value) =>
        new(
            $"BattleSimRunReport.FinalUnits does not support value type {value?.GetType().FullName ?? "<null>"}."
        );
}
