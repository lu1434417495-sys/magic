using Godot;

[GlobalClass]
public partial class PracticeGrowthService : RefCounted
{
    public static readonly StringName TRACK_MEDITATION = "meditation";
    public static readonly StringName TRACK_CULTIVATION = "cultivation";
    private static readonly Godot.Collections.Array<StringName> PRACTICE_TRACKS = new() { TRACK_MEDITATION, TRACK_CULTIVATION };
    private const int TIER_BASIC = 0, TIER_INTERMEDIATE = 1, TIER_ADVANCED = 2, TIER_ULTIMATE = 3;
    private static readonly Godot.Collections.Dictionary TIER_NAME_TO_VALUE = new() { {"basic",TIER_BASIC},{"intermediate",TIER_INTERMEDIATE},{"advanced",TIER_ADVANCED},{"ultimate",TIER_ULTIMATE} };
    private static readonly Godot.Collections.Dictionary TIER_VALUE_TO_NAME = new() { {TIER_BASIC,"basic"},{TIER_INTERMEDIATE,"intermediate"},{TIER_ADVANCED,"advanced"},{TIER_ULTIMATE,"ultimate"} };
    private static readonly StringName MP_MAX_ATTR = "mp_max", AURA_MAX_ATTR = "aura_max";
    private Godot.Collections.Dictionary _skill_defs = new(), _profession_defs = new();

    public void setup(Godot.Collections.Dictionary skillDefs, Godot.Collections.Dictionary professionDefs) { _skill_defs = skillDefs; _profession_defs = professionDefs; }

    public StringName get_track_type_for_skill(StringName skillId) { var sd = _skill_defs.ContainsKey(skillId) ? _skill_defs[skillId].AsGodotObject() as SkillDef : null; return sd != null ? _get_exclusive_practice_track(sd) : new StringName(""); }

    private static StringName _get_exclusive_practice_track(SkillDef sd) { if (sd == null) return new StringName(""); StringName matched = new StringName(""); int count = 0; foreach (var tt in PRACTICE_TRACKS) { if (sd.tags.Contains(tt)) { matched = tt; count++; } } if (count != 1||sd.tags.Count!=1) return new StringName(""); return matched; }

    public static int resolve_tier_value(StringName tierName) => TIER_NAME_TO_VALUE.ContainsKey(tierName) ? (int)(long)TIER_NAME_TO_VALUE[tierName] : -1;
    public static StringName resolve_tier_name(int tierValue) => TIER_VALUE_TO_NAME.ContainsKey(tierValue) ? TIER_VALUE_TO_NAME[tierValue].AsStringName() : new StringName("");

    public Godot.Collections.Dictionary get_practice_skill_learn_status(StringName memberId, StringName skillId)
    {
        var r = new Godot.Collections.Dictionary { {"member_id",memberId},{"skill_id",skillId},{"is_practice_skill",false},{"track_type",new StringName("")},{"tier_value",-1},{"tier_name",new StringName("")},{"current_skill_level",0},{"effective_max_level",0},{"is_at_effective_max_level",false} };
        var sd = _skill_defs.ContainsKey(skillId) ? _skill_defs[skillId].AsGodotObject() as SkillDef : null;
        var tt = sd != null ? _get_exclusive_practice_track(sd) : new StringName("");
        if (sd == null || tt == "") return r;
        r["track_type"] = tt; r["is_practice_skill"] = true;
        int tv = TIER_NAME_TO_VALUE.ContainsKey(sd.practice_tier) ? (int)(long)TIER_NAME_TO_VALUE[sd.practice_tier] : -1;
        r["tier_value"] = tv; r["tier_name"] = sd.practice_tier;
        return r;
    }

    public Godot.Collections.Dictionary get_track_slot_status(StringName memberId, StringName trackType, int tierValue)
    {
        var r = new Godot.Collections.Dictionary { {"member_id",memberId},{"track_type",trackType},{"tier_value",tierValue},{"occupied",false},{"occupying_skill_id",new StringName("")},{"is_at_effective_max_level",false} };
        if (trackType==""||tierValue<0) return r;
        var ms = _resolve_member_state(memberId); if (ms?.progression==null) return r;
        foreach (var sk in ProgressionDataUtils.sorted_string_keys(ms.progression.Get("skills").AsGodotDictionary())) {
            var skId = new StringName(sk); var sp = ms.progression.Call("get_skill_progress",skId).AsGodotObject();
            if (sp==null||!(bool)sp.Get("is_learned")) continue;
            var sd = _skill_defs.ContainsKey(skId)?_skill_defs[skId].AsGodotObject() as SkillDef:null;
            if (sd==null||_get_exclusive_practice_track(sd)!=trackType) continue;
            int st = TIER_NAME_TO_VALUE.ContainsKey(sd.practice_tier)?(int)(long)TIER_NAME_TO_VALUE[sd.practice_tier]:-1;
            if (st!=tierValue) continue;
            r["occupied"]=true; r["occupying_skill_id"]=skId;
            r["is_at_effective_max_level"]=SkillEffectiveMaxLevelRules.is_at_effective_max_level(sd,Variant.From(sp),ms.progression);
            break;
        }
        return r;
    }

    public Godot.Collections.Dictionary replace_practice_skill(StringName memberId, StringName newSkillId, Godot.Collections.Dictionary options = null)
    {
        var r = new Godot.Collections.Dictionary { {"ok",false},{"member_id",memberId},{"skill_id",newSkillId},{"replaced_skill_id",new StringName("")},{"replacement_source",new StringName("")} };
        var ms = _resolve_member_state(memberId); if (ms?.progression==null){r["error"]="member_not_found";return r;}
        var ns=_skill_defs.ContainsKey(newSkillId)?_skill_defs[newSkillId].AsGodotObject() as SkillDef:null;
        if (ns==null){r["error"]="skill_not_found";return r;}
        var tt=_get_exclusive_practice_track(ns);if(tt==""){r["error"]="not_practice_skill";return r;}
        int ntv=TIER_NAME_TO_VALUE.ContainsKey(ns.practice_tier)?(int)(long)TIER_NAME_TO_VALUE[ns.practice_tier]:-1;
        if (ntv<0){r["error"]="invalid_tier";return r;}
        var ts=get_track_slot_status(memberId,tt,ntv);
        if ((bool)ts["occupied"]){
            var osk=ts["occupying_skill_id"].AsStringName();
            if (!SkillEffectiveMaxLevelRules.is_at_effective_max_level(ns,Variant.From(ms.progression.Call("get_skill_progress",osk)),ms.progression)){r["error"]="slot_occupied_not_at_max";return r;}
            var replaced=ms.progression.Call("replace_practice_skill",osk,newSkillId).AsGodotDictionary();
            if (!(bool)replaced["ok"]){r["error"]=replaced.ContainsKey("error")?replaced["error"].AsString():"replace_failed";return r;}
            r["replaced_skill_id"]=osk; r["replacement_source"]="slot_replacement";
        } else {
            if (!(bool)ms.progression.Call("learn_skill",newSkillId,options??new Godot.Collections.Dictionary())){r["error"]="learn_failed";return r;}
        }
        r["ok"]=true; return r;
    }

    private PartyMemberState _resolve_member_state(StringName memberId) { return null; /* Caller must provide member state via context */ }

    public static string get_track_display_name(StringName trackType) => trackType==TRACK_MEDITATION?"冥想":"修炼";
    public static string get_tier_display_name(int tierValue) { switch (tierValue) { case TIER_BASIC:return "基础"; case TIER_INTERMEDIATE:return "进阶"; case TIER_ADVANCED:return "高阶"; case TIER_ULTIMATE:return "终极"; default:return ""; } }
}
