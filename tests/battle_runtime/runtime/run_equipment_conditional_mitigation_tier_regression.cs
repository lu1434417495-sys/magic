using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_conditional_mitigation_tier_regression : LifecycleTestSceneTree
{
    private static readonly StringName HalfBindingId = "binding.test.dragon_ward";
    private static readonly StringName HalfUnlabeledBindingId =
        "binding.test.dragon_ward_unlabeled";
    private static readonly StringName DoubleBindingId = "binding.test.dragon_ward_double";
    private static readonly StringName ImmuneBindingId = "binding.test.dragon_ward_immune";
    private static readonly StringName FixedDrBindingId = "binding.test.fire_dr";
    private static readonly StringName OriginBindingId = "binding.test.origin_ward";
    private static readonly StringName BreathSaveTag = "dragon_breath";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestDamageOriginClosedDomain();
        TestHandlerSpecDeclaresPreviewAndAiSupport();
        TestContentValidationAcceptsAndRejects();
        TestQueryConditionsAndProvenance();
        TestResolverAggregation();
        TestTierFixedDrSaveOrdering();
        TestExecutePreviewParity();
        TestOriginClassificationFailsClosed();

        RequestTestExit(_test.Finish("Equipment conditional mitigation tier regression"));
    }

    private void TestDamageOriginClosedDomain()
    {
        (BattleDamageOriginKind Kind, string Id)[] expected =
        {
            (BattleDamageOriginKind.MainDirectEffect, "main_direct_effect"),
            (BattleDamageOriginKind.TimelineUpkeep, "timeline_upkeep"),
            (BattleDamageOriginKind.Terrain, "terrain"),
            (BattleDamageOriginKind.Reflection, "reflection"),
            (BattleDamageOriginKind.SelfDamage, "self_damage"),
            (BattleDamageOriginKind.EquipmentBonus, "equipment_bonus"),
            (BattleDamageOriginKind.EquipmentDirectReaction, "equipment_direct_reaction"),
            (BattleDamageOriginKind.EquipmentTriggeredSkill, "equipment_triggered_skill"),
        };
        foreach ((BattleDamageOriginKind kind, string id) in expected)
        {
            _test.Eq(
                BattleDamageOriginContentRules.ToStringName(kind),
                new StringName(id),
                $"{kind} should map to its stable origin id."
            );
            _test.Eq(
                BattleDamageOriginContentRules.ToDamageOriginKind(id),
                kind,
                $"{id} should map back to {kind}."
            );
            _test.True(
                BattleDamageOriginContentRules.IsValid(kind),
                $"{kind} should be a valid damage origin."
            );
        }
        foreach (StringName invalid in new StringName[] { "", "unknown_origin", "dragon_breath" })
        {
            _test.Eq(
                BattleDamageOriginContentRules.ToDamageOriginKind(invalid),
                BattleDamageOriginKind.Unknown,
                $"{invalid} should fail closed to Unknown."
            );
        }
        _test.False(
            BattleDamageOriginContentRules.IsValid(BattleDamageOriginKind.Unknown),
            "Unknown should never be a valid damage origin."
        );
        _test.Eq(
            BattleDamageOriginContentRules.ToStringName(BattleDamageOriginKind.Unknown),
            new StringName(""),
            "Unknown should project to the empty id so eq conditions fail closed."
        );
    }

    private void TestHandlerSpecDeclaresPreviewAndAiSupport()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> actionSpecs =
            registry.GetActionHandlerSpecsTyped();
        _test.True(
            actionSpecs.ContainsKey("grant_mitigation_tier"),
            "grant_mitigation_tier should be a registered action handler."
        );
        EquipmentAbilityHandlerSpec spec = actionSpecs["grant_mitigation_tier"];
        _test.Eq(
            spec.PayloadImportModelType,
            typeof(GrantMitigationTierActionPayloadImportModel),
            "grant_mitigation_tier should declare its authoring payload type."
        );
        _test.Eq(
            spec.PayloadDefinitionType,
            typeof(GrantMitigationTierActionPayloadDefinition),
            "grant_mitigation_tier should declare its immutable definition type."
        );
        _test.True(
            spec.SupportsConsumer(EquipmentAbilityConsumerKind.Execution),
            "grant_mitigation_tier should declare execution support."
        );
        _test.True(
            spec.SupportsConsumer(EquipmentAbilityConsumerKind.Preview),
            "grant_mitigation_tier should declare preview support."
        );
        _test.True(
            spec.SupportsConsumer(EquipmentAbilityConsumerKind.AiScoring),
            "grant_mitigation_tier should declare AI scoring support."
        );
    }

    private void TestContentValidationAcceptsAndRejects()
    {
        using var registry = new EquipmentAbilityContentRegistry();

        EquipmentAbilityRegistryBuildResult validResult = registry.Rebuild(
            new[] { BuildAuthoringPack("half", "holder", new[] { "fire", "freeze", "poison", "acid", "lightning" }) },
            BuildValidationContext()
        );
        _test.True(
            validResult.Success,
            $"a valid grant_mitigation_tier pack should build: {FormatErrors(validResult.Errors)}"
        );
        EquipmentAbilityBindingDefinition definition =
            registry.GetBindingDefinitionsTyped()["binding.test.mitigation_tier"];
        GrantMitigationTierActionPayloadDefinition payload =
            definition.Reactions[0].Actions[0].PayloadDefinition
                as GrantMitigationTierActionPayloadDefinition;
        _test.True(
            payload != null,
            "grant_mitigation_tier should project to its immutable payload definition."
        );
        _test.Eq(
            payload?.MitigationTier ?? "",
            new StringName("half"),
            "projected payload should preserve the authored tier."
        );
        _test.Eq(
            payload?.DamageTags?.Count ?? 0,
            5,
            "projected payload should preserve all authored damage tags."
        );

        EquipmentAbilityRegistryBuildResult normalTierResult = registry.Rebuild(
            new[] { BuildAuthoringPack("normal", "holder", new[] { "fire" }) },
            BuildValidationContext()
        );
        _test.False(normalTierResult.Success, "normal tier should be rejected.");
        AssertErrorContains(
            normalTierResult.Errors,
            "EQA_GRANT_MITIGATION_TIER_INVALID",
            "mitigation_tier"
        );

        EquipmentAbilityRegistryBuildResult bogusTierResult = registry.Rebuild(
            new[] { BuildAuthoringPack("bogus_tier", "holder", new[] { "fire" }) },
            BuildValidationContext()
        );
        _test.False(bogusTierResult.Success, "an unknown tier should be rejected.");
        AssertErrorContains(
            bogusTierResult.Errors,
            "EQA_GRANT_MITIGATION_TIER_INVALID",
            "mitigation_tier"
        );

        EquipmentAbilityRegistryBuildResult emptyTagsResult = registry.Rebuild(
            new[] { BuildAuthoringPack("half", "holder", Array.Empty<string>()) },
            BuildValidationContext()
        );
        _test.False(emptyTagsResult.Success, "empty damage_tags should be rejected.");
        AssertErrorContains(
            emptyTagsResult.Errors,
            "EQA_ACTION_REQUIRED_FIELD_MISSING",
            "grant_mitigation_tier"
        );

        EquipmentAbilityRegistryBuildResult unknownTagResult = registry.Rebuild(
            new[] { BuildAuthoringPack("half", "holder", new[] { "invented_tag" }) },
            BuildValidationContext()
        );
        _test.False(unknownTagResult.Success, "an unknown damage tag should be rejected.");
        AssertErrorContains(
            unknownTagResult.Errors,
            "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
            "invented_tag"
        );

        EquipmentAbilityRegistryBuildResult badSelectorResult = registry.Rebuild(
            new[] { BuildAuthoringPack("half", "attacker", new[] { "fire" }) },
            BuildValidationContext()
        );
        _test.False(badSelectorResult.Success, "an unsupported selector should be rejected.");
        AssertErrorContains(
            badSelectorResult.Errors,
            "EQA_GRANT_MITIGATION_TIER_TARGET_SELECTOR_UNSUPPORTED",
            "target_selector"
        );
    }

    private void TestQueryConditionsAndProvenance()
    {
        using TierFixture fixture = TierFixture.Create();
        fixture.Attach(fixture.Holder, "holder", HalfBindingId);

        IReadOnlyList<BattleEquipmentAbilityMitigationTierResult> dragonBreath =
            CollectTiers(fixture, fixture.DragonAttacker, "fire", BreathSaveTag);
        _test.Eq(dragonBreath.Count, 1, "dragon breath fire should grant one conditional tier.");
        _test.Eq(
            dragonBreath[0].MitigationTier,
            new StringName("half"),
            "the conditional tier should be half."
        );
        _test.Eq(
            dragonBreath[0].BindingId,
            HalfBindingId,
            "the query result should keep binding provenance."
        );
        _test.Eq(
            dragonBreath[0].ActionId,
            new StringName("action.dragon_ward_half"),
            "the query result should keep action provenance."
        );
        _test.Eq(
            dragonBreath[0].Label,
            "Test dragon ward",
            "the query result should keep the authored label."
        );

        _test.Eq(
            CollectTiers(fixture, fixture.PlainAttacker, "fire", BreathSaveTag).Count,
            0,
            "a non-dragon attacker should not satisfy the creature_type_tags condition."
        );
        _test.Eq(
            CollectTiers(fixture, fixture.DragonAttacker, "fire", "magic").Count,
            0,
            "a non-breath save tag should not satisfy the save_tag condition."
        );
        _test.Eq(
            CollectTiers(fixture, fixture.DragonAttacker, "force", BreathSaveTag).Count,
            0,
            "a damage tag outside the authored list should not grant the tier."
        );
        _test.Eq(
            CollectTiers(fixture, fixture.DragonAttacker, "freeze", BreathSaveTag).Count,
            1,
            "freeze should be covered by the authored damage tag list."
        );
    }

    private void TestResolverAggregation()
    {
        using TierFixture fixture = TierFixture.Create();
        using var resolver = new BattleDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );

        fixture.Attach(fixture.Holder, "holder", HalfBindingId);
        AttackEffectResolutionResult dragonResult = ResolveDamage(
            resolver,
            fixture,
            fixture.DragonAttacker,
            "fire",
            BreathSaveTag
        );
        _test.Eq(dragonResult.Damage, 6, "dragon breath half tier should halve 12 to 6.");
        _test.Eq(
            dragonResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Half,
            "the damage event should report the half tier."
        );
        _test.Eq(
            CountEquipmentTierSources(dragonResult.DamageEvents[0].MitigationSources),
            1,
            "the report should keep one conditional tier source."
        );
        _test.True(
            HasEquipmentTierSource(
                dragonResult.DamageEvents[0].MitigationSources,
                "Test dragon ward"
            ),
            "the report should keep the conditional tier label provenance."
        );

        fixture.ResetHolder();
        AttackEffectResolutionResult plainResult = ResolveDamage(
            resolver,
            fixture,
            fixture.PlainAttacker,
            "fire",
            BreathSaveTag
        );
        _test.Eq(plainResult.Damage, 12, "a non-dragon attacker should deal full damage.");
        _test.Eq(
            plainResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Normal,
            "a non-dragon attacker should keep the normal tier."
        );

        fixture.ResetHolder();
        AttackEffectResolutionResult nonBreathResult = ResolveDamage(
            resolver,
            fixture,
            fixture.DragonAttacker,
            "fire",
            "magic"
        );
        _test.Eq(nonBreathResult.Damage, 12, "a non-breath save tag should deal full damage.");

        fixture.ResetHolder();
        AttackEffectResolutionResult otherTagResult = ResolveDamage(
            resolver,
            fixture,
            fixture.DragonAttacker,
            "force",
            BreathSaveTag
        );
        _test.Eq(otherTagResult.Damage, 12, "an unlisted damage tag should deal full damage.");

        fixture.ResetHolder();
        fixture.Attach(fixture.Holder, "holder", HalfBindingId, HalfUnlabeledBindingId);
        AttackEffectResolutionResult doubleHalfResult = ResolveDamage(
            resolver,
            fixture,
            fixture.DragonAttacker,
            "fire",
            BreathSaveTag
        );
        _test.Eq(
            doubleHalfResult.Damage,
            6,
            "two conditional half sources should halve once, not quarter."
        );
        _test.Eq(
            CountEquipmentTierSources(doubleHalfResult.DamageEvents[0].MitigationSources),
            2,
            "both half sources should keep report provenance."
        );
        _test.True(
            HasEquipmentTierSource(
                doubleHalfResult.DamageEvents[0].MitigationSources,
                "binding.test.dragon_ward_unlabeled/action.dragon_ward_half_b"
            ),
            "an unlabeled source should fall back to binding/action provenance."
        );

        fixture.ResetHolder();
        fixture.Attach(fixture.Holder, "holder", HalfBindingId, DoubleBindingId);
        AttackEffectResolutionResult cancelledResult = ResolveDamage(
            resolver,
            fixture,
            fixture.DragonAttacker,
            "fire",
            BreathSaveTag
        );
        _test.Eq(
            cancelledResult.Damage,
            12,
            "half and double should cancel back to normal damage."
        );
        _test.Eq(
            cancelledResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Normal,
            "half and double should cancel to the normal tier."
        );
        _test.Eq(
            CountEquipmentTierSources(cancelledResult.DamageEvents[0].MitigationSources),
            2,
            "cancelled half/double sources should remain in the report."
        );

        fixture.ResetHolder();
        fixture.Attach(fixture.Holder, "holder", HalfBindingId, ImmuneBindingId);
        AttackEffectResolutionResult immuneResult = ResolveDamage(
            resolver,
            fixture,
            fixture.DragonAttacker,
            "fire",
            BreathSaveTag
        );
        _test.Eq(immuneResult.Damage, 0, "immune should win over half and zero the damage.");
        _test.Eq(
            immuneResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Immune,
            "immune should take priority over half."
        );
    }

    private void TestTierFixedDrSaveOrdering()
    {
        using TierFixture fixture = TierFixture.Create();
        using var resolver = new BattleDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Holder, "holder", HalfBindingId, FixedDrBindingId);

        AttackEffectResolutionResult failedSave = ResolveSaveDamage(
            resolver,
            fixture,
            saveRoll: 1
        );
        DamageEventResult failedEvent = failedSave.DamageEvents[0];
        _test.Eq(failedEvent.RolledDamage, 12, "baseline breath roll should be 12.");
        _test.Eq(failedEvent.TierAdjustedDamage, 6, "tier should halve before fixed DR.");
        _test.Eq(failedEvent.FixedMitigationTotal, 2, "fixed DR should apply after the tier.");
        _test.Eq(failedEvent.ResolvedDamage, 4, "failed save should keep 12/2-2=4.");
        _test.False(failedEvent.SaveSuccess, "a natural 1 should fail the save.");
        _test.Eq(failedSave.Damage, 4, "failed save committed damage should be 4.");

        fixture.ResetHolder();
        AttackEffectResolutionResult successfulSave = ResolveSaveDamage(
            resolver,
            fixture,
            saveRoll: 20
        );
        DamageEventResult successEvent = successfulSave.DamageEvents[0];
        _test.True(successEvent.SaveSuccess, "a natural 20 should succeed the save.");
        _test.Eq(successEvent.PreSaveDamage, 4, "pre-save damage should be 4.");
        _test.Eq(
            successEvent.SaveAdjustedDamage,
            2,
            "a successful save should halve the post-DR 4 to 2."
        );
        _test.Eq(successfulSave.Damage, 2, "successful save committed damage should be 2.");

        fixture.ResetHolder();
        fixture.Attach(fixture.Holder, "holder", FixedDrBindingId);
        AttackEffectResolutionResult controlFailed = ResolveSaveDamage(
            resolver,
            fixture,
            saveRoll: 1
        );
        _test.Eq(
            controlFailed.Damage,
            10,
            "without the conditional tier fixed DR alone should give 12-2=10."
        );

        fixture.ResetHolder();
        fixture.Attach(fixture.Holder, "holder", FixedDrBindingId);
        AttackEffectResolutionResult controlSuccess = ResolveSaveDamage(
            resolver,
            fixture,
            saveRoll: 20
        );
        _test.Eq(
            controlSuccess.Damage,
            5,
            "without the conditional tier a successful save should give 10/2=5."
        );
    }

    private void TestExecutePreviewParity()
    {
        using TierFixture fixture = TierFixture.Create();
        using var resolver = new BattleDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Holder, "holder", HalfBindingId);
        CombatEffectDefinition effect = BuildBreathEffect("fire", BreathSaveTag);
        DamageResolutionContext context = BuildDamageContext(fixture, fixture.DragonAttacker);

        AttackEffectResolutionResult executeResult = resolver.ResolveEffects(
            fixture.DragonAttacker,
            fixture.Holder,
            new[] { effect },
            context
        );
        BattleDamagePreviewResult preview = resolver.PreviewDamageEffectTyped(
            fixture.DragonAttacker,
            fixture.Holder,
            effect,
            context,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(executeResult.Damage, 6, "execute should halve the dragon breath to 6.");
        _test.Eq(
            preview.Damage,
            executeResult.Damage,
            "preview should match the execute damage for a conditional tier hit."
        );
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            100 - executeResult.Damage,
            "preview must not mutate live hp beyond the execute result."
        );

        fixture.ResetHolder();
        AttackEffectResolutionResult plainExecute = resolver.ResolveEffects(
            fixture.PlainAttacker,
            fixture.Holder,
            new[] { effect },
            context
        );
        BattleDamagePreviewResult plainPreview = resolver.PreviewDamageEffectTyped(
            fixture.PlainAttacker,
            fixture.Holder,
            effect,
            context,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(plainExecute.Damage, 12, "execute should keep full damage for a non-dragon.");
        _test.Eq(
            plainPreview.Damage,
            plainExecute.Damage,
            "preview should match execute when the condition fails."
        );
    }

    private void TestOriginClassificationFailsClosed()
    {
        using TierFixture fixture = TierFixture.Create();
        using var resolver = new BattleDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Holder, "holder", OriginBindingId);

        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "fire",
            power: 12
        );
        AttackEffectResolutionResult mainSkillResult = resolver.ResolveEffects(
            fixture.PlainAttacker,
            fixture.Holder,
            new[] { effect },
            BuildDamageContext(fixture, fixture.PlainAttacker)
        );
        _test.Eq(
            mainSkillResult.Damage,
            6,
            "a main direct effect should satisfy the damage_origin_kind condition."
        );

        fixture.ResetHolder();
        int taggedDamage = resolver.ApplyTaggedDirectDamageToTargetTyped(
            fixture.Holder,
            12,
            "fire",
            fixture.PlainAttacker,
            fixture.State
        );
        _test.Eq(
            taggedDamage,
            12,
            "tagged direct damage without a classified origin should fail closed."
        );
    }

    private static IReadOnlyList<BattleEquipmentAbilityMitigationTierResult> CollectTiers(
        TierFixture fixture,
        BattleUnitState attacker,
        StringName damageTag,
        StringName saveTag
    ) =>
        fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery.CollectMitigationTiers(
            new BattleEquipmentAbilityMitigationTierContext
            {
                SourceUnit = attacker,
                TargetUnit = fixture.Holder,
                BattleState = fixture.State,
                SkillId = "test_dragon_breath",
                SaveTag = saveTag,
                DamageTag = damageTag,
                DamageOriginKind = BattleDamageOriginKind.MainDirectEffect,
            }
        );

    private static AttackEffectResolutionResult ResolveDamage(
        BattleDamageResolver resolver,
        TierFixture fixture,
        BattleUnitState attacker,
        StringName damageTag,
        StringName saveTag
    )
    {
        return resolver.ResolveEffects(
            attacker,
            fixture.Holder,
            new[] { BuildBreathEffect(damageTag, saveTag) },
            BuildDamageContext(fixture, attacker)
        );
    }

    private static AttackEffectResolutionResult ResolveSaveDamage(
        BattleDamageResolver resolver,
        TierFixture fixture,
        int saveRoll
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "fire",
            power: 12,
            saveDc: 10,
            saveAbility: "agility",
            saveTag: BreathSaveTag,
            savePartialOnSuccess: true
        );
        return resolver.ResolveEffects(
            fixture.DragonAttacker,
            fixture.Holder,
            new[] { effect },
            DamageResolutionContext
                .Create(
                    criticalHit: false,
                    attackSuccess: true,
                    secondaryHitSuccess: false,
                    skillId: "test_dragon_breath",
                    saveRollOverrides: new[] { saveRoll }
                )
                .WithBattleState(fixture.State)
        );
    }

    private static CombatEffectDefinition BuildBreathEffect(
        StringName damageTag,
        StringName saveTag
    ) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: damageTag,
            power: 12,
            saveTag: saveTag
        );

    private static DamageResolutionContext BuildDamageContext(
        TierFixture fixture,
        BattleUnitState attacker
    ) =>
        DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: "test_dragon_breath"
            )
            .WithBattleState(fixture.State)
            .WithDamageOriginKind(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    attacker,
                    fixture.Holder
                )
            );

    private static int CountEquipmentTierSources(MitigationSourceResult[] sources)
    {
        int count = 0;
        foreach (MitigationSourceResult source in sources ?? Array.Empty<MitigationSourceResult>())
        {
            if (source.Type == "equipment_ability_mitigation_tier")
                count++;
        }
        return count;
    }

    private static bool HasEquipmentTierSource(MitigationSourceResult[] sources, string sourceId)
    {
        foreach (MitigationSourceResult source in sources ?? Array.Empty<MitigationSourceResult>())
        {
            if (source.Type == "equipment_ability_mitigation_tier" && source.StatusId == sourceId)
                return true;
        }
        return false;
    }

    private static EquipmentAbilityContentPackImportModel BuildAuthoringPack(
        StringName mitigationTier,
        StringName targetSelector,
        IReadOnlyList<string> damageTags
    )
    {
        GrantMitigationTierActionPayloadImportModel payload = new()
        {
            target_selector = targetSelector.ToString(),
            mitigation_tier = mitigationTier.ToString(),
            label = "Test dragon ward",
            damage_tags = damageTags ?? Array.Empty<string>(),
        };
        return new EquipmentAbilityContentPackImportModel
        {
            pack_id = "pack.test.mitigation_tier",
            schema_version = 1,
            load_order = 10,
            bindings = new[]
            {
                new EquipmentAbilityBindingImportModel
                {
                    binding_id = "binding.test.mitigation_tier",
                    trait_id = "trait.weapon.flame",
                    override_mode = "add",
                    allowed_source_kinds = new[] { "equipment_fixed" },
                    reactions = new[]
                    {
                        new EquipmentAbilityReactionImportModel
                        {
                            reaction_id = "reaction.conditional_tier",
                            trigger = "on_damage_roll",
                            timing = "before_damage",
                            condition_group = new EquipmentAbilityConditionGroupImportModel
                            {
                                mode = "all",
                                conditions = new[]
                                {
                                    new EquipmentAbilityConditionImportModel
                                    {
                                        condition_id = "condition.attacker_dragon",
                                        kind = "compare_fact",
                                        payload = new CompareFactConditionPayloadImportModel
                                        {
                                            left = new EquipmentAbilityFactQueryImportModel
                                            {
                                                query_kind = "fact",
                                                fact_id = "creature_type_tags",
                                                subject = "target",
                                            },
                                            compare = "contains",
                                            right = new EquipmentAbilityFactQueryImportModel
                                            {
                                                query_kind = "literal",
                                                string_name_literal = "dragon",
                                            },
                                        },
                                    },
                                    new EquipmentAbilityConditionImportModel
                                    {
                                        condition_id = "condition.breath_save_tag",
                                        kind = "compare_fact",
                                        payload = new CompareFactConditionPayloadImportModel
                                        {
                                            left = new EquipmentAbilityFactQueryImportModel
                                            {
                                                query_kind = "fact",
                                                fact_id = "save_tag",
                                            },
                                            compare = "eq",
                                            right = new EquipmentAbilityFactQueryImportModel
                                            {
                                                query_kind = "literal",
                                                string_name_literal = BreathSaveTag.ToString(),
                                            },
                                        },
                                    },
                                },
                            },
                            actions = new[]
                            {
                                new EquipmentAbilityActionImportModel
                                {
                                    action_id = "action.grant_tier",
                                    kind = "grant_mitigation_tier",
                                    payload = payload,
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext()
    {
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName> { "trait.weapon.flame" },
            KnownSkillIds = new HashSet<StringName> { "known_skill" },
            KnownStatusIds = new HashSet<StringName> { "burning" },
        };
    }

    private void AssertErrorContains(
        IReadOnlyList<string> errors,
        string code,
        string pathFragment
    )
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(code) && (error ?? "").Contains(pathFragment))
                return;
        }
        _test.Fail(
            $"Expected error containing code={code} path={pathFragment}. errors={FormatErrors(errors)}"
        );
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors ?? Array.Empty<string>())
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private sealed class TierFixture : IDisposable
    {
        private TierFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState holder,
            BattleUnitState dragonAttacker,
            BattleUnitState plainAttacker
        )
        {
            Runtime = runtime;
            State = state;
            Holder = holder;
            DragonAttacker = dragonAttacker;
            PlainAttacker = plainAttacker;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Holder { get; }
        internal BattleUnitState DragonAttacker { get; }
        internal BattleUnitState PlainAttacker { get; }

        internal static TierFixture Create()
        {
            var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [HalfBindingId] = BuildDragonWardBinding(
                    HalfBindingId,
                    "action.dragon_ward_half",
                    "half",
                    "Test dragon ward"
                ),
                [HalfUnlabeledBindingId] = BuildDragonWardBinding(
                    HalfUnlabeledBindingId,
                    "action.dragon_ward_half_b",
                    "half",
                    ""
                ),
                [DoubleBindingId] = BuildDragonWardBinding(
                    DoubleBindingId,
                    "action.dragon_ward_double",
                    "double",
                    "Test dragon bane"
                ),
                [ImmuneBindingId] = BuildDragonWardBinding(
                    ImmuneBindingId,
                    "action.dragon_ward_immune",
                    "immune",
                    "Test dragon immunity"
                ),
                [FixedDrBindingId] = BuildFixedDrBinding(),
                [OriginBindingId] = BuildOriginBinding(),
            };
            var runtime = new BattleRuntimeModule();
            runtime.setup(equipment_ability_bindings: bindings);

            BattleUnitState holder = Unit("tier_holder", "heroes");
            BattleUnitState dragonAttacker = Unit("tier_dragon_attacker", "enemies");
            dragonAttacker.ReplaceCreatureTypeTagsTyped(new StringName[] { "dragon" });
            BattleUnitState plainAttacker = Unit("tier_plain_attacker", "enemies");

            var state = new BattleState { battle_id = "conditional_mitigation_tier_test" };
            state.SetUnit(holder);
            state.SetUnit(dragonAttacker);
            state.SetUnit(plainAttacker);
            return new TierFixture(runtime, state, holder, dragonAttacker, plainAttacker);
        }

        internal void Attach(
            BattleUnitState unit,
            StringName suffix,
            params StringName[] bindingIds
        )
        {
            StringName instanceId = $"eq_tier_{suffix}";
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = "item.test.conditional_tier",
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName>(bindingIds),
                    },
                },
                temporalProgressModifiers: null
            );
        }

        internal void ResetHolder()
        {
            Holder.SetCurrentHp(100);
        }

        private static BattleUnitState Unit(StringName unitId, StringName factionId)
        {
            BattleUnitState unit = new BattleUnitState
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
            }.WithCombatResourcesForTest(hp: 100, isAlive: true);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
            unit.attribute_snapshot.SetValue("agility", 10);
            return unit;
        }

        private static EquipmentAbilityBindingDefinition BuildDragonWardBinding(
            StringName bindingId,
            StringName actionId,
            StringName tier,
            string label
        )
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = bindingId,
                TraitId = "trait.test.conditional_tier",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.conditional_tier",
                        Trigger = EquipmentAbilityTriggerKind.OnDamageRoll,
                        Timing = EquipmentAbilityTimingKind.BeforeDamage,
                        ConditionGroup = BuildDragonBreathConditionGroup(),
                        Actions = new[]
                        {
                            new EquipmentAbilityActionDefinition
                            {
                                ActionId = actionId,
                                Kind = BattleEquipmentAbilityRuntimeService.ActionKindGrantMitigationTier,
                                PayloadDefinition = new GrantMitigationTierActionPayloadDefinition
                                {
                                    TargetSelector = "holder",
                                    MitigationTier = tier,
                                    DamageTags = new StringName[]
                                    {
                                        "fire",
                                        "freeze",
                                        "poison",
                                        "acid",
                                        "lightning",
                                    },
                                    Label = label,
                                },
                            },
                        },
                    },
                },
            };
        }

        private static EquipmentConditionGroupDefinition BuildDragonBreathConditionGroup()
        {
            return new EquipmentConditionGroupDefinition
            {
                Mode = "all",
                Conditions = new[]
                {
                    new EquipmentAbilityConditionDefinition
                    {
                        ConditionId = "condition.attacker_dragon",
                        Kind = "compare_fact",
                        PayloadDefinition = new CompareFactConditionPayloadDefinition
                        {
                            Left = new EquipmentAbilityFactQueryDefinition
                            {
                                QueryKind = "fact",
                                FactId = "creature_type_tags",
                                Subject = "target",
                            },
                            Compare = "contains",
                            Right = new EquipmentAbilityFactQueryDefinition
                            {
                                QueryKind = "literal",
                                StringNameLiteral = "dragon",
                            },
                        },
                    },
                    new EquipmentAbilityConditionDefinition
                    {
                        ConditionId = "condition.breath_save_tag",
                        Kind = "compare_fact",
                        PayloadDefinition = new CompareFactConditionPayloadDefinition
                        {
                            Left = new EquipmentAbilityFactQueryDefinition
                            {
                                QueryKind = "fact",
                                FactId = "save_tag",
                            },
                            Compare = "eq",
                            Right = new EquipmentAbilityFactQueryDefinition
                            {
                                QueryKind = "literal",
                                StringNameLiteral = BreathSaveTag,
                            },
                        },
                    },
                },
            };
        }

        private static EquipmentAbilityBindingDefinition BuildFixedDrBinding()
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = FixedDrBindingId,
                TraitId = "trait.test.conditional_tier",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.fire_dr",
                        Trigger = EquipmentAbilityTriggerKind.OnDamageRoll,
                        Timing = EquipmentAbilityTimingKind.BeforeDamage,
                        Actions = new[]
                        {
                            new EquipmentAbilityActionDefinition
                            {
                                ActionId = "action.fire_dr",
                                Kind = "damage_reduction",
                                PayloadDefinition = new DamageReductionActionPayloadDefinition
                                {
                                    TargetSelector = "holder",
                                    Amount = 2,
                                    DamageTags = new StringName[] { "fire" },
                                    Label = "Test fire DR",
                                },
                            },
                        },
                    },
                },
            };
        }

        private static EquipmentAbilityBindingDefinition BuildOriginBinding()
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = OriginBindingId,
                TraitId = "trait.test.conditional_tier",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.origin_tier",
                        Trigger = EquipmentAbilityTriggerKind.OnDamageRoll,
                        Timing = EquipmentAbilityTimingKind.BeforeDamage,
                        ConditionGroup = new EquipmentConditionGroupDefinition
                        {
                            Mode = "all",
                            Conditions = new[]
                            {
                                new EquipmentAbilityConditionDefinition
                                {
                                    ConditionId = "condition.main_direct_origin",
                                    Kind = "compare_fact",
                                    PayloadDefinition = new CompareFactConditionPayloadDefinition
                                    {
                                        Left = new EquipmentAbilityFactQueryDefinition
                                        {
                                            QueryKind = "fact",
                                            FactId = "damage_origin_kind",
                                        },
                                        Compare = "eq",
                                        Right = new EquipmentAbilityFactQueryDefinition
                                        {
                                            QueryKind = "literal",
                                            StringNameLiteral = "main_direct_effect",
                                        },
                                    },
                                },
                            },
                        },
                        Actions = new[]
                        {
                            new EquipmentAbilityActionDefinition
                            {
                                ActionId = "action.origin_half",
                                Kind = BattleEquipmentAbilityRuntimeService.ActionKindGrantMitigationTier,
                                PayloadDefinition = new GrantMitigationTierActionPayloadDefinition
                                {
                                    TargetSelector = "holder",
                                    MitigationTier = "half",
                                    DamageTags = new StringName[] { "fire" },
                                    Label = "Test origin ward",
                                },
                            },
                        },
                    },
                },
            };
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
