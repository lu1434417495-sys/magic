#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

public sealed class BarrierContentRegistry : IDisposable
{
    private readonly IContentJsonSourceReader _sourceReader;
    private readonly Dictionary<StringName, BarrierLayerDefinition> _layerDefinitions = new();
    private readonly Dictionary<StringName, BarrierProfileDefinition> _profileDefinitions = new();
    private readonly List<string> _validationErrors = new();
    private bool _disposed;

    internal BarrierContentRegistry()
        : this(new GodotContentJsonSourceReader(), loadDefaultContent: true) { }

    internal BarrierContentRegistry(
        IContentJsonSourceReader sourceReader,
        bool loadDefaultContent = true
    )
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild()
    {
        ThrowIfDisposed();
        _layerDefinitions.Clear();
        _profileDefinitions.Clear();
        _validationErrors.Clear();

        ContentImportBatch<BarrierLayerImportModel> layerBatch =
            BarrierJsonAuthoringDomains
                .CreateLayerDescriptor(BarrierJsonDomains.LayerDirectory, _sourceReader)
                .Import();
        ContentImportBatch<BarrierProfileImportModel> profileBatch =
            BarrierJsonAuthoringDomains
                .CreateProfileDescriptor(BarrierJsonDomains.ProfileDirectory, _sourceReader)
                .Import();
        AppendDiagnostics(layerBatch.Diagnostics);
        AppendDiagnostics(profileBatch.Diagnostics);

        foreach (ContentImportEntry<BarrierLayerImportModel> entry in layerBatch.Entries)
        {
            try
            {
                BarrierLayerDefinition definition = BarrierDefinitionProjector.ProjectLayer(
                    entry.Import
                );
                if (!_layerDefinitions.TryAdd(definition.LayerId, definition))
                {
                    _validationErrors.Add(
                        $"Duplicate barrier layer_id registered: {definition.LayerId}."
                    );
                }
            }
            catch (Exception exception)
            {
                _validationErrors.Add(
                    $"Barrier layer projection failed at {entry.Context.SourceLabel}: {exception.Message}"
                );
            }
        }

        foreach (ContentImportEntry<BarrierProfileImportModel> entry in profileBatch.Entries)
        {
            try
            {
                BarrierProfileDefinition definition = BarrierDefinitionProjector.ProjectProfile(
                    entry.Import,
                    _layerDefinitions
                );
                if (!_profileDefinitions.TryAdd(definition.ProfileId, definition))
                {
                    _validationErrors.Add(
                        $"Duplicate barrier profile_id registered: {definition.ProfileId}."
                    );
                    continue;
                }
                AppendProfileDefinitionErrors(definition);
            }
            catch (Exception exception)
            {
                _validationErrors.Add(
                    $"Barrier profile projection failed at {entry.Context.SourceLabel}: {exception.Message}"
                );
            }
        }
    }

    public BarrierProfileDefinition? GetProfileDef(StringName profileId) =>
        profileId != "" && _profileDefinitions.TryGetValue(profileId, out var definition)
            ? definition
            : null;

    public IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetProfileDefsTyped() =>
        new ReadOnlyDictionary<StringName, BarrierProfileDefinition>(
            new Dictionary<StringName, BarrierProfileDefinition>(_profileDefinitions)
        );

    internal IReadOnlyDictionary<StringName, BarrierLayerDefinition> GetLayerDefsTyped() =>
        new ReadOnlyDictionary<StringName, BarrierLayerDefinition>(
            new Dictionary<StringName, BarrierLayerDefinition>(_layerDefinitions)
        );

    public Godot.Collections.Array<string> Validate() => new(ValidateTyped());

    public IReadOnlyList<string> ValidateTyped() =>
        new ReadOnlyCollection<string>(new List<string>(_validationErrors));

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _layerDefinitions.Clear();
        _profileDefinitions.Clear();
        _validationErrors.Clear();
        GC.SuppressFinalize(this);
    }

    private void AppendProfileDefinitionErrors(BarrierProfileDefinition profile)
    {
        var orders = new HashSet<int>();
        foreach (BarrierLayerDefinition layer in profile.Layers)
        {
            if (!orders.Add(layer.Order))
            {
                _validationErrors.Add(
                    $"Barrier profile {profile.ProfileId} declares duplicate layer order {layer.Order}."
                );
            }
        }
    }

    private void AppendDiagnostics(IReadOnlyList<ContentJsonDiagnostic> diagnostics)
    {
        foreach (ContentJsonDiagnostic diagnostic in diagnostics)
        {
            _validationErrors.Add(
                $"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
