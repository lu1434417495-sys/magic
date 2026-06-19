using Godot;
using System.Collections;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattlePreview : RefCounted
{
    private readonly List<string> _logLines = new();
    private readonly List<StringName> _targetUnitIds = new();
    private readonly List<Vector2I> _targetCoords = new();
    private readonly List<StringName> _randomChainCandidateUnitIds = new();
    private GDictionary _saveBranchPreview = new();
    private BattleDamagePreviewRangeService.SkillDamagePreview? _damagePreview;
    private BattleFatePreviewData _fatePreview;

    public bool allowed { get; set; } = false;
    public GArray log_lines
    {
        get => BuildLogLinesArray();
        set => SetLogLines(value);
    }
    public Godot.Collections.Array<StringName> target_unit_ids
    {
        get => new Godot.Collections.Array<StringName>(_targetUnitIds);
        set => SetTargetUnitIds(value);
    }
    public Godot.Collections.Array<Vector2I> target_coords
    {
        get => new Godot.Collections.Array<Vector2I>(_targetCoords);
        set => SetTargetCoords(value);
    }
    public Godot.Collections.Array<StringName> random_chain_candidate_unit_ids
    {
        get => new Godot.Collections.Array<StringName>(_randomChainCandidateUnitIds);
        set => SetRandomChainCandidateUnitIds(value);
    }
    public Vector2I resolved_anchor_coord { get; set; } = new Vector2I(-1, -1);
    public int move_cost { get; set; } = 0;
    public AttackPreviewData hit_preview { get; set; }
    public GDictionary damage_preview
    {
        get => BattleDamagePreviewRangeProjection.Project(_damagePreview);
        set => SetDamagePreview(DecodeDamagePreview(value));
    }
    public GDictionary fate_preview
    {
        get => (_fatePreview ?? hit_preview?.FatePreview)?.ToDictionary() ?? new GDictionary();
        set => SetFatePreview(BattleFatePreviewData.FromDictionary(value));
    }
    public GDictionary save_branch_preview
    {
        get => _saveBranchPreview.Duplicate(true);
        set => _saveBranchPreview = value?.Duplicate(true) ?? new GDictionary();
    }
    public BattleSpecialProfileGateResult special_profile_gate_result { get; set; }
    public BattleSpecialProfilePreviewFacts special_profile_preview_facts { get; set; }

    internal IReadOnlyList<StringName> TargetUnitIdsTyped => _targetUnitIds;
    internal IReadOnlyList<Vector2I> TargetCoordsTyped => _targetCoords;
    internal IReadOnlyList<StringName> RandomChainCandidateUnitIdsTyped =>
        _randomChainCandidateUnitIds;
    internal IReadOnlyList<string> LogLinesTyped => _logLines;
    internal BattleDamagePreviewRangeService.SkillDamagePreview? DamagePreviewTyped =>
        _damagePreview;
    internal BattleFatePreviewData FatePreviewTyped => _fatePreview ?? hit_preview?.FatePreview;
    internal GDictionary SaveBranchPreviewTyped => _saveBranchPreview;

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

    private GArray BuildLogLinesArray()
    {
        var result = new GArray();
        foreach (string value in _logLines)
        {
            result.Add(value);
        }
        return result;
    }

    internal void SetDamagePreview(BattleDamagePreviewRangeService.SkillDamagePreview? value)
    {
        _damagePreview = value;
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

    internal void SetSaveBranchPreview(GDictionary value)
    {
        _saveBranchPreview = value?.Duplicate(true) ?? new GDictionary();
    }

    internal void ClearSaveBranchPreview()
    {
        _saveBranchPreview.Clear();
    }

    private static BattleDamagePreviewRangeService.SkillDamagePreview? DecodeDamagePreview(
        GDictionary value
    )
    {
        if (value == null || value.Count == 0)
        {
            return null;
        }

        return new BattleDamagePreviewRangeService.SkillDamagePreview(
            ReadBool(value, "has_damage"),
            ReadInt(value, "min_damage"),
            ReadInt(value, "max_damage"),
            new List<BattleDamagePreviewRangeService.DamageEffectRange>()
        );
    }

    private static bool ReadBool(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return false;
        return source[key].AsBool();
    }

    private static int ReadInt(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return 0;
        return source[key].AsInt32();
    }
}
