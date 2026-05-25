using Godot;

[GlobalClass]
public partial class BattleStatusSemanticTable : RefCounted
{
    public static readonly StringName STACK_REFRESH = "refresh", STACK_ADD = "add";
    public static readonly StringName TICK_NONE = "none", TICK_TURN_START_AP_PENALTY = "turn_start_ap_penalty", TICK_TURN_START_DAMAGE = "turn_start_damage", TICK_TIMELINE_DAMAGE = "timeline_damage";
    public const int TU_GRANULARITY = 5, DEFAULT_BLIND_ATTACK_ROLL_PENALTY = 4;
    public static readonly StringName STATUS_ARMOR_BREAK="armor_break", STATUS_ARCHER_PRE_AIM="archer_pre_aim", STATUS_ARCHER_RANGE_UP="archer_range_up", STATUS_ARCHER_SHOOTING_SPECIALIZATION="archer_shooting_specialization", STATUS_ATTACK_UP="attack_up", STATUS_ATTACK_ROLL_BONUS_UP="attack_roll_bonus_up", STATUS_BURNING="burning", STATUS_BLIND="blind", STATUS_DEATH_WARD="death_ward", STATUS_DAMAGE_REDUCTION_UP="damage_reduction_up", STATUS_DODGE_BONUS_UP="dodge_bonus_up", STATUS_FROZEN="frozen", STATUS_GUARDING="guarding", STATUS_HEX_OF_FRAILTY="hex_of_frailty", STATUS_MAGIC_SHIELD="magic_shield", STATUS_MARKED="marked", STATUS_METEOR_CONCUSSED="meteor_concussed", STATUS_PINNED="pinned", STATUS_PRISMATIC_BARRIER="prismatic_barrier", STATUS_PETRIFIED="petrified", STATUS_MADNESS="madness", STATUS_ROOTED="rooted", STATUS_SHOCKED="shocked", STATUS_SLOW="slow", STATUS_SPELLWARD="spellward", STATUS_SOUL_FRACTURE="soul_fracture", STATUS_STAGGERED="staggered", STATUS_TAUNTED="taunted", STATUS_TENDON_CUT="tendon_cut", STATUS_CROWN_BREAK_BROKEN_FANG="crown_break_broken_fang", STATUS_CROWN_BREAK_BROKEN_HAND="crown_break_broken_hand", STATUS_CROWN_BREAK_BLINDED_EYE="crown_break_blinded_eye", STATUS_DOOM_SENTENCE_VERDICT="doom_sentence_verdict", STATUS_LAST_STAND_ACTIVE="last_stand_active", STATUS_WILLPOWER_SAVE_BONUS_UP="willpower_save_bonus_up";

    public static bool has_semantic(StringName sid) => get_semantic(sid).Count > 0;

    public static bool is_harmful_status(StringName sid) { var n = ProgressionDataUtils.to_string_name(sid); return n==STATUS_ARMOR_BREAK||n==STATUS_BLIND||n==STATUS_FROZEN||n==STATUS_MARKED||n==STATUS_METEOR_CONCUSSED||n==STATUS_PINNED||n==STATUS_ROOTED||n==STATUS_SHOCKED||n==STATUS_TAUNTED||n==STATUS_TENDON_CUT||n==STATUS_BURNING||n==STATUS_SLOW||n==STATUS_SOUL_FRACTURE||n==STATUS_STAGGERED||n==STATUS_HEX_OF_FRAILTY||n==STATUS_CROWN_BREAK_BROKEN_FANG||n==STATUS_CROWN_BREAK_BROKEN_HAND||n==STATUS_CROWN_BREAK_BLINDED_EYE||n==STATUS_DOOM_SENTENCE_VERDICT||n==STATUS_PETRIFIED||n==STATUS_MADNESS||n=="black_star_brand_normal"||n=="black_star_brand_elite"; }

    public static bool is_cleansable_harmful_status(StringName sid) => ProgressionDataUtils.to_string_name(sid) == STATUS_PETRIFIED ? false : is_harmful_status(sid);

    public static bool is_dispellable_harmful_status(StringName sid) { var n=ProgressionDataUtils.to_string_name(sid); return n==STATUS_BLIND||n==STATUS_BURNING||n==STATUS_DOOM_SENTENCE_VERDICT||n==STATUS_FROZEN||n==STATUS_HEX_OF_FRAILTY||n==STATUS_MADNESS||n==STATUS_MARKED||n==STATUS_METEOR_CONCUSSED||n==STATUS_PINNED||n==STATUS_ROOTED||n==STATUS_SHOCKED||n==STATUS_SLOW||n==STATUS_SOUL_FRACTURE||n==STATUS_STAGGERED||n==STATUS_TAUNTED; }

    public static bool is_dispellable_beneficial_status(StringName sid) { var n=ProgressionDataUtils.to_string_name(sid); return n==STATUS_ATTACK_UP||n==STATUS_ATTACK_ROLL_BONUS_UP||n==STATUS_DAMAGE_REDUCTION_UP||n==STATUS_DEATH_WARD||n==STATUS_DODGE_BONUS_UP||n==STATUS_MAGIC_SHIELD||n==STATUS_PRISMATIC_BARRIER||n==STATUS_SPELLWARD||n==STATUS_WILLPOWER_SAVE_BONUS_UP; }

    public static bool is_dispellable_harmful_status_entry(BattleStatusEffectState se) { if(se==null)return false; if(_gpb(se.@params,"undispellable",false))return false; if(_gpb(se.@params,"dispellable_harmful_magic",false))return true; if(_gpb(se.@params,"dispellable_magic",false))return is_harmful_status(se.status_id); return is_dispellable_harmful_status(se.status_id); }
    public static bool is_dispellable_beneficial_status_entry(BattleStatusEffectState se) { if(se==null)return false; if(_gpb(se.@params,"undispellable",false))return false; if(_gpb(se.@params,"dispellable_beneficial_magic",false))return true; if(_gpb(se.@params,"dispellable_magic",false))return !is_harmful_status(se.status_id); return is_dispellable_beneficial_status(se.status_id); }

    public static int get_dispel_priority(StringName sid) { var n=ProgressionDataUtils.to_string_name(sid); if(n==STATUS_DEATH_WARD||n==STATUS_MAGIC_SHIELD||n==STATUS_PRISMATIC_BARRIER||n==STATUS_SPELLWARD)return 100; if(n==STATUS_BLIND||n==STATUS_FROZEN||n==STATUS_MADNESS||n==STATUS_ROOTED)return 90; if(n==STATUS_ATTACK_UP||n==STATUS_ATTACK_ROLL_BONUS_UP||n==STATUS_DAMAGE_REDUCTION_UP||n==STATUS_DODGE_BONUS_UP||n==STATUS_WILLPOWER_SAVE_BONUS_UP)return 80; if(n==STATUS_BURNING||n==STATUS_HEX_OF_FRAILTY||n==STATUS_METEOR_CONCUSSED||n==STATUS_PINNED||n==STATUS_SHOCKED||n==STATUS_SLOW||n==STATUS_STAGGERED||n==STATUS_TAUNTED)return 70; return 50; }

    public static Godot.Collections.Dictionary get_semantic(StringName sid)
    {
        var n = ProgressionDataUtils.to_string_name(sid);
        if (n==STATUS_ARCHER_PRE_AIM||n==STATUS_ARCHER_RANGE_UP||n==STATUS_ARCHER_SHOOTING_SPECIALIZATION||n==STATUS_ATTACK_UP||n==STATUS_ATTACK_ROLL_BONUS_UP||n==STATUS_DAMAGE_REDUCTION_UP||n==STATUS_DEATH_WARD||n==STATUS_DODGE_BONUS_UP||n==STATUS_GUARDING||n==STATUS_HEX_OF_FRAILTY||n==STATUS_MAGIC_SHIELD||n==STATUS_PRISMATIC_BARRIER||n==STATUS_SPELLWARD||n==STATUS_LAST_STAND_ACTIVE||n==STATUS_WILLPOWER_SAVE_BONUS_UP) return _brt();
        if (n==STATUS_BLIND) { var s=_brt(); s["attack_roll_penalty"]=DEFAULT_BLIND_ATTACK_ROLL_PENALTY; return s; }
        if (n==STATUS_ARMOR_BREAK||n==STATUS_FROZEN||n==STATUS_MARKED||n==STATUS_PINNED||n==STATUS_ROOTED||n==STATUS_SHOCKED||n==STATUS_TAUNTED||n==STATUS_TENDON_CUT||n==STATUS_CROWN_BREAK_BROKEN_FANG||n==STATUS_CROWN_BREAK_BROKEN_HAND||n==STATUS_CROWN_BREAK_BLINDED_EYE||n==STATUS_DOOM_SENTENCE_VERDICT||n==STATUS_PETRIFIED||n==STATUS_MADNESS) return _brt();
        if (n==STATUS_BURNING) return new Godot.Collections.Dictionary{{"stack_mode",STACK_ADD},{"max_stacks",3},{"tick_mode",TICK_TIMELINE_DAMAGE}};
        if (n==STATUS_SLOW) return new Godot.Collections.Dictionary{{"stack_mode",STACK_REFRESH},{"max_stacks",1},{"tick_mode",TICK_NONE},{"move_cost_delta",1}};
        if (n==STATUS_METEOR_CONCUSSED) return new Godot.Collections.Dictionary{{"stack_mode",STACK_REFRESH},{"max_stacks",1},{"tick_mode",TICK_TURN_START_AP_PENALTY},{"attack_roll_penalty",2},{"ap_penalty_group",STATUS_STAGGERED},{"consume_after_ap_penalty",true},{"display_label","震眩"},{"turn_start_log_reason_id","meteor_concussed_ap_consumed"}};
        if (n==STATUS_STAGGERED) { var ss=_brt(TICK_TURN_START_AP_PENALTY); ss["ap_penalty_group"]=STATUS_STAGGERED; ss["display_label"]="踉跄"; return ss; }
        return new Godot.Collections.Dictionary();
    }

    public static BattleStatusEffectState merge_status(GodotObject effectDef, StringName sourceUnitId, BattleStatusEffectState existingEntry = null)
    {
        if (effectDef == null || ProgressionDataUtils.to_string_name(effectDef.Get("status_id")) == "") return null;
        var semantic = get_semantic(effectDef.Get("status_id").AsStringName());
        var se = existingEntry?.duplicate_state() ?? new BattleStatusEffectState();
        se.status_id = ProgressionDataUtils.to_string_name(effectDef.Get("status_id"));
        se.source_unit_id = sourceUnitId;
        se.@params = _clone_effect_params(effectDef);
        int incomingPower = Mathf.Max(effectDef.Get("power").AsInt32(), 1);
        int prevPower = Mathf.Max(se.power, 0); int prevStacks = Mathf.Max(se.stacks, 0);
        if (semantic.Count == 0) { se.power = effectDef.Get("power").AsInt32(); se.stacks = Mathf.Max(prevStacks + 1, 1); int sd = _resolve_duration_tu(effectDef); if (sd >= 0) se.duration = sd; return se; }
        var stackMode = ProgressionDataUtils.to_string_name(semantic.ContainsKey("stack_mode") ? semantic["stack_mode"] : STACK_REFRESH);
        int maxStacks = semantic.ContainsKey("max_stacks") ? semantic["max_stacks"].AsInt32() : 0;
        se.power = Mathf.Max(prevPower, incomingPower);
        se.stacks = stackMode == STACK_ADD ? (maxStacks > 0 ? Mathf.Min(Mathf.Max(prevStacks+1, 1), maxStacks) : Mathf.Max(prevStacks+1, 1)) : 1;
        int semDur = _resolve_duration_tu(effectDef); if (semDur >= 0) { int prevDur = se.duration; se.duration = Mathf.Max(semDur, prevDur); }
        int tickInt = _resolve_tick_interval_tu(effectDef); if (tickInt > 0) { se.tick_interval_tu = tickInt; if (se.next_tick_at_tu <= 0) se.next_tick_at_tu = tickInt; }
        return se;
    }

    public static int get_turn_start_ap_penalty(BattleStatusEffectState se) { if(se==null)return 0; var s=get_semantic(se.status_id); return ProgressionDataUtils.to_string_name(s.ContainsKey("tick_mode")?s["tick_mode"]:TICK_NONE)!=TICK_TURN_START_AP_PENALTY?0:_get_effect_intensity(se); }
    public static StringName get_turn_start_ap_penalty_group(BattleStatusEffectState se) { if(se==null)return new StringName(""); var s=get_semantic(se.status_id); return ProgressionDataUtils.to_string_name(s.ContainsKey("tick_mode")?s["tick_mode"]:TICK_NONE)!=TICK_TURN_START_AP_PENALTY?new StringName(""):ProgressionDataUtils.to_string_name(s.ContainsKey("ap_penalty_group")?s["ap_penalty_group"]:se.status_id); }
    public static bool should_consume_after_turn_start_ap_penalty(BattleStatusEffectState se) { if(se==null)return false; var s=get_semantic(se.status_id); return ProgressionDataUtils.to_string_name(s.ContainsKey("tick_mode")?s["tick_mode"]:TICK_NONE)==TICK_TURN_START_AP_PENALTY&&s.ContainsKey("consume_after_ap_penalty")&&s["consume_after_ap_penalty"].AsBool(); }
    public static string get_turn_start_ap_penalty_display_label(BattleStatusEffectState se) { if(se==null)return""; var s=get_semantic(se.status_id); if(ProgressionDataUtils.to_string_name(s.ContainsKey("tick_mode")?s["tick_mode"]:TICK_NONE)!=TICK_TURN_START_AP_PENALTY)return""; string l=s.ContainsKey("display_label")?s["display_label"].AsString():""; return l.StripEdges().Length>0?l:(string)se.status_id; }
    public static int get_turn_start_damage(BattleStatusEffectState se) { if(se==null)return 0; var s=get_semantic(se.status_id); return ProgressionDataUtils.to_string_name(s.ContainsKey("tick_mode")?s["tick_mode"]:TICK_NONE)!=TICK_TURN_START_DAMAGE?0:_get_effect_intensity(se); }
    public static int get_timeline_tick_damage(BattleStatusEffectState se) { if(se==null||se.tick_interval_tu<=0)return 0; var s=get_semantic(se.status_id); return ProgressionDataUtils.to_string_name(s.ContainsKey("tick_mode")?s["tick_mode"]:TICK_NONE)!=TICK_TIMELINE_DAMAGE?0:_get_effect_intensity(se); }
    public static int get_move_cost_delta(BattleStatusEffectState se) { if(se==null)return 0; var s=get_semantic(se.status_id); int bd=Mathf.Max(s.ContainsKey("move_cost_delta")?s["move_cost_delta"].AsInt32():0,0); return bd<=0?0:bd*_get_effect_intensity(se); }
    public static int get_attack_roll_penalty(BattleStatusEffectState se) { if(se==null)return 0; var s=get_semantic(se.status_id); int dp=Mathf.Max(s.ContainsKey("attack_roll_penalty")?s["attack_roll_penalty"].AsInt32():0,0); return Mathf.Max(_gpi(se.@params,"attack_roll_penalty",dp),0); }
    public static Godot.Collections.Dictionary advance_timeline_duration(BattleStatusEffectState se, int elapsedTu) { if(se==null||elapsedTu<=0||se.duration<0)return new Godot.Collections.Dictionary{{"expired",false},{"changed",false}}; int pd=se.duration; int rd=Mathf.Max(pd-elapsedTu,0); if(rd<=0)return new Godot.Collections.Dictionary{{"expired",true},{"changed",true}}; se.duration=rd; return new Godot.Collections.Dictionary{{"expired",false},{"changed",rd!=pd}}; }

    private static Godot.Collections.Dictionary _brt(StringName tm=default) { tm=tm==""?TICK_NONE:tm; return new Godot.Collections.Dictionary{{"stack_mode",STACK_REFRESH},{"max_stacks",1},{"tick_mode",tm}}; }
    private static int _resolve_duration_tu(GodotObject ed) { if(ed==null)return -1; if(ed.Get("params").AsGodotDictionary()!=null&&ed.Get("params").AsGodotDictionary().ContainsKey("duration_tu"))return _npt(ed.Get("params").AsGodotDictionary()["duration_tu"].AsInt32(),"status params.duration_tu"); if(ed.Get("duration_tu").AsInt32()>0)return _npt(ed.Get("duration_tu").AsInt32(),"status duration_tu"); return -1; }
    private static int _resolve_tick_interval_tu(GodotObject ed) { if(ed==null)return 0; if(ed.Get("tick_interval_tu").AsInt32()>0)return _npt(ed.Get("tick_interval_tu").AsInt32(),"status tick_interval_tu"); if(ed.Get("params").AsGodotDictionary()!=null&&ed.Get("params").AsGodotDictionary().ContainsKey("tick_interval_tu"))return _npt(ed.Get("params").AsGodotDictionary()["tick_interval_tu"].AsInt32(),"status params.tick_interval_tu"); return 0; }
    private static Godot.Collections.Dictionary _clone_effect_params(GodotObject ed) { if(ed?.Get("params").AsGodotDictionary()==null)return new Godot.Collections.Dictionary(); return ed.Get("params").AsGodotDictionary().Duplicate(true); }
    private static int _gpi(Godot.Collections.Dictionary @params, StringName pk, int fb) { if(@params==null||pk=="")return fb; if(@params.ContainsKey(pk)) return @params[pk].AsInt32(); string pn=(string)pk; if(@params.ContainsKey(pn)) return @params[pn].AsInt32(); foreach(var k in @params.Keys)if(ProgressionDataUtils.to_string_name(k)==pk) return @params[k].AsInt32(); return fb; }
    private static bool _gpb(Godot.Collections.Dictionary @params, StringName pk, bool fb) { if(@params==null||pk=="")return fb; if(@params.ContainsKey(pk)) return @params[pk].AsBool(); string pn=(string)pk; if(@params.ContainsKey(pn)) return @params[pn].AsBool(); foreach(var k in @params.Keys)if(ProgressionDataUtils.to_string_name(k)==pk) return @params[k].AsBool(); return fb; }
    private static int _get_effect_intensity(BattleStatusEffectState se) => se==null?0:Mathf.Max(Mathf.Max(se.power,se.stacks),1);
    private static int _npt(int v, string fl) { if(v<=0)return -1; if(v%TU_GRANULARITY!=0){int cv=((v+TU_GRANULARITY-1)/TU_GRANULARITY)*TU_GRANULARITY; GD.PushError($"{fl} must use {TU_GRANULARITY} TU steps, got {v}; clamping up to {cv}."); return cv;} return v; }
}
