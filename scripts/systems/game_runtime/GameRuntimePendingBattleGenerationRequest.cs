using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class GameRuntimePendingBattleGenerationRequest
{
    private readonly Dictionary<string, object> _context = new(System.StringComparer.Ordinal);

    public EncounterAnchorData EncounterAnchor { get; private set; }

    public int Seed { get; private set; }

    public bool IsEmpty => EncounterAnchor == null;

    internal void Set(EncounterAnchorData encounterAnchor, int seed, GDictionary context)
    {
        EncounterAnchor = encounterAnchor;
        Seed = seed;
        ReplaceContext(context);
    }

    internal Dictionary<string, object> CloneContextPlain() =>
        RuntimePlainPayload.CloneDictionary(_context);

    internal void Clear()
    {
        EncounterAnchor = null;
        Seed = 0;
        ClearContextEntries();
    }

    private void ReplaceContext(GDictionary context)
    {
        ClearContextEntries();
        if (context == null)
            return;
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(
                context,
                "GameRuntimePendingBattleGenerationRequest.context"
            );
        foreach (KeyValuePair<string, object> entry in normalized)
            _context[entry.Key] = entry.Value;
    }

    private void ClearContextEntries()
    {
        _context.Clear();
    }
}
