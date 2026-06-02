using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_identity_payload_validator_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestValidatorNoLongerRequiresGodotRegistration();
        TestValidIdentityPasses();
        TestRegistrySourcePasses();
        TestRejectsMissingRace();
        TestRejectsMissingSubrace();
        TestRejectsSubraceParentMismatch();
        TestRejectsRaceThatDoesNotListSubrace();
        TestRejectsHalfSetBloodlinePair();
        TestRejectsBloodlineStageThatDoesNotBelong();
        TestRejectsHalfSetAscensionPair();
        TestRejectsAscensionStageThatDoesNotBelong();
        TestRejectsAscensionDisallowedRace();
        TestRejectsAscensionDisallowedSubrace();
        TestRejectsAscensionDisallowedBloodline();
        TestBodySizeCacheMismatchIsNotIdentityError();

        if (_failures.Count == 0)
        {
            GD.Print("Identity payload validator regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Identity payload validator regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestValidatorNoLongerRequiresGodotRegistration()
    {
        Type validatorType = typeof(IdentityPayloadValidator);
        AssertTrue(
            validatorType.IsAbstract && validatorType.IsSealed,
            "IdentityPayloadValidator 应是 static helper。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(validatorType),
            "IdentityPayloadValidator 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            validatorType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "IdentityPayloadValidator 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void TestValidIdentityPasses()
    {
        PartyMemberState member = MakeMember();
        member.bloodline_id = "titan";
        member.bloodline_stage_id = "titan_awakened";
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertTrue(errors.Count == 0, "valid identity payload should pass validation");
    }

    private void TestRegistrySourcePasses()
    {
        PartyMemberState member = MakeMember();
        GDictionary bundle = MakeIdentityBundle();
        ProgressionContentRegistry registry = MakeRegistry(bundle);

        IReadOnlyList<string> errors =
            IdentityPayloadValidator.ValidateMemberIdentityForContentSource(member, registry);

        AssertTrue(errors.Count == 0, "registry content source should validate the same typed identity data");
        registry.Dispose();
    }

    private void TestRejectsMissingRace()
    {
        PartyMemberState member = MakeMember();
        member.race_id = "missing_race";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(errors, "references missing race missing_race", "missing race should be rejected");
    }

    private void TestRejectsMissingSubrace()
    {
        PartyMemberState member = MakeMember();
        member.subrace_id = "missing_subrace";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "references missing subrace missing_subrace",
            "missing subrace should be rejected"
        );
    }

    private void TestRejectsSubraceParentMismatch()
    {
        PartyMemberState member = MakeMember();
        GDictionary bundle = MakeIdentityBundle();
        ReadObject<SubraceDef>(ReadDictionary(bundle, "subrace_defs"), "high_human").parent_race_id =
            "elf";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(member, bundle);
        AssertHasError(
            errors,
            "subrace high_human parent_race_id must be human, got elf",
            "subrace parent mismatch should be rejected"
        );
    }

    private void TestRejectsRaceThatDoesNotListSubrace()
    {
        PartyMemberState member = MakeMember();
        GDictionary bundle = MakeIdentityBundle();
        ReadObject<RaceDef>(ReadDictionary(bundle, "race_defs"), "human").subrace_ids =
            MakeStringNames(Array.Empty<StringName>());

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(member, bundle);
        AssertHasError(
            errors,
            "race human must list subrace high_human in subrace_ids",
            "race missing selected subrace should be rejected"
        );
    }

    private void TestRejectsHalfSetBloodlinePair()
    {
        PartyMemberState member = MakeMember();
        member.bloodline_id = "titan";
        member.bloodline_stage_id = "";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "bloodline_id and bloodline_stage_id must both be empty or both be set",
            "half-set bloodline pair should be rejected"
        );
    }

    private void TestRejectsBloodlineStageThatDoesNotBelong()
    {
        PartyMemberState member = MakeMember();
        member.bloodline_id = "titan";
        member.bloodline_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "bloodline_stage_id dragon_awakened does not belong to bloodline titan",
            "bloodline stage from another bloodline should be rejected"
        );
    }

    private void TestRejectsHalfSetAscensionPair()
    {
        PartyMemberState member = MakeMember();
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "ascension_id and ascension_stage_id must both be empty or both be set",
            "half-set ascension pair should be rejected"
        );
    }

    private void TestRejectsAscensionStageThatDoesNotBelong()
    {
        PartyMemberState member = MakeMember();
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "elf_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "ascension_stage_id elf_awakened does not belong to ascension dragon_ascension",
            "ascension stage from another ascension should be rejected"
        );
    }

    private void TestRejectsAscensionDisallowedRace()
    {
        PartyMemberState member = MakeMember();
        member.race_id = "elf";
        member.subrace_id = "moon_elf";
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "ascension dragon_ascension does not allow race elf",
            "ascension allowed race gate should be enforced"
        );
    }

    private void TestRejectsAscensionDisallowedSubrace()
    {
        PartyMemberState member = MakeMember();
        member.subrace_id = "low_human";
        member.ascension_id = "dragon_ascension";
        member.ascension_stage_id = "dragon_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "ascension dragon_ascension does not allow subrace low_human",
            "ascension allowed subrace gate should be enforced"
        );
    }

    private void TestRejectsAscensionDisallowedBloodline()
    {
        PartyMemberState member = MakeMember();
        member.ascension_id = "bloodline_locked_ascension";
        member.ascension_stage_id = "bloodline_locked_awakened";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertHasError(
            errors,
            "ascension bloodline_locked_ascension does not allow bloodline",
            "ascension allowed bloodline gate should be enforced"
        );
    }

    private void TestBodySizeCacheMismatchIsNotIdentityError()
    {
        PartyMemberState member = MakeMember();
        member.body_size = 99;
        member.body_size_category = "boss";

        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentity(
            member,
            MakeIdentityBundle()
        );
        AssertTrue(
            errors.Count == 0,
            "stale body size cache should be repairable data, not identity-invalid data"
        );
    }

    private static PartyMemberState MakeMember()
    {
        return new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            race_id = "human",
            subrace_id = "high_human",
            body_size = 2,
            body_size_category = "medium",
        };
    }

    private static GDictionary MakeIdentityBundle()
    {
        return new GDictionary
        {
            ["race_defs"] = new GDictionary
            {
                ["human"] = MakeRace(
                    "human",
                    new[] { new StringName("high_human"), new StringName("low_human") },
                    "medium"
                ),
                ["elf"] = MakeRace(
                    "elf",
                    new[] { new StringName("moon_elf") },
                    "medium"
                ),
            },
            ["subrace_defs"] = new GDictionary
            {
                ["high_human"] = MakeSubrace("high_human", "human", ""),
                ["low_human"] = MakeSubrace("low_human", "human", ""),
                ["moon_elf"] = MakeSubrace("moon_elf", "elf", ""),
            },
            ["bloodline_defs"] = new GDictionary
            {
                ["titan"] = MakeBloodline(
                    "titan",
                    new[] { new StringName("titan_awakened") }
                ),
                ["dragon"] = MakeBloodline(
                    "dragon",
                    new[] { new StringName("dragon_awakened") }
                ),
            },
            ["bloodline_stage_defs"] = new GDictionary
            {
                ["titan_awakened"] = MakeBloodlineStage("titan_awakened", "titan"),
                ["dragon_awakened"] = MakeBloodlineStage("dragon_awakened", "dragon"),
            },
            ["ascension_defs"] = new GDictionary
            {
                ["dragon_ascension"] = MakeAscension(
                    "dragon_ascension",
                    new[] { new StringName("dragon_awakened") },
                    new[] { new StringName("human") },
                    new[] { new StringName("high_human") },
                    Array.Empty<StringName>()
                ),
                ["elf_ascension"] = MakeAscension(
                    "elf_ascension",
                    new[] { new StringName("elf_awakened") },
                    new[] { new StringName("elf") },
                    new[] { new StringName("moon_elf") },
                    Array.Empty<StringName>()
                ),
                ["bloodline_locked_ascension"] = MakeAscension(
                    "bloodline_locked_ascension",
                    new[] { new StringName("bloodline_locked_awakened") },
                    Array.Empty<StringName>(),
                    Array.Empty<StringName>(),
                    new[] { new StringName("titan") }
                ),
            },
            ["ascension_stage_defs"] = new GDictionary
            {
                ["dragon_awakened"] = MakeAscensionStage(
                    "dragon_awakened",
                    "dragon_ascension",
                    "large"
                ),
                ["elf_awakened"] = MakeAscensionStage("elf_awakened", "elf_ascension", ""),
                ["bloodline_locked_awakened"] = MakeAscensionStage(
                    "bloodline_locked_awakened",
                    "bloodline_locked_ascension",
                    ""
                ),
            },
        };
    }

    private static RaceDef MakeRace(
        StringName id,
        IEnumerable<StringName> subraceIds,
        StringName bodySizeCategory
    )
    {
        RaceDef race = new()
        {
            race_id = id,
            body_size_category = bodySizeCategory,
        };
        AddStringNames(race.subrace_ids, subraceIds);
        return race;
    }

    private static SubraceDef MakeSubrace(
        StringName id,
        StringName parentRaceId,
        StringName bodySizeCategory
    )
    {
        return new SubraceDef
        {
            subrace_id = id,
            parent_race_id = parentRaceId,
            body_size_category_override = bodySizeCategory,
        };
    }

    private static BloodlineDef MakeBloodline(StringName id, IEnumerable<StringName> stageIds)
    {
        BloodlineDef bloodline = new()
        {
            bloodline_id = id,
        };
        AddStringNames(bloodline.stage_ids, stageIds);
        return bloodline;
    }

    private static BloodlineStageDef MakeBloodlineStage(
        StringName id,
        StringName bloodlineId
    )
    {
        return new BloodlineStageDef
        {
            stage_id = id,
            bloodline_id = bloodlineId,
        };
    }

    private static AscensionDef MakeAscension(
        StringName id,
        IEnumerable<StringName> stageIds,
        IEnumerable<StringName> allowedRaceIds,
        IEnumerable<StringName> allowedSubraceIds,
        IEnumerable<StringName> allowedBloodlineIds
    )
    {
        AscensionDef ascension = new()
        {
            ascension_id = id,
        };
        AddStringNames(ascension.stage_ids, stageIds);
        AddStringNames(ascension.allowed_race_ids, allowedRaceIds);
        AddStringNames(ascension.allowed_subrace_ids, allowedSubraceIds);
        AddStringNames(ascension.allowed_bloodline_ids, allowedBloodlineIds);
        return ascension;
    }

    private static AscensionStageDef MakeAscensionStage(
        StringName id,
        StringName ascensionId,
        StringName bodySizeCategory
    )
    {
        return new AscensionStageDef
        {
            stage_id = id,
            ascension_id = ascensionId,
            body_size_category_override = bodySizeCategory,
        };
    }

    private static GStringNameArray MakeStringNames(IEnumerable<StringName> values)
    {
        GStringNameArray result = new();
        AddStringNames(result, values);
        return result;
    }

    private static void AddStringNames(GStringNameArray target, IEnumerable<StringName> values)
    {
        foreach (StringName value in values)
            target.Add(value);
    }

    private static ProgressionContentRegistry MakeRegistry(GDictionary bundle)
    {
        ProgressionContentRegistry registry = new();
        registry._race_defs = ReadDictionary(bundle, "race_defs");
        registry._subrace_defs = ReadDictionary(bundle, "subrace_defs");
        registry._bloodline_defs = ReadDictionary(bundle, "bloodline_defs");
        registry._bloodline_stage_defs = ReadDictionary(bundle, "bloodline_stage_defs");
        registry._ascension_defs = ReadDictionary(bundle, "ascension_defs");
        registry._ascension_stage_defs = ReadDictionary(bundle, "ascension_stage_defs");
        return registry;
    }

    private static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return new GDictionary();
        Variant value = source[key];
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static T ReadObject<T>(GDictionary source, string key)
        where T : class
    {
        if (source == null || !source.ContainsKey(key))
            return null;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
    }

    private void AssertHasError(
        IReadOnlyList<string> errors,
        string fragment,
        string message
    )
    {
        foreach (string error in errors)
        {
            if (error.Contains(fragment, StringComparison.Ordinal))
            {
                AssertTrue(true, message);
                return;
            }
        }
        AssertTrue(false, $"{message}; got errors: {string.Join(", ", errors)}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }
}
