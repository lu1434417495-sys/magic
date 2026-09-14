using System;
using Godot;

public sealed class TraitSaveTagBonusEntryDefinition
{
    public TraitSaveTagBonusEntryDefinition(
        StringName saveTag,
        int bonus,
        StringName stackMode
    )
    {
        SaveTag = saveTag;
        Bonus = bonus;
        StackMode = stackMode;
    }

    public StringName SaveTag { get; }
    public int Bonus { get; }
    public StringName StackMode { get; }

    internal TraitSaveTagBonusStackModeKind StackModeKind =>
        TraitContentRules.ToSaveTagBonusStackModeKind(StackMode);

    internal static TraitSaveTagBonusEntryDefinition FromDiagnosticFixture(
        TraitSaveTagBonusEntryDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ProgressionDefinitionProjection.RequireKnown(
            TraitContentRules.ToSaveTagBonusStackModeKind(source.stack_mode)
                != TraitSaveTagBonusStackModeKind.Unknown,
            $"{path}.stack_mode",
            source.stack_mode
        );
        return new TraitSaveTagBonusEntryDefinition(
            source.save_tag,
            source.bonus,
            source.stack_mode
        );
    }
}
