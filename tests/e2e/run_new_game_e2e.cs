using System;
using System.Threading.Tasks;
using Godot;

public partial class run_new_game_e2e : E2eSceneTree
{
    internal const string CharacterName = "E2E Hero";
    private const string TestWorldConfigPath =
        "res://data/configs/world_map/test_world_map_config.tres";

    private protected override string ScenarioLabel => "E2E create new game";

    private protected override async Task RunScenarioAsync()
    {
        WorldMapSystem worldMap = await CreateTestGameThroughUiAsync(CharacterName);
        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");

        Test.True(gameSession != null, "New-game flow should keep the canonical GameSession.");
        if (gameSession == null)
            return;

        PartyState partyState = gameSession.GetPartyState();
        PartyMemberState mainCharacter = partyState?.GetMemberState(
            partyState.GetResolvedMainCharacterMemberId()
        );

        Test.True(gameSession.HasActiveWorld(), "New-game confirmation should create an active world.");
        Test.True(
            !string.IsNullOrWhiteSpace(gameSession.GetActiveSaveId()),
            "New-game confirmation should allocate a save slot."
        );
        Test.True(
            FileAccess.FileExists(gameSession.GetActiveSavePath()),
            "New-game confirmation should persist the save file inside the isolated user data."
        );
        Test.Eq(
            gameSession.GetGenerationConfigPath(),
            TestWorldConfigPath,
            "The login TestButton should create the configured test world."
        );
        Test.True(mainCharacter != null, "The created party should expose its main character.");
        Test.Eq(
            mainCharacter?.display_name ?? "",
            CharacterName,
            "Character creation should persist the name entered through the real LineEdit."
        );
        Test.True(worldMap.world_map_view.IsVisibleInTree(), "The world-map view should be visible.");
        Test.True(
            worldMap.party_button != null
                && worldMap.party_button.IsVisibleInTree()
                && !worldMap.party_button.Disabled,
            "The world-map party action should be usable after creation."
        );
        Test.Eq(
            worldMap._runtime.GetPlayerCoord(),
            gameSession.GetPlayerCoord(),
            "World runtime and persisted session should agree on the initial player coordinate."
        );
        Test.False(worldMap._runtime.IsBattleActive(), "A new world should start outside battle.");
    }
}
