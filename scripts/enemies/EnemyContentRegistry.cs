using Godot;

[GlobalClass]
public partial class EnemyContentRegistry : RefCounted
{
    private const string ENEMY_CONTENT_SEED_RESOURCE_PATH =
        "res://data/configs/enemies/enemy_content_seed.tres";
    private const string ENEMY_BRAIN_CONFIG_DIRECTORY = "res://data/configs/enemies/brains";
    private const string ENEMY_TEMPLATE_CONFIG_DIRECTORY = "res://data/configs/enemies/templates";
    private const string WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY =
        "res://data/configs/enemies/rosters";

    private Godot.Collections.Dictionary _enemy_templates = new(),
        _enemy_ai_brains = new(),
        _wild_encounter_rosters = new();
    private Godot.Collections.Array<string> _validation_errors = new();
    private string _enemy_content_seed_resource_path = ENEMY_CONTENT_SEED_RESOURCE_PATH;
    private string _enemy_template_directory = ENEMY_TEMPLATE_CONFIG_DIRECTORY;
    private string _enemy_ai_brain_directory = ENEMY_BRAIN_CONFIG_DIRECTORY;
    private string _wild_encounter_roster_directory = WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY;
    private bool _validate_seed_directory_completeness = true;
    private Godot.Collections.Dictionary _seed_enemy_ai_brain_paths = new(),
        _seed_enemy_template_paths = new(),
        _seed_wild_encounter_roster_paths = new();

    public EnemyContentRegistry()
    {
        System.GC.SuppressFinalize(this);
        rebuild();
    }

    public void configure_seed_resource(
        string seedResourcePath = ENEMY_CONTENT_SEED_RESOURCE_PATH,
        bool rebuildNow = true,
        bool validateSeedDirCompleteness = false
    )
    {
        _enemy_content_seed_resource_path = seedResourcePath;
        _validate_seed_directory_completeness =
            validateSeedDirCompleteness || seedResourcePath == ENEMY_CONTENT_SEED_RESOURCE_PATH;
        if (rebuildNow)
            rebuild();
    }

    public void configure_directories(
        string templateDir = ENEMY_TEMPLATE_CONFIG_DIRECTORY,
        string brainDir = ENEMY_BRAIN_CONFIG_DIRECTORY,
        string rosterDir = WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY,
        bool rebuildNow = true
    )
    {
        _enemy_content_seed_resource_path = "";
        _enemy_template_directory = templateDir;
        _enemy_ai_brain_directory = brainDir;
        _wild_encounter_roster_directory = rosterDir;
        _validate_seed_directory_completeness = false;
        if (rebuildNow)
            rebuild();
    }

    public void rebuild()
    {
        _enemy_templates.Clear();
        _enemy_ai_brains.Clear();
        _wild_encounter_rosters.Clear();
        _validation_errors.Clear();
        _seed_enemy_ai_brain_paths.Clear();
        _seed_enemy_template_paths.Clear();
        _seed_wild_encounter_roster_paths.Clear();
        if (_enemy_content_seed_resource_path.Length > 0)
        {
            _register_seed_resource(_enemy_content_seed_resource_path);
            if (_validate_seed_directory_completeness)
                foreach (var e in _collect_seed_directory_completeness_errors())
                    _validation_errors.Add(e);
        }
        else
        {
            _scan_directory(
                _enemy_ai_brain_directory,
                (p) => _register_brain_resource(p),
                "EnemyContentRegistry brain scan"
            );
            _scan_directory(
                _enemy_template_directory,
                (p) => _register_template_resource(p),
                "EnemyContentRegistry template scan"
            );
            _scan_directory(
                _wild_encounter_roster_directory,
                (p) => _register_wild_encounter_roster_resource(p),
                "EnemyContentRegistry roster scan"
            );
        }
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public Godot.Collections.Dictionary get_enemy_templates() => _enemy_templates.Duplicate();

    public Godot.Collections.Dictionary get_enemy_ai_brains() => _enemy_ai_brains.Duplicate();

    public Godot.Collections.Dictionary get_wild_encounter_rosters() =>
        _wild_encounter_rosters.Duplicate();

    public Godot.Collections.Array<string> validate() => _validation_errors.Duplicate();

    private void _register_seed_resource(string resourcePath)
    {
        var r = GodotContentResourceLifetime.Keep(GD.Load<Resource>(resourcePath));
        if (r == null)
        {
            _validation_errors.Add($"Failed to load enemy content seed {resourcePath}.");
            return;
        }
        if (r is not EnemyContentSeed seed)
        {
            _validation_errors.Add(
                $"Enemy content seed {resourcePath} is not an EnemyContentSeed."
            );
            return;
        }
        foreach (var b in seed.enemy_ai_brains)
        {
            GodotContentResourceLifetime.Keep(b);
            _remember_seed_resource_path(_seed_enemy_ai_brain_paths, b);
            _register_brain_entry(b, $"{resourcePath}::enemy_ai_brains");
        }
        foreach (var t in seed.enemy_templates)
        {
            GodotContentResourceLifetime.Keep(t);
            _remember_seed_resource_path(_seed_enemy_template_paths, t);
            _register_template_entry(t, $"{resourcePath}::enemy_templates");
        }
        foreach (var w in seed.wild_encounter_rosters)
        {
            GodotContentResourceLifetime.Keep(w);
            _remember_seed_resource_path(_seed_wild_encounter_roster_paths, w);
            _register_wild_encounter_roster_entry(w, $"{resourcePath}::wild_encounter_rosters");
        }
    }

    private static void _remember_seed_resource_path(
        Godot.Collections.Dictionary seedPaths,
        Resource r
    )
    {
        if (r == null)
            return;
        string rp = (r.ResourcePath ?? "").Replace("\\", "/");
        if (rp.Length > 0)
            seedPaths[rp] = true;
    }

    private Godot.Collections.Array<string> _collect_seed_directory_completeness_errors()
    {
        var e = new Godot.Collections.Array<string>();
        _append_seed_dir_errors(
            e,
            _enemy_ai_brain_directory,
            _seed_enemy_ai_brain_paths,
            "enemy_ai_brains"
        );
        _append_seed_dir_errors(
            e,
            _enemy_template_directory,
            _seed_enemy_template_paths,
            "enemy_templates"
        );
        _append_seed_dir_errors(
            e,
            _wild_encounter_roster_directory,
            _seed_wild_encounter_roster_paths,
            "wild_encounter_rosters"
        );
        return e;
    }

    private void _append_seed_dir_errors(
        Godot.Collections.Array<string> errors,
        string dirPath,
        Godot.Collections.Dictionary seedPaths,
        string seedColName
    )
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(dirPath)))
        {
            errors.Add($"Enemy content seed completeness could not find {dirPath}.");
            return;
        }
        foreach (var rp in _collect_resource_paths_in_directory(dirPath))
        {
            if (!seedPaths.ContainsKey(rp))
                errors.Add(
                    $"Enemy content seed {_enemy_content_seed_resource_path} is missing {seedColName} entry for {rp}."
                );
        }
    }

    private static Godot.Collections.Array<string> _collect_resource_paths_in_directory(
        string dirPath
    )
    {
        var r = new Godot.Collections.Array<string>();
        var dir = DirAccess.Open(dirPath);
        if (dir == null)
            return r;
        dir.ListDirBegin();
        while (true)
        {
            string n = dir.GetNext();
            if (string.IsNullOrEmpty(n))
                break;
            if (n == "." || n == "..")
                continue;
            string ep = $"{dirPath}/{n}";
            if (dir.CurrentIsDir())
            {
                foreach (var cr in _collect_resource_paths_in_directory(ep))
                    r.Add(cr);
                continue;
            }
            if (n.EndsWith(".tres") || n.EndsWith(".res"))
                r.Add(ep.Replace("\\", "/"));
        }
        dir.ListDirEnd();
        r.Sort();
        return r;
    }

    private void _scan_directory(
        string dirPath,
        System.Action<string> registerCallback,
        string scanLabel
    )
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(dirPath)))
        {
            _validation_errors.Add($"{scanLabel} could not find {dirPath}.");
            return;
        }
        var dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            _validation_errors.Add($"{scanLabel} could not open {dirPath}.");
            return;
        }
        dir.ListDirBegin();
        while (true)
        {
            string n = dir.GetNext();
            if (string.IsNullOrEmpty(n))
                break;
            if (n == "." || n == "..")
                continue;
            string ep = $"{dirPath}/{n}";
            if (dir.CurrentIsDir())
                _scan_directory(ep, registerCallback, scanLabel);
            else if (n.EndsWith(".tres") || n.EndsWith(".res"))
                registerCallback(ep);
        }
        dir.ListDirEnd();
    }

    private void _register_brain_resource(string rp)
    {
        var r = GodotContentResourceLifetime.Keep(GD.Load<Resource>(rp));
        _register_brain_entry(r, rp);
    }

    private void _register_template_resource(string rp)
    {
        var r = GodotContentResourceLifetime.Keep(GD.Load<Resource>(rp));
        _register_template_entry(r, rp);
    }

    private void _register_wild_encounter_roster_resource(string rp)
    {
        var r = GodotContentResourceLifetime.Keep(GD.Load<Resource>(rp));
        _register_wild_encounter_roster_entry(r, rp);
    }

    private void _register_brain_entry(Resource r, string sourceLabel)
    {
        if (r == null)
        {
            _validation_errors.Add($"Failed to load enemy brain config {sourceLabel}.");
            return;
        }
        if (r is not EnemyAiBrainDef brain || brain.brain_id == "")
        {
            _validation_errors.Add($"Enemy brain config {sourceLabel} is not an EnemyAiBrainDef.");
            return;
        }
        if (_enemy_ai_brains.ContainsKey(brain.brain_id))
        {
            _validation_errors.Add($"Duplicate enemy brain_id registered: {brain.brain_id}");
            return;
        }
        _enemy_ai_brains[brain.brain_id] = brain;
    }

    private void _register_template_entry(Resource r, string sourceLabel)
    {
        if (r == null)
        {
            _validation_errors.Add($"Failed to load enemy template config {sourceLabel}.");
            return;
        }
        if (r is not EnemyTemplateDef tmpl)
        {
            _validation_errors.Add(
                $"Enemy template config {sourceLabel} is not an EnemyTemplateDef."
            );
            return;
        }
        if (tmpl.template_id == "")
        {
            foreach (
                var error in tmpl.validate_schema(
                    _enemy_ai_brains,
                    _get_item_defs_for_validation(),
                    _get_skill_defs_for_validation()
                )
            )
                _validation_errors.Add(error);
            return;
        }
        if (_enemy_templates.ContainsKey(tmpl.template_id))
        {
            _validation_errors.Add($"Duplicate enemy template_id registered: {tmpl.template_id}");
            return;
        }
        _enemy_templates[tmpl.template_id] = tmpl;
    }

    private void _register_wild_encounter_roster_entry(Resource r, string sourceLabel)
    {
        if (r == null)
        {
            _validation_errors.Add($"Failed to load wild encounter roster config {sourceLabel}.");
            return;
        }
        if (r is not WildEncounterRosterDef roster || roster.profile_id == "")
        {
            _validation_errors.Add(
                $"Wild encounter roster config {sourceLabel} is not a WildEncounterRosterDef."
            );
            return;
        }
        if (_wild_encounter_rosters.ContainsKey(roster.profile_id))
        {
            _validation_errors.Add(
                $"Duplicate wild encounter profile_id registered: {roster.profile_id}"
            );
            return;
        }
        _wild_encounter_rosters[roster.profile_id] = roster;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var e = new Godot.Collections.Array<string>();
        var sd = _get_skill_defs_for_validation();
        foreach (var bk in ProgressionDataUtils.sorted_string_keys(_enemy_ai_brains))
        {
            var b = _enemy_ai_brains[new StringName(bk)].AsGodotObject() as EnemyAiBrainDef;
            if (b != null)
                foreach (var ve in b.validate_schema(sd))
                    e.Add(ve);
        }
        var id = _get_item_defs_for_validation();
        foreach (var tk in ProgressionDataUtils.sorted_string_keys(_enemy_templates))
        {
            var t = _enemy_templates[new StringName(tk)].AsGodotObject() as EnemyTemplateDef;
            if (t != null)
                foreach (var ve in t.validate_schema(_enemy_ai_brains, id, sd))
                    e.Add(ve);
        }
        foreach (var rk in ProgressionDataUtils.sorted_string_keys(_wild_encounter_rosters))
        {
            var w =
                _wild_encounter_rosters[new StringName(rk)].AsGodotObject()
                as WildEncounterRosterDef;
            if (w != null)
                foreach (var ve in w.validate_schema(_enemy_templates))
                    e.Add(ve);
        }
        return e;
    }

    private static Godot.Collections.Dictionary _get_item_defs_for_validation()
    {
        using var ir = new ItemContentRegistry();
        return ir.get_item_defs();
    }

    private static Godot.Collections.Dictionary _get_skill_defs_for_validation()
    {
        using var sr = new SkillContentRegistry();
        return sr.get_skill_defs();
    }
}
