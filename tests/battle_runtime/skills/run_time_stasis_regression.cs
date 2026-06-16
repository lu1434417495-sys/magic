using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// M2 time_stasis 技能链回归：elite/boss 降级、temporal release / dispel 余波、
// 静滞目标过滤、读条冻结与 temporal-only 内容校验。
public partial class run_time_stasis_regression : SceneTree
{
    private static readonly StringName TemporalTag =
        BattleSaveContentRules.ToStringName(BattleSaveTagKind.Temporal);

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestStasisApplicationDowngradesForEliteBoss();
        TestReverberationSaveBonusGuardsAgainstChainStasis();
        TestTemporalReleaseRemovesStasisAndAddsReverberation();
        TestDispelRemovesStasisWithReverberationAndCleanseDoesNot();
        TestStasisTargetGateBlocksNonReleaseSkills();
        TestPendingCastFreezesUnderStasisAndResumes();
        TestTimeSlowHalvesPendingCastProgress();
        TestTemporalContentValidationRules();

        Quit(_test.Finish("Time stasis regression"));
    }

    private void TestStasisApplicationDowngradesForEliteBoss()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("stasis_source", "player");
        BattleUnitState normalTarget = MakeUnit("normal_target", "enemy");
        resolver.ResolveEffects(
            source,
            normalTarget,
            new GArray { MakeStasisEffect() },
            new GDictionary { ["save_roll_override"] = 5 }
        );
        _test.True(
            normalTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "普通目标豁免失败应获得 time_stasis。"
        );

        BattleUnitState bossTarget = MakeUnit("boss_target_unit", "enemy");
        bossTarget.attribute_snapshot.SetValue("boss_target", 1);
        resolver.ResolveEffects(
            source,
            bossTarget,
            new GArray { MakeStasisEffect() },
            new GDictionary { ["save_roll_override"] = 5 }
        );
        _test.False(
            bossTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "boss 目标不应获得 time_stasis。"
        );
        _test.True(
            bossTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_SLOW),
            "boss 目标的静滞结果应降级为 time_slow。"
        );

        BattleUnitState eliteTarget = MakeUnit("elite_target_unit", "enemy");
        eliteTarget.attribute_snapshot.SetValue("fortune_mark_target", 1);
        resolver.ResolveEffects(
            source,
            eliteTarget,
            new GArray { MakeStasisEffect() },
            new GDictionary { ["save_roll_override"] = 5 }
        );
        _test.False(
            eliteTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "elite 目标不应获得 time_stasis。"
        );
        _test.True(
            eliteTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_SLOW),
            "elite 目标的静滞结果应降级为 time_slow。"
        );

        BattleUnitState criticalSaveTarget = MakeUnit("crit_save_target", "enemy");
        criticalSaveTarget.attribute_snapshot.SetValue("boss_target", 1);
        resolver.ResolveEffects(
            source,
            criticalSaveTarget,
            new GArray { MakeStasisEffect() },
            new GDictionary { ["save_roll_override"] = 20 }
        );
        _test.False(
            criticalSaveTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_SLOW)
                || criticalSaveTarget.HasStatusEffect(
                    BattleStatusSemanticTable.STATUS_TIME_STASIS
                ),
            "critical success 豁免应完全免疫静滞与降级减速。"
        );
    }

    private void TestReverberationSaveBonusGuardsAgainstChainStasis()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("chain_source", "player");
        BattleUnitState target = MakeUnit("chain_target", "enemy");
        BattleTemporalStatusService.ApplyTimeReverberation(target, source.unit_id);

        // DC 14、d20=9：无余波时 9 < 14 必中；余波 +5 后 14 >= 14 豁免成功。
        resolver.ResolveEffects(
            source,
            target,
            new GArray { MakeStasisEffect() },
            new GDictionary { ["save_roll_override"] = 9 }
        );
        _test.False(
            target.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "time_reverberation 的 temporal save bonus 应防住连锁静滞。"
        );

        BattleUnitState bareTarget = MakeUnit("bare_target", "enemy");
        resolver.ResolveEffects(
            source,
            bareTarget,
            new GArray { MakeStasisEffect() },
            new GDictionary { ["save_roll_override"] = 9 }
        );
        _test.True(
            bareTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "无余波目标在同样掷骰下应获得 time_stasis。"
        );
    }

    private void TestTemporalReleaseRemovesStasisAndAddsReverberation()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("release_source", "player");
        BattleUnitState target = MakeUnit("release_target", "player");
        ApplyTimeStasis(target, 60);

        GDictionary result = resolver.ResolveEffects(
            source,
            target,
            new GArray { MakeTemporalReleaseEffect() }
        );

        _test.False(
            target.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "temporal release 应解除 time_stasis。"
        );
        _test.True(
            target.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_REVERBERATION),
            "temporal release 解除静滞后应添加 time_reverberation。"
        );
        _test.True(DictBool(result, "applied"), "temporal release 应记为 applied。");

        BattleUnitState cleanTarget = MakeUnit("clean_target", "player");
        GDictionary noopResult = resolver.ResolveEffects(
            source,
            cleanTarget,
            new GArray { MakeTemporalReleaseEffect() }
        );
        _test.False(
            DictBool(noopResult, "applied", true),
            "无 temporal 状态时 temporal release 不应记为 applied。"
        );
        _test.False(
            cleanTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_REVERBERATION),
            "无静滞可解时不应添加 time_reverberation。"
        );
    }

    private void TestDispelRemovesStasisWithReverberationAndCleanseDoesNot()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = MakeUnit("dispel_source", "player");
        BattleUnitState dispelTarget = MakeUnit("dispel_target", "player");
        ApplyTimeStasis(dispelTarget, 60);

        CombatEffectDef dispelEffect = new()
        {
            effect_type = "dispel_magic",
            power = 3,
            remove_harmful = true,
        };
        resolver.ResolveEffects(source, dispelTarget, new GArray { dispelEffect });
        _test.False(
            dispelTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "dispel_magic 应能解除 time_stasis。"
        );
        _test.True(
            dispelTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_REVERBERATION),
            "dispel 解除静滞后应添加 time_reverberation。"
        );

        BattleUnitState cleanseTarget = MakeUnit("cleanse_target", "player");
        ApplyTimeStasis(cleanseTarget, 60);
        CombatEffectDef cleanseEffect = new()
        {
            effect_type = "cleanse_harmful",
            power = 3,
        };
        resolver.ResolveEffects(source, cleanseTarget, new GArray { cleanseEffect });
        _test.True(
            cleanseTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "普通 cleanse_harmful 不应解除 time_stasis。"
        );
        _test.False(
            cleanseTarget.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_REVERBERATION),
            "未解除静滞时不应添加 time_reverberation。"
        );
    }

    private void TestStasisTargetGateBlocksNonReleaseSkills()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState attacker = fixture.AddUnit("gate_attacker", "player", new Vector2I(1, 1));
        BattleUnitState stasisTarget = fixture.AddUnit("gate_target", "enemy", new Vector2I(1, 2));
        ApplyTimeStasis(stasisTarget, 60);

        SkillDef damageSkill = MakeSkillDef(
            "gate_damage_skill",
            new CombatEffectDef
            {
                effect_type = "damage",
                damage_tag = "fire",
                power = 5,
            }
        );
        string blockedMessage = fixture.Runtime._get_unit_skill_target_validation_message(
            attacker,
            stasisTarget,
            damageSkill
        );
        _test.True(
            !string.IsNullOrEmpty(blockedMessage),
            "非 temporal release 技能不应能以静滞单位为目标。"
        );

        SkillDef releaseSkill = MakeSkillDef("gate_release_skill", MakeTemporalReleaseEffect());
        string allowedMessage = fixture.Runtime._get_unit_skill_target_validation_message(
            attacker,
            stasisTarget,
            releaseSkill
        );
        _test.Eq(allowedMessage, "", "temporal release 技能应能以静滞单位为目标。");

        _test.False(
            BattleTemporalStatusService.CanTargetTimeStasis(stasisTarget, damageSkill),
            "CanTargetTimeStasis 应拒绝普通技能。"
        );
        _test.True(
            BattleTemporalStatusService.CanTargetTimeStasis(stasisTarget, releaseSkill),
            "CanTargetTimeStasis 应放行 temporal release 技能。"
        );
    }

    private void TestPendingCastFreezesUnderStasisAndResumes()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState caster = fixture.AddUnit("frozen_caster", "player", new Vector2I(1, 1));
        BeginPendingCast(fixture, caster, remainingCastProgress: 500);
        ApplyTimeStasis(caster, 10);

        fixture.Step(5);
        fixture.Step(5);

        _test.True(caster.HasPendingCast(), "静滞期间 pending cast 不应被中断或结算。");
        _test.Eq(
            caster.pending_cast?.RemainingCastProgress ?? -1,
            500,
            "静滞期间读条进度应完全冻结。"
        );
        _test.False(
            caster.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            "静滞应已自然到期。"
        );

        fixture.Step(5);

        // 解除后读条恢复推进；本 fixture 没有注册技能内容，完成时按 live-state 重验失败清除 pending。
        _test.False(
            caster.HasPendingCast() && caster.pending_cast.RemainingCastProgress == 500,
            "静滞解除后读条进度应恢复推进。"
        );
    }

    private void TestTimeSlowHalvesPendingCastProgress()
    {
        Fixture fixture = BuildFixture();
        BattleUnitState slowCaster = fixture.AddUnit("slow_caster", "player", new Vector2I(1, 1));
        BeginPendingCast(fixture, slowCaster, remainingCastProgress: 1000);
        slowCaster.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_SLOW,
                source_unit_id = "temporal_caster",
                power = 1,
                stacks = 1,
                duration = 600,
                status_tags = new List<StringName> { TemporalTag },
            }
        );

        fixture.Step(5);
        _test.Eq(
            slowCaster.pending_cast?.RemainingCastProgress ?? -1,
            750,
            "50% 减速下 5 TU 应只推进 250 读条进度。"
        );
        fixture.Step(5);
        _test.Eq(
            slowCaster.pending_cast?.RemainingCastProgress ?? -1,
            500,
            "减速读条应继续按 50% 推进。"
        );
    }

    private void TestTemporalContentValidationRules()
    {
        using SkillContentRegistry registry = new();

        // 1) 施加 time_stasis 必须带 temporal tag / temporal save_tag / save。
        var missingErrors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            missingErrors,
            "stasis_missing_meta",
            new CombatEffectDef
            {
                effect_type = "status",
                status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
                duration_tu = 15,
            },
            "test_effect"
        );
        _test.True(
            missingErrors.Count >= 3,
            $"缺少 temporal tag/save_tag/save 的静滞效果应被拒绝。 errors={FormatErrors(missingErrors)}"
        );

        var validErrors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            validErrors,
            "stasis_valid",
            MakeStasisEffect(),
            "test_effect"
        );
        _test.Eq(validErrors.Count, 0, $"合法静滞效果不应报错。 errors={FormatErrors(validErrors)}");

        // 2) save_bonus_by_tag 参数：string key / 非法 tag / 非 int 值都应被拒绝。
        var bonusErrors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            bonusErrors,
            "bad_save_bonus_by_tag",
            new CombatEffectDef
            {
                effect_type = "status",
                status_id = "attack_up",
                duration_tu = 30,
                @params = new GDictionary
                {
                    // string key "temporal" 与 StringName TemporalTag 在 Godot Dictionary 中
                    // 是同一个 key，非 int 值用例必须用独立的合法 tag key。
                    [new StringName("save_bonus_by_tag")] = new GDictionary
                    {
                        ["temporal"] = 5,
                        [new StringName("not_a_save_tag")] = 5,
                        [BattleSaveContentRules.ToStringName(BattleSaveTagKind.Sleep)] = 1.5,
                    },
                },
            },
            "test_effect"
        );
        _test.True(
            bonusErrors.Count >= 3,
            $"save_bonus_by_tag 的 string key/非法 tag/非 int 值应被拒绝。 errors={FormatErrors(bonusErrors)}"
        );

        // 3) 直接施加 time_reverberation 应被拒绝。
        var reverbErrors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            reverbErrors,
            "direct_reverberation",
            new CombatEffectDef
            {
                effect_type = "status",
                status_id = BattleStatusSemanticTable.STATUS_TIME_REVERBERATION,
                duration_tu = 60,
            },
            "test_effect"
        );
        _test.True(
            reverbErrors.Count >= 1,
            $"内容直接施加 time_reverberation 应被拒绝。 errors={FormatErrors(reverbErrors)}"
        );

        // 4) temporal-tagged erase_status 指向非 temporal 状态应被拒绝；
        //    解除 temporal 状态但缺 temporal tag 也应被拒绝。
        var mismatchedEraseErrors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            mismatchedEraseErrors,
            "temporal_erase_wrong_status",
            new CombatEffectDef
            {
                effect_type = "erase_status",
                status_id = "burning",
                effect_tags = new Godot.Collections.Array<StringName> { TemporalTag },
            },
            "test_effect"
        );
        _test.True(
            mismatchedEraseErrors.Count >= 1,
            $"temporal erase_status 指向非 temporal 状态应被拒绝。 errors={FormatErrors(mismatchedEraseErrors)}"
        );

        var untaggedEraseErrors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            untaggedEraseErrors,
            "untagged_temporal_erase",
            new CombatEffectDef
            {
                effect_type = "erase_status",
                status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
            },
            "test_effect"
        );
        _test.True(
            untaggedEraseErrors.Count >= 1,
            $"解除 temporal 状态但缺 temporal tag 应被拒绝。 errors={FormatErrors(untaggedEraseErrors)}"
        );

        // 5) temporal-only 解控技能拒绝混入伤害、治疗、位移或普通状态。
        foreach (
            CombatEffectDef forbiddenEffect in new[]
            {
                new CombatEffectDef { effect_type = "damage", damage_tag = "fire", power = 5 },
                new CombatEffectDef { effect_type = "heal", power = 5 },
                new CombatEffectDef
                {
                    effect_type = "forced_move",
                    forced_move_mode = "knockback",
                    forced_move_distance = 1,
                },
                new CombatEffectDef
                {
                    effect_type = "status",
                    status_id = "attack_up",
                    duration_tu = 30,
                },
            }
        )
        {
            CombatSkillDef mixedProfile = new()
            {
                skill_id = "mixed_temporal_release",
                effect_defs = new Godot.Collections.Array<CombatEffectDef>
                {
                    MakeTemporalReleaseEffect(),
                    forbiddenEffect,
                },
            };
            var mixedErrors = new Godot.Collections.Array<string>();
            registry.AppendTemporalReleaseSkillValidationErrors(
                mixedErrors,
                "mixed_temporal_release",
                mixedProfile
            );
            _test.True(
                mixedErrors.Count >= 1,
                $"temporal release 混入 {forbiddenEffect.effect_type} 应被拒绝。 errors={FormatErrors(mixedErrors)}"
            );
        }

        CombatSkillDef pureProfile = new()
        {
            skill_id = "pure_temporal_release",
            effect_defs = new Godot.Collections.Array<CombatEffectDef>
            {
                MakeTemporalReleaseEffect(),
            },
        };
        var pureErrors = new Godot.Collections.Array<string>();
        registry.AppendTemporalReleaseSkillValidationErrors(
            pureErrors,
            "pure_temporal_release",
            pureProfile
        );
        _test.Eq(
            pureErrors.Count,
            0,
            $"纯 temporal release 技能不应报错。 errors={FormatErrors(pureErrors)}"
        );
    }

    private static void BeginPendingCast(
        Fixture fixture,
        BattleUnitState caster,
        int remainingCastProgress
    )
    {
        BattlePendingCastState pendingCast = new()
        {
            SourceUnitId = caster.unit_id,
            SkillId = "test_casting_skill",
            TargetMode = BattleTargetMode.Ground,
            BindingMode = PendingCastBindingModeKind.GroundBind,
            StartedCoord = caster.coord,
            StartedTu = fixture.State.timeline.current_tu,
            BaseCastingTimeTu = remainingCastProgress / 100,
            RemainingCastProgress = remainingCastProgress,
            LastMaintenanceCheckpointHp = caster.current_hp,
            CastSequence = fixture.State.AllocateCastSequence(),
        };
        pendingCast.SetTargetCoords(new[] { caster.coord });
        caster.SetPendingCast(pendingCast);
    }

    private static void ApplyTimeStasis(BattleUnitState unit, int durationTu)
    {
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
                source_unit_id = "temporal_caster",
                power = 1,
                stacks = 1,
                duration = durationTu,
                status_tags = new List<StringName> { TemporalTag },
            }
        );
    }

    private static CombatEffectDef MakeStasisEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "status",
            status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
            duration_tu = 30,
            save_dc = 14,
            save_ability = "willpower",
            save_tag = TemporalTag,
            effect_tags = new Godot.Collections.Array<StringName> { TemporalTag },
        };
    }

    private static CombatEffectDef MakeTemporalReleaseEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "erase_status",
            status_id = BattleStatusSemanticTable.STATUS_TIME_STASIS,
            effect_tags = new Godot.Collections.Array<StringName> { TemporalTag },
        };
    }

    private static SkillDef MakeSkillDef(StringName skillId, CombatEffectDef effectDef)
    {
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = new CombatSkillDef
            {
                skill_id = skillId,
                effect_defs = new Godot.Collections.Array<CombatEffectDef> { effectDef },
            },
        };
    }

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 100,
            current_mp = 20,
            current_stamina = 20,
            current_ap = 2,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        return unit;
    }

    private static Fixture BuildFixture()
    {
        BattleRuntimeModule runtime = new();
        runtime.setup();
        BattleState state = BuildState(new Vector2I(6, 6));
        runtime._state = state;
        return new Fixture(runtime, state);
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "time_stasis_regression",
            phase = "timeline_running",
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                BattleCellState cell = new()
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

    private static bool DictBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsBool();
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        return string.Join(" || ", errors ?? System.Array.Empty<string>());
    }

    private readonly record struct Fixture(BattleRuntimeModule Runtime, BattleState State)
    {
        internal BattleUnitState AddUnit(StringName unitId, StringName factionId, Vector2I coord)
        {
            BattleUnitState unit = new()
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
                control_mode = "manual",
                current_hp = 100,
                current_mp = 20,
                current_stamina = 20,
                current_ap = 2,
                current_move_points = 4,
                is_alive = true,
            };
            unit.SetAnchorCoord(coord);
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.HpMax),
                100
            );
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 20);
            unit.attribute_snapshot.SetValue("willpower", 10);
            unit.attribute_snapshot.SetValue("willpower_modifier", 0);
            State.SetUnit(unit);
            if (factionId == new StringName("player"))
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            Runtime._grid_service.PlaceUnit(State, unit, unit.coord, true);
            return unit;
        }

        internal void Step(int tuDelta)
        {
            Runtime._timeline_driver.ApplyTimelineStep(new BattleEventBatch(), tuDelta);
        }
    }
}
