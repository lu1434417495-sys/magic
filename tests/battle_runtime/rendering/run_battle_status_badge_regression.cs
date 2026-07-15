using System;
using Godot;

public partial class run_battle_status_badge_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestAdapterProjectsStatusEffects();
        TestAdapterHonorsDebuffOverride();
        TestHoverOverlayRendersTargetStatuses();
        RequestTestExit(_test.Finish("Battle status badge regression"));
    }

    private void TestAdapterProjectsStatusEffects()
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            "status_unit",
            "player",
            new Vector2I(0, 0),
            currentAp: 2,
            currentHp: 30
        );
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "poisoned",
                stacks = 2,
                duration = 30,
            }
        );
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "attack_up",
                display_label = "攻击提升",
                stacks = 1,
                duration = -1,
            }
        );

        var statuses = BattleHudAdapter.BuildStatusEffectSnapshots(unit);
        _test.Eq(statuses.Count, 2, "adapter 应为每个状态效果输出一条快照。");

        BattleHudStatusEffectSnapshot poisoned = FindStatus(statuses, "poisoned");
        _test.True(poisoned != null, "应包含 poisoned 状态快照。");
        _test.Eq(poisoned.Label, "中毒", "无 display_label 时应回落到语义表标签。");
        _test.Eq(poisoned.Stacks, 2, "应投影层数。");
        _test.Eq(poisoned.RemainingTu, 30, "应投影剩余 TU。");
        _test.True(poisoned.IsDebuff, "poisoned 应按语义表判为减益。");
        _test.True(
            poisoned.TooltipText.Contains("剩余 30 TU"),
            "tooltip 应包含剩余时长。"
        );

        BattleHudStatusEffectSnapshot attackUp = FindStatus(statuses, "attack_up");
        _test.True(attackUp != null, "应包含 attack_up 状态快照。");
        _test.Eq(attackUp.Label, "攻击提升", "配置了 display_label 时应优先使用。");
        _test.Eq(attackUp.RemainingTu, -1, "无时限状态 remaining_tu 应为 -1。");
        _test.False(attackUp.IsDebuff, "attack_up 应判为增益。");
        _test.True(
            attackUp.TooltipText.Contains("持续到战斗结束"),
            "无时限状态 tooltip 应说明持续语义。"
        );

        _test.Eq(
            BattleMapPanel.FormatStatusBadgeText(poisoned),
            "中毒×2 30TU",
            "徽章文本应拼接 label×层数 + 剩余TU。"
        );
        _test.Eq(
            BattleMapPanel.FormatStatusBadgeText(attackUp),
            "攻击提升",
            "单层无时限状态徽章只显示标签。"
        );
    }

    private void TestAdapterHonorsDebuffOverride()
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            "override_unit",
            "player",
            new Vector2I(0, 0),
            currentAp: 2,
            currentHp: 30
        );
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "custom_curse",
                display_label = "诅咒",
                stacks = 1,
                duration = 20,
                counts_as_debuff_override = true,
                counts_as_debuff = true,
            }
        );

        var statuses = BattleHudAdapter.BuildStatusEffectSnapshots(unit);
        BattleHudStatusEffectSnapshot curse = FindStatus(statuses, "custom_curse");
        _test.True(curse != null, "应包含 custom_curse 状态快照。");
        _test.True(curse.IsDebuff, "counts_as_debuff_override 应生效判为减益。");
    }

    private void TestHoverOverlayRendersTargetStatuses()
    {
        var overlay = new BattleHoverPreviewOverlay();
        Root.AddChild(overlay);
        overlay._Ready();

        overlay.ApplyPreview(MakeHover(statusEffects: new[]
        {
            new BattleHudStatusEffectSnapshot(
                "poisoned", "中毒", 2, 30, true, "中毒 · 减益 · 层数 2 · 剩余 30 TU"
            ),
        }));
        var statusRow = overlay.GetNode<HFlowContainer>("HoverLayout/TargetStatusRow");
        _test.True(statusRow.Visible, "目标有状态效果时 TargetStatusRow 应可见。");
        _test.Eq(statusRow.GetChildCount(), 1, "TargetStatusRow 应为每个状态渲染一个徽章。");
        Label badgeLabel = statusRow.GetChild(0).GetChild<Label>(0);
        _test.Eq(badgeLabel.Text, "中毒×2 30TU", "hover 状态徽章文本应与 UnitCard 规则一致。");

        overlay.ApplyPreview(MakeHover(statusEffects: Array.Empty<BattleHudStatusEffectSnapshot>()));
        _test.False(statusRow.Visible, "目标无状态效果时 TargetStatusRow 应隐藏。");

        overlay.QueueFree();
    }

    private static BattleHudStatusEffectSnapshot FindStatus(
        System.Collections.Generic.IReadOnlyList<BattleHudStatusEffectSnapshot> statuses,
        string statusId
    )
    {
        foreach (BattleHudStatusEffectSnapshot status in statuses)
        {
            if (status.StatusId == statusId)
                return status;
        }
        return null;
    }

    private static BattleHoverSnapshot MakeHover(
        System.Collections.Generic.IReadOnlyList<BattleHudStatusEffectSnapshot> statusEffects
    )
    {
        var targetUnit = new BattleHoverTargetUnitSnapshot(
            UnitId: new StringName("hover_target"),
            Name: "测试目标",
            Glyph: "T",
            PortraitKey: "",
            PrimaryColor: new Color(0.2f, 0.2f, 0.2f, 1.0f),
            EdgeColor: new Color(0.9f, 0.5f, 0.2f, 1.0f),
            HpCurrent: 30,
            HpMax: 30,
            MpCurrent: 0,
            MpMax: 1,
            MpVisible: false,
            StaminaCurrent: 0,
            StaminaMax: 1,
            AuraCurrent: 0,
            AuraMax: 1,
            AuraVisible: false,
            ApCurrent: 0,
            ApMax: 1,
            IsEnemy: true,
            IsSelf: false,
            StatusEffects: statusEffects
        );
        return new BattleHoverSnapshot(
            hoverCoord: new Vector2I(1, 0),
            hoverIsValidTarget: false,
            hasSelectedSkill: false,
            hitPreview: BattlePresentationPayload.Empty,
            hitStageRates: Array.Empty<int>(),
            hitBadgeText: "",
            fateBadges: Array.Empty<BattleHudFateBadgeSnapshot>(),
            saveBranchPreview: BattlePresentationPayload.Empty,
            saveBranchPreviewText: "",
            damageMin: 0,
            damageMax: 0,
            damageText: "",
            targetUnit: targetUnit
        );
    }
}
