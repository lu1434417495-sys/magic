using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

public partial class run_item_icon_asset_catalog_validator_regression : LifecycleTestSceneTree
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
            AssertProductionInventory(coordinator);
            AssertInvalidItemsBlockPublication(coordinator);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected item icon asset catalog exception: {exception}");
        }

        RequestTestExit(_test.Finish("Item icon asset catalog validator regression"));
    }

    private void AssertProductionInventory(ApplicationLifetimeCoordinator coordinator)
    {
        using ItemContentRegistry registry = new();
        IReadOnlyDictionary<StringName, ItemDefinition> formalItems =
            registry.GetItemDefsTyped();
        ContentSnapshot snapshot = coordinator.ContentHost.GetSnapshot();
        ItemDefinition[] withIcons = formalItems.Values
            .Where(definition => !string.IsNullOrEmpty(definition?.IconAssetId))
            .ToArray();

        _test.Eq(withIcons.Length, 110, "formal items should retain 110 non-empty icon asset IDs");
        _test.True(
            withIcons.All(definition =>
                definition.IconAssetId == EngineAssetIds.DefaultItemIcon
            ),
            "every migrated non-empty item icon should use the catalog-backed default ID"
        );
        _test.Eq(
            ItemIconAssetCatalogValidator.Validate(
                snapshot.Items,
                coordinator.ContentHost.EngineAssets
            ).Count,
            0,
            "every non-empty formal item icon ID should resolve as a catalog Texture2D"
        );
    }

    private void AssertInvalidItemsBlockPublication(
        ApplicationLifetimeCoordinator coordinator
    )
    {
        ItemDefinition unknown = BuildItem("unknown_item_icon", "test.item_icon.unknown");
        ItemDefinition wrongType = BuildItem("wrong_type_item_icon", "battle.board.prop_scene");
        var items = new Dictionary<StringName, ItemDefinition>
        {
            [unknown.ItemId] = unknown,
            [wrongType.ItemId] = wrongType,
        };
        ProcessContentHost mainHost = coordinator.ContentHost;
        long mainEpoch = mainHost.Epoch;
        Exception failure;

        using (
            ProcessContentHost probe = ProcessContentHost.CreateSyntheticPublicationProbeForTest(
                epoch => new ContentSnapshotBuildArtifact(
                    SyntheticContentSnapshotFactory.Create(
                        new SyntheticContentSnapshotSeed
                        {
                            Epoch = epoch,
                            Items = items,
                        }
                    )
                ),
                mainHost.EngineAssets
            )
        )
        {
            failure = Capture(() => probe.BuildAndSeal());
            _test.False(probe.HasSnapshot, "invalid item icons must not publish a snapshot");
            _test.Eq(
                probe.RollbackAttemptCountForTest,
                1,
                "invalid item icon publication should roll back once"
            );
        }

        _test.True(
            failure is InvalidDataException,
            "unknown or wrong-type item icons should fail snapshot publication"
        );
        _test.True(
            failure?.Message.Contains("test.item_icon.unknown", StringComparison.Ordinal) == true,
            "publication failure should identify the unknown item icon ID"
        );
        _test.True(
            failure?.Message.Contains("battle.board.prop_scene", StringComparison.Ordinal) == true,
            "publication failure should identify the wrong-type item icon ID"
        );
        _test.Eq(mainHost.Epoch, mainEpoch, "failed item publication must not advance epoch");
    }

    private static ItemDefinition BuildItem(StringName itemId, string iconAssetId) =>
        new TestItemDefinitionBuilder
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            icon_asset_id = iconAssetId,
            is_stackable = false,
            max_stack = 1,
        }.ToDefinition();

    private static Exception Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
