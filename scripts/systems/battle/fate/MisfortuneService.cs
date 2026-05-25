using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MisfortuneService : RefCounted
{
    private static readonly StringName CalamityReasonOrdinaryMiss = "ordinary_miss";
    private static readonly StringName CalamityReasonCriticalFail = "critical_fail";
    private static readonly StringName CalamityReasonStrongDebuff = "strong_debuff";
    private static readonly StringName CalamityReasonAdjacentAllyDefeated = "adjacent_ally_defeated";
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
    private Callable _unitByMemberIdCallback = new();
    private GDictionary _calamityByMemberId = new();
    private GDictionary _reasonFlagsByMemberId = new();
    private GDictionary _processedAdjacentDefeatUnitIds = new();
    private GDictionary _misstepToSchemeUsedByMemberId = new();
    private GDictionary _blackStarBrandFreeUsedByMemberId = new();
    private GDictionary _blackCrownSealUsedByMemberId = new();
    private GDictionary _doomSentenceUsedByMemberId = new();

    public static bool IsMisfortuneGatedSkill(StringName skillId)
    {
        return MisfortuneSkillGateRules.ContainsKey(ProgressionDataUtils.to_string_name(skillId));
    }

    public static string GetSkillSidecarMissingMessage(StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        return _DictionaryGet(rule, "sidecar_missing_message", "Misfortune battle sidecar 未初始化。").AsString();
    }

    public static string GetSkillDefaultBlockMessage(StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        return _DictionaryGet(rule, "default_block_message", "calamity 不足，无法施放该技能。").AsString();
    }

    private static GDictionary _GetSkillGateRule(StringName skillId)
    {
        var normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        var ruleVariant = _DictionaryGet(MisfortuneSkillGateRules, normalizedSkillId, default);
        return ruleVariant.VariantType == Variant.Type.Dictionary ? ruleVariant.AsGodotDictionary() : new GDictionary();
    }

    public void Setup(BattleFateEventBus fateEventBus, Callable unitByMemberIdCallback)
    {
        _unitByMemberIdCallback = !unitByMemberIdCallback.Equals(default(Callable)) ? unitByMemberIdCallback : new Callable();
        BindFateEventBus(fateEventBus);
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

    public void BindFateEventBus(BattleFateEventBus fateEventBus)
    {
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched -= _OnFateEvent;
        _fateEventBus = fateEventBus;
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched += _OnFateEvent;
    }

    public new void Dispose()
    {
        BindFateEventBus(null);
        _unitByMemberIdCallback = new Callable();
        _calamityByMemberId = new GDictionary();
        _reasonFlagsByMemberId.Clear();
        _processedAdjacentDefeatUnitIds.Clear();
        _misstepToSchemeUsedByMemberId.Clear();
        _blackStarBrandFreeUsedByMemberId.Clear();
        _blackCrownSealUsedByMemberId.Clear();
        _doomSentenceUsedByMemberId.Clear();
        base.Dispose();
    }

    public GDictionary GetCalamityByMemberId()
    {
        return (GDictionary)ProgressionDataUtils.to_string_name_int_map(_calamityByMemberId).Duplicate(true);
    }

    public int GetMemberCalamity(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return 0;
        return Mathf.Max(_DictionaryGet(_calamityByMemberId, normalizedMemberId, 0).AsInt32(), 0);
    }

    public int GetMemberCalamityCap(StringName memberId)
    {
        return _CalculateCalamityCap(_ResolveUnitByMemberId(memberId));
    }

    public int GetBlackStarBrandCalamityCost(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return BlackStarBrandRepeatCalamityCost;
        if (!_DictionaryGet(_blackStarBrandFreeUsedByMemberId, normalizedMemberId, false).AsBool())
            return 0;
        return BlackStarBrandRepeatCalamityCost;
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

    public string GetSkillCastBlockReason(BattleUnitState unitState, StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        if (rule.Count == 0)
            return "";
        var gateType = ProgressionDataUtils.to_string_name(_DictionaryGet(rule, "gate_type", ""));
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

    public GDictionary ConsumeSkillCast(BattleUnitState unitState, StringName skillId)
    {
        var rule = _GetSkillGateRule(skillId);
        if (rule.Count == 0)
            return new GDictionary
            {
                ["ok"] = true,
                ["gated"] = false,
                ["member_id"] = unitState != null ? ProgressionDataUtils.to_string_name(unitState.source_member_id).ToString() : "",
            };
        var gateType = ProgressionDataUtils.to_string_name(_DictionaryGet(rule, "gate_type", ""));
        switch ((string)gateType)
        {
            case "black_star_brand":
                return ConsumeBlackStarBrandCast(unitState);
            case "crown_break":
                return ConsumeCrownBreakCast(unitState);
            case "doom_sentence":
                return ConsumeDoomSentenceCast(unitState);
            case "black_crown_seal":
                return ConsumeBlackCrownSealCast(unitState);
            default:
                return new GDictionary
                {
                    ["ok"] = true,
                    ["gated"] = false,
                    ["member_id"] = unitState != null ? ProgressionDataUtils.to_string_name(unitState.source_member_id).ToString() : "",
                };
        }
    }

    public GDictionary ConsumeBlackStarBrandCast(BattleUnitState unitState)
    {
        if (unitState == null)
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = "技能施放者无效。",
                ["calamity_cost"] = BlackStarBrandRepeatCalamityCost,
            };
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = "黑星烙印只能由正式成员施放。",
                ["calamity_cost"] = BlackStarBrandRepeatCalamityCost,
            };
        int calamityCost = GetBlackStarBrandCalamityCost(memberId);
        int currentCalamity = GetMemberCalamity(memberId);
        if (calamityCost > 0 && currentCalamity < calamityCost)
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = "calamity 不足，无法施放黑星烙印。",
                ["calamity_cost"] = calamityCost,
                ["remaining_calamity"] = currentCalamity,
            };
        if (calamityCost > 0)
            _calamityByMemberId[memberId] = Mathf.Max(currentCalamity - calamityCost, 0);
        _blackStarBrandFreeUsedByMemberId[memberId] = true;
        return new GDictionary
        {
            ["ok"] = true,
            ["member_id"] = memberId.ToString(),
            ["calamity_cost"] = calamityCost,
            ["free_cast"] = calamityCost <= 0,
            ["remaining_calamity"] = GetMemberCalamity(memberId),
        };
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

    public GDictionary ConsumeCrownBreakCast(BattleUnitState unitState)
    {
        if (unitState == null)
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = "技能施放者无效。",
                ["calamity_cost"] = CrownBreakCalamityCost,
            };
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = "折冠只能由正式成员施放。",
                ["calamity_cost"] = CrownBreakCalamityCost,
            };
        int currentCalamity = GetMemberCalamity(memberId);
        if (currentCalamity < CrownBreakCalamityCost)
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = "calamity 不足，无法施放折冠。",
                ["calamity_cost"] = CrownBreakCalamityCost,
                ["remaining_calamity"] = currentCalamity,
            };
        _calamityByMemberId[memberId] = Mathf.Max(currentCalamity - CrownBreakCalamityCost, 0);
        return new GDictionary
        {
            ["ok"] = true,
            ["member_id"] = memberId.ToString(),
            ["calamity_cost"] = CrownBreakCalamityCost,
            ["remaining_calamity"] = GetMemberCalamity(memberId),
        };
    }

    public string GetDoomSentenceCastBlockReason(BattleUnitState unitState)
    {
        if (unitState == null)
            return "技能施放者无效。";
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return "厄命宣判只能由正式成员施放。";
        if (_DictionaryGet(_doomSentenceUsedByMemberId, memberId, false).AsBool())
            return "厄命宣判每战只能施放 1 次。";
        if (GetMemberCalamityCap(memberId) < DoomSentenceCalamityCost)
            return "本战 calamity 上限不足 5，无法施放厄命宣判。";
        if (GetMemberCalamity(memberId) < DoomSentenceCalamityCost)
            return "calamity 不足，无法施放厄命宣判。";
        return "";
    }

    public bool CanCastDoomSentence(BattleUnitState unitState)
    {
        return string.IsNullOrEmpty(GetDoomSentenceCastBlockReason(unitState));
    }

    public GDictionary ConsumeDoomSentenceCast(BattleUnitState unitState)
    {
        var blockReason = GetDoomSentenceCastBlockReason(unitState);
        var memberId = unitState != null ? ProgressionDataUtils.to_string_name(unitState.source_member_id) : new StringName("");
        if (!string.IsNullOrEmpty(blockReason))
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = blockReason,
                ["calamity_cost"] = DoomSentenceCalamityCost,
                ["remaining_calamity"] = GetMemberCalamity(memberId),
            };
        int currentCalamity = GetMemberCalamity(memberId);
        _calamityByMemberId[memberId] = Mathf.Max(currentCalamity - DoomSentenceCalamityCost, 0);
        _doomSentenceUsedByMemberId[memberId] = true;
        return new GDictionary
        {
            ["ok"] = true,
            ["member_id"] = memberId.ToString(),
            ["calamity_cost"] = DoomSentenceCalamityCost,
            ["remaining_calamity"] = GetMemberCalamity(memberId),
        };
    }

    public string GetBlackCrownSealCastBlockReason(BattleUnitState unitState)
    {
        if (unitState == null)
            return "技能施放者无效。";
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return "黑冠封印只能由正式成员施放。";
        if (_DictionaryGet(_blackCrownSealUsedByMemberId, memberId, false).AsBool())
            return "黑冠封印每战只能施放 1 次。";
        return "";
    }

    public bool CanCastBlackCrownSeal(BattleUnitState unitState)
    {
        return string.IsNullOrEmpty(GetBlackCrownSealCastBlockReason(unitState));
    }

    public GDictionary ConsumeBlackCrownSealCast(BattleUnitState unitState)
    {
        var blockReason = GetBlackCrownSealCastBlockReason(unitState);
        var memberId = unitState != null ? ProgressionDataUtils.to_string_name(unitState.source_member_id) : new StringName("");
        if (!string.IsNullOrEmpty(blockReason))
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = blockReason,
                ["member_id"] = memberId.ToString(),
            };
        _blackCrownSealUsedByMemberId[memberId] = true;
        return new GDictionary
        {
            ["ok"] = true,
            ["member_id"] = memberId.ToString(),
        };
    }

    public bool HasTriggeredReason(StringName memberId, StringName reasonId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        if (normalizedMemberId == "" || normalizedReasonId == "")
            return false;
        var memberReasonFlags = _EnsureMemberReasonFlags(normalizedMemberId);
        return _DictionaryGet(memberReasonFlags, normalizedReasonId, false).AsBool();
    }

    public Variant HandleTrigger(StringName reasonId, GDictionary payload)
    {
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        switch ((string)normalizedReasonId)
        {
            case "strong_debuff":
                return _HandleStrongDebuffTrigger(payload);
            case "adjacent_ally_defeated":
                return _HandleAdjacentAllyDefeatTrigger(payload);
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

    public GDictionary HandleAppliedStatuses(BattleUnitState targetUnit, Variant statusEffectIds)
    {
        var result = HandleTrigger(CalamityReasonStrongDebuff, new GDictionary
        {
            ["target_unit"] = targetUnit,
            ["status_effect_ids"] = statusEffectIds,
        });
        return result.VariantType == Variant.Type.Dictionary ? result.AsGodotDictionary() : new GDictionary();
    }

    public GArray HandleAdjacentAllyDefeat(BattleUnitState defeatedUnit, GArray adjacentUnits)
    {
        var result = HandleTrigger(CalamityReasonAdjacentAllyDefeated, new GDictionary
        {
            ["defeated_unit"] = defeatedUnit,
            ["adjacent_units"] = adjacentUnits,
        });
        var typedResults = new GArray();
        if (result.VariantType == Variant.Type.Array)
        {
            foreach (var entry in result.AsGodotArray())
            {
                if (entry.VariantType == Variant.Type.Dictionary)
                    typedResults.Add(entry.AsGodotDictionary());
            }
        }
        return typedResults;
    }

    public GDictionary HandleLowHpTurnEnd(BattleUnitState unitState)
    {
        var result = HandleTrigger(CalamityReasonLowHpEndTurn, new GDictionary
        {
            ["unit_state"] = unitState,
        });
        return result.VariantType == Variant.Type.Dictionary ? result.AsGodotDictionary() : new GDictionary();
    }

    public GDictionary HandleBossPhaseChanged(BattleUnitState unitState, StringName phaseId)
    {
        var result = HandleTrigger(CalamityReasonBossPhaseChanged, new GDictionary
        {
            ["unit_state"] = unitState,
            ["phase_id"] = phaseId,
        });
        return result.VariantType == Variant.Type.Dictionary ? result.AsGodotDictionary() : new GDictionary();
    }

    private GDictionary _HandleStrongDebuffTrigger(GDictionary payload)
    {
        var targetUnit = _DictionaryGet(payload, "target_unit", default).As<BattleUnitState>();
        if (targetUnit == null)
            return new GDictionary();
        var strongStatusIds = _ExtractStrongAttackDebuffIds(_DictionaryGet(payload, "status_effect_ids", new GArray()));
        if (strongStatusIds.Count == 0)
            return new GDictionary();
        return _RegisterReason(targetUnit, CalamityReasonStrongDebuff, new GDictionary
        {
            ["status_ids"] = ProgressionDataUtils.string_name_array_to_string_array(strongStatusIds),
        });
    }

    private GArray _HandleAdjacentAllyDefeatTrigger(GDictionary payload)
    {
        var defeatedUnit = payload.GetValueOrDefault("defeated_unit", default).As<BattleUnitState>();
        var adjacentUnits = payload.GetValueOrDefault("adjacent_units", new GArray()).AsGodotArray();
        var results = new GArray();
        if (defeatedUnit == null || defeatedUnit.unit_id == "")
            return results;
        if (_processedAdjacentDefeatUnitIds.ContainsKey(defeatedUnit.unit_id))
            return results;
        _processedAdjacentDefeatUnitIds[defeatedUnit.unit_id] = true;
        foreach (var unitVariant in adjacentUnits)
        {
            var observerUnit = unitVariant.AsGodotObject() as BattleUnitState;
            if (observerUnit == null)
                continue;
            var result = _RegisterReason(observerUnit, CalamityReasonAdjacentAllyDefeated, new GDictionary
            {
                ["defeated_unit_id"] = defeatedUnit.unit_id.ToString(),
            });
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
        var phaseId = ProgressionDataUtils.to_string_name(payload.GetValueOrDefault("phase_id", ""));
        return _RegisterReason(unitState, CalamityReasonBossPhaseChanged, new GDictionary
        {
            ["phase_id"] = ProgressionDataUtils.to_string_name(phaseId).ToString(),
        });
    }

    private GDictionary _HandleMemberReasonTrigger(GDictionary payload, StringName reasonId)
    {
        var unitState = payload.GetValueOrDefault("unit_state", default).As<BattleUnitState>();
        if (unitState != null)
            return _RegisterReason(unitState, reasonId);
        var memberId = ProgressionDataUtils.to_string_name(payload.GetValueOrDefault("member_id", ""));
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
        var memberId = ProgressionDataUtils.to_string_name(_DictionaryGet(payload, "attacker_member_id", ""));
        if (memberId == "")
            return;
        HandleTrigger(reasonId, new GDictionary { ["member_id"] = memberId });
    }

    private GDictionary _RegisterReason(BattleUnitState unitState, StringName reasonId, GDictionary metadata = null)
    {
        var normalizedReasonId = ProgressionDataUtils.to_string_name(reasonId);
        if (unitState == null || normalizedReasonId == "")
            return new GDictionary();
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return new GDictionary();
        var memberReasonFlags = _EnsureMemberReasonFlags(memberId);
        bool wasFirstReason = memberReasonFlags.Count == 0;
        if (memberReasonFlags.ContainsKey(normalizedReasonId) && memberReasonFlags[normalizedReasonId].AsBool())
            return new GDictionary
            {
                ["member_id"] = memberId.ToString(),
                ["reason_id"] = normalizedReasonId.ToString(),
                ["granted"] = false,
                ["already_triggered"] = true,
                ["calamity"] = GetMemberCalamity(memberId),
                ["cap"] = _CalculateCalamityCap(unitState),
            };
        memberReasonFlags[normalizedReasonId] = true;
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
            ["metadata"] = metadata != null ? (GDictionary)metadata.Duplicate(true) : new GDictionary(),
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
            Mathf.Max(unitState.attribute_snapshot.Call("get_value", CalamityCapacityBonusStatId).AsInt32(), 0),
            MaxCalamityCapacityBonus
        );
        int hiddenLuckAtBirth = unitState.attribute_snapshot.Call("get_value", "hidden_luck_at_birth").AsInt32();
        return BaseCalamityCap + calamityCapacityBonus + (hiddenLuckAtBirth <= -5 ? 1 : 0);
    }

    private Godot.Collections.Array<StringName> _ExtractStrongAttackDebuffIds(Variant statusEffectIds)
    {
        var strongStatusIds = new Godot.Collections.Array<StringName>();
        if (statusEffectIds.VariantType != Variant.Type.Array)
            return strongStatusIds;
        foreach (var statusIdVariant in statusEffectIds.AsGodotArray())
        {
            var statusId = ProgressionDataUtils.to_string_name(statusIdVariant);
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
        int maxHp = Mathf.Max(unitState.attribute_snapshot.Call("get_value", "hp_max").AsInt32(), 0);
        if (maxHp <= 0)
            return false;
        return unitState.current_hp * 100 <= maxHp * BattleState.LOW_HP_ATTACK_DISADVANTAGE_PERCENT();
    }

    private GDictionary _EnsureMemberReasonFlags(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return new GDictionary();
        if (!_reasonFlagsByMemberId.ContainsKey(normalizedMemberId))
            _reasonFlagsByMemberId[normalizedMemberId] = new GDictionary();
        return _reasonFlagsByMemberId[normalizedMemberId].AsGodotDictionary();
    }

    private BattleUnitState _ResolveUnitByMemberId(StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "" || _unitByMemberIdCallback.Equals(default(Callable)))
            return null;
        return _unitByMemberIdCallback.Call(normalizedMemberId).As<BattleUnitState>();
    }

    private int _GetBonusCalamityForReason(BattleUnitState unitState, StringName reasonId)
    {
        if (unitState == null || reasonId != CalamityReasonCriticalFail)
            return 0;
        var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
        if (memberId == "")
            return 0;
        if (_DictionaryGet(_misstepToSchemeUsedByMemberId, memberId, false).AsBool())
            return 0;
        if (!_UnitHasSkill(unitState, MisstepToSchemeSkillId))
            return 0;
        _misstepToSchemeUsedByMemberId[memberId] = true;
        return 1;
    }

    private bool _UnitHasSkill(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return false;
        if (unitState.known_active_skill_ids.Contains(skillId))
            return true;
        return _DictionaryGet(unitState.known_skill_level_map, skillId, 0).AsInt32() > 0;
    }

    private static Variant _DictionaryGet(GDictionary dict, Variant key, Variant fallback)
    {
        if (dict == null)
            return fallback;
        if (dict.ContainsKey(key))
            return dict[key];
        return fallback;
    }
}

