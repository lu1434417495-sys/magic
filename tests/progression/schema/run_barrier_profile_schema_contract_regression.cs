using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_barrier_profile_schema_contract_regression : LifecycleTestSceneTree
{
    private static readonly StringName[] ExpectedLayerIds =
    {
        "red", "orange", "yellow", "green", "blue", "indigo", "violet",
    };

    private static readonly StringName[] ExpectedBreakers =
    {
        "mage_cone_of_cold",
        "mage_gust_of_wind",
        "mage_spell_disjunction",
        "mage_passwall",
        "mage_arcane_missile",
        "mage_continual_light",
        "mage_dispel_magic",
    };

    private static readonly StringName[] SingleLayerProfileIds =
    {
        "prismatic_red_ward",
        "prismatic_orange_ward",
        "prismatic_yellow_ward",
        "prismatic_green_ward",
        "prismatic_blue_ward",
        "prismatic_indigo_ward",
        "prismatic_violet_ward",
    };

    private static readonly StringName[] ExpectedOutcomes =
    {
        "damage", "damage", "damage", "poison_death", "status", "status", "banish",
    };

    private static readonly Dictionary<StringName, StringName> ExpectedStatuses = new()
    {
        ["blue"] = "petrified",
        ["indigo"] = "madness",
    };

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        using var registry = new BarrierContentRegistry();
        IReadOnlyList<string> errors = registry.ValidateTyped();
        _test.Eq(errors.Count, 0, $"Barrier JSON registry must validate: {string.Join(" | ", errors)}");

        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profiles =
            registry.GetProfileDefsTyped();
        IReadOnlyDictionary<StringName, BarrierLayerDefinition> layers =
            registry.GetLayerDefsTyped();
        _test.Eq(profiles.Count, 8, "Barrier JSON must publish one sphere and seven wards.");
        _test.Eq(layers.Count, 7, "Barrier layer JSON must publish seven canonical colors.");

        AssertSphere(profiles, layers);
        AssertSingleLayerProfiles(profiles, layers);
        RequestTestExit(_test.Finish("Barrier profile schema contract regression"));
    }

    private void AssertSphere(
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profiles,
        IReadOnlyDictionary<StringName, BarrierLayerDefinition> layers
    )
    {
        _test.True(profiles.TryGetValue("prismatic_sphere", out BarrierProfileDefinition sphere),
            "Prismatic sphere must be projected from JSON.");
        if (sphere == null)
            return;

        _test.Eq(sphere.ProfileId, new StringName("prismatic_sphere"), "Profile id must be stable.");
        _test.Eq(sphere.Layers.Count, 7, "Prismatic sphere must reference seven layer IDs.");
        _test.False(sphere.CatchAllProjectedEffects,
            "Prismatic sphere blocks only categories matched by remaining layers.");

        for (int index = 0; index < ExpectedLayerIds.Length; index++)
        {
            StringName layerId = ExpectedLayerIds[index];
            _test.True(layers.TryGetValue(layerId, out BarrierLayerDefinition canonical),
                $"Canonical layer must exist: {layerId}.");
            if (canonical == null || index >= sphere.Layers.Count)
                continue;

            BarrierLayerDefinition projected = sphere.Layers[index];
            _test.True(ReferenceEquals(projected, canonical),
                $"Profile layer {layerId} must resolve the canonical layer definition by ID.");
            _test.Eq(projected.LayerId, layerId, "Layer order must preserve 2E color order.");
            _test.Eq(projected.Order, index + 1, "Layer order must be one-based and stable.");
            _test.True(projected.BreakerSkillIds.Contains(ExpectedBreakers[index]),
                $"Layer {layerId} must preserve its breaker skill.");
            _test.True(projected.PassageOutcomes.Count > 0,
                $"Layer {layerId} must preserve at least one passage outcome.");
            if (projected.PassageOutcomes.Count == 0)
                continue;

            BarrierOutcomeDefinition outcome = projected.PassageOutcomes[0];
            _test.Eq(outcome.OutcomeType, ExpectedOutcomes[index],
                $"Layer {layerId} must preserve its typed passage outcome.");
            if (ExpectedStatuses.TryGetValue(layerId, out StringName expectedStatus))
                _test.Eq(outcome.StatusId, expectedStatus,
                    $"Layer {layerId} must preserve its status effect.");
        }
    }

    private void AssertSingleLayerProfiles(
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profiles,
        IReadOnlyDictionary<StringName, BarrierLayerDefinition> layers
    )
    {
        for (int index = 0; index < ExpectedLayerIds.Length; index++)
        {
            StringName profileId = SingleLayerProfileIds[index];
            StringName layerId = ExpectedLayerIds[index];
            _test.True(profiles.TryGetValue(profileId, out BarrierProfileDefinition profile),
                $"Single-layer profile must exist: {profileId}.");
            if (profile == null)
                continue;

            _test.Eq(profile.RadiusCells, 1, $"{profileId} must use radius 1.");
            _test.Eq(profile.DurationTu, 40, $"{profileId} base duration must be 40 TU.");
            _test.False(profile.CatchAllProjectedEffects,
                $"{profileId} must not enable catch-all projected blocking.");
            _test.Eq(profile.Layers.Count, 1,
                $"{profileId} must reference exactly one layer ID.");
            if (profile.Layers.Count == 1 && layers.TryGetValue(layerId, out BarrierLayerDefinition canonical))
                _test.True(ReferenceEquals(profile.Layers[0], canonical),
                    $"{profileId} must reuse canonical layer {layerId} by ID.");
        }
    }
}
