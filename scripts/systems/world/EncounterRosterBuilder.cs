using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using System;

[GlobalClass]
public partial class EncounterRosterBuilder : RefCounted
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

        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["drop_type"] = DropType.ToString(),
                ["drop_source_kind"] = DropSourceKind.ToString(),
                ["drop_source_id"] = DropSourceId.ToString(),
                ["drop_source_label"] = DropSourceLabel,
                ["drop_entry_id"] = DropEntryId.ToString(),
                ["item_id"] = ItemId.ToString(),
                ["quantity"] = Quantity,
            };
        }
    }

    private sealed class EncounterBuildContextData
    {
        public EncounterBuildContextData(
            IReadOnlyDictionary<StringName, SkillDef> skillDefs,
            IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
            IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
            IReadOnlyDictionary<StringName, ItemDef> itemDefs,
            int growthStage,
            int? enemyUnitCountOverride
        )
        {
            SkillDefs = skillDefs ?? new Dictionary<StringName, SkillDef>();
            EnemyTemplates = enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDef>();
            EnemyAiBrains = enemyAiBrains ?? new Dictionary<StringName, EnemyAiBrainDef>();
            ItemDefs = itemDefs ?? new Dictionary<StringName, ItemDef>();
            GrowthStage = Mathf.Max(growthStage, 0);
            EnemyUnitCountOverride = enemyUnitCountOverride;
        }

        public IReadOnlyDictionary<StringName, SkillDef> SkillDefs { get; }
        public IReadOnlyDictionary<StringName, EnemyTemplateDef> EnemyTemplates { get; }
        public IReadOnlyDictionary<StringName, EnemyAiBrainDef> EnemyAiBrains { get; }
        public IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        EncounterBuildContextData buildContext = BuildEncounterBuildContextFromTyped(
            encounterAnchor,
            skillDefs,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            growthStageOverride,
            enemyUnitCountOverride,
            allowSetupEnemyTemplateFallback: false
        );
        return BuildEnemyUnitsWithContext(encounterAnchor, buildContext);
    }

    internal GArray BuildLootEntriesTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = null,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        EncounterBuildContextData buildContext = BuildEncounterBuildContextFromTyped(
            encounterAnchor,
            skillDefs,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            growthStageOverride,
            enemyUnitCountOverride,
            allowSetupEnemyTemplateFallback: true
        );
        return BuildLootEntriesWithContext(encounterAnchor, buildContext);
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
                previewEntries.Add(previewEntry.ToDictionary());
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
                projectedEntries.Add(previewEntry.ToDictionary());
            }
        }
        return projectedEntries;
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
            foreach (BattleUnitState unit in Objects<BattleUnitState>(builtUnits))
            {
                enemyUnits.Add(unit);
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
                battle_sprite_texture = template != null ? template.battle_sprite_texture : null,
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
                body_size = Mathf.Max(template != null ? template.body_size : 1, 1),
                action_threshold =
                    template != null
                        ? template.action_threshold
                        : BattleUnitState.DefaultActionThreshold,
            };
            unitState.RefreshFootprint();
            ApplyEnemyWeaponProjection(unitState, template, buildContext.ItemDefs);
            unitState.attribute_snapshot = BuildEnemySnapshotFromTemplate(template);
            var snapshot = unitState.attribute_snapshot as AttributeSnapshot;
            unitState.current_hp =
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)) : 0;
            unitState.current_mp =
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)) : 0;
            unitState.current_stamina =
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax)) : 0;
            unitState.current_ap =
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints)) : 0;
            unitState.current_move_points = BattleUnitState.DefaultMovePointsPerTurn;
            unitState.known_active_skill_ids =
                template != null
                    ? new GStringNameArray(template.skill_ids)
                    : new GStringNameArray();
            if (unitState.known_active_skill_ids.Count == 0)
            {
                unitState.known_active_skill_ids = PickDefaultEnemySkillIds(buildContext.SkillDefs);
            }
            EnsureBasicAttackSkill(unitState, buildContext.SkillDefs);
            foreach (StringName rawSkillId in unitState.known_active_skill_ids)
            {
                StringName normalizedSkillId = new StringName(rawSkillId.ToString());
                int configuredLevel = template != null
                    ? template.GetSkillLevelTyped(normalizedSkillId, 1)
                    : 1;
                unitState.known_skill_level_map[normalizedSkillId] = Mathf.Max(configuredLevel, 1);
            }
            SyncEnemyUnlockedResources(unitState, buildContext.SkillDefs);
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

    private AttributeSnapshot BuildEnemySnapshotFromTemplate(EnemyTemplateDef template)
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
        if (template != null)
        {
            ApplyEnemyTargetRank(snapshot, template.TargetRankKind);
        }
        return snapshot;
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
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
            if (IsValidEnemyCombatSkill(GetSkillDef(skillDefs, preferredSkillId)))
            {
                return new GStringNameArray { preferredSkillId };
            }
        }

        foreach (StringName skillId in SortedIndexKeys(skillDefs))
        {
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
        if (skillDef.SkillTypeKind != SkillTypeKind.Active)
        {
            return false;
        }
        if (!skillDef.CanUseInCombat())
        {
            return false;
        }
        if (skillDef.combat_profile == null)
        {
            return false;
        }
        if (skillDef.combat_profile.TargetModeKind != BattleTargetMode.Unit)
        {
            return false;
        }
        return skillDef.combat_profile.TargetFilterKind == BattleTargetFilter.Enemy;
    }

    private static void EnsureBasicAttackSkill(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        if (
            unitState == null
            || skillDefs == null
            || !skillDefs.ContainsKey(BasicAttackSkillId)
        )
        {
            return;
        }
        if (!unitState.known_active_skill_ids.Contains(BasicAttackSkillId))
        {
            unitState.known_active_skill_ids.Add(BasicAttackSkillId);
        }
    }

    private static void SyncEnemyUnlockedResources(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
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
            SkillDef skillDef = GetSkillDef(skillDefs, skillId);
            if (skillDef == null || skillDef.combat_profile == null)
            {
                continue;
            }
            int skillLevel = unitState.HasKnownSkillLevelTyped(skillId)
                ? Mathf.Max(unitState.GetKnownSkillLevelTyped(skillId), 1)
                : 1;
            CombatSkillResourceCosts costs = skillDef.combat_profile.GetEffectiveResourceCostValues(
                skillLevel
            );
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

    private static SkillDef GetSkillDef(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        StringName skillId
    )
    {
        return skillDefs != null && skillId != "" && skillDefs.TryGetValue(skillId, out SkillDef skillDef)
            ? skillDef
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

    private EncounterBuildContextData BuildEncounterBuildContextFromDictionary(
        EncounterAnchorData encounterAnchor,
        GDictionary source,
        bool allowSetupEnemyTemplateFallback
    )
    {
        source ??= new GDictionary();
        bool looksLikeSkillDefDict = LooksLikeSkillDefDict(source);
        GDictionary buildContext = looksLikeSkillDefDict ? new GDictionary() : source;
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = looksLikeSkillDefDict
            ? BuildSkillDefIndex(source)
            : BuildSkillDefIndex(GetDictionary(buildContext, "skill_defs", new GDictionary()));
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            ResolveEnemyTemplateIndex(
                buildContext,
                allowSetupEnemyTemplateFallback ? _enemyTemplateIndex : null
            );
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = BuildEnemyAiBrainIndex(
            GetDictionary(buildContext, "enemy_ai_brains", new GDictionary())
        );
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = BuildItemDefIndex(
            GetDictionary(buildContext, "item_defs", new GDictionary())
        );
        int growthStage = encounterAnchor != null ? encounterAnchor.growth_stage : 0;
        if (
            TryGetValue(buildContext, "growth_stage", out object growthStageValue)
            && TryAsInt(growthStageValue, out int parsedGrowthStage)
        )
        {
            growthStage = parsedGrowthStage;
        }
        int? enemyUnitCountOverride = null;
        if (
            TryGetValue(buildContext, "enemy_unit_count", out object enemyUnitCountValue)
            && TryAsInt(enemyUnitCountValue, out int enemyUnitCount)
        )
        {
            enemyUnitCountOverride = enemyUnitCount;
        }
        return new EncounterBuildContextData(
            skillDefs,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            growthStage,
            enemyUnitCountOverride
        );
    }

    private EncounterBuildContextData BuildEncounterBuildContextFromTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        int? growthStageOverride,
        int? enemyUnitCountOverride,
        bool allowSetupEnemyTemplateFallback
    )
    {
        int growthStage = Mathf.Max(growthStageOverride ?? encounterAnchor?.growth_stage ?? 0, 0);
        return new EncounterBuildContextData(
            skillDefs,
            enemyTemplates
                ?? (allowSetupEnemyTemplateFallback ? _enemyTemplateIndex : null),
            enemyAiBrains,
            itemDefs,
            growthStage,
            enemyUnitCountOverride
        );
    }

    private static Dictionary<StringName, SkillDef> BuildSkillDefIndex(GDictionary skillDefs)
    {
        return BuildResourceIndex<SkillDef>(skillDefs);
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
        where T : GodotObject
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
        where T : GodotObject
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
