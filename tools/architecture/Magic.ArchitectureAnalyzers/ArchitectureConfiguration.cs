using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Magic.ArchitectureAnalyzers;

internal static class ArchitectureConfigurationLoader
{
    internal const string RulesFileName = "layer_rules.json";
    internal const string BaselineFileName = "layer_baseline.json";
    internal const string InventoryRequestFileName = "layer_inventory_request.json";

    internal static ArchitectureConfigurationLoadResult Load(
        ImmutableArray<AdditionalText> additionalFiles,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return LoadCore(additionalFiles, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ArchitectureConfigurationLoadResult.Failure(
                new[]
                {
                    new ArchitectureConfigurationError(
                        "configuration_processing_failed",
                        "AdditionalFiles",
                        "Unexpected analyzer configuration failure: " + exception.Message
                    ),
                }
            );
        }
    }

    private static ArchitectureConfigurationLoadResult LoadCore(
        ImmutableArray<AdditionalText> additionalFiles,
        CancellationToken cancellationToken
    )
    {
        var errors = new List<ArchitectureConfigurationError>();
        AdditionalText? rulesFile = GetSingleFile(
            additionalFiles,
            RulesFileName,
            "missing_rules_file",
            "duplicate_rules_file",
            errors
        );
        AdditionalText? baselineFile = GetSingleFile(
            additionalFiles,
            BaselineFileName,
            "missing_baseline_file",
            "duplicate_baseline_file",
            errors
        );
        AdditionalText? inventoryRequestFile = GetOptionalSingleFile(
            additionalFiles,
            InventoryRequestFileName,
            "duplicate_inventory_request_file",
            errors
        );

        if (errors.Count != 0 || rulesFile == null || baselineFile == null)
            return ArchitectureConfigurationLoadResult.Failure(errors);

        LayerRulesDocument? rules = Deserialize<LayerRulesDocument>(
            rulesFile,
            cancellationToken,
            errors
        );
        LayerBaselineDocument? baseline = Deserialize<LayerBaselineDocument>(
            baselineFile,
            cancellationToken,
            errors
        );
        LayerInventoryRequestDocument? inventoryRequest = inventoryRequestFile == null
            ? null
            : Deserialize<LayerInventoryRequestDocument>(
                inventoryRequestFile,
                cancellationToken,
                errors
            );
        if (errors.Count != 0 || rules == null || baseline == null)
            return ArchitectureConfigurationLoadResult.Failure(errors);

        ValidateRules(rules, rulesFile.Path, errors);
        ValidateBaseline(baseline, baselineFile.Path, rules, errors);
        if (inventoryRequest != null && inventoryRequestFile != null)
            ValidateInventoryRequest(inventoryRequest, inventoryRequestFile.Path, errors);
        if (errors.Count != 0)
            return ArchitectureConfigurationLoadResult.Failure(errors);

        try
        {
            return ArchitectureConfigurationLoadResult.Success(
                ArchitecturePolicy.Create(
                    rules,
                    baseline,
                    rulesFile.Path,
                    inventoryRequest != null
                )
            );
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    "invalid_source_root",
                    rulesFile.Path,
                    "Unable to resolve sourceRootRelativeToRulesFile: " + exception.Message
                )
            );
            return ArchitectureConfigurationLoadResult.Failure(errors);
        }
    }

    private static AdditionalText? GetSingleFile(
        ImmutableArray<AdditionalText> files,
        string fileName,
        string missingCode,
        string duplicateCode,
        List<ArchitectureConfigurationError> errors
    )
    {
        List<AdditionalText> matches = files
            .Where(file => string.Equals(
                Path.GetFileName(file.Path),
                fileName,
                StringComparison.OrdinalIgnoreCase
            ))
            .ToList();
        if (matches.Count == 1)
            return matches[0];

        if (matches.Count == 0)
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    missingCode,
                    fileName,
                    "Required AdditionalFile '" + fileName + "' is missing."
                )
            );
            return null;
        }

        errors.Add(
            new ArchitectureConfigurationError(
                duplicateCode,
                fileName,
                "Required AdditionalFile '" + fileName + "' was supplied more than once."
            )
        );
        return null;
    }

    private static AdditionalText? GetOptionalSingleFile(
        ImmutableArray<AdditionalText> files,
        string fileName,
        string duplicateCode,
        List<ArchitectureConfigurationError> errors
    )
    {
        List<AdditionalText> matches = files
            .Where(file => string.Equals(
                Path.GetFileName(file.Path),
                fileName,
                StringComparison.OrdinalIgnoreCase
            ))
            .ToList();
        if (matches.Count == 0)
            return null;
        if (matches.Count == 1)
            return matches[0];
        errors.Add(
            new ArchitectureConfigurationError(
                duplicateCode,
                fileName,
                "Optional AdditionalFile '" + fileName + "' was supplied more than once."
            )
        );
        return null;
    }

    private static T? Deserialize<T>(
        AdditionalText file,
        CancellationToken cancellationToken,
        List<ArchitectureConfigurationError> errors
    ) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        Microsoft.CodeAnalysis.Text.SourceText? sourceText;
        try
        {
            sourceText = file.GetText(cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    "unreadable_config",
                    file.Path,
                    "Unable to read analyzer configuration: " + exception.Message
                )
            );
            return null;
        }
        if (sourceText == null)
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    "unreadable_config",
                    file.Path,
                    "Unable to read analyzer configuration."
                )
            );
            return null;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sourceText.ToString());
            using var stream = new MemoryStream(bytes, writable: false);
            var serializer = new DataContractJsonSerializer(typeof(T));
            T? document = serializer.ReadObject(stream) as T;
            if (document == null)
            {
                errors.Add(
                    new ArchitectureConfigurationError(
                        "null_config_document",
                        file.Path,
                        "Analyzer configuration must be a JSON object, not null."
                    )
                );
            }
            return document;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException))
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    "invalid_json",
                    file.Path,
                    "Invalid JSON configuration: " + exception.Message
                )
            );
            return null;
        }
    }

    private static void ValidateRules(
        LayerRulesDocument document,
        string configFile,
        List<ArchitectureConfigurationError> errors
    )
    {
        if (document.SchemaVersion != 1)
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    "unsupported_rules_version",
                    configFile,
                    "layer_rules.json schemaVersion must be 1."
                )
            );
        }

        if (string.IsNullOrWhiteSpace(document.SourceRootRelativeToRulesFile))
        {
            errors.Add(
                new ArchitectureConfigurationError(
                    "missing_source_root",
                    configFile,
                    "sourceRootRelativeToRulesFile is required."
                )
            );
        }

        var layerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (LayerDefinition? layer in document.Layers ?? new List<LayerDefinition>())
        {
            if (layer == null)
            {
                errors.Add(new ArchitectureConfigurationError(
                    "null_layer",
                    configFile,
                    "layers cannot contain null entries."
                ));
                continue;
            }
            if (string.IsNullOrWhiteSpace(layer.Id))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "invalid_layer",
                    configFile,
                    "Every layer requires a non-empty id."
                ));
                continue;
            }
            if (!layerIds.Add(layer.Id))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "duplicate_layer",
                    configFile,
                    "Layer id '" + layer.Id + "' is duplicated."
                ));
            }
            if (!layer.AnalyzeOutgoingDependencies.HasValue)
            {
                errors.Add(new ArchitectureConfigurationError(
                    "missing_layer_mode",
                    configFile,
                    "Layer '" + layer.Id + "' must set analyzeOutgoingDependencies."
                ));
            }
        }
        if (layerIds.Count == 0)
        {
            errors.Add(new ArchitectureConfigurationError(
                "missing_layers",
                configFile,
                "At least one layer is required."
            ));
        }

        List<PathMappingDefinition> pathMappings =
            document.PathMappings ?? new List<PathMappingDefinition>();
        if (pathMappings.Count == 0)
        {
            errors.Add(new ArchitectureConfigurationError(
                "missing_path_mappings",
                configFile,
                "At least one ordered pathMapping is required."
            ));
        }
        foreach (PathMappingDefinition? mapping in pathMappings)
        {
            if (mapping == null)
            {
                errors.Add(new ArchitectureConfigurationError(
                    "null_path_mapping",
                    configFile,
                    "pathMappings cannot contain null entries."
                ));
                continue;
            }
            if (string.IsNullOrWhiteSpace(mapping.Glob))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "invalid_path_mapping",
                    configFile,
                    "Every pathMapping requires a non-empty glob."
                ));
            }
            if (string.IsNullOrWhiteSpace(mapping.Layer) || !layerIds.Contains(mapping.Layer))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "unknown_mapping_layer",
                    configFile,
                    "Path mapping '" + mapping.Glob + "' references an unknown layer."
                ));
            }
        }

        var overrideSymbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (SymbolOverrideDefinition? symbolOverride in
            document.SymbolOverrides ?? new List<SymbolOverrideDefinition>())
        {
            if (symbolOverride == null)
            {
                errors.Add(new ArchitectureConfigurationError(
                    "null_symbol_override",
                    configFile,
                    "symbolOverrides cannot contain null entries."
                ));
                continue;
            }
            if (string.IsNullOrWhiteSpace(symbolOverride.Symbol)
                || !overrideSymbols.Add(symbolOverride.Symbol))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "duplicate_or_invalid_symbol_override",
                    configFile,
                    "Every symbolOverride requires a unique documentation symbol id."
                ));
            }
            if (string.IsNullOrWhiteSpace(symbolOverride.Layer)
                || !layerIds.Contains(symbolOverride.Layer))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "unknown_override_layer",
                    configFile,
                    "Symbol override '" + symbolOverride.Symbol + "' references an unknown layer."
                ));
            }
        }

        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DenyRuleDefinition? rule in
            document.DenyRules ?? new List<DenyRuleDefinition>())
        {
            if (rule == null)
            {
                errors.Add(new ArchitectureConfigurationError(
                    "null_deny_rule",
                    configFile,
                    "denyRules cannot contain null entries."
                ));
                continue;
            }
            if (string.IsNullOrWhiteSpace(rule.Id) || !ruleIds.Add(rule.Id))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "duplicate_or_invalid_rule",
                    configFile,
                    "Every denyRule requires a unique non-empty id."
                ));
            }
            ValidateLayerList(rule.FromLayers, "fromLayers", rule.Id, layerIds, configFile, errors);
            ValidateLayerList(rule.ToLayers, "toLayers", rule.Id, layerIds, configFile, errors);
        }
        if (ruleIds.Count == 0)
        {
            errors.Add(new ArchitectureConfigurationError(
                "missing_deny_rules",
                configFile,
                "At least one denyRule is required for the semantic spike."
            ));
        }
    }

    private static void ValidateLayerList(
        List<string>? values,
        string fieldName,
        string ruleId,
        HashSet<string> layerIds,
        string configFile,
        List<ArchitectureConfigurationError> errors
    )
    {
        if (values == null || values.Count == 0)
        {
            errors.Add(new ArchitectureConfigurationError(
                "missing_rule_layers",
                configFile,
                "denyRule '" + ruleId + "' requires " + fieldName + "."
            ));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? layer in values)
        {
            if (string.IsNullOrWhiteSpace(layer)
                || !seen.Add(layer)
                || !layerIds.Contains(layer))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "invalid_rule_layer",
                    configFile,
                    "denyRule '" + ruleId + "' has a duplicate or unknown "
                        + fieldName + " entry '" + layer + "'."
                ));
            }
        }
    }

    private static void ValidateBaseline(
        LayerBaselineDocument document,
        string configFile,
        LayerRulesDocument rules,
        List<ArchitectureConfigurationError> errors
    )
    {
        if (document.SchemaVersion != 1)
        {
            errors.Add(new ArchitectureConfigurationError(
                "unsupported_baseline_version",
                configFile,
                "layer_baseline.json schemaVersion must be 1."
            ));
        }

        var ruleIds = new HashSet<string>(
            (rules.DenyRules ?? new List<DenyRuleDefinition>())
                .Where(rule => rule != null)
                .Select(rule => rule.Id),
            StringComparer.Ordinal
        );
        var entries = new HashSet<string>(StringComparer.Ordinal);
        foreach (BaselineEntryDefinition? entry in
            document.Entries ?? new List<BaselineEntryDefinition>())
        {
            if (entry == null)
            {
                errors.Add(new ArchitectureConfigurationError(
                    "null_baseline_entry",
                    configFile,
                    "entries cannot contain null entries."
                ));
                continue;
            }
            if (string.IsNullOrWhiteSpace(entry.Rule)
                || string.IsNullOrWhiteSpace(entry.Source)
                || string.IsNullOrWhiteSpace(entry.Target))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "invalid_baseline_entry",
                    configFile,
                    "Every baseline entry requires rule, source, and target."
                ));
                continue;
            }
            if (!ruleIds.Contains(entry.Rule))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "unknown_baseline_rule",
                    configFile,
                    "Baseline entry references unknown rule '" + entry.Rule + "'."
                ));
            }
            if (!entries.Add(ArchitectureKey.Create(entry.Rule, entry.Source, entry.Target)))
            {
                errors.Add(new ArchitectureConfigurationError(
                    "duplicate_baseline_entry",
                    configFile,
                    "Baseline contains a duplicate exact symbol tuple."
                ));
            }
        }
    }

    private static void ValidateInventoryRequest(
        LayerInventoryRequestDocument document,
        string configFile,
        List<ArchitectureConfigurationError> errors
    )
    {
        if (document.SchemaVersion != 1)
        {
            errors.Add(new ArchitectureConfigurationError(
                "unsupported_inventory_request_version",
                configFile,
                "layer_inventory_request.json schemaVersion must be 1."
            ));
        }
        if (!string.Equals(document.Scope, "crossLayer", StringComparison.Ordinal))
        {
            errors.Add(new ArchitectureConfigurationError(
                "unsupported_inventory_scope",
                configFile,
                "layer_inventory_request.json scope must be 'crossLayer'."
            ));
        }
    }
}

internal sealed class ArchitectureConfigurationLoadResult
{
    internal ArchitecturePolicy? Policy { get; }
    internal ImmutableArray<ArchitectureConfigurationError> Errors { get; }
    internal bool IsSuccess => Policy != null && Errors.IsEmpty;

    private ArchitectureConfigurationLoadResult(
        ArchitecturePolicy? policy,
        ImmutableArray<ArchitectureConfigurationError> errors
    )
    {
        Policy = policy;
        Errors = errors;
    }

    internal static ArchitectureConfigurationLoadResult Success(ArchitecturePolicy policy) =>
        new ArchitectureConfigurationLoadResult(policy, ImmutableArray<ArchitectureConfigurationError>.Empty);

    internal static ArchitectureConfigurationLoadResult Failure(
        IEnumerable<ArchitectureConfigurationError> errors
    ) => new ArchitectureConfigurationLoadResult(
        null,
        errors.OrderBy(error => error.ConfigFile, StringComparer.Ordinal)
            .ThenBy(error => error.Code, StringComparer.Ordinal)
            .ThenBy(error => error.Message, StringComparer.Ordinal)
            .ToImmutableArray()
    );
}

internal sealed class ArchitectureConfigurationError
{
    internal string Code { get; }
    internal string ConfigFile { get; }
    internal string Message { get; }

    internal ArchitectureConfigurationError(string code, string configFile, string message)
    {
        Code = code;
        ConfigFile = configFile;
        Message = message;
    }
}

internal sealed class ArchitecturePolicy
{
    private readonly string _sourceRoot;
    private readonly ImmutableArray<PathMapping> _pathMappings;
    private readonly ImmutableDictionary<string, Layer> _layers;
    private readonly ImmutableDictionary<string, string> _symbolOverrides;
    private readonly ImmutableDictionary<string, ImmutableArray<DependencyRule>> _rulesByPair;
    private readonly ImmutableHashSet<string> _baseline;
    internal bool ReportCrossLayerInventory { get; }

    private ArchitecturePolicy(
        string sourceRoot,
        ImmutableArray<PathMapping> pathMappings,
        ImmutableDictionary<string, Layer> layers,
        ImmutableDictionary<string, string> symbolOverrides,
        ImmutableDictionary<string, ImmutableArray<DependencyRule>> rulesByPair,
        ImmutableHashSet<string> baseline,
        bool reportCrossLayerInventory
    )
    {
        _sourceRoot = sourceRoot;
        _pathMappings = pathMappings;
        _layers = layers;
        _symbolOverrides = symbolOverrides;
        _rulesByPair = rulesByPair;
        _baseline = baseline;
        ReportCrossLayerInventory = reportCrossLayerInventory;
    }

    internal static ArchitecturePolicy Create(
        LayerRulesDocument rulesDocument,
        LayerBaselineDocument baselineDocument,
        string rulesFilePath,
        bool reportCrossLayerInventory
    )
    {
        string rulesFullPath = Path.GetFullPath(rulesFilePath);
        string rulesDirectory = Path.GetDirectoryName(rulesFullPath)
            ?? throw new InvalidOperationException("Rules file has no directory.");
        string sourceRoot = NormalizeFullPath(Path.Combine(
            rulesDirectory,
            rulesDocument.SourceRootRelativeToRulesFile!
        ));

        var layers = (rulesDocument.Layers ?? new List<LayerDefinition>())
            .ToImmutableDictionary(
                layer => layer.Id,
                layer => new Layer(layer.Id, layer.AnalyzeOutgoingDependencies!.Value),
                StringComparer.Ordinal
            );
        var mappings = (rulesDocument.PathMappings ?? new List<PathMappingDefinition>())
            .Select(mapping => new PathMapping(mapping.Glob, mapping.Layer))
            .ToImmutableArray();
        var symbolOverrides = (rulesDocument.SymbolOverrides
                ?? new List<SymbolOverrideDefinition>())
            .ToImmutableDictionary(
                symbolOverride => symbolOverride.Symbol,
                symbolOverride => symbolOverride.Layer,
                StringComparer.Ordinal
            );

        var rulesByPair = new Dictionary<string, List<DependencyRule>>(StringComparer.Ordinal);
        foreach (DenyRuleDefinition definition in
            rulesDocument.DenyRules ?? new List<DenyRuleDefinition>())
        {
            var rule = new DependencyRule(definition.Id);
            foreach (string from in definition.FromLayers!)
            {
                foreach (string to in definition.ToLayers!)
                {
                    string pair = ArchitectureKey.CreatePair(from, to);
                    if (!rulesByPair.TryGetValue(pair, out List<DependencyRule>? pairRules))
                    {
                        pairRules = new List<DependencyRule>();
                        rulesByPair.Add(pair, pairRules);
                    }
                    pairRules.Add(rule);
                }
            }
        }

        var immutableRules = rulesByPair.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderBy(rule => rule.Id, StringComparer.Ordinal).ToImmutableArray(),
            StringComparer.Ordinal
        );
        var baseline = (baselineDocument.Entries ?? new List<BaselineEntryDefinition>())
            .Select(entry => ArchitectureKey.Create(entry.Rule, entry.Source, entry.Target))
            .ToImmutableHashSet(StringComparer.Ordinal);

        return new ArchitecturePolicy(
            sourceRoot,
            mappings,
            layers,
            symbolOverrides,
            immutableRules,
            baseline,
            reportCrossLayerInventory
        );
    }

    internal string? GetSourceLayer(ISymbol sourceSymbol, Location location)
    {
        if (TryGetOverride(sourceSymbol, out string layer))
            return layer;
        return location.SourceTree == null ? null : ClassifyPath(location.SourceTree.FilePath);
    }

    internal ImmutableArray<string> GetDeclaredLayers(ISymbol symbol)
    {
        if (TryGetOverride(symbol, out string overrideLayer))
            return ImmutableArray.Create(overrideLayer);

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (Location location in symbol.Locations)
        {
            if (!location.IsInSource || location.SourceTree == null)
                continue;
            string? layer = ClassifyPath(location.SourceTree.FilePath);
            if (layer != null)
                result.Add(layer);
        }

        if (result.Count == 0 && symbol.ContainingType != null
            && !SymbolEqualityComparer.Default.Equals(symbol, symbol.ContainingType))
        {
            return GetDeclaredLayers(symbol.ContainingType);
        }

        return result.OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray();
    }

    internal bool AnalyzeOutgoing(string layer) =>
        _layers.TryGetValue(layer, out Layer? value) && value.AnalyzeOutgoingDependencies;

    internal ImmutableArray<DependencyRule> GetDenyRules(string fromLayer, string toLayer) =>
        _rulesByPair.TryGetValue(
            ArchitectureKey.CreatePair(fromLayer, toLayer),
            out ImmutableArray<DependencyRule> rules
        ) ? rules : ImmutableArray<DependencyRule>.Empty;

    internal bool IsBaseline(string ruleId, string sourceId, string targetId) =>
        _baseline.Contains(ArchitectureKey.Create(ruleId, sourceId, targetId));

    internal string? GetUnclassifiedSourcePath(string filePath)
    {
        string? relativePath = GetRepositoryRelativePath(filePath);
        if (relativePath == null || ClassifyPath(filePath) != null)
            return null;
        return relativePath;
    }

    internal string GetStablePath(Location location)
    {
        if (location.SourceTree == null)
            return string.Empty;
        return GetRepositoryRelativePath(location.SourceTree.FilePath)
            ?? NormalizeFullPath(location.SourceTree.FilePath);
    }

    private bool TryGetOverride(ISymbol symbol, out string layer)
    {
        string? symbolId = ArchitectureSymbolIdentity.GetDocumentationId(symbol);
        if (symbolId != null
            && _symbolOverrides.TryGetValue(symbolId, out string? symbolLayer)
            && symbolLayer != null)
        {
            layer = symbolLayer;
            return true;
        }

        INamedTypeSymbol? containingType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (containingType != null)
        {
            string? typeId = ArchitectureSymbolIdentity.GetDocumentationId(containingType);
            if (typeId != null
                && _symbolOverrides.TryGetValue(typeId, out string? typeLayer)
                && typeLayer != null)
            {
                layer = typeLayer;
                return true;
            }
        }

        layer = string.Empty;
        return false;
    }

    private string? ClassifyPath(string filePath)
    {
        string? relativePath = GetRepositoryRelativePath(filePath);
        if (relativePath == null)
            return null;
        foreach (PathMapping mapping in _pathMappings)
        {
            if (mapping.IsMatch(relativePath))
                return mapping.Layer;
        }
        return null;
    }

    private string? GetRepositoryRelativePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;
        string fullPath;
        try
        {
            fullPath = NormalizeFullPath(filePath);
        }
        catch
        {
            return null;
        }

        if (string.Equals(fullPath, _sourceRoot, PathComparison))
            return string.Empty;
        string prefix = _sourceRoot + "/";
        return fullPath.StartsWith(prefix, PathComparison)
            ? fullPath.Substring(prefix.Length)
            : null;
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
}

internal sealed class Layer
{
    internal string Id { get; }
    internal bool AnalyzeOutgoingDependencies { get; }

    internal Layer(string id, bool analyzeOutgoingDependencies)
    {
        Id = id;
        AnalyzeOutgoingDependencies = analyzeOutgoingDependencies;
    }
}

internal sealed class DependencyRule
{
    internal string Id { get; }

    internal DependencyRule(string id)
    {
        Id = id;
    }
}

internal sealed class PathMapping
{
    private readonly Regex _regex;
    internal string Layer { get; }

    internal PathMapping(string glob, string layer)
    {
        Layer = layer;
        _regex = new Regex(GlobToRegex(glob), RegexOptions.CultureInvariant);
    }

    internal bool IsMatch(string path) => _regex.IsMatch(path);

    private static string GlobToRegex(string glob)
    {
        string normalized = glob.Replace('\\', '/');
        var builder = new StringBuilder("^");
        for (int index = 0; index < normalized.Length; index++)
        {
            char current = normalized[index];
            if (current == '*')
            {
                bool isDouble = index + 1 < normalized.Length && normalized[index + 1] == '*';
                if (!isDouble)
                {
                    builder.Append("[^/]*");
                    continue;
                }

                bool followedBySlash = index + 2 < normalized.Length
                    && normalized[index + 2] == '/';
                builder.Append(followedBySlash ? "(?:.*/)?" : ".*");
                index += followedBySlash ? 2 : 1;
                continue;
            }
            if (current == '?')
            {
                builder.Append("[^/]");
                continue;
            }
            builder.Append(Regex.Escape(current.ToString()));
        }
        builder.Append('$');
        return builder.ToString();
    }
}

internal static class ArchitectureKey
{
    private const string Separator = "\u001f";

    internal static string Create(string ruleId, string sourceId, string targetId) =>
        ruleId + Separator + sourceId + Separator + targetId;

    internal static string CreatePair(string fromLayer, string toLayer) =>
        fromLayer + Separator + toLayer;

    internal static string CreateInventory(
        string sourceLayer,
        string targetLayer,
        string sourceId,
        string targetId
    ) => sourceLayer + Separator + targetLayer + Separator + sourceId + Separator + targetId;
}

[DataContract]
internal sealed class LayerRulesDocument
{
    [DataMember(Name = "schemaVersion")]
    internal int SchemaVersion { get; set; }

    [DataMember(Name = "sourceRootRelativeToRulesFile")]
    internal string? SourceRootRelativeToRulesFile { get; set; }

    [DataMember(Name = "layers")]
    internal List<LayerDefinition>? Layers { get; set; }

    [DataMember(Name = "pathMappings")]
    internal List<PathMappingDefinition>? PathMappings { get; set; }

    [DataMember(Name = "symbolOverrides", EmitDefaultValue = false)]
    internal List<SymbolOverrideDefinition>? SymbolOverrides { get; set; }

    [DataMember(Name = "denyRules")]
    internal List<DenyRuleDefinition>? DenyRules { get; set; }
}

[DataContract]
internal sealed class LayerDefinition
{
    [DataMember(Name = "id")]
    internal string Id { get; set; } = string.Empty;

    [DataMember(Name = "analyzeOutgoingDependencies")]
    internal bool? AnalyzeOutgoingDependencies { get; set; }
}

[DataContract]
internal sealed class PathMappingDefinition
{
    [DataMember(Name = "glob")]
    internal string Glob { get; set; } = string.Empty;

    [DataMember(Name = "layer")]
    internal string Layer { get; set; } = string.Empty;
}

[DataContract]
internal sealed class SymbolOverrideDefinition
{
    [DataMember(Name = "symbol")]
    internal string Symbol { get; set; } = string.Empty;

    [DataMember(Name = "layer")]
    internal string Layer { get; set; } = string.Empty;
}

[DataContract]
internal sealed class DenyRuleDefinition
{
    [DataMember(Name = "id")]
    internal string Id { get; set; } = string.Empty;

    [DataMember(Name = "fromLayers")]
    internal List<string>? FromLayers { get; set; }

    [DataMember(Name = "toLayers")]
    internal List<string>? ToLayers { get; set; }
}

[DataContract]
internal sealed class LayerBaselineDocument
{
    [DataMember(Name = "schemaVersion")]
    internal int SchemaVersion { get; set; }

    [DataMember(Name = "entries")]
    internal List<BaselineEntryDefinition>? Entries { get; set; }
}

[DataContract]
internal sealed class LayerInventoryRequestDocument
{
    [DataMember(Name = "schemaVersion")]
    internal int SchemaVersion { get; set; }

    [DataMember(Name = "scope")]
    internal string Scope { get; set; } = string.Empty;
}

[DataContract]
internal sealed class BaselineEntryDefinition
{
    [DataMember(Name = "rule")]
    internal string Rule { get; set; } = string.Empty;

    [DataMember(Name = "source")]
    internal string Source { get; set; } = string.Empty;

    [DataMember(Name = "target")]
    internal string Target { get; set; } = string.Empty;

    [DataMember(Name = "note", EmitDefaultValue = false)]
    internal string? Note { get; set; }
}
