using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_equipment_durability_selected_target_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestSelectedCommitOnlyMutatesRequestedInstance();
        TestStaleSelectedCommitDoesNotFallbackToReplacement();
        TestSelectedCommitSaveSuccessReturnsResolvedResultWithoutMutation();
        TestConfiguredWeightMapDoesNotDefaultUnweightedSlot();
        TestOccupiedSlotSelectionReportsMatchedSlot();
        TestTypedCombatEffectSlotWeightsBuildSelectorQueryDespiteLegacyParams();

        RequestTestExit(_test.Finish("Equipment durability selected target regression"));
    }

    private void TestSelectedCommitOnlyMutatesRequestedInstance()
    {
        using BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("caster", "player");
        BattleUnitState target = BuildUnit("target", "enemy");
        EquipInstance(target, "main_hand", "bronze_sword", "eq_main", 20);
        EquipInstance(target, "off_hand", "bronze_shield", "eq_off", 20);

        EquipmentDurabilityCommitResult result = resolver.ApplyEquipmentDurabilityDamageToSelection(
            new EquipmentDurabilityCommitRequest
            {
                SourceUnit = caster,
                TargetUnit = target,
                TargetEquipment = BuildTargetRef(target, "off_hand", "off_hand"),
                EffectDefinition = DisjunctionEffect(7),
                DamageContext = HitContext(saveRollOverride: 1),
                TotalDamage = 1,
                TotalShieldAbsorbed = 0,
                SourceKey = "selected_commit_test",
                ActionId = "damage_off_hand",
            }
        );

        _test.True(result.Resolved, "selected commit should resolve through the function call.");
        _test.Eq(
            result.EquipmentInstanceId,
            new StringName("eq_off"),
            "commit result should point at the selected instance."
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedInstance("main_hand").current_durability,
            20,
            "selected commit should not mutate a different equipped instance."
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedInstance("off_hand").current_durability,
            13,
            "selected commit should apply durability loss to the selected instance."
        );
    }

    private void TestStaleSelectedCommitDoesNotFallbackToReplacement()
    {
        using BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("stale_caster", "player");
        BattleUnitState target = BuildUnit("stale_target", "enemy");
        EquipInstance(target, "main_hand", "bronze_sword", "eq_old", 20);
        EquipmentAbilityEquipmentTargetRef selectedTarget = BuildTargetRef(
            target,
            "main_hand",
            "main_hand"
        );
        EquipInstance(target, "main_hand", "iron_sword", "eq_new", 20);

        EquipmentDurabilityCommitResult result = resolver.ApplyEquipmentDurabilityDamageToSelection(
            new EquipmentDurabilityCommitRequest
            {
                SourceUnit = caster,
                TargetUnit = target,
                TargetEquipment = selectedTarget,
                EffectDefinition = DisjunctionEffect(7),
                DamageContext = HitContext(saveRollOverride: 1),
                TotalDamage = 1,
                TotalShieldAbsorbed = 0,
                SourceKey = "selected_commit_test",
                ActionId = "stale_ref",
            }
        );

        EquipmentInstanceState replacement = target.GetEquipmentView().GetEquippedInstance("main_hand");
        _test.False(result.Resolved, "stale selected target should not resolve.");
        _test.Eq(result.NoOpReason, new StringName("target_equipment_changed"), "stale selected target should report a stable no-op reason.");
        _test.Eq(replacement.instance_id, new StringName("eq_new"), "replacement equipment should remain equipped.");
        _test.Eq(replacement.current_durability, 20, "stale selected target should not damage replacement equipment.");
    }

    private void TestSelectedCommitSaveSuccessReturnsResolvedResultWithoutMutation()
    {
        using BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("save_caster", "player");
        BattleUnitState target = BuildUnit("save_target", "enemy");
        EquipInstance(target, "main_hand", "bronze_sword", "eq_saved", 20);

        EquipmentDurabilityCommitResult result = resolver.ApplyEquipmentDurabilityDamageToSelection(
            new EquipmentDurabilityCommitRequest
            {
                SourceUnit = caster,
                TargetUnit = target,
                TargetEquipment = BuildTargetRef(target, "main_hand", "main_hand"),
                EffectDefinition = DisjunctionEffect(7),
                DamageContext = HitContext(saveRollOverride: 20),
                TotalDamage = 1,
                TotalShieldAbsorbed = 0,
                SourceKey = "selected_commit_test",
                ActionId = "save_success",
            }
        );

        _test.True(result.Resolved, "save success should still resolve the selected commit.");
        _test.True(result.SaveResult.Success, "natural 20 should pass the equipment durability save.");
        _test.Eq(result.DurabilityLoss, 0, "save success should not lose durability.");
        _test.Eq(result.DurabilityAfter, 20, "save success result should report unchanged durability.");
        _test.Eq(
            target.GetEquipmentView().GetEquippedInstance("main_hand").current_durability,
            20,
            "save success should leave selected equipment durability unchanged."
        );
    }

    private void TestConfiguredWeightMapDoesNotDefaultUnweightedSlot()
    {
        using BattleDamageResolver resolver = new();
        BattleUnitState target = BuildUnit("weight_target", "enemy");
        EquipInstance(target, "main_hand", "bronze_sword", "eq_weighted_main", 20);

        BattleDamageResolver.EquipmentDurabilitySelectionResult result =
            resolver.SelectEquipmentForDurabilityDamage(
                new BattleDamageResolver.EquipmentDurabilitySelectionQuery
                {
                    TargetUnit = target,
                    TargetSlots = Names("main_hand"),
                    SlotWeights = Weights(("off_hand", 5)),
                    ConsumeRandom = false,
                }
            );

        _test.False(
            result.HasSelection,
            "candidate-only selector should not select when random consumption is disabled."
        );
        _test.Eq(
            result.Candidates.Count,
            0,
            "a configured weight map should not assign default weight to an unweighted slot."
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedInstance("main_hand").current_durability,
            20,
            "selector should not mutate equipment durability."
        );
    }

    private void TestOccupiedSlotSelectionReportsMatchedSlot()
    {
        using BattleDamageResolver resolver = new();
        BattleUnitState caster = BuildUnit("occupied_slot_caster", "player");
        BattleUnitState target = BuildUnit("occupied_slot_target", "enemy");
        EquipInstance(
            target,
            "main_hand",
            "greatsword",
            "eq_two_hand",
            20,
            Names("main_hand", "off_hand")
        );

        BattleDamageResolver.EquipmentDurabilitySelectionResult selection =
            resolver.SelectEquipmentForDurabilityDamage(
                new BattleDamageResolver.EquipmentDurabilitySelectionQuery
                {
                    TargetUnit = target,
                    TargetSlots = Names("off_hand"),
                    SlotWeights = Weights(("off_hand", 5)),
                    ConsumeRandom = true,
                }
            );

        _test.True(
            selection.HasSelection,
            $"selector should find occupied-slot target instead of {selection.NoTargetReason}."
        );
        if (!selection.HasSelection)
            return;
        _test.Eq(
            selection.SelectedTarget.EntrySlotId,
            new StringName("main_hand"),
            "multi-slot equipment identity should stay on the entry slot."
        );
        _test.Eq(
            selection.SelectedTarget.SlotId,
            new StringName("off_hand"),
            "selected target ref should preserve the occupied slot that matched."
        );

        EquipmentDurabilityCommitResult result = resolver.ApplyEquipmentDurabilityDamageToSelection(
            new EquipmentDurabilityCommitRequest
            {
                SourceUnit = caster,
                TargetUnit = target,
                TargetEquipment = selection.SelectedTarget,
                EffectDefinition = DisjunctionEffect(7),
                DamageContext = HitContext(saveRollOverride: 1),
                TotalDamage = 1,
                TotalShieldAbsorbed = 0,
                SourceKey = "selected_commit_test",
                ActionId = "occupied_slot",
            }
        );

        _test.True(
            result.Resolved,
            $"selector-produced target ref should commit instead of {result.NoOpReason}."
        );
        _test.Eq(
            result.EntrySlotId,
            new StringName("main_hand"),
            "commit result should keep the entry slot identity."
        );
        _test.Eq(
            result.SlotId,
            new StringName("off_hand"),
            "commit result should keep the occupied slot that matched the selector."
        );
        _test.Eq(
            result.EquipmentInstanceId,
            new StringName("eq_two_hand"),
            "commit result should identify the selected equipment instance."
        );
        _test.Eq(
            target.GetEquipmentView().GetEquippedInstance("main_hand").current_durability,
            13,
            "occupied-slot selection should damage the owning entry equipment."
        );
    }

    private void TestTypedCombatEffectSlotWeightsBuildSelectorQueryDespiteLegacyParams()
    {
        using BattleDamageResolver resolver = new();
        BattleUnitState target = BuildUnit("legacy_weight_target", "enemy");
        EquipInstance(target, "main_hand", "bronze_sword", "eq_legacy_weight", 20);

        CombatEffectDefinition effect = DisjunctionEffectFromResource(
            7,
            targetSlots: Names("main_hand"),
            typedSlotWeights: CombatEffectSlotWeights(("main_hand", 1)),
            slotWeightMap: WeightMap(("off_hand", 5))
        );
        _test.Eq(
            effect.EquipmentDurabilitySlotWeights.Count,
            1,
            "test effect should carry typed durability slot weights."
        );
        _test.Eq(
            effect.GetStringNameListParamTyped("target_slots").Count,
            1,
            "test effect should carry target_slots."
        );

        BattleDamageResolver.EquipmentDurabilitySelectionResult selection =
            resolver.SelectEquipmentForDurabilityDamage(
                new BattleDamageResolver.EquipmentDurabilitySelectionQuery
                {
                    TargetUnit = target,
                    TargetSlots = effect.GetStringNameListParamTyped("target_slots"),
                    SlotWeights = effect.EquipmentDurabilitySlotWeights,
                    ConsumeRandom = false,
                }
            );

        _test.Eq(
            selection.Candidates.Count,
            1,
            "typed selector query should use CombatEffectDefinition slot weights, not legacy params.slot_weight_map."
        );
        _test.Eq(
            selection.TotalWeight,
            1,
            "typed selector query should preserve the typed main_hand weight."
        );
    }

    private static EquipmentAbilityEquipmentTargetRef BuildTargetRef(
        BattleUnitState unit,
        StringName entrySlotId,
        StringName slotId
    )
    {
        EquipmentEntryState entry = unit.GetEquipmentView().GetEntry(entrySlotId);
        EquipmentInstanceState instance = entry.GetEquipmentInstance();
        return new EquipmentAbilityEquipmentTargetRef
        {
            UnitId = unit.unit_id,
            EntrySlotId = entrySlotId,
            SlotId = slotId,
            ItemId = entry.item_id,
            EquipmentInstanceId = entry.instance_id,
            OccupiedSlotIds = new GStringNameArray(entry.occupied_slot_ids),
            CurrentDurability = instance.current_durability,
        };
    }

    private void EquipInstance(
        BattleUnitState unitState,
        StringName slotId,
        StringName itemId,
        StringName instanceId,
        int currentDurability,
        GStringNameArray occupiedSlotIds = null
    )
    {
        EquipmentState equipmentState = unitState.GetEquipmentView() ?? new EquipmentState();
        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(itemId, instanceId);
        instance.current_durability = currentDurability;
        bool equipped = equipmentState.SetEquippedEntry(
            slotId,
            itemId,
            occupiedSlotIds ?? Names(slotId),
            instance
        );
        _test.True(equipped, "test equipment instance should be equipped.");
        unitState.SetEquipmentView(equipmentState);
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            is_alive = true,
            current_hp = 30,
            current_mp = 0,
            current_stamina = 0,
            current_aura = 0,
            current_ap = 1,
            attribute_snapshot = new AttributeSnapshot(),
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 0);
        unit.attribute_snapshot.SetValue("intelligence", 18);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 3);
        return unit;
    }

    private static DamageResolutionContext HitContext(int saveRollOverride) =>
        DamageResolutionContext.FromDictionary(
            new GDictionary
            {
                ["attack_success"] = true,
                ["save_roll_override"] = saveRollOverride,
            }
        );

    private static CombatEffectDefinition DisjunctionEffect(
        int power,
        GStringNameArray targetSlots = null
    ) =>
        TestSkillDefinitionProjection.BuildEffect(
            "equipment_durability_damage",
            power: Mathf.Max(power, 1),
            effectTargetTeamFilter: "enemy",
            saveDcMode: "caster_spell",
            saveAbility: "willpower",
            saveDcSourceAbility: "intelligence",
            saveTag: "equipment_disjunction",
            requireDamageApplied: true,
            parameters: new System.Collections.Generic.Dictionary<string, object>
            {
                ["max_damaged_items"] = 1,
                ["target_slots"] = targetSlots ?? Names("main_hand"),
            }
        );

    private static CombatEffectDefinition DisjunctionEffectFromResource(
        int power,
        GStringNameArray targetSlots = null,
        Godot.Collections.Array<CombatEffectSlotWeightDef> typedSlotWeights = null,
        GDictionary slotWeightMap = null
    ) =>
        CombatEffectDefinition.FromResource(
            new CombatEffectDef
            {
                effect_type = "equipment_durability_damage",
                power = Mathf.Max(power, 1),
                effect_target_team_filter = "enemy",
                save_dc_mode = "caster_spell",
                save_ability = "willpower",
                save_dc_source_ability = "intelligence",
                save_tag = "equipment_disjunction",
                require_damage_applied = true,
                equipment_durability_slot_weights =
                    typedSlotWeights ?? new Godot.Collections.Array<CombatEffectSlotWeightDef>(),
                @params = new GDictionary
                {
                    ["max_damaged_items"] = 1,
                    ["slot_weight_map"] = slotWeightMap ?? WeightMap(("main_hand", 1)),
                    ["target_slots"] = targetSlots ?? Names("main_hand"),
                },
            },
            "test.equipment_durability_selected_target.effect"
        );

    private static Godot.Collections.Array<CombatEffectSlotWeightDef> CombatEffectSlotWeights(
        params (StringName SlotId, int Weight)[] values
    )
    {
        var result = new Godot.Collections.Array<CombatEffectSlotWeightDef>();
        foreach ((StringName slotId, int weight) in values)
        {
            result.Add(
                new CombatEffectSlotWeightDef
                {
                    slot_id = slotId,
                    weight = weight,
                }
            );
        }
        return result;
    }

    private static GStringNameArray Names(params StringName[] values)
    {
        GStringNameArray result = new();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private static IReadOnlyList<EquipmentSlotWeightDefinition> Weights(
        params (StringName SlotId, int Weight)[] values
    )
    {
        var result = new List<EquipmentSlotWeightDefinition>();
        foreach ((StringName slotId, int weight) in values)
        {
            result.Add(
                new EquipmentSlotWeightDefinition
                {
                    SlotId = slotId,
                    Weight = weight,
                }
            );
        }
        return result;
    }

    private static GDictionary WeightMap(params (StringName SlotId, int Weight)[] values)
    {
        GDictionary result = new();
        foreach ((StringName slotId, int weight) in values)
            result[slotId] = weight;
        return result;
    }

}
