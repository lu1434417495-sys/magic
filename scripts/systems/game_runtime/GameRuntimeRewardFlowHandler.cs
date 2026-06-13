using System;
using Godot;
using Godot.Collections;

public sealed class GameRuntimeRewardFlowHandler
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";
    private static readonly string InvalidPromotionChoiceMessage =
        "晋升提交无效，当前选择仍需确认。";

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    public void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    public void Dispose()
    {
        _runtime = null;
    }

    internal Dictionary GetCurrentPromotionPrompt()
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

    internal GameRuntimeFacade.RuntimeCommandResult CommandConfirmPendingRewardTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetActiveReward() == null && !PresentPendingRewardIfReady())
            return CommandErrorTyped("当前没有待确认的角色奖励。");
        if (GetActiveReward() == null)
            return CommandErrorTyped("当前没有待确认的角色奖励。");
        OnCharacterRewardConfirmed();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandChoosePromotionTyped(
        StringName professionId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var prompt = GetCurrentPromotionPrompt();
        if (prompt.Count == 0)
            return CommandErrorTyped("当前没有待确认的职业晋升选择。");
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
                return CommandOkTyped();
            return CommandErrorTyped(InvalidPromotionChoiceMessage);
        }
        return CommandErrorTyped(string.Format("当前晋升列表中不存在职业 {0}。", professionId));
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSubmitPromotionChoiceTyped(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (!OnPromotionChoiceSubmitted(memberId, professionId, selection))
            return CommandErrorTyped(InvalidPromotionChoiceMessage);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandCancelPromotionChoiceTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        OnPromotionChoiceCancelled();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandConfirmActiveRewardTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        OnCharacterRewardConfirmed();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandCloseActiveModalTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        switch (GetActiveModalKind())
        {
            case RuntimeModalKind.Settlement:
                CloseSettlementModal();
                return CommandOkTyped();
            case RuntimeModalKind.ContractBoard:
                CloseContractBoardModal();
                return CommandOkTyped();
            case RuntimeModalKind.Shop:
                CloseShopModal();
                return CommandOkTyped();
            case RuntimeModalKind.Forge:
                CloseForgeModal();
                return CommandOkTyped();
            case RuntimeModalKind.Stagecoach:
                CloseStagecoachModal();
                return CommandOkTyped();
            case RuntimeModalKind.CharacterInfo:
                OnCharacterInfoWindowClosed();
                return CommandOkTyped();
            case RuntimeModalKind.Party:
                ClosePartyManagementModal();
                return CommandOkTyped();
            case RuntimeModalKind.Warehouse:
                ClosePartyWarehouseModal();
                return CommandOkTyped();
            case RuntimeModalKind.SubmapConfirm:
                CancelSubmapEntryPrompt();
                return CommandOkTyped();
            case RuntimeModalKind.BattleStartConfirm:
                return CommandErrorTyped("当前战斗开始确认必须点击\"开始战斗\"。");
            case RuntimeModalKind.Promotion:
                return CommandErrorTyped("当前晋升选择必须确认后才能继续。");
            case RuntimeModalKind.Reward:
                return CommandErrorTyped("当前角色奖励必须确认后才能继续。");
            default:
                return CommandErrorTyped("当前没有可关闭的窗口。");
        }
    }

    public void OnCharacterInfoWindowClosed()
    {
        if (!HasRuntime())
            return;
        ClearActiveCharacterInfoContext();
        SetActiveModalKind(RuntimeModalKind.None);
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
                SetActiveModalKind(RuntimeModalKind.None);
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
        SetActiveModalKind(RuntimeModalKind.None);
        SyncPartyStateFromCharacterManagement();
        var persistError = PersistPartyState();
        if (delta.needs_promotion_modal)
        {
            SetPendingWorldPromotionPrompt(
                _runtime.BuildRuntimePromotionPromptInternal(delta, "确认后将在世界地图立即生效。")
            );
            SetActiveModalKind(RuntimeModalKind.Promotion);
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
            SetActiveModalKind(RuntimeModalKind.Promotion);
            UpdateStatus("当前晋升选择必须确认后才能继续战斗。");
            return;
        }

        if (GetPendingWorldPromotionPrompt().Count == 0)
        {
            UpdateStatus("当前晋升选择无法取消。");
            return;
        }
        SetActiveModalKind(RuntimeModalKind.Promotion);
        UpdateStatus("当前晋升选择必须确认后才能继续结算奖励。");
    }

    public void OnCharacterRewardConfirmed()
    {
        if (!HasRuntime() || GetActiveReward() == null)
            return;
        var reward = GetActiveReward();
        ClearActiveReward();
        SetActiveModalKind(RuntimeModalKind.None);

        var delta = ApplyPendingCharacterRewardToParty(reward);
        SyncPartyStateFromCharacterManagement();
        var persistError = PersistPartyState();
        if (delta != null && delta.needs_promotion_modal)
        {
            SetPendingWorldPromotionPrompt(
                _runtime.BuildRuntimePromotionPromptInternal(delta, "确认后将在世界地图立即生效。")
            );
            SetActiveModalKind(RuntimeModalKind.Promotion);
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
                delta.MasteryChangesTyped.Count == 0
                && delta.KnowledgeChangesTyped.Count == 0
                && delta.AttributeChangesTyped.Count == 0
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

    internal void EnqueuePendingCharacterRewardsTyped(
        System.Collections.Generic.IEnumerable<PendingCharacterReward> rewardOptions
    )
    {
        if (!HasRuntime())
            return;
        _runtime.EnqueueCharacterRewardsTyped(rewardOptions);
    }

    public bool PresentPendingRewardIfReady()
    {
        var activeModalKind = GetActiveModalKind();
        if (!HasRuntime() || IsBattleActive())
            return false;
        if (GetPendingWorldPromotionPrompt().Count > 0)
        {
            if (activeModalKind != RuntimeModalKind.Promotion)
            {
                SetActiveModalKind(RuntimeModalKind.Promotion);
                return true;
            }
            return false;
        }
        if (GetActiveReward() != null)
        {
            if (activeModalKind != RuntimeModalKind.Reward)
            {
                SetActiveModalKind(RuntimeModalKind.Reward);
                return true;
            }
            return false;
        }
        if (
            activeModalKind == RuntimeModalKind.Settlement
            || activeModalKind == RuntimeModalKind.ContractBoard
            || activeModalKind == RuntimeModalKind.Shop
            || activeModalKind == RuntimeModalKind.Forge
            || activeModalKind == RuntimeModalKind.Stagecoach
            || activeModalKind == RuntimeModalKind.CharacterInfo
            || activeModalKind == RuntimeModalKind.Party
            || activeModalKind == RuntimeModalKind.Warehouse
            || activeModalKind == RuntimeModalKind.SubmapConfirm
            || activeModalKind == RuntimeModalKind.BattleStartConfirm
            || activeModalKind == RuntimeModalKind.GameOver
        )
            return false;
        var partyState = GetPartyState();
        if (partyState == null)
            return false;
        if (partyState.pending_character_rewards.Count == 0)
            return false;

        SetActiveReward(partyState.GetNextPendingCharacterReward());
        if (GetActiveReward() == null)
            return false;
        SetActiveModalKind(RuntimeModalKind.Reward);
        return true;
    }

    private bool HasRuntime()
    {
        return _runtime != null;
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandOkTyped(string message = "")
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success(message ?? "");
    }

    private Dictionary CommandOk(string message = "")
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = true, ["message"] = message };
        return _runtime.BuildCommandOk(message);
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandErrorTyped(string message)
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.BuildCommandError(message);
    }

    private void RejectInvalidPromotionChoice()
    {
        SetActiveModalKind(RuntimeModalKind.Promotion);
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
        foreach (CharacterProgressionDelta delta in batch.ProgressionDeltasTyped)
        {
            if (PromotionDeltaApplied(delta, memberId, professionId))
                return true;
        }
        return false;
    }

    private bool BattlePromotionBatchNeedsFollowUp(BattleEventBatch batch, StringName memberId)
    {
        if (batch == null)
            return false;
        foreach (CharacterProgressionDelta delta in batch.ProgressionDeltasTyped)
        {
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
        return delta.HasChangedProfessionId(professionId);
    }

    private Dictionary GetPendingPromotionPrompt()
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime.GetPendingPromotionPrompt();
    }

    private Dictionary GetPendingWorldPromotionPrompt()
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime.GetPendingWorldPromotionPromptState();
    }

    private PendingCharacterReward GetActiveReward()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetActiveReward();
    }

    private RuntimeModalKind GetActiveModalKind()
    {
        return HasRuntime() ? _runtime.GetActiveModalKind() : RuntimeModalKind.None;
    }

    private void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        if (HasRuntime())
            _runtime.SetRuntimeActiveModalKind(modalKind);
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.UpdateStatus(message);
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.IsBattleActive();
    }

    private void ClearActiveCharacterInfoContext()
    {
        if (HasRuntime())
            _runtime.ClearActiveCharacterInfoContext();
    }

    private void CloseSettlementModal()
    {
        if (HasRuntime())
            _runtime.CloseSettlementModal();
    }

    private void CloseContractBoardModal()
    {
        if (HasRuntime())
            _runtime.CloseContractBoardModal();
    }

    private void CloseShopModal()
    {
        if (HasRuntime())
            _runtime.CloseShopModal();
    }

    private void CloseForgeModal()
    {
        if (HasRuntime())
            _runtime.CloseForgeModal();
    }

    private void CloseStagecoachModal()
    {
        if (HasRuntime())
            _runtime.CloseStagecoachModal();
    }

    private void ClosePartyManagementModal()
    {
        if (HasRuntime())
            _runtime.ClosePartyManagementModal();
    }

    private void ClosePartyWarehouseModal()
    {
        if (HasRuntime())
            _runtime.ClosePartyWarehouseModal();
    }

    private void CancelSubmapEntryPrompt()
    {
        if (HasRuntime())
            _runtime.CommandCancelSubmapEntryTyped();
    }

    private void ClearPendingPromotionPrompt()
    {
        if (HasRuntime())
            _runtime.ClearPendingPromotionPrompt();
    }

    private void ClearPendingWorldPromotionPrompt()
    {
        if (HasRuntime())
            _runtime.ClearPendingWorldPromotionPromptState();
    }

    private BattleEventBatch SubmitBattlePromotionChoice(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return null;
        return _runtime.SubmitBattlePromotionChoice(memberId, professionId, selection);
    }

    private void ApplyBattleBatch(BattleEventBatch batch)
    {
        if (HasRuntime() && batch != null)
            _runtime.ApplyBattleBatch(batch);
    }

    private CharacterProgressionDelta PromoteProfession(
        StringName memberId,
        StringName professionId,
        Dictionary selection
    )
    {
        if (!HasRuntime())
            return null;
        return _runtime.PromoteProfession(memberId, professionId, selection);
    }

    private void SyncPartyStateFromCharacterManagement()
    {
        if (HasRuntime())
            _runtime.SyncPartyStateFromCharacterManagement();
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return (Error)_runtime.PersistPartyState();
    }

    private void SetPendingWorldPromotionPrompt(Dictionary prompt)
    {
        if (HasRuntime())
            _runtime.SetPendingWorldPromotionPromptState(prompt);
    }

    private string GetMemberDisplayName(StringName memberId)
    {
        if (!HasRuntime())
            return memberId.ToString();
        return _runtime.GetMemberDisplayName(memberId);
    }

    private void ClearActiveReward()
    {
        if (HasRuntime())
            _runtime.ClearActiveRewardState();
    }

    private CharacterProgressionDelta ApplyPendingCharacterRewardToParty(
        PendingCharacterReward reward
    )
    {
        if (!HasRuntime())
            return null;
        return _runtime.ApplyPendingCharacterRewardToParty(reward);
    }

    private PartyState GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetPartyState();
    }

    private void SetActiveReward(PendingCharacterReward reward)
    {
        if (HasRuntime())
            _runtime.SetActiveRewardState(reward);
    }

    private static StringName DictionaryStringName(Dictionary dictionary, string key)
    {
        if (!TryReadString(dictionary, key, out string value))
            return "";
        return new StringName(value);
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

    private static bool TryReadString(Dictionary dictionary, string key, out string value)
    {
        value = "";
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return false;
        var rawValue = dictionary[key];
        if (rawValue.VariantType != Variant.Type.String)
            return false;
        value = rawValue.AsString();
        return true;
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
