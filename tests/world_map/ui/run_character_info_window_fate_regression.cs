using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_info_window_fate_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene CharacterInfoWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/character_info_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        await TestCharacterInfoWindowRendersFateSectionHappyPath();
        await TestCharacterInfoWindowRejectsBadSectionSchema();
        await TestCharacterInfoWindowRejectsBadFatePayload();
        await TestCharacterInfoWindowRejectsStringNameStringFields();
        RequestTestExit(_test.Finish("CharacterInfoWindow fate regression"));
    }

    private async Task<CharacterInfoWindow> CreateWindow()
    {
        var window = CharacterInfoWindowScene.Instantiate<CharacterInfoWindow>();
        Root.AddChild(window);
        await ProcessFrames(1);
        return window;
    }

    private async Task TestCharacterInfoWindowRendersFateSectionHappyPath()
    {
        CharacterInfoWindow window = await CreateWindow();
        window.ShowCharacter(
            new GDictionary
            {
                ["display_name"] = "黑冠见证者",
                ["meta_label"] = "战斗单位  |  玩家前排",
                ["sections"] = new GArray
                {
                    new GDictionary
                    {
                        ["title"] = "基础概览",
                        ["entries"] = new GArray
                        {
                            new GDictionary { ["label"] = "职业", ["value"] = "见厄者" },
                        },
                    },
                },
                ["fate"] = new GDictionary
                {
                    ["hidden_luck_at_birth"] = 7,
                    ["faith_luck_bonus"] = -13,
                    ["effective_luck"] = -6,
                    ["fortune_marked"] = 1,
                    ["doom_marked"] = 1,
                    ["doom_authority"] = 4,
                    ["has_misfortune"] = true,
                },
                ["status_label"] = "战斗单位",
            }
        );
        await ProcessFrames(1);

        _test.Eq(window.sections_container.GetChildCount(), 2, "显式 sections + fate payload 应渲染为两个段落。");
        List<string> renderedTexts = CollectLabelTexts(window.sections_container);
        AssertHas(renderedTexts, "命运", "happy path 应追加命运段落标题。");
        AssertHas(renderedTexts, "生来暗运：", "happy path 应渲染生来暗运标签。");
        AssertHas(renderedTexts, "+7", "happy path 应按原值显示 hidden_luck_at_birth=+7。");
        AssertHas(renderedTexts, "信仰赐运：", "happy path 应渲染信仰赐运标签。");
        AssertHas(renderedTexts, "-13", "happy path 应渲染 faith_luck_bonus。");
        AssertHas(renderedTexts, "有效运势：", "happy path 应渲染有效运势标签。");
        AssertHas(renderedTexts, "-6", "happy path 应渲染 effective_luck=-6。");
        AssertHas(renderedTexts, "1（已获福印）", "happy path 应渲染 fortune_marked。");
        AssertHas(renderedTexts, "1（已见黑兆）", "happy path 应渲染 doom_marked。");
        AssertHas(renderedTexts, "厄权：", "已入 Misfortune 时应显示 doom_authority 标签。");
        AssertHas(renderedTexts, "4 级", "已入 Misfortune 时应显示 doom_authority 值。");
        AssertHas(
            renderedTexts,
            "生来暗运已处于极端正运档，界面会按原值保留该刻印。",
            "hidden_luck_at_birth=+7 时应给出极端正运提示。"
        );
        AssertHas(
            renderedTexts,
            "有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。",
            "effective_luck=-6 时应给出下限提示。"
        );

        await DisposeNode(window);
    }

    private async Task TestCharacterInfoWindowRejectsBadSectionSchema()
    {
        CharacterInfoWindow window = await CreateWindow();

        GDictionary missingSections = MakeValidCharacterInfoPayload();
        missingSections.Remove("sections");
        window.ShowCharacter(missingSections);
        await ProcessFrames(1);
        _test.False(window.Visible, "缺少 sections 的人物信息 payload 应拒绝。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "缺少 sections 时不应渲染默认 section。");

        window.ShowCharacter(MakeValidCharacterInfoPayload(new GDictionary { ["type_label"] = "世界 NPC" }));
        await ProcessFrames(1);
        _test.False(window.Visible, "含旧 type_label 的人物信息 payload 应拒绝。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "旧 top-level 字段不应被忽略后继续渲染。");

        window.ShowCharacter(
            MakeValidCharacterInfoPayload(
                new GDictionary
                {
                    ["sections"] = new GArray
                    {
                        new GDictionary
                        {
                            ["title"] = "旧段落",
                            ["entries"] = new GArray { new GDictionary { ["text"] = "正式 text entry。" } },
                            ["body"] = "旧 body 字段。",
                            ["rows"] = new GArray { new GDictionary { ["text"] = "旧 rows 字段。" } },
                            ["lines"] = new GArray { "旧 lines 字段。" },
                        },
                    },
                }
            )
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "section 含旧 body/rows/lines 字段时应拒绝整份 payload。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "旧 body/rows/lines 字段不应被忽略后继续渲染。");

        window.ShowCharacter(
            MakeValidCharacterInfoPayload(
                new GDictionary
                {
                    ["sections"] = new GArray
                    {
                        new GDictionary
                        {
                            ["title"] = "装备摘要",
                            ["entries"] = new GArray { new GDictionary { ["value"] = "塔盾" } },
                        },
                    },
                }
            )
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "value-only entry 不属于当前 schema，应拒绝整份 payload。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "value-only entry 不应被转换成 text entry。");

        window.ShowCharacter(
            MakeValidCharacterInfoPayload(
                new GDictionary
                {
                    ["sections"] = new GArray
                    {
                        new GDictionary
                        {
                            ["title"] = "基础概览",
                            ["entries"] = new GArray
                            {
                                new GDictionary
                                {
                                    ["label"] = "职业",
                                    ["value"] = "旅人",
                                    ["text"] = "旧混合条目。",
                                },
                            },
                        },
                    },
                }
            )
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "entry 只能是 {label,value} 或 {text}，混合字段应拒绝。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "混合 entry 不应按 label/value 局部渲染。");

        await DisposeNode(window);
    }

    private async Task TestCharacterInfoWindowRejectsBadFatePayload()
    {
        CharacterInfoWindow window = await CreateWindow();

        window.ShowCharacter(MakeValidCharacterInfoPayload(new GDictionary { ["fate"] = default(Variant) }));
        await ProcessFrames(1);
        _test.False(window.Visible, "显式 null fate payload 应拒绝整个人物信息 payload。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "显式 null fate 不应被当成缺省 fate 渲染基础 section。");

        window.ShowCharacter(
            new GDictionary
            {
                ["display_name"] = "未命名旅人",
                ["meta_label"] = "战斗单位",
                ["sections"] = BaseSections(),
                ["fate"] = new GDictionary { ["hidden_luck_at_birth"] = -6 },
                ["status_label"] = "",
            }
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "缺字段 fate payload 应拒绝整个人物信息 payload。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "缺字段 fate payload 不应保留基础 section。");

        window.ShowCharacter(
            new GDictionary
            {
                ["display_name"] = "字符串运势旅人",
                ["meta_label"] = "战斗单位",
                ["sections"] = BaseSections(),
                ["fate"] = new GDictionary
                {
                    ["hidden_luck_at_birth"] = -6,
                    ["faith_luck_bonus"] = 0,
                    ["effective_luck"] = "-6",
                    ["fortune_marked"] = 0,
                    ["doom_marked"] = 0,
                    ["doom_authority"] = 0,
                    ["has_misfortune"] = false,
                },
                ["status_label"] = "",
            }
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "错类型 fate payload 不应被字符串转 int 后展示。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "错类型 fate payload 不应渲染任何 section。");

        await DisposeNode(window);
    }

    private async Task TestCharacterInfoWindowRejectsStringNameStringFields()
    {
        CharacterInfoWindow window = await CreateWindow();

        window.ShowCharacter(
            MakeValidCharacterInfoPayload(new GDictionary { ["display_name"] = new StringName("字符串名旅人") })
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "StringName display_name 不应被窗口当成正式字符串渲染。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "StringName display_name 不应继续渲染 section。");

        window.ShowCharacter(
            MakeValidCharacterInfoPayload(
                new GDictionary
                {
                    ["sections"] = new GArray
                    {
                        new GDictionary
                        {
                            ["title"] = new StringName("基础概览"),
                            ["entries"] = new GArray
                            {
                                new GDictionary { ["label"] = new StringName("职业"), ["value"] = "旅人" },
                            },
                        },
                    },
                }
            )
        );
        await ProcessFrames(1);
        _test.False(window.Visible, "StringName section/title 字段不应被窗口当成正式字符串渲染。");
        _test.Eq(window.sections_container.GetChildCount(), 0, "StringName section/title 字段不应继续渲染 section。");

        await DisposeNode(window);
    }

    private static GDictionary MakeValidCharacterInfoPayload(GDictionary overrides = null)
    {
        GDictionary data = new()
        {
            ["display_name"] = "严格旅人",
            ["meta_label"] = "战斗单位",
            ["sections"] = BaseSections(),
            ["status_label"] = "",
        };
        if (overrides != null)
        {
            foreach (Variant key in overrides.Keys)
                data[key] = overrides[key];
        }
        return data;
    }

    private static GArray BaseSections()
    {
        return new GArray
        {
            new GDictionary
            {
                ["title"] = "基础概览",
                ["entries"] = new GArray
                {
                    new GDictionary { ["label"] = "职业", ["value"] = "旅人" },
                },
            },
        };
    }

    private void AssertHas(List<string> texts, string expected, string message)
    {
        _test.True(texts.Contains(expected), message);
    }

    private static List<string> CollectLabelTexts(Node node)
    {
        var texts = new List<string>();
        if (node is Label label)
            texts.Add(label.Text);
        foreach (Node child in node.GetChildren())
            texts.AddRange(CollectLabelTexts(child));
        return texts;
    }

    private async Task DisposeNode(Node node)
    {
        node.QueueFree();
        await ProcessFrames(1);
    }

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}
