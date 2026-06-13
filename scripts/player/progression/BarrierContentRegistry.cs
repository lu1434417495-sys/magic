using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BarrierContentRegistry : RefCounted
{
    private const string BARRIER_CONFIG_DIRECTORY = "res://data/configs/barriers";

    private System.Collections.Generic.Dictionary<StringName, BarrierProfileDef> _profile_defs = new();

    private Godot.Collections.Array<string> _validation_errors = new();

    private bool _disposed;

    public BarrierContentRegistry()
    {
        System.GC.SuppressFinalize(this);
        Rebuild();
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        System.GC.SuppressFinalize(this);
        _profile_defs.Clear();
        _validation_errors.Clear();
        base.Dispose();
    }

    public void Rebuild()
    {
        _profile_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(BARRIER_CONFIG_DIRECTORY);
    }

    public BarrierProfileDef GetProfileDef(StringName profileId) =>
        profileId != "" && _profile_defs.TryGetValue(profileId, out var def) ? def : null;

    public Godot.Collections.Array<string> Validate()
    {
        var c = new Godot.Collections.Array<string>();
        foreach (var e in _validation_errors)
            c.Add(e);
        return c;
    }

    private void _scan_directory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validation_errors.Add($"BarrierContentRegistry could not find {directoryPath}.");
            return;
        }

        var dir = DirAccess.Open(directoryPath);

        if (dir == null)
        {
            _validation_errors.Add($"BarrierContentRegistry could not open {directoryPath}.");
            return;
        }

        dir.ListDirBegin();

        while (true)
        {
            string entryName = dir.GetNext();

            if (string.IsNullOrEmpty(entryName))
                break;

            if (entryName == "." || entryName == "..")
                continue;

            string entryPath = $"{directoryPath}/{entryName}";

            if (dir.CurrentIsDir())
            {
                _scan_directory(entryPath);
                continue;
            }

            if (entryName.EndsWith(".tres") || entryName.EndsWith(".res"))
                _register_profile_resource(entryPath);
        }

        dir.ListDirEnd();
    }

    private void _register_profile_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        var profile = resource as BarrierProfileDef;

        if (profile == null)
        {
            _validation_errors.Add($"Barrier profile {resourcePath} must use BarrierProfileDef.");
            return;
        }

        if (profile.profile_id == "")
        {
            _validation_errors.Add($"Barrier profile {resourcePath} must declare profile_id.");
            return;
        }

        if (_profile_defs.ContainsKey(profile.profile_id))
        {
            _validation_errors.Add($"Duplicate barrier profile_id {profile.profile_id}.");
            return;
        }

        _profile_defs[profile.profile_id] = profile;

        _append_profile_validation_errors(profile);
    }

    private void _append_profile_validation_errors(BarrierProfileDef profile)
    {
        var ownerLabel = $"Barrier profile {profile.profile_id}";

        if (profile.AnchorModeKind == BarrierAnchorMode.Unknown)
            _validation_errors.Add(
                $"{ownerLabel} declares unsupported anchor_mode {profile.anchor_mode}."
            );

        if (!IsSupportedBarrierAreaPattern(profile.AreaPatternKind))
            _validation_errors.Add(
                $"{ownerLabel} declares unsupported area_pattern {profile.area_pattern}."
            );

        if (profile.radius_cells < 0)
            _validation_errors.Add($"{ownerLabel}.radius_cells must be >= 0.");

        if (profile.duration_tu < 0)
            _validation_errors.Add($"{ownerLabel}.duration_tu must be >= 0.");

        if (profile.layers.Count == 0)
        {
            _validation_errors.Add($"{ownerLabel} must declare at least one layer.");
            return;
        }

        var seenLayerIds = new Godot.Collections.Dictionary();

        var seenOrders = new Godot.Collections.Dictionary();

        for (int i = 0; i < profile.layers.Count; i++)
        {
            var layer = profile.layers[i];

            var layerLabel = $"{ownerLabel}.layers[{i}]";

            if (layer == null)
            {
                _validation_errors.Add($"{layerLabel} must be a BarrierLayerDef.");
                continue;
            }

            if (layer.layer_id == "")
                _validation_errors.Add($"{layerLabel}.layer_id must be non-empty.");
            else if (seenLayerIds.ContainsKey(layer.layer_id))
                _validation_errors.Add(
                    $"{ownerLabel} declares duplicate layer_id {layer.layer_id}."
                );
            else
                seenLayerIds[layer.layer_id] = true;

            if (seenOrders.ContainsKey(layer.order))
                _validation_errors.Add(
                    $"{ownerLabel} declares duplicate layer order {layer.order}."
                );
            else
                seenOrders[layer.order] = true;

            for (int j = 0; j < layer.passage_outcomes.Count; j++)
            {
                var outcome = layer.passage_outcomes[j];

                var outcomeLabel = $"{layerLabel}.passage_outcomes[{j}]";

                if (outcome == null)
                {
                    _validation_errors.Add($"{outcomeLabel} must be a BarrierOutcomeDef.");
                    continue;
                }

                if (outcome.OutcomeKind == BarrierOutcomeKind.Unknown)
                    _validation_errors.Add(
                        $"{outcomeLabel} declares unsupported outcome_type {outcome.outcome_type}."
                    );
            }
        }
    }

    private static bool IsSupportedBarrierAreaPattern(BattleAreaPattern areaPattern)
    {
        return areaPattern
            is BattleAreaPattern.Single
                or BattleAreaPattern.Diamond
                or BattleAreaPattern.Square
                or BattleAreaPattern.Radius
                or BattleAreaPattern.Cross;
    }
}
