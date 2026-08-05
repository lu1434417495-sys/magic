using System;
using System.Collections.Generic;
using Godot;

public partial class run_wolf_alpha_content_regression : LifecycleTestSceneTree
{
    private static readonly StringName WolfAlphaTemplateId = "wolf_alpha";
    private static readonly StringName HamstringSkillId = "wolf_alpha_hamstring_bite";
    private static readonly StringName HowlSkillId = "wolf_alpha_dominance_howl";
    private static readonly StringName CullSkillId = "wolf_alpha_cull_the_weak";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            TestTemplateContract(snapshot);
            TestSkillContracts(snapshot);
            TestGeneratedSkillLevels(snapshot);
            TestFormalEncounterGeneration(snapshot);
            TestPackLeaderBrain(snapshot);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Wolf alpha content regression"));
    }

    private void TestTemplateContract(ContentSnapshot snapshot)
    {
        bool found = snapshot.EnemyTemplates.TryGetValue(
            WolfAlphaTemplateId,
            out EnemyTemplateDefinition template
        );
        _test.True(found, "正式敌人内容应包含荒狼头目。");
        if (!found || template == null)
            return;

        _test.Eq(template.CreatureLevel, 4, "荒狼头目必须是4级怪物。");
        _test.Eq(template.HitDieSides, 10, "荒狼头目应使用d10生命骰。");
        _test.Eq(template.DerivedHpMax, 50, "4级、d10、体质16应派生50点生命。");
        _test.Eq(template.BrainId, new StringName("pack_leader"), "荒狼头目应使用独立狼群首领AI。");
        _test.Eq(template.InitialStateId, new StringName("hunt"), "荒狼头目初始状态应为hunt。");
        _test.Eq(template.TargetRank, new StringName("elite"), "荒狼头目应为精英目标。");
        _test.Eq(template.GeneratedCoreSkillCount, 3, "荒狼头目每次生成必须随机选择三项核心技能。");
        _test.Eq(template.SkillLevelMap.Count, 0, "荒狼头目不得在模板中固化技能等级。");
        _test.Eq(template.SkillIds.Count, 5, "荒狼头目必须固定配置五个技能。");

        StringName[] expectedSkills =
        {
            "basic_attack",
            "charge",
            HamstringSkillId,
            HowlSkillId,
            CullSkillId,
        };
        foreach (StringName expectedSkillId in expectedSkills)
        {
            _test.True(
                Contains(template.SkillIds, expectedSkillId),
                $"荒狼头目技能集合应包含{expectedSkillId}。"
            );
        }

        AssertBaseAttribute(template, "strength", 16);
        AssertBaseAttribute(template, "agility", 15);
        AssertBaseAttribute(template, "constitution", 16);
        AssertBaseAttribute(template, "perception", 14);
        AssertBaseAttribute(template, "intelligence", 5);
        AssertBaseAttribute(template, "willpower", 10);
        _test.True(template.HasTag("wolf"), "荒狼头目必须带wolf生物标签。");
        _test.True(template.HasTag("bite"), "荒狼头目必须带bite天生武器标签。");
        _test.True(
            template.AttributeOverrides.TryGetValue(
                AttributeContentRules.NaturalArmorAcBonus,
                out int naturalArmor
            ) && naturalArmor == 2,
            "荒狼头目应获得+2天生护甲。"
        );
    }

    private void TestSkillContracts(ContentSnapshot snapshot)
    {
        TestHamstring(snapshot);
        TestHowl(snapshot);
        TestCull(snapshot);
    }

    private void TestHamstring(ContentSnapshot snapshot)
    {
        SkillDefinition skill = GetSkill(snapshot, HamstringSkillId, "咬断脚筋");
        if (skill?.CombatProfile == null)
            return;

        _test.Eq(skill.MaxLevel, 5, "咬断脚筋应有五级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "咬断脚筋核心等级应从3级开始。");
        _test.True(skill.CombatProfile.AllowsNaturalWeapon, "咬断脚筋必须允许天生武器。");
        CombatSkillResourceCosts costs = skill.CombatProfile.GetEffectiveResourceCostValues(1);
        _test.Eq(costs.ApCost, 1, "咬断脚筋应消耗1AP。");
        _test.Eq(costs.StaminaCost, 18, "咬断脚筋应消耗18体力。");
        _test.Eq(costs.CooldownTu, 60, "咬断脚筋应冷却60TU。");

        int[] expectedAttackBonus = { 0, 1, 1, 1, 1 };
        int[] expectedDiceSides = { 0, 0, 4, 4, 6 };
        int[] expectedDuration = { 30, 30, 40, 50, 60 };
        for (int level = 1; level <= 5; level++)
        {
            BattleUnitState caster = BuildCaster(HamstringSkillId, level);
            using var rules = new BattleSkillResolutionRules();
            IReadOnlyList<CombatEffectDefinition> effects =
                rules.CollectUnitSkillEffectDefinitions(skill, null, caster);
            CombatEffectDefinition damage = FindEffect(effects, "damage");
            CombatEffectDefinition slow = FindEffect(effects, "status");
            _test.Eq(
                skill.CombatProfile.GetEffectiveAttackRollBonus(level),
                expectedAttackBonus[level - 1],
                $"咬断脚筋{level}级攻击加值应正确。"
            );
            _test.True(damage?.AddWeaponDice == true, $"咬断脚筋{level}级应造成天生武器伤害。");
            _test.Eq(damage?.DiceSides ?? -1, expectedDiceSides[level - 1], $"咬断脚筋{level}级技能骰应正确。");
            _test.Eq(slow?.StatusId ?? new StringName(""), new StringName("slow"), $"咬断脚筋{level}级应施加slow。");
            _test.Eq(slow?.Power ?? -1, 1, $"咬断脚筋{level}级移动成本只应+1。");
            _test.Eq(slow?.DurationTu ?? -1, expectedDuration[level - 1], $"咬断脚筋{level}级持续时间应正确。");
            _test.Eq(slow?.StackBehavior ?? new StringName(""), new StringName("refresh"), "咬断脚筋只应刷新，不叠加。");
            BattleTestFixture.DisposeBattleUnit(caster);
        }
    }

    private void TestHowl(ContentSnapshot snapshot)
    {
        SkillDefinition skill = GetSkill(snapshot, HowlSkillId, "统御长嚎");
        if (skill?.CombatProfile == null)
            return;

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(combat.TargetMode, new StringName("ground"), "统御长嚎应为地面范围技能。");
        _test.Eq(combat.RangeValue, 0, "统御长嚎应以自己为中心。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), "统御长嚎应使用半径范围。");
        _test.Eq(combat.AreaValue, 2, "统御长嚎范围应为周围2格。");
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(1);
        _test.Eq(costs.ApCost, 1, "统御长嚎应消耗1AP。");
        _test.Eq(costs.StaminaCost, 20, "统御长嚎应消耗20体力。");
        _test.Eq(costs.CooldownTu, 120, "统御长嚎应冷却120TU。");

        int[] expectedPower = { 1, 1, 1, 1, 2 };
        int[] expectedDuration = { 30, 40, 60, 80, 80 };
        for (int level = 1; level <= 5; level++)
        {
            BattleUnitState caster = BuildCaster(HowlSkillId, level);
            using var rules = new BattleSkillResolutionRules();
            IReadOnlyList<CombatEffectDefinition> effects =
                rules.CollectGroundUnitEffectDefinitions(skill, null, caster);
            CombatEffectDefinition effect = FindEffect(effects, "status");
            _test.Eq(effect?.StatusId ?? new StringName(""), new StringName("attack_roll_bonus_up"), $"统御长嚎{level}级应提高攻击检定。");
            _test.Eq(effect?.Power ?? -1, expectedPower[level - 1], $"统御长嚎{level}级加值应正确。");
            _test.Eq(effect?.DurationTu ?? -1, expectedDuration[level - 1], $"统御长嚎{level}级持续时间应正确。");
            _test.Eq(effect?.RequiredTargetCreatureTypeTag ?? new StringName(""), new StringName("wolf"), "统御长嚎只应强化wolf标签单位。");

            var wolf = new BattleUnitState { unit_id = "wolf_target" };
            wolf.AddCreatureTypeTagTyped("wolf");
            var humanoid = new BattleUnitState { unit_id = "humanoid_target" };
            humanoid.AddCreatureTypeTagTyped("humanoid");
            _test.True(BattleEffectTargetRequirementRules.IsSatisfied(effect, wolf), "wolf目标应满足统御长嚎要求。");
            _test.False(BattleEffectTargetRequirementRules.IsSatisfied(effect, humanoid), "同阵营非wolf目标不得获得统御长嚎。");
            BattleTestFixture.DisposeBattleUnit(wolf);
            BattleTestFixture.DisposeBattleUnit(humanoid);
            BattleTestFixture.DisposeBattleUnit(caster);
        }
    }

    private void TestCull(ContentSnapshot snapshot)
    {
        SkillDefinition skill = GetSkill(snapshot, CullSkillId, "弱者扑杀");
        if (skill?.CombatProfile == null)
            return;

        _test.True(skill.CombatProfile.AllowsNaturalWeapon, "弱者扑杀必须允许天生武器。");
        CombatSkillResourceCosts costs = skill.CombatProfile.GetEffectiveResourceCostValues(1);
        _test.Eq(costs.ApCost, 2, "弱者扑杀应消耗2AP。");
        _test.Eq(costs.StaminaCost, 24, "弱者扑杀应消耗24体力。");
        _test.Eq(costs.CooldownTu, 80, "弱者扑杀应冷却80TU。");
        _test.Eq(skill.CombatProfile.GetEffectiveAttackRollBonus(1), 1, "弱者扑杀攻击检定应+1。");

        int[] diceCount = { 1, 1, 1, 1, 2 };
        int[] diceSides = { 6, 6, 6, 8, 6 };
        int[] bonusCount = { 1, 1, 2, 2, 2 };
        int[] bonusSides = { 6, 8, 6, 8, 8 };
        for (int level = 1; level <= 5; level++)
        {
            BattleUnitState caster = BuildCaster(CullSkillId, level);
            using var rules = new BattleSkillResolutionRules();
            CombatEffectDefinition damage = FindEffect(
                rules.CollectUnitSkillEffectDefinitions(skill, null, caster),
                "damage"
            );
            _test.Eq(damage?.BonusCondition ?? new StringName(""), new StringName("target_low_hp"), $"弱者扑杀{level}级应检查目标低血。");
            _test.Eq(damage?.HpRatioThresholdPercent ?? -1, 50, "弱者扑杀低血阈值应为50%。");
            _test.Eq(damage?.DiceCount ?? -1, diceCount[level - 1], $"弱者扑杀{level}级常驻技能骰数量应正确。");
            _test.Eq(damage?.DiceSides ?? -1, diceSides[level - 1], $"弱者扑杀{level}级常驻技能骰面数应正确。");
            _test.Eq(damage?.BonusDamageDiceCount ?? -1, bonusCount[level - 1], $"弱者扑杀{level}级低血骰数量应正确。");
            _test.Eq(damage?.BonusDamageDiceSides ?? -1, bonusSides[level - 1], $"弱者扑杀{level}级低血骰面数应正确。");
            BattleTestFixture.DisposeBattleUnit(caster);
        }
    }

    private void TestGeneratedSkillLevels(ContentSnapshot snapshot)
    {
        if (
            !snapshot.EnemyTemplates.TryGetValue(
                WolfAlphaTemplateId,
                out EnemyTemplateDefinition template
            )
        )
            return;

        BattleUnitState first = BuildGeneratedUnit(template);
        BattleUnitState repeated = BuildGeneratedUnit(template);
        EnemySkillLevelGenerationService.ApplyGeneratedLevels(
            first,
            template,
            snapshot.Skills,
            generationSeed: 7301,
            unitIndex: 0
        );
        EnemySkillLevelGenerationService.ApplyGeneratedLevels(
            repeated,
            template,
            snapshot.Skills,
            generationSeed: 7301,
            unitIndex: 0
        );
        _test.Eq(BuildLevelSignature(first, template), BuildLevelSignature(repeated, template), "相同seed与单位序号必须生成相同技能等级。");
        AssertGeneratedLevelShape(first, template, snapshot.Skills);

        var signatures = new HashSet<string>();
        for (long seed = 1; seed <= 24; seed++)
        {
            BattleUnitState candidate = BuildGeneratedUnit(template);
            EnemySkillLevelGenerationService.ApplyGeneratedLevels(
                candidate,
                template,
                snapshot.Skills,
                seed,
                unitIndex: 0
            );
            AssertGeneratedLevelShape(candidate, template, snapshot.Skills);
            signatures.Add(BuildLevelSignature(candidate, template));
            BattleTestFixture.DisposeBattleUnit(candidate);
        }
        _test.True(signatures.Count > 1, "不同生成seed应能产生不同的狼王技能等级组合。");
        BattleTestFixture.DisposeBattleUnit(first);
        BattleTestFixture.DisposeBattleUnit(repeated);
    }

    private void TestFormalEncounterGeneration(ContentSnapshot snapshot)
    {
        const long generationSeed = 7301;
        StringName rosterId = "wolf_alpha_generation_test_roster";
        StringName encounterId = "wolf_alpha_generation_test_encounter";
        var roster = new WildEncounterRosterDefinition(
            rosterId,
            "荒狼头目生成回归",
            0,
            0,
            new[]
            {
                new WildEncounterRosterStageDefinition(
                    0,
                    new[]
                    {
                        new WildEncounterRosterUnitEntryDefinition(
                            WolfAlphaTemplateId,
                            1,
                            "荒狼头目"
                        ),
                    }
                ),
            }
        );
        var encounter = new BattleEncounterDefinition(
            encounterId,
            "荒狼头目生成回归",
            rosterId,
            BattleEliminationObjectiveDefinition.Instance,
            new BattleEncounterWorldResolutionDefinition(
                BattleWorldResolutionMode.Clear,
                BattleWorldResolutionMode.Preserve,
                BattleWorldResolutionMode.Preserve,
                0
            )
        );
        using var builder = new EncounterRosterBuilder();
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounterId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [rosterId] = roster,
            },
            snapshot.EnemyTemplates
        );
        var anchor = new EncounterAnchorData
        {
            entity_id = "wolf_alpha_generation_test_anchor",
            display_name = "荒狼头目",
            encounter_profile_id = encounterId,
            faction_id = "hostile",
            world_coord = Vector2I.Zero,
            region_tag = "test",
            growth_stage = 0,
        };

        IReadOnlyList<BattleUnitState> units = builder.BuildEnemyUnitStatesFromDefinitions(
            anchor,
            snapshot.Skills,
            snapshot.EnemyTemplates,
            snapshot.EnemyBrains,
            snapshot.Items,
            snapshot.Traits,
            snapshot.EquipmentAbilityBindings,
            generationSeed: generationSeed
        );
        try
        {
            _test.Eq(units.Count, 1, "正式EncounterRosterBuilder入口应生成一只荒狼头目。");
            if (units.Count != 1 || units[0] == null)
                return;

            BattleUnitState unit = units[0];
            _test.Eq(
                unit.enemy_template_id,
                WolfAlphaTemplateId,
                "正式生成的BattleUnitState应保留wolf_alpha模板来源。"
            );
            AssertGeneratedLevelShape(
                unit,
                snapshot.EnemyTemplates[WolfAlphaTemplateId],
                snapshot.Skills
            );
        }
        finally
        {
            foreach (BattleUnitState unit in units ?? Array.Empty<BattleUnitState>())
                BattleTestFixture.DisposeBattleUnit(unit);
        }
    }

    private void AssertGeneratedLevelShape(
        BattleUnitState unit,
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, SkillDefinition> skills
    )
    {
        _test.Eq(unit.GetKnownSkillLevelTyped("basic_attack", 0), 1, "基础攻击应保持当前敌人生成默认1级。");
        int coreCount = 0;
        foreach (StringName skillId in template.SkillIds)
        {
            if (skillId == (StringName)"basic_attack")
                continue;
            SkillDefinition skill = skills[skillId];
            int coreLevel = EnemySkillLevelGenerationService.ResolveCoreSkillLevel(skill);
            int level = unit.GetKnownSkillLevelTyped(skillId, 0);
            _test.True(level >= 1 && level <= skill.MaxLevel, $"{skillId}生成等级必须在1到上限之间。");
            if (level >= coreLevel)
                coreCount++;
            else
                _test.True(level < coreLevel, $"{skillId}未选为核心时必须低于核心等级。");
        }
        _test.Eq(coreCount, 3, "四项特殊技能中必须恰有三项达到核心等级。");
    }

    private void TestPackLeaderBrain(ContentSnapshot snapshot)
    {
        bool found = snapshot.EnemyBrains.TryGetValue(
            "pack_leader",
            out EnemyAiBrainDefinition brain
        );
        _test.True(found, "正式AI内容应包含pack_leader。");
        if (!found || brain == null)
            return;

        _test.Eq(brain.DefaultStateId, new StringName("hunt"), "pack_leader默认状态应为hunt。");
        _test.Eq(brain.TransitionRules.Count, 0, "pack_leader不应在低血时撤退。");
        EnemyAiStateDefinition state = brain.GetState("hunt");
        _test.True(state != null, "pack_leader应声明hunt状态。");
        if (state == null)
            return;

        bool hasCharge = false;
        bool hasHamstring = false;
        bool hasCull = false;
        bool hasBasic = false;
        bool hasHowl = false;
        bool hasRetreat = false;
        foreach (EnemyAiActionDefinition action in state.Actions)
        {
            if (action is RetreatActionDefinition)
                hasRetreat = true;
            if (action is UseChargeActionDefinition charge && charge.SkillId == (StringName)"charge")
                hasCharge = true;
            if (action is UseGroundSkillActionDefinition howl && Contains(howl.SkillIds, HowlSkillId))
            {
                hasHowl = true;
                _test.Eq(howl.MinimumHitCount, 2, "统御长嚎AI至少影响两只狼时才应使用。");
            }
            if (action is UseUnitSkillActionDefinition unitAction)
            {
                if (Contains(unitAction.SkillIds, HamstringSkillId))
                    hasHamstring = true;
                if (Contains(unitAction.SkillIds, CullSkillId))
                {
                    hasCull = true;
                    _test.Eq(unitAction.TargetSelector, new StringName("lowest_hp_enemy"), "弱者扑杀AI应优先最低生命敌人。");
                }
                if (Contains(unitAction.SkillIds, "basic_attack"))
                    hasBasic = true;
            }
        }
        _test.True(hasCharge, "pack_leader必须能使用冲锋。");
        _test.True(hasHamstring, "pack_leader必须能使用咬断脚筋。");
        _test.True(hasCull, "pack_leader必须能使用弱者扑杀。");
        _test.True(hasHowl, "pack_leader必须能使用统御长嚎。");
        _test.True(hasBasic, "pack_leader必须保留基础攻击兜底。");
        _test.False(hasRetreat, "pack_leader的hunt状态不得包含撤退动作。");
    }

    private SkillDefinition GetSkill(
        ContentSnapshot snapshot,
        StringName skillId,
        string displayName
    )
    {
        bool found = snapshot.Skills.TryGetValue(skillId, out SkillDefinition skill);
        _test.True(found, $"正式技能内容应包含{displayName}（{skillId}）。");
        return found ? skill : null;
    }

    private void AssertBaseAttribute(
        EnemyTemplateDefinition template,
        StringName attributeId,
        int expected
    )
    {
        bool found = template.BaseAttributeOverrides.TryGetValue(attributeId, out int actual);
        _test.True(found, $"荒狼头目必须显式配置{attributeId}。");
        if (found)
            _test.Eq(actual, expected, $"荒狼头目{attributeId}应为{expected}。");
    }

    private static BattleUnitState BuildCaster(StringName skillId, int level)
    {
        var caster = new BattleUnitState
        {
            unit_id = $"{skillId}_{level}",
            faction_id = "enemy",
        }.WithCombatResourcesForTest(hp: 50, ap: 2, stamina: 100, isAlive: true);
        caster.AddKnownActiveSkill(skillId);
        caster.SetKnownSkillLevelTyped(skillId, level);
        caster.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "natural",
                weapon_profile_type_id = "natural_weapon",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 3,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        return caster;
    }

    private static BattleUnitState BuildGeneratedUnit(EnemyTemplateDefinition template)
    {
        var unit = new BattleUnitState
        {
            unit_id = "wolf_alpha_generated",
            enemy_template_id = template.TemplateId,
        };
        foreach (StringName skillId in template.SkillIds)
            unit.AddKnownActiveSkill(skillId);
        return unit;
    }

    private static string BuildLevelSignature(
        BattleUnitState unit,
        EnemyTemplateDefinition template
    )
    {
        var values = new List<string>();
        foreach (StringName skillId in template.SkillIds)
            values.Add($"{skillId}:{unit.GetKnownSkillLevelTyped(skillId, 0)}");
        return string.Join("|", values);
    }

    private static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effects,
        StringName effectType
    )
    {
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectType == effectType)
                return effect;
        }
        return null;
    }

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }
}
