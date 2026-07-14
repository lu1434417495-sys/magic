using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed record BattleHoverTargetUnitSnapshot(
    StringName UnitId,
    string Name,
    string Glyph,
    string PortraitKey,
    Color PrimaryColor,
    Color EdgeColor,
    int HpCurrent,
    int HpMax,
    int MpCurrent,
    int MpMax,
    bool MpVisible,
    int StaminaCurrent,
    int StaminaMax,
    int AuraCurrent,
    int AuraMax,
    bool AuraVisible,
    int ApCurrent,
    int ApMax,
    bool IsEnemy,
    bool IsSelf
) : IBattlePresentationSnapshotValue
{
    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("unit_id", UnitId ?? new StringName("")),
            ("name", Name ?? ""),
            ("glyph", Glyph ?? ""),
            ("portrait_key", PortraitKey ?? ""),
            ("primary_color", PrimaryColor),
            ("edge_color", EdgeColor),
            ("hp_current", HpCurrent),
            ("hp_max", HpMax),
            ("mp_current", MpCurrent),
            ("mp_max", MpMax),
            ("mp_visible", MpVisible),
            ("stamina_current", StaminaCurrent),
            ("stamina_max", StaminaMax),
            ("aura_current", AuraCurrent),
            ("aura_max", AuraMax),
            ("aura_visible", AuraVisible),
            ("ap_current", ApCurrent),
            ("ap_max", ApMax),
            ("is_enemy", IsEnemy),
            ("is_self", IsSelf)
        );
}

internal sealed class BattleHoverSnapshot : IBattlePresentationSnapshotValue
{
    private readonly ReadOnlyCollection<int> _hitStageRates;
    private readonly ReadOnlyCollection<BattleHudFateBadgeSnapshot> _fateBadges;

    internal BattleHoverSnapshot(
        Vector2I hoverCoord,
        bool hoverIsValidTarget,
        bool hasSelectedSkill,
        BattlePresentationPayload hitPreview,
        IEnumerable<int> hitStageRates,
        string hitBadgeText,
        IEnumerable<BattleHudFateBadgeSnapshot> fateBadges,
        BattlePresentationPayload saveBranchPreview,
        string saveBranchPreviewText,
        int damageMin,
        int damageMax,
        string damageText,
        BattleHoverTargetUnitSnapshot targetUnit
    )
    {
        HoverCoord = hoverCoord;
        HoverIsValidTarget = hoverIsValidTarget;
        HasSelectedSkill = hasSelectedSkill;
        HitPreview = hitPreview ?? BattlePresentationPayload.Empty;
        _hitStageRates = new List<int>(hitStageRates ?? Array.Empty<int>()).AsReadOnly();
        HitBadgeText = hitBadgeText ?? "";
        _fateBadges = new List<BattleHudFateBadgeSnapshot>(
            fateBadges ?? Array.Empty<BattleHudFateBadgeSnapshot>()
        ).AsReadOnly();
        SaveBranchPreview = saveBranchPreview ?? BattlePresentationPayload.Empty;
        SaveBranchPreviewText = saveBranchPreviewText ?? "";
        DamageMin = damageMin;
        DamageMax = damageMax;
        DamageText = damageText ?? "";
        TargetUnit = targetUnit;
    }

    internal Vector2I HoverCoord { get; }
    internal bool HoverIsValidTarget { get; }
    internal bool HasSelectedSkill { get; }
    internal BattlePresentationPayload HitPreview { get; }
    internal IReadOnlyList<int> HitStageRates => _hitStageRates;
    internal string HitBadgeText { get; }
    internal IReadOnlyList<BattleHudFateBadgeSnapshot> FateBadges => _fateBadges;
    internal BattlePresentationPayload SaveBranchPreview { get; }
    internal string SaveBranchPreviewText { get; }
    internal int DamageMin { get; }
    internal int DamageMax { get; }
    internal string DamageText { get; }
    internal BattleHoverTargetUnitSnapshot TargetUnit { get; }

    public IReadOnlyDictionary<string, object> CanonicalFacts =>
        BattlePresentationSnapshotFacts.Map(
            ("hover_coord", HoverCoord),
            ("hover_is_valid_target", HoverIsValidTarget),
            ("has_selected_skill", HasSelectedSkill),
            ("hit_preview", HitPreview),
            ("hit_stage_rates", _hitStageRates),
            ("hit_badge_text", HitBadgeText),
            ("fate_badges", _fateBadges),
            ("save_branch_preview", SaveBranchPreview),
            ("save_branch_preview_text", SaveBranchPreviewText),
            ("damage_min", DamageMin),
            ("damage_max", DamageMax),
            ("damage_text", DamageText),
            ("target_unit", (object)TargetUnit ?? BattlePresentationPayload.Empty)
        );

    internal GodotProjectionLease<GDictionary> BuildLease() =>
        BattlePresentationSnapshotProjection.BuildLease(
            this,
            "battle-hover-snapshot",
            "BattleHoverSnapshot.root"
        );
}
