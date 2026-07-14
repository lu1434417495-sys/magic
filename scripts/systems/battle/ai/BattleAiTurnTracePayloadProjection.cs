using System;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleAiTurnTracePayloadProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(
        BattleAiTurnTraceProjection trace
    )
    {
        return TraceDictionaryProjection.BuildLease(
            ToPlainDictionary(trace),
            "battle_ai_turn_trace",
            LifetimeDomain.Request,
            "BattleAiTurnTracePayloadProjection.BuildLease"
        );
    }

    internal static Dictionary<string, object> BuildPlain(
        BattleAiTurnTraceProjection trace
    ) => ToPlainDictionary(trace);

    internal static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleAiTurnTraceProjection trace,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        TraceDictionaryProjection.WriteInto(lease, target, ToPlainDictionary(trace), reason);
    }

    internal static GDictionary WriteOwned<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleAiTurnTraceProjection trace,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return TraceDictionaryProjection.WriteDictionary(
            lease,
            ToPlainDictionary(trace),
            reason
        );
    }

    internal static GArray WriteArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<BattleAiTurnTraceProjection> traces,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (traces == null)
            return result;

        int index = 0;
        foreach (BattleAiTurnTraceProjection trace in traces)
        {
            result.Add(WriteOwned(lease, trace, $"{reason}[{index}]"));
            index++;
        }
        return result;
    }

    private static Dictionary<string, object> ToPlainDictionary(
        BattleAiTurnTraceProjection trace
    )
    {
        if (trace == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        return trace.ToTraceDictionary();
    }
}
