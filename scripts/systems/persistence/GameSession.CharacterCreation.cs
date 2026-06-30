using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of GameSession — new-game character/party defaults + starting skill/weapon grants.
// Pure physical split: same class, no behavior change. See GameSession.cs.
public partial class GameSession
{

    private int ApplyCharacterCreationPayloadToMainCharacter(GDictionary payload)
    {
        if (payload == null || payload.Count == 0)
            return (int)Error.Ok;
        if (_party_state == null)
        {
            throw new InvalidOperationException(
                "GameSession cannot apply character creation payload without party state."
            );
        }

        StringName mainMemberId = _party_state.GetResolvedMainCharacterMemberId();
        if (mainMemberId == "")
        {
            throw new InvalidOperationException(
                "GameSession cannot apply character creation payload without a resolvable main character."
            );
        }

        PartyMemberState memberState = _party_state.GetMemberState(mainMemberId);
        UnitProgress progression = memberState?.progression as UnitProgress;
        if (memberState == null || progression == null || progression.unit_base_attributes == null)
        {
            throw new InvalidOperationException(
                $"GameSession cannot apply character creation payload because main character {mainMemberId} is incomplete."
            );
        }

        if (
            !CharacterCreationService.ApplyCharacterCreationPayloadToMemberForContentSource(
                memberState,
                payload,
                _progression_content_registry,
                new CharacterCreationOptions(bakeRerollLuck: true)
            )
        )
        {
            PushSessionError(
                "session.character_creation.invalid_payload",
                $"GameSession rejected invalid character creation payload for main character {mainMemberId}.",
                Json.Stringify(new GDictionary { ["member_id"] = mainMemberId })
            );
            return (int)Error.InvalidData;
        }

        RevokeOrphanRacialSkills(_party_state);
        BackfillRacialGrantedSkills(_party_state);
        return (int)Error.Ok;
    }

    private void ApplyCharacterCreationIdentityPayload(
        PartyMemberState member_state,
        GDictionary payload
    )
    {
        if (member_state == null || payload == null)
            return;
        member_state.SetIdentity(
            ReadPayloadStringName(payload, "race_id", member_state.race_id, false),
            ReadPayloadStringName(payload, "subrace_id", member_state.subrace_id, false)
        );
        member_state.SetAgeProjection(
            ReadPayloadNonnegativeInt(payload, "age_years", member_state.age_years),
            member_state.biological_age_years,
            member_state.astral_memory_years,
            ReadPayloadNonnegativeInt(
                payload,
                "birth_at_world_step",
                member_state.birth_at_world_step
            )
        );
        member_state.SetAgeStageProjection(
            ReadPayloadStringName(payload, "age_profile_id", member_state.age_profile_id, false),
            ReadPayloadStringName(
                payload,
                "natural_age_stage_id",
                member_state.natural_age_stage_id,
                false
            ),
            ReadPayloadStringName(
                payload,
                "effective_age_stage_id",
                member_state.effective_age_stage_id,
                false
            ),
            ReadPayloadStringName(
                payload,
                "effective_age_stage_source_type",
                member_state.effective_age_stage_source_type,
                true
            ),
            ReadPayloadStringName(
                payload,
                "effective_age_stage_source_id",
                member_state.effective_age_stage_source_id,
                true
            )
        );
        member_state.SetBodySizeCategory(
            ReadPayloadStringName(payload, "body_size_category", member_state.body_size_category, false)
        );
        member_state.SetVersatilityPick(
            ReadPayloadStringName(payload, "versatility_pick", member_state.versatility_pick, true)
        );
        if (
            payload.ContainsKey("active_stage_advancement_modifier_ids")
            && HasArray(payload, "active_stage_advancement_modifier_ids")
        )
            member_state.SetActiveStageAdvancementModifierIds(
                ProgressionDataUtils.to_string_name_array(
                    payload["active_stage_advancement_modifier_ids"]
                )
            );
        member_state.SetBloodline(
            ReadPayloadStringName(payload, "bloodline_id", member_state.bloodline_id, true),
            ReadPayloadStringName(
                payload,
                "bloodline_stage_id",
                member_state.bloodline_stage_id,
                true
            )
        );
        StringName ascensionId = ReadPayloadStringName(
            payload,
            "ascension_id",
            member_state.ascension_id,
            true
        );
        StringName ascensionStageId = ReadPayloadStringName(
            payload,
            "ascension_stage_id",
            member_state.ascension_stage_id,
            true
        );
        int ascensionStartedAtWorldStep =
            payload.ContainsKey("ascension_started_at_world_step")
            && HasInt(payload, "ascension_started_at_world_step")
                ? Mathf.Max(
                payload["ascension_started_at_world_step"].AsInt32(),
                -1
            )
                : member_state.ascension_started_at_world_step;
        StringName originalRaceIdBeforeAscension = ReadPayloadStringName(
            payload,
            "original_race_id_before_ascension",
            member_state.original_race_id_before_ascension,
            true
        );
        member_state.SetAscension(
            ascensionId,
            ascensionStageId,
            ascensionStartedAtWorldStep,
            originalRaceIdBeforeAscension
        );
        member_state.SetAgeProjection(
            member_state.age_years,
            ReadPayloadNonnegativeInt(
                payload,
                "biological_age_years",
                member_state.biological_age_years
            ),
            ReadPayloadNonnegativeInt(
                payload,
                "astral_memory_years",
                member_state.astral_memory_years
            ),
            member_state.birth_at_world_step
        );
        RefreshMemberBodySizeFromIdentity(member_state);
    }

    private void ApplyInitialHpFormula(PartyMemberState member_state)
    {
        if (member_state?.progression is not UnitProgress progression)
            return;
        UnitBaseAttributes attributes = progression.unit_base_attributes;
        if (attributes == null)
            return;
        int constitution = attributes.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int initialHpMax = CharacterCreationService.CalculateInitialHpMax(constitution);
        attributes.SetAttributeValue(AttributeService.ToStringName(AttributeIdKind.HpMax), initialHpMax);
        member_state.SetCurrentHp(initialHpMax);
    }

    private bool RefreshMemberBodySizeFromIdentity(PartyMemberState member_state)
    {
        StringName category = ResolveBodySizeCategoryForMember(member_state);
        if (category == "")
            return false;
        int resolvedBodySize = BodySizeContentRules.GetBodySizeForCategory(category);
        if (
            member_state.body_size_category == category
            && member_state.body_size == resolvedBodySize
        )
            return false;
        return member_state.SetBodySizeCategory(category);
    }

    private StringName ResolveBodySizeCategoryForMember(PartyMemberState member_state)
    {
        if (member_state == null || _progression_content_registry == null)
            return "";
        IReadOnlyDictionary<StringName, AscensionStageDef> ascensionStageDefs =
            _progression_content_registry.GetAscensionStageDefsTyped();
        ascensionStageDefs.TryGetValue(
            member_state.ascension_stage_id,
            out AscensionStageDef ascensionStageDef
        );
        if (
            ascensionStageDef != null
            && ascensionStageDef.body_size_category_override != ""
            && BodySizeContentRules.IsValidBodySizeCategory(
                ascensionStageDef.body_size_category_override
            )
        )
        {
            return ascensionStageDef.body_size_category_override;
        }
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs =
            _progression_content_registry.GetSubraceDefsTyped();
        subraceDefs.TryGetValue(member_state.subrace_id, out SubraceDef subraceDef);
        if (
            subraceDef != null
            && subraceDef.body_size_category_override != ""
            && BodySizeContentRules.IsValidBodySizeCategory(subraceDef.body_size_category_override)
        )
        {
            return subraceDef.body_size_category_override;
        }
        IReadOnlyDictionary<StringName, RaceDef> raceDefs =
            _progression_content_registry.GetRaceDefsTyped();
        raceDefs.TryGetValue(member_state.race_id, out RaceDef raceDef);
        if (
            raceDef != null
            && BodySizeContentRules.IsValidBodySizeCategory(raceDef.body_size_category)
        )
            return raceDef.body_size_category;
        return "";
    }

    private StringName ReadPayloadStringName(
        GDictionary payload,
        string field_name,
        StringName fallback,
        bool allow_empty
    )
    {
        if (payload == null || !payload.ContainsKey(field_name))
            return fallback;
        if (!HasString(payload, field_name))
            return fallback;
        StringName parsed = GetStringName(payload, field_name);
        if (parsed == "" && !allow_empty)
            return fallback;
        return parsed;
    }

    private int ReadPayloadNonnegativeInt(GDictionary payload, string field_name, int fallback)
    {
        if (
            payload == null
            || !payload.ContainsKey(field_name)
            || !HasInt(payload, field_name)
        )
            return fallback;
        return Mathf.Max(payload[field_name].AsInt32(), 0);
    }

    private PartyState CreateDefaultPartyState()
    {
        var partyState = new PartyState();
        partyState.gold = 180;

        PartyMemberState swordMember = BuildDefaultMemberState(
            "player_sword_01",
            "剑士",
            "warrior_heavy_strike",
            "portrait_sword",
            0,
            4,
            2,
            3,
            1,
            1,
            1,
            12
        );

        partyState.SetMemberState(swordMember);
        partyState.leader_member_id = "player_sword_01";
        partyState.main_character_member_id = "player_sword_01";
        partyState.active_member_ids = ProgressionDataUtils.to_string_name_array(
            new GArray { "player_sword_01" }
        );
        partyState.reserve_member_ids = ProgressionDataUtils.to_string_name_array(new GArray());
        return partyState;
    }

    private PartyMemberState BuildDefaultMemberState(
        StringName member_id,
        string display_name,
        StringName starting_skill_id,
        StringName portrait_id,
        int current_mp,
        int strength,
        int agility,
        int constitution,
        int perception,
        int intelligence,
        int willpower,
        int storage_space = 0
    )
    {
        var memberState = new PartyMemberState
        {
            member_id = member_id,
            display_name = display_name,
            faction_id = "player",
            portrait_id = portrait_id,
            control_mode = "manual",
        };
        memberState.SetCurrentMp(current_mp);
        memberState.SetIdentity("human", "common_human");
        memberState.SetAgeProjection(24, 24, 0, 0);
        memberState.SetAgeStageProjection("human_age_profile", "adult", "adult", "", "");
        memberState.SetBodySizeCategory("medium");
        memberState.SetVersatilityPick("");
        memberState.SetActiveStageAdvancementModifierIds(Array.Empty<StringName>());
        memberState.ClearBloodline();
        memberState.ClearAscension();

        var progression = new UnitProgress
        {
            unit_id = member_id,
            display_name = display_name,
            character_level = 0,
        };

        var unitBaseAttributes = new UnitBaseAttributes
        {
            strength = strength,
            agility = agility,
            constitution = constitution,
            perception = perception,
            intelligence = intelligence,
            willpower = willpower,
        };
        int initialHpMax = CharacterCreationService.CalculateInitialHpMax(constitution);
        unitBaseAttributes.custom_stats["hp_max"] = initialHpMax;
        unitBaseAttributes.custom_stats["mp_max"] = current_mp;
        unitBaseAttributes.custom_stats["storage_space"] = Mathf.Max(storage_space, 0);
        memberState.SetCurrentHp(initialHpMax);
        progression.unit_base_attributes = unitBaseAttributes;

        var starterSkill = new UnitSkillProgress
        {
            skill_id = starting_skill_id,
            is_learned = true,
            is_core = true,
            assigned_profession_id = "warrior",
            granted_source_type = UnitSkillProgress.ToStringName(
                UnitSkillGrantSourceType.Profession
            ),
            granted_source_id = "warrior",
        };
        progression.SetSkillProgress(starterSkill);

        var warriorProgress = new UnitProfessionProgress
        {
            profession_id = "warrior",
            rank = 0,
            is_active = false,
        };
        warriorProgress.AddCoreSkill(starting_skill_id);
        progression.SetProfessionProgress(warriorProgress);
        SkillDefinition randomStartingSkillDefinition = GrantRandomStartingBookSkill(progression);
        RefreshProgressionRuntimeState(progression);

        memberState.progression = progression;
        EquipStartingWeaponForSkill(memberState, randomStartingSkillDefinition);
        return memberState;
    }

    private SkillDefinition GrantRandomStartingBookSkill(UnitProgress progression)
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            GetRandomStartSkillDefinitions();
        if (progression == null || skillDefinitions.Count == 0)
            return null;

        GStringNameArray eligibleSkillIds = new();
        foreach (StringName skillId in GetSortedSkillDefinitionIds(skillDefinitions))
        {
            SkillDefinition skillDefinition = skillDefinitions.TryGetValue(
                skillId,
                out SkillDefinition resolvedSkillDefinition
            )
                ? resolvedSkillDefinition
                : null;
            if (!IsRandomStartBookSkillCandidate(skillDefinition, progression))
                continue;
            eligibleSkillIds.Add(skillId);
        }

        if (eligibleSkillIds.Count == 0)
            return null;

        StringName selectedSkillId = eligibleSkillIds[
            TrueRandomSeedService.RandiRange(0, eligibleSkillIds.Count - 1)
        ];
        SkillDefinition selectedSkillDefinition = skillDefinitions.TryGetValue(
            selectedSkillId,
            out SkillDefinition resolvedSelectedSkillDefinition
        )
            ? resolvedSelectedSkillDefinition
            : null;
        if (selectedSkillDefinition == null)
            return null;

        UnitSkillProgress skillProgress = progression.GetSkillProgress(selectedSkillId);
        if (skillProgress == null)
        {
            skillProgress = new UnitSkillProgress();
            skillProgress.skill_id = selectedSkillId;
        }

        skillProgress.is_learned = true;
        skillProgress.granted_source_type = UnitSkillProgress.ToStringName(
            UnitSkillGrantSourceType.Player
        );
        skillProgress.granted_source_id = "";
        skillProgress.skill_level = ResolveRandomStartSkillInitialLevel(selectedSkillDefinition);
        skillProgress.current_mastery = 0;
        skillProgress.total_mastery_earned = 0;
        progression.SetSkillProgress(skillProgress);
        return selectedSkillDefinition;
    }

    private void EquipStartingWeaponForSkill(
        PartyMemberState member_state,
        SkillDefinition skillDefinition
    )
    {
        if (member_state?.equipment_state == null)
            return;
        StringName itemId = ResolveStartingWeaponItemIdForSkill(skillDefinition);
        if (itemId == "")
            return;
        ItemDef itemDef = GetObject<ItemDef>(_item_defs, itemId);
        if (itemDef == null || !itemDef.IsWeapon())
            return;
        StringName instanceId = AllocateEquipmentInstanceId();
        if (instanceId == "")
            return;
        EquipmentInstanceState equipmentInstance = EquipmentInstanceState.CreateInstance(
            itemId,
            instanceId
        );
        IReadOnlyList<StringName> occupiedSlots = itemDef.GetFinalOccupiedSlotIdsTyped(
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand)
        );
        member_state.equipment_state.SetEquippedEntry(
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
            itemId,
            occupiedSlots,
            equipmentInstance
        );
    }

    private StringName ResolveStartingWeaponItemIdForSkill(SkillDefinition skillDefinition)
    {
        GStringNameArray candidates = new();
        if (
            SkillMatchesStartingWeaponType(
                skillDefinition,
                new GStringNameArray { "crossbow" },
                new GStringArray { "crossbow" }
            )
        )
            candidates.Add(StartingCrossbowWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skillDefinition,
                new GStringNameArray { "archer", "bow" },
                new GStringArray { "archer_" }
            )
        )
            candidates.Add(StartingArcherWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skillDefinition,
                new GStringNameArray { "mage", "magic", "spell" },
                new GStringArray { "mage_" }
            )
        )
            candidates.Add(StartingMageWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skillDefinition,
                new GStringNameArray { "priest", "faith", "heal" },
                new GStringArray { "priest_", "saint_" }
            )
        )
            candidates.Add(StartingPriestWeaponItemId);
        if (
            SkillMatchesStartingWeaponType(
                skillDefinition,
                new GStringNameArray { "warrior", "melee", "shield" },
                new GStringArray { "warrior_" }
            )
        )
            candidates.Add(StartingMeleeWeaponItemId);
        candidates.Add(StartingMeleeWeaponItemId);
        return FirstValidStartingWeaponItemId(candidates);
    }

    private bool SkillMatchesStartingWeaponType(
        SkillDefinition skillDefinition,
        GStringNameArray tag_ids,
        GStringArray skill_id_prefixes
    )
    {
        if (skillDefinition == null)
            return false;
        foreach (StringName tagId in tag_ids)
        {
            if (skillDefinition.HasTag(tagId))
                return true;
        }
        string skillIdText = skillDefinition.SkillId.ToString();
        foreach (string prefix in skill_id_prefixes)
        {
            if (skillIdText.StartsWith(prefix))
                return true;
        }
        return false;
    }

    private StringName FirstValidStartingWeaponItemId(GStringNameArray candidates)
    {
        foreach (StringName itemId in candidates)
        {
            if (itemId == "")
                continue;
            ItemDef itemDef = GetObject<ItemDef>(_item_defs, itemId);
            if (itemDef != null && itemDef.IsWeapon())
                return itemId;
        }
        return "";
    }

    private void RefreshProgressionRuntimeState(UnitProgress progression)
    {
        if (progression == null)
            return;
        var progressionService = new ProgressionService();
        progressionService.SetupDefinitions(
            progression,
            GetRandomStartSkillDefinitions(),
            BuildProfessionDefIndex(_profession_defs)
        );
        progressionService.RefreshRuntimeState();
    }

    private IReadOnlyDictionary<StringName, SkillDefinition> GetRandomStartSkillDefinitions()
    {
        return GetSkillDefinitionsTyped();
    }

    private static List<StringName> GetSortedSkillDefinitionIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var sortedSkillIds = new List<StringName>(skillDefinitions?.Keys ?? Array.Empty<StringName>());
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return sortedSkillIds;
    }

    private bool IsRandomStartBookSkillCandidate(
        SkillDefinition skillDefinition,
        UnitProgress progression
    )
    {
        if (skillDefinition == null || skillDefinition.SkillId == "")
            return false;
        if (skillDefinition.LearnSourceKind != SkillLearnSourceKind.Book)
            return false;
        if (skillDefinition.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            return false;
        if (
            skillDefinition.LearnRequirements.Count > 0
            || skillDefinition.KnowledgeRequirements.Count > 0
            || skillDefinition.SkillLevelRequirements.Count > 0
            || skillDefinition.AttributeRequirements.Count > 0
            || skillDefinition.AchievementRequirements.Count > 0
        )
        {
            return false;
        }
        UnitSkillProgress learnedProgress = progression?.GetSkillProgress(skillDefinition.SkillId);
        return learnedProgress == null || !learnedProgress.is_learned;
    }

    public int ResolveRandomStartSkillInitialLevel(SkillDefinition skillDefinition)
    {
        return ResolveRandomStartSkillInitialLevel(skillDefinition, null);
    }

    private int ResolveRandomStartSkillInitialLevel(
        SkillDefinition skillDefinition,
        UnitProgress progression
    )
    {
        if (skillDefinition == null)
            return 0;
        int mappedLevel = RandomStartSkillLevelByTier.TryGetValue(
            ResolveRandomStartSkillTier(skillDefinition),
            out int configuredLevel
        )
            ? configuredLevel
            : 0;
        int maxInitialLevel =
            skillDefinition.MaxLevel >= 0 ? Mathf.Max(skillDefinition.MaxLevel, 0) : 999;
        if (progression != null && skillDefinition.DynamicMaxLevelStatId != "")
        {
            int effectiveMax = SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(
                skillDefinition,
                null,
                progression
            );
            if (effectiveMax > 0)
                maxInitialLevel = Mathf.Min(maxInitialLevel, effectiveMax);
        }
        if (skillDefinition.NonCoreMaxLevel > 0)
            maxInitialLevel = Mathf.Min(maxInitialLevel, skillDefinition.NonCoreMaxLevel);
        return Mathf.Clamp(mappedLevel, 0, maxInitialLevel);
    }

    private StringName ResolveRandomStartSkillTier(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null)
            return RandomStartSkillTierBasic;

        string description = skillDefinition.Description ?? "";
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsUltimate))
            return RandomStartSkillTierUltimate;
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsAdvanced))
            return RandomStartSkillTierAdvanced;
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsIntermediate))
            return RandomStartSkillTierIntermediate;
        if (DescriptionContainsAnyKeyword(description, RandomStartSkillKeywordsBasic))
            return RandomStartSkillTierBasic;

        int tierScore = BuildRandomStartSkillTierScore(skillDefinition);
        if (tierScore >= 14)
            return RandomStartSkillTierUltimate;
        if (tierScore >= 9)
            return RandomStartSkillTierAdvanced;
        if (tierScore >= 6)
            return RandomStartSkillTierIntermediate;
        return RandomStartSkillTierBasic;
    }

    private bool DescriptionContainsAnyKeyword(string description, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if ((description ?? "").Contains(keyword))
                return true;
        }
        return false;
    }

    private int BuildRandomStartSkillTierScore(SkillDefinition skillDefinition)
    {
        if (skillDefinition?.CombatProfile == null)
            return 0;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        int score = 0;
        score += combatProfile.ApCost * 2;
        score += combatProfile.MpCost;
        score += combatProfile.StaminaCost;
        score += combatProfile.AuraCost * 2;
        score += Mathf.Max(combatProfile.CooldownTu / 5 - 1, 0);
        if (combatProfile.TargetModeKind == BattleTargetMode.Ground)
            score += 1;
        var areaPattern = BattleTypedNames.ToAreaPattern(combatProfile.AreaPattern);
        if (areaPattern != BattleAreaPattern.Unknown && areaPattern != BattleAreaPattern.Single)
            score += 1;
        if (skillDefinition.HasTag("aoe"))
            score += 1;
        if (skillDefinition.HasTag("finisher"))
            score += 2;
        if (skillDefinition.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            score += 2;
        return score;
    }
}
