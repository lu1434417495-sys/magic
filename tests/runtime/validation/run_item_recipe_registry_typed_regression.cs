using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_item_recipe_registry_typed_regression : SceneTree
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

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Item/recipe registry typed regression"));
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

}
