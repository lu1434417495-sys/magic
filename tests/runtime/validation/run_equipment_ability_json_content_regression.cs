#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Godot;

public partial class run_equipment_ability_json_content_regression : LifecycleTestSceneTree
{
    private const string ProductionJsonPath =
        "res://data/configs/json/equipment_abilities/equipment_abilities.json";
    private const string TrackedSchemaPath =
        "res://data/schemas/content/equipment_abilities.schema.json";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            string productionJson = FileAccess.GetFileAsString(ProductionJsonPath);
            ContentImportBatch<EquipmentAbilityContentPackImportModel> production =
                EquipmentAbilityContentJsonAuthoringDomain.CreateImportDescriptor(
                    EquipmentAbilityContentJsonAuthoringDomain.ProductionDirectory,
                    new GodotContentJsonSourceReader()
                ).Import();

            AssertProductionJsonInventory(production);
            AssertCanonicalRoundTrip(production);
            AssertPayloadKindAndSchemaCoverage();
            AssertIndependentSchemaAndDomainDiagnostics(productionJson);
            AssertProductionSnapshotUsesJson();
            AssertDefinitionsDropResourceProvenance();
            AssertWindupContextSurvivesStatusExpansion();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected equipment ability JSON content exception: {exception}");
        }

        RequestTestExit(_test.Finish("Equipment ability JSON content regression"));
    }

    private void AssertProductionJsonInventory(
        ContentImportBatch<EquipmentAbilityContentPackImportModel> production
    )
    {
        _test.False(
            production.HasErrors,
            $"production equipment ability JSON imports cleanly: {Format(production.Diagnostics)}"
        );
        _test.Eq(production.Entries.Count, 55, "production JSON contains all 55 HEAD packs");
        _test.Eq(
            production.Entries.Sum(entry => entry.Import.bindings.Count),
            170,
            "production JSON contains all 170 HEAD bindings"
        );

    }

    private void AssertCanonicalRoundTrip(
        ContentImportBatch<EquipmentAbilityContentPackImportModel> production
    )
    {
        EquipmentAbilityContentPackImportModel[] imports = production.Entries
            .Select(entry => entry.Import)
            .ToArray();
        string canonical = EquipmentAbilityImportCanonicalJson.WriteDocument("all", imports);
        ContentImportBatch<EquipmentAbilityContentPackImportModel> roundTrip = Import(
            canonical,
            schemaOnly: false
        );
        _test.False(
            roundTrip.HasErrors,
            $"canonical JSON round-trip is clean: {Format(roundTrip.Diagnostics)}"
        );
        _test.Eq(roundTrip.Entries.Count, 55, "canonical round-trip retains all packs");
        _test.Eq(
            roundTrip.Entries.Sum(entry => entry.Import.bindings.Count),
            170,
            "canonical round-trip retains all bindings"
        );
        _test.Eq(
            EquipmentAbilityImportCanonicalJson.WriteDocument(
                "all",
                roundTrip.Entries.Select(entry => entry.Import).ToArray()
            ),
            canonical,
            "canonical round-trip is stable"
        );
    }

    private void AssertPayloadKindAndSchemaCoverage()
    {
        _test.Eq(EquipmentAbilityPayloadKindCatalog.Conditions.Count, 3, "three condition kinds are closed");
        _test.Eq(EquipmentAbilityPayloadKindCatalog.Actions.Count, 26, "26 executable action kinds remain after ghost removal");
        _test.Eq(
            EquipmentAbilityPayloadKindCatalog.Conditions.Count
                + EquipmentAbilityPayloadKindCatalog.Actions.Count,
            29,
            "schema covers 29 valid payload kinds after removing grant_skill"
        );

        using var registry = new EquipmentAbilityContentRegistry();
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> conditionHandlers =
            registry.GetConditionHandlerSpecsTyped();
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> actionHandlers =
            registry.GetActionHandlerSpecsTyped();
        _test.Eq(conditionHandlers.Count, 3, "every condition kind has a runtime handler");
        _test.Eq(actionHandlers.Count, 26, "every action kind has a runtime handler");
        _test.False(actionHandlers.ContainsKey("grant_skill"), "grant_skill is absent from runtime handlers");

        foreach ((string kind, EquipmentAbilityPayloadKindSpec spec) in EquipmentAbilityPayloadKindCatalog.Conditions)
        {
            _test.True(conditionHandlers.ContainsKey(kind), $"condition handler covers {kind}");
            _test.Eq(conditionHandlers[kind].PayloadJsonDtoType, spec.JsonDtoType, $"condition DTO matches {kind}");
            _test.Eq(conditionHandlers[kind].PayloadImportModelType, spec.ImportModelType, $"condition import matches {kind}");
        }
        foreach ((string kind, EquipmentAbilityPayloadKindSpec spec) in EquipmentAbilityPayloadKindCatalog.Actions)
        {
            _test.True(actionHandlers.ContainsKey(kind), $"action handler covers {kind}");
            _test.Eq(actionHandlers[kind].PayloadJsonDtoType, spec.JsonDtoType, $"action DTO matches {kind}");
            _test.Eq(actionHandlers[kind].PayloadImportModelType, spec.ImportModelType, $"action import matches {kind}");
        }

        string schema = new ContentJsonSchemaExporter().Export(
            EquipmentAbilityContentJsonAuthoringDomain.SchemaRegistration
        );
        _test.Eq(
            NormalizeNewlines(FileAccess.GetFileAsString(TrackedSchemaPath)),
            NormalizeNewlines(schema),
            "tracked equipment schema is byte-exact with registration export"
        );
        foreach (string kind in EquipmentAbilityPayloadKindCatalog.Conditions.Keys.Concat(
            EquipmentAbilityPayloadKindCatalog.Actions.Keys
        ))
        {
            _test.True(schema.Contains($"\"const\": \"{kind}\"", StringComparison.Ordinal), $"schema oneOf covers {kind}");
        }
        _test.False(schema.Contains("grant_skill", StringComparison.Ordinal), "schema drops grant_skill");
        _test.False(schema.Contains("on_battle_end", StringComparison.Ordinal), "schema drops on_battle_end");
        _test.False(schema.Contains("after_battle", StringComparison.Ordinal), "schema drops after_battle");
        _test.False(
            Enum.GetNames<EquipmentAbilityTriggerKind>().Contains("OnBattleEnd", StringComparer.Ordinal),
            "runtime trigger enum drops OnBattleEnd"
        );
    }

    private void AssertIndependentSchemaAndDomainDiagnostics(string productionJson)
    {
        JsonObject extraMemberDocument = Parse(productionJson);
        FirstEntry(extraMemberDocument)["unexpected_field"] = true;
        ContentImportBatch<EquipmentAbilityContentPackImportModel> extraMember = Import(
            extraMemberDocument.ToJsonString(),
            schemaOnly: true
        );
        AssertDiagnostic(extraMember, EquipmentAbilityJsonImportRules.InvalidDto, "");

        JsonObject unknownKindDocument = Parse(productionJson);
        JsonObject kindCarrier = FindObject(unknownKindDocument, value =>
            value.ContainsKey("kind") && value.ContainsKey("payload"));
        kindCarrier["kind"] = "future_handler";
        ContentImportBatch<EquipmentAbilityContentPackImportModel> unknownKind = Import(
            unknownKindDocument.ToJsonString(),
            schemaOnly: true
        );
        AssertDiagnostic(unknownKind, EquipmentAbilityJsonImportRules.UnknownKind, "/kind");
        AssertDiagnostic(unknownKind, EquipmentAbilityJsonImportRules.InvalidPayload, "/payload");

        JsonObject invalidPayloadDocument = Parse(productionJson);
        JsonObject invalidPayloadCarrier = FindObject(invalidPayloadDocument, value =>
            value.ContainsKey("kind") && value.ContainsKey("payload"));
        invalidPayloadCarrier["payload"] = new JsonObject();
        ContentImportBatch<EquipmentAbilityContentPackImportModel> invalidPayload = Import(
            invalidPayloadDocument.ToJsonString(),
            schemaOnly: true
        );
        AssertDiagnostic(invalidPayload, EquipmentAbilityJsonImportRules.InvalidPayload, "/payload");

        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("trigger") && value.ContainsKey("timing"),
            value => value["trigger"] = "on_future_event",
            EquipmentAbilityImportVocabularyValidator.UnknownTrigger,
            "/trigger"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("trigger") && value.ContainsKey("timing"),
            value => value["timing"] = "after_future_event",
            EquipmentAbilityImportVocabularyValidator.UnknownTiming,
            "/timing"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("mode") && value.ContainsKey("conditions"),
            value => value["mode"] = "future_group_mode",
            EquipmentAbilityImportVocabularyValidator.UnknownGroupMode,
            "/mode"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("query_kind"),
            value => value["query_kind"] = "future_query_kind",
            EquipmentAbilityImportVocabularyValidator.UnknownFactQueryKind,
            "/query_kind"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("query_kind") && value["query_kind"]?.GetValue<string>() == "fact",
            value => value["fact_id"] = "future_fact",
            EquipmentAbilityImportVocabularyValidator.UnknownFactId,
            "/fact_id"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("query_kind"),
            value => value["subject"] = "future_subject",
            EquipmentAbilityImportVocabularyValidator.UnknownFactSubject,
            "/subject"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("query_kind"),
            value => value["aggregation"] = "future_aggregation",
            EquipmentAbilityImportVocabularyValidator.UnknownFactAggregation,
            "/aggregation"
        );
        AssertDomainVocabularyDiagnostic(
            productionJson,
            value => value.ContainsKey("query_kind"),
            value => value["value_kind"] = "future_value_kind",
            EquipmentAbilityImportVocabularyValidator.UnknownFactValueKind,
            "/value_kind"
        );
    }

    private void AssertProductionSnapshotUsesJson()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator");
        ContentSnapshot snapshot = coordinator.ContentHost.GetSnapshot();
        _test.Eq(snapshot.EquipmentAbilityPacks.Count, 55, "production snapshot publishes 55 JSON packs");
        _test.Eq(snapshot.EquipmentAbilityBindings.Count, 170, "production snapshot publishes 170 JSON bindings");
    }

    private void AssertDefinitionsDropResourceProvenance()
    {
        Type[] definitionTypes =
        {
            typeof(EquipmentAbilityContentPackDefinition),
            typeof(EquipmentAbilityBindingDefinition),
            typeof(EquipmentAbilityReactionDefinition),
            typeof(EquipmentAbilityActionDefinition),
        };
        foreach (Type type in definitionTypes)
        {
            _test.True(
                type.GetProperty("ResourcePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null,
                $"{type.Name} must not retain ResourcePath provenance"
            );
            _test.False(typeof(Resource).IsAssignableFrom(type), $"{type.Name} remains a plain definition");
        }
    }

    private void AssertWindupContextSurvivesStatusExpansion()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        EquipmentAbilityContentPackImportModel pack = new()
        {
            pack_id = "pack.windup_negative",
            schema_version = 1,
            bindings = new[]
            {
                new EquipmentAbilityBindingImportModel
                {
                    binding_id = "binding.windup_negative",
                    trait_id = "trait.windup_negative",
                    reactions = new[]
                    {
                        new EquipmentAbilityReactionImportModel
                        {
                            reaction_id = "reaction.windup_negative",
                            trigger = "on_hit",
                            timing = "after_hit",
                            actions = new[]
                            {
                                new EquipmentAbilityActionImportModel
                                {
                                    action_id = "action.windup_negative",
                                    kind = "trigger_skill",
                                    payload = new TriggerSkillActionPayloadImportModel
                                    {
                                        skill_id = "skill.windup_negative",
                                        skill_level = 1,
                                        target_selector = "target",
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
        EquipmentAbilityRegistryBuildResult result = registry.Rebuild(
            new[] { pack },
            new EquipmentAbilityContentValidationContext
            {
                KnownTraitIds = new HashSet<StringName> { "trait.windup_negative" },
                KnownSkillIds = new HashSet<StringName> { "skill.windup_negative" },
                WindupSkillIds = new HashSet<StringName> { "skill.windup_negative" },
                KnownStatusIds = new HashSet<StringName>(),
            }
        );
        _test.False(result.Success, "automatic trigger_skill rejects windup skills after status expansion");
        _test.True(
            result.Errors.Any(error => error.Contains("EQA_REFERENCE_WINDUP_SKILL_UNSUPPORTED", StringComparison.Ordinal)),
            "windup rejection exposes the stable validation code"
        );
    }

    private static ContentImportBatch<EquipmentAbilityContentPackImportModel> Import(
        string json,
        bool schemaOnly
    )
    {
        var reader = new FakeSourceReader(new ContentJsonSourceText("probe.json", json));
        return schemaOnly
            ? EquipmentAbilityContentJsonAuthoringDomain.CreateSchemaImportDescriptor("memory://equipment", reader).Import()
            : EquipmentAbilityContentJsonAuthoringDomain.CreateImportDescriptor("memory://equipment", reader).Import();
    }

    private void AssertDiagnostic(
        ContentImportBatch<EquipmentAbilityContentPackImportModel> batch,
        string ruleId,
        string pointerSuffix
    )
    {
        ContentJsonDiagnostic? diagnostic = batch.Diagnostics.FirstOrDefault(value =>
            value.RuleId == ruleId
            && value.JsonPointer.EndsWith(pointerSuffix, StringComparison.Ordinal)
        );
        _test.True(
            diagnostic != null,
            $"diagnostic golden contains {ruleId} at *{pointerSuffix}: {Format(batch.Diagnostics)}"
        );
        if (diagnostic != null)
        {
            _test.True(!string.IsNullOrWhiteSpace(diagnostic.SourceLabel), $"{ruleId} has a source label");
            _test.True(diagnostic.JsonPointer.StartsWith("/entries/", StringComparison.Ordinal), $"{ruleId} has a machine-locatable JSON pointer");
        }
    }

    private void AssertDomainVocabularyDiagnostic(
        string productionJson,
        Func<JsonObject, bool> predicate,
        Action<JsonObject> mutate,
        string ruleId,
        string pointerSuffix
    )
    {
        JsonObject document = Parse(productionJson);
        mutate(FindObject(document, predicate));
        string mutatedJson = document.ToJsonString();
        ContentImportBatch<EquipmentAbilityContentPackImportModel> schema = Import(
            mutatedJson,
            schemaOnly: true
        );
        _test.False(
            schema.HasErrors,
            $"schema tier accepts structurally valid {ruleId} probe for domain evaluation: {Format(schema.Diagnostics)}"
        );
        ContentImportBatch<EquipmentAbilityContentPackImportModel> domain = Import(
            mutatedJson,
            schemaOnly: false
        );
        AssertDiagnostic(domain, ruleId, pointerSuffix);
    }

    private static JsonObject Parse(string json) =>
        JsonNode.Parse(json)?.AsObject()
        ?? throw new InvalidOperationException("Equipment ability JSON document did not parse.");

    private static JsonObject FirstEntry(JsonObject document) =>
        document["entries"]?.AsArray()[0]?.AsObject()
        ?? throw new InvalidOperationException("Equipment ability JSON has no entries.");

    private static JsonObject FindObject(JsonNode node, Func<JsonObject, bool> predicate)
    {
        if (node is JsonObject candidate)
        {
            if (predicate(candidate))
                return candidate;
            foreach ((_, JsonNode? child) in candidate)
            {
                if (child == null)
                    continue;
                try { return FindObject(child, predicate); }
                catch (InvalidOperationException) { }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child == null)
                    continue;
                try { return FindObject(child, predicate); }
                catch (InvalidOperationException) { }
            }
        }
        throw new InvalidOperationException("Required JSON object was not found.");
    }

    private static string NormalizeNewlines(string value) =>
        (value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Format(IReadOnlyList<ContentJsonDiagnostic> diagnostics) =>
        string.Join(" | ", diagnostics.Select(value =>
            $"{value.RuleId} {value.SourceLabel}{value.JsonPointer}: {value.Message}"));

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;
        internal FakeSourceReader(params ContentJsonSourceText[] sources) => _sources = sources;
        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) => _sources;
    }
}
