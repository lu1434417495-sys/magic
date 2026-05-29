using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;

// Development-only headless bridge for automation and debugging.
// This is not a player-facing startup path or UI layer.
[GlobalClass]
public partial class HeadlessGameTestSession : RefCounted
{
    private static readonly StringName EncounterKindSettlement = "settlement";
    private static readonly StringName HeadlessSettlementLootProfileId = "wolf_den";
    private static readonly StringName HeadlessSettlementLootEncounterId =
        "headless_settlement_wolf_den";
    private const string HeadlessSettlementLootDisplayName = "荒狼巢穴";

    private GodotObject _gameSession;
    private GodotObject _runtime;
    private bool _ownsGameSession;
    private EncounterAnchorData _activeHeadlessEncounterAnchor;
    private string _lastBattleStartDiagnostic = "";

    public void initialize()
    {
        EnsureGameSession();
    }

    public GodotObject get_game_session()
    {
        return _gameSession;
    }

    public GodotObject get_runtime_facade()
    {
        return _runtime;
    }

    public bool has_world_loaded()
    {
        return _runtime != null;
    }

    public Godot.Collections.Array<GDictionary> list_presets()
    {
        return WorldPresetRegistry.list_presets();
    }

    public Godot.Collections.Array<GDictionary> list_save_slots()
    {
        EnsureGameSession();
        return ToDictionaryArray(Call(_gameSession, "list_save_slots"));
    }

    public GDictionary create_new_game(StringName preset_id)
    {
        EnsureGameSession();
        GDictionary preset = WorldPresetRegistry.get_preset(preset_id);
        if (preset.Count == 0)
        {
            return Result(false, $"未找到世界预设 {preset_id}。");
        }

        UnloadWorldScene();
        int createError = Call(
                _gameSession,
                "create_new_save",
                GdInterop.GetString(preset, "generation_config_path"),
                preset_id,
                GdInterop.GetString(preset, "display_name", "世界")
            )
            .AsInt32();
        if (createError != (int)Error.Ok)
        {
            return Result(false, $"创建世界失败，错误码 {createError}。");
        }
        return ensure_world_loaded();
    }

    public GDictionary load_game(string save_id)
    {
        EnsureGameSession();
        if (string.IsNullOrEmpty(save_id))
        {
            return Result(false, "存档 ID 不能为空。");
        }

        UnloadWorldScene();
        int loadError = Call(_gameSession, "load_save", save_id).AsInt32();
        if (loadError != (int)Error.Ok)
        {
            return Result(false, $"加载存档失败，错误码 {loadError}。");
        }
        return ensure_world_loaded();
    }

    public GDictionary ensure_world_loaded()
    {
        EnsureGameSession();
        if (!CallBool(_gameSession, "has_active_world"))
        {
            return Result(false, "当前没有已加载的世界。");
        }

        if (has_world_loaded())
        {
            settle_frames();
            return Result(true, "世界地图已可用。");
        }

        _runtime = new GameRuntimeFacade();
        Call(_runtime, "setup", _gameSession);
        settle_frames();
        return Result(true, "世界地图已载入。");
    }

    public void settle_frames()
    {
        settle_frames(2);
    }

    public void settle_frames(int frame_count)
    {
        if (_runtime == null || !_runtime.HasMethod("advance"))
        {
            return;
        }

        int iterations = Mathf.Max(frame_count, 1);
        for (int index = 0; index < iterations; index++)
        {
            Call(_runtime, "advance", 0.0f);
            TryCompleteHeadlessPendingBattleStart();
        }
    }

    private bool TryCompleteHeadlessPendingBattleStart()
    {
        if (_runtime == null || CallBool(_runtime, "is_battle_active"))
        {
            return true;
        }

        GDictionary pendingRequest = GetRuntimeDictionary("_pending_battle_generation_request");
        if (pendingRequest.Count == 0)
        {
            return false;
        }

        EncounterAnchorData encounterAnchor = pendingRequest.ContainsKey("encounter_anchor")
            ? pendingRequest["encounter_anchor"].AsGodotObject() as EncounterAnchorData
            : null;
        if (encounterAnchor == null)
        {
            return false;
        }

        GodotObject battleRuntime = CallObject(_runtime, "get_battle_runtime");
        if (battleRuntime == null)
        {
            return false;
        }
        SyncBattleRuntimeContentCatalogs(battleRuntime);

        int seed = pendingRequest.ContainsKey("seed")
            ? pendingRequest["seed"].AsInt32()
            : TrueRandomSeedService.RandiRange(1, int.MaxValue - 1);
        GDictionary context = GdInterop.GetDictionary(pendingRequest, "context").Duplicate(true);
        if (!context.ContainsKey("world_coord"))
        {
            context["world_coord"] = encounterAnchor.world_coord;
        }
        if (!context.ContainsKey("battle_terrain_profile"))
        {
            context["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor)
                .ToString();
        }
        context["validate_spawn_reachability"] = false;

        var runtimeState =
            CallObject(battleRuntime, "start_battle", encounterAnchor, seed, context)
            as BattleState;
        var storedState = CallObject(battleRuntime, "get_state") as BattleState;
        _lastBattleStartDiagnostic = BuildBattleStartDiagnostic(
            runtimeState,
            storedState,
            seed,
            context
        );
        if (runtimeState == null || runtimeState.is_empty())
        {
            return false;
        }

        _activeHeadlessEncounterAnchor = encounterAnchor;
        _runtime.Set("_pending_battle_generation_request", new GDictionary());
        Call(_runtime, "refresh_battle_runtime_state");
        Call(_runtime, "present_battle_start_confirmation");
        return true;
    }

    public GDictionary set_party_storage_capacity(int capacity)
    {
        if (!has_world_loaded() || _runtime == null)
        {
            return Result(false, "当前世界地图不可用。");
        }

        var partyState = CallObject(_runtime, "get_party_state") as PartyState;
        if (partyState == null)
        {
            return Result(false, "当前不存在队伍数据。");
        }

        int resolvedCapacity = Mathf.Max(capacity, 0);
        bool firstMemberAssigned = false;
        foreach (var memberValue in partyState.member_states.Values)
        {
            var memberState = memberValue.AsGodotObject() as PartyMemberState;
            var unitProgress = memberState?.progression as UnitProgress;
            UnitBaseAttributes unitBaseAttributes = unitProgress?.unit_base_attributes;
            if (unitBaseAttributes == null)
            {
                continue;
            }

            unitBaseAttributes.custom_stats["storage_space"] = !firstMemberAssigned
                ? resolvedCapacity
                : 0;
            firstMemberAssigned = true;
        }

        settle_frames(1);
        if (!firstMemberAssigned)
        {
            return Result(false, "当前队伍没有可调整仓库容量的成员。");
        }
        return Result(true, $"已将共享仓库总容量调整为 {resolvedCapacity}。");
    }

    public GDictionary start_battle_by_kind(StringName encounter_kind)
    {
        if (!has_world_loaded() || _runtime == null)
        {
            return Result(false, "当前世界地图不可用。");
        }
        if (CallBool(_runtime, "is_battle_active"))
        {
            return Result(false, "当前已有进行中的战斗。");
        }

        EncounterAnchorData encounterAnchor =
            FindNearestEncounterAnchor(encounter_kind)
            ?? BuildHeadlessEncounterAnchor(encounter_kind);
        if (encounterAnchor == null)
        {
            return Result(false, $"未找到 encounter_kind={encounter_kind} 的遭遇。");
        }

        _activeHeadlessEncounterAnchor = encounterAnchor;
        Call(_gameSession, "set_battle_save_lock", true);
        StartBattleDirect(encounterAnchor);
        if (!CallBool(_runtime, "is_battle_active"))
        {
            _activeHeadlessEncounterAnchor = null;
            Call(_gameSession, "set_battle_save_lock", false);
            string statusText = _runtime.HasMethod("get_status_text")
                ? Call(_runtime, "get_status_text").AsString()
                : "";
            return Result(
                false,
                $"遭遇 {encounterAnchor.display_name} 未能开始战斗。status={statusText}; {_lastBattleStartDiagnostic}"
            );
        }
        return Result(true, $"已进入遭遇 {encounterAnchor.display_name} 的战斗准备。");
    }

    private void StartBattleDirect(EncounterAnchorData encounterAnchor)
    {
        _lastBattleStartDiagnostic = "";
        if (_runtime == null || encounterAnchor == null)
        {
            _lastBattleStartDiagnostic = "runtime_or_anchor_missing";
            return;
        }

        GodotObject battleRuntime = CallObject(_runtime, "get_battle_runtime");
        if (battleRuntime == null)
        {
            _lastBattleStartDiagnostic = "battle_runtime_missing";
            return;
        }
        SyncBattleRuntimeContentCatalogs(battleRuntime);

        Call(_runtime, "prepare_battle_start", encounterAnchor);
        var context = new GDictionary
        {
            ["world_coord"] = encounterAnchor.world_coord,
            ["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor).ToString(),
            ["validate_spawn_reachability"] = false,
        };
        int seed = TrueRandomSeedService.RandiRange(1, int.MaxValue - 1);
        var runtimeState =
            CallObject(battleRuntime, "start_battle", encounterAnchor, seed, context)
            as BattleState;
        var storedState = CallObject(battleRuntime, "get_state") as BattleState;
        _lastBattleStartDiagnostic = BuildBattleStartDiagnostic(
            runtimeState,
            storedState,
            seed,
            context
        );
        if (runtimeState == null || runtimeState.is_empty())
        {
            return;
        }

        _runtime.Set("_pending_battle_generation_request", new GDictionary());
        Call(_runtime, "refresh_battle_runtime_state");
        Call(_runtime, "present_battle_start_confirmation");
    }

    public GDictionary finish_active_battle(StringName winner_faction_id)
    {
        if (!has_world_loaded() || _runtime == null)
        {
            return Result(false, "当前世界地图不可用。");
        }
        if (!CallBool(_runtime, "is_battle_active"))
        {
            return Result(false, "当前没有进行中的战斗。");
        }
        if (winner_faction_id != "player" && winner_faction_id != "hostile")
        {
            return Result(false, "胜利方只能是 player 或 hostile。");
        }

        var battleState = CallObject(_runtime, "get_battle_state") as BattleState;
        if (battleState == null || battleState.is_empty())
        {
            return Result(false, "当前战斗状态不可用。");
        }

        PrimeHeadlessBattleLootIfNeeded(winner_faction_id);
        battleState.phase = "battle_ended";
        battleState.winner_faction_id = winner_faction_id;
        battleState.active_unit_id = "";
        battleState.timeline.ready_unit_ids.Clear();
        battleState.timeline.frozen = true;
        Call(_runtime, "refresh_battle_runtime_state");
        GDictionary result = ToDictionary(Call(_runtime, "command_battle_wait_or_resolve"));
        _activeHeadlessEncounterAnchor = null;
        settle_frames(1);
        return result;
    }

    public GDictionary change_battle_equipment(StringName operation, StringName slot_id)
    {
        return change_battle_equipment(operation, slot_id, "", "", new GDictionary());
    }

    public GDictionary change_battle_equipment(
        StringName operation,
        StringName slot_id,
        StringName item_id
    )
    {
        return change_battle_equipment(operation, slot_id, item_id, "", new GDictionary());
    }

    public GDictionary change_battle_equipment(
        StringName operation,
        StringName slot_id,
        StringName item_id,
        StringName instance_id
    )
    {
        return change_battle_equipment(operation, slot_id, item_id, instance_id, new GDictionary());
    }

    public GDictionary change_battle_equipment(
        StringName operation,
        StringName slot_id,
        StringName item_id,
        StringName instance_id,
        GDictionary options
    )
    {
        options ??= new GDictionary();
        if (!has_world_loaded() || _runtime == null)
        {
            return Result(false, "当前世界地图不可用。");
        }
        if (!CallBool(_runtime, "is_battle_active"))
        {
            return Result(false, "当前没有进行中的战斗。");
        }

        var battleState = CallObject(_runtime, "get_battle_state") as BattleState;
        if (battleState == null || battleState.is_empty())
        {
            return Result(false, "当前战斗状态不可用。");
        }
        if (battleState.phase != "unit_acting" || battleState.active_unit_id == "")
        {
            return Result(false, "当前没有可手动操作的行动单位。");
        }
        if (battleState.modal_state != "")
        {
            return Result(false, "当前战斗流程阻止换装。");
        }

        var activeUnit = battleState.units.ContainsKey(battleState.active_unit_id)
            ? battleState.units[battleState.active_unit_id].AsGodotObject() as BattleUnitState
            : null;
        if (activeUnit == null || !activeUnit.is_alive)
        {
            return Result(false, "当前行动单位不可用。");
        }
        if (activeUnit.control_mode != "manual")
        {
            return Result(false, "当前行动单位不是手动单位。");
        }

        var facade = _runtime as GameRuntimeFacade;
        if (facade == null)
        {
            return Result(false, "当前战斗运行时不可用。");
        }

        var battleRuntime = facade.get_battle_runtime();
        if (battleRuntime == null)
        {
            return Result(false, "当前战斗运行时不可用。");
        }

        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_CHANGE_EQUIPMENT(),
            unit_id = activeUnit.unit_id,
            target_unit_id = ProgressionDataUtils.to_string_name(
                GdInterop.GetString(options, "target_unit_id", activeUnit.unit_id.ToString())
            ),
            equipment_operation = operation,
            equipment_slot_id = slot_id,
            equipment_item_id = item_id,
            equipment_instance_id = instance_id,
        };

        if (operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP())
        {
            GDictionary resolvedInstance = ResolveBattleBackpackEquipmentInstance(
                battleState,
                item_id,
                instance_id
            );
            if (!GdInterop.GetBool(resolvedInstance, "ok", false))
            {
                return resolvedInstance;
            }

            command.equipment_instance_id = GdInterop.GetStringName(resolvedInstance, "instance_id");
            command.equipment_item_id = GdInterop.GetStringName(resolvedInstance, "item_id");
            command.equipment_instance = new GDictionary
            {
                ["instance_id"] = command.equipment_instance_id.ToString(),
                ["item_id"] = command.equipment_item_id.ToString(),
            };
        }
        else if (operation == BattleCommand.EQUIPMENT_OPERATION_UNEQUIP())
        {
            if (command.equipment_instance_id != "")
            {
                command.equipment_instance = new GDictionary
                {
                    ["instance_id"] = command.equipment_instance_id.ToString(),
                    ["item_id"] = command.equipment_item_id.ToString(),
                };
            }
        }
        else
        {
            return Result(false, "战斗换装操作只能是 equip 或 unequip。");
        }

        var batch = battleRuntime.issue_command(command);
        if (batch != null)
        {
            facade._apply_battle_batch(batch);
        }
        settle_frames(1);

        GDictionary report = FindLastChangeEquipmentReport(
            batch != null ? batch.report_entries : new GArray()
        );
        if (report.Count == 0)
        {
            return Result(false, "战斗换装命令未产生结果。");
        }
        return Result(GdInterop.GetBool(report, "ok", false), GdInterop.GetString(report, "text"));
    }

    public GDictionary build_snapshot()
    {
        var sessionSnapshot = new GDictionary
        {
            ["active_save_id"] =
                _gameSession != null ? Call(_gameSession, "get_active_save_id").AsString() : "",
            ["generation_config_path"] =
                _gameSession != null
                    ? Call(_gameSession, "get_generation_config_path").AsString()
                    : "",
            ["world_loaded"] = has_world_loaded(),
            ["presets"] = WorldPresetRegistry.list_presets(),
            ["save_slots"] =
                _gameSession != null && _gameSession.HasMethod("peek_save_slots")
                    ? Call(_gameSession, "peek_save_slots")
                    : new GArray(),
        };

        var snapshot = new GDictionary
        {
            ["session"] = sessionSnapshot,
            ["validation"] =
                _gameSession != null
                    ? ToDictionary(Call(_gameSession, "get_content_validation_snapshot"))
                    : new GDictionary(),
            ["status"] = new GDictionary { ["view"] = "none", ["text"] = "" },
            ["modal"] = new GDictionary { ["id"] = "" },
            ["logs"] =
                _gameSession != null
                    ? ToDictionary(Call(_gameSession, "get_log_snapshot"))
                    : new GDictionary(),
            ["world"] = new GDictionary(),
            ["submap"] = new GDictionary(),
            ["party"] = new GDictionary(),
            ["settlement"] = new GDictionary(),
            ["character_info"] = new GDictionary(),
            ["warehouse"] = new GDictionary(),
            ["battle"] = new GDictionary(),
            ["reward"] = new GDictionary(),
            ["promotion"] = new GDictionary(),
        };

        if (has_world_loaded())
        {
            GDictionary worldSnapshot = ToDictionary(Call(_runtime, "build_headless_snapshot"));
            foreach (var key in worldSnapshot.Keys)
            {
                snapshot[key] = worldSnapshot[key];
            }
            AugmentBattleSnapshot(snapshot);
        }
        return snapshot;
    }

    public string build_text_snapshot()
    {
        return GameTextSnapshotRenderer.render_full_snapshot(build_snapshot());
    }

    public void dispose()
    {
        dispose(false);
    }

    public void dispose(bool clear_persisted_game)
    {
        UnloadWorldScene();
        if (_gameSession != null && GodotObject.IsInstanceValid(_gameSession))
        {
            if (clear_persisted_game)
            {
                Call(_gameSession, "clear_persisted_game");
            }
            if (_ownsGameSession && _gameSession is Node node)
            {
                node.QueueFree();
                settle_frames(2);
            }
        }
        _gameSession = null;
        _ownsGameSession = false;
        _activeHeadlessEncounterAnchor = null;
    }

    private void EnsureGameSession()
    {
        if (_gameSession != null && GodotObject.IsInstanceValid(_gameSession))
        {
            return;
        }

        SceneTree sceneTree = GetSceneTree();
        if (sceneTree == null)
        {
            return;
        }

        _gameSession = sceneTree.Root.GetNodeOrNull<Node>("GameSession");
        if (_gameSession != null)
        {
            _ownsGameSession = false;
            return;
        }

        _gameSession = new GameSession();
        if (_gameSession is Node gameSessionNode)
        {
            gameSessionNode.Name = "GameSession";
            sceneTree.Root.AddChild(gameSessionNode);
        }
        else
        {
            _gameSession.Set("name", "GameSession");
        }
        _ownsGameSession = true;
        settle_frames(1);
    }

    private void UnloadWorldScene()
    {
        if (!has_world_loaded())
        {
            AbortHeadlessBattleSaveIfLocked();
            _runtime = null;
            _activeHeadlessEncounterAnchor = null;
            return;
        }

        if (_runtime != null)
        {
            Call(_runtime, "dispose");
        }
        AbortHeadlessBattleSaveIfLocked();
        _runtime = null;
        _activeHeadlessEncounterAnchor = null;
        settle_frames();
    }

    private void AbortHeadlessBattleSaveIfLocked()
    {
        if (_gameSession == null || !GodotObject.IsInstanceValid(_gameSession))
        {
            return;
        }

        bool wasBattleLocked =
            _gameSession.HasMethod("is_battle_save_locked")
            && CallBool(_gameSession, "is_battle_save_locked");
        if (wasBattleLocked && _gameSession.HasMethod("discard_pending_save"))
        {
            Call(_gameSession, "discard_pending_save");
        }
        Call(_gameSession, "set_battle_save_lock", false);
    }

    private static SceneTree GetSceneTree()
    {
        return Engine.GetMainLoop() as SceneTree;
    }

    private EncounterAnchorData FindNearestEncounterAnchor(StringName encounterKind)
    {
        if (_runtime == null)
        {
            return null;
        }

        Vector2I playerCoord = Call(_runtime, "get_player_coord").AsVector2I();
        EncounterAnchorData nearestEncounter = null;
        int nearestDistance = int.MaxValue;
        GDictionary worldData = ToDictionary(Call(_runtime, "get_world_data"));
        foreach (var encounterValue in GdInterop.GetArray(worldData, "encounter_anchors"))
        {
            var encounterAnchor = encounterValue.AsGodotObject() as EncounterAnchorData;
            if (encounterAnchor == null || encounterAnchor.is_cleared)
            {
                continue;
            }
            if (encounterKind != "" && encounterAnchor.encounter_kind != encounterKind)
            {
                continue;
            }
            if (
                encounterKind == EncounterKindSettlement
                && !EncounterHasFormalLoot(encounterAnchor)
            )
            {
                continue;
            }

            Vector2I delta = encounterAnchor.world_coord - playerCoord;
            int distance = Math.Abs(delta.X) + Math.Abs(delta.Y);
            if (distance > nearestDistance)
            {
                continue;
            }
            if (
                distance == nearestDistance
                && nearestEncounter != null
                && string.CompareOrdinal(
                    encounterAnchor.entity_id.ToString(),
                    nearestEncounter.entity_id.ToString()
                ) >= 0
            )
            {
                continue;
            }
            nearestDistance = distance;
            nearestEncounter = encounterAnchor;
        }
        return nearestEncounter;
    }

    private bool EncounterHasFormalLoot(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null || _gameSession == null)
        {
            return false;
        }

        var builder = new EncounterRosterBuilder();
        builder.setup(
            ToDictionary(Call(_gameSession, "get_wild_encounter_rosters")),
            ToDictionary(Call(_gameSession, "get_enemy_templates"))
        );
        return builder.build_loot_entries(encounterAnchor, new GDictionary()).Count > 0;
    }

    private EncounterAnchorData BuildHeadlessEncounterAnchor(StringName encounterKind)
    {
        if (encounterKind != EncounterKindSettlement || _runtime == null)
        {
            return null;
        }

        GDictionary rosters = ToDictionary(Call(_gameSession, "get_wild_encounter_rosters"));
        if (!rosters.ContainsKey(HeadlessSettlementLootProfileId))
        {
            return null;
        }

        var encounterAnchor = new EncounterAnchorData
        {
            entity_id = HeadlessSettlementLootEncounterId,
            display_name = HeadlessSettlementLootDisplayName,
            world_coord = Call(_runtime, "get_player_coord").AsVector2I(),
            faction_id = "hostile",
            encounter_kind = EncounterKindSettlement,
            encounter_profile_id = HeadlessSettlementLootProfileId,
        };
        return EncounterHasFormalLoot(encounterAnchor) ? encounterAnchor : null;
    }

    private static StringName ResolveBattleTerrainProfile(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
        {
            return "default";
        }

        string regionTag = encounterAnchor.region_tag.ToString().StripEdges().ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        return regionTag switch
        {
            "canyon" or "north_wilds" or "south_wilds" => "canyon",
            "narrow_assault" => "narrow_assault",
            "holdout_push" => "holdout_push",
            _ => "default",
        };
    }

    private static string BuildBattleStartDiagnostic(
        BattleState returnedState,
        BattleState storedState,
        int seed,
        GDictionary context
    )
    {
        string returnedSummary =
            returnedState == null
                ? "returned=null"
                : $"returned_empty={returnedState.is_empty()},returned_units={returnedState.units.Count},returned_cells={returnedState.cells.Count},returned_terrain={returnedState.terrain_profile_id}";
        string storedSummary =
            storedState == null
                ? "stored=null"
                : $"stored_empty={storedState.is_empty()},stored_units={storedState.units.Count},stored_cells={storedState.cells.Count},stored_terrain={storedState.terrain_profile_id}";
        return $"seed={seed},terrain={GdInterop.GetString(context, "battle_terrain_profile")}; {returnedSummary}; {storedSummary}";
    }

    private void SyncBattleRuntimeContentCatalogs(GodotObject battleRuntime)
    {
        if (
            battleRuntime == null
            || _gameSession == null
            || !GodotObject.IsInstanceValid(_gameSession)
        )
        {
            return;
        }

        battleRuntime.Set("_skill_defs", ToDictionary(Call(_gameSession, "get_skill_defs")));
        battleRuntime.Set("_item_defs", ToDictionary(Call(_gameSession, "get_item_defs")));
    }

    private void PrimeHeadlessBattleLootIfNeeded(StringName winnerFactionId)
    {
        if (winnerFactionId != "player" || _runtime == null || _gameSession == null)
        {
            return;
        }
        if (_activeHeadlessEncounterAnchor == null)
        {
            return;
        }

        GodotObject battleRuntime = CallObject(_runtime, "get_battle_runtime");
        if (battleRuntime == null)
        {
            return;
        }

        GArray existingLootEntries = GdInterop.GetArray(battleRuntime, "_active_loot_entries");
        if (existingLootEntries.Count > 0)
        {
            return;
        }

        var rosterBuilder = new EncounterRosterBuilder();
        rosterBuilder.setup(
            ToDictionary(Call(_gameSession, "get_wild_encounter_rosters")),
            ToDictionary(Call(_gameSession, "get_enemy_templates"))
        );
        GArray previewLootEntries = rosterBuilder.build_loot_entries(
            _activeHeadlessEncounterAnchor,
            new GDictionary()
        );
        if (previewLootEntries.Count == 0)
        {
            return;
        }
        battleRuntime.Set("_active_loot_entries", previewLootEntries.Duplicate(true));
    }

    private GDictionary ResolveBattleBackpackEquipmentInstance(
        BattleState battleState,
        StringName itemId,
        StringName instanceId
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        StringName normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        if (normalizedItemId == "" && normalizedInstanceId == "")
        {
            return Result(
                false,
                "用法: battle equip <slot_id> <item_id> [instance_id=<instance_id>]"
            );
        }

        WarehouseState backpackView = battleState?.get_party_backpack_view();
        if (backpackView == null)
        {
            return Result(false, "战斗背包状态不可用。");
        }

        var matchingInstances = new List<GDictionary>();
        foreach (EquipmentInstanceState instance in backpackView.get_non_empty_instances())
        {
            if (instance == null)
            {
                continue;
            }

            StringName candidateInstanceId = ProgressionDataUtils.to_string_name(
                instance.instance_id
            );
            StringName candidateItemId = ProgressionDataUtils.to_string_name(instance.item_id);
            if (normalizedInstanceId != "" && candidateInstanceId != normalizedInstanceId)
            {
                continue;
            }
            if (normalizedItemId != "" && candidateItemId != normalizedItemId)
            {
                continue;
            }
            matchingInstances.Add(
                new GDictionary
                {
                    ["instance_id"] = candidateInstanceId.ToString(),
                    ["item_id"] = candidateItemId.ToString(),
                }
            );
        }

        if (matchingInstances.Count == 1)
        {
            GDictionary matchedInstance = matchingInstances[0];
            matchedInstance["ok"] = true;
            return matchedInstance;
        }
        if (normalizedInstanceId == "" && matchingInstances.Count > 1)
        {
            return Result(
                false,
                $"战斗背包中有多个 {normalizedItemId} 装备实例，请指定 instance_id。"
            );
        }

        string label =
            normalizedInstanceId != ""
                ? normalizedInstanceId.ToString()
                : normalizedItemId.ToString();
        return Result(false, $"战斗背包中找不到装备 {label}。");
    }

    private static GDictionary FindLastChangeEquipmentReport(GArray reportEntries)
    {
        for (int index = reportEntries.Count - 1; index >= 0; index--)
        {
            var reportValue = reportEntries[index];
            if (!GdInterop.TryUnboxToDictionary(reportValue, out GDictionary report))
            {
                continue;
            }

            string reportType = GdInterop.GetString(
                report,
                "type",
                GdInterop.GetString(report, "entry_type")
            );
            if (reportType == "change_equipment")
            {
                return report;
            }
        }
        return new GDictionary();
    }

    private void AugmentBattleSnapshot(GDictionary snapshot)
    {
        GDictionary battleSnapshot = GdInterop.GetDictionary(snapshot, "battle");
        if (!GdInterop.GetBool(battleSnapshot, "active", false))
        {
            return;
        }

        var battleState =
            _runtime != null ? CallObject(_runtime, "get_battle_state") as BattleState : null;
        if (battleState == null || battleState.is_empty())
        {
            return;
        }

        battleSnapshot["party_backpack"] = BuildBattleBackpackSnapshot(
            battleState.get_party_backpack_view()
        );
        GArray units = GdInterop.GetArray(battleSnapshot, "units");
        foreach (GDictionary unitSnapshot in GdInterop.ReadDictionaryItems(units))
        {
            StringName unitId = GdInterop.GetStringName(unitSnapshot, "unit_id");
            var unitState = battleState.units.ContainsKey(unitId)
                ? battleState.units[unitId].AsGodotObject() as BattleUnitState
                : null;
            if (unitState == null)
            {
                continue;
            }

            GDictArray equipmentEntries = BuildBattleEquipmentEntries(
                unitState.get_equipment_view()
            );
            unitSnapshot["hp_max"] = GetBattleUnitHpMax(unitState);
            unitSnapshot["equipment"] = equipmentEntries;
            unitSnapshot["equipment_count"] = equipmentEntries.Count;
        }
        snapshot["battle"] = battleSnapshot;
    }

    private static GDictionary BuildBattleBackpackSnapshot(WarehouseState backpackView)
    {
        var stackEntries = new GDictArray();
        var equipmentEntries = new GDictArray();
        if (backpackView != null)
        {
            foreach (WarehouseStackState stack in backpackView.get_non_empty_stacks())
            {
                if (stack == null)
                {
                    continue;
                }
                stackEntries.Add(
                    new GDictionary
                    {
                        ["item_id"] = stack.item_id.ToString(),
                        ["quantity"] = stack.quantity,
                    }
                );
            }

            foreach (EquipmentInstanceState instance in backpackView.get_non_empty_instances())
            {
                if (instance == null)
                {
                    continue;
                }
                equipmentEntries.Add(
                    new GDictionary
                    {
                        ["instance_id"] = instance.instance_id.ToString(),
                        ["item_id"] = instance.item_id.ToString(),
                    }
                );
            }
        }

        equipmentEntries = SortEquipmentEntriesByInstanceId(equipmentEntries);
        return new GDictionary
        {
            ["stack_count"] = stackEntries.Count,
            ["equipment_instance_count"] = equipmentEntries.Count,
            ["used_slots"] = stackEntries.Count + equipmentEntries.Count,
            ["stacks"] = stackEntries,
            ["equipment_instances"] = equipmentEntries,
        };
    }

    private static GDictArray BuildBattleEquipmentEntries(GodotObject equipmentView)
    {
        var entries = new GDictArray();
        if (equipmentView == null || !equipmentView.HasMethod("get_entry_slot_ids"))
        {
            return entries;
        }

        foreach (
            Variant entrySlotValue in Call(equipmentView, "get_entry_slot_ids").AsGodotArray()
        )
        {
            StringName entrySlotId = ProgressionDataUtils.to_string_name(entrySlotValue);
            GodotObject entry = CallObject(equipmentView, "get_entry", entrySlotId);
            if (entry == null)
            {
                continue;
            }

            entries.Add(
                new GDictionary
                {
                    ["slot_id"] = entrySlotId.ToString(),
                    ["item_id"] = GdInterop.GetStringName(entry, "item_id").ToString(),
                    ["instance_id"] = GdInterop.GetStringName(entry, "instance_id").ToString(),
                    ["occupied_slot_ids"] = StringNameArrayToStringArray(
                        GdInterop.GetArray(entry, "occupied_slot_ids")
                    ),
                }
            );
        }
        return entries;
    }

    private static int GetBattleUnitHpMax(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
        {
            return 0;
        }
        return Mathf.Max(
            unitState.attribute_snapshot.get_value(new StringName("hp_max")),
            1
        );
    }

    private static Godot.Collections.Array<string> StringNameArrayToStringArray(GArray values)
    {
        var result = new Godot.Collections.Array<string>();
        foreach (var value in values)
        {
            result.Add(ProgressionDataUtils.to_string_name(value).ToString());
        }
        return result;
    }

    private static GDictArray SortEquipmentEntriesByInstanceId(GDictArray entries)
    {
        var sorted = new List<GDictionary>();
        foreach (GDictionary entry in entries)
        {
            sorted.Add(entry);
        }
        sorted.Sort(
            (left, right) =>
                string.CompareOrdinal(
                    GdInterop.GetString(left, "instance_id"),
                    GdInterop.GetString(right, "instance_id")
                )
        );

        var result = new GDictArray();
        foreach (GDictionary entry in sorted)
        {
            result.Add(entry);
        }
        return result;
    }

    private static GDictionary Result(bool ok, string message)
    {
        return new GDictionary { ["ok"] = ok, ["message"] = message ?? "" };
    }

    private static Variant Call(GodotObject target, StringName method, params object[] args)
    {
        if (target == null)
        {
            return default;
        }
        var values = new Variant[args?.Length ?? 0];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = GdInterop.ToVariant(args[index]);
        }
        return target.Call(method, values);
    }

    private static GodotObject CallObject(
        GodotObject target,
        StringName method,
        params object[] args
    )
    {
        var value = Call(target, method, args);
        return value.VariantType == Variant.Type.Nil ? null : value.AsGodotObject();
    }

    private static bool CallBool(GodotObject target, StringName method, params object[] args)
    {
        var value = Call(target, method, args);
        return value.VariantType != Variant.Type.Nil && value.AsBool();
    }

    private static GDictionary ToDictionary(Variant rawValue)
    {
        return rawValue.VariantType == Variant.Type.Dictionary
            ? rawValue.AsGodotDictionary()
            : new GDictionary();
    }

    private GDictionary GetRuntimeDictionary(StringName property)
    {
        if (_runtime == null)
        {
            return new GDictionary();
        }
        return GdInterop.GetDictionary(_runtime, property);
    }

    private static Godot.Collections.Array<GDictionary> ToDictionaryArray(object rawValue)
    {
        var result = new Godot.Collections.Array<GDictionary>();
        Godot.Collections.Array values = rawValue switch
        {
            Variant value when value.VariantType == Variant.Type.Array => value.AsGodotArray(),
            Godot.Collections.Array array => array,
            _ => null,
        };
        if (values == null)
        {
            return result;
        }

        foreach (GDictionary entry in GdInterop.ReadDictionaryItems(values))
            result.Add(entry);
        return result;
    }
}
