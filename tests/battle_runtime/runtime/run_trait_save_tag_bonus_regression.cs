using System.Collections.Generic;
using Godot;

public partial class run_trait_save_tag_bonus_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private static readonly StringName FrightenedTag =
        BattleSaveContentRules.ToStringName(BattleSaveTagKind.Frightened);

    private static readonly StringName DragonFrightfulPresenceTag =
        BattleSaveContentRules.ToStringName(
            BattleSaveTagKind.DragonFrightfulPresence
        );

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAddStackModeSumsAcrossTraits();
        TestHighestStackModeTakesHighestValue();
        TestMixedStackModesCombine();
        TestTagBonusEntersSaveResolution();
        TestTagBonusCoexistsWithAbilityAndStatusBonuses();
        TestPreviewUsesSameTagBonus();
        TestDragonFrightfulPresenceTagFlow();
        TestDuplicateAndExactCapturePreserveTagBonuses();

        RequestTestExit(_test.Finish("Trait save tag bonus regression"));
    }

    private void TestAddStackModeSumsAcrossTraits()
    {
        BattleUnitState unit = MakeUnit("add_stack_target");
        ProjectTraits(
            unit,
            MakeTraitDef(
                "fear_boots_trait",
                MakeEntry(FrightenedTag, 3, "add")
            ),
            MakeTraitDef(
                "fear_set_trait",
                MakeEntry(FrightenedTag, 3, "add")
            )
        );
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            6,
            "two add contributions on the same save tag should sum."
        );

        ProjectTraits(
            unit,
            MakeTraitDef(
                "fear_boots_trait",
                MakeEntry(FrightenedTag, 3, "add")
            ),
            MakeTraitDef(
                "fear_set_trait",
                MakeEntry(FrightenedTag, 3, "add")
            )
        );
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            6,
            "re-projection should replace rather than accumulate tag bonuses."
        );
    }

    private void TestHighestStackModeTakesHighestValue()
    {
        BattleUnitState unit = MakeUnit("highest_stack_target");
        ProjectTraits(
            unit,
            MakeTraitDef(
                "highest_low_trait",
                MakeEntry(FrightenedTag, 3, "highest")
            ),
            MakeTraitDef(
                "highest_high_trait",
                MakeEntry(FrightenedTag, 5, "highest")
            )
        );
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            5,
            "highest contributions should keep only the highest value."
        );
    }

    private void TestMixedStackModesCombine()
    {
        BattleUnitState unit = MakeUnit("mixed_stack_target");
        ProjectTraits(
            unit,
            MakeTraitDef(
                "mixed_add_a_trait",
                MakeEntry(FrightenedTag, 3, "add")
            ),
            MakeTraitDef(
                "mixed_add_b_trait",
                MakeEntry(FrightenedTag, 3, "add")
            ),
            MakeTraitDef(
                "mixed_highest_trait",
                MakeEntry(FrightenedTag, 5, "highest")
            )
        );
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            11,
            "mixed modes should resolve to add sum plus highest value."
        );
    }

    private void TestTagBonusEntersSaveResolution()
    {
        BattleUnitState target = MakeUnit("tag_bonus_save_target");
        target.ReplaceSaveTagBonusesTyped(
            new Dictionary<StringName, int> { [FrightenedTag] = 6 }
        );

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(FrightenedTag, "willpower", 16),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(result.Bonus, 6, "unit tag bonus should enter the total save bonus.");
        _test.Eq(result.RollTotal, 16, "roll total should include the tag bonus.");
        _test.True(result.Success, "tag bonus should raise the save to the DC.");

        BattleSaveResult untagged = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect("poison", "constitution", 16),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(
            untagged.Bonus,
            0,
            "tag bonus should not leak into a different save tag."
        );
    }

    private void TestTagBonusCoexistsWithAbilityAndStatusBonuses()
    {
        BattleUnitState target = MakeUnit("coexist_save_target");
        target.ReplaceSaveTagBonusesTyped(
            new Dictionary<StringName, int> { [FrightenedTag] = 6 }
        );
        target.AddSaveBonusByAbilityTyped("willpower", 2);
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "fear_ward_minor",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                save_bonus = 1,
            }
        );
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "fear_ward_major",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                save_bonus = 4,
            }
        );

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(FrightenedTag, "willpower", 22),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(
            result.Bonus,
            12,
            "total should be ability 2 + tag 6 + status max 4; status Math.Max semantics unchanged."
        );
        _test.Eq(result.RollTotal, 22, "roll total should sum all three bonus lanes.");
        _test.True(result.Success, "combined bonus should meet the DC.");
    }

    private void TestPreviewUsesSameTagBonus()
    {
        BattleUnitState target = MakeUnit("preview_tag_bonus_target");
        target.ReplaceSaveTagBonusesTyped(
            new Dictionary<StringName, int> { [FrightenedTag] = 6 }
        );

        BattleSaveProbabilityResult estimate =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                null,
                target,
                MakeSaveDamageEffect(FrightenedTag, "willpower", 16)
            );
        _test.Eq(
            estimate.Bonus,
            6,
            "preview/AI estimate should read the same tag bonus read view."
        );
        _test.True(
            estimate.SuccessProbabilityBasisPoints > 5000,
            "tag bonus should raise the estimated success probability."
        );
    }

    private void TestDragonFrightfulPresenceTagFlow()
    {
        BattleUnitState target = MakeUnit("dragon_fright_target");
        target.ReplaceSaveTagBonusesTyped(
            new Dictionary<StringName, int> { [DragonFrightfulPresenceTag] = 3 }
        );
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "dragonslayer_focus",
                source_unit_id = "test_source",
                power = 1,
                stacks = 1,
                duration = -1,
                control_save_bonus = 2,
            }
        );

        BattleSaveResult result = BattleSaveResolver.ResolveSaveResult(
            null,
            target,
            MakeSaveDamageEffect(DragonFrightfulPresenceTag, "willpower", 15),
            BattleSaveContext.WithSaveRollOverride(10)
        );
        _test.Eq(
            result.Bonus,
            5,
            "dragon frightful presence should take tag bonus plus control status bonus."
        );
        _test.True(result.Success, "combined bonuses should meet the DC.");

        BattleUnitState immuneTarget = MakeUnit("dragon_fright_immune_target");
        immuneTarget.AddSaveImmunityTagTyped(DragonFrightfulPresenceTag);
        BattleSaveResult immuneResult = BattleSaveResolver.ResolveSaveResult(
            null,
            immuneTarget,
            MakeSaveDamageEffect(DragonFrightfulPresenceTag, "willpower", 15),
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.True(
            immuneResult.Immune,
            "dragon_frightful_presence immunity tag should make the save immune."
        );
    }

    private void TestDuplicateAndExactCapturePreserveTagBonuses()
    {
        BattleUnitState unit = MakeUnit("duplicate_tag_bonus_target");
        ProjectTraits(
            unit,
            MakeTraitDef(
                "duplicate_fear_trait",
                MakeEntry(FrightenedTag, 3, "add")
            )
        );

        BattleUnitState clone = unit.clone();
        _test.Eq(
            clone.GetSaveBonusByTagTyped(FrightenedTag),
            3,
            "gameplay clone should carry tag bonuses."
        );
        clone.ReplaceSaveTagBonusesTyped(
            new Dictionary<StringName, int> { [FrightenedTag] = 9 }
        );
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            3,
            "clone tag bonus writes should not leak into the source owner."
        );

        BattleUnitSaveModifierSnapshot captured =
            unit.CaptureSaveModifiersForMutationSnapshotExact();
        var restored = new BattleUnitState();
        restored.RestoreSaveModifiersForMutationSnapshotExact(captured);
        _test.Eq(
            restored.GetSaveBonusByTagTyped(FrightenedTag),
            3,
            "exact capture/restore should preserve tag bonuses."
        );
        captured.BonusByTag.Put(FrightenedTag, 77);
        _test.Eq(
            unit.GetSaveBonusByTagTyped(FrightenedTag),
            3,
            "exact capture should return a detached tag bonus map."
        );
    }

    private static void ProjectTraits(
        BattleUnitState unit,
        params TraitImportModel[] authoredTraits
    )
    {
        var definitions = new Dictionary<StringName, TraitDefinition>();
        var instances = new List<BattleEffectiveTraitInstanceState>();
        foreach (TraitImportModel authored in authoredTraits)
        {
            TraitDefinition definition = TraitDefinitionProjector.Project(authored);
            definitions[definition.TraitId] = definition;
            instances.Add(
                TraitTestData.EffectiveTrait(
                    definition.TraitId,
                    definition.TraitId,
                    "passive",
                    "none",
                    "none"
                )
            );
        }
        unit.ReplaceEffectiveTraitsTyped(instances);
        BattleTraitPassiveProjectionService.ProjectEffectiveTraitPassives(
            unit,
            definitions
        );
    }

    private static TraitImportModel MakeTraitDef(
        StringName traitId,
        params TraitSaveTagBonusEntryImportModel[] entries
    )
        => new(
            traitId.ToString(),
            traitId.ToString(),
            "Save tag bonus battle fixture.",
            System.Array.Empty<string>(),
            new[] { "equipment_fixed" },
            "save_advantage",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            System.Array.Empty<TraitAttributeModifierImportModel>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<string>(),
            System.Array.Empty<TraitDamageResistanceEntryImportModel>(),
            System.Array.Empty<TraitSaveBonusEntryImportModel>(),
            entries,
            System.Array.Empty<TraitPassiveStatusEffectImportModel>(),
            System.Array.Empty<TraitRollValueSchemaEntryImportModel>()
        );

    private static TraitSaveTagBonusEntryImportModel MakeEntry(
        StringName saveTag,
        int bonus,
        StringName stackMode
    ) => new(saveTag.ToString(), bonus, stackMode.ToString());

    private static CombatEffectDefinition MakeSaveDamageEffect(
        StringName saveTag,
        StringName saveAbility,
        int saveDc
    ) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "fire",
            power: 10,
            saveDc: saveDc,
            saveAbility: saveAbility,
            saveTag: saveTag,
            savePartialOnSuccess: false
        );

    private static BattleUnitState MakeUnit(StringName unitId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 30,
            mp: 0,
            stamina: 20,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        return unit;
    }
}
