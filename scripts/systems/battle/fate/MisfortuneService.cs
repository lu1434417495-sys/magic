using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal enum MisfortuneSkillKind
{
    Unknown = 0,
    BlackStarBrand,
    CrownBreak,
    DoomSentence,
    BlackCrownSeal,
}

internal sealed class MisfortuneService : IDisposable
{
    private static readonly StringName CalamityReasonOrdinaryMiss = "ordinary_miss";
    private static readonly StringName CalamityReasonCriticalFail = "critical_fail";
    private static readonly StringName CalamityReasonStrongDebuff = "strong_debuff";
    private static readonly StringName CalamityReasonAdjacentAllyDefeated =
        "adjacent_ally_defeated";
    private static readonly StringName CalamityReasonLowHpEndTurn = "low_hp_end_turn";
    private static readonly StringName CalamityReasonBossPhaseChanged = "boss_phase_changed";

    private static readonly StringName CalamityCapacityBonusStatId = "calamity_capacity_bonus";
    private static readonly StringName ReverseFortuneStatusId = "reverse_fortune";
    private static readonly StringName MisstepToSchemeSkillId = "misstep_to_scheme";
    private static readonly StringName BlackStarBrandSkillId =
        MisfortuneContentRules.BlackStarBrandSkillId;
    private static readonly StringName CrownBreakSkillId =
        MisfortuneContentRules.CrownBreakSkillId;
    private static readonly StringName DoomSentenceSkillId =
        MisfortuneContentRules.DoomSentenceSkillId;
    private static readonly StringName BlackCrownSealSkillId =
        MisfortuneContentRules.BlackCrownSealSkillId;

    private const int BaseCalamityCap = 3;
    private const int MaxCalamityCapacityBonus = 2;
    private const int ReverseFortuneDurationTu = 60;
    private const int BlackStarBrandRepeatCalamityCost = 1;
    private const int CrownBreakCalamityCost = 2;
    private const int DoomSentenceCalamityCost = 5;

    private static readonly StringName GateTypeBlackStarBrand = "black_star_brand";
    private static readonly StringName GateTypeCrownBreak = "crown_break";
    private static readonly StringName GateTypeDoomSentence = "doom_sentence";
    private static readonly StringName GateTypeBlackCrownSeal = "black_crown_seal";

    private static readonly Dictionary<StringName, MisfortuneSkillGateRule> MisfortuneSkillGateRules =
        new()
    {
        [BlackStarBrandSkillId] = new MisfortuneSkillGateRule(
            GateTypeBlackStarBrand,
            "黑星烙印的 calamity sidecar 未初始化。",
            "calamity 不足，无法施放黑星烙印。"
        ),
        [CrownBreakSkillId] = new MisfortuneSkillGateRule(
            GateTypeCrownBreak,
            "折冠的 calamity sidecar 未初始化。",
            "calamity 不足，无法施放折冠。"
        ),
        [DoomSentenceSkillId] = new MisfortuneSkillGateRule(
            GateTypeDoomSentence,
            "厄命宣判的 calamity sidecar 未初始化。",
            "calamity 不足，无法施放厄命宣判。"
        ),
        [BlackCrownSealSkillId] = new MisfortuneSkillGateRule(
            GateTypeBlackCrownSeal,
            "黑冠封印的 battle sidecar 未初始化。",
            "黑冠封印每战只能施放 1 次。"
        ),
    };

    private BattleFateEventBus _fateEventBus = null;
    private Func<StringName, BattleUnitState> _unitByMemberIdResolver;
    private BattleCalamityStore _calamityByMemberId = new();
    private readonly Dictionary<StringName, HashSet<StringName>> _reasonFlagsByMemberId = new();
    private readonly HashSet<StringName> _processedAdjacentDefeatUnitIds = new();
    private readonly HashSet<StringName> _misstepToSchemeUsedByMemberId = new();
    private readonly HashSet<StringName> _blackStarBrandFreeUsedByMemberId = new();
    private readonly HashSet<StringName> _blackCrownSealUsedByMemberId = new();
    private readonly HashSet<StringName> _doomSentenceUsedByMemberId = new();

    internal static StringName ToStringName(MisfortuneSkillKind kind)
    {
        return kind switch
        {
            MisfortuneSkillKind.BlackStarBrand => BlackStarBrandSkillId,
            MisfortuneSkillKind.CrownBreak => CrownBreakSkillId,
            MisfortuneSkillKind.DoomSentence => DoomSentenceSkillId,
            MisfortuneSkillKind.BlackCrownSeal => BlackCrownSealSkillId,
            _ => "",
        };
    }

    public static bool IsMisfortuneGatedSkill(StringName skillId)
    {
        return MisfortuneContentRules.IsGatedSkill(skillId);
    }

    public static string GetSkillSidecarMissingMessage(StringName skillId)
    {
        return TryGetSkillGateRule(skillId, out MisfortuneSkillGateRule rule)
            ? rule.SidecarMissingMessage
            : "Misfortune battle sidecar 未初始化。";
    }

    public static string GetSkillDefaultBlockMessage(StringName skillId)
    {
        return TryGetSkillGateRule(skillId, out MisfortuneSkillGateRule rule)
            ? rule.DefaultBlockMessage
            : "calamity 不足，无法施放该技能。";
    }

    private static bool TryGetSkillGateRule(
        StringName skillId,
        out MisfortuneSkillGateRule rule
    )
    {
        rule = default(MisfortuneSkillGateRule);
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        return normalizedSkillId != ""
            && MisfortuneSkillGateRules.TryGetValue(normalizedSkillId, out rule);
    }

    internal void Setup(
        BattleFateEventBus fateEventBus,
        Func<StringName, BattleUnitState> unitByMemberIdResolver
    )
    {
        _unitByMemberIdResolver = unitByMemberIdResolver;
        BindFateEventBus(fateEventBus);
    }

    internal void BeginBattle(BattleCalamityStore calamityStore)
    {
        _reasonFlagsByMemberId.Clear();
        _processedAdjacentDefeatUnitIds.Clear();
        _misstepToSchemeUsedByMemberId.Clear();
        _blackStarBrandFreeUsedByMemberId.Clear();
        _blackCrownSealUsedByMemberId.Clear();
        _doomSentenceUsedByMemberId.Clear();
        _calamityByMemberId = calamityStore ?? new BattleCalamityStore();
        _calamityByMemberId.Clear();
    }

    private void BindFateEventBus(BattleFateEventBus fateEventBus)
    {
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched -= _OnFateEvent;
        _fateEventBus = fateEventBus;
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched += _OnFateEvent;
    }

    public void Dispose()
    {
        System.GC.SuppressFinalize(this);
        BindFateEventBus(null);
        _unitByMemberIdResolver = null;
        _calamityByMemberId = new BattleCalamityStore();
        _reasonFlagsByMemberId.Clear();
        _processedAdjacentDefeatUnitIds.Clear();
        _misstepToSchemeUsedByMemberId.Clear();
        _blackStarBrandFreeUsedByMemberId.Clear();
        _blackCrownSealUsedByMemberId.Clear();
        _doomSentenceUsedByMemberId.Clear();
    }

    internal Dictionary<StringName, int> GetCalamityByMemberIdSnapshot()
    {
        return _calamityByMemberId?.Snapshot() ?? new Dictionary<StringName, int>();
    }

    internal int GetMemberCalamity(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return 0;
        return Mathf.Max(ReadCalamityValue(normalizedMemberId), 0);
    }

    internal int GetMemberCalamityCap(StringName memberId)
    {
        return _CalculateCalamityCap(_ResolveUnitByMemberId(memberId));
    }

    internal int GetBlackStarBrandCalamityCost(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return BlackStarBrandRepeatCalamityCost;
        if (!_blackStarBrandFreeUsedByMemberId.Contains(normalizedMemberId))
            return 0;
        return BlackStarBrandRepeatCalamityCost;
    }

    private bool CanCastBlackStarBrand(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return false;
        int calamityCost = GetBlackStarBrandCalamityCost(memberId);
        return calamityCost <= 0 || GetMemberCalamity(memberId) >= calamityCost;
    }

    internal string GetSkillCastBlockMessage(BattleUnitState unitState, StringName skillId)
    {
        if (!TryGetSkillGateRule(skillId, out MisfortuneSkillGateRule rule))
            return "";
        var gateType = rule.GateType;
        switch ((string)gateType)
        {
            case "black_star_brand":
                return CanCastBlackStarBrand(unitState) ? "" : GetSkillDefaultBlockMessage(skillId);
            case "crown_break":
                return CanCastCrownBreak(unitState) ? "" : GetSkillDefaultBlockMessage(skillId);
            case "doom_sentence":
                return GetDoomSentenceCastBlockReason(unitState);
            case "black_crown_seal":
                return GetBlackCrownSealCastBlockReason(unitState);
            default:
                return "";
        }
    }

    internal MisfortuneSkillCastResult ConsumeSkillCastResult(
        BattleUnitState unitState,
        StringName skillId
    )
    {
        if (!TryGetSkillGateRule(skillId, out MisfortuneSkillGateRule rule))
            return MisfortuneSkillCastResult.Success(
                unitState != null
                    ? ProgressionDataUtils.to_string_name(unitState.source_member_id)
                    : default,
                gated: false
            );
        var gateType = rule.GateType;
        switch ((string)gateType)
        {
            case "black_star_brand":
                return ConsumeBlackStarBrandCastResult(unitState);
            case "crown_break":
                return ConsumeCrownBreakCastResult(unitState);
            case "doom_sentence":
                return ConsumeDoomSentenceCastResult(unitState);
            case "black_crown_seal":
                return ConsumeBlackCrownSealCastResult(unitState);
            default:
                return MisfortuneSkillCastResult.Success(
                    unitState != null
                        ? ProgressionDataUtils.to_string_name(unitState.source_member_id)
                        : default,
                    gated: false
                );
        }
    }

    private MisfortuneSkillCastResult ConsumeBlackStarBrandCastResult(BattleUnitState unitState)
    {
        if (unitState == null)
            return MisfortuneSkillCastResult.Failure(
                "技能施放者无效。",
                calamityCost: BlackStarBrandRepeatCalamityCost
            );
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return MisfortuneSkillCastResult.Failure(
                "黑星烙印只能由正式成员施放。",
                calamityCost: BlackStarBrandRepeatCalamityCost
            );
        int calamityCost = GetBlackStarBrandCalamityCost(memberId);
        int currentCalamity = GetMemberCalamity(memberId);
        if (calamityCost > 0 && currentCalamity < calamityCost)
            return MisfortuneSkillCastResult.Failure(
                "calamity 不足，无法施放黑星烙印。",
                memberId,
                calamityCost,
                currentCalamity
            );
        if (calamityCost > 0)
            _calamityByMemberId.Put(memberId, currentCalamity - calamityCost);
        _blackStarBrandFreeUsedByMemberId.Add(memberId);
        return MisfortuneSkillCastResult.Success(
            memberId,
            calamityCost: calamityCost,
            remainingCalamity: GetMemberCalamity(memberId),
            freeCast: calamityCost <= 0
        );
    }

    private bool CanCastCrownBreak(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return false;
        return GetMemberCalamity(memberId) >= CrownBreakCalamityCost;
    }

    private MisfortuneSkillCastResult ConsumeCrownBreakCastResult(BattleUnitState unitState)
    {
        if (unitState == null)
            return MisfortuneSkillCastResult.Failure(
                "技能施放者无效。",
                calamityCost: CrownBreakCalamityCost
            );
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return MisfortuneSkillCastResult.Failure(
                "折冠只能由正式成员施放。",
                calamityCost: CrownBreakCalamityCost
            );
        int currentCalamity = GetMemberCalamity(memberId);
        if (currentCalamity < CrownBreakCalamityCost)
            return MisfortuneSkillCastResult.Failure(
                "calamity 不足，无法施放折冠。",
                memberId,
                CrownBreakCalamityCost,
                currentCalamity
            );
        _calamityByMemberId.Put(memberId, currentCalamity - CrownBreakCalamityCost);
        return MisfortuneSkillCastResult.Success(
            memberId,
            calamityCost: CrownBreakCalamityCost,
            remainingCalamity: GetMemberCalamity(memberId)
        );
    }

    private string GetDoomSentenceCastBlockReason(BattleUnitState unitState)
    {
        if (unitState == null)
            return "技能施放者无效。";
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return "厄命宣判只能由正式成员施放。";
        if (_doomSentenceUsedByMemberId.Contains(memberId))
            return "厄命宣判每战只能施放 1 次。";
        if (GetMemberCalamityCap(memberId) < DoomSentenceCalamityCost)
            return "本战 calamity 上限不足 5，无法施放厄命宣判。";
        if (GetMemberCalamity(memberId) < DoomSentenceCalamityCost)
            return "calamity 不足，无法施放厄命宣判。";
        return "";
    }

    private bool CanCastDoomSentence(BattleUnitState unitState)
    {
        return string.IsNullOrEmpty(GetDoomSentenceCastBlockReason(unitState));
    }

    private MisfortuneSkillCastResult ConsumeDoomSentenceCastResult(BattleUnitState unitState)
    {
        var blockReason = GetDoomSentenceCastBlockReason(unitState);
        var memberId =
            unitState != null
                ? ProgressionDataUtils.to_string_name(unitState.source_member_id)
                : new StringName("");
        if (!string.IsNullOrEmpty(blockReason))
            return MisfortuneSkillCastResult.Failure(
                blockReason,
                memberId,
                DoomSentenceCalamityCost,
                GetMemberCalamity(memberId)
            );
        int currentCalamity = GetMemberCalamity(memberId);
        _calamityByMemberId.Put(memberId, currentCalamity - DoomSentenceCalamityCost);
        _doomSentenceUsedByMemberId.Add(memberId);
        return MisfortuneSkillCastResult.Success(
            memberId,
            calamityCost: DoomSentenceCalamityCost,
            remainingCalamity: GetMemberCalamity(memberId)
        );
    }

    private string GetBlackCrownSealCastBlockReason(BattleUnitState unitState)
    {
        if (unitState == null)
            return "技能施放者无效。";
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return "黑冠封印只能由正式成员施放。";
        if (_blackCrownSealUsedByMemberId.Contains(memberId))
            return "黑冠封印每战只能施放 1 次。";
        return "";
    }

    private bool CanCastBlackCrownSeal(BattleUnitState unitState)
    {
        return string.IsNullOrEmpty(GetBlackCrownSealCastBlockReason(unitState));
    }

    private MisfortuneSkillCastResult ConsumeBlackCrownSealCastResult(BattleUnitState unitState)
    {
        var blockReason = GetBlackCrownSealCastBlockReason(unitState);
        var memberId =
            unitState != null
                ? ProgressionDataUtils.to_string_name(unitState.source_member_id)
                : new StringName("");
        if (!string.IsNullOrEmpty(blockReason))
            return MisfortuneSkillCastResult.Failure(blockReason, memberId);
        _blackCrownSealUsedByMemberId.Add(memberId);
        return MisfortuneSkillCastResult.Success(memberId);
    }

    internal bool HasTriggeredReason(StringName memberId, StringName reasonId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        if (normalizedMemberId == "" || normalizedReasonId == "")
            return false;
        return _reasonFlagsByMemberId.TryGetValue(
                normalizedMemberId,
                out HashSet<StringName> memberReasonFlags
            )
            && memberReasonFlags.Contains(normalizedReasonId);
    }

    internal GDictionary HandleTrigger(MisfortuneTriggerRequest request)
    {
        var normalizedReasonId = ProgressionDataUtils.to_string_name(request.ReasonId);
        switch ((string)normalizedReasonId)
        {
            case "strong_debuff":
                return _HandleStrongDebuffTrigger(request);
            case "adjacent_ally_defeated":
            {
                GArray results = _HandleAdjacentAllyDefeatTrigger(request);
                return new GDictionary
                {
                    ["result_count"] = results.Count,
                    ["results"] = results,
                };
            }
            case "low_hp_end_turn":
                return _HandleLowHpTurnEndTrigger(request);
            case "boss_phase_changed":
                return _HandleBossPhaseChangedTrigger(request);
            case "ordinary_miss":
            case "critical_fail":
                return _HandleMemberReasonTrigger(request, normalizedReasonId);
            default:
                return new GDictionary();
        }
    }

    internal GDictionary HandleAppliedStatuses(BattleUnitState targetUnit, GArray statusEffectIds)
    {
        return HandleTrigger(
            MisfortuneTriggerRequest.StrongDebuff(
                targetUnit,
                ReadStringNameItems(statusEffectIds)
            )
        );
    }

    internal GDictionary HandleAppliedStatuses(
        BattleUnitState targetUnit,
        GStringNameArray statusEffectIds
    )
    {
        var typedStatusIds = new List<StringName>();
        if (statusEffectIds != null)
        {
            foreach (StringName statusId in statusEffectIds)
            {
                if (statusId != "")
                {
                    typedStatusIds.Add(statusId);
                }
            }
        }
        return HandleTrigger(MisfortuneTriggerRequest.StrongDebuff(targetUnit, typedStatusIds));
    }

    internal GDictionary HandleAppliedStatuses(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    )
    {
        var typedStatusIds = new List<StringName>();
        foreach (StringName statusId in statusEffectIds ?? Array.Empty<StringName>())
        {
            if (statusId != "")
                typedStatusIds.Add(statusId);
        }
        return HandleTrigger(MisfortuneTriggerRequest.StrongDebuff(targetUnit, typedStatusIds));
    }

    private GDictionary _HandleStrongDebuffTrigger(MisfortuneTriggerRequest request)
    {
        var targetUnit = request.TargetUnit;
        if (targetUnit == null)
            return new GDictionary();
        var strongStatusIds = _ExtractStrongAttackDebuffIds(request.StatusEffectIds);
        if (strongStatusIds.Count == 0)
            return new GDictionary();
        return _RegisterReason(
            targetUnit,
            CalamityReasonStrongDebuff,
            new GDictionary
            {
                ["status_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                    strongStatusIds
                ),
            }
        );
    }

    private GArray _HandleAdjacentAllyDefeatTrigger(MisfortuneTriggerRequest request)
    {
        var defeatedUnit = request.DefeatedUnit;
        var results = new GArray();
        if (defeatedUnit == null || defeatedUnit.unit_id == "")
            return results;
        if (_processedAdjacentDefeatUnitIds.Contains(defeatedUnit.unit_id))
            return results;
        _processedAdjacentDefeatUnitIds.Add(defeatedUnit.unit_id);
        foreach (BattleUnitState observerUnit in request.AdjacentUnits)
        {
            if (observerUnit == null)
                continue;
            var result = _RegisterReason(
                observerUnit,
                CalamityReasonAdjacentAllyDefeated,
                new GDictionary { ["defeated_unit_id"] = defeatedUnit.unit_id.ToString() }
            );
            if (result.Count > 0)
                results.Add(result);
        }
        return results;
    }

    private GDictionary _HandleLowHpTurnEndTrigger(MisfortuneTriggerRequest request)
    {
        var unitState = request.UnitState;
        if (unitState == null || !unitState.IsAlive() || !_IsLowHpHardship(unitState))
            return new GDictionary();
        return _RegisterReason(unitState, CalamityReasonLowHpEndTurn);
    }

    private GDictionary _HandleBossPhaseChangedTrigger(MisfortuneTriggerRequest request)
    {
        var unitState = request.UnitState;
        var phaseId = request.PhaseId;
        return _RegisterReason(
            unitState,
            CalamityReasonBossPhaseChanged,
            new GDictionary
            {
                ["phase_id"] = ProgressionDataUtils.to_string_name(phaseId).ToString(),
            }
        );
    }

    private GDictionary _HandleMemberReasonTrigger(
        MisfortuneTriggerRequest request,
        StringName reasonId
    )
    {
        var unitState = request.UnitState;
        if (unitState != null)
            return _RegisterReason(unitState, reasonId);
        var memberId = request.MemberId;
        if (memberId == "")
            return new GDictionary();
        var resolvedUnit = _ResolveUnitByMemberId(memberId);
        if (resolvedUnit == null)
            return new GDictionary();
        return _RegisterReason(resolvedUnit, reasonId);
    }

    private void _OnFateEvent(BattleFateEventPayload payload)
    {
        if (payload == null)
            return;
        switch ((string)payload.EventType)
        {
            case "ordinary_miss":
                HandleTrigger(MisfortuneTriggerRequest.FromFateEvent(payload));
                break;
            case "critical_fail":
                HandleTrigger(MisfortuneTriggerRequest.FromFateEvent(payload));
                break;
        }
    }

    private GDictionary _RegisterReason(
        BattleUnitState unitState,
        StringName reasonId,
        GDictionary metadata = null
    )
    {
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        if (unitState == null || normalizedReasonId == "")
            return new GDictionary();
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return new GDictionary();
        HashSet<StringName> memberReasonFlags = _EnsureMemberReasonFlags(memberId);
        bool wasFirstReason = memberReasonFlags.Count == 0;
        if (memberReasonFlags.Contains(normalizedReasonId))
            return new GDictionary
            {
                ["member_id"] = memberId.ToString(),
                ["reason_id"] = normalizedReasonId.ToString(),
                ["granted"] = false,
                ["already_triggered"] = true,
                ["calamity"] = GetMemberCalamity(memberId),
                ["cap"] = _CalculateCalamityCap(unitState),
            };
        memberReasonFlags.Add(normalizedReasonId);
        int previousCalamity = GetMemberCalamity(memberId);
        int calamityCap = _CalculateCalamityCap(unitState);
        int intendedGain = 1 + _GetBonusCalamityForReason(unitState, normalizedReasonId);
        int nextCalamity = Mathf.Min(previousCalamity + intendedGain, calamityCap);
        int grantedCalamity = Mathf.Max(nextCalamity - previousCalamity, 0);
        int bonusCalamity = Mathf.Max(grantedCalamity - 1, 0);
        _calamityByMemberId.Put(memberId, nextCalamity);
        bool reverseFortuneGranted = false;
        if (wasFirstReason && normalizedReasonId == CalamityReasonCriticalFail)
            reverseFortuneGranted = _GrantReverseFortune(unitState);
        return new GDictionary
        {
            ["member_id"] = memberId.ToString(),
            ["reason_id"] = normalizedReasonId.ToString(),
            ["granted"] = grantedCalamity > 0,
            ["already_triggered"] = false,
            ["calamity"] = nextCalamity,
            ["bonus_calamity"] = bonusCalamity,
            ["cap"] = calamityCap,
            ["reverse_fortune_granted"] = reverseFortuneGranted,
            ["metadata"] = metadata ?? new GDictionary(),
        };
    }

    private bool _GrantReverseFortune(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        BattleStatusEffectState statusEntry = BuildSourceStatus(
            unitState,
            ReverseFortuneStatusId,
            1,
            ReverseFortuneDurationTu
        );
        unitState.SetStatusEffect(statusEntry);
        return true;
    }

    private static BattleStatusEffectState BuildSourceStatus(
        BattleUnitState unitState,
        StringName statusId,
        int power,
        int durationTu
    )
    {
        return new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = unitState?.unit_id ?? new StringName(""),
            power = power,
            stacks = 1,
            duration = durationTu,
        };
    }

    private int _CalculateCalamityCap(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return BaseCalamityCap;
        int calamityCapacityBonus = Mathf.Min(
            Mathf.Max(
                unitState
                    .attribute_snapshot.GetValue(CalamityCapacityBonusStatId),
                0
            ),
            MaxCalamityCapacityBonus
        );
        int hiddenLuckAtBirth = unitState
            .attribute_snapshot.GetValue("hidden_luck_at_birth");
        return BaseCalamityCap + calamityCapacityBonus + (hiddenLuckAtBirth <= -5 ? 1 : 0);
    }

    private StringNameList _ExtractStrongAttackDebuffIds(
        IEnumerable<StringName> statusEffectIds
    )
    {
        var strongStatusIds = new StringNameList();
        if (statusEffectIds == null)
            return strongStatusIds;
        foreach (StringName statusIdValue in statusEffectIds)
        {
            var statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "")
                continue;
            if (!BattleState.IsStrongAttackDisadvantageStatusId(statusId))
                continue;
            if (strongStatusIds.Contains(statusId))
                continue;
            strongStatusIds.Add(statusId);
        }
        return strongStatusIds;
    }

    private static IReadOnlyList<StringName> ReadStringNameItems(GArray values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(value);
            if (statusId != "" && !result.Contains(statusId))
                result.Add(statusId);
        }
        return result;
    }

    private bool _IsLowHpHardship(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return false;
        int maxHp = Mathf.Max(
            unitState.attribute_snapshot.GetValue("hp_max"),
            0
        );
        if (maxHp <= 0)
            return false;
        return unitState.GetCurrentHp() * 100
            <= maxHp * BattleState.LowHpAttackDisadvantagePercent;
    }

    private HashSet<StringName> _EnsureMemberReasonFlags(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return new HashSet<StringName>();
        if (!_reasonFlagsByMemberId.TryGetValue(
                normalizedMemberId,
                out HashSet<StringName> memberReasonFlags
            ))
        {
            memberReasonFlags = new HashSet<StringName>();
            _reasonFlagsByMemberId[normalizedMemberId] = memberReasonFlags;
        }
        return memberReasonFlags;
    }

    private BattleUnitState _ResolveUnitByMemberId(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "" || _unitByMemberIdResolver == null)
            return null;
        return _unitByMemberIdResolver.Invoke(normalizedMemberId);
    }

    private int _GetBonusCalamityForReason(BattleUnitState unitState, StringName reasonId)
    {
        if (unitState == null || reasonId != CalamityReasonCriticalFail)
            return 0;
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return 0;
        if (_misstepToSchemeUsedByMemberId.Contains(memberId))
            return 0;
        if (!_UnitHasSkill(unitState, MisstepToSchemeSkillId))
            return 0;
        _misstepToSchemeUsedByMemberId.Add(memberId);
        return 1;
    }

    private bool _UnitHasSkill(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return false;
        if (unitState.KnowsActiveSkill(skillId))
            return true;
        return unitState.GetKnownSkillLevelTyped(skillId) > 0;
    }

    private readonly record struct MisfortuneSkillGateRule(
        StringName GateType,
        string SidecarMissingMessage,
        string DefaultBlockMessage
    );

    private int ReadCalamityValue(StringName memberId)
    {
        return _calamityByMemberId?.Get(memberId) ?? 0;
    }

}
