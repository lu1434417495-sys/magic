using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class GameRuntimePendingBattleGenerationRequest
{
    private readonly List<GameRuntimePayloadEntry> _context = new();

    public EncounterAnchorData EncounterAnchor { get; private set; }

    public int Seed { get; private set; }

    public bool IsEmpty => EncounterAnchor == null;

    internal void Set(EncounterAnchorData encounterAnchor, int seed, GDictionary context)
    {
        EncounterAnchor = encounterAnchor;
        Seed = seed;
        ReplaceContext(context);
    }

    internal GDictionary CloneContext()
    {
        var result = new GDictionary();
        foreach (GameRuntimePayloadEntry entry in _context)
            result[entry.Key] = entry.Value;
        return result;
    }

    internal void Clear()
    {
        EncounterAnchor = null;
        Seed = 0;
        _context.Clear();
    }

    private void ReplaceContext(GDictionary context)
    {
        _context.Clear();
        if (context == null)
            return;
        foreach (Variant key in context.Keys)
            _context.Add(new GameRuntimePayloadEntry(key, context[key]));
    }
}

internal readonly struct GameRuntimePayloadEntry
{
    internal readonly Variant Key;
    private readonly Variant _value;

    internal GameRuntimePayloadEntry(Variant key, Variant value)
    {
        Key = key;
        _value = DuplicateVariant(value);
    }

    internal Variant Value => DuplicateVariant(_value);

    private static Variant DuplicateVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary => Variant.From(value.AsGodotDictionary().Duplicate(true)),
            Variant.Type.Array => Variant.From(value.AsGodotArray().Duplicate(true)),
            _ => value,
        };
    }
}
