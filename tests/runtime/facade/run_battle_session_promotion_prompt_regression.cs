using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_session_promotion_prompt_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPromotionPromptFiltersInvalidCandidates();

        Quit(_test.Finish("Battle session promotion prompt regression"));
    }

    private void TestPromotionPromptFiltersInvalidCandidates()
    {
        GameSession gameSession = new();
        GameRuntimeFacade runtime = new();
        BattleSessionFacade facade = new();
        try
        {
            int createError = gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, (int)Error.Ok, "Promotion prompt test should create a test save.");
            if (createError != (int)Error.Ok)
                return;

            PartyState partyState = BuildPartyState();
            int partyError;
            try
            {
                partyError = gameSession.SetPartyState(partyState);
            }
            finally
            {
                GodotRefCountedDisposer.DisposeIfValid(partyState);
            }
            _test.Eq(partyError, (int)Error.Ok, "Promotion prompt test should install party state.");
            if (partyError != (int)Error.Ok)
                return;

            runtime.Setup(gameSession);
            facade.Setup(runtime);

            PendingProfessionChoice pendingChoice = new();
            CharacterProgressionDelta delta = new()
            {
                member_id = "hero",
                needs_promotion_modal = true,
            };
            GDictionary prompt;
            try
            {
                pendingChoice.SetCandidateProfessionIds(
                    new GStringNameArray
                    {
                        "warrior",
                        "rogue",
                        "mage",
                        "priest",
                    }
                );
                pendingChoice.SetTargetRank("warrior", 1);
                pendingChoice.SetTargetRank("priest", 0);
                delta.AddPendingProfessionChoice(pendingChoice);

                prompt = facade.BuildPromotionPrompt(delta, "确认后将在战斗中立即生效。");
            }
            finally
            {
                GodotRefCountedDisposer.DisposeIfValid(pendingChoice);
                GodotRefCountedDisposer.DisposeIfValid(delta);
            }
            GArray choices = DictArray(prompt, "choices");
            _test.Eq(
                choices.Count,
                1,
                "Prompt should expose only candidates with a known profession and positive target rank."
            );
            if (choices.Count > 0)
            {
                GDictionary firstChoice = choices[0].AsGodotDictionary();
                _test.True(
                    HasExactStringValue(firstChoice, "profession_id"),
                    "Prompt profession_id should stay on the formal string payload surface."
                );
                _test.Eq(
                    DictString(firstChoice, "profession_id", ""),
                    "warrior",
                    "Prompt should keep the valid warrior candidate."
                );
                _test.True(
                    ArrayHasOnlyExactStrings(DictArray(firstChoice, "granted_skill_ids")),
                    "Prompt granted_skill_ids should stay on the formal string-array payload surface."
                );
            }
            _test.True(
                HasExactStringValue(prompt, "member_id"),
                "Prompt member_id should stay on the formal string payload surface."
            );
            _test.True(
                HasExactStringValue(prompt, "member_name"),
                "Prompt member_name should stay on the formal string payload surface."
            );
            _test.Eq(
                DictString(prompt, "member_name", ""),
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

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
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

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static string DictString(GDictionary dictionary, string key, string defaultValue)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return defaultValue;
        }
        Variant value = dictionary[key];
        if (value.VariantType != Variant.Type.String)
        {
            return defaultValue;
        }
        return value.AsString();
    }

    private static bool HasExactStringValue(GDictionary dictionary, string key)
    {
        return dictionary != null
            && dictionary.ContainsKey(key)
            && dictionary[key].VariantType == Variant.Type.String;
    }

    private static bool ArrayHasOnlyExactStrings(GArray values)
    {
        if (values == null)
            return true;
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.String)
                return false;
        }
        return true;
    }
}
