using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using System;

public sealed class EncounterRosterBuilder : IDisposable
{
    private static readonly StringName BasicAttackSkillId = "basic_attack";
    private static readonly IReadOnlyDictionary<StringName, int> EmptyIntMap =
        new Dictionary<StringName, int>();

    private sealed class ParsedDropDefinition
    {
        public ParsedDropDefinition(
            StringName dropEntryId,
            StringName dropType,
            StringName itemId,
            int quantity
        )
        {
            DropEntryId = dropEntryId;
            DropType = dropType;
            ItemId = itemId;
            Quantity = quantity;
        }

        public StringName DropEntryId { get; }
        public StringName DropType { get; }
        public StringName ItemId { get; }
        public int Quantity { get; }
    }

    private sealed class PreviewLootEntryData
    {
        public PreviewLootEntryData(
            StringName dropType,
            StringName dropSourceKind,
            StringName dropSourceId,
            string dropSourceLabel,
            StringName dropEntryId,
            StringName itemId,
            int quantity
        )
        {
            DropType = dropType;
            DropSourceKind = dropSourceKind;
            DropSourceId = dropSourceId;
            DropSourceLabel = dropSourceLabel;
            DropEntryId = dropEntryId;
            ItemId = itemId;
            Quantity = quantity;
        }

        public StringName DropType { get; }
        public StringName DropSourceKind { get; }
        public StringName DropSourceId { get; }
        public string DropSourceLabel { get; }
        public StringName DropEntryId { get; }
        public StringName ItemId { get; }
        public int Quantity { get; }

        public string AggregateKey => $"{DropType}|{ItemId}";

        public PreviewLootEntryData WithQuantity(int quantity)
        {
            return new PreviewLootEntryData(
                DropType,
                DropSourceKind,
                DropSourceId,
                DropSourceLabel,
                DropEntryId,
                ItemId,
                quantity
            );
        }

        public PreviewLootEntryData WithDropEntryId(StringName dropEntryId)
        {
            return new PreviewLootEntryData(
                DropType,
                DropSourceKind,
                DropSourceId,
                DropSourceLabel,
                dropEntryId,
                ItemId,
                Quantity
            );
        }
    }

    private sealed class EncounterBuildContextData
    {
        public EncounterBuildContextData(
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
            IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
            IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
            IReadOnlyDictionary<StringName, ItemDef> itemDefs,
            IReadOnlyDictionary<StringName, TraitDef> traitDefs,
            IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings,
            int growthStage,
            int? enemyUnitCountOverride
        )
        {
            SkillDefinitions =
                skillDefinitions ?? new Dictionary<StringName, SkillDefinition>();
            EnemyTemplates = enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDef>();
            EnemyAiBrains = enemyAiBrains ?? new Dictionary<StringName, EnemyAiBrainDef>();
            ItemDefs = itemDefs ?? new Dictionary<StringName, ItemDef>();
            TraitDefs = traitDefs ?? new Dictionary<StringName, TraitDef>();
            EquipmentAbilityBindings =
                equipmentAbilityBindings
                ?? new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
            GrowthStage = Mathf.Max(growthStage, 0);
            EnemyUnitCountOverride = enemyUnitCountOverride;
        }

        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions { get; }
        public IReadOnlyDictionary<StringName, EnemyTemplateDef> EnemyTemplates { get; }
        public IReadOnlyDictionary<StringName, EnemyAiBrainDef> EnemyAiBrains { get; }
        public IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        public IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; }
        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentAbilityBindings { get; }
        public int GrowthStage { get; }
        public int? EnemyUnitCountOverride { get; }
    }

    private Dictionary<StringName, WildEncounterRosterDef> _wildEncounterRosterIndex = new();
    private Dictionary<StringName, EnemyTemplateDef> _enemyTemplateIndex = new();

    internal void Setup(
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> wildEncounterRosters,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        _wildEncounterRosterIndex = new Dictionary<StringName, WildEncounterRosterDef>(
            wildEncounterRosters ?? new Dictionary<StringName, WildEncounterRosterDef>()
        );
        _enemyTemplateIndex = new Dictionary<StringName, EnemyTemplateDef>(
            enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDef>()
        );
    }

    internal GArray BuildEnemyUnitsTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        return BuildEnemyUnitsFromDefinitionsTyped(
            encounterAnchor,
            skillDefinitions,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            traitDefs,
            equipmentAbilityBindings,
            growthStageOverride,
            enemyUnitCountOverride
        );
    }

    internal GArray BuildEnemyUnitsFromDefinitionsTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        EncounterBuildContextData buildContext = BuildEncounterBuildContextFromTyped(
            encounterAnchor,
            skillDefinitions,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            traitDefs,
            equipmentAbilityBindings,
            growthStageOverride,
            enemyUnitCountOverride,
            allowSetupEnemyTemplateFallback: false
        );
        return BuildEnemyUnitsWithContext(encounterAnchor, buildContext);
    }

    internal GArray BuildLootEntriesTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = null,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        return BuildLootEntriesFromDefinitionsTyped(
            encounterAnchor,
            skillDefinitions,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            growthStageOverride,
            enemyUnitCountOverride
        );
    }

    internal GArray BuildLootEntriesFromDefinitionsTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = null,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        EncounterBuildContextData buildContext = BuildEncounterBuildContextFromTyped(
            encounterAnchor,
            skillDefinitions,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            null,
            null,
            growthStageOverride,
            enemyUnitCountOverride,
            allowSetupEnemyTemplateFallback: true
        );
        return BuildLootEntriesWithContext(encounterAnchor, buildContext);
    }

    private static EnemyTemplateDef ResolveEnemyTemplate(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        if (enemyTemplates == null || enemyTemplates.Count == 0)
            return null;
        if (
            encounterAnchor != null
            && encounterAnchor.enemy_roster_template_id != ""
            && enemyTemplates.TryGetValue(encounterAnchor.enemy_roster_template_id, out EnemyTemplateDef template)
        )
            return template;
        return null;
    }

    private WildEncounterRosterDef ResolveWildEncounterRoster(EncounterAnchorData encounterAnchor)
    {
        if (_wildEncounterRosterIndex == null || _wildEncounterRosterIndex.Count == 0)
            return null;
        var anchor = encounterAnchor;
        if (
            anchor != null
            && anchor.encounter_profile_id != ""
            && _wildEncounterRosterIndex.TryGetValue(
                anchor.encounter_profile_id,
                out WildEncounterRosterDef roster
            )
        )
        {
            return roster;
        }
        return null;
    }

    private GArray BuildPreviewLootEntriesFromRoster(
        EncounterAnchorData encounterAnchor,
        WildEncounterRosterDef encounterRoster,
        EncounterBuildContextData buildContext
    )
    {
        if (
            encounterRoster == null
            || buildContext == null
            || buildContext.EnemyTemplates == null
            || buildContext.EnemyTemplates.Count == 0
        )
        {
            return new GArray();
        }
        var aggregatedEntries = new Dictionary<string, PreviewLootEntryData>();
        var orderedKeys = new List<string>();
        foreach (
            WildEncounterRosterUnitEntryDef unitEntry in encounterRoster.GetStageUnitEntriesTyped(
                buildContext.GrowthStage
            )
        )
        {
            StringName templateId = unitEntry.template_id;
            if (
                templateId == ""
                || !buildContext.EnemyTemplates.TryGetValue(templateId, out EnemyTemplateDef template)
            )
            {
                continue;
            }
            int unitCount = Mathf.Max(unitEntry.count, 1);
            List<PreviewLootEntryData> previewEntries = BuildPreviewLootEntriesFromTemplate(
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

    private static List<PreviewLootEntryData> BuildPreviewLootEntriesFromTemplate(
        EnemyTemplateDef template,
        int unitCount,
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel
    )
    {
        if (template == null)
        {
            return new List<PreviewLootEntryData>();
        }
        List<PreviewLootEntryData> formalEntries = BuildFormalLootEntries(
            template.drop_entries,
            dropSourceKind,
            dropSourceId,
            dropSourceLabel
        );
        var multipliedEntries = new List<PreviewLootEntryData>(formalEntries.Count);
        int multiplier = Mathf.Max(unitCount, 1);
        foreach (PreviewLootEntryData formalEntry in formalEntries)
        {
            multipliedEntries.Add(formalEntry.WithQuantity(formalEntry.Quantity * multiplier));
        }
        return multipliedEntries;
    }

    private static List<PreviewLootEntryData> BuildFormalLootEntries(
        IEnumerable<DropEntryDef> dropEntries,
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel
    )
    {
        var lootEntries = new List<PreviewLootEntryData>();
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
        foreach (DropEntryDef entryData in dropEntries)
        {
            ParsedDropDefinition parsedEntry = ParseDropDefinition(entryData);
            if (parsedEntry == null)
            {
                return new List<PreviewLootEntryData>();
            }
            lootEntries.Add(
                new PreviewLootEntryData(
                    parsedEntry.DropType,
                    normalizedSourceKind,
                    normalizedSourceId,
                    normalizedSourceLabel,
                    parsedEntry.DropEntryId,
                    parsedEntry.ItemId,
                    parsedEntry.Quantity
                )
            );
        }
        return lootEntries;
    }

    private static ParsedDropDefinition ParseDropDefinition(DropEntryDef entryData)
    {
        if (entryData == null)
        {
            return null;
        }
        StringName dropEntryId = StrictStringNameValue(entryData.drop_entry_id);
        StringName dropType = StrictStringNameValue(entryData.drop_type);
        StringName itemId = StrictStringNameValue(entryData.item_id);
        if (dropEntryId == "" || itemId == "")
        {
            return null;
        }
        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);
        if (dropKind != BattleLootDropKind.Item && dropKind != BattleLootDropKind.RandomEquipment)
        {
            return null;
        }
        int quantity = entryData.quantity;
        if (quantity <= 0)
        {
            return null;
        }
        return new ParsedDropDefinition(dropEntryId, dropType, itemId, quantity);
    }

    private static StringName StrictStringNameValue(StringName value)
    {
        return value != null && value != "" ? value : new StringName("");
    }

    private static void MergePreviewLootEntries(
        Dictionary<string, PreviewLootEntryData> targetEntries,
        List<string> orderedKeys,
        IReadOnlyList<PreviewLootEntryData> previewEntries
    )
    {
        foreach (PreviewLootEntryData previewEntry in previewEntries)
        {
            string entryKey = previewEntry.AggregateKey;
            if (!targetEntries.TryGetValue(entryKey, out PreviewLootEntryData mergedEntry))
            {
                StringName mergedDropEntryId = new(
                    $"{previewEntry.DropSourceKind}_{previewEntry.DropSourceId}_{previewEntry.ItemId}"
                );
                targetEntries[entryKey] = previewEntry.WithDropEntryId(mergedDropEntryId);
                orderedKeys.Add(entryKey);
                continue;
            }
            targetEntries[entryKey] = mergedEntry.WithQuantity(
                mergedEntry.Quantity + previewEntry.Quantity
            );
        }
    }

    private static GArray PreviewEntryMapToArray(
        IReadOnlyDictionary<string, PreviewLootEntryData> targetEntries,
        IEnumerable<string> orderedKeys
    )
    {
        var previewEntries = new GArray();
        foreach (string entryKey in orderedKeys)
        {
            if (targetEntries.TryGetValue(entryKey, out PreviewLootEntryData previewEntry))
            {
                previewEntries.Add(ProjectPreviewLootEntry(previewEntry));
            }
        }
        return previewEntries;
    }

    private static GArray PreviewEntriesToArray(IEnumerable<PreviewLootEntryData> previewEntries)
    {
        var projectedEntries = new GArray();
        if (previewEntries == null)
        {
            return projectedEntries;
        }
        foreach (PreviewLootEntryData previewEntry in previewEntries)
        {
            if (previewEntry != null)
            {
                projectedEntries.Add(ProjectPreviewLootEntry(previewEntry));
            }
        }
        return projectedEntries;
    }

    private static GDictionary ProjectPreviewLootEntry(PreviewLootEntryData entry)
    {
        if (entry == null)
            return new GDictionary();
        return new GDictionary
        {
            ["drop_type"] = entry.DropType.ToString(),
            ["drop_source_kind"] = entry.DropSourceKind.ToString(),
            ["drop_source_id"] = entry.DropSourceId.ToString(),
            ["drop_source_label"] = entry.DropSourceLabel,
            ["drop_entry_id"] = entry.DropEntryId.ToString(),
            ["item_id"] = entry.ItemId.ToString(),
            ["quantity"] = entry.Quantity,
        };
    }

    private GArray BuildProfileEnemyUnits(
        EncounterAnchorData encounterAnchor,
        WildEncounterRosterDef encounterRoster,
        EncounterBuildContextData buildContext,
        int nextUnitIndex
    )
    {
        var enemyUnits = new GArray();
        foreach (
            WildEncounterRosterUnitEntryDef unitEntry in encounterRoster.GetStageUnitEntriesTyped(
                buildContext.GrowthStage
            )
        )
        {
            StringName templateId = unitEntry.template_id;
            if (templateId == "")
            {
                continue;
            }
            if (!buildContext.EnemyTemplates.TryGetValue(templateId, out EnemyTemplateDef template))
            {
                continue;
            }
            int unitCount = Mathf.Max(unitEntry.count, 1);
            GArray builtUnits = BuildUnitsFromTemplate(
                encounterAnchor,
                template,
                buildContext,
                nextUnitIndex,
                unitCount,
                unitEntry.display_name,
                true
            );
            nextUnitIndex += builtUnits.Count;
            foreach (BattleUnitState unit in BattleUnits(builtUnits))
            {
                enemyUnits.Add(unit.ToDictionary());
            }
        }
        if (enemyUnits.Count != 0)
        {
            return enemyUnits;
        }
        var fallbackTemplate = ResolveEnemyTemplate(encounterAnchor, buildContext.EnemyTemplates);
        if (fallbackTemplate != null)
        {
            return BuildTemplateEnemyUnits(
                encounterAnchor,
                fallbackTemplate,
                buildContext
            );
        }
        ReportMissingEnemyTemplate(encounterAnchor);
        return new GArray();
    }

    private GArray BuildTemplateEnemyUnits(
        EncounterAnchorData encounterAnchor,
        EnemyTemplateDef template,
        EncounterBuildContextData buildContext
    )
    {
        int enemyCount = Mathf.Max(
            buildContext.EnemyUnitCountOverride ?? template.enemy_count,
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
        EncounterBuildContextData buildContext,
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
            && buildContext.EnemyAiBrains.TryGetValue(template.brain_id, out EnemyAiBrainDef resolvedBrain)
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
                battle_sprite_texture_path = GetTextureResourcePath(template?.battle_sprite_texture),
                faction_id =
                    encounterAnchor != null && encounterAnchor.faction_id != ""
                        ? encounterAnchor.faction_id
                        : new StringName("hostile"),
                control_mode = "ai",
                ai_brain_id = template != null ? template.brain_id : new StringName(""),
                ai_state_id =
                    template != null
                        ? template.GetInitialStateId(brain)
                        : new StringName("engage"),
                ai_blackboard = new BattleAiBlackboard(),
                action_threshold =
                    template != null
                        ? template.action_threshold
                        : BattleUnitState.DefaultActionThreshold,
            };
            unitState.SetBodySizeProjection(Mathf.Max(template != null ? template.body_size : 1, 1));
            ApplyEnemyWeaponProjection(unitState, template, buildContext.ItemDefs);
            unitState.creature_type_tags =
                BattleEquipmentAbilityProjectionService.ProjectCreatureTypeTags(template);
            unitState.equipment_ability_sources =
                BattleEquipmentAbilityProjectionService.ProjectEnemyBattleOnlySources(
                    unitState,
                    template,
                    buildContext.EquipmentAbilityBindings,
                    buildContext.TraitDefs,
                    buildContext.ItemDefs
                );
            unitState.attribute_snapshot = BuildEnemySnapshotFromTemplate(
                template,
                buildContext.ItemDefs
            );
            var snapshot = unitState.attribute_snapshot as AttributeSnapshot;
            unitState.SetCombatResources(
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)) : 0,
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)) : 0,
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax)) : 0,
                unitState.current_aura,
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints)) : 0,
                BattleUnitState.DefaultMovePointsPerTurn
            );
            unitState.save_advantage_tags = CopyTemplateSaveAdvantageTags(template);
            if (template != null)
            {
                unitState.damage_resistances.ReplaceWithTyped(
                    template.GetDamageResistancesTyped()
                );
            }
            unitState.SetKnownActiveSkillIds(
                template != null
                    ? new GStringNameArray(template.skill_ids)
                    : new GStringNameArray()
            );
            if (unitState.known_active_skill_ids.Count == 0)
            {
                unitState.SetKnownActiveSkillIds(
                    PickDefaultEnemySkillIds(buildContext.SkillDefinitions)
                );
            }
            EnsureBasicAttackSkill(unitState, buildContext.SkillDefinitions);
            foreach (StringName rawSkillId in unitState.known_active_skill_ids)
            {
                StringName normalizedSkillId = new StringName(rawSkillId.ToString());
                int configuredLevel = template != null
                    ? template.GetSkillLevelTyped(normalizedSkillId, 1)
                    : 1;
                unitState.SetKnownSkillLevelTyped(normalizedSkillId, Mathf.Max(configuredLevel, 1));
            }
            SyncEnemyUnlockedResources(unitState, buildContext.SkillDefinitions);
            enemyUnits.Add(unitState.ToDictionary());
        }
        return enemyUnits;
    }

    private static IEnumerable<BattleUnitState> BattleUnits(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (BattleUnitState.TryReadUnitPayload(rawValue, out BattleUnitState value))
            {
                yield return value;
            }
        }
    }

    private static GStringNameArray CopyTemplateSaveAdvantageTags(EnemyTemplateDef template)
    {
        return template?.save_advantage_tags != null
            ? new GStringNameArray(template.save_advantage_tags)
            : new GStringNameArray();
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
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        IReadOnlyDictionary<StringName, int> baseAttributes =
            template?.GetBaseAttributeOverridesResolvedTyped() ?? EmptyIntMap;
        var unitProgress = new UnitProgress();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            unitProgress.unit_base_attributes.SetAttributeValue(
                attributeId,
                baseAttributes.TryGetValue(attributeId, out int value) ? value : 0
            );
        }
        IReadOnlyDictionary<StringName, int> stats =
            template?.GetAttributeOverridesTyped() ?? EmptyIntMap;
        ApplyEnemyAcComponentOverridesToProgress(unitProgress, stats);
        var attributeService = new AttributeService();
        attributeService.Setup(unitProgress);
        AttributeSnapshot snapshot = attributeService.GetSnapshot();
        ApplyEnemyAttributeOverrides(snapshot, stats);
        ApplyEnemyDerivedCombatStats(snapshot, template, stats, itemDefs);
        if (template != null)
        {
            ApplyEnemyTargetRank(snapshot, template.TargetRankKind);
        }
        return snapshot;
    }

    private static void ApplyEnemyDerivedCombatStats(
        AttributeSnapshot snapshot,
        EnemyTemplateDef template,
        IReadOnlyDictionary<StringName, int> declaredStats,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        if (snapshot == null || template == null)
        {
            return;
        }
        StringName hpMaxId = AttributeService.ToStringName(AttributeIdKind.HpMax);
        if (!declaredStats.ContainsKey(hpMaxId))
        {
            snapshot.SetValue(hpMaxId, template.GetDerivedHpMaxTyped());
        }
        StringName attackBonusId = AttributeService.ToStringName(AttributeIdKind.AttackBonus);
        if (!declaredStats.ContainsKey(attackBonusId))
        {
            snapshot.SetValue(attackBonusId, template.GetDerivedAttackBonusTyped(itemDefs));
        }
    }

    private static void ApplyEnemyTargetRank(
        AttributeSnapshot snapshot,
        EnemyTargetRankKind targetRank
    )
    {
        if (snapshot == null)
        {
            return;
        }
        if (targetRank == EnemyTargetRankKind.Boss)
        {
            snapshot.SetValue("fortune_mark_target", 2);
            snapshot.SetValue("boss_target", 1);
        }
        else if (targetRank == EnemyTargetRankKind.Elite)
        {
            snapshot.SetValue("fortune_mark_target", 1);
            snapshot.SetValue("boss_target", 0);
        }
        else
        {
            snapshot.SetValue("fortune_mark_target", 0);
            snapshot.SetValue("boss_target", 0);
        }
    }

    private static void ApplyEnemyWeaponProjection(
        BattleUnitState unitState,
        EnemyTemplateDef template,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        if (unitState == null)
        {
            return;
        }
        WeaponProjection projection = template?.GetWeaponProjectionTyped(itemDefs);
        if (projection == null || projection.IsEmpty())
        {
            unitState.ClearWeaponProjection();
            return;
        }
        unitState.ApplyWeaponProjectionTyped(projection);
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        return BuildResourceIndex<ItemDef>(itemDefs);
    }

    private static void ApplyEnemyAttributeOverrides(
        AttributeSnapshot snapshot,
        IReadOnlyDictionary<StringName, int> stats
    )
    {
        if (snapshot == null || stats == null)
        {
            return;
        }
        foreach ((StringName attributeId, int configuredValue) in stats)
        {
            int value = configuredValue;
            if (attributeId == AttributeService.ToStringName(AttributeIdKind.HpMax))
            {
                value = Mathf.Max(value, 1);
            }
            else if (
                attributeId == AttributeService.ToStringName(AttributeIdKind.MpMax)
                || attributeId == AttributeService.ToStringName(AttributeIdKind.StaminaMax)
                || attributeId == AttributeService.ToStringName(AttributeIdKind.AuraMax)
            )
            {
                value = Mathf.Max(value, 0);
            }
            else if (attributeId == AttributeService.ToStringName(AttributeIdKind.ActionPoints))
            {
                value = Mathf.Max(value, 1);
            }
            snapshot.SetValue(attributeId, value);
        }
    }

    private static void ApplyEnemyAcComponentOverridesToProgress(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, int> stats
    )
    {
        if (unitProgress == null || unitProgress.unit_base_attributes == null || stats == null)
        {
            return;
        }
        foreach (StringName componentId in AttributeService.AC_COMPONENT_ATTRIBUTE_IDS)
        {
            if (stats.TryGetValue(componentId, out int componentValue))
            {
                unitProgress.unit_base_attributes.SetAttributeValue(
                    componentId,
                    Mathf.Max(componentValue, 0)
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

    private static GStringNameArray PickDefaultEnemySkillIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
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
            if (IsValidEnemyCombatSkill(GetSkillDefinition(skillDefinitions, preferredSkillId)))
            {
                return new GStringNameArray { preferredSkillId };
            }
        }

        foreach (StringName skillId in SortedIndexKeys(skillDefinitions))
        {
            if (IsValidEnemyCombatSkill(GetSkillDefinition(skillDefinitions, skillId)))
            {
                return new GStringNameArray { skillId };
            }
        }
        return new GStringNameArray();
    }

    private static bool IsValidEnemyCombatSkill(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null)
        {
            return false;
        }
        if (skillDefinition.SkillTypeKind != SkillTypeKind.Active)
        {
            return false;
        }
        if (!skillDefinition.CanUseInCombat())
        {
            return false;
        }
        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        if (combatProfile == null)
        {
            return false;
        }
        if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
        {
            return false;
        }
        return combatProfile.TargetFilterKind == BattleTargetFilter.Enemy;
    }

    private static void EnsureBasicAttackSkill(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (
            unitState == null
            || skillDefinitions == null
            || !skillDefinitions.ContainsKey(BasicAttackSkillId)
        )
        {
            return;
        }
        unitState.AddKnownActiveSkill(BasicAttackSkillId);
    }

    private static void SyncEnemyUnlockedResources(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (unitState == null)
        {
            return;
        }
        unitState.SyncDefaultCombatResourceUnlocks();
        int mpMax = 0;
        int auraMax = 0;
        var snapshot = unitState.attribute_snapshot as AttributeSnapshot;
        if (snapshot != null)
        {
            mpMax = snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax));
            auraMax = snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax));
        }
        if (unitState.current_mp > 0 || mpMax > 0)
        {
            unitState.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        }
        if (unitState.current_aura > 0 || auraMax > 0)
        {
            unitState.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        }
        foreach (StringName skillId in unitState.known_active_skill_ids)
        {
            SkillDefinition skillDefinition = GetSkillDefinition(skillDefinitions, skillId);
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile == null)
            {
                continue;
            }
            int skillLevel = unitState.HasKnownSkillLevelTyped(skillId)
                ? Mathf.Max(unitState.GetKnownSkillLevelTyped(skillId), 1)
                : 1;
            CombatSkillResourceCosts costs = combatProfile.GetEffectiveResourceCostValues(skillLevel);
            if (costs.MpCost > 0)
            {
                unitState.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
            }
            if (costs.AuraCost > 0)
            {
                unitState.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
            }
        }
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        return skillDefinitions != null
            && skillId != ""
            && skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private GArray BuildEnemyUnitsWithContext(
        EncounterAnchorData encounterAnchor,
        EncounterBuildContextData buildContext
    )
    {
        var encounterRoster = ResolveWildEncounterRoster(encounterAnchor);
        if (encounterRoster != null)
        {
            return BuildProfileEnemyUnits(
                encounterAnchor,
                encounterRoster,
                buildContext,
                0
            );
        }

        var template = ResolveEnemyTemplate(encounterAnchor, buildContext.EnemyTemplates);
        if (template != null)
        {
            return BuildTemplateEnemyUnits(
                encounterAnchor,
                template,
                buildContext
            );
        }
        ReportMissingEnemyTemplate(encounterAnchor);
        return new GArray();
    }

    private GArray BuildLootEntriesWithContext(
        EncounterAnchorData encounterAnchor,
        EncounterBuildContextData buildContext
    )
    {
        var encounterRoster = ResolveWildEncounterRoster(encounterAnchor);
        if (encounterRoster == null)
        {
            var template = ResolveEnemyTemplate(encounterAnchor, buildContext.EnemyTemplates);
            if (template == null)
            {
                return new GArray();
            }
            int enemyCount = Mathf.Max(
                buildContext.EnemyUnitCountOverride ?? template.enemy_count,
                1
            );
            return PreviewEntriesToArray(
                BuildPreviewLootEntriesFromTemplate(
                    template,
                    enemyCount,
                    "enemy_template",
                    template.template_id,
                    template.display_name
                )
            );
        }
        return BuildPreviewLootEntriesFromRoster(
            encounterAnchor,
            encounterRoster,
            buildContext
        );
    }

    private EncounterBuildContextData BuildEncounterBuildContextFromTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings,
        int? growthStageOverride,
        int? enemyUnitCountOverride,
        bool allowSetupEnemyTemplateFallback
    )
    {
        int growthStage = Mathf.Max(growthStageOverride ?? encounterAnchor?.growth_stage ?? 0, 0);
        return new EncounterBuildContextData(
            skillDefinitions,
            enemyTemplates
                ?? (allowSetupEnemyTemplateFallback ? _enemyTemplateIndex : null),
            enemyAiBrains,
            itemDefs,
            traitDefs,
            equipmentAbilityBindings,
            growthStage,
            enemyUnitCountOverride
        );
    }

    private static Dictionary<StringName, EnemyTemplateDef> BuildEnemyTemplateIndex(
        GDictionary enemyTemplates
    )
    {
        return BuildResourceIndex<EnemyTemplateDef>(enemyTemplates);
    }

    private static IReadOnlyDictionary<StringName, EnemyTemplateDef> ResolveEnemyTemplateIndex(
        GDictionary buildContext,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> fallback
    )
    {
        if (
            TryGetValue(buildContext, "enemy_templates", out object rawTemplates)
            && TryAsDictionary(rawTemplates, out GDictionary enemyTemplates)
        )
        {
            return BuildEnemyTemplateIndex(enemyTemplates);
        }
        return fallback ?? new Dictionary<StringName, EnemyTemplateDef>();
    }

    private static Dictionary<StringName, WildEncounterRosterDef> BuildWildEncounterRosterIndex(
        GDictionary wildEncounterRosters
    )
    {
        return BuildResourceIndex<WildEncounterRosterDef>(wildEncounterRosters);
    }

    private static Dictionary<StringName, EnemyAiBrainDef> BuildEnemyAiBrainIndex(
        GDictionary enemyAiBrains
    )
    {
        return BuildResourceIndex<EnemyAiBrainDef>(enemyAiBrains);
    }

    private static Dictionary<StringName, T> BuildResourceIndex<T>(GDictionary values)
        where T : Resource
    {
        var result = new Dictionary<StringName, T>();
        if (values == null)
        {
            return result;
        }
        foreach (Variant rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            if (!TryGetExactValue(values, rawKey, out object rawValue) || !TryAsObject(rawValue, out T value))
            {
                continue;
            }
            AddIndexedValue(result, rawKey.AsStringName(), value);
        }
        return result;
    }

    private static void AddIndexedValue<T>(
        Dictionary<StringName, T> index,
        StringName key,
        T value
    )
        where T : Resource
    {
        if (index == null || key == "" || value == null)
        {
            return;
        }
        index[key] = value;
    }

    private static IEnumerable<StringName> SortedIndexKeys<T>(
        IReadOnlyDictionary<StringName, T> values
    )
    {
        if (values == null || values.Count == 0)
        {
            yield break;
        }
        List<string> sortedKeys = new();
        foreach (StringName key in values.Keys)
        {
            if (key != "")
            {
                sortedKeys.Add(key.ToString());
            }
        }
        sortedKeys.Sort(StringComparer.Ordinal);
        foreach (string key in sortedKeys)
        {
            yield return new StringName(key);
        }
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

    private static string GetTextureResourcePath(Texture2D texture)
    {
        string path = texture?.ResourcePath ?? "";
        return string.IsNullOrEmpty(path) ? "" : path;
    }

    private static IEnumerable<T> Objects<T>(GArray values)
        where T : RefCounted
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

    public void Dispose()
    {
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
        where T : class
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
