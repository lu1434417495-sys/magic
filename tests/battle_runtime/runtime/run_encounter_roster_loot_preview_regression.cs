using System.Collections.Generic;
using Godot;

public partial class run_encounter_roster_loot_preview_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTemplateLootSchemaUsesFormalBoundary();
        TestTemplateLootSchemaRejectsMissingDropSourceLabel();
        TestRosterLootPreviewAggregatesEntriesByItemId();
        RequestTestExit(_test.Finish("Encounter roster loot preview regression"));
    }

    private void TestTemplateLootSchemaUsesFormalBoundary()
    {
        using EncounterRosterBuilder builder = new();
        EnemyTemplateDef template = BuildDropTemplate(
            "canonical_drop_template",
            "荒狼掉落测试",
            2,
            new DropEntryDef
            {
                drop_entry_id = "wolf_hide_bundle",
                drop_type = "item",
                item_id = "beast_hide",
                quantity = 1,
            }
        );
        WildEncounterRosterDefinition roster = BuildRosterDefinition(
            "canonical_drop_roster",
            "荒狼掉落测试",
            new WildEncounterRosterUnitEntryDefinition(
                template.template_id,
                2,
                template.display_name
            )
        );
        BattleEncounterDefinition encounter = BuildEliminationEncounterDefinition(
            "canonical_drop_encounter",
            "荒狼掉落测试",
            roster.ProfileId
        );
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounter.EncounterId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [roster.ProfileId] = roster,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [template.template_id] = ProjectTemplate(template),
            }
        );

        IReadOnlyList<IReadOnlyDictionary<string, object>> lootEntries = builder.BuildLootEntriesPlain(
            BuildEncounterAnchor("wolf_den_drop_schema", encounter.EncounterId)
        );

        _test.Eq(lootEntries.Count, 1, "canonical enemy template 应暴露 1 条正式掉落 schema。");
        if (lootEntries.Count == 0)
        {
            return;
        }
        IReadOnlyDictionary<string, object> lootEntry = lootEntries[0];
        List<BattleLootEntry> parsedEntries = BattleLootEntryPayload.ParseEntriesPlain(
            lootEntries
        );
        _test.Eq(
            parsedEntries.Count,
            1,
            "plain loot preview 应能直接解析为 typed BattleLootEntry，无 Godot 投影。"
        );
        if (parsedEntries.Count > 0)
        {
            _test.Eq(
                parsedEntries[0].ItemId,
                new StringName("beast_hide"),
                "plain loot parser 应保留 item_id。"
            );
            _test.Eq(parsedEntries[0].Quantity, 2, "plain loot parser 应保留聚合数量。");
        }

        _test.True(!lootEntry.ContainsKey("drop_id"), "掉落 schema 不应继续暴露旧 drop_id alias。");
        _test.Eq(
            DictString(lootEntry, "drop_source_kind"),
            "encounter_roster",
            "掉落 schema 应标记来源类型。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_source_id"),
            "canonical_drop_roster",
            "掉落 schema 应保留稳定来源标识。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_source_label"),
            "荒狼掉落测试",
            "掉落 schema 应保留显式来源标签。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_entry_id"),
            "encounter_roster_canonical_drop_roster_beast_hide",
            "roster 聚合后的掉落 schema 应生成稳定 entry 标识。"
        );
        _test.Eq(
            DictString(lootEntry, "item_id"),
            "beast_hide",
            "canonical enemy template 掉落应指向正式物品标识。"
        );
        _test.Eq(
            DictInt(lootEntry, "quantity"),
            2,
            "canonical enemy template 掉落数量应保持稳定。"
        );
    }

    private void TestTemplateLootSchemaRejectsMissingDropSourceLabel()
    {
        using EncounterRosterBuilder builder = new();
        EnemyTemplateDef template = BuildDropTemplate(
            "missing_label_drop_template",
            "",
            1,
            new DropEntryDef
            {
                drop_entry_id = "missing_label_hide",
                drop_type = "item",
                item_id = "beast_hide",
                quantity = 1,
            }
        );
        WildEncounterRosterDefinition roster = BuildRosterDefinition(
            "missing_label_drop_roster",
            "",
            new WildEncounterRosterUnitEntryDefinition(
                template.template_id,
                1,
                template.display_name
            )
        );
        BattleEncounterDefinition encounter = BuildEliminationEncounterDefinition(
            "missing_label_drop_encounter",
            "Missing Label Drop Encounter",
            roster.ProfileId
        );
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounter.EncounterId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [roster.ProfileId] = roster,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [template.template_id] = ProjectTemplate(template),
            }
        );

        IReadOnlyList<IReadOnlyDictionary<string, object>> lootEntries = builder.BuildLootEntriesPlain(
            BuildEncounterAnchor("missing_label_drop_schema", encounter.EncounterId)
        );

        _test.Eq(
            lootEntries.Count,
            0,
            "缺少 drop_source_label 时不应从 id 默认回退生成正式 loot。"
        );
    }

    private void TestRosterLootPreviewAggregatesEntriesByItemId()
    {
        using EncounterRosterBuilder builder = new();
        EnemyTemplateDef wolfA = BuildDropTemplate(
            "wolf_a",
            "荒狼 A",
            1,
            new DropEntryDef
            {
                drop_entry_id = "hide_a",
                drop_type = "item",
                item_id = "beast_hide",
                quantity = 1,
            }
        );
        EnemyTemplateDef wolfB = BuildDropTemplate(
            "wolf_b",
            "荒狼 B",
            1,
            new DropEntryDef
            {
                drop_entry_id = "hide_b",
                drop_type = "item",
                item_id = "beast_hide",
                quantity = 3,
            }
        );
        WildEncounterRosterDefinition roster = BuildRosterDefinition(
            "wolf_den",
            "Wolf Den",
            new WildEncounterRosterUnitEntryDefinition("wolf_a", 2, "荒狼 A"),
            new WildEncounterRosterUnitEntryDefinition("wolf_b", 1, "荒狼 B")
        );
        BattleEncounterDefinition encounter = BuildEliminationEncounterDefinition(
            "wolf_den_elimination",
            "Wolf Den",
            roster.ProfileId
        );
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounter.EncounterId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [roster.ProfileId] = roster,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [wolfA.template_id] = ProjectTemplate(wolfA),
                [wolfB.template_id] = ProjectTemplate(wolfB),
            }
        );

        EncounterAnchorData encounterAnchor = BuildEncounterAnchor(
            "wolf_den_profile",
            encounter.EncounterId
        );
        encounterAnchor.growth_stage = 0;

        IReadOnlyList<IReadOnlyDictionary<string, object>> lootEntries =
            builder.BuildLootEntriesPlain(encounterAnchor);

        _test.Eq(lootEntries.Count, 1, "roster 聚合后相同 item_id 的掉落应合并为 1 条。");
        if (lootEntries.Count == 0)
        {
            return;
        }
        IReadOnlyDictionary<string, object> lootEntry = lootEntries[0];

        _test.Eq(
            DictString(lootEntry, "drop_source_kind"),
            "encounter_roster",
            "roster 掉落 schema 应标记 encounter_roster 来源。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_source_id"),
            "wolf_den",
            "roster 掉落 schema 应保留 profile_id。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_source_label"),
            "Wolf Den",
            "roster 掉落 schema 应保留 roster 显示名。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_entry_id"),
            "encounter_roster_wolf_den_beast_hide",
            "roster 聚合后应生成稳定的合并 entry 标识。"
        );
        _test.Eq(
            DictInt(lootEntry, "quantity"),
            5,
            "roster 聚合应按 unit count * drop quantity 合并数量。"
        );
    }

    private static EnemyTemplateDef BuildDropTemplate(
        StringName templateId,
        string displayName,
        int enemyCount,
        params DropEntryDef[] dropEntries
    )
    {
        EnemyTemplateDef template = new()
        {
            template_id = templateId,
            display_name = displayName,
            enemy_count = enemyCount,
        };
        foreach (DropEntryDef dropEntry in dropEntries)
        {
            template.drop_entries.Add(dropEntry);
        }
        return template;
    }

    private static WildEncounterRosterDefinition BuildRosterDefinition(
        StringName profileId,
        string displayName,
        params WildEncounterRosterUnitEntryDefinition[] unitEntries
    )
    {
        return new WildEncounterRosterDefinition(
            profileId,
            displayName,
            0,
            0,
            new[] { new WildEncounterRosterStageDefinition(0, unitEntries) }
        );
    }

    private static BattleEncounterDefinition BuildEliminationEncounterDefinition(
        StringName encounterId,
        string displayName,
        StringName rosterProfileId
    ) =>
        new(
            encounterId,
            displayName,
            rosterProfileId,
            BattleEliminationObjectiveDefinition.Instance,
            new BattleEncounterWorldResolutionDefinition(
                BattleWorldResolutionMode.Clear,
                BattleWorldResolutionMode.Preserve,
                BattleWorldResolutionMode.Preserve,
                0
            )
        );

    private static EnemyTemplateDefinition ProjectTemplate(EnemyTemplateDef template) =>
        template.ToDefinition(new Dictionary<StringName, ItemDefinition>());

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName entityId,
        StringName encounterProfileId
    )
    {
        return new EncounterAnchorData
        {
            entity_id = entityId,
            display_name = entityId.ToString(),
            encounter_profile_id = encounterProfileId,
            faction_id = "hostile",
            world_coord = Vector2I.Zero,
            vision_range = 1,
        };
    }

    private static string DictString(
        IReadOnlyDictionary<string, object> data,
        string key,
        string fallback = ""
    )
    {
        return data != null
            && data.TryGetValue(key, out object value)
            && value is string text
                ? text
                : fallback;
    }

    private static int DictInt(
        IReadOnlyDictionary<string, object> data,
        string key,
        int fallback = 0
    )
    {
        return data != null
            && data.TryGetValue(key, out object value)
            && value is int number
                ? number
                : fallback;
    }

}
