using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_encounter_roster_loot_preview_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
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
        builder.Setup(
            new Dictionary<StringName, WildEncounterRosterDef>(),
            new Dictionary<StringName, EnemyTemplateDef> { [template.template_id] = template }
        );

        GArray lootEntries = builder.BuildLootEntriesTyped(
            BuildTemplateEncounterAnchor("wolf_den_drop_schema", template.template_id)
        );

        _test.Eq(lootEntries.Count, 1, "canonical enemy template 应暴露 1 条正式掉落 schema。");
        if (lootEntries.Count == 0 || !TryGetDictionary(lootEntries[0], out GDictionary lootEntry))
        {
            return;
        }

        _test.True(!lootEntry.ContainsKey("drop_id"), "掉落 schema 不应继续暴露旧 drop_id alias。");
        _test.Eq(
            DictString(lootEntry, "drop_source_kind"),
            "enemy_template",
            "掉落 schema 应标记来源类型。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_source_id"),
            "canonical_drop_template",
            "掉落 schema 应保留稳定来源标识。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_source_label"),
            "荒狼掉落测试",
            "掉落 schema 应保留显式来源标签。"
        );
        _test.Eq(
            DictString(lootEntry, "drop_entry_id"),
            "wolf_hide_bundle",
            "掉落 schema 应保留稳定 entry 标识。"
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
        builder.Setup(
            new Dictionary<StringName, WildEncounterRosterDef>(),
            new Dictionary<StringName, EnemyTemplateDef> { [template.template_id] = template }
        );

        GArray lootEntries = builder.BuildLootEntriesTyped(
            BuildTemplateEncounterAnchor("missing_label_drop_schema", template.template_id)
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
        WildEncounterRosterDef roster = BuildRoster(
            "wolf_den",
            "Wolf Den",
            new WildEncounterRosterUnitEntryDef { template_id = "wolf_a", count = 2 },
            new WildEncounterRosterUnitEntryDef { template_id = "wolf_b", count = 1 }
        );
        builder.Setup(
            new Dictionary<StringName, WildEncounterRosterDef> { [roster.profile_id] = roster },
            new Dictionary<StringName, EnemyTemplateDef>
            {
                [wolfA.template_id] = wolfA,
                [wolfB.template_id] = wolfB,
            }
        );

        EncounterAnchorData encounterAnchor = BuildTemplateEncounterAnchor("wolf_den_profile", "wolf_a");
        encounterAnchor.encounter_profile_id = roster.profile_id;
        encounterAnchor.growth_stage = 0;

        GArray lootEntries = builder.BuildLootEntriesTyped(encounterAnchor);

        _test.Eq(lootEntries.Count, 1, "roster 聚合后相同 item_id 的掉落应合并为 1 条。");
        if (lootEntries.Count == 0 || !TryGetDictionary(lootEntries[0], out GDictionary lootEntry))
        {
            return;
        }

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

    private static WildEncounterRosterDef BuildRoster(
        StringName profileId,
        string displayName,
        params WildEncounterRosterUnitEntryDef[] unitEntries
    )
    {
        WildEncounterRosterDef roster = new()
        {
            profile_id = profileId,
            display_name = displayName,
            initial_stage = 0,
        };
        WildEncounterRosterStageDef stageDef = new()
        {
            stage = 0,
        };
        foreach (WildEncounterRosterUnitEntryDef unitEntry in unitEntries)
        {
            stageDef.unit_entries.Add(unitEntry);
        }
        roster.stages.Add(stageDef);
        return roster;
    }

    private static EncounterAnchorData BuildTemplateEncounterAnchor(
        StringName entityId,
        StringName templateId
    )
    {
        return new EncounterAnchorData
        {
            entity_id = entityId,
            display_name = entityId.ToString(),
            enemy_roster_template_id = templateId,
            faction_id = "hostile",
            world_coord = Vector2I.Zero,
            vision_range = 1,
        };
    }

    private static string DictString(GDictionary data, string key, string fallback = "")
    {
        return data != null && data.ContainsKey(key) ? data[key].AsString() : fallback;
    }

    private static int DictInt(GDictionary data, string key, int fallback = 0)
    {
        return data != null && data.ContainsKey(key) ? data[key].AsInt32() : fallback;
    }

    private static bool TryGetDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }

        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsGodotDictionary();
            return true;
        }
        catch
        {
        }

        value = new GDictionary();
        return false;
    }

}
