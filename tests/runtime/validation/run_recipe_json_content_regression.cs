#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_recipe_json_content_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            ContentImportBatch<RecipeImportModel> production =
                RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
                    RecipeContentJsonAuthoringDomain.ProductionDirectory,
                    new GodotContentJsonSourceReader()
                ).Import();
            _test.False(production.HasErrors, "production recipe JSON imports without diagnostics");
            _test.Eq(production.Entries.Count, 4, "production recipe JSON contains four recipes");

            RecipeImportModel militia = production.Entries
                .Select(value => value.Import)
                .Single(value => value.RecipeId == "forge_militia_axe");
            RecipeDefinition definition = RecipeDefinitionProjector.Project(militia);
            _test.Eq(definition.InputItemIds.Count, 3, "recipe projector preserves all inputs");
            _test.Eq(definition.InputItemQuantities[1], 1, "recipe projector preserves quantities");
            _test.Eq(definition.OutputItemId, new StringName("militia_axe"), "recipe projector preserves output ID");

            string canonical = RecipeImportCanonicalJson.WriteDocument(
                new ContentCanonicalJsonWriter(),
                "canonical_probe",
                new[] { militia }
            );
            ContentImportBatch<RecipeImportModel> roundTrip =
                RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
                    "res://virtual/recipes",
                    new FakeSourceReader(new ContentJsonSourceText("canonical.json", canonical))
                ).Import();
            _test.False(roundTrip.HasErrors, "canonical recipe JSON reimports cleanly");
            _test.Eq(roundTrip.Entries.Count, 1, "canonical recipe JSON retains one entry");
            _test.Eq(roundTrip.Entries[0].Import.Inputs.Count, 3, "canonical round-trip retains inputs");

            ContentImportBatch<RecipeImportModel> invalid = ImportSingle(
                "{\"recipe_id\":\"bad\",\"display_name\":\"Bad\",\"description\":\"\"," +
                "\"inputs\":[{\"item_id\":\"iron_ore\",\"quantity\":0},{\"item_id\":\"iron_ore\",\"quantity\":1}]," +
                "\"output_item_id\":\"militia_axe\",\"output_quantity\":1," +
                "\"required_facility_tags\":[\"forge\",\"forge\"],\"failure_reason\":\"\"}"
            );
            _test.True(invalid.HasErrors, "invalid recipe is rejected");
            _test.True(invalid.Diagnostics.Any(value => value.RuleId == RecipeJsonRules.Quantity), "quantity rule is stable");
            _test.True(invalid.Diagnostics.Any(value => value.RuleId == RecipeJsonRules.DuplicateInput), "duplicate input rule is stable");
            _test.True(invalid.Diagnostics.Any(value => value.RuleId == RecipeJsonRules.DuplicateFacility), "duplicate facility rule is stable");

            ContentImportBatch<RecipeImportModel> invalidId = ImportSingle(
                "{\"recipe_id\":\"\",\"display_name\":\"Bad\",\"description\":\"\"," +
                "\"inputs\":[{\"item_id\":\"iron_ore\",\"quantity\":1}]," +
                "\"output_item_id\":\"militia_axe\",\"output_quantity\":1," +
                "\"required_facility_tags\":[\"forge\"],\"failure_reason\":\"\"}"
            );
            _test.True(
                invalidId.Diagnostics.Any(value =>
                    value.RuleId == ContentJsonDocumentLoader.InvalidEntryIdRule
                ),
                "empty recipe ID is rejected by the document entry-ID rule"
            );

            const string duplicateEntry =
                "{\"recipe_id\":\"duplicate_recipe\",\"display_name\":\"Duplicate\",\"description\":\"\"," +
                "\"inputs\":[{\"item_id\":\"iron_ore\",\"quantity\":1}]," +
                "\"output_item_id\":\"militia_axe\",\"output_quantity\":1," +
                "\"required_facility_tags\":[\"forge\"],\"failure_reason\":\"\"}";
            ContentImportBatch<RecipeImportModel> duplicateIds = ImportEntries(
                duplicateEntry + "," + duplicateEntry
            );
            _test.True(
                duplicateIds.Diagnostics.Any(value =>
                    value.RuleId == ContentJsonDocumentLoader.DuplicateEntryIdRule
                ),
                "duplicate recipe IDs are rejected by the document contract"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected recipe JSON content exception: {exception}");
        }

        RequestTestExit(_test.Finish("Recipe JSON content regression"));
    }

    private static ContentImportBatch<RecipeImportModel> ImportSingle(string entryJson)
        => ImportEntries(entryJson);

    private static ContentImportBatch<RecipeImportModel> ImportEntries(string entriesJson)
    {
        string document =
            "{\"schema\":1,\"domain\":\"recipes\",\"family\":\"invalid\",\"templates\":{},\"entries\":["
            + entriesJson
            + "]}";
        return RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
            "res://virtual/recipes",
            new FakeSourceReader(new ContentJsonSourceText("invalid.json", document))
        ).Import();
    }

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal FakeSourceReader(params ContentJsonSourceText[] sources) => _sources = sources;

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) => _sources;
    }
}
