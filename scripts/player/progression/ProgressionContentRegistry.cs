using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class ProgressionContentRegistry : RefCounted
{
    private static readonly StringName HpMax = "hp_max";
    private static readonly StringName SkillTypeActive = "active";
    private static readonly StringName SkillTypePassive = "passive";
    private static readonly StringName UnlockModeStandard = "standard";
    private static readonly StringName UnlockModeCompositeUpgrade = "composite_upgrade";
    private static readonly StringName CoreTransitionInherit = "inherit";
    private static readonly StringName CoreTransitionReplaceSources = "replace_sources_with_result";
    private static readonly StringName PracticeMeditation = "meditation";
    private static readonly StringName PracticeCultivation = "cultivation";

    private static readonly HashSet<StringName> ValidSkillTypes = new()
    {
        SkillTypeActive,
        SkillTypePassive,
    };
    private static readonly HashSet<StringName> ValidLearnSources = new()
    {
        "book",
        "innate",
        "player",
        "profession",
        "race",
        "subrace",
        "ascension",
        "bloodline",
    };
    private static readonly HashSet<StringName> ValidUnlockModes = new()
    {
        UnlockModeStandard,
        UnlockModeCompositeUpgrade,
    };
    private static readonly HashSet<StringName> ValidCoreSkillTransitionModes = new()
    {
        CoreTransitionInherit,
        CoreTransitionReplaceSources,
    };
    private static readonly StringName[] PracticeTrackTags =
    {
        PracticeMeditation,
        PracticeCultivation,
    };
    private static readonly HashSet<StringName> ValidPracticeTiers = new()
    {
        "basic",
        "intermediate",
        "advanced",
        "ultimate",
    };
    private static readonly HashSet<int> ValidBodySizes = new()
    {
        BodySizeContentRules.BODY_SIZE_TINY,
        BodySizeContentRules.BODY_SIZE_MEDIUM,
        BodySizeContentRules.BODY_SIZE_LARGE,
        BodySizeContentRules.BODY_SIZE_HUGE,
        BodySizeContentRules.BODY_SIZE_GARGANTUAN,
        BodySizeContentRules.BODY_SIZE_BOSS,
    };

    private GDictionary _skillDefs = new();
    private GDictionary _professionDefs = new();
    private GDictionary _achievementDefs = new();
    private GDictionary _questDefs = new();
    private GDictionary _raceDefs = new();
    private GDictionary _subraceDefs = new();
    private GDictionary _raceTraitDefs = new();
    private GDictionary _ageProfileDefs = new();
    private GDictionary _bloodlineDefs = new();
    private GDictionary _bloodlineStageDefs = new();
    private GDictionary _ascensionDefs = new();
    private GDictionary _ascensionStageDefs = new();
    private GDictionary _stageAdvancementDefs = new();

    private readonly SkillContentRegistry _skillContentRegistry = new();
    private readonly ProfessionContentRegistry _professionContentRegistry = new();
    private readonly RaceContentRegistry _raceContentRegistry = new();
    private readonly SubraceContentRegistry _subraceContentRegistry = new();
    private readonly RaceTraitContentRegistry _raceTraitContentRegistry = new();
    private readonly AgeContentRegistry _ageContentRegistry = new();
    private readonly BloodlineContentRegistry _bloodlineContentRegistry = new();
    private readonly AscensionContentRegistry _ascensionContentRegistry = new();
    private readonly StageAdvancementContentRegistry _stageAdvancementContentRegistry = new();

    private GStringArray _validationErrors = new();
    private GStringArray _questRegistrationErrors = new();

    public GDictionary _skill_defs
    {
        get => _skillDefs;
        set => _skillDefs = value ?? new GDictionary();
    }
    public GDictionary _profession_defs
    {
        get => _professionDefs;
        set => _professionDefs = value ?? new GDictionary();
    }
    public GDictionary _achievement_defs
    {
        get => _achievementDefs;
        set => _achievementDefs = value ?? new GDictionary();
    }
    public GDictionary _quest_defs
    {
        get => _questDefs;
        set => _questDefs = value ?? new GDictionary();
    }
    public GDictionary _race_defs
    {
        get => _raceDefs;
        set => _raceDefs = value ?? new GDictionary();
    }
    public GDictionary _subrace_defs
    {
        get => _subraceDefs;
        set => _subraceDefs = value ?? new GDictionary();
    }
    public GDictionary _race_trait_defs
    {
        get => _raceTraitDefs;
        set => _raceTraitDefs = value ?? new GDictionary();
    }
    public GDictionary _age_profile_defs
    {
        get => _ageProfileDefs;
        set => _ageProfileDefs = value ?? new GDictionary();
    }
    public GDictionary _bloodline_defs
    {
        get => _bloodlineDefs;
        set => _bloodlineDefs = value ?? new GDictionary();
    }
    public GDictionary _bloodline_stage_defs
    {
        get => _bloodlineStageDefs;
        set => _bloodlineStageDefs = value ?? new GDictionary();
    }
    public GDictionary _ascension_defs
    {
        get => _ascensionDefs;
        set => _ascensionDefs = value ?? new GDictionary();
    }
    public GDictionary _ascension_stage_defs
    {
        get => _ascensionStageDefs;
        set => _ascensionStageDefs = value ?? new GDictionary();
    }
    public GDictionary _stage_advancement_defs
    {
        get => _stageAdvancementDefs;
        set => _stageAdvancementDefs = value ?? new GDictionary();
    }
    public GStringArray _validation_errors
    {
        get => _validationErrors;
        set => _validationErrors = value ?? new GStringArray();
    }
    public GStringArray _quest_registration_errors
    {
        get => _questRegistrationErrors;
        set => _questRegistrationErrors = value ?? new GStringArray();
    }

    public ProgressionContentRegistry()
    {
        rebuild();
    }

    public new void Dispose()
    {
        ClearRuntimeCaches();
        _skillContentRegistry.Dispose();
        _professionContentRegistry.Dispose();
        _raceContentRegistry.Dispose();
        _subraceContentRegistry.Dispose();
        _raceTraitContentRegistry.Dispose();
        _ageContentRegistry.Dispose();
        _bloodlineContentRegistry.Dispose();
        _ascensionContentRegistry.Dispose();
        _stageAdvancementContentRegistry.Dispose();
        base.Dispose();
    }

    public void dispose() => Dispose();

    public void rebuild()
    {
        ClearRuntimeCaches();

        _skillContentRegistry.rebuild();
        _skillDefs = DuplicateDictionary(_skillContentRegistry.get_skill_defs());
        AppendArray(_validationErrors, _skillContentRegistry.validate());

        _professionContentRegistry.setup(_skillDefs);
        _professionDefs = DuplicateDictionary(_professionContentRegistry.get_profession_defs());

        _raceContentRegistry.rebuild();
        _raceDefs = DuplicateDictionary(_raceContentRegistry.get_race_defs());
        _subraceContentRegistry.rebuild();
        _subraceDefs = DuplicateDictionary(_subraceContentRegistry.get_subrace_defs());
        _raceTraitContentRegistry.rebuild();
        _raceTraitDefs = DuplicateDictionary(_raceTraitContentRegistry.get_race_trait_defs());
        _ageContentRegistry.rebuild();
        _ageProfileDefs = DuplicateDictionary(_ageContentRegistry.get_age_profile_defs());
        _bloodlineContentRegistry.rebuild();
        _bloodlineDefs = DuplicateDictionary(_bloodlineContentRegistry.get_bloodline_defs());
        _bloodlineStageDefs = DuplicateDictionary(
            _bloodlineContentRegistry.get_bloodline_stage_defs()
        );
        _ascensionContentRegistry.rebuild();
        _ascensionDefs = DuplicateDictionary(_ascensionContentRegistry.get_ascension_defs());
        _ascensionStageDefs = DuplicateDictionary(
            _ascensionContentRegistry.get_ascension_stage_defs()
        );
        _stageAdvancementContentRegistry.rebuild();
        _stageAdvancementDefs = DuplicateDictionary(
            _stageAdvancementContentRegistry.get_stage_advancement_defs()
        );

        _register_seed_achievements();
        _register_seed_quests();

        AppendArray(_validationErrors, _professionContentRegistry.validate());
        AppendArray(_validationErrors, _raceContentRegistry.validate());
        AppendArray(_validationErrors, _subraceContentRegistry.validate());
        AppendArray(_validationErrors, _raceTraitContentRegistry.validate());
        AppendArray(_validationErrors, _ageContentRegistry.validate());
        AppendArray(_validationErrors, _bloodlineContentRegistry.validate());
        AppendArray(_validationErrors, _ascensionContentRegistry.validate());
        AppendArray(_validationErrors, _stageAdvancementContentRegistry.validate());
        AppendArray(_validationErrors, _collect_validation_errors());
    }

    public GDictionary get_skill_defs() => DuplicateDictionary(_skillDefs);

    public GDictionary get_profession_defs() => DuplicateDictionary(_professionDefs);

    public GDictionary get_achievement_defs() => DuplicateDictionary(_achievementDefs);

    public GDictionary get_quest_defs() => DuplicateDictionary(_questDefs);

    public GStringArray get_quest_registration_errors() =>
        DuplicateStringArray(_questRegistrationErrors);

    public GDictionary get_race_defs() => DuplicateDictionary(_raceDefs);

    public GDictionary get_subrace_defs() => DuplicateDictionary(_subraceDefs);

    public GDictionary get_race_trait_defs() => DuplicateDictionary(_raceTraitDefs);

    public GDictionary get_age_profile_defs() => DuplicateDictionary(_ageProfileDefs);

    public GDictionary get_bloodline_defs() => DuplicateDictionary(_bloodlineDefs);

    public GDictionary get_bloodline_stage_defs() => DuplicateDictionary(_bloodlineStageDefs);

    public GDictionary get_ascension_defs() => DuplicateDictionary(_ascensionDefs);

    public GDictionary get_ascension_stage_defs() => DuplicateDictionary(_ascensionStageDefs);

    public GDictionary get_stage_advancement_defs() => DuplicateDictionary(_stageAdvancementDefs);

    public GDictionary get_bundle()
    {
        return new GDictionary
        {
            ["skill_defs"] = get_skill_defs(),
            ["profession_defs"] = get_profession_defs(),
            ["achievement_defs"] = get_achievement_defs(),
            ["quest_defs"] = get_quest_defs(),
            ["race"] = get_race_defs(),
            ["subrace"] = get_subrace_defs(),
            ["race_trait"] = get_race_trait_defs(),
            ["age_profile"] = get_age_profile_defs(),
            ["bloodline"] = get_bloodline_defs(),
            ["bloodline_stage"] = get_bloodline_stage_defs(),
            ["ascension"] = get_ascension_defs(),
            ["ascension_stage"] = get_ascension_stage_defs(),
            ["stage_advancement"] = get_stage_advancement_defs(),
            ["race_defs"] = get_race_defs(),
            ["subrace_defs"] = get_subrace_defs(),
            ["race_trait_defs"] = get_race_trait_defs(),
            ["age_profile_defs"] = get_age_profile_defs(),
            ["bloodline_defs"] = get_bloodline_defs(),
            ["bloodline_stage_defs"] = get_bloodline_stage_defs(),
            ["ascension_defs"] = get_ascension_defs(),
            ["ascension_stage_defs"] = get_ascension_stage_defs(),
            ["stage_advancement_defs"] = get_stage_advancement_defs(),
        };
    }

    public GStringArray validate()
    {
        var errors = DuplicateStringArray(_validationErrors);
        AppendUniqueErrors(errors, _skillContentRegistry.validate());
        AppendUniqueErrors(errors, _professionContentRegistry.validate());
        AppendUniqueErrors(errors, _raceContentRegistry.validate());
        AppendUniqueErrors(errors, _subraceContentRegistry.validate());
        AppendUniqueErrors(errors, _raceTraitContentRegistry.validate());
        AppendUniqueErrors(errors, _ageContentRegistry.validate());
        AppendUniqueErrors(errors, _bloodlineContentRegistry.validate());
        AppendUniqueErrors(errors, _ascensionContentRegistry.validate());
        AppendUniqueErrors(errors, _stageAdvancementContentRegistry.validate());
        AppendUniqueErrors(errors, _collect_validation_errors());
        return errors;
    }

    public void replace_validation_sources(GDictionary sources)
    {
        _validationErrors.Clear();
        _skillDefs = DuplicateDictionary(GetDictionary(sources, "skill_defs"));
        _professionDefs = DuplicateDictionary(GetDictionary(sources, "profession_defs"));
        _achievementDefs = DuplicateDictionary(GetDictionary(sources, "achievement_defs"));
        _questDefs = DuplicateDictionary(GetDictionary(sources, "quest_defs"));
        _raceDefs = DuplicateDictionary(GetDictionary(sources, "race_defs"));
        _subraceDefs = DuplicateDictionary(GetDictionary(sources, "subrace_defs"));
        _raceTraitDefs = DuplicateDictionary(GetDictionary(sources, "race_trait_defs"));
        _ageProfileDefs = DuplicateDictionary(GetDictionary(sources, "age_profile_defs"));
        _bloodlineDefs = DuplicateDictionary(GetDictionary(sources, "bloodline_defs"));
        _bloodlineStageDefs = DuplicateDictionary(GetDictionary(sources, "bloodline_stage_defs"));
        _ascensionDefs = DuplicateDictionary(GetDictionary(sources, "ascension_defs"));
        _ascensionStageDefs = DuplicateDictionary(GetDictionary(sources, "ascension_stage_defs"));
        _stageAdvancementDefs = DuplicateDictionary(
            GetDictionary(sources, "stage_advancement_defs")
        );
    }

    public GStringArray _collect_validation_errors()
    {
        var errors = new GStringArray();

        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_skillDefs))
        {
            var skillId = new StringName(skillKey);
            _append_invalid_skill_errors(errors, skillId, GetObject<SkillDef>(_skillDefs, skillId));
        }

        foreach (string achievementKey in ProgressionDataUtils.sorted_string_keys(_achievementDefs))
        {
            var achievementId = new StringName(achievementKey);
            _append_invalid_achievement_errors(
                errors,
                achievementId,
                GetObject<AchievementDef>(_achievementDefs, achievementId)
            );
        }

        _append_identity_phase2_validation_errors(errors);
        return errors;
    }

    public void _append_identity_phase2_validation_errors(GStringArray errors)
    {
        _append_global_stage_id_errors(errors);

        foreach (string raceKey in ProgressionDataUtils.sorted_string_keys(_raceDefs))
        {
            var raceId = new StringName(raceKey);
            _append_race_phase2_errors(errors, raceId, GetObject<RaceDef>(_raceDefs, raceId));
        }
        foreach (string subraceKey in ProgressionDataUtils.sorted_string_keys(_subraceDefs))
        {
            var subraceId = new StringName(subraceKey);
            _append_subrace_phase2_errors(
                errors,
                subraceId,
                GetObject<SubraceDef>(_subraceDefs, subraceId)
            );
        }
        foreach (string traitKey in ProgressionDataUtils.sorted_string_keys(_raceTraitDefs))
        {
            var traitId = new StringName(traitKey);
            _append_race_trait_phase2_errors(
                errors,
                traitId,
                GetObject<RaceTraitDef>(_raceTraitDefs, traitId)
            );
        }
        foreach (string profileKey in ProgressionDataUtils.sorted_string_keys(_ageProfileDefs))
        {
            var profileId = new StringName(profileKey);
            _append_age_profile_phase2_errors(
                errors,
                profileId,
                GetObject<AgeProfileDef>(_ageProfileDefs, profileId)
            );
        }
        foreach (string bloodlineKey in ProgressionDataUtils.sorted_string_keys(_bloodlineDefs))
        {
            var bloodlineId = new StringName(bloodlineKey);
            _append_bloodline_phase2_errors(
                errors,
                bloodlineId,
                GetObject<BloodlineDef>(_bloodlineDefs, bloodlineId)
            );
        }
        foreach (string stageKey in ProgressionDataUtils.sorted_string_keys(_bloodlineStageDefs))
        {
            var stageId = new StringName(stageKey);
            _append_bloodline_stage_phase2_errors(
                errors,
                stageId,
                GetObject<BloodlineStageDef>(_bloodlineStageDefs, stageId)
            );
        }
        foreach (string ascensionKey in ProgressionDataUtils.sorted_string_keys(_ascensionDefs))
        {
            var ascensionId = new StringName(ascensionKey);
            _append_ascension_phase2_errors(
                errors,
                ascensionId,
                GetObject<AscensionDef>(_ascensionDefs, ascensionId)
            );
        }
        foreach (string stageKey in ProgressionDataUtils.sorted_string_keys(_ascensionStageDefs))
        {
            var stageId = new StringName(stageKey);
            _append_ascension_stage_phase2_errors(
                errors,
                stageId,
                GetObject<AscensionStageDef>(_ascensionStageDefs, stageId)
            );
        }
        foreach (
            string modifierKey in ProgressionDataUtils.sorted_string_keys(_stageAdvancementDefs)
        )
        {
            var modifierId = new StringName(modifierKey);
            _append_stage_advancement_phase2_errors(
                errors,
                modifierId,
                GetObject<StageAdvancementModifier>(_stageAdvancementDefs, modifierId)
            );
        }
    }

    private void ClearRuntimeCaches()
    {
        _skillDefs.Clear();
        _professionDefs.Clear();
        _achievementDefs.Clear();
        _questDefs.Clear();
        _questRegistrationErrors.Clear();
        _raceDefs.Clear();
        _subraceDefs.Clear();
        _raceTraitDefs.Clear();
        _ageProfileDefs.Clear();
        _bloodlineDefs.Clear();
        _bloodlineStageDefs.Clear();
        _ascensionDefs.Clear();
        _ascensionStageDefs.Clear();
        _stageAdvancementDefs.Clear();
        _validationErrors.Clear();
    }

    private void _register_seed_achievements()
    {
        _register_achievement(
            _build_achievement(
                "battle_won_first",
                "首战归来",
                "亲自完成一次战斗胜利，证明自己已经能从正式交战中平安归来。",
                "battle_won",
                "",
                1,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_ATTRIBUTE_DELTA(),
                        HpMax,
                        "生命上限",
                        8,
                        "首战后的胆气与耐力提升。"
                    ),
                }
            )
        );
    }

    private void _register_seed_quests()
    {
        _register_quest(
            _build_quest(
                "contract_manual_drill",
                "训练记录",
                "在训练场完成两次记录，用于验证任务命令与状态推进链。",
                "service_contract_board",
                new GArray
                {
                    new GDictionary
                    {
                        ["objective_id"] = "train_once",
                        ["objective_type"] = QuestDef.OBJECTIVE_SETTLEMENT_ACTION(),
                        ["target_id"] = "service:training",
                        ["target_value"] = 2,
                    },
                },
                new GArray
                {
                    new GDictionary { ["reward_type"] = QuestDef.REWARD_GOLD(), ["amount"] = 30 },
                }
            )
        );

        _register_quest(
            _build_quest(
                "contract_settlement_warehouse",
                "据点仓储巡查",
                "前往据点服务台完成一次仓储交接。",
                "service_contract_board",
                new GArray
                {
                    new GDictionary
                    {
                        ["objective_id"] = "warehouse_visit",
                        ["objective_type"] = QuestDef.OBJECTIVE_SETTLEMENT_ACTION(),
                        ["target_id"] = "service:warehouse",
                        ["target_value"] = 1,
                    },
                },
                new GArray
                {
                    new GDictionary { ["reward_type"] = QuestDef.REWARD_GOLD(), ["amount"] = 60 },
                }
            )
        );

        _register_quest(
            _build_quest(
                "contract_first_hunt",
                "首轮狩猎",
                "击败任意一组敌对遭遇，证明队伍已具备外出作战能力。",
                "service_contract_board",
                new GArray
                {
                    new GDictionary
                    {
                        ["objective_id"] = "defeat_enemy_once",
                        ["objective_type"] = QuestDef.OBJECTIVE_DEFEAT_ENEMY(),
                        ["target_id"] = "",
                        ["target_value"] = 1,
                    },
                },
                new GArray
                {
                    new GDictionary { ["reward_type"] = QuestDef.REWARD_GOLD(), ["amount"] = 80 },
                }
            )
        );

        _register_quest(
            _build_quest(
                "contract_regional_bounty",
                "地区悬赏",
                "由悬赏署单独发放的区域通缉，用来验证多 provider 任务板的过滤边界。",
                "service_bounty_registry",
                new GArray
                {
                    new GDictionary
                    {
                        ["objective_id"] = "defeat_enemy_once",
                        ["objective_type"] = QuestDef.OBJECTIVE_DEFEAT_ENEMY(),
                        ["target_id"] = "",
                        ["target_value"] = 1,
                    },
                },
                new GArray
                {
                    new GDictionary { ["reward_type"] = QuestDef.REWARD_GOLD(), ["amount"] = 120 },
                },
                new GStringNameArray { "contract", "bounty" }
            )
        );

        _register_achievement(
            _build_achievement(
                "settlement_wayfarer",
                "行路借火",
                "在据点完成一次事务，学会把旅途见闻整理成可反复回想的经验。",
                "settlement_action_completed",
                "",
                1,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_KNOWLEDGE_UNLOCK(),
                        "wayfarer_notes",
                        "旅途见闻",
                        1,
                        "据点经历转化成了可保留的见闻。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "enemy_defeated_apprentice",
                "开刃",
                "累计击倒 3 名敌人，开始掌握主动突进的节奏。",
                "enemy_defeated",
                "",
                3,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_SKILL_UNLOCK(),
                        "charge",
                        "冲锋",
                        1,
                        "连战后的脚步更敢向前。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "near_death_unbroken",
                "濒死未倒",
                "在生命低于三分之一时承受重击仍存活，证明自身已经能在生死边缘守住形神。",
                "near_death_unbroken_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "warrior_heavy_strike_practice",
                "重击热身",
                "累计施放 5 次重击，挥砍节奏进一步稳定。",
                "skill_used",
                "warrior_heavy_strike",
                5,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_SKILL_MASTERY(),
                        "warrior_heavy_strike",
                        "重击",
                        10,
                        "熟能生巧。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "profession_promoted_first",
                "迈向正职",
                "完成首次职业晋升，体魄和力量都得到巩固。",
                "profession_promoted",
                "",
                1,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_ATTRIBUTE_DELTA(),
                        UnitBaseAttributes.STRENGTH(),
                        "力量",
                        1,
                        "正式晋升让动作更加扎实。"
                    ),
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_ATTRIBUTE_DELTA(),
                        HpMax,
                        "生命上限",
                        5,
                        "长期训练开始反映到体魄上。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "skill_learned_guard_break",
                "添一门手段",
                "学会裂甲斩，开始愿意把近战手段拓展到不同战术用途。",
                "skill_learned",
                "warrior_guard_break",
                1,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_ATTRIBUTE_DELTA(),
                        UnitBaseAttributes.PERCEPTION(),
                        "感知",
                        1,
                        "换用不同兵器后，对出手距离和节奏的判断更敏锐。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "knowledge_learned_field_manual",
                "把见闻记下来",
                "学会《野外手册》，开始把零散经历整理成能反复调用的知识。",
                "knowledge_learned",
                "field_manual",
                1,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_ATTRIBUTE_DELTA(),
                        UnitBaseAttributes.WILLPOWER(),
                        "意志",
                        1,
                        "把经验写成规则后，行动会更有把握。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "skill_mastery_charge_stride",
                "冲锋起步",
                "累计获得 20 点冲锋熟练度，开始掌握直线突进的起手节奏。",
                "skill_mastery_gained",
                "charge",
                20,
                new GArray
                {
                    _build_achievement_reward(
                        AchievementRewardDef.TYPE_ATTRIBUTE_DELTA(),
                        UnitBaseAttributes.AGILITY(),
                        "敏捷",
                        1,
                        "反复练习冲锋后，脚步转换更利落。"
                    ),
                }
            )
        );

        _register_achievement(
            _build_achievement(
                "fortuna_guidance_true",
                "Fortuna Guidance I",
                "已被 Fortuna 标记后，再次对 elite 或 boss 触发一次劣势大成功。",
                "fortuna_guidance_true_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "fortuna_guidance_devout",
                "Fortuna Guidance II",
                "已信 Fortuna 的角色在低血且承受强 debuff 的逆境中活下来并赢下战斗。",
                "fortuna_guidance_devout_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "fortuna_guidance_exalted",
                "Fortuna Guidance III",
                "已信 Fortuna 的角色用高位威胁区间而非门骰，对 elite 或 boss 打出一次大成功。",
                "fortuna_guidance_exalted_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "fortuna_guidance_blessed",
                "Fortuna Guidance IV",
                "完成一个章节且无人永久死亡，并且该角色在本章内至少经历过一次 Fortuna 相关战斗事件。",
                "fortuna_guidance_blessed_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_true",
                "Misfortune Guidance I",
                "已被黑冕标记后，成功用 Misfortune 的封印链终结一次 elite 或 boss。",
                "misfortune_guidance_true_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_devout",
                "Misfortune Guidance II",
                "同一战斗内曾遭遇大失败或强 debuff，随后再用封印链赢下 elite 或 boss。",
                "misfortune_guidance_devout_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_exalted",
                "Misfortune Guidance III",
                "把同一战斗中未用完的 calamity 结算成 shard，并用固定黑冕材料打造第一件黑暗装备。",
                "misfortune_guidance_exalted_manual",
                "",
                1,
                new GArray()
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_blessed",
                "Misfortune Guidance IV",
                "用 doom_sentence 的宣判击杀完成一次 boss 终结。",
                "misfortune_guidance_blessed_manual",
                "",
                1,
                new GArray()
            )
        );
    }

    private AchievementDef _build_achievement(
        StringName achievementId,
        string displayName,
        string description,
        StringName eventType,
        StringName subjectId,
        int threshold,
        GArray rewards
    )
    {
        var achievement = new AchievementDef
        {
            achievement_id = achievementId,
            display_name = displayName,
            description = description,
            event_type = eventType,
            subject_id = subjectId,
            threshold = threshold,
        };
        foreach (AchievementRewardDef reward in GdInterop.ReadObjectItems<AchievementRewardDef>(rewards))
        {
            achievement.rewards.Add(reward);
        }
        return achievement;
    }

    private QuestDef _build_quest(
        StringName questId,
        string displayName,
        string description,
        StringName providerInteractionId,
        GArray objectiveDefs,
        GArray rewardEntries,
        GStringNameArray tags = null
    )
    {
        var questDef = new QuestDef
        {
            quest_id = questId,
            display_name = displayName,
            description = description,
            provider_interaction_id = providerInteractionId,
            tags = tags != null ? DuplicateStringNameArray(tags) : new GStringNameArray(),
        };
        foreach (GDictionary objectiveValue in GdInterop.ReadDictionaryItems(objectiveDefs))
        {
            questDef.objective_defs.Add(objectiveValue.Duplicate(true));
        }
        foreach (GDictionary rewardValue in GdInterop.ReadDictionaryItems(rewardEntries))
        {
            questDef.reward_entries.Add(rewardValue.Duplicate(true));
        }
        return questDef;
    }

    private AchievementRewardDef _build_achievement_reward(
        StringName rewardType,
        StringName targetId,
        string targetLabel,
        int amount,
        string reasonText = ""
    )
    {
        return new AchievementRewardDef
        {
            reward_type = rewardType,
            target_id = targetId,
            target_label = targetLabel,
            amount = amount,
            reason_text = reasonText,
        };
    }

    private void _register_achievement(AchievementDef achievementDef)
    {
        if (achievementDef == null || achievementDef.achievement_id == "")
        {
            _validationErrors.Add(
                "Encountered an achievement definition without an achievement_id."
            );
            return;
        }
        if (_achievementDefs.ContainsKey(achievementDef.achievement_id))
        {
            _validationErrors.Add(
                $"Duplicate achievement_id registered: {achievementDef.achievement_id}"
            );
            return;
        }
        _achievementDefs[achievementDef.achievement_id] = achievementDef;
    }

    private void _register_quest(QuestDef questDef)
    {
        if (questDef == null || questDef.quest_id == "")
        {
            _questRegistrationErrors.Add("Encountered a quest definition without a quest_id.");
            return;
        }
        if (_questDefs.ContainsKey(questDef.quest_id))
        {
            _questRegistrationErrors.Add($"Duplicate quest_id registered: {questDef.quest_id}");
            return;
        }
        _questDefs[questDef.quest_id] = questDef;
    }

    private void _append_race_phase2_errors(GStringArray errors, StringName raceId, RaceDef raceDef)
    {
        if (raceDef == null)
        {
            return;
        }
        string ownerLabel = $"Race {raceId}";
        _append_body_size_category_error(
            errors,
            ownerLabel,
            "body_size_category",
            raceDef.body_size_category,
            false
        );
        _append_damage_resistance_errors(errors, ownerLabel, raceDef.damage_resistances);
        _append_trait_reference_errors(errors, ownerLabel, raceDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            raceDef.racial_granted_skills,
            "race"
        );

        if (raceDef.age_profile_id != "" && !_ageProfileDefs.ContainsKey(raceDef.age_profile_id))
        {
            errors.Add($"{ownerLabel} references missing age_profile {raceDef.age_profile_id}.");
        }

        if (raceDef.default_subrace_id != "")
        {
            if (!_subraceDefs.ContainsKey(raceDef.default_subrace_id))
            {
                errors.Add(
                    $"{ownerLabel} references missing default_subrace {raceDef.default_subrace_id}."
                );
            }
            else if (!raceDef.subrace_ids.Contains(raceDef.default_subrace_id))
            {
                errors.Add(
                    $"{ownerLabel} default_subrace {raceDef.default_subrace_id} must be listed in subrace_ids."
                );
            }
        }

        foreach (StringName subraceId in raceDef.subrace_ids)
        {
            if (subraceId == "")
            {
                continue;
            }
            var subraceDef = GetObject<SubraceDef>(_subraceDefs, subraceId);
            if (subraceDef == null)
            {
                errors.Add($"{ownerLabel} references missing subrace {subraceId}.");
                continue;
            }
            if (subraceDef.parent_race_id != raceId)
            {
                errors.Add(
                    $"{ownerLabel} subrace {subraceId} parent_race_id must be {raceId}, got {subraceDef.parent_race_id}."
                );
            }
        }
    }

    private void _append_subrace_phase2_errors(
        GStringArray errors,
        StringName subraceId,
        SubraceDef subraceDef
    )
    {
        if (subraceDef == null)
        {
            return;
        }
        string ownerLabel = $"Subrace {subraceId}";
        _append_body_size_category_error(
            errors,
            ownerLabel,
            "body_size_category_override",
            subraceDef.body_size_category_override,
            true
        );
        _append_damage_resistance_errors(errors, ownerLabel, subraceDef.damage_resistances);
        _append_trait_reference_errors(errors, ownerLabel, subraceDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            subraceDef.racial_granted_skills,
            "subrace"
        );

        if (subraceDef.parent_race_id == "")
        {
            return;
        }
        var parentRace = GetObject<RaceDef>(_raceDefs, subraceDef.parent_race_id);
        if (parentRace == null)
        {
            errors.Add($"{ownerLabel} references missing parent_race {subraceDef.parent_race_id}.");
            return;
        }
        if (!parentRace.subrace_ids.Contains(subraceId))
        {
            errors.Add(
                $"{ownerLabel} parent_race {subraceDef.parent_race_id} must list this subrace in subrace_ids."
            );
        }
    }

    private void _append_race_trait_phase2_errors(
        GStringArray errors,
        StringName traitId,
        RaceTraitDef traitDef
    )
    {
        if (traitDef == null)
        {
            return;
        }
        StringName triggerType = traitDef.trigger_type;
        if (triggerType == "" || triggerType == TraitTriggerContentRules.TRIGGER_PASSIVE())
        {
            return;
        }
        if (!TraitTriggerContentRules.has_dispatch_for_trait_trigger(traitId, triggerType))
        {
            errors.Add(
                $"RaceTrait {traitId} trigger_type {triggerType} has no TraitTriggerHooks dispatch."
            );
        }
    }

    private void _append_age_profile_phase2_errors(
        GStringArray errors,
        StringName profileId,
        AgeProfileDef profileDef
    )
    {
        if (profileDef == null)
        {
            return;
        }
        string ownerLabel = $"AgeProfile {profileId}";
        if (profileDef.race_id != "")
        {
            var raceDef = GetObject<RaceDef>(_raceDefs, profileDef.race_id);
            if (raceDef == null)
            {
                errors.Add($"{ownerLabel} references missing race {profileDef.race_id}.");
            }
            else if (raceDef.age_profile_id != profileId)
            {
                errors.Add(
                    $"{ownerLabel} race {profileDef.race_id} must reference this profile as age_profile_id."
                );
            }
        }
        if (profileDef.stage_rules.Count == 0)
        {
            errors.Add($"{ownerLabel} must declare at least one stage_rules entry.");
        }

        GDictionary stageIds = _collect_age_profile_stage_ids(profileDef);
        foreach (StringName stageId in profileDef.creation_stage_ids)
        {
            if (stageId != "" && !stageIds.ContainsKey(stageId))
            {
                errors.Add($"{ownerLabel} creation_stage_ids references missing stage {stageId}.");
            }
        }
        foreach (var stageKeyValue in profileDef.default_age_by_stage.Keys)
        {
            StringName stageId = _strict_to_string_name(stageKeyValue);
            if (stageId != "" && !stageIds.ContainsKey(stageId))
            {
                errors.Add(
                    $"{ownerLabel} default_age_by_stage references missing stage {stageId}."
                );
            }
        }
        foreach (AgeStageRule stageRule in profileDef.stage_rules)
        {
            if (stageRule == null)
            {
                continue;
            }
            _append_trait_reference_errors(
                errors,
                $"{ownerLabel} stage {stageRule.stage_id}",
                stageRule.trait_ids,
                "trait_ids"
            );
        }
    }

    private void _append_bloodline_phase2_errors(
        GStringArray errors,
        StringName bloodlineId,
        BloodlineDef bloodlineDef
    )
    {
        if (bloodlineDef == null)
        {
            return;
        }
        string ownerLabel = $"Bloodline {bloodlineId}";
        _append_trait_reference_errors(errors, ownerLabel, bloodlineDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            bloodlineDef.racial_granted_skills,
            "bloodline"
        );
        foreach (StringName stageId in bloodlineDef.stage_ids)
        {
            if (stageId == "")
            {
                continue;
            }
            var stageDef = GetObject<BloodlineStageDef>(_bloodlineStageDefs, stageId);
            if (stageDef == null)
            {
                errors.Add($"{ownerLabel} references missing bloodline_stage {stageId}.");
                continue;
            }
            if (stageDef.bloodline_id != bloodlineId)
            {
                errors.Add(
                    $"{ownerLabel} stage {stageId} bloodline_id must be {bloodlineId}, got {stageDef.bloodline_id}."
                );
            }
        }
    }

    private void _append_bloodline_stage_phase2_errors(
        GStringArray errors,
        StringName stageId,
        BloodlineStageDef stageDef
    )
    {
        if (stageDef == null)
        {
            return;
        }
        string ownerLabel = $"BloodlineStage {stageId}";
        _append_trait_reference_errors(errors, ownerLabel, stageDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            stageDef.racial_granted_skills,
            "bloodline"
        );
        if (stageDef.bloodline_id == "")
        {
            return;
        }
        var bloodlineDef = GetObject<BloodlineDef>(_bloodlineDefs, stageDef.bloodline_id);
        if (bloodlineDef == null)
        {
            errors.Add($"{ownerLabel} references missing bloodline {stageDef.bloodline_id}.");
            return;
        }
        if (!bloodlineDef.stage_ids.Contains(stageId))
        {
            errors.Add(
                $"{ownerLabel} bloodline {stageDef.bloodline_id} must list this stage in stage_ids."
            );
        }
    }

    private void _append_ascension_phase2_errors(
        GStringArray errors,
        StringName ascensionId,
        AscensionDef ascensionDef
    )
    {
        if (ascensionDef == null)
        {
            return;
        }
        string ownerLabel = $"Ascension {ascensionId}";
        _append_trait_reference_errors(errors, ownerLabel, ascensionDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.racial_granted_skills,
            "ascension"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.allowed_race_ids,
            "allowed_race_ids",
            _raceDefs,
            "race"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.allowed_subrace_ids,
            "allowed_subrace_ids",
            _subraceDefs,
            "subrace"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.allowed_bloodline_ids,
            "allowed_bloodline_ids",
            _bloodlineDefs,
            "bloodline"
        );

        foreach (StringName stageId in ascensionDef.stage_ids)
        {
            if (stageId == "")
            {
                continue;
            }
            var stageDef = GetObject<AscensionStageDef>(_ascensionStageDefs, stageId);
            if (stageDef == null)
            {
                errors.Add($"{ownerLabel} references missing ascension_stage {stageId}.");
                continue;
            }
            if (stageDef.ascension_id != ascensionId)
            {
                errors.Add(
                    $"{ownerLabel} stage {stageId} ascension_id must be {ascensionId}, got {stageDef.ascension_id}."
                );
            }
        }
    }

    private void _append_ascension_stage_phase2_errors(
        GStringArray errors,
        StringName stageId,
        AscensionStageDef stageDef
    )
    {
        if (stageDef == null)
        {
            return;
        }
        string ownerLabel = $"AscensionStage {stageId}";
        _append_body_size_category_error(
            errors,
            ownerLabel,
            "body_size_category_override",
            stageDef.body_size_category_override,
            true
        );
        _append_trait_reference_errors(errors, ownerLabel, stageDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            stageDef.racial_granted_skills,
            "ascension"
        );
        if (stageDef.ascension_id == "")
        {
            return;
        }
        var ascensionDef = GetObject<AscensionDef>(_ascensionDefs, stageDef.ascension_id);
        if (ascensionDef == null)
        {
            errors.Add($"{ownerLabel} references missing ascension {stageDef.ascension_id}.");
            return;
        }
        if (!ascensionDef.stage_ids.Contains(stageId))
        {
            errors.Add(
                $"{ownerLabel} ascension {stageDef.ascension_id} must list this stage in stage_ids."
            );
        }
    }

    private void _append_stage_advancement_phase2_errors(
        GStringArray errors,
        StringName modifierId,
        StageAdvancementModifier modifier
    )
    {
        if (modifier == null)
        {
            return;
        }
        string ownerLabel = $"StageAdvancement {modifierId}";
        if (!StageAdvancementModifier.VALID_TARGET_AXES().Contains(modifier.target_axis))
        {
            errors.Add($"{ownerLabel} uses unsupported target_axis {modifier.target_axis}.");
        }
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_race_ids,
            "applies_to_race_ids",
            _raceDefs,
            "race"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_subrace_ids,
            "applies_to_subrace_ids",
            _subraceDefs,
            "subrace"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_bloodline_ids,
            "applies_to_bloodline_ids",
            _bloodlineDefs,
            "bloodline"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_ascension_ids,
            "applies_to_ascension_ids",
            _ascensionDefs,
            "ascension"
        );
        _append_stage_advancement_max_stage_error(errors, ownerLabel, modifier);
    }

    private void _append_stage_advancement_max_stage_error(
        GStringArray errors,
        string ownerLabel,
        StageAdvancementModifier modifier
    )
    {
        if (modifier.max_stage_id == "")
        {
            return;
        }
        if (modifier.target_axis == StageAdvancementModifier.TARGET_AXIS_BLOODLINE())
        {
            if (!_bloodlineStageDefs.ContainsKey(modifier.max_stage_id))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing bloodline_stage {modifier.max_stage_id}."
                );
            }
        }
        else if (modifier.target_axis == StageAdvancementModifier.TARGET_AXIS_DIVINE())
        {
            if (!_ascensionStageDefs.ContainsKey(modifier.max_stage_id))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing ascension_stage {modifier.max_stage_id}."
                );
            }
        }
        else
        {
            GDictionary knownStageIds = _collect_known_identity_stage_ids();
            if (!knownStageIds.ContainsKey(modifier.max_stage_id))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing stage {modifier.max_stage_id}."
                );
            }
        }
    }

    private void _append_global_stage_id_errors(GStringArray errors)
    {
        var stageSources = new GDictionary();
        foreach (string stageKey in ProgressionDataUtils.sorted_string_keys(_bloodlineStageDefs))
        {
            _append_global_stage_id(
                errors,
                stageSources,
                new StringName(stageKey),
                "bloodline_stage"
            );
        }
        foreach (string stageKey in ProgressionDataUtils.sorted_string_keys(_ascensionStageDefs))
        {
            _append_global_stage_id(
                errors,
                stageSources,
                new StringName(stageKey),
                "ascension_stage"
            );
        }
    }

    private static void _append_global_stage_id(
        GStringArray errors,
        GDictionary stageSources,
        StringName stageId,
        string stageSource
    )
    {
        if (stageId == "")
        {
            return;
        }
        if (stageSources.ContainsKey(stageId))
        {
            errors.Add(
                $"Stage id {stageId} must be globally unique across bloodline_stage and ascension_stage; declared by {stageSources[stageId]} and {stageSource}."
            );
            return;
        }
        stageSources[stageId] = stageSource;
    }

    private void _append_trait_reference_errors(
        GStringArray errors,
        string ownerLabel,
        GStringNameArray traitIds,
        string fieldLabel
    )
    {
        foreach (StringName traitId in traitIds)
        {
            if (traitId == "")
            {
                continue;
            }
            if (!_raceTraitDefs.ContainsKey(traitId))
            {
                errors.Add($"{ownerLabel} {fieldLabel} references missing trait {traitId}.");
            }
        }
    }

    private void _append_racial_granted_skill_reference_errors(
        GStringArray errors,
        string ownerLabel,
        System.Collections.IEnumerable grantedSkills,
        StringName expectedLearnSource
    )
    {
        int index = 0;
        foreach (Resource grantedSkillValue in grantedSkills)
        {
            var grantedSkill = grantedSkillValue as RacialGrantedSkill;
            if (grantedSkill == null || grantedSkill.skill_id == "")
            {
                index++;
                continue;
            }
            var skillDef = GetObject<SkillDef>(_skillDefs, grantedSkill.skill_id);
            if (skillDef == null)
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] references missing skill {grantedSkill.skill_id}."
                );
                index++;
                continue;
            }
            if (skillDef.learn_source != expectedLearnSource)
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] skill {grantedSkill.skill_id} learn_source must be {expectedLearnSource}, got {skillDef.learn_source}."
                );
            }
            if (grantedSkill.minimum_skill_level > skillDef.max_level)
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] skill {grantedSkill.skill_id} minimum_skill_level must be <= max_level {skillDef.max_level}."
                );
            }
            index++;
        }
    }

    private static void _append_id_reference_errors(
        GStringArray errors,
        string ownerLabel,
        GStringNameArray values,
        string fieldLabel,
        GDictionary targetDefs,
        string targetLabel
    )
    {
        foreach (StringName valueId in values)
        {
            if (valueId == "")
            {
                continue;
            }
            if (!targetDefs.ContainsKey(valueId))
            {
                errors.Add(
                    $"{ownerLabel} {fieldLabel} references missing {targetLabel} {valueId}."
                );
            }
        }
    }

    private static void _append_damage_resistance_errors(
        GStringArray errors,
        string ownerLabel,
        GDictionary damageResistances
    )
    {
        foreach (var keyValue in damageResistances.Keys)
        {
            StringName damageTag = _strict_to_string_name(keyValue);
            if (damageTag == "")
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances key {keyValue} must be a non-empty String or StringName."
                );
                continue;
            }
            if (!DamageTagContentRules.is_valid_damage_tag(damageTag))
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances references unsupported damage tag {damageTag}."
                );
            }
            StringName mitigationTier = _strict_to_string_name(damageResistances[keyValue]);
            if (mitigationTier == "")
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances[{damageTag}] must be a non-empty String or StringName."
                );
                continue;
            }
            if (!DamageTagContentRules.is_valid_mitigation_tier(mitigationTier))
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances[{damageTag}] uses unsupported mitigation tier {mitigationTier}."
                );
            }
        }
    }

    private static void _append_body_size_category_error(
        GStringArray errors,
        string ownerLabel,
        string fieldLabel,
        StringName category,
        bool allowEmpty
    )
    {
        if (category == "")
        {
            if (!allowEmpty)
            {
                errors.Add($"{ownerLabel} {fieldLabel} must be a non-empty body_size_category.");
            }
            return;
        }
        if (!BodySizeContentRules.is_valid_body_size_category(category))
        {
            errors.Add(
                $"{ownerLabel} {fieldLabel} uses unsupported body_size_category {category}."
            );
        }
    }

    private static GDictionary _collect_age_profile_stage_ids(AgeProfileDef profileDef)
    {
        var stageIds = new GDictionary();
        if (profileDef == null)
        {
            return stageIds;
        }
        foreach (AgeStageRule stageRule in profileDef.stage_rules)
        {
            if (stageRule != null && stageRule.stage_id != "")
            {
                stageIds[stageRule.stage_id] = true;
            }
        }
        return stageIds;
    }

    private GDictionary _collect_known_identity_stage_ids()
    {
        var stageIds = new GDictionary();
        foreach (string profileKey in ProgressionDataUtils.sorted_string_keys(_ageProfileDefs))
        {
            var profileDef = GetObject<AgeProfileDef>(_ageProfileDefs, new StringName(profileKey));
            foreach (var stageId in _collect_age_profile_stage_ids(profileDef).Keys)
            {
                stageIds[stageId] = true;
            }
        }
        foreach (string stageKey in ProgressionDataUtils.sorted_string_keys(_bloodlineStageDefs))
        {
            stageIds[new StringName(stageKey)] = true;
        }
        foreach (string stageKey in ProgressionDataUtils.sorted_string_keys(_ascensionStageDefs))
        {
            stageIds[new StringName(stageKey)] = true;
        }
        return stageIds;
    }

    private static StringName _strict_to_string_name(object rawValue)
    {
        StringName normalized = GdInterop.ToStringName(rawValue);
        string normalizedText = normalized.ToString().StripEdges();
        return string.IsNullOrEmpty(normalizedText) ? new StringName("") : new StringName(normalizedText);
    }

    private static bool HasDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return false;
        }
        return GdInterop.HasDictionary(source, key);
    }

    private void _append_invalid_skill_errors(
        GStringArray errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef == null)
        {
            return;
        }
        if (!ValidSkillTypes.Contains(skillDef.skill_type))
        {
            errors.Add($"Skill {skillId} uses unsupported skill_type {skillDef.skill_type}.");
        }
        if (!ValidLearnSources.Contains(skillDef.learn_source))
        {
            errors.Add($"Skill {skillId} uses unsupported learn_source {skillDef.learn_source}.");
        }
        if (!ValidUnlockModes.Contains(skillDef.unlock_mode))
        {
            errors.Add($"Skill {skillId} uses unsupported unlock_mode {skillDef.unlock_mode}.");
        }
        if (!ValidCoreSkillTransitionModes.Contains(skillDef.core_skill_transition_mode))
        {
            errors.Add(
                $"Skill {skillId} uses unsupported core_skill_transition_mode {skillDef.core_skill_transition_mode}."
            );
        }
        if (skillDef.max_level < 0 && skillDef.dynamic_max_level_stat_id == "")
        {
            errors.Add($"Skill {skillId} must have max_level >= 0.");
        }
        if (skillDef.non_core_max_level < 0)
        {
            errors.Add($"Skill {skillId} non_core_max_level must be >= 0.");
        }
        if (
            skillDef.non_core_max_level > skillDef.max_level
            && skillDef.max_level >= 0
            && skillDef.dynamic_max_level_stat_id == ""
        )
        {
            errors.Add($"Skill {skillId} non_core_max_level must be <= max_level.");
        }
        if (
            skillDef.mastery_curve.Length != skillDef.max_level
            && skillDef.max_level >= 0
            && skillDef.dynamic_max_level_stat_id == ""
        )
        {
            errors.Add($"Skill {skillId} mastery_curve size must match max_level.");
        }
        _append_dynamic_max_level_errors(errors, skillId, skillDef);
        _append_practice_skill_errors(errors, skillId, skillDef);
        _append_skill_attribute_growth_errors(errors, skillId, skillDef);
        _append_skill_requirement_errors(
            errors,
            skillId,
            skillDef.learn_requirements,
            "learn_requirements"
        );
        _append_skill_level_requirement_errors(errors, skillId, skillDef.skill_level_requirements);
        _append_attribute_requirement_errors(errors, skillId, skillDef.attribute_requirements);
        _append_skill_requirement_errors(
            errors,
            skillId,
            skillDef.upgrade_source_skill_ids,
            "upgrade_source_skill_ids"
        );
        foreach (StringName achievementId in skillDef.achievement_requirements)
        {
            if (achievementId == "")
            {
                errors.Add($"Skill {skillId} has an empty achievement requirement.");
            }
        }
        if (
            skillDef.unlock_mode == UnlockModeCompositeUpgrade
            && skillDef.upgrade_source_skill_ids.Count == 0
        )
        {
            errors.Add(
                $"Skill {skillId} is composite_upgrade but missing upgrade_source_skill_ids."
            );
        }
    }

    private static void _append_practice_skill_errors(
        GStringArray errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        int trackCount = 0;
        foreach (StringName trackTag in PracticeTrackTags)
        {
            if (skillDef.tags.Contains(trackTag))
            {
                trackCount++;
            }
        }
        if (trackCount == 0)
        {
            if (skillDef.practice_tier != "")
            {
                errors.Add(
                    $"Skill {skillId} practice_tier requires meditation or cultivation tag."
                );
            }
            return;
        }
        if (trackCount != 1)
        {
            errors.Add($"Skill {skillId} must use exactly one practice track tag.");
        }
        if (skillDef.tags.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} practice tags must be exclusive; tags must contain only meditation or cultivation."
            );
        }
        if (!ValidPracticeTiers.Contains(skillDef.practice_tier))
        {
            errors.Add(
                $"Skill {skillId} practice_tier must be one of basic, intermediate, advanced, ultimate."
            );
        }
    }

    private static void _append_dynamic_max_level_errors(
        GStringArray errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        bool hasDynamicStat = skillDef.dynamic_max_level_stat_id != "";
        if (!hasDynamicStat)
        {
            if (skillDef.dynamic_max_level_base != 0)
            {
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_base requires dynamic_max_level_stat_id."
                );
            }
            if (skillDef.dynamic_max_level_per_stat != 0)
            {
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_per_stat requires dynamic_max_level_stat_id."
                );
            }
            return;
        }
        if (skillDef.dynamic_max_level_base <= 0)
        {
            errors.Add($"Skill {skillId} dynamic_max_level_base must be >= 1.");
        }
        if (skillDef.dynamic_max_level_per_stat == 0)
        {
            errors.Add(
                $"Skill {skillId} dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set."
            );
        }
    }

    private static void _append_skill_attribute_growth_errors(
        GStringArray errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef.attribute_growth_progress.Count == 0 && skillDef.growth_tier == "")
        {
            return;
        }
        if (!AttributeGrowthContentRules.is_valid_growth_tier(skillDef.growth_tier))
        {
            errors.Add($"Skill {skillId} uses unsupported growth_tier {skillDef.growth_tier}.");
            return;
        }
        int progressTotal = 0;
        foreach (var attributeKey in skillDef.attribute_growth_progress.Keys)
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(attributeKey);
            int amount = skillDef.attribute_growth_progress[attributeKey].AsInt32();
            if (!AttributeGrowthContentRules.is_valid_attribute_id(attributeId))
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress references invalid attribute {attributeId}."
                );
            }
            if (amount <= 0)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be > 0."
                );
            }
            progressTotal += amount;
        }
        int expectedTotal = AttributeGrowthContentRules.get_tier_budget(skillDef.growth_tier);
        if (progressTotal != expectedTotal)
        {
            errors.Add(
                $"Skill {skillId} attribute_growth_progress total must equal {expectedTotal} for growth_tier {skillDef.growth_tier}."
            );
        }
    }

    private void _append_skill_requirement_errors(
        GStringArray errors,
        StringName skillId,
        GStringNameArray requirementIds,
        string contextLabel
    )
    {
        foreach (StringName requiredSkillId in requirementIds)
        {
            if (requiredSkillId == "")
            {
                errors.Add($"Skill {skillId} has an empty skill reference in {contextLabel}.");
                continue;
            }
            if (!_skillDefs.ContainsKey(requiredSkillId))
            {
                errors.Add(
                    $"Skill {skillId} references missing skill {requiredSkillId} in {contextLabel}."
                );
            }
        }
    }

    private void _append_skill_level_requirement_errors(
        GStringArray errors,
        StringName skillId,
        GDictionary skillLevelRequirements
    )
    {
        foreach (var skillKeyValue in skillLevelRequirements.Keys)
        {
            StringName requiredSkillId = ProgressionDataUtils.to_string_name(skillKeyValue);
            if (requiredSkillId == "")
            {
                errors.Add($"Skill {skillId} has an empty skill_id in skill_level_requirements.");
                continue;
            }
            if (!_skillDefs.ContainsKey(requiredSkillId))
            {
                errors.Add(
                    $"Skill {skillId} references missing skill {requiredSkillId} in skill_level_requirements."
                );
            }
            int requiredLevel = skillLevelRequirements[skillKeyValue].AsInt32();
            if (requiredLevel <= 0)
            {
                errors.Add(
                    $"Skill {skillId} requires non-positive level {requiredLevel} for {requiredSkillId} in skill_level_requirements."
                );
            }
        }
    }

    private static void _append_attribute_requirement_errors(
        GStringArray errors,
        StringName skillId,
        GDictionary attributeRequirements
    )
    {
        foreach (var attributeKeyValue in attributeRequirements.Keys)
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(attributeKeyValue);
            if (attributeId == "")
            {
                errors.Add($"Skill {skillId} has an empty attribute_id in attribute_requirements.");
                continue;
            }
            if (!UnitBaseAttributes.get_all_base_attribute_ids().Contains(attributeId))
            {
                errors.Add(
                    $"Skill {skillId} references unsupported attribute {attributeId} in attribute_requirements."
                );
            }
            int requiredValue = attributeRequirements[attributeKeyValue].AsInt32();
            if (requiredValue <= 0)
            {
                errors.Add(
                    $"Skill {skillId} requires non-positive value {requiredValue} for {attributeId} in attribute_requirements."
                );
            }
        }
    }

    private void _append_invalid_achievement_errors(
        GStringArray errors,
        StringName achievementId,
        AchievementDef achievementDef
    )
    {
        if (achievementDef == null)
        {
            return;
        }
        if (achievementDef.event_type == "")
        {
            errors.Add($"Achievement {achievementId} is missing event_type.");
        }
        if (achievementDef.threshold <= 0)
        {
            errors.Add($"Achievement {achievementId} must have a positive threshold.");
        }
        foreach (AchievementRewardDef reward in achievementDef.rewards)
        {
            if (reward == null)
                continue;
            if (reward.reward_type == "")
            {
                errors.Add($"Achievement {achievementId} has a reward without reward_type.");
            }
            if (reward.target_id == "")
            {
                errors.Add($"Achievement {achievementId} has a reward without target_id.");
            }
            if (reward.amount == 0)
            {
                errors.Add(
                    $"Achievement {achievementId} has a zero-amount reward for {reward.target_id}."
                );
            }
            if (
                reward.reward_type != ""
                && !PendingCharacterRewardContentRules.is_supported_entry_type(reward.reward_type)
            )
            {
                errors.Add(
                    $"Achievement {achievementId} uses unsupported reward_type {reward.reward_type}."
                );
                continue;
            }
            if (
                reward.reward_type == AchievementRewardDef.TYPE_SKILL_UNLOCK()
                || reward.reward_type == AchievementRewardDef.TYPE_SKILL_MASTERY()
            )
            {
                if (!_skillDefs.ContainsKey(reward.target_id))
                {
                    errors.Add(
                        $"Achievement {achievementId} references missing skill {reward.target_id}."
                    );
                }
            }
            else if (reward.reward_type == "attribute_progress")
            {
                if (
                    !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(
                        reward.target_id
                    )
                )
                {
                    errors.Add(
                        $"Achievement {achievementId} attribute_progress reward references unsupported attribute {reward.target_id}."
                    );
                }
            }
        }
    }

    private static GDictionary DuplicateDictionary(GDictionary source)
    {
        return source != null ? source.Duplicate() : new GDictionary();
    }

    private static GStringArray DuplicateStringArray(GStringArray source)
    {
        var result = new GStringArray();
        if (source == null)
        {
            return result;
        }
        foreach (string value in source)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray DuplicateStringNameArray(GStringNameArray source)
    {
        var result = new GStringNameArray();
        if (source == null)
        {
            return result;
        }
        foreach (StringName value in source)
        {
            result.Add(value);
        }
        return result;
    }

    private static void AppendArray(GStringArray target, GStringArray source)
    {
        if (source == null)
        {
            return;
        }
        foreach (string value in source)
        {
            target.Add(value);
        }
    }

    private static void AppendUniqueErrors(GStringArray target, GStringArray source)
    {
        if (source == null)
        {
            return;
        }
        foreach (string value in source)
        {
            if (!target.Contains(value))
            {
                target.Add(value);
            }
        }
    }

    private static GDictionary GetDictionary(GDictionary source, string key)
    {
        if (
            source == null
            || !source.ContainsKey(key)
            || !HasDictionary(source, key)
        )
        {
            return new GDictionary();
        }
        return GdInterop.GetDictionary(source, key);
    }

    private static T GetObject<T>(GDictionary source, StringName key)
        where T : class
    {
        if (source == null || !source.ContainsKey(key))
        {
            return null;
        }
        return GdInterop.GetObject(source, key) as T;
    }
}
