using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal readonly record struct BattleChainDamagePreviewHopData(
    int HopIndex,
    StringName OriginUnitId,
    Vector2I OriginCoord,
    StringName TargetUnitId,
    Vector2I TargetCoord,
    int Distance,
    int OutgoingRange,
    bool OriginWasConductive,
    bool Blocked
);

internal sealed class BattleChainDamagePreviewData
{
    internal BattleChainDamagePreviewData(
        StringName primaryTargetUnitId,
        IReadOnlyList<BattleChainDamagePreviewHopData> normalHops,
        IReadOnlyList<BattleChainDamagePreviewHopData> backlashHops,
        string summaryText
    )
    {
        PrimaryTargetUnitId = primaryTargetUnitId;
        NormalHops = Freeze(normalHops);
        BacklashHops = Freeze(backlashHops);
        SummaryText = summaryText ?? "";
    }

    internal StringName PrimaryTargetUnitId { get; }
    internal IReadOnlyList<BattleChainDamagePreviewHopData> NormalHops { get; }
    internal IReadOnlyList<BattleChainDamagePreviewHopData> BacklashHops { get; }
    internal string SummaryText { get; }

    internal int NormalReachedTargetCount
    {
        get
        {
            int count = PrimaryTargetUnitId == "" ? 0 : 1;
            foreach (BattleChainDamagePreviewHopData hop in NormalHops)
            {
                if (!hop.Blocked)
                    count += 1;
            }
            return count;
        }
    }

    private static IReadOnlyList<BattleChainDamagePreviewHopData> Freeze(
        IReadOnlyList<BattleChainDamagePreviewHopData> values
    )
    {
        return values == null || values.Count == 0
            ? Array.Empty<BattleChainDamagePreviewHopData>()
            : new ReadOnlyCollection<BattleChainDamagePreviewHopData>(
                new List<BattleChainDamagePreviewHopData>(values)
            );
    }
}
