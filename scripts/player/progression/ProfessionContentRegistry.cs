using Godot;

[GlobalClass]
public partial class ProfessionContentRegistry : RefCounted
{
    private const string PROFESSION_CONFIG_DIRECTORY = "res://data/configs/professions";
    private Godot.Collections.Dictionary _profession_defs = new();
    private Godot.Collections.Array<string> _validation_errors = new();

    public ProfessionContentRegistry() { rebuild(); }
    public void rebuild() { _profession_defs.Clear(); _validation_errors.Clear(); _scan_directory(PROFESSION_CONFIG_DIRECTORY); }
    public Godot.Collections.Dictionary get_profession_defs() => _profession_defs.Duplicate();
    public Godot.Collections.Array<string> validate() { var c = new Godot.Collections.Array<string>(); foreach (var e in _validation_errors) c.Add(e); return c; }

    private void _scan_directory(string directoryPath) { if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath))) { _validation_errors.Add($"ProfessionContentRegistry could not find {directoryPath}."); return; } var dir = DirAccess.Open(directoryPath); if (dir == null) { _validation_errors.Add($"ProfessionContentRegistry could not open {directoryPath}."); return; } dir.ListDirBegin(); while (true) { string n = dir.GetNext(); if (string.IsNullOrEmpty(n)) break; if (n == "." || n == "..") continue; string p = $"{directoryPath}/{n}"; if (dir.CurrentIsDir()) _scan_directory(p); else if (n.EndsWith(".tres") || n.EndsWith(".res")) _register_profession_resource(p); } dir.ListDirEnd(); }

    private void _register_profession_resource(string resourcePath) { var resource = GD.Load<Resource>(resourcePath); if (resource == null) { _validation_errors.Add($"Failed to load profession config {resourcePath}."); return; } var pd = resource as ProfessionDef; if (pd == null) { _validation_errors.Add($"Profession config {resourcePath} is not a ProfessionDef."); return; } if (pd.profession_id == "") { _validation_errors.Add($"Profession config {resourcePath} is missing profession_id."); return; } if (_profession_defs.ContainsKey(pd.profession_id)) { _validation_errors.Add($"Duplicate profession_id registered: {pd.profession_id}"); return; } _profession_defs[pd.profession_id] = pd; }
}
