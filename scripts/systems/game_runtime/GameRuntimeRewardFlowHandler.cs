using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeRewardFlowHandler : RefCounted
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";
    private static readonly string InvalidPromotionChoiceMessage =
        "晋升提交无效，当前选择仍需确认。";

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void setup(GodotObject runtime) => Setup(runtime);

    public new void Dispose()
    {
        _runtime = null;
    }

    public void dispose() => Dispose();

    public Dictionary get_current_promotion_prompt() => GetCurrentPromotionPrompt();

    public Dictionary command_confirm_pending_reward() => CommandConfirmPendingReward();

    public Dictionary command_choose_promotion(StringName professionId) =>
        CommandChoosePromotion(professionId);

    public Dictionary command_close_active_modal() => CommandCloseActiveModal();

    public Dictionary submit_promotion_choice(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    ) => SubmitPromotionChoice(memberId, professionId, selection);

    public Dictionary cancel_promotion_choice() => CancelPromotionChoice();

    public Dictionary confirm_active_reward() => ConfirmActiveReward();

    public void on_character_info_window_closed() => OnCharacterInfoWindowClosed();

    public bool on_promotion_choice_submitted(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    ) => OnPromotionChoiceSubmitted(memberId, professionId, selection);

    public void on_promotion_choice_cancelled() => OnPromotionChoiceCancelled();

    public void on_character_reward_confirmed() => OnCharacterRewardConfirmed();

    public void enqueue_pending_character_rewards(Godot.Collections.Array rewardOptions) =>
        EnqueuePendingCharacterRewards(rewardOptions);

    public bool present_pending_reward_if_ready() => PresentPendingRewardIfReady();

    public Dictionary GetCurrentPromotionPrompt()
    {
        if (!HasRuntime())
            return new Dictionary();
        var pending = GetPendingPromotionPrompt();
        if (pending.Count > 0)
            return pending;
        var worldPending = GetPendingWorldPromotionPrompt();
        if (worldPending.Count > 0)
            return worldPending;
        return new Dictionary();
    }

    public Dictionary CommandConfirmPendingReward()
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetActiveReward() == null && !PresentPendingRewardIfReady())
            return CommandError("当前没有待确认的角色奖励。");
        if (GetActiveReward() == null)
            return CommandError("当前没有待确认的角色奖励。");
        OnCharacterRewardConfirmed();
        return CommandOk();
    }

    public Dictionary CommandChoosePromotion(StringName professionId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var prompt = GetCurrentPromotionPrompt();
        if (prompt.Count == 0)
            return CommandError("当前没有待确认的职业晋升选择。");
        var memberId = DictionaryStringName(prompt, "member_id");
        var choices = DictionaryArray(prompt, "choices");
        foreach (var choiceValue in choices)
        {
            if (choiceValue.VariantType != Variant.Type.Dictionary)
                continue;
            var choiceData = choiceValue.AsGodotDictionary();
            var candidateProfessionId = DictionaryStringName(choiceData, "profession_id");
            if (candidateProfessionId != professionId)
                continue;
            var selection = DictionaryDictionary(choiceData, "selection").Duplicate(true);
            if (OnPromotionChoiceSubmitted(memberId, candidateProfessionId, selection))
                return CommandOk();
            return CommandError(InvalidPromotionChoiceMessage);
        }
        return CommandError(string.Format("当前晋升列表中不存在职业 {0}。", professionId));
    }

    public Dictionary CommandCloseActiveModal()
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        switch (GetActiveModalId())
        {
            case "settlement":
                CloseSettlementModal();
                return CommandOk();
            case "contract_board":
                CloseContractBoardModal();
                return CommandOk();
            case "shop":
                CloseShopModal();
                return CommandOk();
            case "forge":
                CloseForgeModal();
                return CommandOk();
            case "stagecoach":
                CloseStagecoachModal();
                return CommandOk();
            case "character_info":
                OnCharacterInfoWindowClosed();
                return CommandOk();
            case "party":
                ClosePartyManagementModal();
                return CommandOk();
            case "warehouse":
                ClosePartyWarehouseModal();
                return CommandOk();
            case "submap_confirm":
                CancelSubmapEntryPrompt();
                return CommandOk();
            case "battle_start_confirm":
                return CommandError("当前战斗开始确认必须点击\"开始战斗\"。");
            case "promotion":
                return CommandError("当前晋升选择必须确认后才能继续。");
            case "reward":
                return CommandError("当前角色奖励必须确认后才能继续。");
            default:
                return CommandError("当前没有可关闭的窗口。");
        }
    }

    public Dictionary SubmitPromotionChoice(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (!OnPromotionChoiceSubmitted(memberId, professionId, selection))
            return CommandError(InvalidPromotionChoiceMessage);
        return CommandOk();
    }

    public Dictionary CancelPromotionChoice()
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        OnPromotionChoiceCancelled();
        return CommandOk();
    }

    public Dictionary ConfirmActiveReward()
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        OnCharacterRewardConfirmed();
        return CommandOk();
    }

    public void OnCharacterInfoWindowClosed()
    {
        if (!HasRuntime())
            return;
        ClearActiveCharacterInfoContext();
        SetActiveModalId("");
        UpdateStatus("已关闭人物信息窗。");
        PresentPendingRewardIfReady();
    }

    public bool OnPromotionChoiceSubmitted(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return false;
        if (IsBattleActive())
        {
            if (
                !PromotionPromptContainsChoice(
                    GetPendingPromotionPrompt(),
                    memberId,
                    professionId,
                    selection
                )
            )
            {
                RejectInvalidPromotionChoice();
                return false;
            }
            var batch = SubmitBattlePromotionChoice(memberId, professionId, selection);
            ApplyBattleBatch(batch);
            if (!BattlePromotionBatchApplied(batch, memberId, professionId))
            {
                RejectInvalidPromotionChoice();
                return false;
            }
            if (!BattlePromotionBatchNeedsFollowUp(batch, memberId))
            {
                ClearPendingPromotionPrompt();
                SetActiveModalId("");
            }
            return true;
        }

        var prompt = GetPendingWorldPromotionPrompt();
        if (prompt.Count == 0)
        {
            RejectInvalidPromotionChoice();
            return false;
        }
        if (!PromotionPromptContainsChoice(prompt, memberId, professionId, selection))
        {
            RejectInvalidPromotionChoice();
            return false;
        }
        var delta = PromoteProfession(memberId, professionId, selection);
        if (!PromotionDeltaApplied(delta, memberId, professionId))
        {
            RejectInvalidPromotionChoice();
            return false;
        }
        ClearPendingWorldPromotionPrompt();
        SetActiveModalId("");
        SyncPartyStateFromCharacterManagement();
        var persistError = PersistPartyState();
        if (delta.needs_promotion_modal)
        {
            SetPendingWorldPromotionPrompt(
                BuildRuntimePromotionPrompt(delta, "确认后将在世界地图立即生效。")
            );
            SetActiveModalId("promotion");
            if (persistError == Error.Ok)
                UpdateStatus(
                    string.Format(
                        "{0} 完成晋升后还有后续抉择待确认。",
                        GetMemberDisplayName(memberId)
                    )
                );
            else
                UpdateStatus(
                    string.Format(
                        "{0} 的晋升已应用，但队伍状态持久化失败。",
                        GetMemberDisplayName(memberId)
                    )
                );
            return true;
        }

        if (persistError == Error.Ok)
            UpdateStatus(string.Format("{0} 完成职业晋升。", GetMemberDisplayName(memberId)));
        else
            UpdateStatus(
                string.Format(
                    "{0} 完成职业晋升，但队伍状态持久化失败。",
                    GetMemberDisplayName(memberId)
                )
            );
        PresentPendingRewardIfReady();
        return true;
    }

    public void OnPromotionChoiceCancelled()
    {
        if (!HasRuntime())
            return;
        if (IsBattleActive())
        {
            if (GetPendingPromotionPrompt().Count == 0)
            {
                UpdateStatus("当前晋升选择无法取消。");
                return;
            }
            SetActiveModalId("promotion");
            UpdateStatus("当前晋升选择必须确认后才能继续战斗。");
            return;
        }

        if (GetPendingWorldPromotionPrompt().Count == 0)
        {
            UpdateStatus("当前晋升选择无法取消。");
            return;
        }
        SetActiveModalId("promotion");
        UpdateStatus("当前晋升选择必须确认后才能继续结算奖励。");
    }

    public void OnCharacterRewardConfirmed()
    {
        if (!HasRuntime() || GetActiveReward() == null)
            return;
        var reward = GetActiveReward();
        ClearActiveReward();
        SetActiveModalId("");

        var delta = ApplyPendingCharacterRewardToParty(reward);
        SyncPartyStateFromCharacterManagement();
        var persistError = PersistPartyState();
        if (delta != null && delta.needs_promotion_modal)
        {
            SetPendingWorldPromotionPrompt(
                BuildRuntimePromotionPrompt(delta, "确认后将在世界地图立即生效。")
            );
            SetActiveModalId("promotion");
            if (persistError == Error.Ok)
                UpdateStatus(
                    string.Format("{0} 的角色奖励已入账，职业晋升待确认。", reward.member_name)
                );
            else
                UpdateStatus(
                    string.Format(
                        "{0} 的角色奖励已入账，但队伍状态持久化失败。",
                        reward.member_name
                    )
                );
            return;
        }

        if (
            delta == null
            || (
                delta.mastery_changes.Count == 0
                && delta.knowledge_changes.Count == 0
                && delta.attribute_changes.Count == 0
            )
        )
        {
            if (persistError == Error.Ok)
                UpdateStatus(
                    string.Format("{0} 的本批奖励当前没有可入账项目。", reward.member_name)
                );
            else
                UpdateStatus(
                    string.Format("{0} 的奖励处理完成，但队伍状态持久化失败。", reward.member_name)
                );
        }
        else
        {
            if (persistError == Error.Ok)
                UpdateStatus(string.Format("{0} 的角色奖励已结算。", reward.member_name));
            else
                UpdateStatus(
                    string.Format(
                        "{0} 的角色奖励已结算，但队伍状态持久化失败。",
                        reward.member_name
                    )
                );
        }
        PresentPendingRewardIfReady();
    }

    public void EnqueuePendingCharacterRewards(Godot.Collections.Array rewardOptions)
    {
        if (!HasRuntime())
            return;
        EnqueueCharacterRewards(rewardOptions);
    }

    public bool PresentPendingRewardIfReady()
    {
        var activeModalId = GetActiveModalId();
        if (!HasRuntime() || IsBattleActive())
            return false;
        if (GetPendingWorldPromotionPrompt().Count > 0)
        {
            if (activeModalId != "promotion")
            {
                SetActiveModalId("promotion");
                return true;
            }
            return false;
        }
        if (GetActiveReward() != null)
        {
            if (activeModalId != "reward")
            {
                SetActiveModalId("reward");
                return true;
            }
            return false;
        }
        if (
            activeModalId == "settlement"
            || activeModalId == "contract_board"
            || activeModalId == "shop"
            || activeModalId == "forge"
            || activeModalId == "stagecoach"
            || activeModalId == "character_info"
            || activeModalId == "party"
            || activeModalId == "warehouse"
            || activeModalId == "submap_confirm"
            || activeModalId == "battle_start_confirm"
            || activeModalId == "game_over"
        )
            return false;
        var partyState = GetPartyState();
        if (partyState == null)
            return false;
        var pendingRewards = partyState.Get("pending_character_rewards").AsGodotArray();
        if (pendingRewards.Count == 0)
            return false;

        SetActiveReward(partyState.Call("get_next_pending_character_reward").AsGodotObject());
        if (GetActiveReward() == null)
            return false;
        SetActiveModalId("reward");
        return true;
    }

    private bool HasRuntime()
    {
        return _runtime != null;
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private Dictionary CommandOk(string message = "")
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = true, ["message"] = message };
        return _runtime.Call("build_command_ok", message).AsGodotDictionary();
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.Call("build_command_error", message).AsGodotDictionary();
    }

    private void RejectInvalidPromotionChoice()
    {
        SetActiveModalId("promotion");
        UpdateStatus(InvalidPromotionChoiceMessage);
    }

    private bool PromotionPromptContainsChoice(
        Dictionary prompt,
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (prompt.Count == 0)
            return false;
        if (DictionaryStringName(prompt, "member_id") != memberId)
            return false;
        var choices = DictionaryArray(prompt, "choices");
        if (choices.Count == 0)
            return false;
        foreach (var choiceValue in choices)
        {
            if (choiceValue.VariantType != Variant.Type.Dictionary)
                continue;
            var choiceData = choiceValue.AsGodotDictionary();
            if (DictionaryStringName(choiceData, "profession_id") != professionId)
                continue;
            if (!TryDictionary(choiceData, "selection", out Dictionary choiceSelection))
                continue;
            if (choiceSelection.Equals(selection))
                return true;
        }
        return false;
    }

    private bool BattlePromotionBatchApplied(
        BattleEventBatch batch,
        StringName memberId,
        StringName professionId
    )
    {
        if (batch == null)
            return false;
        foreach (var deltaValue in batch.progression_deltas)
        {
            var delta = deltaValue.As<CharacterProgressionDelta>();
            if (PromotionDeltaApplied(delta, memberId, professionId))
                return true;
        }
        return false;
    }

    private bool BattlePromotionBatchNeedsFollowUp(BattleEventBatch batch, StringName memberId)
    {
        if (batch == null)
            return false;
        foreach (var deltaValue in batch.progression_deltas)
        {
            var delta = deltaValue.As<CharacterProgressionDelta>();
            if (delta != null && delta.member_id == memberId && delta.needs_promotion_modal)
                return true;
        }
        return false;
    }

    private bool PromotionDeltaApplied(
        CharacterProgressionDelta delta,
        StringName memberId,
        StringName professionId
    )
    {
        if (delta == null)
            return false;
        if (delta.member_id != memberId)
            return false;
        if (delta.needs_promotion_modal)
            return true;
        return delta.changed_profession_ids.Contains(professionId);
    }

    private Dictionary GetPendingPromotionPrompt()
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime.Call("get_pending_promotion_prompt").AsGodotDictionary();
    }

    private Dictionary GetPendingWorldPromotionPrompt()
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime.Call("get_pending_world_promotion_prompt_state").AsGodotDictionary();
    }

    private PendingCharacterReward GetActiveReward()
    {
        if (!HasRuntime())
            return null;
        var result = _runtime.Call("get_active_reward_state");
        if (result.VariantType == Variant.Type.Nil)
            return null;
        return result.As<PendingCharacterReward>();
    }

    private string GetActiveModalId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.Call("get_active_modal_id").AsString();
    }

    private void SetActiveModalId(string modalId)
    {
        if (HasRuntime())
            _runtime.Call("set_runtime_active_modal_id", modalId);
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.Call("update_status", message);
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.Call("is_battle_active").AsBool();
    }

    private void ClearActiveCharacterInfoContext()
    {
        if (HasRuntime())
            _runtime.Call("clear_active_character_info_context");
    }

    private void CloseSettlementModal()
    {
        if (HasRuntime())
            _runtime.Call("close_settlement_modal");
    }

    private void CloseContractBoardModal()
    {
        if (HasRuntime())
            _runtime.Call("close_contract_board_modal");
    }

    private void CloseShopModal()
    {
        if (HasRuntime())
            _runtime.Call("close_shop_modal");
    }

    private void CloseForgeModal()
    {
        if (HasRuntime())
            _runtime.Call("close_forge_modal");
    }

    private void CloseStagecoachModal()
    {
        if (HasRuntime())
            _runtime.Call("close_stagecoach_modal");
    }

    private void ClosePartyManagementModal()
    {
        if (HasRuntime())
            _runtime.Call("close_party_management_modal");
    }

    private void ClosePartyWarehouseModal()
    {
        if (HasRuntime())
            _runtime.Call("close_party_warehouse_modal");
    }

    private void CancelSubmapEntryPrompt()
    {
        if (HasRuntime())
            _runtime.Call("command_cancel_submap_entry");
    }

    private void ClearPendingPromotionPrompt()
    {
        if (HasRuntime())
            _runtime.Call("clear_pending_promotion_prompt");
    }

    private void ClearPendingWorldPromotionPrompt()
    {
        if (HasRuntime())
            _runtime.Call("clear_pending_world_promotion_prompt_state");
    }

    private BattleEventBatch SubmitBattlePromotionChoice(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return null;
        return _runtime
            .Call("submit_battle_promotion_choice", memberId, professionId, selection)
            .As<BattleEventBatch>();
    }

    private void ApplyBattleBatch(BattleEventBatch batch)
    {
        if (HasRuntime() && batch != null)
            _runtime.Call("apply_battle_batch", batch);
    }

    private CharacterProgressionDelta PromoteProfession(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return null;
        return _runtime
            .Call("promote_profession", memberId, professionId, selection)
            .As<CharacterProgressionDelta>();
    }

    private void SyncPartyStateFromCharacterManagement()
    {
        if (HasRuntime())
            _runtime.Call("sync_party_state_from_character_management");
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return (Error)_runtime.Call("persist_party_state").AsInt32();
    }

    private Dictionary BuildRuntimePromotionPrompt(
        CharacterProgressionDelta delta,
        string selectionHint
    )
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime
            .Call("build_runtime_promotion_prompt", delta, selectionHint)
            .AsGodotDictionary();
    }

    private void SetPendingWorldPromotionPrompt(Dictionary prompt)
    {
        if (HasRuntime())
            _runtime.Call("set_pending_world_promotion_prompt_state", prompt);
    }

    private string GetMemberDisplayName(StringName memberId)
    {
        if (!HasRuntime())
            return memberId.ToString();
        return _runtime.Call("get_member_display_name", memberId).AsString();
    }

    private void ClearActiveReward()
    {
        if (HasRuntime())
            _runtime.Call("clear_active_reward_state");
    }

    private CharacterProgressionDelta ApplyPendingCharacterRewardToParty(
        PendingCharacterReward reward
    )
    {
        if (!HasRuntime())
            return null;
        return _runtime
            .Call("apply_pending_character_reward_to_party", reward)
            .As<CharacterProgressionDelta>();
    }

    private void EnqueueCharacterRewards(Godot.Collections.Array rewardOptions)
    {
        if (HasRuntime())
            _runtime.Call("enqueue_character_rewards", rewardOptions);
    }

    private GodotObject GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.Call("get_party_state").AsGodotObject();
    }

    private void SetActiveReward(GodotObject reward)
    {
        if (HasRuntime())
            _runtime.Call("set_active_reward_state", reward);
    }

    private static StringName DictionaryStringName(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(dictionary[key]);
    }

    private static Godot.Collections.Array DictionaryArray(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Godot.Collections.Array();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    private static Dictionary DictionaryDictionary(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Dictionary();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Dictionary();
    }

    private static bool TryDictionary(Dictionary dictionary, string key, out Dictionary value)
    {
        value = new Dictionary();
        if (dictionary == null || !dictionary.ContainsKey(key))
            return false;
        var rawValue = dictionary[key];
        if (rawValue.VariantType != Variant.Type.Dictionary)
            return false;
        value = rawValue.AsGodotDictionary();
        return true;
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GodotObject target)
            || !GodotObject.IsInstanceValid(target)
        )
            return null;
        return target;
    }
}
