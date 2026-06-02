using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MisfortuneService : RefCounted
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
    public static readonly StringName BLACK_STAR_BRAND_SKILL_ID = "black_star_brand";
    public static readonly StringName CROWN_BREAK_SKILL_ID = "crown_break";
    public static readonly StringName DOOM_SENTENCE_SKILL_ID = "doom_sentence";
    public static readonly StringName BLACK_CROWN_SEAL_SKILL_ID = "black_crown_seal";

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

    private static readonly GDictionary MisfortuneSkillGateRules = new()
    {
        ["black_star_brand"] = new GDictionary
        {
            ["gate_type"] = GateTypeBlackStarBrand,
            ["sidecar_missing_message"] = "黑星烙印的 calamity sidecar 未初始化。",
            ["default_block_message"] = "calamity 不足，无法施放黑星烙印。",
        },
        ["crown_break"] = new GDictionary
        {
            ["gate_type"] = GateTypeCrownBreak,
            ["sidecar_missing_message"] = "折冠的 calamity sidecar 未初始化。",
            ["default_block_message"] = "calamity 不足，无法施放折冠。",
        },
        ["doom_sentence"] = new GDictionary
        {
            ["gate_type"] = GateTypeDoomSentence,
            ["sidecar_missing_message"] = "厄命宣判的 calamity sidecar 未初始化。",
            ["default_block_message"] = "calamity 不足，无法施放厄命宣判。",
        },
        ["black_crown_seal"] = new GDictionary
        {
            ["gate_type"] = GateTypeBlackCrownSeal,
            ["sidecar_missing_message"] = "黑冠封印的 battle sidecar 未初始化。",
            ["default_block_message"] = "黑冠封印每战只能施放 1 次。",
        },
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

    public static StringName BLACK_STAR_BRAND_SKILL_ID_VALUE() => BLACK_STAR_BRAND_SKILL_ID;

    public static StringName CROWN_BREAK_SKILL_ID_VALUE() => CROWN_BREAK_SKILL_ID;

    public static StringName DOOM_SENTENCE_SKILL_ID_VALUE() => DOOM_SENTENCE_SKILL_ID;

    public static StringName BLACK_CROWN_SEAL_SKILL_ID_VALUE() => BLACK_CROWN_SEAL_SKILL_ID;

    public static int BASE_CALAMITY_CAP() => BaseCalamityCap;

    public static int BLACK_STAR_BRAND_REPEAT_CALAMITY_COST_VALUE() =>
        BlackStarBrandRepeatCalamityCost;

    public static StringName CALAMITY_REASON_ORDINARY_MISS() => CalamityReasonOrdinaryMiss;

    public static StringName CALAMITY_REASON_CRITICAL_FAIL() => CalamityReasonCriticalFail;

    public static StringName CALAMITY_REASON_STRONG_DEBUFF() => CalamityReasonStrongDebuff;

    public static StringName CALAMITY_REASON_ADJACENT_ALLY_DEFEATED() =>
        CalamityReasonAdjacentAllyDefeated;

    public static StringName CALAMITY_REASON_LOW_HP_END_TURN() => CalamityReasonLowHpEndTurn;

    public static StringName CALAMITY_REASON_BOSS_PHASE_CHANGED() => CalamityReasonBossPhaseChanged;

    public static bool IsMisfortuneGatedSkill(StringName skillId)
    {
        return MisfortuneSkillGateRules.ContainsKey(ProgressionDataUtils.to_string_name(skillId));
    }

    public static bool is_misfortune_gated_skill(StringName skill_id)
    {
        return IsMisfortuneGatedSkill(skill_id);
    }

    public static string GetSkillSidecarMissingMessage(StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        return rule
            .GetValueOrDefault("sidecar_missing_message", "Misfortune battle sidecar 未初始化。")
            .AsString();
    }

    public static string get_skill_sidecar_missing_message(StringName skill_id)
    {
        return GetSkillSidecarMissingMessage(skill_id);
    }

    public static string GetSkillDefaultBlockMessage(StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        return rule
            .GetValueOrDefault("default_block_message", "calamity 不足，无法施放该技能。")
            .AsString();
    }

    public static string get_skill_default_block_message(StringName skill_id)
    {
        return GetSkillDefaultBlockMessage(skill_id);
    }

    private static GDictionary _GetSkillGateRule(StringName skillId)
    {
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        var ruleValue = MisfortuneSkillGateRules.GetValueOrDefault(normalizedSkillId, default);
        return ruleValue.VariantType == Variant.Type.Dictionary
            ? ruleValue.AsGodotDictionary()
            : new GDictionary();
    }

    public void Setup(
        BattleFateEventBus fateEventBus,
        Func<StringName, BattleUnitState> unitByMemberIdResolver
    )
    {
        _unitByMemberIdResolver = unitByMemberIdResolver;
        BindFateEventBus(fateEventBus);
    }

    public void setup(
        BattleFateEventBus fate_event_bus = null,
        Func<StringName, BattleUnitState> unit_by_member_id_resolver = null
    )
    {
        Setup(fate_event_bus, unit_by_member_id_resolver);
    }

    public void BeginBattle(GDictionary calamityStore)
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

    public void begin_battle(GDictionary calamity_store = null)
    {
        BeginBattle(calamity_store ?? new GDictionary());
    }

    public void BindFateEventBus(BattleFateEventBus fateEventBus)
    {
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched -= _OnFateEvent;
        _fateEventBus = fateEventBus;
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched += _OnFateEvent;
    }

    public void bind_fate_event_bus(BattleFateEventBus fate_event_bus = null)
    {
        BindFateEventBus(fate_event_bus);
    }

    public new void Dispose()
    {
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

    public void dispose()
    {
        BindFateEventBus(null);
        _unitByMemberIdResolver = null;
        _calamityByMemberId = new GDictionary();
        _reasonFlagsByMemberId.Clear();
        _processedAdjacentDefeatUnitIds.Clear();
        _misstepToSchemeUsedByMemberId.Clear();
        _blackStarBrandFreeUsedByMemberId.Clear();
        _blackCrownSealUsedByMemberId.Clear();
        _doomSentenceUsedByMemberId.Clear();
    }

    public GDictionary GetCalamityByMemberId()
    {
        return (GDictionary)
            ProgressionDataUtils.to_string_name_int_map(_calamityByMemberId).Duplicate(true);
    }

    public Dictionary<StringName, int> GetCalamityByMemberIdSnapshot()
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

    public GDictionary get_calamity_by_member_id()
    {
        return GetCalamityByMemberId();
    }

    public int GetMemberCalamity(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return 0;
        return Mathf.Max(
            _calamityByMemberId.GetValueOrDefault(normalizedMemberId, 0).AsInt32(),
            0
        );
    }

    public int get_member_calamity(StringName member_id)
    {
        return GetMemberCalamity(member_id);
    }

    public int GetMemberCalamityCap(StringName memberId)
    {
        return _CalculateCalamityCap(_ResolveUnitByMemberId(memberId));
    }

    public int get_member_calamity_cap(StringName member_id)
    {
        return GetMemberCalamityCap(member_id);
    }

    public int GetBlackStarBrandCalamityCost(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return BlackStarBrandRepeatCalamityCost;
        if (!_blackStarBrandFreeUsedByMemberId.Contains(normalizedMemberId))
            return 0;
        return BlackStarBrandRepeatCalamityCost;
    }

    public int get_black_star_brand_calamity_cost(StringName member_id)
    {
        return GetBlackStarBrandCalamityCost(member_id);
    }

    public bool CanCastBlackStarBrand(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return false;
        int calamityCost = GetBlackStarBrandCalamityCost(memberId);
        return calamityCost <= 0 || GetMemberCalamity(memberId) >= calamityCost;
    }

    public bool can_cast_black_star_brand(BattleUnitState unit_state)
    {
        return CanCastBlackStarBrand(unit_state);
    }

    public string GetSkillCastBlockReason(BattleUnitState unitState, StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        if (rule.Count == 0)
            return "";
        var gateType = ProgressionDataUtils.to_string_name(
            rule.GetValueOrDefault("gate_type", "")
        );
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

    public string get_skill_cast_block_reason(BattleUnitState unit_state, StringName skill_id)
    {
        return GetSkillCastBlockReason(unit_state, skill_id);
    }

    public MisfortuneSkillCastResult ConsumeSkillCastResult(
        BattleUnitState unitState,
        StringName skillId
    )
    {
        var rule = _GetSkillGateRule(skillId);
        if (rule.Count == 0)
            return MisfortuneSkillCastResult.Success(
                unitState != null
                    ? ProgressionDataUtils.to_string_name(unitState.source_member_id)
                    : default,
                gated: false
            );
        var gateType = ProgressionDataUtils.to_string_name(
            rule.GetValueOrDefault("gate_type", "")
        );
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

    public GDictionary ConsumeSkillCast(BattleUnitState unitState, StringName skillId) =>
        ConsumeSkillCastResult(unitState, skillId).ToDictionary();

    public GDictionary consume_skill_cast(BattleUnitState unit_state, StringName skill_id)
    {
        return ConsumeSkillCast(unit_state, skill_id);
    }

    public MisfortuneSkillCastResult ConsumeBlackStarBrandCastResult(BattleUnitState unitState)
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

    public GDictionary ConsumeBlackStarBrandCast(BattleUnitState unitState) =>
        ConsumeBlackStarBrandCastResult(unitState).ToDictionary();

    public GDictionary consume_black_star_brand_cast(BattleUnitState unit_state)
    {
        return ConsumeBlackStarBrandCast(unit_state);
    }

    public bool CanCastCrownBreak(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return false;
        return GetMemberCalamity(memberId) >= CrownBreakCalamityCost;
    }

    public bool can_cast_crown_break(BattleUnitState unit_state)
    {
        return CanCastCrownBreak(unit_state);
    }

    public MisfortuneSkillCastResult ConsumeCrownBreakCastResult(BattleUnitState unitState)
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

    public GDictionary ConsumeCrownBreakCast(BattleUnitState unitState) =>
        ConsumeCrownBreakCastResult(unitState).ToDictionary();

    public GDictionary consume_crown_break_cast(BattleUnitState unit_state)
    {
        return ConsumeCrownBreakCast(unit_state);
    }

    public string GetDoomSentenceCastBlockReason(BattleUnitState unitState)
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

    public string get_doom_sentence_cast_block_reason(BattleUnitState unit_state)
    {
        return GetDoomSentenceCastBlockReason(unit_state);
    }

    public bool CanCastDoomSentence(BattleUnitState unitState)
    {
        return string.IsNullOrEmpty(GetDoomSentenceCastBlockReason(unitState));
    }

    public bool can_cast_doom_sentence(BattleUnitState unit_state)
    {
        return CanCastDoomSentence(unit_state);
    }

    public MisfortuneSkillCastResult ConsumeDoomSentenceCastResult(BattleUnitState unitState)
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

    public GDictionary ConsumeDoomSentenceCast(BattleUnitState unitState) =>
        ConsumeDoomSentenceCastResult(unitState).ToDictionary();

    public GDictionary consume_doom_sentence_cast(BattleUnitState unit_state)
    {
        return ConsumeDoomSentenceCast(unit_state);
    }

    public string GetBlackCrownSealCastBlockReason(BattleUnitState unitState)
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

    public string get_black_crown_seal_cast_block_reason(BattleUnitState unit_state)
    {
        return GetBlackCrownSealCastBlockReason(unit_state);
    }

    public bool CanCastBlackCrownSeal(BattleUnitState unitState)
    {
        return string.IsNullOrEmpty(GetBlackCrownSealCastBlockReason(unitState));
    }

    public bool can_cast_black_crown_seal(BattleUnitState unit_state)
    {
        return CanCastBlackCrownSeal(unit_state);
    }

    public MisfortuneSkillCastResult ConsumeBlackCrownSealCastResult(BattleUnitState unitState)
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

    public GDictionary ConsumeBlackCrownSealCast(BattleUnitState unitState) =>
        ConsumeBlackCrownSealCastResult(unitState).ToDictionary();

    public GDictionary consume_black_crown_seal_cast(BattleUnitState unit_state)
    {
        return ConsumeBlackCrownSealCast(unit_state);
    }

    public bool HasTriggeredReason(StringName memberId, StringName reasonId)
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

    public bool has_triggered_reason(StringName member_id, StringName reason_id)
    {
        return HasTriggeredReason(member_id, reason_id);
    }

    public GDictionary HandleTrigger(StringName reasonId, GDictionary payload)
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

    public GDictionary handle_trigger(StringName reason_id, GDictionary payload = null)
    {
        return HandleTrigger(reason_id, payload ?? new GDictionary());
    }

    public GDictionary HandleAppliedStatuses(BattleUnitState targetUnit, GArray statusEffectIds)
    {
        return _HandleStrongDebuffTrigger(
            new GDictionary
            {
                ["target_unit"] = targetUnit,
                ["status_effect_ids"] = statusEffectIds,
            }
        );
    }

    public GDictionary handle_applied_statuses(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        return HandleAppliedStatuses(target_unit, status_effect_ids ?? new GArray());
    }

    public GArray HandleAdjacentAllyDefeat(BattleUnitState defeatedUnit, GArray adjacentUnits)
    {
        return _HandleAdjacentAllyDefeatTrigger(
            new GDictionary
            {
                ["defeated_unit"] = defeatedUnit,
                ["adjacent_units"] = adjacentUnits ?? new GArray(),
            }
        );
    }

    public GArray handle_adjacent_ally_defeat(BattleUnitState defeated_unit, GArray adjacent_units)
    {
        return HandleAdjacentAllyDefeat(defeated_unit, adjacent_units ?? new GArray());
    }

    public GDictionary HandleLowHpTurnEnd(BattleUnitState unitState)
    {
        return _HandleLowHpTurnEndTrigger(new GDictionary { ["unit_state"] = unitState });
    }

    public GDictionary handle_low_hp_turn_end(BattleUnitState unit_state)
    {
        return HandleLowHpTurnEnd(unit_state);
    }

    public GDictionary HandleBossPhaseChanged(BattleUnitState unitState, StringName phaseId)
    {
        return _HandleBossPhaseChangedTrigger(
            new GDictionary { ["unit_state"] = unitState, ["phase_id"] = phaseId }
        );
    }

    public GDictionary handle_boss_phase_changed(
        BattleUnitState unit_state,
        StringName phase_id = default
    )
    {
        return HandleBossPhaseChanged(unit_state, phase_id);
    }

    private GDictionary _HandleStrongDebuffTrigger(GDictionary payload)
    {
        var targetUnit = payload.GetValueOrDefault("target_unit", default).As<BattleUnitState>();
        if (targetUnit == null)
            return new GDictionary();
        var strongStatusIds = _ExtractStrongAttackDebuffIds(
            payload != null
            && payload.ContainsKey("status_effect_ids")
            && payload["status_effect_ids"].VariantType == Variant.Type.Array
                ? payload["status_effect_ids"].AsGodotArray()
                : new GArray()
        );
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
        var defeatedUnit = payload
            .GetValueOrDefault("defeated_unit", default)
            .As<BattleUnitState>();
        var adjacentUnits = payload
            .GetValueOrDefault("adjacent_units", new GArray())
            .AsGodotArray();
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
        var unitState = payload.GetValueOrDefault("unit_state", default).As<BattleUnitState>();
        if (unitState == null || !unitState.is_alive || !_IsLowHpHardship(unitState))
            return new GDictionary();
        return _RegisterReason(unitState, CalamityReasonLowHpEndTurn);
    }

    private GDictionary _HandleBossPhaseChangedTrigger(GDictionary payload)
    {
        var unitState = payload.GetValueOrDefault("unit_state", default).As<BattleUnitState>();
        var phaseId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("phase_id", "")
        );
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
        var unitState = payload.GetValueOrDefault("unit_state", default).As<BattleUnitState>();
        if (unitState != null)
            return _RegisterReason(unitState, reasonId);
        var memberId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("member_id", "")
        );
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
        var memberId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("attacker_member_id", "")
        );
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
        var statusEntry = new BattleStatusEffectState();
        statusEntry.status_id = ReverseFortuneStatusId;
        statusEntry.source_unit_id = unitState.unit_id;
        statusEntry.power = 1;
        statusEntry.stacks = 1;
        statusEntry.duration = ReverseFortuneDurationTu;
        unitState.set_status_effect(statusEntry);
        return true;
    }

    private int _CalculateCalamityCap(BattleUnitState unitState)
    {
        if (unitState == null || unitState.attribute_snapshot == null)
            return BaseCalamityCap;
        int calamityCapacityBonus = Mathf.Min(
            Mathf.Max(
                unitState
                    .attribute_snapshot.get_value(CalamityCapacityBonusStatId),
                0
            ),
            MaxCalamityCapacityBonus
        );
        int hiddenLuckAtBirth = unitState
            .attribute_snapshot.get_value("hidden_luck_at_birth");
        return BaseCalamityCap + calamityCapacityBonus + (hiddenLuckAtBirth <= -5 ? 1 : 0);
    }

    private Godot.Collections.Array<StringName> _ExtractStrongAttackDebuffIds(GArray statusEffectIds)
    {
        var strongStatusIds = new Godot.Collections.Array<StringName>();
        if (statusEffectIds == null)
            return strongStatusIds;
        foreach (var statusIdValue in statusEffectIds)
        {
            var statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "")
                continue;
            if (!BattleState.STRONG_ATTACK_DISADVANTAGE_STATUS_IDS().ContainsKey(statusId))
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
            unitState.attribute_snapshot.get_value("hp_max"),
            0
        );
        if (maxHp <= 0)
            return false;
        return unitState.current_hp * 100
            <= maxHp * BattleState.LOW_HP_ATTACK_DISADVANTAGE_PERCENT();
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
        return unitState.known_skill_level_map.GetValueOrDefault(skillId, 0).AsInt32() > 0;
    }

}
