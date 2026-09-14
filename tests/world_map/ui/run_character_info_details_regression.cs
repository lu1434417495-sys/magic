using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_character_info_details_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        CharacterInfoWindow window = null;
        var builder = new GameRuntimeCharacterInfoBuilder();
        try
        {
            var catalog = Root.GetNode<GameSession>("GameSession").GetContentCatalogTyped();
            var query = new Query(catalog);
            builder.Setup(query);
            BattleUnitState unit = BuildUnit(query);
            IReadOnlyList<GameRuntimeCharacterInfoSection> sections =
                builder.BuildBattleCharacterInfoSections(unit, "战斗单位", "玩家");
            CheckAttributes(sections);
            CheckTraits(unit, sections, builder, query);

            Root.ContentScaleSize = new Vector2I(1280, 720);
            Root.Size = new Vector2I(1280, 720);
            window = GD.Load<PackedScene>("res://scenes/ui/character_info_window.tscn")
                .Instantiate<CharacterInfoWindow>();
            Root.AddChild(window);
            window.ShowCharacter(new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle, "人物信息验证", "战斗单位 · 玩家", "可行动", sections));
            await Frames();
            var panel = window.GetNode<PanelContainer>("CenterContainer/Panel");
            _test.True(new Rect2(Vector2.Zero, window.Size).Encloses(panel.GetGlobalRect()),
                "完整属性和特性内容应保持在 720p 视口内。");
            _test.True(window.sections_scroll.GetVScrollBar().MaxValue > window.sections_scroll.Size.Y,
                "长内容应通过正文滚动，不撑大窗口。");
            _test.True(window.close_button.GetGlobalRect().End.Y < 720,
                "长内容窗口的关闭按钮应始终可见。");
            _test.True(AllLabels(window).Any(label => label.Text == "生效特性"),
                "场景应渲染独立生效特性标题。");
            _test.True(AllLabels(window).Any(label => label.Text.Contains(query.RaceTrait.Description)),
                "特性效果正文应直接可读。");
            window.GetNode<Button>("%TraitsButton").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            _test.True(window.sections_scroll.ScrollVertical > 0, "特性按钮应跳到生效特性区。");
            window.GetNode<Button>("%AttributesButton").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Label attributesTitle = AllLabels(window).First(label => label.Text == "基础属性");
            _test.True(window.sections_scroll.GetGlobalRect().Intersects(attributesTitle.GetGlobalRect()),
                "属性按钮应将基础属性标题带入正文可视区。");

            string captureDirectory = OS.GetEnvironment("MAGIC_CHARACTER_INFO_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(captureDirectory) && DisplayServer.GetName() != "headless")
            {
                System.IO.Directory.CreateDirectory(captureDirectory);
                await Capture(window, captureDirectory, "attributes.png");
                window.GetNode<Button>("%TraitsButton").EmitSignal(Button.SignalName.Pressed);
                await Frames();
                await Capture(window, captureDirectory, "traits.png");
            }

            unit.attribute_snapshot.SetValue("strength", 22);
            unit.ReplaceEffectiveTraitsTyped(Array.Empty<BattleEffectiveTraitInstanceState>());
            var refreshed = builder.BuildBattleCharacterInfoSections(unit, "战斗单位", "玩家");
            window.ShowCharacter(new GameRuntimeCharacterInfoContext(
                GameRuntimeCharacterInfoSource.Battle, "另一名人物", "", "", refreshed));
            await Frames();
            _test.True(AllLabels(window).Any(label => label.Text == "22（调整值 +6）"),
                "重新打开应使用最新属性快照。");
            _test.False(AllLabels(window).Any(label => label.Text.Contains(query.RaceTrait.Description)),
                "重新打开必须清理上一份特性内容。");
            _test.True(AllLabels(window).Any(label => label.Text == "当前没有生效特性。"),
                "空特性应明确显示空态。");
            _test.Eq(window.sections_scroll.ScrollVertical, 0, "切换人物后正文滚动应归零。");
            GC.KeepAlive(query);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            window?.Free();
            builder.Dispose();
            RequestTestExit(_test.Finish("Character info details regression"));
        }
    }

    private void CheckAttributes(IReadOnlyList<GameRuntimeCharacterInfoSection> sections)
    {
        var attributes = sections.Single(section => section.Title == "基础属性").Entries;
        _test.Eq(attributes.Count, 6, "人物信息应包含完整六维。");
        _test.Eq(attributes.Single(entry => entry.Label == "力量").Value, "18（调整值 +7）",
            "应读取最终调整值，不在 UI 里重算或抹掉 modifier overlay。");
        _test.Eq(attributes.Single(entry => entry.Label == "意志").Value, "8（调整值 -1）",
            "负调整值必须保留。");
        var combat = sections.Single(section => section.Title == "战斗属性").Entries;
        _test.Eq(combat.Single(entry => entry.Label == "AC").Value, "19", "应显示实际护甲等级。");
        _test.Eq(combat.Single(entry => entry.Label == "基础攻击加值").Value, "4", "应显示攻击属性。");
        _test.Eq(combat.Single(entry => entry.Label == "护甲敏捷上限").Value, "不限", "负一上限应按语义展示。");
    }

    private void CheckTraits(BattleUnitState unit, IReadOnlyList<GameRuntimeCharacterInfoSection> sections,
        GameRuntimeCharacterInfoBuilder builder, Query query)
    {
        var traits = sections.Single(section => section.Title == "生效特性").Entries;
        _test.Eq(traits.Count, 5, "应显示所有有效实例，同名多来源不被 UI 二次去重。");
        _test.True(traits.Any(entry => entry.Value.Contains($"种族 · {query.Race.DisplayName}")),
            "应显示真正种族来源名称。");
        _test.True(traits.Any(entry => entry.Value.Contains($"技能 · {query.Skill.DisplayName}")),
            "角色 trait 的技能来源应解析为技能名称。");
        _test.True(traits.Any(entry => entry.Value.Contains($"装备 · {query.Item.DisplayName}")),
            "装备来源应读取当前战斗装备实例。");
        _test.True(traits.Any(entry => entry.Value.Contains("装备词条") && entry.Value.Contains("层数 3")
            && entry.Value.Contains("等级 2")), "随机词条应保留实例等级和层数。");
        _test.True(traits.Any(entry => entry.Value.Contains("套装")), "套装 trait 应出现在有效列表。");
        unit.ReplaceEffectiveTraitsTyped(new[] { Instance(query.RaceTrait, "identity", query.Race.RaceId, "remaining") });
        var remaining = builder.BuildBattleCharacterTraitEntries(unit);
        _test.Eq(remaining.Count, 1, "已移除特性不得从装备内容定义或世界角色重新补回。");
    }

    private static BattleUnitState BuildUnit(Query query)
    {
        var unit = new BattleUnitState { display_name = "人物信息验证", source_member_id = "" };
        int[] scores = { 18, 14, 16, 12, 10, 8 };
        int index = 0;
        foreach (StringName id in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            unit.attribute_snapshot.SetValue(id, scores[index++]);
        unit.attribute_snapshot.SetValue("strength_modifier", 7);
        foreach (StringName id in AttributeService.RESOURCE_ATTRIBUTE_IDS)
            unit.attribute_snapshot.SetValue(id, 20);
        foreach (StringName id in AttributeService.COMBAT_ATTRIBUTE_IDS)
            unit.attribute_snapshot.SetValue(id, 0);
        unit.attribute_snapshot.SetValue("armor_class", 19);
        unit.attribute_snapshot.SetValue("armor_max_dex_bonus", -1);
        unit.attribute_snapshot.SetValue("base_attack_bonus", 4);
        unit.attribute_snapshot.SetValue("spell_proficiency_bonus", 3);
        unit.attribute_snapshot.SetValue("weapon_attack_range", 2);
        unit.equipment_view.SetEquippedEntry("main_hand", query.Item.ItemId,
            new[] { (StringName)"main_hand" },
            new EquipmentInstanceState { instance_id = "equipped_blade", item_id = query.Item.ItemId });
        unit.ReplaceEffectiveTraitsTyped(new[]
        {
            Instance(query.RaceTrait, "identity", query.Race.RaceId, "race"),
            Instance(query.CharacterTrait, "character", query.Skill.SkillId, "skill"),
            Instance(query.CharacterTrait, "equipment_fixed", "equipped_blade", "fixed"),
            Instance(query.CharacterTrait, "equipment_roll", "equipped_blade", "rolled", 2, 3),
            Instance(query.CharacterTrait, "gear_set_threshold", "equipped_blade", "set"),
        });
        return unit;
    }

    private static BattleEffectiveTraitInstanceState Instance(TraitDefinition trait, string source,
        StringName sourceId, string key, int rank = 1, int stacks = 1) => new()
    {
        trait_id = trait.TraitId, effective_instance_key = key, source_type = source, source_id = sourceId,
        effect_type = trait.EffectType, trigger_type = trait.TriggerType,
        charge_scope = trait.ChargeScope, charge_reset_timing = trait.ChargeResetTiming,
        rank = rank, stacks = stacks,
    };

    private async Task Frames()
    {
        for (int i = 0; i < 8; i++)
            await ToSignal(this, SignalName.ProcessFrame);
    }

    private async Task Capture(CharacterInfoWindow window, string directory, string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = window.GetViewport().GetTexture().GetImage();
        _test.Eq(image.SavePng(System.IO.Path.Combine(directory, name)), Error.Ok, "应保存界面截图。");
    }

    private static IEnumerable<Label> AllLabels(Node node) =>
        node.FindChildren("*", "Label", true, false).OfType<Label>();

    private sealed class Query : IGameRuntimeCharacterInfoQuery
    {
        private readonly GameContentCatalog _catalog;
        internal RaceDefinition Race { get; }
        internal TraitDefinition RaceTrait { get; }
        internal TraitDefinition CharacterTrait { get; }
        internal ItemDefinition Item { get; }
        internal SkillDefinition Skill { get; }

        internal Query(GameContentCatalog catalog)
        {
            _catalog = catalog;
            Race = catalog.GetProgressionIdentityCatalogTyped().RaceDefs.Values.First(race => race.TraitIds.Count > 0);
            RaceTrait = catalog.GetTraitDefsTyped()[Race.TraitIds[0]];
            CharacterTrait = BuildTraitFixture();
            Item = catalog.GetItemDefsTyped().Values.First(item => item.IsWeapon());
            Skill = catalog.GetSkillDefinitionsTyped().Values.First();
        }

        public string FormatCoord(Vector2I coord) => $"({coord.X},{coord.Y})";
        public string GetSkillDisplayName(StringName id) => _catalog.GetSkillDefinitionsTyped()[id].DisplayName;
        public bool HasPartyMember(StringName id) => false;
        public bool TryGetItemDefinition(StringName id, out ItemDefinition definition) => _catalog.GetItemDefsTyped().TryGetValue(id, out definition);
        public bool TryGetTraitDefinition(StringName id, out TraitDefinition definition)
        {
            if (id == CharacterTrait.TraitId)
            {
                definition = CharacterTrait;
                return true;
            }
            return _catalog.GetTraitDefsTyped().TryGetValue(id, out definition);
        }

        private static TraitDefinition BuildTraitFixture()
        {
            var import = new TraitImportModel("test_resilience", "韧性（展示测试）", "体质提高 2 点。",
                Array.Empty<string>(), new[] { "character", "equipment_fixed", "equipment_roll", "gear_set_threshold" },
                "attribute_modifier", "passive", "stack_by_instance", "none", "none", "", 0, 0,
                new[] { new TraitAttributeModifierImportModel("constitution", "flat", 2, 0, "", "") },
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<TraitDamageResistanceEntryImportModel>(), Array.Empty<TraitSaveBonusEntryImportModel>(),
                Array.Empty<TraitSaveTagBonusEntryImportModel>(), Array.Empty<TraitPassiveStatusEffectImportModel>(),
                Array.Empty<TraitRollValueSchemaEntryImportModel>());
            var errors = new TraitImportModelValidator().ValidateMessages(import);
            if (errors.Count > 0)
                throw new InvalidOperationException(errors[0]);
            return TraitDefinitionProjector.Project(import);
        }
        public bool TryGetSkillDefinition(StringName id, out SkillDefinition definition) => _catalog.GetSkillDefinitionsTyped().TryGetValue(id, out definition);
        public ProgressionIdentityCatalogData GetIdentityCatalog() => _catalog.GetProgressionIdentityCatalogTyped();
        public Godot.Collections.Dictionary GetIdentitySummary(StringName id) => new();
    }
}
