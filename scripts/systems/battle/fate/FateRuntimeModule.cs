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

    private const string FortunaGuidanceServicePath = "res://scripts/systems/battle/fate/fortuna_guidance_service.gd";
    private const string LowLuckEventServicePath = "res://scripts/systems/battle/fate/low_luck_event_service.gd";
    private const string MisfortuneGuidanceServicePath = "res://scripts/systems/battle/fate/misfortune_guidance_service.gd";
    private const string MisfortuneServicePath = "res://scripts/systems/battle/fate/misfortune_service.gd";

    private GodotObject _characterGateway;
    private GodotObject _battleRuntimeGateway;
    private Callable _unitByMemberIdCallback = new();
    private FortuneService _fortuneService = new();
    private GodotObject _fortunaGuidanceService = NewGdObject(FortunaGuidanceServicePath);
    private GodotObject _lowLuckEventService = NewGdObject(LowLuckEventServicePath);
    private GodotObject _misfortuneGuidanceService = NewGdObject(MisfortuneGuidanceServicePath);
    private GodotObject _misfortuneService = NewGdObject(MisfortuneServicePath);

    public void setup(
        GodotObject character_gateway = null,
        BattleFateEventBus fate_event_bus = null,
        GodotObject battle_runtime_gateway = null,
        Callable unit_by_member_id_callback = default)
    {
        _characterGateway = character_gateway;
        _battleRuntimeGateway = battle_runtime_gateway;
        _unitByMemberIdCallback = IsCallableValid(unit_by_member_id_callback) ? unit_by_member_id_callback : new Callable();

        // Guidance must see the pre-mark state before FortuneService mutates fortune_marked on the same bus event.
        _fortunaGuidanceService?.Call("setup", _characterGateway, fate_event_bus);
        _fortuneService?.setup(_characterGateway, fate_event_bus);
        _misfortuneService?.Call("setup", fate_event_bus, _unitByMemberIdCallback);
        _lowLuckEventService?.Call("setup", _characterGateway, fate_event_bus);
        _misfortuneGuidanceService?.Call("setup", _characterGateway, _battleRuntimeGateway);
    }

    public void dispose()
    {
        DisposeSidecar(ref _fortunaGuidanceService);
        if (_fortuneService != null)
        {
            _fortuneService.dispose();
            _fortuneService.Dispose();
            _fortuneService = null;
        }
        DisposeSidecar(ref _misfortuneService);
        DisposeSidecar(ref _lowLuckEventService);
        DisposeSidecar(ref _misfortuneGuidanceService);
        _characterGateway = null;
        _battleRuntimeGateway = null;
        _unitByMemberIdCallback = new Callable();
        if (GodotObject.IsInstanceValid(this))
            Dispose();
    }

    public void begin_battle(GDictionary calamity_store = null)
    {
        _misfortuneService?.Call("begin_battle", calamity_store ?? new GDictionary());
    }

    public GDictionary get_calamity_by_member_id()
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return ToDictionary(_misfortuneService.Call("get_calamity_by_member_id"));
    }

    public int get_member_calamity(StringName member_id)
    {
        if (_misfortuneService == null)
            return 0;
        return _misfortuneService.Call("get_member_calamity", member_id).AsInt32();
    }

    public int get_member_calamity_cap(StringName member_id)
    {
        if (_misfortuneService == null)
            return BaseCalamityCap;
        return _misfortuneService.Call("get_member_calamity_cap", member_id).AsInt32();
    }

    public int get_black_star_brand_cast_cost(StringName member_id)
    {
        if (_misfortuneService == null)
            return BlackStarBrandRepeatCalamityCost;
        return _misfortuneService.Call("get_black_star_brand_calamity_cost", member_id).AsInt32();
    }

    public bool has_misfortune_reason(StringName member_id, StringName reason_id)
    {
        return _misfortuneService != null && _misfortuneService.Call("has_triggered_reason", member_id, reason_id).AsBool();
    }

    public string get_misfortune_skill_cast_block_reason(GodotObject unit_state, StringName skill_id)
    {
        if (_misfortuneService == null)
            return GetSkillSidecarMissingMessage(skill_id);
        return _misfortuneService.Call("get_skill_cast_block_reason", unit_state, skill_id).AsString();
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
        return ToDictionary(_misfortuneService.Call("consume_skill_cast", unit_state, skill_id));
    }

    public Variant handle_misfortune_trigger(StringName reason_id, GDictionary payload = null)
    {
        if (_misfortuneService == null)
            return Variant.From(new GDictionary());
        return _misfortuneService.Call("handle_trigger", reason_id, payload ?? new GDictionary());
    }

    public GDictionary handle_member_boss_phase_changed(StringName member_id, StringName phase_id = default)
    {
        GodotObject unitState = ResolveUnitByMemberId(member_id);
        if (unitState == null)
            return new GDictionary();
        Variant result = handle_misfortune_trigger(
            CalamityReasonBossPhaseChanged,
            new GDictionary
            {
                ["unit_state"] = unitState,
                ["phase_id"] = GdInterop.IsEmpty(phase_id) ? new StringName("") : phase_id,
            });
        return ToDictionary(result);
    }

    public GDictionary handle_applied_statuses(GodotObject target_unit, Variant status_effect_ids)
    {
        Variant result = handle_misfortune_trigger(
            CalamityReasonStrongDebuff,
            new GDictionary
            {
                ["target_unit"] = target_unit,
                ["status_effect_ids"] = status_effect_ids,
            });
        return ToDictionary(result);
    }

    public GDictionary handle_battle_resolution(BattleState battle_state, BattleResolutionResult battle_resolution_result)
    {
        GDictionary lowLuckEventResult = new();
        if (_lowLuckEventService != null)
        {
            lowLuckEventResult = ToDictionary(_lowLuckEventService.Call("handle_battle_resolution", battle_state, battle_resolution_result));
            MergeLowLuckBattleResultIntoResolution(battle_resolution_result, lowLuckEventResult);
        }

        Godot.Collections.Array<StringName> fortunaGuidanceUnlocks = new();
        if (_fortunaGuidanceService != null)
            fortunaGuidanceUnlocks = ToStringNameArray(_fortunaGuidanceService.Call("handle_battle_resolution", battle_state, battle_resolution_result));

        Godot.Collections.Array<StringName> misfortuneGuidanceUnlocks = new();
        if (_misfortuneGuidanceService != null)
            misfortuneGuidanceUnlocks = ToStringNameArray(_misfortuneGuidanceService.Call("handle_battle_resolution", battle_state, battle_resolution_result));

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
        return ToStringNameArray(_fortunaGuidanceService.Call("handle_chapter_completed", payload ?? new GDictionary()));
    }

    public Godot.Collections.Array<StringName> handle_misfortune_forge_result(
        StringName member_id,
        GDictionary result,
        GDictionary item_defs = null)
    {
        if (_misfortuneGuidanceService == null)
            return new Godot.Collections.Array<StringName>();
        return ToStringNameArray(_misfortuneGuidanceService.Call("handle_forge_result", member_id, result ?? new GDictionary(), item_defs ?? new GDictionary()));
    }

    public GDictionary resolve_low_luck_settlement_event_rewards(GDictionary context)
    {
        if (_lowLuckEventService == null)
            return new GDictionary();
        return ToDictionary(_lowLuckEventService.Call("handle_settlement_action", context ?? new GDictionary()));
    }

    public void clear_misfortune_exalted_ready_flags(GArray member_ids = null)
    {
        if (_misfortuneGuidanceService != null)
            _misfortuneGuidanceService.Call("clear_exalted_ready_flags", member_ids ?? new GArray());
    }

    public void set_fortune_confirmation_rng_for_testing(Variant rng = default)
    {
        _fortuneService?.set_confirmation_rng_for_testing(rng);
    }

    private GodotObject ResolveUnitByMemberId(StringName memberId)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (GdInterop.IsEmpty(normalizedMemberId) || !IsCallableValid(_unitByMemberIdCallback))
            return null;
        Variant result = _unitByMemberIdCallback.Call(normalizedMemberId);
        return result.VariantType == Variant.Type.Object ? result.AsGodotObject() : null;
    }

    private static void MergeLowLuckBattleResultIntoResolution(
        BattleResolutionResult battleResolutionResult,
        GDictionary lowLuckEventResult)
    {
        if (battleResolutionResult == null || lowLuckEventResult == null || lowLuckEventResult.Count == 0)
            return;

        GArray extraLootEntries = GdInterop.GetArray(lowLuckEventResult, "loot_entries");
        if (extraLootEntries.Count > 0)
        {
            GArray mergedLootEntries = battleResolutionResult.loot_entries.Duplicate(true);
            GArray duplicatedLootEntries = extraLootEntries.Duplicate(true);
            foreach (Variant entry in duplicatedLootEntries)
                mergedLootEntries.Add(entry);
            battleResolutionResult.set_loot_entries(mergedLootEntries);
        }

        GArray extraRewards = GdInterop.GetArray(lowLuckEventResult, "pending_character_rewards");
        if (extraRewards.Count > 0)
        {
            GArray mergedRewards = battleResolutionResult.get_pending_character_rewards_copy();
            GArray duplicatedRewards = extraRewards.Duplicate(true);
            foreach (Variant entry in duplicatedRewards)
                mergedRewards.Add(entry);
            battleResolutionResult.set_pending_character_rewards(mergedRewards);
        }
    }

    private static GodotObject NewGdObject(string scriptPath)
    {
        GDScript script = GD.Load<GDScript>(scriptPath);
        return script?.Call("new").AsGodotObject();
    }

    private static void CallIfPresent(GodotObject target, StringName methodName)
    {
        if (target != null && target.HasMethod(methodName))
            target.Call(methodName);
    }

    private static void DisposeSidecar(ref GodotObject sidecar)
    {
        if (sidecar == null)
            return;
        if (GodotObject.IsInstanceValid(sidecar))
        {
            CallIfPresent(sidecar, "dispose");
            sidecar.Dispose();
        }
        sidecar = null;
    }

    private static bool IsCallableValid(Callable callable)
    {
        return !callable.Equals(default(Callable)) && !string.IsNullOrEmpty(callable.Method.ToString());
    }

    private static GDictionary ToDictionary(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(Variant value)
    {
        Godot.Collections.Array<StringName> result = new();
        if (value.VariantType != Variant.Type.Array)
            return result;

        foreach (Variant rawValue in value.AsGodotArray())
        {
            StringName normalizedValue = ProgressionDataUtils.to_string_name(rawValue);
            if (!GdInterop.IsEmpty(normalizedValue))
                result.Add(normalizedValue);
        }
        return result;
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
