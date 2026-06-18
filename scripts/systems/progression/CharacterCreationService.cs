using System.Collections.Generic;
using Godot;

public sealed class CharacterCreationOptions
{
    public static readonly CharacterCreationOptions Default = new();

    public bool BakeRerollLuck { get; }

    public CharacterCreationOptions(bool bakeRerollLuck = false)
    {
        BakeRerollLuck = bakeRerollLuck;
    }
}

public sealed class CharacterCreationService
{
    private const int HIDDEN_LUCK_AT_BIRTH_MAX = 2,
        HIDDEN_LUCK_AT_BIRTH_MIN = -6,
        INITIAL_HP_BASE = 14,
        MAXIMUM_REROLL_TIER_MINIMUM = 10_000_000;
    internal static readonly StringName DefaultSourceId = "birth_roll";
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

    public static int CalculateInitialHpMax(int constitutionValue) =>
        Mathf.Max(
            1,
            INITIAL_HP_BASE
                + ProgressionService.CalculateConstitutionModifier(constitutionValue) * 2
        );

    public static PartyMemberState CreateMemberFromCharacterCreationPayloadWithoutContentSource(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        CharacterCreationOptions options = null
    ) => CreateMemberFromCharacterCreationPayload(
        memberId,
        payload,
        ProgressionContentSourceRef.Empty,
        options
    );

    public static PartyMemberState CreateMemberFromCharacterCreationPayloadForContentSource(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        ProgressionContentRegistry progressionContentSource,
        CharacterCreationOptions options = null
    ) => CreateMemberFromCharacterCreationPayload(
        memberId,
        payload,
        ProgressionContentSourceRef.FromRegistry(progressionContentSource),
        options
    );

    internal static PartyMemberState CreateMemberFromCharacterCreationPayloadForIdentityCatalog(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        ProgressionIdentityCatalogData progressionContentSource,
        CharacterCreationOptions options = null
    ) => CreateMemberFromCharacterCreationPayload(
        memberId,
        payload,
        ProgressionContentSourceRef.FromIdentityCatalog(progressionContentSource),
        options
    );

    private static PartyMemberState CreateMemberFromCharacterCreationPayload(
        StringName memberId,
        Godot.Collections.Dictionary payload,
        ProgressionContentSourceRef progressionContentSource,
        CharacterCreationOptions options = null
    )
    {
        options ??= CharacterCreationOptions.Default;
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

    public static bool ApplyCharacterCreationPayloadToMemberWithoutContentSource(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        CharacterCreationOptions options = null
    ) => ApplyCharacterCreationPayloadToMember(
        memberState,
        payload,
        ProgressionContentSourceRef.Empty,
        options
    );

    public static bool ApplyCharacterCreationPayloadToMemberForContentSource(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        ProgressionContentRegistry progressionContentSource,
        CharacterCreationOptions options = null
    ) => ApplyCharacterCreationPayloadToMember(
        memberState,
        payload,
        ProgressionContentSourceRef.FromRegistry(progressionContentSource),
        options
    );

    internal static bool ApplyCharacterCreationPayloadToMemberForIdentityCatalog(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        ProgressionIdentityCatalogData progressionContentSource,
        CharacterCreationOptions options = null
    ) => ApplyCharacterCreationPayloadToMember(
        memberState,
        payload,
        ProgressionContentSourceRef.FromIdentityCatalog(progressionContentSource),
        options
    );

    private static bool ApplyCharacterCreationPayloadToMember(
        PartyMemberState memberState,
        Godot.Collections.Dictionary payload,
        ProgressionContentSourceRef progressionContentSource,
        CharacterCreationOptions options = null
    )
    {
        options ??= CharacterCreationOptions.Default;
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
                ba.SetAttributeValue(new StringName(aid), payload[aid].AsInt32());
        if (!_apply_identity_payload_to_member(memberState, payload, progressionContentSource))
            return false;
        var at = AttributeService.ToStringName(AttributeIdKind.ActionThreshold);
        if (payload.ContainsKey((string)at))
            ba.SetAttributeValue(at, payload[(string)at].AsInt32());
        if (options.BakeRerollLuck)
        {
            var asv = new AttributeService();
            asv.Setup(memberState.progression);
            var cc = new CharacterCreationService();
            if (!TryReadRerollCount(payload, out int rerollCount))
                return false;
            cc.BakeHiddenLuckAtBirth(asv, rerollCount);
        }
        int con = ba.GetAttributeValue(new StringName("constitution"));
        int ihp = CalculateInitialHpMax(con);
        ba.SetAttributeValue(AttributeService.ToStringName(AttributeIdKind.HpMax), ihp);
        memberState.SetCurrentHp(ihp);
        return true;
    }

    public static int MapRerollCountToHiddenLuckAtBirth(int rerollCount) =>
        _map_integer_reroll_count(rerollCount);

    public bool BakeHiddenLuckAtBirth(
        AttributeService attributeService,
        int rerollCount,
        StringName sourceId = default
    )
    {
        if (attributeService == null)
            return false;
        sourceId = sourceId == "" ? DefaultSourceId : sourceId;
        int targetHL = MapRerollCountToHiddenLuckAtBirth(rerollCount);
        int currentHL = attributeService.GetBaseValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth));
        int delta = targetHL - currentHL;
        if (delta == 0)
            return true;
        return attributeService.ApplyPermanentAttributeChange(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth),
            delta,
            new Godot.Collections.Dictionary
            {
                {
                    "source_type",
                    AttributeService.ToStringName(AttributeSourceKind.CharacterCreation)
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
        ms.SetIdentity(
            _rpsn(payload, "race_id", ms.race_id, false),
            _rpsn(payload, "subrace_id", ms.subrace_id, false)
        );
        ms.SetAgeProjection(
            _rpnni(payload, "age_years", ms.age_years),
            ms.biological_age_years,
            ms.astral_memory_years,
            _rpnni(payload, "birth_at_world_step", ms.birth_at_world_step)
        );
        ms.SetAgeStageProjection(
            _rpsn(payload, "age_profile_id", ms.age_profile_id, false),
            _rpsn(payload, "natural_age_stage_id", ms.natural_age_stage_id, false),
            _rpsn(payload, "effective_age_stage_id", ms.effective_age_stage_id, false),
            _rpsn(
                payload,
                "effective_age_stage_source_type",
                ms.effective_age_stage_source_type,
                true
            ),
            _rpsn(
                payload,
                "effective_age_stage_source_id",
                ms.effective_age_stage_source_id,
                true
            )
        );
        ms.SetVersatilityPick(_rpsn(payload, "versatility_pick", ms.versatility_pick, true));
        if (
            payload.ContainsKey("active_stage_advancement_modifier_ids")
            && payload["active_stage_advancement_modifier_ids"].VariantType == Variant.Type.Array
        )
            ms.SetActiveStageAdvancementModifierIds(
                ProgressionDataUtils.to_string_name_array(
                    payload["active_stage_advancement_modifier_ids"]
                )
            );
        if (!ms.SetBloodline(
            _rpsn(payload, "bloodline_id", ms.bloodline_id, true),
            _rpsn(payload, "bloodline_stage_id", ms.bloodline_stage_id, true)
        ))
            return false;
        StringName ascensionId = _rpsn(payload, "ascension_id", ms.ascension_id, true);
        StringName ascensionStageId = _rpsn(payload, "ascension_stage_id", ms.ascension_stage_id, true);
        int ascensionStartedAtWorldStep =
            payload.ContainsKey("ascension_started_at_world_step")
            && payload["ascension_started_at_world_step"].VariantType == Variant.Type.Int
                ? Mathf.Max(
                payload["ascension_started_at_world_step"].AsInt32(),
                -1
            )
                : ms.ascension_started_at_world_step;
        StringName originalRaceIdBeforeAscension = _rpsn(
            payload,
            "original_race_id_before_ascension",
            ms.original_race_id_before_ascension,
            true
        );
        if (!ms.SetAscension(
            ascensionId,
            ascensionStageId,
            ascensionStartedAtWorldStep,
            originalRaceIdBeforeAscension
        ))
            return false;
        ms.SetAgeProjection(
            ms.age_years,
            _rpnni(payload, "biological_age_years", ms.biological_age_years),
            _rpnni(payload, "astral_memory_years", ms.astral_memory_years),
            ms.birth_at_world_step
        );
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
        if (PayloadHasInvalidIdentityPairs(ms, payload))
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
        c.SetIdentity(
            _rpsn(payload, "race_id", ms.race_id, false),
            _rpsn(payload, "subrace_id", ms.subrace_id, false)
        );
        c.SetBloodline(
            _rpsn(payload, "bloodline_id", ms.bloodline_id, true),
            _rpsn(payload, "bloodline_stage_id", ms.bloodline_stage_id, true)
        );
        c.SetAscension(
            _rpsn(payload, "ascension_id", ms.ascension_id, true),
            _rpsn(payload, "ascension_stage_id", ms.ascension_stage_id, true),
            ms.ascension_started_at_world_step,
            ms.original_race_id_before_ascension
        );
        return c;
    }

    private static bool PayloadHasInvalidIdentityPairs(
        PartyMemberState ms,
        Godot.Collections.Dictionary payload
    )
    {
        StringName bloodlineId = _rpsn(payload, "bloodline_id", ms.bloodline_id, true);
        StringName bloodlineStageId = _rpsn(
            payload,
            "bloodline_stage_id",
            ms.bloodline_stage_id,
            true
        );
        if ((bloodlineId == "") != (bloodlineStageId == ""))
            return true;

        StringName ascensionId = _rpsn(payload, "ascension_id", ms.ascension_id, true);
        StringName ascensionStageId = _rpsn(
            payload,
            "ascension_stage_id",
            ms.ascension_stage_id,
            true
        );
        return (ascensionId == "") != (ascensionStageId == "");
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

        private readonly ProgressionIdentityCatalogData _identityCatalog;

        private ProgressionContentSourceRef(ProgressionIdentityCatalogData identityCatalog)
        {
            _identityCatalog = identityCatalog;
        }

        internal bool HasSource => _identityCatalog != null;

        internal static ProgressionContentSourceRef FromRegistry(ProgressionContentRegistry contentSource)
        {
            return contentSource != null
                ? new ProgressionContentSourceRef(contentSource.GetIdentityCatalogTyped())
                : Empty;
        }

        internal static ProgressionContentSourceRef FromIdentityCatalog(
            ProgressionIdentityCatalogData contentSource
        )
        {
            return contentSource != null ? new ProgressionContentSourceRef(contentSource) : Empty;
        }

        internal IReadOnlyList<string> ValidateMemberIdentity(PartyMemberState memberState)
        {
            return IdentityPayloadValidator.ValidateMemberIdentityTyped(
                memberState,
                _identityCatalog
            );
        }

        internal StringName ResolveBodySizeCategory(PartyMemberState memberState)
        {
            return IdentityPayloadValidator.ResolveBodySizeCategoryForMemberTyped(
                memberState,
                _identityCatalog
            );
        }

        internal bool RefreshMemberBodySize(PartyMemberState memberState)
        {
            return IdentityPayloadValidator.RefreshMemberBodySizeFromIdentityTyped(
                memberState,
                _identityCatalog
            );
        }
    }
}
