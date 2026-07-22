using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_prismatic_sphere_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private IReadOnlyDictionary<StringName, BarrierProfileDefinition> _barrierProfileDefinitions;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestResult exitCode = null;
        try
        {
            _barrierProfileDefinitions = BarrierDefinitionTestContent.LoadValidated();
            TestPrismaticSphereCommandGrantsEffectAppliedMasteryOnce();
            TestSingleLayerWardCommandsCreateExactlyOneLayer();
            TestCombinedGenericAndSpecialEffectsDoNotDoubleGrantMastery();
            TestPrismaticSphereCreatesOrderedLayers();
            TestLayerDamageUsesConfiguredDamageTagMitigation();
            TestProjectedCategoriesRespectRemainingLayersWithoutCatchAll();
            TestProjectedWeaponAbilityCategoriesRespectRangedBoundary();
            TestProjectedWeaponCategoriesMatchBasicAttackPreviewAndCommit();
            TestOrderedMultiHitBreakerPreviewMatchesCommit();
            TestRandomChainPreviewSeparatesCandidatePoolFromEffectiveTargets();
            TestGroundEffectWithUnmatchedCategoryPassesBarrier();
            TestGroundAoePreviewAndExecutionClipAtBarrierBoundary();
            TestGroundAoeTerrainClipAtBarrierBoundary();
            TestGroundAoeBreakerClipsUnitAndTerrainWithoutSameCastPenetration();
            TestGroundAoeAutoCastClipsAtBarrierBoundary();
            TestGroundAoePendingCastClipsAtBarrierBoundary();
            TestPrismaticSphereBlocksDeeperBreakersUntilOuterLayerBreaks();
            TestProjectedEffectBarrierGeometryRespectsBoundary();
            TestDeathWardWithoutLastStandDoesNotBlockFatalPhysicalDamage();
            TestGreenLayerInstantDeathUsesFatalDamageChain();
            TestPetrifiedBlocksTurnUntilSelfSaveSucceeds();
            TestVioletLayerTeleportsNonSummonsAndRemovesSummons();
            TestCleanseHarmfulRemovesMadnessButNotPetrified();
            TestDispelMagicRemovesMagicStatusesByRelation();
            exitCode = _test.Finish("Prismatic sphere regression");
        }
        finally
        {
            RequestTestExit(exitCode ?? _test.Finish("Prismatic sphere regression", 1));
        }
    }

    private void TestProjectedWeaponAbilityCategoriesRespectRangedBoundary()
    {
        AssertProjectedWeaponAbilityBarrierResult(
            "ash_longbow",
            "",
            expectedCategory: "nonmagical_missile",
            expectedBlocked: false,
            message: "普通远程武器伤害应投影为非魔法投射，但不应被只剩黄色层的法球阻挡。"
        );
        AssertProjectedWeaponAbilityBarrierResult(
            "weapon_unique_crossbow_gorgon_329",
            "binding.weapon.crossbow.gorgon.petrifying_gaze",
            expectedCategory: "petrification",
            expectedBlocked: true,
            message: "远程蛇发女妖之弩携带的石化效果应被黄色层阻挡。"
        );
        AssertProjectedWeaponAbilityBarrierResult(
            "weapon_unique_bow_scorpion_339",
            "binding.weapon.bow.scorpion.scorpion_arrow",
            expectedCategory: "poison",
            expectedBlocked: true,
            message: "远程蝎尾弓携带的毒素效果应被黄色层阻挡。"
        );
        AssertProjectedWeaponAbilityBarrierResult(
            "weapon_unique_morningstar_viper_206",
            "binding.weapon.morningstar.viper.venom_strike",
            expectedCategory: "",
            expectedBlocked: false,
            message: "近战毒蛇晨星不得把毒素类别带入投射屏障判定。",
            syntheticProjectedCategory: "poison"
        );
        AssertProjectedWeaponAbilityBarrierResult(
            "weapon_unique_polearm_rock_halberd_148",
            "binding.weapon.polearm.rock_halberd.stone_touch",
            expectedCategory: "",
            expectedBlocked: false,
            message: "近战岩石戟不得把石化类别带入投射屏障判定。",
            syntheticProjectedCategory: "petrification"
        );
        AssertExplicitMagicalMissileDoesNotGainNonmagicalProjection();
    }

    private void TestProjectedWeaponCategoriesMatchBasicAttackPreviewAndCommit()
    {
        AssertProjectedWeaponBasicAttackPreviewAndCommit(
            "ash_longbow",
            "",
            expectedBlocked: true,
            afterHitStatusId: "",
            remainingLayerId: "red",
            remainingLayerLabel: "红色层"
        );
        AssertProjectedWeaponBasicAttackPreviewAndCommit(
            "weapon_unique_crossbow_gorgon_329",
            "binding.weapon.crossbow.gorgon.petrifying_gaze",
            expectedBlocked: true,
            afterHitStatusId: "slow"
        );
        AssertProjectedWeaponBasicAttackPreviewAndCommit(
            "weapon_unique_bow_scorpion_339",
            "binding.weapon.bow.scorpion.scorpion_arrow",
            expectedBlocked: true,
            afterHitStatusId: "paralyzed"
        );
        AssertProjectedWeaponBasicAttackPreviewAndCommit(
            "weapon_unique_morningstar_viper_206",
            "binding.weapon.morningstar.viper.venom_strike",
            expectedBlocked: false,
            afterHitStatusId: "",
            expectBonusDamage: true,
            syntheticProjectedCategory: "poison"
        );
        AssertProjectedWeaponBasicAttackPreviewAndCommit(
            "weapon_unique_polearm_rock_halberd_148",
            "binding.weapon.polearm.rock_halberd.complete_petrification",
            expectedBlocked: false,
            afterHitStatusId: "rock_halberd_petrification_count",
            syntheticProjectedCategory: "petrification"
        );
    }

    private void AssertExplicitMagicalMissileDoesNotGainNonmagicalProjection()
    {
        using Fixture fixture = BuildRuntimeWithSphereAndProjectedWeapon("ash_longbow", "");
        SetOnlyRemainingLayer(fixture.State, "red");
        CombatEffectDefinition weaponDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            requiresWeapon: true,
            addWeaponDice: true,
            useWeaponPhysicalDamageTag: true,
            resolveAsWeaponAttack: true
        );
        SkillDefinition magicalWeaponSkill = TestSkillDefinitionProjection.BuildSkill(
            "explicit_magical_weapon_projectile_probe",
            displayName: "显式魔法武器投射测试",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "explicit_magical_weapon_projectile_probe",
                effects: new[] { weaponDamage },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 10,
                deliveryCategories: new[] { new StringName("magical_missile") }
            )
        );

        IReadOnlyList<StringName> projectedCategories = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .CollectProjectedWeaponEffectCategories(
                fixture.Enemy,
                new[] { weaponDamage },
                magicalWeaponSkill
            );
        _test.False(
            projectedCategories.Contains(new StringName("nonmagical_missile")),
            "显式 magical_missile 的远程武器技能不得再投影 nonmagical_missile。"
        );
        IReadOnlyList<StringName> resolvedCategories = BattleEffectCategoryResolver.ResolveCategories(
            magicalWeaponSkill,
            new[] { weaponDamage },
            projectedCategories
        );
        _test.True(
            resolvedCategories.Contains(new StringName("magical_missile"))
                && !resolvedCategories.Contains(new StringName("nonmagical_missile")),
            "显式魔法投射应只保留 magical_missile，而不是同时命中红层类别。"
        );

        BattleBarrierInteractionResult result = fixture.Runtime._layered_barrier_service
            .ResolveSkillBarrierInteractionResult(
                fixture.Enemy,
                fixture.Caster,
                magicalWeaponSkill,
                new[] { weaponDamage },
                new BattleEventBatch()
            );
        _test.False(result.Blocked, "只剩红层时，显式魔法投射不得被当作非魔法投射阻挡。");
        _test.Eq(
            ActiveLayerId(FirstBarrier(fixture.State)),
            new StringName("red"),
            "显式魔法投射穿过红层后不得改变红层状态。"
        );
    }

    private void TestOrderedMultiHitBreakerPreviewMatchesCommit()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        SkillDefinition magicMissile = snapshot.Skills["mage_arcane_missile"];
        using Fixture fixture = BuildRuntimeWithSphere(magicMissile);
        SetOnlyRemainingLayer(fixture.State, "blue");
        LearnSkill(fixture.Enemy, magicMissile.SkillId);
        fixture.Enemy.current_ap = 2;
        fixture.Enemy.current_mp = 120;
        fixture.Enemy.current_stamina = 40;
        fixture.Enemy.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        fixture.Enemy.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina)
        );
        fixture.State.active_unit_id = fixture.Enemy.unit_id;
        fixture.Runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(
                new GArray { 1, 1, 1, 1 },
                new GArray { 1, 1, 1, 1 }
            )
        );
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(20));
        fixture.Runtime.SetupStateForTests(fixture.State);

        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = fixture.Enemy.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(magicMissile.SkillId),
            skill_id = magicMissile.SkillId,
            target_coord = fixture.Caster.coord,
        };
        command.AddTargetUnitId(fixture.Caster.unit_id);
        command.AddTargetUnitId(fixture.Caster.unit_id);
        BattlePreview firstPreview = null;
        BattlePreview secondPreview = null;
        try
        {
            firstPreview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                firstPreview?.allowed == true,
                $"奥术飞弹重复目标预览应保持可施放；logs={string.Join(" | ", firstPreview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
            _test.Eq(
                firstPreview?.TargetUnitIdsTyped.Count ?? 0,
                1,
                "第一枚飞弹应在预览副本中破解蓝层并被挡，第二枚应预计命中。"
            );
            _test.True(
                firstPreview?.ContainsTargetUnitId(fixture.Caster.unit_id) == true
                    && firstPreview.DamagePreviewTyped.HasValue,
                "顺序破层后，预览必须保留第二枚飞弹的目标和伤害。"
            );
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                new StringName("blue"),
                "第一次预览不得修改真实蓝层。"
            );

            secondPreview = fixture.Runtime.PreviewCommand(command);
            _test.Eq(
                secondPreview?.TargetUnitIdsTyped.Count ?? 0,
                1,
                "重复预览必须从同一真实状态重新模拟，并保持相同结果。"
            );
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                new StringName("blue"),
                "重复预览不得累积破层副作用。"
            );

            int hpBefore = fixture.Caster.current_hp;
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(
                fixture.Caster.current_hp < hpBefore,
                "正式执行时第一枚飞弹破蓝层后，第二枚应造成伤害。"
            );
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                new StringName(""),
                "正式执行应真正破解最后的蓝层。"
            );
            _test.True(
                LogsContain(batch?.LogLinesTyped, "蓝色层")
                    && LogsContain(batch?.LogLinesTyped, "破解"),
                "正式执行日志应记录蓝层被第一枚飞弹破解。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(firstPreview);
            BattleTestFixture.DisposeBattlePreview(secondPreview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestRandomChainPreviewSeparatesCandidatePoolFromEffectiveTargets()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        SkillDefinition chainLightning = snapshot.Skills["mage_chain_lightning"];
        using Fixture fixture = BuildRuntimeWithSphere(chainLightning);
        SetOnlyRemainingLayer(fixture.State, "indigo");
        LearnSkill(fixture.Enemy, chainLightning.SkillId);
        fixture.Enemy.current_ap = 2;
        fixture.Enemy.current_mp = 120;
        fixture.Enemy.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        BattleUnitState outsideTarget = BuildUnit(
            "random_chain_outside_target",
            "法球外目标",
            "player",
            new Vector2I(6, 2)
        );
        AddUnit(fixture.Runtime, fixture.State, outsideTarget, false);
        fixture.State.active_unit_id = fixture.Enemy.unit_id;
        fixture.Runtime.ConfigureDamageResolverForTests(
            new FixedFailedSaveDamageResolver(
                new GArray { 1, 1, 1, 1, 1, 1, 1, 1 },
                new GArray { 20, 20, 20, 20 }
            )
        );
        fixture.Runtime.SetupStateForTests(fixture.State);

        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = fixture.Enemy.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(chainLightning.SkillId),
            skill_id = chainLightning.SkillId,
        };
        BattlePreview firstPreview = null;
        BattlePreview secondPreview = null;
        try
        {
            firstPreview = fixture.Runtime.PreviewCommand(command);
            _test.True(firstPreview?.allowed == true, "连锁闪电在混合屏障候选池中应保持可施放。");
            _test.True(
                firstPreview?.RandomChainCandidateUnitIdsTyped.Contains(
                    fixture.Caster.unit_id
                ) == true
                    && firstPreview.RandomChainCandidateUnitIdsTyped.Contains(
                        outsideTarget.unit_id
                    ),
                "随机链候选池必须保留法球内外两个真实抽样候选。"
            );
            _test.False(
                firstPreview?.ContainsTargetUnitId(fixture.Caster.unit_id) == true,
                "随机链预览不得伪造确定目标。"
            );
            _test.True(
                firstPreview?.TargetUnitIdsTyped.Count == 0
                    && firstPreview.RandomChainImpactCandidateUnitIdsTyped.Contains(
                        outsideTarget.unit_id
                    )
                    && !firstPreview.RandomChainImpactCandidateUnitIdsTyped.Contains(
                        fixture.Caster.unit_id
                    )
                    && firstPreview.DamagePreviewTyped.HasValue,
                "法球外候选仍应出现在随机链预计受影响目标和伤害预览中。"
            );
            _test.True(
                LogsContain(firstPreview?.LogLinesTyped, "靛色层")
                    && LogsContain(firstPreview?.LogLinesTyped, "其中 1 个单位可受到影响"),
                "随机链预览应同时说明屏障阻挡和有效候选数量。"
            );
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                new StringName("indigo"),
                "随机链预览不得修改真实靛色层。"
            );

            secondPreview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                secondPreview?.RandomChainImpactCandidateUnitIdsTyped.Contains(
                    outsideTarget.unit_id
                ) == true
                    && secondPreview.RandomChainImpactCandidateUnitIdsTyped.Contains(
                        fixture.Caster.unit_id
                    ) == false,
                "重复随机链预览必须保持相同的屏障过滤结果。"
            );
            int insideHpBefore = fixture.Caster.current_hp;
            int outsideHpBefore = outsideTarget.current_hp;
            fixture.Runtime.IssueCommand(command);
            _test.Eq(
                fixture.Caster.current_hp,
                insideHpBefore,
                "正式随机链执行中，法球内目标应被靛色层阻挡。"
            );
            _test.True(
                outsideTarget.current_hp < outsideHpBefore,
                "正式随机链执行中，法球外目标仍应受到连锁闪电伤害。"
            );
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                new StringName("indigo"),
                "非破解随机链不得破坏靛色层。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(firstPreview);
            BattleTestFixture.DisposeBattlePreview(secondPreview);
            BattleTestFixture.DisposeBattleCommand(command);
            BattleTestFixture.DisposeBattleUnit(outsideTarget);
        }
    }

    private void AssertProjectedWeaponBasicAttackPreviewAndCommit(
        StringName weaponItemId,
        StringName bindingId,
        bool expectedBlocked,
        StringName afterHitStatusId,
        bool expectBonusDamage = false,
        StringName syntheticProjectedCategory = default,
        StringName remainingLayerId = default,
        string remainingLayerLabel = ""
    )
    {
        syntheticProjectedCategory = ProgressionDataUtils.to_string_name(
            syntheticProjectedCategory
        );
        remainingLayerId = ProgressionDataUtils.to_string_name(remainingLayerId);
        if (remainingLayerId == "")
            remainingLayerId = "yellow";
        if (string.IsNullOrEmpty(remainingLayerLabel))
            remainingLayerLabel = "黄色层";
        using Fixture fixture = BuildRuntimeWithSphereAndProjectedWeapon(
            weaponItemId,
            bindingId,
            useBoundaryTargetGeometry: true,
            syntheticProjectedCategory: syntheticProjectedCategory
        );
        SetOnlyRemainingLayer(fixture.State, remainingLayerId);
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(fixture.Enemy);
        fixture.State.active_unit_id = fixture.Enemy.unit_id;
        fixture.Runtime.SetupStateForTests(fixture.State);

        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            fixture.Enemy,
            fixture.Caster
        );
        BattlePreview firstPreview = null;
        BattlePreview secondPreview = null;
        try
        {
            firstPreview = fixture.Runtime.PreviewCommand(command);
            secondPreview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                firstPreview?.allowed == true && secondPreview?.allowed == true,
                $"{weaponItemId} 的基础攻击动作应保持可施放。"
            );
            _test.Eq(
                firstPreview?.ContainsTargetUnitId(fixture.Caster.unit_id) == true,
                !expectedBlocked,
                $"{weaponItemId} 预览中的预计受影响目标必须与{remainingLayerLabel}判定一致。"
            );
            _test.Eq(
                firstPreview?.DamagePreviewTyped.HasValue == true,
                !expectedBlocked,
                $"{weaponItemId} 预览中的伤害范围必须与{remainingLayerLabel}判定一致。"
            );
            if (expectedBlocked)
            {
                _test.True(
                    LogsContain(firstPreview?.LogLinesTyped, remainingLayerLabel)
                        && LogsContain(firstPreview?.LogLinesTyped, "阻挡"),
                    $"{weaponItemId} 的预览日志应明确报告{remainingLayerLabel}阻挡。"
                );
            }
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                remainingLayerId,
                $"{weaponItemId} 连续预览不得改变{remainingLayerLabel}。"
            );

            int hpBefore = fixture.Caster.current_hp;
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            if (expectedBlocked)
            {
                _test.Eq(
                    fixture.Caster.current_hp,
                    hpBefore,
                    $"{weaponItemId} 被{remainingLayerLabel}阻挡后不得造成基础或装备附加伤害。"
                );
                if (afterHitStatusId != "")
                {
                    _test.False(
                        fixture.Caster.HasStatusEffect(afterHitStatusId),
                        $"{weaponItemId} 被{remainingLayerLabel}阻挡后不得触发 {afterHitStatusId} 后效。"
                    );
                }
                _test.True(
                    LogsContain(batch?.LogLinesTyped, remainingLayerLabel)
                        && LogsContain(batch?.LogLinesTyped, "阻挡"),
                    $"{weaponItemId} 的执行日志应明确报告{remainingLayerLabel}阻挡。"
                );
            }
            else
            {
                int damageDealt = hpBefore - fixture.Caster.current_hp;
                _test.True(
                    damageDealt > 0,
                    $"近战武器 {weaponItemId} 应穿过黄色层并造成真实武器伤害。"
                );
                if (afterHitStatusId != "")
                {
                    _test.True(
                        fixture.Caster.HasStatusEffect(afterHitStatusId),
                        $"近战武器 {weaponItemId} 应正常触发 {afterHitStatusId} 后效。"
                    );
                }
                if (expectBonusDamage)
                {
                    WeaponDice activeDice = fixture.Enemy.weapon_uses_two_hands
                        ? fixture.Enemy.weapon_two_handed_dice
                        : fixture.Enemy.weapon_one_handed_dice;
                    int fixedBaseDamage =
                        Math.Max(activeDice?.dice_count ?? 0, 0)
                        + (activeDice?.flat_bonus ?? 0);
                    _test.True(
                        damageDealt > fixedBaseDamage,
                        $"近战武器 {weaponItemId} 应正常触发额外伤害型 after-hit，actual={damageDealt}, base={fixedBaseDamage}。"
                    );
                }
            }
            _test.Eq(
                ActiveLayerId(FirstBarrier(fixture.State)),
                remainingLayerId,
                $"{weaponItemId} 的普通攻击不应破坏{remainingLayerLabel}。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(firstPreview);
            BattleTestFixture.DisposeBattlePreview(secondPreview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void AssertProjectedWeaponAbilityBarrierResult(
        StringName weaponItemId,
        StringName bindingId,
        StringName expectedCategory,
        bool expectedBlocked,
        string message,
        StringName syntheticProjectedCategory = default
    )
    {
        syntheticProjectedCategory = ProgressionDataUtils.to_string_name(
            syntheticProjectedCategory
        );
        using Fixture fixture = BuildRuntimeWithSphereAndProjectedWeapon(
            weaponItemId,
            bindingId,
            syntheticProjectedCategory: syntheticProjectedCategory
        );
        MarkLayersBroken(
            fixture.State,
            "red",
            "orange",
            "green",
            "blue",
            "indigo",
            "violet"
        );
        CombatEffectDefinition weaponDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            requiresWeapon: true,
            addWeaponDice: true,
            useWeaponPhysicalDamageTag: true,
            resolveAsWeaponAttack: true
        );
        SkillDefinition weaponSkill = TestSkillDefinitionProjection.BuildSkill(
            "projected_weapon_category_probe",
            displayName: "装备投射类别测试",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "projected_weapon_category_probe",
                effects: new[] { weaponDamage },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 10
            )
        );

        if (syntheticProjectedCategory != "")
        {
            StringName syntheticBindingId = BuildSyntheticProjectedBindingId(
                weaponItemId,
                syntheticProjectedCategory
            );
            _test.True(
                fixture.Runtime.GetEquipmentAbilityBindingIndexTyped().TryGetValue(
                    syntheticBindingId,
                    out EquipmentAbilityBindingDefinition syntheticBinding
                )
                    && syntheticBinding.Reactions.Any(
                        reaction =>
                            reaction.ProjectedEffectCategories.Contains(
                                syntheticProjectedCategory
                            )
                    ),
                $"测试夹具必须真实声明 {syntheticProjectedCategory} 投射类别。"
            );
            _test.Eq(
                fixture.Enemy.weapon_range_type,
                new StringName("melee"),
                $"{weaponItemId} 的运行态必须保持近战武器。"
            );
            _test.Eq(
                fixture.Runtime.GetItemDefIndexTyped()[weaponItemId].GetWeaponRangeType(),
                new StringName("melee"),
                $"{weaponItemId} 的正式物品定义必须保持近战武器。"
            );
        }

        IReadOnlyList<StringName> categories = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .CollectProjectedWeaponEffectCategories(
                fixture.Enemy,
                new[] { weaponDamage },
                weaponSkill
            );
        if (expectedCategory == "")
        {
            _test.Eq(
                categories.Count,
                0,
                $"{weaponItemId} must not contribute projected equipment categories."
            );
        }
        else
        {
            _test.True(
                categories.Contains(expectedCategory),
                $"{weaponItemId} must contribute {expectedCategory}."
            );
        }

        BattleBarrierInteractionResult result = fixture.Runtime._layered_barrier_service
            .ResolveSkillBarrierInteractionResult(
                fixture.Enemy,
                fixture.Caster,
                weaponSkill,
                new[] { weaponDamage },
                new BattleEventBatch()
            );
        _test.Eq(result.Blocked, expectedBlocked, message);

        IReadOnlyList<Vector2I> effectCoords = new[] { fixture.Caster.coord };
        BattleGroundEffectBarrierClipResult preview = fixture.Runtime._layered_barrier_service
            .PreviewGroundEffectBarrierClipResult(
                fixture.State.GetUnitView(fixture.Enemy.unit_id),
                weaponSkill,
                new[] { weaponDamage },
                Array.Empty<CombatEffectDefinition>(),
                effectCoords
            );
        _test.Eq(
            preview.UnitEffects.BlockedCoords.Count > 0,
            expectedBlocked,
            $"{weaponItemId} ground preview must match projected unit-effect blocking."
        );

        BattleGroundEffectBarrierClipResult terrainOnlyPreview = fixture.Runtime
            ._layered_barrier_service.PreviewGroundEffectBarrierClipResult(
                fixture.State.GetUnitView(fixture.Enemy.unit_id),
                weaponSkill,
                Array.Empty<CombatEffectDefinition>(),
                new[] { weaponDamage },
                effectCoords
            );
        _test.Eq(
            terrainOnlyPreview.TerrainEffects.BlockedCoords.Count,
            0,
            "装备附加类别只能阻挡投射到单位的效果，不能裁剪纯地形效果。"
        );
    }

    private void TestSingleLayerWardCommandsCreateExactlyOneLayer()
    {
        var wards = new (StringName SkillId, StringName ProfileId, StringName LayerId)[]
        {
            ("mage_prismatic_red_ward", "prismatic_red_ward", "red"),
            ("mage_prismatic_orange_ward", "prismatic_orange_ward", "orange"),
            ("mage_prismatic_yellow_ward", "prismatic_yellow_ward", "yellow"),
            ("mage_prismatic_green_ward", "prismatic_green_ward", "green"),
            ("mage_prismatic_blue_ward", "prismatic_blue_ward", "blue"),
            ("mage_prismatic_indigo_ward", "prismatic_indigo_ward", "indigo"),
            ("mage_prismatic_violet_ward", "prismatic_violet_ward", "violet"),
        };

        foreach ((StringName skillId, StringName profileId, StringName layerId) in wards)
        {
            AssertSingleLayerWardCommand(skillId, profileId, layerId, 1, 40);
        }
        AssertSingleLayerWardCommand(
            "mage_prismatic_red_ward",
            "prismatic_red_ward",
            "red",
            3,
            60
        );
        AssertSingleLayerWardCommand(
            "mage_prismatic_red_ward",
            "prismatic_red_ward",
            "red",
            5,
            80
        );
    }

    private void AssertSingleLayerWardCommand(
        StringName skillId,
        StringName profileId,
        StringName layerId,
        int skillLevel,
        int expectedDurationTu
    )
    {
        SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
            $"res://data/configs/skills/{skillId}.tres",
            $"prismatic_single_layer:{skillId}:level_{skillLevel}"
        );
        _test.True(skill?.CombatProfile != null, $"{skillId} must load as a combat skill.");
        if (skill?.CombatProfile == null)
            return;

        using MasteryCommandFixture fixture = BuildMasteryCommandFixture(skill, skillLevel);
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = fixture.Caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            target_unit_id = fixture.Caster.unit_id,
            target_coord = fixture.Caster.coord,
        };
        command.AddTargetUnitId(fixture.Caster.unit_id);
        BattleEventBatch batch = null;
        try
        {
            batch = fixture.Runtime.IssueCommand(command);
            BattleBarrierInstanceState barrier = FirstBarrier(fixture.State);
            _test.True(
                barrier is { IsEmpty: false },
                $"{skillId} level {skillLevel} must create a barrier instance."
            );
            if (barrier == null || barrier.IsEmpty)
                return;
            _test.Eq(barrier.ProfileId, profileId, $"{skillId} must use {profileId}.");
            _test.Eq(barrier.RadiusCells, 1, $"{skillId} must create a radius-1 barrier.");
            _test.Eq(
                barrier.RemainingTu,
                expectedDurationTu,
                $"{skillId} level {skillLevel} must use its configured duration."
            );
            _test.Eq(barrier.Layers.Count, 1, $"{skillId} must create exactly one layer.");
            _test.Eq(ActiveLayerId(barrier), layerId, $"{skillId} must create only {layerId}.");
        }
        finally
        {
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestPrismaticSphereCommandGrantsEffectAppliedMasteryOnce()
    {
        SkillDefinition sphereSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/mage_prismatic_sphere.tres",
            "prismatic_sphere:mastery_command"
        );
        _test.True(
            sphereSkill?.CombatProfile?.MasteryTriggerModeKind
                == CombatSkillMasteryTriggerMode.EffectApplied,
            "虹光法球正式资源应以 effect_applied 作为熟练度触发条件。"
        );
        AssertUnitSkillCommandGrantsMasteryOnce(
            sphereSkill,
            "虹光法球成功创建屏障后应恰好获得 1 点 effect_applied 熟练度。"
        );
    }

    private void TestCombinedGenericAndSpecialEffectsDoNotDoubleGrantMastery()
    {
        CombatEffectDefinition statusEffect = TestSkillDefinitionProjection.BuildEffect(
            "status",
            effectTargetTeamFilter: "self",
            statusId: "prismatic_mastery_marker",
            appliedStatusDurationTu: 30
        );
        CombatEffectDefinition barrierEffect = BuildLayeredBarrierEffect();
        SkillDefinition combinedSkill = TestSkillDefinitionProjection.BuildSkill(
            "test_prismatic_combined_mastery",
            displayName: "虹光法球复合熟练度测试",
            tags: new[] { new StringName("test"), new StringName("magic") },
            maxLevel: 2,
            masteryCurve: new[] { 400, 800 },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "test_prismatic_combined_mastery",
                effects: new[] { statusEffect, barrierEffect },
                targetMode: "unit",
                targetTeamFilter: "self",
                rangeValue: 0,
                areaPattern: "self",
                targetSelectionMode: "self",
                masteryTriggerMode: "effect_applied",
                masteryAmountMode: "per_target_rank"
            )
        );
        AssertUnitSkillCommandGrantsMasteryOnce(
            combinedSkill,
            "同一目标的通用状态与特殊屏障同时成功时，effect_applied 熟练度不得重复记账。"
        );
    }

    private void AssertUnitSkillCommandGrantsMasteryOnce(
        SkillDefinition skillDefinition,
        string assertionMessage
    )
    {
        _test.True(skillDefinition?.CombatProfile != null, "熟练度回归需要有效的技能定义。");
        if (skillDefinition?.CombatProfile == null)
        {
            return;
        }

        using MasteryCommandFixture fixture = BuildMasteryCommandFixture(skillDefinition);
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = fixture.Caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillDefinition.SkillId),
            skill_id = skillDefinition.SkillId,
            target_unit_id = fixture.Caster.unit_id,
            target_coord = fixture.Caster.coord,
        };
        command.AddTargetUnitId(fixture.Caster.unit_id);
        BattlePreview preview = null;
        BattleEventBatch batch = null;
        try
        {
            preview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                preview?.allowed == true,
                $"{skillDefinition.DisplayName} 的真实单位指令应允许自施放。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
            int masteryBefore = fixture.SkillProgress.current_mastery;

            batch = fixture.Runtime.IssueCommand(command);

            _test.True(
                FirstBarrier(fixture.State) is { IsEmpty: false },
                $"{skillDefinition.DisplayName} 指令成功后应创建分层屏障。"
            );
            _test.Eq(
                fixture.SkillProgress.current_mastery,
                masteryBefore + 1,
                assertionMessage
            );
            _test.Eq(
                batch?.ProgressionDeltasTyped.Count ?? 0,
                1,
                $"{skillDefinition.DisplayName} 应只产生一份熟练度 progression delta。"
            );
        }
        finally
        {
            batch?.Dispose();
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestPrismaticSphereCreatesOrderedLayers()
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleBarrierInstanceState barrier = FirstBarrier(fixture.State);
        _test.True(barrier != null && !barrier.IsEmpty, "虹光法球应写入 battle_state.layered_barrier_fields。");
        _test.Eq(ActiveLayerId(barrier), new StringName("red"), "新建虹光法球的第一活动层应为红色层。");
        _test.Eq(barrier.Layers.Count, 7, "虹光法球应包含 7 层。");
    }

    private void TestLayerDamageUsesConfiguredDamageTagMitigation()
    {
        AssertRedLayerDamage("", 20, "无火焰抗性的目标应承受完整红层伤害。");
        AssertRedLayerDamage("half", 10, "火焰半伤应把红层伤害减半。");
        AssertRedLayerDamage("double", 40, "火焰易伤应把红层伤害翻倍。");
        AssertRedLayerDamage("immune", 0, "火焰免疫应完全吸收红层伤害。");
    }

    private void AssertRedLayerDamage(
        StringName mitigationTier,
        int expectedDamage,
        string message
    )
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleUnitState target = fixture.Enemy;
        if (mitigationTier != "")
        {
            target.damage_resistances["fire"] = mitigationTier;
        }
        MarkLayersBroken(
            fixture.State,
            "orange",
            "yellow",
            "green",
            "blue",
            "indigo",
            "violet"
        );
        SetLayerSaveRollOverride(fixture.State, "red", 1);
        int hpBefore = target.current_hp;

        fixture.Runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
            target,
            new Vector2I(5, 2),
            new Vector2I(4, 2),
            new BattleEventBatch()
        );

        _test.Eq(hpBefore - target.current_hp, expectedDamage, message);
    }

    private void TestProjectedCategoriesRespectRemainingLayersWithoutCatchAll()
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState source = fixture.Enemy;
        BattleUnitState target = fixture.Caster;
        _test.False(
            FirstBarrier(state).CatchAllProjectedEffects,
            "虹光法球正式 profile 不应再使用投射效果 catch-all。"
        );

        var cases = new (StringName LayerId, StringName Category)[]
        {
            (new StringName("red"), new StringName("nonmagical_missile")),
            (new StringName("orange"), new StringName("magical_missile")),
            (new StringName("yellow"), new StringName("poison")),
            (new StringName("yellow"), new StringName("gas")),
            (new StringName("yellow"), new StringName("petrification")),
            (new StringName("green"), new StringName("breath_weapon")),
            (new StringName("blue"), new StringName("location")),
            (new StringName("blue"), new StringName("detection")),
            (new StringName("blue"), new StringName("mental_attack")),
            (new StringName("blue"), new StringName("psychic")),
            (new StringName("indigo"), new StringName("spell")),
            (new StringName("violet"), new StringName("force_effect")),
            (new StringName("violet"), new StringName("antimagic")),
        };
        foreach ((StringName layerId, StringName category) in cases)
        {
            SetOnlyRemainingLayer(state, layerId);
            SkillDefinition skill = BuildCategorizedSkill(
                $"test_prismatic_{category}",
                category
            );
            BattleBarrierInteractionResult result =
                runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                    source,
                    target,
                    skill,
                    Array.Empty<CombatEffectDefinition>(),
                    new BattleEventBatch()
                );
            _test.True(
                result.Blocked,
                $"{category} 应在 {layerId} 层仍存在时被阻挡。"
            );
        }

        SetAllLayersUnbroken(state);
        SkillDefinition multiCategorySkill = BuildCategorizedSkill(
            "test_prismatic_multi_category",
            "spell",
            "magical_missile"
        );
        var orangeBatch = new BattleEventBatch();
        BattleBarrierInteractionResult orangeResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                source,
                target,
                multiCategorySkill,
                Array.Empty<CombatEffectDefinition>(),
                orangeBatch
            );
        _test.True(orangeResult.Blocked, "多类别技能应被第一个仍存在的匹配层阻挡。");
        _test.True(
            LogsContain(orangeBatch.LogLinesTyped, "橙色层"),
            "魔法投射与法术类别同时存在时，完整法球应先由橙色层阻挡。"
        );

        MarkLayersBroken(state, "orange");
        var indigoBatch = new BattleEventBatch();
        BattleBarrierInteractionResult indigoResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                source,
                target,
                multiCategorySkill,
                Array.Empty<CombatEffectDefinition>(),
                indigoBatch
            );
        _test.True(indigoResult.Blocked, "橙色层破坏后，仍存在的靛色法术层应继续阻挡。");
        _test.True(
            LogsContain(indigoBatch.LogLinesTyped, "靛色层"),
            "橙色层破坏后应报告实际阻挡的靛色层。"
        );

        MarkLayersBroken(state, "indigo");
        BattleBarrierInteractionResult passResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                source,
                target,
                multiCategorySkill,
                Array.Empty<CombatEffectDefinition>(),
                new BattleEventBatch()
            );
        _test.False(
            passResult.Blocked,
            "魔法投射与法术对应层都被破坏后，其他无关色层不得兜底阻挡。"
        );

        SetAllLayersUnbroken(state);
        SkillDefinition unmatchedSkill = BuildCategorizedSkill(
            "test_prismatic_unmatched",
            "unmatched_projected_effect"
        );
        BattleBarrierInteractionResult unmatchedResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                source,
                target,
                unmatchedSkill,
                Array.Empty<CombatEffectDefinition>(),
                new BattleEventBatch()
            );
        _test.False(
            unmatchedResult.Blocked,
            "未匹配任何色层的投射类别必须穿透完整虹光法球。"
        );
    }

    private void TestGroundEffectWithUnmatchedCategoryPassesBarrier()
    {
        using Fixture fixture = BuildRuntimeWithSphere(enemyCoord: new Vector2I(6, 2));
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "fire"
        );
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "test_prismatic_unmatched_ground",
            displayName: "未分类地面投射测试",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "test_prismatic_unmatched_ground",
                effects: new[] { damageEffect },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                rangeValue: 4,
                deliveryCategories: new[] { new StringName("unmatched_projected_effect") }
            )
        );
        Vector2I[] effectCoords =
        {
            new(5, 2),
            new(4, 2),
            new(3, 2),
        };

        BattleGroundEffectBarrierClipResult result =
            fixture.Runtime._layered_barrier_service.ResolveGroundEffectBarrierClipResult(
                fixture.Enemy,
                skill,
                new[] { damageEffect },
                Array.Empty<CombatEffectDefinition>(),
                effectCoords,
                new BattleEventBatch()
            );

        _test.False(result.Applied, "未匹配类别的地面投射不应触发屏障裁剪。");
        _test.Eq(result.UnitEffects.AllowedCoords.Count, 3, "全部跨界地格都应保留。");
        _test.Eq(result.UnitEffects.BlockedCoords.Count, 0, "不得产生被兜底裁剪的地格。");
    }

    private void TestGroundAoePreviewAndExecutionClipAtBarrierBoundary()
    {
        SkillDefinition groundAoe = BuildGroundAoeSkill("test_prismatic_ground_aoe");
        using Fixture fixture = BuildRuntimeWithSphere(
            groundAoe,
            mapSize: new Vector2I(8, 5),
            enemyCoord: new Vector2I(6, 2)
        );
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState source = fixture.Enemy;
        LearnSkill(source, groundAoe.SkillId);

        BattleUnitState outsideTarget = BuildUnit(
            "outside_target",
            "法球外目标",
            "player",
            new Vector2I(4, 1)
        );
        BattleUnitState insideTarget = BuildUnit(
            "inside_target",
            "法球内目标",
            "player",
            new Vector2I(3, 2)
        );
        BattleUnitState boundaryTarget = BuildUnit(
            "boundary_target",
            "跨边界大体型目标",
            "player",
            new Vector2I(4, 2)
        );
        _test.True(
            boundaryTarget.SetBodySizeCategory("large"),
            "测试前置：跨边界目标应使用真实的大体型占用格。"
        );
        AddUnit(runtime, state, outsideTarget, false);
        AddUnit(runtime, state, insideTarget, false);
        AddUnit(runtime, state, boundaryTarget, false);
        state.active_unit_id = source.unit_id;
        runtime.SetupStateForTests(state);

        BattleCommand command = BuildGroundSkillCommand(
            source.unit_id,
            groundAoe.SkillId,
            new Vector2I(4, 2)
        );
        BattlePreview preview = runtime.PreviewCommand(command);

        _test.True(preview.allowed, "跨越虹光法球边界的地面 AoE 应允许施放。");
        _test.True(preview.ContainsTargetCoord(new Vector2I(5, 2)), "预览应保留法球外生效格。");
        _test.False(preview.ContainsTargetCoord(new Vector2I(4, 2)), "预览应裁掉进入法球的目标格。");
        _test.False(preview.ContainsTargetCoord(new Vector2I(3, 2)), "预览应裁掉继续深入法球的地格。");
        _test.True(
            preview.ContainsTargetUnitId(boundaryTarget.unit_id),
            "大体型单位任一占用格仍在允许范围内时，预览应保留该单位。"
        );
        _test.False(
            preview.ContainsTargetUnitId(insideTarget.unit_id),
            "只有被裁剪占用格被覆盖的单位不应出现在预览中。"
        );
        _test.Eq(
            ActiveLayerId(FirstBarrier(state)),
            new StringName("red"),
            "只读预览不得改变虹光法球活动层。"
        );

        int outsideHpBefore = outsideTarget.current_hp;
        int insideHpBefore = insideTarget.current_hp;
        int boundaryHpBefore = boundaryTarget.current_hp;
        BattleEventBatch batch = runtime.IssueCommand(command);

        _test.True(outsideTarget.current_hp < outsideHpBefore, "法球外允许地格上的单位应受到 AoE。");
        _test.Eq(insideTarget.current_hp, insideHpBefore, "法球内被裁剪地格上的单位不应受到 AoE。");
        _test.True(
            boundaryTarget.current_hp < boundaryHpBefore,
            "跨边界大体型单位应按允许占用格命中，而不是按锚点整只阻挡。"
        );
        _test.True(
            LogsContain(batch.LogLinesTyped, "阻挡了") && LogsContain(batch.LogLinesTyped, "2 个地格"),
            "执行日志应聚合报告本次被虹光法球裁剪的地格数量。"
        );
    }

    private void TestGroundAoeAutoCastClipsAtBarrierBoundary()
    {
        SkillDefinition groundAoe = BuildGroundAoeSkill("test_prismatic_auto_ground_aoe");
        using Fixture fixture = BuildRuntimeWithSphere(
            groundAoe,
            mapSize: new Vector2I(8, 5),
            enemyCoord: new Vector2I(6, 2)
        );
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState source = fixture.Enemy;
        LearnSkill(source, groundAoe.SkillId);
        BattleUnitState outsideTarget = BuildUnit(
            "auto_outside_target",
            "自动施法外侧目标",
            "player",
            new Vector2I(5, 2)
        );
        BattleUnitState insideTarget = BuildUnit(
            "auto_inside_target",
            "自动施法内侧目标",
            "player",
            new Vector2I(3, 2)
        );
        AddUnit(runtime, state, outsideTarget, false);
        AddUnit(runtime, state, insideTarget, false);
        runtime.SetupStateForTests(state);

        ContingencyReleaseContext releaseContext = new()
        {
            InstanceId = "test:auto_ground_clip",
            SetupId = "auto_ground_clip",
            OwnerMemberId = "test_owner",
            OwnerUnitId = source.unit_id,
            CasterUnitId = source.unit_id,
            TriggerType = "affected_by_spell",
        };
        AutoCastRequest request = new()
        {
            CasterUnitId = source.unit_id,
            OwnerMemberId = "test_owner",
            OwnerUnitId = source.unit_id,
            SetupId = "auto_ground_clip",
            InstanceId = "test:auto_ground_clip",
            SourceSkillId = "test_contingency_source",
            SourceSkillLevel = 1,
            SourceSkillGrantSourceType = UnitSkillGrantSourceType.Player,
            StoredSkillId = groundAoe.SkillId,
            CastLevel = 1,
            TargetResolution = ContingencyTargetResolutionResult.GroundTarget(
                new Vector2I(4, 2),
                new[]
                {
                    new Vector2I(4, 2),
                    new Vector2I(5, 2),
                    new Vector2I(3, 2),
                    new Vector2I(4, 1),
                    new Vector2I(4, 3),
                }
            ),
            ReleaseContext = releaseContext,
        };
        int outsideHpBefore = outsideTarget.current_hp;
        int insideHpBefore = insideTarget.current_hp;
        var batch = new BattleEventBatch();

        bool executed = runtime._skill_orchestrator.ExecuteAutoCast(request, batch);

        _test.True(executed, "Contingency 自动地面施法应在部分地格被裁剪时成功执行。");
        _test.True(
            outsideTarget.current_hp < outsideHpBefore,
            "Contingency 自动施法应影响法球外允许地格。"
        );
        _test.Eq(
            insideTarget.current_hp,
            insideHpBefore,
            "Contingency 自动施法不应把效果送入法球内被裁剪地格。"
        );
    }

    private void TestGroundAoeTerrainClipAtBarrierBoundary()
    {
        StringName terrainEffectId = "test_prismatic_terrain_clip";
        SkillDefinition groundAoe = BuildGroundTerrainAoeSkill(
            "test_prismatic_ground_terrain_aoe",
            terrainEffectId
        );
        using Fixture fixture = BuildRuntimeWithSphere(
            groundAoe,
            mapSize: new Vector2I(8, 5),
            enemyCoord: new Vector2I(6, 2)
        );
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState source = fixture.Enemy;
        LearnSkill(source, groundAoe.SkillId);
        state.active_unit_id = source.unit_id;
        runtime.SetupStateForTests(state);
        BattleCommand command = BuildGroundSkillCommand(
            source.unit_id,
            groundAoe.SkillId,
            new Vector2I(4, 2)
        );

        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview.ContainsTargetCoord(new Vector2I(5, 2)), "地形预览应保留法球外地格。");
        _test.False(preview.ContainsTargetCoord(new Vector2I(3, 2)), "地形预览应裁掉法球内地格。");

        BattleEventBatch batch = runtime.IssueCommand(command);

        _test.True(
            CellHasTerrainEffect(runtime, state, new Vector2I(5, 2), terrainEffectId),
            "地形效果应写入法球外允许地格。"
        );
        _test.False(
            CellHasTerrainEffect(runtime, state, new Vector2I(3, 2), terrainEffectId),
            "地形效果不应写入法球内被裁剪地格。"
        );
        _test.True(
            CoordsContain(batch.ChangedCoordsTyped, new Vector2I(5, 2)),
            "允许地格的地形变化应进入 changed coords。"
        );
        _test.False(
            CoordsContain(batch.ChangedCoordsTyped, new Vector2I(3, 2)),
            "被裁剪地格不应伪造地形 changed coord。"
        );
    }

    private void TestGroundAoeBreakerClipsUnitAndTerrainWithoutSameCastPenetration()
    {
        StringName terrainEffectId = "test_prismatic_ground_mark";
        SkillDefinition groundAoe = BuildGroundAoeSkillWithTerrain(
            "mage_cone_of_cold",
            terrainEffectId
        );
        using Fixture fixture = BuildRuntimeWithSphere(
            groundAoe,
            mapSize: new Vector2I(8, 5),
            enemyCoord: new Vector2I(6, 2)
        );
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState source = fixture.Enemy;
        LearnSkill(source, groundAoe.SkillId);
        BattleUnitState outsideTarget = BuildUnit(
            "breaker_outside_target",
            "破解法术外侧目标",
            "player",
            new Vector2I(5, 2)
        );
        BattleUnitState insideTarget = BuildUnit(
            "breaker_inside_target",
            "破解法术内侧目标",
            "player",
            new Vector2I(3, 2)
        );
        AddUnit(runtime, state, outsideTarget, false);
        AddUnit(runtime, state, insideTarget, false);
        state.active_unit_id = source.unit_id;
        runtime.SetupStateForTests(state);
        BattleCommand command = BuildGroundSkillCommand(
            source.unit_id,
            groundAoe.SkillId,
            new Vector2I(4, 2)
        );

        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview.allowed, "破解当前层的地面 AoE 应允许施放。");
        _test.Eq(
            ActiveLayerId(FirstBarrier(state)),
            new StringName("red"),
            "破解法术预览不得提前破坏红色层。"
        );
        int outsideHpBefore = outsideTarget.current_hp;
        int insideHpBefore = insideTarget.current_hp;

        runtime.IssueCommand(command);

        _test.Eq(
            ActiveLayerId(FirstBarrier(state)),
            new StringName("orange"),
            "一次地面 AoE 应只提交一次红层破解。"
        );
        _test.True(
            outsideTarget.current_hp < outsideHpBefore,
            "破解法术仍应影响法球外允许地格上的单位。"
        );
        _test.Eq(
            insideTarget.current_hp,
            insideHpBefore,
            "本次施法不得借刚破解的红层继续影响法球内单位。"
        );
        _test.True(
            CellHasTerrainEffect(runtime, state, new Vector2I(5, 2), terrainEffectId),
            "破解法术的地形效果应写入法球外允许地格。"
        );
        _test.False(
            CellHasTerrainEffect(runtime, state, new Vector2I(3, 2), terrainEffectId),
            "本次施法的地形效果不得穿透刚破解的红层。"
        );
    }

    private void TestGroundAoePendingCastClipsAtBarrierBoundary()
    {
        SkillDefinition groundAoe = BuildGroundAoeSkill(
            "test_prismatic_pending_ground_aoe",
            castingTimeTu: 10
        );
        using Fixture fixture = BuildRuntimeWithSphere(
            groundAoe,
            mapSize: new Vector2I(8, 5),
            enemyCoord: new Vector2I(6, 2)
        );
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState source = fixture.Enemy;
        LearnSkill(source, groundAoe.SkillId);
        BattleUnitState outsideTarget = BuildUnit(
            "pending_outside_target",
            "读条外侧目标",
            "player",
            new Vector2I(5, 2)
        );
        BattleUnitState insideTarget = BuildUnit(
            "pending_inside_target",
            "读条内侧目标",
            "player",
            new Vector2I(3, 2)
        );
        AddUnit(runtime, state, outsideTarget, false);
        AddUnit(runtime, state, insideTarget, false);
        runtime.SetupStateForTests(state);
        BattlePendingCastState pendingCast = new()
        {
            SourceUnitId = source.unit_id,
            SkillId = groundAoe.SkillId,
            TargetMode = BattleTargetMode.Ground,
            BindingMode = PendingCastBindingModeKind.GroundBind,
            StartedCoord = source.coord,
            StartedTu = state.timeline?.current_tu ?? 0,
            BaseCastingTimeTu = 10,
            RemainingCastProgress = 0,
            LastMaintenanceCheckpointHp = source.current_hp,
        };
        pendingCast.SetTargetCoords(new[] { new Vector2I(4, 2) });
        int outsideHpBefore = outsideTarget.current_hp;
        int insideHpBefore = insideTarget.current_hp;
        var batch = new BattleEventBatch();

        bool resolved = runtime._skill_orchestrator.ResolvePendingCast(
            source,
            pendingCast,
            batch
        );

        _test.True(resolved, "读条地面法术应在部分地格被裁剪时成功释放。");
        _test.True(
            outsideTarget.current_hp < outsideHpBefore,
            "读条释放应影响法球外允许地格。"
        );
        _test.Eq(
            insideTarget.current_hp,
            insideHpBefore,
            "读条释放不应影响法球内被裁剪地格。"
        );
    }

    private void TestPrismaticSphereBlocksDeeperBreakersUntilOuterLayerBreaks()
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState caster = fixture.Caster;
        BattleUnitState enemy = fixture.Enemy;
        var batch = new BattleEventBatch();

        SkillDefinition magicMissile = BuildSkill("mage_arcane_missile", "奥术飞弹", "mage", "magic");
        BattleBarrierInteractionResult blockedResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                magicMissile,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(blockedResult.Blocked, "外层仍在时，蓝色层破解法术应被阻挡。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("red"), "错误顺序的破解不应破坏红色层。");

        SkillDefinition coneOfCold = BuildSkill("mage_cone_of_cold", "寒冰锥", "mage", "magic", "freeze");
        BattleBarrierInteractionResult breakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                coneOfCold,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(breakResult.Blocked, "正确破解法术应被法球消耗。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("orange"), "寒冰锥应只破坏最外侧红色层。");

        SkillDefinition gustOfWind = BuildSkill("mage_gust_of_wind", "强风术", "mage", "magic", "air");
        BattleBarrierInteractionResult orangeBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                gustOfWind,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(orangeBreakResult.Blocked, "强风术应被虹光法球消耗以破解橙色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("yellow"), "强风术应在红层破除后破解橙色层。");

        SkillDefinition disintegrate = BuildSkill("mage_spell_disjunction", "裂解术", "mage", "magic", "arcane");
        BattleBarrierInteractionResult yellowBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                disintegrate,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(yellowBreakResult.Blocked, "裂解术应被虹光法球消耗以破解黄色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("green"), "裂解术应在橙层破除后破解黄色层。");

        SkillDefinition passwall = BuildSkill("mage_passwall", "穿墙术", "mage", "magic", "earth");
        BattleBarrierInteractionResult greenBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                passwall,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(greenBreakResult.Blocked, "穿墙术应被虹光法球消耗以破解绿色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("blue"), "穿墙术应在黄层破除后破解绿色层。");

        BattleBarrierInteractionResult blueBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                magicMissile,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(blueBreakResult.Blocked, "奥术飞弹应被虹光法球消耗以破解蓝色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("indigo"), "奥术飞弹应在绿层破除后破解蓝色层。");

        SkillDefinition continualLight = BuildSkill("mage_continual_light", "恒光术", "mage", "magic", "radiant");
        BattleBarrierInteractionResult indigoBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                continualLight,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(indigoBreakResult.Blocked, "恒光术应被虹光法球消耗以破解靛色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("violet"), "恒光术应在蓝层破除后破解靛色层。");

        SkillDefinition dispelMagic = BuildSkill("mage_dispel_magic", "解除魔法", "mage", "magic", "dispel");
        BattleBarrierInteractionResult violetBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                dispelMagic,
                Array.Empty<CombatEffectDefinition>(),
                batch
            );
        _test.True(violetBreakResult.Blocked, "解除魔法应被虹光法球消耗以破解紫色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName(""), "解除魔法应在靛层破除后破解最后的紫色层。");
    }

    private void TestProjectedEffectBarrierGeometryRespectsBoundary()
    {
        List<Vector2I> barrierCoords = DiamondArea(new Vector2I(2, 2), 2);
        _test.False(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(2, 2),
                new Vector2I(3, 2),
                barrierCoords
            ),
            "法球内部到内部的投射效果不应被屏障拦截。"
        );
        _test.True(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(2, 2),
                new Vector2I(5, 2),
                barrierCoords
            ),
            "法球内部到外部的投射效果应被屏障拦截。"
        );
        _test.True(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 2),
                new Vector2I(2, 2),
                barrierCoords
            ),
            "法球外部到内部的投射效果应被屏障拦截。"
        );
        _test.True(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 2),
                new Vector2I(-1, 2),
                barrierCoords
            ),
            "法球外部到外部但线段穿过屏障时应被拦截。"
        );
        _test.False(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 4),
                new Vector2I(6, 4),
                barrierCoords
            ),
            "法球外部到外部且未穿过屏障时不应被拦截。"
        );
    }

    private void TestDeathWardWithoutLastStandDoesNotBlockFatalPhysicalDamage()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState source = BuildUnit(
            "plain_fatal_source",
            "普通致命攻击者",
            "enemy",
            new Vector2I(0, 0)
        );
        BattleUnitState target = BuildUnit(
            "spellward_only_target",
            "仅有负能量免疫目标",
            "player",
            new Vector2I(1, 0)
        );
        target.current_hp = 8;
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 8);
        SetStatus(target, "death_ward", new GDictionary { ["damage_tag"] = "negative_energy" });

        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 99,
            damageTag: "physical_slash"
        );
        using GodotProjectionLease<GDictionary> resultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
                resolver.ResolveEffects(
                source,
                target,
                EffectArray(damageEffect)
            )
        );
        GDictionary result = resultLease.Value;
        _test.Eq(
            result != null && result.ContainsKey("damage") ? result["damage"].AsInt32() : -1,
            8,
            "非 Last Stand 来源的 death_ward 不应吞掉普通致命 HP 伤害，damage 字段应记录实际 HP 损失。"
        );
        _test.Eq(target.current_hp, 0, "非 Last Stand 来源的 death_ward 遭遇普通致命伤害时应正常归零。");
        _test.False(target.is_alive, "非 Last Stand 来源的 death_ward 不应阻止死亡状态。");
    }

    private void TestGreenLayerInstantDeathUsesFatalDamageChain()
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState enemy = fixture.Enemy;
        SkillDefinition lastStandSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_last_stand.tres",
            "prismatic_sphere:res://data/configs/skills/warrior_last_stand.tres"
        );
        _test.True(
            lastStandSkill != null && lastStandSkill.CombatProfile != null,
            "绿色层即死回归需要 warrior_last_stand 技能资源。"
        );
        if (lastStandSkill == null || lastStandSkill.CombatProfile == null)
        {
            return;
        }

        runtime.GetDamageResolver().SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [(StringName)"warrior_last_stand"] = lastStandSkill
            }
        );
        enemy.current_hp = 8;
        SetStatus(
            enemy,
            "death_ward",
            sourceSkillId: "warrior_last_stand",
            sourceSkillLevel: 7
        );
        SetStatus(enemy, "staggered");
        MarkLayersBroken(state, "red", "orange", "yellow", "blue", "indigo", "violet");
        SetLayerSaveRollOverride(state, "green", 1);
        AssertActiveLayerSaveRollOverride(state, "green", 1);

        BattleBarrierInteractionResult result =
            runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
                enemy,
                new Vector2I(5, 2),
                new Vector2I(4, 2),
                new BattleEventBatch()
            );
        _test.False(result.Blocked, "不屈抵消绿色层即死后，穿越不应因死亡终止。");
        _test.True(enemy.is_alive && enemy.current_hp > 0, "绿色层即死应触发现有免死链并把目标救回正 HP。");
        _test.False(enemy.HasStatusEffect("death_ward"), "绿色层即死触发不屈后应消耗 death_ward。");
        _test.False(enemy.HasStatusEffect("staggered"), "Lv5+ 不屈触发后仍应清理负面状态。");
        _test.True(enemy.HasStatusEffect("last_stand_active"), "Lv7 不屈触发后应保留 last_stand_active。");
    }

    private void TestPetrifiedBlocksTurnUntilSelfSaveSucceeds()
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleUnitState target = fixture.Enemy;
        var batch = new BattleEventBatch();
        var petrified = new BattleStatusEffectState
        {
            status_id = "petrified",
            source_unit_id = "caster",
            power = 1,
            stacks = 1,
            duration = -1,
            self_save_ability = "constitution",
            self_save_dc = 15,
            self_save_roll_override = 1,
            self_save_tag = "constitution",
        };
        target.SetStatusEffect(petrified);

        BattleTurnControlStatusResult failResult =
            runtime._skill_turn_resolver.ResolveTurnControlStatusResult(target, batch);
        _test.True(failResult.SkipTurn, "石化自检失败应跳过行动。");
        _test.True(target.HasStatusEffect("petrified"), "石化失败后状态应保留。");

        BattleStatusEffectState entry = target.GetStatusEffect("petrified");
        entry.self_save_roll_override = 20;
        target.SetStatusEffect(entry);
        BattleTurnControlStatusResult successResult =
            runtime._skill_turn_resolver.ResolveTurnControlStatusResult(target, batch);
        _test.False(successResult.SkipTurn, "石化自检成功应允许本次行动继续。");
        _test.False(target.HasStatusEffect("petrified"), "石化自检成功应解除石化。");
    }

    private void TestVioletLayerTeleportsNonSummonsAndRemovesSummons()
    {
        using Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState enemy = fixture.Enemy;
        var batch = new BattleEventBatch();
        MarkLayersBroken(state, "red", "orange", "yellow", "green", "blue", "indigo");
        SetLayerSaveRollOverride(state, "violet", 1);
        AssertActiveLayerSaveRollOverride(state, "violet", 1);

        BattleBarrierInteractionResult result =
            runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
                enemy,
                new Vector2I(5, 2),
                new Vector2I(4, 2),
                batch
            );
        _test.True(result.Blocked, "紫色层放逐应终止本次穿越。");
        _test.True(enemy.is_alive, "非召唤物被紫色层命中后应保留存活状态。");
        _test.False(
            CoordInsideBarrier(enemy.coord, FirstBarrier(state)),
            "非召唤物应被传送到法球外合法坐标。"
        );

        BattleUnitState summon = BuildUnit("summon", "召唤物", "enemy", new Vector2I(6, 2));
        summon.ai_blackboard.summoned = true;
        AddUnit(runtime, state, summon, true);
        BattleBarrierInteractionResult summonResult =
            runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
                summon,
                new Vector2I(6, 2),
                new Vector2I(4, 2),
                batch
            );
        _test.True(summonResult.Blocked, "召唤物被放逐也应终止穿越。");
        _test.False(summon.is_alive, "召唤物应被紫色层直接移除。");
    }

    private void TestCleanseHarmfulRemovesMadnessButNotPetrified()
    {
        BattleUnitState source = BuildUnit("source", "施法者", "player", Vector2I.Zero);
        BattleUnitState target = BuildUnit("target", "目标", "player", new Vector2I(1, 0));
        SetStatus(target, "madness");
        SetStatus(target, "petrified");
        var cleanse = TestSkillDefinitionProjection.BuildEffect("cleanse_harmful");
        var resolver = new BattleDamageResolver();
        resolver.ResolveEffects(source, target, EffectArray(cleanse));
        _test.False(target.HasStatusEffect("madness"), "cleanse_harmful 应解除 madness。");
        _test.True(target.HasStatusEffect("petrified"), "cleanse_harmful 不应解除 petrified。");
    }

    private void TestDispelMagicRemovesMagicStatusesByRelation()
    {
        BattleUnitState source = BuildUnit("source", "施法者", "player", Vector2I.Zero);
        BattleUnitState ally = BuildUnit("ally", "友方", "player", new Vector2I(1, 0));
        BattleUnitState enemy = BuildUnit("enemy", "敌方", "enemy", new Vector2I(2, 0));
        var dispel = TestSkillDefinitionProjection.BuildEffect(
            "dispel_magic",
            power: 1,
            maxStatusRemoved: 1
        );
        var resolver = new BattleDamageResolver();

        SetStatus(ally, "blind");
        SetStatus(ally, "petrified");
        using GodotProjectionLease<GDictionary> allyResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
                resolver.ResolveEffects(source, ally, EffectArray(dispel))
            );
        GDictionary allyResult = allyResultLease.Value;
        _test.True(DictBool(allyResult, "applied"), "解除魔法命中友方时应能移除可驱散减益。");
        _test.False(ally.HasStatusEffect("blind"), "解除魔法应移除友方 blind。");
        _test.True(ally.HasStatusEffect("petrified"), "解除魔法不应移除 petrified。");
        _test.True(
            DictArrayHasStringName(allyResult, "removed_status_effect_ids", "blind"),
            "解除魔法结果应报告被移除的友方状态。"
        );

        SetStatus(enemy, "magic_shield");
        SetStatus(enemy, "attack_up");
        SetStatus(enemy, "marked");
        using GodotProjectionLease<GDictionary> enemyResultLease =
            AttackEffectResolutionResultReader.BuildGodotPayloadLease(
                resolver.ResolveEffects(source, enemy, EffectArray(dispel))
            );
        GDictionary enemyResult = enemyResultLease.Value;
        _test.True(DictBool(enemyResult, "applied"), "解除魔法命中敌方时应能移除可驱散增益。");
        _test.False(enemy.HasStatusEffect("magic_shield"), "解除魔法应优先移除敌方高优先级魔法增益。");
        _test.True(enemy.HasStatusEffect("attack_up"), "单次解除魔法只应移除配置数量内的敌方增益。");
        _test.True(enemy.HasStatusEffect("marked"), "解除魔法不应移除敌方身上的有害状态。");
        _test.True(
            DictArrayHasStringName(enemyResult, "removed_status_effect_ids", "magic_shield"),
            "解除魔法结果应报告被移除的敌方状态。"
        );
    }

    private MasteryCommandFixture BuildMasteryCommandFixture(
        SkillDefinition skillDefinition,
        int skillLevel = 1
    )
    {
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [skillDefinition.SkillId] = skillDefinition,
        };
        var progression = new UnitProgress
        {
            unit_id = "hero",
            display_name = "虹光法球施法者",
        };
        var skillProgress = new UnitSkillProgress
        {
            skill_id = skillDefinition.SkillId,
            is_learned = true,
            skill_level = skillLevel,
            current_mastery = 0,
            total_mastery_earned = 0,
            granted_source_type = "player",
        };
        progression.SetSkillProgress(skillProgress);
        var memberState = new PartyMemberState
        {
            member_id = "hero",
            display_name = "虹光法球施法者",
            progression = progression,
            current_hp = 120,
            current_mp = 240,
        };
        var partyState = new PartyState
        {
            leader_member_id = memberState.member_id,
            main_character_member_id = memberState.member_id,
            active_member_ids = new StringNameList { memberState.member_id },
        };
        partyState.SetMemberState(memberState);

        var characterManagement = new CharacterManagementModule();
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        BattleUnitState caster = null;
        BattleUnitState enemy = null;
        try
        {
            characterManagement.setup(partyState, skillDefinitions);
            runtime.setup(
                characterManagement,
                skillDefinitions,
                barrier_profile_definitions: _barrierProfileDefinitions
            );
            state = BuildState(new Vector2I(7, 5));
            caster = BuildUnit("mastery_caster", "虹光法球施法者", "player", new Vector2I(2, 2));
            caster.source_member_id = memberState.member_id;
            caster.current_ap = 5;
            caster.current_mp = 240;
            caster.UnlockCombatResource(
                CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
            );
            caster.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.MpMax),
                240
            );
            LearnSkill(caster, skillDefinition.SkillId, skillLevel);
            enemy = BuildUnit("mastery_enemy", "熟练度见证者", "enemy", new Vector2I(5, 2));
            AddUnit(runtime, state, caster, false);
            AddUnit(runtime, state, enemy, true);
            state.active_unit_id = caster.unit_id;
            runtime.SetupStateForTests(state);
            return new MasteryCommandFixture(
                runtime,
                state,
                caster,
                enemy,
                characterManagement,
                skillProgress
            );
        }
        catch
        {
            runtime.Dispose();
            characterManagement.Dispose();
            BattleTestFixture.DisposeBattleUnit(caster);
            BattleTestFixture.DisposeBattleUnit(enemy);
            BattleTestFixture.DisposeBattleState(state);
            throw;
        }
    }

    private Fixture BuildRuntimeWithSphere(
        SkillDefinition additionalSkill = null,
        Vector2I? mapSize = null,
        Vector2I? enemyCoord = null
    )
    {
        var runtime = new BattleRuntimeModule();
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            additionalSkill != null
                ? new Dictionary<StringName, SkillDefinition>
                {
                    [additionalSkill.SkillId] = additionalSkill,
                }
                : null;
        runtime.setup(
            skill_definitions: skillDefinitions,
            barrier_profile_definitions: _barrierProfileDefinitions
        );
        BattleState state = BuildState(mapSize ?? new Vector2I(7, 5));
        runtime.SetupStateForTests(state);
        BattleUnitState caster = BuildUnit("caster", "施法者", "player", new Vector2I(2, 2));
        BattleUnitState enemy = BuildUnit(
            "enemy",
            "敌人",
            "enemy",
            enemyCoord ?? new Vector2I(5, 2)
        );
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, enemy, true);
        SkillDefinition skill = BuildSkill("mage_prismatic_sphere", "虹光法球", "mage", "magic");
        CombatEffectDefinition effect = BuildLayeredBarrierEffect();
        runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
            caster,
            caster,
            skill,
            effect,
            new BattleEventBatch()
        );
        return new Fixture(runtime, state, caster, enemy);
    }

    private Fixture BuildRuntimeWithSphereAndProjectedWeapon(
        StringName weaponItemId,
        StringName bindingId,
        bool useBoundaryTargetGeometry = false,
        StringName syntheticProjectedCategory = default
    )
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        syntheticProjectedCategory = ProgressionDataUtils.to_string_name(
            syntheticProjectedCategory
        );
        var equipmentAbilityBindings = new Dictionary<
            StringName,
            EquipmentAbilityBindingDefinition
        >(snapshot.EquipmentAbilityBindings);
        StringName syntheticBindingId = "";
        if (syntheticProjectedCategory != "")
        {
            syntheticBindingId = BuildSyntheticProjectedBindingId(
                weaponItemId,
                syntheticProjectedCategory
            );
            equipmentAbilityBindings[syntheticBindingId] =
                new EquipmentAbilityBindingDefinition
                {
                    BindingId = syntheticBindingId,
                    Reactions = new[]
                    {
                        new EquipmentAbilityReactionDefinition
                        {
                            ReactionId = $"reaction.{syntheticBindingId}",
                            Trigger = EquipmentAbilityTriggerKind.OnHit,
                            Timing = EquipmentAbilityTimingKind.AfterHit,
                            ProjectedEffectCategories = new[]
                            {
                                syntheticProjectedCategory,
                            },
                        },
                    },
                };
        }
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            skill_definitions: snapshot.Skills,
            item_defs: snapshot.Items,
            trait_defs: snapshot.Traits,
            equipment_ability_bindings: equipmentAbilityBindings,
            barrier_profile_definitions: _barrierProfileDefinitions
        );
        runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(
                new GArray { 1, 1, 1, 1, 1, 1 },
                new GArray { 1, 1, 1, 1, 1, 1 }
            )
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(15));
        BattleState state = BuildState(new Vector2I(7, 5));
        runtime.SetupStateForTests(state);
        BattleUnitState barrierOwner = BuildUnit(
            "projected_weapon_barrier_owner",
            "屏障创建者",
            "player",
            new Vector2I(2, 2)
        );
        BattleUnitState target = useBoundaryTargetGeometry
            ? BuildUnit(
                "projected_weapon_target",
                "屏障内目标",
                "player",
                new Vector2I(4, 2)
            )
            : barrierOwner;
        BattleUnitState source = BuildUnit(
            "projected_weapon_source",
            "装备攻击者",
            "enemy",
            new Vector2I(5, 2)
        );
        ItemDefinition weaponDefinition = snapshot.Items[weaponItemId];
        WeaponProfileDefinition weaponProfile = weaponDefinition.WeaponProfile;
        source.weapon_profile_kind = BattleUnitState.ToStringName(
            BattleWeaponProfileKind.Equipped
        );
        source.weapon_item_id = weaponItemId;
        source.weapon_profile_type_id = weaponProfile?.WeaponTypeId ?? new StringName("");
        source.weapon_range_type = weaponDefinition.GetWeaponRangeType();
        source.weapon_family = weaponProfile?.Family ?? new StringName("");
        source.weapon_attack_range = weaponDefinition.GetWeaponAttackRange();
        source.weapon_one_handed_dice = BuildWeaponDice(weaponProfile?.OneHandedDice);
        source.weapon_two_handed_dice = BuildWeaponDice(weaponProfile?.TwoHandedDice);
        source.weapon_is_versatile =
            weaponProfile?.OneHandedDice != null && weaponProfile?.TwoHandedDice != null;
        source.weapon_uses_two_hands =
            weaponProfile?.OneHandedDice == null && weaponProfile?.TwoHandedDice != null;
        source.weapon_current_grip = BattleUnitState.ToStringName(
            source.weapon_uses_two_hands
                ? BattleWeaponGripKind.TwoHanded
                : BattleWeaponGripKind.OneHanded
        );
        source.weapon_physical_damage_tag = weaponDefinition.GetWeaponPhysicalDamageTag();
        source.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        source.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        source.equipment_ability_sources.Add(
            new BattleEquipmentAbilitySourceState
            {
                EffectiveInstanceKey = $"projected:{weaponItemId}",
                EquipmentDefId = weaponItemId,
                SourceEquipmentInstanceId = $"projected_instance:{weaponItemId}",
                SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                AbilityIds =
                    syntheticBindingId == ""
                        ? new List<StringName> { bindingId }
                        : new List<StringName> { bindingId, syntheticBindingId },
            }
        );
        AddUnit(runtime, state, barrierOwner, false);
        if (!ReferenceEquals(target, barrierOwner))
            AddUnit(runtime, state, target, false);
        AddUnit(runtime, state, source, true);
        SkillDefinition sphereSkill = BuildSkill(
            "mage_prismatic_sphere",
            "虹光法球",
            "mage",
            "magic"
        );
        runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
            barrierOwner,
            barrierOwner,
            sphereSkill,
            BuildLayeredBarrierEffect(),
            new BattleEventBatch()
        );
        return new Fixture(
            runtime,
            state,
            target,
            source,
            ReferenceEquals(target, barrierOwner) ? null : barrierOwner
        );
    }

    private static StringName BuildSyntheticProjectedBindingId(
        StringName weaponItemId,
        StringName projectedCategory
    ) => $"test.binding.{weaponItemId}.{projectedCategory}.projected";

    private static WeaponDice BuildWeaponDice(WeaponDamageDiceDefinition definition)
    {
        return definition == null
            ? new WeaponDice()
            : new WeaponDice
            {
                dice_count = definition.DiceCount,
                dice_sides = definition.DiceSides,
                flat_bonus = definition.FlatBonus,
            };
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "prismatic_sphere_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 120,
            current_mp = 120,
            current_stamina = 40,
            current_ap = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), AttributeService.BASE_ARMOR_CLASS);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("intelligence", 14);
        unit.attribute_snapshot.SetValue("constitution_modifier", 0);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 2);
        return unit;
    }

    private static SkillDefinition BuildSkill(StringName skillId, string displayName, params StringName[] tags)
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: displayName,
            tags: tags
        );
    }

    private static SkillDefinition BuildCategorizedSkill(
        StringName skillId,
        params StringName[] deliveryCategories
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 10,
                deliveryCategories: deliveryCategories
            )
        );
    }

    private static CombatEffectDefinition BuildLayeredBarrierEffect()
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            durationTu: 120,
            saveDc: 15,
            saveDcMode: "static",
            saveAbility: "willpower",
            saveTag: "magic",
            parameters: new Dictionary<string, object>
            {
                ["area_pattern"] = "diamond",
                ["profile_id"] = "prismatic_sphere",
                ["radius_cells"] = 2,
            }
        );
    }

    private static SkillDefinition BuildGroundAoeSkill(StringName skillId, int castingTimeTu = 0)
    {
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "force"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "测试地面范围法术",
            tags: new[] { new StringName("test"), new StringName("magic") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damageEffect },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_cell",
                rangeValue: 4,
                areaPattern: "cross",
                areaValue: 1,
                castingTimeTu: castingTimeTu,
                pendingCastBindingMode: "ground_bind",
                deliveryCategories: new[] { new StringName("spell") }
            )
        );
    }

    private static SkillDefinition BuildGroundAoeSkillWithTerrain(
        StringName skillId,
        StringName terrainEffectId
    )
    {
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "freeze"
        );
        CombatEffectDefinition terrainEffect = TestSkillDefinitionProjection.BuildEffect(
            "terrain_effect",
            terrainEffectId: terrainEffectId,
            displayName: "测试法球地形标记"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "测试地面范围破解法术",
            tags: new[] { new StringName("test"), new StringName("magic") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damageEffect, terrainEffect },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_cell",
                rangeValue: 4,
                areaPattern: "cross",
                areaValue: 1,
                deliveryCategories: new[] { new StringName("spell") }
            )
        );
    }

    private static SkillDefinition BuildGroundTerrainAoeSkill(
        StringName skillId,
        StringName terrainEffectId
    )
    {
        CombatEffectDefinition terrainEffect = TestSkillDefinitionProjection.BuildEffect(
            "terrain_effect",
            terrainEffectId: terrainEffectId,
            displayName: "测试法球地形裁剪"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "测试地面地形范围法术",
            tags: new[] { new StringName("test"), new StringName("magic") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { terrainEffect },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_cell",
                rangeValue: 4,
                areaPattern: "cross",
                areaValue: 1,
                deliveryCategories: new[] { new StringName("spell") }
            )
        );
    }

    private static BattleCommand BuildGroundSkillCommand(
        StringName sourceUnitId,
        StringName skillId,
        Vector2I targetCoord
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = sourceUnitId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static void LearnSkill(
        BattleUnitState unitState,
        StringName skillId,
        int skillLevel = 1
    )
    {
        unitState.known_active_skill_ids.Add(skillId);
        unitState.known_skill_level_map[skillId] = skillLevel;
    }

    private static bool LogsContain(IEnumerable<string> logLines, string fragment)
    {
        foreach (string line in logLines ?? Array.Empty<string>())
        {
            if (line?.Contains(fragment, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }
        return false;
    }

    private static bool CellHasTerrainEffect(
        BattleRuntimeModule runtime,
        BattleState state,
        Vector2I coord,
        StringName terrainEffectId
    )
    {
        BattleCellState cell = runtime?._grid_service?.GetCellState(state, coord);
        return cell?.terrain_effect_ids?.Contains(terrainEffectId) == true;
    }

    private static bool CoordsContain(IEnumerable<Vector2I> coords, Vector2I expected)
    {
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            if (coord == expected)
            {
                return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<CombatEffectDefinition> EffectArray(
        params CombatEffectDefinition[] effects
    ) => effects ?? Array.Empty<CombatEffectDefinition>();

    private static void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        if (state == null || unit == null)
        {
            return;
        }
        state.SetUnit(unit);
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        runtime?._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private static void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        GDictionary parameters = null,
        StringName sourceSkillId = default,
        int? sourceSkillLevel = null
    )
    {
        var status = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = "source",
            power = 1,
            stacks = 1,
            duration = -1,
            @params = OwnedDictionary(
                parameters?.Duplicate(true) as GDictionary ?? new GDictionary(),
                $"prismatic_sphere.status_params.{statusId}"
            ),
            source_skill_id = sourceSkillId,
            source_skill_level = sourceSkillLevel,
        };
        unitState.SetStatusEffect(status);
    }

    private static GDictionary OwnedDictionary(GDictionary dictionary, string reason) =>
        TestResourceOwnership.OwnWrapper(dictionary, reason);

    private static BattleBarrierInstanceState FirstBarrier(BattleState state)
    {
        GDictionary barrierFields = state?.ProjectLayeredBarrierFields() ?? new GDictionary();
        if (barrierFields.Count == 0)
        {
            return new BattleBarrierInstanceState();
        }
        foreach (var rawKey in barrierFields.Keys)
        {
            return BattleBarrierInstanceState.FromRuntimeDict(
                barrierFields[rawKey].AsGodotDictionary()
            );
        }
        return new BattleBarrierInstanceState();
    }

    private static StringName FirstBarrierKey(BattleState state)
    {
        GDictionary barrierFields = state?.ProjectLayeredBarrierFields() ?? new GDictionary();
        if (barrierFields.Count == 0)
        {
            return "";
        }
        foreach (var rawKey in barrierFields.Keys)
        {
            return ProgressionDataUtils.to_string_name(rawKey);
        }
        return "";
    }

    private static void StoreFirstBarrier(BattleState state, BattleBarrierInstanceState barrier)
    {
        StringName key = FirstBarrierKey(state);
        if (state == null || key == "" || barrier == null)
        {
            return;
        }
        state.PutLayeredBarrierFieldPayload(key, barrier.ToRuntimeDict());
    }

    private static StringName ActiveLayerId(BattleBarrierInstanceState barrier)
    {
        return ActiveLayer(barrier)?.LayerId ?? "";
    }

    private static BattleBarrierLayerState ActiveLayer(BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
            return null;
        foreach (BattleBarrierLayerState layer in barrier.Layers)
            if (layer != null && !layer.Broken)
                return layer;
        return null;
    }

    private void AssertActiveLayerSaveRollOverride(
        BattleState state,
        StringName expectedLayerId,
        int expectedRoll
    )
    {
        BattleBarrierLayerState activeLayer = ActiveLayer(FirstBarrier(state));
        _test.Eq(
            activeLayer?.LayerId ?? new StringName(""),
            expectedLayerId,
            $"{expectedLayerId} 应成为当前活动层。"
        );
        _test.True(
            activeLayer?.HasSaveRollOverride == true
                && activeLayer.SaveRollOverride == expectedRoll,
            $"{expectedLayerId} 应保留保存检定 override={expectedRoll}，避免回退到随机豁免。"
        );
    }

    private static void MarkLayersBroken(BattleState state, params StringName[] layerIds)
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var targetLayerIds = new HashSet<StringName>(layerIds ?? Array.Empty<StringName>());
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && targetLayerIds.Contains(layer.LayerId))
            {
                layer.Broken = true;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static void SetOnlyRemainingLayer(BattleState state, StringName remainingLayerId)
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null)
                layer.Broken = layer.LayerId != remainingLayerId;
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static void SetAllLayersUnbroken(BattleState state)
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null)
                layer.Broken = false;
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static void SetLayerSaveRollOverride(
        BattleState state,
        StringName layerId,
        int roll
    )
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && layer.LayerId == layerId)
            {
                layer.HasSaveRollOverride = true;
                layer.SaveRollOverride = roll;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static bool CoordInsideBarrier(Vector2I coord, BattleBarrierInstanceState barrier)
    {
        if (barrier == null || barrier.IsEmpty)
        {
            return false;
        }
        return BattleBarrierGeometryService.CoordInsideBarrier(
            coord,
            DiamondArea(barrier.AnchorCoord, barrier.RadiusCells)
        );
    }

    private static List<Vector2I> DiamondArea(Vector2I center, int radius)
    {
        var coords = new List<Vector2I>();
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                Vector2I coord = new(x, y);
                if (Math.Abs(coord.X - center.X) + Math.Abs(coord.Y - center.Y) <= radius)
                {
                    coords.Add(coord);
                }
            }
        }
        return coords;
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsBool();
    }

    private static bool DictArrayHasStringName(
        GDictionary dictionary,
        string key,
        StringName expected
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return false;
        }
        GArray values = dictionary[key].AsGodotArray();
        foreach (var value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == expected)
            {
                return true;
            }
        }
        return false;
    }

    private sealed class MasteryCommandFixture : IDisposable
    {
        private bool _disposed;

        public MasteryCommandFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState caster,
            BattleUnitState enemy,
            CharacterManagementModule characterManagement,
            UnitSkillProgress skillProgress
        )
        {
            Runtime = runtime;
            State = state;
            Caster = caster;
            Enemy = enemy;
            CharacterManagement = characterManagement;
            SkillProgress = skillProgress;
        }

        public BattleRuntimeModule Runtime { get; }
        public BattleState State { get; }
        public BattleUnitState Caster { get; }
        public BattleUnitState Enemy { get; }
        public CharacterManagementModule CharacterManagement { get; }
        public UnitSkillProgress SkillProgress { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Runtime?.Dispose();
            CharacterManagement?.Dispose();
            BattleTestFixture.DisposeBattleUnit(Caster);
            BattleTestFixture.DisposeBattleUnit(Enemy);
            BattleTestFixture.DisposeBattleState(State);
        }
    }

    private readonly record struct Fixture(
        BattleRuntimeModule Runtime,
        BattleState State,
        BattleUnitState Caster,
        BattleUnitState Enemy,
        BattleUnitState AdditionalUnit = null
    ) : IDisposable
    {
        public void Dispose()
        {
            Runtime?.Dispose();
            BattleTestFixture.DisposeBattleUnit(Caster);
            BattleTestFixture.DisposeBattleUnit(Enemy);
            BattleTestFixture.DisposeBattleUnit(AdditionalUnit);
            BattleTestFixture.DisposeBattleState(State);
        }
    }
}
