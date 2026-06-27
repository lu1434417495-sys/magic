using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_contingency_content_validator_regression : SceneTree
{
    private const string TestWorldConfig =
        "res://data/configs/world_map/test_world_map_config.tres";
    private const int SaveCompressionMode = (int)FileAccess.CompressionMode.Zstd;

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCatalogContainsRealChainContingencySkill();
        TestCatalogContainsV1StorableAutomationProfiles();
        TestStoredSkillWithoutAutomationProfileIsRejected();
        TestCanBeStoredFalseIsRejected();
        TestMinSkillLevelGreaterThanSourceSkillLevelIsRejected();
        TestSourceSkillLevelAboveKnownLevelIsRejected();
        TestStoredCastLevelAboveKnownLevelIsRejected();
        TestForbiddenTagIntersectionRejectsBeforeAllowlistSuccess();
        TestTargetResolverOutsideAllowlistIsRejected();
        TestUnsupportedParameterBindingKeyIsRejected();
        TestLoadSaveFailsWhenPersistedSetupReferencesInvalidStoredSkill();

        Quit(_test.Finish("Contingency content validator regression"));
    }

    private void TestCatalogContainsRealChainContingencySkill()
    {
        using GameSession session = new();
        IReadOnlyDictionary<StringName, SkillDefinition> skills =
            session.GetContentCatalogTyped().GetSkillDefinitionsTyped();

        _test.True(
            skills.TryGetValue("mage_chain_contingency", out SkillDefinition chainSkill),
            "Catalog should contain real mage_chain_contingency skill definition."
        );
        _test.True(
            chainSkill != null && chainSkill.HasTag("contingency"),
            "mage_chain_contingency should declare contingency tag."
        );
        _test.True(
            chainSkill != null && chainSkill.HasTag("meta_spell"),
            "mage_chain_contingency should declare meta_spell tag."
        );
    }

    private void TestCatalogContainsV1StorableAutomationProfiles()
    {
        using GameSession session = new();
        IReadOnlyDictionary<StringName, SkillDefinition> skills =
            session.GetContentCatalogTyped().GetSkillDefinitionsTyped();

        ExpectProfile(
            skills,
            "mage_mirror_image",
            minLevel: 1,
            effectCategory: "defensive_self_buff",
            allowedResolver: "self"
        );
        ExpectProfile(
            skills,
            "mage_rock_armor",
            minLevel: 1,
            effectCategory: "defensive_self_buff",
            allowedResolver: "self"
        );
        ExpectProfile(
            skills,
            "mage_magic_shield",
            minLevel: 3,
            effectCategory: "shield",
            allowedResolver: "self"
        );
        ExpectProfile(
            skills,
            "mage_blink",
            minLevel: 7,
            effectCategory: "mobility",
            allowedResolver: "empty_cell_near_owner"
        );
        ExpectProfile(
            skills,
            "mage_thunderclap",
            minLevel: 5,
            effectCategory: "damage",
            allowedResolver: "owner_centered_area"
        );
        ExpectProfile(
            skills,
            "priest_aid",
            minLevel: 3,
            effectCategory: "shield",
            allowedResolver: "owner_centered_area"
        );
    }

    private void TestStoredSkillWithoutAutomationProfileIsRejected()
    {
        GDictionary setup = BuildSetupPayload(
            storedSkillId: "skill_without_profile",
            sourceSkillLevel: 5
        );
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 5),
            LearnedSkill("skill_without_profile", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill("skill_without_profile")
            )
        );

        ExpectErrorContains(
            errors,
            "automation_profile_missing",
            "Stored skill without automation profile should be rejected."
        );
    }

    private void TestCanBeStoredFalseIsRejected()
    {
        GDictionary setup = BuildSetupPayload(storedSkillId: "non_storable_skill");
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 5),
            LearnedSkill("non_storable_skill", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "non_storable_skill",
                    BuildAutomation(canBeStored: false, minLevel: 1, allowedResolver: "self")
                )
            )
        );

        ExpectErrorContains(
            errors,
            "not_storable",
            "can_be_stored_in_contingency=false should be rejected."
        );
    }

    private void TestMinSkillLevelGreaterThanSourceSkillLevelIsRejected()
    {
        GDictionary setup = BuildSetupPayload(
            storedSkillId: "high_gate_skill",
            sourceSkillLevel: 2
        );
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 2),
            LearnedSkill("high_gate_skill", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "high_gate_skill",
                    BuildAutomation(canBeStored: true, minLevel: 3, allowedResolver: "self")
                )
            )
        );

        ExpectErrorContains(
            errors,
            "source_skill_level_too_low",
            "Stored skill min_contingency_skill_level greater than source skill level should reject."
        );
    }

    private void TestSourceSkillLevelAboveKnownLevelIsRejected()
    {
        GDictionary setup = BuildSetupPayload(
            storedSkillId: "high_gate_skill",
            sourceSkillLevel: 7
        );
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 3),
            LearnedSkill("high_gate_skill", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "high_gate_skill",
                    BuildAutomation(canBeStored: true, minLevel: 7, allowedResolver: "self")
                )
            )
        );

        ExpectErrorContains(
            errors,
            "source_skill_level_exceeds_known_level",
            "Persisted source_skill_level above owner known level should reject."
        );
    }

    private void TestStoredCastLevelAboveKnownLevelIsRejected()
    {
        GDictionary setup = BuildSetupPayload(
            storedSkillId: "high_gate_skill",
            sourceSkillLevel: 7,
            castLevel: 5
        );
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 7),
            LearnedSkill("high_gate_skill", 2)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "high_gate_skill",
                    BuildAutomation(canBeStored: true, minLevel: 7, allowedResolver: "self")
                )
            )
        );

        ExpectErrorContains(
            errors,
            "stored_skill_cast_level_exceeds_known_level",
            "Persisted stored spell cast_level above owner known level should reject."
        );
    }

    private void TestForbiddenTagIntersectionRejectsBeforeAllowlistSuccess()
    {
        GDictionary setup = BuildSetupPayload(storedSkillId: "forbidden_tag_skill");
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 5),
            LearnedSkill("forbidden_tag_skill", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "forbidden_tag_skill",
                    BuildAutomation(
                        canBeStored: true,
                        minLevel: 1,
                        allowedResolver: "self",
                        tags: new[] { "contingency_forbidden", "defensive_self_buff" }
                    )
                )
            )
        );

        _test.True(errors.Count > 0, "Forbidden tag setup should produce validation errors.");
        if (errors.Count > 0)
        {
            _test.True(
                errors[0].Contains("forbidden_tag"),
                "Forbidden tag intersection should be reported before allowlist success."
            );
        }
    }

    private void TestTargetResolverOutsideAllowlistIsRejected()
    {
        GDictionary setup = BuildSetupPayload(
            storedSkillId: "self_only_skill",
            resolver: new GDictionary { ["type"] = "owner_centered_area" }
        );
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 5),
            LearnedSkill("self_only_skill", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "self_only_skill",
                    BuildAutomation(canBeStored: true, minLevel: 1, allowedResolver: "self")
                )
            )
        );

        ExpectErrorContains(
            errors,
            "target_resolver_not_allowed",
            "Target resolver absent from allowed_target_resolvers should be rejected."
        );
    }

    private void TestUnsupportedParameterBindingKeyIsRejected()
    {
        GDictionary setup = BuildSetupPayload(
            storedSkillId: "binding_contract_skill",
            parameterBindings: new GDictionary { ["unexpected_key"] = true }
        );
        PartyState partyState = BuildPartyStateWithSetup(
            setup,
            LearnedSkill("mage_chain_contingency", 5),
            LearnedSkill("binding_contract_skill", 5)
        );
        IReadOnlyList<string> errors = ContingencyContentValidator.ValidateAllSetupsForSaveLoad(
            partyState,
            BuildSyntheticSkillDefinitions(
                SyntheticSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
                SyntheticSkill(
                    "binding_contract_skill",
                    BuildAutomation(canBeStored: true, minLevel: 1, allowedResolver: "self")
                )
            )
        );

        ExpectErrorContains(
            errors,
            "unsupported_parameter_binding",
            "Unsupported persisted parameter binding key should be rejected."
        );
    }

    private void TestLoadSaveFailsWhenPersistedSetupReferencesInvalidStoredSkill()
    {
        GameSession session = new();
        try
        {
            Error createError = (Error)session.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "Load-time validator test should create a baseline save.");
            if (createError != Error.Ok)
                return;

            PartyState invalidPartyState = BuildPartyStateWithSetup(
                BuildSetupPayload(storedSkillId: "mage_chain_contingency"),
                LearnedSkill("mage_chain_contingency", 5)
            );
            GDictionary payload = BuildSavePayloadForSession(session, invalidPartyState);
            Error writeError = OverwriteActiveSavePayload(session, payload);
            _test.Eq(writeError, Error.Ok, "Load-time validator test should overwrite save payload.");
            if (writeError != Error.Ok)
                return;

            Error loadError = (Error)session.LoadSave(session.GetActiveSaveId());
            _test.Eq(
                loadError,
                Error.InvalidData,
                "LoadSave should fail when a persisted setup references invalid stored skill content."
            );
            GDictionary status = session.GetSaveStatus();
            _test.Eq(
                DictString(status, "last_error_reason"),
                "contingency_content_validation",
                "LoadSave should surface a stable content validation failure reason."
            );
        }
        finally
        {
            session.ClearPersistedGame();
            session.Dispose();
        }
    }

    private void ExpectProfile(
        IReadOnlyDictionary<StringName, SkillDefinition> skills,
        StringName skillId,
        int minLevel,
        StringName effectCategory,
        StringName allowedResolver
    )
    {
        _test.True(skills.TryGetValue(skillId, out SkillDefinition skillDefinition), $"{skillId} should be registered.");
        ContingencyAutomationDefinition profile = skillDefinition?.ContingencyAutomationProfile;
        _test.True(profile != null, $"{skillId} should declare a contingency automation profile.");
        if (profile == null)
            return;

        _test.True(
            profile.CanBeStoredInContingency,
            $"{skillId} should be storable in contingency."
        );
        _test.Eq(
            profile.MinContingencySkillLevel,
            minLevel,
            $"{skillId} should declare expected min_contingency_skill_level."
        );
        _test.Eq(
            profile.EffectCategory,
            effectCategory,
            $"{skillId} should declare expected effect_category."
        );
        _test.True(
            HasStringName(profile.AllowedTargetResolvers, allowedResolver),
            $"{skillId} should allow target resolver {allowedResolver}."
        );
        _test.False(
            profile.RequiresManualTargeting,
            $"{skillId} should not require manual targeting in V1."
        );
        _test.True(
            profile.AllowedParameterBindings != null,
            $"{skillId} should declare allowed_parameter_bindings as an explicit contract."
        );
        _test.Eq(
            profile.AllowedParameterBindings.Count,
            0,
            $"{skillId} V1 profile should use empty allowed_parameter_bindings."
        );
    }

    private void ExpectErrorContains(
        IReadOnlyList<string> errors,
        string expectedFragment,
        string message
    )
    {
        bool found = false;
        foreach (string error in errors)
        {
            if (error.Contains(expectedFragment, StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }
        if (!found)
            _test.Fail($"{message} | expected fragment={expectedFragment}");
    }

    private static IReadOnlyDictionary<StringName, SkillDefinition> BuildSyntheticSkillDefinitions(
        params SkillDefinition[] skills
    )
    {
        Dictionary<StringName, SkillDefinition> result = new();
        foreach (SkillDefinition skill in skills)
        {
            if (skill != null && skill.SkillId != "")
                result[skill.SkillId] = skill;
        }
        return result;
    }

    private static SkillDefinition SyntheticSkill(
        string skillId,
        ContingencyAutomationDefinition automation = null,
        string[] tags = null
    )
    {
        return new SkillDefinition(
            skillId,
            skillId,
            skillId,
            "",
            "passive",
            10,
            0,
            "",
            0,
            0,
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            ToStringNameList(tags),
            "",
            System.Array.Empty<StringName>(),
            "standard",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, Variant>>(),
            null,
            automation
        );
    }

    private static ContingencyAutomationDefinition BuildAutomation(
        bool canBeStored,
        int minLevel,
        string allowedResolver,
        string[] tags = null
    )
    {
        return new ContingencyAutomationDefinition(
            canBeStored,
            minLevel,
            "test_effect",
            ToStringNameList(tags),
            0,
            new[] { new StringName(allowedResolver) },
            false,
            new Dictionary<string, Variant>()
        );
    }

    private static IReadOnlyList<StringName> ToStringNameList(string[] values)
    {
        if (values == null || values.Length == 0)
            return System.Array.Empty<StringName>();
        var result = new List<StringName>(values.Length);
        foreach (string value in values)
            result.Add(value);
        return result;
    }

    private static PartyState BuildPartyStateWithSetup(
        GDictionary setupPayload,
        params UnitSkillProgress[] learnedSkills
    )
    {
        PartyMemberState memberState = BuildMemberState(learnedSkills);
        GDictionary memberPayload = memberState.ToDictionary();
        memberPayload["contingency_matrix_setups"] = new GArray { setupPayload };

        PartyState partyState = new()
        {
            version = 6,
            gold = 25,
            leader_member_id = "hero_001",
            main_character_member_id = "hero_001",
        };
        partyState.SetMemberState(memberState);
        partyState.active_member_ids.Add("hero_001");
        GDictionary partyPayload = partyState.ToDictionary();
        partyPayload["version"] = 6;
        partyPayload["member_states"] = new GDictionary { ["hero_001"] = memberPayload };
        return PartyState.FromDictionary(partyPayload);
    }

    private static PartyMemberState BuildMemberState(params UnitSkillProgress[] learnedSkills)
    {
        UnitProgress progress = new()
        {
            unit_id = "hero_001",
            display_name = "Content Hero",
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
        foreach (UnitSkillProgress skill in learnedSkills)
            progress.SetSkillProgress(skill);

        return new PartyMemberState
        {
            member_id = "hero_001",
            display_name = "Content Hero",
            progression = progress,
            current_hp = 20,
            current_mp = 18,
            current_aura = 0,
        };
    }

    private static UnitSkillProgress LearnedSkill(string skillId, int level)
    {
        return new UnitSkillProgress
        {
            skill_id = skillId,
            is_learned = true,
            skill_level = level,
            current_mastery = 0,
            total_mastery_earned = 0,
            is_core = false,
            granted_source_type = "player",
        };
    }

    private static GDictionary BuildSetupPayload(
        string storedSkillId,
        int sourceSkillLevel = 5,
        int castLevel = 1,
        GDictionary resolver = null,
        GDictionary parameterBindings = null
    )
    {
        return new GDictionary
        {
            ["setup_id"] = $"setup_{storedSkillId}",
            ["display_name"] = "Emergency Matrix",
            ["enabled"] = true,
            ["charged"] = false,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = sourceSkillLevel,
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = 0,
            ["material_costs"] = new GArray(),
            ["trigger"] = new GDictionary
            {
                ["type"] = "hp_below_percent",
                ["subject"] = "owner",
                ["percent"] = 30,
                ["crossing_only"] = true,
                ["timing"] = "after_hp_changed",
            },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray
            {
                new GDictionary
                {
                    ["stored_skill_id"] = storedSkillId,
                    ["cast_level"] = castLevel,
                    ["order"] = 1,
                    ["target_resolver"] = resolver ?? new GDictionary { ["type"] = "self" },
                    ["parameter_bindings"] = parameterBindings ?? new GDictionary(),
                    ["fallback_policy"] = "skip_if_invalid",
                },
            },
        };
    }

    private static GDictionary BuildSavePayloadForSession(
        GameSession gameSession,
        PartyState partyState
    )
    {
        return gameSession._save_serializer.BuildSavePayload(
            gameSession.GetActiveSaveId(),
            gameSession.GetGenerationConfigPath(),
            gameSession.GetActiveSaveMeta(),
            gameSession.GetWorldData(),
            gameSession.GetPlayerCoord(),
            gameSession.GetPlayerFactionId(),
            partyState,
            (int)Time.GetUnixTimeFromSystem()
        );
    }

    private static Error OverwriteActiveSavePayload(GameSession gameSession, GDictionary payload)
    {
        using FileAccess saveFile = FileAccess.OpenCompressed(
            gameSession.GetActiveSavePath(),
            FileAccess.ModeFlags.Write,
            (FileAccess.CompressionMode)SaveCompressionMode
        );
        if (saveFile == null)
            return FileAccess.GetOpenError();
        saveFile.StoreVar(payload, false);
        return Error.Ok;
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        if (values == null)
            return false;
        foreach (StringName value in values)
            if (value == target)
                return true;
        return false;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return "";
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.StringName
            ? value.AsStringName().ToString()
            : value.AsString();
    }
}
