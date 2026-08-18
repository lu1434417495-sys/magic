using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

public partial class run_skill_validator_diagnostic_golden_regression : LifecycleTestSceneTree
{
    private const string FixtureRoot =
        "res://tests/fixtures/skill_validator_diagnostic_golden/cases";
    private const string ExactGoldenPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/exact_diagnostic_golden.tsv";
    private const string RuleInventoryPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/rule_inventory.tsv";
    private const string RuleExceptionsPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/rule_exceptions.tsv";
    private const string SourceAddSiteInventoryPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/source_add_site_inventory.tsv";
    private const string SourceEmitterCallEdgeInventoryPath =
        "res://tests/fixtures/skill_validator_diagnostic_golden/source_emitter_call_edge_inventory.tsv";
    private const string MissingDirectoryFixtureId = "@missing_directory";
    private const string NullLoaderFixtureId = "@null_loader";
    private const string MissingDirectoryExceptionRuleId =
        "T1A7.REG.SCAN_DIRECTORY.MISSING_DIRECTORY";
    private const string NullLoaderExceptionRuleId =
        "T1A7.REG.REGISTER_SKILL_RESOURCE.LOAD_FAILURE";

    private const int ExpectedDirectAddSiteCount = 512;
    private const int ExpectedReachableContentSourceSiteCount = 506;
    private const int ExpectedSemanticRuleCount = 741;
    private const int ExpectedExactOccurrenceCount = 873;
    private const int ExpectedUniqueDiagnosticCount = 869;
    private const int ExpectedRealFixtureCount = 87;
    private const int ExpectedSyntheticFixtureCount = 2;
    private const int ExpectedEmitterMethodCount = 65;
    private const int ExpectedExceptionCount = 10;
    private const int ExpectedRuleWitnessOccurrenceCount = 741;
    private const int ExpectedCollateralOccurrenceCount = 130;
    private const int ExpectedSyntheticNonContentOccurrenceCount = 2;
    private const int ExpectedAllAddSiteCount = 532;
    private const int ExpectedReviewedNonDiagnosticAddSiteCount = 20;
    private const int ExpectedEmitterCallEdgeCount = 143;
    private const string ExpectedCollateralAssociationSha256 =
        "FADA5C86EF907EF0CDD77B9510FD4CAB336DCB37E66941E8A71D28DDB3ABA6E0";

    private static readonly HashSet<string> AllowedExceptionStatuses = new(
        StringComparer.Ordinal
    )
    {
        "NON_CONTENT_ENVIRONMENTAL",
        "DEAD_RULE_REMOVED",
        "NON_VALIDATOR_FORWARDER",
        "INDIRECT_VALIDATOR_FORWARDER",
        "SKILL_CALLSITE_RESOURCE_NORMALIZED_UNREACHABLE",
    };

    private static readonly Dictionary<string, string> ExpectedSourcePathByOwner = new(
        StringComparer.Ordinal
    )
    {
        ["SkillContentRegistry"] =
            "res://scripts/player/progression/SkillContentRegistry.cs",
        ["SkillCombatProfileValidator"] =
            "res://scripts/player/progression/SkillCombatProfileValidator.cs",
        ["SkillDamageEffectValidator"] =
            "res://scripts/player/progression/SkillDamageEffectValidator.cs",
        ["SkillExecuteEffectValidator"] =
            "res://scripts/player/progression/SkillExecuteEffectValidator.cs",
        ["SkillLevelDescriptionContentRules"] =
            "res://scripts/player/progression/SkillLevelDescriptionContentRules.cs",
        ["SaveTagListContentRules"] =
            "res://scripts/player/progression/SaveTagListContentRules.cs",
        ["CombatLineThroughAttackContentRules"] =
            "res://scripts/player/progression/CombatLineThroughAttackContentRules.cs",
        ["CombatSequentialLineHitContentRules"] =
            "res://scripts/player/progression/CombatSequentialLineHitContentRules.cs",
    };

    private const string EffectCategoryRulesPath =
        "res://scripts/player/progression/CombatEffectCategoryContentRules.cs";

    private static readonly HashSet<string> ExpectedEmitterOwnerMethods = new(
        StringComparer.Ordinal
    )
    {
        "CombatLineThroughAttackContentRules::AppendValidationErrors",
        "CombatLineThroughAttackContentRules::ValidateCurve",
        "CombatSequentialLineHitContentRules::AppendValidationErrors",
        "CombatSequentialLineHitContentRules::ValidateCurve",
        "SaveTagListContentRules::AppendValidationErrors",
        "SkillCombatProfileValidator::AppendAirbornePullProfileValidationErrors",
        "SkillCombatProfileValidator::AppendApproachAttackValidationErrors",
        "SkillCombatProfileValidator::AppendAttributeScaledDiceValidationErrors",
        "SkillCombatProfileValidator::AppendCastingTimeCompatibilityErrors",
        "SkillCombatProfileValidator::AppendCastingTimeEffectCompatibilityErrors",
        "SkillCombatProfileValidator::AppendChainDamageLevelWindowValidationErrors",
        "SkillCombatProfileValidator::AppendChainDamageValidationErrors",
        "SkillCombatProfileValidator::AppendCombatProfileValidationErrors",
        "SkillCombatProfileValidator::AppendDirectionalPiercingValidationErrors",
        "SkillCombatProfileValidator::AppendEffectValidationErrors",
        "SkillCombatProfileValidator::AppendPhantasmalKillLevelDescriptionValidationErrors",
        "SkillCombatProfileValidator::AppendProjectileCategoryOwnershipErrors",
        "SkillCombatProfileValidator::AppendRangedWeaponReactionValidationErrors",
        "SkillCombatProfileValidator::AppendSaveValidationErrors",
        "SkillCombatProfileValidator::AppendSourceRetreatProfileValidationErrors",
        "SkillCombatProfileValidator::AppendSpellFateValidationErrors",
        "SkillCombatProfileValidator::AppendSpellReactionValidationErrors",
        "SkillCombatProfileValidator::AppendStringNameArrayValidationErrors",
        "SkillCombatProfileValidator::AppendTypedEffectParamValidationErrors",
        "SkillCombatProfileValidator::AppendUniqueStringNameArrayValidationErrors",
        "SkillCombatProfileValidator::AppendWeightedSaveFailureOutcomeValidationErrors",
        "SkillContentRegistry::AppendAttributeGrowthValidationErrors",
        "SkillContentRegistry::AppendDynamicMaxLevelValidationErrors",
        "SkillContentRegistry::AppendPracticeSkillValidationErrors",
        "SkillContentRegistry::AppendRawIntRequirementEntryErrors",
        "SkillContentRegistry::AppendSkillValidationErrors",
        "SkillContentRegistry::AppendSpellReactionReferenceValidationErrors",
        "SkillContentRegistry::RegisterSkillResource",
        "SkillContentRegistry::RequireBool",
        "SkillContentRegistry::RequireInt",
        "SkillContentRegistry::RequireIntRangeParam",
        "SkillContentRegistry::RequireNonNegativeIntParam",
        "SkillContentRegistry::RequirePositiveIntParam",
        "SkillContentRegistry::RequirePositiveTuParam",
        "SkillContentRegistry::RequireRange",
        "SkillContentRegistry::RequireStringName",
        "SkillContentRegistry::RequireStringNameParam",
        "SkillContentRegistry::TryReadLevelOverrideInt",
        "SkillContentRegistry::TryReadStrictIntParam",
        "SkillDamageEffectValidator::AppendDamageEffectMitigationBypassValidationErrors",
        "SkillDamageEffectValidator::AppendDamageEffectValidationErrors",
        "SkillDamageEffectValidator::AppendDamageSegmentMitigationBypassValidationErrors",
        "SkillDamageEffectValidator::AppendEquipmentDurabilityDamageValidationErrors",
        "SkillDamageEffectValidator::AppendExtraDamageSegmentValidationErrors",
        "SkillDamageEffectValidator::AppendJumpEffectValidationErrors",
        "SkillDamageEffectValidator::AppendPathStepAoeValidationErrors",
        "SkillDamageEffectValidator::AppendStatusDamageFilterValidationErrors",
        "SkillDamageEffectValidator::AppendStringNameListValidationErrors",
        "SkillDamageEffectValidator::AppendTargetDamageMultiplierRuleValidationErrors",
        "SkillDamageEffectValidator::_append_equipment_slot_array_validation_errors",
        "SkillDamageEffectValidator::_append_equipment_slot_weight_validation_errors",
        "SkillExecuteEffectValidator::AppendExecuteCombatProfileValidationErrors",
        "SkillExecuteEffectValidator::AppendExecuteEffectValidationErrors",
        "SkillExecuteEffectValidator::AppendGradedSaveExecuteParamKeyValidationErrors",
        "SkillExecuteEffectValidator::AppendSaveBonusByTagValidationErrors",
        "SkillExecuteEffectValidator::AppendTemporalReleaseSkillValidationErrors",
        "SkillExecuteEffectValidator::AppendTemporalStatusEffectValidationErrors",
        "SkillExecuteEffectValidator::ValidateExecuteEffectSet",
        "SkillLevelDescriptionContentRules::AppendExpressionValidationErrors",
        "SkillLevelDescriptionContentRules::CollectValidationErrors",
    };

    private static readonly Dictionary<string, string> RequiredExceptionSignatures = new(
        StringComparer.Ordinal
    )
    {
        ["T1A7.REG.SCAN_DIRECTORY.COULD_NOT_OPEN"] =
            "SkillContentRegistry\tScanDirectory\tres://scripts/player/progression/SkillContentRegistry.cs:150\tDIRECT_BRANCH\tSkillContentRegistry\tScanDirectory\tres://scripts/player/progression/SkillContentRegistry.cs:150\tdirectory_open_failure\tNON_CONTENT_ENVIRONMENTAL",
        ["T1A7.REG.SCAN_DIRECTORY.MISSING_DIRECTORY"] =
            "SkillContentRegistry\tScanDirectory\tres://scripts/player/progression/SkillContentRegistry.cs:143\tDIRECT_BRANCH\tSkillContentRegistry\tScanDirectory\tres://scripts/player/progression/SkillContentRegistry.cs:143\tmissing_directory\tNON_CONTENT_ENVIRONMENTAL",
        ["T1A7.REG.REGISTER_SKILL_RESOURCE.LOAD_FAILURE"] =
            "SkillContentRegistry\tRegisterSkillResource\tres://scripts/player/progression/SkillContentRegistry.cs:188\tDIRECT_BRANCH\tSkillContentRegistry\tRegisterSkillResource\tres://scripts/player/progression/SkillContentRegistry.cs:188\tresource_load_failure\tNON_CONTENT_ENVIRONMENTAL",
        ["T1A7.CPV.SPELL_CRITICAL_MP_REFUND_RANGE"] =
            "SkillCombatProfileValidator\tAppendSpellFateValidationErrors\t-\tDIRECT_BRANCH\tSkillCombatProfileValidator\tAppendSpellFateValidationErrors\t-\tremoved_branch\tDEAD_RULE_REMOVED",
        ["T1A7.CPV.FUMBLE_PROTECTION_EXTRA_MP_NON_NEGATIVE"] =
            "SkillCombatProfileValidator\tAppendSpellFateValidationErrors\t-\tDIRECT_BRANCH\tSkillCombatProfileValidator\tAppendSpellFateValidationErrors\t-\tremoved_branch\tDEAD_RULE_REMOVED",
        ["T1A7.REG.VALIDATION_ERRORS_SETTER_FORWARD"] =
            "SkillContentRegistry\t_validation_errors.set\tres://scripts/player/progression/SkillContentRegistry.cs:25\tDIRECT_BRANCH\tSkillContentRegistry\t_validation_errors.set\tres://scripts/player/progression/SkillContentRegistry.cs:25\tcaller_error_forward\tNON_VALIDATOR_FORWARDER",
        ["T1A7.REG.LEVEL_DESCRIPTION_FORWARD"] =
            "SkillContentRegistry\tAppendSkillValidationErrors\tres://scripts/player/progression/SkillContentRegistry.cs:288\tDIRECT_BRANCH\tSkillContentRegistry\tAppendSkillValidationErrors\tres://scripts/player/progression/SkillContentRegistry.cs:288\tlevel_description_forward\tINDIRECT_VALIDATOR_FORWARDER",
        ["T1A7.SAVE_TAG_LIST.SAVE_ADVANTAGE_TAGS_NON_NULL_LIST"] =
            "SaveTagListContentRules\tAppendValidationErrors\tres://scripts/player/progression/SaveTagListContentRules.cs:24\tCALLSITE_BRANCH\tSkillCombatProfileValidator\tAppendEffectValidationErrors\tres://scripts/player/progression/SkillCombatProfileValidator.cs:1862\tfield=Skill {skillId} effect {contextLabel}.save_advantage_tags;branch=null\tSKILL_CALLSITE_RESOURCE_NORMALIZED_UNREACHABLE",
        ["T1A7.SAVE_TAG_LIST.SAVE_DISADVANTAGE_TAGS_NON_NULL_LIST"] =
            "SaveTagListContentRules\tAppendValidationErrors\tres://scripts/player/progression/SaveTagListContentRules.cs:24\tCALLSITE_BRANCH\tSkillCombatProfileValidator\tAppendEffectValidationErrors\tres://scripts/player/progression/SkillCombatProfileValidator.cs:1867\tfield=Skill {skillId} effect {contextLabel}.save_disadvantage_tags;branch=null\tSKILL_CALLSITE_RESOURCE_NORMALIZED_UNREACHABLE",
        ["T1A7.SAVE_TAG_LIST.SAVE_IMMUNITY_TAGS_NON_NULL_LIST"] =
            "SaveTagListContentRules\tAppendValidationErrors\tres://scripts/player/progression/SaveTagListContentRules.cs:24\tCALLSITE_BRANCH\tSkillCombatProfileValidator\tAppendEffectValidationErrors\tres://scripts/player/progression/SkillCombatProfileValidator.cs:1872\tfield=Skill {skillId} effect {contextLabel}.save_immunity_tags;branch=null\tSKILL_CALLSITE_RESOURCE_NORMALIZED_UNREACHABLE",
    };

    private static readonly Dictionary<string, string> RequiredExceptionEvidenceSha256 = new(
        StringComparer.Ordinal
    )
    {
        ["T1A7.REG.SCAN_DIRECTORY.COULD_NOT_OPEN"] =
            "79B53E2A87BC9BED938AB10D51EA0078121C247EE2BFEF39059F69A607832958",
        ["T1A7.REG.SCAN_DIRECTORY.MISSING_DIRECTORY"] =
            "6A8BACC7EDDCA6F4D17B47548DFB8460A1DDEB9D8E901B457FDD3B2AD28C2A17",
        ["T1A7.REG.REGISTER_SKILL_RESOURCE.LOAD_FAILURE"] =
            "156F529B80C413F92F5665F6D49804592409FBBA5A3F5D51805EA5440B44B4C4",
        ["T1A7.CPV.SPELL_CRITICAL_MP_REFUND_RANGE"] =
            "583B873E25E1CEFD0A93709BAB2C56EBC22EBEEA1CCE3A8B36A9DC1678DADAB4",
        ["T1A7.CPV.FUMBLE_PROTECTION_EXTRA_MP_NON_NEGATIVE"] =
            "5A0FE6D6EE602600A23A23ACF69D10084FB509EAC6E0C91DAD82871F5BB1E2EA",
        ["T1A7.REG.VALIDATION_ERRORS_SETTER_FORWARD"] =
            "E4E085193D43F3E8D2F769BFAF75ABACAEB32ABB78FCB2311E609CEF16BD35C0",
        ["T1A7.REG.LEVEL_DESCRIPTION_FORWARD"] =
            "079194EA89B79187960974CAE78E7926581AF60329B86C21493053D659F2E59B",
        ["T1A7.SAVE_TAG_LIST.SAVE_ADVANTAGE_TAGS_NON_NULL_LIST"] =
            "EA3FF612522CEDA2D53062AC611E66C8C18ECC380A3B3DA5664E94E45568B544",
        ["T1A7.SAVE_TAG_LIST.SAVE_DISADVANTAGE_TAGS_NON_NULL_LIST"] =
            "887B91F954EA23608495F3D0E3AFC5855D0C3E78B2D982A3C3ACF6A294D66C31",
        ["T1A7.SAVE_TAG_LIST.SAVE_IMMUNITY_TAGS_NON_NULL_LIST"] =
            "7A9F8FCBAE9227DB0B8041D2709DD92162810911AA13601DA410D06CF06A344D",
    };

    private sealed class NullContentResourceLoader : IContentResourceLoader
    {
        public T LoadCanonical<T>(string resourcePath)
            where T : Resource => null;
    }

    private sealed record ExactDiagnosticRow(
        string OccurrenceKey,
        string FixtureId,
        string ExpectedDiagnostic,
        string Classification,
        string Binding,
        string Reason
    );

    private sealed record SemanticRuleRow(
        string SemanticRuleId,
        string OriginOwner,
        string OriginMethod,
        string OriginSite,
        string InstanceKind,
        string InstanceOwner,
        string InstanceMethod,
        string InstanceSite,
        string Selector,
        string Trigger,
        string WitnessFixtureId,
        string ExpectedOccurrenceKey
    );

    private sealed record RuleExceptionRow(
        string RuleId,
        string OriginOwner,
        string OriginMethod,
        string OriginSite,
        string InstanceKind,
        string InstanceOwner,
        string InstanceMethod,
        string InstanceSite,
        string Selector,
        string Status,
        string Evidence
    );

    private sealed record SourceSiteLocation(string SourcePath, int Line);

    private sealed record SourceMemberSite(
        string Owner,
        string Member,
        string SourceSite
    );

    private sealed record SourceAddSiteRow(
        string Owner,
        string Member,
        string SourceSite,
        string Receiver,
        string Classification,
        string Reason
    );

    private sealed record SourceCallEdgeRow(
        string CallerOwner,
        string CallerMember,
        string CallSite,
        string CalleeOwner,
        string CalleeMember,
        string Classification,
        string Reason
    );

    private enum CSharpTokenKind
    {
        Identifier,
        StringLiteral,
        Symbol,
    }

    private sealed record CSharpToken(CSharpTokenKind Kind, string Text, int Line);

    private static readonly HashSet<string> AllowedInstanceKinds = new(
        StringComparer.Ordinal
    )
    {
        "DIRECT_BRANCH",
        "QUALIFIED_CALLSITE",
        "LOOP_MEMBER",
        "MAPPING_ENTRY",
        "CALLSITE_BRANCH",
    };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            AssertSourceGateSelfTests();
            if (OS.GetEnvironment("T1A7_SOURCE_DUMP") == "1")
            {
                DumpSourceStructureForAuthoring();
                return;
            }
            AssertOfficialCorpusHasNoDiagnostics();
            if (OS.GetEnvironment("T1A7_DUMP") == "1")
            {
                DumpCorpusForExactAuthoring();
                return;
            }

            IReadOnlyList<ExactDiagnosticRow> exactRows = ReadExactDiagnosticGolden();
            IReadOnlyDictionary<string, ExactDiagnosticRow> exactByOccurrence =
                BuildExactOccurrenceIndex(exactRows);
            IReadOnlyList<SemanticRuleRow> semanticRules = ReadRuleInventory(
                exactByOccurrence
            );
            IReadOnlyList<RuleExceptionRow> exceptions = ReadRuleExceptions(
                semanticRules
            );
            AssertArtifactClosure(exactRows, semanticRules, exceptions);
            AssertNegativeCorpusMatchesExact(exactRows);
        }
        catch (Exception exception)
        {
            _test.Fail($"Skill validator diagnostic golden crashed: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Skill validator diagnostic golden regression"));
        }
    }

    private void AssertOfficialCorpusHasNoDiagnostics()
    {
        using TestContentResourceLoader loader = new();
        using SkillContentRegistry registry = new(loader);
        List<string> diagnostics = CopyDiagnostics(registry.Validate());
        _test.Eq(
            diagnostics.Count,
            0,
            $"全量正式 skill 内容的 diagnostic golden 必须为空: {FormatDiagnostics(diagnostics)}"
        );
    }

    private static IReadOnlyList<ExactDiagnosticRow> ReadExactDiagnosticGolden()
    {
        using FileAccess file = OpenRequiredArtifact(ExactGoldenPath);
        var rows = new List<ExactDiagnosticRow>();
        var seenOccurrenceKeys = new HashSet<string>(StringComparer.Ordinal);
        var closedFixtureIds = new HashSet<string>(StringComparer.Ordinal);
        string currentFixtureId = null;
        string previousDiagnostic = null;
        int fixtureOrdinal = 0;
        int lineNumber = 0;
        while (!file.EofReached())
        {
            lineNumber++;
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            RequireColumnCount(ExactGoldenPath, lineNumber, columns, 6);
            string diagnostic = DecodeDiagnostic(
                ExactGoldenPath,
                lineNumber,
                columns[2]
            );
            var row = new ExactDiagnosticRow(
                columns[0],
                columns[1],
                diagnostic,
                columns[3],
                columns[4],
                columns[5]
            );
            RequireNonEmpty(ExactGoldenPath, lineNumber, "occurrence_key", row.OccurrenceKey);
            RequireNonEmpty(ExactGoldenPath, lineNumber, "fixture_id", row.FixtureId);
            RequireNonEmpty(
                ExactGoldenPath,
                lineNumber,
                "diagnostic",
                row.ExpectedDiagnostic
            );
            RequireNonEmpty(
                ExactGoldenPath,
                lineNumber,
                "classification",
                row.Classification
            );
            RequireNonEmpty(ExactGoldenPath, lineNumber, "binding", row.Binding);
            RequireNonEmpty(ExactGoldenPath, lineNumber, "reason", row.Reason);
            ValidateExactClassification(row, lineNumber);
            if (!seenOccurrenceKeys.Add(row.OccurrenceKey))
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{lineNumber} duplicates occurrence_key {row.OccurrenceKey}."
                );

            if (!string.Equals(currentFixtureId, row.FixtureId, StringComparison.Ordinal))
            {
                if (currentFixtureId != null)
                    closedFixtureIds.Add(currentFixtureId);
                if (closedFixtureIds.Contains(row.FixtureId))
                    throw new InvalidOperationException(
                        $"{ExactGoldenPath}:{lineNumber} reopens non-contiguous fixture {row.FixtureId}."
                    );
                currentFixtureId = row.FixtureId;
                previousDiagnostic = null;
                fixtureOrdinal = 0;
            }

            fixtureOrdinal++;
            string expectedOccurrenceKey = $"{row.FixtureId}.{fixtureOrdinal:D4}";
            if (!string.Equals(
                row.OccurrenceKey,
                expectedOccurrenceKey,
                StringComparison.Ordinal
            ))
            {
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{lineNumber} occurrence_key must be {expectedOccurrenceKey}; occurrence ordinals are exact-multiset metadata only."
                );
            }
            if (
                previousDiagnostic != null
                && string.CompareOrdinal(previousDiagnostic, row.ExpectedDiagnostic) > 0
            )
            {
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{lineNumber} diagnostics for fixture {row.FixtureId} must be ordinal-sorted while retaining duplicates."
                );
            }
            previousDiagnostic = row.ExpectedDiagnostic;
            rows.Add(row);
        }
        if (rows.Count == 0)
            throw new InvalidOperationException($"{ExactGoldenPath} must not be empty.");
        return rows;
    }

    private static void ValidateExactClassification(
        ExactDiagnosticRow row,
        int artifactLineNumber
    )
    {
        if (row.Classification == "RULE_WITNESS")
        {
            if (!row.Binding.StartsWith("T1A7.", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{artifactLineNumber} RULE_WITNESS must bind a T1A7 semantic_rule_id."
                );
            if (row.Reason != "SEMANTIC_RULE_WITNESS")
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{artifactLineNumber} RULE_WITNESS must use reason SEMANTIC_RULE_WITNESS."
                );
            if (IsSyntheticFixture(row.FixtureId))
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{artifactLineNumber} synthetic fixture cannot be a RULE_WITNESS."
                );
            return;
        }
        if (row.Classification == "COLLATERAL")
        {
            if (
                !row.Binding.StartsWith("T1A7.", StringComparison.Ordinal)
                || row.Reason != "ADDITIONAL_RULE_OCCURRENCE"
            )
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{artifactLineNumber} COLLATERAL must bind an existing T1A7 semantic rule and use reason ADDITIONAL_RULE_OCCURRENCE."
                );
            if (IsSyntheticFixture(row.FixtureId))
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{artifactLineNumber} synthetic fixture cannot be COLLATERAL."
                );
            return;
        }
        if (row.Classification == "SYNTHETIC_NON_CONTENT")
        {
            string expectedBinding = row.FixtureId switch
            {
                MissingDirectoryFixtureId => MissingDirectoryExceptionRuleId,
                NullLoaderFixtureId => NullLoaderExceptionRuleId,
                _ => null,
            };
            if (
                expectedBinding == null
                || row.Binding != expectedBinding
                || row.Reason != "REVIEWED_SYNTHETIC_EXCEPTION"
            )
                throw new InvalidOperationException(
                    $"{ExactGoldenPath}:{artifactLineNumber} SYNTHETIC_NON_CONTENT fixture/reason binding is not reviewed."
                );
            return;
        }
        throw new InvalidOperationException(
            $"{ExactGoldenPath}:{artifactLineNumber} uses unknown classification {row.Classification}."
        );
    }

    private static IReadOnlyDictionary<string, ExactDiagnosticRow> BuildExactOccurrenceIndex(
        IReadOnlyList<ExactDiagnosticRow> rows
    )
    {
        var result = new Dictionary<string, ExactDiagnosticRow>(StringComparer.Ordinal);
        foreach (ExactDiagnosticRow row in rows)
            result.Add(row.OccurrenceKey, row);
        return result;
    }

    private static IReadOnlyList<SemanticRuleRow> ReadRuleInventory(
        IReadOnlyDictionary<string, ExactDiagnosticRow> exactByOccurrence
    )
    {
        using FileAccess file = OpenRequiredArtifact(RuleInventoryPath);
        var rows = new List<SemanticRuleRow>();
        var seenRuleIds = new HashSet<string>(StringComparer.Ordinal);
        var seenExpectedOccurrences = new HashSet<string>(StringComparer.Ordinal);
        SourceSiteLocation previousLocation = null;
        string previousTrigger = null;
        int lineNumber = 0;
        while (!file.EofReached())
        {
            lineNumber++;
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            RequireColumnCount(RuleInventoryPath, lineNumber, columns, 12);
            var row = new SemanticRuleRow(
                columns[0],
                columns[1],
                columns[2],
                columns[3],
                columns[4],
                columns[5],
                columns[6],
                columns[7],
                columns[8],
                columns[9],
                columns[10],
                columns[11]
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "semantic_rule_id",
                row.SemanticRuleId
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "origin_owner",
                row.OriginOwner
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "origin_method",
                row.OriginMethod
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "instance_kind",
                row.InstanceKind
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "instance_owner",
                row.InstanceOwner
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "instance_method",
                row.InstanceMethod
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "selector",
                row.Selector
            );
            RequireNonEmpty(RuleInventoryPath, lineNumber, "trigger", row.Trigger);
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "witness_fixture",
                row.WitnessFixtureId
            );
            RequireNonEmpty(
                RuleInventoryPath,
                lineNumber,
                "expected_occurrence_key",
                row.ExpectedOccurrenceKey
            );
            if (!row.SemanticRuleId.StartsWith("T1A7.", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} semantic_rule_id must start with T1A7."
                );
            if (
                Regex.IsMatch(row.SemanticRuleId, @"\.[0-9A-Fa-f]{8}(?:_\d+)?$")
                || Regex.IsMatch(row.SemanticRuleId, @"\.\d{4}$")
            )
            {
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} semantic_rule_id {row.SemanticRuleId} looks like a rendered-text fingerprint or occurrence ordinal."
                );
            }
            if (!seenRuleIds.Add(row.SemanticRuleId))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} duplicates semantic_rule_id {row.SemanticRuleId}."
                );
            if (IsSyntheticFixture(row.WitnessFixtureId))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} semantic rule {row.SemanticRuleId} must use a real .tres fixture, not {row.WitnessFixtureId}."
                );
            if (!seenExpectedOccurrences.Add(row.ExpectedOccurrenceKey))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} reuses exact witness {row.ExpectedOccurrenceKey}; each semantic rule needs an explicit exact occurrence."
                );
            if (!exactByOccurrence.TryGetValue(
                row.ExpectedOccurrenceKey,
                out ExactDiagnosticRow exactRow
            ))
            {
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} references missing exact occurrence {row.ExpectedOccurrenceKey}."
                );
            }
            if (!string.Equals(
                exactRow.FixtureId,
                row.WitnessFixtureId,
                StringComparison.Ordinal
            ))
            {
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} witness fixture {row.WitnessFixtureId} does not own exact occurrence {row.ExpectedOccurrenceKey}."
                );
            }

            if (!AllowedInstanceKinds.Contains(row.InstanceKind))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} uses unknown instance_kind {row.InstanceKind}."
                );

            SourceSiteLocation location = ParseSourceSite(
                RuleInventoryPath,
                lineNumber,
                row.OriginSite
            );
            if (!ExpectedSourcePathByOwner.TryGetValue(row.OriginOwner, out string expectedPath))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} has unknown origin owner {row.OriginOwner}."
                );
            if (!string.Equals(location.SourcePath, expectedPath, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} origin owner {row.OriginOwner} must use source path {expectedPath}, got {location.SourcePath}."
                );
            string ownerMethod = $"{row.OriginOwner}::{row.OriginMethod}";
            if (!ExpectedEmitterOwnerMethods.Contains(ownerMethod))
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} has unreviewed emitter owner/method {ownerMethod}."
                );

            SourceSiteLocation instanceLocation = ParseSourceSite(
                RuleInventoryPath,
                lineNumber,
                row.InstanceSite
            );
            string expectedInstancePath = row.InstanceOwner == "CombatEffectCategoryContentRules"
                ? EffectCategoryRulesPath
                : ExpectedSourcePathByOwner.TryGetValue(
                    row.InstanceOwner,
                    out string knownInstancePath
                )
                    ? knownInstancePath
                    : throw new InvalidOperationException(
                        $"{RuleInventoryPath}:{lineNumber} has unknown instance owner {row.InstanceOwner}."
                    );
            if (!string.Equals(
                instanceLocation.SourcePath,
                expectedInstancePath,
                StringComparison.Ordinal
            ))
            {
                throw new InvalidOperationException(
                    $"{RuleInventoryPath}:{lineNumber} instance owner {row.InstanceOwner} must use source path {expectedInstancePath}, got {instanceLocation.SourcePath}."
                );
            }
            ValidateDirectBranchIdentity(
                RuleInventoryPath,
                lineNumber,
                row.InstanceKind,
                row.OriginOwner,
                row.OriginMethod,
                row.OriginSite,
                row.InstanceOwner,
                row.InstanceMethod,
                row.InstanceSite
            );
            if (previousLocation != null)
            {
                int pathOrder = string.CompareOrdinal(
                    previousLocation.SourcePath,
                    location.SourcePath
                );
                bool outOfOrder = pathOrder > 0
                    || (
                        pathOrder == 0
                        && (
                            previousLocation.Line > location.Line
                            || (
                                previousLocation.Line == location.Line
                                && string.CompareOrdinal(previousTrigger, row.Trigger) > 0
                            )
                        )
                    );
                if (outOfOrder)
                    throw new InvalidOperationException(
                        $"{RuleInventoryPath}:{lineNumber} must be sorted by source path, numeric source line, then trigger."
                    );
            }
            previousLocation = location;
            previousTrigger = row.Trigger;
            rows.Add(row);
        }
        if (rows.Count == 0)
            throw new InvalidOperationException($"{RuleInventoryPath} must not be empty.");
        return rows;
    }

    private static IReadOnlyList<RuleExceptionRow> ReadRuleExceptions(
        IReadOnlyList<SemanticRuleRow> semanticRules
    )
    {
        using FileAccess file = OpenRequiredArtifact(RuleExceptionsPath);
        var semanticRuleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (SemanticRuleRow rule in semanticRules)
            semanticRuleIds.Add(rule.SemanticRuleId);

        var rows = new List<RuleExceptionRow>();
        var seenRuleIds = new HashSet<string>(StringComparer.Ordinal);
        int lineNumber = 0;
        while (!file.EofReached())
        {
            lineNumber++;
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            RequireColumnCount(RuleExceptionsPath, lineNumber, columns, 11);
            var row = new RuleExceptionRow(
                columns[0],
                columns[1],
                columns[2],
                columns[3],
                columns[4],
                columns[5],
                columns[6],
                columns[7],
                columns[8],
                columns[9],
                columns[10]
            );
            RequireNonEmpty(RuleExceptionsPath, lineNumber, "rule_id", row.RuleId);
            RequireNonEmpty(
                RuleExceptionsPath,
                lineNumber,
                "origin_owner",
                row.OriginOwner
            );
            RequireNonEmpty(
                RuleExceptionsPath,
                lineNumber,
                "origin_method",
                row.OriginMethod
            );
            RequireNonEmpty(
                RuleExceptionsPath,
                lineNumber,
                "instance_kind",
                row.InstanceKind
            );
            RequireNonEmpty(
                RuleExceptionsPath,
                lineNumber,
                "instance_owner",
                row.InstanceOwner
            );
            RequireNonEmpty(
                RuleExceptionsPath,
                lineNumber,
                "instance_method",
                row.InstanceMethod
            );
            RequireNonEmpty(RuleExceptionsPath, lineNumber, "selector", row.Selector);
            RequireNonEmpty(RuleExceptionsPath, lineNumber, "status", row.Status);
            RequireNonEmpty(RuleExceptionsPath, lineNumber, "evidence", row.Evidence);
            if (!AllowedInstanceKinds.Contains(row.InstanceKind))
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{lineNumber} uses unknown instance_kind {row.InstanceKind}."
                );
            if (!seenRuleIds.Add(row.RuleId))
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{lineNumber} duplicates rule_id {row.RuleId}."
                );
            if (semanticRuleIds.Contains(row.RuleId))
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{lineNumber} rule_id {row.RuleId} also appears in the semantic inventory."
                );
            if (!AllowedExceptionStatuses.Contains(row.Status))
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{lineNumber} uses forbidden or unreviewed status {row.Status}."
                );
            ValidateExceptionSourceSite(
                lineNumber,
                row.Status,
                "origin",
                row.OriginOwner,
                row.OriginSite
            );
            ValidateExceptionSourceSite(
                lineNumber,
                row.Status,
                "instance",
                row.InstanceOwner,
                row.InstanceSite
            );
            ValidateReviewedExceptionSignature(row, lineNumber);
            ValidateDirectBranchIdentity(
                RuleExceptionsPath,
                lineNumber,
                row.InstanceKind,
                row.OriginOwner,
                row.OriginMethod,
                row.OriginSite,
                row.InstanceOwner,
                row.InstanceMethod,
                row.InstanceSite
            );
            if (
                row.Status == "DEAD_RULE_REMOVED"
                && !row.Evidence.Contains("d3964a02", StringComparison.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{lineNumber} removed dead rule {row.RuleId} must cite commit d3964a02."
                );
            }
            if (
                row.Status == "SKILL_CALLSITE_RESOURCE_NORMALIZED_UNREACHABLE"
                && (
                    !row.Evidence.Contains("CombatEffectDef.cs:", StringComparison.Ordinal)
                    || !row.Evidence.Contains("TestContentResourceLoader", StringComparison.Ordinal)
                    || !row.Evidence.Contains("SkillContentRegistry", StringComparison.Ordinal)
                )
            )
            {
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{lineNumber} normalized Skill callsite exception must retain Resource-path evidence."
                );
            }
            rows.Add(row);
        }
        foreach (string requiredRuleId in RequiredExceptionSignatures.Keys)
        {
            if (!seenRuleIds.Contains(requiredRuleId))
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath} is missing reviewed exception {requiredRuleId}."
                );
        }
        return rows;
    }

    private static string ExceptionSignature(RuleExceptionRow row) =>
        $"{row.OriginOwner}\t{row.OriginMethod}\t{row.OriginSite}\t{row.InstanceKind}\t{row.InstanceOwner}\t{row.InstanceMethod}\t{row.InstanceSite}\t{row.Selector}\t{row.Status}";

    private static void ValidateReviewedExceptionSignature(
        RuleExceptionRow row,
        int artifactLineNumber
    )
    {
        if (!RequiredExceptionSignatures.TryGetValue(
            row.RuleId,
            out string expectedSignature
        ))
        {
            throw new InvalidOperationException(
                $"{RuleExceptionsPath}:{artifactLineNumber} adds unreviewed exception {row.RuleId}."
            );
        }
        string actualSignature = ExceptionSignature(row);
        if (!string.Equals(actualSignature, expectedSignature, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{RuleExceptionsPath}:{artifactLineNumber} exception signature mismatch for {row.RuleId}."
            );
        if (
            !RequiredExceptionEvidenceSha256.TryGetValue(
                row.RuleId,
                out string expectedEvidenceSha256
            )
        )
        {
            throw new InvalidOperationException(
                $"{RuleExceptionsPath}:{artifactLineNumber} lacks reviewed evidence hash for {row.RuleId}."
            );
        }
        string actualEvidenceSha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(row.Evidence)
            )
        );
        if (actualEvidenceSha256 != expectedEvidenceSha256)
            throw new InvalidOperationException(
                $"{RuleExceptionsPath}:{artifactLineNumber} evidence hash mismatch for {row.RuleId}."
            );
    }

    private static void ValidateDirectBranchIdentity(
        string artifactPath,
        int artifactLineNumber,
        string kind,
        string originOwner,
        string originMethod,
        string originSite,
        string instanceOwner,
        string instanceMethod,
        string instanceSite
    )
    {
        if (kind != "DIRECT_BRANCH")
            return;
        if (
            originOwner != instanceOwner
            || originMethod != instanceMethod
            || originSite != instanceSite
        )
        {
            throw new InvalidOperationException(
                $"{artifactPath}:{artifactLineNumber} DIRECT_BRANCH instance must exactly equal its origin owner/method/site."
            );
        }
    }

    private static void ValidateExceptionSourceSite(
        int artifactLineNumber,
        string status,
        string role,
        string owner,
        string sourceSite
    )
    {
        if (status == "DEAD_RULE_REMOVED")
        {
            if (sourceSite != "-")
                throw new InvalidOperationException(
                    $"{RuleExceptionsPath}:{artifactLineNumber} removed {role} site must be '-'."
                );
            return;
        }

        SourceSiteLocation location = ParseSourceSite(
            RuleExceptionsPath,
            artifactLineNumber,
            sourceSite
        );
        if (!ExpectedSourcePathByOwner.TryGetValue(owner, out string expectedPath))
            throw new InvalidOperationException(
                $"{RuleExceptionsPath}:{artifactLineNumber} has unknown {role} owner {owner}."
            );
        if (!string.Equals(location.SourcePath, expectedPath, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{RuleExceptionsPath}:{artifactLineNumber} {role} owner {owner} must use source path {expectedPath}, got {location.SourcePath}."
            );
    }

    private static void AssertArtifactClosure(
        IReadOnlyList<ExactDiagnosticRow> exactRows,
        IReadOnlyList<SemanticRuleRow> semanticRules,
        IReadOnlyList<RuleExceptionRow> exceptions
    )
    {
        RequireCount(
            "exact diagnostic occurrences",
            exactRows.Count,
            ExpectedExactOccurrenceCount
        );
        RequireCount("rule exceptions", exceptions.Count, ExpectedExceptionCount);
        AssertExactSemanticBindings(exactRows, semanticRules);

        var uniqueDiagnostics = new HashSet<string>(StringComparer.Ordinal);
        var exactFixtureIds = new HashSet<string>(StringComparer.Ordinal);
        var realExactFixtureIds = new HashSet<string>(StringComparer.Ordinal);
        var syntheticExactFixtureIds = new HashSet<string>(StringComparer.Ordinal);
        var actualFixtureOrder = new List<string>();
        string previousFixtureId = null;
        foreach (ExactDiagnosticRow row in exactRows)
        {
            uniqueDiagnostics.Add(row.ExpectedDiagnostic);
            exactFixtureIds.Add(row.FixtureId);
            if (IsSyntheticFixture(row.FixtureId))
                syntheticExactFixtureIds.Add(row.FixtureId);
            else
                realExactFixtureIds.Add(row.FixtureId);
            if (!string.Equals(previousFixtureId, row.FixtureId, StringComparison.Ordinal))
            {
                actualFixtureOrder.Add(row.FixtureId);
                previousFixtureId = row.FixtureId;
            }
        }
        RequireCount(
            "unique exact diagnostics",
            uniqueDiagnostics.Count,
            ExpectedUniqueDiagnosticCount
        );
        RequireCount(
            "synthetic exact fixtures",
            syntheticExactFixtureIds.Count,
            ExpectedSyntheticFixtureCount
        );
        if (
            !syntheticExactFixtureIds.SetEquals(
                new[] { MissingDirectoryFixtureId, NullLoaderFixtureId }
            )
        )
        {
            throw new InvalidOperationException(
                $"{ExactGoldenPath} synthetic fixtures must be exactly {MissingDirectoryFixtureId} and {NullLoaderFixtureId}."
            );
        }

        string[] fixtureIds = DirAccess.GetDirectoriesAt(FixtureRoot);
        Array.Sort(fixtureIds, StringComparer.Ordinal);
        RequireCount("real fixture directories", fixtureIds.Length, ExpectedRealFixtureCount);
        var actualDirectorySet = new HashSet<string>(fixtureIds, StringComparer.Ordinal);
        if (!actualDirectorySet.SetEquals(realExactFixtureIds))
            throw new InvalidOperationException(
                $"Fixture directory universe and {ExactGoldenPath} real fixture IDs must be equal."
            );
        RequireCount(
            "exact fixture IDs",
            exactFixtureIds.Count,
            ExpectedRealFixtureCount + ExpectedSyntheticFixtureCount
        );

        var expectedFixtureOrder = new List<string>(fixtureIds)
        {
            MissingDirectoryFixtureId,
            NullLoaderFixtureId,
        };
        if (actualFixtureOrder.Count != expectedFixtureOrder.Count)
            throw new InvalidOperationException(
                $"{ExactGoldenPath} fixture ordering count mismatch."
            );
        for (int index = 0; index < expectedFixtureOrder.Count; index++)
        {
            if (!string.Equals(
                actualFixtureOrder[index],
                expectedFixtureOrder[index],
                StringComparison.Ordinal
            ))
            {
                throw new InvalidOperationException(
                    $"{ExactGoldenPath} fixture order mismatch at index {index}: expected {expectedFixtureOrder[index]}, got {actualFixtureOrder[index]}."
                );
            }
        }

        var contentSourceSites = new HashSet<string>(StringComparer.Ordinal);
        var emitterOwnerMethods = new HashSet<string>(StringComparer.Ordinal);
        foreach (SemanticRuleRow rule in semanticRules)
        {
            contentSourceSites.Add(rule.OriginSite);
            emitterOwnerMethods.Add($"{rule.OriginOwner}::{rule.OriginMethod}");
            if (!actualDirectorySet.Contains(rule.WitnessFixtureId))
                throw new InvalidOperationException(
                    $"Semantic rule {rule.SemanticRuleId} references missing fixture directory {rule.WitnessFixtureId}."
                );
            string fixturePath = $"{FixtureRoot}/{rule.WitnessFixtureId}";
            string[] files = DirAccess.GetFilesAt(fixturePath);
            bool hasTres = Array.Exists(
                files,
                fileName => fileName.EndsWith(".tres", StringComparison.Ordinal)
            );
            if (!hasTres)
                throw new InvalidOperationException(
                    $"Semantic rule {rule.SemanticRuleId} witness {rule.WitnessFixtureId} must contain a real .tres resource."
                );
        }
        RequireCount(
            "reachable content source sites",
            contentSourceSites.Count,
            ExpectedReachableContentSourceSiteCount
        );
        RequireCount(
            "emitter owner/method pairs",
            emitterOwnerMethods.Count,
            ExpectedEmitterMethodCount
        );
        if (!emitterOwnerMethods.SetEquals(ExpectedEmitterOwnerMethods))
            throw new InvalidOperationException(
                $"{RuleInventoryPath} emitter owner/method set does not match the reviewed snapshot."
            );

        var exceptionStatusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (RuleExceptionRow row in exceptions)
        {
            exceptionStatusCounts.TryGetValue(row.Status, out int count);
            exceptionStatusCounts[row.Status] = count + 1;
        }
        RequireStatusCount(exceptionStatusCounts, "NON_CONTENT_ENVIRONMENTAL", 3);
        RequireStatusCount(exceptionStatusCounts, "DEAD_RULE_REMOVED", 2);
        RequireStatusCount(exceptionStatusCounts, "NON_VALIDATOR_FORWARDER", 1);
        RequireStatusCount(exceptionStatusCounts, "INDIRECT_VALIDATOR_FORWARDER", 1);
        RequireStatusCount(
            exceptionStatusCounts,
            "SKILL_CALLSITE_RESOURCE_NORMALIZED_UNREACHABLE",
            3
        );

        var expectedDirectAddSites = new HashSet<string>(StringComparer.Ordinal);
        foreach (SemanticRuleRow row in semanticRules)
            expectedDirectAddSites.Add(
                SourceMemberSiteKey(row.OriginOwner, row.OriginMethod, row.OriginSite)
            );
        foreach (RuleExceptionRow row in exceptions)
        {
            if (row.Status != "DEAD_RULE_REMOVED")
                expectedDirectAddSites.Add(
                    SourceMemberSiteKey(row.OriginOwner, row.OriginMethod, row.OriginSite)
                );
        }
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex =
            ReadDirectDiagnosticAddSiteIndex();
        var actualDirectAddSites = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceMemberSite site in directSinkIndex.Values)
            actualDirectAddSites.Add(
                SourceMemberSiteKey(site.Owner, site.Member, site.SourceSite)
            );
        RequireCount(
            "current direct Add sites",
            actualDirectAddSites.Count,
            ExpectedDirectAddSiteCount
        );
        RequireExactSet(
            "direct diagnostic Add owner/member/source sites",
            expectedDirectAddSites,
            actualDirectAddSites
        );
        AssertSourceAddSiteInventory(directSinkIndex);
        AssertEmitterCallEdgeInventory();
        AssertFiniteInstanceExpansion(semanticRules, exceptions, directSinkIndex);
        RequireCount("semantic rules", semanticRules.Count, ExpectedSemanticRuleCount);
    }

    private static void AssertExactSemanticBindings(
        IReadOnlyList<ExactDiagnosticRow> exactRows,
        IReadOnlyList<SemanticRuleRow> semanticRules,
        bool enforceSnapshotCounts = true,
        string expectedCollateralAssociationSha256 = null
    )
    {
        var ruleById = new Dictionary<string, SemanticRuleRow>(StringComparer.Ordinal);
        foreach (SemanticRuleRow rule in semanticRules)
            ruleById.Add(rule.SemanticRuleId, rule);

        var witnessedRuleIds = new HashSet<string>(StringComparer.Ordinal);
        int ruleWitnessCount = 0;
        int collateralCount = 0;
        int syntheticCount = 0;
        foreach (ExactDiagnosticRow exactRow in exactRows)
        {
            if (exactRow.Classification == "RULE_WITNESS")
            {
                ruleWitnessCount++;
                if (!ruleById.TryGetValue(exactRow.Binding, out SemanticRuleRow rule))
                    throw new InvalidOperationException(
                        $"{ExactGoldenPath} occurrence {exactRow.OccurrenceKey} binds missing semantic rule {exactRow.Binding}."
                    );
                if (!witnessedRuleIds.Add(exactRow.Binding))
                    throw new InvalidOperationException(
                        $"{ExactGoldenPath} semantic rule {exactRow.Binding} has multiple RULE_WITNESS rows."
                    );
                if (
                    rule.ExpectedOccurrenceKey != exactRow.OccurrenceKey
                    || rule.WitnessFixtureId != exactRow.FixtureId
                )
                {
                    throw new InvalidOperationException(
                        $"{ExactGoldenPath} occurrence {exactRow.OccurrenceKey} and {RuleInventoryPath} rule {exactRow.Binding} are not a bidirectional fixture/occurrence binding."
                    );
                }
            }
            else if (exactRow.Classification == "COLLATERAL")
            {
                collateralCount++;
                if (!ruleById.ContainsKey(exactRow.Binding))
                    throw new InvalidOperationException(
                        $"{ExactGoldenPath} collateral occurrence {exactRow.OccurrenceKey} binds missing semantic rule {exactRow.Binding}."
                    );
            }
            else if (exactRow.Classification == "SYNTHETIC_NON_CONTENT")
            {
                syntheticCount++;
                if (!RequiredExceptionSignatures.ContainsKey(exactRow.Binding))
                    throw new InvalidOperationException(
                        $"{ExactGoldenPath} synthetic occurrence {exactRow.OccurrenceKey} binds missing reviewed exception {exactRow.Binding}."
                    );
            }
        }
        if (enforceSnapshotCounts)
        {
            RequireCount(
                "RULE_WITNESS exact occurrences",
                ruleWitnessCount,
                ExpectedRuleWitnessOccurrenceCount
            );
            RequireCount(
                "COLLATERAL exact occurrences",
                collateralCount,
                ExpectedCollateralOccurrenceCount
            );
            RequireCount(
                "SYNTHETIC_NON_CONTENT exact occurrences",
                syntheticCount,
                ExpectedSyntheticNonContentOccurrenceCount
            );
        }
        string requiredCollateralAssociationSha256 =
            expectedCollateralAssociationSha256
            ?? (enforceSnapshotCounts ? ExpectedCollateralAssociationSha256 : null);
        if (requiredCollateralAssociationSha256 != null)
            AssertCollateralAssociationSignature(
                exactRows,
                requiredCollateralAssociationSha256
            );
        if (!witnessedRuleIds.SetEquals(ruleById.Keys))
            throw new InvalidOperationException(
                $"{ExactGoldenPath} RULE_WITNESS semantic IDs and {RuleInventoryPath} semantic IDs must be an exact set."
            );
    }

    private static void AssertCollateralAssociationSignature(
        IReadOnlyList<ExactDiagnosticRow> exactRows,
        string expectedSha256
    )
    {
        string actualSha256 = BuildCollateralAssociationSha256(exactRows);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{ExactGoldenPath} COLLATERAL occurrence-to-semantic associations do not match the reviewed closed signature."
            );
    }

    private static string BuildCollateralAssociationSha256(
        IReadOnlyList<ExactDiagnosticRow> exactRows
    )
    {
        var associations = new List<string>();
        foreach (ExactDiagnosticRow row in exactRows)
        {
            if (row.Classification == "COLLATERAL")
                associations.Add($"{row.OccurrenceKey}\t{row.Binding}");
        }
        associations.Sort(StringComparer.Ordinal);
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join("\n", associations))
            )
        );
    }

    private void AssertNegativeCorpusMatchesExact(
        IReadOnlyList<ExactDiagnosticRow> exactRows
    )
    {
        var expectedByFixture = new SortedDictionary<string, List<string>>(
            StringComparer.Ordinal
        );
        foreach (ExactDiagnosticRow row in exactRows)
        {
            if (!expectedByFixture.TryGetValue(row.FixtureId, out List<string> diagnostics))
            {
                diagnostics = new List<string>();
                expectedByFixture.Add(row.FixtureId, diagnostics);
            }
            diagnostics.Add(row.ExpectedDiagnostic);
        }

        foreach ((string fixtureId, List<string> expected) in expectedByFixture)
        {
            List<string> actual = ValidateFixture(fixtureId);
            expected.Sort(StringComparer.Ordinal);
            actual.Sort(StringComparer.Ordinal);
            _test.Eq(
                actual.Count,
                expected.Count,
                $"fixture {fixtureId} diagnostic 数量必须与 exact golden 一致。 actual={FormatDiagnostics(actual)} expected={FormatDiagnostics(expected)}"
            );
            int count = Math.Min(actual.Count, expected.Count);
            for (int index = 0; index < count; index++)
            {
                _test.Eq(
                    actual[index],
                    expected[index],
                    $"fixture {fixtureId} diagnostic exact golden mismatch at sorted index {index}."
                );
            }
        }
    }

    private void DumpCorpusForExactAuthoring()
    {
        string[] fixtureIds = DirAccess.GetDirectoriesAt(FixtureRoot);
        Array.Sort(fixtureIds, StringComparer.Ordinal);
        foreach (string fixtureId in fixtureIds)
            DumpFixtureDiagnostics(fixtureId);
        DumpFixtureDiagnostics(MissingDirectoryFixtureId);
        DumpFixtureDiagnostics(NullLoaderFixtureId);
        _test.Fail(
            $"T1A7_DUMP=1 emitted current diagnostics for {ExactGoldenPath} authoring."
        );
    }

    private static void AssertSourceGateSelfTests()
    {
        string sample = string.Join(
            "\n",
            new[]
            {
                "internal sealed class Sample",
                "{",
                "    void Real()",
                "    {",
                "        // errors.Add(\"line comment\");",
                "        /* errors.Add(\"block comment\"); */",
                "        var normal = \"errors.Add(\\\"normal string\\\")\";",
                "        var verbatim = @\"errors.Add(\"\"verbatim string\"\")\";",
                "        var interpolated = $\"errors.Add({value})\";",
                "        var dollarAt = $@\"errors.Add({value})\";",
                "        var atDollar = @$\"errors.Add({value})\";",
                "        var raw = \"\"\"errors.Add(\"raw string\")\"\"\";",
                "        var multiDollarRaw = $$\"\"\"errors.Add({{value}})\"\"\";",
                "        errors.Add(\"real\");",
                "    }",
                "}",
            }
        );
        IReadOnlyList<SourceAddSiteRow> sites = ReadAllAddSitesFromTokens(
            "Sample",
            "res://sample.cs",
            LexCSharp(sample)
        );
        if (
            sites.Count != 1
            || sites[0].Receiver != "errors"
            || sites[0].Member != "Real"
        )
        {
            throw new InvalidOperationException(
                "T1a.7 lexer self-test must ignore comments and normal/verbatim/interpolated/raw string fake sinks."
            );
        }

        ExpectRejectedMutation(
            "origin method exchange",
            () =>
            {
                var expected = new HashSet<string>(StringComparer.Ordinal)
                {
                    SourceAddSiteKey(
                        sites[0] with
                        {
                            Member = "WrongMethod",
                        }
                    ),
                };
                var actual = new HashSet<string>(StringComparer.Ordinal)
                {
                    SourceAddSiteKey(sites[0]),
                };
                RequireExactSet("origin method mutation", expected, actual);
            }
        );

        ExpectRejectedMutation(
            "finite instance method exchange",
            () =>
            {
                string actualKey = FiniteInstanceKey(
                    "QUALIFIED_CALLSITE",
                    "SkillContentRegistry",
                    "RequireInt",
                    "res://registry.cs:10",
                    "Sample",
                    "Real",
                    "res://sample.cs:14",
                    "field=value"
                );
                string mutatedKey = FiniteInstanceKey(
                    "QUALIFIED_CALLSITE",
                    "SkillContentRegistry",
                    "RequireInt",
                    "res://registry.cs:10",
                    "Sample",
                    "WrongInstanceMethod",
                    "res://sample.cs:14",
                    "field=value"
                );
                RequireExactSet(
                    "instance method mutation",
                    new HashSet<string>(StringComparer.Ordinal) { mutatedKey },
                    new HashSet<string>(StringComparer.Ordinal) { actualKey }
                );
            }
        );

        ExpectRejectedMutation(
            "reviewed exception redirection",
            () =>
                ValidateReviewedExceptionSignature(
                    new RuleExceptionRow(
                        "T1A7.REG.SCAN_DIRECTORY.MISSING_DIRECTORY",
                        "SkillContentRegistry",
                        "ScanDirectory",
                        "res://scripts/player/progression/SkillContentRegistry.cs:150",
                        "DIRECT_BRANCH",
                        "SkillContentRegistry",
                        "ScanDirectory",
                        "res://scripts/player/progression/SkillContentRegistry.cs:150",
                        "missing_directory",
                        "NON_CONTENT_ENVIRONMENTAL",
                        "mutation"
                    ),
                    1
                )
        );
        ExpectRejectedMutation(
            "reviewed exception evidence one-character edit",
            () =>
                ValidateReviewedExceptionSignature(
                    new RuleExceptionRow(
                        "T1A7.REG.SCAN_DIRECTORY.MISSING_DIRECTORY",
                        "SkillContentRegistry",
                        "ScanDirectory",
                        "res://scripts/player/progression/SkillContentRegistry.cs:143",
                        "DIRECT_BRANCH",
                        "SkillContentRegistry",
                        "ScanDirectory",
                        "res://scripts/player/progression/SkillContentRegistry.cs:143",
                        "missing_directory",
                        "NON_CONTENT_ENVIRONMENTAL",
                        "Directory availability defense, not a .tres content rule. The exact golden keeps deterministic synthetic fixture @missing_directory for res://scripts/player/progression/SkillContentRegistry.cs:143, but it is excluded from the content semantic-rule denominator.!"
                    ),
                    1
                )
        );

        var semanticA = new SemanticRuleRow(
            "T1A7.SELFTEST.A",
            "Sample",
            "Real",
            "res://sample.cs:1",
            "DIRECT_BRANCH",
            "Sample",
            "Real",
            "res://sample.cs:1",
            "branch=a",
            "a",
            "fixture",
            "fixture.0001"
        );
        var semanticB = semanticA with
        {
            SemanticRuleId = "T1A7.SELFTEST.B",
            Selector = "branch=b",
            Trigger = "b",
            ExpectedOccurrenceKey = "fixture.0002",
        };
        ExpectRejectedMutation(
            "exact occurrence semantic binding swap",
            () =>
                AssertExactSemanticBindings(
                    new[]
                    {
                        new ExactDiagnosticRow(
                            "fixture.0001",
                            "fixture",
                            "a",
                            "RULE_WITNESS",
                            semanticB.SemanticRuleId,
                            "SEMANTIC_RULE_WITNESS"
                        ),
                        new ExactDiagnosticRow(
                            "fixture.0002",
                            "fixture",
                            "b",
                            "RULE_WITNESS",
                            semanticA.SemanticRuleId,
                            "SEMANTIC_RULE_WITNESS"
                        ),
                    },
                    new[] { semanticA, semanticB },
                    enforceSnapshotCounts: false
                )
        );

        var validWitness = new ExactDiagnosticRow(
            "fixture.0001",
            "fixture",
            "a",
            "RULE_WITNESS",
            semanticA.SemanticRuleId,
            "SEMANTIC_RULE_WITNESS"
        );
        ExpectRejectedMutation(
            "collateral missing binding",
            () =>
                ValidateExactClassification(
                    new ExactDiagnosticRow(
                        "fixture.0002",
                        "fixture",
                        "a again",
                        "COLLATERAL",
                        "-",
                        "ADDITIONAL_RULE_OCCURRENCE"
                    ),
                    1
                )
        );
        ExpectRejectedMutation(
            "collateral unknown semantic binding",
            () =>
                AssertExactSemanticBindings(
                    new[]
                    {
                        validWitness,
                        new ExactDiagnosticRow(
                            "fixture.0002",
                            "fixture",
                            "a again",
                            "COLLATERAL",
                            "T1A7.SELFTEST.UNKNOWN",
                            "ADDITIONAL_RULE_OCCURRENCE"
                        ),
                    },
                    new[] { semanticA },
                    enforceSnapshotCounts: false
                )
        );
        var validWitnessB = new ExactDiagnosticRow(
            "fixture.0002",
            "fixture",
            "b",
            "RULE_WITNESS",
            semanticB.SemanticRuleId,
            "SEMANTIC_RULE_WITNESS"
        );
        var reviewedCollateral = new ExactDiagnosticRow(
            "fixture.0003",
            "fixture",
            "a again",
            "COLLATERAL",
            semanticA.SemanticRuleId,
            "ADDITIONAL_RULE_OCCURRENCE"
        );
        string reviewedCollateralAssociationSha256 =
            BuildCollateralAssociationSha256(new[] { reviewedCollateral });
        ExpectRejectedMutation(
            "collateral binding swap to another existing semantic ID",
            () =>
                AssertExactSemanticBindings(
                    new[]
                    {
                        validWitness,
                        validWitnessB,
                        reviewedCollateral with
                        {
                            Binding = semanticB.SemanticRuleId,
                        },
                    },
                    new[] { semanticA, semanticB },
                    enforceSnapshotCounts: false,
                    expectedCollateralAssociationSha256: reviewedCollateralAssociationSha256
                )
        );
        ExpectRejectedMutation(
            "synthetic wrong reviewed exception binding",
            () =>
                ValidateExactClassification(
                    new ExactDiagnosticRow(
                        "@missing_directory.0001",
                        MissingDirectoryFixtureId,
                        "synthetic",
                        "SYNTHETIC_NON_CONTENT",
                        NullLoaderExceptionRuleId,
                        "REVIEWED_SYNTHETIC_EXCEPTION"
                    ),
                    1
                )
        );
        ExpectRejectedMutation(
            "synthetic non-fixed reason",
            () =>
                ValidateExactClassification(
                    new ExactDiagnosticRow(
                        "@missing_directory.0001",
                        MissingDirectoryFixtureId,
                        "synthetic",
                        "SYNTHETIC_NON_CONTENT",
                        MissingDirectoryExceptionRuleId,
                        "MISSING_DIRECTORY_ENVIRONMENTAL"
                    ),
                    1
                )
        );

        string aliasSample = string.Join(
            "\n",
            new[]
            {
                "internal sealed class Sample",
                "{",
                "    void Real() { errors.Add(\"real\"); }",
                "    void Alias() { validationErrors.Add(\"alias\"); }",
                "}",
            }
        );
        IReadOnlyList<SourceAddSiteRow> aliasSites = ReadAllAddSitesFromTokens(
            "Sample",
            "res://sample.cs",
            LexCSharp(aliasSample)
        );
        ExpectRejectedMutation(
            "new Add receiver alias",
            () =>
            {
                var expected = new HashSet<string>(StringComparer.Ordinal);
                var actual = new HashSet<string>(StringComparer.Ordinal);
                foreach (SourceAddSiteRow row in aliasSites)
                {
                    actual.Add(SourceAddSiteKey(row));
                    if (row.Receiver == "errors")
                        expected.Add(SourceAddSiteKey(row));
                }
                RequireExactSet("receiver alias mutation", expected, actual);
            }
        );

        string complexReceiverSample = string.Join(
            "\n",
            new[]
            {
                "internal sealed class Sample",
                "{",
                "    void Complex()",
                "    {",
                "        errors?.Add(\"null conditional\");",
                "        GetErrors().Add(\"call result\");",
                "        obj.List.Add(\"member chain\");",
                "    }",
                "}",
            }
        );
        IReadOnlyList<SourceAddSiteRow> complexReceiverSites =
            ReadAllAddSitesFromTokens(
                "Sample",
                "res://sample.cs",
                LexCSharp(complexReceiverSample)
            );
        if (complexReceiverSites.Count != 3)
            throw new InvalidOperationException(
                "T1a.7 all-Add lexer self-test must recognize null-conditional, call-result, and member-chain receivers."
            );
        ExpectRejectedMutation(
            "complex Add receiver expressions",
            () =>
            {
                var actual = new HashSet<string>(StringComparer.Ordinal);
                foreach (SourceAddSiteRow row in complexReceiverSites)
                    actual.Add(SourceAddSiteKey(row));
                RequireExactSet(
                    "complex receiver mutation",
                    new HashSet<string>(StringComparer.Ordinal),
                    actual
                );
            }
        );

        string wrapperSample = string.Join(
            "\n",
            new[]
            {
                "internal sealed class Sample",
                "{",
                "    void Existing() { SkillContentRegistry.RequireInt(); }",
                "    void NewWrapper() { SkillContentRegistry.RequireInt(); }",
                "}",
            }
        );
        IReadOnlyList<SourceCallEdgeRow> wrapperEdges =
            ReadManagedEmitterCallEdgesFromTokens(
                "SkillCombatProfileValidator",
                "res://sample.cs",
                LexCSharp(wrapperSample)
            );
        if (wrapperEdges.Count != 2)
            throw new InvalidOperationException(
                "T1a.7 call-edge self-test must recognize both existing and new wrapper emitter calls."
            );
        ExpectRejectedMutation(
            "new wrapper call to existing emitter",
            () =>
            {
                var expected = new HashSet<string>(StringComparer.Ordinal)
                {
                    SourceCallEdgeKey(wrapperEdges[0]),
                };
                var actual = new HashSet<string>(StringComparer.Ordinal)
                {
                    SourceCallEdgeKey(wrapperEdges[0]),
                    SourceCallEdgeKey(wrapperEdges[1]),
                };
                RequireExactSet("new wrapper call-edge mutation", expected, actual);
            }
        );

        string methodGroupSample = string.Join(
            "\n",
            new[]
            {
                "internal sealed class Sample",
                "{",
                "    void Capture() { Action f = AppendEffectValidationErrors; }",
                "}",
            }
        );
        IReadOnlyList<SourceCallEdgeRow> methodGroupEdges =
            ReadManagedEmitterCallEdgesFromTokens(
                "SkillCombatProfileValidator",
                "res://sample.cs",
                LexCSharp(methodGroupSample)
            );
        if (
            methodGroupEdges.Count != 1
            || methodGroupEdges[0].Classification != "MANAGED_EMITTER_METHOD_GROUP"
        )
        {
            throw new InvalidOperationException(
                "T1a.7 emitter reference self-test must recognize method-group capture."
            );
        }
        ExpectRejectedMutation(
            "new managed emitter method-group reference",
            () =>
                RequireExactSet(
                    "method-group mutation",
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal)
                    {
                        SourceCallEdgeKey(methodGroupEdges[0]),
                    }
                )
        );

        string setterSample = string.Join(
            "\n",
            new[]
            {
                "internal sealed class Sample",
                "{",
                "    object Errors",
                "    {",
                "        set { _validationErrors.Add(\"forward\"); }",
                "    }",
                "}",
            }
        );
        IReadOnlyList<SourceAddSiteRow> setterSites = ReadAllAddSitesFromTokens(
            "Sample",
            "res://sample.cs",
            LexCSharp(setterSample)
        );
        if (setterSites.Count != 1 || setterSites[0].Member != "Errors.set")
            throw new InvalidOperationException(
                "T1a.7 source member binding self-test must bind property setter sinks."
            );
    }

    private static void ExpectRejectedMutation(string label, Action mutation)
    {
        try
        {
            mutation();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            $"T1a.7 mutation self-test did not reject {label}."
        );
    }

    private void DumpSourceStructureForAuthoring()
    {
        foreach (SourceAddSiteRow row in ReadAllAddSites())
        {
            ConsoleProcessOutput.WriteStandard(
                $"T1A7_SOURCE_ADD\t{row.Owner}\t{row.Member}\t{row.SourceSite}\t{row.Receiver}\t{row.Classification}\t{row.Reason}"
            );
        }
        foreach (SourceCallEdgeRow row in ReadManagedEmitterCallEdges())
        {
            ConsoleProcessOutput.WriteStandard(
                $"T1A7_SOURCE_EDGE\t{row.CallerOwner}\t{row.CallerMember}\t{row.CallSite}\t{row.CalleeOwner}\t{row.CalleeMember}\t{row.Classification}\t{row.Reason}"
            );
        }
        var finiteKeys = new List<string>(
            ReadFiniteInstanceKeys(ReadDirectDiagnosticAddSiteIndex())
        );
        finiteKeys.Sort(StringComparer.Ordinal);
        foreach (string finiteKey in finiteKeys)
            ConsoleProcessOutput.WriteStandard($"T1A7_SOURCE_FINITE\t{finiteKey}");
        _test.Fail("T1A7_SOURCE_DUMP=1 emitted reviewed source-structure artifacts.");
    }

    private void DumpFixtureDiagnostics(string fixtureId)
    {
        List<string> diagnostics = ValidateFixture(fixtureId);
        diagnostics.Sort(StringComparer.Ordinal);
        for (int index = 0; index < diagnostics.Count; index++)
        {
            string encoded = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(diagnostics[index])
            );
            ConsoleProcessOutput.WriteStandard(
                $"T1A7_DIAGNOSTIC\t{fixtureId}\t{index + 1:D4}\t{encoded}"
            );
        }
    }

    private static IReadOnlyDictionary<string, SourceMemberSite> ReadDirectDiagnosticAddSiteIndex()
    {
        var result = new Dictionary<string, SourceMemberSite>(StringComparer.Ordinal);
        foreach (SourceAddSiteRow row in ReadAllAddSites())
        {
            if (row.Receiver is not ("errors" or "_validationErrors"))
                continue;
            var site = new SourceMemberSite(row.Owner, row.Member, row.SourceSite);
            if (!result.TryAdd(row.SourceSite, site))
                throw new InvalidOperationException(
                    $"T1a.7 source lexer found multiple direct diagnostic Add sinks at {row.SourceSite}; source-site identity must be unambiguous."
                );
        }
        return result;
    }

    private static IReadOnlyList<SourceAddSiteRow> ReadAllAddSites()
    {
        var result = new List<SourceAddSiteRow>();
        foreach ((string owner, string sourcePath) in ExpectedSourcePathByOwner)
        {
            IReadOnlyList<CSharpToken> tokens = LexCSharp(ReadRequiredSource(sourcePath));
            result.AddRange(ReadAllAddSitesFromTokens(owner, sourcePath, tokens));
        }
        return result;
    }

    private static IReadOnlyList<SourceAddSiteRow> ReadAllAddSitesFromTokens(
        string owner,
        string sourcePath,
        IReadOnlyList<CSharpToken> tokens
    )
    {
        var result = new List<SourceAddSiteRow>();
        for (int dotIndex = 1; dotIndex + 2 < tokens.Count; dotIndex++)
        {
            if (
                tokens[dotIndex].Text != "."
                || tokens[dotIndex + 1].Text != "Add"
                || tokens[dotIndex + 2].Text != "("
            )
            {
                continue;
            }
            string receiver = DescribeAddReceiver(tokens, dotIndex);
            string member = FindEnclosingMember(tokens, dotIndex + 1);
            string classification = receiver is "errors" or "_validationErrors"
                ? "DIAGNOSTIC_SINK"
                : "REVIEWED_NON_DIAGNOSTIC";
            string reason = classification == "DIAGNOSTIC_SINK"
                ? "MANAGED_VALIDATION_DIAGNOSTIC"
                : "LOCAL_COLLECTION_ADD";
            result.Add(
                new SourceAddSiteRow(
                    owner,
                    member,
                    $"{sourcePath}:{tokens[dotIndex + 1].Line}",
                    receiver,
                    classification,
                    reason
                )
            );
        }
        return result;
    }

    private static string DescribeAddReceiver(
        IReadOnlyList<CSharpToken> tokens,
        int dotIndex
    )
    {
        CSharpToken previous = tokens[dotIndex - 1];
        if (previous.Kind == CSharpTokenKind.Identifier)
            return previous.Text;
        if (previous.Text == "?" && dotIndex >= 2)
        {
            CSharpToken nullConditionalBase = tokens[dotIndex - 2];
            string baseDescriptor = nullConditionalBase.Kind == CSharpTokenKind.Identifier
                ? nullConditionalBase.Text
                : "EXPRESSION";
            return $"NULL_CONDITIONAL:{baseDescriptor}";
        }
        return previous.Text switch
        {
            ")" => "CALL_OR_PAREN_RESULT",
            "]" => "INDEXER_RESULT",
            "}" => "INITIALIZER_RESULT",
            _ => $"EXPRESSION:{previous.Text}",
        };
    }

    private static void AssertSourceAddSiteInventory(
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        IReadOnlyList<SourceAddSiteRow> expectedRows = ReadSourceAddSiteInventory();
        IReadOnlyList<SourceAddSiteRow> actualRows = ReadAllAddSites();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        int expectedDiagnosticCount = 0;
        int expectedReviewedCount = 0;
        foreach (SourceAddSiteRow row in expectedRows)
        {
            expected.Add(SourceAddSiteKey(row));
            if (row.Classification == "DIAGNOSTIC_SINK")
                expectedDiagnosticCount++;
            else
                expectedReviewedCount++;
        }
        foreach (SourceAddSiteRow row in actualRows)
            actual.Add(SourceAddSiteKey(row));
        RequireCount("all source .Add callsites", actual.Count, ExpectedAllAddSiteCount);
        RequireCount(
            "source diagnostic .Add callsites",
            expectedDiagnosticCount,
            ExpectedDirectAddSiteCount
        );
        RequireCount(
            "reviewed non-diagnostic .Add callsites",
            expectedReviewedCount,
            ExpectedReviewedNonDiagnosticAddSiteCount
        );
        RequireExactSet("all source .Add owner/member/site/receiver classifications", expected, actual);
        if (directSinkIndex.Count != expectedDiagnosticCount)
            throw new InvalidOperationException(
                $"{SourceAddSiteInventoryPath} diagnostic classification must equal the direct sink index."
            );
    }

    private static IReadOnlyList<SourceAddSiteRow> ReadSourceAddSiteInventory()
    {
        using FileAccess file = OpenRequiredArtifact(SourceAddSiteInventoryPath);
        var result = new List<SourceAddSiteRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int lineNumber = 0;
        while (!file.EofReached())
        {
            lineNumber++;
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            RequireColumnCount(SourceAddSiteInventoryPath, lineNumber, columns, 6);
            var row = new SourceAddSiteRow(
                columns[0],
                columns[1],
                columns[2],
                columns[3],
                columns[4],
                columns[5]
            );
            RequireNonEmpty(SourceAddSiteInventoryPath, lineNumber, "owner", row.Owner);
            RequireNonEmpty(SourceAddSiteInventoryPath, lineNumber, "member", row.Member);
            RequireNonEmpty(SourceAddSiteInventoryPath, lineNumber, "receiver", row.Receiver);
            RequireNonEmpty(
                SourceAddSiteInventoryPath,
                lineNumber,
                "classification",
                row.Classification
            );
            RequireNonEmpty(SourceAddSiteInventoryPath, lineNumber, "reason", row.Reason);
            SourceSiteLocation location = ParseSourceSite(
                SourceAddSiteInventoryPath,
                lineNumber,
                row.SourceSite
            );
            if (
                !ExpectedSourcePathByOwner.TryGetValue(row.Owner, out string expectedPath)
                || location.SourcePath != expectedPath
            )
            {
                throw new InvalidOperationException(
                    $"{SourceAddSiteInventoryPath}:{lineNumber} owner/path is not reviewed."
                );
            }
            bool diagnostic = row.Receiver is "errors" or "_validationErrors";
            if (
                diagnostic
                    ? row.Classification != "DIAGNOSTIC_SINK"
                        || row.Reason != "MANAGED_VALIDATION_DIAGNOSTIC"
                    : row.Classification != "REVIEWED_NON_DIAGNOSTIC"
                        || row.Reason != "LOCAL_COLLECTION_ADD"
            )
            {
                throw new InvalidOperationException(
                    $"{SourceAddSiteInventoryPath}:{lineNumber} receiver classification is not reviewed."
                );
            }
            if (!seen.Add(SourceAddSiteKey(row)))
                throw new InvalidOperationException(
                    $"{SourceAddSiteInventoryPath}:{lineNumber} duplicates a source Add site row."
                );
            result.Add(row);
        }
        return result;
    }

    private static string SourceAddSiteKey(SourceAddSiteRow row) =>
        $"{row.Owner}\t{row.Member}\t{row.SourceSite}\t{row.Receiver}\t{row.Classification}\t{row.Reason}";

    private static IReadOnlyList<SourceCallEdgeRow> ReadManagedEmitterCallEdges()
    {
        var result = new List<SourceCallEdgeRow>();
        foreach ((string callerOwner, string sourcePath) in ExpectedSourcePathByOwner)
        {
            IReadOnlyList<CSharpToken> tokens = LexCSharp(ReadRequiredSource(sourcePath));
            result.AddRange(
                ReadManagedEmitterCallEdgesFromTokens(
                    callerOwner,
                    sourcePath,
                    tokens
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<SourceCallEdgeRow> ReadManagedEmitterCallEdgesFromTokens(
        string callerOwner,
        string sourcePath,
        IReadOnlyList<CSharpToken> tokens
    )
    {
        var emitterOwnersByMethod = new Dictionary<string, List<string>>(
            StringComparer.Ordinal
        );
        foreach (string signature in ExpectedEmitterOwnerMethods)
        {
            string[] parts = signature.Split("::", StringSplitOptions.None);
            if (!emitterOwnersByMethod.TryGetValue(parts[1], out List<string> owners))
            {
                owners = new List<string>();
                emitterOwnersByMethod.Add(parts[1], owners);
            }
            owners.Add(parts[0]);
        }

        var result = new List<SourceCallEdgeRow>();
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            CSharpToken methodToken = tokens[index];
            if (
                methodToken.Kind != CSharpTokenKind.Identifier
                || !emitterOwnersByMethod.TryGetValue(
                    methodToken.Text,
                    out List<string> possibleOwners
                )
            )
            {
                continue;
            }
            if (!TryFindEnclosingMember(tokens, index, out string callerMember))
                continue;

            string calleeOwner = ResolveCalleeOwner(
                callerOwner,
                tokens,
                index,
                possibleOwners
            );
            if (calleeOwner == null)
                throw new InvalidOperationException(
                    $"T1a.7 managed emitter call {methodToken.Text} at {sourcePath}:{methodToken.Line} has an unresolved receiver owner."
                );
            string calleeSignature = $"{calleeOwner}::{methodToken.Text}";
            if (!ExpectedEmitterOwnerMethods.Contains(calleeSignature))
                continue;
            bool isCall = tokens[index + 1].Text == "(";
            result.Add(
                new SourceCallEdgeRow(
                    callerOwner,
                    callerMember,
                    $"{sourcePath}:{methodToken.Line}",
                    calleeOwner,
                    methodToken.Text,
                    isCall ? "MANAGED_EMITTER_CALL" : "MANAGED_EMITTER_METHOD_GROUP",
                    isCall
                        ? "REVIEWED_MANAGED_EMITTER_CALL_EDGE"
                        : "REVIEWED_MANAGED_EMITTER_METHOD_GROUP_REFERENCE"
                )
            );
        }
        return result;
    }

    private static string ResolveCalleeOwner(
        string callerOwner,
        IReadOnlyList<CSharpToken> tokens,
        int methodIndex,
        IReadOnlyList<string> possibleOwners
    )
    {
        if (methodIndex >= 2 && tokens[methodIndex - 1].Text == ".")
        {
            string receiver = tokens[methodIndex - 2].Text;
            string resolved = receiver switch
            {
                "_damageEffectValidator" => "SkillDamageEffectValidator",
                "_executeEffectValidator" => "SkillExecuteEffectValidator",
                "_combatProfileValidator" => "SkillCombatProfileValidator",
                "this" => callerOwner,
                _ => null,
            };
            if (resolved != null)
                return resolved;
            foreach (string owner in possibleOwners)
            {
                if (receiver == owner)
                    return owner;
            }
        }
        foreach (string possibleOwner in possibleOwners)
        {
            if (possibleOwner == callerOwner)
                return callerOwner;
        }
        return possibleOwners.Count == 1 ? possibleOwners[0] : null;
    }

    private static void AssertEmitterCallEdgeInventory()
    {
        IReadOnlyList<SourceCallEdgeRow> expectedRows = ReadEmitterCallEdgeInventory();
        IReadOnlyList<SourceCallEdgeRow> actualRows = ReadManagedEmitterCallEdges();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (SourceCallEdgeRow row in expectedRows)
            expected.Add(SourceCallEdgeKey(row));
        foreach (SourceCallEdgeRow row in actualRows)
            actual.Add(SourceCallEdgeKey(row));
        RequireCount("managed emitter call edges", actual.Count, ExpectedEmitterCallEdgeCount);
        RequireExactSet("managed emitter call edges", expected, actual);
    }

    private static IReadOnlyList<SourceCallEdgeRow> ReadEmitterCallEdgeInventory()
    {
        using FileAccess file = OpenRequiredArtifact(SourceEmitterCallEdgeInventoryPath);
        var result = new List<SourceCallEdgeRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int lineNumber = 0;
        while (!file.EofReached())
        {
            lineNumber++;
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            RequireColumnCount(SourceEmitterCallEdgeInventoryPath, lineNumber, columns, 7);
            var row = new SourceCallEdgeRow(
                columns[0],
                columns[1],
                columns[2],
                columns[3],
                columns[4],
                columns[5],
                columns[6]
            );
            foreach ((string column, string value) in new[]
            {
                ("caller_owner", row.CallerOwner),
                ("caller_member", row.CallerMember),
                ("callee_owner", row.CalleeOwner),
                ("callee_member", row.CalleeMember),
                ("classification", row.Classification),
                ("reason", row.Reason),
            })
            {
                RequireNonEmpty(SourceEmitterCallEdgeInventoryPath, lineNumber, column, value);
            }
            SourceSiteLocation location = ParseSourceSite(
                SourceEmitterCallEdgeInventoryPath,
                lineNumber,
                row.CallSite
            );
            if (
                !ExpectedSourcePathByOwner.TryGetValue(
                    row.CallerOwner,
                    out string expectedPath
                )
                || location.SourcePath != expectedPath
                || !ExpectedEmitterOwnerMethods.Contains(
                    $"{row.CalleeOwner}::{row.CalleeMember}"
                )
                || (
                    row.Classification == "MANAGED_EMITTER_CALL"
                        ? row.Reason != "REVIEWED_MANAGED_EMITTER_CALL_EDGE"
                        : row.Classification == "MANAGED_EMITTER_METHOD_GROUP"
                            ? row.Reason
                                != "REVIEWED_MANAGED_EMITTER_METHOD_GROUP_REFERENCE"
                            : true
                )
            )
            {
                throw new InvalidOperationException(
                    $"{SourceEmitterCallEdgeInventoryPath}:{lineNumber} call edge is not reviewed."
                );
            }
            if (!seen.Add(SourceCallEdgeKey(row)))
                throw new InvalidOperationException(
                    $"{SourceEmitterCallEdgeInventoryPath}:{lineNumber} duplicates a managed emitter call edge."
                );
            result.Add(row);
        }
        return result;
    }

    private static string SourceCallEdgeKey(SourceCallEdgeRow row) =>
        $"{row.CallerOwner}\t{row.CallerMember}\t{row.CallSite}\t{row.CalleeOwner}\t{row.CalleeMember}\t{row.Classification}\t{row.Reason}";

    private static void AssertFiniteInstanceExpansion(
        IReadOnlyList<SemanticRuleRow> semanticRules,
        IReadOnlyList<RuleExceptionRow> exceptions,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (SemanticRuleRow row in semanticRules)
        {
            if (row.InstanceKind != "DIRECT_BRANCH")
                expected.Add(
                    FiniteInstanceKey(
                        row.InstanceKind,
                        row.OriginOwner,
                        row.OriginMethod,
                        row.OriginSite,
                        row.InstanceOwner,
                        row.InstanceMethod,
                        row.InstanceSite,
                        row.Selector
                    )
                );
        }
        foreach (RuleExceptionRow row in exceptions)
        {
            if (row.Status != "DEAD_RULE_REMOVED" && row.InstanceKind != "DIRECT_BRANCH")
                expected.Add(
                    FiniteInstanceKey(
                        row.InstanceKind,
                        row.OriginOwner,
                        row.OriginMethod,
                        row.OriginSite,
                        row.InstanceOwner,
                        row.InstanceMethod,
                        row.InstanceSite,
                        row.Selector
                    )
                );
        }
        HashSet<string> actual = ReadFiniteInstanceKeys(directSinkIndex);
        RequireExactSet("finite diagnostic instance expansion", expected, actual);
    }

    private static HashSet<string> ReadFiniteInstanceKeys(
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        string registryPath = ExpectedSourcePathByOwner["SkillContentRegistry"];
        string combatPath = ExpectedSourcePathByOwner["SkillCombatProfileValidator"];
        string saveTagPath = ExpectedSourcePathByOwner["SaveTagListContentRules"];

        foreach (string sourcePath in ExpectedSourcePathByOwner.Values)
        {
            IReadOnlyList<CSharpToken> tokens = LexCSharp(ReadRequiredSource(sourcePath));
            for (int index = 0; index + 3 < tokens.Count; index++)
            {
                if (
                    tokens[index].Text != "SkillContentRegistry"
                    || tokens[index + 1].Text != "."
                    || tokens[index + 2].Kind != CSharpTokenKind.Identifier
                    || tokens[index + 3].Text != "("
                )
                {
                    continue;
                }
                string helper = tokens[index + 2].Text;
                if (helper is not ("RequireBool" or "RequireInt" or "RequireStringName" or "TryReadLevelOverrideInt"))
                    continue;
                int closeIndex = FindMatchingClose(tokens, index + 3, "(", ")");
                IReadOnlyList<(int Start, int End)> arguments = SplitArguments(
                    tokens,
                    index + 4,
                    closeIndex
                );
                if (helper == "TryReadLevelOverrideInt")
                {
                    if (arguments.Count == 6)
                    {
                        string fieldName = FirstStringLiteral(tokens, arguments[4]);
                        if (fieldName != null)
                            AddFiniteKey(
                                result,
                                directSinkIndex,
                                "QUALIFIED_CALLSITE",
                                $"{registryPath}:748",
                                $"{sourcePath}:{tokens[index + 2].Line}",
                                $"fieldName={fieldName}",
                                tokens
                            );
                    }
                    continue;
                }
                if (arguments.Count < 5)
                    throw new InvalidOperationException(
                        $"T1a.7 source lexer could not read {helper} call at {sourcePath}:{tokens[index + 2].Line}."
                    );
                string field = FirstStringLiteral(tokens, arguments[2]);
                if (field == null)
                    throw new InvalidOperationException(
                        $"T1a.7 {helper} field selector at {sourcePath}:{tokens[index + 2].Line} must remain a literal/interpolated string."
                    );
                int originLine = helper switch
                {
                    "RequireStringName" => 514,
                    "RequireInt" => 528,
                    "RequireBool" => 542,
                    _ => throw new InvalidOperationException($"Unexpected helper {helper}."),
                };
                AddFiniteKey(
                    result,
                    directSinkIndex,
                    "QUALIFIED_CALLSITE",
                    $"{registryPath}:{originLine}",
                    $"{sourcePath}:{tokens[index + 2].Line}",
                    $"field={field}",
                    tokens
                );
            }
        }

        IReadOnlyList<CSharpToken> combatTokens = LexCSharp(ReadRequiredSource(combatPath));
        IReadOnlyList<CSharpToken> costMembers = ReadCostLoopMembers(combatTokens);
        foreach (CSharpToken member in costMembers)
        {
            AddFiniteKey(
                result,
                directSinkIndex,
                "LOOP_MEMBER",
                $"{registryPath}:748",
                $"{combatPath}:{member.Line}",
                $"fieldName={member.Text}",
                combatTokens
            );
            AddFiniteKey(
                result,
                directSinkIndex,
                "LOOP_MEMBER",
                $"{combatPath}:491",
                $"{combatPath}:{member.Line}",
                $"costKey={member.Text};costValue<0",
                combatTokens
            );
        }

        ReadTypedEffectParamMappingKeys(combatTokens, combatPath, result, directSinkIndex);
        ReadSaveTagCallsiteKeys(combatTokens, combatPath, saveTagPath, result, directSinkIndex);
        ReadStringNameHelperKeys(combatTokens, combatPath, result, directSinkIndex);
        ReadProjectileHelperKeys(combatTokens, combatPath, result, directSinkIndex);
        ReadRequiredCategoryKeys(result, directSinkIndex);
        return result;
    }

    private static void ReadTypedEffectParamMappingKeys(
        IReadOnlyList<CSharpToken> tokens,
        string combatPath,
        HashSet<string> result,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        int nameIndex = FindToken(tokens, "TypedEffectParamTargets", 0);
        int openIndex = FindToken(tokens, "{", nameIndex + 1);
        int closeIndex = FindMatchingClose(tokens, openIndex, "{", "}");
        int mappingCount = 0;
        for (int index = openIndex + 1; index + 4 < closeIndex; index++)
        {
            if (
                tokens[index].Text != "{"
                || tokens[index + 1].Kind != CSharpTokenKind.StringLiteral
                || tokens[index + 2].Text != ","
                || tokens[index + 3].Kind != CSharpTokenKind.StringLiteral
                || tokens[index + 4].Text != "}"
            )
            {
                continue;
            }
            mappingCount++;
            AddFiniteKey(
                result,
                directSinkIndex,
                "MAPPING_ENTRY",
                $"{combatPath}:2942",
                $"{combatPath}:{tokens[index + 1].Line}",
                $"{tokens[index + 1].Text}=>{tokens[index + 3].Text}",
                tokens
            );
        }
        if (mappingCount == 0)
            throw new InvalidOperationException("T1a.7 did not find TypedEffectParamTargets entries.");
    }

    private static void ReadSaveTagCallsiteKeys(
        IReadOnlyList<CSharpToken> tokens,
        string combatPath,
        string saveTagPath,
        HashSet<string> result,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        var branches = new (int Line, string Name)[]
        {
            (24, "null"),
            (34, "empty"),
            (42, "removed_suffix"),
            (48, "unsupported"),
            (54, "duplicate"),
        };
        for (int index = 0; index + 3 < tokens.Count; index++)
        {
            if (
                tokens[index].Text != "SaveTagListContentRules"
                || tokens[index + 1].Text != "."
                || tokens[index + 2].Text != "AppendValidationErrors"
                || tokens[index + 3].Text != "("
            )
            {
                continue;
            }
            int closeIndex = FindMatchingClose(tokens, index + 3, "(", ")");
            IReadOnlyList<(int Start, int End)> arguments = SplitArguments(
                tokens,
                index + 4,
                closeIndex
            );
            string field = arguments.Count >= 2
                ? FirstStringLiteral(tokens, arguments[1])
                : null;
            if (field == null)
                throw new InvalidOperationException(
                    $"T1a.7 SaveTagList callsite at {combatPath}:{tokens[index + 2].Line} must keep a literal field label."
                );
            foreach ((int originLine, string branch) in branches)
            {
                AddFiniteKey(
                    result,
                    directSinkIndex,
                    "CALLSITE_BRANCH",
                    $"{saveTagPath}:{originLine}",
                    $"{combatPath}:{tokens[index + 2].Line}",
                    $"field={field};branch={branch}",
                    tokens
                );
            }
        }
    }

    private static void ReadStringNameHelperKeys(
        IReadOnlyList<CSharpToken> tokens,
        string combatPath,
        HashSet<string> result,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            string helper = tokens[index].Text;
            if (
                helper is not ("AppendStringNameArrayValidationErrors" or "AppendUniqueStringNameArrayValidationErrors")
                || tokens[index + 1].Text != "("
            )
            {
                continue;
            }
            if (!TryFindEnclosingMember(tokens, index, out string callerMember))
                continue;
            if (
                helper == "AppendStringNameArrayValidationErrors"
                && callerMember == "AppendUniqueStringNameArrayValidationErrors"
            )
            {
                continue;
            }
            int closeIndex = FindMatchingClose(tokens, index + 1, "(", ")");
            IReadOnlyList<(int Start, int End)> arguments = SplitArguments(
                tokens,
                index + 2,
                closeIndex
            );
            string field = arguments.Count >= 3
                ? FirstStringLiteral(tokens, arguments[2])
                : null;
            if (field == null)
                throw new InvalidOperationException(
                    $"T1a.7 {helper} callsite at {combatPath}:{tokens[index].Line} must keep a literal field label."
                );
            AddFiniteKey(
                result,
                directSinkIndex,
                "QUALIFIED_CALLSITE",
                $"{combatPath}:3097",
                $"{combatPath}:{tokens[index].Line}",
                $"field={field};branch=non_empty",
                tokens
            );
            if (helper == "AppendUniqueStringNameArrayValidationErrors")
            {
                AddFiniteKey(
                    result,
                    directSinkIndex,
                    "QUALIFIED_CALLSITE",
                    $"{combatPath}:3114",
                    $"{combatPath}:{tokens[index].Line}",
                    $"field={field};branch=duplicate",
                    tokens
                );
            }
        }
    }

    private static void ReadProjectileHelperKeys(
        IReadOnlyList<CSharpToken> tokens,
        string combatPath,
        HashSet<string> result,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (
                tokens[index].Text != "AppendProjectileCategoryOwnershipErrors"
                || tokens[index + 1].Text != "("
            )
            {
                continue;
            }
            if (!TryFindEnclosingMember(tokens, index, out _))
                continue;
            int closeIndex = FindMatchingClose(tokens, index + 1, "(", ")");
            IReadOnlyList<(int Start, int End)> arguments = SplitArguments(
                tokens,
                index + 2,
                closeIndex
            );
            string field = arguments.Count >= 3
                ? FirstStringLiteral(tokens, arguments[2])
                : null;
            if (field == null)
                throw new InvalidOperationException(
                    $"T1a.7 projectile callsite at {combatPath}:{tokens[index].Line} must keep a literal field label."
                );
            foreach ((int line, string branch) in new[] { (3132, "derived"), (3138, "removed") })
            {
                AddFiniteKey(
                    result,
                    directSinkIndex,
                    "QUALIFIED_CALLSITE",
                    $"{combatPath}:{line}",
                    $"{combatPath}:{tokens[index].Line}",
                    $"field={field};branch={branch}",
                    tokens
                );
            }
        }
    }

    private static void ReadRequiredCategoryKeys(
        HashSet<string> result,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex
    )
    {
        IReadOnlyList<CSharpToken> tokens = LexCSharp(ReadRequiredSource(EffectCategoryRulesPath));
        string combatPath = ExpectedSourcePathByOwner["SkillCombatProfileValidator"];
        for (int index = 0; index + 5 < tokens.Count; index++)
        {
            if (
                tokens[index].Text != "required"
                || tokens[index + 1].Text != "."
                || tokens[index + 2].Text != "Add"
                || tokens[index + 3].Text != "("
                || tokens[index + 4].Kind != CSharpTokenKind.Identifier
                || tokens[index + 5].Text != ")"
            )
            {
                continue;
            }
            string mappingMember = FindEnclosingMember(tokens, index + 2);
            bool delivery = mappingMember == "RequiredDeliveryCategories";
            if (!delivery && mappingMember != "RequiredEffectCategories")
                throw new InvalidOperationException(
                    $"T1a.7 required category mapping at {EffectCategoryRulesPath}:{tokens[index + 2].Line} has unexpected member {mappingMember}."
                );
            AddFiniteKey(
                result,
                directSinkIndex,
                "CALLSITE_BRANCH",
                $"{combatPath}:{(delivery ? 412 : 1934)}",
                $"{EffectCategoryRulesPath}:{tokens[index + 2].Line}",
                $"requiredCategory={tokens[index + 4].Text}",
                tokens
            );
        }
    }

    private static IReadOnlyList<CSharpToken> ReadCostLoopMembers(
        IReadOnlyList<CSharpToken> tokens
    )
    {
        for (int index = 0; index + 6 < tokens.Count; index++)
        {
            if (
                tokens[index].Text != "string"
                || tokens[index + 1].Text != "costKey"
                || tokens[index + 2].Text != "in"
            )
            {
                continue;
            }
            int openIndex = FindToken(tokens, "{", index + 3);
            int closeIndex = FindMatchingClose(tokens, openIndex, "{", "}");
            var result = new List<CSharpToken>();
            for (int memberIndex = openIndex + 1; memberIndex < closeIndex; memberIndex++)
            {
                if (tokens[memberIndex].Kind == CSharpTokenKind.StringLiteral)
                    result.Add(tokens[memberIndex]);
            }
            return result;
        }
        throw new InvalidOperationException("T1a.7 did not find the level-override costKey loop.");
    }

    private static int FindToken(
        IReadOnlyList<CSharpToken> tokens,
        string text,
        int startIndex
    )
    {
        for (int index = startIndex; index < tokens.Count; index++)
        {
            if (tokens[index].Text == text)
                return index;
        }
        throw new InvalidOperationException($"T1a.7 source lexer did not find token {text}.");
    }

    private static int FindMatchingClose(
        IReadOnlyList<CSharpToken> tokens,
        int openIndex,
        string open,
        string close
    )
    {
        int depth = 0;
        for (int index = openIndex; index < tokens.Count; index++)
        {
            if (tokens[index].Text == open)
                depth++;
            else if (tokens[index].Text == close && --depth == 0)
                return index;
        }
        throw new InvalidOperationException(
            $"T1a.7 source lexer did not find matching {close} for {open} at token {openIndex}."
        );
    }

    private static IReadOnlyList<(int Start, int End)> SplitArguments(
        IReadOnlyList<CSharpToken> tokens,
        int startIndex,
        int endIndex
    )
    {
        var result = new List<(int Start, int End)>();
        int argumentStart = startIndex;
        int parenthesisDepth = 0;
        int bracketDepth = 0;
        int braceDepth = 0;
        for (int index = startIndex; index < endIndex; index++)
        {
            string token = tokens[index].Text;
            if (token == "(") parenthesisDepth++;
            else if (token == ")") parenthesisDepth--;
            else if (token == "[") bracketDepth++;
            else if (token == "]") bracketDepth--;
            else if (token == "{") braceDepth++;
            else if (token == "}") braceDepth--;
            else if (
                token == ","
                && parenthesisDepth == 0
                && bracketDepth == 0
                && braceDepth == 0
            )
            {
                result.Add((argumentStart, index));
                argumentStart = index + 1;
            }
        }
        result.Add((argumentStart, endIndex));
        return result;
    }

    private static string FirstStringLiteral(
        IReadOnlyList<CSharpToken> tokens,
        (int Start, int End) argument
    )
    {
        for (int index = argument.Start; index < argument.End; index++)
        {
            if (tokens[index].Kind == CSharpTokenKind.StringLiteral)
                return tokens[index].Text;
        }
        return null;
    }

    private static string FiniteInstanceKey(
        string kind,
        string originOwner,
        string originMember,
        string originSite,
        string instanceOwner,
        string instanceMember,
        string instanceSite,
        string selector
    ) =>
        $"{kind}\t{originOwner}\t{originMember}\t{originSite}\t{instanceOwner}\t{instanceMember}\t{instanceSite}\t{selector}";

    private static void AddFiniteKey(
        HashSet<string> result,
        IReadOnlyDictionary<string, SourceMemberSite> directSinkIndex,
        string kind,
        string originSite,
        string instanceSite,
        string selector,
        IReadOnlyList<CSharpToken> instanceTokens
    )
    {
        if (!directSinkIndex.TryGetValue(originSite, out SourceMemberSite origin))
            throw new InvalidOperationException(
                $"T1a.7 finite instance references missing direct origin {originSite}."
            );
        SourceSiteLocation instanceLocation = ParseSourceSite(
            "T1a.7 source structure",
            1,
            instanceSite
        );
        string instanceOwner = OwnerForSourcePath(instanceLocation.SourcePath);
        int instanceTokenIndex = FindFirstTokenIndexAtLine(
            instanceTokens,
            instanceLocation.Line
        );
        string instanceMember = FindEnclosingMember(
            instanceTokens,
            instanceTokenIndex
        );
        string key = FiniteInstanceKey(
            kind,
            origin.Owner,
            origin.Member,
            origin.SourceSite,
            instanceOwner,
            instanceMember,
            instanceSite,
            selector
        );
        if (!result.Add(key))
            throw new InvalidOperationException(
                $"T1a.7 source lexer found duplicate finite instance {key}."
            );
    }

    private static string SourceMemberSiteKey(
        string owner,
        string member,
        string sourceSite
    ) => $"{owner}\t{member}\t{sourceSite}";

    private static string OwnerForSourcePath(string sourcePath)
    {
        if (sourcePath == EffectCategoryRulesPath)
            return "CombatEffectCategoryContentRules";
        foreach ((string owner, string expectedPath) in ExpectedSourcePathByOwner)
        {
            if (sourcePath == expectedPath)
                return owner;
        }
        throw new InvalidOperationException(
            $"T1a.7 source structure uses unreviewed owner path {sourcePath}."
        );
    }

    private static int FindFirstTokenIndexAtLine(
        IReadOnlyList<CSharpToken> tokens,
        int line
    )
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Line == line)
                return index;
        }
        throw new InvalidOperationException(
            $"T1a.7 source lexer found no token at reviewed source line {line}."
        );
    }

    private static string FindEnclosingMember(
        IReadOnlyList<CSharpToken> tokens,
        int tokenIndex
    )
    {
        var openBraces = new List<int>();
        for (int index = 0; index < tokenIndex; index++)
        {
            if (tokens[index].Text == "{")
                openBraces.Add(index);
            else if (tokens[index].Text == "}" && openBraces.Count > 0)
                openBraces.RemoveAt(openBraces.Count - 1);
        }

        for (int stackIndex = openBraces.Count - 1; stackIndex >= 0; stackIndex--)
        {
            int openIndex = openBraces[stackIndex];
            int headerEnd = openIndex - 1;
            if (headerEnd < 0)
                continue;
            if (tokens[headerEnd].Text is "set" or "init" or "get")
            {
                string accessor = tokens[headerEnd].Text;
                if (stackIndex == 0)
                    throw new InvalidOperationException(
                        $"T1a.7 source lexer could not bind {accessor} accessor at line {tokens[headerEnd].Line} to a property."
                    );
                int propertyOpen = openBraces[stackIndex - 1];
                string propertyName = FindPropertyNameBeforeOpen(tokens, propertyOpen);
                return $"{propertyName}.{accessor}";
            }
            if (tokens[headerEnd].Text != ")")
                continue;
            int openParenthesis = FindMatchingOpen(
                tokens,
                headerEnd,
                "(",
                ")"
            );
            if (openParenthesis == 0)
                continue;
            CSharpToken candidate = tokens[openParenthesis - 1];
            if (
                candidate.Kind != CSharpTokenKind.Identifier
                || candidate.Text
                    is "if"
                        or "for"
                        or "foreach"
                        or "while"
                        or "switch"
                        or "catch"
                        or "using"
                        or "lock"
                        or "new"
            )
            {
                continue;
            }
            return candidate.Text;
        }

        for (int stackIndex = openBraces.Count - 1; stackIndex >= 0; stackIndex--)
        {
            int fieldInitializerOpen = openBraces[stackIndex];
            for (int index = fieldInitializerOpen - 1; index >= 0; index--)
            {
                if (tokens[index].Text is ";" or "{")
                    break;
                if (tokens[index].Text != "=")
                    continue;
                for (int nameIndex = index - 1; nameIndex >= 0; nameIndex--)
                {
                    if (tokens[nameIndex].Kind == CSharpTokenKind.Identifier)
                        return tokens[nameIndex].Text;
                }
            }
        }
        throw new InvalidOperationException(
            $"T1a.7 source lexer could not bind token {tokens[tokenIndex].Text} at line {tokens[tokenIndex].Line} to an enclosing method, accessor, or field."
        );
    }

    private static bool TryFindEnclosingMember(
        IReadOnlyList<CSharpToken> tokens,
        int tokenIndex,
        out string member
    )
    {
        try
        {
            member = FindEnclosingMember(tokens, tokenIndex);
            return true;
        }
        catch (InvalidOperationException)
        {
            member = null;
            return false;
        }
    }

    private static string FindPropertyNameBeforeOpen(
        IReadOnlyList<CSharpToken> tokens,
        int propertyOpenIndex
    )
    {
        for (int index = propertyOpenIndex - 1; index >= 0; index--)
        {
            if (tokens[index].Kind == CSharpTokenKind.Identifier)
                return tokens[index].Text;
            if (tokens[index].Text is ";" or "}")
                break;
        }
        throw new InvalidOperationException(
            $"T1a.7 source lexer could not bind property accessor before line {tokens[propertyOpenIndex].Line}."
        );
    }

    private static int FindMatchingOpen(
        IReadOnlyList<CSharpToken> tokens,
        int closeIndex,
        string open,
        string close
    )
    {
        int depth = 0;
        for (int index = closeIndex; index >= 0; index--)
        {
            if (tokens[index].Text == close)
                depth++;
            else if (tokens[index].Text == open && --depth == 0)
                return index;
        }
        throw new InvalidOperationException(
            $"T1a.7 source lexer did not find matching {open} before token {closeIndex}."
        );
    }

    private static string ReadRequiredSource(string sourcePath)
    {
        using FileAccess file = OpenRequiredArtifact(sourcePath);
        return file.GetAsText();
    }

    private static IReadOnlyList<CSharpToken> LexCSharp(string source)
    {
        var result = new List<CSharpToken>();
        int line = 1;
        int index = 0;
        while (index < source.Length)
        {
            char current = source[index];
            if (current == '\r')
            {
                index++;
                continue;
            }
            if (current == '\n')
            {
                line++;
                index++;
                continue;
            }
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }
            if (current == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                index += 2;
                while (index < source.Length && source[index] != '\n')
                    index++;
                continue;
            }
            if (current == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < source.Length)
                {
                    if (source[index] == '\n')
                        line++;
                    if (source[index] == '*' && source[index + 1] == '/')
                    {
                        index += 2;
                        break;
                    }
                    index++;
                }
                continue;
            }
            if (current == '\'' )
            {
                index++;
                while (index < source.Length)
                {
                    if (source[index] == '\\' && index + 1 < source.Length)
                    {
                        index += 2;
                        continue;
                    }
                    if (source[index++] == '\'')
                        break;
                }
                continue;
            }

            if (TryReadCSharpString(source, ref index, ref line, out CSharpToken stringToken))
            {
                result.Add(stringToken);
                continue;
            }
            if (char.IsLetter(current) || current == '_')
            {
                int start = index++;
                while (
                    index < source.Length
                    && (char.IsLetterOrDigit(source[index]) || source[index] == '_')
                )
                {
                    index++;
                }
                result.Add(
                    new CSharpToken(
                        CSharpTokenKind.Identifier,
                        source[start..index],
                        line
                    )
                );
                continue;
            }
            result.Add(new CSharpToken(CSharpTokenKind.Symbol, current.ToString(), line));
            index++;
        }
        return result;
    }

    private static bool TryReadCSharpString(
        string source,
        ref int index,
        ref int line,
        out CSharpToken token
    )
    {
        token = null;
        int start = index;
        int tokenLine = line;
        int dollarCount = 0;
        while (start + dollarCount < source.Length && source[start + dollarCount] == '$')
            dollarCount++;

        int rawQuoteStart = start + dollarCount;
        int rawQuoteCount = CountRun(source, rawQuoteStart, '"');
        if (rawQuoteCount >= 3)
        {
            index = rawQuoteStart + rawQuoteCount;
            var rawValue = new StringBuilder();
            while (index < source.Length)
            {
                int closingQuoteCount = CountRun(source, index, '"');
                if (closingQuoteCount >= rawQuoteCount)
                {
                    index += rawQuoteCount;
                    token = new CSharpToken(
                        CSharpTokenKind.StringLiteral,
                        rawValue.ToString(),
                        tokenLine
                    );
                    return true;
                }
                char valueChar = source[index++];
                if (valueChar == '\n')
                    line++;
                rawValue.Append(valueChar);
            }
            throw new InvalidOperationException(
                $"T1a.7 C# lexer found unterminated raw string at line {tokenLine}."
            );
        }

        bool verbatim = false;
        int quoteIndex = -1;
        if (start < source.Length && source[start] == '@')
        {
            if (start + 1 < source.Length && source[start + 1] == '"')
            {
                verbatim = true;
                quoteIndex = start + 1;
            }
            else if (
                start + 2 < source.Length
                && source[start + 1] == '$'
                && source[start + 2] == '"'
            )
            {
                verbatim = true;
                quoteIndex = start + 2;
            }
        }
        else if (dollarCount > 0)
        {
            int afterDollars = start + dollarCount;
            if (
                afterDollars + 1 < source.Length
                && source[afterDollars] == '@'
                && source[afterDollars + 1] == '"'
            )
            {
                verbatim = true;
                quoteIndex = afterDollars + 1;
            }
            else if (afterDollars < source.Length && source[afterDollars] == '"')
            {
                quoteIndex = afterDollars;
            }
        }
        else if (start < source.Length && source[start] == '"')
        {
            quoteIndex = start;
        }

        if (quoteIndex < 0)
            return false;
        index = quoteIndex + 1;
        var value = new StringBuilder();
        while (index < source.Length)
        {
            char valueChar = source[index++];
            if (valueChar == '\n')
                line++;
            if (verbatim && valueChar == '"' && index < source.Length && source[index] == '"')
            {
                value.Append('"');
                index++;
                continue;
            }
            if (valueChar == '"')
            {
                token = new CSharpToken(
                    CSharpTokenKind.StringLiteral,
                    value.ToString(),
                    tokenLine
                );
                return true;
            }
            if (!verbatim && valueChar == '\\' && index < source.Length)
            {
                char escaped = source[index++];
                value.Append(
                    escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => escaped,
                    }
                );
                continue;
            }
            value.Append(valueChar);
        }
        throw new InvalidOperationException(
            $"T1a.7 C# lexer found unterminated string at line {tokenLine}."
        );
    }

    private static int CountRun(string source, int index, char value)
    {
        int count = 0;
        while (index + count < source.Length && source[index + count] == value)
            count++;
        return count;
    }

    private static void RequireExactSet(
        string label,
        IReadOnlySet<string> expected,
        IReadOnlySet<string> actual
    )
    {
        var missing = new List<string>();
        var unexpected = new List<string>();
        foreach (string value in expected)
        {
            if (!actual.Contains(value))
                missing.Add(value);
        }
        foreach (string value in actual)
        {
            if (!expected.Contains(value))
                unexpected.Add(value);
        }
        if (missing.Count == 0 && unexpected.Count == 0)
            return;
        missing.Sort(StringComparer.Ordinal);
        unexpected.Sort(StringComparer.Ordinal);
        throw new InvalidOperationException(
            $"T1a.7 {label} exact-set mismatch. missing=[{string.Join(", ", missing)}] unexpected=[{string.Join(", ", unexpected)}]"
        );
    }

    private static List<string> ValidateFixture(string fixtureId)
    {
        if (fixtureId == NullLoaderFixtureId)
        {
            using SkillContentRegistry nullRegistry = new(
                new NullContentResourceLoader(),
                loadDefaultContent: false
            );
            nullRegistry.LoadFromDirectory($"{FixtureRoot}/registry_not_skill");
            return CopyDiagnostics(nullRegistry.Validate());
        }
        using TestContentResourceLoader loader = new();
        using SkillContentRegistry registry = new(loader, loadDefaultContent: false);
        string directoryPath = fixtureId == MissingDirectoryFixtureId
            ? $"{FixtureRoot}/directory_that_does_not_exist"
            : $"{FixtureRoot}/{fixtureId}";
        registry.LoadFromDirectory(directoryPath);
        return CopyDiagnostics(registry.Validate());
    }

    private static FileAccess OpenRequiredArtifact(string path)
    {
        if (!FileAccess.FileExists(path))
            throw new InvalidOperationException($"Required T1a.7 artifact is missing: {path}.");
        FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"Unable to open required T1a.7 artifact {path}.");
        return file;
    }

    private static string DecodeDiagnostic(string path, int lineNumber, string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"{path}:{lineNumber} has invalid base64 diagnostic.",
                exception
            );
        }
    }

    private static SourceSiteLocation ParseSourceSite(
        string artifactPath,
        int artifactLineNumber,
        string sourceSite
    )
    {
        RequireNonEmpty(artifactPath, artifactLineNumber, "source_site", sourceSite);
        int separatorIndex = sourceSite.LastIndexOf(':');
        if (
            separatorIndex <= 0
            || !sourceSite.StartsWith("res://", StringComparison.Ordinal)
            || !sourceSite[..separatorIndex].EndsWith(".cs", StringComparison.Ordinal)
            || !int.TryParse(sourceSite[(separatorIndex + 1)..], out int sourceLine)
            || sourceLine <= 0
        )
        {
            throw new InvalidOperationException(
                $"{artifactPath}:{artifactLineNumber} source_site must use res://path.cs:<positive-line> syntax; got {sourceSite}."
            );
        }
        return new SourceSiteLocation(sourceSite[..separatorIndex], sourceLine);
    }

    private static void RequireColumnCount(
        string path,
        int lineNumber,
        string[] columns,
        int expected
    )
    {
        if (columns.Length != expected)
            throw new InvalidOperationException(
                $"{path}:{lineNumber} must have {expected} tab-separated columns; got {columns.Length}."
            );
    }

    private static void RequireNonEmpty(
        string path,
        int lineNumber,
        string column,
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{path}:{lineNumber} has an empty {column}."
            );
    }

    private static void RequireCount(string label, int actual, int expected)
    {
        if (actual != expected)
            throw new InvalidOperationException(
                $"T1a.7 {label} count mismatch: expected {expected}, got {actual}."
            );
    }

    private static void RequireStatusCount(
        IReadOnlyDictionary<string, int> counts,
        string status,
        int expected
    )
    {
        counts.TryGetValue(status, out int actual);
        RequireCount($"exception status {status}", actual, expected);
    }

    private static bool IsSyntheticFixture(string fixtureId) =>
        fixtureId == MissingDirectoryFixtureId || fixtureId == NullLoaderFixtureId;

    private static List<string> CopyDiagnostics(IEnumerable<string> diagnostics)
    {
        var result = new List<string>();
        if (diagnostics == null)
            return result;
        foreach (string diagnostic in diagnostics)
            result.Add(diagnostic ?? "");
        return result;
    }

    private static string FormatDiagnostics(IEnumerable<string> diagnostics)
    {
        return $"[{string.Join(" | ", diagnostics ?? Array.Empty<string>())}]";
    }
}
