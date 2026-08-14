using Godot;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class BattlePreview
{
    private readonly List<string> _logLines = new();
    private readonly List<StringName> _targetUnitIds = new();
    private readonly List<Vector2I> _targetCoords = new();
    private readonly List<Vector2I> _sourceRetreatPath = new();
    private readonly List<Vector2I> _sourceAdvancePath = new();
    private readonly List<StringName> _randomChainCandidateUnitIds = new();
    private readonly List<StringName> _randomChainImpactCandidateUnitIds = new();
    private readonly List<BattleStatusContributionPreviewData> _statusContributionPreviews =
        new();
    private readonly ReadOnlyCollection<string> _logLinesView;
    private readonly ReadOnlyCollection<StringName> _targetUnitIdsView;
    private readonly ReadOnlyCollection<Vector2I> _targetCoordsView;
    private readonly ReadOnlyCollection<Vector2I> _sourceRetreatPathView;
    private readonly ReadOnlyCollection<Vector2I> _sourceAdvancePathView;
    private readonly ReadOnlyCollection<StringName> _randomChainCandidateUnitIdsView;
    private readonly ReadOnlyCollection<StringName> _randomChainImpactCandidateUnitIdsView;
    private readonly ReadOnlyCollection<BattleStatusContributionPreviewData>
        _statusContributionPreviewsView;
    private BattleSaveBranchPreviewData _saveBranchPreview;
    private BattleDamagePreviewRangeService.SkillDamagePreview? _damagePreview;
    private BattleFatePreviewData _fatePreview;
    private BattleEquipmentAbilityCommandPreviewResult _equipmentAbilityPreview;
    private BattleTerrainContactPreviewData _terrainContactPreview;
    private BattleShieldPreviewData _shieldPreview;
    private BattleEquipmentDurabilityPreviewData _equipmentDurabilityPreview;
    private BattleForcedMovePreviewData _forcedMovePreview;
    private BattlePositionSwapPreviewData _positionSwapPreview;
    private BattleRangedWeaponReactionPreviewData _rangedWeaponReactionPreview;

    public bool allowed { get; set; } = false;
    public ReadOnlyCollection<string> log_lines => _logLinesView;
    public StringNameList target_unit_ids
    {
        get => new(_targetUnitIds);
        set => SetTargetUnitIds(value);
    }
    public Vector2IList target_coords
    {
        get => new(_targetCoords);
        set => SetTargetCoords(value);
    }
    public StringNameList random_chain_candidate_unit_ids
    {
        get => new(_randomChainCandidateUnitIds);
        set => SetRandomChainCandidateUnitIds(value);
    }
    public Vector2IList source_retreat_path
    {
        get => new(_sourceRetreatPath);
        set => SetSourceRetreatPath(value);
    }
    public Vector2IList source_advance_path
    {
        get => new(_sourceAdvancePath);
        set => SetSourceAdvancePath(value);
    }
    public Vector2I resolved_anchor_coord { get; set; } = new Vector2I(-1, -1);
    public int move_cost { get; set; } = 0;
    public AttackPreviewData hit_preview { get; set; }
    public BattleSpecialProfileGateResult special_profile_gate_result { get; set; }
    public BattleSpecialProfilePreviewFacts special_profile_preview_facts { get; set; }

    /// 释放预览持有的攻击预览引用。原为 <c>BattleRuntimeModule.DisposeBattlePreview</c>，
    /// 但它只操作 BattlePreview 自身、不碰 hub，故下移到本类型所属的层。
    internal void ReleaseHitPreview()
    {
        hit_preview = null;
    }

    internal IReadOnlyList<StringName> TargetUnitIdsTyped => _targetUnitIdsView;
    internal IReadOnlyList<Vector2I> TargetCoordsTyped => _targetCoordsView;
    internal IReadOnlyList<Vector2I> SourceRetreatPathTyped => _sourceRetreatPathView;
    internal IReadOnlyList<Vector2I> SourceAdvancePathTyped => _sourceAdvancePathView;
    internal IReadOnlyList<StringName> RandomChainCandidateUnitIdsTyped =>
        _randomChainCandidateUnitIdsView;
    internal IReadOnlyList<StringName> RandomChainImpactCandidateUnitIdsTyped =>
        _randomChainImpactCandidateUnitIdsView;
    internal IReadOnlyList<string> LogLinesTyped => _logLinesView;
    internal IReadOnlyList<BattleStatusContributionPreviewData>
        StatusContributionPreviewsTyped => _statusContributionPreviewsView;
    internal BattleDamagePreviewRangeService.SkillDamagePreview? DamagePreviewTyped =>
        CloneDamagePreview(_damagePreview);
    internal BattleFatePreviewData FatePreviewTyped => _fatePreview ?? hit_preview?.FatePreview;
    internal BattleSaveBranchPreviewData SaveBranchPreviewTyped => _saveBranchPreview;
    internal BattleEquipmentAbilityCommandPreviewResult EquipmentAbilityPreviewTyped =>
        _equipmentAbilityPreview ?? BattleEquipmentAbilityCommandPreviewResult.None;
    internal BattleTerrainContactPreviewData TerrainContactPreviewTyped =>
        _terrainContactPreview;
    internal BattleShieldPreviewData ShieldPreviewTyped => CloneShieldPreview(_shieldPreview);
    internal BattleEquipmentDurabilityPreviewData EquipmentDurabilityPreviewTyped =>
        _equipmentDurabilityPreview?.Clone();
    internal BattleForcedMovePreviewData ForcedMovePreviewTyped =>
        _forcedMovePreview?.Clone();
    internal BattlePositionSwapPreviewData PositionSwapPreviewTyped =>
        _positionSwapPreview?.Clone();
    internal BattleRangedWeaponReactionPreviewData RangedWeaponReactionPreviewTyped =>
        _rangedWeaponReactionPreview;

    public BattlePreview()
    {
        _logLinesView = _logLines.AsReadOnly();
        _targetUnitIdsView = _targetUnitIds.AsReadOnly();
        _targetCoordsView = _targetCoords.AsReadOnly();
        _sourceRetreatPathView = _sourceRetreatPath.AsReadOnly();
        _sourceAdvancePathView = _sourceAdvancePath.AsReadOnly();
        _randomChainCandidateUnitIdsView = _randomChainCandidateUnitIds.AsReadOnly();
        _randomChainImpactCandidateUnitIdsView =
            _randomChainImpactCandidateUnitIds.AsReadOnly();
        _statusContributionPreviewsView = _statusContributionPreviews.AsReadOnly();
    }

    internal void SetTargetUnitIds(IEnumerable<StringName> values)
    {
        _targetUnitIds.Clear();
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            _targetUnitIds.Add(ProgressionDataUtils.to_string_name(value));
        }
    }

    internal void ClearTargetUnitIds()
    {
        _targetUnitIds.Clear();
    }

    internal void AddTargetUnitId(StringName value)
    {
        _targetUnitIds.Add(ProgressionDataUtils.to_string_name(value));
    }

    internal bool ContainsTargetUnitId(StringName value)
    {
        return _targetUnitIds.Contains(ProgressionDataUtils.to_string_name(value));
    }

    internal void SetTargetCoords(IEnumerable<Vector2I> values)
    {
        _targetCoords.Clear();
        if (values == null)
        {
            return;
        }
        foreach (Vector2I value in values)
        {
            _targetCoords.Add(value);
        }
    }

    internal void ClearTargetCoords()
    {
        _targetCoords.Clear();
    }

    internal void AddTargetCoord(Vector2I value)
    {
        _targetCoords.Add(value);
    }

    internal bool ContainsTargetCoord(Vector2I value)
    {
        return _targetCoords.Contains(value);
    }

    internal void SetSourceRetreatPath(IEnumerable<Vector2I> values)
    {
        _sourceRetreatPath.Clear();
        if (values == null)
        {
            return;
        }
        foreach (Vector2I value in values)
        {
            _sourceRetreatPath.Add(value);
        }
    }

    internal void ClearSourceRetreatPath()
    {
        _sourceRetreatPath.Clear();
    }

    internal void SetSourceAdvancePath(IEnumerable<Vector2I> values)
    {
        _sourceAdvancePath.Clear();
        if (values == null)
            return;
        foreach (Vector2I value in values)
            _sourceAdvancePath.Add(value);
    }

    internal void ClearSourceAdvancePath()
    {
        _sourceAdvancePath.Clear();
    }

    internal void SetRandomChainCandidateUnitIds(IEnumerable<StringName> values)
    {
        _randomChainCandidateUnitIds.Clear();
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            _randomChainCandidateUnitIds.Add(ProgressionDataUtils.to_string_name(value));
        }
    }

    internal void ClearRandomChainCandidateUnitIds()
    {
        _randomChainCandidateUnitIds.Clear();
    }

    internal void AddRandomChainCandidateUnitId(StringName value)
    {
        _randomChainCandidateUnitIds.Add(ProgressionDataUtils.to_string_name(value));
    }

    internal void SetRandomChainImpactCandidateUnitIds(IEnumerable<StringName> values)
    {
        _randomChainImpactCandidateUnitIds.Clear();
        if (values == null)
            return;
        foreach (StringName value in values)
        {
            _randomChainImpactCandidateUnitIds.Add(
                ProgressionDataUtils.to_string_name(value)
            );
        }
    }

    internal void SetLogLines(IEnumerable values)
    {
        _logLines.Clear();
        if (values == null)
        {
            return;
        }
        foreach (object value in values)
        {
            _logLines.Add(value?.ToString() ?? "");
        }
    }

    internal void ClearLogLines()
    {
        _logLines.Clear();
    }

    internal void AddLogLine(string value)
    {
        _logLines.Add(value ?? "");
    }

    internal void ClearStatusContributionPreviews()
    {
        _statusContributionPreviews.Clear();
    }

    internal void SetRangedWeaponReactionPreview(
        BattleRangedWeaponReactionPreviewData preview
    )
    {
        _rangedWeaponReactionPreview = preview;
    }

    internal void AddStatusContributionPreview(
        BattleStatusContributionPreviewData value
    )
    {
        if (value != null)
            _statusContributionPreviews.Add(value);
    }

    internal void InsertLogLine(int index, string value)
    {
        _logLines.Insert(index, value ?? "");
    }

    internal void SetDamagePreview(BattleDamagePreviewRangeService.SkillDamagePreview? value)
    {
        _damagePreview = CloneDamagePreview(value);
    }

    internal void ClearDamagePreview()
    {
        _damagePreview = null;
    }

    internal void SetFatePreview(BattleFatePreviewData value)
    {
        _fatePreview = value;
    }

    internal void ClearFatePreview()
    {
        _fatePreview = null;
    }

    internal void SetSaveBranchPreview(BattleSaveBranchPreviewData value)
    {
        _saveBranchPreview = value?.Clone();
    }

    internal void ClearSaveBranchPreview()
    {
        _saveBranchPreview = null;
    }

    internal void SetEquipmentAbilityPreview(
        BattleEquipmentAbilityCommandPreviewResult value
    )
    {
        _equipmentAbilityPreview = value;
    }

    internal void SetTerrainContactPreview(BattleTerrainContactPreviewData value)
    {
        _terrainContactPreview = value;
    }

    internal void SetShieldPreview(BattleShieldPreviewData value)
    {
        _shieldPreview = CloneShieldPreview(value);
    }

    internal void ClearShieldPreview()
    {
        _shieldPreview = null;
    }

    internal void SetEquipmentDurabilityPreview(BattleEquipmentDurabilityPreviewData value)
    {
        _equipmentDurabilityPreview = value?.Clone();
    }

    internal void ClearEquipmentDurabilityPreview()
    {
        _equipmentDurabilityPreview = null;
    }

    internal void SetForcedMovePreview(BattleForcedMovePreviewData value)
    {
        _forcedMovePreview = value?.Clone();
    }

    internal void ClearForcedMovePreview()
    {
        _forcedMovePreview = null;
    }

    internal void SetPositionSwapPreview(BattlePositionSwapPreviewData value)
    {
        _positionSwapPreview = value?.Clone();
    }

    internal void ClearPositionSwapPreview()
    {
        _positionSwapPreview = null;
    }

    private static BattleDamagePreviewRangeService.SkillDamagePreview? CloneDamagePreview(
        BattleDamagePreviewRangeService.SkillDamagePreview? value
    )
    {
        if (!value.HasValue)
        {
            return null;
        }
        BattleDamagePreviewRangeService.SkillDamagePreview preview = value.Value;
        return new BattleDamagePreviewRangeService.SkillDamagePreview(
            preview.HasDamage,
            preview.MinDamage,
            preview.MaxDamage,
            new List<BattleDamagePreviewRangeService.DamageEffectRange>(
                preview.DamageRanges
                    ?? System.Array.Empty<BattleDamagePreviewRangeService.DamageEffectRange>()
            )
        );
    }

    private static BattleShieldPreviewData CloneShieldPreview(BattleShieldPreviewData value)
    {
        if (value == null)
        {
            return null;
        }
        return new BattleShieldPreviewData
        {
            HasShield = value.HasShield,
            MinShieldHp = value.MinShieldHp,
            MaxShieldHp = value.MaxShieldHp,
            ExpectedShieldHpBasisPoints = value.ExpectedShieldHpBasisPoints,
            AttributeModifier = value.AttributeModifier,
            DurationTu = value.DurationTu,
            TargetCount = value.TargetCount,
            ExpectedBenefitingTargetCount = value.ExpectedBenefitingTargetCount,
            ExpectedTotalNetGainBasisPoints = value.ExpectedTotalNetGainBasisPoints,
            RollPerTarget = value.RollPerTarget,
            ShieldFamily = value.ShieldFamily,
        };
    }

}
