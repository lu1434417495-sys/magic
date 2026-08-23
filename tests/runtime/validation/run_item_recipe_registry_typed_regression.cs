using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_item_recipe_registry_typed_regression : LifecycleTestSceneTree
{
    private const string InvalidRecipeDirectory =
        "res://tests/fixtures/resource_validation/recipe_registry_invalid";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestOfficialItemRegistryIsImmutableAndValid();
        TestOfficialRecipeRegistryIsImmutableAndValid();
        TestInvalidRecipeRegistryPreservesDiagnosticsAcrossValidationSurfaces();
        TestRecipeRegistryRejectsMissingItemReference();
        TestItemTraitValidationAcceptsSourceScopedReferences();
        TestItemTraitValidationRejectsWrongSourceAndUnsatisfiableRollGroups();
        TestProjectionRejectsNullNestedValues();
        TestItemDefinitionIsDeeplyReadOnly();
        TestInvalidWeaponDiceRemainInvalid();

        RequestTestExit(_test.Finish("Item/recipe registry typed regression"));
    }

    private void TestOfficialItemRegistryIsImmutableAndValid()
    {
        using ItemContentRegistry registry = new();

        IReadOnlyDictionary<StringName, ItemDefinition> typedItemDefs =
            registry.GetItemDefsTyped();
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GStringArray projectedErrors = registry.Validate();

        _test.Eq(typedErrors.Count, 0, $"正式 typed item validation 不应报错: {FormatErrors(typedErrors)}");
        _test.Eq(
            projectedErrors.Count,
            0,
            $"正式 item registry 不应报错: {FormatErrors(projectedErrors)}"
        );
        _test.True(
            typedItemDefs.ContainsKey("steel_longsword"),
            "typed item defs 应保留正式 steel_longsword。"
        );
        AssertDictionaryRejectsRemoval(
            typedItemDefs,
            "steel_longsword",
            "item registry 返回的 definitions 必须拒绝消费方删除"
        );
    }

    private void TestOfficialRecipeRegistryIsImmutableAndValid()
    {
        using ItemContentRegistry itemRegistry = new();
        using RecipeContentRegistry recipeRegistry = new();

        recipeRegistry.Setup(itemRegistry.GetItemDefsTyped());

        IReadOnlyDictionary<StringName, RecipeDefinition> typedRecipeDefs =
            recipeRegistry.GetRecipeDefsTyped();
        IReadOnlyList<string> typedErrors = recipeRegistry.ValidateTyped();
        GStringArray projectedErrors = recipeRegistry.Validate();

        _test.Eq(typedErrors.Count, 0, $"正式 typed recipe validation 不应报错: {FormatErrors(typedErrors)}");
        _test.Eq(
            projectedErrors.Count,
            0,
            $"正式 recipe registry 不应报错: {FormatErrors(projectedErrors)}"
        );
        _test.True(
            typedRecipeDefs.ContainsKey("forge_militia_axe"),
            "typed recipe defs 应保留正式 forge_militia_axe。"
        );
        AssertDictionaryRejectsRemoval(
            typedRecipeDefs,
            "forge_militia_axe",
            "recipe registry 返回的 definitions 必须拒绝消费方删除"
        );
    }

    private void TestInvalidRecipeRegistryPreservesDiagnosticsAcrossValidationSurfaces()
    {
        using ItemContentRegistry itemRegistry = new();
        using RecipeContentRegistry recipeRegistry = new();

        recipeRegistry.Setup(itemRegistry.GetItemDefsTyped());
        recipeRegistry.LoadFromJsonDirectory(
            InvalidRecipeDirectory,
            new GodotContentJsonSourceReader()
        );

        IReadOnlyList<string> typedErrors = recipeRegistry.ValidateTyped();
        GStringArray projectedErrors = recipeRegistry.Validate();

        AssertContains(typedErrors, ContentJsonDocumentLoader.DuplicateEntryIdRule, "duplicate_recipes.json", "typed validation 应拒绝重复 recipe id。");
        AssertContains(typedErrors, ContentJsonDocumentLoader.InvalidEntryIdRule, "invalid_recipe_id.json", "typed validation 应拒绝缺失 recipe id。");
        AssertContains(projectedErrors, ContentJsonDocumentLoader.DuplicateEntryIdRule, "duplicate_recipes.json", "public validation 应保留重复 recipe id 诊断。");
        AssertContains(projectedErrors, ContentJsonDocumentLoader.InvalidEntryIdRule, "invalid_recipe_id.json", "public validation 应保留缺失 recipe id 诊断。");
    }

    private void TestRecipeRegistryRejectsMissingItemReference()
    {
        const string document =
            "{\"schema\":1,\"domain\":\"recipes\",\"family\":\"missing_reference\",\"templates\":{},\"entries\":[{"
            + "\"recipe_id\":\"missing_reference_recipe\",\"display_name\":\"Missing reference\",\"description\":\"\","
            + "\"inputs\":[{\"item_id\":\"missing_item\",\"quantity\":1}],\"output_item_id\":\"militia_axe\","
            + "\"output_quantity\":1,\"required_facility_tags\":[\"forge\"],\"failure_reason\":\"\"}]}";
        using ItemContentRegistry itemRegistry = new();
        using RecipeContentRegistry recipeRegistry = new();
        recipeRegistry.Setup(itemRegistry.GetItemDefsTyped());
        recipeRegistry.LoadFromJsonDirectory(
            "fixture://missing_recipe_reference",
            new SingleJsonSourceReader("missing_reference.json", document)
        );

        IReadOnlyList<string> typedErrors = recipeRegistry.ValidateTyped();
        GStringArray projectedErrors = recipeRegistry.Validate();
        AssertContains(typedErrors, "missing input item", "missing_item", "typed validation 应拒绝缺失的 input item。");
        AssertContains(projectedErrors, "missing input item", "missing_item", "public validation 应保留缺失 input item 诊断。");
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

    private void TestProjectionRejectsNullNestedValues()
    {
        TestItemDefinitionBuilder badGroupItem = new() { item_id = "bad_null_group" };
        badGroupItem.trait_roll_groups.Add(null);
        AssertInvalidData(
            () => badGroupItem.ToDefinition(),
            "trait_roll_groups[0]",
            "null trait roll group must fail projection"
        );

        TestTraitRollGroupDefinitionBuilder badEntryGroup = new() { group_id = "bad_null_entry" };
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

    private void TestItemDefinitionIsDeeplyReadOnly()
    {
        TestTraitRollGroupDefinitionBuilder rollGroup = new() { group_id = "prefix", roll_count = 1 };
        rollGroup.entries.Add(
            new TestTraitRollGroupEntryDefinitionBuilder { trait_id = "sharp_edge", weight = 2 }
        );
        TestItemDefinitionBuilder itemRaw = new()
        {
            item_id = "flat_sword",
            display_name = "Flat Sword",
            item_category = "equipment",
            equipment_type_id = "weapon",
            is_stackable = false,
            max_stack = 1,
            base_price = 100,
            buy_price = 120,
            sell_price = 60,
            equip_requirement = new EquipmentRequirementDefinition(
                new[] { "fighter" },
                0,
                0,
                new[] { new EquipmentAttributeRequirementDefinition("strength", 12) }
            ),
            weapon_profile = new TestWeaponProfileDefinitionBuilder
            {
                weapon_type_id = "longsword",
                family = "sword",
                range_type = "melee",
                damage_tag = "physical_slash",
                attack_range = 1,
                one_handed_dice = new TestWeaponDamageDiceDefinitionBuilder
                {
                    dice_count = 1,
                    dice_sides = 8,
                },
                properties = new Godot.Collections.Array<StringName> { "versatile" },
            },
        };
        itemRaw.tags.Add("flat_tag");
        itemRaw.trait_roll_groups.Add(rollGroup);
        itemRaw.attribute_modifiers.Add(
            new AttributeModifierDefinition(
                "strength",
                "flat",
                1,
                0,
                "item",
                "flat_sword"
            )
        );

        ItemDefinition definition = itemRaw.ToDefinition();

        _test.Eq(definition.ItemId, new StringName("flat_sword"), "projection should keep item id");
        _test.Eq(definition.DisplayName, "Flat Sword", "projection should keep display text");
        _test.Eq(definition.BasePrice, 100, "projection should keep authored price");
        _test.Eq(definition.Tags.Count, 1, "projection should keep flat tags");
        _test.True(definition.WeaponProfile != null, "projection should keep weapon profile");
        _test.True(definition.EquipRequirement != null, "projection should keep equipment requirement");
        _test.Eq(definition.TraitRollGroups.Count, 1, "projection should keep trait roll groups");
        _test.Eq(
            definition.AttributeModifiers[0].SourceId,
            new StringName("flat_sword"),
            "projection should preserve canonical modifier source id"
        );

        bool mutationRejected = false;
        try
        {
            ((IList<StringName>)definition.Tags).Add("illegal_mutation");
        }
        catch (NotSupportedException)
        {
            mutationRejected = true;
        }
        _test.True(mutationRejected, "definition lists should reject mutation");
        _test.Eq(itemRaw.tags.Count, 1, "projection must not mutate the import fixture");

        TestItemDefinitionBuilder invalidGroupRaw = new()
        {
            item_id = "invalid_group_item",
            CategoryKind = ItemCategoryKind.Equipment,
        };
        invalidGroupRaw.trait_roll_groups.Add(new TestTraitRollGroupDefinitionBuilder { group_id = "" });
        List<string> invalidGroupErrors = ItemTraitContentValidator.Validate(
            new Dictionary<StringName, ItemDefinition>
            {
                [invalidGroupRaw.item_id] = invalidGroupRaw.ToDefinition(),
            },
            new Dictionary<StringName, TraitDefinition>(),
            "fixture_items"
        );
        AssertContains(
            invalidGroupErrors,
            "trait_roll_groups[0].group_id",
            "must be non-empty",
            "item trait validation must reject an empty trait-roll group id"
        );

        TestItemDefinitionBuilder duplicateGroupsRaw = new()
        {
            item_id = "duplicate_group_item",
            CategoryKind = ItemCategoryKind.Equipment,
        };
        duplicateGroupsRaw.trait_roll_groups.Add(
            new TestTraitRollGroupDefinitionBuilder { group_id = "duplicate" }
        );
        duplicateGroupsRaw.trait_roll_groups.Add(
            new TestTraitRollGroupDefinitionBuilder { group_id = "duplicate" }
        );
        List<string> duplicateGroupErrors = ItemTraitContentValidator.Validate(
            new Dictionary<StringName, ItemDefinition>
            {
                [duplicateGroupsRaw.item_id] = duplicateGroupsRaw.ToDefinition(),
            },
            new Dictionary<StringName, TraitDefinition>(),
            "fixture_items"
        );
        AssertContains(
            duplicateGroupErrors,
            "trait_roll_groups[1].group_id",
            "duplicates duplicate",
            "item trait validation must reject duplicate trait-roll group ids"
        );
    }

    private void TestInvalidWeaponDiceRemainInvalid()
    {
        WeaponDamageDiceDefinition invalidDice = new WeaponDamageDiceDefinition(0, -2, 0);
        IReadOnlyList<string> errors = WeaponDamageDiceDefinition.ValidateDice(
            "invalid_weapon",
            invalidDice
        );
        _test.Eq(errors.Count, 2, "zero/negative authored dice must fail validation");

        WeaponProfileDefinition profile = new(
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
        );
        _test.Eq(profile.OneHandedDice.DiceCount, 0, "definition must not normalize invalid dice count");
        _test.Eq(profile.OneHandedDice.DiceSides, -2, "definition must not normalize invalid dice sides");
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

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private sealed class SingleJsonSourceReader : IContentJsonSourceReader
    {
        private readonly string _fileName;
        private readonly string _json;

        internal SingleJsonSourceReader(string fileName, string json)
        {
            _fileName = fileName;
            _json = json;
        }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(
            string directoryPath
        ) => new[] { new ContentJsonSourceText(_fileName, _json) };
    }

    private void AssertDictionaryRejectsRemoval<TValue>(
        IReadOnlyDictionary<StringName, TValue> definitions,
        StringName knownKey,
        string message
    )
    {
        bool mutationRejected = definitions is not IDictionary<StringName, TValue>;
        if (definitions is IDictionary<StringName, TValue> dictionary)
        {
            try
            {
                dictionary.Remove(knownKey);
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }
        }
        _test.True(mutationRejected, message);
        _test.True(definitions.ContainsKey(knownKey), $"{message}，且原定义仍应存在。");
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
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<TraitDamageResistanceEntryDefinition>(),
            System.Array.Empty<TraitSaveBonusEntryDefinition>(),
            System.Array.Empty<TraitSaveTagBonusEntryDefinition>(),
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
        TestItemDefinitionBuilder itemDef = new()
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
            TestTraitRollGroupDefinitionBuilder group = new()
            {
                group_id = "prefix",
                roll_count = rollCount,
            };
            foreach (string traitId in rollTraits)
            {
                group.entries.Add(
                    new TestTraitRollGroupEntryDefinitionBuilder
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
        IEnumerable<string> errors,
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
