using System.Collections.Immutable;
using System.Text;
using Magic.ArchitectureAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

internal static class Program
{
    private const string RulesPath = "/repo/tools/architecture/layer_rules.json";
    private const string BaselinePath = "/repo/tools/architecture/layer_baseline.json";
    private const string InventoryRequestPath =
        "/repo/tools/architecture/layer_inventory_request.json";
    private static int _compilationIndex;

    private static readonly string ValidRules = """
        {
          "schemaVersion": 1,
          "sourceRootRelativeToRulesFile": "../..",
          "layers": [
            { "id": "source", "analyzeOutgoingDependencies": true },
            { "id": "target", "analyzeOutgoingDependencies": true },
            { "id": "allowed", "analyzeOutgoingDependencies": true },
            { "id": "tests", "analyzeOutgoingDependencies": false }
          ],
          "pathMappings": [
            { "glob": "scripts/source/**/*.cs", "layer": "source" },
            { "glob": "scripts/target/**/*.cs", "layer": "target" },
            { "glob": "scripts/allowed/**/*.cs", "layer": "allowed" },
            { "glob": "tests/**/*.cs", "layer": "tests" }
          ],
          "symbolOverrides": [],
          "denyRules": [
            {
              "id": "source-no-target",
              "fromLayers": ["source"],
              "toLayers": ["target"]
            }
          ]
        }
        """;

    private static readonly string EmptyBaseline = """
        {
          "schemaVersion": 1,
          "entries": []
        }
        """;

    private static readonly string InventoryRequest = """
        {
          "schemaVersion": 1,
          "scope": "crossLayer"
        }
        """;

    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("forbidden semantic reference kinds", ForbiddenSemanticReferenceKinds),
            ("constructed containing generic arguments", ConstructedContainingGenericArguments),
            ("allowed direction", AllowedDirectionReportsNothing),
            ("deduplication and deterministic order", RepeatedEdgesAreDeduplicatedAndStable),
            ("exact baseline", BaselineSuppressesOnlyExactPair),
            ("opt-in cross-layer inventory", CrossLayerInventoryIsExactAndStable),
            ("partial ownership", PartialAcrossLayersReportsOwnershipError),
            ("unclassified source", UnclassifiedSourceFailsClosed),
            ("configuration failures", InvalidConfigurationFailsClosed),
        };

        try
        {
            foreach ((string name, Func<Task> run) in tests)
            {
                await run();
                Console.WriteLine("PASS " + name);
            }
            Console.WriteLine($"Architecture analyzer semantic spike: PASS ({tests.Length} tests)");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Architecture analyzer semantic spike: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task ForbiddenSemanticReferenceKinds()
    {
        var cases = new[]
        {
            new SemanticCase(
                "invocation",
                "namespace Source { public sealed class Caller { public void Execute() { Target.Api.Run(); } } }",
                "namespace Target { public static class Api { public static void Run() { } } }",
                "M:Target.Api.Run"
            ),
            new SemanticCase(
                "object creation",
                "namespace Source { public sealed class Caller { public object Execute() { return new Target.Item(); } } }",
                "namespace Target { public sealed class Item { } }",
                "T:Target.Item"
            ),
            new SemanticCase(
                "constant",
                "namespace Source { public sealed class Caller { public int Execute() { return Target.Values.Answer; } } }",
                "namespace Target { public static class Values { public const int Answer = 42; } }",
                "F:Target.Values.Answer"
            ),
            new SemanticCase(
                "static field",
                "namespace Source { public sealed class Caller { public int Execute() { return Target.Values.Mutable; } } }",
                "namespace Target { public static class Values { public static int Mutable = 42; } }",
                "F:Target.Values.Mutable"
            ),
            new SemanticCase(
                "generic argument",
                "namespace Source { public sealed class Box<T> { } public sealed class Caller { private Box<Target.Payload> _value = new(); } }",
                "namespace Target { public sealed class Payload { } }",
                "T:Target.Payload"
            ),
            new SemanticCase(
                "inheritance",
                "namespace Source { public sealed class Derived : Target.Base { } }",
                "namespace Target { public class Base { } }",
                "T:Target.Base"
            ),
            new SemanticCase(
                "nameof type",
                "namespace Source { public sealed class Caller { public string Execute() { return nameof(Target.Payload); } } }",
                "namespace Target { public sealed class Payload { } }",
                "T:Target.Payload"
            ),
            new SemanticCase(
                "nameof property",
                "namespace Source { public sealed class Caller { public string Execute() { return nameof(Target.Api.Value); } } }",
                "namespace Target { public sealed class Payload { } public static class Api { public static Payload Value { get; } = new(); } }",
                "P:Target.Api.Value"
            ),
            new SemanticCase(
                "nameof overloaded method group",
                "namespace Source { public sealed class Caller { public string Execute() { return nameof(Target.Api.Run); } } }",
                "namespace Target { public static class Api { public static void Run() { } public static void Run(int value) { } } }",
                "T:Target.Api"
            ),
        };

        foreach (SemanticCase testCase in cases)
        {
            ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
                new TestSource("/repo/scripts/source/Caller.cs", testCase.Source),
                new TestSource("/repo/scripts/target/Target.cs", testCase.Target)
            );
            ImmutableArray<Diagnostic> forbidden = diagnostics
                .Where(diagnostic => diagnostic.Id == LayerDependencyAnalyzer.ForbiddenDependencyId)
                .ToImmutableArray();
            AssertEqual(1, forbidden.Length, testCase.Name + " should report exactly one edge");
            AssertEqual(
                testCase.ExpectedTarget,
                GetProperty(forbidden[0], "TargetSymbol"),
                testCase.Name + " target symbol"
            );
            AssertEqual("source-no-target", GetProperty(forbidden[0], "RuleId"), testCase.Name + " rule");
            AssertTrue(forbidden[0].Location.IsInSource, testCase.Name + " must have a source location");
            AssertTrue(
                forbidden[0].Location.SourceTree?.FilePath.EndsWith("Caller.cs", StringComparison.Ordinal) == true,
                testCase.Name + " must point at the source dependency"
            );
            AssertNoOtherAnalyzerErrors(diagnostics, LayerDependencyAnalyzer.ForbiddenDependencyId);
        }
    }

    private static async Task AllowedDirectionReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/source/Caller.cs",
                "namespace Source { public sealed class Caller { public void Execute() { Allowed.Api.Run(); } } }"
            ),
            new TestSource(
                "/repo/scripts/allowed/Api.cs",
                "namespace Allowed { public static class Api { public static void Run() { } } }"
            )
        );
        AssertEqual(0, diagnostics.Length, "allowed dependency should not report diagnostics");
    }

    private static async Task ConstructedContainingGenericArguments()
    {
        ImmutableArray<Diagnostic> nestedTypeDiagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/source/Caller.cs",
                "namespace Source { public sealed class Caller { private Target.Outer<Target.PayloadA>.Inner<Target.PayloadB> _value; } }"
            ),
            new TestSource(
                "/repo/scripts/target/Types.cs",
                "namespace Target { public sealed class PayloadA { } public sealed class PayloadB { } public class Outer<T> { public class Inner<TInner> { } } }"
            )
        );
        AssertSequenceEqual(
            new[]
            {
                "T:Target.Outer`1.Inner`1",
                "T:Target.PayloadA",
                "T:Target.PayloadB",
            },
            ForbiddenTargets(nestedTypeDiagnostics),
            "nested constructed type must include containing and own generic arguments"
        );

        ImmutableArray<Diagnostic> memberDiagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/source/Caller.cs",
                "namespace Source { public sealed class Caller { public int Execute() { Target.Api<Target.Payload>.Run(); return Target.Box<Target.Payload>.Value; } } }"
            ),
            new TestSource(
                "/repo/scripts/target/Members.cs",
                "namespace Target { public sealed class Payload { } public static class Api<T> { public static void Run() { } } public static class Box<T> { public static int Value = 1; } }"
            )
        );
        AssertSequenceEqual(
            new[]
            {
                "F:Target.Box`1.Value",
                "M:Target.Api`1.Run",
                "T:Target.Payload",
            },
            ForbiddenTargets(memberDiagnostics),
            "constructed member references must include containing generic arguments without container edges"
        );

        ImmutableArray<Diagnostic> methodReferenceDiagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/source/Caller.cs",
                "namespace Source { public sealed class Caller { public System.Action Reference() { return Target.Api.Run<Target.Payload>; } } }"
            ),
            new TestSource(
                "/repo/scripts/target/Method.cs",
                "namespace Target { public sealed class Payload { } public static class Api { public static void Run<T>() { } } }"
            )
        );
        AssertSequenceEqual(
            new[]
            {
                "M:Target.Api.Run``1",
                "T:Target.Payload",
            },
            ForbiddenTargets(methodReferenceDiagnostics),
            "generic method references must include their constructed method arguments"
        );
    }

    private static async Task RepeatedEdgesAreDeduplicatedAndStable()
    {
        var sources = new[]
        {
            new TestSource(
                "/repo/scripts/source/Caller.cs",
                "namespace Source { public sealed class Caller { public void Execute() { Target.Api.Z(); Target.Api.Z(); Target.Api.A(); } } }"
            ),
            new TestSource(
                "/repo/scripts/target/Api.cs",
                "namespace Target { public static class Api { public static void A() { } public static void Z() { } } }"
            ),
        };
        ImmutableArray<Diagnostic> first = await RunAnalyzer(sources);
        ImmutableArray<Diagnostic> second = await RunAnalyzer(sources);
        string[] firstKeys = ForbiddenKeys(first);
        string[] secondKeys = ForbiddenKeys(second);

        AssertEqual(2, firstKeys.Length, "repeated target method should be deduplicated");
        AssertSequenceEqual(firstKeys, secondKeys, "diagnostic order must be stable across runs");
        AssertSequenceEqual(
            firstKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            firstKeys,
            "diagnostics must be emitted in stable key order"
        );
    }

    private static async Task BaselineSuppressesOnlyExactPair()
    {
        string baseline = """
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "rule": "source-no-target",
                  "source": "M:Source.Caller.A",
                  "target": "M:Target.Api.X",
                  "note": "synthetic existing debt"
                }
              ]
            }
            """;
        ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
            new[]
            {
                new TestSource(
                    "/repo/scripts/source/Caller.cs",
                    "namespace Source { public sealed class Caller { public void A() { Target.Api.X(); Target.Api.Y(); } public void B() { Target.Api.X(); } } }"
                ),
                new TestSource(
                    "/repo/scripts/target/Api.cs",
                    "namespace Target { public static class Api { public static void X() { } public static void Y() { } } }"
                ),
            },
            baseline: baseline
        );
        string[] keys = ForbiddenKeys(diagnostics);
        AssertSequenceEqual(
            new[]
            {
                "source-no-target|M:Source.Caller.A|M:Target.Api.Y",
                "source-no-target|M:Source.Caller.B|M:Target.Api.X",
            },
            keys,
            "baseline must suppress only its exact symbol tuple"
        );
    }

    private static async Task PartialAcrossLayersReportsOwnershipError()
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/source/Split.Source.cs",
                "namespace Shared { public partial class Split { } }"
            ),
            new TestSource(
                "/repo/scripts/target/Split.Target.cs",
                "namespace Shared { public partial class Split { } }"
            )
        );
        ImmutableArray<Diagnostic> ownership = diagnostics
            .Where(diagnostic => diagnostic.Id == LayerDependencyAnalyzer.AmbiguousPartialOwnershipId)
            .ToImmutableArray();
        AssertEqual(1, ownership.Length, "cross-layer partial must report one ownership error");
        AssertEqual("T:Shared.Split", GetProperty(ownership[0], "Symbol"), "partial symbol id");
        AssertEqual("source,target", GetProperty(ownership[0], "Layers"), "partial layer set");
        AssertNoOtherAnalyzerErrors(diagnostics, LayerDependencyAnalyzer.AmbiguousPartialOwnershipId);
    }

    private static async Task CrossLayerInventoryIsExactAndStable()
    {
        string baseline = """
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "rule": "source-no-target",
                  "source": "M:Source.Caller.Execute",
                  "target": "T:Target.Item",
                  "note": "synthetic existing debt"
                }
              ]
            }
            """;
        var sources = new[]
        {
            new TestSource(
                "/repo/scripts/source/Caller.cs",
                "namespace Source { public sealed class Caller { public void Execute() { _ = new Target.Item(); _ = typeof(Target.Item); } } }"
            ),
            new TestSource(
                "/repo/scripts/target/Item.cs",
                "namespace Target { public sealed class Item { } }"
            ),
        };
        ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
            sources,
            baseline: baseline,
            inventoryRequest: InventoryRequest
        );
        ImmutableArray<Diagnostic> inventory = diagnostics
            .Where(item => item.Id == LayerDependencyAnalyzer.InventoryDependencyId)
            .ToImmutableArray();
        AssertEqual(1, inventory.Length, "cross-layer inventory must deduplicate exact tuple");
        Diagnostic diagnostic = inventory[0];
        AssertEqual("M:Source.Caller.Execute", GetProperty(diagnostic, "SourceSymbol"), "inventory source");
        AssertEqual("T:Target.Item", GetProperty(diagnostic, "TargetSymbol"), "inventory target");
        AssertEqual("source", GetProperty(diagnostic, "SourceLayer"), "inventory source layer");
        AssertEqual("target", GetProperty(diagnostic, "TargetLayer"), "inventory target layer");
        AssertEqual(
            "[\"object creation\",\"typeof\"]",
            GetProperty(diagnostic, "ReferenceKindsJson"),
            "inventory reference kinds"
        );
        AssertEqual(
            "[\"source-no-target\"]",
            GetProperty(diagnostic, "MatchedRuleIdsJson"),
            "inventory matched rules"
        );
        AssertEqual(
            "[\"source-no-target\"]",
            GetProperty(diagnostic, "BaselinedRuleIdsJson"),
            "inventory baseline annotations"
        );
        AssertEqual(
            "scripts/source/Caller.cs",
            GetProperty(diagnostic, "SourcePath"),
            "inventory evidence path"
        );
        AssertEqual(
            0,
            diagnostics.Count(item => item.Id == LayerDependencyAnalyzer.ForbiddenDependencyId),
            "baseline suppresses the gate diagnostic but not inventory"
        );
    }

    private static async Task InvalidConfigurationFailsClosed()
    {
        var cases = new[]
        {
            new ConfigurationCase(
                "missing rules",
                new AdditionalText[] { new InMemoryAdditionalText(BaselinePath, EmptyBaseline) },
                "missing_rules_file"
            ),
            new ConfigurationCase(
                "missing baseline",
                new AdditionalText[] { new InMemoryAdditionalText(RulesPath, ValidRules) },
                "missing_baseline_file"
            ),
            new ConfigurationCase(
                "malformed json",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, "{ invalid"),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                },
                "invalid_json"
            ),
            new ConfigurationCase(
                "unreadable rules",
                new AdditionalText[]
                {
                    new ThrowingAdditionalText(RulesPath),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                },
                "unreadable_config"
            ),
            new ConfigurationCase(
                "null rules document",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, "null"),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                },
                "null_config_document"
            ),
            new ConfigurationCase(
                "null baseline document",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, ValidRules),
                    new InMemoryAdditionalText(BaselinePath, "null"),
                },
                "null_config_document"
            ),
            new ConfigurationCase(
                "null layer entry",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(
                        RulesPath,
                        ValidRules.Replace("\"layers\": [", "\"layers\": [ null,", StringComparison.Ordinal)
                    ),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                },
                "null_layer"
            ),
            new ConfigurationCase(
                "unsupported version",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, ValidRules),
                    new InMemoryAdditionalText(BaselinePath, "{ \"schemaVersion\": 2, \"entries\": [] }"),
                },
                "unsupported_baseline_version"
            ),
            new ConfigurationCase(
                "duplicate rules file",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, ValidRules),
                    new InMemoryAdditionalText("/other/layer_rules.json", ValidRules),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                },
                "duplicate_rules_file"
            ),
            new ConfigurationCase(
                "duplicate baseline tuple",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, ValidRules),
                    new InMemoryAdditionalText(
                        BaselinePath,
                        """
                        {
                          "schemaVersion": 1,
                          "entries": [
                            { "rule": "source-no-target", "source": "M:S.A", "target": "M:T.B" },
                            { "rule": "source-no-target", "source": "M:S.A", "target": "M:T.B" }
                          ]
                        }
                        """
                    ),
                },
                "duplicate_baseline_entry"
            ),
            new ConfigurationCase(
                "duplicate inventory request",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, ValidRules),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                    new InMemoryAdditionalText(InventoryRequestPath, InventoryRequest),
                    new InMemoryAdditionalText("/other/layer_inventory_request.json", InventoryRequest),
                },
                "duplicate_inventory_request_file"
            ),
            new ConfigurationCase(
                "unsupported inventory request",
                new AdditionalText[]
                {
                    new InMemoryAdditionalText(RulesPath, ValidRules),
                    new InMemoryAdditionalText(BaselinePath, EmptyBaseline),
                    new InMemoryAdditionalText(
                        InventoryRequestPath,
                        "{ \"schemaVersion\": 2, \"scope\": \"crossLayer\" }"
                    ),
                },
                "unsupported_inventory_request_version"
            ),
        };

        foreach (ConfigurationCase testCase in cases)
        {
            ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
                new[] { new TestSource("/repo/scripts/source/Placeholder.cs", "public sealed class Placeholder { }") },
                additionalFiles: testCase.Files
            );
            AssertEqual(1, diagnostics.Length, testCase.Name + " should report one fail-closed error");
            Diagnostic diagnostic = diagnostics[0];
            AssertEqual(
                LayerDependencyAnalyzer.ConfigurationErrorId,
                diagnostic.Id,
                testCase.Name + " diagnostic id"
            );
            AssertEqual(
                DiagnosticSeverity.Error,
                diagnostic.Severity,
                testCase.Name + " severity"
            );
            AssertEqual(
                testCase.ExpectedErrorCode,
                GetProperty(diagnostic, "ConfigErrorCode"),
                testCase.Name + " error code"
            );
            string configFile = GetProperty(diagnostic, "ConfigFile");
            AssertTrue(
                diagnostic.GetMessage().Contains(configFile, StringComparison.Ordinal),
                testCase.Name + " message must include the configuration path"
            );
            AssertTrue(!diagnostic.Location.IsInSource, testCase.Name + " should not blame game source");
        }
    }

    private static async Task UnclassifiedSourceFailsClosed()
    {
        ImmutableArray<Diagnostic> diagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/unmapped/Unknown.cs",
                "namespace Unmapped { public sealed class Unknown { } }"
            )
        );
        AssertEqual(1, diagnostics.Length, "unclassified source must fail closed");
        Diagnostic diagnostic = diagnostics[0];
        AssertEqual(
            LayerDependencyAnalyzer.UnclassifiedSourceId,
            diagnostic.Id,
            "unclassified diagnostic id"
        );
        AssertEqual(
            "scripts/unmapped/Unknown.cs",
            GetProperty(diagnostic, "SourcePath"),
            "unclassified repository-relative path"
        );
        AssertTrue(diagnostic.Location.IsInSource, "unclassified source must have a source location");

        ImmutableArray<Diagnostic> duplicatePathDiagnostics = await RunAnalyzer(
            new TestSource(
                "/repo/scripts/unmapped/Duplicate.cs",
                "namespace Unmapped { public sealed class First { } }"
            ),
            new TestSource(
                "/repo/scripts/unmapped/Duplicate.cs",
                "namespace Unmapped { public sealed class Second { } }"
            )
        );
        AssertEqual(
            1,
            duplicatePathDiagnostics.Count(item =>
                item.Id == LayerDependencyAnalyzer.UnclassifiedSourceId),
            "the same unclassified path must be reported once"
        );

        ImmutableArray<Diagnostic> ignoredDiagnostics = await RunAnalyzer(
            new TestSource(
                "/external/External.cs",
                "namespace External { public sealed class OutsideRepository { } }"
            ),
            new TestSource(
                "/repo/obj/Generated.g.cs",
                "namespace Generated { public sealed class CompilerGenerated { } }"
            )
        );
        AssertEqual(
            0,
            ignoredDiagnostics.Length,
            "outside-root and generated syntax trees must not require path mappings"
        );
    }

    private static Task<ImmutableArray<Diagnostic>> RunAnalyzer(params TestSource[] sources) =>
        RunAnalyzer((IReadOnlyList<TestSource>)sources);

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(
        IReadOnlyList<TestSource> sources,
        string? rules = null,
        string? baseline = null,
        string? inventoryRequest = null,
        IReadOnlyList<AdditionalText>? additionalFiles = null
    )
    {
        var syntaxTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(
                SourceText.From(source.Text, Encoding.UTF8),
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp12),
                source.Path
            ))
            .ToImmutableArray();
        var compilation = CSharpCompilation.Create(
            "ArchitectureSpike" + Interlocked.Increment(ref _compilationIndex),
            syntaxTrees,
            GetPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        ImmutableArray<Diagnostic> compilationErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!compilationErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                "Synthetic compilation failed:\n" + string.Join("\n", compilationErrors)
            );
        }

        IReadOnlyList<AdditionalText> files;
        if (additionalFiles != null)
        {
            files = additionalFiles;
        }
        else
        {
            var generatedFiles = new List<AdditionalText>
            {
                new InMemoryAdditionalText(RulesPath, rules ?? ValidRules),
                new InMemoryAdditionalText(BaselinePath, baseline ?? EmptyBaseline),
            };
            if (inventoryRequest != null)
            {
                generatedFiles.Add(
                    new InMemoryAdditionalText(InventoryRequestPath, inventoryRequest)
                );
            }
            files = generatedFiles;
        }
        var analyzerOptions = new AnalyzerOptions(files.ToImmutableArray());
        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new LayerDependencyAnalyzer()),
            analyzerOptions
        );
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> GetPlatformReferences()
    {
        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedAssemblies))
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");
        return trustedAssemblies.Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }

    private static string[] ForbiddenKeys(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => diagnostic.Id == LayerDependencyAnalyzer.ForbiddenDependencyId)
            .Select(diagnostic => string.Join(
                "|",
                GetProperty(diagnostic, "RuleId"),
                GetProperty(diagnostic, "SourceSymbol"),
                GetProperty(diagnostic, "TargetSymbol")
            ))
            .ToArray();

    private static string[] ForbiddenTargets(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => diagnostic.Id == LayerDependencyAnalyzer.ForbiddenDependencyId)
            .Select(diagnostic => GetProperty(diagnostic, "TargetSymbol"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string GetProperty(Diagnostic diagnostic, string name)
    {
        if (!diagnostic.Properties.TryGetValue(name, out string? value) || value == null)
            throw new InvalidOperationException($"Diagnostic {diagnostic.Id} is missing property {name}.");
        return value;
    }

    private static void AssertNoOtherAnalyzerErrors(
        IEnumerable<Diagnostic> diagnostics,
        string allowedId
    )
    {
        Diagnostic[] unexpected = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Id != allowedId)
            .ToArray();
        if (unexpected.Length != 0)
        {
            throw new InvalidOperationException(
                "Unexpected analyzer diagnostics:\n" + string.Join("\n", unexpected.AsEnumerable())
            );
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'");
    }

    private static void AssertSequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        string message
    )
    {
        if (expected.Count != actual.Count)
            throw new InvalidOperationException($"{message}: count {actual.Count}, expected {expected.Count}");
        for (int index = 0; index < expected.Count; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(expected[index], actual[index]))
            {
                throw new InvalidOperationException(
                    $"{message}: index {index} expected '{expected[index]}', actual '{actual[index]}'"
                );
            }
        }
    }

    private sealed record TestSource(string Path, string Text);
    private sealed record SemanticCase(string Name, string Source, string Target, string ExpectedTarget);
    private sealed record ConfigurationCase(
        string Name,
        IReadOnlyList<AdditionalText> Files,
        string ExpectedErrorCode
    );
}

internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;
    public override string Path { get; }

    internal InMemoryAdditionalText(string path, string text)
    {
        Path = path;
        _text = SourceText.From(text, Encoding.UTF8);
    }

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}

internal sealed class ThrowingAdditionalText : AdditionalText
{
    public override string Path { get; }

    internal ThrowingAdditionalText(string path)
    {
        Path = path;
    }

    public override SourceText? GetText(CancellationToken cancellationToken = default) =>
        throw new IOException("synthetic read failure");
}
