using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_character_info_identity_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestBuilderUsesPlainCSharpHelperShape();
        TestBattleCharacterInfoIncludesIdentitySection();

        if (_failures.Count == 0)
        {
            GD.Print("Character info identity regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Character info identity regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestBuilderUsesPlainCSharpHelperShape()
    {
        var builderType = typeof(GameRuntimeCharacterInfoBuilder);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(builderType),
            "GameRuntimeCharacterInfoBuilder should be a plain C# helper, not a GodotObject."
        );
        AssertFalse(
            System.Attribute.IsDefined(builderType, typeof(GlobalClassAttribute)),
            "GameRuntimeCharacterInfoBuilder should not be a Godot GlobalClass."
        );
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
            unit.attribute_snapshot.set_value("hp_max", 20);
            unit.attribute_snapshot.set_value("mp_max", 5);

            GDictionaryArray sections = builder.BuildBattleCharacterInfoSections(
                unit,
                "战斗单位",
                "玩家"
            );
            GDictionary identitySection = FindSection(sections, "身份与特性");
            AssertTrue(identitySection.Count > 0, "战斗人物信息应包含身份与特性 section。");
            Godot.Collections.Array entries = identitySection.ContainsKey("entries")
                ? identitySection["entries"].AsGodotArray()
                : new Godot.Collections.Array();

            AssertTrue(HasPairEntry(entries, "种族", "Human"), "身份 section 应显示 race。");
            AssertTrue(HasPairEntry(entries, "亚种", "High Human"), "身份 section 应显示 subrace。");
            AssertTrue(HasPairEntry(entries, "有效阶段", "Dragon Awakened"), "身份 section 应显示 effective stage。");
            AssertTrue(HasPairEntry(entries, "血脉", "Titan · Awakened"), "身份 section 应显示 bloodline/stage。");
            AssertTrue(HasPairEntry(entries, "升华", "Dragon · Awakened"), "身份 section 应显示 ascension/stage。");
            AssertTrue(HasPairEntry(entries, "伤害抗性", "fire=half"), "身份 section 应显示 damage resistance。");
            AssertTrue(HasPairEntry(entries, "豁免优势", "charm"), "身份 section 应显示 save advantage。");
            AssertTrue(HasTextEntry(entries, "特性：Dragon stage"), "身份 section 应显示 trait summary。");
            AssertTrue(
                HasTextEntry(entries, "种族法术：Dragon Breath（Dragon，每场战斗 1 次）"),
                "身份 section 应显示 racial skill。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime()
    {
        GameRuntimeFacade runtime = new();
        runtime._character_management.setup(
            BuildPartyState(),
            BuildSkillDefs(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            default,
            BuildProgressionContentBundle()
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
        partyState.set_member_state(
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

    private static GDictionary BuildSkillDefs()
    {
        return new GDictionary
        {
            ["dragon_breath"] = new SkillDef
            {
                skill_id = "dragon_breath",
                display_name = "Dragon Breath",
            },
        };
    }

    private static GDictionary BuildProgressionContentBundle()
    {
        return new GDictionary
        {
            ["race_defs"] = new GDictionary
            {
                ["human"] = new RaceDef
                {
                    race_id = "human",
                    display_name = "Human",
                    racial_trait_summary = new Godot.Collections.Array<string>
                    {
                        "Human ambition",
                    },
                    damage_resistances = new GDictionary { ["fire"] = "half" },
                    save_advantage_tags = new GStringNameArray { "charm" },
                },
            },
            ["subrace_defs"] = new GDictionary
            {
                ["high_human"] = new SubraceDef
                {
                    subrace_id = "high_human",
                    parent_race_id = "human",
                    display_name = "High Human",
                },
            },
            ["age_profile_defs"] = new GDictionary
            {
                ["human_age_profile"] = new AgeProfileDef
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
                },
            },
            ["bloodline_defs"] = new GDictionary
            {
                ["titan"] = new BloodlineDef
                {
                    bloodline_id = "titan",
                    display_name = "Titan",
                },
            },
            ["bloodline_stage_defs"] = new GDictionary
            {
                ["titan_awakened"] = new BloodlineStageDef
                {
                    stage_id = "titan_awakened",
                    bloodline_id = "titan",
                    display_name = "Awakened",
                },
            },
            ["ascension_defs"] = new GDictionary
            {
                ["dragon"] = new AscensionDef
                {
                    ascension_id = "dragon",
                    display_name = "Dragon",
                    racial_granted_skills = new Godot.Collections.Array<RacialGrantedSkill>
                    {
                        new()
                        {
                            skill_id = "dragon_breath",
                            charge_kind = RacialGrantedSkill.CHARGE_KIND_PER_BATTLE(),
                            charges = 1,
                        },
                    },
                },
            },
            ["ascension_stage_defs"] = new GDictionary
            {
                ["dragon_awakened"] = new AscensionStageDef
                {
                    stage_id = "dragon_awakened",
                    ascension_id = "dragon",
                    display_name = "Awakened",
                },
            },
        };
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }
}
