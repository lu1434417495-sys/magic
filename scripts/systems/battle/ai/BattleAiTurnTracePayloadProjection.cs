using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleAiTurnTracePayloadProjection
{
    internal static Godot.Collections.Array<GDictionary> ProjectArray(
        IEnumerable<BattleAiTurnTraceProjection> traces
    )
    {
        var result = new Godot.Collections.Array<GDictionary>();
        if (traces == null)
        {
            RuntimeStateLifecycle.MarkValueGraphFinalizerless(
                result,
                "BattleAiTurnTracePayloadProjection.ProjectArray"
            );
            return result;
        }

        foreach (BattleAiTurnTraceProjection trace in traces)
        {
            result.Add(Project(trace));
        }
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            result,
            "BattleAiTurnTracePayloadProjection.ProjectArray"
        );
        return result;
    }

    internal static GDictionary Project(BattleAiTurnTraceProjection trace) =>
        TraceDictionaryProjection.ToDictionary(
            trace?.ToTraceDictionary()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal)
        );
}
