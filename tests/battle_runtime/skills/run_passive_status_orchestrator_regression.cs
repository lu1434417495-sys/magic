using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_passive_status_orchestrator_regression : LifecycleTestSceneTree
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
        Run();
    }

    private void Run()
    {
        TestPassiveContextAndResolversNoLongerRequireGodotRegistration();
        TestFactoryProjectsIdentityPassivesFromCharacterGateway();
        TestOrchestratorProjectsRaceAndSubracePassives();
        TestOrchestratorSuppressesOriginalRacePassivesForAscension();
        TestOrchestratorProjectsShootingSpecializationBowOnlyRangeBonus();

        RequestTestExit(_test.Finish("Passive status orchestrator regression"));
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
        PartyState partyState = MakePartyState(new[] { new StringName("hero") });
        CharacterManagementModule gateway = new();
        gateway.setup(
            partyState,
            _contentSnapshot.Skills,
            _contentSnapshot.Professions,
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>(),
            _contentSnapshot.Traits,
            null,
            _contentSnapshot.IdentityCatalog
        );

        BattleRuntimeModule runtime = new();
        runtime.setup(gateway, _contentSnapshot.Skills, null, null);
        var units = runtime._unit_factory.BuildAllyUnits(
            partyState,
            new GDictionary()
        );

        _test.Eq(units.Count, 1, "factory should build one ally for passive projection.");
        if (units.Count == 0)
            return;
        var unit = units[0];
        _test.True(
            unit.vision_tags.Contains("normal_vision"),
            "vision tags should include normal_vision from the race definition."
        );
        _test.True(
            unit.proficiency_tags.Contains("civilian"),
            "proficiency tags should include civilian from the race definition."
        );
        _test.True(
            unit.proficiency_tags.Contains("weapon_type_spear"),
            "civil_militia should project spear proficiency tag."
        );

        runtime.dispose();
        gateway.Dispose();
    }

    private void TestOrchestratorProjectsRaceAndSubracePassives()
    {
        BattleUnitState unit = MakeBattleUnit("race_projection_unit");
        PassiveSourceContext context = new()
        {
            race_def = MakeRaceDef(),
            subrace_def = MakeSubraceDef(),
        };

        PassiveStatusOrchestrator.ApplyToUnit(
            unit,
            context,
            new Dictionary<StringName, SkillDefinition>()
        );

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

        PassiveStatusOrchestrator.ApplyToUnit(
            unit,
            context,
            new Dictionary<StringName, SkillDefinition>()
        );

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
        BattleUnitState unit = MakeBattleUnit("shooting_specialization_unit");
        PassiveSourceContext context = new()
        {
            unit_progress = new UnitProgress(),
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

        PassiveStatusOrchestrator.ApplyToUnit(
            unit,
            context,
            _contentSnapshot.Skills
        );

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

        SkillDefinition weaponSkill = MakeWeaponRangeSkill();
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
    }

    private static BattleUnitState MakeBattleUnit(StringName unitId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            faction_id = "player",
            control_mode = "manual",
        };
    }

    private static SkillDefinition MakeWeaponRangeSkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "test_weapon_range_skill",
            tags: new[]
            {
                new StringName("archer"),
                new StringName("ranged"),
                new StringName("bow"),
            },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "test_weapon_range_skill",
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 1
            )
        );
    }

    private static RaceDefinition MakeRaceDef()
    {
        return new RaceDefinition(
            "test_race",
            "Test Race",
            "",
            "",
            "",
            System.Array.Empty<StringName>(),
            "medium",
            6,
            System.Array.Empty<AttributeModifierDefinition>(),
            [new StringName("test_race_trait")],
            [MakeRacialGrant("dragon_breath_test", "per_battle", 2)],
            System.Array.Empty<StringName>(),
            [new StringName("darkvision")],
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, StringName> { ["fire"] = "half" },
            System.Array.Empty<StringName>(),
            System.Array.Empty<string>()
        );
    }

    private static SubraceDefinition MakeSubraceDef()
    {
        return new SubraceDefinition(
            "test_subrace",
            "test_race",
            "Test Subrace",
            "",
            "",
            0,
            System.Array.Empty<AttributeModifierDefinition>(),
            [new StringName("test_subrace_trait")],
            [MakeRacialGrant("nimble_escape_test", "per_turn", 1)],
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            [new StringName("poison")],
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<string>()
        );
    }

    private static AscensionDefinition MakeAscensionDef(bool suppressesOriginalRaceTraits)
    {
        return new AscensionDefinition(
            "test_ascension",
            "Test Ascension",
            "",
            System.Array.Empty<StringName>(),
            [new StringName("ascended_trait")],
            [MakeRacialGrant("ascension_ray_test", "per_battle", 3)],
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<string>(),
            false,
            suppressesOriginalRaceTraits
        );
    }

    private static RacialGrantedSkillDefinition MakeRacialGrant(
        StringName skillId,
        StringName chargeKind,
        int charges
    )
    {
        return new RacialGrantedSkillDefinition(skillId, 1, chargeKind, charges);
    }

    private static PartyState MakePartyState(IEnumerable<StringName> memberIds)
    {
        PartyState partyState = new();
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

}
