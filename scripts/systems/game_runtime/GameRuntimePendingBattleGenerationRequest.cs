using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class GameRuntimePendingBattleGenerationRequest
{
    private GDictionary _context = new();

    public EncounterAnchorData EncounterAnchor { get; private set; }

    public int Seed { get; private set; }

    public bool IsEmpty => EncounterAnchor == null;

    internal void Set(EncounterAnchorData encounterAnchor, int seed, GDictionary context)
    {
        EncounterAnchor = encounterAnchor;
        Seed = seed;
        _context = (context ?? new GDictionary()).Duplicate(true);
    }

    internal GDictionary CloneContext()
    {
        return _context.Duplicate(true);
    }

    internal void Clear()
    {
        EncounterAnchor = null;
        Seed = 0;
        _context = new GDictionary();
    }
}
