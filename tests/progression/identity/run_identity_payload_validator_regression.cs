using System;
using System.Collections.Generic;
using Godot;

public partial class run_identity_payload_validator_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        PartyMemberState valid = MakeMember();
        valid.bloodline_id = "titan";
        valid.bloodline_stage_id = "titan_awakened";
        valid.ascension_id = "dragon_ascension";
        valid.ascension_stage_id = "dragon_awakened";
        AssertNoErrors(valid, MakeIdentityCatalog(), "valid identity payload should pass validation");

        PartyMemberState missingRace = MakeMember();
        missingRace.race_id = "missing_race";
        AssertOnlyError(missingRace, MakeIdentityCatalog(), "member hero references missing race missing_race");

        PartyMemberState missingSubrace = MakeMember();
        missingSubrace.subrace_id = "missing_subrace";
        AssertOnlyError(missingSubrace, MakeIdentityCatalog(), "member hero references missing subrace missing_subrace");

        AssertOnlyError(
            MakeMember(), MakeIdentityCatalog(subraceParentRaceId: "elf"),
            "member hero subrace high_human parent_race_id must be human, got elf"
        );
        AssertOnlyError(
            MakeMember(), MakeIdentityCatalog(humanSubraceIds: Array.Empty<StringName>()),
            "member hero race human must list subrace high_human in subrace_ids"
        );

        PartyMemberState halfBloodline = MakeMember();
        halfBloodline.bloodline_id = "titan";
        AssertOnlyError(
            halfBloodline, MakeIdentityCatalog(),
            "member hero bloodline_id and bloodline_stage_id must both be empty or both be set"
        );

        PartyMemberState wrongBloodlineStage = MakeMember();
        wrongBloodlineStage.bloodline_id = "titan";
        wrongBloodlineStage.bloodline_stage_id = "dragon_awakened";
        AssertOnlyError(
            wrongBloodlineStage, MakeIdentityCatalog(),
            "member hero bloodline_stage_id dragon_awakened does not belong to bloodline titan"
        );

        PartyMemberState halfAscension = MakeMember();
        halfAscension.ascension_id = "dragon_ascension";
        AssertOnlyError(
            halfAscension, MakeIdentityCatalog(),
            "member hero ascension_id and ascension_stage_id must both be empty or both be set"
        );

        PartyMemberState wrongAscensionStage = MakeMember();
        wrongAscensionStage.ascension_id = "dragon_ascension";
        wrongAscensionStage.ascension_stage_id = "elf_awakened";
        AssertOnlyError(
            wrongAscensionStage, MakeIdentityCatalog(),
            "member hero ascension_stage_id elf_awakened does not belong to ascension dragon_ascension"
        );

        PartyMemberState disallowedRace = MakeMember();
        disallowedRace.ascension_id = "dragon_ascension";
        disallowedRace.ascension_stage_id = "dragon_awakened";
        AssertOnlyError(
            disallowedRace,
            MakeIdentityCatalog(dragonAscensionAllowedRaceIds: new[] { new StringName("elf") }),
            "member hero ascension dragon_ascension does not allow race human"
        );

        PartyMemberState disallowedSubrace = MakeMember();
        disallowedSubrace.subrace_id = "low_human";
        disallowedSubrace.ascension_id = "dragon_ascension";
        disallowedSubrace.ascension_stage_id = "dragon_awakened";
        AssertOnlyError(
            disallowedSubrace, MakeIdentityCatalog(),
            "member hero ascension dragon_ascension does not allow subrace low_human"
        );

        PartyMemberState disallowedBloodline = MakeMember();
        disallowedBloodline.bloodline_id = "dragon";
        disallowedBloodline.bloodline_stage_id = "dragon_awakened";
        disallowedBloodline.ascension_id = "bloodline_locked_ascension";
        disallowedBloodline.ascension_stage_id = "bloodline_locked_awakened";
        AssertOnlyError(
            disallowedBloodline, MakeIdentityCatalog(),
            "member hero ascension bloodline_locked_ascension does not allow bloodline dragon"
        );

        PartyMemberState staleCache = MakeMember();
        staleCache.body_size = 99;
        staleCache.body_size_category = "boss";
        AssertNoErrors(staleCache, MakeIdentityCatalog(), "stale body size cache should remain repairable");

        RequestTestExit(_test.Finish("Identity payload validator regression"));
    }

    private static PartyMemberState MakeMember() =>
        new()
        {
            member_id = "hero", display_name = "Hero", race_id = "human",
            subrace_id = "high_human", body_size = 2, body_size_category = "medium",
        };

    private static ProgressionIdentityCatalogData MakeIdentityCatalog(
        StringName subraceParentRaceId = default,
        IReadOnlyList<StringName> humanSubraceIds = null,
        IReadOnlyList<StringName> dragonAscensionAllowedRaceIds = null
    )
    {
        StringName parent = subraceParentRaceId == null || subraceParentRaceId.IsEmpty
            ? new StringName("human")
            : subraceParentRaceId;
        IReadOnlyList<StringName> listedSubraces = humanSubraceIds
            ?? new[] { new StringName("high_human"), new StringName("low_human") };
        IReadOnlyList<StringName> allowedDragonRaces = dragonAscensionAllowedRaceIds
            ?? new[] { new StringName("human") };

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition>
            {
                ["human"] = Race("human", listedSubraces),
                ["elf"] = Race("elf", new[] { new StringName("moon_elf") }),
            },
            new Dictionary<StringName, SubraceDefinition>
            {
                ["high_human"] = Subrace("high_human", parent),
                ["low_human"] = Subrace("low_human", "human"),
                ["moon_elf"] = Subrace("moon_elf", "elf"),
            },
            new Dictionary<StringName, AgeProfileDefinition>(),
            new Dictionary<StringName, BloodlineDefinition>
            {
                ["titan"] = Bloodline("titan", "titan_awakened"),
                ["dragon"] = Bloodline("dragon", "dragon_awakened"),
            },
            new Dictionary<StringName, BloodlineStageDefinition>
            {
                ["titan_awakened"] = BloodlineStage("titan_awakened", "titan"),
                ["dragon_awakened"] = BloodlineStage("dragon_awakened", "dragon"),
            },
            new Dictionary<StringName, AscensionDefinition>
            {
                ["dragon_ascension"] = Ascension("dragon_ascension", "dragon_awakened", allowedDragonRaces, new[] { new StringName("high_human") }, Array.Empty<StringName>()),
                ["elf_ascension"] = Ascension("elf_ascension", "elf_awakened", new[] { new StringName("elf") }, new[] { new StringName("moon_elf") }, Array.Empty<StringName>()),
                ["bloodline_locked_ascension"] = Ascension("bloodline_locked_ascension", "bloodline_locked_awakened", Array.Empty<StringName>(), Array.Empty<StringName>(), new[] { new StringName("titan") }),
            },
            new Dictionary<StringName, AscensionStageDefinition>
            {
                ["dragon_awakened"] = AscensionStage("dragon_awakened", "dragon_ascension", "large"),
                ["elf_awakened"] = AscensionStage("elf_awakened", "elf_ascension", ""),
                ["bloodline_locked_awakened"] = AscensionStage("bloodline_locked_awakened", "bloodline_locked_ascension", ""),
            },
            new Dictionary<StringName, StageAdvancementDefinition>()
        );
    }

    private static RaceDefinition Race(StringName id, IReadOnlyList<StringName> subraces) =>
        new(id, id.ToString(), "Fixture race.", "", subraces.Count > 0 ? subraces[0] : "", subraces, "medium", 6,
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), new Dictionary<StringName, StringName>(), Array.Empty<StringName>(), Array.Empty<string>());

    private static SubraceDefinition Subrace(StringName id, StringName parent) =>
        new(id, parent, id.ToString(), "Fixture subrace.", "", 0, Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(), Array.Empty<StringName>(), Array.Empty<string>());

    private static BloodlineDefinition Bloodline(StringName id, StringName stage) =>
        new(id, id.ToString(), "Fixture bloodline.", new[] { stage }, Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<AttributeModifierDefinition>(), Array.Empty<string>());

    private static BloodlineStageDefinition BloodlineStage(StringName id, StringName owner) =>
        new(id, owner, id.ToString(), "Fixture bloodline stage.", Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<string>());

    private static AscensionDefinition Ascension(
        StringName id, StringName stage, IReadOnlyList<StringName> races,
        IReadOnlyList<StringName> subraces, IReadOnlyList<StringName> bloodlines
    ) =>
        new(id, id.ToString(), "Fixture ascension.", new[] { stage }, Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), races, subraces, bloodlines, Array.Empty<string>(), false, false);

    private static AscensionStageDefinition AscensionStage(StringName id, StringName owner, StringName size) =>
        new(id, owner, id.ToString(), "Fixture ascension stage.", Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(), size, Array.Empty<string>());

    private void AssertOnlyError(PartyMemberState member, ProgressionIdentityCatalogData catalog, string expected)
    {
        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(member, catalog);
        _test.True(errors.Count == 1 && errors[0] == expected, $"expected one exact identity error '{expected}', got: {string.Join(" | ", errors)}");
    }

    private void AssertNoErrors(PartyMemberState member, ProgressionIdentityCatalogData catalog, string message)
    {
        IReadOnlyList<string> errors = IdentityPayloadValidator.ValidateMemberIdentityTyped(member, catalog);
        _test.True(errors.Count == 0, $"{message}: {string.Join(" | ", errors)}");
    }
}
