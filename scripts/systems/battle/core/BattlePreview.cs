using Godot;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class BattlePreview
{
    private readonly List<string> _logLines = new();
    private readonly List<StringName> _targetUnitIds = new();
    private readonly List<Vector2I> _targetCoords = new();
    private readonly List<StringName> _randomChainCandidateUnitIds = new();
    private readonly ReadOnlyCollection<string> _logLinesView;
    private readonly ReadOnlyCollection<StringName> _targetUnitIdsView;
    private readonly ReadOnlyCollection<Vector2I> _targetCoordsView;
    private readonly ReadOnlyCollection<StringName> _randomChainCandidateUnitIdsView;
    private BattleSaveBranchPreviewData _saveBranchPreview;
    private BattleDamagePreviewRangeService.SkillDamagePreview? _damagePreview;
    private BattleFatePreviewData _fatePreview;

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
    public Vector2I resolved_anchor_coord { get; set; } = new Vector2I(-1, -1);
    public int move_cost { get; set; } = 0;
    public AttackPreviewData hit_preview { get; set; }
    public BattleSpecialProfileGateResult special_profile_gate_result { get; set; }
    public BattleSpecialProfilePreviewFacts special_profile_preview_facts { get; set; }

    internal IReadOnlyList<StringName> TargetUnitIdsTyped => _targetUnitIdsView;
    internal IReadOnlyList<Vector2I> TargetCoordsTyped => _targetCoordsView;
    internal IReadOnlyList<StringName> RandomChainCandidateUnitIdsTyped =>
        _randomChainCandidateUnitIdsView;
    internal IReadOnlyList<string> LogLinesTyped => _logLinesView;
    internal BattleDamagePreviewRangeService.SkillDamagePreview? DamagePreviewTyped =>
        CloneDamagePreview(_damagePreview);
    internal BattleFatePreviewData FatePreviewTyped => _fatePreview ?? hit_preview?.FatePreview;
    internal BattleSaveBranchPreviewData SaveBranchPreviewTyped => _saveBranchPreview;

    public BattlePreview()
    {
        _logLinesView = _logLines.AsReadOnly();
        _targetUnitIdsView = _targetUnitIds.AsReadOnly();
        _targetCoordsView = _targetCoords.AsReadOnly();
        _randomChainCandidateUnitIdsView = _randomChainCandidateUnitIds.AsReadOnly();
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

}
