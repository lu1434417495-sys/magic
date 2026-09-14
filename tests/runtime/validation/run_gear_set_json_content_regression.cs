#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public partial class run_gear_set_json_content_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            ContentImportBatch<GearSetImportModel> production =
                GearSetContentJsonAuthoringDomain.CreateImportDescriptor(
                    GearSetContentJsonAuthoringDomain.ProductionDirectory,
                    new GodotContentJsonSourceReader()
                ).Import();
            _test.False(production.HasErrors, "production gear-set JSON imports without diagnostics");
            _test.Eq(production.Entries.Count, 2, "production gear-set JSON contains two sets");

            GearSetImportModel import = production.Entries
                .Single(entry => entry.Import.GearSetId == "phoenix_rebirth_set")
                .Import;
            GearSetDefinition definition = GearSetDefinitionProjector.Project(import);
            _test.Eq(definition.MemberItemIds.Count, 10, "gear-set projector preserves ten members");
            _test.Eq(definition.Thresholds.Count, 4, "gear-set projector preserves four thresholds");
            _test.Eq(definition.Thresholds[1].AttributeModifiers[0].Value, 20, "gear-set projector preserves threshold modifiers");

            string canonical = GearSetImportCanonicalJson.WriteDocument(
                new ContentCanonicalJsonWriter(),
                "canonical_probe",
                new[] { import }
            );
            ContentImportBatch<GearSetImportModel> roundTrip =
                GearSetContentJsonAuthoringDomain.CreateImportDescriptor(
                    "res://virtual/gear_sets",
                    new FakeSourceReader(new ContentJsonSourceText("canonical.json", canonical))
                ).Import();
            _test.False(roundTrip.HasErrors, "canonical gear-set JSON reimports cleanly");
            _test.Eq(roundTrip.Entries[0].Import.Thresholds.Count, 4, "canonical round-trip retains thresholds");

            ContentImportBatch<GearSetImportModel> invalid = ImportSingle(
                "{\"gear_set_id\":\"bad_set\",\"display_name\":\"Bad\",\"description\":\"\"," +
                "\"member_item_ids\":[\"item_a\",\"item_b\"],\"usage_anchor_item_id\":\"missing\"," +
                "\"thresholds\":[" +
                "{\"threshold_id\":\"two\",\"required_piece_count\":2,\"display_name\":\"\",\"description\":\"\",\"mandatory_member_item_ids\":[],\"attribute_modifiers\":[],\"granted_trait_ids\":[]}," +
                "{\"threshold_id\":\"one\",\"required_piece_count\":1,\"display_name\":\"\",\"description\":\"\",\"mandatory_member_item_ids\":[],\"attribute_modifiers\":[],\"granted_trait_ids\":[]}" +
                "]}"
            );
            _test.True(invalid.HasErrors, "invalid gear set is rejected");
            _test.True(invalid.Diagnostics.Any(value => value.RuleId == GearSetJsonRules.UsageAnchor), "usage-anchor rule is stable");
            _test.True(invalid.Diagnostics.Any(value => value.RuleId == GearSetJsonRules.ThresholdOrder), "threshold-order rule is stable");
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected gear-set JSON content exception: {exception}");
        }

        RequestTestExit(_test.Finish("Gear-set JSON content regression"));
    }

    private static ContentImportBatch<GearSetImportModel> ImportSingle(string entryJson)
    {
        string document =
            "{\"schema\":1,\"domain\":\"gear_sets\",\"family\":\"invalid\",\"templates\":{},\"entries\":["
            + entryJson
            + "]}";
        return GearSetContentJsonAuthoringDomain.CreateImportDescriptor(
            "res://virtual/gear_sets",
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
