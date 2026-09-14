using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_definition_plain_value_graph_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestAllowedGodotValuesBecomePlainFrozenValues();
            TestMathAllowlistProjectionRoundTrip();
            TestAllDefinitionGraphsDefensivelyDeepFreezeSyntheticInput();
            TestTypedSkillResourceFieldsProjectToFrozenPlainGraph();
            TestMalformedGodotValuesReportFullSkillPaths();
            TestStrictDictionaryAndPackedValueRejection();
            TestSyntheticIllegalObjectAndCycleRejection();
            TestResourceDefaultsAreEffectiveWithoutWritingBack();
            TestDiagnosticFixtureLevelOverrideProjectionRules();
            TestDiagnosticFixtureDescriptionVariablesRequireStrings();
            TestFormalFlawReadOverrideMigrationPreservesEffectiveBehavior();
            TestFingerprintAndLevelDescriptionRemainStable();
        }
        catch (Exception exception)
        {
            _test.Fail($"Skill definition plain value graph regression crashed: {exception}");
        }
        finally
        {
            RequestTestExit(_test.Finish("Skill definition plain value graph regression"));
        }
    }

    private void TestMathAllowlistProjectionRoundTrip()
    {
        var mathValues = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["vector2"] = new Vector2(1.25f, -2.5f),
            ["vector2i"] = new Vector2I(2, -3),
            ["rect2"] = new Rect2(new Vector2(1.0f, 2.0f), new Vector2(3.0f, 4.0f)),
            ["rect2i"] = new Rect2I(new Vector2I(5, 6), new Vector2I(7, 8)),
            ["vector3"] = new Vector3(1.0f, 2.0f, 3.0f),
            ["vector3i"] = new Vector3I(4, 5, 6),
            ["transform2d"] = Transform2D.Identity,
            ["vector4"] = new Vector4(1.0f, 2.0f, 3.0f, 4.0f),
            ["vector4i"] = new Vector4I(5, 6, 7, 8),
            ["plane"] = new Plane(new Vector3(0.0f, 1.0f, 0.0f), 2.5f),
            ["quaternion"] = Quaternion.Identity,
            ["aabb"] = new Aabb(new Vector3(1.0f, 2.0f, 3.0f), new Vector3(4.0f, 5.0f, 6.0f)),
            ["basis"] = Basis.Identity,
            ["transform3d"] = Transform3D.Identity,
            ["projection"] = Projection.Identity,
            ["color"] = new Color(0.1f, 0.2f, 0.3f, 0.4f),
        };
        IReadOnlyDictionary<string, object> normalized =
            ContentValueNormalizer.NormalizeDictionary(mathValues, "skill.math.params");
        Dictionary<string, object> cloned = RuntimePlainPayload.CloneDictionary(normalized);
        AssertMathValuesEqual(mathValues, cloned, "plain clone");
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        using (
            GodotProjectionLease<GDictionary> lease = RuntimePlainPayload.ProjectDictionaryLease(
                normalized,
                "skill-math-round-trip",
                LifetimeDomain.Request,
                "skill.math.params"
            )
        )
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "Math projection should register one request lease."
            );
            _test.Eq(
                active.ActiveOwnerCount,
                baseline.ActiveOwnerCount + 1,
                "Math-only projection should own only its root dictionary."
            );

            Dictionary<string, object> nonStrict = RuntimePlainPayload.NormalizeDictionary(
                lease.Value,
                "skill.math.non_strict"
            );
            Dictionary<string, object> strict = RuntimePlainPayload.NormalizeDictionaryStrict(
                lease.Value,
                "skill.math.strict"
            );
            Dictionary<string, object> restored = RuntimePlainPayload.RestoreSaveDictionary(
                lease.Value,
                "skill.math.restore"
            );
            IReadOnlyDictionary<string, object> renormalized =
                ContentValueNormalizer.NormalizeDictionary(restored, "skill.math.renormalized");

            AssertMathValuesEqual(mathValues, nonStrict, "non-strict normalize");
            AssertMathValuesEqual(mathValues, strict, "strict normalize");
            AssertMathValuesEqual(mathValues, restored, "save restore");
            AssertMathValuesEqual(mathValues, renormalized, "content renormalize");
        }

        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            after.ActiveOwnerCount,
            baseline.ActiveOwnerCount,
            "Math projection owners should return to baseline."
        );
        _test.Eq(
            after.ActiveLeaseCount,
            baseline.ActiveLeaseCount,
            "Math projection lease should return to baseline."
        );
        _test.Eq(
            after.ActiveScopeCount,
            baseline.ActiveScopeCount,
            "Math projection scopes should return to baseline."
        );
        _test.Eq(
            after.ActiveContentBorrowerCount,
            baseline.ActiveContentBorrowerCount,
            "Math projection borrowers should return to baseline."
        );
    }

    private void TestAllowedGodotValuesBecomePlainFrozenValues()
    {
        GDictionary root = new();
        GDictionary nested = new();
        GArray list = new();
        try
        {
            nested["name"] = new StringName("nested_name");
            list.Add(9);
            list.Add(nested);
            root["nil"] = default(Variant);
            root["bool"] = true;
            root["int"] = 7;
            root["float"] = 1.5;
            root["string"] = "text";
            root["string_name"] = new StringName("content_id");
            root["vector2i"] = new Vector2I(2, 3);
            root["vector3"] = new Vector3(1.0f, 2.0f, 3.0f);
            root["color"] = new Color(0.1f, 0.2f, 0.3f, 1.0f);
            root["list"] = list;

            IReadOnlyDictionary<string, object> normalized =
                ContentValueNormalizer.NormalizeDictionary(root, "skill.allowed.params");

            _test.True(normalized["nil"] == null, "Nil should normalize to null.");
            _test.Eq(normalized["bool"], true, "Bool should stay a plain bool.");
            _test.Eq(normalized["int"], 7L, "Integral Variant should normalize to Int64.");
            _test.Eq(normalized["float"], 1.5d, "Float Variant should normalize to Double.");
            _test.Eq(normalized["string"], "text", "String should stay a string.");
            _test.Eq(
                normalized["string_name"],
                new StringName("content_id"),
                "StringName should remain the approved Godot value type."
            );
            _test.Eq(
                normalized["vector2i"],
                new Vector2I(2, 3),
                "Approved math values should remain value types."
            );
            _test.False(
                ContainsGodotContainerOrVariant(normalized),
                "Normalized content graphs must not retain Variant or Godot collection wrappers."
            );
        }
        finally
        {
            root.Clear();
            list.Clear();
            nested.Clear();
            nested.Dispose();
            list.Dispose();
            root.Dispose();
        }
    }

    private void TestAllDefinitionGraphsDefensivelyDeepFreezeSyntheticInput()
    {
        var nestedList = new List<object>
        {
            3,
            new Dictionary<string, object> { ["inner"] = "original" },
        };
        var valueSource = new Dictionary<string, object>
        {
            ["number"] = 4,
            ["nested"] = nestedList,
        };
        var descriptionSource = new Dictionary<string, string>
        {
            ["number"] = "4",
            ["text"] = "original",
        };
        var descriptionLevels = new Dictionary<int, SkillDescriptionVariables>
        {
            [1] = new SkillDescriptionVariables(descriptionSource),
        };
        var levelOverrides = new Dictionary<int, CombatSkillLevelOverrideImportModel>
        {
            [1] = new CombatSkillLevelOverrideImportModel(apCost: 4),
        };

        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "plain_graph_skill",
            levelDescriptionConfigs: descriptionLevels
        );
        ContingencyAutomationDefinition contingency =
            TestSkillDefinitionProjection.BuildContingencyAutomation(
                allowedParameterBindings: valueSource
            );
        CombatSkillDefinition combat = TestSkillDefinitionProjection.BuildCombatProfile(
            "plain_graph_skill",
            levelOverrides: levelOverrides
        );
        var payloadSlots = new List<StringName> { "body" };
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "equipment_durability_damage",
            payload: new EquipmentDurabilityDamageEffectPayloadDefinition(
                1,
                payloadSlots
            )
        );
        var variantEffects = new List<CombatEffectDefinition> { effect };
        CombatCastVariantDefinition castVariant = TestSkillDefinitionProjection.BuildCastVariant(
            "plain_graph_variant",
            0,
            variantEffects,
            square2Corner: CombatCastSquare2CornerKind.BottomRight
        );

        valueSource["number"] = 99;
        nestedList[0] = 88;
        ((Dictionary<string, object>)nestedList[1])["inner"] = "changed";
        descriptionSource["number"] = "99";
        descriptionLevels[1] = new SkillDescriptionVariables();
        levelOverrides[1] = new CombatSkillLevelOverrideImportModel(apCost: 99);
        payloadSlots[0] = "head";
        variantEffects.Clear();

        _test.Eq(
            skill.LevelDescriptionConfigs[1]["number"],
            "4",
            "SkillDefinition should defensively copy typed description variables."
        );
        AssertFrozenGraph(contingency.AllowedParameterBindings, "ContingencyAutomationDefinition");
        _test.Eq(
            combat.LevelOverrides[1].ApCost ?? -1,
            4,
            "CombatSkillDefinition should freeze the typed level override map."
        );
        _test.Eq(castVariant.Square2Corner, CombatCastSquare2CornerKind.BottomRight,
            "Cast variant should retain its typed corner.");
        _test.Eq(castVariant.EffectDefinitions.Count, 1,
            "Cast variant should defensively copy its effects.");
        _test.True(ReferenceEquals(castVariant.EffectDefinitions[0], effect),
            "Cast variant should retain the immutable effect definition.");
        EquipmentDurabilityDamageEffectPayloadDefinition effectPayload =
            effect.Payload as EquipmentDurabilityDamageEffectPayloadDefinition;
        _test.True(effectPayload != null, "CombatEffectDefinition should retain its typed payload.");
        _test.Eq(
            effectPayload?.TargetSlots[0] ?? new StringName(""),
            new StringName("body"),
            "CombatEffectDefinition payload should defensively copy target slots."
        );

        bool mapMutationRejected = false;
        try
        {
            ((IDictionary<string, object>)contingency.AllowedParameterBindings)["new"] = 1L;
        }
        catch (NotSupportedException)
        {
            mapMutationRejected = true;
        }
        _test.True(mapMutationRejected, "Top-level normalized maps must reject mutation.");

        bool listMutationRejected = false;
        try
        {
            ((IList<object>)contingency.AllowedParameterBindings["nested"])[0] = 12L;
        }
        catch (NotSupportedException)
        {
            listMutationRejected = true;
        }
        _test.True(listMutationRejected, "Nested normalized lists must reject mutation.");

        bool variantMutationRejected = false;
        try
        {
            ((IList<CombatEffectDefinition>)castVariant.EffectDefinitions).Clear();
        }
        catch (NotSupportedException)
        {
            variantMutationRejected = true;
        }
        _test.True(variantMutationRejected, "Cast variant effect lists must reject mutation.");

        bool payloadMutationRejected = false;
        try
        {
            ((IList<StringName>)effectPayload.TargetSlots)[0] = "head";
        }
        catch (NotSupportedException)
        {
            payloadMutationRejected = true;
        }
        _test.True(payloadMutationRejected, "Typed payload lists must reject mutation.");
    }

    private void TestTypedSkillResourceFieldsProjectToFrozenPlainGraph()
    {
        using var scope = new NativeLeaseScope(
            "skill-typed-resource-projection",
            LifetimeDomain.Request
        );
        GArray chainStatusIdsOwner = scope.Own(
            new GArray { new StringName("shocked") },
            "skill-typed-resource-chain-status-ids"
        );
        var chainStatusIds = new Godot.Collections.Array<StringName>(chainStatusIdsOwner);
        GArray chainTerrainIdsOwner = scope.Own(
            new GArray { new StringName("wet") },
            "skill-typed-resource-chain-terrain-ids"
        );
        var chainTerrainIds = new Godot.Collections.Array<StringName>(chainTerrainIdsOwner);
        GArray successorSaveTagsOwner = scope.Own(
            new GArray { new StringName("sleep") },
            "skill-typed-resource-successor-save-tags"
        );
        var successorSaveTags = new Godot.Collections.Array<StringName>(
            successorSaveTagsOwner
        );
        GArray excludedCreatureTagsOwner = scope.Own(
            new GArray { new StringName("undead"), new StringName("construct") },
            "skill-typed-resource-excluded-creature-tags"
        );
        var excludedCreatureTags = new Godot.Collections.Array<StringName>(
            excludedCreatureTagsOwner
        );

        CombatEffectDef chainResource = scope.Own(
            new CombatEffectDef
            {
                effect_type = "chain_damage",
                chain_base_hop_range = 2,
                chain_conductive_hop_range = 4,
                chain_max_total_targets = 5,
                chain_conductive_status_ids = chainStatusIds,
                chain_conductive_terrain_effect_ids = chainTerrainIds,
                chain_backlash_hop_range_bonus = 1,
            },
            "skill-typed-resource-chain-effect"
        );
        CombatEffectDef saveResource = scope.Own(
            new CombatEffectDef
            {
                effect_type = "damage",
                save_dc_mode = "caster_spell",
                save_dc_bonus = 3,
            },
            "skill-typed-resource-save-effect"
        );
        CombatEffectDef statusResource = scope.Own(
            new CombatEffectDef
            {
                effect_type = "status",
                status_id = "sleeping",
                skip_turn = true,
                break_on_positive_damage = true,
                on_removed_status_id = "wakeful",
                on_removed_status_save_immunity_tags = successorSaveTags,
                on_removed_status_undispellable = true,
                on_removed_status_consume_after_normal_turn = true,
            },
            "skill-typed-resource-status-effect"
        );
        CombatEffectDef defaultResource = scope.Own(
            new CombatEffectDef { effect_type = "damage" },
            "skill-typed-resource-default-effect"
        );
        GArray effectDefsOwner = scope.Own(
            new GArray(),
            "skill-typed-resource-effect-defs"
        );
        var effectDefs = new Godot.Collections.Array<CombatEffectDef>(effectDefsOwner)
        {
            chainResource,
            saveResource,
            statusResource,
            defaultResource,
        };
        CombatSkillDef combatResource = scope.Own(
            new CombatSkillDef
            {
                skill_id = "typed_projection_probe",
                excluded_target_creature_type_tags = excludedCreatureTags,
                effect_defs = effectDefs,
            },
            "skill-typed-resource-combat"
        );
        SkillDef skillResource = scope.Own(
            new SkillDef
            {
                skill_id = "typed_projection_probe",
                combat_profile = combatResource,
            },
            "skill-typed-resource-skill"
        );

        SkillDefinition skill = SkillDefinition.FromDiagnosticFixture(skillResource);
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "Typed Resource probe should project a combat definition.");
        if (combat == null)
            return;
        _test.Eq(combat.EffectDefinitions.Count, 4, "All synthetic effects should project.");
        if (combat.EffectDefinitions.Count != 4)
            return;

        CombatEffectDefinition chain = combat.EffectDefinitions[0];
        CombatEffectDefinition save = combat.EffectDefinitions[1];
        CombatEffectDefinition status = combat.EffectDefinitions[2];
        CombatEffectDefinition defaultEffect = combat.EffectDefinitions[3];
        CombatChainDamageDefinition chainDamage = chain.ChainDamage;

        _test.True(chainDamage != null, "chain_damage should project a typed chain definition.");
        if (chainDamage == null)
            return;
        _test.Eq(chainDamage.BaseHopRange, 2, "Chain base hop range should project exactly.");
        _test.Eq(
            chainDamage.ConductiveHopRange,
            4,
            "Chain conductive hop range should project exactly."
        );
        _test.Eq(chainDamage.MaxTotalTargets, 5, "Chain target limit should project exactly.");
        _test.Eq(
            chainDamage.BacklashHopRangeBonus,
            1,
            "Chain backlash range bonus should project exactly."
        );
        _test.True(
            chainDamage.ConductiveStatusIds.SequenceEqual(new StringName[] { "shocked" }),
            "Chain conductive status ids should project exactly."
        );
        _test.True(
            chainDamage.ConductiveTerrainEffectIds.SequenceEqual(new StringName[] { "wet" }),
            "Chain conductive terrain ids should project exactly."
        );
        _test.Eq(save.SaveDcBonus, 3, "Authored save DC bonus should project exactly.");
        _test.True(status.SkipTurn, "Status skip-turn behavior should project.");
        _test.True(
            status.BreakOnPositiveDamage,
            "Status positive-damage removal behavior should project."
        );
        _test.Eq(
            status.OnRemovedStatusId,
            new StringName("wakeful"),
            "Status successor id should project exactly."
        );
        _test.True(
            status.OnRemovedStatusSaveImmunityTags.SequenceEqual(
                new StringName[] { "sleep" }
            ),
            "Status successor save tags should project exactly."
        );
        _test.True(
            status.OnRemovedStatusUndispellable,
            "Status successor undispellable flag should project."
        );
        _test.True(
            status.OnRemovedStatusConsumeAfterNormalTurn,
            "Status successor normal-turn consumption should project."
        );
        _test.True(
            combat.ExcludedTargetCreatureTypeTags.SequenceEqual(
                new StringName[] { "undead", "construct" }
            ),
            "Excluded target creature tags should project in authored order."
        );
        _test.True(
            defaultEffect.ChainDamage == null,
            "A non-chain effect should keep the optional chain definition null."
        );
        _test.Eq(defaultEffect.SaveDcBonus, 0, "Default save DC bonus should remain zero.");
        _test.False(defaultEffect.SkipTurn, "Default status skip-turn flag should remain false.");
        _test.False(
            defaultEffect.BreakOnPositiveDamage,
            "Default positive-damage removal flag should remain false."
        );
        _test.False(
            defaultEffect.OnRemovedStatusUndispellable,
            "Default successor undispellable flag should remain false."
        );
        _test.False(
            defaultEffect.OnRemovedStatusConsumeAfterNormalTurn,
            "Default successor consumption flag should remain false."
        );
        _test.Eq(
            defaultEffect.OnRemovedStatusId,
            new StringName(""),
            "Default successor status id should remain empty."
        );
        _test.Eq(
            defaultEffect.OnRemovedStatusSaveImmunityTags.Count,
            0,
            "Default successor save-tag list should remain empty."
        );

        chainStatusIds.Add("mutated_status");
        chainTerrainIds.Clear();
        successorSaveTags[0] = "mutated_save_tag";
        excludedCreatureTags.Clear();
        _test.True(
            chainDamage.ConductiveStatusIds.SequenceEqual(new StringName[] { "shocked" }),
            "Mutating the authored chain status array must not change the definition."
        );
        _test.True(
            chainDamage.ConductiveTerrainEffectIds.SequenceEqual(new StringName[] { "wet" }),
            "Mutating the authored chain terrain array must not change the definition."
        );
        _test.True(
            status.OnRemovedStatusSaveImmunityTags.SequenceEqual(
                new StringName[] { "sleep" }
            ),
            "Mutating the authored successor save tags must not change the definition."
        );
        _test.True(
            combat.ExcludedTargetCreatureTypeTags.SequenceEqual(
                new StringName[] { "undead", "construct" }
            ),
            "Mutating authored excluded creature tags must not change the definition."
        );
        _test.True(
            combat
                .WithStaminaCost(combat.StaminaCost + 1)
                .ExcludedTargetCreatureTypeTags.SequenceEqual(
                    combat.ExcludedTargetCreatureTypeTags
                ),
            "WithStaminaCost should preserve excluded target creature tags."
        );
        _test.True(
            combat
                .WithArea("circle", 2)
                .ExcludedTargetCreatureTypeTags.SequenceEqual(
                    combat.ExcludedTargetCreatureTypeTags
                ),
            "WithArea should preserve excluded target creature tags."
        );

        foreach (CombatEffectDefinition effect in new[] { chain, save, status, defaultEffect })
        {
            AssertTypedEffectFieldsEqual(
                effect,
                effect.WithEffectType(effect.EffectType),
                "WithEffectType"
            );
            AssertTypedEffectFieldsEqual(
                effect,
                effect.WithPreResistanceDamageMultiplier(0.75d),
                "WithPreResistanceDamageMultiplier"
            );
        }

        AssertStringNameListRejectsMutation(
            chainDamage.ConductiveStatusIds,
            "Chain conductive status ids"
        );
        AssertStringNameListRejectsMutation(
            chainDamage.ConductiveTerrainEffectIds,
            "Chain conductive terrain ids"
        );
        AssertNoResourceOrGodotCollection(
            skill,
            "SkillDefinition"
        );
        AssertNoResourceOrGodotCollection(
            combat,
            "SkillDefinition.CombatProfile"
        );
        AssertNoResourceOrGodotCollection(
            combat.EffectDefinitions,
            "CombatSkillDefinition.EffectDefinitions"
        );
        AssertNoResourceOrGodotCollection(
            chain,
            "CombatSkillDefinition.EffectDefinitions[0]"
        );
        AssertNoResourceOrGodotCollection(
            save,
            "CombatSkillDefinition.EffectDefinitions[1]"
        );
        AssertNoResourceOrGodotCollection(
            status,
            "CombatSkillDefinition.EffectDefinitions[2]"
        );
        AssertNoResourceOrGodotCollection(
            defaultEffect,
            "CombatSkillDefinition.EffectDefinitions[3]"
        );
        AssertNoResourceOrGodotCollection(
            combat.ExcludedTargetCreatureTypeTags,
            "CombatSkillDefinition.ExcludedTargetCreatureTypeTags"
        );
        AssertNoResourceOrGodotCollection(
            chainDamage,
            "CombatEffectDefinition.ChainDamage"
        );
        AssertNoResourceOrGodotCollection(
            chainDamage.ConductiveStatusIds,
            "CombatChainDamageDefinition.ConductiveStatusIds"
        );
        AssertNoResourceOrGodotCollection(
            chainDamage.ConductiveTerrainEffectIds,
            "CombatChainDamageDefinition.ConductiveTerrainEffectIds"
        );
        AssertNoResourceOrGodotCollection(
            status.OnRemovedStatusSaveImmunityTags,
            "CombatEffectDefinition.OnRemovedStatusSaveImmunityTags"
        );
    }

    private void TestMalformedGodotValuesReportFullSkillPaths()
    {
        using (
            var effectScope = new NativeLeaseScope(
                "skill-plain-value-effect-path",
                LifetimeDomain.Request
            )
        )
        {
            Resource illegalEffectValue = effectScope.Own(
                new Resource(),
                "plain-value-effect-illegal-object"
            );
            GDictionary effectNested = effectScope.Own(
                new GDictionary { ["bad"] = illegalEffectValue },
                "plain-value-effect-nested"
            );
            GArray effectList = effectScope.Own(
                new GArray { effectNested },
                "plain-value-effect-list"
            );
            GDictionary effectParams = effectScope.Own(
                new GDictionary { ["outer"] = effectList },
                "plain-value-effect-params"
            );
            CombatEffectDef effect = effectScope.Own(
                new CombatEffectDef { effect_type = "damage", @params = effectParams },
                "plain-value-effect"
            );
            GArray effectDefsOwner = effectScope.Own(
                new GArray(),
                "plain-value-effect-defs-owner"
            );
            var effectDefs = new Godot.Collections.Array<CombatEffectDef>(effectDefsOwner);
            effectDefs.Add(effect);
            CombatSkillDef effectCombat = effectScope.Own(
                new CombatSkillDef { effect_defs = effectDefs },
                "plain-value-effect-combat"
            );
            SkillDef effectSkill = effectScope.Own(
                new SkillDef { skill_id = "charge", combat_profile = effectCombat },
                "plain-value-effect-skill"
            );

            AssertInvalidDataPath(
                () => SkillDefinition.FromDiagnosticFixture(effectSkill),
                "<SkillDiagnosticFixture:charge>/entries/0/combat_profile/effect_defs/0/payload/outer",
                "Nested effect Object rejection should identify the complete authored skill path."
            );
        }

        using (
            var variantScope = new NativeLeaseScope(
                "skill-plain-value-variant-path",
                LifetimeDomain.Request
            )
        )
        {
            Resource illegalVariantValue = variantScope.Own(
                new Resource(),
                "plain-value-variant-illegal-object"
            );
            GDictionary variantParams = variantScope.Own(
                new GDictionary { ["nested"] = illegalVariantValue },
                "plain-value-variant-params"
            );
            CombatCastVariantDef variant = variantScope.Own(
                new CombatCastVariantDef { variant_id = "wide", @params = variantParams },
                "plain-value-cast-variant"
            );
            GArray variantsOwner = variantScope.Own(
                new GArray(),
                "plain-value-cast-variants-owner"
            );
            var variants = new Godot.Collections.Array<CombatCastVariantDef>(variantsOwner);
            variants.Add(variant);
            CombatSkillDef variantCombat = variantScope.Own(
                new CombatSkillDef { cast_variants = variants },
                "plain-value-variant-combat"
            );
            SkillDef variantSkill = variantScope.Own(
                new SkillDef { skill_id = "teleport", combat_profile = variantCombat },
                "plain-value-variant-skill"
            );

            AssertInvalidDataPath(
                () => SkillDefinition.FromDiagnosticFixture(variantSkill),
                "<SkillDiagnosticFixture:teleport>/entries/0/combat_profile/cast_variants/0/payload/nested",
                "Cast-variant Object rejection should identify the complete authored skill path."
            );
        }
    }

    private void TestStrictDictionaryAndPackedValueRejection()
    {
        GDictionary nonStringKey = new() { [1] = "bad" };
        GDictionary emptyKey = new() { [""] = "bad" };
        GDictionary packedValue = new() { ["packed"] = new byte[] { 1, 2 } };
        try
        {
            AssertInvalidDataPath(
                () => ContentValueNormalizer.NormalizeDictionary(
                    nonStringKey,
                    "skill.strict.params"
                ),
                "skill.strict.params",
                "Non-string dictionary keys must be rejected."
            );
            AssertInvalidDataPath(
                () => ContentValueNormalizer.NormalizeDictionary(
                    emptyKey,
                    "skill.strict.params"
                ),
                "skill.strict.params",
                "Empty dictionary keys must be rejected."
            );
            AssertInvalidDataPath(
                () => ContentValueNormalizer.NormalizeDictionary(
                    packedValue,
                    "skill.strict.params"
                ),
                "skill.strict.params.packed",
                "Packed arrays must be rejected instead of silently stringified."
            );
        }
        finally
        {
            nonStringKey.Clear();
            emptyKey.Clear();
            packedValue.Clear();
            nonStringKey.Dispose();
            emptyKey.Dispose();
            packedValue.Dispose();
        }
    }

    private void TestSyntheticIllegalObjectAndCycleRejection()
    {
        var illegal = new Dictionary<string, object>
        {
            ["outer"] = new List<object>
            {
                new Dictionary<string, object> { ["bad"] = new object() },
            },
        };
        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeDictionary(
                illegal,
                "synthetic.illegal"
            ),
            "synthetic.illegal.outer[0].bad",
            "Synthetic illegal objects must be rejected with the full nested path."
        );

        var cycle = new Dictionary<string, object>();
        cycle["self"] = cycle;
        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeDictionary(cycle, "synthetic.cycle"),
            "synthetic.cycle.self",
            "Synthetic cycles must fail deterministically instead of recursing indefinitely."
        );

        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeDictionary(
                new DuplicateKeyReadOnlyDictionary(),
                "synthetic.duplicate"
            ),
            "synthetic.duplicate",
            "Duplicate normalized synthetic keys must be rejected."
        );

        var managedStringNameKeys = new Dictionary<StringName, object>
        {
            ["content_id"] = 1,
        };
        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeValue(
                managedStringNameKeys,
                "synthetic.string_name_keys"
            ),
            "synthetic.string_name_keys",
            "Managed maps must use string keys even though raw Godot dictionaries also accept StringName keys."
        );
    }

    private void TestResourceDefaultsAreEffectiveWithoutWritingBack()
    {
        using (
            var scope = new NativeLeaseScope(
                "skill-plain-value-defaults",
                LifetimeDomain.Request
            )
        )
        {
            CombatSkillDef rawCombat = scope.Own(
                new CombatSkillDef { skill_id = "" },
                "plain-value-default-combat"
            );
            SkillDef rawSkill = scope.Own(
                new SkillDef
                {
                    skill_id = "default_probe",
                    icon_id = "",
                    combat_profile = rawCombat,
                },
                "plain-value-default-skill"
            );

            SkillDefinition definition = SkillDefinition.FromDiagnosticFixture(rawSkill);
            _test.True(definition != null, "Default probe should project a SkillDefinition.");
            _test.Eq(
                definition?.IconId?.ToString() ?? "",
                "",
                "Missing icon_id should remain empty in the typed projection."
            );
            _test.Eq(
                definition?.CombatProfile?.SkillId ?? default,
                new StringName("default_probe"),
                "Missing combat_profile.skill_id should use the parent skill id in the typed projection."
            );
            _test.True(rawSkill.icon_id == "", "Projection must not write icon_id back.");
            _test.True(
                rawCombat.skill_id == "",
                "Projection must not write combat_profile.skill_id back."
            );
        }
    }

    private void TestDiagnosticFixtureLevelOverrideProjectionRules()
    {
        using (
            var resetScope = new NativeLeaseScope(
                "skill-level-override-reset",
                LifetimeDomain.Request
            )
        )
        {
            GDictionary levelOne = resetScope.Own(
                new GDictionary
                {
                    ["attack_resolution_mode"] = "direct_effect",
                    ["attack_defense_mode"] = "flat_footed",
                },
                "skill-level-override-reset-one"
            );
            GDictionary levelTwo = resetScope.Own(
                new GDictionary
                {
                    ["attack_resolution_mode"] = "",
                    ["attack_defense_mode"] = "",
                },
                "skill-level-override-reset-two"
            );
            GDictionary overrides = resetScope.Own(
                new GDictionary { [1] = levelOne, [2] = levelTwo },
                "skill-level-override-reset-map"
            );
            CombatSkillDef combatResource = resetScope.Own(
                new CombatSkillDef
                {
                    skill_id = "level_override_reset",
                    attack_resolution_mode = "fate_attack",
                    attack_defense_mode = "touch",
                    level_overrides = overrides,
                },
                "skill-level-override-reset-combat"
            );
            SkillDef skillResource = resetScope.Own(
                new SkillDef
                {
                    skill_id = "level_override_reset",
                    combat_profile = combatResource,
                },
                "skill-level-override-reset-skill"
            );

            CombatSkillDefinition combat = SkillDefinition.FromDiagnosticFixture(skillResource).CombatProfile;
            _test.Eq(
                combat.GetEffectiveAttackResolutionMode(0),
                CombatSkillAttackResolutionMode.FateAttack,
                "Before the first override, attack resolution should use the authored base mode."
            );
            _test.Eq(
                combat.GetEffectiveAttackDefenseMode(0),
                CombatSkillAttackDefenseMode.Touch,
                "Before the first override, attack defense should use the authored base mode."
            );
            _test.Eq(
                combat.GetEffectiveAttackResolutionMode(1),
                CombatSkillAttackResolutionMode.DirectEffect,
                "A present non-empty attack resolution override should apply at its level."
            );
            _test.Eq(
                combat.GetEffectiveAttackDefenseMode(1),
                CombatSkillAttackDefenseMode.FlatFooted,
                "A present non-empty attack defense override should apply at its level."
            );
            _test.Eq(
                combat.GetEffectiveAttackResolutionMode(2),
                CombatSkillAttackResolutionMode.Auto,
                "An explicit empty attack resolution override should reset to Auto."
            );
            _test.Eq(
                combat.GetEffectiveAttackDefenseMode(2),
                CombatSkillAttackDefenseMode.Normal,
                "An explicit empty attack defense override should reset to Normal."
            );
            _test.Eq(
                combat.GetEffectiveAttackResolutionMode(3),
                CombatSkillAttackResolutionMode.Auto,
                "The explicit attack resolution reset should remain present at later levels."
            );
            _test.Eq(
                combat.GetEffectiveAttackDefenseMode(3),
                CombatSkillAttackDefenseMode.Normal,
                "The explicit attack defense reset should remain present at later levels."
            );
        }

        AssertInvalidLevelOverrideProjection(
            "unknown_override_field",
            1,
            "future_field",
            1,
            "<SkillDiagnosticFixture:unknown_override_field>/entries/0/combat_profile/level_overrides/1/future_field",
            "Unknown diagnostic fixture level override fields must fail closed."
        );
        AssertInvalidLevelOverrideProjection(
            "overflow_override_value",
            1,
            "ap_cost",
            (long)int.MaxValue + 1L,
            "<SkillDiagnosticFixture:overflow_override_value>/entries/0/combat_profile/level_overrides/1/ap_cost",
            "Diagnostic fixture level override integers outside Int32 must fail explicitly."
        );
        AssertInvalidLevelOverrideProjection(
            "overflow_override_level",
            (long)int.MaxValue + 1L,
            "ap_cost",
            1,
            "<SkillDiagnosticFixture:overflow_override_level>/entries/0/combat_profile/level_overrides",
            "Diagnostic fixture level keys outside Int32 must fail explicitly."
        );
    }

    private void AssertInvalidLevelOverrideProjection(
        string skillId,
        Variant level,
        string field,
        Variant value,
        string expectedPath,
        string message
    )
    {
        using var scope = new NativeLeaseScope(
            $"skill-level-override-invalid-{skillId}",
            LifetimeDomain.Request
        );
        GDictionary levelOverride = scope.Own(
            new GDictionary { [field] = value },
            $"skill-level-override-invalid-{skillId}-entry"
        );
        GDictionary overrides = scope.Own(
            new GDictionary { [level] = levelOverride },
            $"skill-level-override-invalid-{skillId}-map"
        );
        CombatSkillDef combatResource = scope.Own(
            new CombatSkillDef { skill_id = skillId, level_overrides = overrides },
            $"skill-level-override-invalid-{skillId}-combat"
        );
        SkillDef skillResource = scope.Own(
            new SkillDef { skill_id = skillId, combat_profile = combatResource },
            $"skill-level-override-invalid-{skillId}-skill"
        );
        AssertInvalidDataPath(
            () => SkillDefinition.FromDiagnosticFixture(skillResource),
            expectedPath,
            message
        );
    }

    private void TestDiagnosticFixtureDescriptionVariablesRequireStrings()
    {
        AssertInvalidDescriptionVariable(
            "description_string_name",
            Variant.From(new StringName("named_value")),
            "StringName"
        );
        AssertInvalidDescriptionVariable("description_int", Variant.From(4), "Int");
        AssertInvalidDescriptionVariable("description_float", Variant.From(4.5), "Float");
        AssertInvalidDescriptionVariable("description_bool", Variant.From(true), "Bool");
    }

    private void AssertInvalidDescriptionVariable(
        string skillId,
        Variant value,
        string typeLabel
    )
    {
        using var scope = new NativeLeaseScope(
            $"skill-description-variable-{skillId}",
            LifetimeDomain.Request
        );
        GDictionary config = scope.Own(
            new GDictionary { ["power"] = value },
            $"skill-description-variable-{skillId}-config"
        );
        GDictionary configs = scope.Own(
            new GDictionary { ["0"] = config },
            $"skill-description-variable-{skillId}-configs"
        );
        SkillDef skillResource = scope.Own(
            new SkillDef
            {
                skill_id = skillId,
                level_description_configs = configs,
            },
            $"skill-description-variable-{skillId}-skill"
        );
        AssertInvalidDataPath(
            () => SkillDefinition.FromDiagnosticFixture(skillResource),
            $"<SkillDiagnosticFixture:{skillId}>/entries/0/level_description_configs/0/power",
            $"Diagnostic fixture description variable {typeLabel} values must be rejected."
        );
    }

    private void TestFormalFlawReadOverrideMigrationPreservesEffectiveBehavior()
    {
        SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "warrior_flaw_read"
        );
        CombatSkillDefinition combat = skill.CombatProfile;

        _test.True(combat != null, "Flaw Read should retain its combat profile.");
        if (combat == null)
            return;
        _test.True(
            combat.LevelOverrides.Keys.SequenceEqual(new[] { 3 }),
            "Flaw Read should retain only its supported stamina override."
        );
        _test.Eq(
            combat.GetEffectiveResourceCostValues(2).StaminaCost,
            22,
            "Flaw Read should retain its base stamina cost before level 3."
        );
        _test.Eq(
            combat.GetEffectiveResourceCostValues(3).StaminaCost,
            16,
            "Flaw Read should apply its supported stamina override at level 3."
        );
        _test.Eq(
            combat.GetEffectiveResourceCostValues(4).StaminaCost,
            16,
            "Removing inert level-4 fields must not change the carried stamina override."
        );
        _test.Eq(
            combat.EffectDefinitions.Count,
            1,
            "Flaw Read should retain its authored status effect."
        );
        if (combat.EffectDefinitions.Count == 1)
        {
            _test.Eq(
                combat.EffectDefinitions[0].StatusId,
                new StringName("hex_of_frailty"),
                "Flaw Read should retain the status ID authored on the effect itself."
            );
            _test.Eq(
                combat.EffectDefinitions[0].Power,
                0,
                "Removing the never-consumed power override must preserve effective effect power."
            );
        }
    }

    private void TestFingerprintAndLevelDescriptionRemainStable()
    {
        var config = new Dictionary<string, string>
        {
            ["power"] = "4",
            ["status"] = "burning",
        };
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "plain_graph_description",
            levelDescriptionTemplate: "伤害{power}，状态{status}",
            levelDescriptionConfigs:
                new Dictionary<int, SkillDescriptionVariables>
                {
                    [0] = new SkillDescriptionVariables(config),
                }
        );

        string fingerprintBefore = BuildFingerprint(skill.LevelDescriptionConfigs[0]);
        string descriptionBefore;
        using (GDictionary context = new())
        {
            descriptionBefore = SkillLevelDescriptionFormatter.BuildLevelDescription(
                skill,
                0,
                context
            );
        }

        config["power"] = "99";
        config["status"] = "changed";

        string fingerprintAfter = BuildFingerprint(skill.LevelDescriptionConfigs[0]);
        string descriptionAfter;
        using (GDictionary context = new())
        {
            descriptionAfter = SkillLevelDescriptionFormatter.BuildLevelDescription(
                skill,
                0,
                context
            );
        }

        _test.Eq(
            fingerprintAfter,
            fingerprintBefore,
            "Skill plain-value fingerprint must not change when caller-owned inputs mutate."
        );
        _test.Eq(descriptionBefore, "伤害4，状态burning", "Level description output stays stable.");
        _test.Eq(
            descriptionAfter,
            descriptionBefore,
            "Level descriptions must read the frozen definition graph."
        );
    }

    private void AssertFrozenGraph(
        IReadOnlyDictionary<string, object> graph,
        string ownerLabel
    )
    {
        _test.Eq(graph["number"], 4L, $"{ownerLabel} should copy integral values.");
        _test.True(
            graph["nested"] is IReadOnlyList<object> values
                && values.Count == 2
                && Equals(values[0], 3L)
                && values[1] is IReadOnlyDictionary<string, object> nested
                && Equals(nested["inner"], "original"),
            $"{ownerLabel} should recursively copy and freeze nested list/map values."
        );
    }

    private void AssertTypedEffectFieldsEqual(
        CombatEffectDefinition expected,
        CombatEffectDefinition actual,
        string cloneMethod
    )
    {
        _test.True(actual != null, $"{cloneMethod} should return an effect definition.");
        if (actual == null)
            return;
        _test.True(
            ChainDamageEquals(actual.ChainDamage, expected.ChainDamage),
            $"{cloneMethod} should preserve the typed chain definition."
        );
        _test.Eq(
            actual.SaveDcBonus,
            expected.SaveDcBonus,
            $"{cloneMethod} should preserve the save DC bonus."
        );
        _test.Eq(
            actual.SkipTurn,
            expected.SkipTurn,
            $"{cloneMethod} should preserve the skip-turn flag."
        );
        _test.Eq(
            actual.BreakOnPositiveDamage,
            expected.BreakOnPositiveDamage,
            $"{cloneMethod} should preserve the positive-damage removal flag."
        );
        _test.Eq(
            actual.OnRemovedStatusId,
            expected.OnRemovedStatusId,
            $"{cloneMethod} should preserve the successor status id."
        );
        _test.True(
            actual.OnRemovedStatusSaveImmunityTags.SequenceEqual(
                expected.OnRemovedStatusSaveImmunityTags
            ),
            $"{cloneMethod} should preserve successor save-immunity tags."
        );
        _test.Eq(
            actual.OnRemovedStatusUndispellable,
            expected.OnRemovedStatusUndispellable,
            $"{cloneMethod} should preserve the successor undispellable flag."
        );
        _test.Eq(
            actual.OnRemovedStatusConsumeAfterNormalTurn,
            expected.OnRemovedStatusConsumeAfterNormalTurn,
            $"{cloneMethod} should preserve normal-turn successor consumption."
        );
    }

    private static bool ChainDamageEquals(
        CombatChainDamageDefinition left,
        CombatChainDamageDefinition right
    )
    {
        if (ReferenceEquals(left, right))
            return true;
        return left != null
            && right != null
            && left.BaseHopRange == right.BaseHopRange
            && left.ConductiveHopRange == right.ConductiveHopRange
            && left.MaxTotalTargets == right.MaxTotalTargets
            && left.BacklashHopRangeBonus == right.BacklashHopRangeBonus
            && left.ConductiveStatusIds.SequenceEqual(right.ConductiveStatusIds)
            && left.ConductiveTerrainEffectIds.SequenceEqual(
                right.ConductiveTerrainEffectIds
            );
    }

    private void AssertStringNameListRejectsMutation(
        IReadOnlyList<StringName> values,
        string label
    )
    {
        bool mutationRejected = values is not IList<StringName>;
        if (values is IList<StringName> mutableValues)
        {
            try
            {
                mutableValues.Add("forbidden_mutation");
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }
        }
        _test.True(mutationRejected, $"{label} should reject collection mutation.");
    }

    private void AssertNoResourceOrGodotCollection(object value, string path)
    {
        _test.False(value is Resource, $"{path} must not retain a Resource instance.");
        _test.False(
            value?.GetType().Namespace?.StartsWith(
                "Godot.Collections",
                StringComparison.Ordinal
            ) == true,
            $"{path} must not retain a Godot collection wrapper."
        );
        if (value is IEnumerable values && value is not string)
        {
            int index = 0;
            foreach (object child in values)
            {
                AssertNoResourceOrGodotCollection(child, $"{path}[{index}]");
                index++;
            }
        }
    }

    private void AssertMathValuesEqual(
        IReadOnlyDictionary<string, object> expected,
        IReadOnlyDictionary<string, object> actual,
        string label
    )
    {
        _test.Eq(actual.Count, expected.Count, $"{label} should preserve the math value count.");
        foreach (KeyValuePair<string, object> entry in expected)
        {
            _test.True(
                actual.TryGetValue(entry.Key, out object actualValue),
                $"{label} should preserve key '{entry.Key}'."
            );
            _test.True(
                actualValue?.GetType() == entry.Value.GetType(),
                $"{label} should preserve the CLR type for '{entry.Key}'."
            );
            _test.Eq(
                actualValue,
                entry.Value,
                $"{label} should round-trip '{entry.Key}' without value loss."
            );
        }
    }

    private void AssertInvalidDataPath(Action action, string expectedPath, string message)
    {
        try
        {
            action();
            _test.Fail($"{message} Expected InvalidDataException.");
        }
        catch (InvalidDataException exception)
        {
            _test.True(
                exception.Message.Contains(expectedPath, StringComparison.Ordinal),
                $"{message} actual={exception.Message}"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"{message} Expected InvalidDataException, got {exception.GetType().Name}.");
        }
    }

    private static bool ContainsGodotContainerOrVariant(object value)
    {
        if (value is Variant or GDictionary or GArray or GodotObject)
            return true;
        if (value is IReadOnlyDictionary<string, object> dictionary)
            return dictionary.Values.Any(ContainsGodotContainerOrVariant);
        if (value is IReadOnlyList<object> list)
            return list.Any(ContainsGodotContainerOrVariant);
        return false;
    }

    private static string BuildFingerprint(object value)
    {
        var builder = new StringBuilder();
        AppendFingerprint(builder, value);
        return builder.ToString();
    }

    private static void AppendFingerprint(StringBuilder builder, object value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;
            case IReadOnlyDictionary<string, object> dictionary:
                builder.Append('{');
                foreach (string key in dictionary.Keys.OrderBy(key => key, StringComparer.Ordinal))
                {
                    builder.Append(key).Append(':');
                    AppendFingerprint(builder, dictionary[key]);
                    builder.Append(';');
                }
                builder.Append('}');
                return;
            case IReadOnlyDictionary<string, string> stringDictionary:
                builder.Append('{');
                foreach (
                    string key in stringDictionary.Keys.OrderBy(
                        key => key,
                        StringComparer.Ordinal
                    )
                )
                {
                    builder.Append(key).Append(':');
                    AppendFingerprint(builder, stringDictionary[key]);
                    builder.Append(';');
                }
                builder.Append('}');
                return;
            case IReadOnlyList<object> list:
                builder.Append('[');
                foreach (object entry in list)
                {
                    AppendFingerprint(builder, entry);
                    builder.Append(';');
                }
                builder.Append(']');
                return;
            case StringName stringName:
                builder.Append("sn:").Append(stringName.ToString());
                return;
            case IFormattable formattable:
                builder.Append(value.GetType().Name)
                    .Append(':')
                    .Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            default:
                builder.Append(value.GetType().Name).Append(':').Append(value);
                return;
        }
    }

    private sealed class DuplicateKeyReadOnlyDictionary
        : IReadOnlyDictionary<string, object>
    {
        public object this[string key] => 1;
        public IEnumerable<string> Keys => new[] { "same", "same" };
        public IEnumerable<object> Values => new object[] { 1, 2 };
        public int Count => 2;

        public bool ContainsKey(string key) => key == "same";

        public bool TryGetValue(string key, out object value)
        {
            value = 1;
            return key == "same";
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>("same", 1);
            yield return new KeyValuePair<string, object>("same", 2);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
