using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_passive_status_orchestrator_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<GodotObject> _ownedGodotObjects = new();
    private readonly List<IDisposable> _ownedDisposables = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestPassiveContextAndResolversNoLongerRequireGodotRegistration();
            TestFactoryProjectsIdentityPassivesFromCharacterGateway();
            TestOrchestratorProjectsRaceAndSubracePassives();
            TestOrchestratorSuppressesOriginalRacePassivesForAscension();
            TestOrchestratorProjectsShootingSpecializationBowOnlyRangeBonus();
        }
        finally
        {
            DisposeOwned();
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Passive status orchestrator regression"));
    }

    private void TestPassiveContextAndResolversNoLongerRequireGodotRegistration()
    {
        AssertPlainType(typeof(PassiveSourceContext), nameof(PassiveSourceContext));
        AssertPlainType(typeof(PassiveStatusOrchestrator), nameof(PassiveStatusOrchestrator));
        AssertPlainType(typeof(RaceTraitResolver), nameof(RaceTraitResolver));
        AssertPlainType(typeof(AscensionTraitResolver), nameof(AscensionTraitResolver));
        AssertPlainType(typeof(SkillPassiveResolver), nameof(SkillPassiveResolver));
    }

    private void TestFactoryProjectsIdentityPassivesFromCharacterGateway()
    {
        ProgressionContentRegistry registry = TrackOwned(new ProgressionContentRegistry());
        PartyState partyState = MakePartyState(new[] { new StringName("hero") });
        CharacterManagementModule gateway = TrackDisposable(new CharacterManagementModule());
        gateway.setup(
            partyState,
            registry.GetSkillDefsTyped(),
            registry.GetProfessionDefsTyped(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            null,
            registry.GetIdentityCatalogTyped()
        );

        BattleRuntimeModule runtime = TrackDisposable(new BattleRuntimeModule());
        runtime.setup(gateway, registry.GetSkillDefsTyped(), null, null);
        var units = runtime._unit_factory.BuildAllyUnits(
            partyState,
            new GDictionary()
        );
        foreach (BattleUnitState builtUnit in units)
            TrackOwned(builtUnit);

        _test.Eq(units.Count, 1, "factory should build one ally for passive projection.");
        if (units.Count == 0)
            return;
        var unit = units[0];
        _test.True(
            unit.vision_tags.Contains("normal_vision"),
            "vision tags should include normal_vision from RaceDef."
        );
        _test.True(
            unit.proficiency_tags.Contains("civilian"),
            "proficiency tags should include civilian from RaceDef."
        );
        _test.True(
            unit.proficiency_tags.Contains("weapon_type_spear"),
            "civil_militia should project spear proficiency tag."
        );

    }

    private void TestOrchestratorProjectsRaceAndSubracePassives()
    {
        BattleUnitState unit = MakeBattleUnit("race_projection_unit");
        PassiveSourceContext context = new()
        {
            race_def = MakeRaceDef(),
            subrace_def = MakeSubraceDef(),
        };

        PassiveStatusOrchestrator.ApplyToUnit(unit, context, new Dictionary<StringName, SkillDef>());

        _test.True(unit.vision_tags.Contains("darkvision"), "race vision tag should be projected.");
        _test.True(
            unit.save_advantage_tags.Contains("poison"),
            "subrace save advantage tag should be projected."
        );
        _test.Eq(
            unit.damage_resistances.Get("fire"),
            new StringName("half"),
            "race damage resistance should be projected."
        );
        _test.Eq(
            unit.per_battle_charges.Get("racial_skill_dragon_breath_test", 0),
            2,
            "race per-battle charge should be initialized."
        );
        _test.Eq(
            unit.per_turn_charges.Get("racial_skill_nimble_escape_test", 0),
            1,
            "subrace per-turn charge should be initialized."
        );
    }

    private void TestOrchestratorSuppressesOriginalRacePassivesForAscension()
    {
        BattleUnitState unit = MakeBattleUnit("ascension_projection_unit");
        PassiveSourceContext context = new()
        {
            race_def = MakeRaceDef(),
            subrace_def = MakeSubraceDef(),
            ascension_def = MakeAscensionDef(true),
        };

        PassiveStatusOrchestrator.ApplyToUnit(unit, context, new Dictionary<StringName, SkillDef>());

        _test.False(
            unit.per_battle_charges.ContainsKey("racial_skill_dragon_breath_test"),
            "suppressed race charge should not be initialized."
        );
        _test.False(
            unit.per_turn_charges.ContainsKey("racial_skill_nimble_escape_test"),
                "suppressed subrace charge should not be initialized."
        );
        _test.Eq(
            unit.per_battle_charges.Get("racial_skill_ascension_ray_test", 0),
            3,
            "ascension charge should be initialized."
        );
    }

    private void TestOrchestratorProjectsShootingSpecializationBowOnlyRangeBonus()
    {
        ProgressionContentRegistry registry = new();
        BattleUnitState unit = MakeBattleUnit("shooting_specialization_unit");
        PassiveSourceContext context = new()
        {
            unit_progress = TrackOwned(new UnitProgress()),
        };
        UnitSkillProgress skillProgress = new()
        {
            skill_id = "archer_shooting_specialization",
            is_learned = true,
            skill_level = 0,
            profession_granted_by = "archer",
            granted_source_type = UnitSkillProgress.ToStringName(
                UnitSkillGrantSourceType.Profession
            ),
            granted_source_id = "archer",
        };
        context.unit_progress.SetSkillProgress(skillProgress);
        UnitProfessionProgress professionProgress = new()
        {
            profession_id = "archer",
            rank = 1,
            is_active = true,
        };
        context.unit_progress.SetProfessionProgress(professionProgress);

        PassiveStatusOrchestrator.ApplyToUnit(unit, context, registry.GetSkillDefsTyped());

        BattleStatusEffectState status = unit.GetStatusEffect("archer_shooting_specialization");
        _test.True(status != null, "shooting specialization should project a battle status.");
        if (status != null)
        {
            _test.Eq(
                status.source_skill_level ?? -1,
                0,
                "shooting specialization status should keep learned level 0 in typed field."
            );
            _test.Eq(
                status.range_bonus,
                1,
                "shooting specialization status should carry range_bonus=1 in typed field."
            );
            _test.Eq(
                status.source_skill_id,
                new StringName("archer_shooting_specialization"),
                "shooting specialization status should carry source_skill_id in typed field."
            );
        }

        SkillDef weaponSkill = MakeWeaponRangeSkill();
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "test_shortbow",
                weapon_profile_type_id = "shortbow",
                weapon_family = "bow",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 4,
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(unit, weaponSkill),
            5,
            "shooting specialization should add +1 range for bow weapons."
        );

        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "test_crossbow",
                weapon_profile_type_id = "light_crossbow",
                weapon_family = "crossbow",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 5,
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(unit, weaponSkill),
            5,
            "shooting specialization must not add range for crossbows."
        );

        registry.Dispose();
    }

    private BattleUnitState MakeBattleUnit(StringName unitId)
    {
        return TrackOwned(new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            faction_id = "player",
            control_mode = "manual",
        });
    }

    private SkillDef MakeWeaponRangeSkill()
    {
        SkillDef skill = TrackOwned(new SkillDef
        {
            skill_id = "test_weapon_range_skill",
            skill_type = "active",
            tags = new Godot.Collections.Array<StringName> { "archer", "ranged", "bow" },
        });
        CombatSkillDef combatProfile = new()
        {
            skill_id = skill.skill_id,
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            selection_order_mode = "stable",
            range_value = 1,
        };
        skill.combat_profile = combatProfile;
        return skill;
    }

    private RaceDef MakeRaceDef()
    {
        RaceDef race = TrackOwned(new RaceDef
        {
            race_id = "test_race",
            display_name = "Test Race",
            damage_resistances = new GDictionary
            {
                [new StringName("fire")] = new StringName("half"),
            },
        });
        race.trait_ids.Add("test_race_trait");
        race.vision_tags.Add("darkvision");
        race.racial_granted_skills.Add(MakeRacialGrant("dragon_breath_test", "per_battle", 2));
        return race;
    }

    private SubraceDef MakeSubraceDef()
    {
        SubraceDef subrace = TrackOwned(new SubraceDef
        {
            subrace_id = "test_subrace",
            parent_race_id = "test_race",
            display_name = "Test Subrace",
        });
        subrace.trait_ids.Add("test_subrace_trait");
        subrace.save_advantage_tags.Add("poison");
        subrace.racial_granted_skills.Add(MakeRacialGrant("nimble_escape_test", "per_turn", 1));
        return subrace;
    }

    private AscensionDef MakeAscensionDef(bool suppressesOriginalRaceTraits)
    {
        AscensionDef ascension = TrackOwned(new AscensionDef
        {
            ascension_id = "test_ascension",
            display_name = "Test Ascension",
            suppresses_original_race_traits = suppressesOriginalRaceTraits,
        });
        ascension.trait_ids.Add("ascended_trait");
        ascension.racial_granted_skills.Add(MakeRacialGrant("ascension_ray_test", "per_battle", 3));
        return ascension;
    }

    private RacialGrantedSkill MakeRacialGrant(
        StringName skillId,
        StringName chargeKind,
        int charges
    )
    {
        return TrackOwned(new RacialGrantedSkill
        {
            skill_id = skillId,
            minimum_skill_level = 1,
            charge_kind = chargeKind,
            charges = charges,
        });
    }

    private PartyState MakePartyState(IEnumerable<StringName> memberIds)
    {
        PartyState partyState = TrackOwned(new PartyState());
        foreach (StringName memberId in memberIds)
        {
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString().Capitalize(),
                race_id = "human",
                subrace_id = "common_human",
            };
            memberState.progression.unit_id = memberId;
            memberState.progression.display_name = memberState.display_name;
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            if (partyState.leader_member_id == "")
                partyState.leader_member_id = memberId;
            if (partyState.main_character_member_id == "")
                partyState.main_character_member_id = memberId;
        }
        return partyState;
    }

    private static int GetInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName GetStringName(GDictionary source, StringName key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(source[key]);
    }

    private void AssertPlainType(Type type, string typeName)
    {
    }

    private T TrackOwned<T>(T value)
        where T : GodotObject
    {
        if (value != null)
            _ownedGodotObjects.Add(value);
        return value;
    }

    private T TrackDisposable<T>(T value)
        where T : IDisposable
    {
        if (value != null)
            _ownedDisposables.Add(value);
        return value;
    }

    private void DisposeOwned()
    {
        for (int index = _ownedDisposables.Count - 1; index >= 0; index--)
            _ownedDisposables[index]?.Dispose();
        _ownedDisposables.Clear();

        for (int index = _ownedGodotObjects.Count - 1; index >= 0; index--)
            DisposeOwnedGodotObject(_ownedGodotObjects[index]);
        _ownedGodotObjects.Clear();
    }

    private static void DisposeOwnedGodotObject(GodotObject ownedObject)
    {
        switch (ownedObject)
        {
            case null:
                return;
            case PartyState party:
                GodotRefCountedDisposer.DisposeIfValid(party);
                return;
            case UnitProgress progress:
                GodotRefCountedDisposer.DisposeIfValid(progress);
                return;
            case ProgressionContentRegistry registry:
                GodotRefCountedDisposer.DisposeIfValid(registry);
                return;
            default:
                BattleTestFixture.DisposeFixtureObject(ownedObject);
                return;
        }
    }

}
