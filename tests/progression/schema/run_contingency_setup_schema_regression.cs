using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_contingency_setup_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCurrentPartyPayloadAcceptsOneUnchargedSetup();
        TestCurrentPartyPayloadAcceptsOneChargedSetup();
        TestUnchargedSetupRejectsReservedMpAndMaterialCosts();
        TestDisabledSetupRejectsChargedState();
        TestRejectsTwoChargedSetupsOnOneMember();
        TestCurrentPartyPayloadRequiresContingencySetups();
        TestRejectsUnknownFieldsInEveryContingencyPayload();
        TestAllAuthoritativeResolverTypesParse();
        TestSafeCellIsOnlyEmptyCellPreference();
        TestOldRootAndPartyVersionsReject();
        TestParameterBindingsAcceptOnlyFlatSupportedValues();
        TestParameterBindingArraysRoundTripAsStringNames();

        RequestTestExit(_test.Finish("Contingency setup schema regression"));
    }

    private void TestCurrentPartyPayloadAcceptsOneUnchargedSetup()
    {
        GDictionary setupPayload = BuildSetupPayload(
            "setup_uncharged",
            charged: false,
            reservedMpMax: 0,
            materialCosts: new GArray()
        );
        PartyState partyState = PartyState.FromDictionary(BuildPartyPayload(setupPayload));

        _test.True(partyState != null, "Current party payload should accept one uncharged setup.");
        PartyMemberState memberState = partyState?.GetMemberState("hero_001");
        _test.Eq(
            memberState?.GetContingencySetupsTyped().Count ?? -1,
            1,
            "Member should expose one typed contingency setup."
        );
        _test.True(
            memberState?.TryGetContingencySetupTyped("setup_uncharged", out _) ?? false,
            "Member should resolve setup by setup_id."
        );
        _test.Eq(
            memberState?.GetTotalReservedMpMax() ?? -1,
            0,
            "Uncharged setup should not reserve MP."
        );
    }

    private void TestCurrentPartyPayloadAcceptsOneChargedSetup()
    {
        GDictionary setupPayload = BuildSetupPayload(
            "setup_charged",
            charged: true,
            reservedMpMax: 12,
            materialCosts: new GArray { BuildMaterialCostPayload() }
        );
        PartyState partyState = PartyState.FromDictionary(BuildPartyPayload(setupPayload));
        PartyMemberState memberState = partyState?.GetMemberState("hero_001");

        _test.True(partyState != null, "Current party payload should accept one charged setup.");
        _test.Eq(
            memberState?.GetChargedContingencySetupCount() ?? -1,
            1,
            "Member should count one charged contingency setup."
        );
        _test.Eq(
            memberState?.GetTotalReservedMpMax() ?? -1,
            12,
            "Charged setup should contribute reserved_mp_max."
        );

        GDictionary roundTrip = memberState
            ?.GetContingencySetupsTyped()[0]
            ?.ToDictionary();
        _test.True(
            roundTrip != null && HasExactKeys(roundTrip, SetupKeys()),
            "Setup ToDictionary should preserve the exact boundary keys."
        );
    }

    private void TestUnchargedSetupRejectsReservedMpAndMaterialCosts()
    {
        _test.True(
            PartyState.FromDictionary(
                BuildPartyPayload(
                    BuildSetupPayload(
                        "setup_bad_reserved",
                        charged: false,
                        reservedMpMax: 1,
                        materialCosts: new GArray()
                    )
                )
            ) == null,
            "charged=false with reserved_mp_max > 0 should fail."
        );
        _test.True(
            PartyState.FromDictionary(
                BuildPartyPayload(
                    BuildSetupPayload(
                        "setup_bad_materials",
                        charged: false,
                        reservedMpMax: 0,
                        materialCosts: new GArray { BuildMaterialCostPayload() }
                    )
                )
            ) == null,
            "charged=false with non-empty material_costs should fail."
        );
    }

    private void TestDisabledSetupRejectsChargedState()
    {
        GDictionary setupPayload = BuildSetupPayload(
            "setup_disabled_charged",
            charged: true,
            reservedMpMax: 8,
            materialCosts: new GArray { BuildMaterialCostPayload() }
        );
        setupPayload["enabled"] = false;

        _test.True(
            PartyState.FromDictionary(BuildPartyPayload(setupPayload)) == null,
            "enabled=false with charged=true should fail current setup schema."
        );
    }

    private void TestRejectsTwoChargedSetupsOnOneMember()
    {
        GDictionary first = BuildSetupPayload(
            "setup_charged_a",
            charged: true,
            reservedMpMax: 6,
            materialCosts: new GArray { BuildMaterialCostPayload() }
        );
        GDictionary second = BuildSetupPayload(
            "setup_charged_b",
            charged: true,
            reservedMpMax: 7,
            materialCosts: new GArray { BuildMaterialCostPayload() }
        );
        _test.True(
            PartyState.FromDictionary(BuildPartyPayload(first, second)) == null,
            "Two charged setups on one member should fail V1 strict self-use state."
        );
    }

    private void TestCurrentPartyPayloadRequiresContingencySetups()
    {
        GDictionary partyPayload = BuildPartyPayload(
            BuildSetupPayload(
                "setup_present",
                charged: false,
                reservedMpMax: 0,
                materialCosts: new GArray()
            )
        );
        GDictionary memberPayload = partyPayload["member_states"]
            .AsGodotDictionary()["hero_001"]
            .AsGodotDictionary();
        memberPayload.Remove("contingency_matrix_setups");

        _test.True(
            PartyState.FromDictionary(partyPayload) == null,
            "Current PartyState.version 7 member payload should require contingency_matrix_setups."
        );
    }

    private void TestRejectsUnknownFieldsInEveryContingencyPayload()
    {
        ExpectSetupRejected("setup unknown", setup => setup["unknown"] = true);
        ExpectSetupRejected("trigger unknown", setup => setup["trigger"].AsGodotDictionary()["unknown"] = true);
        ExpectSetupRejected(
            "resolver unknown",
            setup => FirstStoredSpell(setup)["target_resolver"].AsGodotDictionary()["unknown"] = true
        );
        ExpectSetupRejected("stored spell unknown", setup => FirstStoredSpell(setup)["unknown"] = true);
        ExpectSetupRejected(
            "material cost unknown",
            setup =>
            {
                setup["charged"] = true;
                setup["reserved_mp_max"] = 5;
                setup["material_costs"] = new GArray { BuildMaterialCostPayload(extraField: true) };
            }
        );
    }

    private void TestAllAuthoritativeResolverTypesParse()
    {
        string[] simpleResolvers =
        {
            "self",
            "trigger_source",
            "trigger_target",
            "nearest_enemy_to_owner",
            "nearest_enemy_to_trigger_cell",
            "owner_centered_area",
            "attacker_cell",
        };
        foreach (string resolverType in simpleResolvers)
        {
            GDictionary resolverPayload = new() { ["type"] = resolverType };
            ContingencyTargetResolverState resolver =
                ContingencyTargetResolverState.FromDictionary(resolverPayload);
            _test.True(resolver != null, $"{resolverType} resolver should parse with exact fields.");
            _test.True(
                HasExactKeys(resolver?.ToDictionary(), new[] { "type" }),
                $"{resolverType} resolver should round-trip exact fields."
            );
        }

        GDictionary emptyCellResolver = new()
        {
            ["type"] = "empty_cell_near_owner",
            ["preference"] = "safe_cell",
            ["max_distance"] = 4,
        };
        ContingencyTargetResolverState emptyCell =
            ContingencyTargetResolverState.FromDictionary(emptyCellResolver);
        _test.True(emptyCell != null, "empty_cell_near_owner resolver should parse exact fields.");
        _test.True(
            HasExactKeys(emptyCell?.ToDictionary(), new[] { "type", "preference", "max_distance" }),
            "empty_cell_near_owner resolver should round-trip exact fields."
        );
    }

    private void TestSafeCellIsOnlyEmptyCellPreference()
    {
        _test.True(
            ContingencyTargetResolverState.FromDictionary(new GDictionary { ["type"] = "safe_cell" })
                == null,
            "safe_cell should not parse as target_resolver.type."
        );
        _test.True(
            ContingencyTargetResolverState.FromDictionary(
                new GDictionary
                {
                    ["type"] = "empty_cell_near_owner",
                    ["preference"] = "safe_cell",
                    ["max_distance"] = 3,
                }
            ) != null,
            "safe_cell should parse only as empty_cell_near_owner.preference."
        );
    }

    private void TestOldRootAndPartyVersionsReject()
    {
        GDictionary partyPayload = BuildPartyPayload(
            BuildSetupPayload(
                "setup_version_gate",
                charged: false,
                reservedMpMax: 0,
                materialCosts: new GArray()
            )
        );
        GDictionary oldPartyPayload = (GDictionary)partyPayload.Duplicate(true);
        oldPartyPayload["version"] = 6;
        _test.True(
            PartyState.FromDictionary(oldPartyPayload) == null,
            "PartyState.version 6 should fail without migration."
        );

        var serializer = new SaveSerializer();
        serializer.Setup(11, 3, 4);
        GDictionary saveMeta = serializer.BuildSaveMeta(
            "save_contingency_schema",
            "Schema Test",
            "res://data/configs/world_map/default_world_generation.tres",
            "default",
            "Default",
            new Vector2I(8, 8),
            100,
            100
        );
        GDictionary payload = serializer.BuildSavePayload(
            "save_contingency_schema",
            "res://data/configs/world_map/default_world_generation.tres",
            saveMeta,
            BuildWorldDataPayload(),
            Vector2I.Zero,
            "player",
            PartyState.FromDictionary(partyPayload),
            100
        );
        payload["version"] = 10;
        GDictionary decoded = serializer.DecodePayload(
            payload,
            "res://data/configs/world_map/default_world_generation.tres",
            new WorldMapGenerationConfig(),
            saveMeta
        );
        _test.True(
            decoded["error"].AsInt32() != (int)Error.Ok,
            "Root save version 10 should fail without migration."
        );
    }

    private void TestParameterBindingsAcceptOnlyFlatSupportedValues()
    {
        GDictionary bindings = new()
        {
            ["bool_value"] = true,
            ["int_value"] = 3,
            ["float_value"] = 1.5f,
            ["string_value"] = "fire",
            ["string_name_value"] = new StringName("ice"),
            ["string_name_array_value"] = new GArray { "fire", new StringName("ice") },
        };
        GDictionary spell = BuildStoredSpellPayload("mage_mirror_image", 1, 1);
        spell["parameter_bindings"] = bindings;
        _test.True(
            ContingencyStoredSpellEntryState.FromDictionary(spell) != null,
            "parameter_bindings should accept bool, int, float, String/StringName, and Array[StringName]."
        );

        GDictionary badNested = BuildStoredSpellPayload("mage_mirror_image", 1, 1);
        badNested["parameter_bindings"] = new GDictionary
        {
            ["nested"] = new GDictionary { ["not_allowed"] = true },
        };
        _test.True(
            ContingencyStoredSpellEntryState.FromDictionary(badNested) == null,
            "parameter_bindings should reject nested Dictionary values."
        );

        GDictionary badArray = BuildStoredSpellPayload("mage_mirror_image", 1, 1);
        badArray["parameter_bindings"] = new GDictionary
        {
            ["array"] = new GArray { "ok", 3 },
        };
        _test.True(
            ContingencyStoredSpellEntryState.FromDictionary(badArray) == null,
            "parameter_bindings Array values should contain only StringName-compatible values."
        );
    }

    private void TestParameterBindingArraysRoundTripAsStringNames()
    {
        GDictionary spell = BuildStoredSpellPayload("mage_mirror_image", 1, 1);
        spell["parameter_bindings"] = new GDictionary
        {
            ["damage_tags"] = new GArray { "fire", new StringName("ice") },
        };

        ContingencyStoredSpellEntryState restored =
            ContingencyStoredSpellEntryState.FromDictionary(spell);
        GDictionary roundTrip = restored?.ToDictionary();
        GArray tags = roundTrip?["parameter_bindings"].AsGodotDictionary()["damage_tags"].AsGodotArray();

        _test.True(restored != null, "parameter binding Array[StringName] should parse.");
        _test.True(tags != null && tags.Count == 2, "parameter binding array should round-trip.");
        if (tags != null && tags.Count == 2)
        {
            _test.Eq(
                tags[0].VariantType,
                Variant.Type.StringName,
                "parameter binding array item 0 should round-trip as StringName."
            );
            _test.Eq(
                tags[1].VariantType,
                Variant.Type.StringName,
                "parameter binding array item 1 should round-trip as StringName."
            );
        }
    }

    private void ExpectSetupRejected(string label, Action<GDictionary> mutate)
    {
        GDictionary setup = BuildSetupPayload(
            $"setup_{label.Replace(" ", "_")}",
            charged: false,
            reservedMpMax: 0,
            materialCosts: new GArray()
        );
        mutate(setup);
        _test.True(
            PartyState.FromDictionary(BuildPartyPayload(setup)) == null,
            $"{label} field should reject setup payload."
        );
    }

    private static GDictionary FirstStoredSpell(GDictionary setup) =>
        setup["stored_spells"].AsGodotArray()[0].AsGodotDictionary();

    private static GDictionary BuildPartyPayload(params GDictionary[] setupPayloads)
    {
        PartyMemberState memberState = BuildMemberState();
        GDictionary memberPayload = memberState.ToDictionary();
        GArray setupArray = new();
        foreach (GDictionary setupPayload in setupPayloads)
            setupArray.Add(setupPayload);
        memberPayload["contingency_matrix_setups"] = setupArray;

        PartyState partyState = new()
        {
            version = 7,
            gold = 25,
            leader_member_id = "hero_001",
            main_character_member_id = "hero_001",
        };
        partyState.SetMemberState(memberState);
        partyState.active_member_ids.Add("hero_001");
        GDictionary partyPayload = partyState.ToDictionary();
        partyPayload["version"] = 7;
        partyPayload["member_states"] = new GDictionary { ["hero_001"] = memberPayload };
        return partyPayload;
    }

    private static PartyMemberState BuildMemberState()
    {
        UnitProgress progress = new()
        {
            unit_id = "hero_001",
            display_name = "Schema Hero",
            character_level = 7,
            unit_base_attributes = new UnitBaseAttributes
            {
                strength = 10,
                agility = 10,
                constitution = 10,
                perception = 10,
                intelligence = 16,
                willpower = 12,
            },
        };
        PartyMemberState memberState = new()
        {
            member_id = "hero_001",
            display_name = "Schema Hero",
            progression = progress,
            current_hp = 20,
            current_mp = 18,
            current_aura = 0,
        };
        return memberState;
    }

    private static GDictionary BuildSetupPayload(
        string setupId,
        bool charged,
        int reservedMpMax,
        GArray materialCosts
    )
    {
        return new GDictionary
        {
            ["setup_id"] = setupId,
            ["display_name"] = "Emergency Matrix",
            ["enabled"] = true,
            ["charged"] = charged,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = 5,
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = reservedMpMax,
            ["material_costs"] = materialCosts,
            ["trigger"] = new GDictionary
            {
                ["type"] = "hp_below_percent",
                ["subject"] = "owner",
                ["percent"] = 30,
                ["crossing_only"] = true,
                ["timing"] = "after_hp_changed",
            },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray { BuildStoredSpellPayload("mage_mirror_image", 2, 1) },
        };
    }

    private static GDictionary BuildStoredSpellPayload(string skillId, int castLevel, int order)
    {
        return new GDictionary
        {
            ["stored_skill_id"] = skillId,
            ["cast_level"] = castLevel,
            ["order"] = order,
            ["target_resolver"] = new GDictionary { ["type"] = "self" },
            ["parameter_bindings"] = new GDictionary(),
            ["fallback_policy"] = "skip_if_invalid",
        };
    }

    private static GDictionary BuildMaterialCostPayload(bool extraField = false)
    {
        GDictionary payload = new()
        {
            ["item_id"] = "special_contingency_gem",
            ["quantity"] = 1,
        };
        if (extraField)
            payload["unknown"] = true;
        return payload;
    }

    private static GDictionary BuildWorldDataPayload()
    {
        return new GDictionary
        {
            ["map_seed"] = 1,
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = new GArray(),
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["resource_nodes"] = new GArray(),
            ["mounted_submaps"] = new GDictionary(),
        };
    }

    private static string[] SetupKeys() => new[]
    {
        "setup_id",
        "display_name",
        "enabled",
        "charged",
        "source_skill_id",
        "source_skill_level",
        "matrix_load",
        "reserved_mp_max",
        "material_costs",
        "trigger",
        "release_mode",
        "stored_spells",
    };

    private static bool HasExactKeys(GDictionary payload, string[] expectedKeys)
    {
        if (payload == null || payload.Count != expectedKeys.Length)
            return false;
        foreach (string key in expectedKeys)
            if (!payload.ContainsKey(key))
                return false;
        return true;
    }
}
