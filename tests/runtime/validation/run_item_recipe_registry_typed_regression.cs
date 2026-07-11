using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_item_recipe_registry_typed_regression : LifecycleTestSceneTree
{
    private const string InvalidRecipeDirectory =
        "res://tests/fixtures/resource_validation/recipe_registry_invalid";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialItemRegistryTypedBoundaryMatchesPublicBoundary();
        TestOfficialRecipeRegistryTypedBoundaryMatchesPublicBoundary();
        TestInvalidRecipeRegistryTypedBoundaryMatchesPublicBoundary();
        TestItemTraitValidationAcceptsSourceScopedReferences();
        TestItemTraitValidationRejectsWrongSourceAndUnsatisfiableRollGroups();

        RequestTestExit(_test.Finish("Item/recipe registry typed regression"));
    }

    private void TestOfficialItemRegistryTypedBoundaryMatchesPublicBoundary()
    {
        using ItemContentRegistry registry = new();

        IReadOnlyDictionary<StringName, ItemDef> typedItemDefs = registry.GetItemDefsTyped();
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GDictionary projectedItemDefs = ProjectItemDefs(typedItemDefs);
        GStringArray projectedErrors = registry.Validate();

        _test.Eq(
            typedItemDefs.Count,
            projectedItemDefs.Count,
            "item registry typed/public item defs 数量应保持一致。"
        );
        _test.Eq(
            typedErrors.Count,
            projectedErrors.Count,
            "item registry typed/public validation error 数量应保持一致。"
        );
        _test.Eq(
            projectedErrors.Count,
            0,
            $"正式 item registry 不应报错: {FormatErrors(projectedErrors)}"
        );
        _test.True(
            typedItemDefs.ContainsKey("steel_longsword"),
            "typed item defs 应保留正式 steel_longsword。"
        );
    }

    private void TestOfficialRecipeRegistryTypedBoundaryMatchesPublicBoundary()
    {
        using ItemContentRegistry itemRegistry = new();
        using RecipeContentRegistry recipeRegistry = new();

        recipeRegistry.Setup(itemRegistry.GetItemDefsTyped());

        IReadOnlyDictionary<StringName, RecipeDef> typedRecipeDefs = recipeRegistry.GetRecipeDefsTyped();
        IReadOnlyList<string> typedErrors = recipeRegistry.ValidateTyped();
        GDictionary projectedRecipeDefs = ProjectRecipeDefs(typedRecipeDefs);
        GStringArray projectedErrors = recipeRegistry.Validate();

        _test.Eq(
            typedRecipeDefs.Count,
            projectedRecipeDefs.Count,
            "recipe registry typed/public recipe defs 数量应保持一致。"
        );
        _test.Eq(
            typedErrors.Count,
            projectedErrors.Count,
            "recipe registry typed/public validation error 数量应保持一致。"
        );
        _test.Eq(
            projectedErrors.Count,
            0,
            $"正式 recipe registry 不应报错: {FormatErrors(projectedErrors)}"
        );
    }

    private void TestInvalidRecipeRegistryTypedBoundaryMatchesPublicBoundary()
    {
        using ItemContentRegistry itemRegistry = new();
        using RecipeContentRegistry recipeRegistry = new();

        recipeRegistry.Setup(itemRegistry.GetItemDefsTyped());
        recipeRegistry.LoadFromDirectory(InvalidRecipeDirectory);

        IReadOnlyList<string> typedErrors = recipeRegistry.ValidateTyped();
        GStringArray projectedErrors = recipeRegistry.Validate();

        _test.Eq(
            typedErrors.Count,
            projectedErrors.Count,
            "invalid recipe fixture 下 typed/public validation error 数量应保持一致。"
        );
        _test.True(
            typedErrors.Count > 0,
            $"invalid recipe fixture 应保持非法。 errors={FormatErrors(typedErrors)}"
        );
    }

    private void TestItemTraitValidationAcceptsSourceScopedReferences()
    {
        Dictionary<StringName, TraitDefinition> traits = BuildTraitDefinitions();
        Dictionary<StringName, ItemDef> items = new()
        {
            ["trait_sword"] = BuildEquipmentItem(
                "trait_sword",
                fixedTraits: new[] { "guarded_grip" },
                rollTraits: new[] { "sharp_edge" }
            ),
        };

        List<string> errors = ItemTraitContentValidator.Validate(items, traits, "fixture_items");

        _test.Eq(
            errors.Count,
            0,
            $"Valid item trait references should pass. errors={FormatErrors(errors)}"
        );
    }

    private void TestItemTraitValidationRejectsWrongSourceAndUnsatisfiableRollGroups()
    {
        Dictionary<StringName, TraitDefinition> traits = BuildTraitDefinitions();
        Dictionary<StringName, ItemDef> items = new()
        {
            ["bad_fixed"] = BuildEquipmentItem(
                "bad_fixed",
                fixedTraits: new[] { "identity_only" },
                rollTraits: System.Array.Empty<string>()
            ),
            ["bad_roll"] = BuildEquipmentItem(
                "bad_roll",
                fixedTraits: System.Array.Empty<string>(),
                rollTraits: new[] { "guarded_grip" }
            ),
            ["bad_exclusive"] = BuildEquipmentItem(
                "bad_exclusive",
                fixedTraits: System.Array.Empty<string>(),
                rollTraits: new[] { "sharp_edge", "heavy_head" },
                rollCount: 2,
                exclusiveGroup: "prefix"
            ),
        };

        List<string> errors = ItemTraitContentValidator.Validate(items, traits, "fixture_items");

        AssertContains(
            errors,
            "bad_fixed",
            "equipment_fixed",
            "fixed trait should require equipment_fixed source."
        );
        AssertContains(
            errors,
            "bad_roll",
            "equipment_roll",
            "roll group trait should require equipment_roll source."
        );
        AssertContains(
            errors,
            "bad_exclusive",
            "unsatisfiable",
            "exclusive groups should reject impossible roll_count."
        );
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static GDictionary ProjectItemDefs(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null)
                continue;
            result[itemId] = itemDef;
        }
        return result;
    }

    private static GDictionary ProjectRecipeDefs(
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs
    )
    {
        GDictionary result = new();
        if (recipeDefs == null)
            return result;
        foreach ((StringName recipeId, RecipeDef recipeDef) in recipeDefs)
        {
            if (recipeId == "" || recipeDef == null)
                continue;
            result[recipeId] = recipeDef;
        }
        return result;
    }

    private static Dictionary<StringName, TraitDefinition> BuildTraitDefinitions()
    {
        return new Dictionary<StringName, TraitDefinition>
        {
            ["guarded_grip"] = BuildTraitDefinition(
                "guarded_grip",
                [new StringName("equipment_fixed")]
            ),
            ["sharp_edge"] = BuildTraitDefinition(
                "sharp_edge",
                [new StringName("equipment_roll")],
                [
                    new TraitRollValueSchemaEntryDefinition(
                        "amount",
                        "int",
                        1,
                        6,
                        System.Array.Empty<StringName>()
                    ),
                ]
            ),
            ["heavy_head"] = BuildTraitDefinition(
                "heavy_head",
                [new StringName("equipment_roll")]
            ),
            ["identity_only"] = BuildTraitDefinition(
                "identity_only",
                [new StringName("identity")]
            ),
        };
    }

    private static TraitDefinition BuildTraitDefinition(
        StringName traitId,
        IReadOnlyList<StringName> allowedSourceKinds,
        IReadOnlyList<TraitRollValueSchemaEntryDefinition> rollValueSchema = null
    ) =>
        new(
            traitId,
            traitId.ToString(),
            "Validation fixture.",
            System.Array.Empty<StringName>(),
            allowedSourceKinds,
            "attribute_modifier",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<TraitDamageResistanceEntryDefinition>(),
            System.Array.Empty<TraitSaveBonusEntryDefinition>(),
            System.Array.Empty<TraitPassiveStatusEffectDefinition>(),
            rollValueSchema ?? System.Array.Empty<TraitRollValueSchemaEntryDefinition>()
        );

    private static ItemDef BuildEquipmentItem(
        string itemId,
        string[] fixedTraits,
        string[] rollTraits,
        int rollCount = 1,
        string exclusiveGroup = ""
    )
    {
        ItemDef itemDef = new()
        {
            item_id = itemId,
            display_name = itemId,
            item_category = "equipment",
            equipment_type_id = "weapon",
            is_stackable = false,
            max_stack = 1,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            trait_ids = ToStringNameArray(fixedTraits),
        };

        if (rollTraits != null && rollTraits.Length > 0)
        {
            TraitRollGroupDef group = new()
            {
                group_id = "prefix",
                roll_count = rollCount,
            };
            foreach (string traitId in rollTraits)
            {
                group.entries.Add(
                    new TraitRollGroupEntryDef
                    {
                        trait_id = traitId,
                        weight = 1,
                        exclusive_group = exclusiveGroup,
                    }
                );
            }
            itemDef.trait_roll_groups.Add(group);
        }

        return itemDef;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(string[] values)
    {
        Godot.Collections.Array<StringName> result = new();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value ?? "");
        return result;
    }

    private void AssertContains(
        IReadOnlyList<string> errors,
        string firstNeedle,
        string secondNeedle,
        string message
    )
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(firstNeedle) && (error ?? "").Contains(secondNeedle))
                return;
        }
        _test.Fail($"{message} errors={FormatErrors(errors)}");
    }

}
