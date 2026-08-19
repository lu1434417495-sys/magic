using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class GearSetContentRegistry : IDisposable
{
    private const string GearSetJsonDirectoryPath = GearSetContentJsonAuthoringDomain.ProductionDirectory;

    private readonly IContentJsonSourceReader _sourceReader;
    private readonly string _configDirectoryPath;
    private readonly Dictionary<StringName, GearSetDefinition> _definitions = new();
    private readonly List<string> _loadValidationErrors = new();
    private readonly List<string> _definitionValidationErrors = new();
    private bool _disposed;

    internal GearSetContentRegistry(
        string configDirectoryPath = GearSetJsonDirectoryPath,
        IContentJsonSourceReader sourceReader = null
    )
    {
        if (string.IsNullOrWhiteSpace(configDirectoryPath))
            throw new ArgumentException("Gear-set config directory must be non-empty.", nameof(configDirectoryPath));
        _configDirectoryPath = configDirectoryPath.TrimEnd('/');
        _sourceReader = sourceReader ?? new GodotContentJsonSourceReader();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _definitions.Clear();
        _loadValidationErrors.Clear();
        _definitionValidationErrors.Clear();
        GC.SuppressFinalize(this);
    }

    internal void Rebuild()
    {
        ThrowIfDisposed();
        _definitions.Clear();
        _loadValidationErrors.Clear();
        _definitionValidationErrors.Clear();
        LoadJsonDirectory();
    }

    internal IReadOnlyDictionary<StringName, GearSetDefinition> GetDefinitionsTyped()
    {
        ThrowIfDisposed();
        return new ReadOnlyDictionary<StringName, GearSetDefinition>(
            new Dictionary<StringName, GearSetDefinition>(_definitions)
        );
    }

    internal IReadOnlyList<string> ValidateTyped(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingDefinitions
    )
    {
        ThrowIfDisposed();
        _definitionValidationErrors.Clear();
        foreach (GearSetDefinition definition in SortedDefinitions())
        {
            AppendDefinitionErrors(
                definition,
                itemDefinitions,
                traitDefinitions,
                bindingDefinitions
            );
        }

        var result = new List<string>(_loadValidationErrors.Count + _definitionValidationErrors.Count);
        result.AddRange(_loadValidationErrors);
        result.AddRange(_definitionValidationErrors);
        return new ReadOnlyCollection<string>(result);
    }

    internal static IReadOnlyList<string> ValidateDefinitionsTyped(
        IReadOnlyDictionary<StringName, GearSetDefinition> definitions,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(definitions);
        using var registry = new GearSetContentRegistry();
        foreach ((StringName gearSetId, GearSetDefinition definition) in definitions)
            registry._definitions.Add(gearSetId, definition);
        return registry.ValidateTyped(
            itemDefinitions,
            traitDefinitions,
            bindingDefinitions
        );
    }

    private void LoadJsonDirectory()
    {
        ContentImportBatch<GearSetImportModel> batch =
            GearSetContentJsonAuthoringDomain.CreateImportDescriptor(
                _configDirectoryPath,
                _sourceReader
            ).Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
        {
            _loadValidationErrors.Add(
                $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
        if (batch.HasErrors)
            return;

        foreach (ContentImportEntry<GearSetImportModel> entry in batch.Entries)
        {
            GearSetDefinition definition = GearSetDefinitionProjector.Project(entry.Import);
            if (_definitions.ContainsKey(definition.GearSetId))
            {
                _loadValidationErrors.Add(
                    $"Duplicate gear_set_id {definition.GearSetId} at {entry.Context.SourceLabel}."
                );
                continue;
            }
            _definitions.Add(definition.GearSetId, definition);
        }
    }

    private void AppendDefinitionErrors(
        GearSetDefinition definition,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingDefinitions
    )
    {
        if (definition == null)
            return;
        string owner = $"Gear set {definition.GearSetId}";
        if (definition.DisplayName.Length == 0)
            _definitionValidationErrors.Add($"{owner}.display_name must be non-empty.");

        var memberIds = new HashSet<StringName>();
        for (int index = 0; index < definition.MemberItemIds.Count; index++)
        {
            StringName memberItemId = definition.MemberItemIds[index];
            string path = $"{owner}.member_item_ids[{index}]";
            if (memberItemId == "")
            {
                _definitionValidationErrors.Add($"{path} must be non-empty.");
                continue;
            }
            if (!memberIds.Add(memberItemId))
            {
                _definitionValidationErrors.Add($"{path} duplicates member {memberItemId}.");
                continue;
            }
            if (
                itemDefinitions == null
                || !itemDefinitions.TryGetValue(memberItemId, out ItemDefinition itemDefinition)
                || itemDefinition == null
            )
            {
                _definitionValidationErrors.Add($"{path} references missing item {memberItemId}.");
                continue;
            }
            if (!itemDefinition.IsEquipment() || itemDefinition.IsStackable)
                _definitionValidationErrors.Add(
                    $"{path} must reference a non-stackable equipment item."
                );
        }
        if (memberIds.Count == 0)
            _definitionValidationErrors.Add($"{owner}.member_item_ids must not be empty.");

        if (definition.UsageAnchorItemId == "")
            _definitionValidationErrors.Add($"{owner}.usage_anchor_item_id must be non-empty.");
        else if (!memberIds.Contains(definition.UsageAnchorItemId))
            _definitionValidationErrors.Add(
                $"{owner}.usage_anchor_item_id {definition.UsageAnchorItemId} must be a member item."
            );

        var thresholdIds = new HashSet<StringName>();
        int previousRequiredCount = 0;
        for (int index = 0; index < definition.Thresholds.Count; index++)
        {
            GearSetThresholdDefinition threshold = definition.Thresholds[index];
            string thresholdPath = $"{owner}.thresholds[{index}]";
            if (threshold == null)
            {
                _definitionValidationErrors.Add($"{thresholdPath} must not be null.");
                continue;
            }
            if (threshold.ThresholdId == "")
                _definitionValidationErrors.Add($"{thresholdPath}.threshold_id must be non-empty.");
            else if (!thresholdIds.Add(threshold.ThresholdId))
                _definitionValidationErrors.Add(
                    $"{thresholdPath}.threshold_id duplicates {threshold.ThresholdId}."
                );
            if (threshold.RequiredPieceCount <= 0)
                _definitionValidationErrors.Add(
                    $"{thresholdPath}.required_piece_count must be positive."
                );
            if (threshold.RequiredPieceCount <= previousRequiredCount)
                _definitionValidationErrors.Add(
                    $"{thresholdPath}.required_piece_count must be strictly increasing."
                );
            if (threshold.RequiredPieceCount > memberIds.Count)
                _definitionValidationErrors.Add(
                    $"{thresholdPath}.required_piece_count exceeds the member count {memberIds.Count}."
                );
            previousRequiredCount = Math.Max(previousRequiredCount, threshold.RequiredPieceCount);

            var mandatoryIds = new HashSet<StringName>();
            for (int mandatoryIndex = 0; mandatoryIndex < threshold.MandatoryMemberItemIds.Count; mandatoryIndex++)
            {
                StringName mandatoryItemId = threshold.MandatoryMemberItemIds[mandatoryIndex];
                string mandatoryPath =
                    $"{thresholdPath}.mandatory_member_item_ids[{mandatoryIndex}]";
                if (mandatoryItemId == "" || !memberIds.Contains(mandatoryItemId))
                    _definitionValidationErrors.Add(
                        $"{mandatoryPath} must reference a member item."
                    );
                else if (!mandatoryIds.Add(mandatoryItemId))
                    _definitionValidationErrors.Add(
                        $"{mandatoryPath} duplicates {mandatoryItemId}."
                    );
            }
            if (mandatoryIds.Count > threshold.RequiredPieceCount)
                _definitionValidationErrors.Add(
                    $"{thresholdPath}.mandatory_member_item_ids cannot exceed required_piece_count."
                );

            AppendModifierErrors(threshold, thresholdPath);
            AppendTraitErrors(
                definition,
                threshold,
                thresholdPath,
                mandatoryIds,
                traitDefinitions,
                bindingDefinitions
            );
        }
        if (definition.Thresholds.Count == 0)
            _definitionValidationErrors.Add($"{owner}.thresholds must not be empty.");
    }

    private void AppendModifierErrors(
        GearSetThresholdDefinition threshold,
        string thresholdPath
    )
    {
        var seenAttributeIds = new HashSet<StringName>();
        for (int index = 0; index < threshold.AttributeModifiers.Count; index++)
        {
            AttributeModifierDefinition modifier = threshold.AttributeModifiers[index];
            string path = $"{thresholdPath}.attribute_modifiers[{index}]";
            if (modifier == null)
            {
                _definitionValidationErrors.Add($"{path} must not be null.");
                continue;
            }
            if (modifier.AttributeId == "")
                _definitionValidationErrors.Add($"{path}.attribute_id must be non-empty.");
            else if (!seenAttributeIds.Add(modifier.AttributeId))
                _definitionValidationErrors.Add(
                    $"{path}.attribute_id duplicates direct threshold attribute {modifier.AttributeId}."
                );
            if (!AttributeModifier.IsValidMode(modifier.Mode))
                _definitionValidationErrors.Add($"{path}.mode {modifier.Mode} is unsupported.");
        }
    }

    private void AppendTraitErrors(
        GearSetDefinition definition,
        GearSetThresholdDefinition threshold,
        string thresholdPath,
        IReadOnlySet<StringName> mandatoryIds,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingDefinitions
    )
    {
        var seenTraitIds = new HashSet<StringName>();
        for (int index = 0; index < threshold.GrantedTraitIds.Count; index++)
        {
            StringName traitId = threshold.GrantedTraitIds[index];
            string path = $"{thresholdPath}.granted_trait_ids[{index}]";
            if (traitId == "")
            {
                _definitionValidationErrors.Add($"{path} must be non-empty.");
                continue;
            }
            if (!seenTraitIds.Add(traitId))
                _definitionValidationErrors.Add($"{path} duplicates {traitId}.");
            if (
                traitDefinitions == null
                || !traitDefinitions.TryGetValue(traitId, out TraitDefinition traitDefinition)
                || traitDefinition == null
            )
            {
                _definitionValidationErrors.Add($"{path} references missing trait {traitId}.");
                continue;
            }
            if (!TraitContentRules.IsSourceKindAllowed(traitDefinition, TraitSourceKind.GearSetThreshold))
                _definitionValidationErrors.Add(
                    $"{path} trait {traitId} must allow gear_set_threshold."
                );
            foreach (AttributeModifierDefinition traitModifier in traitDefinition.AttributeModifiers)
            {
                if (
                    traitModifier == null
                    || traitModifier.AttributeId == ""
                    || !HasDirectThresholdAttribute(threshold, traitModifier.AttributeId)
                )
                {
                    continue;
                }
                _definitionValidationErrors.Add(
                    $"{path} trait {traitId} duplicates direct threshold attribute {traitModifier.AttributeId}."
                );
            }

            if (
                GrantsPerWorldDayAction(traitId, bindingDefinitions)
                && threshold.RequiredPieceCount < definition.MemberItemIds.Count
                && !mandatoryIds.Contains(definition.UsageAnchorItemId)
            )
            {
                _definitionValidationErrors.Add(
                    $"{path} grants a per_world_day action before the full-set threshold; usage_anchor_item_id {definition.UsageAnchorItemId} must be mandatory."
                );
            }
        }
    }

    private static bool HasDirectThresholdAttribute(
        GearSetThresholdDefinition threshold,
        StringName attributeId
    )
    {
        foreach (AttributeModifierDefinition modifier in threshold.AttributeModifiers)
        {
            if (modifier?.AttributeId == attributeId)
                return true;
        }
        return false;
    }

    private static bool GrantsPerWorldDayAction(
        StringName traitId,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingDefinitions
    )
    {
        if (bindingDefinitions == null)
            return false;
        StringName sourceKind = TraitContentRules.ToStringName(TraitSourceKind.GearSetThreshold);
        foreach (EquipmentAbilityBindingDefinition binding in bindingDefinitions.Values)
        {
            if (
                binding == null
                || binding.TraitId != traitId
                || !binding.AllowedSourceKinds.Contains(sourceKind)
            )
            {
                continue;
            }
            foreach (EquipmentGrantedActionDefinition action in binding.GrantedActions)
            {
                if (action?.UsagePeriodKind == EquipmentAbilityUsagePeriodKind.PerWorldDay)
                    return true;
            }
        }
        return false;
    }

    private List<GearSetDefinition> SortedDefinitions()
    {
        var result = new List<GearSetDefinition>(_definitions.Values);
        result.Sort(
            (left, right) =>
                string.CompareOrdinal(
                    left?.GearSetId.ToString() ?? "",
                    right?.GearSetId.ToString() ?? ""
                )
        );
        return result;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
