using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_enemy_content_registry_typed_regression : LifecycleTestSceneTree
{
    private const string OfficialSeedPath = "res://data/configs/enemies/enemy_content_seed.tres";
    private const string InvalidReferenceSeedPath =
        "res://tests/fixtures/enemy_content/invalid_roster/enemy_content_seed.tres";
    private const string TargetModeMismatchSeedPath =
        "res://tests/synthetic/enemy_action_target_mode_mismatch.tres";
    private const string SkillLevelMismatchSeedPath =
        "res://tests/synthetic/enemy_action_skill_level_mismatch.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestProcessSnapshotPublishesImmutablePlainEnemyDefinitions();
        TestOfficialSharedWolfEncounterStages();
        TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed();
        TestRegistryRejectsActionSkillTargetModeMismatch();
        TestRegistryRejectsTemplateLevelWithoutCompatibleCastOption();

        RequestTestExit(_test.Finish("Enemy content registry typed regression"));
    }

    private void TestProcessSnapshotPublishesImmutablePlainEnemyDefinitions()
    {
        ProcessContentHost host = Root
            .GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator")
            .ContentHost;
        ContentSnapshot snapshot = host.GetSnapshot();

        _test.True(snapshot.EnemyTemplates.ContainsKey("wolf_raider"), "snapshot 应发布正式敌人模板定义。");
        _test.True(snapshot.EnemyBrains.ContainsKey("melee_aggressor"), "snapshot 应发布正式 AI brain 定义。");
        _test.True(snapshot.EncounterRosters.ContainsKey("wolf_den"), "snapshot 应发布正式 encounter roster 定义。");
        _test.Eq(snapshot.BattleSimProfiles.Count, 4, "snapshot 应一次投影四个正式 BattleSim profile。");

        EnemyTemplateDefinition template = snapshot.EnemyTemplates["wolf_raider"];
        using TestContentResourceLoader loader = new();
        using EnemyContentRegistry rawRegistry = new(loader);
        EnemyTemplateDef rawTemplate = rawRegistry.GetEnemyTemplatesTyped()["wolf_raider"];
        int rawTagCount = rawTemplate.tags.Count;
        int rawDropCount = rawTemplate.drop_entries.Count;
        EnemyTemplateDefinition repeatedProjection = rawTemplate.ToDefinition(snapshot.Items);
        _test.Eq(template.TemplateId, new StringName("wolf_raider"), "模板 key/value id 应保持一致。");
        _test.Eq(rawTemplate.tags.Count, rawTagCount, "definition projection 不得修改 source tags。");
        _test.Eq(rawTemplate.drop_entries.Count, rawDropCount, "definition projection 不得修改 source drops。");
        _test.Eq(repeatedProjection.TemplateId, template.TemplateId, "重复投影应保持字段稳定。");
        _test.Eq(
            template.BattleSpriteTexturePath,
            string.IsNullOrWhiteSpace(rawTemplate.battle_sprite_texture?.ResourcePath)
                ? ""
                : ContentPathCanonicalizer.Canonicalize(
                    rawTemplate.battle_sprite_texture.ResourcePath
                ),
            "texture wrapper 必须投影为资源路径。"
        );
        _test.True(
            template.Tags is not Godot.Collections.Array<StringName>,
            "模板列表必须是只读 CLR collection。"
        );
        _test.True(
            Throws<NotSupportedException>(() =>
                ((IDictionary<StringName, EnemyTemplateDefinition>)snapshot.EnemyTemplates).Add(
                    "forbidden",
                    template
                )
            ),
            "enemy snapshot dictionary 应拒绝修改。"
        );
        _test.True(
            Throws<NotSupportedException>(() =>
                ((IList<StringName>)template.Tags).Add("forbidden")
            ),
            "enemy template nested list 应拒绝修改。"
        );

        foreach (
            Type rootType in new[]
            {
                typeof(EnemyTemplateDefinition),
                typeof(EnemyAiBrainDefinition),
                typeof(WildEncounterRosterDefinition),
                typeof(BattleAiScoreProfileDefinition),
            }
        )
        {
            foreach (Type type in EnumerateTypeGraph(rootType))
            {
                _test.False(
                    typeof(GodotObject).IsAssignableFrom(type),
                    $"definition graph 不得包含 GodotObject: {rootType.Name} -> {type.FullName}"
                );
                _test.False(
                    type.Namespace?.StartsWith("Godot.Collections", StringComparison.Ordinal) == true,
                    $"definition graph 不得包含 Godot collection: {rootType.Name} -> {type.FullName}"
                );
                _test.False(
                    type == typeof(Variant),
                    $"definition graph 不得包含 Variant: {rootType.Name}"
                );
            }
        }
    }

    private void TestOfficialSharedWolfEncounterStages()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        bool hasSharedWolfRoster = snapshot.EncounterRosters.TryGetValue(
            "wolf_pack_skirmish",
            out WildEncounterRosterDefinition sharedWolfRoster
        );
        bool hasSharedWolfEncounter = snapshot.BattleEncounters.TryGetValue(
            "wolf_wilds",
            out BattleEncounterDefinition sharedWolfEncounter
        );

        _test.False(
            snapshot.EncounterRosters.ContainsKey("folk_farmers_plea_wolves"),
            "《农夫的恳求》不得复制一份任务专属荒狼 roster。"
        );
        _test.False(
            snapshot.BattleEncounters.ContainsKey("folk_farmers_plea_wolves"),
            "《农夫的恳求》不得复制一份任务专属 battle encounter。"
        );
        _test.True(hasSharedWolfRoster, "正式 enemy snapshot 应发布共享荒狼 roster。");
        _test.True(hasSharedWolfEncounter, "正式 battle snapshot 应发布共享 wolf_wilds 遭遇。");
        if (!hasSharedWolfRoster || !hasSharedWolfEncounter)
            return;

        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> ordinaryWolfEntries =
            sharedWolfRoster.GetStageUnitEntries(0);
        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> fiveWolfEntries =
            sharedWolfRoster.GetStageUnitEntries(1);
        WildEncounterRosterStageDefinition mixedWolfStage = null;
        foreach (WildEncounterRosterStageDefinition stage in sharedWolfRoster.Stages)
        {
            if (stage?.Stage == 2)
            {
                mixedWolfStage = stage;
                break;
            }
        }
        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> mixedWolfEntries =
            mixedWolfStage?.UnitEntries
            ?? Array.Empty<WildEncounterRosterUnitEntryDefinition>();
        bool hasRegularWolfTemplate = snapshot.EnemyTemplates.TryGetValue(
            "wolf_pack",
            out EnemyTemplateDefinition regularWolfTemplate
        );
        bool hasWorgTemplate = snapshot.EnemyTemplates.TryGetValue(
            "worg",
            out EnemyTemplateDefinition worgTemplate
        );

        _test.Eq(
            sharedWolfEncounter.RosterProfileId,
            new StringName("wolf_pack_skirmish"),
            "wolf_wilds 应继续引用同一份共享荒狼 roster。"
        );
        _test.Eq(sharedWolfRoster.GetMaxStage(), 2, "共享荒狼 roster 应正式声明到 stage 2。");
        _test.Eq(sharedWolfRoster.Stages.Count, 3, "共享荒狼 roster 应恰好声明三个阶段。");
        _test.True(mixedWolfStage != null, "共享荒狼 roster 必须精确声明 stage 2，不能依赖阶段回退。");
        _test.Eq(ordinaryWolfEntries.Count, 1, "共享荒狼 roster stage 0 应只有一类敌人。");
        _test.Eq(fiveWolfEntries.Count, 1, "共享荒狼 roster stage 1 应只有一类敌人。");
        _test.Eq(mixedWolfEntries.Count, 2, "共享荒狼 roster stage 2 应有常规狼和座狼两类敌人。");
        _test.True(hasRegularWolfTemplate, "正式 enemy snapshot 应发布常规狼模板。");
        _test.True(hasWorgTemplate, "正式 enemy snapshot 应发布座狼模板。");
        if (
            ordinaryWolfEntries.Count != 1
            || fiveWolfEntries.Count != 1
            || mixedWolfEntries.Count != 2
            || !hasRegularWolfTemplate
            || !hasWorgTemplate
        )
            return;

        _test.Eq(
            ordinaryWolfEntries[0].TemplateId,
            new StringName("wolf_pack"),
            "共享荒狼 roster stage 0 应使用正式 wolf_pack 模板。"
        );
        _test.Eq(
            ordinaryWolfEntries[0].Count,
            2,
            "普通 wolf_wilds 锚点应继续使用共享 roster 的两狼阶段。"
        );
        _test.Eq(
            fiveWolfEntries[0].TemplateId,
            new StringName("wolf_pack"),
            "共享荒狼 roster stage 1 应继续使用同一正式 wolf_pack 模板。"
        );
        _test.Eq(
            fiveWolfEntries[0].Count,
            5,
            "共享 wolf_wilds 遭遇的 stage 1 应在同一场战斗生成五只荒狼。"
        );
        _test.Eq(
            mixedWolfEntries[0].TemplateId,
            new StringName("wolf_pack"),
            "共享荒狼 roster stage 2 的第一类敌人应为常规狼。"
        );
        _test.Eq(
            mixedWolfEntries[0].Count,
            3,
            "共享荒狼 roster stage 2 应生成三只常规狼。"
        );
        _test.Eq(
            mixedWolfEntries[1].TemplateId,
            new StringName("worg"),
            "共享荒狼 roster stage 2 的第二类敌人应为座狼。"
        );
        _test.Eq(
            mixedWolfEntries[1].Count,
            2,
            "共享荒狼 roster stage 2 应生成两只座狼。"
        );
        _test.Eq(
            mixedWolfEntries[0].Count + mixedWolfEntries[1].Count,
            5,
            "共享荒狼 roster stage 2 总数应保持五只狼。"
        );
        _test.Eq(regularWolfTemplate.CreatureLevel, 0, "常规狼模板应保持 0 级。");
        _test.Eq(worgTemplate.CreatureLevel, 1, "座狼模板应修正为 1 级。");
        _test.Eq(regularWolfTemplate.SkillIds.Count, 1, "常规狼模板应只配置一个技能。");
        if (regularWolfTemplate.SkillIds.Count == 1)
        {
            _test.Eq(
                regularWolfTemplate.SkillIds[0],
                new StringName("basic_attack"),
                "常规狼模板唯一技能应为基础攻击。"
            );
        }
        _test.Eq(worgTemplate.SkillIds.Count, 2, "座狼模板应配置基础攻击和冲锋两个技能。");
        if (worgTemplate.SkillIds.Count == 2)
        {
            _test.Eq(
                worgTemplate.SkillIds[0],
                new StringName("basic_attack"),
                "座狼模板应保留基础攻击。"
            );
            _test.Eq(
                worgTemplate.SkillIds[1],
                new StringName("charge"),
                "座狼模板应允许使用冲锋。"
            );
        }
    }

    private void TestRebuildClearsOfficialCatalogBeforeLoadingInvalidSeed()
    {
        using TestContentResourceLoader loader = new();
        using EnemyContentRegistry registry = new(loader);
        registry.ConfigureSeedResource(OfficialSeedPath, true, true);
        registry.ConfigureSeedResource(InvalidReferenceSeedPath, true, false);

        IReadOnlyDictionary<StringName, EnemyAiBrainDef> typedBrains = registry.GetEnemyAiBrainsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> typedTemplates = registry.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> typedRosters =
            registry.GetWildEncounterRostersTyped();
        IReadOnlyList<string> typedErrors = registry.ValidateTyped();
        GStringArray errors = registry.Validate();

        _test.Eq(
            typedErrors.Count,
            errors.Count,
            "invalid seed 下 enemy registry typed/public validation error 数量应保持一致。"
        );
        _test.Eq(typedBrains.Count, 0, "invalid roster fixture 不应残留上一轮官方 brain catalog。");
        _test.Eq(typedTemplates.Count, 0, "invalid roster fixture 不应残留上一轮官方 template catalog。");
        _test.Eq(typedRosters.Count, 1, "invalid roster fixture 应只注册自身 roster。");
        _test.True(
            typedRosters.ContainsKey("invalid_roster"),
            "invalid roster fixture 应保留 invalid_roster typed key。"
        );
        _test.True(
            errors.Count > 0,
            $"invalid roster fixture 应稳定报告缺失 template。 errors={FormatErrors(errors)}"
        );
    }

    private void TestRegistryRejectsActionSkillTargetModeMismatch()
    {
        SkillDefinition groundSkill = TestSkillDefinitionProjection.BuildSkill(
            "fixture_ground_skill",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "fixture_ground_skill",
                targetMode: "ground",
                targetSelectionMode: "single_coord"
            )
        );
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [groundSkill.SkillId] = groundSkill,
        };

        UseGroundSkillAction validGroundAction = TestResourceOwnership.Own(
            new UseGroundSkillAction
            {
                action_id = "valid_ground_action",
                desired_min_distance = 1,
                desired_max_distance = 1,
                DistanceReferenceKind = EnemyAiDistanceReference.TargetCoord,
            },
            "enemy_action_target_mode.valid_ground_action"
        );
        validGroundAction.skill_ids.Add(groundSkill.SkillId);

        UseUnitSkillAction invalidUnitAction = TestResourceOwnership.Own(
            new UseUnitSkillAction
            {
                action_id = "invalid_unit_action",
                desired_min_distance = 1,
                desired_max_distance = 1,
                DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
            },
            "enemy_action_target_mode.invalid_unit_action"
        );
        invalidUnitAction.skill_ids.Add(groundSkill.SkillId);

        EnemyAiStateDef state = TestResourceOwnership.Own(
            new EnemyAiStateDef
            {
                state_id = "engage",
            },
            "enemy_action_target_mode.state"
        );
        state.actions.Add(validGroundAction);
        state.actions.Add(invalidUnitAction);

        EnemyAiBrainDef brain = TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = "target_mode_mismatch_brain",
                default_state_id = state.state_id,
            },
            "enemy_action_target_mode.brain"
        );
        brain.states.Add(state);

        EnemyContentSeed seed = TestResourceOwnership.Own(
            new EnemyContentSeed(),
            "enemy_action_target_mode.seed"
        );
        seed.enemy_ai_brains.Add(brain);

        using TestContentResourceLoader loader = new();
        loader.RegisterCanonical(TargetModeMismatchSeedPath, seed);
        using EnemyContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.ConfigureSeedResource(
            TargetModeMismatchSeedPath,
            rebuildNow: false,
            validateSeedDirCompleteness: false
        );
        registry.Rebuild(
            new EnemyContentValidationContext(
                new Dictionary<StringName, ItemDefinition>(),
                skillDefinitions
            )
        );

        IReadOnlyList<string> errors = registry.ValidateTyped();
        _test.Eq(
            errors.Count,
            1,
            $"只有 unit action 引用 ground skill 应被拒绝: {FormatErrors(errors)}"
        );
        string error = errors.Count == 1 ? errors[0] : "";
        _test.True(
            error.Contains("invalid_unit_action", StringComparison.Ordinal)
                && error.Contains("fixture_ground_skill", StringComparison.Ordinal)
                && error.Contains("target_mode unit", StringComparison.Ordinal)
                && error.Contains("found ground", StringComparison.Ordinal),
            $"诊断应包含 action、skill 和期望/实际 target mode: {FormatErrors(errors)}"
        );
    }

    private void TestRegistryRejectsTemplateLevelWithoutCompatibleCastOption()
    {
        CombatEffectDefinition damageEffect =
            TestSkillDefinitionProjection.BuildEffect("damage");
        SkillDefinition highLevelVariantSkill =
            TestSkillDefinitionProjection.BuildSkill(
                "fixture_level_five_unit_skill",
                maxLevel: 5,
                combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                    "fixture_level_five_unit_skill",
                    targetMode: "unit",
                    castVariants: new[]
                    {
                        TestSkillDefinitionProjection.BuildCastVariant(
                            "level_five_unit",
                            5,
                            new[] { damageEffect },
                            targetMode: "unit",
                            footprintPattern: "single",
                            requiredCoordCount: 1
                        ),
                    }
                )
            );
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [highLevelVariantSkill.SkillId] = highLevelVariantSkill,
        };

        UseUnitSkillAction action = TestResourceOwnership.Own(
            new UseUnitSkillAction
            {
                action_id = "level_locked_unit_action",
                desired_min_distance = 1,
                desired_max_distance = 1,
                DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
            },
            "enemy_action_skill_level.action"
        );
        action.skill_ids.Add(highLevelVariantSkill.SkillId);

        EnemyAiStateDef state = TestResourceOwnership.Own(
            new EnemyAiStateDef
            {
                state_id = "engage",
            },
            "enemy_action_skill_level.state"
        );
        state.actions.Add(action);

        EnemyAiBrainDef brain = TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = "level_locked_brain",
                default_state_id = state.state_id,
            },
            "enemy_action_skill_level.brain"
        );
        brain.states.Add(state);

        EnemyTemplateDef template = TestResourceOwnership.Own(
            new EnemyTemplateDef
            {
                template_id = "level_one_enemy",
                display_name = "Level One Enemy",
                brain_id = brain.brain_id,
                cognition_kind = "sapient",
                initial_state_id = state.state_id,
            },
            "enemy_action_skill_level.template"
        );
        template.tags.Add("beast");
        template.skill_ids.Add(highLevelVariantSkill.SkillId);
        template.skill_level_map[highLevelVariantSkill.SkillId] = 1;
        foreach (
            StringName attributeId in new StringName[]
            {
                "strength",
                "agility",
                "constitution",
                "perception",
                "intelligence",
                "willpower",
            }
        )
        {
            template.base_attribute_overrides[attributeId] = 10;
        }

        EnemyContentSeed seed = TestResourceOwnership.Own(
            new EnemyContentSeed(),
            "enemy_action_skill_level.seed"
        );
        seed.enemy_ai_brains.Add(brain);
        seed.enemy_templates.Add(template);

        using TestContentResourceLoader loader = new();
        loader.RegisterCanonical(SkillLevelMismatchSeedPath, seed);
        using EnemyContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.ConfigureSeedResource(
            SkillLevelMismatchSeedPath,
            rebuildNow: false,
            validateSeedDirCompleteness: false
        );
        registry.Rebuild(
            new EnemyContentValidationContext(
                new Dictionary<StringName, ItemDefinition>(),
                skillDefinitions
            )
        );

        IReadOnlyList<string> errors = registry.ValidateTyped();
        _test.Eq(
            errors.Count,
            1,
            $"只有模板实际等级无法解锁 unit variant 应被拒绝: {FormatErrors(errors)}"
        );
        string error = errors.Count == 1 ? errors[0] : "";
        _test.True(
            error.Contains("level_one_enemy", StringComparison.Ordinal)
                && error.Contains("level_locked_unit_action", StringComparison.Ordinal)
                && error.Contains(
                    "fixture_level_five_unit_skill",
                    StringComparison.Ordinal
                )
                && error.Contains("at level 1", StringComparison.Ordinal)
                && error.Contains("at skill level 1", StringComparison.Ordinal),
            $"等级诊断应包含 template、action、skill 和实际等级: {FormatErrors(errors)}"
        );

        template.skill_level_map[highLevelVariantSkill.SkillId] = "invalid";
        registry.Rebuild(
            new EnemyContentValidationContext(
                new Dictionary<StringName, ItemDefinition>(),
                skillDefinitions
            )
        );

        IReadOnlyList<string> malformedLevelErrors = registry.ValidateTyped();
        _test.Eq(
            malformedLevelErrors.Count,
            1,
            $"无效等级值只应报告等级 schema 根因，不应再用回退等级制造兼容性误报: "
                + $"{FormatErrors(malformedLevelErrors)}"
        );
        string malformedLevelError =
            malformedLevelErrors.Count == 1 ? malformedLevelErrors[0] : "";
        _test.True(
            malformedLevelError.Contains(
                "skill_level_map[fixture_level_five_unit_skill] must be an int",
                StringComparison.Ordinal
            )
                && !malformedLevelError.Contains(
                    "incompatible skill",
                    StringComparison.Ordinal
                ),
            $"无效等级值应由 skill_level_map schema 独占诊断: {FormatErrors(malformedLevelErrors)}"
        );
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
        {
            values.Add(error ?? "");
        }
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static IEnumerable<Type> EnumerateTypeGraph(Type root)
    {
        var pending = new Stack<Type>();
        var visited = new HashSet<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Type current = pending.Pop();
            if (current == null || !visited.Add(current))
                continue;
            yield return current;
            if (current.IsArray)
                pending.Push(current.GetElementType());
            if (current.IsGenericType)
            {
                foreach (Type argument in current.GetGenericArguments())
                    pending.Push(argument);
            }
            foreach (
                PropertyInfo property in current.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                )
            )
            {
                if (property.DeclaringType == current)
                    pending.Push(property.PropertyType);
            }
        }
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

}
