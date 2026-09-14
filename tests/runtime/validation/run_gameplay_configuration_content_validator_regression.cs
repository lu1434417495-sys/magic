#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_gameplay_configuration_content_validator_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            AssertProductionContentPublishesWithoutDiagnostics();
            AssertBattleSkillRoleProjectsIntoGenerationFixtures();
            AssertRewardAmountRulesAreKindSpecific();
            AssertFixtureDiagnosticsUseFieldPointers();
            AssertCrossDomainReferencesFailClosed();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected gameplay configuration validator exception: {exception}");
        }

        RequestTestExit(_test.Finish("Gameplay configuration content validator regression"));
    }

    private void AssertBattleSkillRoleProjectsIntoGenerationFixtures()
    {
        var registry = new GameplayConfigurationContentRegistry();
        registry.Rebuild();
        if (registry.GetValidationErrors().Count != 0)
            return;

        GameplayConfigurationDefinition configuration = registry.GetDefinition();
        StringName basicAttackSkillId = configuration.BattleSkillRoles.BasicAttackSkillId;
        _test.True(
            basicAttackSkillId != "",
            "battle_skill_roles must publish a non-empty basic attack role"
        );
        _test.Eq(
            configuration.SkillGenerationBattleSim.BasicAttackSkillId,
            basicAttackSkillId,
            "skill generation fixture must borrow the shared basic attack role"
        );
        _test.Eq(
            configuration.EquipmentGenerationBattleSim.BasicAttackSkillId,
            basicAttackSkillId,
            "equipment generation fixture must borrow the shared basic attack role"
        );
    }

    private void AssertProductionContentPublishesWithoutDiagnostics()
    {
        var registry = new GameplayConfigurationContentRegistry();
        registry.Rebuild();
        IReadOnlyList<string> importErrors = registry.GetValidationErrors();
        _test.Eq(
            importErrors.Count,
            0,
            $"production gameplay configuration should import cleanly: {FormatErrors(importErrors)}"
        );
        if (importErrors.Count != 0)
            return;

        GameplayConfigurationDefinition configuration = registry.GetDefinition();
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        IReadOnlyList<string> crossDomainErrors = GameplayConfigurationCrossDomainValidator.Validate(
            configuration,
            snapshot.Skills,
            snapshot.Items,
            snapshot.EnemyBrains,
            snapshot.Races,
            snapshot.Subraces,
            snapshot.AgeProfiles
        );
        _test.Eq(
            crossDomainErrors.Count,
            0,
            $"production gameplay configuration references should resolve: {FormatErrors(crossDomainErrors)}"
        );
    }

    private void AssertRewardAmountRulesAreKindSpecific()
    {
        AssertAmountRejected("attribute_delta", 0);
        AssertAmountRejected("attribute_delta", -1);
        AssertAmountRejected("skill_mastery", 0);
        AssertAmountRejected("skill_mastery", -1);

        AssertAmountAccepted("skill_unlock", 0);
        AssertAmountAccepted("knowledge_unlock", 0);
        AssertAmountAccepted("attribute_progress", 0);

        IReadOnlyList<ContentJsonDiagnostic> unsupported = Validate(
            BuildValidDto([BuildReward("unsupported_reward", 1)])
        );
        _test.True(
            unsupported.Any(diagnostic =>
                diagnostic.RuleId == GameplayConfigurationJsonRules.InvalidValue
                && diagnostic.JsonPointer
                    == "/entries/0/achievements/0/rewards/0/reward_type"
            ),
            "unknown achievement reward kinds must fail closed at reward_type"
        );
    }

    private void AssertAmountRejected(string rewardType, int amount)
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            BuildValidDto([BuildReward(rewardType, amount)])
        );
        _test.True(
            diagnostics.Any(diagnostic =>
                diagnostic.RuleId == GameplayConfigurationJsonRules.InvalidValue
                && diagnostic.JsonPointer == "/entries/0/achievements/0/rewards/0/amount"
            ),
            $"{rewardType} amount {amount} must be rejected at its amount field"
        );
    }

    private void AssertAmountAccepted(string rewardType, int amount)
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            BuildValidDto([BuildReward(rewardType, amount)])
        );
        _test.Eq(
            diagnostics.Count,
            0,
            $"{rewardType} amount {amount} should preserve its existing zero-value semantics: {FormatDiagnostics(diagnostics)}"
        );
    }

    private void AssertFixtureDiagnosticsUseFieldPointers()
    {
        var emptyFixture = new SkillGenerationBattleSimFixtureJsonDto();
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            BuildValidDto(
                skillFixture: emptyFixture,
                battleSkillRoles: new BattleSkillRolesJsonDto()
            )
        );
        string[] expectedPointers =
        {
            "/entries/0/battle_skill_roles/basic_attack_skill_id",
            "/entries/0/skill_generation_battle_sim/ground_benchmark_skill_id",
            "/entries/0/skill_generation_battle_sim/multi_target_benchmark_skill_id",
            "/entries/0/skill_generation_battle_sim/ranged_unit_benchmark_skill_id",
            "/entries/0/skill_generation_battle_sim/melee_brain_id",
            "/entries/0/skill_generation_battle_sim/mage_brain_id",
            "/entries/0/skill_generation_battle_sim/ranged_brain_id",
        };
        foreach (string pointer in expectedPointers)
        {
            _test.True(
                diagnostics.Any(diagnostic =>
                    diagnostic.RuleId == GameplayConfigurationJsonRules.IdRequired
                    && diagnostic.JsonPointer == pointer
                ),
                $"fixture diagnostic should use JSON field pointer {pointer}"
            );
        }
        _test.False(
            diagnostics.Any(diagnostic =>
                diagnostic.JsonPointer.StartsWith(
                    "/entries/0/skill_generation_battle_sim/0",
                    StringComparison.Ordinal
                )
            ),
            "fixture diagnostics must not use array-index pointers for object fields"
        );
    }

    private void AssertCrossDomainReferencesFailClosed()
    {
        var registry = new GameplayConfigurationContentRegistry();
        registry.Rebuild();
        if (registry.GetValidationErrors().Count != 0)
            return;

        GameplayConfigurationDefinition production = registry.GetDefinition();
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        var achievement = new AchievementDefinition(
            "invalid_reference_probe",
            "Invalid Reference Probe",
            "Validator-only definition.",
            "battle_won",
            "",
            1,
            [
                new AchievementRewardDefinition(
                    "attribute_delta",
                    "invalid_permanent_attribute",
                    "Invalid Attribute",
                    1,
                    "Validator probe."
                ),
                new AchievementRewardDefinition(
                    "attribute_progress",
                    "hp_max",
                    "Invalid Progress Target",
                    1,
                    "Validator probe."
                ),
                new AchievementRewardDefinition(
                    "skill_unlock",
                    "missing_reward_skill",
                    "Missing Skill",
                    0,
                    "Validator probe."
                ),
            ]
        );
        var achievements = new Dictionary<StringName, AchievementDefinition>
        {
            [achievement.AchievementId] = achievement,
        };
        var shop = new SettlementShopDefinition(
            "probe_interaction",
            "probe_shop",
            "Probe Shop",
            1,
            [new SettlementShopItemDefinition("missing_shop_item", 1, 1, 0, 10000)],
            Array.Empty<SettlementShopItemDefinition>(),
            0,
            0,
            10000
        );
        SkillGenerationBattleSimFixtureDefinition productionFixture =
            production.SkillGenerationBattleSim;
        var invalidFixture = new SkillGenerationBattleSimFixtureDefinition(
            "missing_fixture_skill",
            productionFixture.GroundBenchmarkSkillId,
            productionFixture.MultiTargetBenchmarkSkillId,
            productionFixture.RangedUnitBenchmarkSkillId,
            "missing_brain",
            productionFixture.MageBrainId,
            productionFixture.RangedBrainId
        );
        var invalid = new GameplayConfigurationDefinition(
            "invalid_reference_probe",
            achievements,
            [shop],
            production.NewGameParty,
            new BattleSkillRoleDefinition("missing_fixture_skill"),
            invalidFixture,
            production.EquipmentGenerationBattleSim
        );

        IReadOnlyList<string> errors = GameplayConfigurationCrossDomainValidator.Validate(
            invalid,
            snapshot.Skills,
            snapshot.Items,
            snapshot.EnemyBrains,
            snapshot.Races,
            snapshot.Subraces,
            snapshot.AgeProfiles
        );
        _test.Eq(
            errors.Count,
            6,
            $"invalid typed references should produce one diagnostic each: {FormatErrors(errors)}"
        );
        AssertError(errors, "attribute_delta references invalid attribute invalid_permanent_attribute");
        AssertError(errors, "attribute_progress references invalid attribute hp_max");
        AssertError(errors, "references missing skill missing_reward_skill");
        AssertError(errors, "references missing item missing_shop_item");
        AssertError(errors, "references missing skill missing_fixture_skill");
        AssertError(errors, "references missing brain missing_brain");
    }

    private void AssertError(IReadOnlyList<string> errors, string fragment)
    {
        _test.True(
            errors.Any(error => error.Contains(fragment, StringComparison.Ordinal)),
            $"cross-domain diagnostics should contain '{fragment}': {FormatErrors(errors)}"
        );
    }

    private static GameplayConfigurationJsonDto BuildValidDto(
        IReadOnlyList<GameplayAchievementRewardJsonDto>? rewards = null,
        SkillGenerationBattleSimFixtureJsonDto? skillFixture = null,
        BattleSkillRolesJsonDto? battleSkillRoles = null
    ) =>
        new()
        {
            ConfigurationId = "validator_probe",
            Achievements =
            [
                new GameplayAchievementJsonDto
                {
                    AchievementId = "validator_probe_achievement",
                    DisplayName = "Validator Probe",
                    Description = "Minimal valid gameplay configuration.",
                    EventType = "battle_won",
                    SubjectId = "",
                    Threshold = 1,
                    Rewards = rewards ?? [BuildReward("knowledge_unlock", 0)],
                },
            ],
            SettlementShops = Array.Empty<SettlementShopJsonDto>(),
            NewGameParty = new NewGamePartyJsonDto
            {
                Gold = 0,
                LeaderMemberId = "probe_member",
                MainCharacterMemberId = "probe_member",
                ActiveMemberIds = ["probe_member"],
                ReserveMemberIds = Array.Empty<string>(),
                Members =
                [
                    new NewGameMemberJsonDto
                    {
                        MemberId = "probe_member",
                        DisplayName = "Probe Member",
                        FactionId = "player",
                        PortraitId = "portrait_probe",
                        ControlMode = "manual",
                        RaceId = "probe_race",
                        SubraceId = "probe_subrace",
                        AgeYears = 20,
                        AgeProfileId = "probe_age_profile",
                        NaturalAgeStageId = "adult",
                        EffectiveAgeStageId = "adult",
                        BodySizeCategory = "medium",
                        CurrentMp = 0,
                        StorageSpace = 0,
                        BaseAttributes = new Dictionary<string, int>
                        {
                            ["strength"] = 1,
                            ["agility"] = 1,
                            ["constitution"] = 1,
                            ["perception"] = 1,
                            ["intelligence"] = 1,
                            ["willpower"] = 1,
                        },
                        StartingSkillIds = Array.Empty<string>(),
                    },
                ],
                StartingWeaponRules = Array.Empty<NewGameStartingWeaponRuleJsonDto>(),
                StartingWeaponFallbackItemId = "probe_weapon",
            },
            BattleSkillRoles = battleSkillRoles ?? new BattleSkillRolesJsonDto
            {
                BasicAttackSkillId = "probe_basic_attack",
            },
            SkillGenerationBattleSim = skillFixture ?? new SkillGenerationBattleSimFixtureJsonDto
            {
                GroundBenchmarkSkillId = "probe_ground_skill",
                MultiTargetBenchmarkSkillId = "probe_multi_target_skill",
                RangedUnitBenchmarkSkillId = "probe_ranged_skill",
                MeleeBrainId = "probe_melee_brain",
                MageBrainId = "probe_mage_brain",
                RangedBrainId = "probe_ranged_brain",
            },
            EquipmentGenerationBattleSim = new EquipmentGenerationBattleSimFixtureJsonDto
            {
                MeleeBrainId = "probe_melee_brain",
            },
        };

    private static GameplayAchievementRewardJsonDto BuildReward(string rewardType, int amount) =>
        new()
        {
            RewardType = rewardType,
            TargetId = "probe_target",
            TargetLabel = "Probe Target",
            Amount = amount,
            ReasonText = "Validator probe.",
        };

    private static IReadOnlyList<ContentJsonDiagnostic> Validate(
        GameplayConfigurationJsonDto dto
    ) => GameplayConfigurationJsonValidator.Validate(
        new JsonContentEntryContext(
            GameplayConfigurationJsonDomain.DomainId,
            dto.ConfigurationId,
            $"fixture.json#{dto.ConfigurationId}",
            "/entries/0"
        ),
        dto
    );

    private static string FormatDiagnostics(
        IReadOnlyList<ContentJsonDiagnostic> diagnostics
    ) => string.Join(
        " | ",
        diagnostics.Select(diagnostic =>
            $"{diagnostic.RuleId}@{diagnostic.JsonPointer}: {diagnostic.Message}"
        )
    );

    private static string FormatErrors(IReadOnlyList<string> errors) =>
        errors.Count == 0 ? "[]" : $"[{string.Join(" | ", errors)}]";
}
