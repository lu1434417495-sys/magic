using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_world_save_reload_e2e : E2eSceneTree
{
    private protected override string ScenarioLabel => "E2E world save cold-process reload";

    private protected override async Task RunScenarioAsync()
    {
        RequireIsolatedUserData();

        LoginScreen login = await LoadProjectMainSceneAsync<LoginScreen>();
        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        Test.True(gameSession != null, "Reload flow should use the canonical GameSession autoload.");
        if (gameSession == null)
            return;

        List<Dictionary<string, object>> slots = gameSession.ListSaveSlotsPlain();
        Test.Eq(
            slots.Count,
            1,
            "The round-trip sandbox should contain exactly the save made by the mutation process."
        );

        await Wait.UntilAsync(
            () =>
                login.load_button != null
                && login.load_button.IsVisibleInTree()
                && !login.load_button.Disabled
                && login.load_button.GetGlobalRect().Size.X > 0.0f
                && login.load_button.GetGlobalRect().Size.Y > 0.0f,
            600,
            "login LoadButton to become clickable"
        );
        await Input.ClickAsync(login.load_button);

        SaveListWindow saveList = login.save_list_window;
        await Wait.UntilAsync(
            () =>
                saveList != null
                && GodotObject.IsInstanceValid(saveList)
                && saveList.IsVisibleInTree()
                && saveList.GetSelectedItemId() != (StringName)"",
            600,
            "round-trip save list to select the persisted slot"
        );

        StringName selectedSaveId = saveList.GetSelectedItemId();
        Button confirmButton = saveList.GetNode<Button>("%ConfirmButton");
        await Wait.UntilAsync(
            () =>
                GodotObject.IsInstanceValid(confirmButton)
                && confirmButton.IsInsideTree()
                && confirmButton.IsVisibleInTree()
                && !confirmButton.Disabled
                && confirmButton.GetGlobalRect().Size.X > 0.0f
                && confirmButton.GetGlobalRect().Size.Y > 0.0f,
            600,
            "round-trip save confirm button to become clickable"
        );
        await Input.ClickAsync(confirmButton);

        WorldMapSystem worldMap = await WaitForCurrentSceneAsync<WorldMapSystem>(
            "round-trip save to enter the world-map scene",
            1800
        );
        await Wait.UntilAsync(
            () =>
                worldMap._runtime != null
                && worldMap._game_session != null
                && worldMap.world_map_view != null,
            1800,
            "round-trip world-map runtime bindings"
        );

        GameRuntimeFacade runtime = worldMap._runtime;
        WorldRuntimeData worldData = runtime.GetActiveWorldRuntimeData();
        Test.True(worldData != null, "Reloaded save should expose typed world runtime data.");
        if (worldData == null)
            return;

        Test.True(
            worldData.HasPlayerStartCoord,
            "Reloaded world data should retain the original player start coordinate."
        );
        if (!worldData.HasPlayerStartCoord)
            return;

        bool foundExpectedMove = WorldSaveRoundTripE2e.TryChooseSafeAdjacentMove(
            runtime.GetGridSystem(),
            worldData,
            worldData.PlayerStartCoord,
            out WorldSaveRoundTripE2e.MovePlan expectedMove
        );
        Test.True(
            foundExpectedMove,
            "Reload should reproduce the deterministic safe-adjacent target from persisted world data."
        );
        if (!foundExpectedMove)
            return;

        PartyState partyState = gameSession.GetPartyState();
        PartyMemberState mainCharacter = partyState?.GetMemberState(
            partyState.GetResolvedMainCharacterMemberId()
        );

        Test.Eq(
            gameSession.GetActiveSaveId(),
            selectedSaveId.ToString(),
            "The real save-list UI should load its selected round-trip slot."
        );
        Test.Eq(
            mainCharacter?.display_name ?? "",
            WorldSaveRoundTripE2e.CharacterName,
            "The cold process should restore the character created by the mutation process."
        );
        Test.Eq(
            runtime.GetPlayerCoord(),
            expectedMove.TargetCoord,
            "The cold process should restore the exact coordinate reached before shutdown."
        );
        Test.Eq(
            gameSession.GetPlayerCoord(),
            expectedMove.TargetCoord,
            "Reloaded runtime and session should agree on the moved coordinate."
        );
        Test.True(
            runtime.GetPlayerCoord() != worldData.PlayerStartCoord,
            "Reloaded player coordinate should differ from the original start coordinate."
        );
        Test.Eq(
            runtime.GetWorldStep(),
            WorldSaveRoundTripE2e.ExpectedWorldStep,
            "The cold process should restore the one-step world-time advance."
        );
        Test.Eq(
            worldData.WorldStep,
            WorldSaveRoundTripE2e.ExpectedWorldStep,
            "Typed reloaded world data should contain the persisted world step."
        );
        Test.True(worldMap.world_map_view.IsVisibleInTree(), "Reloaded save should render the world map.");
        Test.False(runtime.IsBattleActive(), "The restored mutation should remain outside battle.");
        Test.False(
            gameSession.IsBattleSaveLocked(),
            "The restored mutation should not carry a battle save lock."
        );
        Test.True(
            string.IsNullOrEmpty(runtime.GetActiveModalId()),
            "The restored mutation should not open an incidental world modal."
        );
    }
}
