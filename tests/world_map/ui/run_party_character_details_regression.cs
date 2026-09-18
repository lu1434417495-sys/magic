using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_party_character_details_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly string[] Tabs = { "概览", "属性", "种族", "职业", "技能", "装备", "特性", "成就" };
    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            await CheckMemberDetails();
            if (DisplayServer.GetName() != "headless" && OS.GetEnvironment("MAGIC_PARTY_DETAILS_CAPTURE_DIR").Length > 0)
                await CaptureWorldWindow();
        }
        catch (Exception error) { _test.Fail(error.ToString()); }
        finally { RequestTestExit(_test.Finish("Party character details regression")); }
    }

    private async Task CheckMemberDetails()
    {
        PartyManagementWindow window = null;
        var manager = new CharacterManagementModule();
        try
        {
            ContentSnapshot content = GameSessionTestFactory.GetProcessSnapshot();
            var traits = new Dictionary<StringName, TraitDefinition>(content.Traits);
            traits["fixture_training"] = TraitTestData.Definition("fixture_training", new[] { "character" });
            traits["fixture_roll"] = TraitTestData.Definition("fixture_roll", new[] { "equipment_roll" },
                stackPolicy: "stack_by_instance",
                rollValueSchema: new[] { new TraitRollValueSchemaEntryImportModel("strength", "int", 1, 9, Array.Empty<string>()) });
            var hero = new PartyMemberState
            {
                member_id = "hero", display_name = "林间旅者", race_id = "elf", subrace_id = "high_elf",
                age_profile_id = "elf_age_profile", age_years = 150, biological_age_years = 150,
                natural_age_stage_id = "adult", effective_age_stage_id = "adult", current_hp = 30,
            };
            var other = new PartyMemberState { member_id = "other", display_name = "另一名成员" };
            StringName sourceSkill = "warrior_guard";
            hero.trait_instances.Add(TraitInstanceState.Create("training_1", "fixture_training",
                TraitSourceKind.Character, sourceSkill));
            StringName itemId = content.GearSets["dragon_scale_set"].MemberItemIds[0];
            ItemDefinition item = content.Items[itemId];
            StringName slot = item.GetEquipmentSlotIdsTyped()[0];
            var equipment = EquipmentInstanceState.CreateInstance(itemId, "fixture_equipment");
            equipment.rarity = (int)EquipmentInstanceState.RarityTier.RARE;
            equipment.current_durability = 7;
            equipment.trait_instances.Add(TraitInstanceState.Create("roll_1", "fixture_roll",
                TraitSourceKind.EquipmentRoll, equipment.instance_id,
                rank: 2, stacks: 3, rollValues: new[] { TraitRollValueState.CreateInt("strength", 4) }));
            _test.True(hero.equipment_state.SetEquippedEntry(slot, itemId,
                item.GetFinalOccupiedSlotIdsTyped(slot), equipment), "装备实例可装入测试成员。");
            var party = new PartyState { leader_member_id = "hero", main_character_member_id = "hero",
                active_member_ids = new StringNameList(new StringName[] { "hero", "other" }) };
            party.SetMemberState(hero);
            party.SetMemberState(other);
            manager.setup(party, content.Skills, content.Professions, content.Achievements,
                content.Items, content.Quests, traits, null, content.IdentityCatalog, content.GearSets);
            hero = party.GetMemberState("hero");
            var learnedIds = content.Skills.Keys.Take(14).ToArray();
            foreach (StringName id in learnedIds)
                hero.progression.SetSkillProgress(new UnitSkillProgress { skill_id = id, is_learned = true, skill_level = 1 });
            PromotionHistoryTestFixture.Record(hero.progression, sourceSkill, "warrior");
            Root.ContentScaleSize = new Vector2I(1280, 720);
            Root.Size = new Vector2I(1280, 720);
            window = GD.Load<PackedScene>("res://scenes/ui/party_management_window.tscn").Instantiate<PartyManagementWindow>();
            Root.AddChild(window);
            window.SetItemDefs(content.Items);
            window.SetAchievementDefs(content.Achievements);
            window.SetSkillDefinitions(content.Skills);
            window.SetProfessionDefs(content.Professions);
            window.SetTraitDefs(traits);
            window.SetIdentityCatalog(content.IdentityCatalog);
            window.ShowParty(PartyManagementViewBuilder.Capture(party, manager));
            await Frames();
            var tabs = window.GetNode<TabContainer>("%DetailsTabs");
            _test.Eq(tabs.GetTabCount(), Tabs.Length, "人物档案包含八个独立页签。");
            for (int i = 0; i < Tabs.Length; i++)
                _test.Eq(tabs.GetTabTitle(i), Tabs[i], "页签顺序符合档案分类。");
            _test.True(window.race_label.Text.Contains(content.IdentityCatalog.RaceDefs["elf"].Description), "种族页展示正式种族说明。");
            _test.True(window.race_label.Text.Contains("豁免优势：魅惑"), "种族页展示已配置的豁免能力。");
            _test.True(window.race_label.Text.Contains("150"), "种族页展示成员年龄。");
            _test.True(window.traits_label.Text.Contains("种族 · 精灵"), "生效特性有种族来源。");
            _test.True(window.traits_label.Text.Contains($"技能 · {content.Skills[sourceSkill].DisplayName}"), "生效特性有技能来源。");
            _test.True(window.traits_label.Text.Contains($"装备词条 · {item.DisplayName}"), "随机词条保留装备来源。");
            _test.True(window.traits_label.Text.Contains("等级 2") && window.traits_label.Text.Contains("层数 3"), "特性保留等级和层数。");
            _test.True(window.equipment_label.Text.Contains("品质：稀有") && window.equipment_label.Text.Contains("耐久：7 /"), "装备页读取真实品质和当前耐久。");
            _test.True(window.equipment_label.Text.Contains("fixture_roll") && window.equipment_label.Text.Contains("力量：4"), "装备页展示随机词条及掷值。");
            _test.True(window.professions_label.Text.Contains("晋升至 1 阶"), "职业页展示成长历史。");
            foreach (StringName id in learnedIds)
                _test.True(window.skills_label.Text.Contains(content.Skills[id].DisplayName), "技能页不截断已学技能。");
            _test.True(window.attributes_label.Text.Contains("调整值"), "属性页展示实际六维调整值。");

            tabs.CurrentTab = 6;
            await Frames();
            var scroll = (ScrollContainer)tabs.GetTabControl(6);
            scroll.ScrollVertical = 80;
            _test.True(window.SelectMember("other"), "可以切换队员。");
            await Frames();
            _test.Eq(tabs.CurrentTab, 6, "换人保留当前分类。");
            _test.Eq(scroll.ScrollVertical, 0, "换人重置正文位置。");
            _test.False(window.traits_label.Text.Contains("fixture_roll"), "换人清理旧的随机词条。");
            window.SelectMember("hero");
            hero.trait_instances.Clear();
            hero.equipment_state = new EquipmentState();
            window.SetPartyView(PartyManagementViewBuilder.Capture(party, manager));
            window.SelectMember("hero");
            _test.False(window.traits_label.Text.Contains("fixture_roll"), "卸装后特性刷新。");
            _test.False(window.traits_label.Text.Contains("fixture_training"), "移除角色特性后刷新。");
            _test.False(window.equipment_label.Text.Contains("耐久：7 /"), "卸装后装备详情刷新。");
            await CheckAchievements(window, party, content, tabs, manager);
            foreach (Vector2I logicalSize in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                Root.ContentScaleSize = logicalSize;
                Root.Size = logicalSize;
                await Frames();
                for (int i = 0; i < tabs.GetTabCount(); i++)
                {
                    tabs.CurrentTab = i;
                    await Frames();
                    _test.True(new Rect2(Vector2.Zero, window.Size).Encloses(window.panel.GetGlobalRect()), "长内容不撑出视口。");
                    _test.True(window.panel.GetGlobalRect().Encloses(window.close_button.GetGlobalRect()), "关闭按钮始终可见。");
                }
            }
        }
        finally { window?.Free(); manager.Dispose(); }
    }

    private async Task CheckAchievements(PartyManagementWindow window, PartyState party,
        ContentSnapshot content, TabContainer tabs, CharacterManagementModule manager)
    {
        UnitProgress progression = party.GetMemberState("hero").progression;
        SeedOwnedAchievements(progression, content);
        int stateCount = progression.AchievementProgressTyped.Count;
        window.SetPartyView(PartyManagementViewBuilder.Capture(party, manager));
            window.SelectMember("hero");
        tabs.CurrentTab = 7;
        await Frames();
        string text = window.achievements_label.Text;
        _test.True(text.Contains("已获得 5 项成就"), "成就页只统计已获得的成就。");
        _test.True(text.Contains("取得时间："), "已解锁成就展示取得时间。");
        _test.False(text.Contains("进行中") || text.Contains("未开始") || text.Contains("进度："), "成就页不展示未激活分类、进度或总完成度。");
        _test.True(text.Contains("生命上限 +8") && text.Contains("解锁技能：冲锋")
            && text.Contains("重击 熟练度 +10") && text.Contains("解锁知识：旅途见闻"), "成就奖励保留种类、目标和数值。");
        foreach (AchievementDefinition achievement in content.Achievements.Values)
        {
            bool owned = progression.GetAchievementProgressState(achievement.AchievementId)?.is_unlocked == true;
            _test.Eq(text.Contains(achievement.DisplayName), owned, "只显示已获得成就的名称，包含全部已获得条目。");
            _test.Eq(text.Contains(achievement.Description), owned, "未激活成就的条件说明不展示。");
        }
        _test.Eq(progression.AchievementProgressTyped.Count, stateCount, "浏览成就不会为未开始条目创建进度。");
        _test.False(progression.GetAchievementProgressState("skill_mastery_charge_stride").is_unlocked, "达到阈值仍以解锁事实为准，展示不会授予成就。");
        var scroll = (ScrollContainer)tabs.GetTabControl(7);
        scroll.ScrollVertical = 80;
        await Frames();
        _test.True(scroll.ScrollVertical > 0, "成就长列表可滚动。");
        window.SelectMember("other");
        await Frames();
        _test.Eq(tabs.CurrentTab, 7, "换人保留成就页。");
        _test.Eq(scroll.ScrollVertical, 0, "换人重置成就滚动。");
        _test.Eq(window.achievements_label.Text, "尚未获得成就。", "无成就成员只展示空态。");
        _test.False(window.achievements_label.Text.Contains("取得时间："), "换人清理上一成员解锁信息。");
        window.SelectMember("hero");
        progression.GetAchievementProgressState("enemy_defeated_apprentice").is_unlocked = false;
        window.SetPartyView(PartyManagementViewBuilder.Capture(party, manager));
            window.SelectMember("hero");
        _test.False(window.achievements_label.Text.Contains(content.Achievements["enemy_defeated_apprentice"].DisplayName), "失去解锁状态后刷新移除条目。");
        window.SetAchievementDefs(null);
        _test.True(window.achievements_label.Text.Contains("暂无成就数据"), "缺少内容时显示空态。");
        window.SetAchievementDefs(content.Achievements);
        window.HideWindow();
        _test.Eq(window.achievements_label.Text, "", "关闭清理成就详情。");
        window.ShowParty(PartyManagementViewBuilder.Capture(party));
        _test.Eq(tabs.CurrentTab, 0, "重新打开返回概览。");
    }

    private static void SeedOwnedAchievements(UnitProgress progression, ContentSnapshot content)
    {
        StringName[] ownedIds = { "battle_won_first", "settlement_wayfarer", "enemy_defeated_apprentice",
            "warrior_heavy_strike_practice", "profession_promoted_first" };
        for (int i = 0; i < ownedIds.Length; i++)
            progression.SetAchievementProgressState(new AchievementProgressState
            {
                achievement_id = ownedIds[i], current_value = content.Achievements[ownedIds[i]].Threshold,
                is_unlocked = true, unlocked_at_unix_time = 1700000000 + i * 3600,
            });
        progression.SetAchievementProgressState(new AchievementProgressState
        {
            achievement_id = "skill_mastery_charge_stride",
            current_value = content.Achievements["skill_mastery_charge_stride"].Threshold,
        });
    }

    private async Task CaptureWorldWindow()
    {
        string isolationRoot = OS.GetEnvironment("MAGIC_E2E_USER_DATA_ROOT");
        if (OS.GetEnvironment("MAGIC_E2E_ISOLATED_USER_DATA") != "1" || string.IsNullOrEmpty(isolationRoot)
            || !System.IO.Path.GetFullPath(OS.GetUserDataDir()).StartsWith(
                System.IO.Path.GetFullPath(isolationRoot) + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Native world capture requires isolated test user data.");
        WorldMapSystem world = null;
        var wait = new E2eWait(this);
        var input = new E2eInputDriver(this, wait);
        try
        {
            var session = Root.GetNode<GameSession>("GameSession");
            _test.Eq(session.CreateNewSave("test"), (int)Error.Ok, "创建隔离世界场景。");
            world = GD.Load<PackedScene>("res://scenes/main/world_map.tscn").Instantiate<WorldMapSystem>();
            Root.AddChild(world);
            await wait.UntilAsync(() => world._runtime != null && world.party_button.Size.X > 0, 180, "世界地图就绪");
            await input.ClickAsync(world.party_button);
            await wait.UntilAsync(() => world.party_management_window.Visible, 60, "大地图打开人物管理");
            var window = world.party_management_window;
            var tabs = window.GetNode<TabContainer>("%DetailsTabs");
            string directory = OS.GetEnvironment("MAGIC_PARTY_DETAILS_CAPTURE_DIR");
            System.IO.Directory.CreateDirectory(directory);
            foreach (int height in new[] { 720, 2160 })
            {
                Root.Size = height == 720 ? new Vector2I(1280, 720) : new Vector2I(3840, 2160);
                Root.ContentScaleSize = height == 720 ? new Vector2I(1280, 720) : new Vector2I(1920, 1080);
                await Frames(10);
                for (int i = 0; i < Tabs.Length; i++)
                {
                    TabBar bar = tabs.GetTabBar();
                    await input.ClickAtAsync(bar, bar.GetTabRect(i).GetCenter());
                    await Frames();
                    _test.Eq(tabs.CurrentTab, i, "真实指针可以切换全部页签。");
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using Image screenshot = world.GetViewport().GetTexture().GetImage();
                    _test.Eq(screenshot.SavePng(System.IO.Path.Combine(directory, $"party-{i}-{height}.png")), Error.Ok, "保存原生窗口截图。");
                    _test.Eq(screenshot.GetHeight(), height, "截图保留原生物理分辨率。");
                    _test.True(bar.GetTabRect(Tabs.Length - 1).End.X <= bar.Size.X,
                        "八个页签全部位于可点击范围内。");
                    if (i == 2 || i == 4 || i == 7)
                    {
                        var scroll = (ScrollContainer)tabs.GetTabControl(i);
                        if (scroll.GetVScrollBar().MaxValue > scroll.GetVScrollBar().Page)
                        {
                            for (int step = 0; step < 12; step++)
                                await input.ClickAtAsync(scroll, new Vector2(40, 40), MouseButton.WheelDown);
                            _test.True(scroll.ScrollVertical > 0, "长内容可用真实滚轮阅读。");
                            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                            using Image bottom = world.GetViewport().GetTexture().GetImage();
                            _test.Eq(bottom.SavePng(System.IO.Path.Combine(directory, $"party-{i}-{height}-scrolled.png")), Error.Ok, "保存滚动后的内容。");
                            scroll.ScrollVertical = 0;
                        }
                    }
                }
            }
            // Only this isolated capture fixture receives achievements; no player save is involved.
            SeedOwnedAchievements(world._runtime.GetPartyState().GetMemberState(window.GetSelectedMemberId()).progression,
                GameSessionTestFactory.GetProcessSnapshot());
            window.SetPartyView(PartyManagementViewBuilder.Capture(world._runtime.GetPartyState(), world._runtime._character_management));
            window.SelectMember("hero");
            await input.ClickAtAsync(tabs.GetTabBar(), tabs.GetTabBar().GetTabRect(7).GetCenter());
            await Frames();
            _test.True(window.achievements_label.Text.Contains("已获得 5 项成就"), "4K 原生窗口显示已获得成就。");
            _test.False(window.achievements_label.Text.Contains("冲锋起步"), "4K 原生窗口隐藏尚未解锁的成就。");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using (Image owned = world.GetViewport().GetTexture().GetImage())
            {
                _test.Eq(owned.GetSize(), new Vector2I(3840, 2160), "成就详情截图为原生 4K。");
                _test.Eq(owned.SavePng(System.IO.Path.Combine(directory, "party-achievements-owned-2160.png")), Error.Ok, "保存已获得成就截图。");
            }
            var achievementScroll = (ScrollContainer)tabs.GetTabControl(7);
            for (int step = 0; step < 12; step++)
                await input.ClickAtAsync(achievementScroll, new Vector2(40, 40), MouseButton.WheelDown);
            _test.True(achievementScroll.ScrollVertical > 0, "4K 已获得成就列表可用滚轮阅读。");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using (Image bottom = world.GetViewport().GetTexture().GetImage())
                _test.Eq(bottom.SavePng(System.IO.Path.Combine(directory, "party-achievements-owned-2160-scrolled.png")), Error.Ok, "保存已获得成就滚动截图。");
            await input.ClickAsync(window.close_button);
            _test.False(window.Visible, "关闭按钮返回大地图。");
        }
        finally
        {
            await input.ReleaseAllAsync();
            if (world != null) { world.QueueFree(); await Frames(); }
        }
    }

    private async Task Frames(int count = 6)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}
