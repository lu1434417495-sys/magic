using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GArray = Godot.Collections.Array;
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
            IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
            IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
            IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
            IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
            IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings,
            int growthStage,
            int? enemyUnitCountOverride
        )
        {
            SkillDefinitions =
                skillDefinitions ?? new Dictionary<StringName, SkillDefinition>();
            EnemyTemplates = enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDefinition>();
            EnemyAiBrains = enemyAiBrains ?? new Dictionary<StringName, EnemyAiBrainDefinition>();
            ItemDefs = itemDefs ?? new Dictionary<StringName, ItemDefinition>();
            TraitDefs = traitDefs ?? new Dictionary<StringName, TraitDefinition>();
            EquipmentAbilityBindings =
                equipmentAbilityBindings
                ?? new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
            GrowthStage = Mathf.Max(growthStage, 0);
            EnemyUnitCountOverride = enemyUnitCountOverride;
        }

        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions { get; }
        public IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates { get; }
        public IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyAiBrains { get; }
        public IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        public IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentAbilityBindings { get; }
        public int GrowthStage { get; }
        public int? EnemyUnitCountOverride { get; }
    }

    private Dictionary<StringName, WildEncounterRosterDefinition> _wildEncounterRosterIndex = new();
    private Dictionary<StringName, EnemyTemplateDefinition> _enemyTemplateIndex = new();
    private Dictionary<StringName, BattleEncounterDefinition> _battleEncounterIndex = new();

    internal void Setup(
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters,
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> wildEncounterRosters,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates
    )
    {
        _battleEncounterIndex = new Dictionary<StringName, BattleEncounterDefinition>(
            battleEncounters ?? new Dictionary<StringName, BattleEncounterDefinition>()
        );
        _wildEncounterRosterIndex = new Dictionary<StringName, WildEncounterRosterDefinition>(
            wildEncounterRosters ?? new Dictionary<StringName, WildEncounterRosterDefinition>()
        );
        _enemyTemplateIndex = new Dictionary<StringName, EnemyTemplateDefinition>(
            enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDefinition>()
        );
    }

    // Canonical Godot projection boundary only. Formal battle startup must
    // consume BuildEnemyUnitStatesFromDefinitions so runtime-only unit state
    // does not cross a lossy codec round-trip.
    internal GodotProjectionLease<GArray> BuildEnemyUnitsLease(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        return BuildEnemyUnitsFromDefinitionsLease(
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

    // Canonical Godot projection boundary only; not an internal runtime
    // handoff for the returned BattleUnitState graph.
    internal GodotProjectionLease<GArray> BuildEnemyUnitsFromDefinitionsLease(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        IReadOnlyList<BattleUnitState> units =
            BuildEnemyUnitStatesFromDefinitions(
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
        var root = new GArray();
        GodotProjectionLease<GArray> lease = GodotProjectionLease<GArray>.CreateOwnedRoot(
            root,
            "EncounterRosterBuilder.enemy_units",
            LifetimeDomain.Request,
            "EncounterRosterBuilder.enemy_units"
        );
        try
        {
            foreach (BattleUnitState unit in units)
            {
                if (unit == null)
                    continue;
                root.Add(
                    RuntimePlainPayload.ProjectDictionaryInto(
                        lease,
                        unit.BuildSnapshotPlain(),
                        "EncounterRosterBuilder.enemy_unit"
                    )
                );
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal IReadOnlyList<BattleUnitState>
        BuildEnemyUnitStatesFromDefinitions(
            EncounterAnchorData encounterAnchor,
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
            IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
            IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
            IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
            IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null,
            IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
                equipmentAbilityBindings = null,
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
        return BuildEnemyUnitsWithContext(
            encounterAnchor,
            buildContext
        );
    }

    internal IReadOnlyList<BattleScenarioActorSpawnRequest>
        BuildScenarioActorUnitsFromDefinitions(
            EncounterAnchorData encounterAnchor,
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
            IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
            IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
            IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
            IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null,
            IReadOnlyDictionary<
                StringName,
                EquipmentAbilityBindingDefinition
            > equipmentAbilityBindings = null,
            int? growthStageOverride = null
        )
    {
        BattleEncounterDefinition encounter = ResolveBattleEncounter(
            encounterAnchor
        );
        if (encounter == null || encounter.ScenarioActors.Count == 0)
            return Array.Empty<BattleScenarioActorSpawnRequest>();

        EncounterBuildContextData buildContext = BuildEncounterBuildContextFromTyped(
            encounterAnchor,
            skillDefinitions,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            traitDefs,
            equipmentAbilityBindings,
            growthStageOverride,
            null,
            allowSetupEnemyTemplateFallback: false
        );
        var requests = new List<BattleScenarioActorSpawnRequest>();
        foreach (BattleScenarioActorDefinition actorDefinition in encounter.ScenarioActors)
        {
            if (
                actorDefinition == null
                || !buildContext.EnemyTemplates.TryGetValue(
                    actorDefinition.TemplateId,
                    out EnemyTemplateDefinition template
                )
            )
            {
                continue;
            }
            List<BattleUnitState> builtUnits = BuildUnitsFromTemplate(
                encounterAnchor,
                template,
                buildContext,
                0,
                1,
                actorDefinition.DisplayName,
                actorDefinition.ActorId,
                false
            );
            if (builtUnits.Count != 1 || builtUnits[0] == null)
                continue;
            BattleUnitState unit = builtUnits[0];
            string anchorId =
                encounterAnchor?.entity_id.ToString() ?? "battle";
            unit.unit_id =
                $"{anchorId}_scenario_{actorDefinition.ActorId}";
            unit.source_member_id = "";
            unit.encounter_actor_id = actorDefinition.ActorId;
            unit.faction_id = "player";
            unit.ControlModeKind = BattleUnitControlMode.Ai;
            requests.Add(
                new BattleScenarioActorSpawnRequest(
                    unit,
                    actorDefinition.SpawnZoneId,
                    actorDefinition.SpawnEdge,
                    actorDefinition.SpawnDepth
                )
            );
        }
        return requests.AsReadOnly();
    }

    internal IReadOnlyList<IReadOnlyDictionary<string, object>> BuildLootEntriesPlain(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains = null,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs = null,
        int? growthStageOverride = null,
        int? enemyUnitCountOverride = null
    )
    {
        return BuildLootEntriesFromDefinitionsPlain(
            encounterAnchor,
            skillDefinitions,
            enemyTemplates,
            enemyAiBrains,
            itemDefs,
            growthStageOverride,
            enemyUnitCountOverride
        );
    }

    internal IReadOnlyList<IReadOnlyDictionary<string, object>> BuildLootEntriesFromDefinitionsPlain(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains = null,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs = null,
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
        return BuildLootEntriesWithContextPlain(encounterAnchor, buildContext);
    }

    private WildEncounterRosterDefinition ResolveWildEncounterRoster(
        EncounterAnchorData encounterAnchor
    )
    {
        BattleEncounterDefinition battleEncounter = ResolveBattleEncounter(
            encounterAnchor
        );
        if (
            battleEncounter == null
            || _wildEncounterRosterIndex == null
            || _wildEncounterRosterIndex.Count == 0
        )
            return null;
        return _wildEncounterRosterIndex.TryGetValue(
            battleEncounter.RosterProfileId,
            out WildEncounterRosterDefinition roster
        )
            ? roster
            : null;
    }

    private BattleEncounterDefinition ResolveBattleEncounter(
        EncounterAnchorData encounterAnchor
    )
    {
        if (
            _battleEncounterIndex == null
            || _battleEncounterIndex.Count == 0
            || encounterAnchor == null
            || encounterAnchor.encounter_profile_id == ""
        )
        {
            return null;
        }
        return _battleEncounterIndex.TryGetValue(
            encounterAnchor.encounter_profile_id,
            out BattleEncounterDefinition battleEncounter
        )
            ? battleEncounter
            : null;
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object>> BuildPreviewLootFactsFromRoster(
        EncounterAnchorData encounterAnchor,
        WildEncounterRosterDefinition encounterRoster,
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
            return System.Array.Empty<IReadOnlyDictionary<string, object>>();
        }
        var aggregatedEntries = new Dictionary<string, PreviewLootEntryData>();
        var orderedKeys = new List<string>();
        foreach (
            WildEncounterRosterUnitEntryDefinition unitEntry in encounterRoster.GetStageUnitEntries(
                buildContext.GrowthStage
            )
        )
        {
            StringName templateId = unitEntry.TemplateId;
            if (
                templateId == ""
                || !buildContext.EnemyTemplates.TryGetValue(
                    templateId,
                    out EnemyTemplateDefinition template
                )
            )
            {
                continue;
            }
            int unitCount = Mathf.Max(unitEntry.Count, 1);
            List<PreviewLootEntryData> previewEntries = BuildPreviewLootEntriesFromTemplate(
                template,
                unitCount,
                "encounter_roster",
                encounterRoster.ProfileId,
                encounterRoster.DisplayName
            );
            MergePreviewLootEntries(aggregatedEntries, orderedKeys, previewEntries);
        }
        return PreviewEntryMapToFacts(aggregatedEntries, orderedKeys);
    }

    private static List<PreviewLootEntryData> BuildPreviewLootEntriesFromTemplate(
        EnemyTemplateDefinition template,
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
            template.DropEntries,
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
        IEnumerable<DropEntryDefinition> dropEntries,
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
        foreach (DropEntryDefinition entryData in dropEntries)
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

    private static ParsedDropDefinition ParseDropDefinition(DropEntryDefinition entryData)
    {
        if (entryData == null)
        {
            return null;
        }
        StringName dropEntryId = StrictStringNameValue(entryData.DropEntryId);
        StringName dropType = StrictStringNameValue(entryData.DropType);
        StringName itemId = StrictStringNameValue(entryData.ItemId);
        if (dropEntryId == "" || itemId == "")
        {
            return null;
        }
        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);
        if (dropKind != BattleLootDropKind.Item && dropKind != BattleLootDropKind.RandomEquipment)
        {
            return null;
        }
        int quantity = entryData.Quantity;
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

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> PreviewEntryMapToFacts(
        IReadOnlyDictionary<string, PreviewLootEntryData> targetEntries,
        IEnumerable<string> orderedKeys
    )
    {
        var previewEntries = new List<IReadOnlyDictionary<string, object>>();
        foreach (string entryKey in orderedKeys)
        {
            if (targetEntries.TryGetValue(entryKey, out PreviewLootEntryData previewEntry))
            {
                previewEntries.Add(BuildPreviewLootEntryFacts(previewEntry));
            }
        }
        return previewEntries.AsReadOnly();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> PreviewEntriesToFacts(
        IEnumerable<PreviewLootEntryData> previewEntries
    )
    {
        var projectedEntries = new List<IReadOnlyDictionary<string, object>>();
        if (previewEntries == null)
        {
            return projectedEntries.AsReadOnly();
        }
        foreach (PreviewLootEntryData previewEntry in previewEntries)
        {
            if (previewEntry != null)
            {
                projectedEntries.Add(BuildPreviewLootEntryFacts(previewEntry));
            }
        }
        return projectedEntries.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, object> BuildPreviewLootEntryFacts(
        PreviewLootEntryData entry
    )
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new ReadOnlyDictionary<string, object>(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["drop_type"] = entry.DropType.ToString(),
                ["drop_source_kind"] = entry.DropSourceKind.ToString(),
                ["drop_source_id"] = entry.DropSourceId.ToString(),
                ["drop_source_label"] = entry.DropSourceLabel,
                ["drop_entry_id"] = entry.DropEntryId.ToString(),
                ["item_id"] = entry.ItemId.ToString(),
                ["quantity"] = entry.Quantity,
            }
        );
    }

    private List<BattleUnitState> BuildProfileEnemyUnits(
        EncounterAnchorData encounterAnchor,
        WildEncounterRosterDefinition encounterRoster,
        EncounterBuildContextData buildContext,
        int nextUnitIndex
    )
    {
        var enemyUnits = new List<BattleUnitState>();
        foreach (
            WildEncounterRosterUnitEntryDefinition unitEntry in encounterRoster.GetStageUnitEntries(
                buildContext.GrowthStage
            )
        )
        {
            StringName templateId = unitEntry.TemplateId;
            if (templateId == "")
            {
                continue;
            }
            if (
                !buildContext.EnemyTemplates.TryGetValue(
                    templateId,
                    out EnemyTemplateDefinition template
                )
            )
            {
                continue;
            }
            int unitCount = Mathf.Max(unitEntry.Count, 1);
            List<BattleUnitState> builtUnits = BuildUnitsFromTemplate(
                encounterAnchor,
                template,
                buildContext,
                nextUnitIndex,
                unitCount,
                unitEntry.DisplayName,
                unitEntry.ActorId,
                true
            );
            nextUnitIndex += builtUnits.Count;
            enemyUnits.AddRange(builtUnits);
        }
        if (enemyUnits.Count != 0)
        {
            return enemyUnits;
        }
        ReportMissingEncounterRoster(encounterAnchor);
        return new List<BattleUnitState>();
    }

    private List<BattleUnitState> BuildUnitsFromTemplate(
        EncounterAnchorData encounterAnchor,
        EnemyTemplateDefinition template,
        EncounterBuildContextData buildContext,
        int startIndex,
        int unitCount,
        string displayNameOverride,
        StringName encounterActorId,
        bool useNumericSuffix
    )
    {
        var enemyUnits = new List<BattleUnitState>();
        int resolvedUnitCount = Mathf.Max(unitCount, 1);
        string baseDisplayName = displayNameOverride ?? "";
        if (string.IsNullOrEmpty(baseDisplayName))
        {
            baseDisplayName = template != null ? template.DisplayName : "";
        }
        if (string.IsNullOrEmpty(baseDisplayName))
        {
            baseDisplayName = encounterAnchor != null ? encounterAnchor.display_name : "敌人";
        }
        EnemyAiBrainDefinition brain =
            template != null
            && buildContext.EnemyAiBrains.TryGetValue(
                template.BrainId,
                out EnemyAiBrainDefinition resolvedBrain
            )
                ? resolvedBrain
                : null;
        for (int localIndex = 0; localIndex < resolvedUnitCount; localIndex++)
        {
            int globalIndex = startIndex + localIndex;
            var unitState = new BattleUnitState
            {
                unit_id = BuildEnemyUnitId(encounterAnchor, globalIndex),
                enemy_template_id = template != null ? template.TemplateId : new StringName(""),
                encounter_actor_id = encounterActorId,
                display_name = ResolveEnemyUnitDisplayName(
                    baseDisplayName,
                    localIndex,
                    resolvedUnitCount,
                    useNumericSuffix
                ),
                battle_sprite_texture_path = template?.BattleSpriteTexturePath ?? "",
                faction_id =
                    encounterAnchor != null && encounterAnchor.faction_id != ""
                        ? encounterAnchor.faction_id
                        : new StringName("hostile"),
                control_mode = "ai",
                ai_brain_id = template != null ? template.BrainId : new StringName(""),
                ai_state_id =
                    template != null
                        ? template.GetInitialStateId(brain)
                        : new StringName("engage"),
                ai_blackboard = new BattleAiBlackboard(),
            };
            unitState.SetActionThresholdTyped(
                template != null
                    ? template.ActionThreshold
                    : BattleUnitState.DefaultActionThreshold
            );
            unitState.SetBodySizeProjection(Mathf.Max(template != null ? template.BodySize : 1, 1));
            ApplyEnemyWeaponProjection(unitState, template, buildContext.ItemDefs);
            unitState.ReplaceCreatureTypeTagsTyped(
                BattleEquipmentAbilityProjectionService.ProjectCreatureTypeTags(
                    template
                )
            );
            BattleEquipmentAbilityProjectionResult
                equipmentAbilityProjection =
                    BattleEquipmentAbilityProjectionService
                        .ProjectEnemyBattleOnly(
                            unitState,
                            template,
                            buildContext
                                .EquipmentAbilityBindings,
                            buildContext.TraitDefs,
                            buildContext.ItemDefs
                        );
            unitState.ReplaceEquipmentAbilityProjectionTyped(
                equipmentAbilityProjection.Sources,
                equipmentAbilityProjection
                    .TemporalProgressModifiers
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
                unitState.GetCurrentAura(),
                snapshot != null ? snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints)) : 0,
                BattleUnitState.DefaultMovePointsPerTurn
            );
            unitState.ReplaceSaveTagsTyped(
                template?.SaveAdvantageTags,
                template?.SaveDisadvantageTags,
                template?.SaveImmunityTags
            );
            if (template != null)
            {
                unitState.ReplaceDamageResistancesTyped(
                    template.DamageResistances
                );
            }
            unitState.SetKnownActiveSkillIds(
                template?.SkillIds ?? Array.Empty<StringName>()
            );
            if (unitState.GetKnownActiveSkillsViewTyped().Count == 0)
            {
                unitState.SetKnownActiveSkillIds(
                    PickDefaultEnemySkillIds(buildContext.SkillDefinitions)
                );
            }
            EnsureBasicAttackSkill(unitState, buildContext.SkillDefinitions);
            foreach (
                StringName rawSkillId in unitState.GetKnownActiveSkillsViewTyped()
            )
            {
                StringName normalizedSkillId = new StringName(rawSkillId.ToString());
                int configuredLevel = template != null
                    ? template.GetSkillLevelTyped(normalizedSkillId, 1)
                    : 1;
                unitState.SetKnownSkillLevelTyped(normalizedSkillId, Mathf.Max(configuredLevel, 1));
            }
            SyncEnemyUnlockedResources(unitState, buildContext.SkillDefinitions);
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
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        IReadOnlyDictionary<StringName, int> baseAttributes =
            template?.BaseAttributeOverrides ?? EmptyIntMap;
        var unitProgress = new UnitProgress();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            unitProgress.unit_base_attributes.SetAttributeValue(
                attributeId,
                baseAttributes.TryGetValue(attributeId, out int value) ? value : 0
            );
        }
        IReadOnlyDictionary<StringName, int> stats =
            template?.AttributeOverrides ?? EmptyIntMap;
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
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, int> declaredStats,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        if (snapshot == null || template == null)
        {
            return;
        }
        StringName hpMaxId = AttributeService.ToStringName(AttributeIdKind.HpMax);
        if (!declaredStats.ContainsKey(hpMaxId))
        {
            snapshot.SetValue(hpMaxId, template.DerivedHpMax);
        }
        StringName attackBonusId = AttributeService.ToStringName(AttributeIdKind.AttackBonus);
        if (!declaredStats.ContainsKey(attackBonusId))
        {
            snapshot.SetValue(attackBonusId, template.DerivedAttackBonus);
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
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        if (unitState == null)
        {
            return;
        }
        WeaponProjection projection = template?.Weapon?.ToRuntimeProjection();
        if (projection == null || projection.IsEmpty())
        {
            unitState.ClearWeaponProjection();
            return;
        }
        unitState.ApplyWeaponProjectionTyped(projection);
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
        foreach (
            StringName componentId in AttributeContentRules.ArmorClassComponentAttributeIds
        )
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

    private static void ReportMissingEncounterRoster(EncounterAnchorData encounterAnchor)
    {
        string anchorId =
            encounterAnchor != null ? encounterAnchor.entity_id.ToString() : "unknown";
        string encounterProfileId =
            encounterAnchor != null ? encounterAnchor.encounter_profile_id.ToString() : "";
        GameLog.Error(
            $"Encounter {anchorId} cannot resolve a battle encounter roster from profile {encounterProfileId}.",
            "encounter.missing_battle_encounter_roster",
            "encounter"
        );
    }

    private static IReadOnlyList<StringName> PickDefaultEnemySkillIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        StringName[] preferredSkillIds =
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
                return new[] { preferredSkillId };
            }
        }

        foreach (StringName skillId in SortedIndexKeys(skillDefinitions))
        {
            if (IsValidEnemyCombatSkill(GetSkillDefinition(skillDefinitions, skillId)))
            {
                return new[] { skillId };
            }
        }
        return Array.Empty<StringName>();
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
        if (unitState.GetCurrentMp() > 0 || mpMax > 0)
        {
            unitState.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        }
        if (unitState.GetCurrentAura() > 0 || auraMax > 0)
        {
            unitState.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        }
        foreach (StringName skillId in unitState.GetKnownActiveSkillsViewTyped())
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

    private List<BattleUnitState> BuildEnemyUnitsWithContext(
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

        ReportMissingEncounterRoster(encounterAnchor);
        return new List<BattleUnitState>();
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object>> BuildLootEntriesWithContextPlain(
        EncounterAnchorData encounterAnchor,
        EncounterBuildContextData buildContext
    )
    {
        var encounterRoster = ResolveWildEncounterRoster(encounterAnchor);
        if (encounterRoster == null)
        {
            return System.Array.Empty<IReadOnlyDictionary<string, object>>();
        }
        return BuildPreviewLootFactsFromRoster(
            encounterAnchor,
            encounterRoster,
            buildContext
        );
    }

    private EncounterBuildContextData BuildEncounterBuildContextFromTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
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

    public void Dispose()
    {
        _battleEncounterIndex.Clear();
        _wildEncounterRosterIndex.Clear();
        _enemyTemplateIndex.Clear();
    }
}
