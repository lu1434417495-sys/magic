using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed record EnemyContentDefinitionGraph(
    IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates,
    IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyBrains,
    IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> EncounterRosters
);

internal sealed class EnemyContentValidationContext
{
    internal EnemyContentValidationContext(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        ItemDefinitions = itemDefinitions
            ?? throw new System.ArgumentNullException(nameof(itemDefinitions));
        SkillDefinitions = skillDefinitions
            ?? throw new System.ArgumentNullException(nameof(skillDefinitions));
    }

    internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefinitions { get; }
    internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions { get; }
}

public class EnemyContentRegistry : IValidatableRegistry, System.IDisposable
{
    private const string ENEMY_CONTENT_SEED_RESOURCE_PATH =
        "res://data/configs/enemies/enemy_content_seed.tres";
    private const string ENEMY_BRAIN_CONFIG_DIRECTORY = "res://data/configs/enemies/brains";
    private const string ENEMY_TEMPLATE_CONFIG_DIRECTORY = "res://data/configs/enemies/templates";
    private const string WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY =
        "res://data/configs/enemies/rosters";

    private readonly IContentResourceLoader _loader;
    private readonly Dictionary<StringName, EnemyTemplateDef> _enemy_templates = new();
    private readonly Dictionary<StringName, EnemyAiBrainDef> _enemy_ai_brains = new();
    private readonly Dictionary<StringName, WildEncounterRosterDef> _wild_encounter_rosters = new();
    private readonly List<string> _validation_errors = new();
    private string _enemy_content_seed_resource_path = ENEMY_CONTENT_SEED_RESOURCE_PATH;
    private string _enemy_template_directory = ENEMY_TEMPLATE_CONFIG_DIRECTORY;
    private string _enemy_ai_brain_directory = ENEMY_BRAIN_CONFIG_DIRECTORY;
    private string _wild_encounter_roster_directory = WILD_ENCOUNTER_ROSTER_CONFIG_DIRECTORY;
    private bool _validate_seed_directory_completeness = true;
    private readonly HashSet<string> _seed_enemy_ai_brain_paths = new(
        System.StringComparer.Ordinal
    );
    private readonly HashSet<string> _seed_enemy_template_paths = new(
        System.StringComparer.Ordinal
    );
    private readonly HashSet<string> _seed_wild_encounter_roster_paths = new(
        System.StringComparer.Ordinal
    );
    private bool _disposed;

    internal EnemyContentRegistry(IContentResourceLoader loader)
        : this(loader, loadDefaultContent: true) { }

    internal EnemyContentRegistry(IContentResourceLoader loader, bool loadDefaultContent)
    {
        _loader = loader ?? throw new System.ArgumentNullException(nameof(loader));
        if (loadDefaultContent)
            Rebuild();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _enemy_templates.Clear();
        _enemy_ai_brains.Clear();
        _wild_encounter_rosters.Clear();
        _validation_errors.Clear();
        _seed_enemy_ai_brain_paths.Clear();
        _seed_enemy_template_paths.Clear();
        _seed_wild_encounter_roster_paths.Clear();
    }

    public void ConfigureSeedResource(
        string seedResourcePath = ENEMY_CONTENT_SEED_RESOURCE_PATH,
        bool rebuildNow = true,
        bool validateSeedDirCompleteness = false
    )
    {
        _enemy_content_seed_resource_path = seedResourcePath;
        _validate_seed_directory_completeness =
            validateSeedDirCompleteness || seedResourcePath == ENEMY_CONTENT_SEED_RESOURCE_PATH;
        if (rebuildNow)
            Rebuild();
    }

    public void ConfigureDirectories(
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
            Rebuild();
    }

    public void Rebuild()
    {
        RebuildCore(validationContext: null);
    }

    internal void Rebuild(EnemyContentValidationContext validationContext)
    {
        System.ArgumentNullException.ThrowIfNull(validationContext);
        RebuildCore(validationContext);
    }

    private void RebuildCore(EnemyContentValidationContext validationContext)
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
            _register_seed_resource(_enemy_content_seed_resource_path, validationContext);
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
                (p) => _register_template_resource(p, validationContext),
                "EnemyContentRegistry template scan"
            );
            _scan_directory(
                _wild_encounter_roster_directory,
                (p) => _register_wild_encounter_roster_resource(p),
                "EnemyContentRegistry roster scan"
            );
        }
        foreach (var e in _collect_validation_errors(validationContext))
            _validation_errors.Add(e);
    }

    public Godot.Collections.Array<string> Validate() => ToGodotStringArray(_validation_errors);

    internal IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplatesTyped() =>
        _enemy_templates;

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainsTyped() =>
        _enemy_ai_brains;

    internal IReadOnlyDictionary<StringName, WildEncounterRosterDef> GetWildEncounterRostersTyped()
        => _wild_encounter_rosters;

    internal EnemyContentDefinitionGraph ProjectDefinitions(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (_validation_errors.Count != 0)
        {
            throw new System.IO.InvalidDataException(
                "Enemy content must validate before immutable definition projection: "
                    + string.Join(" | ", _validation_errors)
            );
        }

        var brains = new Dictionary<StringName, EnemyAiBrainDefinition>();
        foreach (StringName brainId in SortedKeys(_enemy_ai_brains.Keys))
        {
            EnemyAiBrainDef source = _enemy_ai_brains[brainId];
            if (!brains.TryAdd(brainId, source.ToDefinition()))
                throw new System.IO.InvalidDataException($"Duplicate enemy brain_id projected: {brainId}");
        }

        var templates = new Dictionary<StringName, EnemyTemplateDefinition>();
        foreach (StringName templateId in SortedKeys(_enemy_templates.Keys))
        {
            EnemyTemplateDef source = _enemy_templates[templateId];
            if (!templates.TryAdd(templateId, source.ToDefinition(itemDefinitions)))
                throw new System.IO.InvalidDataException(
                    $"Duplicate enemy template_id projected: {templateId}"
                );
        }

        var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>();
        foreach (StringName rosterId in SortedKeys(_wild_encounter_rosters.Keys))
        {
            WildEncounterRosterDef source = _wild_encounter_rosters[rosterId];
            if (!rosters.TryAdd(rosterId, source.ToDefinition()))
                throw new System.IO.InvalidDataException(
                    $"Duplicate encounter roster profile_id projected: {rosterId}"
                );
        }

        return new EnemyContentDefinitionGraph(
            new ReadOnlyDictionary<StringName, EnemyTemplateDefinition>(templates),
            new ReadOnlyDictionary<StringName, EnemyAiBrainDefinition>(brains),
            new ReadOnlyDictionary<StringName, WildEncounterRosterDefinition>(rosters)
        );
    }

    public IReadOnlyList<string> ValidateTyped() => _validation_errors;

    private void _register_seed_resource(
        string resourcePath,
        EnemyContentValidationContext validationContext
    )
    {
        var r = _loader.LoadCanonical<Resource>(resourcePath);
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
            _remember_seed_resource_path(_seed_enemy_ai_brain_paths, b);
            _register_brain_entry(b, $"{resourcePath}::enemy_ai_brains");
        }
        foreach (var t in seed.enemy_templates)
        {
            _remember_seed_resource_path(_seed_enemy_template_paths, t);
            _register_template_entry(
                t,
                $"{resourcePath}::enemy_templates",
                validationContext
            );
        }
        foreach (var w in seed.wild_encounter_rosters)
        {
            _remember_seed_resource_path(_seed_wild_encounter_roster_paths, w);
            _register_wild_encounter_roster_entry(w, $"{resourcePath}::wild_encounter_rosters");
        }
    }

    private static void _remember_seed_resource_path(
        HashSet<string> seedPaths,
        Resource r
    )
    {
        if (r == null)
            return;
        string rp = (r.ResourcePath ?? "").Replace("\\", "/");
        if (rp.Length > 0)
            seedPaths.Add(rp);
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
        HashSet<string> seedPaths,
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
            if (!seedPaths.Contains(rp))
                errors.Add(
                    $"Enemy content seed {_enemy_content_seed_resource_path} is missing {seedColName} entry for {rp}."
                );
        }
    }

    private static List<string> _collect_resource_paths_in_directory(string dirPath)
    {
        var r = new List<string>();
        DirAccess dir = DirAccess.Open(dirPath);
        if (dir == null)
            return r;
        try
        {
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
                    {
                        r.Add(cr);
                    }
                    continue;
                }
                if (n.EndsWith(".tres") || n.EndsWith(".res"))
                    r.Add(ep.Replace("\\", "/"));
            }
            dir.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(dir);
        }
        r.Sort(System.StringComparer.Ordinal);
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
        DirAccess dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            _validation_errors.Add($"{scanLabel} could not open {dirPath}.");
            return;
        }
        try
        {
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
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(dir);
        }
    }

    private void _register_brain_resource(string rp)
    {
        var r = _loader.LoadCanonical<Resource>(rp);
        _register_brain_entry(r, rp);
    }

    private void _register_template_resource(
        string rp,
        EnemyContentValidationContext validationContext
    )
    {
        var r = _loader.LoadCanonical<Resource>(rp);
        _register_template_entry(r, rp, validationContext);
    }

    private void _register_wild_encounter_roster_resource(string rp)
    {
        var r = _loader.LoadCanonical<Resource>(rp);
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

    private void _register_template_entry(
        Resource r,
        string sourceLabel,
        EnemyContentValidationContext validationContext
    )
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
            var knownBrains = new Dictionary<StringName, EnemyAiBrainDef>(_enemy_ai_brains);
            IReadOnlyDictionary<StringName, ItemDefinition> itemDefs =
                ResolveItemDefinitions(validationContext);
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                ResolveSkillDefinitions(validationContext);
            foreach (
                var error in tmpl.ValidateSchemaTyped(knownBrains, itemDefs, skillDefinitions)
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

    private Godot.Collections.Array<string> _collect_validation_errors(
        EnemyContentValidationContext validationContext
    )
    {
        var e = new Godot.Collections.Array<string>();
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitionIndex =
            ResolveSkillDefinitions(validationContext);
        foreach (StringName brainId in SortedKeys(_enemy_ai_brains.Keys))
        {
            if (_enemy_ai_brains.TryGetValue(brainId, out EnemyAiBrainDef brain) && brain != null)
            {
                foreach (var ve in brain.ValidateSchema(skillDefinitionIndex))
                {
                    e.Add(ve);
                }
            }
        }
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefIndex =
            ResolveItemDefinitions(validationContext);
        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>(_enemy_ai_brains);
        foreach (StringName templateId in SortedKeys(_enemy_templates.Keys))
        {
            if (
                _enemy_templates.TryGetValue(templateId, out EnemyTemplateDef template)
                && template != null
            )
            {
                foreach (
                    var ve in template.ValidateSchemaTyped(
                        brainIndex,
                        itemDefIndex,
                        skillDefinitionIndex
                    )
                )
                {
                    e.Add(ve);
                }
            }
        }
        var knownTemplateIds = new HashSet<StringName>(_enemy_templates.Keys);
        foreach (StringName rosterId in SortedKeys(_wild_encounter_rosters.Keys))
        {
            if (
                _wild_encounter_rosters.TryGetValue(rosterId, out WildEncounterRosterDef roster)
                && roster != null
            )
            {
                foreach (var ve in roster.ValidateSchemaTyped(knownTemplateIds))
                {
                    e.Add(ve);
                }
            }
        }
        return e;
    }

    private IReadOnlyDictionary<StringName, ItemDefinition> ResolveItemDefinitions(
        EnemyContentValidationContext validationContext
    ) =>
        validationContext?.ItemDefinitions ?? _get_item_defs_for_validation_typed();

    private IReadOnlyDictionary<StringName, SkillDefinition> ResolveSkillDefinitions(
        EnemyContentValidationContext validationContext
    ) =>
        validationContext?.SkillDefinitions
        ?? _get_skill_definitions_for_validation_typed();

    private IReadOnlyDictionary<StringName, ItemDefinition> _get_item_defs_for_validation_typed()
    {
        using var ir = new ItemContentRegistry(_loader);
        return new Dictionary<StringName, ItemDefinition>(ir.GetItemDefsTyped());
    }

    private IReadOnlyDictionary<StringName, SkillDefinition> _get_skill_definitions_for_validation_typed()
    {
        using var sr = new SkillContentRegistry(_loader);
        return EnemyTemplateDef.CloneSkillDefinitionIndex(sr.GetSkillDefinitionsTyped());
    }

    private static Godot.Collections.Array<string> ToGodotStringArray(IEnumerable<string> values)
    {
        var result = new Godot.Collections.Array<string>();
        if (values == null)
        {
            return result;
        }
        foreach (string value in values)
        {
            result.Add(value ?? "");
        }
        return result;
    }

    private static IEnumerable<StringName> SortedKeys(IEnumerable<StringName> keys)
    {
        if (keys == null)
        {
            yield break;
        }
        var sorted = new List<string>();
        foreach (StringName key in keys)
        {
            if (key != "")
            {
                sorted.Add(key.ToString());
            }
        }
        sorted.Sort(System.StringComparer.Ordinal);
        foreach (string key in sorted)
        {
            yield return new StringName(key);
        }
    }
}
