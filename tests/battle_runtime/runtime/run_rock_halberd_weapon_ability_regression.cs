using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_rock_halberd_weapon_ability_regression : SceneTree
{
    private static readonly StringName RockHalberdItemId =
        "weapon_unique_polearm_rock_halberd_148";
    private static readonly StringName StoneTouchTraitId =
        "weapon.polearm.rock_halberd.stone_touch";
    private static readonly StringName CompletePetrificationTraitId =
        "weapon.polearm.rock_halberd.complete_petrification";
    private static readonly StringName StoneTouchBindingId =
        "binding.weapon.polearm.rock_halberd.stone_touch";
    private static readonly StringName CompletePetrificationBindingId =
        "binding.weapon.polearm.rock_halberd.complete_petrification";
    private static readonly StringName PetrificationCountStatusId =
        "rock_halberd_petrification_count";
    private static readonly StringName SlowStatusId = "slow";
    private static readonly StringName ParalyzedStatusId = "paralyzed";
    private static readonly StringName StoneTearsTraitId =
        "weapon.polearm.rock_halberd.stone_tears";
    private static readonly StringName PetrificationImmunityTag = "petrification_immunity";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRockHalberdContentLoadsAndProjects();
            TestStoneTearsGrantsImmunityAndPerception();
            TestStoneTouchAppliesSlowOnlyOnFailedConSave();
            TestThirdHitOnSameTargetAppliesParalyzedOnlyOnFailedConSave();
            TestPetrificationCountDoesNotShareAcrossTargets();
            Quit(_test.Finish("Rock Halberd weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(_test.Finish("Rock Halberd weapon ability regression"));
        }
    }

    private void TestRockHalberdContentLoadsAndProjects()
    {
        using RockHalberdFixture fixture = RockHalberdFixture.Build(new GArray());

        _test.True(fixture.ItemDefs.ContainsKey(RockHalberdItemId), "Content registry should load Rock Halberd item.");
        _test.True(fixture.TraitDefs.ContainsKey(StoneTouchTraitId), "Content registry should load Stone Touch trait.");
        _test.True(
            fixture.TraitDefs.ContainsKey(CompletePetrificationTraitId),
            "Content registry should load Complete Petrification trait."
        );
        _test.True(
            fixture.Bindings.ContainsKey(StoneTouchBindingId),
            "Equipment ability registry should load Stone Touch binding."
        );
        _test.True(
            fixture.Bindings.ContainsKey(CompletePetrificationBindingId),
            "Equipment ability registry should load Complete Petrification binding."
        );

        ItemDef rawRockHalberd = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_halberd_rock_halberd.tres"
        );
        _test.True(rawRockHalberd != null, "Raw Rock Halberd resource should load.");
        if (rawRockHalberd != null)
        {
            _test.Eq(rawRockHalberd.display_name, "岩石之戟", "Rock Halberd display name should match design.");
            _test.Eq(
                rawRockHalberd.base_item_id,
                new StringName("weapon_type_halberd_base"),
                "Rock Halberd should inherit the halberd base item."
            );
            _test.Eq(rawRockHalberd.base_price, 45000, "Rock Halberd base price should be 45000.");
            WeaponProfileDef rawProfile = rawRockHalberd.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "Rock Halberd should declare a weapon profile override.");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.weapon_type_id, new StringName("halberd"), "Rock Halberd weapon type should be halberd.");
                _test.Eq(rawProfile.training_group, new StringName("martial"), "Rock Halberd training should be martial.");
                _test.Eq(rawProfile.range_type, new StringName("melee"), "Rock Halberd range type should be melee.");
                _test.Eq(rawProfile.family, new StringName("polearm"), "Rock Halberd family should be polearm.");
                _test.Eq(rawProfile.damage_tag, new StringName("physical_slash"), "Rock Halberd damage tag should be physical_slash.");
                _test.Eq(rawProfile.attack_range, 2, "Rock Halberd attack range should be 2.");
                _test.True(rawProfile.one_handed_dice == null, "Rock Halberd should not declare one-handed damage.");
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "Rock Halberd two-handed dice should be 1D10+3.");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 10, "Rock Halberd two-handed dice should be 1D10+3.");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 3, "Rock Halberd two-handed dice should be 1D10+3.");
                _test.True(ContainsStringName(rawProfile.GetPropertiesTyped(), "two_handed"), "Rock Halberd should declare two_handed.");
                _test.True(ContainsStringName(rawProfile.GetPropertiesTyped(), "heavy"), "Rock Halberd should declare heavy.");
                _test.True(ContainsStringName(rawProfile.GetPropertiesTyped(), "reach"), "Rock Halberd should declare reach.");
            }
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildRockHalberdUnit("projection");
        _test.Eq(equipped.weapon_item_id, RockHalberdItemId, "Equipped unit should keep Rock Halberd item id.");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("halberd"), "Rock Halberd should project as halberd.");
        _test.Eq(equipped.weapon_family, new StringName("polearm"), "Rock Halberd should project polearm family.");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "Rock Halberd should project slashing damage.");
        _test.Eq(equipped.weapon_attack_range, 2, "Rock Halberd attack range should project as 2.");
        _test.True(equipped.weapon_uses_two_hands, "Rock Halberd should project as two-handed.");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "Projected two-handed dice should be 1D10+3.");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "Projected two-handed dice should be 1D10+3.");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "Projected two-handed dice should be 1D10+3.");
        AssertUnitHasTraitAndAbilitySource(equipped, StoneTouchTraitId, StoneTouchBindingId, "eq_rock_halberd_projection");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CompletePetrificationTraitId,
            CompletePetrificationBindingId,
            "eq_rock_halberd_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "Removing Rock Halberd should clear weapon item id.");
        _test.Eq(
            equipped.equipment_ability_sources.Count,
            0,
            "Removing Rock Halberd should clear equipment ability sources."
        );
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "Removing Rock Halberd should restore baseline trait projection count."
        );
        _test.False(
            equipped.effective_trait_ids.Contains(StoneTouchTraitId),
            "Removing Rock Halberd should clear Stone Touch trait."
        );
        _test.False(
            equipped.effective_trait_ids.Contains(CompletePetrificationTraitId),
            "Removing Rock Halberd should clear Complete Petrification trait."
        );
        _test.False(
            equipped.effective_trait_ids.Contains(StoneTearsTraitId),
            "Removing Rock Halberd should clear Stone Tears trait."
        );
        _test.False(
            ContainsStringName(equipped.save_advantage_tags, PetrificationImmunityTag),
            "Removing Rock Halberd should clear petrification save immunity."
        );
    }

    private void TestStoneTearsGrantsImmunityAndPerception()
    {
        using RockHalberdFixture fixture = RockHalberdFixture.Build(new GArray());
        _test.True(
            fixture.TraitDefs.ContainsKey(StoneTearsTraitId),
            "Content registry should load Stone Tears trait."
        );

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("stone_tears_baseline");
        _test.False(
            ContainsStringName(baseline.save_advantage_tags, PetrificationImmunityTag),
            "Baseline unit should not have petrification save immunity."
        );
        _test.Eq(
            baseline.save_bonus_by_ability?.Get(new StringName("perception")) ?? 0,
            0,
            "Baseline unit should not have a perception save bonus."
        );

        BattleUnitState equipped = fixture.BuildRockHalberdUnit("stone_tears");
        _test.True(
            equipped.effective_trait_ids.Contains(StoneTearsTraitId),
            "Equipping Rock Halberd should project Stone Tears trait."
        );
        _test.True(
            ContainsStringName(equipped.save_advantage_tags, PetrificationImmunityTag),
            "Stone Tears should grant petrification save immunity."
        );
        _test.Eq(
            equipped.save_bonus_by_ability?.Get(new StringName("perception")) ?? 0,
            1,
            "Stone Tears should grant perception saves +1 without changing the attribute."
        );
        _test.Eq(
            equipped.attribute_snapshot.GetValue("perception"),
            baseline.attribute_snapshot.GetValue("perception"),
            "Stone Tears should not change the perception attribute value."
        );

        BattleUnitState attacker = fixture.BuildRockHalberdUnit("stone_tears_attacker");
        BattleUnitState immuneTarget = BuildTarget("stone_tears_immune", new Vector2I(1, 0));
        immuneTarget.save_advantage_tags.Add(PetrificationImmunityTag);
        ResolveAfterHit(
            fixture,
            attacker,
            immuneTarget,
            "rock_halberd_stone_tears_immune",
            saveRollOverride: 1
        );
        _test.False(
            immuneTarget.HasStatusEffect(SlowStatusId),
            "A target with petrification save immunity should not receive Stone Touch slow."
        );
    }

    private void TestStoneTouchAppliesSlowOnlyOnFailedConSave()
    {
        using RockHalberdFixture fixture = RockHalberdFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildRockHalberdUnit("stone_touch");

        BattleUnitState failedTarget = BuildTarget("stone_touch_failed", new Vector2I(1, 0));
        BattleEquipmentAbilityAfterHitResult failedResult = ResolveAfterHit(
            fixture,
            attacker,
            failedTarget,
            "rock_halberd_stone_touch_failed",
            saveRollOverride: 1
        );
        BattleStatusEffectState slow = failedTarget.GetStatusEffect(SlowStatusId);
        _test.True(slow != null, "A failed DC14 CON save on first hit should apply slow.");
        _test.Eq(slow?.duration ?? -1, 60, "Rock Halberd slow should last 60 TU.");
        _test.Eq(slow?.source_unit_id ?? new StringName(""), attacker.unit_id, "Rock Halberd slow should record source unit.");
        BattleEquipmentAbilityStatusActionResult slowResult = FindStatusResult(failedResult, SlowStatusId);
        _test.True(slowResult?.Applied == true, "Failed slow save should be reported as applied.");
        _test.Eq(slowResult?.SaveResult.Dc ?? 0, 14, "Stone Touch slow save DC should be 14.");
        _test.Eq(slowResult?.SaveResult.Ability ?? new StringName(""), new StringName("constitution"), "Stone Touch save ability should be CON.");

        BattleUnitState successTarget = BuildTarget("stone_touch_success", new Vector2I(1, 0));
        BattleEquipmentAbilityAfterHitResult successResult = ResolveAfterHit(
            fixture,
            attacker,
            successTarget,
            "rock_halberd_stone_touch_success",
            saveRollOverride: 20
        );
        _test.False(successTarget.HasStatusEffect(SlowStatusId), "A successful DC14 CON save should not apply slow.");
        BattleEquipmentAbilityStatusActionResult skippedSlow = FindStatusResult(successResult, SlowStatusId);
        _test.True(skippedSlow != null, "Successful slow save should still emit a status gate result.");
        _test.False(skippedSlow?.Applied ?? true, "Successful slow save should be reported as not applied.");
        _test.Eq(skippedSlow?.SaveResult.Dc ?? 0, 14, "Successful Stone Touch gate should use DC14.");
    }

    private void TestThirdHitOnSameTargetAppliesParalyzedOnlyOnFailedConSave()
    {
        using RockHalberdFixture fixture = RockHalberdFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildRockHalberdUnit("third_hit");
        BattleUnitState target = BuildTarget("third_hit_target", new Vector2I(1, 0));

        for (int hit = 1; hit <= 2; hit++)
        {
            ResolveAfterHit(
                fixture,
                attacker,
                target,
                $"rock_halberd_same_target_hit_{hit}",
                saveRollOverride: 20
            );
            BattleStatusEffectState counter = target.GetStatusEffect(PetrificationCountStatusId);
            _test.True(counter != null, $"Hit {hit} should record petrification count.");
            _test.Eq(counter?.stacks ?? 0, hit, $"Hit {hit} should leave count at {hit}.");
            _test.False(target.HasStatusEffect(ParalyzedStatusId), $"Hit {hit} should not paralyze before three hits.");
        }

        BattleEquipmentAbilityAfterHitResult thirdResult = ResolveAfterHit(
            fixture,
            attacker,
            target,
            "rock_halberd_same_target_hit_3",
            saveRollOverride: 1
        );
        BattleStatusEffectState thirdCounter = target.GetStatusEffect(PetrificationCountStatusId);
        _test.Eq(thirdCounter?.stacks ?? 0, 3, "Third hit should leave petrification count at 3.");
        BattleStatusEffectState paralyzed = target.GetStatusEffect(ParalyzedStatusId);
        _test.True(paralyzed != null, "Third hit on same target should apply paralyzed after failed DC16 CON save.");
        _test.Eq(paralyzed?.duration ?? -1, 60, "Rock Halberd paralyzed should last 60 TU.");
        _test.Eq(paralyzed?.source_unit_id ?? new StringName(""), attacker.unit_id, "Rock Halberd paralyzed should record source unit.");
        BattleEquipmentAbilityStatusActionResult paralyzeResult = FindStatusResult(thirdResult, ParalyzedStatusId);
        _test.True(paralyzeResult?.Applied == true, "Failed third-hit save should be reported as paralyzed applied.");
        _test.Eq(paralyzeResult?.SaveResult.Dc ?? 0, 16, "Complete Petrification save DC should be 16.");
        _test.Eq(
            paralyzeResult?.SaveResult.Ability ?? new StringName(""),
            new StringName("constitution"),
            "Complete Petrification save ability should be CON."
        );

        BattleUnitState successTarget = BuildTarget("third_hit_success_target", new Vector2I(1, 0));
        for (int hit = 1; hit <= 3; hit++)
        {
            ResolveAfterHit(
                fixture,
                attacker,
                successTarget,
                $"rock_halberd_success_target_hit_{hit}",
                saveRollOverride: 20
            );
        }
        _test.False(
            successTarget.HasStatusEffect(ParalyzedStatusId),
            "Third hit should not paralyze when the DC16 CON save succeeds."
        );
    }

    private void TestPetrificationCountDoesNotShareAcrossTargets()
    {
        using RockHalberdFixture fixture = RockHalberdFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildRockHalberdUnit("target_local");
        BattleUnitState firstTarget = BuildTarget("first_target", new Vector2I(1, 0));
        BattleUnitState otherTarget = BuildTarget("other_target", new Vector2I(2, 0));

        ResolveAfterHit(fixture, attacker, firstTarget, "rock_halberd_first_target_hit_1", 20);
        ResolveAfterHit(fixture, attacker, firstTarget, "rock_halberd_first_target_hit_2", 20);
        _test.Eq(
            firstTarget.GetStatusEffect(PetrificationCountStatusId)?.stacks ?? 0,
            2,
            "First target should hold its own two-hit count."
        );
        _test.False(firstTarget.HasStatusEffect(ParalyzedStatusId), "Two hits on first target should not paralyze.");

        ResolveAfterHit(fixture, attacker, otherTarget, "rock_halberd_other_target_hit_1", 20);
        _test.Eq(
            otherTarget.GetStatusEffect(PetrificationCountStatusId)?.stacks ?? 0,
            1,
            "Other target should start a separate one-hit count."
        );
        _test.False(otherTarget.HasStatusEffect(ParalyzedStatusId), "Other target should not inherit first target paralyze count.");

        ResolveAfterHit(fixture, attacker, firstTarget, "rock_halberd_first_target_hit_3", 1);
        _test.True(
            firstTarget.HasStatusEffect(ParalyzedStatusId),
            "Returning to first target for the third hit should use first target count."
        );
        _test.False(
            otherTarget.HasStatusEffect(ParalyzedStatusId),
            "Paralyzing first target should not paralyze the other target."
        );
    }

    private static BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
        RockHalberdFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int saveRollOverride
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        return fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = state,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static BattleEquipmentAbilityStatusActionResult FindStatusResult(
        BattleEquipmentAbilityAfterHitResult result,
        StringName statusId
    )
    {
        foreach (BattleEquipmentAbilityStatusActionResult statusResult in result?.StatusResults ?? Array.Empty<BattleEquipmentAbilityStatusActionResult>())
        {
            if (statusResult?.StatusId == statusId)
                return statusResult;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 100,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        if (unit == null)
            throw new InvalidOperationException("unit is null.");
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"unit missing equipment ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentEquipment)
            throw new InvalidOperationException($"{bindingId} should come from persistent equipment.");
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
        {
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
        }
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources ?? new List<BattleEquipmentAbilitySourceState>())
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private sealed class RockHalberdFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private RockHalberdFixture(
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            PartyState partyState,
            BattleRuntimeModule runtime
        )
        {
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = itemRegistry.GetItemDefsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static RockHalberdFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemRegistry.GetItemDefsTyped(),
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemRegistry.GetItemDefsTyped(),
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new RockHalberdFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildRockHalberdUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                RockHalberdItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    RockHalberdItemId,
                    $"eq_rock_halberd_{label}"
                )
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            return units[0];
        }

        private static PartyState BuildPartyState(StringName memberId)
        {
            PartyState partyState = new();
            PartyMemberState memberState = new()
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
    }
}
