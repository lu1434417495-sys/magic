using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_enemy_template_attribute_projection_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEnemyTemplateTypedOverridesProjectIntoEncounterRosterBuilder();
        Quit(_test.Finish("Enemy template attribute projection regression"));
    }

    private void TestEnemyTemplateTypedOverridesProjectIntoEncounterRosterBuilder()
    {
        using GameSession gameSession = new();
        using EnemyTemplateDef template = BuildTemplate();
        using EncounterRosterBuilder builder = new();

        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDef>(
            gameSession.GetEnemyTemplatesTyped()
        )
        {
            [template.template_id] = template
        };
        builder.Setup(new Dictionary<StringName, WildEncounterRosterDef>(), enemyTemplates);

        IReadOnlyDictionary<StringName, int> typedBaseAttributes =
            template.GetBaseAttributeOverridesResolvedTyped();
        IReadOnlyDictionary<StringName, int> typedAttributeOverrides =
            template.GetAttributeOverridesTyped();

        _test.Eq(typedBaseAttributes.Count, 6, "typed base attribute override map 应完整包含六维基础属性。");
        _test.Eq(
            typedBaseAttributes[new StringName("strength")],
            14,
            "typed base attribute override 应解析正式 StringName key strength。"
        );
        _test.Eq(
            typedBaseAttributes[new StringName("agility")],
            11,
            "typed base attribute override 应解析 StringName key agility。"
        );
        _test.Eq(
            typedAttributeOverrides[AttributeService.ToStringName(AttributeIdKind.HpMax)],
            37,
            "typed attribute override 应保留 hp_max。"
        );
        _test.Eq(
            template.GetSkillLevelTyped("basic_attack", 1),
            3,
            "typed skill level 读取应支持正式 StringName key basic_attack。"
        );

        GArray enemyUnits = builder.BuildEnemyUnitsTyped(
            BuildEncounterAnchor(template.template_id),
            gameSession.GetSkillDefsTyped(),
            enemyTemplates,
            gameSession.GetEnemyAiBrainsTyped(),
            gameSession.GetItemDefsTyped()
        );

        _test.Eq(enemyUnits.Count, 1, "自定义敌方模板应只构建一个敌方单位。");
        if (enemyUnits.Count == 0)
        {
            return;
        }

        BattleUnitState unit = enemyUnits[0].AsGodotObject() as BattleUnitState;
        _test.True(unit != null, "EncounterRosterBuilder 应返回 BattleUnitState。");
        if (unit == null || unit.attribute_snapshot == null)
        {
            return;
        }

        AttributeSnapshot snapshot = unit.attribute_snapshot;
        _test.Eq(snapshot.GetValue("strength"), 14, "敌方单位 snapshot 应使用 typed base attribute strength。");
        _test.Eq(snapshot.GetValue("agility"), 11, "敌方单位 snapshot 应使用 typed base attribute agility。");
        _test.Eq(snapshot.GetValue("constitution"), 12, "敌方单位 snapshot 应使用 typed base attribute constitution。");
        _test.Eq(snapshot.GetValue("perception"), 9, "敌方单位 snapshot 应使用 typed base attribute perception。");
        _test.Eq(snapshot.GetValue("intelligence"), 8, "敌方单位 snapshot 应使用 typed base attribute intelligence。");
        _test.Eq(snapshot.GetValue("willpower"), 10, "敌方单位 snapshot 应使用 typed base attribute willpower。");
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)),
            37,
            "敌方单位 snapshot 应应用 typed hp_max override。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax)),
            13,
            "敌方单位 snapshot 应应用 typed stamina_max override。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints)),
            2,
            "敌方单位 snapshot 应应用 typed action_points override。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus)),
            4,
            "敌方单位 snapshot 应应用 typed armor_ac_bonus override。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus)),
            1,
            "敌方单位 snapshot 应应用 typed dodge_bonus override。"
        );
        _test.Eq(
            snapshot.GetValue("fortune_mark_target"),
            1,
            "elite target_rank 应继续映射 fortune_mark_target。"
        );
        _test.Eq(
            unit.current_hp,
            37,
            "敌方单位 current_hp 应从 typed snapshot 的 hp_max 初始化。"
        );
        _test.Eq(
            unit.current_stamina,
            13,
            "敌方单位 current_stamina 应从 typed snapshot 的 stamina_max 初始化。"
        );
        _test.Eq(
            unit.current_ap,
            2,
            "敌方单位 current_ap 应从 typed snapshot 的 action_points 初始化。"
        );
        _test.Eq(
            unit.known_skill_level_map.Get("basic_attack", 0),
            3,
            "敌方单位 known_skill_level_map 应使用 typed skill_level_map 结果。"
        );
    }

    private static EncounterAnchorData BuildEncounterAnchor(StringName templateId)
    {
        return new EncounterAnchorData
        {
            entity_id = "typed_attribute_anchor",
            display_name = "Typed Attribute Enemy",
            world_coord = new Vector2I(6, 6),
            faction_id = "hostile",
            enemy_roster_template_id = templateId,
            region_tag = "typed_tests",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "",
            growth_stage = 0,
            suppressed_until_step = 0,
        };
    }

    private static EnemyTemplateDef BuildTemplate()
    {
        EnemyTemplateDef template = new()
        {
            template_id = "typed_attribute_enemy",
            display_name = "Typed Attribute Enemy",
            brain_id = "melee_aggressor",
            initial_state_id = "engage",
            enemy_count = 1,
            target_rank = "elite",
        };
        template.base_attribute_overrides[new StringName("strength")] = 14;
        template.base_attribute_overrides[new StringName("agility")] = 11;
        template.base_attribute_overrides[new StringName("constitution")] = 12;
        template.base_attribute_overrides[new StringName("perception")] = 9;
        template.base_attribute_overrides[new StringName("intelligence")] = 8;
        template.base_attribute_overrides[new StringName("willpower")] = 10;
        template.attribute_overrides[AttributeService.ToStringName(AttributeIdKind.HpMax)] = 37;
        template.attribute_overrides[AttributeService.ToStringName(AttributeIdKind.StaminaMax)] = 13;
        template.attribute_overrides[AttributeService.ToStringName(AttributeIdKind.ActionPoints)] = 2;
        template.attribute_overrides[AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus)] = 4;
        template.attribute_overrides[AttributeService.ToStringName(AttributeIdKind.DodgeBonus)] = 1;
        template.skill_ids.Add("basic_attack");
        template.skill_level_map[new StringName("basic_attack")] = 3;
        return template;
    }

    private static int DictInt(GDictionary dictionary, StringName key, int fallback)
    {
        if (dictionary == null)
        {
            return fallback;
        }
        if (dictionary.ContainsKey(key))
        {
            Variant value = dictionary[key];
            if (value.VariantType == Variant.Type.Int)
            {
                return value.AsInt32();
            }
        }
        if (dictionary.ContainsKey(key.ToString()))
        {
            Variant value = dictionary[key.ToString()];
            if (value.VariantType == Variant.Type.Int)
            {
                return value.AsInt32();
            }
        }
        return fallback;
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

}
