using System;
using System.Collections.Generic;
using Godot;

public sealed class TraitPassiveStatusEffectDefinition
{
    public TraitPassiveStatusEffectDefinition(
        StringName statusId,
        int power,
        int stacks,
        string displayLabel,
        bool undispellable,
        bool countsAsDebuffOverride,
        bool countsAsDebuff,
        IReadOnlyList<StringName> saveImmunityTags
    )
    {
        StatusId = statusId;
        Power = power;
        Stacks = stacks;
        DisplayLabel = ProgressionDefinitionProjection.RequireString(
            displayLabel,
            "TraitPassiveStatusEffectDefinition.DisplayLabel"
        );
        Undispellable = undispellable;
        CountsAsDebuffOverride = countsAsDebuffOverride;
        CountsAsDebuff = countsAsDebuff;
        SaveImmunityTags = ProgressionDefinitionProjection.FreezeValues(
            saveImmunityTags,
            "TraitPassiveStatusEffectDefinition.SaveImmunityTags"
        );
    }

    public StringName StatusId { get; }
    public int Power { get; }
    public int Stacks { get; }
    public string DisplayLabel { get; }
    public bool Undispellable { get; }
    public bool CountsAsDebuffOverride { get; }
    public bool CountsAsDebuff { get; }
    public IReadOnlyList<StringName> SaveImmunityTags { get; }

    internal static TraitPassiveStatusEffectDefinition FromResource(
        TraitPassiveStatusEffectDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TraitPassiveStatusEffectDefinition(
            source.status_id,
            source.power,
            source.stacks,
            ProgressionDefinitionProjection.RequireString(
                source.display_label,
                $"{path}.display_label"
            ),
            source.undispellable,
            source.counts_as_debuff_override,
            source.counts_as_debuff,
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.SaveImmunityTagsProjectionBorrowed,
                $"{path}.save_immunity_tags"
            )
        );
    }
}
