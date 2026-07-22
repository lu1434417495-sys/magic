using System;
using System.Threading.Tasks;
using Godot;

public partial class run_world_save_mutation_e2e : E2eSceneTree
{
    private const int MovementMaxFrames = 600;
    private const ulong MovementTimeoutMsec = 5000;

    private protected override string ScenarioLabel => "E2E world save mutation";

    private protected override async Task RunScenarioAsync()
    {
        WorldMapSystem worldMap = await CreateTestGameThroughUiAsync(
            WorldSaveRoundTripE2e.CharacterName
        );
        GameRuntimeFacade runtime = worldMap._runtime;
        GameSession gameSession = worldMap._game_session;
        WorldRuntimeData worldData = runtime?.GetActiveWorldRuntimeData();

        Test.True(runtime != null, "New-game flow should expose the live world runtime.");
        Test.True(gameSession != null, "New-game flow should expose the canonical GameSession.");
        Test.True(worldData != null, "New-game flow should expose typed world runtime data.");
        if (runtime == null || gameSession == null || worldData == null)
            return;

        Test.True(
            worldData.HasPlayerStartCoord,
            "Generated test-world data should retain its deterministic player start coordinate."
        );
        if (!worldData.HasPlayerStartCoord)
            return;

        Vector2I initialCoord = runtime.GetPlayerCoord();
        int initialWorldStep = runtime.GetWorldStep();
        Test.Eq(
            initialCoord,
            worldData.PlayerStartCoord,
            "Mutation should begin at the generated world's recorded start coordinate."
        );
        Test.Eq(
            initialWorldStep,
            WorldSaveRoundTripE2e.InitialWorldStep,
            "A newly created test world should begin at world step zero."
        );

        bool foundSafeMove = WorldSaveRoundTripE2e.TryChooseSafeAdjacentMove(
            runtime.GetGridSystem(),
            worldData,
            initialCoord,
            out WorldSaveRoundTripE2e.MovePlan move
        );
        Test.True(
            foundSafeMove,
            "The generated test world should have a deterministic adjacent cell without an encounter, event, resource, NPC, or new-settlement entry."
        );
        if (!foundSafeMove)
            return;

        await Input.TapKeyAsync(move.Keycode);
        await Wait.UntilAsync(
            () =>
                runtime.GetPlayerCoord() == move.TargetCoord
                && runtime.GetWorldStep() == WorldSaveRoundTripE2e.ExpectedWorldStep,
            MovementMaxFrames,
            MovementTimeoutMsec,
            $"world movement and step advance to {move.TargetCoord}"
        );

        Test.Eq(
            runtime.GetPlayerCoord(),
            move.TargetCoord,
            "Real keyboard input should move the live runtime to the selected safe adjacent cell."
        );
        Test.True(
            runtime.GetPlayerCoord() != initialCoord,
            "The world-save mutation must change the player coordinate."
        );
        Test.Eq(
            runtime.GetWorldStep(),
            WorldSaveRoundTripE2e.ExpectedWorldStep,
            "One world-map key press should advance the live world by exactly one step."
        );
        Test.Eq(
            worldData.WorldStep,
            WorldSaveRoundTripE2e.ExpectedWorldStep,
            "Typed world runtime data should contain the advanced world step before shutdown."
        );
        Test.Eq(
            gameSession.GetPlayerCoord(),
            move.TargetCoord,
            "The session should stage the moved coordinate for the normal shutdown commit."
        );
        Test.True(
            gameSession.HasPendingSave(),
            "The movement should leave a pending canonical save for normal runtime disposal to commit."
        );
        Test.True(
            FileAccess.FileExists(gameSession.GetActiveSavePath()),
            "The isolated save slot should still exist before normal E2E shutdown."
        );
        Test.False(runtime.IsBattleActive(), "The safe mutation must not enter battle.");
        Test.False(
            gameSession.IsBattleSaveLocked(),
            "The safe mutation must leave canonical saves unlocked."
        );
        Test.True(
            string.IsNullOrEmpty(runtime.GetActiveModalId()),
            "The safe mutation must not open an incidental world modal."
        );
    }
}
