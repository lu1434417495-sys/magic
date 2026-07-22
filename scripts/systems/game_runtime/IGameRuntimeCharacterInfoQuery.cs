using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Narrow read-only capability used by the character-info projection builder.
// Definitions are borrowed from the current content snapshot; callers must not retain them
// across runtime rebinds. Identity summaries are detached projection payloads.
internal interface IGameRuntimeCharacterInfoQuery
{
    string FormatCoord(Vector2I coord);

    string GetSkillDisplayName(StringName skillId);

    bool HasPartyMember(StringName memberId);

    bool TryGetItemDefinition(StringName itemId, out ItemDefinition itemDefinition);

    bool TryGetTraitDefinition(StringName traitId, out TraitDefinition traitDefinition);

    GDictionary GetIdentitySummary(StringName memberId);
}
