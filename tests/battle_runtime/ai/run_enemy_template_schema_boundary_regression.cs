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
        try
        {
            TestTypedSchemaValidationAcceptsTypedReferenceTables();
            TestDictionaryReferenceIndicesBuildTypedSchemaInputsFromStringNameKeys();
            TestTypedSchemaValidationRejectsMissingTypedItemReferences();
        }
        finally
        {
            _test.DisposeTrackedGodotObjects();
            GodotSharpCleanup.CollectPendingFinalizers();
        }

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

    private EnemyTemplateDef BuildValidTemplate(StringName templateId, StringName weaponItemId)
    {
        var template = _test.Track(new EnemyTemplateDef
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
        });
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

    private EnemyAiBrainDef BuildBrain(StringName brainId, StringName stateId)
    {
        return _test.Track(new EnemyAiBrainDef
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
        });
    }

    private SkillDef BuildSkill(StringName skillId, int maxLevel)
    {
        return _test.Track(new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            max_level = maxLevel,
        });
    }

    private ItemDef MakeWeapon(StringName itemId, StringName weaponTypeId)
    {
        var itemDef = _test.Track(new ItemDef
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
        });
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

    private static string FormatErrors(GStringArray errors) => string.Join(" | ", errors);

}
