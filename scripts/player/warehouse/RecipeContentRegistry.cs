using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class RecipeContentRegistry : RefCounted, IValidatableRegistry
{
    private const string RECIPE_CONFIG_DIRECTORY = "res://data/configs/recipes";

    private const int RECIPE_QUANTITY_MAX = 9999;

    private readonly Dictionary<StringName, RecipeDef> _recipe_defs = new();

    private readonly List<string> _validation_errors = new();

    private Dictionary<StringName, ItemDef> _item_defs = new();
    private bool _disposed;

    public RecipeContentRegistry()
    {
        System.GC.SuppressFinalize(this);
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeManagedRegistry();
        }
        base.Dispose(disposing);
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        System.GC.SuppressFinalize(this);
        _recipe_defs.Clear();
        _validation_errors.Clear();
        _item_defs.Clear();
    }

    internal void Setup(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        _item_defs = itemDefs != null
            ? new Dictionary<StringName, ItemDef>(itemDefs)
            : new Dictionary<StringName, ItemDef>();
        Rebuild();
    }

    public void Rebuild()
    {
        LoadFromDirectory(RECIPE_CONFIG_DIRECTORY);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        _recipe_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
    }

    public Godot.Collections.Array<string> Validate()
    {
        var c = new Godot.Collections.Array<string>();
        foreach (var e in _validation_errors)
            c.Add(e);
        return c;
    }

    internal IReadOnlyDictionary<StringName, RecipeDef> GetRecipeDefsTyped() => _recipe_defs;

    public IReadOnlyList<string> ValidateTyped() => _validation_errors;

    private void _scan_directory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validation_errors.Add($"RecipeContentRegistry could not find {directoryPath}.");
            return;
        }

        var dir = DirAccess.Open(directoryPath);

        if (dir == null)
        {
            _validation_errors.Add($"RecipeContentRegistry could not open {directoryPath}.");
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
            string p = $"{directoryPath}/{n}";
            if (dir.CurrentIsDir())
                _scan_directory(p);
            else if (n.EndsWith(".tres") || n.EndsWith(".res"))
                _register_recipe_resource(p);
        }

        dir.ListDirEnd();
    }

    private void _register_recipe_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load recipe config {resourcePath}.");
            return;
        }

        if (resource is not RecipeDef rd)
        {
            _validation_errors.Add($"Recipe config {resourcePath} is not a RecipeDef.");
            return;
        }

        if (rd.recipe_id == "")
        {
            _validation_errors.Add($"Recipe config {resourcePath} is missing recipe_id.");
            return;
        }

        if (_recipe_defs.ContainsKey(rd.recipe_id))
        {
            _validation_errors.Add($"Duplicate recipe_id registered: {rd.recipe_id}");
            return;
        }

        if (rd.input_item_ids.Count == 0)
        {
            _validation_errors.Add($"Recipe {rd.recipe_id} must declare at least one input item.");
            return;
        }

        if (rd.input_item_ids.Count != rd.input_item_quantities.Length)
        {
            _validation_errors.Add(
                $"Recipe {rd.recipe_id} input_item_ids size must match input_item_quantities size."
            );
            return;
        }

        if (rd.output_item_id == "")
        {
            _validation_errors.Add($"Recipe {rd.recipe_id} is missing output_item_id.");
            return;
        }

        if (rd.output_quantity <= 0)
        {
            _validation_errors.Add($"Recipe {rd.recipe_id} must have output_quantity >= 1.");
            return;
        }

        if (rd.output_quantity > RECIPE_QUANTITY_MAX)
        {
            _validation_errors.Add(
                $"Recipe {rd.recipe_id} output_quantity {rd.output_quantity} exceeds max {RECIPE_QUANTITY_MAX}."
            );
            return;
        }

        if (rd.required_facility_tags.Count == 0)
        {
            _validation_errors.Add(
                $"Recipe {rd.recipe_id} must declare at least one required_facility_tag."
            );
            return;
        }

        var facilityTagSet = new HashSet<StringName>();

        foreach (var rawTag in rd.required_facility_tags)
        {
            var ft = ProgressionDataUtils.to_string_name(rawTag);
            if (ft == "")
            {
                _validation_errors.Add(
                    $"Recipe {rd.recipe_id} declares an empty required_facility_tag."
                );
                return;
            }
            if (!facilityTagSet.Add(ft))
            {
                _validation_errors.Add(
                    $"Recipe {rd.recipe_id} declares duplicate required_facility_tag {ft}."
                );
                return;
            }
        }

        var seenInputItems = new HashSet<StringName>();

        for (int i = 0; i < rd.input_item_ids.Count; i++)
        {
            var inputId = ProgressionDataUtils.to_string_name(rd.input_item_ids[i]);

            int inputQty = rd.input_item_quantities[i];

            if (inputId == "")
            {
                _validation_errors.Add($"Recipe {rd.recipe_id} declares an empty input_item_id.");
                return;
            }

            if (!seenInputItems.Add(inputId))
            {
                _validation_errors.Add(
                    $"Recipe {rd.recipe_id} declares duplicate input_item_id {inputId}; aggregate quantity in a single entry."
                );
                return;
            }

            if (inputQty <= 0)
            {
                _validation_errors.Add(
                    $"Recipe {rd.recipe_id} input quantity for {inputId} must be >= 1."
                );
                return;
            }

            if (inputQty > RECIPE_QUANTITY_MAX)
            {
                _validation_errors.Add(
                    $"Recipe {rd.recipe_id} input quantity for {inputId} ({inputQty}) exceeds max {RECIPE_QUANTITY_MAX}."
                );
                return;
            }

            if (_item_defs.Count > 0 && !_item_defs.ContainsKey(inputId))
            {
                _validation_errors.Add(
                    $"Recipe {rd.recipe_id} references missing input item {inputId}."
                );
                return;
            }
        }

        if (_item_defs.Count > 0 && !_item_defs.ContainsKey(rd.output_item_id))
        {
            _validation_errors.Add(
                $"Recipe {rd.recipe_id} references missing output item {rd.output_item_id}."
            );
            return;
        }

        _recipe_defs[rd.recipe_id] = rd;
    }

}
