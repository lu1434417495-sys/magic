using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// Stable text snapshots for regression diffing and agent/debug inspection.
// This renderer is a development aid, not a player-facing presentation layer.
public static class GameTextSnapshotRenderer
{
    public static string RenderFullSnapshot(GDictionary snapshot)
    {
        snapshot ??= new GDictionary();
        var sections = new List<string>();
        AppendSection(sections, "SESSION", BuildSessionLines(GetDictionary(snapshot, "session")));
        AppendSection(
            sections,
            "STATUS",
            BuildStatusLines(GetDictionary(snapshot, "status"), GetDictionary(snapshot, "modal"))
        );
        AppendSection(
            sections,
            "VALIDATION",
            BuildValidationLines(GetDictionary(snapshot, "validation"))
        );
        AppendSection(sections, "LOG", BuildLogLines(GetDictionary(snapshot, "logs")));
        AppendSection(sections, "WORLD", BuildWorldLines(GetDictionary(snapshot, "world")));
        AppendSection(sections, "SUBMAP", BuildSubmapLines(GetDictionary(snapshot, "submap")));
        AppendSection(
            sections,
            "GAME_OVER",
            BuildGameOverLines(GetDictionary(snapshot, "game_over"))
        );
        AppendSection(sections, "PARTY", BuildPartyLines(GetDictionary(snapshot, "party")));
        AppendSection(
            sections,
            "QUEST",
            BuildQuestLines(GetDictionary(GetDictionary(snapshot, "party"), "quests"))
        );
        AppendSection(
            sections,
            "SETTLEMENT",
            BuildSettlementLines(GetDictionary(snapshot, "settlement"))
        );
        AppendSection(
            sections,
            "CONTRACT_BOARD",
            BuildContractBoardLines(GetDictionary(snapshot, "contract_board"))
        );
        AppendSection(sections, "SHOP", BuildShopLines(GetDictionary(snapshot, "shop")));
        AppendSection(sections, "FORGE", BuildForgeLines(GetDictionary(snapshot, "forge")));
        AppendSection(
            sections,
            "STAGECOACH",
            BuildStagecoachLines(GetDictionary(snapshot, "stagecoach"))
        );
        AppendSection(
            sections,
            "CHARACTER",
            BuildCharacterLines(GetDictionary(snapshot, "character_info"))
        );
        AppendSection(
            sections,
            "WAREHOUSE",
            BuildWarehouseLines(GetDictionary(snapshot, "warehouse"))
        );
        AppendSection(sections, "BATTLE", BuildBattleLines(GetDictionary(snapshot, "battle")));
        AppendSection(sections, "LOOT", BuildLootLines(GetDictionary(snapshot, "loot")));
        AppendSection(sections, "REWARD", BuildRewardLines(GetDictionary(snapshot, "reward")));
        AppendSection(
            sections,
            "PROMOTION",
            BuildPromotionLines(GetDictionary(snapshot, "promotion"))
        );
        return string.Join("\n\n", sections);
    }

    public static string RenderWorldSnapshot(GDictionary snapshot)
    {
        return RenderFullSnapshot(snapshot);
    }

    public static string FormatCoord(Vector2I coord)
    {
        return $"({coord.X},{coord.Y})";
    }

    private static void AppendSection(List<string> sections, string title, List<string> lines)
    {
        if (lines.Count == 0)
            return;
        sections.Add($"[{title}]\n{string.Join("\n", lines)}");
    }

    private static List<string> BuildSessionLines(GDictionary session)
    {
        if (IsEmpty(session))
            return new List<string>();
        var lines = new List<string>
        {
            $"active_save_id={GetString(session, "active_save_id")}",
            $"generation_config={GetString(session, "generation_config_path")}",
            $"world_loaded={FormatBool(ReadExactBool(session, "world_loaded"))}",
        };
        foreach (GDictionary preset in Dictionaries(GetArray(session, "presets")))
        {
            lines.Add($"presets={BuildPresetLabels(GetArray(session, "presets"))}");
            break;
        }
        if (HasArray(session, "presets") && GetArray(session, "presets").Count == 0)
            lines.Add("presets=");
        if (
            HasArray(session, "presets")
            && GetArray(session, "presets").Count > 0
            && !lines[^1].StartsWith("presets=", StringComparison.Ordinal)
        )
            lines.Add($"presets={BuildPresetLabels(GetArray(session, "presets"))}");

        if (TryGetArray(session, "save_slots", out var saveSlots))
        {
            lines.Add($"save_slot_count={saveSlots.Count}");
            foreach (GDictionary saveSlot in Dictionaries(saveSlots))
            {
                lines.Add(
                    $"save={GetString(saveSlot, "save_id")} | {GetString(saveSlot, "display_name")} | {GetString(saveSlot, "world_preset_name")}"
                );
            }
        }
        return lines;
    }

    private static string BuildPresetLabels(GArray presets)
    {
        var labels = new List<string>();
        foreach (GDictionary preset in Dictionaries(presets))
        {
            labels.Add($"{GetString(preset, "preset_id")}:{GetString(preset, "display_name")}");
        }
        return string.Join(" | ", labels);
    }

    private static List<string> BuildStatusLines(GDictionary status, GDictionary modal)
    {
        return new List<string>
        {
            $"view={GetString(status, "view")}",
            $"modal={GetString(modal, "id")}",
            $"text={GetString(status, "text")}",
        };
    }

    private static List<string> BuildValidationLines(GDictionary validationSnapshot)
    {
        if (IsEmpty(validationSnapshot))
            return new List<string>();
        var lines = new List<string>
        {
            $"ok={FormatBool(ReadExactBool(validationSnapshot, "ok"))}",
            $"error_count={GetInt(validationSnapshot, "error_count")}",
        };
        if (!TryGetDictionary(validationSnapshot, "domains", out var domains))
            return lines;
        var domainIds = new List<string>();
        if (TryGetArray(validationSnapshot, "domain_order", out var domainOrder))
        {
            foreach (object domainIdValue in domainOrder)
                domainIds.Add(StringFromValue(domainIdValue));
        }
        if (domainIds.Count == 0)
        {
            foreach (object key in domains.Keys)
                domainIds.Add(StringFromValue(key));
            domainIds.Sort(StringComparer.Ordinal);
        }
        foreach (string domainId in domainIds)
        {
            if (!TryGetDictionary(domains, domainId, out var domainSnapshot))
                continue;
            lines.Add($"domain={domainId} | errors={GetInt(domainSnapshot, "error_count")}");
            if (!TryGetArray(domainSnapshot, "errors", out var errors))
                continue;
            foreach (object errorValue in errors)
            {
                string errorText = StringFromValue(errorValue);
                lines.Add($"  - {errorText}");
            }
        }
        return lines;
    }

    private static List<string> BuildLogLines(GDictionary logSnapshot)
    {
        if (IsEmpty(logSnapshot))
            return new List<string>();
        var lines = new List<string>();
        string fileName = ExtractLogFileName(logSnapshot);
        if (!string.IsNullOrEmpty(fileName))
            lines.Add($"file_name={fileName}");
        lines.Add($"entry_count={GetInt(logSnapshot, "entry_count")}");
        if (TryGetArray(logSnapshot, "entries", out var entries))
        {
            foreach (GDictionary entry in Dictionaries(entries))
            {
                lines.Add(
                    $"entry={GetInt(entry, "seq")} | {GetString(entry, "level")} | {GetString(entry, "domain")} | {GetString(entry, "event_id")} | {GetString(entry, "message")}"
                );
            }
        }
        return lines;
    }

    private static string ExtractLogFileName(GDictionary logSnapshot)
    {
        string virtualPath = GetString(logSnapshot, "virtual_path");
        if (!string.IsNullOrEmpty(virtualPath))
        {
            string virtualFileName = GetFileName(virtualPath);
            if (!string.IsNullOrEmpty(virtualFileName))
                return virtualFileName;
        }
        string filePath = GetString(logSnapshot, "file_path");
        return string.IsNullOrEmpty(filePath) ? "" : GetFileName(filePath);
    }

    private static List<string> BuildWorldLines(GDictionary world)
    {
        if (IsEmpty(world))
            return new List<string>();
        var lines = new List<string>
        {
            $"map_id={GetString(world, "map_id")}",
            $"map_display_name={GetString(world, "map_display_name")}",
            $"is_submap={FormatBool(ReadExactBool(world, "is_submap"))}",
            $"world_step={GetInt(world, "world_step")}",
            $"player_coord={FormatCoord(GetDictionary(world, "player_coord"))}",
            $"player_visible_on_map={FormatBool(ReadExactBool(world, "player_visible_on_map", true))}",
            $"selected_coord={FormatCoord(GetDictionary(world, "selected_coord"))}",
            $"selected_settlement_id={GetString(world, "selected_settlement_id")}",
            $"selected_npc_name={GetString(world, "selected_npc_name")}",
            $"selected_world_event_id={GetString(world, "selected_world_event_id")}",
            $"selected_world_event_name={GetString(world, "selected_world_event_name")}",
            $"selected_encounter_id={GetString(world, "selected_encounter_id")}",
            $"selected_encounter_name={GetString(world, "selected_encounter_name")}",
        };
        AppendNearbyWorldEvents(lines, GetArray(world, "nearby_world_events"));
        AppendNearbyEncounters(lines, GetArray(world, "nearby_encounters"));
        return lines;
    }

    private static void AppendNearbyWorldEvents(List<string> lines, GArray nearbyEvents)
    {
        foreach (GDictionary worldEvent in Dictionaries(nearbyEvents))
        {
            lines.Add(
                $"nearby_world_event={GetString(worldEvent, "event_id")} | {GetString(worldEvent, "display_name")} | distance={GetInt(worldEvent, "distance")} | coord={FormatCoord(GetDictionary(worldEvent, "coord"))}"
            );
        }
    }

    private static void AppendNearbyEncounters(List<string> lines, GArray nearbyEncounters)
    {
        foreach (GDictionary encounter in Dictionaries(nearbyEncounters))
        {
            lines.Add(
                $"nearby_encounter={GetString(encounter, "entity_id")} | {GetString(encounter, "display_name")} | distance={GetInt(encounter, "distance")} | coord={FormatCoord(GetDictionary(encounter, "coord"))}"
            );
        }
    }

    private static List<string> BuildSubmapLines(GDictionary submap)
    {
        if (IsEmpty(submap))
            return new List<string>();
        var prompt = GetDictionary(submap, "prompt");
        return new List<string>
        {
            $"active={FormatBool(ReadExactBool(submap, "active"))}",
            $"map_id={GetString(submap, "map_id")}",
            $"map_display_name={GetString(submap, "map_display_name")}",
            $"return_hint={GetString(submap, "return_hint_text")}",
            $"confirm_visible={FormatBool(ReadExactBool(submap, "confirm_visible"))}",
            $"prompt_title={GetString(prompt, "title")}",
            $"prompt_target={GetString(prompt, "target_display_name")}",
        };
    }

    private static List<string> BuildGameOverLines(GDictionary gameOver)
    {
        if (IsEmpty(gameOver))
            return new List<string>();
        return new List<string>
        {
            $"title={GetString(gameOver, "title")}",
            $"description={GetString(gameOver, "description")}",
            $"confirm_text={GetString(gameOver, "confirm_text")}",
            $"main_character_member_id={GetString(gameOver, "main_character_member_id")}",
            $"main_character_name={GetString(gameOver, "main_character_name")}",
            $"main_character_dead={FormatBool(ReadExactBool(gameOver, "main_character_dead"))}",
        };
    }

    private static List<string> BuildPartyLines(GDictionary party)
    {
        if (IsEmpty(party))
            return new List<string>();
        var lines = new List<string>
        {
            $"gold={GetInt(party, "gold")}",
            $"leader_member_id={GetString(party, "leader_member_id")}",
            $"active_member_ids={FormatArray(GetArray(party, "active_member_ids"))}",
            $"reserve_member_ids={FormatArray(GetArray(party, "reserve_member_ids"))}",
            $"selected_member_id={GetString(party, "selected_member_id")}",
            $"pending_reward_count={GetInt(party, "pending_reward_count")}",
        };
        if (TryGetArray(party, "members", out var members))
        {
            foreach (GDictionary member in Dictionaries(members))
            {
                var achievementSummary = GetDictionary(member, "achievement_summary");
                var attributes = GetDictionary(member, "attributes");
                lines.Add(
                    $"member={GetString(member, "member_id")} | {GetString(member, "roster_role")} | hp={GetInt(member, "current_hp")} mp={GetInt(member, "current_mp")} | leader={FormatBool(ReadExactBool(member, "is_leader"))} | unlocked={GetInt(achievementSummary, "unlocked_count")} in_progress={GetInt(achievementSummary, "in_progress_count")} recent={GetString(achievementSummary, "recent_unlocked_name")} | ac={GetInt(attributes, "armor_class")} | equip={FormatEquipment(GetArray(member, "equipment"))}"
                );
                AppendMemberProgressionLines(lines, member);
            }
        }
        AppendMemberContingencyLines(lines, GetDictionary(party, "contingency_status_by_member"));
        return lines;
    }

    private static void AppendMemberContingencyLines(List<string> lines, GDictionary statusByMember)
    {
        if (IsEmpty(statusByMember))
            return;
        foreach (string memberId in SortedStringKeys(statusByMember))
        {
            if (!TryGetDictionary(statusByMember, memberId, out var status))
                continue;
            foreach (GDictionary setup in Dictionaries(GetArray(status, "setups")))
            {
                GDictionary trigger = GetDictionary(setup, "trigger");
                string triggerText = GetString(trigger, "type");
                if (ContainsKey(trigger, "percent"))
                    triggerText = $"{triggerText}:{GetInt(trigger, "percent")}";
                lines.Add(
                    $"member_contingency={memberId} | {GetString(setup, "setup_id")} | charged={(ReadExactBool(setup, "charged") ? "yes" : "no")} | reserved_mp_max={GetInt(setup, "reserved_mp_max")} | material=special_contingency_gem:{GetInt(setup, "material_quantity")} | trigger={triggerText} | release={GetString(setup, "release_mode")} | spells={FormatContingencySpells(GetArray(setup, "stored_spells"))}"
                );
            }
        }
    }

    private static string FormatContingencySpells(GArray spells)
    {
        var parts = new List<string>();
        foreach (GDictionary spell in Dictionaries(spells))
        {
            parts.Add(
                $"{GetString(spell, "stored_skill_id")}@{GetInt(spell, "cast_level")}:{GetString(GetDictionary(spell, "target_resolver"), "type")}"
            );
        }
        return string.Join(",", parts);
    }

    private static void AppendMemberProgressionLines(List<string> lines, GDictionary member)
    {
        string memberId = GetString(member, "member_id");
        if (string.IsNullOrEmpty(memberId))
            return;
        if (MemberHasProgressionSnapshot(member))
        {
            lines.Add(
                $"member_progression={memberId} | resources={FormatArray(GetArray(member, "unlocked_combat_resource_ids"))} | aura={GetInt(member, "current_aura")} | active_core={FormatArray(GetArray(member, "active_core_skill_ids"))} | active_trigger={GetString(member, "active_level_trigger_core_skill_id")} | locked_trigger={FormatArray(GetArray(member, "locked_level_trigger_skill_ids"))} | blocked_relearn={FormatArray(GetArray(member, "blocked_relearn_skill_ids"))}"
            );
        }
        AppendMemberSkillLines(lines, memberId, GetArray(member, "skill_entries"));
        AppendMemberProfessionLines(lines, memberId, GetArray(member, "profession_entries"));
    }

    private static bool MemberHasProgressionSnapshot(GDictionary member)
    {
        string[] fieldNames =
        {
            "unlocked_combat_resource_ids",
            "current_aura",
            "active_core_skill_ids",
            "active_level_trigger_core_skill_id",
            "locked_level_trigger_skill_ids",
            "blocked_relearn_skill_ids",
            "skill_entries",
            "profession_entries",
        };
        foreach (string fieldName in fieldNames)
        {
            if (ContainsKey(member, fieldName))
                return true;
        }
        return false;
    }

    private static void AppendMemberSkillLines(
        List<string> lines,
        string memberId,
        GArray skillEntries
    )
    {
        foreach (GDictionary skillEntry in Dictionaries(skillEntries))
        {
            string skillId = GetString(skillEntry, "skill_id");
            if (string.IsNullOrEmpty(skillId))
                continue;
            lines.Add(
                $"member_skill={memberId} | {skillId} | lv={GetInt(skillEntry, "level")} | core={FormatBool(ReadExactBool(skillEntry, "is_core"))} | trigger_active={FormatBool(ReadExactBool(skillEntry, "is_level_trigger_active"))} | trigger_locked={FormatBool(ReadExactBool(skillEntry, "is_level_trigger_locked"))} | growth_claimed={FormatBool(ReadExactBool(skillEntry, "core_max_growth_claimed"))} | profession={GetString(skillEntry, "assigned_profession_id")}"
            );
        }
    }

    private static void AppendMemberProfessionLines(
        List<string> lines,
        string memberId,
        GArray professionEntries
    )
    {
        foreach (GDictionary professionEntry in Dictionaries(professionEntries))
        {
            string professionId = GetString(professionEntry, "profession_id");
            if (string.IsNullOrEmpty(professionId))
                continue;
            lines.Add(
                $"member_profession={memberId} | {professionId} | rank={GetInt(professionEntry, "rank")} | active={FormatBool(ReadExactBool(professionEntry, "is_active"))} | core={FormatArray(GetArray(professionEntry, "core_skill_ids"))} | granted={FormatArray(GetArray(professionEntry, "granted_skill_ids"))}"
            );
        }
    }

    private static List<string> BuildQuestLines(GDictionary quests)
    {
        if (IsEmpty(quests))
            return new List<string>();
        var lines = new List<string>
        {
            $"active_quest_ids={FormatArray(GetArray(quests, "active_quest_ids"))}",
            $"claimable_quest_ids={FormatArray(GetArray(quests, "claimable_quest_ids"))}",
            $"completed_quest_ids={FormatArray(GetArray(quests, "completed_quest_ids"))}",
        };
        AppendQuestDetailLines(lines, GetArray(quests, "active_quests"));
        AppendQuestDetailLines(lines, GetArray(quests, "claimable_quests"));
        return lines;
    }

    private static void AppendQuestDetailLines(List<string> lines, GArray questOptions)
    {
        foreach (GDictionary quest in Dictionaries(questOptions))
        {
            if (!ContainsKey(quest, "stage_id"))
                continue;
            if (!TryGetExactString(quest, "stage_id", out string stageId))
                continue;
            if (string.IsNullOrEmpty(stageId))
                continue;
            lines.Add(
                $"quest={GetExactString(quest, "quest_id")} | stage={stageId} | status={GetExactString(quest, "status_id")} | progress={FormatQuestProgress(GetDictionary(quest, "objective_progress"))} | accepted={GetInt(quest, "accepted_at_world_step", -1)} | completed={GetInt(quest, "completed_at_world_step", -1)} | rewarded={GetInt(quest, "reward_claimed_at_world_step", -1)} | context={FormatKeyValuePairs(GetDictionary(quest, "last_progress_context"))}"
            );
        }
    }

    private static List<string> BuildShopLines(GDictionary shopSnapshot)
    {
        if (IsEmpty(shopSnapshot))
            return new List<string>();
        var windowData = GetDictionary(shopSnapshot, "window_data");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(shopSnapshot, "visible"))}",
            $"title={GetExactString(windowData, "title")}",
            $"settlement_id={GetExactString(windowData, "settlement_id")}",
        };
        AppendEntryLines(
            lines,
            GetArray(windowData, "entries"),
            "entry",
            "display_name",
            "state_label",
            "cost_label"
        );
        return lines;
    }

    private static List<string> BuildContractBoardLines(GDictionary contractBoardSnapshot)
    {
        if (IsEmpty(contractBoardSnapshot))
            return new List<string>();
        var windowData = GetDictionary(contractBoardSnapshot, "window_data");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(contractBoardSnapshot, "visible"))}",
            $"title={GetExactString(windowData, "title")}",
            $"settlement_id={GetExactString(windowData, "settlement_id")}",
            $"provider_interaction_id={GetExactString(windowData, "provider_interaction_id")}",
        };
        AppendEntryLines(
            lines,
            GetArray(windowData, "entries"),
            "entry",
            "display_name",
            "state_label",
            "cost_label"
        );
        return lines;
    }

    private static List<string> BuildStagecoachLines(GDictionary stagecoachSnapshot)
    {
        if (IsEmpty(stagecoachSnapshot))
            return new List<string>();
        var windowData = GetDictionary(stagecoachSnapshot, "window_data");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(stagecoachSnapshot, "visible"))}",
            $"title={GetExactString(windowData, "title")}",
            $"settlement_id={GetExactString(windowData, "settlement_id")}",
        };
        AppendEntryLines(
            lines,
            GetArray(windowData, "entries"),
            "route",
            "display_name",
            "state_label",
            "cost_label"
        );
        return lines;
    }

    private static List<string> BuildForgeLines(GDictionary forgeSnapshot)
    {
        if (IsEmpty(forgeSnapshot))
            return new List<string>();
        var windowData = GetDictionary(forgeSnapshot, "window_data");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(forgeSnapshot, "visible"))}",
            $"title={GetExactString(windowData, "title")}",
            $"settlement_id={GetExactString(windowData, "settlement_id")}",
        };
        AppendEntryLines(
            lines,
            GetArray(windowData, "entries"),
            "entry",
            "display_name",
            "state_label",
            "cost_label"
        );
        return lines;
    }

    private static void AppendEntryLines(
        List<string> lines,
        GArray entries,
        string prefix,
        string labelKey,
        string stateKey,
        string costKey
    )
    {
        foreach (GDictionary entry in Dictionaries(entries))
        {
            lines.Add(
                $"{prefix}={GetExactString(entry, labelKey)} | state={GetExactString(entry, stateKey)} | cost={GetExactString(entry, costKey)}"
            );
        }
    }

    private static List<string> BuildSettlementLines(GDictionary settlement)
    {
        if (IsEmpty(settlement))
            return new List<string>();
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(settlement, "visible"))}",
            $"settlement_id={GetExactString(settlement, "settlement_id")}",
            $"display_name={GetExactString(settlement, "display_name")}",
            $"tier_name={GetExactString(settlement, "tier_name")}",
            $"faction_id={GetExactString(settlement, "faction_id")}",
            $"feedback={GetExactString(settlement, "feedback_text")}",
        };
        if (TryGetArray(settlement, "services", out var services))
        {
            foreach (GDictionary service in Dictionaries(services))
            {
                lines.Add(
                    $"service={GetExactString(service, "action_id")} | {GetExactString(service, "facility_name")} | {GetExactString(service, "npc_name")} | {GetExactString(service, "service_type")} | {GetExactString(service, "interaction_script_id")}"
                );
            }
        }
        return lines;
    }

    private static List<string> BuildCharacterLines(GDictionary characterInfo)
    {
        if (IsEmpty(characterInfo))
            return new List<string>();
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(characterInfo, "visible"))}",
            $"source={GetExactString(characterInfo, "source")}",
            $"display_name={GetExactString(characterInfo, "display_name")}",
            $"meta_label={GetExactString(characterInfo, "meta_label")}",
            $"status_label={GetExactString(characterInfo, "status_label")}",
        };
        if (TryGetArray(characterInfo, "sections", out var sections))
        {
            foreach (GDictionary section in Dictionaries(sections))
            {
                lines.Add(
                    $"section={GetExactString(section, "title")} | entries={CountCharacterSectionEntries(section, "entries")}"
                );
            }
        }
        return lines;
    }

    private static int CountCharacterSectionEntries(GDictionary section, string key)
    {
        if (section == null)
            return 0;
        TryRead(section, key, out Variant entriesValue);
        if (TryAsArray(entriesValue, out var entries))
            return entries.Count;
        if (entriesValue.VariantType == Variant.Type.PackedStringArray)
            return entriesValue.AsStringArray().Length;
        return 0;
    }

    private static List<string> BuildWarehouseLines(GDictionary warehouse)
    {
        if (IsEmpty(warehouse))
            return new List<string>();
        var windowData = GetDictionary(warehouse, "window_data");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(warehouse, "visible"))}",
            $"entry_label={GetString(warehouse, "entry_label")}",
            $"title={GetString(windowData, "title")}",
            $"meta={GetString(windowData, "meta")}",
            $"summary={GetString(windowData, "summary_text")}",
            $"status={GetString(windowData, "status_text")}",
        };
        if (TryGetArray(windowData, "entries", out var entries))
        {
            foreach (GDictionary entry in Dictionaries(entries))
            {
                lines.Add(
                    $"entry={GetString(entry, "item_id")} | qty={GetInt(entry, "quantity")} | total={GetInt(entry, "total_quantity")} | stackable={FormatBool(ReadExactBool(entry, "is_stackable"))} | limit={GetInt(entry, "stack_limit")} | mode={GetString(entry, "storage_mode")}"
                );
            }
        }
        return lines;
    }

    private static List<string> BuildBattleLines(GDictionary battle)
    {
        if (IsEmpty(battle))
            return new List<string>();
        var startPrompt = GetDictionary(battle, "start_prompt");
        var lines = new List<string>
        {
            $"active={FormatBool(ReadExactBool(battle, "active"))}",
            $"encounter_id={GetString(battle, "encounter_id")}",
            $"encounter_name={GetString(battle, "encounter_name")}",
            $"phase={GetString(battle, "phase")}",
            $"active_unit_id={GetString(battle, "active_unit_id")}",
            $"active_unit_name={GetString(battle, "active_unit_name")}",
            $"modal_state={GetString(battle, "modal_state")}",
            $"winner_faction_id={GetString(battle, "winner_faction_id")}",
            $"selected_coord={FormatCoord(GetDictionary(battle, "selected_coord"))}",
            $"selected_skill_id={GetString(battle, "selected_skill_id")}",
            $"selected_skill_variant_id={GetString(battle, "selected_skill_variant_id")}",
            $"selected_target_coords={FormatCoordArray(GetArray(battle, "selected_target_coords"))}",
            $"selected_target_unit_ids={FormatArray(GetArray(battle, "selected_target_unit_ids"))}",
            $"selected_target_unit_count={GetInt(battle, "selected_target_unit_count")}",
            $"start_confirm_visible={FormatBool(ReadExactBool(battle, "start_confirm_visible"))}",
            $"start_prompt_title={GetString(startPrompt, "title")}",
            $"start_prompt_description={GetString(startPrompt, "description")}",
            $"start_prompt_confirm_text={GetString(startPrompt, "confirm_text")}",
        };
        var calamitySnapshot = GetDictionary(battle, "calamity_by_member_id");
        if (!IsEmpty(calamitySnapshot))
            lines.Add($"calamity={FormatKeyValuePairs(calamitySnapshot)}");
        AppendHudLines(lines, GetDictionary(battle, "hud"));
        lines.Add($"report_entry_count={GetInt(battle, "report_entry_count")}");
        AppendReportLines(lines, GetArray(battle, "report_entries"));
        AppendPartyBackpackLines(lines, GetDictionary(battle, "party_backpack"));
        AppendBattleContingencyLines(lines, GetDictionary(battle, "contingency"));
        AppendUnitLines(lines, GetArray(battle, "units"));
        return lines;
    }

    private static void AppendBattleContingencyLines(List<string> lines, GDictionary contingency)
    {
        if (IsEmpty(contingency))
            return;
        foreach (GDictionary instance in Dictionaries(GetArray(contingency, "instances")))
        {
            lines.Add(
                $"battle_contingency={GetString(instance, "instance_id")} | setup={GetString(instance, "setup_id")} | owner={GetString(instance, "owner_member_id")}/{GetString(instance, "owner_unit_id")} | caster={GetString(instance, "caster_unit_id")} | consumed={FormatBool(ReadExactBool(instance, "consumed"))} | suppressed={FormatBool(ReadExactBool(instance, "suppressed"))}"
            );
        }
        foreach (GDictionary context in Dictionaries(GetArray(contingency, "queued_release_contexts")))
        {
            lines.Add(
                $"battle_contingency_queue={GetString(context, "instance_id")} | trigger={GetString(context, "trigger_type")} | owner={GetString(context, "owner_unit_id")} | source={GetString(context, "triggering_unit_id")}"
            );
        }
    }

    private static void AppendHudLines(List<string> lines, GDictionary hud)
    {
        if (IsEmpty(hud))
            return;
        lines.Add($"hud_header={GetString(hud, "header_subtitle")}");
        var roundBadge = GetDictionary(hud, "round_badge");
        lines.Add(
            $"hud_round={GetString(roundBadge, "tu_text")} · {GetString(roundBadge, "ready_text")}"
        );
        string commandText = ContainsKey(hud, "command_text")
            ? GetString(hud, "command_text")
            : GetString(hud, "skill_subtitle");
        lines.Add($"hud_command={commandText}");
        lines.Add($"hud_log={GetString(hud, "log_text")}");
    }

    private static void AppendReportLines(List<string> lines, GArray reportEntries)
    {
        foreach (GDictionary reportEntry in Dictionaries(reportEntries))
        {
            string reportType = GetString(reportEntry, "type");
            if (reportType == "change_equipment")
            {
                lines.Add(BuildChangeEquipmentReportLine(reportEntry));
                continue;
            }
            lines.Add(
                $"report={GetString(reportEntry, "entry_type")} | reason={GetString(reportEntry, "reason_id")} | tags={FormatArray(GetArray(reportEntry, "event_tags"))} | text={GetString(reportEntry, "text")}"
            );
        }
    }

    private static void AppendPartyBackpackLines(List<string> lines, GDictionary partyBackpack)
    {
        if (IsEmpty(partyBackpack))
            return;
        lines.Add(
            $"backpack_used_slots={GetInt(partyBackpack, "used_slots")} | stacks={GetInt(partyBackpack, "stack_count")} | equipment_instances={GetInt(partyBackpack, "equipment_instance_count")}"
        );
        if (TryGetArray(partyBackpack, "stacks", out var stackEntries))
        {
            foreach (GDictionary stackEntry in Dictionaries(stackEntries))
            {
                lines.Add(
                    $"backpack_stack={GetString(stackEntry, "item_id")} | qty={GetInt(stackEntry, "quantity")}"
                );
            }
        }
        if (TryGetArray(partyBackpack, "equipment_instances", out var equipmentEntries))
        {
            foreach (GDictionary equipmentEntry in Dictionaries(equipmentEntries))
            {
                lines.Add(
                    $"backpack_equipment={GetString(equipmentEntry, "instance_id")} | {GetString(equipmentEntry, "item_id")}"
                );
            }
        }
    }

    private static void AppendUnitLines(List<string> lines, GArray units)
    {
        foreach (GDictionary unit in Dictionaries(units))
        {
            lines.Add(
                $"unit={GetString(unit, "unit_id")} | {GetString(unit, "display_name")} | {GetString(unit, "faction_id")} | hp={GetInt(unit, "current_hp")}/{GetInt(unit, "hp_max")} mp={GetInt(unit, "current_mp")} st={GetInt(unit, "current_stamina")}/{GetInt(unit, "stamina_max")} au={GetInt(unit, "current_aura")}/{GetInt(unit, "aura_max")} shield={GetInt(unit, "current_shield_hp")}/{GetInt(unit, "shield_max_hp")} dur={GetInt(unit, "shield_duration", -1)} ap={GetInt(unit, "current_ap")} move={GetInt(unit, "current_move_points")} | alive={FormatBool(ReadExactBool(unit, "is_alive"))} | coord={FormatCoord(GetDictionary(unit, "coord"))} | equip={FormatBattleEquipment(GetArray(unit, "equipment"))}"
            );
            var pendingCast = GetDictionary(unit, "pending_cast");
            if (!IsEmpty(pendingCast))
            {
                lines.Add(
                    $"pending_cast={GetString(unit, "unit_id")} | skill={GetString(pendingCast, "skill_id")} | mode={GetString(pendingCast, "target_mode")} | binding={GetString(pendingCast, "binding_mode")} | remaining_progress={GetInt(pendingCast, "remaining_cast_progress")} | remaining_tu={GetInt(pendingCast, "remaining_cast_tu")} | targets={FormatArray(GetArray(pendingCast, "target_unit_ids"))} | coords={FormatCoordArray(GetArray(pendingCast, "target_coords"))}"
                );
            }
        }
    }

    private static string BuildChangeEquipmentReportLine(GDictionary reportEntry)
    {
        return $"report=change_equipment | ok={FormatBool(ReadExactBool(reportEntry, "ok"))} | error={GetString(reportEntry, "error_code")} | op={GetString(reportEntry, "operation")} | unit={GetString(reportEntry, "unit_id")} | target={GetString(reportEntry, "target_unit_id")} | slot={GetString(reportEntry, "slot_id")} | item={GetString(reportEntry, "item_id")} | instance={GetString(reportEntry, "instance_id")} | ap={GetInt(reportEntry, "ap_before")}>{GetInt(reportEntry, "ap_after")} | hp={GetInt(reportEntry, "hp_before")}/{GetInt(reportEntry, "hp_max_before")}>{GetInt(reportEntry, "hp_after")}/{GetInt(reportEntry, "hp_max_after")} | hp_clamped={FormatBool(ReadExactBool(reportEntry, "hp_clamped"))} | text={GetString(reportEntry, "text")}";
    }

    private static List<string> BuildLootLines(GDictionary loot)
    {
        if (IsEmpty(loot))
            return new List<string>();
        return new List<string>
        {
            $"battle_name={GetString(loot, "battle_name")}",
            $"winner_faction_id={GetString(loot, "winner_faction_id")}",
            $"loot_entry_count={GetInt(loot, "loot_entry_count")}",
            $"loot_summary={GetString(loot, "loot_summary_text")}",
            $"overflow_entry_count={GetInt(loot, "overflow_entry_count")}",
            $"overflow_summary={GetString(loot, "overflow_summary_text")}",
        };
    }

    private static List<string> BuildRewardLines(GDictionary rewardSnapshot)
    {
        if (IsEmpty(rewardSnapshot))
            return new List<string>();
        var reward = GetDictionary(rewardSnapshot, "reward");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(rewardSnapshot, "visible"))}",
            $"remaining_count={GetInt(rewardSnapshot, "remaining_count")}",
            $"reward_id={GetString(reward, "reward_id")}",
            $"member_id={GetString(reward, "member_id")}",
            $"member_name={GetString(reward, "member_name")}",
            $"source_label={GetString(reward, "source_label")}",
            $"summary={GetString(reward, "summary_text")}",
        };
        if (TryGetArray(reward, "entries", out var entries))
        {
            foreach (GDictionary entry in Dictionaries(entries))
            {
                lines.Add(
                    $"entry={GetString(entry, "entry_type")} | {GetString(entry, "target_id")} | amount={GetInt(entry, "amount")} | reason={GetString(entry, "reason_text")}"
                );
            }
        }
        return lines;
    }

    private static List<string> BuildPromotionLines(GDictionary promotion)
    {
        if (IsEmpty(promotion))
            return new List<string>();
        var prompt = GetDictionary(promotion, "prompt");
        var lines = new List<string>
        {
            $"visible={FormatBool(ReadExactBool(promotion, "visible"))}",
            $"member_id={GetString(prompt, "member_id")}",
            $"member_name={GetString(prompt, "member_name")}",
        };
        if (TryGetArray(prompt, "choices", out var choices))
        {
            foreach (GDictionary choice in Dictionaries(choices))
            {
                lines.Add(
                    $"choice={GetString(choice, "profession_id")} | {GetString(choice, "display_name")} | {GetString(choice, "summary")} | skills={FormatArray(GetArray(choice, "granted_skill_ids"))}"
                );
            }
        }
        return lines;
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string FormatCoord(GDictionary coord)
    {
        return $"({GetInt(coord, "x")},{GetInt(coord, "y")})";
    }

    private static string FormatCoordArray(GArray coords)
    {
        var parts = new List<string>();
        foreach (object coordValue in coords)
        {
            if (!TryAsDictionary(coordValue, out var coord))
            {
                parts.Add(FormatCoord(new GDictionary()));
                continue;
            }
            parts.Add(FormatCoord(coord));
        }
        return string.Join(" ", parts);
    }

    private static string FormatArray(GArray values)
    {
        var parts = new List<string>();
        foreach (object value in values)
            parts.Add(StringFromValue(value));
        return string.Join(" ", parts);
    }

    private static string FormatEquipment(GArray entries)
    {
        var parts = new List<string>();
        foreach (GDictionary entry in Dictionaries(entries))
        {
            parts.Add($"{GetString(entry, "slot_id")}:{GetString(entry, "item_id")}");
        }
        return string.Join(" ", parts);
    }

    private static string FormatBattleEquipment(GArray entries)
    {
        var parts = new List<string>();
        foreach (GDictionary entry in Dictionaries(entries))
        {
            string instanceId = GetString(entry, "instance_id");
            if (string.IsNullOrEmpty(instanceId))
                parts.Add($"{GetString(entry, "slot_id")}:{GetString(entry, "item_id")}");
            else
                parts.Add(
                    $"{GetString(entry, "slot_id")}:{GetString(entry, "item_id")}#{instanceId}"
                );
        }
        return string.Join(" ", parts);
    }

    private static string FormatQuestProgress(GDictionary progress)
    {
        if (IsEmpty(progress))
            return "";
        var parts = new List<string>();
        foreach (string key in SortedStringKeys(progress))
            parts.Add($"{key}:{GetInt(progress, key)}");
        return string.Join(" ", parts);
    }

    private static string FormatKeyValuePairs(GDictionary value)
    {
        if (IsEmpty(value))
            return "";
        var parts = new List<string>();
        foreach (string key in SortedStringKeys(value))
            parts.Add($"{key}={GetString(value, key)}");
        return string.Join(" ", parts);
    }

    private static List<string> SortedStringKeys(GDictionary values)
    {
        var keys = new List<string>();
        foreach (object key in values.Keys)
            keys.Add(StringFromValue(key));
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static bool ContainsKey(GDictionary dictionary, string key)
    {
        if (dictionary == null)
            return false;
        return dictionary.ContainsKey(key);
    }

    private static bool IsEmpty(GDictionary dictionary)
    {
        return dictionary == null || dictionary.Count == 0;
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
            yield break;
        foreach (object rawValue in values)
        {
            if (TryAsDictionary(rawValue, out var value))
                yield return value;
        }
    }

    private static GDictionary GetDictionary(GDictionary dictionary, string key)
    {
        return TryGetDictionary(dictionary, key, out var value) ? value : new GDictionary();
    }

    private static GArray GetArray(GDictionary dictionary, string key)
    {
        return TryGetArray(dictionary, key, out var value) ? value : new GArray();
    }

    private static bool HasArray(GDictionary dictionary, string key)
    {
        if (dictionary == null) return false;
        TryRead(dictionary, key, out Variant v);
        return TryAsArray(v, out _);
    }

    private static bool TryGetArray(GDictionary dictionary, string key, out GArray value)
    {
        value = new GArray();
        if (dictionary == null) return false;
        TryRead(dictionary, key, out Variant v);
        return TryAsArray(v, out value);
    }

    private static bool TryGetDictionary(GDictionary dictionary, string key, out GDictionary value)
    {
        value = new GDictionary();
        if (dictionary == null) return false;
        TryRead(dictionary, key, out Variant v);
        return TryAsDictionary(v, out value);
    }

    private static bool TryGetExactString(GDictionary dictionary, string key, out string value)
    {
        value = "";
        if (dictionary == null) return false;
        TryRead(dictionary, key, out Variant v);
        if (v.VariantType != Variant.Type.String)
            return false;
        value = v.AsString();
        return true;
    }

    private static string GetString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null) return fallback;
        TryRead(dictionary, key, out Variant v);
        return StringFromValue(v, fallback);
    }

    private static string GetExactString(GDictionary dictionary, string key, string fallback = "")
    {
        if (!TryGetExactString(dictionary, key, out string value))
            return fallback;
        return value;
    }

    private static int GetInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null) return fallback;
        TryRead(dictionary, key, out Variant v);
        return IntFromValue(v, fallback);
    }

    private static bool ReadExactBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null) return fallback;
        TryRead(dictionary, key, out Variant v);
        return v.VariantType == Variant.Type.Bool ? v.AsBool() : fallback;
    }

    private static bool TryRead(GDictionary dictionary, string key, out Variant value)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
        {
            value = default;
            return false;
        }
        if (dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        value = default;
        return false;
    }

    private static bool TryAsArray(object rawValue, out GArray value)
    {
        if (rawValue is GArray array)
        {
            value = array;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static string StringFromValue(object rawValue, string fallback = "")
    {
        if (rawValue == null)
            return fallback;
        if (rawValue is string text)
            return text;
        if (rawValue is not Variant value)
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Nil => fallback,
            Variant.Type.String => value.AsString(),
            _ => fallback,
        };
    }

    private static int IntFromValue(object rawValue, int fallback = 0)
    {
        if (rawValue is int intValue)
            return intValue;
        if (rawValue is long longValue)
            return (int)longValue;
        if (rawValue is not Variant value)
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Nil => fallback,
            _ => fallback,
        };
    }

    private static string GetFileName(string path)
    {
        string normalized = path.Replace('\\', '/');
        int index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[(index + 1)..] : normalized;
    }
}
