using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_skill_book_item_helpers_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSkillBookFactoryGeneratesTypedItemDefs();
        TestSkillBookValidatorReportsCrossTableErrors();
        TestSkillBookHelpersArePlainStaticHelpers();

        if (_failures.Count == 0)
        {
            GD.Print("Skill book item helpers regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Skill book item helpers regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestSkillBookFactoryGeneratesTypedItemDefs()
    {
        Dictionary<StringName, SkillDef> skillDefs = new()
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
                item_category = ItemDef.ITEM_CATEGORY_SKILL_BOOK(),
                granted_skill_id = "existing_book",
            },
        };
        skillDefs["existing_book"] = BuildSkill("existing_book", "已有技能书", "book");

        Dictionary<StringName, ItemDef> generated = SkillBookItemFactory.BuildGeneratedItemDefs(
            skillDefs,
            existingItemDefs
        );

        StringName aimedShotItemId = SkillBookItemFactory.BuildItemIdForSkill("archer_aimed_shot");
        AssertTrue(generated.ContainsKey(aimedShotItemId), "book 来源技能应生成技能书物品。");
        ItemDef generatedBook = generated[aimedShotItemId];
        AssertEq(generatedBook.item_id, aimedShotItemId, "技能书 item_id 应使用 canonical id。");
        AssertEq(generatedBook.display_name, "精准射击 技能书", "技能书显示名应来自 display_name。");
        AssertTrue(generatedBook.description.Contains("精准射击"), "技能书说明应包含技能显示名。");
        AssertEq(generatedBook.icon, "res://icon.svg", "技能书应使用默认图标。");
        AssertEq(generatedBook.max_stack, 20, "技能书默认最大堆叠应为 20。");
        AssertEq(generatedBook.item_category, ItemDef.ITEM_CATEGORY_SKILL_BOOK(), "技能书分类应为 skill_book。");
        AssertEq(generatedBook.granted_skill_id, new StringName("archer_aimed_shot"), "技能书应授予对应技能。");
        AssertTrue(
            !generated.ContainsKey("skill_book_blank_display"),
            "缺少 display_name 的 book 技能不应生成技能书。"
        );
        AssertTrue(
            !generated.ContainsKey("skill_book_teacher_only"),
            "非 book learn_source 不应生成技能书。"
        );
        AssertTrue(
            !generated.ContainsKey("skill_book_existing_book"),
            "已有 canonical item 时不应重复生成。"
        );
    }

    private void TestSkillBookValidatorReportsCrossTableErrors()
    {
        Dictionary<StringName, SkillDef> skillDefs = new()
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
                item_category = ItemDef.ITEM_CATEGORY_MISC(),
            },
            ["skill_book_wrong_grant_skill"] = BuildSkillBookItem(
                "skill_book_wrong_grant_skill",
                "book_skill"
            ),
        };

        List<string> errors = SkillBookItemContentValidator.Validate(itemDefs, skillDefs);

        AssertContains(
            errors,
            "Skill book item manual_missing references missing skill missing_skill.",
            "缺失技能引用应报错。"
        );
        AssertContains(
            errors,
            "Skill book item manual_teacher granted_skill_id teacher_skill learn_source must be book, got teacher.",
            "技能书引用非 book 技能应报错。"
        );
        AssertContains(
            errors,
            "Item skill_book_collision_skill occupies generated skill book id for skill collision_skill but item_category must be skill_book.",
            "canonical id 被非技能书占用应报错。"
        );
        AssertContains(
            errors,
            "Skill book item skill_book_wrong_grant_skill occupies generated skill book id for skill wrong_grant_skill but grants book_skill.",
            "canonical id 技能书授予错误技能应报错。"
        );
    }

    private void TestSkillBookHelpersArePlainStaticHelpers()
    {
        AssertPlainStaticHelper(typeof(SkillBookItemFactory), "SkillBookItemFactory");
        AssertPlainStaticHelper(typeof(SkillBookItemContentValidator), "SkillBookItemContentValidator");

        MethodInfo buildGenerated = typeof(SkillBookItemFactory).GetMethod(
            nameof(SkillBookItemFactory.BuildGeneratedItemDefs),
            BindingFlags.Public | BindingFlags.Static
        );
        AssertEq(
            buildGenerated?.ReturnType,
            typeof(Dictionary<StringName, ItemDef>),
            "BuildGeneratedItemDefs 应返回 typed item-def map。"
        );

        MethodInfo validate = typeof(SkillBookItemContentValidator).GetMethod(
            nameof(SkillBookItemContentValidator.Validate),
            BindingFlags.Public | BindingFlags.Static
        );
        AssertEq(validate?.ReturnType, typeof(List<string>), "Validate 应返回 typed List<string>。");
    }

    private static SkillDef BuildSkill(string skillId, string displayName, string learnSource) =>
        new()
        {
            skill_id = new StringName(skillId),
            display_name = displayName,
            description = $"{displayName} description",
            learn_source = new StringName(learnSource),
            skill_type = "passive",
            max_level = 1,
        };

    private static ItemDef BuildSkillBookItem(string itemId, string grantedSkillId) =>
        new()
        {
            item_id = new StringName(itemId),
            item_category = ItemDef.ITEM_CATEGORY_SKILL_BOOK(),
            granted_skill_id = new StringName(grantedSkillId),
        };

    private void AssertPlainStaticHelper(System.Type type, string typeName)
    {
        AssertTrue(type.IsAbstract && type.IsSealed, $"{typeName} 应是 C# static helper。");
        AssertTrue(!typeof(RefCounted).IsAssignableFrom(type), $"{typeName} 不应继承 RefCounted。");
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            $"{typeName} 不应注册为 Godot GlobalClass。"
        );
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                string fullName = parameter.ParameterType.FullName ?? "";
                AssertTrue(
                    !fullName.StartsWith("Godot.Collections.Dictionary"),
                    $"{typeName}.{method.Name} 不应公开 Godot Dictionary 参数。"
                );
            }
        }
    }

    private void AssertContains(List<string> values, string expected, string message)
    {
        if (values.Contains(expected))
            return;
        _failures.Add($"{message} | expected={expected} actual={string.Join(" | ", values)}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
