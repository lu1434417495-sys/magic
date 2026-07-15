using System;
using Godot;

public partial class run_battle_hover_hp_predict_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDamagePreviewSplitsHpBar();
        TestNoDamageKeepsFullHpFill();
        TestInvalidTargetSkipsPredictSegment();
        RequestTestExit(_test.Finish("Battle hover HP predict segment regression"));
    }

    private void TestDamagePreviewSplitsHpBar()
    {
        BattleHoverPreviewOverlay overlay = CreateOverlay();
        overlay.ApplyPreview(
            MakeHover(hpCurrent: 45, hpMax: 60, damageMin: 20, damageMax: 25, isValidTarget: true)
        );

        ProgressBar hpBar = overlay.GetNode<ProgressBar>("HoverLayout/TargetHpStack/TargetHpBar");
        ProgressBar lossBar = overlay.GetNode<ProgressBar>(
            "HoverLayout/TargetHpStack/TargetHpLossBar"
        );
        Label hpLabel = overlay.GetNode<Label>("HoverLayout/TargetHpLabel");
        _test.Eq((int)lossBar.Value, 45, "预扣层应按当前 HP 填充。");
        _test.Eq((int)lossBar.MaxValue, 60, "预扣层最大值应为 HP 上限。");
        _test.Eq((int)hpBar.Value, 20, "剩余层应按最坏伤害后的剩余 HP 填充（45-25）。");
        _test.Eq(hpLabel.Text, "HP 45/60 → 受击后 20~25", "HP 文本应标注受击后剩余区间。");
        overlay.QueueFree();
    }

    private void TestNoDamageKeepsFullHpFill()
    {
        BattleHoverPreviewOverlay overlay = CreateOverlay();
        overlay.ApplyPreview(
            MakeHover(hpCurrent: 45, hpMax: 60, damageMin: 0, damageMax: 0, isValidTarget: true)
        );

        ProgressBar hpBar = overlay.GetNode<ProgressBar>("HoverLayout/TargetHpStack/TargetHpBar");
        Label hpLabel = overlay.GetNode<Label>("HoverLayout/TargetHpLabel");
        _test.Eq((int)hpBar.Value, 45, "无伤害预览时剩余层应等于当前 HP。");
        _test.Eq(hpLabel.Text, "HP 45/60", "无伤害预览时不应出现受击后文本。");
        overlay.QueueFree();
    }

    private void TestInvalidTargetSkipsPredictSegment()
    {
        BattleHoverPreviewOverlay overlay = CreateOverlay();
        overlay.ApplyPreview(
            MakeHover(hpCurrent: 30, hpMax: 30, damageMin: 10, damageMax: 12, isValidTarget: false)
        );

        ProgressBar hpBar = overlay.GetNode<ProgressBar>("HoverLayout/TargetHpStack/TargetHpBar");
        Label hpLabel = overlay.GetNode<Label>("HoverLayout/TargetHpLabel");
        _test.Eq((int)hpBar.Value, 30, "非法目标不应渲染预扣段。");
        _test.Eq(hpLabel.Text, "HP 30/30", "非法目标不应出现受击后文本。");
        overlay.QueueFree();
    }

    private BattleHoverPreviewOverlay CreateOverlay()
    {
        var overlay = new BattleHoverPreviewOverlay();
        Root.AddChild(overlay);
        overlay._Ready();
        return overlay;
    }

    private static BattleHoverSnapshot MakeHover(
        int hpCurrent,
        int hpMax,
        int damageMin,
        int damageMax,
        bool isValidTarget
    )
    {
        var targetUnit = new BattleHoverTargetUnitSnapshot(
            UnitId: new StringName("hover_target"),
            Name: "测试目标",
            Glyph: "T",
            PortraitKey: "",
            PrimaryColor: new Color(0.2f, 0.2f, 0.2f, 1.0f),
            EdgeColor: new Color(0.9f, 0.5f, 0.2f, 1.0f),
            HpCurrent: hpCurrent,
            HpMax: hpMax,
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
            IsSelf: false
        );
        return new BattleHoverSnapshot(
            hoverCoord: new Vector2I(1, 0),
            hoverIsValidTarget: isValidTarget,
            hasSelectedSkill: true,
            hitPreview: BattlePresentationPayload.Empty,
            hitStageRates: Array.Empty<int>(),
            hitBadgeText: "",
            fateBadges: Array.Empty<BattleHudFateBadgeSnapshot>(),
            saveBranchPreview: BattlePresentationPayload.Empty,
            saveBranchPreviewText: "",
            damageMin: damageMin,
            damageMax: damageMax,
            damageText: damageMax > 0 ? $"伤害 {damageMin}-{damageMax}" : "",
            targetUnit: targetUnit
        );
    }
}
