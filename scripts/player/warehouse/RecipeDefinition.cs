using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class RecipeDefinition
{
    public RecipeDefinition(
        StringName recipeId,
        string displayName,
        string description,
        IReadOnlyList<StringName> inputItemIds,
        IReadOnlyList<int> inputItemQuantities,
        StringName outputItemId,
        int outputQuantity,
        IReadOnlyList<StringName> requiredFacilityTags,
        string failureReason
    )
    {
        RecipeId = recipeId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        InputItemIds = FreezeValues(inputItemIds, nameof(inputItemIds));
        InputItemQuantities = FreezeValues(inputItemQuantities, nameof(inputItemQuantities));
        if (InputItemIds.Count != InputItemQuantities.Count)
        {
            throw new ArgumentException(
                "Recipe input item ids and quantities must have matching counts.",
                nameof(inputItemQuantities)
            );
        }
        OutputItemId = outputItemId;
        OutputQuantity = outputQuantity;
        RequiredFacilityTags = FreezeValues(requiredFacilityTags, nameof(requiredFacilityTags));
        FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason));
    }

    public StringName RecipeId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> InputItemIds { get; }
    public IReadOnlyList<int> InputItemQuantities { get; }
    public StringName OutputItemId { get; }
    public int OutputQuantity { get; }
    public IReadOnlyList<StringName> RequiredFacilityTags { get; }
    public string FailureReason { get; }

    private static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}
