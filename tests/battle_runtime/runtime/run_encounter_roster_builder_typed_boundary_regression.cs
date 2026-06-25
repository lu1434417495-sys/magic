using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_encounter_roster_builder_typed_boundary_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedEnemyUnitBuildMatchesPublicBoundary();
        TestEncounterBuilderUnlocksCasterMpResources();
        TestTypedLootPreviewMatchesPublicBoundary();
        Quit(_test.Finish("Encounter roster builder typed boundary regression"));
    }

    private void TestTypedEnemyUnitBuildMatchesPublicBoundary()
    {
        using GameSession gameSession = new();
        using EncounterRosterBuilder builder = new();
        using BattleRuntimeModule runtime = new();
        builder.Setup(
            gameSession.GetWildEncounterRostersTyped(),
            gameSession.GetEnemyTemplatesTyped()
        );
        runtime.setup(
            null,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            builder,
            null,
            gameSession.GetItemDefsTyped()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "mist_hollow_typed_boundary",
            display_name = "雾沼伏猎群",
            world_coord = new Vector2I(9, 9),
            faction_id = "hostile",
            enemy_roster_template_id = "mist_beast",
            region_tag = "south_wilds",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "mist_hollow",
            growth_stage = 2,
            suppressed_until_step = 0,
        };

        GArray typedUnits = builder.BuildEnemyUnitsTyped(
            encounterAnchor,
            runtime.GetSkillDefIndexTyped(),
            runtime.GetEnemyTemplateIndexTyped(),
            runtime.GetEnemyAiBrainIndexTyped(),
            runtime.BuildItemDefIndexSnapshotTyped()
        );
        GArray sessionUnits = builder.BuildEnemyUnitsTyped(
            encounterAnchor,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            gameSession.GetItemDefsTyped()
        );

        _test.Eq(typedUnits.Count, sessionUnits.Count, "不同 typed 输入源构建的 enemy unit 数量应一致。");
        _test.Eq(
            SummarizeUnits(typedUnits),
            SummarizeUnits(sessionUnits),
            "不同 typed 输入源构建的 enemy unit 结果应保持一致。"
        );
    }

    private void TestEncounterBuilderUnlocksCasterMpResources()
    {
        using GameSession gameSession = new();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetWildEncounterRostersTyped(),
            gameSession.GetEnemyTemplatesTyped()
        );

        foreach (StringName templateId in new[] { new StringName("mist_beast"), new StringName("mist_weaver"), new StringName("wolf_shaman") })
        {
            BattleUnitState unit = BuildSingleTemplateUnit(builder, gameSession, templateId);
            _test.True(unit != null, $"{templateId} caster resource visibility 回归应能构建单位。");
            if (unit == null)
            {
                continue;
            }

            _test.True(unit.current_mp > 0, $"{templateId} 模板应带有真实 MP 池。");
            _test.True(
                unit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
                $"{templateId} 通过 EncounterRosterBuilder 入场时应解锁 MP 资源显示。"
            );
        }
    }

    private void TestTypedLootPreviewMatchesPublicBoundary()
    {
        using GameSession gameSession = new();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetWildEncounterRostersTyped(),
            gameSession.GetEnemyTemplatesTyped()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "wolf_den_typed_loot_boundary",
            display_name = "Wolf Den",
            world_coord = new Vector2I(4, 4),
            faction_id = "hostile",
            enemy_roster_template_id = "wolf_pack",
            region_tag = "north_wilds",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement),
            encounter_profile_id = "wolf_den",
            growth_stage = 0,
            suppressed_until_step = 0,
        };

        GArray typedLoot = builder.BuildLootEntriesTyped(encounterAnchor);
        GArray explicitTypedLoot = builder.BuildLootEntriesTyped(
            encounterAnchor,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            gameSession.GetItemDefsTyped()
        );

        _test.Eq(typedLoot.Count, explicitTypedLoot.Count, "typed loot preview 数量应一致。");
        _test.Eq(
            SummarizeLoot(typedLoot),
            SummarizeLoot(explicitTypedLoot),
            "不同 typed 输入源的 loot preview 结果应保持一致。"
        );
    }

    private static string SummarizeUnits(GArray units)
    {
        List<string> values = new();
        foreach (Variant unitValue in units ?? new GArray())
        {
            if (!BattleUnitState.TryReadUnitPayload(unitValue, out BattleUnitState unit) || unit == null)
            {
                values.Add("null");
                continue;
            }
            values.Add(
                $"{unit.enemy_template_id}|{unit.ai_brain_id}|{unit.ai_state_id}|{unit.display_name}|{unit.weapon_profile_kind}|{unit.weapon_item_id}|{unit.current_hp}|{unit.current_stamina}|{unit.known_skill_level_map.Get("basic_attack", 0)}"
            );
        }
        return string.Join(" || ", values);
    }

    private static string SummarizeLoot(GArray lootEntries)
    {
        List<string> values = new();
        foreach (Variant entryValue in lootEntries ?? new GArray())
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
            {
                values.Add("non_dict");
                continue;
            }
            GDictionary entry = entryValue.AsGodotDictionary();
            values.Add(
                $"{DictString(entry, "drop_source_kind")}|{DictString(entry, "drop_source_id")}|{DictString(entry, "drop_entry_id")}|{DictString(entry, "item_id")}|{DictInt(entry, "quantity", 0)}"
            );
        }
        return string.Join(" || ", values);
    }

    private static BattleUnitState BuildSingleTemplateUnit(
        EncounterRosterBuilder builder,
        GameSession gameSession,
        StringName templateId
    )
    {
        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = $"{templateId}_typed_mp_unlock",
            display_name = templateId.ToString(),
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            enemy_roster_template_id = templateId,
            region_tag = "typed_tests",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "",
            growth_stage = 0,
            suppressed_until_step = 0,
        };

        GArray enemyUnits = builder.BuildEnemyUnitsTyped(
            encounterAnchor,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            gameSession.GetItemDefsTyped()
        );
        return enemyUnits.Count > 0
            && BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState unit)
                ? unit
                : null;
    }

    private static GDictionary ProjectEnemyTemplates(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        GDictionary result = new();
        if (enemyTemplates == null)
            return result;
        foreach ((StringName templateId, EnemyTemplateDef template) in enemyTemplates)
            result[templateId] = template;
        return result;
    }

    private static GDictionary ProjectEnemyAiBrains(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains
    )
    {
        GDictionary result = new();
        if (enemyAiBrains == null)
            return result;
        foreach ((StringName brainId, EnemyAiBrainDef brain) in enemyAiBrains)
            result[brainId] = brain;
        return result;
    }

    private static GDictionary ProjectSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        GDictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
            result[skillId] = skillDef;
        return result;
    }

    private static GDictionary ProjectItemDefs(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
            result[itemId] = itemDef;
        return result;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return "";
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Nil ? "" : value.ToString();
    }

}
