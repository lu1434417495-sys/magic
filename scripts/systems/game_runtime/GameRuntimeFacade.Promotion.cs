using Godot;
using System;
using System.Linq;
using GDictionary = Godot.Collections.Dictionary;

public sealed partial class GameRuntimeFacade
{
    private readonly GameRuntimePromotionNotifications _promotion_notifications = new();

    internal int GetPromotionReadyMemberCount() => _promotion_notifications.MemberCount;
    internal string GetPromotionReminderText() => _promotion_notifications.Hint;

    // Commands and completed battle batches invalidate the query. Idle frames only inspect
    // the domain revision and dispatch already discovered opportunities at a safe modal boundary.
    internal bool RefreshPromotionNotifications(bool invalidate = false)
    {
        if (_disposed || _character_management == null || _party_state == null)
            return false;
        if (invalidate) _promotion_notifications.Invalidate();
        bool changed = false;
        if (_promotion_notifications.NeedsRefresh(_character_management.PromotionAvailabilityRevision))
        {
            var opportunities = new System.Collections.Generic.List<GameRuntimePromotionNotifications.Opportunity>();
            var names = new System.Collections.Generic.Dictionary<StringName, string>();
            foreach (PartyMemberState member in _party_state.GetMemberStates())
            {
                if (IsBattleActive() && _battle_runtime._find_unit_by_member_id(member.member_id)?.IsAlive() != true)
                    continue;
                names[member.member_id] = GetMemberDisplayName(member.member_id);
                foreach (var offer in _character_management.GetPromotionOffers(member.member_id))
                {
                    if (offer.DefaultSelection is not { IsWellFormed: true } selection) continue;
                    foreach (var professionId in offer.CandidateProfessionIdsTyped)
                        opportunities.Add(new(member.member_id, professionId,
                            selection.TargetRank, selection.GrowthTriggerSkillId));
                }
            }
            changed = _promotion_notifications.Refresh(_character_management.PromotionAvailabilityRevision,
                opportunities, names);
            if (changed && _promotion_notifications.MemberCount > 0)
                _log_runtime_event(GameLogLevel.Info, "progression", "promotion.available",
                    _promotion_notifications.Hint);
        }
        if (_active_modal_kind != RuntimeModalKind.None || HasPendingBattleGenerationRequest()
            || (IsBattleActive() && _is_battle_finished()))
            return changed;
        foreach (StringName memberId in _promotion_notifications.UnpresentedMembers)
        {
            if (OpenPromotion(memberId).Ok)
                return true;
        }
        return changed;
    }

    internal RuntimeCommandResult CommandOpenPromotionTyped(StringName memberId = default) =>
        ExecuteLoggedCommandTyped("promotion.open", "promotion",
            new GDictionary { ["member_id"] = memberId }, () => OpenPromotion(memberId));

    private RuntimeCommandResult OpenPromotion(StringName memberId)
    {
        memberId ??= "";
        if (_character_management == null || _party_state == null)
            return BuildCommandErrorResult("运行时尚未初始化。");
        if (_active_modal_kind != RuntimeModalKind.None && _active_modal_kind != RuntimeModalKind.Party)
            return BuildCommandErrorResult("请先关闭当前窗口。");
        foreach (PartyMemberState member in _party_state.GetMemberStates()
            .OrderBy(member => (string)member.member_id, StringComparer.Ordinal))
        {
            if (memberId != "" && member.member_id != memberId)
                continue;
            if (IsBattleActive() && !_battle_runtime.CanOpenPromotion(member.member_id))
                continue;
            var offers = _character_management.GetPromotionOffers(member.member_id);
            if (offers.Count == 0)
                continue;
            var delta = new CharacterProgressionDelta { member_id = member.member_id };
            foreach (var offer in offers)
                delta.AddPendingProfessionChoice(offer);
            var prompt = _battle_session_facade.BuildPromotionPrompt(delta,
                "确认后人物等级提升 1 级；所选成长技能成为核心。本次只使用该技能的成长机会。可随时暂缓。");
            if (prompt.IsEmpty)
                continue;
            if (IsBattleActive())
            {
                if (!_battle_runtime.OpenPromotion(member.member_id))
                    return BuildCommandErrorResult("当前战斗状态无法打开晋升。");
                SetPendingPromotionPrompt(prompt);
            }
            else
                SetPendingWorldPromotionPromptState(prompt);
            _promotion_notifications.MarkPresented(prompt);
            SetRuntimeActiveModalKind(RuntimeModalKind.Promotion);
            UpdateStatus($"请选择 {GetMemberDisplayName(member.member_id)} 的成长技能和职业。");
            return BuildCommandOkResult();
        }
        return BuildCommandErrorResult("当前没有可晋升方案。先将一个未用于成长的技能练到基础上限，并满足职业条件。");
    }
}
