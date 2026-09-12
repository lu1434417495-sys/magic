using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_equipment_weapon_profile_overlay_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        try
        {
            TestRangeDeltaAndClampApplied();
            TestDiceAddAndOverrideApplied();
            TestDamageTagAndGripOverridesApplied();
            TestRequireEquippedWeaponSkipsUnarmedUnit();
            TestRequiredWeaponFamilyFilterSkips();
            TestEquipmentTagConditionGatesOverlay();
            TestRemovalRestoresBaselineAndRefreshIsIdempotent();
            TestOverlayPriorityDeterminesOverrideWinner();
            TestEnemyWeaponProjectionAppliesOverlays();
            RequestTestExit(_test.Finish("Equipment weapon profile overlay regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Equipment weapon profile overlay regression"));
        }
    }

    private void TestRangeDeltaAndClampApplied()
    {
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_reach_sword",
            "shortsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        sword.trait_ids = new GStringNameArray { "trait.weapon.overlay_reach" };
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { sword },
            BuildOverlayTraitMap("trait.weapon.overlay_reach"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.weapon.overlay_reach"] = BuildOverlayBinding(
                    "binding.weapon.overlay_reach",
                    "trait.weapon.overlay_reach",
                    "blade",
                    "weapon",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.reach",
                        AttackRangeDelta = 5,
                        MaxAttackRange = 3,
                    }
                ),
            }
        );
        BattleUnitState unit = BuildEquippedAllyUnit(
            runtimeScope,
            sword,
            "overlay_reach_instance"
        );
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            3,
            "range delta should apply and clamp to max_attack_range."
        );
        _test.Eq(
            unit.GetWeaponAttackRange(),
            3,
            "typed range read port should expose the overlay-composed range."
        );
    }

    private void TestDiceAddAndOverrideApplied()
    {
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_dice_longsword",
            "longsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 8, 0),
            MakeWeaponDice(1, 10, 0),
            new[] { new StringName("versatile") }
        );
        sword.trait_ids = new GStringNameArray { "trait.weapon.overlay_dice" };
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { sword },
            BuildOverlayTraitMap("trait.weapon.overlay_dice"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.weapon.overlay_dice"] = BuildOverlayBinding(
                    "binding.weapon.overlay_dice",
                    "trait.weapon.overlay_dice",
                    "blade",
                    "weapon",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.dice_override",
                        Priority = 1,
                        OneHandedDiceOverlay = new EquipmentWeaponDiceOverlayDefinition
                        {
                            Mode = EquipmentWeaponDiceOverlayModeKind.Override,
                            DiceOverride = MakeFixedDiceExpression(2, 4, 1),
                        },
                    },
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.dice_add",
                        Priority = 2,
                        OneHandedDiceOverlay = new EquipmentWeaponDiceOverlayDefinition
                        {
                            Mode = EquipmentWeaponDiceOverlayModeKind.Add,
                            DiceCountDelta = 1,
                            DiceSidesOverride = 8,
                            FlatBonusDelta = 2,
                        },
                        TwoHandedDiceOverlay = new EquipmentWeaponDiceOverlayDefinition
                        {
                            Mode = EquipmentWeaponDiceOverlayModeKind.Add,
                            DiceCountDelta = 1,
                        },
                    }
                ),
            }
        );
        BattleUnitState unit = BuildEquippedAllyUnit(
            runtimeScope,
            sword,
            "overlay_dice_instance"
        );
        BattleWeaponProjectionValues weapon = unit.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(weapon.OneHandedDice.DiceCount, 3, "override then add should compose in priority order for dice count.");
        _test.Eq(weapon.OneHandedDice.DiceSides, 8, "add overlay should replace dice sides after the override.");
        _test.Eq(weapon.OneHandedDice.FlatBonus, 3, "override flat bonus and add flat delta should compose.");
        _test.Eq(weapon.TwoHandedDice.DiceCount, 2, "two-handed add overlay should raise the base 2H dice count.");
        _test.Eq(weapon.TwoHandedDice.DiceSides, 10, "two-handed add overlay should keep base dice sides.");
    }

    private void TestDamageTagAndGripOverridesApplied()
    {
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_grip_longsword",
            "longsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 8, 0),
            MakeWeaponDice(1, 10, 0),
            new[] { new StringName("versatile") }
        );
        sword.trait_ids = new GStringNameArray { "trait.weapon.overlay_grip" };
        TestItemDefinitionBuilder shield = MakeOffHandEquipment("overlay_shield");
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { sword, shield },
            BuildOverlayTraitMap("trait.weapon.overlay_grip"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.weapon.overlay_grip"] = BuildOverlayBinding(
                    "binding.weapon.overlay_grip",
                    "trait.weapon.overlay_grip",
                    "blade",
                    "weapon",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.grip",
                        PhysicalDamageTagOverride = "physical_pierce",
                        GripOverride = "two_handed",
                        UsesTwoHandsOverride = true,
                    }
                ),
            }
        );
        BattleUnitState unit = BuildEquippedAllyUnit(
            runtimeScope,
            sword,
            "overlay_grip_instance"
        );
        unit.GetEquipmentView()
            .SetEquippedEntry(
                "off_hand",
                shield.item_id,
                SlotIds("off_hand"),
                MakeEquipmentInstance(shield.item_id, "overlay_shield_instance")
            );
        runtimeScope.Runtime._unit_factory.RefreshEquipmentProjection(unit);
        BattleWeaponProjectionValues forced = unit.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(
            forced.PhysicalDamageTag,
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Pierce),
            "physical damage tag override should replace the base tag."
        );
        _test.True(
            forced.UsesTwoHands,
            "uses_two_hands override should win over the shield-implied one-handed resolution."
        );
        _test.Eq(
            forced.CurrentGrip,
            BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
            "grip override should write the two-handed grip even with an occupied off-hand."
        );
        _test.Eq(
            forced.ActiveDice.DiceSides,
            10,
            "forced two-handed grip should activate the two-handed dice."
        );
    }

    private void TestRequireEquippedWeaponSkipsUnarmedUnit()
    {
        TestItemDefinitionBuilder armor = MakeBodyArmor("overlay_plate", "plate");
        armor.trait_ids = new GStringNameArray { "trait.armor.overlay_reach" };
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { armor },
            BuildOverlayTraitMap("trait.armor.overlay_reach"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.armor.overlay_reach"] = BuildOverlayBinding(
                    "binding.armor.overlay_reach",
                    "trait.armor.overlay_reach",
                    "plate",
                    "armor",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.armor_reach",
                        RequireEquippedWeapon = true,
                        AttackRangeDelta = 2,
                    }
                ),
            }
        );
        PartyMemberState memberState = runtimeScope.PartyState.GetMemberState("hero");
        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "body",
            armor.item_id,
            SlotIds("body"),
            MakeEquipmentInstance(armor.item_id, "overlay_plate_instance")
        );
        BattleUnitState unit = BuildSingleAllyUnit(
            runtimeScope.Runtime._unit_factory,
            runtimeScope.PartyState,
            "armor-overlay-unarmed"
        );
        _test.Eq(
            unit.GetEquipmentAbilitySourcesReadViewTyped().Count,
            1,
            "armor binding should project an equipment ability source."
        );
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            1,
            "require_equipped_weapon overlay must skip an unarmed unit."
        );
    }

    private void TestRequiredWeaponFamilyFilterSkips()
    {
        TestItemDefinitionBuilder armor = MakeBodyArmor("overlay_bow_plate", "plate");
        armor.trait_ids = new GStringNameArray { "trait.armor.overlay_bow" };
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_family_sword",
            "shortsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { armor, sword },
            BuildOverlayTraitMap("trait.armor.overlay_bow"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.armor.overlay_bow"] = BuildOverlayBinding(
                    "binding.armor.overlay_bow",
                    "trait.armor.overlay_bow",
                    "plate",
                    "armor",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.bow_only",
                        RequiredWeaponFamilies = new HashSet<StringName> { "bow" },
                        AttackRangeDelta = 2,
                    }
                ),
            }
        );
        PartyMemberState memberState = runtimeScope.PartyState.GetMemberState("hero");
        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "body",
            armor.item_id,
            SlotIds("body"),
            MakeEquipmentInstance(armor.item_id, "overlay_bow_plate_instance")
        );
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            sword.item_id,
            SlotIds("main_hand"),
            MakeEquipmentInstance(sword.item_id, "overlay_family_sword_instance")
        );
        BattleUnitState unit = BuildSingleAllyUnit(
            runtimeScope.Runtime._unit_factory,
            runtimeScope.PartyState,
            "armor-overlay-family"
        );
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            1,
            "bow-only overlay must not apply to a sword-family weapon."
        );
    }

    private void TestEquipmentTagConditionGatesOverlay()
    {
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_condition_sword",
            "shortsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        sword.trait_ids = new GStringNameArray { "trait.weapon.overlay_condition" };
        TestItemDefinitionBuilder charm = MakeOffHandEquipment("overlay_lucky_charm");
        charm.tags = new GStringNameArray { "lucky" };
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { sword, charm },
            BuildOverlayTraitMap("trait.weapon.overlay_condition"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.weapon.overlay_condition"] = BuildOverlayBinding(
                    "binding.weapon.overlay_condition",
                    "trait.weapon.overlay_condition",
                    "blade",
                    "weapon",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.lucky_reach",
                        AttackRangeDelta = 2,
                        ConditionGroup = new EquipmentConditionGroupDefinition
                        {
                            Mode = "all",
                            Conditions = new[]
                            {
                                new EquipmentAbilityConditionDefinition
                                {
                                    ConditionId = "condition.lucky_charm",
                                    Kind = "has_equipment_tag",
                                    PayloadDefinition = new HasEquipmentTagConditionPayloadDefinition
                                    {
                                        Subject = "source",
                                        EquipmentSelector = "off_hand",
                                        AllTags = new StringName[] { "lucky" },
                                    },
                                },
                            },
                        },
                    }
                ),
            }
        );
        BattleUnitState unit = BuildEquippedAllyUnit(
            runtimeScope,
            sword,
            "overlay_condition_instance"
        );
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            1,
            "conditioned overlay should stay inactive without the off-hand lucky item."
        );
        unit.GetEquipmentView()
            .SetEquippedEntry(
                "off_hand",
                charm.item_id,
                SlotIds("off_hand"),
                MakeEquipmentInstance(charm.item_id, "overlay_lucky_charm_instance")
            );
        runtimeScope.Runtime._unit_factory.RefreshEquipmentProjection(unit);
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            3,
            "conditioned overlay should apply once the off-hand lucky item is equipped."
        );
    }

    private void TestRemovalRestoresBaselineAndRefreshIsIdempotent()
    {
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_cleanup_sword",
            "shortsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        sword.trait_ids = new GStringNameArray { "trait.weapon.overlay_cleanup" };
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { sword },
            BuildOverlayTraitMap("trait.weapon.overlay_cleanup"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.weapon.overlay_cleanup"] = BuildOverlayBinding(
                    "binding.weapon.overlay_cleanup",
                    "trait.weapon.overlay_cleanup",
                    "blade",
                    "weapon",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.cleanup",
                        AttackRangeDelta = 2,
                    }
                ),
            }
        );
        BattleUnitState unit = BuildEquippedAllyUnit(
            runtimeScope,
            sword,
            "overlay_cleanup_instance"
        );
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            3,
            "overlay should raise the equipped weapon range."
        );
        runtimeScope.Runtime._unit_factory.RefreshWeaponProjection(unit);
        runtimeScope.Runtime._unit_factory.RefreshWeaponProjection(unit);
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            3,
            "repeated weapon projection refreshes must not stack overlay deltas."
        );
        unit.GetEquipmentView().ClearSlot("main_hand");
        runtimeScope.Runtime._unit_factory.RefreshBattleUnit(unit);
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            1,
            "removing the source equipment should restore the unarmed baseline range."
        );
    }

    private void TestOverlayPriorityDeterminesOverrideWinner()
    {
        TestItemDefinitionBuilder sword = MakeWeapon(
            "overlay_priority_sword",
            "shortsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        sword.trait_ids = new GStringNameArray { "trait.weapon.overlay_priority" };
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithOverlays(
            new[] { sword },
            BuildOverlayTraitMap("trait.weapon.overlay_priority"),
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                ["binding.weapon.overlay_priority"] = BuildOverlayBinding(
                    "binding.weapon.overlay_priority",
                    "trait.weapon.overlay_priority",
                    "blade",
                    "weapon",
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.low_priority",
                        Priority = 1,
                        PhysicalDamageTagOverride = "physical_blunt",
                    },
                    new EquipmentWeaponProfileOverlayDefinition
                    {
                        OverlayId = "overlay.high_priority",
                        Priority = 9,
                        PhysicalDamageTagOverride = "physical_pierce",
                    }
                ),
            }
        );
        BattleUnitState unit = BuildEquippedAllyUnit(
            runtimeScope,
            sword,
            "overlay_priority_instance"
        );
        _test.Eq(
            unit.GetWeaponProjectionReadViewTyped().Values.PhysicalDamageTag,
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Pierce),
            "the higher-priority overlay should win override fields (last-wins in stable order)."
        );
    }

    private void TestEnemyWeaponProjectionAppliesOverlays()
    {
        TestItemDefinitionBuilder blade = MakeWeapon(
            "enemy_overlay_blade",
            "shortsword",
            TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        blade.trait_ids = new GStringNameArray { "trait.weapon.enemy_overlay" };
        ItemDefinition bladeDefinition = blade.ToDefinition();
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            [bladeDefinition.ItemId] = bladeDefinition,
        };
        var templateBuilder = new TestEnemyTemplateDefinitionBuilder
        {
            TemplateId = "overlay_raider",
            DisplayName = "Overlay Raider",
            BrainId = "",
            CognitionKind = "sapient",
            EnemyCount = 1,
            BodySize = BattleUnitState.BodySizeMedium,
            AttackEquipmentItemId = "enemy_overlay_blade",
        };
        templateBuilder.BaseAttributeOverrides["strength"] = 10;
        templateBuilder.BaseAttributeOverrides["agility"] = 10;
        templateBuilder.BaseAttributeOverrides["constitution"] = 10;
        templateBuilder.BaseAttributeOverrides["perception"] = 10;
        templateBuilder.BaseAttributeOverrides["intelligence"] = 10;
        templateBuilder.BaseAttributeOverrides["willpower"] = 10;
        EnemyTemplateDefinition template = templateBuilder.Build(itemDefs);
        var templates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [template.TemplateId] = template,
        };
        var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
        {
            ["binding.weapon.enemy_overlay"] = BuildOverlayBinding(
                "binding.weapon.enemy_overlay",
                "trait.weapon.enemy_overlay",
                "blade",
                "weapon",
                new EquipmentWeaponProfileOverlayDefinition
                {
                    OverlayId = "overlay.enemy_reach",
                    AttackRangeDelta = 2,
                }
            ),
        };

        StringName encounterProfileId = "test_overlay_raider_encounter";
        StringName rosterProfileId = "test_overlay_raider_roster";
        WildEncounterRosterDefinition roster = new(
            rosterProfileId,
            "Overlay Raider Roster",
            0,
            1,
            new[]
            {
                new WildEncounterRosterStageDefinition(
                    0,
                    new[]
                    {
                        new WildEncounterRosterUnitEntryDefinition(
                            "overlay_raider",
                            1,
                            "Overlay Raider"
                        ),
                    }
                ),
            }
        );
        BattleEncounterDefinition encounter = new(
            encounterProfileId,
            "Overlay Raider Encounter",
            rosterProfileId,
            BattleEliminationObjectiveDefinition.Instance,
            new BattleEncounterWorldResolutionDefinition(
                BattleWorldResolutionMode.Clear,
                BattleWorldResolutionMode.Preserve,
                BattleWorldResolutionMode.Preserve,
                0
            )
        );
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounterProfileId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [rosterProfileId] = roster,
            },
            templates
        );
        IReadOnlyList<BattleUnitState> units = builder.BuildEnemyUnitStatesFromDefinitions(
            new EncounterAnchorData
            {
                entity_id = "overlay_raider_anchor",
                display_name = "Overlay Raider",
                world_coord = new Vector2I(3, 3),
                faction_id = "hostile",
                region_tag = "overlay_tests",
                vision_range = 2,
                encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
                encounter_profile_id = encounterProfileId,
                growth_stage = 0,
                suppressed_until_step = 0,
            },
            _contentSnapshot.Skills,
            templates,
            new Dictionary<StringName, EnemyAiBrainDefinition>(),
            itemDefs,
            BuildOverlayTraitMap("trait.weapon.enemy_overlay"),
            bindings
        );
        _test.Eq(units.Count, 1, "enemy overlay fixture should build exactly one unit.");
        if (units.Count == 0)
        {
            return;
        }
        BattleUnitState enemy = units[0];
        _test.Eq(
            enemy.GetEquipmentAbilitySourcesReadViewTyped().Count,
            1,
            "enemy battle-only equipment should project an ability source."
        );
        _test.Eq(
            enemy.GetWeaponProjectionReadViewTyped().Values.AttackRange,
            3,
            "enemy weapon projection should include the overlay range delta."
        );
    }

    private BattleUnitState BuildEquippedAllyUnit(
        BattleRuntimeScope runtimeScope,
        TestItemDefinitionBuilder weapon,
        StringName instanceId
    )
    {
        PartyMemberState memberState = runtimeScope.PartyState.GetMemberState("hero");
        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            weapon.item_id,
            SlotIds("main_hand"),
            MakeEquipmentInstance(weapon.item_id, instanceId)
        );
        return BuildSingleAllyUnit(
            runtimeScope.Runtime._unit_factory,
            runtimeScope.PartyState,
            $"equipped-{weapon.item_id}"
        );
    }

    private BattleRuntimeScope BuildRuntimeWithOverlays(
        IReadOnlyList<TestItemDefinitionBuilder> items,
        Dictionary<StringName, TraitDefinition> traitDefs,
        Dictionary<StringName, EquipmentAbilityBindingDefinition> bindings
    )
    {
        PartyState partyState = BuildPartyState("hero");
        var itemDefs = new Dictionary<StringName, ItemDefinition>();
        foreach (
            TestItemDefinitionBuilder itemDef
            in items ?? Array.Empty<TestItemDefinitionBuilder>()
        )
        {
            if (itemDef == null)
                continue;
            ItemDefinition itemDefinition = itemDef.ToDefinition();
            itemDefs[itemDefinition.ItemId] = itemDefinition;
        }

        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            _contentSnapshot.Skills,
            _contentSnapshot.Professions,
            new Dictionary<StringName, AchievementDefinition>(),
            itemDefs,
            new Dictionary<StringName, QuestDefinition>(),
            traitDefs,
            null,
            new ProgressionIdentityCatalogData()
        );

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            characterManagement,
            _contentSnapshot.Skills,
            item_defs: itemDefs,
            trait_defs: traitDefs,
            equipment_ability_bindings: bindings
        );
        return new BattleRuntimeScope(runtime, partyState, characterManagement);
    }

    private static Dictionary<StringName, TraitDefinition> BuildOverlayTraitMap(StringName traitId)
    {
        return new Dictionary<StringName, TraitDefinition>
        {
            [traitId] = new TraitDefinition(
                traitId,
                traitId.ToString(),
                "Overlay fixture trait.",
                new[] { new StringName("weapon_feat") },
                new[] { new StringName("equipment_fixed") },
                "halfling_luck",
                "on_natural_one",
                "stack_by_instance",
                "none",
                "none",
                "",
                0,
                0,
                Array.Empty<AttributeModifierDefinition>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<TraitDamageResistanceEntryDefinition>(),
                Array.Empty<TraitSaveBonusEntryDefinition>(),
                Array.Empty<TraitSaveTagBonusEntryDefinition>(),
                Array.Empty<TraitPassiveStatusEffectDefinition>(),
                Array.Empty<TraitRollValueSchemaEntryDefinition>()
            ),
        };
    }

    private static EquipmentAbilityBindingDefinition BuildOverlayBinding(
        StringName bindingId,
        StringName traitId,
        StringName requiredItemTag,
        StringName equipmentTypeId,
        params EquipmentWeaponProfileOverlayDefinition[] overlays
    )
    {
        return new EquipmentAbilityBindingDefinition
        {
            BindingId = bindingId,
            TraitId = traitId,
            AllowedSourceKinds = new HashSet<StringName> { "equipment_fixed" },
            RequiredTraitCategories = new HashSet<StringName> { "weapon_feat" },
            RequiredItemTags = new HashSet<StringName> { requiredItemTag },
            SupportedEquipmentTypeIds = new HashSet<StringName> { equipmentTypeId },
            WeaponProfileOverlays = overlays,
        };
    }

    private static DiceExpressionDefinition MakeFixedDiceExpression(int count, int sides, int flatBonus)
    {
        return new DiceExpressionDefinition
        {
            Terms = new[]
            {
                new DiceExpressionTermDefinition { DiceCount = count, DiceSides = sides },
            },
            FlatBonus = flatBonus,
        };
    }

    private static BattleUnitState BuildSingleAllyUnit(
        BattleUnitFactory factory,
        PartyState partyState,
        string label
    )
    {
        var units = factory.BuildAllyUnits(partyState, new GDictionary());
        if (units.Count != 1)
        {
            throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
        }
        return units[0];
    }

    private static PartyState BuildPartyState(StringName memberId)
    {
        var partyState = new PartyState();
        var memberState = new PartyMemberState
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            progression = new UnitProgress(),
            equipment_state = new EquipmentState(),
        };
        partyState.SetMemberState(memberState);
        partyState.active_member_ids.Add(memberId);
        partyState.leader_member_id = memberId;
        return partyState;
    }

    private static TestItemDefinitionBuilder MakeWeapon(
        StringName itemId,
        StringName weaponTypeId,
        StringName damageTag,
        int attackRange,
        TestWeaponDamageDiceDefinitionBuilder oneHandedDice,
        TestWeaponDamageDiceDefinitionBuilder twoHandedDice,
        IReadOnlyList<StringName> properties
    )
    {
        var itemDef = new TestItemDefinitionBuilder
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
            tags = new GStringNameArray { "melee", "blade" },
        };
        var profile = new TestWeaponProfileDefinitionBuilder
        {
            weapon_type_id = weaponTypeId,
            training_group = "martial",
            range_type = "melee",
            family = "sword",
            damage_tag = damageTag,
            attack_range = attackRange,
            one_handed_dice = oneHandedDice,
            two_handed_dice = twoHandedDice,
        };
        foreach (StringName property in properties ?? Array.Empty<StringName>())
        {
            if (property != "")
            {
                profile.properties.Add(property);
            }
        }
        itemDef.weapon_profile = profile;
        return itemDef;
    }

    private static TestItemDefinitionBuilder MakeBodyArmor(StringName itemId, StringName tag)
    {
        return new TestItemDefinitionBuilder
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            equipment_type_id = "armor",
            equipment_slot_ids = new Godot.Collections.Array<string> { "body" },
            is_stackable = false,
            max_stack = 1,
            tags = new GStringNameArray { tag },
        };
    }

    private static TestItemDefinitionBuilder MakeOffHandEquipment(StringName itemId)
    {
        return new TestItemDefinitionBuilder
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            equipment_type_id = "shield",
            equipment_slot_ids = new Godot.Collections.Array<string> { "off_hand" },
            is_stackable = false,
            max_stack = 1,
        };
    }

    private static TestWeaponDamageDiceDefinitionBuilder MakeWeaponDice(
        int count,
        int sides,
        int bonus
    )
    {
        return new TestWeaponDamageDiceDefinitionBuilder
        {
            dice_count = count,
            dice_sides = sides,
            flat_bonus = bonus,
        };
    }

    private static EquipmentInstanceState MakeEquipmentInstance(StringName itemId, StringName instanceId)
    {
        return EquipmentInstanceState.CreateInstance(itemId, instanceId);
    }

    private static GStringNameArray SlotIds(params StringName[] values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value != "")
            {
                result.Add(value);
            }
        }
        return result;
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        internal BattleRuntimeScope(
            BattleRuntimeModule runtime,
            PartyState partyState,
            CharacterManagementModule characterManagement
        )
        {
            Runtime = runtime;
            PartyState = partyState;
            CharacterManagement = characterManagement;
        }

        internal BattleRuntimeModule Runtime { get; }

        internal PartyState PartyState { get; }

        private CharacterManagementModule CharacterManagement { get; }

        public void Dispose()
        {
            Runtime?.dispose();
            CharacterManagement?.Dispose();
        }
    }
}
