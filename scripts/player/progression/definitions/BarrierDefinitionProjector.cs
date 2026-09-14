#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class BarrierDefinitionProjector
{
    internal static BarrierLayerDefinition ProjectLayer(BarrierLayerImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        return new BarrierLayerDefinition(
            new StringName(import.LayerId),
            import.DisplayName,
            import.Order,
            import.BlockedCategories.Select(value => new StringName(value)).ToArray(),
            import.BreakerSkillIds.Select(value => new StringName(value)).ToArray(),
            import.PassageOutcomes.Select(ProjectOutcome).ToArray()
        );
    }

    internal static BarrierProfileDefinition ProjectProfile(
        BarrierProfileImportModel import,
        IReadOnlyDictionary<StringName, BarrierLayerDefinition> layers
    )
    {
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(layers);
        var resolvedLayers = new List<BarrierLayerDefinition>(import.LayerIds.Count);
        foreach (string layerIdText in import.LayerIds)
        {
            var layerId = new StringName(layerIdText);
            if (!layers.TryGetValue(layerId, out BarrierLayerDefinition? layer) || layer is null)
            {
                throw new InvalidOperationException(
                    $"Barrier profile {import.ProfileId} references missing layer {layerIdText}."
                );
            }
            resolvedLayers.Add(layer);
        }
        return new BarrierProfileDefinition(
            new StringName(import.ProfileId),
            import.DisplayName,
            ToStringName(import.AnchorMode),
            ToStringName(import.AreaPattern),
            import.RadiusCells,
            import.DurationTu,
            import.CatchAllProjectedEffects,
            resolvedLayers
        );
    }

    private static BarrierOutcomeDefinition ProjectOutcome(BarrierOutcomeImportModel import) =>
        new(
            ToStringName(import.OutcomeKind),
            import.Amount,
            new StringName(import.DamageTag),
            import.HalfOnSuccess,
            import.SuccessAmount,
            new StringName(import.SuccessDamageTag),
            import.FatalDamage,
            new StringName(import.StatusId),
            new StringName(import.SaveAbility),
            new StringName(import.SaveTag),
            import.SaveDc
        );

    private static StringName ToStringName(BarrierAnchorImportKind value) =>
        value == BarrierAnchorImportKind.Fixed ? new StringName("fixed") : new StringName("");

    private static StringName ToStringName(BarrierAreaPatternImportKind value) =>
        new(
            value switch
            {
                BarrierAreaPatternImportKind.Single => "single",
                BarrierAreaPatternImportKind.Diamond => "diamond",
                BarrierAreaPatternImportKind.Square => "square",
                BarrierAreaPatternImportKind.Radius => "radius",
                BarrierAreaPatternImportKind.Cross => "cross",
                _ => "",
            }
        );

    private static StringName ToStringName(BarrierOutcomeImportKind value) =>
        new(
            value switch
            {
                BarrierOutcomeImportKind.Damage => "damage",
                BarrierOutcomeImportKind.PoisonDeath => "poison_death",
                BarrierOutcomeImportKind.Status => "status",
                BarrierOutcomeImportKind.Banish => "banish",
                _ => "",
            }
        );
}
