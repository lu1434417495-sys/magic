using System.Collections.Generic;
using Godot;

public partial class run_skill_book_item_helpers_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
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
        Dictionary<StringName, ItemDefinition> existingItemDefs = new()
        {
            ["skill_book_existing_book"] = BuildSkillBookItem(
                "skill_book_existing_book",
                "existing_book"
            ),
        };
        skillDefs["existing_book"] = BuildSkill("existing_book", "已有技能书", "book");

        IReadOnlyDictionary<StringName, ItemDefinition> generated =
            SkillBookItemFactory.BuildGeneratedItemDefinitions(skillDefs, existingItemDefs);

        _test.True(
            RejectsMutation(generated),
            "generated skill-book index should reject mutation through any dictionary capability it exposes."
        );

        StringName aimedShotItemId = SkillBookItemFactory.BuildItemIdForSkill("archer_aimed_shot");
        _test.True(generated.ContainsKey(aimedShotItemId), "book 来源技能应生成技能书物品。");
        ItemDefinition generatedBook = generated[aimedShotItemId];
        _test.Eq(generatedBook.ItemId, aimedShotItemId, "技能书 item_id 应使用 canonical id。");
        _test.True(!string.IsNullOrWhiteSpace(generatedBook.DisplayName), "技能书应生成非空显示名。");
        _test.True(!string.IsNullOrWhiteSpace(generatedBook.Description), "技能书应生成非空说明。");
        _test.Eq(
            generatedBook.IconAssetId,
            EngineAssetIds.DefaultItemIcon,
            "技能书应使用默认图标 asset id。"
        );
        _test.Eq(generatedBook.MaxStack, 20, "技能书默认最大堆叠应为 20。");
        _test.Eq(generatedBook.CategoryKind, ItemCategoryKind.SkillBook, "技能书分类应为 skill_book。");
        _test.Eq(generatedBook.GrantedSkillId, new StringName("archer_aimed_shot"), "技能书应授予对应技能。");
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
        _test.Eq(existingItemDefs.Count, 1, "generation must not mutate existing item index.");
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
        Dictionary<StringName, ItemDefinition> itemDefs = new()
        {
            ["manual_missing"] = BuildSkillBookItem("manual_missing", "missing_skill"),
            ["manual_teacher"] = BuildSkillBookItem("manual_teacher", "teacher_skill"),
            ["skill_book_collision_skill"] = BuildItem(
                "skill_book_collision_skill",
                ItemCategoryKind.Misc,
                ""
            ),
            ["skill_book_wrong_grant_skill"] = BuildSkillBookItem(
                "skill_book_wrong_grant_skill",
                "book_skill"
            ),
        };

        List<string> errors = SkillBookItemContentValidator.Validate(itemDefs, skillDefs);

        _test.Eq(errors.Count, 4, $"非法技能书 fixture 应只触发四项目标规则: {string.Join(" | ", errors)}");
        AssertHasError(
            errors,
            "Skill book item manual_missing references missing skill missing_skill.",
            "缺失技能引用应命中 manual_missing 的精确诊断。"
        );
        AssertHasError(
            errors,
            "Skill book item manual_teacher granted_skill_id teacher_skill learn_source must be book, got teacher.",
            "非 book learn_source 应命中 manual_teacher 的精确诊断。"
        );
        AssertHasError(
            errors,
            "Item skill_book_collision_skill occupies generated skill book id for skill collision_skill but item_category must be skill_book.",
            "canonical category collision 应命中 collision_skill 的精确诊断。"
        );
        AssertHasError(
            errors,
            "Skill book item skill_book_wrong_grant_skill occupies generated skill book id for skill wrong_grant_skill but grants book_skill.",
            "错误 granted skill 应命中 wrong_grant_skill 的精确诊断。"
        );
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

    private static ItemDefinition BuildSkillBookItem(string itemId, string grantedSkillId) =>
        BuildItem(itemId, ItemCategoryKind.SkillBook, grantedSkillId);

    private static ItemDefinition BuildItem(
        string itemId,
        ItemCategoryKind category,
        string grantedSkillId
    )
    {
        TestItemDefinitionBuilder raw = new()
        {
            item_id = new StringName(itemId),
            CategoryKind = category,
            granted_skill_id = new StringName(grantedSkillId),
        };
        return raw.ToDefinition();
    }

    private void AssertHasError(IReadOnlyList<string> errors, string fragment, string message)
    {
        foreach (string error in errors ?? new List<string>())
        {
            if ((error ?? "").Contains(fragment))
                return;
        }
        _test.Fail($"{message} expected={fragment} errors={string.Join(" | ", errors ?? new List<string>())}");
    }

    private static bool RejectsMutation(
        IReadOnlyDictionary<StringName, ItemDefinition> snapshot
    )
    {
        if (snapshot is not IDictionary<StringName, ItemDefinition> mutableSnapshot)
            return true;
        try
        {
            mutableSnapshot.Clear();
            return false;
        }
        catch (System.NotSupportedException)
        {
            return true;
        }
    }


}
