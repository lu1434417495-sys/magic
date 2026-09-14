#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class run_equipment_closure_generation_catalog_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            ApplicationLifetimeCoordinator coordinator =
                Root.GetNode<ApplicationLifetimeCoordinator>(
                    "ApplicationLifetimeCoordinator"
                );
            ContentSnapshot snapshot = coordinator.ContentHost.GetSnapshot();
            EquipmentClosureGenerationCatalog catalog =
                EquipmentClosureGenerationCatalog.Build(
                    snapshot,
                    coordinator.ContentHost.EngineAssets
                );

            AssertStableIdOnlyCatalog(catalog, snapshot);
            AssertMachineReadableProtocol(catalog);
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected equipment closure generation catalog exception: {exception}"
            );
        }

        RequestTestExit(
            _test.Finish("Equipment closure generation catalog regression")
        );
    }

    private void AssertStableIdOnlyCatalog(
        EquipmentClosureGenerationCatalog catalog,
        ContentSnapshot snapshot
    )
    {
        _test.Eq(
            catalog.SchemaDomains.Count,
            5,
            "equipment generator catalog exposes all five closure schemas"
        );
        _test.Eq(
            catalog.ItemIds.Count,
            snapshot.Items.Count,
            "equipment generator catalog exposes every published item ID"
        );
        _test.Eq(
            catalog.ProfessionIds.Count,
            snapshot.Professions.Count,
            "equipment generator catalog exposes every profession ID used by item requirements"
        );
        _test.Eq(
            catalog.TraitIds.Count,
            snapshot.Traits.Count,
            "equipment generator catalog exposes every published trait ID"
        );
        _test.Eq(
            catalog.EquipmentAbilityPackIds.Count,
            snapshot.EquipmentAbilityPacks.Count,
            "equipment generator catalog exposes every published ability-pack ID"
        );
        _test.Eq(
            catalog.EquipmentAbilityBindingIds.Count,
            snapshot.EquipmentAbilityBindings.Count,
            "equipment generator catalog exposes every published binding ID"
        );
        _test.Eq(
            catalog.GearSetIds.Count,
            snapshot.GearSets.Count,
            "equipment generator catalog exposes every published gear-set ID"
        );
        _test.Eq(
            catalog.RecipeIds.Count,
            snapshot.Recipes.Count,
            "equipment generator catalog exposes every published recipe ID"
        );
        _test.True(
            catalog.TextureAssetIds.Contains("ui.item.icon.default"),
            "equipment generator catalog exposes the migrated item icon asset ID"
        );

        string json = catalog.ToJson();
        _test.False(
            json.Contains(".tres", StringComparison.OrdinalIgnoreCase),
            "equipment generator catalog does not expose authored Resource paths"
        );
        _test.False(
            json.Contains("res://", StringComparison.OrdinalIgnoreCase),
            "equipment generator catalog contains stable IDs rather than engine paths"
        );
        _test.True(
            IsOrdinallySorted(catalog.ItemIds)
                && IsOrdinallySorted(catalog.ProfessionIds)
                && IsOrdinallySorted(catalog.TraitIds)
                && IsOrdinallySorted(catalog.EquipmentAbilityPackIds)
                && IsOrdinallySorted(catalog.EquipmentAbilityBindingIds)
                && IsOrdinallySorted(catalog.GearSetIds)
                && IsOrdinallySorted(catalog.RecipeIds)
                && IsOrdinallySorted(catalog.TextureAssetIds),
            "equipment generator catalog ID lists are deterministic ordinal sequences"
        );
    }

    private void AssertMachineReadableProtocol(
        EquipmentClosureGenerationCatalog catalog
    )
    {
        using JsonDocument document = JsonDocument.Parse(catalog.ToJson());
        JsonElement root = document.RootElement;
        _test.Eq(
            root.GetProperty("protocol").GetString(),
            EquipmentClosureGenerationCatalog.ProtocolId,
            "equipment generator catalog publishes a versioned protocol"
        );
        _test.Eq(
            root.GetProperty("schema_domains").GetArrayLength(),
            5,
            "equipment generator protocol exposes schema-domain names"
        );
        _test.Eq(
            root.GetProperty("profession_ids").GetArrayLength(),
            catalog.ProfessionIds.Count,
            "equipment generator protocol exposes item-requirement profession IDs"
        );
        _test.Eq(
            root.GetProperty("texture_asset_ids").GetArrayLength(),
            catalog.TextureAssetIds.Count,
            "equipment generator protocol exposes the typed asset-ID list"
        );
    }

    private static bool IsOrdinallySorted(System.Collections.Generic.IReadOnlyList<string> values) =>
        values.SequenceEqual(values.OrderBy(value => value, StringComparer.Ordinal));
}
