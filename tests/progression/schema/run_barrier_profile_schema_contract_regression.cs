using System.Collections.Generic;
using Godot;

public partial class run_barrier_profile_schema_contract_regression : LifecycleTestSceneTree
{
    private const string ProfilePath = "res://data/configs/barriers/prismatic_sphere.tres";

    private static readonly string[] RequiredScriptPaths =
    {
        "res://scripts/player/progression/BarrierProfileDef.cs",
        "res://scripts/player/progression/BarrierLayerDef.cs",
        "res://scripts/player/progression/BarrierOutcomeDef.cs",
        "res://scripts/player/progression/BarrierContentRegistry.cs",
    };

    private static readonly StringName[] ExpectedLayerIds =
    {
        "red",
        "orange",
        "yellow",
        "green",
        "blue",
        "indigo",
        "violet",
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
        "damage",
        "damage",
        "damage",
        "poison_death",
        "status",
        "status",
        "banish",
    };

    private static readonly Dictionary<StringName, StringName> ExpectedStatuses = new()
    {
        ["blue"] = "petrified",
        ["indigo"] = "madness",
    };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestBarrierProfileScriptsExist();
        TestPrismaticSphereProfileIsDataOwned();
        TestPrismaticSphereProfileDeclares2eContract();
        TestSingleLayerProfilesReuseCanonicalLayers();

        RequestTestExit(_test.Finish("Barrier profile schema contract regression"));
    }

    private void TestSingleLayerProfilesReuseCanonicalLayers()
    {
        BarrierProfileDef sphere = LoadPrismaticSphereProfile(reportMissing: false);
        if (sphere == null || sphere.layers.Count != ExpectedLayerIds.Length)
            return;

        for (int index = 0; index < ExpectedLayerIds.Length; index++)
        {
            StringName layerId = ExpectedLayerIds[index];
            string profilePath =
                $"res://data/configs/barriers/{SingleLayerProfileIds[index]}.tres";
            BarrierProfileDef profile = GD.Load<BarrierProfileDef>(profilePath);
            if (profile == null)
            {
                _test.Fail($"Single-layer barrier profile must load: {profilePath}.");
                continue;
            }

            _test.Eq(
                profile.profile_id,
                SingleLayerProfileIds[index],
                "Single-layer barrier profile id must match its file contract."
            );
            _test.Eq(profile.radius_cells, 1, $"{profile.profile_id} must use radius 1.");
            _test.Eq(profile.duration_tu, 40, $"{profile.profile_id} base duration must be 40 TU.");
            _test.False(
                profile.catch_all_projected_effects,
                $"{profile.profile_id} must not enable catch-all projected blocking."
            );
            _test.Eq(
                profile.layers.Count,
                1,
                $"{profile.profile_id} must contain exactly one canonical layer."
            );
            if (profile.layers.Count != 1 || profile.layers[0] == null)
                continue;

            BarrierLayerDef singleLayer = profile.layers[0];
            BarrierLayerDef sphereLayer = sphere.layers[index];
            string expectedLayerPath =
                $"res://data/configs/barrier_layers/prismatic/{layerId}.tres";
            _test.Eq(
                singleLayer.ResourcePath,
                expectedLayerPath,
                $"{profile.profile_id} must reference the shared {layerId} layer resource."
            );
            _test.Eq(
                sphereLayer.ResourcePath,
                expectedLayerPath,
                $"Full prismatic sphere must reference the same shared {layerId} layer resource."
            );
            _test.Eq(
                singleLayer.layer_id,
                layerId,
                $"{profile.profile_id} must expose only the {layerId} layer."
            );
        }
    }

    private void TestBarrierProfileScriptsExist()
    {
        foreach (string scriptPath in RequiredScriptPaths)
        {
            AssertResourceScript(scriptPath);
        }
    }

    private void TestPrismaticSphereProfileIsDataOwned()
    {
        BarrierProfileDef profile = LoadPrismaticSphereProfile(reportMissing: true);
        if (profile == null)
        {
            return;
        }

        AssertHasProperty(profile, "profile_id", "BarrierProfileDef must expose profile_id.");
        AssertHasProperty(profile, "layers", "BarrierProfileDef must expose ordered layers.");
        AssertHasProperty(profile, "anchor_mode", "BarrierProfileDef must expose anchor_mode.");
        AssertHasProperty(profile, "area_pattern", "BarrierProfileDef must expose area_pattern.");
        AssertHasProperty(profile, "radius_cells", "BarrierProfileDef must expose radius_cells.");
        AssertHasProperty(
            profile,
            "catch_all_projected_effects",
            "BarrierProfileDef must explicitly declare catch-all projected blocking policy."
        );
        _test.Eq(
            profile.profile_id,
            (StringName)"prismatic_sphere",
            "Prismatic sphere profile id must be stable."
        );
        _test.False(
            profile.catch_all_projected_effects,
            "Prismatic sphere must only block projected effects matched by a remaining layer."
        );
    }

    private void TestPrismaticSphereProfileDeclares2eContract()
    {
        BarrierProfileDef profile = LoadPrismaticSphereProfile(reportMissing: false);
        if (profile == null)
        {
            return;
        }

        _test.Eq(profile.layers.Count, 7, "Prismatic sphere profile must declare exactly seven layers.");

        for (int index = 0; index < ExpectedLayerIds.Length; index++)
        {
            if (index >= profile.layers.Count)
            {
                return;
            }

            BarrierLayerDef layer = profile.layers[index];
            if (layer == null)
            {
                _test.Fail($"Prismatic sphere layer {index} must be a BarrierLayerDef resource.");
                continue;
            }

            AssertHasProperty(layer, "layer_id", "BarrierLayerDef must expose layer_id.");
            AssertHasProperty(layer, "order", "BarrierLayerDef must expose order.");
            AssertHasProperty(
                layer,
                "blocked_categories",
                "BarrierLayerDef must expose blocked_categories."
            );
            AssertHasProperty(
                layer,
                "breaker_skill_ids",
                "BarrierLayerDef must expose breaker_skill_ids."
            );
            AssertHasProperty(
                layer,
                "passage_outcomes",
                "BarrierLayerDef must expose passage_outcomes."
            );
            _test.Eq(
                layer.layer_id,
                ExpectedLayerIds[index],
                "Prismatic sphere layer order must match 2E color order."
            );
            _test.Eq(
                layer.order,
                index + 1,
                "Prismatic sphere layer order field must be one-based and stable."
            );
            _test.True(
                layer.breaker_skill_ids.Contains(ExpectedBreakers[index]),
                $"Layer {layer.layer_id} must declare its breaker skill in data."
            );
            _test.True(
                layer.passage_outcomes.Count > 0,
                $"Layer {layer.layer_id} must declare at least one passage outcome."
            );
            if (layer.passage_outcomes.Count == 0)
            {
                continue;
            }

            BarrierOutcomeDef outcome = layer.passage_outcomes[0];
            AssertHasProperty(outcome, "outcome_type", "BarrierOutcomeDef must expose outcome_type.");
            AssertHasProperty(outcome, "save_ability", "BarrierOutcomeDef must expose save_ability.");
            AssertHasProperty(outcome, "save_tag", "BarrierOutcomeDef must expose save_tag.");
            _test.Eq(
                outcome.outcome_type,
                ExpectedOutcomes[index],
                $"Layer {layer.layer_id} must declare the expected passage outcome type."
            );
            if (ExpectedStatuses.TryGetValue(layer.layer_id, out StringName expectedStatus))
            {
                AssertHasProperty(outcome, "status_id", "Status outcomes must expose status_id.");
                _test.Eq(
                    outcome.status_id,
                    expectedStatus,
                    $"Layer {layer.layer_id} must declare its status effect in data."
                );
            }
        }
    }

    private BarrierProfileDef LoadPrismaticSphereProfile(bool reportMissing)
    {
        if (!FileAccess.FileExists(ProfilePath))
        {
            if (reportMissing)
            {
                _test.Fail($"Prismatic sphere barrier profile must live at {ProfilePath}.");
            }
            return null;
        }

        Resource resource = GD.Load<Resource>(ProfilePath);
        BarrierProfileDef profile = resource as BarrierProfileDef;
        if (profile == null && reportMissing)
        {
            _test.Fail("Prismatic sphere barrier profile must load as a Resource.");
        }
        return profile;
    }

    private void AssertResourceScript(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            _test.Fail($"Required barrier content script is missing: {path}.");
            return;
        }

        Script script = GD.Load<Script>(path);
        if (script == null)
        {
            _test.Fail($"Required barrier content script must load: {path}.");
        }
    }

    private void AssertHasProperty(GodotObject instance, string propertyName, string message)
    {
        if (!HasProperty(instance, propertyName))
        {
            _test.Fail(message);
        }
    }

    private static bool HasProperty(GodotObject instance, string propertyName)
    {
        if (instance == null)
        {
            return false;
        }

        foreach (Godot.Collections.Dictionary property in instance.GetPropertyList())
        {
            if (property.TryGetValue("name", out Variant name) && name.AsString() == propertyName)
            {
                return true;
            }
        }
        return false;
    }
}
