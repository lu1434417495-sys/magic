using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using System;

[GlobalClass]
public partial class EncounterRosterBuilder : RefCounted
{
    private static readonly StringName BasicAttackSkillId = "basic_attack";
    private static readonly string[] DropDefinitionRequiredFields =
    {
        "drop_entry_id",
        "drop_type",
        "item_id",
        "quantity",
    };
    private static readonly string[] FormalLootEntryRequiredFields =
    {
        "drop_type",
        "drop_source_kind",
        "drop_source_id",
        "drop_source_label",
        "drop_entry_id",
        "item_id",
        "quantity",
    };

    private GDictionary _wildEncounterRosters = new();
    private GDictionary _enemyTemplates = new();

    public void setup(GDictionary wild_encounter_rosters = null, GDictionary enemy_templates = null)
    {
        _wildEncounterRosters = wild_encounter_rosters ?? new GDictionary();
        _enemyTemplates = enemy_templates ?? new GDictionary();
    }

    public GArray build_enemy_units(EncounterAnchorData encounter_anchor, GDictionary source = null)
    {
        source ??= new GDictionary();
        GDictionary buildContext = LooksLikeSkillDefDict(source) ? new GDictionary() : source;
        GDictionary skillDefs = LooksLikeSkillDefDict(source)
            ? source
            : GetDictionary(buildContext, "skill_defs", new GDictionary());
        GDictionary enemyTemplates = GetDictionary(
            buildContext,
            "enemy_templates",
            new GDictionary()
        );
        GDictionary enemyAiBrains = GetDictionary(
            buildContext,
            "enemy_ai_brains",
            new GDictionary()
        );

        var encounterRoster = ResolveWildEncounterRoster(encounter_anchor);
        if (encounterRoster != null)
        {
            return BuildProfileEnemyUnits(
                encounter_anchor,
                encounterRoster,
                skillDefs,
                enemyTemplates,
                enemyAiBrains,
                buildContext
            );
        }

        var template = ResolveEnemyTemplate(encounter_anchor, enemyTemplates);
        if (template != null)
        {
            return BuildTemplateEnemyUnits(
                encounter_anchor,
                template,
                skillDefs,
                enemyAiBrains,
                buildContext
            );
        }
        ReportMissingEnemyTemplate(encounter_anchor);
        return new GArray();
    }

    public GArray build_loot_entries(EncounterAnchorData encounter_anchor, GDictionary source = null)
    {
        source ??= new GDictionary();
        GDictionary buildContext = LooksLikeSkillDefDict(source) ? new GDictionary() : source;
        GDictionary enemyTemplates = GetDictionary(
            buildContext,
            "enemy_templates",
            _enemyTemplates ?? new GDictionary()
        );
        var encounterRoster = ResolveWildEncounterRoster(encounter_anchor);
        if (encounterRoster == null)
        {
            var template = ResolveEnemyTemplate(encounter_anchor, enemyTemplates);
            if (template == null)
            {
                return new GArray();
            }
            int enemyCount = Mathf.Max(
                GetInt(buildContext, "enemy_unit_count", template.enemy_count),
                1
            );
            return BuildPreviewLootEntriesFromTemplate(
                template,
                enemyCount,
                "enemy_template",
                template.template_id,
                template.display_name
            );
        }
        return BuildPreviewLootEntriesFromRoster(
            encounter_anchor,
            encounterRoster,
            enemyTemplates,
            buildContext
        );
    }

    private static bool LooksLikeSkillDefDict(GDictionary source)
    {
        if (source == null)
        {
            return false;
        }
        foreach (object value in source.Values)
        {
            if (IsNil(value))
            {
                continue;
            }
            return TryAsObject(value, out SkillDef _);
        }
        return false;
    }

    private static EnemyTemplateDef ResolveEnemyTemplate(
        EncounterAnchorData encounterAnchor,
        GDictionary enemyTemplates
    )
    {
        if (enemyTemplates == null || enemyTemplates.Count == 0)
            return null;
        if (
            encounterAnchor != null
            && encounterAnchor.enemy_roster_template_id != ""
            && TryGetExactValue(enemyTemplates, encounterAnchor.enemy_roster_template_id, out object templateValue)
            && TryAsObject(templateValue, out EnemyTemplateDef template)
        )
            return template;
        return null;
    }

    private WildEncounterRosterDef ResolveWildEncounterRoster(EncounterAnchorData encounterAnchor)
    {
        if (_wildEncounterRosters == null || _wildEncounterRosters.Count == 0)
            return null;
        var anchor = encounterAnchor;
        if (
            anchor != null
            && anchor.encounter_profile_id != ""
            && TryGetExactValue(_wildEncounterRosters, anchor.encounter_profile_id, out object rosterValue)
            && TryAsObject(rosterValue, out WildEncounterRosterDef roster)
        )
        {
            return roster;
        }
        return null;
    }

    private GArray BuildPreviewLootEntriesFromRoster(
        EncounterAnchorData encounterAnchor,
        WildEncounterRosterDef encounterRoster,
        GDictionary enemyTemplates,
        GDictionary buildContext
    )
    {
        if (encounterRoster == null || enemyTemplates == null || enemyTemplates.Count == 0)
        {
            return new GArray();
        }
        int growthStage = Mathf.Max(
            GetInt(
                buildContext,
                "growth_stage",
                encounterAnchor != null ? encounterAnchor.growth_stage : 0
            ),
            0
        );
        var aggregatedEntries = new GDictionary();
        var orderedKeys = new Godot.Collections.Array<string>();
        foreach (GDictionary unitEntry in encounterRoster.get_stage_unit_entries(growthStage))
        {
            if (unitEntry == null)
            {
                continue;
            }
            StringName templateId = GetStringName(unitEntry, "template_id");
            if (
                templateId == ""
                || !TryGetExactValue(enemyTemplates, templateId, out object templateValue)
                || !TryAsObject(templateValue, out EnemyTemplateDef template)
            )
            {
                continue;
            }
            int unitCount = Mathf.Max(GetInt(unitEntry, "count", 1), 1);
            GArray previewEntries = BuildPreviewLootEntriesFromTemplate(
                template,
                unitCount,
                "encounter_roster",
                encounterRoster.profile_id,
                encounterRoster.display_name
            );
            MergePreviewLootEntries(aggregatedEntries, orderedKeys, previewEntries);
        }
        return PreviewEntryMapToArray(aggregatedEntries, orderedKeys);
    }

    private GArray BuildPreviewLootEntriesFromTemplate(
        EnemyTemplateDef template,
        int unitCount,
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel
    )
    {
        if (template == null)
        {
            return new GArray();
        }
        GArray formalEntries = BuildFormalLootEntries(
            template.get_drop_entries_resolved(),
            dropSourceKind,
            dropSourceId,
            dropSourceLabel
        );
        var multipliedEntries = new GArray();
        int multiplier = Mathf.Max(unitCount, 1);
        foreach (GDictionary entryData in Dictionaries(formalEntries))
        {
            GDictionary formalEntry = (GDictionary)entryData.Duplicate(true);
            if (!TryGetStrictInt(formalEntry, "quantity", out int quantity))
            {
                return new GArray();
            }
            formalEntry["quantity"] = quantity * multiplier;
            multipliedEntries.Add(formalEntry);
        }
        return multipliedEntries;
    }

    private GArray BuildFormalLootEntries(
        GDictArray dropEntries,
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel
    )
    {
        var lootEntries = new GArray();
        StringName normalizedSourceKind = StrictStringNameValue(dropSourceKind);
        StringName normalizedSourceId = StrictStringNameValue(dropSourceId);
        string normalizedSourceLabel = (dropSourceLabel ?? "").StripEdges();
        if (
            normalizedSourceKind == ""
            || normalizedSourceId == ""
            || string.IsNullOrEmpty(normalizedSourceLabel)
        )
        {
            return lootEntries;
        }
        if (dropEntries == null)
        {
            return lootEntries;
        }
        foreach (GDictionary entryData in dropEntries)
        {
            if (entryData == null)
            {
                return new GArray();
            }
            GDictionary parsedEntry = ParseDropDefinition(entryData);
            if (parsedEntry.Count == 0)
            {
                return new GArray();
            }
            if (!TryGetStrictInt(parsedEntry, "quantity", out int quantity))
            {
                return new GArray();
            }
            lootEntries.Add(
                new GDictionary
                {
                    ["drop_type"] = StrictStringNameField(parsedEntry, "drop_type").ToString(),
                    ["drop_source_kind"] = normalizedSourceKind.ToString(),
                    ["drop_source_id"] = normalizedSourceId.ToString(),
                    ["drop_source_label"] = normalizedSourceLabel,
                    ["drop_entry_id"] = StrictStringNameField(parsedEntry, "drop_entry_id")
                        .ToString(),
                    ["item_id"] = StrictStringNameField(parsedEntry, "item_id").ToString(),
                    ["quantity"] = quantity,
                }
            );
        }
        return lootEntries;
    }

    private static GDictionary ParseDropDefinition(GDictionary entryData)
    {
        if (entryData == null || entryData.ContainsKey("drop_id"))
        {
            return new GDictionary();
        }
        if (!HasExactFields(entryData, DropDefinitionRequiredFields))
        {
            return new GDictionary();
        }
        StringName dropEntryId = StrictStringNameField(entryData, "drop_entry_id");
        StringName dropType = StrictStringNameField(entryData, "drop_type");
        StringName itemId = StrictStringNameField(entryData, "item_id");
        if (dropEntryId == "" || itemId == "")
        {
            return new GDictionary();
        }
        if (
            dropType != BattleLootConstants.DROP_TYPE_ITEM()
            && dropType != BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT()
        )
        {
            return new GDictionary();
        }
        if (!TryGetStrictInt(entryData, "quantity", out int quantity))
        {
            return new GDictionary();
        }
        if (quantity <= 0)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["drop_entry_id"] = dropEntryId,
            ["drop_type"] = dropType,
            ["item_id"] = itemId,
            ["quantity"] = quantity,
        };
    }

    private static StringName StrictStringNameValue(StringName value)
    {
        return value != null && value != "" ? value : new StringName("");
    }

    private static StringName StrictStringNameField(GDictionary data, string key)
    {
        if (!TryGetExactValue(data, key, out object value))
        {
            return "";
        }
        return TryAsStrictStringName(value, out StringName parsed) ? parsed : new StringName("");
    }

    private static string StrictStringField(GDictionary data, string key)
    {
        return TryGetExactValue(data, key, out object value)
            && TryAsStrictString(value, out string parsed)
            ? parsed
            : "";
    }

    private static void MergePreviewLootEntries(
        GDictionary targetEntries,
        Godot.Collections.Array<string> orderedKeys,
        GArray previewEntries
    )
    {
        foreach (object previewEntryValue in previewEntries)
        {
            if (!TryAsDictionary(previewEntryValue, out GDictionary previewEntry))
            {
                targetEntries.Clear();
                orderedKeys.Clear();
                return;
            }
            GDictionary parsedEntry = ParseFormalLootEntry(previewEntry);
            if (parsedEntry.Count == 0)
            {
                targetEntries.Clear();
                orderedKeys.Clear();
                return;
            }
            string dropType = GetString(parsedEntry, "drop_type");
            string itemId = GetString(parsedEntry, "item_id");
            if (!TryGetStrictInt(parsedEntry, "quantity", out int quantity))
            {
                targetEntries.Clear();
                orderedKeys.Clear();
                return;
            }
            string entryKey = $"{dropType}|{itemId}";
            if (!targetEntries.ContainsKey(entryKey))
            {
                var clonedEntry = (GDictionary)parsedEntry.Duplicate(true);
                clonedEntry["drop_entry_id"] =
                    $"{GetString(parsedEntry, "drop_source_kind")}_{GetString(parsedEntry, "drop_source_id")}_{itemId}";
                targetEntries[entryKey] = clonedEntry;
                orderedKeys.Add(entryKey);
                continue;
            }
            if (
                !TryGetExactValue(targetEntries, entryKey, out object mergedEntryValue)
                || !TryAsDictionary(mergedEntryValue, out GDictionary mergedEntry)
                || !TryGetStrictInt(mergedEntry, "quantity", out int mergedQuantity)
            )
            {
                targetEntries.Clear();
                orderedKeys.Clear();
                return;
            }
            mergedEntry["quantity"] = mergedQuantity + quantity;
            targetEntries[entryKey] = mergedEntry;
        }
    }

    private static GDictionary ParseFormalLootEntry(GDictionary entryData)
    {
        if (entryData == null || entryData.ContainsKey("drop_id"))
        {
            return new GDictionary();
        }
        if (!HasExactFields(entryData, FormalLootEntryRequiredFields))
        {
            return new GDictionary();
        }
        StringName dropType = StrictStringNameField(entryData, "drop_type");
        StringName dropSourceKind = StrictStringNameField(entryData, "drop_source_kind");
        StringName dropSourceId = StrictStringNameField(entryData, "drop_source_id");
        string dropSourceLabel = StrictStringField(entryData, "drop_source_label");
        StringName dropEntryId = StrictStringNameField(entryData, "drop_entry_id");
        StringName itemId = StrictStringNameField(entryData, "item_id");
        if (dropType == "" || dropSourceKind == "" || dropSourceId == "")
        {
            return new GDictionary();
        }
        if (string.IsNullOrEmpty(dropSourceLabel) || dropEntryId == "" || itemId == "")
        {
            return new GDictionary();
        }
        if (!TryGetStrictInt(entryData, "quantity", out int quantity))
        {
            return new GDictionary();
        }
        if (quantity <= 0)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["drop_type"] = dropType.ToString(),
            ["drop_source_kind"] = dropSourceKind.ToString(),
            ["drop_source_id"] = dropSourceId.ToString(),
            ["drop_source_label"] = dropSourceLabel,
            ["drop_entry_id"] = dropEntryId.ToString(),
            ["item_id"] = itemId.ToString(),
            ["quantity"] = quantity,
        };
    }

    private static GArray PreviewEntryMapToArray(
        GDictionary targetEntries,
        Godot.Collections.Array<string> orderedKeys
    )
    {
        var previewEntries = new GArray();
        foreach (string entryKey in orderedKeys)
        {
            if (!targetEntries.ContainsKey(entryKey))
            {
                continue;
            }
            if (
                TryGetExactValue(targetEntries, entryKey, out object previewEntryValue)
                && TryAsDictionary(previewEntryValue, out GDictionary previewEntry)
                && previewEntry.Count != 0
            )
            {
                previewEntries.Add(previewEntry.Duplicate(true));
            }
        }
        return previewEntries;
    }

    private GArray BuildProfileEnemyUnits(
        EncounterAnchorData encounterAnchor,
        WildEncounterRosterDef encounterRoster,
        GDictionary skillDefs,
        GDictionary enemyTemplates,
        GDictionary enemyAiBrains,
        GDictionary buildContext
    )
    {
        int growthStage = Mathf.Max(
            GetInt(
                buildContext,
                "growth_stage",
                encounterAnchor != null ? encounterAnchor.growth_stage : 0
            ),
            0
        );
        var enemyUnits = new GArray();
        int nextUnitIndex = 0;
        foreach (GDictionary unitEntry in encounterRoster.get_stage_unit_entries(growthStage))
        {
            if (unitEntry == null)
            {
                continue;
            }
            StringName templateId = GetStringName(unitEntry, "template_id");
            if (templateId == "")
            {
                continue;
            }
            if (
                !TryGetExactValue(enemyTemplates, templateId, out object templateValue)
                || !TryAsObject(templateValue, out EnemyTemplateDef template)
            )
            {
                continue;
            }
            int unitCount = Mathf.Max(GetInt(unitEntry, "count", 1), 1);
            GArray builtUnits = BuildUnitsFromTemplate(
                encounterAnchor,
                template,
                skillDefs,
                enemyAiBrains,
                buildContext,
                nextUnitIndex,
                unitCount,
                GetString(unitEntry, "display_name", ""),
                true
            );
            nextUnitIndex += builtUnits.Count;
            foreach (BattleUnitState unit in Objects<BattleUnitState>(builtUnits))
            {
                enemyUnits.Add(unit);
            }
        }
        if (enemyUnits.Count != 0)
        {
            return enemyUnits;
        }
        var fallbackTemplate = ResolveEnemyTemplate(encounterAnchor, enemyTemplates);
        if (fallbackTemplate != null)
        {
            return BuildTemplateEnemyUnits(
                encounterAnchor,
                fallbackTemplate,
                skillDefs,
                enemyAiBrains,
                buildContext
            );
        }
        ReportMissingEnemyTemplate(encounterAnchor);
        return new GArray();
    }

    private GArray BuildTemplateEnemyUnits(
        EncounterAnchorData encounterAnchor,
        EnemyTemplateDef template,
        GDictionary skillDefs,
        GDictionary enemyAiBrains,
        GDictionary buildContext
    )
    {
        int enemyCount = Mathf.Max(
            GetInt(buildContext, "enemy_unit_count", template.enemy_count),
            1
        );
        string fallbackDisplayName = "敌人";
        if (template != null && !string.IsNullOrEmpty(template.display_name))
        {
            fallbackDisplayName = template.display_name;
        }
        else if (encounterAnchor != null && !string.IsNullOrEmpty(encounterAnchor.display_name))
        {
            fallbackDisplayName = encounterAnchor.display_name;
        }
        return BuildUnitsFromTemplate(
            encounterAnchor,
            template,
            skillDefs,
            enemyAiBrains,
            buildContext,
            0,
            enemyCount,
            fallbackDisplayName,
            false
        );
    }

    private GArray BuildUnitsFromTemplate(
        EncounterAnchorData encounterAnchor,
        EnemyTemplateDef template,
        GDictionary skillDefs,
        GDictionary enemyAiBrains,
        GDictionary buildContext,
        int startIndex,
        int unitCount,
        string displayNameOverride,
        bool useNumericSuffix
    )
    {
        var enemyUnits = new GArray();
        int resolvedUnitCount = Mathf.Max(unitCount, 1);
        string baseDisplayName = displayNameOverride ?? "";
        if (string.IsNullOrEmpty(baseDisplayName))
        {
            baseDisplayName = template != null ? template.display_name : "";
        }
        if (string.IsNullOrEmpty(baseDisplayName))
        {
            baseDisplayName = encounterAnchor != null ? encounterAnchor.display_name : "敌人";
        }
        EnemyAiBrainDef brain =
            template != null
            && TryGetExactValue(enemyAiBrains, template.brain_id, out object brainValue)
            && TryAsObject(brainValue, out EnemyAiBrainDef resolvedBrain)
                ? resolvedBrain
                : null;
        for (int localIndex = 0; localIndex < resolvedUnitCount; localIndex++)
        {
            int globalIndex = startIndex + localIndex;
            var unitState = new BattleUnitState
            {
                unit_id = BuildEnemyUnitId(encounterAnchor, globalIndex),
                enemy_template_id = template != null ? template.template_id : new StringName(""),
                display_name = ResolveEnemyUnitDisplayName(
                    baseDisplayName,
                    localIndex,
                    resolvedUnitCount,
                    useNumericSuffix
                ),
                battle_sprite_texture = template != null ? template.battle_sprite_texture : null,
                faction_id =
                    encounterAnchor != null && encounterAnchor.faction_id != ""
                        ? encounterAnchor.faction_id
                        : new StringName("hostile"),
                control_mode = "ai",
                ai_brain_id = template != null ? template.brain_id : new StringName(""),
                ai_state_id =
                    template != null
                        ? template.get_initial_state_id(brain)
                        : new StringName("engage"),
                ai_blackboard = new GDictionary(),
                body_size = Mathf.Max(template != null ? template.body_size : 1, 1),
                action_threshold =
                    template != null
                        ? template.action_threshold
                        : BattleUnitState.DEFAULT_ACTION_THRESHOLD(),
            };
            unitState.refresh_footprint();
            ApplyEnemyWeaponProjection(unitState, template);
            unitState.attribute_snapshot = BuildEnemySnapshotFromTemplate(
                template,
                encounterAnchor,
                globalIndex,
                buildContext
            );
            var snapshot = unitState.attribute_snapshot as AttributeSnapshot;
            unitState.current_hp =
                snapshot != null ? snapshot.get_value(AttributeService.HP_MAX_ID()) : 0;
            unitState.current_mp =
                snapshot != null ? snapshot.get_value(AttributeService.MP_MAX_ID()) : 0;
            unitState.current_stamina =
                snapshot != null ? snapshot.get_value(AttributeService.STAMINA_MAX_ID()) : 0;
            unitState.current_ap =
                snapshot != null ? snapshot.get_value(AttributeService.ACTION_POINTS_ID()) : 0;
            unitState.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();
            unitState.known_active_skill_ids =
                template != null
                    ? new GStringNameArray(template.skill_ids)
                    : new GStringNameArray();
            if (unitState.known_active_skill_ids.Count == 0)
            {
                unitState.known_active_skill_ids = PickDefaultEnemySkillIds(skillDefs);
            }
            EnsureBasicAttackSkill(unitState, skillDefs);
            foreach (StringName rawSkillId in unitState.known_active_skill_ids)
            {
                StringName normalizedSkillId = new StringName(rawSkillId.ToString());
                int configuredLevel =
                    template != null ? GetInt(template.skill_level_map, normalizedSkillId, 1) : 1;
                unitState.known_skill_level_map[normalizedSkillId] = Mathf.Max(configuredLevel, 1);
            }
            SyncEnemyUnlockedResources(unitState, skillDefs);
            enemyUnits.Add(unitState);
        }
        return enemyUnits;
    }

    private static string ResolveEnemyUnitDisplayName(
        string baseDisplayName,
        int localIndex,
        int unitCount,
        bool useNumericSuffix
    )
    {
        if (unitCount <= 1)
        {
            return baseDisplayName;
        }
        if (useNumericSuffix)
        {
            return $"{baseDisplayName}·{localIndex + 1}";
        }
        return localIndex == 0 ? baseDisplayName : $"{baseDisplayName}·从属{localIndex + 1}";
    }

    private static StringName BuildEnemyUnitId(EncounterAnchorData encounterAnchor, int index)
    {
        string anchorId = encounterAnchor != null ? encounterAnchor.entity_id.ToString() : "wild";
        return new StringName($"{anchorId}_{index + 1:00}");
    }

    private AttributeSnapshot BuildEnemySnapshotFromTemplate(
        EnemyTemplateDef template,
        EncounterAnchorData encounterAnchor,
        int unitIndex,
        GDictionary buildContext
    )
    {
        GDictionary baseAttributes = ResolveEnemyBaseAttributes(
            template,
            encounterAnchor,
            unitIndex,
            buildContext
        );
        var unitProgress = new UnitProgress();
        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
        {
            unitProgress.unit_base_attributes.set_attribute_value(
                attributeId,
                GetInt(baseAttributes, attributeId, 0)
            );
        }
        GDictionary stats = template != null ? template.attribute_overrides : new GDictionary();
        ApplyEnemyAcComponentOverridesToProgress(unitProgress, stats);
        var attributeService = new AttributeService();
        attributeService.setup(unitProgress);
        AttributeSnapshot snapshot = attributeService.get_snapshot();
        ApplyEnemyAttributeOverrides(snapshot, stats);
        if (template != null)
        {
            ApplyEnemyTargetRank(snapshot, template.target_rank);
        }
        return snapshot;
    }

    private static void ApplyEnemyTargetRank(AttributeSnapshot snapshot, StringName targetRank)
    {
        if (snapshot == null)
        {
            return;
        }
        StringName normalizedRank = ProgressionDataUtils.to_string_name(targetRank);
        if (normalizedRank == "boss")
        {
            snapshot.set_value("fortune_mark_target", 2);
            snapshot.set_value("boss_target", 1);
        }
        else if (normalizedRank == "elite")
        {
            snapshot.set_value("fortune_mark_target", 1);
            snapshot.set_value("boss_target", 0);
        }
        else
        {
            snapshot.set_value("fortune_mark_target", 0);
            snapshot.set_value("boss_target", 0);
        }
    }

    private static void ApplyEnemyWeaponProjection(
        BattleUnitState unitState,
        EnemyTemplateDef template
    )
    {
        if (unitState == null)
        {
            return;
        }
        GDictionary projection =
            template != null
                ? template.get_weapon_projection(new GDictionary())
                : new GDictionary();
        if (projection.Count == 0)
        {
            unitState.clear_weapon_projection();
            return;
        }
        unitState.apply_weapon_projection(projection);
    }

    private static GDictionary ResolveEnemyBaseAttributes(
        EnemyTemplateDef template,
        EncounterAnchorData encounterAnchor,
        int unitIndex,
        GDictionary buildContext
    )
    {
        var resolved = new GDictionary();
        GDictionary configured =
            template != null ? template.get_base_attribute_overrides_resolved() : new GDictionary();
        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
        {
            resolved[attributeId] = GetInt(configured, attributeId, 0);
        }
        return resolved;
    }

    private static void ApplyEnemyAttributeOverrides(AttributeSnapshot snapshot, GDictionary stats)
    {
        if (snapshot == null || stats == null)
        {
            return;
        }
        foreach (object rawKey in stats.Keys)
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(rawKey);
            if (attributeId == "")
            {
                continue;
            }
            int value = GetInt(stats, rawKey, 0);
            if (attributeId == AttributeService.HP_MAX_ID())
            {
                value = Mathf.Max(value, 1);
            }
            else if (
                attributeId == AttributeService.MP_MAX_ID()
                || attributeId == AttributeService.STAMINA_MAX_ID()
                || attributeId == AttributeService.AURA_MAX_ID()
            )
            {
                value = Mathf.Max(value, 0);
            }
            else if (attributeId == AttributeService.ACTION_POINTS_ID())
            {
                value = Mathf.Max(value, 1);
            }
            snapshot.set_value(attributeId, value);
        }
    }

    private static void ApplyEnemyAcComponentOverridesToProgress(
        UnitProgress unitProgress,
        GDictionary stats
    )
    {
        if (unitProgress == null || unitProgress.unit_base_attributes == null || stats == null)
        {
            return;
        }
        foreach (StringName componentId in AttributeService.AC_COMPONENT_ATTRIBUTE_IDS_ARRAY())
        {
            if (stats.ContainsKey(componentId))
            {
                unitProgress.unit_base_attributes.set_attribute_value(
                    componentId,
                    Mathf.Max(GetInt(stats, componentId, 0), 0)
                );
            }
            else if (stats.ContainsKey(componentId.ToString()))
            {
                unitProgress.unit_base_attributes.set_attribute_value(
                    componentId,
                    Mathf.Max(GetInt(stats, componentId.ToString(), 0), 0)
                );
            }
        }
    }

    private static void ReportMissingEnemyTemplate(EncounterAnchorData encounterAnchor)
    {
        string anchorId =
            encounterAnchor != null ? encounterAnchor.entity_id.ToString() : "unknown";
        string templateId =
            encounterAnchor != null ? encounterAnchor.enemy_roster_template_id.ToString() : "";
        GameLog.Error(
            $"Encounter {anchorId} cannot build fallback enemy units; missing enemy roster/template {templateId}.",
            "encounter.missing_roster_template",
            "encounter"
        );
    }

    private static GStringNameArray PickDefaultEnemySkillIds(GDictionary skillDefs)
    {
        var preferredSkillIds = new GStringNameArray
        {
            BasicAttackSkillId,
            "warrior_heavy_strike",
            "warrior_combo_strike",
            "warrior_guard_break",
        };
        foreach (StringName preferredSkillId in preferredSkillIds)
        {
            if (IsValidEnemyCombatSkill(GetSkillDef(skillDefs, preferredSkillId)))
            {
                return new GStringNameArray { preferredSkillId };
            }
        }

        foreach (
            string skillKey in ProgressionDataUtils.sorted_string_keys(
                skillDefs ?? new GDictionary()
            )
        )
        {
            var skillId = new StringName(skillKey);
            if (IsValidEnemyCombatSkill(GetSkillDef(skillDefs, skillId)))
            {
                return new GStringNameArray { skillId };
            }
        }
        return new GStringNameArray();
    }

    private static bool IsValidEnemyCombatSkill(SkillDef skillDef)
    {
        if (skillDef == null)
        {
            return false;
        }
        if (skillDef.skill_type != "active")
        {
            return false;
        }
        if (!skillDef.can_use_in_combat())
        {
            return false;
        }
        if (skillDef.combat_profile == null)
        {
            return false;
        }
        if (skillDef.combat_profile.target_mode != "unit")
        {
            return false;
        }
        return skillDef.combat_profile.target_team_filter == "enemy";
    }

    private static void EnsureBasicAttackSkill(BattleUnitState unitState, GDictionary skillDefs)
    {
        if (unitState == null || skillDefs == null || !skillDefs.ContainsKey(BasicAttackSkillId))
        {
            return;
        }
        if (!unitState.known_active_skill_ids.Contains(BasicAttackSkillId))
        {
            unitState.known_active_skill_ids.Add(BasicAttackSkillId);
        }
    }

    private static void SyncEnemyUnlockedResources(BattleUnitState unitState, GDictionary skillDefs)
    {
        if (unitState == null)
        {
            return;
        }
        unitState.sync_default_combat_resource_unlocks();
        int mpMax = 0;
        int auraMax = 0;
        var snapshot = unitState.attribute_snapshot as AttributeSnapshot;
        if (snapshot != null)
        {
            mpMax = snapshot.get_value(AttributeService.MP_MAX_ID());
            auraMax = snapshot.get_value(AttributeService.AURA_MAX_ID());
        }
        if (unitState.current_mp > 0 || mpMax > 0)
        {
            unitState.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        }
        if (unitState.current_aura > 0 || auraMax > 0)
        {
            unitState.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        }
        foreach (StringName skillId in unitState.known_active_skill_ids)
        {
            SkillDef skillDef = GetSkillDef(skillDefs, skillId);
            if (skillDef == null || skillDef.combat_profile == null)
            {
                continue;
            }
            int skillLevel = Mathf.Max(GetInt(unitState.known_skill_level_map, skillId, 1), 1);
            GDictionary costs = skillDef.combat_profile.get_effective_resource_costs(skillLevel);
            if (GetInt(costs, "mp_cost", 0) > 0)
            {
                unitState.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
            }
            if (GetInt(costs, "aura_cost", 0) > 0)
            {
                unitState.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
            }
        }
    }

    private static SkillDef GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        return TryGetExactValue(skillDefs, skillId, out object skillDefValue)
            && TryAsObject(skillDefValue, out SkillDef skillDef)
            ? skillDef
            : null;
    }

    private static bool HasExactFields(GDictionary data, string[] expectedFields)
    {
        if (data == null || data.Count != expectedFields.Length)
        {
            return false;
        }
        foreach (string fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName))
            {
                return false;
            }
        }
        return true;
    }

    private static GDictionary GetDictionary(GDictionary data, string key, GDictionary fallback)
    {
        return TryGetValue(data, key, out object value)
            && TryAsDictionary(value, out GDictionary dictionary)
            ? dictionary
            : fallback;
    }

    private static StringName GetStringName(GDictionary data, string key)
    {
        return TryGetValue(data, key, out object value)
            ? ProgressionDataUtils.to_string_name(value)
            : new StringName("");
    }

    private static int GetInt(GDictionary data, string key, int fallback = 0)
    {
        return TryGetValue(data, key, out object value) && TryAsInt(value, out int result)
            ? result
            : fallback;
    }

    private static int GetInt(GDictionary data, StringName key, int fallback = 0)
    {
        return TryGetValue(data, key, out object value) && TryAsInt(value, out int result)
            ? result
            : fallback;
    }

    private static int GetInt(GDictionary data, object key, int fallback = 0)
    {
        return TryGetExactValue(data, key, out object value) && TryAsInt(value, out int result)
            ? result
            : fallback;
    }

    private static string GetString(GDictionary data, string key, string fallback = "")
    {
        if (!TryGetValue(data, key, out object value) || IsNil(value))
        {
            return fallback;
        }
        return value.ToString();
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (TryAsDictionary(rawValue, out GDictionary value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<T> Objects<T>(GArray values)
        where T : GodotObject
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (TryAsObject(rawValue, out T value))
            {
                yield return value;
            }
        }
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
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

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        value = null;
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        if (rawValue is long longValue)
        {
            value = (int)longValue;
            return true;
        }
        if (rawValue is Variant variant && variant.TryAsInt(out value))
            return true;
        value = 0;
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        if (rawValue is long longValue)
        {
            value = (int)longValue;
            return true;
        }
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.Nil)
            {
                value = 0;
                return false;
            }
            value = variant.AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsStrictStringName(object rawValue, out StringName value)
    {
        if (rawValue is StringName stringNameValue)
        {
            value = StrictStringNameValue(stringNameValue);
            return value != "";
        }
        if (rawValue is string stringValue)
        {
            string text = stringValue.StripEdges();
            value = string.IsNullOrEmpty(text) ? new StringName("") : new StringName(text);
            return value != "";
        }
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = StrictStringNameValue(variant.AsStringName());
                return value != "";
            }
            if (variant.VariantType == Variant.Type.String)
            {
                string text = variant.AsString().StripEdges();
                value = string.IsNullOrEmpty(text) ? new StringName("") : new StringName(text);
                return value != "";
            }
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictString(object rawValue, out string value)
    {
        if (rawValue is string stringValue)
        {
            value = stringValue.StripEdges();
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString().StripEdges();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetValue(GDictionary data, string key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        var stringNameKey = new StringName(key);
        if (data != null && data.ContainsKey(stringNameKey))
        {
            value = data[stringNameKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetValue(GDictionary data, StringName key, out object value)
    {
        if (data != null && key != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        string stringKey = key?.ToString() ?? "";
        if (data != null && data.ContainsKey(stringKey))
        {
            value = data[stringKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, string key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, StringName key, out object value)
    {
        if (data != null && key != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, object key, out object value)
    {
        if (data == null || key == null)
        {
            value = null;
            return false;
        }
        if (key is Variant variantKey)
        {
            if (data.ContainsKey(variantKey))
            {
                value = data[variantKey];
                return true;
            }
        }
        else if (key is string stringKey && data.ContainsKey(stringKey))
        {
            value = data[stringKey];
            return true;
        }
        else if (key is StringName stringNameKey && data.ContainsKey(stringNameKey))
        {
            value = data[stringNameKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool IsNil(object rawValue)
    {
        return rawValue == null
            || rawValue is Variant variant && variant.VariantType == Variant.Type.Nil;
    }
}
