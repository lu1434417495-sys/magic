using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_skill_tooltip_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        BattleMapPanel panel = null;
        BattleTestFixture fixture = null;
        using var adapter = new BattleHudAdapter();
        try
        {
            int captureScale = int.TryParse(OS.GetEnvironment("MAGIC_SKILL_TOOLTIP_CAPTURE_SCALE"), out int configuredScale)
                ? Math.Clamp(configuredScale, 1, 3) : 1;
            bool fullscreen = OS.GetEnvironment("MAGIC_SKILL_TOOLTIP_CAPTURE_FULLSCREEN") == "1";
            var displaySettings = new DisplaySettingsService();
            Root.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            displaySettings.ApplySettings(new(new Vector2I(1280, 720) * captureScale, fullscreen), Root);
            Vector2 expectedFrameSize = Vector2.Zero;
            Root.GuiEmbedSubwindows = true;
            var catalog = Root.GetNode<GameSession>("GameSession").GetContentCatalogTyped();
            SkillDefinition skill = catalog.GetSkillDefinitionsTyped()["mage_frost_bolt"];
            var caster = BattleTestFixture.BuildUnit("tooltip_caster", "player", new Vector2I(2, 2), 3);
            caster.display_name = "霜术师";
            caster.SetCombatResources(100, 80, 0, 0, 3, 4);
            caster.attribute_snapshot.SetValue("mp_max", 100);
            caster.SetKnownActiveSkillIds(new[] { skill.SkillId });
            caster.SetKnownSkillLevelTyped(skill.SkillId, 3);
            var member = new PartyMemberState { member_id = "tooltip_member" };
            var progress = new UnitSkillProgress
            {
                skill_id = skill.SkillId, is_learned = true, skill_level = 3,
                current_mastery = 18, total_mastery_earned = 18,
            };
            member.progression.SetSkillProgress(progress);
            caster.source_member_id = member.member_id;
            var enemy = BattleTestFixture.BuildUnit("tooltip_enemy", "enemy", new Vector2I(5, 2));
            enemy.display_name = "训练对手";
            fixture = BattleTestFixture.CreateFlatBattle("tooltip_preview", new Vector2I(8, 5), new[] { caster }, new[] { enemy });
            var context = new Context(catalog, fixture.State, member);
            adapter.SetupRuntimeContext(context);
            BattleHudSnapshot Snapshot() => adapter.BuildSnapshot(fixture.State, caster.GetAnchorCoord(), "", "", "",
                Array.Empty<Vector2I>(), 1, Array.Empty<StringName>(), "", "技能详情预览", null);
            var snapshot = Snapshot();
            var slot = snapshot.SkillSlots.First(value => !value.IsEmpty);
            _test.Eq(slot.SkillLevel, 3, "展示应读取当前技能等级。");
            _test.Eq(slot.Tooltip.Costs.MpCost, 20, "3 级霜击术应显示 20 MP。");
            _test.Eq(slot.Tooltip.Range, 4, "3 级霜击术应显示 4 格射程。");
            _test.Eq(slot.Tooltip.Costs.CooldownTu, 180, "基础冷却应与剩余冷却分别保存。");
            _test.Eq(slot.Tooltip.Mastery.Current, 18, "熟练度应来自角色当前成长进度。");
            _test.Eq(slot.Tooltip.Mastery.Required, skill.GetMasteryRequiredForLevel(3),
                "升级门槛应使用技能正式熟练度曲线。");
            _test.True(slot.Tooltip.LevelEffect.Contains("2D6") && slot.Tooltip.LevelEffect.Contains("60TU"),
                "应使用当前等级描述模板中的伤害与持续时间。");

            panel = GD.Load<PackedScene>("res://scenes/ui/battle_map_panel.tscn").Instantiate<BattleMapPanel>();
            Root.AddChild(panel);
            panel.ShowBattle(fixture.State, caster.GetAnchorCoord(), "", "", "", Array.Empty<Vector2I>(),
                Array.Empty<Vector2I>(), 1, Array.Empty<StringName>(), "");
            await Frames(20);
            panel._apply_snapshot(snapshot);
            await Frames(8);
            BattleSkillSlotButton Button() => panel.skill_grid.FindChildren("*", "Button", true, false)
                .OfType<BattleSkillSlotButton>().Single();
            await Hover(Button());
            Control tooltip = FindTooltip(Root);
            _test.True(tooltip != null, "鼠标停在技能图标上应自动创建自定义详情窗口。");
            if (tooltip != null)
            {
                string text = Text(tooltip);
                _test.True(text.Contains("霜击术") && text.Contains("等级 3") && text.Contains("2D6"),
                    "真正悬停窗口应展示技能名、等级和实际效果。");
                _test.True(text.Contains("20 MP") && text.Contains("180 TU"), "关键消耗与冷却应明确展示。");
                _test.False(text.Contains(skill.Description) || text.Contains("施放说明"),
                    "战斗悬停只展示等级说明，不展示普通描述。");
                _test.True(text.Contains($"18 / {skill.GetMasteryRequiredForLevel(3)}"),
                    "熟练度应同时展示当前值和升级门槛。");
                ProgressBar bar = tooltip.FindChildren("*", "ProgressBar", true, false).OfType<ProgressBar>().Single();
                _test.Eq((int)bar.Value, 18, "熟练度进度条应反映真实进度。");
                expectedFrameSize = tooltip.Size;
                AssertViewportQuarter(tooltip);
                var effectLabel = tooltip.FindChildren("LevelEffectText", "Label", true, false).OfType<Label>().Single();
                _test.Eq(effectLabel.GetThemeFontSize("font_size"), 18, "改变图框尺寸不改变正文字号。");
                _test.True(new Rect2(Vector2.Zero, Root.GetVisibleRect().Size).Encloses(tooltip.GetGlobalRect()),
                    "放大的悬停框应完整留在视口内。");
                await Capture(tooltip, "frost-bolt-level-3.png");
                await Capture(panel, "battle-hover.png");
            }

            await MoveAway();
            _test.True(FindTooltip(Root) == null, "鼠标离开后应关闭悬停窗口。");
            progress.current_mastery = 21;
            progress.total_mastery_earned = 21;
            panel._apply_snapshot(Snapshot());
            await Frames(5);
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null && Text(tooltip).Contains($"21 / {skill.GetMasteryRequiredForLevel(3)}"),
                "仅熟练度变化时也必须刷新悬停内容。");
            _test.Eq(slot.Tooltip.Mastery.Current, 18, "旧 HUD 快照不应随角色进度变化。");

            await MoveAway();
            caster.SetKnownSkillLevelTyped(skill.SkillId, 4);
            progress.skill_level = 4;
            panel._apply_snapshot(Snapshot());
            await Frames(5);
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null && Text(tooltip).Contains("等级 4") && Text(tooltip).Contains("5 格"),
                "等级和射程变化即使图标及消耗不变，也必须刷新缓存。");

            await MoveAway();
            context.BlockReason = "法力不足";
            panel._apply_snapshot(Snapshot());
            await Frames(5);
            _test.True(Button().Disabled, "不可用技能应继续禁止点击。");
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null, "禁用图标也应支持悬停。");
            if (tooltip != null)
                _test.True(Text(tooltip).Contains("法力不足") && Text(tooltip).Contains("20 MP"),
                    $"禁用详情应保留费用和不可用原因。实际内容：{Text(tooltip)}");
            await MoveAway();

            progress.skill_level = SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(skill, progress, member.progression);
            progress.current_mastery = 0;
            caster.SetKnownSkillLevelTyped(skill.SkillId, progress.skill_level);
            panel._apply_snapshot(Snapshot());
            await Frames(5);
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null && Text(tooltip).Contains("已达当前上限"), "满级应明确显示当前等级上限。");
            if (tooltip != null)
                _test.Eq(tooltip.FindChildren("*", "ProgressBar", true, false).Count, 0, "满级不显示误导性的空进度条。");
            await MoveAway();

            caster.source_member_id = "";
            var noProgress = Snapshot();
            _test.True(noProgress.SkillSlots.First(value => !value.IsEmpty).Tooltip.Mastery == null,
                "没有角色成长数据的单位不能虚构熟练度。");
            panel._apply_snapshot(noProgress);
            await Frames(5);
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null && Text(tooltip).Contains("暂无成长进度"), "无成长进度的技能应显示明确空态。");
            await MoveAway();

            SkillDefinition longSkill = catalog.GetSkillDefinitionsTyped()["mage_phantasmal_kill"];
            caster.source_member_id = member.member_id;
            caster.SetKnownActiveSkillIds(new[] { longSkill.SkillId });
            caster.SetKnownSkillLevelTyped(longSkill.SkillId, 1);
            member.progression.SetSkillProgress(new UnitSkillProgress
            {
                skill_id = longSkill.SkillId, is_learned = true, skill_level = 1,
                current_mastery = 620, total_mastery_earned = 620,
            });
            context.BlockReason = "";
            panel._apply_snapshot(Snapshot());
            await Frames(5);
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null, "长等级说明也应支持悬停。");
            if (tooltip != null)
            {
                _test.Eq(tooltip.Size, expectedFrameSize, "长说明不能撑大固定图框。");
                var scroll = tooltip.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single();
                await Capture(tooltip, "phantasmal-kill.png");
                await Capture(panel, "long-skill-battle-hover.png");
                await MoveAway();
                BattleHudSkillSlotSnapshot formalSlot = Snapshot().SkillSlots.First(value => !value.IsEmpty);
                var stressSlot = new BattleHudSkillSlotSnapshot(formalSlot.Index, false,
                    skillEntryId: formalSlot.SkillEntryId, skillId: formalSlot.SkillId,
                    skillLevel: formalSlot.SkillLevel, displayName: formalSlot.DisplayName,
                    shortName: formalSlot.ShortName, iconKey: formalSlot.IconKey,
                    tooltip: formalSlot.Tooltip with
                    {
                        LevelEffect = string.Join("\n\n", Enumerable.Repeat(formalSlot.Tooltip.LevelEffect, 4))
                            + "\n长文本验证结束。",
                    });
                Button().SetSnapshot(stressSlot);
                await Hover(Button());
                tooltip = FindTooltip(Root);
                _test.True(tooltip != null, "超长说明应继续显示固定详情框。");
                if (tooltip == null) return;
                scroll = tooltip.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single();
                _test.True(scroll.GetVScrollBar().MaxValue > scroll.GetVScrollBar().Page,
                    "超长等级说明应在独立区域内滚动。");
                var masteryBar = tooltip.FindChildren("*", "ProgressBar", true, false).OfType<ProgressBar>().Single();
                Rect2 masteryRect = masteryBar.GetGlobalRect();
                Vector2 scrollPoint = scroll.GetGlobalRect().GetCenter();
                using var motion = new InputEventMouseMotion { Position = scrollPoint, GlobalPosition = scrollPoint };
                Root.PushInput(motion, true);
                await ToSignal(CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                _test.True(FindTooltip(Root) == tooltip, "鼠标从图标移入详情框后应保持显示。");
                for (int i = 0; i < 80 && scroll.ScrollVertical < scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page - 1; i++)
                {
                    using var wheel = new InputEventMouseButton { Position = scrollPoint, GlobalPosition = scrollPoint,
                        ButtonIndex = MouseButton.WheelDown, Pressed = true, Factor = 3 };
                    Root.PushInput(wheel, true);
                    await Frames(1);
                }
                await Frames(8);
                _test.True(scroll.ScrollVertical > 0, "鼠标滚轮应能读取长等级说明。");
                _test.Eq(masteryBar.GetGlobalRect(), masteryRect, "滚动等级说明时熟练度位置保持固定。");
                _test.Eq(tooltip.Size, expectedFrameSize, "滚动不能改变图框尺寸。");
                _test.True(scroll.ScrollVertical >= scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page - 1,
                    "滚轮应能读到超长说明末尾。");
                await Capture(tooltip, "long-text-scroll-proof.png");
            }

            displaySettings.ApplySettings(new(new Vector2I(1920, 1080), false), Root);
            await Frames(12);
            _test.True(FindTooltip(Root) == null, "显示尺寸变化应清理旧尺寸悬停框。");
            await MoveAway();
            await Hover(Button());
            tooltip = FindTooltip(Root);
            _test.True(tooltip != null, "调整显示尺寸后可重新悬停。");
            if (tooltip != null)
            {
                AssertViewportQuarter(tooltip);
                var effectLabel = tooltip.FindChildren("LevelEffectText", "Label", true, false).OfType<Label>().Single();
                _test.Eq(effectLabel.GetThemeFontSize("font_size"), 18, "全屏或大窗口使用相同正文字号。");
            }
            panel.HideBattle();
            await Frames(5);
            _test.True(FindTooltip(Root) == null, "离开战斗时必须清理悬停框。");
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            panel?.Free();
            fixture?.Dispose();
            RequestTestExit(_test.Finish("Battle skill tooltip regression"));
        }
    }

    private void AssertViewportQuarter(Control tooltip)
    {
        Vector2 viewportSize = Root.GetVisibleRect().Size;
        float ratio = tooltip.Size.X * tooltip.Size.Y / (viewportSize.X * viewportSize.Y);
        _test.True(Mathf.Abs(ratio - 0.25f) < 0.005f, "图框应占当前视口约四分之一面积。");
        _test.True(new Rect2(Vector2.Zero, viewportSize).Encloses(tooltip.GetGlobalRect()),
            "图框应保持完整可见。");
    }

    private async Task Hover(Control control)
    {
        // Keep the simulated pointer on the target while native layout and hover timers settle.
        ulong deadline = Time.GetTicksMsec() + 2500;
        do
        {
            using var motion = new InputEventMouseMotion
            {
                Position = control.GetGlobalRect().GetCenter(), GlobalPosition = control.GetGlobalRect().GetCenter(),
            };
            Root.PushInput(motion, true);
            await Frames(3);
        } while (FindTooltip(Root) == null && Time.GetTicksMsec() < deadline);
        await Frames(5);
    }

    private async Task MoveAway()
    {
        using var motion = new InputEventMouseMotion { Position = new Vector2(10, 10), GlobalPosition = new Vector2(10, 10) };
        Root.PushInput(motion, true);
        await ToSignal(CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        await Frames(5);
    }

    private static Control FindTooltip(Node node)
    {
        if (node is Control control && node.Name == "BattleSkillTooltip" && control.IsVisibleInTree())
            return control;
        foreach (Node child in node.GetChildren(true))
            if (FindTooltip(child) is Control tooltip) return tooltip;
        return null;
    }

    private static string Text(Node node) => string.Join("\n", node.FindChildren("*", "Label", true, false)
        .OfType<Label>().Select(label => label.Text));

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(this, SignalName.ProcessFrame);
    }

    private async Task Capture(Control control, string fileName)
    {
        string directory = OS.GetEnvironment("MAGIC_SKILL_TOOLTIP_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory) || DisplayServer.GetName() == "headless") return;
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = control.GetViewport().GetTexture().GetImage();
        if (control is BattleSkillTooltipPanel)
        {
            Rect2 rect = control.GetGlobalRect();
            float scale = image.GetWidth() / Root.GetVisibleRect().Size.X;
            using Image cropped = image.GetRegion(new Rect2I((Vector2I)(rect.Position * scale), (Vector2I)(rect.Size * scale)));
            _test.Eq(cropped.SavePng(System.IO.Path.Combine(directory, fileName)), Error.Ok, "应保存真实技能悬停截图。");
        }
        else
            _test.Eq(image.SavePng(System.IO.Path.Combine(directory, fileName)), Error.Ok, "应保存战斗截图。");
    }

    private sealed class Context : IBattleHudContext
    {
        private readonly GameContentCatalog _catalog;
        private readonly BattleState _state;
        private readonly PartyMemberState _member;
        internal string BlockReason = "";
        internal Context(GameContentCatalog catalog, BattleState state, PartyMemberState member)
        { _catalog = catalog; _state = state; _member = member; }
        public BattleState GetBattleState() => _state;
        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindings() => _catalog.GetEquipmentAbilityBindingDefinitionsTyped();
        public int GetBattleWorldStep() => 0;
        public BattlePreview PreviewBattleCommand(BattleCommand command) => null;
        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitions() => _catalog.GetItemDefsTyped();
        public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitions() => _catalog.GetSkillDefinitionsTyped();
        public ISkillCatalog GetSkillCatalog() => _catalog.GetSkillCatalogTyped();
        public PartyMemberState GetPartyMemberState(StringName id) => id == _member.member_id ? _member : null;
        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(StringName id, EquipmentState equipment) => null;
        public string GetBattleSkillCastBlockMessage(BattleUnitState unit, StringName skillId) => BlockReason;
    }
}
