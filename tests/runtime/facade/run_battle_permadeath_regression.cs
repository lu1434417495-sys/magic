using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_permadeath_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestNonMainCharacterBattleDeathPersistsAsRealDeath();
        TestMainCharacterBattleDeathTriggersGameOver();

        Quit(_test.Finish("Battle permadeath regression"));
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
            PartyState partyState = gameSession.GetPartyState();
            PartyMemberState allyState = BuildPartyMember("ally_guard_01", "护卫");
            partyState.SetMemberState(allyState);
            partyState.active_member_ids = new GStringNameArray
            {
                "player_sword_01",
                "ally_guard_01",
            };
            partyState.reserve_member_ids = new GStringNameArray();
            partyState.main_character_member_id = "player_sword_01";

            int persistError = gameSession.SetPartyState(partyState);
            _test.Eq(persistError, (int)Error.Ok, "补充测试队友后应能持久化队伍状态。");
            if (persistError != (int)Error.Ok)
            {
                return;
            }

            facade = new GameRuntimeFacade();
            facade.Setup(gameSession);
            PrepareBattleResolutionContext(
                facade,
                new[]
                {
                    BuildAllyUnit("hero_unit", "player_sword_01", true, 18),
                    BuildAllyUnit("ally_unit", "ally_guard_01", false, 0),
                }
            );
            facade.FinalizeBattleResolution(BuildResolutionResult("player"));

            PartyState updatedParty = facade.GetPartyState();
            PartyMemberState persistedAllyState = updatedParty.GetMemberState("ally_guard_01");
            _test.True(persistedAllyState != null, "战后队友状态仍应保留在 PartyState.member_states 中。");
            _test.True(
                persistedAllyState != null && persistedAllyState.is_dead,
                "非主角在战斗中死亡后应被标记为真实死亡。"
            );
            _test.Eq(
                persistedAllyState != null ? persistedAllyState.current_hp : -1,
                0,
                "真实死亡成员的 HP 应写回 0。"
            );
            _test.False(
                updatedParty.active_member_ids.Contains("ally_guard_01"),
                "真实死亡成员不应继续留在 active roster。"
            );
            _test.False(
                updatedParty.reserve_member_ids.Contains("ally_guard_01"),
                "真实死亡成员不应继续留在 reserve roster。"
            );
            _test.Eq(
                updatedParty.main_character_member_id.ToString(),
                "player_sword_01",
                "主角标识不应因队友死亡而漂移。"
            );
            _test.True(
                facade.GetActiveModalId() != "game_over",
                "只有主角死亡时才应进入 GameOver。"
            );

            reloadedSession = new GameSession();
            int loadError = reloadedSession.LoadSave(gameSession.GetActiveSaveId());
            _test.Eq(loadError, (int)Error.Ok, "真实死亡结果应能通过存档重新加载。");
            if (loadError == (int)Error.Ok)
            {
                PartyState reloadedParty = reloadedSession.GetPartyState();
                PartyMemberState reloadedAllyState = reloadedParty.GetMemberState("ally_guard_01");
                _test.True(
                    reloadedAllyState != null && reloadedAllyState.is_dead,
                    "重新加载存档后，队友死亡标记应保持稳定。"
                );
                _test.False(
                    reloadedParty.active_member_ids.Contains("ally_guard_01"),
                    "重新加载存档后，死亡队友不应被归一化回 active roster。"
                );
                _test.False(
                    reloadedParty.reserve_member_ids.Contains("ally_guard_01"),
                    "重新加载存档后，死亡队友不应被归一化回 reserve roster。"
                );
            }
        }
        finally
        {
            facade?.Dispose();
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
            facade.Setup(gameSession);
            Vector2I persistedPlayerCoord = gameSession.GetPlayerCoord();
            gameSession.SetBattleSaveLock(true);
            Vector2I stagedCoord = persistedPlayerCoord + new Vector2I(3, 0);
            int stagedCoordError = gameSession.SetPlayerCoord(stagedCoord);
            _test.Eq(stagedCoordError, (int)Error.Ok, "战斗锁开启时仍应允许暂存待刷新坐标。");
            _test.True(gameSession.HasPendingSave(), "进入战斗后写入的位置变更应先积累为 pending save。");

            PrepareBattleResolutionContext(
                facade,
                new[] { BuildAllyUnit("hero_unit", "player_sword_01", false, 0) }
            );
            facade.FinalizeBattleResolution(BuildResolutionResult("hostile"));

            PartyState partyState = facade.GetPartyState();
            PartyMemberState protagonistState = partyState.GetMemberState("player_sword_01");
            _test.True(
                protagonistState != null && protagonistState.is_dead,
                "主角在战斗中死亡后应被正式标记为真实死亡。"
            );
            _test.Eq(partyState.active_member_ids.Count, 0, "主角死亡后，active roster 应为空。");
            _test.Eq(facade.GetActiveModalId(), "game_over", "主角死亡后运行时应直接切到 GameOver modal。");
            _test.True(
                DictBool(facade.GetGameOverContext(), "main_character_dead", false),
                "GameOver 上下文应标记主角死亡。"
            );
            _test.False(string.IsNullOrEmpty(facade.GetStatusText()), "GameOver 后应写入稳定状态文本。");
            _test.False(gameSession.HasPendingSave(), "GameOver 分支不应继续保留待刷新的 battle save。");
            _test.False(gameSession.IsBattleSaveLocked(), "GameOver 结束后应解除 battle save lock。");

            string persistedSaveId = gameSession.GetActiveSaveId();
            gameSession.UnloadActiveWorld();
            _test.False(gameSession.HasActiveWorld(), "主角死亡后返回标题前应清掉 GameSession 当前内存态。");
            _test.Eq(gameSession.GetActiveSaveId(), "", "卸载运行时后不应继续保留 active save id。");

            int reloadError = gameSession.LoadSave(persistedSaveId);
            _test.Eq(reloadError, (int)Error.Ok, "卸载内存态后应仍能从磁盘加载上一份存档。");
            if (reloadError == (int)Error.Ok)
            {
                _test.Eq(
                    gameSession.GetPlayerCoord(),
                    persistedPlayerCoord,
                    "卸载后重载应回到战斗前最后一次已存档的位置。"
                );
                PartyState reloadedPartyState = gameSession.GetPartyState();
                PartyMemberState reloadedMainCharacter =
                    reloadedPartyState.GetMemberState("player_sword_01");
                _test.True(
                    reloadedMainCharacter != null && !reloadedMainCharacter.is_dead,
                    "卸载后重载不应带回主角死亡状态。"
                );

                reloadedFacade = new GameRuntimeFacade();
                reloadedFacade.Setup(gameSession);
                _test.True(
                    reloadedFacade.GetActiveModalId() != "game_over",
                    "重新加载上一份存档后不应继续停留在 GameOver。"
                );
            }
        }
        finally
        {
            reloadedFacade?.Dispose();
            facade?.Dispose();
            CleanupSession(gameSession);
        }
    }

    private GameSession CreateTestSession()
    {
        GameSession gameSession = new();
        gameSession.ClearPersistedGame();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, (int)Error.Ok, "测试会话应能创建测试世界存档。");
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
        gameSession.ClearPersistedGame();
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
        unit.SetEquipmentView(new EquipmentState());
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
}
