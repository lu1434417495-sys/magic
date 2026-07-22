using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_load_game_e2e : E2eSceneTree
{
    private protected override string ScenarioLabel => "E2E cold-process save load";

    private protected override async Task RunScenarioAsync()
    {
        RequireIsolatedUserData();

        LoginScreen login = await LoadProjectMainSceneAsync<LoginScreen>();
        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        Test.True(gameSession != null, "Load flow should use the canonical GameSession autoload.");
        if (gameSession == null)
            return;

        List<Dictionary<string, object>> slots = gameSession.ListSaveSlotsPlain();
        Test.Eq(
            slots.Count,
            1,
            "The shared isolated sandbox should contain exactly the save made by the create step."
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
            "save list to select the persisted slot"
        );

        StringName selectedSaveId = saveList.GetSelectedItemId();
        Button confirmButton = saveList.GetNode<Button>("%ConfirmButton");
        await Input.ClickAsync(confirmButton);

        WorldMapSystem worldMap = await WaitForCurrentSceneAsync<WorldMapSystem>(
            "loaded save to enter the world-map scene",
            1800
        );
        await Wait.UntilAsync(
            () =>
                worldMap._runtime != null
                && worldMap._game_session != null
                && worldMap.world_map_view != null,
            1800,
            "loaded world-map runtime bindings"
        );

        PartyState partyState = gameSession.GetPartyState();
        PartyMemberState mainCharacter = partyState?.GetMemberState(
            partyState.GetResolvedMainCharacterMemberId()
        );

        Test.True(gameSession.HasActiveWorld(), "Loading through the save window should activate a world.");
        Test.Eq(
            gameSession.GetActiveSaveId(),
            selectedSaveId.ToString(),
            "The world scene should load the slot selected in the real save-list UI."
        );
        Test.Eq(
            mainCharacter?.display_name ?? "",
            run_new_game_e2e.CharacterName,
            "The cold process should restore the character created by the preceding process."
        );
        Test.True(worldMap.world_map_view.IsVisibleInTree(), "Loaded save should render the world map.");
        Test.Eq(
            worldMap._runtime.GetPlayerCoord(),
            gameSession.GetPlayerCoord(),
            "Loaded runtime and session should agree on the restored player coordinate."
        );
    }
}
