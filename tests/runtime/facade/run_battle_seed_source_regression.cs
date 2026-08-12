using Godot;

public partial class run_battle_seed_source_regression : LifecycleTestSceneTree
{
    private const int LifecycleSoakSeed = 0x5A17_2026;
    private const string TestWorldConfig =
        "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStartBattleCarriesInjectedSeedIntoPendingRequest();

        RequestTestExit(_test.Finish("Battle seed source regression"));
    }

    private void TestStartBattleCarriesInjectedSeedIntoPendingRequest()
    {
        EncounterAnchorData encounterAnchor = BuildEncounterAnchor();
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using GameRuntimeFacade facade = new(new FixedBattleSeedSource(LifecycleSoakSeed));
        _test.Eq(
            (Error)gameSession.StartNewGame(TestWorldConfig),
            Error.Ok,
            "battle seed 回归应先创建真实活动世界。"
        );
        facade.Setup(gameSession);
        facade.GetBattleRuntime()._terrain_generator = new PendingBattleTerrainGenerator();
        facade.SetBattleEncounterDefinitionForTests(
            new BattleEncounterDefinition(
                "wolf_wilds",
                "Battle Seed Source Test",
                "battle_seed_source_roster",
                BattleEliminationObjectiveDefinition.Instance,
                new BattleEncounterWorldResolutionDefinition(
                    BattleWorldResolutionMode.Clear,
                    BattleWorldResolutionMode.Preserve,
                    BattleWorldResolutionMode.Preserve,
                    0
                )
            )
        );

        facade.StartBattle(encounterAnchor);

        _test.True(
            facade.HasPendingBattleGenerationRequest(),
            "StartBattle 应把尚未完成的地形生成保留为 pending request。"
        );
        GameRuntimePendingBattleGenerationRequest request =
            facade.GetPendingBattleGenerationRequestState();
        _test.Eq(
            request.Seed,
            LifecycleSoakSeed,
            "注入的 fixed seed 必须经真实 StartBattle -> BeginBattleStart 进入 pending request。"
        );
        _test.True(
            ReferenceEquals(request.EncounterAnchor, encounterAnchor),
            "pending request 应保留 StartBattle 收到的 typed encounter anchor。"
        );
        gameSession.UnloadActiveWorld();
        gameSession.ClearPersistedGame();
    }

    private static EncounterAnchorData BuildEncounterAnchor() =>
        new()
        {
            entity_id = "battle_seed_source_test",
            display_name = "Battle Seed Source Test",
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            encounter_profile_id = "wolf_wilds",
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
        };
}
