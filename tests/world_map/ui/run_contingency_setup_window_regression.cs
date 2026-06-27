using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_setup_window_regression : SceneTree
{
    private static readonly PackedScene PartyManagementWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/party_management_window.tscn"
    );
    private static readonly PackedScene ContingencySetupWindowScene = GD.Load<PackedScene>(
        "res://scenes/ui/contingency_setup_window.tscn"
    );

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        await TestPartyManagementExposesContingencyEntrySignal();
        await TestContingencyWindowRendersUnchargedSetup();
        await TestUnchargedTemplateSelectionEmitsSelectedPayload();
        await TestChargedSetupDisablesSaveAndShowsClearWarning();
        await TestActionButtonsEmitSignalsWithoutMutatingMember();
        Quit(_test.Finish("Contingency setup window regression"));
    }

    private async Task TestPartyManagementExposesContingencyEntrySignal()
    {
        PartyManagementWindow window = await CreatePartyWindow();
        PartyState partyState = BuildPartyState(MakeMember("hero", "Hero", UnchargedSetup()));
        var requested = new List<StringName>();
        window.contingency_setup_requested += memberId => requested.Add(memberId);

        window.ShowParty(partyState);
        await ProcessFrames(1);
        Button button = window.GetNodeOrNull<Button>("%ContingencySetupButton");
        _test.True(button != null, "PartyManagementWindow should expose ContingencySetupButton.");
        if (button != null)
        {
            _test.False(button.Disabled, "ContingencySetupButton should be enabled for the selected member.");
            button.EmitSignal(Button.SignalName.Pressed);
            await ProcessFrames(1);
        }

        _test.Eq(requested.Count, 1, "ContingencySetupButton should emit one request.");
        if (requested.Count > 0)
            _test.Eq(requested[0], new StringName("hero"), "contingency_setup_requested should carry selected member id.");

        window.HideWindow();
        await ProcessFrames(1);
        if (button != null)
            _test.True(button.Disabled, "ContingencySetupButton should be disabled when no member is selected.");
        await DisposeNode(window);
    }

    private async Task TestUnchargedTemplateSelectionEmitsSelectedPayload()
    {
        ContingencySetupWindow window = await CreateContingencyWindow();
        PartyMemberState member = MakeMember("hero", "Hero", UnchargedSetup());
        using CharacterManagementModule manager = BuildManager(member);
        var saveRequests = new List<(StringName MemberId, StringName PayloadName)>();
        window.save_requested += (memberId, payloadName) => saveRequests.Add((memberId, payloadName));

        window.ShowForMember(member, manager);
        await ProcessFrames(1);
        int ownerTurnIndex = FindOptionIndex(window.trigger_selector, "owner_turn_started");
        _test.True(ownerTurnIndex >= 0, "trigger selector should expose owner_turn_started template.");
        if (ownerTurnIndex >= 0)
        {
            window.trigger_selector.Select(ownerTurnIndex);
            window.trigger_selector.EmitSignal(OptionButton.SignalName.ItemSelected, ownerTurnIndex);
            await ProcessFrames(1);
        }
        window.save_button.EmitSignal(Button.SignalName.Pressed);
        await ProcessFrames(1);

        _test.Eq(saveRequests.Count, 1, "uncharged save action should emit one save request.");
        if (saveRequests.Count > 0)
        {
            _test.Eq(saveRequests[0].MemberId, new StringName("hero"), "save request should include member id.");
            _test.Eq(
                saveRequests[0].PayloadName,
                new StringName("owner_turn_mirror_self"),
                "save request should use the selected template payload."
            );
        }
        _test.True(
            member.TryGetContingencySetupTyped("hp_mirror_self", out ContingencyMatrixSetupState setup),
            "UI save signal should not directly mutate PartyMemberState."
        );
        _test.True(setup != null && !setup.Charged, "UI save signal should leave original setup uncharged.");

        await DisposeNode(window);
    }

    private async Task TestContingencyWindowRendersUnchargedSetup()
    {
        ContingencySetupWindow window = await CreateContingencyWindow();
        PartyMemberState member = MakeMember("hero", "Hero", UnchargedSetup());
        using CharacterManagementModule manager = BuildManager(member);

        window.ShowForMember(member, manager);
        await ProcessFrames(1);

        _test.True(window.Visible, "ShowForMember should show the contingency setup window.");
        _test.True(window.member_status_label.Text.Contains("Hero"), "member status should identify the selected member.");
        _test.True(window.setup_status_label.Text.Contains("hp_mirror_self"), "setup status should show setup id.");
        _test.True(window.trigger_selector.GetItemText(window.trigger_selector.Selected).Contains("hp_below_percent"), "trigger selector should show hp_below_percent.");
        _test.True(window.release_mode_selector.GetItemText(window.release_mode_selector.Selected).Contains("burst_release"), "release selector should show burst_release.");
        _test.True(window.stored_spell_list.GetItemText(0).Contains("mage_mirror_image"), "stored spell list should show mirror image.");
        _test.True(window.target_resolver_selector.GetItemText(window.target_resolver_selector.Selected).Contains("self"), "target resolver should show self.");
        _test.True(window.matrix_preview_label.Text.Contains("matrix_load=3"), "matrix preview should show matrix load.");
        _test.True(window.matrix_preview_label.Text.Contains("reserved_mp_max=0"), "matrix preview should show uncharged MP reservation.");
        _test.True(window.material_preview_label.Text.Contains("special_contingency_gem:0"), "material preview should show zero material receipt.");
        _test.False(window.save_button.Disabled, "uncharged setup should allow save.");
        _test.False(window.charge_button.Disabled, "uncharged setup should allow charge.");

        await DisposeNode(window);
    }

    private async Task TestChargedSetupDisablesSaveAndShowsClearWarning()
    {
        ContingencySetupWindow window = await CreateContingencyWindow();
        PartyMemberState member = MakeMember("hero", "Hero", ChargedSetup());
        using CharacterManagementModule manager = BuildManager(member);

        window.ShowForMember(member, manager);
        await ProcessFrames(1);

        _test.True(window.save_button.Disabled, "charged setup should disable direct save/edit.");
        _test.True(window.charge_button.Disabled, "charged setup should disable direct charge.");
        _test.False(window.clear_charge_button.Disabled, "charged setup should allow clear charge.");
        string warning = window.clear_charge_confirmation_label.Text;
        _test.True(warning.Contains("材料不返还"), "clear confirmation should warn material is not refunded.");
        _test.True(warning.Contains("当前 MP 不恢复"), "clear confirmation should warn current MP is not restored.");

        await DisposeNode(window);
    }

    private async Task TestActionButtonsEmitSignalsWithoutMutatingMember()
    {
        ContingencySetupWindow window = await CreateContingencyWindow();
        PartyMemberState member = MakeMember("hero", "Hero", ChargedSetup());
        using CharacterManagementModule manager = BuildManager(member);
        var saveRequests = new List<(StringName MemberId, StringName PayloadName)>();
        var chargeRequests = new List<(StringName MemberId, StringName SetupId)>();
        var clearRequests = new List<(StringName MemberId, StringName SetupId)>();
        window.save_requested += (memberId, payloadName) => saveRequests.Add((memberId, payloadName));
        window.charge_requested += (memberId, setupId) => chargeRequests.Add((memberId, setupId));
        window.clear_charge_requested += (memberId, setupId) => clearRequests.Add((memberId, setupId));

        window.ShowForMember(member, manager);
        await ProcessFrames(1);
        window.clear_charge_button.EmitSignal(Button.SignalName.Pressed);
        await ProcessFrames(1);

        _test.Eq(saveRequests.Count, 0, "charged clear action should not emit save.");
        _test.Eq(chargeRequests.Count, 0, "charged clear action should not emit charge.");
        _test.Eq(clearRequests.Count, 1, "clear button should emit clear request.");
        if (clearRequests.Count > 0)
        {
            _test.Eq(clearRequests[0].MemberId, new StringName("hero"), "clear request should include member id.");
            _test.Eq(clearRequests[0].SetupId, new StringName("hp_mirror_self"), "clear request should include setup id.");
        }
        _test.True(member.TryGetContingencySetupTyped("hp_mirror_self", out ContingencyMatrixSetupState setup), "test member should still have setup.");
        _test.True(setup != null && setup.Charged, "UI clear signal should not directly mutate PartyMemberState.");

        await DisposeNode(window);
    }

    private async Task<PartyManagementWindow> CreatePartyWindow()
    {
        var window = PartyManagementWindowScene.Instantiate<PartyManagementWindow>();
        Root.AddChild(window);
        await ProcessFrames(1);
        return window;
    }

    private async Task<ContingencySetupWindow> CreateContingencyWindow()
    {
        _test.True(ContingencySetupWindowScene != null, "Contingency setup scene should exist.");
        var window = ContingencySetupWindowScene.Instantiate<ContingencySetupWindow>();
        Root.AddChild(window);
        await ProcessFrames(1);
        return window;
    }

    private static PartyState BuildPartyState(PartyMemberState member)
    {
        PartyState partyState = new()
        {
            leader_member_id = member.member_id,
            active_member_ids = new GStringNameArray { member.member_id },
            reserve_member_ids = new GStringNameArray(),
        };
        partyState.SetMemberState(member);
        return partyState;
    }

    private static PartyMemberState MakeMember(StringName memberId, string displayName, ContingencyMatrixSetupState setup)
    {
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = displayName,
            current_hp = 20,
            current_mp = 30,
        };
        member.progression.unit_id = memberId;
        member.progression.display_name = displayName;
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 30);
        member.progression.SetSkillProgress(LearnedSkill("mage_chain_contingency", 5));
        member.progression.SetSkillProgress(LearnedSkill("mage_mirror_image", 2));
        return member.WithContingencySetupsForMutation(new[] { setup });
    }

    private static CharacterManagementModule BuildManager(PartyMemberState member)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            BuildPartyState(member),
            BuildSkillIndex(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            BuildItemIndex(),
            new Dictionary<StringName, QuestDef>(),
            new Dictionary<StringName, TraitDef>(),
            null,
            new ProgressionIdentityCatalogData()
        );
        return manager;
    }

    private static ContingencyMatrixSetupState UnchargedSetup() =>
        ContingencyMatrixSetupState.FromDictionary(BuildSetupPayload(charged: false));

    private static ContingencyMatrixSetupState ChargedSetup()
    {
        GDictionary payload = BuildSetupPayload(charged: true);
        payload["reserved_mp_max"] = 6;
        payload["material_costs"] = new GArray
        {
            new GDictionary
            {
                ["item_id"] = "special_contingency_gem",
                ["quantity"] = 1,
            },
        };
        return ContingencyMatrixSetupState.FromDictionary(payload);
    }

    private static GDictionary BuildSetupPayload(bool charged) =>
        new()
        {
            ["setup_id"] = "hp_mirror_self",
            ["display_name"] = "濒死镜影",
            ["enabled"] = true,
            ["charged"] = charged,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = 5,
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = charged ? 6 : 0,
            ["material_costs"] = new GArray(),
            ["trigger"] = new GDictionary
            {
                ["type"] = "hp_below_percent",
                ["subject"] = "owner",
                ["percent"] = 30,
                ["crossing_only"] = true,
                ["timing"] = "after_hp_changed",
            },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray
            {
                new GDictionary
                {
                    ["stored_skill_id"] = "mage_mirror_image",
                    ["cast_level"] = 2,
                    ["order"] = 1,
                    ["target_resolver"] = new GDictionary { ["type"] = "self" },
                    ["parameter_bindings"] = new GDictionary(),
                    ["fallback_policy"] = "skip_if_invalid",
                },
            },
        };

    private static UnitSkillProgress LearnedSkill(string skillId, int level) =>
        new()
        {
            skill_id = skillId,
            is_learned = true,
            skill_level = level,
            current_mastery = 0,
            total_mastery_earned = 0,
            is_core = false,
            granted_source_type = "test",
        };

    private static Dictionary<StringName, SkillDefinition> BuildSkillIndex() =>
        new()
        {
            ["mage_chain_contingency"] = BuildSkill("mage_chain_contingency", tags: new[] { "contingency", "meta_spell" }),
            ["mage_mirror_image"] = BuildSkill(
                "mage_mirror_image",
                automation: TestSkillDefinitionProjection.BuildContingencyAutomation(
                    effectCategory: "defensive_self_buff",
                    allowedTargetResolvers: new[] { new StringName("self") }
                )
            ),
        };

    private static SkillDefinition BuildSkill(
        string skillId,
        ContingencyAutomationDefinition automation = null,
        string[] tags = null
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId,
            skillType: "passive",
            maxLevel: 10,
            tags: ToStringNames(tags),
            masteryCurve: new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            contingencyAutomationProfile: automation
        );
    }

    private static IReadOnlyList<StringName> ToStringNames(string[] values)
    {
        if (values == null || values.Length == 0)
            return System.Array.Empty<StringName>();
        List<StringName> result = new(values.Length);
        foreach (string value in values)
            result.Add(value);
        return result;
    }

    private static Dictionary<StringName, ItemDef> BuildItemIndex() =>
        new()
        {
            ["special_contingency_gem"] = new ItemDef
            {
                item_id = "special_contingency_gem",
                display_name = "Special Contingency Gem",
                CategoryKind = ItemCategoryKind.Misc,
                is_stackable = true,
                max_stack = 99,
            },
        };

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

    private static int FindOptionIndex(OptionButton selector, string text)
    {
        if (selector == null)
            return -1;
        for (int index = 0; index < selector.ItemCount; index++)
            if (selector.GetItemText(index).Contains(text))
                return index;
        return -1;
    }
}
