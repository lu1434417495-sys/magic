using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

public partial class run_skill_icon_asset_catalog_validator_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            AssertProductionSkillIconInventory();
            AssertSuccessfulSyntheticPublicationDoesNotAdvanceProcessEpoch();
            AssertInvalidIconsBlockSnapshotPublication();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected skill icon asset catalog regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Skill icon asset catalog validator regression"));
    }

    private void AssertProductionSkillIconInventory()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNode<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        ContentSnapshot snapshot = coordinator.ContentHost.GetSnapshot();
        SkillDefinition[] withIcons = snapshot.Skills.Values
            .Where(definition =>
                definition?.IconId != null
                && !string.IsNullOrEmpty(definition.IconId.ToString())
            )
            .ToArray();

        _test.Eq(
            withIcons.Length,
            25,
            "formal skill content should retain exactly 25 registered icon occurrences"
        );
        _test.Eq(
            withIcons.Select(definition => definition.IconId.ToString())
                .Distinct(StringComparer.Ordinal)
                .Count(),
            23,
            "formal skill content should retain exactly 23 distinct icon asset IDs"
        );
        _test.Eq(
            SkillIconAssetCatalogValidator.Validate(
                snapshot.Skills,
                coordinator.ContentHost.EngineAssets
            ).Count,
            0,
            "every non-empty formal skill icon ID should resolve as a catalog Texture2D"
        );
    }

    private void AssertSuccessfulSyntheticPublicationDoesNotAdvanceProcessEpoch()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNode<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        ProcessContentHost mainHost = coordinator.ContentHost;
        EngineAssetResolver mainResolver = mainHost.EngineAssets;
        ContentSnapshot mainSnapshot = mainHost.GetSnapshot();
        long mainEpoch = mainHost.Epoch;
        long activeEpoch = LifecycleAuditRegistry.Shared
            .CaptureSnapshot()
            .ActiveContentSnapshotEpoch;
        int mainPublishedAssetCount = mainResolver.PublishedAssetCount;
        Texture2D mainIcon = mainResolver.ResolveContentAssetBorrowed<Texture2D>(
            "warrior_whirlwind_slash"
        );
        var candidateEpochs = new List<long>();

        for (int probeIndex = 0; probeIndex < 2; probeIndex++)
        {
            using ProcessContentHost probe =
                ProcessContentHost.CreateSyntheticPublicationProbeForTest(
                    (_, candidateEpoch) =>
                    {
                        candidateEpochs.Add(candidateEpoch);
                        return new ContentSnapshotBuildArtifact(
                            SyntheticContentSnapshotFactory.Create(
                                new SyntheticContentSnapshotSeed
                                {
                                    Epoch = candidateEpoch,
                                    Skills = new Dictionary<StringName, SkillDefinition>(),
                                }
                            )
                        );
                    },
                    mainResolver
                );

            ContentSnapshot probeSnapshot = probe.BuildAndSeal();
            _test.True(
                ReferenceEquals(probe.GetSnapshot(), probeSnapshot),
                $"successful synthetic publication {probeIndex} should expose only its own snapshot"
            );
            _test.True(
                probe.IsSealed && probe.HasSnapshot,
                $"successful synthetic publication {probeIndex} should seal its local publication"
            );
            _test.Eq(
                probe.RollbackAttemptCountForTest,
                0,
                $"successful synthetic publication {probeIndex} should not roll back"
            );
        }

        _test.Eq(candidateEpochs.Count, 2, "both successful probes should capture an epoch");
        for (int index = 0; index < candidateEpochs.Count; index++)
        {
            _test.Eq(
                candidateEpochs[index],
                mainEpoch + 1,
                $"synthetic publication {index} must not advance the process-global candidate epoch"
            );
        }
        _test.Eq(
            mainHost.Epoch,
            mainEpoch,
            "successful synthetic publications must not change the process content epoch"
        );
        _test.True(
            ReferenceEquals(mainHost.GetSnapshot(), mainSnapshot),
            "successful synthetic publications must not replace the process snapshot"
        );
        _test.Eq(
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ActiveContentSnapshotEpoch,
            activeEpoch,
            "successful synthetic publications must not change the lifecycle active content epoch"
        );
        _test.True(
            mainResolver.HasPublishedCatalog
                && mainResolver.PublishedAssetCount == mainPublishedAssetCount,
            "successful synthetic publications must leave the borrowed catalog published"
        );
        _test.True(
            ReferenceEquals(
                mainResolver.ResolveContentAssetBorrowed<Texture2D>(
                    "warrior_whirlwind_slash"
                ),
                mainIcon
            ),
            "successful synthetic publications must preserve borrowed catalog asset identity"
        );
    }

    private void AssertInvalidIconsBlockSnapshotPublication()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNode<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        using var scope = new NativeLeaseScope(
            "skill-icon-catalog-invalid-publication",
            LifetimeDomain.Request
        );
        SkillDefinition unknown = BuildDefinition(
            scope,
            "unknown_icon_probe",
            "test.skill_icon.unknown"
        );
        SkillDefinition wrongType = BuildDefinition(
            scope,
            "wrong_type_icon_probe",
            "battle.board.prop_scene"
        );
        var skills = new Dictionary<StringName, SkillDefinition>
        {
            [unknown.SkillId] = unknown,
            [wrongType.SkillId] = wrongType,
        };
        ProcessContentHost mainHost = coordinator.ContentHost;
        EngineAssetResolver mainResolver = mainHost.EngineAssets;
        ContentSnapshot mainSnapshot = mainHost.GetSnapshot();
        long mainEpoch = mainHost.Epoch;
        int mainPublishedAssetCount = mainResolver.PublishedAssetCount;
        Texture2D mainIcon = mainResolver.ResolveContentAssetBorrowed<Texture2D>(
            "warrior_whirlwind_slash"
        );
        int buildCount = 0;
        Exception failure;

        using (
            ProcessContentHost probe = ProcessContentHost.CreateSyntheticPublicationProbeForTest(
                (_, epoch) =>
                {
                    buildCount++;
                    return new ContentSnapshotBuildArtifact(
                        SyntheticContentSnapshotFactory.Create(
                            new SyntheticContentSnapshotSeed
                            {
                                Epoch = epoch,
                                Skills = skills,
                            }
                        )
                    );
                },
                mainResolver
            )
        )
        {
            failure = Capture(() => probe.BuildAndSeal());

            _test.Eq(buildCount, 1, "invalid icon publication probe should build once");
            _test.Eq(
                probe.RollbackAttemptCountForTest,
                1,
                "failed icon validation should run the actual host rollback once"
            );
            _test.False(probe.HasSnapshot, "failed icon validation must not expose a snapshot");
            _test.False(probe.IsSealed, "failed icon validation must leave publication unsealed");
            _test.Eq(probe.Epoch, 0L, "failed icon validation must not publish an epoch");
        }

        _test.True(
            failure is InvalidDataException,
            "unknown or wrong-type skill icons should fail the actual snapshot publication path"
        );
        _test.True(
            failure?.Message.Contains("test.skill_icon.unknown", StringComparison.Ordinal)
                == true,
            "publication failure should identify the unknown skill icon asset ID"
        );
        _test.True(
            failure?.Message.Contains("battle.board.prop_scene", StringComparison.Ordinal)
                == true,
            "publication failure should identify the registered non-Texture2D asset ID"
        );
        _test.Eq(
            mainHost.Epoch,
            mainEpoch,
            "failed synthetic publication must not change the process content epoch"
        );
        _test.True(
            ReferenceEquals(mainHost.GetSnapshot(), mainSnapshot),
            "failed synthetic publication must not replace the process snapshot"
        );
        _test.True(
            mainResolver.HasPublishedCatalog
                && mainResolver.PublishedAssetCount == mainPublishedAssetCount,
            "disposing the synthetic probe must leave the borrowed process asset resolver published"
        );
        _test.True(
            ReferenceEquals(
                mainResolver.ResolveContentAssetBorrowed<Texture2D>(
                    "warrior_whirlwind_slash"
                ),
                mainIcon
            ),
            "disposing the synthetic probe must not replace or dispose catalog assets"
        );
    }

    private static SkillDefinition BuildDefinition(
        NativeLeaseScope scope,
        StringName skillId,
        StringName iconId
    )
    {
        SkillDef raw = scope.Own(
            new SkillDef
            {
                skill_id = skillId,
                display_name = skillId.ToString(),
                icon_id = iconId,
            },
            $"skill-icon-catalog-{skillId}"
        );
        return SkillDefinition.FromResource(raw);
    }

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
