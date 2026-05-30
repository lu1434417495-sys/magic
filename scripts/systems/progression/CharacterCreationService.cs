using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class CharacterCreationService : RefCounted
{
    private const int HIDDEN_LUCK_AT_BIRTH_MAX = 2,
        HIDDEN_LUCK_AT_BIRTH_MIN = -6,
        INITIAL_HP_BASE = 14,
        MAXIMUM_REROLL_TIER_MINIMUM = 10_000_000;
    private static readonly StringName DefaultSourceId = "birth_roll";
    private const string CreationOptionBakeRerollLuck = "bake_reroll_luck";
    private static readonly Godot.Collections.Array<string> IDENTITY_BODY_SIZE_SOURCE_FIELDS = new()
    {
        "race_id",
        "subrace_id",
        "bloodline_id",
        "bloodline_stage_id",
        "ascension_id",
        "ascension_stage_id",
        "body_size",
        "body_size_category",
    };

    public static StringName DEFAULT_SOURCE_ID() => DefaultSourceId;

    public static string CREATION_OPTION_BAKE_REROLL_LUCK() => CreationOptionBakeRerollLuck;

    public static int calculate_initial_hp_max(int constitutionValue) =>
        Mathf.Max(
            1,
            INITIAL_HP_BASE
                + ProgressionService.calculate_constitution_modifier(constitutionValue) * 2
        );

    public static PartyMemberState create_member_from_character_creation_payload(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        Godot.Collections.Dictionary progressionContentSource = null,
        Godot.Collections.Dictionary options = null
    ) => CreateMemberFromCharacterCreationPayload(
        memberId,
        payload,
        ProgressionContentSourceRef.FromDictionary(progressionContentSource),
        options
    );

    public static PartyMemberState create_member_from_character_creation_payload_without_content_source(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        Godot.Collections.Dictionary options
    ) => CreateMemberFromCharacterCreationPayload(
        memberId,
        payload,
        ProgressionContentSourceRef.Empty,
        options
    );

    public static PartyMemberState create_member_from_character_creation_payload_for_content_source(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        GodotObject progressionContentSource,
        Godot.Collections.Dictionary options = null
    ) => CreateMemberFromCharacterCreationPayload(
        memberId,
        payload,
        ProgressionContentSourceRef.FromObject(progressionContentSource),
        options
    );

    private static PartyMemberState CreateMemberFromCharacterCreationPayload(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        ProgressionContentSourceRef progressionContentSource,
        Godot.Collections.Dictionary options = null
    )
    {
        options ??= new Godot.Collections.Dictionary();
        var ms = new PartyMemberState { member_id = memberId };
        ms.progression = new UnitProgress
        {
            unit_id = memberId,
            unit_base_attributes = new UnitBaseAttributes(),
        };
        return ApplyCharacterCreationPayloadToMember(
            ms,
            payload,
            progressionContentSource,
            options
        )
            ? ms
            : null;
    }

    public static bool apply_character_creation_payload_to_member(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        Godot.Collections.Dictionary progressionContentSource = null,
        Godot.Collections.Dictionary options = null
    ) => ApplyCharacterCreationPayloadToMember(
        memberState,
        payload,
        ProgressionContentSourceRef.FromDictionary(progressionContentSource),
        options
    );

    public static bool apply_character_creation_payload_to_member_without_content_source(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        Godot.Collections.Dictionary options
    ) => ApplyCharacterCreationPayloadToMember(
        memberState,
        payload,
        ProgressionContentSourceRef.Empty,
        options
    );

    public static bool apply_character_creation_payload_to_member_for_content_source(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        GodotObject progressionContentSource,
        Godot.Collections.Dictionary options = null
    ) => ApplyCharacterCreationPayloadToMember(
        memberState,
        payload,
        ProgressionContentSourceRef.FromObject(progressionContentSource),
        options
    );

    private static bool ApplyCharacterCreationPayloadToMember(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        ProgressionContentSourceRef progressionContentSource,
        Godot.Collections.Dictionary options = null
    )
    {
        options ??= new Godot.Collections.Dictionary();
        if (memberState == null || payload == null || payload.Count == 0)
            return false;
        if (
            !_validate_payload_identity_before_mutation(
                memberState,
                payload,
                progressionContentSource
            )
        )
            return false;
        if (memberState.progression == null)
        {
            memberState.progression = new UnitProgress();
        }
        if (memberState.progression.unit_id == "")
            memberState.progression.unit_id = memberState.member_id;
        if (memberState.progression.unit_base_attributes == null)
            memberState.progression.unit_base_attributes = new UnitBaseAttributes();
        string dn = payload.ContainsKey("display_name")
            ? payload["display_name"].AsString().StripEdges()
            : memberState.display_name;
        if (dn.Length > 0)
        {
            memberState.display_name = dn;
            memberState.progression.display_name = dn;
        }
        var ba = memberState.progression.unit_base_attributes;
        foreach (
            string aid in new[]
            {
                "strength",
                "agility",
                "constitution",
                "perception",
                "intelligence",
                "willpower",
            }
        )
            if (payload.ContainsKey(aid))
                ba.set_attribute_value(new StringName(aid), payload[aid].AsInt32());
        if (!_apply_identity_payload_to_member(memberState, payload, progressionContentSource))
            return false;
        var at = AttributeService.ACTION_THRESHOLD_ID();
        if (payload.ContainsKey((string)at))
            ba.set_attribute_value(at, payload[(string)at].AsInt32());
        if (
            options.ContainsKey(CreationOptionBakeRerollLuck)
            && options[CreationOptionBakeRerollLuck].AsBool()
        )
        {
            var asv = new AttributeService();
            asv.setup(memberState.progression);
            var cc = new CharacterCreationService();
            if (!TryReadRerollCount(payload, out int rerollCount))
                return false;
            cc.bake_hidden_luck_at_birth(asv, rerollCount);
        }
        int con = ba.get_attribute_value(new StringName("constitution"));
        int ihp = calculate_initial_hp_max(con);
        ba.set_attribute_value(AttributeService.HP_MAX_ID(), ihp);
        memberState.current_hp = ihp;
        return true;
    }

    public static int map_reroll_count_to_hidden_luck_at_birth(int rerollCount) =>
        _map_integer_reroll_count(rerollCount);

    public bool bake_hidden_luck_at_birth(
        AttributeService attributeService,
        int rerollCount,
        StringName sourceId = default
    )
    {
        if (attributeService == null)
            return false;
        sourceId = sourceId == "" ? DefaultSourceId : sourceId;
        int targetHL = map_reroll_count_to_hidden_luck_at_birth(rerollCount);
        int currentHL = attributeService.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH());
        int delta = targetHL - currentHL;
        if (delta == 0)
            return true;
        return attributeService.apply_permanent_attribute_change(
            UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH(),
            delta,
            new Godot.Collections.Dictionary
            {
                {
                    "source_type",
                    AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION_ID()
                },
                { "source_id", sourceId },
            }
        );
    }

    private static int _map_integer_reroll_count(int rc)
    {
        if (rc <= 0)
            return HIDDEN_LUCK_AT_BIRTH_MAX;
        if (rc >= MAXIMUM_REROLL_TIER_MINIMUM)
            return HIDDEN_LUCK_AT_BIRTH_MIN;
        return 2 - rc.ToString().Length;
    }

    private static bool TryReadRerollCount(Godot.Collections.Dictionary payload, out int rerollCount)
    {
        rerollCount = 0;
        if (payload == null || !payload.ContainsKey("reroll_count"))
            return true;
        var value = payload["reroll_count"];
        if (value.VariantType != Variant.Type.Int)
        {
            return false;
        }
        rerollCount = value.AsInt32();
        return true;
    }

    private static bool _apply_identity_payload_to_member(
        PartyMemberState ms,
        Godot.Collections.Dictionary payload,
        ProgressionContentSourceRef pcs
    )
    {
        bool srf = _payload_requires_body_size_identity_source(payload);
        ms.race_id = _rpsn(payload, "race_id", ms.race_id, false);
        ms.subrace_id = _rpsn(payload, "subrace_id", ms.subrace_id, false);
        ms.age_years = _rpnni(payload, "age_years", ms.age_years);
        ms.birth_at_world_step = _rpnni(payload, "birth_at_world_step", ms.birth_at_world_step);
        ms.age_profile_id = _rpsn(payload, "age_profile_id", ms.age_profile_id, false);
        ms.natural_age_stage_id = _rpsn(
            payload,
            "natural_age_stage_id",
            ms.natural_age_stage_id,
            false
        );
        ms.effective_age_stage_id = _rpsn(
            payload,
            "effective_age_stage_id",
            ms.effective_age_stage_id,
            false
        );
        ms.effective_age_stage_source_type = _rpsn(
            payload,
            "effective_age_stage_source_type",
            ms.effective_age_stage_source_type,
            true
        );
        ms.effective_age_stage_source_id = _rpsn(
            payload,
            "effective_age_stage_source_id",
            ms.effective_age_stage_source_id,
            true
        );
        ms.versatility_pick = _rpsn(payload, "versatility_pick", ms.versatility_pick, true);
        if (
            payload.ContainsKey("active_stage_advancement_modifier_ids")
            && payload["active_stage_advancement_modifier_ids"].VariantType == Variant.Type.Array
        )
            ms.active_stage_advancement_modifier_ids = ProgressionDataUtils.to_string_name_array(
                payload["active_stage_advancement_modifier_ids"]
            );
        ms.bloodline_id = _rpsn(payload, "bloodline_id", ms.bloodline_id, true);
        ms.bloodline_stage_id = _rpsn(payload, "bloodline_stage_id", ms.bloodline_stage_id, true);
        ms.ascension_id = _rpsn(payload, "ascension_id", ms.ascension_id, true);
        ms.ascension_stage_id = _rpsn(payload, "ascension_stage_id", ms.ascension_stage_id, true);
        if (
            payload.ContainsKey("ascension_started_at_world_step")
            && payload["ascension_started_at_world_step"].VariantType == Variant.Type.Int
        )
            ms.ascension_started_at_world_step = Mathf.Max(
                payload["ascension_started_at_world_step"].AsInt32(),
                -1
            );
        ms.original_race_id_before_ascension = _rpsn(
            payload,
            "original_race_id_before_ascension",
            ms.original_race_id_before_ascension,
            true
        );
        ms.biological_age_years = _rpnni(payload, "biological_age_years", ms.biological_age_years);
        ms.astral_memory_years = _rpnni(payload, "astral_memory_years", ms.astral_memory_years);
        if (srf)
            return pcs.RefreshMemberBodySize(ms);
        return true;
    }

    private static bool _validate_payload_identity_before_mutation(
        PartyMemberState ms,
        Godot.Collections.Dictionary payload,
        ProgressionContentSourceRef pcs
    )
    {
        if (!_payload_requires_body_size_identity_source(payload))
            return true;
        if (!pcs.HasSource)
            return false;
        var c = _build_identity_candidate_from_payload(ms, payload);
        var errors = pcs.ValidateMemberIdentity(c);
        if (errors.Count > 0)
            return false;
        return pcs.ResolveBodySizeCategory(c) != "";
    }

    private static PartyMemberState _build_identity_candidate_from_payload(
        PartyMemberState ms,
        Godot.Collections.Dictionary payload
    )
    {
        var c = new PartyMemberState { member_id = ms.member_id };
        c.race_id = _rpsn(payload, "race_id", ms.race_id, false);
        c.subrace_id = _rpsn(payload, "subrace_id", ms.subrace_id, false);
        c.bloodline_id = _rpsn(payload, "bloodline_id", ms.bloodline_id, true);
        c.bloodline_stage_id = _rpsn(payload, "bloodline_stage_id", ms.bloodline_stage_id, true);
        c.ascension_id = _rpsn(payload, "ascension_id", ms.ascension_id, true);
        c.ascension_stage_id = _rpsn(payload, "ascension_stage_id", ms.ascension_stage_id, true);
        return c;
    }

    private static bool _payload_requires_body_size_identity_source(
        Godot.Collections.Dictionary payload
    )
    {
        foreach (string fn in IDENTITY_BODY_SIZE_SOURCE_FIELDS)
            if (payload.ContainsKey(fn))
                return true;
        return false;
    }

    private static StringName _rpsn(
        Godot.Collections.Dictionary payload,
        string fn,
        StringName fb,
        bool ae
    )
    {
        if (!payload.ContainsKey(fn))
            return fb;
        var v = payload[fn];
        if (v.VariantType != Variant.Type.String && v.VariantType != Variant.Type.StringName)
            return fb;
        var p = ProgressionDataUtils.to_string_name(v);
        if (p == "" && !ae)
            return fb;
        return p;
    }

    private static int _rpnni(Godot.Collections.Dictionary payload, string fn, int fb)
    {
        if (!payload.ContainsKey(fn) || payload[fn].VariantType != Variant.Type.Int)
            return fb;
        return Mathf.Max(payload[fn].AsInt32(), 0);
    }

    private readonly struct ProgressionContentSourceRef
    {
        internal static readonly ProgressionContentSourceRef Empty = new();

        private readonly GDictionary _dictionary;
        private readonly ProgressionContentRegistry _registry;

        private ProgressionContentSourceRef(GDictionary dictionary, ProgressionContentRegistry registry)
        {
            _dictionary = dictionary;
            _registry = registry;
        }

        internal bool HasSource => _dictionary != null || _registry != null;

        internal static ProgressionContentSourceRef FromDictionary(GDictionary contentSource)
        {
            return contentSource != null ? new ProgressionContentSourceRef(contentSource, null) : Empty;
        }

        internal static ProgressionContentSourceRef FromRegistry(ProgressionContentRegistry contentSource)
        {
            return contentSource != null ? new ProgressionContentSourceRef(null, contentSource) : Empty;
        }

        internal Godot.Collections.Array<string> ValidateMemberIdentity(PartyMemberState memberState)
        {
            return _registry != null
                ? IdentityPayloadValidator.validate_member_identity_for_content_source(
                    memberState,
                    _registry
                )
                : IdentityPayloadValidator.validate_member_identity(memberState, _dictionary);
        }

        internal StringName ResolveBodySizeCategory(PartyMemberState memberState)
        {
            return _registry != null
                ? IdentityPayloadValidator.resolve_body_size_category_for_content_source(
                    memberState,
                    _registry
                )
                : IdentityPayloadValidator.resolve_body_size_category_for_member(
                    memberState,
                    _dictionary
                );
        }

        internal bool RefreshMemberBodySize(PartyMemberState memberState)
        {
            return _registry != null
                ? IdentityPayloadValidator.refresh_member_body_size_from_content_source(
                    memberState,
                    _registry
                )
                : IdentityPayloadValidator.refresh_member_body_size_from_identity(
                    memberState,
                    _dictionary
                );
        }
    }
}
