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

internal partial class MisfortuneService : RefCounted
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
    private static readonly StringName BlackStarBrandSkillId = "black_star_brand";
    private static readonly StringName CrownBreakSkillId = "crown_break";
    private static readonly StringName DoomSentenceSkillId = "doom_sentence";
    private static readonly StringName BlackCrownSealSkillId = "black_crown_seal";

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
    private GDictionary _calamityByMemberId = new();
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
        return MisfortuneSkillGateRules.ContainsKey(ProgressionDataUtils.to_string_name(skillId));
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

    internal void BeginBattle(GDictionary calamityStore)
    {
        _reasonFlagsByMemberId.Clear();
        _processedAdjacentDefeatUnitIds.Clear();
        _misstepToSchemeUsedByMemberId.Clear();
        _blackStarBrandFreeUsedByMemberId.Clear();
        _blackCrownSealUsedByMemberId.Clear();
        _doomSentenceUsedByMemberId.Clear();
        _calamityByMemberId = calamityStore != null ? calamityStore : new GDictionary();
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

    public new void Dispose()
    {
        System.GC.SuppressFinalize(this);
        BindFateEventBus(null);
        _unitByMemberIdResolver = null;
        _calamityByMemberId = new GDictionary();
        _reasonFlagsByMemberId.Clear();
        _processedAdjacentDefeatUnitIds.Clear();
        _misstepToSchemeUsedByMemberId.Clear();
        _blackStarBrandFreeUsedByMemberId.Clear();
        _blackCrownSealUsedByMemberId.Clear();
        _doomSentenceUsedByMemberId.Clear();
        base.Dispose();
    }

    internal Dictionary<StringName, int> GetCalamityByMemberIdSnapshot()
    {
        var result = new Dictionary<StringName, int>();
        foreach (Variant key in _calamityByMemberId.Keys)
        {
            var memberId = ProgressionDataUtils.to_string_name(key);
            if (memberId == "")
                continue;
            int value = Mathf.Max(_calamityByMemberId[key].AsInt32(), 0);
            if (value > 0)
                result[memberId] = value;
        }
        return result;
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

    internal string GetSkillCastBlockReason(BattleUnitState unitState, StringName skillId)
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
            _calamityByMemberId[memberId] = Mathf.Max(currentCalamity - calamityCost, 0);
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
        _calamityByMemberId[memberId] = Mathf.Max(currentCalamity - CrownBreakCalamityCost, 0);
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
        _calamityByMemberId[memberId] = Mathf.Max(currentCalamity - DoomSentenceCalamityCost, 0);
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

    internal GDictionary HandleTrigger(StringName reasonId, GDictionary payload)
    {
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        switch ((string)normalizedReasonId)
        {
            case "strong_debuff":
                return _HandleStrongDebuffTrigger(payload);
            case "adjacent_ally_defeated":
            {
                GArray results = _HandleAdjacentAllyDefeatTrigger(payload);
                return new GDictionary
                {
                    ["result_count"] = results.Count,
                    ["results"] = results,
                };
            }
            case "low_hp_end_turn":
                return _HandleLowHpTurnEndTrigger(payload);
            case "boss_phase_changed":
                return _HandleBossPhaseChangedTrigger(payload);
            case "ordinary_miss":
            case "critical_fail":
                return _HandleMemberReasonTrigger(payload, normalizedReasonId);
            default:
                return new GDictionary();
        }
    }

    internal GDictionary HandleAppliedStatuses(BattleUnitState targetUnit, GArray statusEffectIds)
    {
        return _HandleStrongDebuffTrigger(
            new GDictionary
            {
                ["target_unit"] = targetUnit,
                ["status_effect_ids"] = statusEffectIds,
            }
        );
    }

    internal GDictionary HandleAppliedStatuses(
        BattleUnitState targetUnit,
        GStringNameArray statusEffectIds
    )
    {
        var typedStatusIds = new List<StringName>();
        foreach (StringName statusId in statusEffectIds ?? new GStringNameArray())
        {
            if (statusId != "")
            {
                typedStatusIds.Add(statusId);
            }
        }
        return HandleAppliedStatusesTyped(targetUnit, typedStatusIds);
    }

    private GDictionary _HandleStrongDebuffTrigger(GDictionary payload)
    {
        var targetUnit = ReadBattleUnit(payload, "target_unit");
        if (targetUnit == null)
            return new GDictionary();
        var statusEffectIds = new List<StringName>();
        if (
            TryReadArray(payload, "status_effect_ids", out GArray statusEffectIdArray)
        )
        {
            foreach (Variant statusIdValue in statusEffectIdArray)
            {
                var statusId = ProgressionDataUtils.to_string_name(statusIdValue);
                if (statusId != "")
                {
                    statusEffectIds.Add(statusId);
                }
            }
        }
        var strongStatusIds = _ExtractStrongAttackDebuffIds(statusEffectIds);
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

    private GDictionary HandleAppliedStatusesTyped(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    )
    {
        if (targetUnit == null)
            return new GDictionary();
        var strongStatusIds = _ExtractStrongAttackDebuffIds(statusEffectIds);
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

    private GArray _HandleAdjacentAllyDefeatTrigger(GDictionary payload)
    {
        var defeatedUnit = ReadBattleUnit(payload, "defeated_unit");
        GArray adjacentUnits = ReadArray(payload, "adjacent_units");
        var results = new GArray();
        if (defeatedUnit == null || defeatedUnit.unit_id == "")
            return results;
        if (_processedAdjacentDefeatUnitIds.Contains(defeatedUnit.unit_id))
            return results;
        _processedAdjacentDefeatUnitIds.Add(defeatedUnit.unit_id);
        foreach (var unitValue in adjacentUnits)
        {
            var observerUnit = unitValue.AsGodotObject() as BattleUnitState;
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

    private GDictionary _HandleLowHpTurnEndTrigger(GDictionary payload)
    {
        var unitState = ReadBattleUnit(payload, "unit_state");
        if (unitState == null || !unitState.is_alive || !_IsLowHpHardship(unitState))
            return new GDictionary();
        return _RegisterReason(unitState, CalamityReasonLowHpEndTurn);
    }

    private GDictionary _HandleBossPhaseChangedTrigger(GDictionary payload)
    {
        var unitState = ReadBattleUnit(payload, "unit_state");
        var phaseId = ReadStringName(payload, "phase_id");
        return _RegisterReason(
            unitState,
            CalamityReasonBossPhaseChanged,
            new GDictionary
            {
                ["phase_id"] = ProgressionDataUtils.to_string_name(phaseId).ToString(),
            }
        );
    }

    private GDictionary _HandleMemberReasonTrigger(GDictionary payload, StringName reasonId)
    {
        var unitState = ReadBattleUnit(payload, "unit_state");
        if (unitState != null)
            return _RegisterReason(unitState, reasonId);
        var memberId = ReadStringName(payload, "member_id");
        if (memberId == "")
            return new GDictionary();
        var resolvedUnit = _ResolveUnitByMemberId(memberId);
        if (resolvedUnit == null)
            return new GDictionary();
        return _RegisterReason(resolvedUnit, reasonId);
    }

    private void _OnFateEvent(StringName eventType, GDictionary payload)
    {
        switch ((string)eventType)
        {
            case "ordinary_miss":
                _HandleFatePayloadReason(payload, CalamityReasonOrdinaryMiss);
                break;
            case "critical_fail":
                _HandleFatePayloadReason(payload, CalamityReasonCriticalFail);
                break;
        }
    }

    private void _HandleFatePayloadReason(GDictionary payload, StringName reasonId)
    {
        var memberId = ReadStringName(payload, "attacker_member_id");
        if (memberId == "")
            return;
        _HandleMemberReasonTrigger(new GDictionary { ["member_id"] = memberId }, reasonId);
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
        _calamityByMemberId[memberId] = nextCalamity;
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
            ["metadata"] =
                metadata != null ? (GDictionary)metadata.Duplicate(true) : new GDictionary(),
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

    private Godot.Collections.Array<StringName> _ExtractStrongAttackDebuffIds(
        IEnumerable<StringName> statusEffectIds
    )
    {
        var strongStatusIds = new Godot.Collections.Array<StringName>();
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
        return unitState.current_hp * 100
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
        if (unitState.known_active_skill_ids.Contains(skillId))
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
        if (_calamityByMemberId == null || memberId == "" || !_calamityByMemberId.ContainsKey(memberId))
        {
            return 0;
        }
        Variant value = _calamityByMemberId[memberId];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static BattleUnitState ReadBattleUnit(GDictionary payload, string key)
    {
        if (payload == null || string.IsNullOrEmpty(key) || !payload.ContainsKey(key))
        {
            return null;
        }
        Variant value = payload[key];
        return value.VariantType == Variant.Type.Object ? value.As<BattleUnitState>() : null;
    }

    private static StringName ReadStringName(GDictionary payload, string key)
    {
        if (payload == null || string.IsNullOrEmpty(key) || !payload.ContainsKey(key))
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(payload[key]);
    }

    private static GArray ReadArray(GDictionary payload, string key)
    {
        return TryReadArray(payload, key, out GArray result) ? result : new GArray();
    }

    private static bool TryReadArray(GDictionary payload, string key, out GArray result)
    {
        result = new GArray();
        if (payload == null || string.IsNullOrEmpty(key) || !payload.ContainsKey(key))
        {
            return false;
        }
        Variant value = payload[key];
        if (value.VariantType != Variant.Type.Array)
        {
            return false;
        }
        result = value.AsGodotArray();
        return true;
    }

}
