using Godot;

[GlobalClass]
public partial class SkillContentRegistry : RefCounted
{
    private const string SKILL_CONFIG_DIRECTORY = "res://data/configs/skills";
    private Godot.Collections.Dictionary _skill_defs = new();
    private Godot.Collections.Array<string> _validation_errors = new();

    public SkillContentRegistry() { rebuild(); }
    public void rebuild() { _skill_defs.Clear(); _validation_errors.Clear(); _scan_directory(SKILL_CONFIG_DIRECTORY); }
    public Godot.Collections.Dictionary get_skill_defs() => _skill_defs.Duplicate();
    public Godot.Collections.Array<string> validate() { var c = new Godot.Collections.Array<string>(); foreach (var e in _validation_errors) c.Add(e); return c; }

    private void _scan_directory(string directoryPath) { if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath))) { _validation_errors.Add($"SkillContentRegistry could not find {directoryPath}."); return; } var dir = DirAccess.Open(directoryPath); if (dir == null) { _validation_errors.Add($"SkillContentRegistry could not open {directoryPath}."); return; } dir.ListDirBegin(); while (true) { string n = dir.GetNext(); if (string.IsNullOrEmpty(n)) break; if (n == "." || n == "..") continue; string p = $"{directoryPath}/{n}"; if (dir.CurrentIsDir()) _scan_directory(p); else if (n.EndsWith(".tres") || n.EndsWith(".res")) _register_skill_resource(p); } dir.ListDirEnd(); }
    private void _register_skill_resource(string resourcePath) { var resource = GD.Load<Resource>(resourcePath); if (resource == null) { _validation_errors.Add($"Failed to load skill config {resourcePath}."); return; } var sd = resource as SkillDef; if (sd == null) { _validation_errors.Add($"Skill config {resourcePath} is not a SkillDef."); return; } if (sd.skill_id == "") { _validation_errors.Add($"Skill config {resourcePath} is missing skill_id."); return; } if (_skill_defs.ContainsKey(sd.skill_id)) { _validation_errors.Add($"Duplicate skill_id registered: {sd.skill_id}"); return; } _skill_defs[sd.skill_id] = sd; }
}
