using Godot;

internal sealed record WildEncounterRosterUnitEntryDefinition(
    StringName TemplateId,
    int Count,
    string DisplayName,
    StringName ActorId = default
)
{
    public StringName ActorId { get; init; } =
        ActorId ?? new StringName("");
}
