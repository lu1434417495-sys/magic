using Godot;

internal sealed record WildEncounterRosterUnitEntryDefinition(
    StringName TemplateId,
    int Count,
    string DisplayName
);
