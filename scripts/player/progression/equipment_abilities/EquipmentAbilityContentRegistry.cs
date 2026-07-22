using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Godot;

internal sealed class EquipmentAbilityContentRegistry : IDisposable
{

    private readonly Dictionary<StringName, EquipmentAbilityContentPackDefinition> _packsById = new();
    private readonly Dictionary<StringName, EquipmentAbilityBindingDefinition> _bindingsById = new();
    private readonly Dictionary<StringName, List<EquipmentAbilityBindingDefinition>> _bindingsByTraitId = new();
    private readonly IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> _conditionSpecs =
        EquipmentAbilityBuiltInHandlerSpecs.BuildConditionSpecs();
    private readonly IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> _actionSpecs =
        EquipmentAbilityBuiltInHandlerSpecs.BuildActionSpecs();
    private readonly IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> _triggerTimingSpecs =
        EquipmentAbilityBuiltInHandlerSpecs.BuildTriggerTimingSpecs();

    private EquipmentAbilityRegistryBuildResult _lastBuildResult = new()
    {
        Success = true,
        Revision = 0,
        Errors = Array.Empty<string>(),
    };
    private int _revision;
    private bool _disposed;
    private readonly EquipmentAbilityBindingValidator _bindingValidator;

    internal EquipmentAbilityContentRegistry(IContentResourceLoader resourceLoader)
    {
        ArgumentNullException.ThrowIfNull(resourceLoader);
        _bindingValidator = new EquipmentAbilityBindingValidator(
            _conditionSpecs,
            _actionSpecs,
            _triggerTimingSpecs
        );
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    public int GetRevision() => _revision;

    public EquipmentAbilityRegistryBuildResult GetLastBuildResultTyped() => _lastBuildResult;

    public IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> GetPackDefinitionsTyped()
    {
        return Snapshot(_packsById);
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetBindingDefinitionsTyped()
    {
        return Snapshot(_bindingsById);
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> GetConditionHandlerSpecsTyped() =>
        _conditionSpecs;

    public IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> GetActionHandlerSpecsTyped() =>
        _actionSpecs;

    public IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> GetTriggerTimingSpecsTyped() =>
        _triggerTimingSpecs;

    public EquipmentAbilityRegistryBuildResult Rebuild(
        IReadOnlyList<EquipmentAbilityContentPackDef> packs,
        EquipmentAbilityContentValidationContext validationContext
    )
    {
        _revision++;
        var errors = new List<string>();
        validationContext ??= new EquipmentAbilityContentValidationContext();
        List<EquipmentAbilityContentPackDef> sortedPacks = SortPacks(packs, errors);
        var nextPacks = new Dictionary<StringName, EquipmentAbilityContentPackDefinition>();
        var nextBindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
        var nextByTrait = new Dictionary<StringName, List<EquipmentAbilityBindingDefinition>>();

        if (errors.Count == 0)
        {
            foreach (EquipmentAbilityContentPackDef pack in sortedPacks)
            {
                string packPath = PackPath(pack);
                var projectedBindings = new List<EquipmentAbilityBindingDefinition>();
                foreach (EquipmentAbilityBindingDef binding in pack.bindings)
                {
                    if (binding == null)
                    {
                        AddError(
                            errors,
                            "EQA_BINDING_NULL",
                            $"{packPath}.bindings",
                            "binding entry must not be null"
                        );
                        continue;
                    }

                    _bindingValidator.ValidateBinding(binding, validationContext, nextBindings, errors);
                    if (errors.Count > 0)
                    {
                        continue;
                    }

                    EquipmentAbilityBindingDefinition definition = EquipmentAbilityDefinitionProjection.ProjectBinding(binding);
                    projectedBindings.Add(definition);

                    if (definition.OverrideMode == EquipmentAbilityBindingOverrideMode.ReplaceBinding)
                    {
                        if (nextBindings.TryGetValue(definition.ReplacesBindingId, out var replaced))
                        {
                            nextBindings.Remove(definition.ReplacesBindingId);
                            RemoveTraitIndex(nextByTrait, replaced);
                        }
                    }
                    nextBindings[definition.BindingId] = definition;
                    AddTraitIndex(nextByTrait, definition);
                }

                if (errors.Count == 0)
                {
                    EquipmentAbilityContentPackDefinition packDefinition = EquipmentAbilityDefinitionProjection.ProjectPack(
                        pack,
                        projectedBindings
                    );
                    nextPacks[packDefinition.PackId] = packDefinition;
                }
            }
        }

        bool success = errors.Count == 0;
        if (success)
        {
            _packsById.Clear();
            foreach ((StringName key, EquipmentAbilityContentPackDefinition value) in nextPacks)
                _packsById[key] = value;
            _bindingsById.Clear();
            foreach ((StringName key, EquipmentAbilityBindingDefinition value) in nextBindings)
                _bindingsById[key] = value;
            _bindingsByTraitId.Clear();
            foreach ((StringName key, List<EquipmentAbilityBindingDefinition> value) in nextByTrait)
                _bindingsByTraitId[key] = value;
        }

        _lastBuildResult = new EquipmentAbilityRegistryBuildResult
        {
            Success = success,
            Revision = _revision,
            Errors = new ReadOnlyCollection<string>(errors),
        };
        return _lastBuildResult;
    }

    public IReadOnlyList<EquipmentAbilityBindingDefinition> FindBindings(
        StringName traitId,
        TraitSourceKind sourceKind,
        IReadOnlySet<StringName> traitCategories,
        ItemDefinition sourceItem
    )
    {
        if (traitId == "" || sourceKind == TraitSourceKind.Unknown)
            return Array.Empty<EquipmentAbilityBindingDefinition>();
        if (!_bindingsByTraitId.TryGetValue(traitId, out List<EquipmentAbilityBindingDefinition> candidates))
            return Array.Empty<EquipmentAbilityBindingDefinition>();
        return EquipmentAbilityBindingMatcher.FindBindings(
            candidates,
            traitId,
            sourceKind,
            traitCategories,
            sourceItem
        );
    }

    private void Clear()
    {
        _packsById.Clear();
        _bindingsById.Clear();
        _bindingsByTraitId.Clear();
        _lastBuildResult = new EquipmentAbilityRegistryBuildResult
        {
            Success = true,
            Revision = _revision,
            Errors = Array.Empty<string>(),
        };
    }

    private static List<EquipmentAbilityContentPackDef> SortPacks(
        IReadOnlyList<EquipmentAbilityContentPackDef> packs,
        List<string> errors
    )
    {
        var input = new List<EquipmentAbilityContentPackDef>();
        if (packs == null || packs.Count == 0)
            return input;

        var byId = new Dictionary<StringName, EquipmentAbilityContentPackDef>();
        foreach (EquipmentAbilityContentPackDef pack in packs)
        {
            if (pack == null)
            {
                AddError(errors, "EQA_PACK_NULL", "equipment_ability.packs", "pack must not be null");
                continue;
            }
            if (pack.pack_id == "")
            {
                AddError(
                    errors,
                    "EQA_PACK_MISSING_ID",
                    "equipment_ability.packs[<missing>]",
                    "pack_id is required"
                );
                continue;
            }
            if (pack.schema_version != 1)
            {
                AddError(
                    errors,
                    "EQA_PACK_SCHEMA_VERSION_UNSUPPORTED",
                    PackPath(pack),
                    "schema_version must be exactly 1"
                );
            }
            if (byId.ContainsKey(pack.pack_id))
            {
                AddError(
                    errors,
                    "EQA_PACK_DUPLICATE_ID",
                    PackPath(pack),
                    $"duplicate pack_id {pack.pack_id}"
                );
                continue;
            }
            byId[pack.pack_id] = pack;
            input.Add(pack);
        }

        foreach (EquipmentAbilityContentPackDef pack in input)
        {
            foreach (StringName dependency in pack.dependencies)
            {
                if (dependency == "" || !byId.ContainsKey(dependency))
                {
                    AddError(
                        errors,
                        "EQA_PACK_DEPENDENCY_MISSING",
                        $"{PackPath(pack)}.dependencies[{dependency}]",
                        $"missing dependency {dependency}"
                    );
                }
            }
        }
        if (errors.Count > 0)
            return input;

        var result = new List<EquipmentAbilityContentPackDef>();
        var emitted = new HashSet<StringName>();
        while (result.Count < input.Count)
        {
            var candidates = new List<EquipmentAbilityContentPackDef>();
            foreach (EquipmentAbilityContentPackDef pack in input)
            {
                if (emitted.Contains(pack.pack_id))
                    continue;
                bool ready = true;
                foreach (StringName dependency in pack.dependencies)
                {
                    if (!emitted.Contains(dependency))
                    {
                        ready = false;
                        break;
                    }
                }
                if (ready)
                    candidates.Add(pack);
            }
            if (candidates.Count == 0)
            {
                AddError(
                    errors,
                    "EQA_PACK_DEPENDENCY_CYCLE",
                    "equipment_ability.packs",
                    "pack dependency graph contains a cycle"
                );
                return input;
            }
            candidates.Sort(ComparePackOrder);
            EquipmentAbilityContentPackDef next = candidates[0];
            emitted.Add(next.pack_id);
            result.Add(next);
        }
        return result;
    }

    private static int ComparePackOrder(
        EquipmentAbilityContentPackDef left,
        EquipmentAbilityContentPackDef right
    )
    {
        int loadOrderCompare = left.load_order.CompareTo(right.load_order);
        return loadOrderCompare != 0
            ? loadOrderCompare
            : string.CompareOrdinal(left.pack_id.ToString(), right.pack_id.ToString());
    }

    internal static bool HasKnownValues(IReadOnlySet<StringName> values) =>
        values != null && values.Count > 0;

    internal static bool ContainsValue(IReadOnlySet<StringName> source, StringName key) =>
        source != null && source.Contains(key);

    internal static bool IsKnownAcComponent(StringName componentId)
    {
        if (componentId == "")
            return false;
        foreach (StringName known in AttributeService.AC_COMPONENT_ATTRIBUTE_IDS)
            if (known == componentId)
                return true;
        return false;
    }

    private static bool IsAllowed(StringName value, params string[] allowed)
    {
        foreach (string candidate in allowed)
        {
            if (value == candidate)
                return true;
        }
        return false;
    }

    private static void AddTraitIndex(
        Dictionary<StringName, List<EquipmentAbilityBindingDefinition>> index,
        EquipmentAbilityBindingDefinition binding
    )
    {
        if (!index.TryGetValue(binding.TraitId, out var list))
        {
            list = new List<EquipmentAbilityBindingDefinition>();
            index[binding.TraitId] = list;
        }
        list.Add(binding);
    }

    private static void RemoveTraitIndex(
        Dictionary<StringName, List<EquipmentAbilityBindingDefinition>> index,
        EquipmentAbilityBindingDefinition binding
    )
    {
        if (!index.TryGetValue(binding.TraitId, out var list))
            return;
        list.RemoveAll(candidate => candidate.BindingId == binding.BindingId);
        if (list.Count == 0)
            index.Remove(binding.TraitId);
    }

    private static IReadOnlyDictionary<StringName, T> Snapshot<T>(Dictionary<StringName, T> source)
    {
        return new ReadOnlyDictionary<StringName, T>(new Dictionary<StringName, T>(source));
    }

    private static string PackPath(EquipmentAbilityContentPackDef pack) =>
        $"equipment_ability.packs[{(pack?.pack_id == "" ? "<missing>" : pack?.pack_id.ToString() ?? "<null>")}]";

    internal static string BindingPath(EquipmentAbilityBindingDef binding) =>
        $"equipment_ability.bindings[{(binding?.binding_id == "" ? "<missing>" : binding?.binding_id.ToString() ?? "<null>")}]";

    internal static string ReactionLabel(EquipmentAbilityReactionDef reaction) =>
        reaction?.reaction_id == "" ? "<missing>" : reaction?.reaction_id.ToString() ?? "<null>";

    internal static void AddError(List<string> errors, string code, string path, string message)
    {
        errors.Add($"{code} {path}: {message}");
    }
}
