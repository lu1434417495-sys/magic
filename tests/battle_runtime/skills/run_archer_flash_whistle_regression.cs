using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_archer_flash_whistle_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_flash_whistle";
    private static readonly StringName DazzledStatusId = "flash_whistle_dazzled";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                "res://data/configs/skills/archer_flash_whistle.tres",
                "archer_flash_whistle_regression"
            );
            TestAuthoredContract(skill);
            TestLevelScaling(skill);
            TestBowGateAndWeaponRange(skill);
            TestRuntimeAreaAndDazzledBehavior(skill);
            TestRepeatedCastRefreshesWithoutStacking(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer flash whistle regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill != null, "炫目鸣镝正式资源应可加载。");
        if (skill?.CombatProfile == null)
        {
            _test.Fail("炫目鸣镝应包含 combat_profile。");
            return;
        }

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(skill.SkillId, SkillId, "炫目鸣镝 skill_id 应稳定。");
        _test.Eq(skill.MaxLevel, 5, "炫目鸣镝核心等级上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "炫目鸣镝非核心等级上限应为3级。");
        _test.Eq(skill.MasteryCurve.Count, 5, "炫目鸣镝熟练度曲线应覆盖五级。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "炫目鸣镝应属于基础成长档。");
        _test.Eq(ReadGrowth(skill, "perception"), 40, "炫目鸣镝应提供40点感知成长进度。");
        _test.Eq(ReadGrowth(skill, "agility"), 20, "炫目鸣镝应提供20点敏捷成长进度。");

        _test.Eq(combat.TargetMode, new StringName("ground"), "炫目鸣镝应选择地格。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "炫目鸣镝只应影响敌人。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), "炫目鸣镝应使用半径范围。");
        _test.Eq(combat.AreaValue, 1, "炫目鸣镝范围半径应为1格。");
        _test.Eq(combat.RangeValue, 0, "炫目鸣镝不应配置独立固定射程。");
        _test.True(combat.RequiresLos, "炫目鸣镝应要求视线。");
        _test.Eq(combat.ProjectileKind, new StringName("nonmagical"), "鸣镝应是非魔法投射物。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "炫目鸣镝应只有一个武器家族门槛。");
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "炫目鸣镝必须装备弓。");
        _test.False(combat.AllowsNaturalWeapon, "天生远程武器不得替代弓施放炫目鸣镝。");
        _test.Eq(combat.MasteryTriggerMode, new StringName("status_applied"), "熟练度应按状态成功施加结算。");
        _test.Eq(combat.MasteryAmountMode, new StringName("per_target_rank"), "多目标熟练度应按目标等级结算。");

        IReadOnlyList<StringName> deliveryCategories =
            BattleEffectCategoryResolver.ResolveCategories(skill, combat.EffectDefinitions);
        _test.True(deliveryCategories.Contains(new StringName("projectile")), "炫目鸣镝应进入投射物交互路径。");
        _test.True(deliveryCategories.Contains(new StringName("nonmagical_projectile")), "炫目鸣镝应进入非魔法投射物屏障路径。");
        _test.False(deliveryCategories.Contains(new StringName("magical_projectile")), "炫目鸣镝不得被识别为魔法投射物。");

        int statusCount = 0;
        int damageCount = 0;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect?.EffectType == new StringName("status"))
                statusCount += 1;
            if (effect?.EffectType == new StringName("damage"))
                damageCount += 1;
        }
        _test.Eq(statusCount, 3, "炫目鸣镝应以三段等级互斥状态表达持续时间成长。");
        _test.Eq(damageCount, 0, "炫目鸣镝不得保留固定伤害效果。");

        _test.Eq(
            BattleStatusSemanticTable.STATUS_FLASH_WHISTLE_DAZZLED,
            DazzledStatusId,
            "正式状态常量应指向炫目鸣镝的独立状态。"
        );
        _test.True(BattleStatusSemanticTable.HasSemantic(DazzledStatusId), "炫目应有正式状态语义。");
        _test.True(BattleStatusSemanticTable.IsHarmfulStatus(DazzledStatusId), "炫目应被 UI 与 AI 识别为减益。");
        _test.True(BattleStatusSemanticTable.IsCleansableHarmfulStatus(DazzledStatusId), "普通净化应能移除非魔法炫目。");
        _test.False(BattleStatusSemanticTable.IsDispellableHarmfulStatus(DazzledStatusId), "魔法驱散不应移除非魔法鸣镝造成的炫目。");
        _test.False(BattleStatusSemanticTable.BlocksPendingCast(DazzledStatusId), "炫目不得阻止施法或待结算施法。");
        _test.Eq(BattleStatusSemanticTable.GetDisplayLabel(DazzledStatusId), "炫目", "炫目状态应显示中文名称。");
        _test.True(DazzledStatusId != BattleStatusSemanticTable.STATUS_BLIND, "炫目不得复用 blind 状态。");
        _test.True(skill.Description.Contains("不视为致盲"), "技能描述应明确炫目不属于致盲。");
        _test.True(skill.Description.Contains("不造成伤害"), "技能描述应明确鸣镝不造成伤害。");
    }

    private void TestLevelScaling(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            return;
        }

        AssertLevel(skill, 0, stamina: 30, cooldownTu: 100, durationTu: 30);
        AssertLevel(skill, 1, stamina: 30, cooldownTu: 100, durationTu: 30);
        AssertLevel(skill, 2, stamina: 28, cooldownTu: 100, durationTu: 30);
        AssertLevel(skill, 3, stamina: 28, cooldownTu: 100, durationTu: 35);
        AssertLevel(skill, 4, stamina: 28, cooldownTu: 90, durationTu: 35);
        AssertLevel(skill, 5, stamina: 26, cooldownTu: 80, durationTu: 40);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldownTu,
        int durationTu
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 1, $"炫目鸣镝{level}级应消耗1AP。");
        _test.Eq(costs.StaminaCost, stamina, $"炫目鸣镝{level}级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"炫目鸣镝{level}级冷却应正确。");

        List<CombatEffectDefinition> active = ActiveEffects(combat.EffectDefinitions, level);
        _test.Eq(active.Count, 1, $"炫目鸣镝{level}级应只有一个生效的状态段。");
        if (active.Count != 1)
        {
            return;
        }

        CombatEffectDefinition effect = active[0];
        _test.Eq(effect.StatusId, DazzledStatusId, $"炫目鸣镝{level}级应施加独立炫目状态。");
        _test.Eq(effect.EffectTargetTeamFilter, new StringName("enemy"), $"炫目鸣镝{level}级只应影响敌人。");
        _test.Eq(effect.DurationTu, durationTu, $"炫目鸣镝{level}级持续时间应正确。");
        _test.Eq(effect.StackBehavior, new StringName("refresh"), $"炫目鸣镝{level}级重复施加应刷新。");
        _test.Eq(effect.AttackRollPenalty, 2, $"炫目鸣镝{level}级攻击检定惩罚应固定为2。");
        _test.True(effect.LockCounterattack, $"炫目鸣镝{level}级应封锁反击。");
        _test.False(effect.LockGuard, $"炫目鸣镝{level}级不得封锁格挡。");
        _test.False(effect.LockDodgeBonus, $"炫目鸣镝{level}级不得封锁闪避加值。");
        _test.True(effect.CountsAsDebuffOverride && effect.CountsAsDebuff, $"炫目鸣镝{level}级应显式计作减益。");
        _test.Eq(effect.SaveDc, 0, $"炫目鸣镝{level}级不应额外引入豁免检定。");
    }

    private void TestBowGateAndWeaponRange(SkillDefinition skill)
    {
        BattleUnitState bowCaster = BuildCaster("flash_whistle_bow_gate", new Vector2I(1, 1), 1);
        ApplyWeapon(bowCaster, "bow", 5, "equipped");
        _test.True(BattleRangeService.UnitMatchesRequiredWeaponFamilies(bowCaster, skill), "装备弓应通过炫目鸣镝武器门槛。");
        _test.Eq(BattleRangeService.GetEffectiveSkillRange(bowCaster, skill), 5, "炫目鸣镝应采用当前弓的攻击射程。");

        BattleUnitState swordCaster = BuildCaster("flash_whistle_sword_gate", new Vector2I(1, 1), 1);
        ApplyWeapon(swordCaster, "sword", 1, "equipped");
        _test.False(BattleRangeService.UnitMatchesRequiredWeaponFamilies(swordCaster, skill), "非弓武器不得施放炫目鸣镝。");

        BattleUnitState naturalCaster = BuildCaster("flash_whistle_natural_gate", new Vector2I(1, 1), 1);
        ApplyWeapon(naturalCaster, "bow", 5, "natural");
        _test.False(BattleRangeService.UnitMatchesRequiredWeaponFamilies(naturalCaster, skill), "天生远程武器不得伪装成装备弓通过门槛。");

        BattleUnitState target = BuildUnit("flash_whistle_gate_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, swordCaster, target);
        int apBefore = swordCaster.GetCurrentAp();
        int staminaBefore = swordCaster.GetCurrentStamina();
        BattleCommand command = BuildCommand(swordCaster, target.GetAnchorCoord());
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.False(preview?.allowed == true, "装备非弓武器时正式预览应拒绝炫目鸣镝。");
        _test.Eq(swordCaster.GetCurrentAp(), apBefore, "武器门槛拒绝不得消耗AP。");
        _test.Eq(swordCaster.GetCurrentStamina(), staminaBefore, "武器门槛拒绝不得消耗体力。");

        BattleTestFixture.DisposeBattleCommand(command);
        BattleTestFixture.DisposeBattleUnit(bowCaster);
        BattleTestFixture.DisposeBattleUnit(naturalCaster);
    }

    private void TestRuntimeAreaAndDazzledBehavior(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("flash_whistle_runtime_caster", new Vector2I(1, 2), 0);
        ApplyWeapon(caster, "bow", 4, "equipped");
        BattleUnitState center = BuildUnit("flash_whistle_center", "enemy", new Vector2I(4, 2));
        BattleUnitState adjacentEnemy = BuildUnit("flash_whistle_adjacent", "enemy", new Vector2I(4, 1));
        BattleUnitState adjacentAlly = BuildUnit("flash_whistle_ally", "player", new Vector2I(5, 2));
        BattleUnitState outsideEnemy = BuildUnit("flash_whistle_outside", "enemy", new Vector2I(6, 2));

        using BattleTestFixture fixture = CreateFixture(
            skill,
            new[] { caster, adjacentAlly },
            new[] { center, adjacentEnemy, outsideEnemy }
        );
        BattleCommand command = BuildCommand(caster, center.GetAnchorCoord());
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"弓射程内存在敌人时炫目鸣镝应可施放。logs={FormatLogs(preview)}");
        _test.True(preview?.TargetUnitIdsTyped.Contains(center.unit_id) == true, "预览应包含中心敌人。");
        _test.True(preview?.TargetUnitIdsTyped.Contains(adjacentEnemy.unit_id) == true, "预览应包含半径1格内敌人。");
        _test.False(preview?.TargetUnitIdsTyped.Contains(adjacentAlly.unit_id) == true, "预览不得包含半径内友军。");
        _test.False(preview?.TargetUnitIdsTyped.Contains(outsideEnemy.unit_id) == true, "预览不得包含半径外敌人。");

        int centerHpBefore = center.GetCurrentHp();
        int adjacentHpBefore = adjacentEnemy.GetCurrentHp();
        int allyHpBefore = adjacentAlly.GetCurrentHp();
        int outsideHpBefore = outsideEnemy.GetCurrentHp();
        int centerApBefore = center.GetCurrentAp();
        int centerMoveBefore = center.GetCurrentMovePoints();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "炫目鸣镝应通过正式命令完成结算。");

        AssertDazzled(center, 30, "中心敌人");
        AssertDazzled(adjacentEnemy, 30, "相邻敌人");
        _test.False(adjacentAlly.HasStatusEffect(DazzledStatusId), "半径内友军不得获得炫目。");
        _test.False(outsideEnemy.HasStatusEffect(DazzledStatusId), "半径外敌人不得获得炫目。");
        _test.False(center.HasStatusEffect(BattleStatusSemanticTable.STATUS_BLIND), "炫目不得写入 blind 状态。");

        _test.Eq(center.GetCurrentHp(), centerHpBefore, "炫目鸣镝不得伤害中心敌人。");
        _test.Eq(adjacentEnemy.GetCurrentHp(), adjacentHpBefore, "炫目鸣镝不得伤害相邻敌人。");
        _test.Eq(adjacentAlly.GetCurrentHp(), allyHpBefore, "炫目鸣镝不得伤害友军。");
        _test.Eq(outsideEnemy.GetCurrentHp(), outsideHpBefore, "炫目鸣镝不得伤害范围外单位。");
        _test.Eq(center.GetCurrentAp(), centerApBefore, "炫目不得修改目标AP。");
        _test.Eq(center.GetCurrentMovePoints(), centerMoveBefore, "炫目不得修改目标移动力。");
        _test.Eq(caster.GetCurrentAp(), 1, "0级炫目鸣镝成功后应消耗1AP。");
        _test.Eq(caster.GetCurrentStamina(), 70, "0级炫目鸣镝成功后应消耗30体力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 100, "0级炫目鸣镝成功后应进入100TU冷却。");

        BattleStatusEffectState dazzled = center.GetStatusEffect(DazzledStatusId);
        var hitResolver = new BattleHitResolver();
        AttackCheckInput attackCheck = hitResolver.BuildSkillAttackCheck(center, adjacentAlly, null);
        _test.Eq(attackCheck.SituationalAttackPenalty, 2, "炫目应通过正式攻击检定路径施加-2惩罚。");
        var turnResolver = new BattleRuntimeSkillTurnResolver();
        _test.True(turnResolver.HasCounterattackLockStatus(center), "炫目应通过类型化状态封锁反击。");
        _test.Eq(BattleStatusSemanticTable.GetMoveCostDelta(dazzled), 0, "炫目不得增加移动成本。");

        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestRepeatedCastRefreshesWithoutStacking(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("flash_whistle_refresh_caster", new Vector2I(1, 1), 5);
        ApplyWeapon(caster, "bow", 4, "equipped");
        BattleUnitState target = BuildUnit("flash_whistle_refresh_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        BattleCommand command = BuildCommand(caster, target.GetAnchorCoord());

        using (BattleEventBatch first = fixture.Runtime.IssueCommand(command))
        {
            _test.True(first != null, "第一次炫目鸣镝应成功施加状态。");
        }
        BattleStatusEffectState firstStatus = target.GetStatusEffect(DazzledStatusId);
        _test.Eq(firstStatus?.duration ?? 0, 40, "5级炫目首次施加应持续40TU。");
        if (firstStatus != null)
            firstStatus.duration = 5;

        caster.SetCurrentAp(2);
        caster.SetCurrentStamina(100);
        caster.SetCooldownTyped(SkillId, 0);
        using (BattleEventBatch second = fixture.Runtime.IssueCommand(command))
        {
            _test.True(second != null, "冷却与资源重置后第二次炫目鸣镝应完成结算。");
        }

        BattleStatusEffectState refreshed = target.GetStatusEffect(DazzledStatusId);
        _test.Eq(refreshed?.duration ?? 0, 40, "重复施加炫目应把剩余时间刷新到40TU。");
        _test.Eq(refreshed?.stacks ?? 0, 1, "重复施加炫目不得增加状态层数。");
        _test.Eq(refreshed?.attack_roll_penalty ?? 0, 2, "重复施加炫目不得把攻击惩罚叠加为-4。");
        _test.Eq(target.GetSortedStatusEffectIdsTyped().Count, 1, "重复施加后目标应仍只有一个炫目状态条目。");

        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void AssertDazzled(BattleUnitState unit, int durationTu, string label)
    {
        BattleStatusEffectState status = unit.GetStatusEffect(DazzledStatusId);
        _test.True(status != null, $"{label}应获得炫目。");
        if (status == null)
        {
            return;
        }
        _test.Eq(status.duration, durationTu, $"{label}炫目持续时间应正确。");
        _test.Eq(status.attack_roll_penalty, 2, $"{label}攻击检定应受到-2惩罚。");
        _test.True(status.lock_counterattack, $"{label}应无法反击。");
        _test.False(status.lock_guard, $"{label}仍应能格挡。");
        _test.False(status.lock_dodge_bonus, $"{label}仍应保留闪避加值。");
        _test.True(status.counts_as_debuff_override && status.counts_as_debuff, $"{label}炫目应计作减益。");
        _test.True(BattleStatusSemanticTable.IsCleansableHarmfulStatusEntry(status), $"{label}的非魔法炫目应能被普通净化。");
        _test.False(BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(status), $"{label}的非魔法炫目不得被魔法驱散。");
    }

    private static List<CombatEffectDefinition> ActiveEffects(
        IReadOnlyList<CombatEffectDefinition> effects,
        int level
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect != null
                && level >= Math.Max(effect.MinSkillLevel, 0)
                && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel)
            )
            {
                result.Add(effect);
            }
        }
        return result;
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState ally,
        BattleUnitState enemy
    ) => CreateFixture(skill, new[] { ally }, new[] { enemy });

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        IReadOnlyList<BattleUnitState> allies,
        IReadOnlyList<BattleUnitState> enemies
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            $"flash_whistle_{allies[0].unit_id}",
            new Vector2I(8, 6),
            allies,
            enemies
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.State.active_unit_id = allies[0].unit_id;
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static BattleUnitState BuildCaster(StringName id, Vector2I coord, int level)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        return caster;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = id,
            source_member_id = id,
            display_name = id.ToString(),
            faction_id = faction,
            control_mode = "manual",
        }.WithCombatResourcesForTest(hp: 100, stamina: 100, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentMovePoints(3);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        int attackRange,
        StringName profileKind
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = profileKind,
                weapon_item_id = profileKind == new StringName("equipped") ? $"flash_whistle_{family}" : "",
                weapon_profile_type_id = $"flash_whistle_{family}",
                weapon_range_type = "ranged",
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I targetCoord)
    {
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;

    private static string FormatLogs(BattlePreview preview) =>
        preview == null ? "" : string.Join(" | ", preview.LogLinesTyped);
}
