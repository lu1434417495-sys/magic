using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_session_promotion_prompt_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPromotionPromptFiltersInvalidCandidates();

        if (_failures.Count == 0)
        {
            GD.Print("Battle session promotion prompt regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle session promotion prompt regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestPromotionPromptFiltersInvalidCandidates()
    {
        GameSession gameSession = new();
        gameSession._profession_defs = new GDictionary
        {
            [new StringName("warrior")] = BuildProfession("warrior", "Warrior"),
            [new StringName("cleric")] = BuildProfession("cleric", "Cleric"),
        };

        GameRuntimeFacade runtime = new()
        {
            _game_session = gameSession,
            _party_state = BuildPartyState(),
        };

        BattleSessionFacade facade = new();
        facade.setup(runtime);

        PendingProfessionChoice pendingChoice = new();
        pendingChoice.candidate_profession_ids = new GStringNameArray
        {
            "warrior",
            "rogue",
            "mage",
            "cleric",
        };
        pendingChoice.set_target_rank("warrior", 1);
        pendingChoice.set_target_rank("cleric", 0);

        CharacterProgressionDelta delta = new()
        {
            member_id = "hero",
            needs_promotion_modal = true,
        };
        delta.pending_profession_choices.Add(pendingChoice);

        GDictionary prompt = facade.build_promotion_prompt(delta, "确认后将在战斗中立即生效。");
        GArray choices = DictArray(prompt, "choices");
        AssertEq(
            choices.Count,
            1,
            "Prompt should expose only candidates with a known profession and positive target rank."
        );
        if (choices.Count > 0)
        {
            GDictionary firstChoice = choices[0].AsGodotDictionary();
            AssertEq(
                DictString(firstChoice, "profession_id", ""),
                "warrior",
                "Prompt should keep the valid warrior candidate."
            );
        }
        AssertEq(
            DictString(prompt, "member_name", ""),
            "Hero",
            "Prompt should still include the member display name."
        );

        facade.dispose();
        runtime.dispose();
        gameSession.Free();
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new();
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        partyState.set_member_state(member);
        return partyState;
    }

    private static ProfessionDef BuildProfession(StringName professionId, string displayName)
    {
        return new ProfessionDef
        {
            profession_id = professionId,
            display_name = displayName,
        };
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
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
        {
            return defaultValue;
        }
        return value.AsString();
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected))
        {
            return;
        }
        _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
