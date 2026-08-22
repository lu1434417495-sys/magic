using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_character_creation_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestIdentityOptionServiceCollectsOnlyLegalRaceSubracePairs();
        TestIdentityOptionServiceChoosesStableDefaults();
        TestRerollMappingCoversAllBandBoundaries();
        TestInitialHpMaxUsesLevelZeroFormula();
        TestBakeHiddenLuckUsesCharacterCreationWritePath();
        TestCreationPayloadRejectsIdentityBodySizeWithoutContentSource();
        TestCreationPayloadRejectsInvalidAscensionPairWithoutMutatingIdentity();
        TestCreationPayloadRejectsInvalidBloodlinePairWithoutMutatingIdentity();
        TestCreationPayloadRejectsAscensionAllowedIdentityWithoutMutatingIdentity();
        TestCreationPayloadRejectsInvalidRaceSubracePairWithoutMutatingMember();
        TestCreationPayloadDerivesBodySizeFromIdentityContentSource();
        TestCreationPayloadDoesNotBakeRerollLuckByDefault();
        TestCreationPayloadCanOptIntoRerollLuckForMainCharacter();
        TestCreationPayloadRejectsNonIntegerRerollLuckWhenOptedIn();

        RequestTestExit(_test.Finish("CharacterCreationService regression"));
    }

    private void TestIdentityOptionServiceCollectsOnlyLegalRaceSubracePairs()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityOptionCatalog();

        IReadOnlyList<StringName> humanSubraces =
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(catalog, "human");
        AssertIdsEq(
            humanSubraces,
            new[] { new StringName("common_human"), new StringName("noble_human") },
            "建卡 subrace 候选应过滤 missing/wrong-parent，并保持稳定字典序。"
        );
        _test.False(
            ContainsId(humanSubraces, "parent_only_human"),
            "parent_race_id 指向 human 但未被 race.subrace_ids 列出的 subrace 不得进入候选。"
        );

        IReadOnlyList<StringName> raceIds =
            CharacterCreationIdentityOptionService.CollectCreationRaceIds(catalog);
        _test.True(ContainsId(raceIds, "human"), "human 有合法 subrace，应进入 race 候选。");
        _test.True(
            ContainsId(raceIds, "invalid_default_race"),
            "default 非法但存在合法 subrace 的 race 仍应进入候选。"
        );
        _test.False(
            ContainsId(raceIds, "orphan_race"),
            "无合法 subrace 的 race 不应进入建卡 race 候选。"
        );

        _test.True(
            CharacterCreationIdentityOptionService.IsValidCreationRaceSubracePair(
                catalog,
                "human",
                "common_human"
            ),
            "合法 race/subrace pair 应通过。"
        );
        _test.False(
            CharacterCreationIdentityOptionService.IsValidCreationRaceSubracePair(
                catalog,
                "human",
                "wrong_parent"
            ),
            "parent mismatch subrace 不得作为合法 pair。"
        );
    }

    private void TestIdentityOptionServiceChoosesStableDefaults()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityOptionCatalog();

        _test.Eq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(catalog, "human", ""),
            new StringName("common_human"),
            "合法 default_subrace_id 应优先成为选择。"
        );
        _test.Eq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(
                catalog,
                "human",
                "noble_human"
            ),
            new StringName("noble_human"),
            "当前选择仍合法时应保留。"
        );
        _test.Eq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(
                catalog,
                "human",
                "parent_only_human"
            ),
            new StringName("common_human"),
            "stale parent-only subrace 必须被纠正为合法候选。"
        );
        _test.Eq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(
                catalog,
                "invalid_default_race",
                ""
            ),
            new StringName("valid_for_invalid_default"),
            "default_subrace_id 非法时应选择第一个合法候选。"
        );

        IReadOnlyList<StringName> orphanSubraces =
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(catalog, "orphan_race");
        _test.Eq(orphanSubraces.Count, 0, "无合法 subrace 的 race 应返回空候选，不扫描 parent_race fallback。");
        _test.Eq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(catalog, "orphan_race", ""),
            new StringName(""),
            "无合法 subrace 的 race 不应产生默认选择。"
        );
    }

    private void TestRerollMappingCoversAllBandBoundaries()
    {
        foreach ((string label, int rerollCount, int expectedHiddenLuck) in new[]
        {
            ("0 次 reroll", 0, 2),
            ("1 次 reroll", 1, 1),
            ("9 次 reroll", 9, 1),
            ("10 次 reroll", 10, 0),
            ("99 次 reroll", 99, 0),
            ("100 次 reroll", 100, -1),
            ("999 次 reroll", 999, -1),
            ("1,000 次 reroll", 1000, -2),
            ("9,999 次 reroll", 9999, -2),
            ("10,000 次 reroll", 10000, -3),
            ("99,999 次 reroll", 99999, -3),
            ("100,000 次 reroll", 100000, -4),
            ("999,999 次 reroll", 999999, -4),
            ("1,000,000 次 reroll", 1000000, -5),
            ("9,999,999 次 reroll", 9999999, -5),
            ("10,000,000 次 reroll", 10000000, -6),
            ("10,000,001 次 reroll", 10000001, -6),
        })
        {
            _test.Eq(
                CharacterCreationService.MapRerollCountToHiddenLuckAtBirth(rerollCount),
                expectedHiddenLuck,
                $"{label} 映射结果错误。"
            );
        }
    }

    private void TestInitialHpMaxUsesLevelZeroFormula()
    {
        _test.Eq(
            CharacterCreationService.CalculateInitialHpMax(10),
            14,
            "10 体质的 0 级初始生命应为 14。"
        );
        _test.Eq(
            CharacterCreationService.CalculateInitialHpMax(14),
            18,
            "14 体质的 0 级初始生命应为 14 + 2*2。"
        );
        _test.Eq(
            CharacterCreationService.CalculateInitialHpMax(8),
            12,
            "8 体质的 0 级初始生命应为 14 - 1*2。"
        );
    }

    private void TestBakeHiddenLuckUsesCharacterCreationWritePath()
    {
        UnitProgress progression = new()
        {
            unit_id = "hero",
            display_name = "Hero",
        };
        progression.unit_base_attributes.SetAttributeValue("hidden_luck_at_birth", 1);

        AttributeService attributeService = new();
        attributeService.Setup(progression);

        CharacterCreationService creationService = new();
        bool baked = creationService.BakeHiddenLuckAtBirth(
            attributeService,
            10000,
            "birth_roll"
        );

        _test.True(
            baked,
            "CharacterCreationService 应能通过 character_creation 来源写入 hidden_luck_at_birth。"
        );
        _test.Eq(
            attributeService.GetBaseValue("hidden_luck_at_birth"),
            -3,
            "CharacterCreationService 应把 reroll=10000 烘焙为 -3。"
        );
    }

    private void TestCreationPayloadRejectsIdentityBodySizeWithoutContentSource()
    {
        GDictionary payload = BuildCreationPayload(0);
        payload["body_size"] = 99;
        payload["body_size_category"] = "boss";

        PartyMemberState memberState =
            CharacterCreationService.CreateMemberFromCharacterCreationPayloadWithoutContentSource(
                "bad_body",
                payload
            );

        _test.True(
            memberState == null,
            "建卡 payload 携带身份/体型字段时必须传内容源，不能保留 payload body_size/body_size_category。"
        );
    }

    private void TestCreationPayloadDerivesBodySizeFromIdentityContentSource()
    {
        GDictionary payload = BuildCreationPayload(0);
        payload["body_size"] = 99;
        payload["body_size_category"] = "boss";
        payload["ascension_id"] = "titan";
        payload["ascension_stage_id"] = "titan_avatar";

        PartyMemberState memberState =
            CharacterCreationService.CreateMemberFromCharacterCreationPayloadForIdentityCatalog(
                "derived_body",
                payload,
                MakeCreationContentSource()
            );

        _test.True(memberState != null, "建卡 payload 有内容源时应能创建角色。");
        if (memberState == null)
            return;
        _test.Eq(
            memberState.body_size_category,
            new StringName("huge"),
            "建卡体型分类应按 ascension stage > subrace > race 派生，不能采用 payload body_size_category。"
        );
        _test.Eq(
            memberState.body_size,
            BodySizeForCategory("huge"),
            "建卡 body_size 应由 body_size_category 映射得到，不能采用 payload body_size。"
        );
    }

    private void TestCreationPayloadRejectsInvalidAscensionPairWithoutMutatingIdentity()
    {
        PartyMemberState memberState = MakeExistingMemberState();
        string beforeIdentity = FormatIdentityBody(memberState);
        GDictionary payload = BuildCreationPayload(0);
        payload["body_size"] = 99;
        payload["body_size_category"] = "boss";
        payload["ascension_id"] = "";
        payload["ascension_stage_id"] = "titan_avatar";

        bool applied = CharacterCreationService.ApplyCharacterCreationPayloadToMemberForIdentityCatalog(
            memberState,
            payload,
            MakeCreationContentSource()
        );

        _test.False(
            applied,
            "建卡替换路径应拒绝半设置 ascension/stage，不应把 stage 当成隐式 ascension。"
        );
        _test.Eq(
            FormatIdentityBody(memberState),
            beforeIdentity,
            "非法 ascension payload 不应污染成员身份或派生体型。"
        );
    }

    private void TestCreationPayloadRejectsInvalidBloodlinePairWithoutMutatingIdentity()
    {
        PartyMemberState memberState = MakeExistingMemberState();
        string beforeIdentity = FormatIdentityBody(memberState);
        GDictionary payload = BuildCreationPayload(0);
        payload["bloodline_id"] = "titan";
        payload["bloodline_stage_id"] = "dragon_awakened";

        bool applied = CharacterCreationService.ApplyCharacterCreationPayloadToMemberForIdentityCatalog(
            memberState,
            payload,
            MakeCreationContentSource()
        );

        _test.False(applied, "建卡替换路径应拒绝不属于该 bloodline 的 stage。");
        _test.Eq(
            FormatIdentityBody(memberState),
            beforeIdentity,
            "非法 bloodline payload 不应污染成员身份或派生体型。"
        );
    }

    private void TestCreationPayloadRejectsAscensionAllowedIdentityWithoutMutatingIdentity()
    {
        PartyMemberState memberState = MakeExistingMemberState();
        string beforeIdentity = FormatIdentityBody(memberState);
        GDictionary payload = BuildCreationPayload(0);
        payload["ascension_id"] = "bloodline_locked_ascension";
        payload["ascension_stage_id"] = "bloodline_locked_awakened";

        bool applied = CharacterCreationService.ApplyCharacterCreationPayloadToMemberForIdentityCatalog(
            memberState,
            payload,
            MakeCreationContentSource()
        );

        _test.False(applied, "建卡替换路径应拒绝不满足 allowed_bloodline_ids 的 ascension。");
        _test.Eq(
            FormatIdentityBody(memberState),
            beforeIdentity,
            "非法 ascension allowed gate 不应污染成员身份或派生体型。"
        );
    }

    private void TestCreationPayloadRejectsInvalidRaceSubracePairWithoutMutatingMember()
    {
        PartyMemberState memberState = MakeExistingMemberState();
        string beforeSurface = FormatCreationSurface(memberState);
        GDictionary payload = BuildCreationPayload(0);
        payload["display_name"] = "Should Not Apply";
        payload["subrace_id"] = "wrong_parent";
        payload["body_size"] = 99;
        payload["body_size_category"] = "boss";

        bool applied = CharacterCreationService.ApplyCharacterCreationPayloadToMemberForIdentityCatalog(
            memberState,
            payload,
            MakeCreationContentSource()
        );

        _test.False(applied, "建卡替换路径应拒绝 race/subrace 双向关系非法的 payload。");
        _test.Eq(
            FormatCreationSurface(memberState),
            beforeSurface,
            "非法 race/subrace payload 不应污染成员身份、体型、显示名或属性。"
        );
    }

    private void TestCreationPayloadDoesNotBakeRerollLuckByDefault()
    {
        PartyMemberState memberState =
            CharacterCreationService.CreateMemberFromCharacterCreationPayloadForIdentityCatalog(
                "companion",
                BuildCreationPayload(0),
                MakeCreationContentSource()
            );

        _test.Eq(
            memberState?.GetHiddenLuckAtBirth() ?? int.MinValue,
            0,
            "非主角通过正式建卡 payload 创建时，即使 payload 带 reroll_count，也应默认 hidden_luck_at_birth=0。"
        );
    }

    private void TestCreationPayloadCanOptIntoRerollLuckForMainCharacter()
    {
        PartyMemberState memberState =
            CharacterCreationService.CreateMemberFromCharacterCreationPayloadForIdentityCatalog(
                "hero",
                BuildCreationPayload(0),
                MakeCreationContentSource(),
                new CharacterCreationOptions(bakeRerollLuck: true)
            );

        _test.Eq(
            memberState?.GetHiddenLuckAtBirth() ?? int.MinValue,
            2,
            "主角建卡 opt-in 后应按 reroll_count=0 烘焙 hidden_luck_at_birth=+2。"
        );
    }

    private void TestCreationPayloadRejectsNonIntegerRerollLuckWhenOptedIn()
    {
        GDictionary payload = BuildCreationPayload(0);
        payload["reroll_count"] = "100";

        PartyMemberState memberState =
            CharacterCreationService.CreateMemberFromCharacterCreationPayloadForIdentityCatalog(
                "bad_reroll",
                payload,
                MakeCreationContentSource(),
                new CharacterCreationOptions(bakeRerollLuck: true)
            );

        _test.True(
            memberState == null,
            "主角建卡 opt-in 烘焙 reroll luck 时应拒绝非 int reroll_count。"
        );
    }

    private static GDictionary BuildCreationPayload(int rerollCount)
    {
        return new GDictionary
        {
            ["display_name"] = "Creation Test",
            ["race_id"] = "human",
            ["subrace_id"] = "common_human",
            ["age_years"] = 24,
            ["birth_at_world_step"] = 0,
            ["age_profile_id"] = "human_age_profile",
            ["natural_age_stage_id"] = "adult",
            ["effective_age_stage_id"] = "adult",
            ["body_size_category"] = "medium",
            ["strength"] = 10,
            ["agility"] = 10,
            ["constitution"] = 10,
            ["perception"] = 10,
            ["intelligence"] = 10,
            ["willpower"] = 10,
            ["action_threshold"] = 30,
            ["reroll_count"] = rerollCount,
        };
    }

    private static ProgressionIdentityCatalogData MakeCreationContentSource()
    {
        RaceDefinition raceDef = MakeIdentityOptionRace("human", "common_human", new[] { "common_human" });
        SubraceDefinition subraceDef = MakeIdentityOptionSubrace("common_human", "human", "large");
        SubraceDefinition wrongParentSubraceDef = MakeIdentityOptionSubrace("wrong_parent", "elf");
        BloodlineDefinition titanBloodlineDef = MakeBloodline("titan", "titan_awakened");
        BloodlineStageDefinition titanBloodlineStageDef = MakeBloodlineStage("titan_awakened", "titan");
        BloodlineDefinition dragonBloodlineDef = MakeBloodline("dragon", "dragon_awakened");
        BloodlineStageDefinition dragonBloodlineStageDef = MakeBloodlineStage("dragon_awakened", "dragon");
        AscensionDefinition ascensionDef = MakeAscension("titan", "titan_avatar", Array.Empty<StringName>());
        AscensionStageDefinition ascensionStageDef = MakeAscensionStage("titan_avatar", "titan", "huge");
        AscensionDefinition bloodlineLockedAscensionDef = MakeAscension(
            "bloodline_locked_ascension", "bloodline_locked_awakened", new[] { new StringName("titan") }
        );
        AscensionStageDefinition bloodlineLockedStageDef = MakeAscensionStage(
            "bloodline_locked_awakened", "bloodline_locked_ascension", ""
        );

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition> { [raceDef.RaceId] = raceDef },
            new Dictionary<StringName, SubraceDefinition>
                {
                    [subraceDef.SubraceId] = subraceDef,
                    [wrongParentSubraceDef.SubraceId] = wrongParentSubraceDef,
                },
            new Dictionary<StringName, AgeProfileDefinition>(),
            new Dictionary<StringName, BloodlineDefinition>
                {
                    [titanBloodlineDef.BloodlineId] = titanBloodlineDef,
                    [dragonBloodlineDef.BloodlineId] = dragonBloodlineDef,
                },
            new Dictionary<StringName, BloodlineStageDefinition>
                {
                    [titanBloodlineStageDef.StageId] = titanBloodlineStageDef,
                    [dragonBloodlineStageDef.StageId] = dragonBloodlineStageDef,
                },
            new Dictionary<StringName, AscensionDefinition>
                {
                    [ascensionDef.AscensionId] = ascensionDef,
                    [bloodlineLockedAscensionDef.AscensionId] = bloodlineLockedAscensionDef,
                },
            new Dictionary<StringName, AscensionStageDefinition>
                {
                    [ascensionStageDef.StageId] = ascensionStageDef,
                    [bloodlineLockedStageDef.StageId] = bloodlineLockedStageDef,
                },
            new Dictionary<StringName, StageAdvancementDefinition>()
        );
    }

    private static int BodySizeForCategory(StringName category)
    {
        return category.ToString() switch
        {
            "tiny" => 1,
            "small" => 1,
            "medium" => 2,
            "large" => 3,
            "huge" => 4,
            "gargantuan" => 5,
            "boss" => 6,
            _ => 0,
        };
    }

    private static ProgressionIdentityCatalogData MakeIdentityOptionCatalog()
    {
        RaceDefinition human = MakeIdentityOptionRace(
            "human",
            "common_human",
            new[] { "common_human", "noble_human", "wrong_parent", "missing_subrace" }
        );
        RaceDefinition orphanRace = MakeIdentityOptionRace("orphan_race", "", Array.Empty<string>());
        RaceDefinition invalidDefaultRace = MakeIdentityOptionRace(
            "invalid_default_race",
            "parent_only_invalid_default",
            new[] { "valid_for_invalid_default" }
        );

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition>
                {
                    [human.RaceId] = human,
                    [orphanRace.RaceId] = orphanRace,
                    [invalidDefaultRace.RaceId] = invalidDefaultRace,
                },
            new Dictionary<StringName, SubraceDefinition>
                {
                [new StringName("common_human")] = MakeIdentityOptionSubrace(
                    "common_human",
                    "human"
                ),
                [new StringName("noble_human")] = MakeIdentityOptionSubrace(
                    "noble_human",
                    "human"
                ),
                [new StringName("parent_only_human")] = MakeIdentityOptionSubrace(
                    "parent_only_human",
                    "human"
                ),
                [new StringName("wrong_parent")] = MakeIdentityOptionSubrace(
                    "wrong_parent",
                    "elf"
                ),
                [new StringName("parent_only_invalid_default")] = MakeIdentityOptionSubrace(
                    "parent_only_invalid_default",
                    "invalid_default_race"
                ),
                [new StringName("valid_for_invalid_default")] = MakeIdentityOptionSubrace(
                    "valid_for_invalid_default",
                    "invalid_default_race"
                ),
                },
            new Dictionary<StringName, AgeProfileDefinition>(),
            new Dictionary<StringName, BloodlineDefinition>(),
            new Dictionary<StringName, BloodlineStageDefinition>(),
            new Dictionary<StringName, AscensionDefinition>(),
            new Dictionary<StringName, AscensionStageDefinition>(),
            new Dictionary<StringName, StageAdvancementDefinition>()
        );
    }

    private static RaceDefinition MakeIdentityOptionRace(
        StringName raceId,
        StringName defaultSubraceId,
        IEnumerable<string> subraceIds
    )
    {
        List<StringName> ids = new();
        foreach (string subraceId in subraceIds)
            ids.Add(subraceId);
        return new RaceDefinition(
            raceId, raceId.ToString(), "Fixture race.", "human_age_profile", defaultSubraceId,
            ids, "medium", 6, Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(), Array.Empty<StringName>(), Array.Empty<string>()
        );
    }

    private static SubraceDefinition MakeIdentityOptionSubrace(
        StringName subraceId,
        StringName parentRaceId,
        StringName bodySizeOverride = default
    ) =>
        new(
            subraceId, parentRaceId, subraceId.ToString(), "Fixture subrace.", bodySizeOverride,
            0, Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(), Array.Empty<StringName>(), Array.Empty<string>()
        );

    private static BloodlineDefinition MakeBloodline(StringName id, StringName stageId) =>
        new(id, id.ToString(), "Fixture bloodline.", new[] { stageId }, Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<AttributeModifierDefinition>(), Array.Empty<string>());

    private static BloodlineStageDefinition MakeBloodlineStage(StringName id, StringName owner) =>
        new(id, owner, id.ToString(), "Fixture bloodline stage.", Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<string>());

    private static AscensionDefinition MakeAscension(StringName id, StringName stageId, IReadOnlyList<StringName> bloodlines) =>
        new(id, id.ToString(), "Fixture ascension.", new[] { stageId }, Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            bloodlines, Array.Empty<string>(), false, false);

    private static AscensionStageDefinition MakeAscensionStage(StringName id, StringName owner, StringName size) =>
        new(id, owner, id.ToString(), "Fixture ascension stage.", Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(), size, Array.Empty<string>());

    private static PartyMemberState MakeExistingMemberState()
    {
        PartyMemberState memberState = new()
        {
            member_id = "hero",
            display_name = "Existing Hero",
            race_id = "human",
            subrace_id = "common_human",
            body_size_category = "large",
        };
        memberState.body_size = BodySizeForCategory(memberState.body_size_category);
        memberState.progression = new UnitProgress
        {
            unit_id = memberState.member_id,
            display_name = memberState.display_name,
            unit_base_attributes = new UnitBaseAttributes(),
        };
        return memberState;
    }

    private static string FormatIdentityBody(PartyMemberState memberState)
    {
        return string.Join(
            "|",
            memberState.race_id,
            memberState.subrace_id,
            memberState.bloodline_id,
            memberState.bloodline_stage_id,
            memberState.ascension_id,
            memberState.ascension_stage_id,
            memberState.body_size_category,
            memberState.body_size
        );
    }

    private static string FormatCreationSurface(PartyMemberState memberState)
    {
        UnitBaseAttributes baseAttributes = memberState?.progression?.unit_base_attributes;
        int strength = baseAttributes != null ? baseAttributes.GetAttributeValue("strength") : -999;
        return string.Join(
            "|",
            memberState?.display_name ?? "",
            memberState?.race_id ?? "",
            memberState?.subrace_id ?? "",
            memberState?.bloodline_id ?? "",
            memberState?.bloodline_stage_id ?? "",
            memberState?.ascension_id ?? "",
            memberState?.ascension_stage_id ?? "",
            memberState?.body_size_category ?? "",
            memberState?.body_size ?? -999,
            strength
        );
    }

    private void AssertIdsEq(
        IReadOnlyList<StringName> actual,
        IReadOnlyList<StringName> expected,
        string message
    )
    {
        if (actual.Count != expected.Count)
        {
            _test.Fail($"{message} | actual={FormatIds(actual)} expected={FormatIds(expected)}");
            return;
        }

        for (int index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _test.Fail(
                    $"{message} | actual={FormatIds(actual)} expected={FormatIds(expected)}"
                );
                return;
            }
        }
    }

    private static bool ContainsId(IEnumerable<StringName> ids, StringName targetId)
    {
        foreach (StringName id in ids)
        {
            if (id == targetId)
                return true;
        }
        return false;
    }

    private static string FormatIds(IEnumerable<StringName> ids)
    {
        List<string> values = new();
        foreach (StringName id in ids)
            values.Add(id.ToString());
        return $"[{string.Join(", ", values)}]";
    }
}
