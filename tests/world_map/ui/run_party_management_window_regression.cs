using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_party_management_window_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene PartyManagementWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/party_management_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        await TestWindowUsesHalfViewportWithMinimumSize();
        await TestLeaderToReserveEmitsRosterBeforeLeader();
        await TestMemberDetailsTolerateMissingSkillAndOccupiedSlots();
        await TestMemberDetailsUseSkillDefinitionSnapshot();
        await TestMemberDetailsUseInjectedCharacterManagementSnapshot();
        RequestTestExit(_test.Finish("Party management window regression"));
    }

    private async Task TestWindowUsesHalfViewportWithMinimumSize()
    {
        Root.Size = new Vector2I(1920, 1080);
        PartyManagementWindow window = await CreateWindow(new Vector2(1920, 1080));
        window.ShowParty(BuildPartyState(new[] { new StringName("hero") }));
        await ProcessFrames(1);

        Control panel = window.GetNode<Control>("%Panel");
        AssertVector2Near(panel.CustomMinimumSize, new Vector2(960, 540), 0.1f, "1920x1080 下队伍管理窗口应使用半屏尺寸。");
        _test.True(window.GetNodeOrNull("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/概览/OverviewLabel") != null, "概览应在右侧详情标签页内。");
        _test.True(window.GetNodeOrNull("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/属性/AttributesLabel") != null, "属性标签页应保留。");
        _test.True(window.GetNodeOrNull("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/装备/EquipmentLabel") != null, "装备标签页应保留。");
        _test.True(window.GetNodeOrNull("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/技能/SkillsLabel") != null, "技能标签页应保留。");
        _test.True(window.GetNodeOrNull("CenterContainer/Panel/MarginContainer/Content/Body/DetailsTabs/职业/ProfessionsLabel") != null, "职业标签页应保留。");

        await DisposeNode(window);

        Root.Size = new Vector2I(1000, 700);
        window = await CreateWindow(new Vector2(1000, 700));
        window.ShowParty(BuildPartyState(new[] { new StringName("hero") }));
        await ProcessFrames(1);

        panel = window.GetNode<Control>("%Panel");
        AssertVector2Near(panel.CustomMinimumSize, new Vector2(860, 540), 0.1f, "小窗口下队伍管理窗口应使用可读保底尺寸。");
        _test.True(panel.CustomMinimumSize.X <= 1000.0f - 96.0f, "保底宽度不应超过横向安全区域。");
        _test.True(panel.CustomMinimumSize.Y <= 700.0f - 60.0f, "保底高度不应超过纵向安全区域。");

        await DisposeNode(window);
        Root.Size = new Vector2I(1280, 720);
    }

    private async Task TestLeaderToReserveEmitsRosterBeforeLeader()
    {
        PartyManagementWindow window = await CreateWindow();
        var partyState = new PartyState();
        PartyMemberState leader = MakeMember("leader", "队长");
        PartyMemberState ally = MakeMember("ally", "队友");
        partyState.leader_member_id = "leader";
        partyState.active_member_ids = new StringNameList { "leader", "ally" };
        partyState.reserve_member_ids = new StringNameList();
        partyState.SetMemberState(leader);
        partyState.SetMemberState(ally);

        var eventOrder = new List<string>();
        var rosterPayloads = new List<(StringNameList Active, StringNameList Reserve)>();
        var leaderPayloads = new List<StringName>();
        window.roster_change_requested += (activeMemberIds, reserveMemberIds) =>
        {
            eventOrder.Add("roster");
            rosterPayloads.Add((new StringNameList(activeMemberIds), new StringNameList(reserveMemberIds)));
        };
        window.leader_change_requested += memberId =>
        {
            eventOrder.Add("leader");
            leaderPayloads.Add(memberId);
        };

        window.ShowParty(partyState);
        await ProcessFrames(1);
        _test.True(window.SelectMember("leader"), "测试应能选中当前队长。");
        window._on_move_to_reserve_button_pressed();
        await ProcessFrames(1);

        AssertStringList(eventOrder, new[] { "roster", "leader" }, "队长移入替补时应先发 roster_change，再发 leader_change。");
        _test.Eq(leaderPayloads.Count, 1, "队长移入替补应只发一次 leader_change。");
        if (leaderPayloads.Count > 0)
            _test.Eq(leaderPayloads[0], new StringName("ally"), "队长移入替补后应选择剩余上阵成员为新队长。");
        _test.Eq(rosterPayloads.Count, 1, "队长移入替补应只发一次 roster_change。");
        if (rosterPayloads.Count > 0)
        {
            _test.True(rosterPayloads[0].Active.Contains("ally"), "roster_change active payload 应包含新队长。");
            _test.False(rosterPayloads[0].Active.Contains("leader"), "roster_change active payload 不应继续包含已下阵队长。");
            _test.True(rosterPayloads[0].Reserve.Contains("leader"), "roster_change reserve payload 应包含已下阵队长。");
        }

        await DisposeNode(window);
    }

    private async Task TestMemberDetailsTolerateMissingSkillAndOccupiedSlots()
    {
        PartyManagementWindow window = await CreateWindow();

        var partyState = new PartyState();
        PartyMemberState hero = MakeMember("hero", "主角");
        var missingSkill = new UnitSkillProgress
        {
            skill_id = "missing_skill",
            is_learned = true,
            skill_level = 2,
        };
        hero.progression.SetSkillProgress(missingSkill);

        EquipmentInstanceState equipmentInstance = EquipmentInstanceState.CreateInstance(
            "iron_greatsword",
            "eq_party_window_001"
        );
        hero.equipment_state = EquipmentState.FromDictionary(
            new GDictionary
            {
                ["equipped_slots"] = new GDictionary
                {
                    ["main_hand"] = new GDictionary
                    {
                        ["occupied_slot_ids"] = new GArray { "main_hand", "off_hand" },
                        ["equipment_instance"] = equipmentInstance.ToDictionary(),
                    },
                },
            }
        );
        _test.True(hero.equipment_state != null, "测试装备状态应能通过字典入口构造双手武器。");
        partyState.leader_member_id = "hero";
        partyState.active_member_ids = new StringNameList { "hero" };
        partyState.reserve_member_ids = new StringNameList();
        partyState.SetMemberState(hero);

        window.ShowParty(partyState);
        await ProcessFrames(1);
        _test.True(window.SelectMember("hero"), "测试应能选中主角。");
        await ProcessFrames(1);

        string equipmentText = window.equipment_label.Text;
        string skillsText = window.skills_label.Text;
        int weaponMentions = equipmentText.Split("iron_greatsword").Length - 1;
        _test.True(weaponMentions == 1, "双手武器应仅计为一件，不应因副手占位重复展示。");
        _test.True(equipmentText.Contains("副手：由主手占用"), "副手占位应显示为被主手占用。");
        _test.True(skillsText.Contains("missing_skill"), "缺失 skill_def 时仍应展示技能 ID。");
        _test.True(skillsText.Contains("技能定义缺失"), "缺失 skill_def 时应显示缺失提示而不是崩溃。");

        await DisposeNode(window);
    }

    private async Task TestMemberDetailsUseInjectedCharacterManagementSnapshot()
    {
        PartyManagementWindow window = await CreateWindow();

        PartyState partyState = BuildPartyState(new[] { new StringName("hero") });
        PartyMemberState hero = partyState.GetMemberState("hero");
        hero.current_hp = 12;
        hero.current_mp = 3;
        hero.progression.unit_base_attributes.SetAttributeValue("strength", 15);
        var manager = new CharacterManagementModule();
        manager.setup(partyState);
        window.SetCharacterManagement(manager);
        window.ShowParty(partyState);
        await ProcessFrames(1);
        _test.True(window.SelectMember("hero"), "测试应能选中主角。");
        await ProcessFrames(1);

        AttributeSnapshot snapshot = manager.GetMemberAttributeSnapshotForEquipmentView(
            hero.member_id,
            hero.equipment_state
        );
        string overviewText = window.overview_label.Text;
        string attributesText = window.attributes_label.Text;
        int expectedHpMax = snapshot.GetValue("hp_max");
        int expectedMpMax = snapshot.GetValue("mp_max");
        int expectedStrength = snapshot.GetValue("strength");
        _test.True(
            overviewText.Contains($"HP {hero.current_hp} / {expectedHpMax}  MP {hero.current_mp} / {expectedMpMax}"),
            "概览资源值应来自注入的角色管理快照。"
        );
        _test.True(
            attributesText.Contains($"力量：{expectedStrength}"),
            "属性页基础属性应来自注入的角色管理快照。"
        );

        await DisposeNode(window);
    }

    private async Task TestMemberDetailsUseSkillDefinitionSnapshot()
    {
        PartyManagementWindow window = await CreateWindow();
        var skillId = new StringName("dto_fire_spark");
        PartyState partyState = BuildPartyState(new[] { new StringName("hero") });
        PartyMemberState hero = partyState.GetMemberState("hero");
        hero.progression.character_level = 7;
        hero.progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = skillId,
                is_learned = true,
                skill_level = 1,
                current_mastery = 12,
                total_mastery_earned = 34,
            }
        );
        window.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [skillId] = BuildWindowSkillDefinition(skillId),
            }
        );
        window.ShowParty(partyState);
        await ProcessFrames(1);
        _test.True(window.SelectMember("hero"), "测试应能选中带有 DTO 技能的主角。");
        await ProcessFrames(1);

        string skillsText = window.skills_label.Text;
        _test.True(skillsText.Contains("DTO火花  Lv.1"), "技能页应使用 SkillDefinition 显示名。");
        _test.True(skillsText.Contains("类型：战斗"), "技能页应使用 SkillDefinition 技能类型。");
        _test.True(skillsText.Contains("说明：来自 SkillDefinition 快照"), "技能页应使用 SkillDefinition 描述。");
        _test.True(
            skillsText.Contains("当前效果：消耗 3 AP，最大 7；快照文本"),
            "技能页当前效果应由 SkillDefinition 等级描述、战斗配置和 runtime context 生成。"
        );
        _test.True(
            skillsText.Contains("升级预览：Lv.2：AP→5，冷却→20"),
            "技能页升级预览应读取 SkillDefinition combat level override。"
        );

        await DisposeNode(window);
    }

    private async Task<PartyManagementWindow> CreateWindow(Vector2? size = null)
    {
        var window = PartyManagementWindowScene.Instantiate<PartyManagementWindow>();
        Root.AddChild(window);
        window.AnchorRight = 0.0f;
        window.AnchorBottom = 0.0f;
        if (size.HasValue)
            window.Size = size.Value;
        await ProcessFrames(1);
        return window;
    }

    private static PartyState BuildPartyState(IReadOnlyList<StringName> memberIds)
    {
        var partyState = new PartyState();
        foreach (StringName memberId in memberIds)
            partyState.SetMemberState(MakeMember(memberId, memberId.ToString()));
        partyState.leader_member_id = memberIds.Count > 0 ? memberIds[0] : new StringName("");
        partyState.active_member_ids = new StringNameList(memberIds);
        partyState.reserve_member_ids = new StringNameList();
        return partyState;
    }

    private static PartyMemberState MakeMember(StringName memberId, string displayName)
    {
        var member = new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
            current_hp = 30,
            current_mp = 8,
        };
        member.progression.unit_id = memberId;
        member.progression.display_name = displayName;
        member.progression.character_level = 1;
        return member;
    }

    private static SkillDefinition BuildWindowSkillDefinition(StringName skillId)
    {
        return new SkillDefinition(
            skillId,
            "DTO火花",
            "",
            "来自 SkillDefinition 快照",
            "combat",
            -1,
            0,
            "character_level",
            0,
            1,
            System.Array.Empty<int>(),
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "消耗 {ap_cost} AP，最大 {dynamic_max_level}；{custom_text}",
            new Dictionary<int, IReadOnlyDictionary<string, object>>
            {
                [1] = new Dictionary<string, object>
                {
                    ["custom_text"] = "快照文本",
                },
            },
            BuildWindowCombatDefinition(skillId)
        );
    }

    private static CombatSkillDefinition BuildWindowCombatDefinition(StringName skillId)
    {
        return new CombatSkillDefinition(
            skillId,
            "single",
            "enemy",
            "single",
            1,
            "single",
            0,
            true,
            3,
            0,
            0,
            0,
            0,
            0,
            0,
            "",
            0,
            "",
            0,
            new Dictionary<int, IReadOnlyDictionary<string, object>>
            {
                [2] = new Dictionary<string, object>
                {
                    ["ap_cost"] = 5,
                    ["cooldown_tu"] = 20,
                },
            },
            "",
            "",
            "",
            "",
            0,
            System.Array.Empty<int>(),
            0,
            "",
            "",
            0,
            "",
            "",
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            "",
            "",
            0,
            0,
            false,
            0,
            "",
            System.Array.Empty<CombatEffectDefinition>(),
            System.Array.Empty<CombatEffectDefinition>(),
            System.Array.Empty<CombatCastVariantDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            0,
            0
        );
    }

    private void AssertStringList(IReadOnlyList<string> actual, IReadOnlyList<string> expected, string message)
    {
        if (actual.Count != expected.Count)
        {
            _test.Fail($"{message} (actual={string.Join(",", actual)} expected={string.Join(",", expected)})");
            return;
        }
        for (int index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _test.Fail($"{message} (actual={string.Join(",", actual)} expected={string.Join(",", expected)})");
                return;
            }
        }
    }

    private void AssertVector2Near(Vector2 actual, Vector2 expected, float tolerance, string message)
    {
        if (Mathf.Abs(actual.X - expected.X) > tolerance || Mathf.Abs(actual.Y - expected.Y) > tolerance)
            _test.Fail($"{message} (actual={actual} expected={expected})");
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
