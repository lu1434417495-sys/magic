using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_info_identity_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestBuilderUsesTypedBattleStatusEntries();
        TestBattleCharacterInfoIncludesIdentitySection();
        TestWorldCharacterInfoRequiresFormalStringValues();
        TestTypedContextBuildsDetachedPlainSnapshot();

        RequestTestExit(_test.Finish("Character info identity regression"));
    }

    private void TestBuilderUsesTypedBattleStatusEntries()
    {
        GameRuntimeCharacterInfoBuilder builder = new();
        BattleUnitState unit = new()
        {
            unit_id = "status_unit",
            display_name = "Status Unit",
        };
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "burning",
                stacks = 2,
                duration = 15,
            }
        );
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "slow",
                stacks = 1,
                duration = 30,
            }
        );

        IReadOnlyList<GameRuntimeCharacterInfoEntry> entries =
            builder.BuildBattleCharacterStatusEntries(unit);
        _test.Eq(entries.Count, 2, "battle status entries 应继续输出两条正式状态。");
        if (entries.Count >= 2)
        {
            _test.Eq(entries[0].Text, "burning x2 · 15 TU", "status entries 应按 typed helper 的排序输出 burning。");
            _test.Eq(entries[1].Text, "slow · 30 TU", "status entries 应按 typed helper 的排序输出 slow。");
        }
    }

    private void TestBattleCharacterInfoIncludesIdentitySection()
    {
        GameRuntimeFacade runtime = BuildRuntime();
        try
        {
            GameRuntimeCharacterInfoBuilder builder = new();
            builder.Setup(runtime);

            BattleUnitState unit = new BattleUnitState()
            {
                source_member_id = "hero",
            }.WithCombatResourcesForTest(
                hp: 10,
                mp: 2
            );
            unit.SetAnchorCoord(new Vector2I(2, 3));
            unit.attribute_snapshot.SetValue("hp_max", 20);
            unit.attribute_snapshot.SetValue("mp_max", 5);

            IReadOnlyList<GameRuntimeCharacterInfoSection> sections =
                builder.BuildBattleCharacterInfoSections(unit, "战斗单位", "玩家");
            GameRuntimeCharacterInfoSection identitySection = FindSection(
                sections,
                "身份与特性"
            );
            _test.True(identitySection != null, "战斗人物信息应包含身份与特性 section。");
            IReadOnlyList<GameRuntimeCharacterInfoEntry> entries =
                identitySection?.Entries ?? System.Array.Empty<GameRuntimeCharacterInfoEntry>();

            _test.True(HasPairEntry(entries, "种族", "Human"), "身份 section 应显示 race。");
            _test.True(HasPairEntry(entries, "亚种", "High Human"), "身份 section 应显示 subrace。");
            _test.True(HasPairEntry(entries, "有效阶段", "Dragon Awakened"), "身份 section 应显示 effective stage。");
            _test.True(HasPairEntry(entries, "血脉", "Titan · Awakened"), "身份 section 应显示 bloodline/stage。");
            _test.True(HasPairEntry(entries, "升华", "Dragon · Awakened"), "身份 section 应显示 ascension/stage。");
            _test.True(HasPairEntry(entries, "伤害抗性", "fire=half"), "身份 section 应显示 damage resistance。");
            _test.True(HasPairEntry(entries, "豁免优势", "charm"), "身份 section 应显示 save advantage。");
            _test.True(HasTextEntry(entries, "特性：Dragon stage"), "身份 section 应显示 trait summary。");
            _test.True(
                HasTextEntry(entries, "种族法术：Dragon Breath（Dragon，每场战斗 1 次）"),
                "身份 section 应显示 racial skill。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestWorldCharacterInfoRequiresFormalStringValues()
    {
        GameRuntimeCharacterInfoBuilder builder = new();
        WorldMapNpcData npc = WorldMapNpcData.FromDictionary(
            new GDictionary
            {
                ["coord"] = new Vector2I(1, 2),
                ["display_name"] = "仓库管理员",
                ["faction_id"] = "player",
                ["service_type"] = new StringName("warehouse"),
                ["facility_name"] = new StringName("仓库"),
            }
        );

        IReadOnlyList<GameRuntimeCharacterInfoSection> sections =
            builder.BuildWorldCharacterInfoSections(npc, new Vector2I(1, 2), "玩家");
        _test.Eq(sections.Count, 1, "world character info 应继续只输出基础概览 section。");
        GameRuntimeCharacterInfoSection overviewSection = FindSection(
            sections,
            "基础概览"
        );
        _test.True(overviewSection != null, "world character info 应保留基础概览 section。");
        IReadOnlyList<GameRuntimeCharacterInfoEntry> entries =
            overviewSection?.Entries ?? System.Array.Empty<GameRuntimeCharacterInfoEntry>();
        _test.False(
            HasPairEntry(entries, "服务", "warehouse"),
            "StringName service_type 不应被 builder 当成正式字符串渲染。"
        );
        _test.False(
            HasPairEntry(entries, "所属设施", "仓库"),
            "StringName facility_name 不应被 builder 当成正式字符串渲染。"
        );

        WorldMapNpcData stringNpc = WorldMapNpcData.FromDictionary(
            new GDictionary
            {
                ["coord"] = new Vector2I(1, 2),
                ["display_name"] = "仓库管理员",
                ["faction_id"] = "player",
                ["service_type"] = "  warehouse  ",
                ["facility_name"] = "  仓库  ",
            }
        );
        _test.Eq(stringNpc.ServiceType, "warehouse", "正式 String service_type 应按原 StripEdges 规则归一化。");
        _test.Eq(stringNpc.FacilityName, "仓库", "正式 String facility_name 应按原 StripEdges 规则归一化。");
        IReadOnlyList<GameRuntimeCharacterInfoSection> stringSections =
            builder.BuildWorldCharacterInfoSections(stringNpc, new Vector2I(1, 2), "玩家");
        GameRuntimeCharacterInfoSection stringOverview = FindSection(
            stringSections,
            "基础概览"
        );
        IReadOnlyList<GameRuntimeCharacterInfoEntry> stringEntries =
            stringOverview?.Entries ?? System.Array.Empty<GameRuntimeCharacterInfoEntry>();
        _test.True(
            HasPairEntry(stringEntries, "服务", "warehouse"),
            "正式 String service_type 应进入 typed world character info。"
        );
        _test.True(
            HasPairEntry(stringEntries, "所属设施", "仓库"),
            "正式 String facility_name 应进入 typed world character info。"
        );
    }

    private void TestTypedContextBuildsDetachedPlainSnapshot()
    {
        var sourceSections = new List<GameRuntimeCharacterInfoSection>
        {
            new(
                "装备",
                new[]
                {
                    GameRuntimeCharacterInfoEntry.Pair("主手", "龙骨断剑 ⓘ", "屠龙详情"),
                    GameRuntimeCharacterInfoEntry.Pair("副手", "空手"),
                    GameRuntimeCharacterInfoEntry.TextEntry("burning x2 · 15 TU"),
                }
            ),
        };
        GameRuntimeCharacterInfoContext context = new(
            GameRuntimeCharacterInfoSource.Battle,
            "Hero",
            "战斗单位  |  阵营 玩家  |  坐标 (2,3)",
            "当前行动单位",
            sourceSections,
            unitId: "unit_1",
            memberId: "hero",
            fate: new GameRuntimeCharacterInfoFate(7, -13, 1, 1, 4)
        );
        sourceSections.Clear();

        IReadOnlyDictionary<string, object> snapshot = context.BuildSnapshotPlain();
        AssertExactKeys(
            snapshot,
            "完整 character_info payload",
            "display_name",
            "meta_label",
            "sections",
            "status_label",
            "source",
            "unit_id",
            "member_id",
            "fate"
        );
        _test.Eq(PlainString(snapshot, "source"), "battle", "typed context 应投影正式 battle source。");
        _test.Eq(PlainString(snapshot, "unit_id"), "unit_1", "typed context 应投影 unit_id。");
        _test.Eq(PlainString(snapshot, "member_id"), "hero", "typed context 应投影可选 member_id。");

        IReadOnlyList<object> sections = PlainList(snapshot, "sections");
        _test.Eq(sections.Count, 1, "typed context 应复制 section，不受来源列表后续修改影响。");
        if (sections.Count == 0)
            return;
        IReadOnlyDictionary<string, object> section = PlainDictionary(sections[0]);
        AssertExactKeys(section, "character_info section", "title", "entries");
        _test.Eq(PlainString(section, "title"), "装备", "typed section title 应保持不变。");
        IReadOnlyList<object> entries = PlainList(section, "entries");
        _test.Eq(entries.Count, 3, "typed section 应同时投影带提示、无提示 pair 与 text entry。");
        if (entries.Count < 3)
            return;
        IReadOnlyDictionary<string, object> pairEntry = PlainDictionary(entries[0]);
        AssertExactKeys(pairEntry, "带 tooltip 的 pair entry", "label", "value", "tooltip");
        _test.Eq(PlainString(pairEntry, "label"), "主手", "pair entry 应投影 label。");
        _test.Eq(PlainString(pairEntry, "tooltip"), "屠龙详情", "非空 tooltip 应保留。");
        IReadOnlyDictionary<string, object> pairWithoutTooltip = PlainDictionary(entries[1]);
        AssertExactKeys(pairWithoutTooltip, "无 tooltip 的 pair entry", "label", "value");
        IReadOnlyDictionary<string, object> textEntry = PlainDictionary(entries[2]);
        AssertExactKeys(textEntry, "text entry", "text");
        _test.Eq(PlainString(textEntry, "text"), "burning x2 · 15 TU", "text entry 应保持单一 text 形状。");

        IReadOnlyDictionary<string, object> fate = PlainDictionary(snapshot, "fate");
        AssertExactKeys(
            fate,
            "character_info fate",
            "hidden_luck_at_birth",
            "faith_luck_bonus",
            "effective_luck",
            "fortune_marked",
            "doom_marked",
            "doom_authority",
            "has_misfortune"
        );
        _test.True(
            fate.TryGetValue("effective_luck", out object effectiveLuck)
                && effectiveLuck is long,
            "fate 数值应保持迁移前 plain snapshot 的 Int64 类型。"
        );
        _test.Eq(PlainLong(fate, "effective_luck"), -6L, "effective_luck 应继续截断到 -6 下限。");
        _test.True(PlainBool(fate, "has_misfortune"), "doom_authority > 0 应继续投影 misfortune。");

        GameRuntimeCharacterInfoContext minimalContext = new(
            GameRuntimeCharacterInfoSource.World,
            "路人",
            "世界 NPC",
            "可见提示单位",
            System.Array.Empty<GameRuntimeCharacterInfoSection>()
        );
        AssertExactKeys(
            minimalContext.BuildSnapshotPlain(),
            "无可选字段的 character_info payload",
            "display_name",
            "meta_label",
            "sections",
            "status_label",
            "source"
        );
    }

    private static GameRuntimeFacade BuildRuntime()
    {
        GameRuntimeFacade runtime = new();
        runtime._character_management.setup(
            BuildPartyState(),
            BuildSkillDefinitions(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>(),
            null,
            BuildProgressionIdentityCatalog()
        );
        return runtime;
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new StringNameList { "hero" },
        };
        partyState.SetMemberState(
            new PartyMemberState
            {
                member_id = "hero",
                display_name = "Hero",
                race_id = "human",
                subrace_id = "high_human",
                age_years = 24,
                age_profile_id = "human_age_profile",
                natural_age_stage_id = "adult",
                effective_age_stage_id = "dragon_awakened",
                body_size = 2,
                body_size_category = "medium",
                bloodline_id = "titan",
                bloodline_stage_id = "titan_awakened",
                ascension_id = "dragon",
                ascension_stage_id = "dragon_awakened",
                progression = new UnitProgress
                {
                    unit_id = "hero",
                    display_name = "Hero",
                    unit_base_attributes = new UnitBaseAttributes(),
                },
            }
        );
        return partyState;
    }

    private static Dictionary<StringName, SkillDefinition> BuildSkillDefinitions()
    {
        Dictionary<StringName, SkillDefinition> result = new();
        StringName skillId = "dragon_breath";
        result[skillId] = new SkillDefinition(
            skillId,
            "Dragon Breath",
            "",
            "",
            "active",
            1,
            1,
            "",
            0,
            0,
            System.Array.Empty<int>(),
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            null
        );
        return result;
    }

    private static ProgressionIdentityCatalogData BuildProgressionIdentityCatalog()
    {
        RaceDefinition race = new(
            "human",
            "Human",
            "",
            "",
            "",
            System.Array.Empty<StringName>(),
            "medium",
            6,
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<RacialGrantedSkillDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            new StringName[] { "charm" },
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, StringName> { ["fire"] = "half" },
            System.Array.Empty<StringName>(),
            new string[] { "Human ambition" }
        );
        SubraceDefinition subrace = new(
            "high_human",
            "human",
            "High Human",
            "",
            "",
            0,
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<RacialGrantedSkillDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<string>()
        );
        AgeProfileDefinition ageProfile = new(
            "human_age_profile",
            "",
            0,
            12,
            16,
            18,
            35,
            53,
            70,
            90,
            new AgeStageRuleDefinition[]
            {
                new(
                    "adult",
                    "Adult",
                    "",
                    System.Array.Empty<AttributeModifierDefinition>(),
                    System.Array.Empty<StringName>(),
                    System.Array.Empty<string>(),
                    false,
                    false
                ),
                new(
                    "dragon_awakened",
                    "Dragon Awakened",
                    "",
                    System.Array.Empty<AttributeModifierDefinition>(),
                    System.Array.Empty<StringName>(),
                    new string[] { "Dragon stage" },
                    false,
                    false
                ),
            },
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>()
        );
        BloodlineDefinition bloodline = new(
            "titan",
            "Titan",
            "",
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<RacialGrantedSkillDefinition>(),
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<string>()
        );
        BloodlineStageDefinition bloodlineStage = new(
            "titan_awakened",
            "titan",
            "Awakened",
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<RacialGrantedSkillDefinition>(),
            System.Array.Empty<string>()
        );
        AscensionDefinition ascension = new(
            "dragon",
            "Dragon",
            "",
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            new RacialGrantedSkillDefinition[]
            {
                new("dragon_breath", 1, "per_battle", 1),
            },
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<string>(),
            false,
            false
        );
        AscensionStageDefinition ascensionStage = new(
            "dragon_awakened",
            "dragon",
            "Awakened",
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<RacialGrantedSkillDefinition>(),
            "",
            System.Array.Empty<string>()
        );

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition> { [race.RaceId] = race },
            new Dictionary<StringName, SubraceDefinition> { [subrace.SubraceId] = subrace },
            new Dictionary<StringName, AgeProfileDefinition> { [ageProfile.ProfileId] = ageProfile },
            new Dictionary<StringName, BloodlineDefinition> { [bloodline.BloodlineId] = bloodline },
            new Dictionary<StringName, BloodlineStageDefinition> { [bloodlineStage.StageId] = bloodlineStage },
            new Dictionary<StringName, AscensionDefinition> { [ascension.AscensionId] = ascension },
            new Dictionary<StringName, AscensionStageDefinition> { [ascensionStage.StageId] = ascensionStage },
            new Dictionary<StringName, StageAdvancementDefinition>()
        );
    }

    private static GameRuntimeCharacterInfoSection FindSection(
        IEnumerable<GameRuntimeCharacterInfoSection> sections,
        string title
    )
    {
        foreach (GameRuntimeCharacterInfoSection section in sections)
        {
            if (section != null && section.Title == title)
                return section;
        }
        return null;
    }

    private static bool HasPairEntry(
        IEnumerable<GameRuntimeCharacterInfoEntry> entries,
        string label,
        string value
    )
    {
        foreach (GameRuntimeCharacterInfoEntry entry in entries)
        {
            if (
                entry != null
                && entry.Kind == GameRuntimeCharacterInfoEntryKind.Pair
                && entry.Label == label
                && entry.Value == value
            )
                return true;
        }
        return false;
    }

    private static bool HasTextEntry(
        IEnumerable<GameRuntimeCharacterInfoEntry> entries,
        string text
    )
    {
        foreach (GameRuntimeCharacterInfoEntry entry in entries)
        {
            if (
                entry != null
                && entry.Kind == GameRuntimeCharacterInfoEntryKind.Text
                && entry.Text == text
            )
                return true;
        }
        return false;
    }

    private static IReadOnlyDictionary<string, object> PlainDictionary(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is IReadOnlyDictionary<string, object> result
            ? result
            : new Dictionary<string, object>();

    private static IReadOnlyDictionary<string, object> PlainDictionary(object value) =>
        value is IReadOnlyDictionary<string, object> result
            ? result
            : new Dictionary<string, object>();

    private void AssertExactKeys(
        IReadOnlyDictionary<string, object> dictionary,
        string payloadLabel,
        params string[] expectedKeys
    )
    {
        _test.Eq(
            dictionary.Count,
            expectedKeys.Length,
            $"{payloadLabel} 不应增加或遗漏 schema key。"
        );
        foreach (string key in expectedKeys)
            _test.True(dictionary.ContainsKey(key), $"{payloadLabel} 应包含 key：{key}。");
    }

    private static IReadOnlyList<object> PlainList(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is IReadOnlyList<object> result
            ? result
            : System.Array.Empty<object>();

    private static string PlainString(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is string text
            ? text
            : "";

    private static long PlainLong(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is long number
            ? number
            : long.MinValue;

    private static bool PlainBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is bool flag
        && flag;
}
