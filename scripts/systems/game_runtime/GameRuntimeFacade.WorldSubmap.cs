using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of GameRuntimeFacade — world sync/fog/event prompts and submap enter/return.
// Pure physical split: same class, no behavior change. See GameRuntimeFacade.cs.
public sealed partial class GameRuntimeFacade
{
    private void _sync_active_world_context()
    {
        _save_active_fog_state_to_world_data();
        WorldMapContextSyncResult syncResult = _world_map_data_context.SyncActiveWorldContext(
            _generation_definition,
            _grid_system,
            _player_coord,
            _selected_coord
        );
        _player_coord = syncResult.PlayerCoord;
        _selected_coord = syncResult.SelectedCoord;
        if (_world_map_data_context.active_generation_definition != null)
        {
            using GodotProjectionLease<GDictionary> fogStateLease =
                _world_map_data_context.GetActiveWorldFogStateLease();
            _fog_system.Setup(
                _world_map_data_context.active_generation_definition.GetWorldSizeCells(),
                fogStateLease.Value
            );
            _world_map_data_context.ValidateWorldSystemSizeConsistency(
                _grid_system,
                _fog_system
            );
        }
    }

    private void _save_active_fog_state_to_world_data()
    {
        _world_map_data_context.SaveActiveWorldFogState(_fog_system);
    }

    private WorldMapEventData GetTriggerableWorldEventAt(Vector2I coord)
    {
        WorldMapEventData worldEvent = _world_map_data_context.GetWorldEventAt(coord);
        return worldEvent != null && worldEvent.IsTriggerableSubmapEntry ? worldEvent : null;
    }

    private void OpenWorldEventPrompt(WorldMapEventData worldEvent)
    {
        if (worldEvent == null)
        {
            return;
        }
        string targetSubmapId = worldEvent.TargetSubmapId.ToString();
        using GodotProjectionLease<GDictionary> submapEntryLease =
            _world_map_data_context.GetMountedSubmapEntryLease(targetSubmapId);
        GDictionary submapEntry = submapEntryLease.Value;
        if (submapEntry.Count == 0)
        {
            UpdateStatusInternal($"未找到目标子地图 {targetSubmapId}。");
            return;
        }
        string targetName = _world_map_data_context.GetMountedSubmapDisplayName(
            targetSubmapId,
            targetSubmapId
        );
        string promptTitle = string.IsNullOrEmpty(worldEvent.PromptTitle)
            ? "进入子地图"
            : worldEvent.PromptTitle;
        if (promptTitle.Length == 0)
            promptTitle = $"进入 {targetName}";
        string promptText = worldEvent.PromptText;
        if (promptText.Length == 0)
            promptText = $"确认后将进入 {targetName}，返回时会回到当前坐标。";
        _pending_submap_prompt.Set(
            worldEvent.EventId,
            _world_map_data_context.active_map_id,
            _player_coord,
            worldEvent.TargetSubmapId,
            targetName,
            promptTitle,
            promptText
        );
        _active_modal_kind = RuntimeModalKind.SubmapConfirm;
        UpdateStatusInternal(
            $"已发现 {ResolveWorldEventDisplayName(worldEvent, targetName)}，确认后可进入。"
        );
    }

    private static string ResolveWorldEventDisplayName(
        WorldMapEventData worldEvent,
        string fallback
    )
    {
        if (worldEvent == null || string.IsNullOrEmpty(worldEvent.DisplayName))
        {
            return fallback;
        }
        return worldEvent.DisplayName;
    }

    internal GameRuntimePendingSubmapPrompt GetPendingSubmapPromptState() =>
        _pending_submap_prompt;

    private GDictionary _confirm_pending_submap_entry()
    {
        return FinalizeCommandResult(ConfirmPendingSubmapEntryTyped());
    }

    private RuntimeCommandResult ConfirmPendingSubmapEntryTyped()
    {
        if (_pending_submap_prompt.IsEmpty)
            return BuildCommandErrorResult("当前没有待确认的子地图入口。");
        var result = EnterSubmapTyped(
            _pending_submap_prompt.TargetSubmapId.ToString(),
            _pending_submap_prompt.SourceMapId,
            _pending_submap_prompt.SourceCoord
        );
        if (result.Ok)
        {
            _pending_submap_prompt.Clear();
            _active_modal_kind = RuntimeModalKind.None;
        }
        return result;
    }

    private GDictionary _enter_submap(string submap_id, string source_map_id, Vector2I source_coord)
    {
        return FinalizeCommandResult(EnterSubmapTyped(submap_id, source_map_id, source_coord));
    }

    private RuntimeCommandResult EnterSubmapTyped(
        string submap_id,
        string source_map_id,
        Vector2I source_coord
    )
    {
        if (_game_session == null)
            return BuildCommandErrorResult("游戏会话不可用，无法进入子地图。");
        if (submap_id.Length == 0)
            return BuildCommandErrorResult("子地图标识不能为空。");
        WorldMapSubmapEnterResult enterResult = _world_map_data_context.EnterSubmap(
            submap_id,
            source_map_id,
            source_coord
        );
        if (!enterResult.Ok)
            return BuildCommandErrorResult(enterResult.Message);
        _player_coord = enterResult.PlayerCoord;
        _selected_coord = _player_coord;
        _active_settlement_id = "";
        _active_settlement_feedback_text = "";
        _active_character_info_context.Clear();
        _sync_active_world_context();
        _RefreshFog();
        int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
        using GodotProjectionLease<GDictionary> rootWorldDataLease =
            _world_map_data_context.GetRootWorldDataLease();
        int worldPersistError = _game_session.SetWorldData(rootWorldDataLease.Value);
        int commitError = (int)Error.Ok;
        if (playerPersistError == (int)Error.Ok && worldPersistError == (int)Error.Ok)
            commitError = CommitRuntimeStateInternal("submap_entry");
        string targetName = enterResult.TargetDisplayName;
        if (
            playerPersistError != (int)Error.Ok
            || worldPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            UpdateStatusInternal($"已进入 {targetName}，但世界状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }
        UpdateStatusInternal($"已进入 {targetName}。{GetSubmapReturnHintText()}");
        return BuildCommandOkResult();
    }

    private GDictionary _return_from_active_submap()
    {
        return FinalizeCommandResult(ReturnFromActiveSubmapTyped());
    }

    private RuntimeCommandResult ReturnFromActiveSubmapTyped()
    {
        if (_game_session == null)
            return BuildCommandErrorResult("游戏会话不可用，无法返回主地图。");
        if (!IsSubmapActive())
            return BuildCommandErrorResult("当前不在子地图中。");
        WorldMapSubmapReturnResult returnResult =
            _world_map_data_context.ReturnFromActiveSubmap(_player_coord);
        if (!returnResult.Ok)
            return BuildCommandErrorResult(returnResult.Message);
        _player_coord = returnResult.PlayerCoord;
        _selected_coord = _player_coord;
        _active_settlement_id = "";
        _active_settlement_feedback_text = "";
        _active_character_info_context.Clear();
        _pending_submap_prompt.Clear();
        _active_modal_kind = RuntimeModalKind.None;
        _sync_active_world_context();
        _RefreshFog();
        int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
        using GodotProjectionLease<GDictionary> rootWorldDataLease =
            _world_map_data_context.GetRootWorldDataLease();
        int worldPersistError = _game_session.SetWorldData(rootWorldDataLease.Value);
        int commitError = (int)Error.Ok;
        if (playerPersistError == (int)Error.Ok && worldPersistError == (int)Error.Ok)
            commitError = CommitRuntimeStateInternal("submap_return");
        if (
            playerPersistError != (int)Error.Ok
            || worldPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            UpdateStatusInternal("已返回原位置，但世界状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }
        UpdateStatusInternal($"已返回原位置 {FormatCoordInternal(_player_coord)}。");
        return BuildCommandOkResult();
    }

    private bool _ensure_submap_generated(string submap_id) =>
        _world_map_data_context.EnsureSubmapGenerated(submap_id);

    private WorldGenerationDefinition _get_submap_generation_definition(string submap_id) =>
        _world_map_data_context.GetSubmapGenerationDefinition(submap_id);

}
