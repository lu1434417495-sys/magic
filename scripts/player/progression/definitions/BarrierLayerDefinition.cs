using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public sealed class BarrierLayerDefinition
{
    public BarrierLayerDefinition(
        StringName layerId,
        string displayName,
        int order,
        IReadOnlyList<StringName> blockedCategories,
        IReadOnlyList<StringName> breakerSkillIds,
        IReadOnlyList<BarrierOutcomeDefinition> passageOutcomes
    )
    {
        LayerId = layerId;
        DisplayName = displayName
            ?? throw new InvalidDataException("BarrierLayerDefinition.DisplayName must not be null.");
        Order = order;
        BlockedCategories = ProgressionDefinitionProjection.FreezeValues(
            blockedCategories,
            "BarrierLayerDefinition.BlockedCategories"
        );
        BreakerSkillIds = ProgressionDefinitionProjection.FreezeValues(
            breakerSkillIds,
            "BarrierLayerDefinition.BreakerSkillIds"
        );
        PassageOutcomes = ProgressionDefinitionProjection.FreezeValues(
            passageOutcomes,
            "BarrierLayerDefinition.PassageOutcomes"
        );
    }

    public StringName LayerId { get; }
    public string DisplayName { get; }
    public int Order { get; }
    public IReadOnlyList<StringName> BlockedCategories { get; }
    public IReadOnlyList<StringName> BreakerSkillIds { get; }
    public IReadOnlyList<BarrierOutcomeDefinition> PassageOutcomes { get; }

    internal static BarrierLayerDefinition FromResource(BarrierLayerDef source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new BarrierLayerDefinition(
            source.layer_id,
            source.display_name,
            source.order,
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.BlockedCategoriesProjectionBorrowed,
                path + ".blocked_categories"
            ),
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.BreakerSkillIdsProjectionBorrowed,
                path + ".breaker_skill_ids"
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.PassageOutcomesProjectionBorrowed,
                path + ".passage_outcomes",
                BarrierOutcomeDefinition.FromResource
            )
        );
    }
}
