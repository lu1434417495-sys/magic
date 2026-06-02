using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class GameRuntimePendingBattleGenerationRequest
{
    private GDictionary _context = new();

    public EncounterAnchorData EncounterAnchor { get; private set; }

    public int Seed { get; private set; }

    public bool IsEmpty => EncounterAnchor == null;

    public void Set(EncounterAnchorData encounterAnchor, int seed, GDictionary context)
    {
        EncounterAnchor = encounterAnchor;
        Seed = seed;
        _context = (context ?? new GDictionary()).Duplicate(true);
    }

    public GDictionary CloneContext()
    {
        return _context.Duplicate(true);
    }

    public void Clear()
    {
        EncounterAnchor = null;
        Seed = 0;
        _context = new GDictionary();
    }
}
