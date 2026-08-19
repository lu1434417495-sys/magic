using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class RecipeContentRegistry : IValidatableRegistry, IDisposable
{
    private const string RecipeJsonDirectory = RecipeContentJsonAuthoringDomain.ProductionDirectory;

    private readonly Dictionary<StringName, RecipeDefinition> _recipeDefs = new();
    private readonly List<string> _validationErrors = new();
    private Dictionary<StringName, ItemDefinition> _itemDefs = new();
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _recipeDefs.Clear();
        _validationErrors.Clear();
        _itemDefs.Clear();
        GC.SuppressFinalize(this);
    }

    internal void Setup(IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions)
    {
        ThrowIfDisposed();
        _itemDefs = itemDefinitions != null
            ? new Dictionary<StringName, ItemDefinition>(itemDefinitions)
            : new Dictionary<StringName, ItemDefinition>();
        Rebuild();
    }

    public void Rebuild() =>
        LoadFromJsonDirectory(RecipeJsonDirectory, new GodotContentJsonSourceReader());

    internal void LoadFromJsonDirectory(
        string directoryPath,
        IContentJsonSourceReader sourceReader
    )
    {
        ThrowIfDisposed();
        _recipeDefs.Clear();
        _validationErrors.Clear();
        ContentImportBatch<RecipeImportModel> batch =
            RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
                directoryPath,
                sourceReader
            ).Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
        {
            _validationErrors.Add(
                $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
        if (batch.HasErrors)
            return;
        foreach (ContentImportEntry<RecipeImportModel> entry in batch.Entries)
            RegisterImport(entry);
    }

    public Godot.Collections.Array<string> Validate()
    {
        ThrowIfDisposed();
        var result = new Godot.Collections.Array<string>();
        foreach (string error in _validationErrors)
            result.Add(error);
        return result;
    }

    internal IReadOnlyDictionary<StringName, RecipeDefinition> GetRecipeDefsTyped()
    {
        ThrowIfDisposed();
        return new ReadOnlyDictionary<StringName, RecipeDefinition>(
            new Dictionary<StringName, RecipeDefinition>(_recipeDefs)
        );
    }

    public IReadOnlyList<string> ValidateTyped()
    {
        ThrowIfDisposed();
        return new ReadOnlyCollection<string>(new List<string>(_validationErrors));
    }

    private void RegisterImport(ContentImportEntry<RecipeImportModel> entry)
    {
        RecipeImportModel import = entry.Import;
        var recipeId = new StringName(import.RecipeId);
        if (_recipeDefs.ContainsKey(recipeId))
        {
            _validationErrors.Add($"Duplicate recipe_id registered: {recipeId}");
            return;
        }

        foreach (RecipeIngredientImportModel input in import.Inputs)
        {
            var inputId = new StringName(input.ItemId);
            if (_itemDefs.Count > 0 && !_itemDefs.ContainsKey(inputId))
            {
                _validationErrors.Add(
                    $"Recipe {recipeId} references missing input item {inputId}."
                );
                return;
            }
        }

        var outputItemId = new StringName(import.OutputItemId);
        if (_itemDefs.Count > 0 && !_itemDefs.ContainsKey(outputItemId))
        {
            _validationErrors.Add(
                $"Recipe {recipeId} references missing output item {outputItemId}."
            );
            return;
        }

        _recipeDefs.Add(recipeId, RecipeDefinitionProjector.Project(import));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
