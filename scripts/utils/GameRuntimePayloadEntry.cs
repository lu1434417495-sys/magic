using Godot;

internal readonly struct GameRuntimePayloadEntry
{
    internal readonly Variant Key;
    private readonly Variant _value;

    internal GameRuntimePayloadEntry(Variant key, Variant value)
    {
        Key = key;
        _value = DuplicateVariant(value);
        MarkRuntimeStateGraph("store");
    }

    internal Variant Value => DuplicateVariant(_value);

    internal void SuppressFinalizers()
    {
        MarkRuntimeStateGraph("clear");
    }

    private void MarkRuntimeStateGraph(string phase)
    {
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            Key,
            $"GameRuntimePayloadEntry.{phase}.key"
        );
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            _value,
            $"GameRuntimePayloadEntry.{phase}.value"
        );
    }

    private static Variant DuplicateVariant(Variant value)
    {
        return RuntimePayloadCopy.CopyVariant(
            value,
            "GameRuntimePayloadEntry.DuplicateVariant"
        );
    }
}
