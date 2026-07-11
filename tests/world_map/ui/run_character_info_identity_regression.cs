using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_character_info_identity_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestBuilderUsesPlainCSharpHelperShape();
        TestBuilderUsesTypedBattleStatusEntries();
        TestBattleCharacterInfoIncludesIdentitySection();
        TestWorldCharacterInfoRequiresFormalStringValues();

        RequestTestExit(_test.Finish("Character info identity regression"));
    }

    private void TestBuilderUsesPlainCSharpHelperShape()
    {
        var builderType = typeof(GameRuntimeCharacterInfoBuilder);
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

        GDictionaryArray entries = builder.BuildBattleCharacterStatusEntries(unit);
        _test.Eq(entries.Count, 2, "battle status entries 应继续输出两条正式状态。");
        if (entries.Count >= 2)
        {
            _test.Eq(DictString(entries[0], "text"), "burning x2 · 15 TU", "status entries 应按 typed helper 的排序输出 burning。");
            _test.Eq(DictString(entries[1], "text"), "slow · 30 TU", "status entries 应按 typed helper 的排序输出 slow。");
        }
    }

    private void TestBattleCharacterInfoIncludesIdentitySection()
    {
        GameRuntimeFacade runtime = BuildRuntime();
        try
        {
            GameRuntimeCharacterInfoBuilder builder = new();
            builder.Setup(runtime);

            BattleUnitState unit = new()
            {
                source_member_id = "hero",
                coord = new Vector2I(2, 3),
                current_hp = 10,
                current_mp = 2,
            };
            unit.attribute_snapshot.SetValue("hp_max", 20);
            unit.attribute_snapshot.SetValue("mp_max", 5);

            GDictionaryArray sections = builder.BuildBattleCharacterInfoSections(
                unit,
                "战斗单位",
                "玩家"
            );
            GDictionary identitySection = FindSection(sections, "身份与特性");
            _test.True(identitySection.Count > 0, "战斗人物信息应包含身份与特性 section。");
            Godot.Collections.Array entries = identitySection.ContainsKey("entries")
                ? identitySection["entries"].AsGodotArray()
                : new Godot.Collections.Array();

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
        GDictionary npc = new()
        {
            ["service_type"] = new StringName("warehouse"),
            ["facility_name"] = new StringName("仓库"),
        };

        GDictionaryArray sections = builder.BuildWorldCharacterInfoSections(
            npc,
            new Vector2I(1, 2),
            "玩家"
        );
        _test.Eq(sections.Count, 1, "world character info 应继续只输出基础概览 section。");
        GDictionary overviewSection = FindSection(sections, "基础概览");
        _test.True(overviewSection.Count > 0, "world character info 应保留基础概览 section。");
        Godot.Collections.Array entries = overviewSection.ContainsKey("entries")
            ? overviewSection["entries"].AsGodotArray()
            : new Godot.Collections.Array();
        _test.False(
            HasPairEntry(entries, "服务", "warehouse"),
            "StringName service_type 不应被 builder 当成正式字符串渲染。"
        );
        _test.False(
            HasPairEntry(entries, "所属设施", "仓库"),
            "StringName facility_name 不应被 builder 当成正式字符串渲染。"
        );
    }

    private static GameRuntimeFacade BuildRuntime()
    {
        GameRuntimeFacade runtime = new();
        runtime._character_management.setup(
            BuildPartyState(),
            BuildSkillDefinitions(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
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
            active_member_ids = new GStringNameArray { "hero" },
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
        RaceDef race = new()
        {
            race_id = "human",
            display_name = "Human",
            racial_trait_summary = new Godot.Collections.Array<string>
            {
                "Human ambition",
            },
            damage_resistances = new GDictionary
            {
                [new StringName("fire")] = new StringName("half"),
            },
            save_advantage_tags = new GStringNameArray { "charm" },
        };
        SubraceDef subrace = new()
        {
            subrace_id = "high_human",
            parent_race_id = "human",
            display_name = "High Human",
        };
        AgeProfileDef ageProfile = new()
        {
            profile_id = "human_age_profile",
            stage_rules = new Godot.Collections.Array<AgeStageRule>
            {
                new() { stage_id = "adult", display_name = "Adult" },
                new()
                {
                    stage_id = "dragon_awakened",
                    display_name = "Dragon Awakened",
                    trait_summary = new Godot.Collections.Array<string>
                    {
                        "Dragon stage",
                    },
                },
            },
        };
        BloodlineDef bloodline = new()
        {
            bloodline_id = "titan",
            display_name = "Titan",
        };
        BloodlineStageDef bloodlineStage = new()
        {
            stage_id = "titan_awakened",
            bloodline_id = "titan",
            display_name = "Awakened",
        };
        AscensionDef ascension = new()
        {
            ascension_id = "dragon",
            display_name = "Dragon",
            racial_granted_skills = new Godot.Collections.Array<RacialGrantedSkill>
            {
                new()
                {
                    skill_id = "dragon_breath",
                    ChargeKind = RacialSkillChargeKind.PerBattle,
                    charges = 1,
                },
            },
        };
        AscensionStageDef ascensionStage = new()
        {
            stage_id = "dragon_awakened",
            ascension_id = "dragon",
            display_name = "Awakened",
        };

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDef> { [race.race_id] = race },
            new Dictionary<StringName, SubraceDef> { [subrace.subrace_id] = subrace },
            new Dictionary<StringName, AgeProfileDef> { [ageProfile.profile_id] = ageProfile },
            new Dictionary<StringName, BloodlineDef> { [bloodline.bloodline_id] = bloodline },
            new Dictionary<StringName, BloodlineStageDef> { [bloodlineStage.stage_id] = bloodlineStage },
            new Dictionary<StringName, AscensionDef> { [ascension.ascension_id] = ascension },
            new Dictionary<StringName, AscensionStageDef> { [ascensionStage.stage_id] = ascensionStage },
            new Dictionary<StringName, StageAdvancementModifier>()
        );
    }

    private static GDictionary FindSection(GDictionaryArray sections, string title)
    {
        foreach (GDictionary section in sections)
        {
            if (DictString(section, "title") == title)
            {
                return section;
            }
        }
        return new GDictionary();
    }

    private static bool HasPairEntry(Godot.Collections.Array entries, string label, string value)
    {
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictString(entry, "label") == label && DictString(entry, "value") == value)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasTextEntry(Godot.Collections.Array entries, string text)
    {
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            if (DictString(entryValue.AsGodotDictionary(), "text") == text)
            {
                return true;
            }
        }
        return false;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : "";
    }
}
