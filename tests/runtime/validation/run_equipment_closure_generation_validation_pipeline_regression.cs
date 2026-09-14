#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_closure_generation_validation_pipeline_regression
    : LifecycleTestSceneTree
{
    private static readonly EquipmentClosureGenerationSourceSet Sources = new(
        "fixture://items",
        "fixture://traits",
        "fixture://equipment_abilities",
        "fixture://gear_sets",
        "fixture://recipes"
    );

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunTests);
    }

    private void RunTests()
    {
        try
        {
            TestSchemaRejection();
            TestDomainRejection();
            TestCrossDomainRejection();
            TestCrossDomainAssetIdRejection();
            TestCrossDomainProfessionIdRejection();
            TestCrossDomainBusinessContractRejection();
            TestGearSetBusinessContractRejection();
            TestBattleSimulationRejection();
            TestAcceptedClosureAndProtocol();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected equipment closure pipeline exception: {exception}");
        }
        RequestTestExit(_test.Finish("Equipment closure generation validation pipeline regression"));
    }

    private void TestSchemaRejection()
    {
        string item = ValidItemEntry().Replace(
            "\"max_stack\":1",
            "\"max_stack\":\"one\"",
            StringComparison.Ordinal
        );
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(item, gate);
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.Schema,
            EquipmentClosureGenerationValidationExitCodes.SchemaRejected,
            ItemJsonImportParser.InvalidDtoRule,
            "schema-invalid item"
        );
        _test.False(gate.Called, "schema rejection should not invoke BattleSim");
    }

    private void TestDomainRejection()
    {
        string item = ValidItemEntry().Replace(
            "\"item_category\":\"equipment\"",
            "\"item_category\":\"relic\"",
            StringComparison.Ordinal
        );
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(item, gate);
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.Domain,
            EquipmentClosureGenerationValidationExitCodes.DomainRejected,
            ItemImportModelValidator.CategoryRule,
            "domain-invalid item"
        );
        _test.False(gate.Called, "domain rejection should not invoke BattleSim");
    }

    private void TestCrossDomainRejection()
    {
        string item = ValidItemEntry().Replace(
            "\"trait_ids\":[]",
            "\"trait_ids\":[\"missing_generated_trait\"]",
            StringComparison.Ordinal
        );
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(item, gate);
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.CrossDomain,
            EquipmentClosureGenerationValidationExitCodes.CrossDomainRejected,
            EquipmentClosureGenerationCrossDomainRules.MissingItemTrait,
            "missing item trait"
        );
        ContentJsonDiagnostic diagnostic = report.Stages[^1].Diagnostics[0];
        _test.Eq(
            diagnostic.SourceLabel,
            "items.json#generated_test_badge",
            "cross-domain diagnostic source label"
        );
        _test.Eq(
            diagnostic.JsonPointer,
            "/entries/0/trait_ids/0",
            "cross-domain diagnostic JSON pointer"
        );
        _test.False(gate.Called, "cross-domain rejection should not invoke BattleSim");
    }

    private void TestBattleSimulationRejection()
    {
        var gate = new FakeGate(reject: true);
        EquipmentClosureGenerationValidationReport report = Validate(
            ValidItemEntry(),
            gate
        );
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.BattleSimulation,
            EquipmentClosureGenerationValidationExitCodes.BattleSimulationRejected,
            EquipmentClosureGenerationBattleSimRules.HighStrengthOutlier,
            "BattleSim outlier"
        );
        _test.True(gate.Called, "BattleSim stage should invoke the configured real-gate port");
    }

    private void TestCrossDomainAssetIdRejection()
    {
        string item = ValidItemEntry().Replace(
            "\"icon_asset_id\":\"\"",
            "\"icon_asset_id\":\"missing.generated.texture\"",
            StringComparison.Ordinal
        );
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(item, gate);
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.CrossDomain,
            EquipmentClosureGenerationValidationExitCodes.CrossDomainRejected,
            EquipmentClosureGenerationCrossDomainRules.MissingTextureAsset,
            "missing texture asset"
        );
        _test.Eq(
            report.Stages[^1].Diagnostics[0].JsonPointer,
            "/entries/0/icon_asset_id",
            "asset diagnostic keeps the field pointer"
        );
    }

    private void TestCrossDomainProfessionIdRejection()
    {
        string item = ValidItemEntry().Replace(
            "\"equip_requirement\":null",
            "\"equip_requirement\":{\"required_profession_ids\":[\"missing_generated_profession\"],\"min_body_size\":0,\"max_body_size\":0,\"attribute_requirements\":[]}",
            StringComparison.Ordinal
        );
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(item, gate);
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.CrossDomain,
            EquipmentClosureGenerationValidationExitCodes.CrossDomainRejected,
            EquipmentClosureGenerationCrossDomainRules.MissingItemProfession,
            "missing profession ID"
        );
        _test.Eq(
            report.Stages[^1].Diagnostics[0].JsonPointer,
            "/entries/0/equip_requirement/required_profession_ids/0",
            "profession diagnostic keeps the field pointer"
        );
    }

    private void TestCrossDomainBusinessContractRejection()
    {
        string item = ValidItemEntry().Replace(
            "\"trait_ids\":[]",
            "\"trait_ids\":[\"brave\"]",
            StringComparison.Ordinal
        );
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(item, gate);
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.CrossDomain,
            EquipmentClosureGenerationValidationExitCodes.CrossDomainRejected,
            EquipmentClosureGenerationCrossDomainRules.ItemTraitContract,
            "item trait source contract"
        );
        ContentJsonDiagnostic diagnostic = report.Stages[^1].Diagnostics[0];
        _test.Eq(
            diagnostic.JsonPointer,
            "/entries/0/trait_ids/0",
            "production item-trait contract diagnostic keeps the field pointer"
        );
        _test.False(gate.Called, "business-contract rejection should not invoke BattleSim");
    }

    private void TestAcceptedClosureAndProtocol()
    {
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(
            ValidItemEntry(),
            gate
        );
        _test.True(report.Success, "valid generated equipment closure should pass four stages");
        _test.Eq(
            report.ExitCode,
            EquipmentClosureGenerationValidationExitCodes.Success,
            "accepted closure exit code"
        );
        _test.Eq(report.Stages.Count, 4, "accepted closure should report four stages");
        _test.True(gate.Called, "accepted closure should invoke BattleSim");
        string json = EquipmentClosureGenerationValidationProtocol.FormatJson(report);
        string ndjson = EquipmentClosureGenerationValidationProtocol.FormatNdjson(report);
        _test.True(
            json.Contains(
                "\"protocol\": \"magic.equipment_closure.validation/v1\"",
                StringComparison.Ordinal
            ),
            "JSON protocol ID"
        );
        _test.True(
            json.Contains("\"rejected_stage\": null", StringComparison.Ordinal),
            "accepted JSON summary"
        );
        _test.True(
            ndjson.Contains("\"type\":\"stage_summary\"", StringComparison.Ordinal),
            "NDJSON stage summary"
        );
        _test.True(
            ndjson.Contains("\"type\":\"summary\"", StringComparison.Ordinal),
            "NDJSON final summary"
        );
        _test.False(
            json.Contains(".tres", StringComparison.OrdinalIgnoreCase)
                || ndjson.Contains(".tres", StringComparison.OrdinalIgnoreCase),
            "generator validation protocol must not expose TRES paths"
        );
    }

    private void TestGearSetBusinessContractRejection()
    {
        const string gearSet =
            "{\"gear_set_id\":\"generated_test_set\",\"display_name\":\"Generated test set\",\"description\":\"\","
            + "\"member_item_ids\":[\"generated_test_badge\"],\"usage_anchor_item_id\":\"generated_test_badge\","
            + "\"thresholds\":[{\"threshold_id\":\"identity_trait\",\"required_piece_count\":1,"
            + "\"display_name\":\"Identity trait\",\"description\":\"\",\"mandatory_member_item_ids\":[],"
            + "\"attribute_modifiers\":[],\"granted_trait_ids\":[\"brave\"]}]}";
        var gate = new FakeGate(reject: false);
        EquipmentClosureGenerationValidationReport report = Validate(
            ValidItemEntry(),
            gate,
            gearSet
        );
        AssertRejected(
            report,
            EquipmentClosureGenerationValidationStageKind.CrossDomain,
            EquipmentClosureGenerationValidationExitCodes.CrossDomainRejected,
            EquipmentClosureGenerationCrossDomainRules.GearSetContract,
            "gear-set trait source contract"
        );
        _test.Eq(
            report.Stages[^1].Diagnostics[0].JsonPointer,
            "/entries/0/thresholds/0/granted_trait_ids/0",
            "gear-set production contract diagnostic keeps the field pointer"
        );
        _test.False(gate.Called, "gear-set business rejection should not invoke BattleSim");
    }

    private EquipmentClosureGenerationValidationReport Validate(
        string itemEntry,
        FakeGate gate,
        string gearSetEntry = ""
    )
    {
        var reader = new FakeSourceReader(new Dictionary<string, string>
        {
            [Sources.ItemsDirectory] = Document("items", itemEntry),
            [Sources.TraitsDirectory] = Document("traits", ""),
            [Sources.EquipmentAbilitiesDirectory] = Document(
                "equipment_abilities",
                ""
            ),
            [Sources.GearSetsDirectory] = Document("gear_sets", gearSetEntry),
            [Sources.RecipesDirectory] = Document("recipes", ""),
        });
        return new EquipmentClosureGenerationValidationService(
            GameSessionTestFactory.GetProcessSnapshot(),
            gate,
            new HashSet<StringName>()
        ).Validate(Sources, reader);
    }

    private void AssertRejected(
        EquipmentClosureGenerationValidationReport report,
        EquipmentClosureGenerationValidationStageKind stage,
        int exitCode,
        string ruleId,
        string label
    )
    {
        _test.False(report.Success, $"{label} should be rejected");
        _test.Eq(report.RejectedStage, stage, $"{label} rejected stage");
        _test.Eq(report.ExitCode, exitCode, $"{label} exit code");
        _test.True(report.Stages[^1].Diagnostics.Count > 0, $"{label} diagnostic");
        _test.Eq(
            report.Stages[^1].Diagnostics[0].RuleId,
            ruleId,
            $"{label} rule ID"
        );
    }

    private static string Document(string domain, string entry) =>
        $"{{\"schema\":1,\"domain\":\"{domain}\",\"family\":\"generated_test\",\"templates\":{{}},\"entries\":[{entry}]}}";

    private static string ValidItemEntry() =>
        "{"
        + "\"item_id\":\"generated_test_badge\","
        + "\"display_name\":\"Generated Test Badge\","
        + "\"description\":\"Generated equipment closure fixture.\","
        + "\"icon_asset_id\":\"\","
        + "\"is_stackable\":false,"
        + "\"base_price\":100,"
        + "\"buy_price\":100,"
        + "\"sell_price\":50,"
        + "\"sellable\":true,"
        + "\"max_stack\":1,"
        + "\"item_category\":\"equipment\","
        + "\"tags\":[\"accessory\",\"badge\"],"
        + "\"crafting_groups\":[],"
        + "\"quest_groups\":[],"
        + "\"trait_ids\":[],"
        + "\"trait_roll_groups\":[],"
        + "\"equipment_slot_ids\":[\"badge\"],"
        + "\"attribute_modifiers\":[],"
        + "\"granted_skill_id\":\"\","
        + "\"occupied_slot_ids\":[],"
        + "\"equip_requirement\":null,"
        + "\"equipment_type_id\":\"accessory\","
        + "\"weapon_profile\":null,"
        + "\"max_dex_bonus\":-1"
        + "}";

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyDictionary<string, string> _documents;

        internal FakeSourceReader(IReadOnlyDictionary<string, string> documents)
        {
            _documents = documents;
        }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(
            string directoryPath
        ) => _documents.TryGetValue(directoryPath, out string? document)
            ? new[] { new ContentJsonSourceText(FileName(directoryPath), document) }
            : Array.Empty<ContentJsonSourceText>();

        private static string FileName(string directory) =>
            directory[(directory.LastIndexOf('/') + 1)..] + ".json";
    }

    private sealed class FakeGate : IEquipmentClosureGenerationBattleSimulationGate
    {
        private readonly bool _reject;

        internal FakeGate(bool reject)
        {
            _reject = reject;
        }

        internal bool Called { get; private set; }

        public EquipmentClosureGenerationBattleSimulationGateResult Evaluate(
            EquipmentClosureGenerationProjectedContent content,
            ContentSnapshot processSnapshot
        )
        {
            Called = true;
            JsonContentEntryContext context =
                content.ItemContexts[new StringName("generated_test_badge")];
            return new EquipmentClosureGenerationBattleSimulationGateResult(
                1,
                _reject
                    ? new[]
                    {
                        new ContentJsonDiagnostic(
                            EquipmentClosureGenerationBattleSimRules.HighStrengthOutlier,
                            "fixture outlier",
                            context.SourceLabel,
                            context.JsonPointer,
                            "inside envelope",
                            "outside envelope"
                        ),
                    }
                    : Array.Empty<ContentJsonDiagnostic>(),
                new Dictionary<string, object>
                {
                    ["sampled_item_count"] = 1,
                }
            );
        }
    }
}
