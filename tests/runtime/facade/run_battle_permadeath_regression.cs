using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_permadeath_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestNonMainCharacterBattleDeathPersistsAsRealDeath();
        TestMainCharacterBattleDeathTriggersGameOver();

        if (_failures.Count == 0)
        {
            GD.Print("Battle permadeath regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle permadeath regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestNonMainCharacterBattleDeathPersistsAsRealDeath()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
        {
            return;
        }

        GameRuntimeFacade facade = null;
        GameSession reloadedSession = null;
        try
        {
            PartyState partyState = gameSession.get_party_state();
            PartyMemberState allyState = BuildPartyMember("ally_guard_01", "护卫");
            partyState.set_member_state(allyState);
            partyState.active_member_ids = new GStringNameArray
            {
                "player_sword_01",
                "ally_guard_01",
            };
            partyState.reserve_member_ids = new GStringNameArray();
            partyState.main_character_member_id = "player_sword_01";

            int persistError = gameSession.set_party_state(partyState);
            AssertEq(persistError, (int)Error.Ok, "补充测试队友后应能持久化队伍状态。");
            if (persistError != (int)Error.Ok)
            {
                return;
            }

            facade = new GameRuntimeFacade();
            facade.setup(gameSession);
            PrepareBattleResolutionContext(
                facade,
                new[]
                {
                    BuildAllyUnit("hero_unit", "player_sword_01", true, 18),
                    BuildAllyUnit("ally_unit", "ally_guard_01", false, 0),
                }
            );
            facade.finalize_battle_resolution(BuildResolutionResult("player"));

            PartyState updatedParty = facade.get_party_state();
            PartyMemberState persistedAllyState = updatedParty.get_member_state("ally_guard_01");
            AssertTrue(persistedAllyState != null, "战后队友状态仍应保留在 PartyState.member_states 中。");
            AssertTrue(
                persistedAllyState != null && persistedAllyState.is_dead,
                "非主角在战斗中死亡后应被标记为真实死亡。"
            );
            AssertEq(
                persistedAllyState != null ? persistedAllyState.current_hp : -1,
                0,
                "真实死亡成员的 HP 应写回 0。"
            );
            AssertFalse(
                updatedParty.active_member_ids.Contains("ally_guard_01"),
                "真实死亡成员不应继续留在 active roster。"
            );
            AssertFalse(
                updatedParty.reserve_member_ids.Contains("ally_guard_01"),
                "真实死亡成员不应继续留在 reserve roster。"
            );
            AssertEq(
                updatedParty.main_character_member_id.ToString(),
                "player_sword_01",
                "主角标识不应因队友死亡而漂移。"
            );
            AssertTrue(
                facade.get_active_modal_id() != "game_over",
                "只有主角死亡时才应进入 GameOver。"
            );

            reloadedSession = new GameSession();
            int loadError = reloadedSession.load_save(gameSession.get_active_save_id());
            AssertEq(loadError, (int)Error.Ok, "真实死亡结果应能通过存档重新加载。");
            if (loadError == (int)Error.Ok)
            {
                PartyState reloadedParty = reloadedSession.get_party_state();
                PartyMemberState reloadedAllyState = reloadedParty.get_member_state("ally_guard_01");
                AssertTrue(
                    reloadedAllyState != null && reloadedAllyState.is_dead,
                    "重新加载存档后，队友死亡标记应保持稳定。"
                );
                AssertFalse(
                    reloadedParty.active_member_ids.Contains("ally_guard_01"),
                    "重新加载存档后，死亡队友不应被归一化回 active roster。"
                );
                AssertFalse(
                    reloadedParty.reserve_member_ids.Contains("ally_guard_01"),
                    "重新加载存档后，死亡队友不应被归一化回 reserve roster。"
                );
            }
        }
        finally
        {
            facade?.dispose();
            CleanupSession(reloadedSession);
            CleanupSession(gameSession);
        }
    }

    private void TestMainCharacterBattleDeathTriggersGameOver()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
        {
            return;
        }

        GameRuntimeFacade facade = null;
        GameRuntimeFacade reloadedFacade = null;
        try
        {
            facade = new GameRuntimeFacade();
            facade.setup(gameSession);
            Vector2I persistedPlayerCoord = gameSession.get_player_coord();
            gameSession.set_battle_save_lock(true);
            Vector2I stagedCoord = persistedPlayerCoord + new Vector2I(3, 0);
            int stagedCoordError = gameSession.set_player_coord(stagedCoord);
            AssertEq(stagedCoordError, (int)Error.Ok, "战斗锁开启时仍应允许暂存待刷新坐标。");
            AssertTrue(gameSession.has_pending_save(), "进入战斗后写入的位置变更应先积累为 pending save。");

            PrepareBattleResolutionContext(
                facade,
                new[] { BuildAllyUnit("hero_unit", "player_sword_01", false, 0) }
            );
            facade.finalize_battle_resolution(BuildResolutionResult("hostile"));

            PartyState partyState = facade.get_party_state();
            PartyMemberState protagonistState = partyState.get_member_state("player_sword_01");
            AssertTrue(
                protagonistState != null && protagonistState.is_dead,
                "主角在战斗中死亡后应被正式标记为真实死亡。"
            );
            AssertEq(partyState.active_member_ids.Count, 0, "主角死亡后，active roster 应为空。");
            AssertEq(facade.get_active_modal_id(), "game_over", "主角死亡后运行时应直接切到 GameOver modal。");
            AssertTrue(
                DictBool(facade.get_game_over_context(), "main_character_dead", false),
                "GameOver 上下文应标记主角死亡。"
            );
            AssertFalse(string.IsNullOrEmpty(facade.get_status_text()), "GameOver 后应写入稳定状态文本。");
            AssertFalse(gameSession.has_pending_save(), "GameOver 分支不应继续保留待刷新的 battle save。");
            AssertFalse(gameSession.is_battle_save_locked(), "GameOver 结束后应解除 battle save lock。");

            string persistedSaveId = gameSession.get_active_save_id();
            gameSession.unload_active_world();
            AssertFalse(gameSession.has_active_world(), "主角死亡后返回标题前应清掉 GameSession 当前内存态。");
            AssertEq(gameSession.get_active_save_id(), "", "卸载运行时后不应继续保留 active save id。");

            int reloadError = gameSession.load_save(persistedSaveId);
            AssertEq(reloadError, (int)Error.Ok, "卸载内存态后应仍能从磁盘加载上一份存档。");
            if (reloadError == (int)Error.Ok)
            {
                AssertEq(
                    gameSession.get_player_coord(),
                    persistedPlayerCoord,
                    "卸载后重载应回到战斗前最后一次已存档的位置。"
                );
                PartyState reloadedPartyState = gameSession.get_party_state();
                PartyMemberState reloadedMainCharacter =
                    reloadedPartyState.get_member_state("player_sword_01");
                AssertTrue(
                    reloadedMainCharacter != null && !reloadedMainCharacter.is_dead,
                    "卸载后重载不应带回主角死亡状态。"
                );

                reloadedFacade = new GameRuntimeFacade();
                reloadedFacade.setup(gameSession);
                AssertTrue(
                    reloadedFacade.get_active_modal_id() != "game_over",
                    "重新加载上一份存档后不应继续停留在 GameOver。"
                );
            }
        }
        finally
        {
            reloadedFacade?.dispose();
            facade?.dispose();
            CleanupSession(gameSession);
        }
    }

    private GameSession CreateTestSession()
    {
        GameSession gameSession = new();
        gameSession.clear_persisted_game();
        int createError = gameSession.create_new_save(TestWorldConfig);
        AssertEq(createError, (int)Error.Ok, "测试会话应能创建测试世界存档。");
        if (createError == (int)Error.Ok)
        {
            return gameSession;
        }
        CleanupSession(gameSession);
        return null;
    }

    private static void CleanupSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.clear_persisted_game();
        gameSession.Free();
    }

    private static void PrepareBattleResolutionContext(
        GameRuntimeFacade facade,
        IEnumerable<BattleUnitState> allyUnits
    )
    {
        BattleState battleState = new()
        {
            phase = "battle_ended",
            timeline = new BattleTimelineState(),
        };
        foreach (BattleUnitState allyUnit in allyUnits)
        {
            battleState.ally_unit_ids.Add(allyUnit.unit_id);
            battleState.units[allyUnit.unit_id] = allyUnit;
        }
        facade._battle_runtime._state = battleState;
        facade._battle_state = battleState;
        facade._active_battle_encounter_id = "test_encounter";
        facade._active_battle_encounter_name = "真实死亡测试";
    }

    private static BattleResolutionResult BuildResolutionResult(StringName winnerFactionId)
    {
        return new BattleResolutionResult
        {
            battle_id = "battle_permadeath_test",
            winner_faction_id = winnerFactionId,
            encounter_resolution = winnerFactionId == (StringName)"player"
                ? "player_victory"
                : "hostile_victory",
        };
    }

    private static BattleUnitState BuildAllyUnit(
        StringName unitId,
        StringName memberId,
        bool isAlive,
        int currentHp
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            source_member_id = memberId,
            display_name = memberId.ToString(),
            faction_id = "player",
            control_mode = "manual",
            is_alive = isAlive,
            current_hp = currentHp,
            current_mp = 0,
        };
        unit.set_equipment_view(new EquipmentState());
        return unit;
    }

    private static PartyMemberState BuildPartyMember(StringName memberId, string displayName)
    {
        PartyMemberState memberState = new()
        {
            member_id = memberId,
            display_name = displayName,
            current_hp = 22,
            current_mp = 4,
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = displayName;
        return memberState;
    }

    private static bool DictBool(GDictionary dictionary, string key, bool defaultValue)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return defaultValue;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : defaultValue;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
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
