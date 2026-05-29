using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class FateRuntimeModule : RefCounted
{
    private const int BaseCalamityCap = 3;
    private const int BlackStarBrandRepeatCalamityCost = 1;

    private static readonly StringName CalamityReasonBossPhaseChanged = "boss_phase_changed";
    private static readonly StringName CalamityReasonStrongDebuff = "strong_debuff";
    private static readonly StringName BlackStarBrandSkillId = "black_star_brand";
    private static readonly StringName CrownBreakSkillId = "crown_break";
    private static readonly StringName DoomSentenceSkillId = "doom_sentence";
    private static readonly StringName BlackCrownSealSkillId = "black_crown_seal";

    private GodotObject _characterGateway;
    private GodotObject _battleRuntimeGateway;
    private Func<StringName, BattleUnitState> _unitByMemberIdResolver;
    private FortuneService _fortuneService = new();
    private FortunaGuidanceService _fortunaGuidanceService = new();
    private LowLuckEventService _lowLuckEventService = new();
    private MisfortuneGuidanceService _misfortuneGuidanceService = new();
    private MisfortuneService _misfortuneService = new();

    public void setup(
        GodotObject character_gateway = null,
        BattleFateEventBus fate_event_bus = null,
        GodotObject battle_runtime_gateway = null,
        Func<StringName, BattleUnitState> unit_by_member_id_resolver = null
    )
    {
        _characterGateway = character_gateway;
        _battleRuntimeGateway = battle_runtime_gateway;
        _unitByMemberIdResolver = unit_by_member_id_resolver;

        // Guidance must see the pre-mark state before FortuneService mutates fortune_marked on the same bus event.
        _fortunaGuidanceService?.setup(_characterGateway, fate_event_bus);
        _fortuneService?.setup(_characterGateway, fate_event_bus);
        _misfortuneService?.Setup(fate_event_bus, _unitByMemberIdResolver);
        _lowLuckEventService?.Setup(_characterGateway, fate_event_bus);
        _misfortuneGuidanceService?.Setup(_characterGateway, _battleRuntimeGateway);
    }

    public void dispose()
    {
        _fortunaGuidanceService?.dispose();
        if (_fortuneService != null)
            _fortuneService.dispose();
        _misfortuneService?.dispose();
        _lowLuckEventService?.dispose();
        _misfortuneGuidanceService?.dispose();
        _characterGateway = null;
        _battleRuntimeGateway = null;
        _unitByMemberIdResolver = null;
    }

    public void begin_battle(GDictionary calamity_store = null)
    {
        _misfortuneService?.BeginBattle(calamity_store ?? new GDictionary());
    }

    public GDictionary get_calamity_by_member_id()
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return _misfortuneService.GetCalamityByMemberId();
    }

    public int get_member_calamity(StringName member_id)
    {
        if (_misfortuneService == null)
            return 0;
        return _misfortuneService.GetMemberCalamity(member_id);
    }

    public int get_member_calamity_cap(StringName member_id)
    {
        if (_misfortuneService == null)
            return BaseCalamityCap;
        return _misfortuneService.GetMemberCalamityCap(member_id);
    }

    public int get_black_star_brand_cast_cost(StringName member_id)
    {
        if (_misfortuneService == null)
            return BlackStarBrandRepeatCalamityCost;
        return _misfortuneService.GetBlackStarBrandCalamityCost(member_id);
    }

    public bool has_misfortune_reason(StringName member_id, StringName reason_id)
    {
        return _misfortuneService != null
            && _misfortuneService.HasTriggeredReason(member_id, reason_id);
    }

    public string get_misfortune_skill_cast_block_reason(
        GodotObject unit_state,
        StringName skill_id
    )
    {
        if (_misfortuneService == null)
            return GetSkillSidecarMissingMessage(skill_id);
        return _misfortuneService.GetSkillCastBlockReason(unit_state as BattleUnitState, skill_id);
    }

    public GDictionary consume_misfortune_skill_cast(GodotObject unit_state, StringName skill_id)
    {
        if (_misfortuneService == null)
        {
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = GetSkillSidecarMissingMessage(skill_id),
            };
        }
        return _misfortuneService.ConsumeSkillCast(unit_state as BattleUnitState, skill_id);
    }

    public GDictionary handle_misfortune_trigger(StringName reason_id, GDictionary payload = null)
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return _misfortuneService.HandleTrigger(reason_id, payload ?? new GDictionary());
    }

    public GDictionary handle_member_boss_phase_changed(
        StringName member_id,
        StringName phase_id = default
    )
    {
        GodotObject unitState = ResolveUnitByMemberId(member_id);
        if (unitState == null)
            return new GDictionary();
        GDictionary result = handle_misfortune_trigger(
            CalamityReasonBossPhaseChanged,
            new GDictionary
            {
                ["unit_state"] = unitState,
                ["phase_id"] = GdInterop.IsEmpty(phase_id) ? new StringName("") : phase_id,
            }
        );
        return result;
    }

    public GDictionary handle_applied_statuses(GodotObject target_unit, GArray status_effect_ids)
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return _misfortuneService.HandleAppliedStatuses(
            target_unit as BattleUnitState,
            status_effect_ids ?? new GArray()
        );
    }

    public GDictionary handle_battle_resolution(
        BattleState battle_state,
        BattleResolutionResult battle_resolution_result
    )
    {
        GDictionary lowLuckEventResult = new();
        if (_lowLuckEventService != null)
        {
            lowLuckEventResult = _lowLuckEventService.HandleBattleResolution(
                battle_state,
                battle_resolution_result
            );
            MergeLowLuckBattleResultIntoResolution(battle_resolution_result, lowLuckEventResult);
        }

        Godot.Collections.Array<StringName> fortunaGuidanceUnlocks = new();
        if (_fortunaGuidanceService != null)
            fortunaGuidanceUnlocks = _fortunaGuidanceService.handle_battle_resolution(
                battle_state,
                battle_resolution_result
            );

        Godot.Collections.Array<StringName> misfortuneGuidanceUnlocks = new();
        if (_misfortuneGuidanceService != null)
            misfortuneGuidanceUnlocks = _misfortuneGuidanceService.HandleBattleResolution(
                battle_state,
                battle_resolution_result
            );

        return new GDictionary
        {
            ["fortuna_guidance_unlocks"] = fortunaGuidanceUnlocks,
            ["misfortune_guidance_unlocks"] = misfortuneGuidanceUnlocks,
            ["low_luck_event_result"] = lowLuckEventResult,
        };
    }

    public Godot.Collections.Array<StringName> handle_fortuna_chapter_completed(GDictionary payload)
    {
        if (_fortunaGuidanceService == null)
            return new Godot.Collections.Array<StringName>();
        return _fortunaGuidanceService.handle_chapter_completed(payload ?? new GDictionary());
    }

    public Godot.Collections.Array<StringName> handle_misfortune_forge_result(
        StringName member_id,
        GDictionary result,
        GDictionary item_defs = null
    )
    {
        if (_misfortuneGuidanceService == null)
            return new Godot.Collections.Array<StringName>();
        return _misfortuneGuidanceService.HandleForgeResult(
            member_id,
            result ?? new GDictionary(),
            item_defs ?? new GDictionary()
        );
    }

    public GDictionary resolve_low_luck_settlement_event_rewards(GDictionary context)
    {
        if (_lowLuckEventService == null)
            return new GDictionary();
        return _lowLuckEventService.HandleSettlementAction(context ?? new GDictionary());
    }

    public void clear_misfortune_exalted_ready_flags(GArray member_ids = null)
    {
        if (_misfortuneGuidanceService != null)
            _misfortuneGuidanceService.ClearExaltedReadyFlags(member_ids ?? new GArray());
    }

    public void set_fortune_confirmation_rng_for_testing(GodotObject rng = null)
    {
        _fortuneService?.set_confirmation_rng_for_testing(rng);
    }

    private GodotObject ResolveUnitByMemberId(StringName memberId)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (GdInterop.IsEmpty(normalizedMemberId) || _unitByMemberIdResolver == null)
            return null;
        return _unitByMemberIdResolver.Invoke(normalizedMemberId);
    }

    private static void MergeLowLuckBattleResultIntoResolution(
        BattleResolutionResult battleResolutionResult,
        GDictionary lowLuckEventResult
    )
    {
        if (
            battleResolutionResult == null
            || lowLuckEventResult == null
            || lowLuckEventResult.Count == 0
        )
            return;

        GArray extraLootEntries = GdInterop.GetArray(lowLuckEventResult, "loot_entries");
        if (extraLootEntries.Count > 0)
        {
            GArray mergedLootEntries = battleResolutionResult.loot_entries.Duplicate(true);
            GArray duplicatedLootEntries = extraLootEntries.Duplicate(true);
            foreach (var entry in duplicatedLootEntries)
                mergedLootEntries.Add(entry);
            battleResolutionResult.set_loot_entries(mergedLootEntries);
        }

        GArray extraRewards = GdInterop.GetArray(lowLuckEventResult, "pending_character_rewards");
        if (extraRewards.Count > 0)
        {
            GArray mergedRewards = battleResolutionResult.get_pending_character_rewards_copy();
            GArray duplicatedRewards = extraRewards.Duplicate(true);
            foreach (var entry in duplicatedRewards)
                mergedRewards.Add(entry);
            battleResolutionResult.set_pending_character_rewards(mergedRewards);
        }
    }

    private static string GetSkillSidecarMissingMessage(StringName skillId)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (normalizedSkillId == BlackStarBrandSkillId)
            return "黑星烙印的 calamity sidecar 未初始化。";
        if (normalizedSkillId == CrownBreakSkillId)
            return "折冠的 calamity sidecar 未初始化。";
        if (normalizedSkillId == DoomSentenceSkillId)
            return "厄命宣判的 calamity sidecar 未初始化。";
        if (normalizedSkillId == BlackCrownSealSkillId)
            return "黑冠封印的 battle sidecar 未初始化。";
        return "Misfortune battle sidecar 未初始化。";
    }
}
