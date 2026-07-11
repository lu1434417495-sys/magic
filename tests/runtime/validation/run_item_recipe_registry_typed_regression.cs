using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        TestDefinitionsContainNoGodotObjectGraph();
        TestProjectionRejectsNullNestedResources();
        TestItemMergeIsPureAndDeeplyReadOnly();
        TestWeaponSubresourcesAreTypedAndInvalidDiceRemainInvalid();

        RequestTestExit(_test.Finish("Item/recipe registry typed regression"));
    }

    private void TestOfficialItemRegistryTypedBoundaryMatchesPublicBoundary()
    {
        using ItemContentRegistry registry = new();

        IReadOnlyDictionary<StringName, ItemDefinition> typedItemDefs =
            registry.GetItemDefsTyped();
        _test.True(
            typedItemDefs is not Dictionary<StringName, ItemDefinition>,
            "item registry should not expose a mutable dictionary."
        );
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

        IReadOnlyDictionary<StringName, RecipeDefinition> typedRecipeDefs =
            recipeRegistry.GetRecipeDefsTyped();
        _test.True(
            typedRecipeDefs is not Dictionary<StringName, RecipeDefinition>,
            "recipe registry should not expose a mutable dictionary."
        );
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
        Dictionary<StringName, ItemDefinition> items = new()
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
        Dictionary<StringName, ItemDefinition> items = new()
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

    private void TestDefinitionsContainNoGodotObjectGraph()
    {
        Type[] definitionTypes =
        {
            typeof(ItemDefinition),
            typeof(RecipeDefinition),
            typeof(TraitRollGroupDefinition),
            typeof(TraitRollGroupEntryDefinition),
            typeof(WeaponProfileDefinition),
            typeof(WeaponDamageDiceDefinition),
            typeof(EquipmentRequirementDefinition),
            typeof(EquipmentAttributeRequirementDefinition),
        };

        foreach (Type definitionType in definitionTypes)
        {
            foreach (
                PropertyInfo property in definitionType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public
                )
            )
            {
                foreach (Type inspected in EnumerateTypeGraph(property.PropertyType))
                {
                    _test.True(
                        !typeof(GodotObject).IsAssignableFrom(inspected),
                        $"{definitionType.Name}.{property.Name} must not retain GodotObject type {inspected.FullName}."
                    );
                    _test.True(
                        inspected.FullName == null
                            || !inspected.FullName.StartsWith(
                                "Godot.Collections.",
                                StringComparison.Ordinal
                            ),
                        $"{definitionType.Name}.{property.Name} must not retain Godot collection type {inspected.FullName}."
                    );
                }
            }
        }
    }

    private void TestProjectionRejectsNullNestedResources()
    {
        ItemDef badGroupItem = new() { item_id = "bad_null_group" };
        badGroupItem.trait_roll_groups.Add(null);
        AssertInvalidData(
            () => badGroupItem.ToDefinition(),
            "trait_roll_groups[0]",
            "null trait roll group must fail projection"
        );

        TraitRollGroupDef badEntryGroup = new() { group_id = "bad_null_entry" };
        badEntryGroup.entries.Add(null);
        AssertInvalidData(
            () => badEntryGroup.ToDefinition(),
            "entries[0]",
            "null trait roll entry must fail projection"
        );

        EquipmentRequirement badRequirement = new();
        badRequirement.attribute_requirements.Add(null);
        AssertInvalidData(
            () => badRequirement.ToDefinition(),
            "attribute_requirements[0]",
            "null equipment attribute requirement must fail projection"
        );

        RecipeDef nullQuantities = new()
        {
            recipe_id = "null_quantities",
            input_item_quantities = null,
        };
        AssertInvalidData(
            () => nullQuantities.ToDefinition(),
            "input_item_quantities",
            "null recipe quantities must fail projection"
        );

        RecipeDef mismatchedRecipe = new()
        {
            recipe_id = "mismatched_recipe",
            input_item_ids = new Godot.Collections.Array<StringName> { "ore" },
            input_item_quantities = Array.Empty<int>(),
        };
        AssertInvalidData(
            () => mismatchedRecipe.ToDefinition(),
            "input_item_quantities",
            "recipe id/quantity count mismatch must fail projection"
        );

        bool typedMismatchRejected = false;
        try
        {
            _ = new RecipeDefinition(
                "typed_mismatch",
                "Typed mismatch",
                "",
                new[] { new StringName("ore") },
                Array.Empty<int>(),
                "ingot",
                1,
                new[] { new StringName("forge") },
                ""
            );
        }
        catch (ArgumentException)
        {
            typedMismatchRejected = true;
        }
        _test.True(typedMismatchRejected, "typed recipe constructor must enforce id/quantity pairing");
    }

    private void TestItemMergeIsPureAndDeeplyReadOnly()
    {
        EquipmentRequirement requirement = new();
        requirement.required_profession_ids.Add("fighter");
        requirement.attribute_requirements.Add(
            new EquipmentAttributeRequirementDef
            {
                attribute_id = "strength",
                min_value = 12,
            }
        );
        TraitRollGroupDef rollGroup = new() { group_id = "prefix", roll_count = 1 };
        rollGroup.entries.Add(
            new TraitRollGroupEntryDef { trait_id = "sharp_edge", weight = 2 }
        );
        ItemDef templateRaw = new()
        {
            item_id = "template_sword",
            display_name = "Template Sword",
            item_category = "equipment",
            equipment_type_id = "weapon",
            is_stackable = false,
            max_stack = 1,
            base_price = 100,
            buy_price = 120,
            sell_price = 60,
            equip_requirement = requirement,
            weapon_profile = new WeaponProfileDef
            {
                weapon_type_id = "longsword",
                family = "sword",
                range_type = "melee",
                damage_tag = "physical_slash",
                attack_range = 1,
                one_handed_dice = new WeaponDamageDiceDef
                {
                    dice_count = 1,
                    dice_sides = 8,
                },
                properties_mode = (int)WeaponProfileDef.PropertyMergeMode.REPLACE,
                properties = new Godot.Collections.Array<StringName> { "versatile" },
            },
        };
        templateRaw.tags.Add("template_tag");
        templateRaw.trait_roll_groups.Add(rollGroup);
        templateRaw.attribute_modifiers.Add(
            new AttributeModifier
            {
                attribute_id = "strength",
                mode = "flat",
                value = 1,
                source_type = "item",
                source_id = "template_sword",
            }
        );

        ItemDef instanceRaw = new()
        {
            item_id = "derived_sword",
            base_item_id = "template_sword",
            is_stackable = false,
            max_stack = 1,
            sellable = true,
        };
        instanceRaw.tags.Add("instance_tag");

        ItemDefinition template = templateRaw.ToDefinition();
        ItemDefinition instance = instanceRaw.ToDefinition();
        ItemDefinition merged = ItemDefinition.MergeWithTemplate(template, instance);

        _test.Eq(merged.ItemId, new StringName("derived_sword"), "merge should keep instance id");
        _test.Eq(merged.BaseItemId, new StringName(""), "merge should clear base item id");
        _test.Eq(merged.DisplayName, "Template Sword", "empty instance text should inherit");
        _test.Eq(merged.BasePrice, 100, "zero instance price should inherit");
        _test.Eq(merged.Tags.Count, 2, "merge should union template and instance tags");
        _test.True(
            !ReferenceEquals(merged.WeaponProfile, template.WeaponProfile),
            "merge should deep-copy weapon profile definition"
        );
        _test.True(
            !ReferenceEquals(merged.EquipRequirement, template.EquipRequirement),
            "merge should deep-copy equipment requirement definition"
        );
        _test.True(
            !ReferenceEquals(merged.TraitRollGroups[0], template.TraitRollGroups[0]),
            "merge should deep-copy trait roll definitions"
        );
        _test.Eq(
            merged.AttributeModifiers[0].SourceId,
            new StringName("derived_sword"),
            "merged modifiers should rewrite source_id to final item id"
        );

        bool mutationRejected = false;
        try
        {
            ((IList<StringName>)merged.Tags).Add("illegal_mutation");
        }
        catch (NotSupportedException)
        {
            mutationRejected = true;
        }
        _test.True(mutationRejected, "definition lists should reject mutation");
        _test.Eq(template.Tags.Count, 1, "merge must not mutate template definition");
        _test.Eq(instance.Tags.Count, 1, "merge must not mutate instance definition");

        ItemDef invalidGroupRaw = new() { item_id = "invalid_group_template" };
        invalidGroupRaw.trait_roll_groups.Add(new TraitRollGroupDef { group_id = "" });
        AssertInvalidData(
            () => ItemDefinition.MergeWithTemplate(
                invalidGroupRaw.ToDefinition(),
                instance
            ),
            "template.trait_roll_groups[0].group_id",
            "merge must not erase an empty trait-roll group id"
        );

        ItemDef duplicateGroupsRaw = new() { item_id = "duplicate_group_template" };
        duplicateGroupsRaw.trait_roll_groups.Add(
            new TraitRollGroupDef { group_id = "duplicate" }
        );
        duplicateGroupsRaw.trait_roll_groups.Add(
            new TraitRollGroupDef { group_id = "duplicate" }
        );
        AssertInvalidData(
            () => ItemDefinition.MergeWithTemplate(
                duplicateGroupsRaw.ToDefinition(),
                instance
            ),
            "template.trait_roll_groups[1].group_id",
            "merge must not erase duplicate trait-roll group ids"
        );
    }

    private void TestWeaponSubresourcesAreTypedAndInvalidDiceRemainInvalid()
    {
        _test.Eq(
            typeof(ItemDef).GetField(nameof(ItemDef.equip_requirement))?.FieldType,
            typeof(EquipmentRequirement),
            "item equip_requirement authoring field must reject unrelated Resource types"
        );
        _test.Eq(
            typeof(ItemDef).GetField(nameof(ItemDef.weapon_profile))?.FieldType,
            typeof(WeaponProfileDef),
            "item weapon_profile authoring field must reject unrelated Resource types"
        );
        _test.True(
            typeof(WeaponProfileDef).GetMethod("Merge", BindingFlags.Public | BindingFlags.Static)
                == null
                && typeof(WeaponProfileDef).GetMethod("DuplicateProfile") == null
                && typeof(WeaponDamageDiceDef).GetMethod("DuplicateDice") == null,
            "raw weapon Resources must not retain merge/duplicate runtime APIs"
        );

        WeaponDamageDiceDefinition invalidDice = new WeaponDamageDiceDefinition(0, -2, 0);
        IReadOnlyList<string> errors = WeaponDamageDiceDefinition.ValidateDice(
            "invalid_weapon",
            invalidDice
        );
        _test.Eq(errors.Count, 2, "zero/negative authored dice must fail validation");

        WeaponProfileDefinition merged = WeaponProfileDefinition.Merge(
            null,
            new WeaponProfileDefinition(
                "invalid_weapon",
                "",
                "melee",
                "",
                "physical_slash",
                1,
                invalidDice,
                null,
                (int)WeaponProfileDefinition.PropertyMergeMode.REPLACE,
                Array.Empty<StringName>()
            )
        );
        _test.Eq(merged.OneHandedDice.DiceCount, 0, "merge must not normalize invalid dice count");
        _test.Eq(merged.OneHandedDice.DiceSides, -2, "merge must not normalize invalid dice sides");
    }

    private void AssertInvalidData(Action action, string pathFragment, string message)
    {
        try
        {
            action();
            _test.Fail($"{message}: expected InvalidDataException.");
        }
        catch (InvalidDataException exception)
        {
            _test.True(
                exception.Message.Contains(pathFragment, StringComparison.Ordinal),
                $"{message}: path should contain {pathFragment}, got {exception.Message}"
            );
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"{message}: expected InvalidDataException, got {exception.GetType().Name}."
            );
        }
    }

    private static IEnumerable<Type> EnumerateTypeGraph(Type root)
    {
        var seen = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Type type = pending.Pop();
            if (type == null || !seen.Add(type))
                continue;
            yield return type;
            if (type.HasElementType)
                pending.Push(type.GetElementType());
            foreach (Type argument in type.GetGenericArguments())
                pending.Push(argument);
        }
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static GDictionary ProjectItemDefs(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDefinition itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null)
                continue;
            result[itemId] = itemId.ToString();
        }
        return result;
    }

    private static GDictionary ProjectRecipeDefs(
        IReadOnlyDictionary<StringName, RecipeDefinition> recipeDefs
    )
    {
        GDictionary result = new();
        if (recipeDefs == null)
            return result;
        foreach ((StringName recipeId, RecipeDefinition recipeDef) in recipeDefs)
        {
            if (recipeId == "" || recipeDef == null)
                continue;
            result[recipeId] = recipeId.ToString();
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

    private static ItemDefinition BuildEquipmentItem(
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

        return itemDef.ToDefinition();
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
