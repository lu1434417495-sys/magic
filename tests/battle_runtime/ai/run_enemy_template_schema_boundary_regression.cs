using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_enemy_template_schema_boundary_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypedSchemaValidationAcceptsTypedReferenceTables();
        TestDictionaryReferenceIndicesBuildTypedSchemaInputsFromStringNameKeys();
        TestTypedSchemaValidationRejectsMissingTypedItemReferences();
        TestSaveAdvantageTagsExportFieldExists();
        TestSaveAdvantageTagsAcceptSupportedSaveModes();
        TestSaveAdvantageTagsRejectEmptyTag();
        TestSaveAdvantageTagsRejectUnsupportedBaseTag();
        TestSkillLevelMapValidationRemainsUnchanged();

        Quit(_test.Finish("Enemy template schema boundary regression"));
    }

    private void TestTypedSchemaValidationAcceptsTypedReferenceTables()
    {
        EnemyTemplateDef template = BuildValidTemplate("typed_schema_template", "typed_schema_weapon");
        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var itemDefIndex = new Dictionary<StringName, ItemDef>
        {
            [template.attack_equipment_item_id] = MakeWeapon(
                template.attack_equipment_item_id,
                "typed_schema_weapon_type"
            ),
        };
        var skillDefIndex = new Dictionary<StringName, SkillDef>
        {
            ["typed_schema_skill"] = BuildSkill("typed_schema_skill", maxLevel: 2),
        };

        GStringArray errors = template.ValidateSchemaTyped(brainIndex, itemDefIndex, skillDefIndex);
        _test.True(
            errors.Count == 0,
            $"typed ValidateSchemaTyped() 应接受正式 typed 引用表。 errors={FormatErrors(errors)}"
        );
    }

    private void TestDictionaryReferenceIndicesBuildTypedSchemaInputsFromStringNameKeys()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "dictionary_schema_template",
            "dictionary_schema_weapon"
        );
        GDictionary knownBrains = new()
        {
            [new StringName("dictionary_schema_brain")] = BuildBrain("dictionary_schema_brain", "engage"),
        };
        GDictionary itemDefs = new()
        {
            [new StringName("dictionary_schema_weapon")] = MakeWeapon(
                "dictionary_schema_weapon",
                "dictionary_schema_weapon_type"
            ),
        };
        GDictionary skillDefs = new()
        {
            [new StringName("typed_schema_skill")] = BuildSkill("typed_schema_skill", maxLevel: 2),
        };

        GStringArray errors = template.ValidateSchemaTyped(
            EnemyTemplateDef.BuildBrainIndex(knownBrains),
            EnemyTemplateDef.BuildItemDefIndex(itemDefs),
            EnemyTemplateDef.BuildSkillDefIndex(skillDefs)
        );
        _test.True(
            errors.Count == 0,
            $"typed ValidateSchemaTyped() 应接受从 StringName-key Dictionary 物化出来的正式 typed 索引。 errors={FormatErrors(errors)}"
        );
    }

    private void TestTypedSchemaValidationRejectsMissingTypedItemReferences()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "missing_item_schema_template",
            "missing_item_schema_weapon"
        );
        template.drop_entries.Add(
            new DropEntryDef
            {
                drop_entry_id = "missing_drop",
                drop_type = "item",
                item_id = "missing_drop_item",
                quantity = 1,
            }
        );

        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var skillDefIndex = new Dictionary<StringName, SkillDef>
        {
            ["typed_schema_skill"] = BuildSkill("typed_schema_skill", maxLevel: 2),
        };

        GStringArray errors = template.ValidateSchemaTyped(
            brainIndex,
            new Dictionary<StringName, ItemDef>(),
            skillDefIndex
        );
        _test.True(
            errors.Count >= 2,
            $"typed ValidateSchemaTyped() 应直接报告缺失装备和掉落 item 引用。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSaveAdvantageTagsExportFieldExists()
    {
        var property = typeof(EnemyTemplateDef).GetProperty("save_advantage_tags");
        _test.True(property != null, "EnemyTemplateDef 应公开 save_advantage_tags 导出字段。");
        _test.True(
            property != null
                && Attribute.IsDefined(property, typeof(ExportAttribute), inherit: true),
            "EnemyTemplateDef.save_advantage_tags 应使用 [Export] 暴露给模板资源。"
        );
    }

    private void TestSaveAdvantageTagsAcceptSupportedSaveModes()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "save_tag_schema_template",
            "save_tag_schema_weapon"
        );
        SetSaveAdvantageTags(
            template,
            "illusion_immunity",
            "illusion",
            "illusion_advantage",
            "illusion_disadvantage"
        );

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            errors.Count == 0,
            $"save_advantage_tags 应接受 illusion 直接优势、优势/劣势后缀和免疫后缀。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSaveAdvantageTagsRejectEmptyTag()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "empty_save_tag_schema_template",
            "empty_save_tag_schema_weapon"
        );
        SetSaveAdvantageTags(template, "");

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "save_advantage_tags"),
            $"save_advantage_tags 空元素应被 schema 拒绝。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSaveAdvantageTagsRejectUnsupportedBaseTag()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "unsupported_save_tag_schema_template",
            "unsupported_save_tag_schema_weapon"
        );
        SetSaveAdvantageTags(template, "unsupported_save_advantage");

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "unsupported_save_advantage"),
            $"save_advantage_tags 应按去除后缀后的基础豁免标签校验并拒绝未知标签。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSkillLevelMapValidationRemainsUnchanged()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "skill_level_boundary_template",
            "skill_level_boundary_weapon"
        );
        template.skill_level_map[new StringName("typed_schema_skill")] = 3;

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "skill_level_map[typed_schema_skill]"),
            $"新增 save_advantage_tags 校验不应改变 skill_level_map 上限校验。 errors={FormatErrors(errors)}"
        );
    }

    private static EnemyTemplateDef BuildValidTemplate(StringName templateId, StringName weaponItemId)
    {
        var template = new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = templateId.ToString(),
            brain_id = "dictionary_schema_brain",
            initial_state_id = "engage",
            attack_equipment_item_id = weaponItemId,
            skill_ids = new GStringNameArray { "typed_schema_skill" },
            skill_level_map = new GDictionary { [new StringName("typed_schema_skill")] = 1 },
            base_attribute_overrides = new GDictionary
            {
                [new StringName("strength")] = 10,
                [new StringName("agility")] = 10,
                [new StringName("constitution")] = 10,
                [new StringName("perception")] = 10,
                [new StringName("intelligence")] = 10,
                [new StringName("willpower")] = 10,
            },
        };
        template.drop_entries.Add(
            new DropEntryDef
            {
                drop_entry_id = "typed_schema_drop",
                drop_type = "item",
                item_id = weaponItemId,
                quantity = 1,
            }
        );
        return template;
    }

    private static EnemyAiBrainDef BuildBrain(StringName brainId, StringName stateId)
    {
        return TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = brainId,
                default_state_id = stateId,
                states = new Godot.Collections.Array<EnemyAiStateDef>
                {
                    new EnemyAiStateDef
                    {
                        state_id = stateId,
                        actions = new Godot.Collections.Array<EnemyAiAction>
                        {
                            new WaitAction { action_id = $"{stateId}_wait" },
                        },
                    },
                },
            },
            "EnemyTemplateSchemaBoundary.BuildBrain"
        );
    }

    private static SkillDef BuildSkill(StringName skillId, int maxLevel)
    {
        return TestResourceOwnership.Own(
            new SkillDef
            {
                skill_id = skillId,
                display_name = skillId.ToString(),
                max_level = maxLevel,
            },
            "EnemyTemplateSchemaBoundary.BuildSkill"
        );
    }

    private static ItemDef MakeWeapon(StringName itemId, StringName weaponTypeId)
    {
        var itemDef = new ItemDef
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
        };
        itemDef.weapon_profile = new WeaponProfileDef
        {
            weapon_type_id = weaponTypeId,
            training_group = "martial",
            range_type = "melee",
            family = "sword",
            damage_tag = ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            attack_range = 1,
            one_handed_dice = new WeaponDamageDiceDef
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            },
        };
        return itemDef;
    }

    private static GStringArray ValidateWithReferenceTables(EnemyTemplateDef template)
    {
        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var itemDefIndex = new Dictionary<StringName, ItemDef>
        {
            [template.attack_equipment_item_id] = MakeWeapon(
                template.attack_equipment_item_id,
                $"{template.attack_equipment_item_id}_type"
            ),
        };
        var skillDefIndex = new Dictionary<StringName, SkillDef>
        {
            ["typed_schema_skill"] = BuildSkill("typed_schema_skill", maxLevel: 2),
        };
        return template.ValidateSchemaTyped(brainIndex, itemDefIndex, skillDefIndex);
    }

    private static void SetSaveAdvantageTags(
        EnemyTemplateDef template,
        params StringName[] saveAdvantageTags
    )
    {
        var tags = new GStringNameArray();
        foreach (StringName tag in saveAdvantageTags ?? Array.Empty<StringName>())
        {
            tags.Add(tag);
        }

        var property = typeof(EnemyTemplateDef).GetProperty("save_advantage_tags");
        property?.SetValue(template, tags);
    }

    private static bool ContainsError(GStringArray errors, string fragment)
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string FormatErrors(GStringArray errors) => string.Join(" | ", errors);

}
