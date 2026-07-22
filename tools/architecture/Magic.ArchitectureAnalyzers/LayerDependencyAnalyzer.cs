using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Magic.ArchitectureAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LayerDependencyAnalyzer : DiagnosticAnalyzer
{
    public const string ForbiddenDependencyId = "MAGICARCH001";
    public const string AmbiguousPartialOwnershipId = "MAGICARCH002";
    public const string UnclassifiedSourceId = "MAGICARCH003";
    public const string InventoryDependencyId = "MAGICARCH100";
    public const string ConfigurationErrorId = "MAGICARCH900";

    internal static readonly DiagnosticDescriptor ForbiddenDependency = new(
        ForbiddenDependencyId,
        "Forbidden architecture dependency",
        "Rule '{0}' forbids '{1}' ({2}) from depending on '{3}' ({4}) via {5}",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd }
    );

    internal static readonly DiagnosticDescriptor AmbiguousPartialOwnership = new(
        AmbiguousPartialOwnershipId,
        "Partial type spans multiple architecture layers",
        "Type '{0}' is declared in multiple architecture layers: {1}",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd }
    );

    internal static readonly DiagnosticDescriptor ConfigurationError = new(
        ConfigurationErrorId,
        "Architecture analyzer configuration error",
        "Configuration '{0}': {1}",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd }
    );

    internal static readonly DiagnosticDescriptor UnclassifiedSource = new(
        UnclassifiedSourceId,
        "Source file is not assigned to an architecture layer",
        "Source file '{0}' is under sourceRoot but does not match any pathMapping",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd }
    );

    internal static readonly DiagnosticDescriptor InventoryDependency = new(
        InventoryDependencyId,
        "Cross-layer architecture dependency inventory",
        "'{0}' ({1}) depends on '{2}' ({3}); references: {4}; matched rules: {5}; baselined rules: {6}",
        "Architecture",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd }
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            ForbiddenDependency,
            AmbiguousPartialOwnership,
            UnclassifiedSource,
            InventoryDependency,
            ConfigurationError
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            ArchitectureConfigurationLoadResult loadResult =
                ArchitectureConfigurationLoader.Load(
                    startContext.Options.AdditionalFiles,
                    startContext.CancellationToken
                );
            if (!loadResult.IsSuccess || loadResult.Policy == null)
            {
                startContext.RegisterCompilationEndAction(endContext =>
                {
                    foreach (ArchitectureConfigurationError error in loadResult.Errors)
                    {
                        var properties = ImmutableDictionary<string, string?>.Empty
                            .Add("ConfigFile", error.ConfigFile)
                            .Add("ConfigErrorCode", error.Code);
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            ConfigurationError,
                            Location.None,
                            properties,
                            error.ConfigFile,
                            error.Message
                        ));
                    }
                });
                return;
            }

            var collector = new DependencyCollector(loadResult.Policy);
            startContext.RegisterSyntaxTreeAction(
                treeContext => collector.RecordCompilationSource(
                    treeContext.Tree,
                    treeContext.CancellationToken
                )
            );
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeSymbol(symbolContext, collector),
                SymbolKind.NamedType,
                SymbolKind.Method,
                SymbolKind.Property,
                SymbolKind.Field,
                SymbolKind.Event
            );
            startContext.RegisterOperationBlockAction(
                operationContext => AnalyzeOperationBlock(operationContext, collector)
            );
            startContext.RegisterCompilationEndAction(collector.ReportDiagnostics);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        DependencyCollector collector
    )
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        ISymbol source = context.Symbol;
        if (source is INamedTypeSymbol namedType)
            collector.RecordPartialOwnership(namedType);

        ImmutableArray<Location> locations = source.Locations
            .Where(location => location.IsInSource && location.SourceTree != null)
            .ToImmutableArray();
        if (locations.IsEmpty)
            return;

        foreach (Location location in locations)
        {
            AnalyzeAttributes(source, source.GetAttributes(), location, collector);
            switch (source)
            {
                case INamedTypeSymbol type:
                    ReportType(source, type.BaseType, location, "base type", collector);
                    foreach (INamedTypeSymbol interfaceType in type.Interfaces)
                        ReportType(source, interfaceType, location, "interface", collector);
                    AnalyzeTypeParameterConstraints(
                        source,
                        type.TypeParameters,
                        location,
                        collector
                    );
                    break;
                case IMethodSymbol method:
                    ReportType(source, method.ReturnType, location, "return type", collector);
                    foreach (IParameterSymbol parameter in method.Parameters)
                    {
                        ReportType(source, parameter.Type, location, "parameter type", collector);
                        AnalyzeAttributes(source, parameter.GetAttributes(), location, collector);
                    }
                    AnalyzeAttributes(
                        source,
                        method.GetReturnTypeAttributes(),
                        location,
                        collector
                    );
                    AnalyzeTypeParameterConstraints(
                        source,
                        method.TypeParameters,
                        location,
                        collector
                    );
                    break;
                case IPropertySymbol property:
                    ReportType(source, property.Type, location, "property type", collector);
                    foreach (IParameterSymbol parameter in property.Parameters)
                        ReportType(source, parameter.Type, location, "indexer parameter", collector);
                    break;
                case IFieldSymbol field:
                    ReportType(source, field.Type, location, "field type", collector);
                    break;
                case IEventSymbol eventSymbol:
                    ReportType(source, eventSymbol.Type, location, "event type", collector);
                    break;
            }
        }
    }

    private static void AnalyzeAttributes(
        ISymbol source,
        ImmutableArray<AttributeData> attributes,
        Location location,
        DependencyCollector collector
    )
    {
        foreach (AttributeData attribute in attributes)
        {
            ReportType(source, attribute.AttributeClass, location, "attribute", collector);
        }
    }

    private static void AnalyzeTypeParameterConstraints(
        ISymbol source,
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        Location location,
        DependencyCollector collector
    )
    {
        foreach (ITypeParameterSymbol typeParameter in typeParameters)
        {
            foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
                ReportType(source, constraintType, location, "generic constraint", collector);
        }
    }

    private static void AnalyzeOperationBlock(
        OperationBlockAnalysisContext context,
        DependencyCollector collector
    )
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var walker = new DependencyOperationWalker(
            context.OwningSymbol,
            collector,
            context.CancellationToken
        );
        foreach (IOperation operationBlock in context.OperationBlocks)
            walker.Visit(operationBlock);
    }

    internal static void ReportType(
        ISymbol source,
        ITypeSymbol? type,
        Location location,
        string referenceKind,
        DependencyCollector collector
    )
    {
        if (type == null)
            return;

        switch (type)
        {
            case IArrayTypeSymbol arrayType:
                ReportType(source, arrayType.ElementType, location, referenceKind, collector);
                return;
            case IPointerTypeSymbol pointerType:
                ReportType(source, pointerType.PointedAtType, location, referenceKind, collector);
                return;
            case IFunctionPointerTypeSymbol functionPointer:
                ReportType(
                    source,
                    functionPointer.Signature.ReturnType,
                    location,
                    referenceKind,
                    collector
                );
                foreach (IParameterSymbol parameter in functionPointer.Signature.Parameters)
                    ReportType(source, parameter.Type, location, referenceKind, collector);
                return;
            case INamedTypeSymbol namedType:
                collector.AddDependency(source, namedType.OriginalDefinition, location, referenceKind);
                ReportConstructedTypeArguments(
                    source,
                    namedType,
                    location,
                    "generic argument",
                    collector
                );
                return;
        }
    }

    internal static void ReportConstructedTypeArguments(
        ISymbol source,
        INamedTypeSymbol? constructedType,
        Location location,
        string referenceKind,
        DependencyCollector collector
    )
    {
        if (constructedType == null)
            return;
        ReportConstructedTypeArguments(
            source,
            constructedType.ContainingType,
            location,
            referenceKind,
            collector
        );
        foreach (ITypeSymbol typeArgument in constructedType.TypeArguments)
        {
            if (typeArgument.TypeKind != TypeKind.TypeParameter)
                ReportType(source, typeArgument, location, referenceKind, collector);
        }
    }
}

internal sealed class DependencyOperationWalker : OperationWalker
{
    private readonly ISymbol _source;
    private readonly DependencyCollector _collector;
    private readonly System.Threading.CancellationToken _cancellationToken;

    internal DependencyOperationWalker(
        ISymbol source,
        DependencyCollector collector,
        System.Threading.CancellationToken cancellationToken
    )
    {
        _source = source;
        _collector = collector;
        _cancellationToken = cancellationToken;
    }

    public override void VisitInvocation(IInvocationOperation operation)
    {
        CheckCancellation();
        IMethodSymbol method = operation.TargetMethod.ReducedFrom ?? operation.TargetMethod;
        _collector.AddDependency(_source, method.OriginalDefinition, operation.Syntax.GetLocation(), "invocation");
        LayerDependencyAnalyzer.ReportConstructedTypeArguments(
            _source,
            operation.TargetMethod.ContainingType,
            operation.Syntax.GetLocation(),
            "invocation containing generic argument",
            _collector
        );
        ReportMethodTypeArguments(operation.TargetMethod, operation.Syntax.GetLocation());
        base.VisitInvocation(operation);
    }

    public override void VisitObjectCreation(IObjectCreationOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.Type,
            operation.Syntax.GetLocation(),
            "object creation",
            _collector
        );
        base.VisitObjectCreation(operation);
    }

    public override void VisitFieldReference(IFieldReferenceOperation operation)
    {
        CheckCancellation();
        _collector.AddDependency(
            _source,
            operation.Field.OriginalDefinition,
            operation.Syntax.GetLocation(),
            operation.Field.IsConst ? "constant reference" : "field reference"
        );
        ReportMemberContainingTypeArguments(operation.Field, operation.Syntax.GetLocation());
        base.VisitFieldReference(operation);
    }

    public override void VisitPropertyReference(IPropertyReferenceOperation operation)
    {
        CheckCancellation();
        _collector.AddDependency(
            _source,
            operation.Property.OriginalDefinition,
            operation.Syntax.GetLocation(),
            "property reference"
        );
        ReportMemberContainingTypeArguments(operation.Property, operation.Syntax.GetLocation());
        base.VisitPropertyReference(operation);
    }

    public override void VisitEventReference(IEventReferenceOperation operation)
    {
        CheckCancellation();
        _collector.AddDependency(
            _source,
            operation.Event.OriginalDefinition,
            operation.Syntax.GetLocation(),
            "event reference"
        );
        ReportMemberContainingTypeArguments(operation.Event, operation.Syntax.GetLocation());
        base.VisitEventReference(operation);
    }

    public override void VisitMethodReference(IMethodReferenceOperation operation)
    {
        CheckCancellation();
        IMethodSymbol method = operation.Method.ReducedFrom ?? operation.Method;
        _collector.AddDependency(
            _source,
            method.OriginalDefinition,
            operation.Syntax.GetLocation(),
            "method reference"
        );
        ReportMemberContainingTypeArguments(operation.Method, operation.Syntax.GetLocation());
        ReportMethodTypeArguments(operation.Method, operation.Syntax.GetLocation());
        base.VisitMethodReference(operation);
    }

    public override void VisitConversion(IConversionOperation operation)
    {
        CheckCancellation();
        if (operation.OperatorMethod != null)
        {
            _collector.AddDependency(
                _source,
                operation.OperatorMethod.OriginalDefinition,
                operation.Syntax.GetLocation(),
                "conversion operator"
            );
            ReportMemberContainingTypeArguments(
                operation.OperatorMethod,
                operation.Syntax.GetLocation()
            );
        }
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.Type,
            operation.Syntax.GetLocation(),
            "conversion type",
            _collector
        );
        base.VisitConversion(operation);
    }

    public override void VisitTypeOf(ITypeOfOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.TypeOperand,
            operation.Syntax.GetLocation(),
            "typeof",
            _collector
        );
        base.VisitTypeOf(operation);
    }

    public override void VisitNameOf(INameOfOperation operation)
    {
        CheckCancellation();
        Location location = operation.Argument.Syntax.GetLocation();
        bool resolvedSymbol = false;
        SemanticModel? semanticModel = operation.SemanticModel;
        if (semanticModel != null)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(
                operation.Argument.Syntax,
                _cancellationToken
            );
            if (symbolInfo.Symbol != null)
            {
                ReportNameOfSymbol(symbolInfo.Symbol, location);
                resolvedSymbol = true;
            }
            else if (!symbolInfo.CandidateSymbols.IsEmpty)
            {
                foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
                    ReportNameOfSymbol(candidate, location);
                resolvedSymbol = true;
            }
        }
        if (!resolvedSymbol)
        {
            LayerDependencyAnalyzer.ReportType(
                _source,
                operation.Argument.Type,
                location,
                "nameof",
                _collector
            );
        }
        base.VisitNameOf(operation);
    }

    public override void VisitIsType(IIsTypeOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.TypeOperand,
            operation.Syntax.GetLocation(),
            "type test",
            _collector
        );
        base.VisitIsType(operation);
    }

    public override void VisitDeclarationPattern(IDeclarationPatternOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.MatchedType,
            operation.Syntax.GetLocation(),
            "declaration pattern",
            _collector
        );
        base.VisitDeclarationPattern(operation);
    }

    public override void VisitVariableDeclaration(IVariableDeclarationOperation operation)
    {
        CheckCancellation();
        foreach (IVariableDeclaratorOperation declarator in operation.Declarators)
        {
            LayerDependencyAnalyzer.ReportType(
                _source,
                declarator.Symbol.Type,
                operation.Syntax.GetLocation(),
                "local variable type",
                _collector
            );
        }
        base.VisitVariableDeclaration(operation);
    }

    public override void VisitArrayCreation(IArrayCreationOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.Type,
            operation.Syntax.GetLocation(),
            "array creation",
            _collector
        );
        base.VisitArrayCreation(operation);
    }

    public override void VisitDefaultValue(IDefaultValueOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.Type,
            operation.Syntax.GetLocation(),
            "default value",
            _collector
        );
        base.VisitDefaultValue(operation);
    }

    public override void VisitSizeOf(ISizeOfOperation operation)
    {
        CheckCancellation();
        LayerDependencyAnalyzer.ReportType(
            _source,
            operation.TypeOperand,
            operation.Syntax.GetLocation(),
            "sizeof",
            _collector
        );
        base.VisitSizeOf(operation);
    }

    private void ReportNameOfSymbol(ISymbol symbol, Location location)
    {
        ISymbol target = symbol is IAliasSymbol alias ? alias.Target : symbol;
        if (target is IMethodSymbol method)
        {
            LayerDependencyAnalyzer.ReportType(
                _source,
                method.ContainingType,
                location,
                "nameof method group",
                _collector
            );
            return;
        }
        if (target is ITypeSymbol type)
        {
            LayerDependencyAnalyzer.ReportType(
                _source,
                type,
                location,
                "nameof",
                _collector
            );
            return;
        }
        if (target.Kind != SymbolKind.Namespace)
        {
            _collector.AddDependency(_source, target, location, "nameof");
            ReportMemberContainingTypeArguments(target, location);
        }
    }

    private void ReportMemberContainingTypeArguments(ISymbol member, Location location) =>
        LayerDependencyAnalyzer.ReportConstructedTypeArguments(
            _source,
            member.ContainingType,
            location,
            "member containing generic argument",
            _collector
        );

    private void ReportMethodTypeArguments(IMethodSymbol method, Location location)
    {
        foreach (ITypeSymbol typeArgument in method.TypeArguments)
        {
            if (typeArgument.TypeKind != TypeKind.TypeParameter)
            {
                LayerDependencyAnalyzer.ReportType(
                    _source,
                    typeArgument,
                    location,
                    "generic method argument",
                    _collector
                );
            }
        }
    }

    private void CheckCancellation() => _cancellationToken.ThrowIfCancellationRequested();
}

internal sealed class DependencyCollector
{
    private readonly ArchitecturePolicy _policy;
    private readonly object _gate = new();
    private readonly Dictionary<string, DependencyFinding> _findings =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, PartialOwnershipFinding> _partialOwnership =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Location> _unclassifiedSources =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, DependencyInventoryFinding> _inventoryFindings =
        new(StringComparer.Ordinal);

    internal DependencyCollector(ArchitecturePolicy policy)
    {
        _policy = policy;
    }

    internal void RecordCompilationSource(
        SyntaxTree syntaxTree,
        System.Threading.CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? relativePath = _policy.GetUnclassifiedSourcePath(syntaxTree.FilePath);
        if (relativePath == null)
            return;
        Location location = syntaxTree.GetLocation(new TextSpan(0, 0));
        lock (_gate)
            _unclassifiedSources[relativePath] = location;
    }

    internal void AddDependency(
        ISymbol sourceSymbol,
        ISymbol targetSymbol,
        Location location,
        string referenceKind
    )
    {
        if (!location.IsInSource || location.SourceTree == null)
            return;

        ISymbol source = ArchitectureSymbolIdentity.NormalizeSource(sourceSymbol);
        ISymbol target = ArchitectureSymbolIdentity.NormalizeTarget(targetSymbol);
        string? sourceId = ArchitectureSymbolIdentity.GetDocumentationId(source);
        string? targetId = ArchitectureSymbolIdentity.GetDocumentationId(target);
        if (sourceId == null || targetId == null || string.Equals(sourceId, targetId, StringComparison.Ordinal))
            return;

        string? sourceLayer = _policy.GetSourceLayer(source, location);
        if (sourceLayer == null || !_policy.AnalyzeOutgoing(sourceLayer))
            return;

        ImmutableArray<string> targetLayers = _policy.GetDeclaredLayers(target);
        foreach (string targetLayer in targetLayers)
        {
            ImmutableArray<DependencyRule> rules = _policy.GetDenyRules(
                sourceLayer,
                targetLayer
            );
            if (_policy.ReportCrossLayerInventory
                && !string.Equals(sourceLayer, targetLayer, StringComparison.Ordinal))
            {
                RecordInventoryDependency(
                    source,
                    target,
                    sourceId,
                    targetId,
                    sourceLayer,
                    targetLayer,
                    rules,
                    referenceKind,
                    location
                );
            }
            foreach (DependencyRule rule in rules)
            {
                string key = ArchitectureKey.Create(rule.Id, sourceId, targetId);
                var finding = new DependencyFinding(
                    key,
                    rule.Id,
                    sourceId,
                    targetId,
                    sourceLayer,
                    targetLayer,
                    source.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    target.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    referenceKind,
                    location,
                    _policy.GetStablePath(location)
                );
                lock (_gate)
                {
                    if (!_findings.TryGetValue(key, out DependencyFinding? existing)
                        || finding.IsEarlierThan(existing))
                    {
                        _findings[key] = finding;
                    }
                }
            }
        }
    }

    private void RecordInventoryDependency(
        ISymbol source,
        ISymbol target,
        string sourceId,
        string targetId,
        string sourceLayer,
        string targetLayer,
        ImmutableArray<DependencyRule> rules,
        string referenceKind,
        Location location
    )
    {
        string key = ArchitectureKey.CreateInventory(
            sourceLayer,
            targetLayer,
            sourceId,
            targetId
        );
        ImmutableArray<string> matchedRuleIds = rules
            .Select(rule => rule.Id)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToImmutableArray();
        ImmutableArray<string> baselinedRuleIds = rules
            .Where(rule => _policy.IsBaseline(rule.Id, sourceId, targetId))
            .Select(rule => rule.Id)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToImmutableArray();
        string stablePath = _policy.GetStablePath(location);
        lock (_gate)
        {
            if (!_inventoryFindings.TryGetValue(
                key,
                out DependencyInventoryFinding? finding
            ))
            {
                finding = new DependencyInventoryFinding(
                    key,
                    sourceId,
                    targetId,
                    sourceLayer,
                    targetLayer,
                    source.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    target.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    matchedRuleIds,
                    baselinedRuleIds,
                    referenceKind,
                    location,
                    stablePath
                );
                _inventoryFindings.Add(key, finding);
                return;
            }
            finding.MergeReference(referenceKind, location, stablePath);
        }
    }

    internal void RecordPartialOwnership(INamedTypeSymbol type)
    {
        ImmutableArray<string> layers = _policy.GetDeclaredLayers(type);
        if (layers.Length <= 1)
            return;

        INamedTypeSymbol normalized = type.OriginalDefinition;
        string? symbolId = ArchitectureSymbolIdentity.GetDocumentationId(normalized);
        if (symbolId == null)
            return;
        Location? location = normalized.Locations
            .Where(candidate => candidate.IsInSource && candidate.SourceTree != null)
            .OrderBy(candidate => _policy.GetStablePath(candidate), StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SourceSpan.Start)
            .FirstOrDefault();
        if (location == null)
            return;

        var finding = new PartialOwnershipFinding(
            symbolId,
            normalized.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            layers,
            location,
            _policy.GetStablePath(location)
        );
        lock (_gate)
        {
            _partialOwnership[symbolId] = finding;
        }
    }

    internal void ReportDiagnostics(CompilationAnalysisContext context)
    {
        ImmutableArray<PartialOwnershipFinding> partialFindings;
        ImmutableArray<DependencyFinding> dependencyFindings;
        ImmutableArray<KeyValuePair<string, Location>> unclassifiedSources;
        ImmutableArray<DependencyInventoryFinding> inventoryFindings;
        lock (_gate)
        {
            unclassifiedSources = _unclassifiedSources
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToImmutableArray();
            inventoryFindings = _inventoryFindings.Values
                .OrderBy(finding => finding.SourceLayer, StringComparer.Ordinal)
                .ThenBy(finding => finding.TargetLayer, StringComparer.Ordinal)
                .ThenBy(finding => finding.SourceId, StringComparer.Ordinal)
                .ThenBy(finding => finding.TargetId, StringComparer.Ordinal)
                .ToImmutableArray();
            partialFindings = _partialOwnership.Values
                .OrderBy(finding => finding.SymbolId, StringComparer.Ordinal)
                .ToImmutableArray();
            dependencyFindings = _findings.Values
                .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.SourceId, StringComparer.Ordinal)
                .ThenBy(finding => finding.TargetId, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        foreach (KeyValuePair<string, Location> entry in unclassifiedSources)
        {
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("SourcePath", entry.Key);
            context.ReportDiagnostic(Diagnostic.Create(
                LayerDependencyAnalyzer.UnclassifiedSource,
                entry.Value,
                properties,
                entry.Key
            ));
        }

        foreach (PartialOwnershipFinding finding in partialFindings)
        {
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("Symbol", finding.SymbolId)
                .Add("Layers", string.Join(",", finding.Layers));
            context.ReportDiagnostic(Diagnostic.Create(
                LayerDependencyAnalyzer.AmbiguousPartialOwnership,
                finding.Location,
                properties,
                finding.SymbolDisplay,
                string.Join(", ", finding.Layers)
            ));
        }

        foreach (DependencyInventoryFinding finding in inventoryFindings)
        {
            ImmutableArray<string> referenceKinds = finding.GetReferenceKinds();
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("SourceSymbol", finding.SourceId)
                .Add("TargetSymbol", finding.TargetId)
                .Add("SourceLayer", finding.SourceLayer)
                .Add("TargetLayer", finding.TargetLayer)
                .Add("SourcePath", finding.SourcePath)
                .Add(
                    "ReferenceKindsJson",
                    DiagnosticPropertyJson.StringArray(referenceKinds)
                )
                .Add(
                    "MatchedRuleIdsJson",
                    DiagnosticPropertyJson.StringArray(finding.MatchedRuleIds)
                )
                .Add(
                    "BaselinedRuleIdsJson",
                    DiagnosticPropertyJson.StringArray(finding.BaselinedRuleIds)
                );
            context.ReportDiagnostic(Diagnostic.Create(
                LayerDependencyAnalyzer.InventoryDependency,
                finding.Location,
                properties,
                finding.SourceDisplay,
                finding.SourceLayer,
                finding.TargetDisplay,
                finding.TargetLayer,
                string.Join(", ", referenceKinds),
                finding.MatchedRuleIds.IsEmpty
                    ? "none"
                    : string.Join(", ", finding.MatchedRuleIds),
                finding.BaselinedRuleIds.IsEmpty
                    ? "none"
                    : string.Join(", ", finding.BaselinedRuleIds)
            ));
        }

        foreach (DependencyFinding finding in dependencyFindings)
        {
            if (_policy.IsBaseline(finding.RuleId, finding.SourceId, finding.TargetId))
                continue;
            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("RuleId", finding.RuleId)
                .Add("SourceSymbol", finding.SourceId)
                .Add("TargetSymbol", finding.TargetId)
                .Add("SourceLayer", finding.SourceLayer)
                .Add("TargetLayer", finding.TargetLayer)
                .Add("ReferenceKind", finding.ReferenceKind);
            context.ReportDiagnostic(Diagnostic.Create(
                LayerDependencyAnalyzer.ForbiddenDependency,
                finding.Location,
                properties,
                finding.RuleId,
                finding.SourceDisplay,
                finding.SourceLayer,
                finding.TargetDisplay,
                finding.TargetLayer,
                finding.ReferenceKind
            ));
        }
    }
}

internal static class ArchitectureSymbolIdentity
{
    internal static ISymbol NormalizeSource(ISymbol symbol)
    {
        ISymbol current = symbol;
        while (current is IMethodSymbol method)
        {
            if (method.AssociatedSymbol != null)
            {
                current = method.AssociatedSymbol;
                break;
            }
            if (method.MethodKind == MethodKind.AnonymousFunction
                || method.MethodKind == MethodKind.LocalFunction)
            {
                if (method.ContainingSymbol == null)
                    break;
                current = method.ContainingSymbol;
                continue;
            }
            break;
        }
        return current.OriginalDefinition;
    }

    internal static ISymbol NormalizeTarget(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            IMethodSymbol normalizedMethod = method.ReducedFrom ?? method;
            if (normalizedMethod.AssociatedSymbol != null)
                return normalizedMethod.AssociatedSymbol.OriginalDefinition;
            return normalizedMethod.OriginalDefinition;
        }
        return symbol.OriginalDefinition;
    }

    internal static string? GetDocumentationId(ISymbol symbol) =>
        DocumentationCommentId.CreateDeclarationId(symbol.OriginalDefinition);
}

internal sealed class DependencyFinding
{
    internal string Key { get; }
    internal string RuleId { get; }
    internal string SourceId { get; }
    internal string TargetId { get; }
    internal string SourceLayer { get; }
    internal string TargetLayer { get; }
    internal string SourceDisplay { get; }
    internal string TargetDisplay { get; }
    internal string ReferenceKind { get; }
    internal Location Location { get; }
    private string StablePath { get; }

    internal DependencyFinding(
        string key,
        string ruleId,
        string sourceId,
        string targetId,
        string sourceLayer,
        string targetLayer,
        string sourceDisplay,
        string targetDisplay,
        string referenceKind,
        Location location,
        string stablePath
    )
    {
        Key = key;
        RuleId = ruleId;
        SourceId = sourceId;
        TargetId = targetId;
        SourceLayer = sourceLayer;
        TargetLayer = targetLayer;
        SourceDisplay = sourceDisplay;
        TargetDisplay = targetDisplay;
        ReferenceKind = referenceKind;
        Location = location;
        StablePath = stablePath;
    }

    internal bool IsEarlierThan(DependencyFinding other)
    {
        int pathComparison = string.Compare(StablePath, other.StablePath, StringComparison.Ordinal);
        if (pathComparison != 0)
            return pathComparison < 0;
        if (Location.SourceSpan.Start != other.Location.SourceSpan.Start)
            return Location.SourceSpan.Start < other.Location.SourceSpan.Start;
        return Location.SourceSpan.Length < other.Location.SourceSpan.Length;
    }
}

internal sealed class DependencyInventoryFinding
{
    private readonly SortedSet<string> _referenceKinds = new(StringComparer.Ordinal);

    internal string Key { get; }
    internal string SourceId { get; }
    internal string TargetId { get; }
    internal string SourceLayer { get; }
    internal string TargetLayer { get; }
    internal string SourceDisplay { get; }
    internal string TargetDisplay { get; }
    internal ImmutableArray<string> MatchedRuleIds { get; }
    internal ImmutableArray<string> BaselinedRuleIds { get; }
    internal Location Location { get; private set; }
    internal string SourcePath { get; private set; }

    internal DependencyInventoryFinding(
        string key,
        string sourceId,
        string targetId,
        string sourceLayer,
        string targetLayer,
        string sourceDisplay,
        string targetDisplay,
        ImmutableArray<string> matchedRuleIds,
        ImmutableArray<string> baselinedRuleIds,
        string referenceKind,
        Location location,
        string sourcePath
    )
    {
        Key = key;
        SourceId = sourceId;
        TargetId = targetId;
        SourceLayer = sourceLayer;
        TargetLayer = targetLayer;
        SourceDisplay = sourceDisplay;
        TargetDisplay = targetDisplay;
        MatchedRuleIds = matchedRuleIds;
        BaselinedRuleIds = baselinedRuleIds;
        Location = location;
        SourcePath = sourcePath;
        _referenceKinds.Add(referenceKind);
    }

    internal void MergeReference(string referenceKind, Location location, string sourcePath)
    {
        _referenceKinds.Add(referenceKind);
        int pathComparison = string.Compare(sourcePath, SourcePath, StringComparison.Ordinal);
        if (pathComparison > 0)
            return;
        if (pathComparison == 0
            && (location.SourceSpan.Start > Location.SourceSpan.Start
                || (location.SourceSpan.Start == Location.SourceSpan.Start
                    && location.SourceSpan.Length >= Location.SourceSpan.Length)))
        {
            return;
        }
        Location = location;
        SourcePath = sourcePath;
    }

    internal ImmutableArray<string> GetReferenceKinds() =>
        _referenceKinds.ToImmutableArray();
}

internal static class DiagnosticPropertyJson
{
    internal static string StringArray(IEnumerable<string> values) =>
        "[" + string.Join(",", values.Select(value => "\"" + Escape(value) + "\"")) + "]";

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
}

internal sealed class PartialOwnershipFinding
{
    internal string SymbolId { get; }
    internal string SymbolDisplay { get; }
    internal ImmutableArray<string> Layers { get; }
    internal Location Location { get; }
    internal string StablePath { get; }

    internal PartialOwnershipFinding(
        string symbolId,
        string symbolDisplay,
        ImmutableArray<string> layers,
        Location location,
        string stablePath
    )
    {
        SymbolId = symbolId;
        SymbolDisplay = symbolDisplay;
        Layers = layers;
        Location = location;
        StablePath = stablePath;
    }
}
