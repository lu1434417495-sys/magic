using System.Collections.Generic;
using Godot;

public partial class run_battle_session_promotion_prompt_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPromotionPromptFiltersInvalidCandidates();
        TestPromotionPromptCapturesBatchProjection();

        RequestTestExit(_test.Finish("Battle session promotion prompt regression"));
    }

    private void TestPromotionPromptFiltersInvalidCandidates()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        BattleSessionFacade facade = new(new FixedBattleSeedSource(1729));
        try
        {
            int createError = gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, (int)Error.Ok, "Promotion prompt test should create a test save.");
            if (createError != (int)Error.Ok)
                return;

            int partyError = gameSession.SetPartyState(BuildPartyState());
            _test.Eq(partyError, (int)Error.Ok, "Promotion prompt test should install party state.");
            if (partyError != (int)Error.Ok)
                return;

            runtime.Setup(gameSession);
            facade.Setup(runtime);

            PendingProfessionChoice pendingChoice = new();
            pendingChoice.SetCandidateProfessionIds(
                new StringNameList
                {
                    "warrior",
                    "rogue",
                    "mage",
                    "priest",
                }
            );
            pendingChoice.SetTargetRank("warrior", 1);
            pendingChoice.SetTargetRank("priest", 0);

            CharacterProgressionDelta delta = new()
            {
                member_id = "hero",
                needs_promotion_modal = true,
            };
            delta.AddPendingProfessionChoice(pendingChoice);

            IReadOnlyDictionary<string, object> prompt = facade.BuildPromotionPromptPlain(
                delta,
                "确认后将在战斗中立即生效。"
            );
            IReadOnlyList<object> choices = PlainArray(prompt, "choices");
            _test.Eq(
                choices.Count,
                1,
                "Prompt should expose only candidates with a known profession and positive target rank."
            );
            if (choices.Count > 0)
            {
                _test.True(
                    choices[0] is IReadOnlyDictionary<string, object>,
                    "Prompt choice should remain a plain dictionary payload."
                );
                IReadOnlyDictionary<string, object> firstChoice =
                    choices[0] as IReadOnlyDictionary<string, object>;
                _test.True(
                    PlainHasExactStringValue(firstChoice, "profession_id"),
                    "Prompt profession_id should stay on the formal string payload surface."
                );
                _test.Eq(
                    PlainString(firstChoice, "profession_id", ""),
                    "warrior",
                    "Prompt should keep the valid warrior candidate."
                );
                _test.True(
                    PlainArrayHasOnlyExactStrings(
                        PlainArray(firstChoice, "granted_skill_ids")
                    ),
                    "Prompt granted_skill_ids should stay on the formal string-array payload surface."
                );
            }
            _test.True(
                PlainHasExactStringValue(prompt, "member_id"),
                "Prompt member_id should stay on the formal string payload surface."
            );
            _test.True(
                PlainHasExactStringValue(prompt, "member_name"),
                "Prompt member_name should stay on the formal string payload surface."
            );
            _test.Eq(
                PlainString(prompt, "member_name", ""),
                "Hero",
                "Prompt should still include the member display name."
            );
        }
        finally
        {
            facade.Dispose();
            runtime.Dispose();
            gameSession.ClearPersistedGame();
            gameSession.Dispose();
        }
    }

    private void TestPromotionPromptCapturesBatchProjection()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        BattleSessionFacade facade = new(new FixedBattleSeedSource(1729));
        try
        {
            int createError = gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, (int)Error.Ok, "Batch prompt test should create a test save.");
            if (createError != (int)Error.Ok)
                return;

            int partyError = gameSession.SetPartyState(BuildPartyState());
            _test.Eq(partyError, (int)Error.Ok, "Batch prompt test should install party state.");
            if (partyError != (int)Error.Ok)
                return;

            runtime.Setup(gameSession);
            facade.Setup(runtime);

            using BattleEventBatch batch = new();
            batch.AddProgressionDelta(BuildPromotionDelta());
            IReadOnlyList<CharacterProgressionDelta> projectedDeltas = batch.progression_deltas;

            _test.Eq(projectedDeltas.Count, 1, "Batch should expose one progression delta payload.");
            if (projectedDeltas.Count == 0)
                return;
            facade.CapturePendingPromotionPrompt(projectedDeltas);
            IReadOnlyDictionary<string, object> prompt =
                runtime.GetPendingPromotionPromptSnapshotPlain();
            IReadOnlyList<object> choices = PlainArray(prompt, "choices");
            int choiceCount = choices.Count;
            _test.Eq(
                choiceCount,
                1,
                "Batch progression delta payload should still capture a promotion prompt."
            );
            if (choiceCount > 0)
            {
                IReadOnlyDictionary<string, object> firstChoice =
                    choices[0] as IReadOnlyDictionary<string, object>;
                _test.Eq(
                    PlainString(firstChoice, "profession_id", ""),
                    "warrior",
                    "Captured prompt should keep the valid warrior candidate."
                );
            }
        }
        finally
        {
            facade.Dispose();
            runtime.Dispose();
            gameSession.ClearPersistedGame();
            gameSession.Dispose();
        }
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new StringNameList { "hero" },
        };
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        member.progression.unit_id = "hero";
        member.progression.display_name = "Hero";
        partyState.SetMemberState(member);
        return partyState;
    }

    private static CharacterProgressionDelta BuildPromotionDelta()
    {
        PendingProfessionChoice pendingChoice = new();
        pendingChoice.SetCandidateProfessionIds(new StringNameList { "warrior" });
        pendingChoice.SetTargetRank("warrior", 1);

        CharacterProgressionDelta delta = new()
        {
            member_id = "hero",
            needs_promotion_modal = true,
        };
        delta.AddPendingProfessionChoice(pendingChoice);
        return delta;
    }

    private static IReadOnlyList<object> PlainArray(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is IReadOnlyList<object> values
            ? values
            : System.Array.Empty<object>();
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        string defaultValue
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is string text
            ? text
            : defaultValue;
    }

    private static bool PlainHasExactStringValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is string;

    private static bool PlainArrayHasOnlyExactStrings(IReadOnlyList<object> values)
    {
        if (values == null)
            return true;
        foreach (object value in values)
        {
            if (value is not string)
                return false;
        }
        return true;
    }

}
