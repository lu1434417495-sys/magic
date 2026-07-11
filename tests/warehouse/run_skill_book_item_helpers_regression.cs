using System.Collections.Generic;
using Godot;

public partial class run_skill_book_item_helpers_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSkillBookFactoryGeneratesTypedItemDefs();
        TestSkillBookValidatorReportsCrossTableErrors();

        RequestTestExit(_test.Finish("Skill book item helpers regression"));
    }

    private void TestSkillBookFactoryGeneratesTypedItemDefs()
    {
        Dictionary<StringName, SkillDefinition> skillDefs = new()
        {
            ["archer_aimed_shot"] = BuildSkill("archer_aimed_shot", "精准射击", "book"),
            ["blank_display"] = BuildSkill("blank_display", "", "book"),
            ["teacher_only"] = BuildSkill("teacher_only", "导师传授", "teacher"),
        };
        Dictionary<StringName, ItemDef> existingItemDefs = new()
        {
            ["skill_book_existing_book"] = new ItemDef
            {
                item_id = "skill_book_existing_book",
                CategoryKind = ItemCategoryKind.SkillBook,
                granted_skill_id = "existing_book",
            },
        };
        skillDefs["existing_book"] = BuildSkill("existing_book", "已有技能书", "book");

        Dictionary<StringName, ItemDef> generated = SkillBookItemFactory.BuildGeneratedItemDefs(
            skillDefs,
            existingItemDefs
        );

        StringName aimedShotItemId = SkillBookItemFactory.BuildItemIdForSkill("archer_aimed_shot");
        _test.True(generated.ContainsKey(aimedShotItemId), "book 来源技能应生成技能书物品。");
        ItemDef generatedBook = generated[aimedShotItemId];
        _test.Eq(generatedBook.item_id, aimedShotItemId, "技能书 item_id 应使用 canonical id。");
        _test.True(!string.IsNullOrWhiteSpace(generatedBook.display_name), "技能书应生成非空显示名。");
        _test.True(!string.IsNullOrWhiteSpace(generatedBook.description), "技能书应生成非空说明。");
        _test.Eq(generatedBook.icon, "res://icon.svg", "技能书应使用默认图标。");
        _test.Eq(generatedBook.max_stack, 20, "技能书默认最大堆叠应为 20。");
        _test.Eq(generatedBook.CategoryKind, ItemCategoryKind.SkillBook, "技能书分类应为 skill_book。");
        _test.Eq(generatedBook.granted_skill_id, new StringName("archer_aimed_shot"), "技能书应授予对应技能。");
        _test.True(
            !generated.ContainsKey("skill_book_blank_display"),
            "缺少 display_name 的 book 技能不应生成技能书。"
        );
        _test.True(
            !generated.ContainsKey("skill_book_teacher_only"),
            "非 book learn_source 不应生成技能书。"
        );
        _test.True(
            !generated.ContainsKey("skill_book_existing_book"),
            "已有 canonical item 时不应重复生成。"
        );
    }

    private void TestSkillBookValidatorReportsCrossTableErrors()
    {
        Dictionary<StringName, SkillDefinition> skillDefs = new()
        {
            ["book_skill"] = BuildSkill("book_skill", "书本技能", "book"),
            ["teacher_skill"] = BuildSkill("teacher_skill", "导师技能", "teacher"),
            ["collision_skill"] = BuildSkill("collision_skill", "冲突技能", "book"),
            ["wrong_grant_skill"] = BuildSkill("wrong_grant_skill", "授予错误", "book"),
        };
        Dictionary<StringName, ItemDef> itemDefs = new()
        {
            ["manual_missing"] = BuildSkillBookItem("manual_missing", "missing_skill"),
            ["manual_teacher"] = BuildSkillBookItem("manual_teacher", "teacher_skill"),
            ["skill_book_collision_skill"] = new ItemDef
            {
                item_id = "skill_book_collision_skill",
                CategoryKind = ItemCategoryKind.Misc,
            },
            ["skill_book_wrong_grant_skill"] = BuildSkillBookItem(
                "skill_book_wrong_grant_skill",
                "book_skill"
            ),
        };

        List<string> errors = SkillBookItemContentValidator.Validate(itemDefs, skillDefs);

        _test.True(errors.Count >= 4, "非法技能书 fixture 应保持非法。");
    }

    private static SkillDefinition BuildSkill(
        string skillId,
        string displayName,
        string learnSource
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            new StringName(skillId),
            displayName: displayName,
            skillType: "passive",
            learnSource: new StringName(learnSource),
            maxLevel: 1
        );

    private static ItemDef BuildSkillBookItem(string itemId, string grantedSkillId) =>
        new()
        {
            item_id = new StringName(itemId),
            CategoryKind = ItemCategoryKind.SkillBook,
            granted_skill_id = new StringName(grantedSkillId),
        };


}
