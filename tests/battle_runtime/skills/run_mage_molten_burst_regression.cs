using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_mage_molten_burst_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private SkillDefinition _molten, _cinder, _bleed;
    public override void _Initialize() => RunAfterProcessStartup(Run);
    private void Run()
    {
        try
        {
            _molten = TestSkillDefinitionProjection.LoadSkillDefinition("mage_molten_burst");
            _cinder = TestSkillDefinitionProjection.LoadSkillDefinition("mage_cinder_bolt");
            _bleed = TestSkillDefinitionProjection.LoadSkillDefinition("warrior_rip_and_tear");
            TestProductionAndProgression();
            TestRealCommands();
            TestRejectedCommands();
            TestSourceBudgetAndTimeline();
            TestLifetimeBudget();
            TestAutoCast();
            TestMitigationMasteryAndDeath();
            TestDicePreviewAndAi();
            TestFatalContinuation();
            TestContentBoundary();
        }
        catch (Exception ex) { _test.Fail($"Unhandled: {ex}"); }
        RequestTestExit(_test.Finish("Mage molten burst regression"));
    }

    private CombatEffectDefinition Effect(int level = 0) =>
        _molten.CombatProfile.EffectDefinitions.Single(e => e.IsUnlockedAtSkillLevel(level));
    private void TestProductionAndProgression()
    {
        int[] ticks = {3,3,3,4,4,6,6,8};
        for (int level = 0; level <= 7; level++)
        {
            var payload = (AdvanceStatusTicksEffectPayloadDefinition)Effect(level).Payload;
            _test.Eq(payload.MaxTicks, ticks[level], "等级结算预算");
            _test.Eq(payload.MaxSources, level < 5 ? 1 : 2, "来源预算");
            _test.Eq(Effect(level).DiceCount, 0, "无独立基础伤害");
            var cost = _molten.CombatProfile.GetEffectiveResourceCostValues(level);
            _test.Eq(cost.ApCost, 1, "1AP施放");
            _test.Eq(cost.MpCost, level == 0 ? 20 : level < 6 ? 18 : 16, "MP成本");
        }
        int[] gates = {60,140,250,400,600};
        for (int level = 1; level <= 5; level++)
        {
            var progress = new UnitProgress { unit_id = $"progress_{level}" };
            var progression = new ProgressionService();
            progression.SetupDefinitions(progress, new Dictionary<StringName,SkillDefinition>{{_molten.SkillId,_molten}},
                new Dictionary<StringName,ProfessionDefinition>());
            _test.True(progression.LearnSkill(_molten.SkillId), "学会生产技能");
            progression.GrantSkillMastery(_molten.SkillId, gates[level-1]-1, "battle");
            _test.Eq(progress.GetSkillProgress(_molten.SkillId).skill_level, level-1, "累计门槛前一点");
            progression.GrantSkillMastery(_molten.SkillId, 1, "battle");
            _test.Eq(progress.GetSkillProgress(_molten.SkillId).skill_level, level, "累计门槛正好升级");
            if (level == 5)
            {
                progression.GrantSkillMastery(_molten.SkillId, 10000, "battle");
                _test.Eq(progress.GetSkillProgress(_molten.SkillId).skill_level, 5, "非核心上限5");
            }
        }
    }

    private void TestRealCommands()
    {
        var fire = Unit("fire", "player", new(1,1), _cinder);
        fire.SetKnownSkillLevelTyped(_cinder.SkillId, 3);
        var caster = Unit("finisher", "player", new(1,2), _molten);
        var target = Unit("enemy", "enemy", new(3,1));
        using var f = Fixture(new[]{fire,caster}, target);
        f.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        f.Runtime.ConfigureDamageResolverForTests(new FixedFailedSaveDamageResolver());
        Cast(f, fire, target, _cinder);
        var source = target.GetStatusEffect("burning").GetSourceContributionsTyped().Single();
        _test.Eq(source.Power, 2, "真实余烬飞弹3级燃烧强度");
        int before = target.GetCurrentHp(), duration = source.DurationTu;
        f.State.active_unit_id = caster.unit_id;
        f.State.phase = "unit_acting";
        var command = Command(caster,target,_molten);
        try
        {
            var preview = f.Runtime.PreviewCommand(command);
            _test.True(preview.allowed, "真实前置使熔爆合法");
            _test.True(preview.LogLinesTyped.Any(l => l.Contains("3 次持续伤害")), "预览展示结算次数");
            _test.Eq(target.GetCurrentHp(), before, "HUD预览不扣血");
            _test.Eq(Contribution(target,source.Identity).NextTickAtTu, 10, "HUD预览不消耗正式时序");
            BattleTestFixture.DisposeBattlePreview(preview);
            f.Runtime.IssueCommand(command)?.Dispose();
        }
        finally { BattleTestFixture.DisposeBattleCommand(command); }
        _test.Eq(before-target.GetCurrentHp(), 6, "3次原有2点燃烧，无额外伤害骰");
        _test.Eq(Contribution(target,source.Identity).NextTickAtTu, 40, "后续从40TU继续");
        _test.Eq(Contribution(target,source.Identity).DurationTu, duration, "不刷新或删除持续时间");
        _test.Eq(Contribution(target,source.Identity).Stacks, 1, "不增减原层数");
        _test.Eq(caster.GetCurrentMp(), 180, "真实支付20MP");
        _test.Eq(caster.GetCooldownTyped(_molten.SkillId), 80, "真实冷却80TU");
        // Refresh through the real source skill: it may extend burning but cannot
        // reinstate the three ticks that were already advanced.
        fire.SetCooldownTyped(_cinder.SkillId,0);
        Cast(f,fire,target,_cinder);
        _test.Eq(Contribution(target,source.Identity).NextTickAtTu,40,"真实重施不重置已消费的未来结算");
    }

    private void TestRejectedCommands()
    {
        foreach (string mode in new[]{"none","bleed","expired","exhausted"})
        {
            var caster=Unit("reject_caster","player",new(1,1),_molten);
            var target=Unit("reject_target","enemy",new(2,1));
            if(mode!="none") AddBurn(target,mode,2,mode=="expired"?0:30,
                next:mode=="exhausted"?40:10,skillId:mode=="bleed"?"warrior_rip_and_tear":_cinder.SkillId);
            using var f=Fixture(new[]{caster},target);
            var command=Command(caster,target,_molten);
            try
            {
                var preview=f.Runtime.PreviewCommand(command);
                _test.False(preview.allowed,$"{mode}无有效未来燃烧不可施放");
                BattleTestFixture.DisposeBattlePreview(preview);
                f.Runtime.IssueCommand(command)?.Dispose();
                _test.Eq(caster.GetCurrentMp(),200,"失败不扣MP");
                _test.Eq(caster.GetCooldownTyped(_molten.SkillId),0,"失败不写冷却");
            }
            finally { BattleTestFixture.DisposeBattleCommand(command); }
        }
    }

    private void TestSourceBudgetAndTimeline()
    {
        var caster=Unit("timeline_caster","player",new(1,1),_molten);
        var target=Unit("timeline_target","enemy",new(2,1));
        var a=AddBurn(target,"a",2,100);
        var b=AddBurn(target,"b",3,80);
        using var f=Fixture(new[]{caster},target);
        using var resolver=Resolver();
        var plan=resolver.BuildStatusTickAdvancePlanTyped(target,Effect(),f.State);
        _test.Eq(plan.Count,3,"0级总预算3次");
        _test.True(plan.All(s=>s.Source==b),"优先到期更近的来源");
        var high=resolver.BuildStatusTickAdvancePlanTyped(target,Effect(5),f.State);
        _test.Eq(high.Count,6,"5级两来源共享6次预算，不能变成12次");
        _test.Eq(high.Select(s=>s.Source).Distinct().Count(),2,"两来源均有结算");
        resolver.ResolveEffects(caster,target,new[]{Effect()},DamageResolutionContext.Empty().WithBattleState(f.State));
        _test.Eq(target.GetCurrentHp(),991,"提前3次3点");
        var turn=new BattleRuntimeSkillTurnResolver();
        turn.Setup(f.Runtime);
        using var batch=new BattleEventBatch();
        f.State.timeline.current_tu=30;
        turn.ApplyUnitStatusPeriodicTicksResult(target,30,batch);
        turn.AdvanceUnitStatusDurations(target,30);
        _test.Eq(target.GetCurrentHp(),985,"30TU内只补另一来源6点，总量与自然15点相同");
        _test.Eq(Contribution(target,b).NextTickAtTu,40,"跳过已提前结算的10/20/30");
        f.State.timeline.current_tu=40;
        turn.ApplyUnitStatusPeriodicTicksResult(target,10,batch);
        _test.Eq(target.GetCurrentHp(),980,"40TU两个来源正常各跳一次");
        var shortTarget=Unit("short","enemy",new(2,2));
        AddBurn(shortTarget,"edge",2,20);
        _test.Eq(resolver.BuildStatusTickAdvancePlanTyped(shortTarget,Effect(),null).Count,2,"与自然时间轴一致，包含到期瞬间一跳");
    }

    private void TestMitigationMasteryAndDeath()
    {
        var caster=Unit("mastery_caster","player",new(1,1),_molten);
        using var resolver=Resolver();
        using var mastery=new BattleSkillMasteryService();
        foreach(var (tier,expected) in new[]{("",9),("half",3),("double",18),("immune",0)})
        {
            var target=Unit("tier_target","enemy",new(2,1));
            AddBurn(target,"tagged",3,100,tag:"fire");
            if(tier!="") target.SetStatusEffect(new BattleStatusEffectState{
                status_id="fire_tier",duration=100,stacks=1,power=1,damage_tag="fire",mitigation_tier=tier});
            var result=resolver.ResolveEffects(caster,target,new[]{Effect()},DamageResolutionContext.Empty());
            _test.Eq(result.Damage,expected,$"{tier}逐跳取整、防护、易伤");
            _test.Eq(result.SaveResults.Length,0,"不再次豁免");
            mastery.Clear();
            mastery.RecordTargetResult(caster,target,_molten,result,new[]{Effect()});
            _test.Eq(mastery.ResolveActiveSkillMasteryAmount(),expected>0?1:0,"多跳同一目标仅奖一次，免疫不计奖");
        }
        var shield=Unit("shield_target","enemy",new(2,1));
        AddBurn(shield,"shield_source",2,100);
        shield.ReplaceShieldStateTyped(10,10,100,"shield",caster.unit_id,"test");
        var shieldResult=resolver.ResolveEffects(caster,shield,new[]{Effect()},DamageResolutionContext.Empty());
        _test.Eq(shieldResult.ShieldAbsorbed,6,"每跳消耗共享护盾");
        _test.Eq(shieldResult.Damage,0,"护盾全吸收时不掉血");
        mastery.Clear();
        mastery.RecordTargetResult(caster,shield,_molten,shieldResult,new[]{Effect()});
        _test.Eq(mastery.ResolveActiveSkillMasteryAmount(),1,"有效护盾伤害计一次熟练度");
        foreach(var (rank,amount) in new[]{("fortune_mark_target",2),("boss_target",3)})
        {
            var ranked=Unit("ranked","enemy",new(2,1));
            ranked.attribute_snapshot.SetValue(rank,1);
            AddBurn(ranked,"rank_source",2,100);
            var result=resolver.ResolveEffects(caster,ranked,new[]{Effect()},DamageResolutionContext.Empty());
            mastery.Clear();
            mastery.RecordTargetResult(caster,ranked,_molten,result,new[]{Effect()});
            _test.Eq(mastery.ResolveActiveSkillMasteryAmount(),amount,"精英/Boss按目标档位奖励，多跳不倍增");
        }
        var dying=Unit("dying","enemy",new(2,1));
        dying.SetCurrentHp(1);
        var identity=AddBurn(dying,"death_source",2,100);
        var deadResult=resolver.ResolveEffects(caster,dying,new[]{Effect()},DamageResolutionContext.Empty());
        _test.Eq(deadResult.DamageEvents.Length,1,"死亡后停止，不继续消耗未来跳数");
    }

    private void TestDicePreviewAndAi()
    {
        var caster=Unit("ai_caster","player",new(1,1),_molten);
        var target=Unit("ai_target","enemy",new(2,1));
        var identity=AddBurn(target,"dice_source",1,100,dice:1);
        using var f=Fixture(new[]{caster},target);
        using var resolver=Resolver();
        var low=resolver.PreviewStatusTickAdvanceTyped(caster,target,Effect(),f.State,BattleStatusTickAdvancePreviewMode.Minimum);
        var average=resolver.PreviewStatusTickAdvanceTyped(caster,target,Effect(),f.State);
        var high=resolver.PreviewStatusTickAdvanceTyped(caster,target,Effect(),f.State,BattleStatusTickAdvancePreviewMode.Maximum);
        _test.Eq(low.HpDamage,3,"1D6三跳最低3");
        _test.Eq(high.HpDamage,18,"1D6三跳最高18");
        _test.True(average.HpDamage>low.HpDamage&&average.HpDamage<high.HpDamage,"平均骰模式正确");
        _test.Eq(target.GetCurrentHp(),1000,"预览不扣正式HP");
        _test.Eq(Contribution(target,identity).NextTickAtTu,10,"预览不消耗正式燃烧");
        using var rngProbe=new NoRandomPreviewResolver();
        rngProbe.SetSkillDefinitions(new Dictionary<StringName,SkillDefinition>{{_cinder.SkillId,_cinder}});
        foreach(var mode in Enum.GetValues<BattleStatusTickAdvancePreviewMode>())
            rngProbe.PreviewStatusTickAdvanceTyped(caster,target,Effect(),f.State,mode);
        using var scorer=new BattleAiScoreService();
        var context=new BattleAiContext{state=f.State,unit_state=caster,grid_service=f.Runtime._grid_service};
        context.SetSkillDefinitions(new Dictionary<StringName,SkillDefinition>{{_molten.SkillId,_molten},{_cinder.SkillId,_cinder}});
        var command=Command(caster,target,_molten);
        var preview=f.Runtime.PreviewCommand(command);
        try
        {
            var score=scorer.BuildSkillScoreInput(context,_molten,command,preview,new[]{Effect()},
                new Dictionary<string,object>{{"desired_min_distance",0},{"desired_max_distance",5}});
            _test.Eq(score.estimated_damage,average.HpDamage,"AI共享结算预览");
            _test.Eq(score.hit_payoff_score,0,"安全目标的已有伤害不重复计普通输出收益");
            _test.Eq(score.estimated_control_count,0,"燃烧兑现不是控制");
            _test.Eq(Contribution(target,identity).NextTickAtTu,10,"AI不修改正式时序");
            target.SetCurrentHp(1);
            var lethal=scorer.BuildSkillScoreInput(context,_molten,command,preview,new[]{Effect()},
                new Dictionary<string,object>{{"desired_min_distance",0},{"desired_max_distance",5}});
            _test.Eq(lethal.estimated_lethal_target_count,1,"最低骰也致死时AI保留收割价值");
            _test.True(lethal.hit_payoff_score>0,"收割收益未被普通输出扣除抵消");
            target.SetCurrentHp(1000);
        }
        finally {BattleTestFixture.DisposeBattlePreview(preview);BattleTestFixture.DisposeBattleCommand(command);}
        var real=resolver.ResolveEffects(caster,target,new[]{Effect()},DamageResolutionContext.Empty().WithBattleState(f.State));
        _test.Eq(real.Damage,high.HpDamage,"实际最大骰与预览上界一致");
    }

    private void TestContentBoundary()
    {
        var errors=new Godot.Collections.Array<string>();
        var validator=new SkillDefinitionCombatProfileValidator(new SkillDefinitionDamageEffectValidator(),new SkillDefinitionExecuteEffectValidator());
        var unsupported=TestSkillDefinitionProjection.BuildCombatProfile("unsupported",effects:new[]{Effect()},
            targetMode:"ground",targetSelectionMode:"single_unit",attackResolutionMode:"direct_effect");
        validator.AppendCombatProfileValidationErrors(errors,"unsupported",unsupported);
        _test.True(errors.Any(e=>e.Contains("advance_status_ticks")),"未落地的地面模式导入即拒绝");
        foreach(var (ticks,sources,status) in new[]{(0,1,"burning"),(33,1,"burning"),(3,0,"burning"),(3,9,"burning"),(3,1,"bone_chill")})
        {
            var raw=TestResourceOwnership.Own(new CombatEffectDef{effect_type="advance_status_ticks",status_id=status,
                required_target_status_id=status,required_target_status_min_stacks=1,effect_target_team_filter="enemy",
                status_tick_limit=ticks,status_source_limit=sources,status_source_tag="fire"},"molten-invalid-payload");
            var invalid=CombatEffectDefinition.FromDiagnosticFixture(raw,"molten-invalid-payload");
            errors.Clear();
            var profile=TestSkillDefinitionProjection.BuildCombatProfile("invalid",effects:new[]{invalid},
                targetMode:"unit",targetSelectionMode:"single_unit",attackResolutionMode:"direct_effect");
            validator.AppendCombatProfileValidationErrors(errors,"invalid",profile);
            _test.True(errors.Any(e=>e.Contains("advance_status_ticks requires a source-scoped")),"非法上限/非来源状态拒绝");
        }
    }

    private void TestLifetimeBudget()
    {
        foreach(int level in new[]{0,1,3,4,5,6,7})
        {
            int RunPath(bool advance)
            {
                var caster=Unit("budget_caster","player",new(1,1),_molten);
                var target=Unit("budget_target","enemy",new(2,1));
                AddBurn(target,"a",2,80);
                AddBurn(target,"b",3,60);
                using var f=Fixture(new[]{caster},target);
                using var resolver=Resolver();
                int immediate=0;
                if(advance)
                    immediate=resolver.ResolveEffects(caster,target,new[]{Effect(level)},DamageResolutionContext.Empty().WithBattleState(f.State)).Damage;
                var turn=new BattleRuntimeSkillTurnResolver();
                turn.Setup(f.Runtime);
                using var batch=new BattleEventBatch();
                for(int tu=5;tu<=320;tu+=5)
                {
                    f.State.timeline.current_tu=tu;
                    turn.ApplyUnitStatusPeriodicTicksResult(target,5,batch);
                    turn.AdvanceUnitStatusDurations(target,5);
                    if(tu==80||tu==160||tu==320)
                        _test.Eq(1000-target.GetCurrentHp(),34,$"L{level} advance={advance} {tu}TU总量守恒");
                }
                if(advance) GD.Print($"MOLTEN_PROBE level={level} immediate_hp={immediate} lifetime_hp={1000-target.GetCurrentHp()} baseline_hp=34");
                return immediate;
            }
            RunPath(false);
            _test.True(RunPath(true)>0,"提前收益在即时窗口出现");
        }
    }

    private void TestAutoCast()
    {
        var caster=Unit("auto_caster","player",new(1,1),_molten);
        var target=Unit("auto_target","enemy",new(2,1));
        var identity=AddBurn(target,"auto_source",2,30);
        using var f=Fixture(new[]{caster},target);
        var request=new AutoCastRequest{
            CasterUnitId=caster.unit_id,OwnerUnitId=caster.unit_id,OwnerMemberId=caster.source_member_id,
            SetupId="molten_auto",InstanceId="molten_auto_instance",SourceSkillId="test_contingency_source",
            SourceSkillLevel=1,SourceSkillGrantSourceType=UnitSkillGrantSourceType.Player,StoredSkillId=_molten.SkillId,CastLevel=1,
            TargetResolution=ContingencyTargetResolutionResult.UnitTarget(target.unit_id,target.GetAnchorCoord()),
            ReleaseContext=new ContingencyReleaseContext{InstanceId="molten_auto_instance",SetupId="molten_auto",
                OwnerMemberId=caster.source_member_id,OwnerUnitId=caster.unit_id,CasterUnitId=caster.unit_id,TriggerType="affected_by_spell"}};
        using var batch=new BattleEventBatch();
        bool applied=false;
        BattleReactionRootTestHelper.ExecuteInReactionRoot(f.Runtime,batch,BattleEffectOrigin.AutoCast(request),
            ()=>applied=f.Runtime._skill_orchestrator.ExecuteAutoCast(request,batch));
        _test.True(applied,"自动执行共用状态结算链");
        _test.Eq(target.GetCurrentHp(),994,"自动施放同样结算三跳");
        _test.Eq(Contribution(target,identity).NextTickAtTu,40,"自动施放扣除未来tick");
        BattleReactionRootTestHelper.ExecuteInReactionRoot(f.Runtime,batch,BattleEffectOrigin.AutoCast(request),
            ()=>applied=f.Runtime._skill_orchestrator.ExecuteAutoCast(request,batch));
        _test.False(applied,"自动重入也不能重放耗尽来源");
    }

    private partial class NoRandomPreviewResolver : BattleDamageResolver
    {
        public override int _roll_damage_die(int sides) => throw new InvalidOperationException("Preview consumed RNG");
    }
    private sealed class HalfSurvivalArbiter : IBattleFatalInterceptArbiter
    {
        public BattleFatalInterceptResult Resolve(BattleFatalInterceptContext context) => BattleFatalInterceptResult.None;
        public BattleFatalInterceptPreviewResult Preview(BattleFatalInterceptContext context)
        {
            var survived=BattleDamagePreviewWorkingSet.CreateDetached(context.SourceUnit,context.TargetUnit,context.BattleState);
            survived.TargetPreview.SetCurrentHp(10);
            var died=BattleDamagePreviewWorkingSet.CreateDetached(context.SourceUnit,context.TargetUnit,context.BattleState);
            died.TargetPreview.MarkDead();
            return new BattleFatalInterceptPreviewResult{
                InterceptProbabilityBasisPoints=5000,ExpectedRecoveryHp=10,ExpectedSurvivalHp=5,
                ContinuationBranches=new[]{
                    new BattleFatalInterceptPreviewBranch{ProbabilityBasisPoints=5000,BattleState=survived.BattleState,
                        SourceUnit=survived.SourcePreview,TargetUnit=survived.TargetPreview,Intercepted=true},
                    new BattleFatalInterceptPreviewBranch{ProbabilityBasisPoints=5000,BattleState=died.BattleState,
                        SourceUnit=died.SourcePreview,TargetUnit=died.TargetPreview,Intercepted=false}}};
        }
    }
    private void TestFatalContinuation()
    {
        var caster=Unit("fatal_caster","player",new(1,1),_molten);
        var target=Unit("fatal_target","enemy",new(2,1));
        target.SetCurrentHp(1);
        var identity=AddBurn(target,"fatal_source",2,100);
        using var f=Fixture(new[]{caster},target);
        using var resolver=Resolver();
        resolver.SetFatalInterceptArbiter(new HalfSurvivalArbiter());
        var working=BattleDamagePreviewWorkingSet.CreateDetached(caster,target,f.State);
        var preview=resolver.PreviewStatusTickAdvanceOnWorkingSetTyped(working,Effect());
        _test.False(preview.TargetDefeated,"有50%免死分支时不标必杀");
        var survivor=working.ContinuationBranches.Single(b=>b.TargetUnit.IsAlive());
        _test.Eq(survivor.TargetUnit.GetCurrentHp(),6,"复活后还结算剩余两跳，不遗漏或重放首跳");
        _test.Eq(Contribution(survivor.TargetUnit,identity).NextTickAtTu,40,"生存分支消耗三跳");
        _test.Eq(target.GetCurrentHp(),1,"免死预览不改正式HP");
        _test.Eq(Contribution(target,identity).NextTickAtTu,10,"免死预览不改正式tick");
    }

    private BattleDamageResolver Resolver()
    {
        var resolver=new FixedHitMaxDamageResolver();
        resolver.SetSkillDefinitions(new Dictionary<StringName,SkillDefinition>{{_molten.SkillId,_molten},{_cinder.SkillId,_cinder}});
        return resolver;
    }
    private BattleStatusSourceIdentity AddBurn(BattleUnitState target,string source,int power,int duration,
        int next=10,StringName tag=default,int dice=0,StringName skillId=default)
    {
        var identity=BattleStatusSourceIdentity.Skill(source,skillId==default?_cinder.SkillId:skillId);
        var status=target.GetStatusEffect("burning")??new BattleStatusEffectState{status_id="burning"};
        status.SetSourceContributionTyped(new BattleStatusSourceContributionState{
            Identity=identity,Power=power,Stacks=1,DurationTu=duration,TickIntervalTu=10,NextTickAtTu=next,
            DamageTag=tag==default?new StringName(""):tag,TimelineDamageDiceCount=dice,TimelineDamageDiceSides=dice>0?6:0});
        status.RebuildSourceContributionAggregateTyped();
        target.SetStatusEffect(status);
        return identity;
    }
    private static BattleStatusSourceContributionState Contribution(BattleUnitState unit,BattleStatusSourceIdentity identity) =>
        unit.GetStatusEffect("burning").GetSourceContributionTyped(identity);
    private static BattleUnitState Unit(string id,string faction,Vector2I coord,SkillDefinition skill=null)
    {
        var u=BattleTestFixture.BuildUnit(id,faction,coord,currentAp:2,currentHp:1000);
        u.source_member_id=id;
        u.SetCurrentMp(200);
        u.attribute_snapshot.SetValue(AttributeService.MP_MAX,200);
        u.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS,1);
        u.attribute_snapshot.SetValue("action_points",2);
        u.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        if(skill!=null){u.AddKnownActiveSkill(skill.SkillId);u.SetKnownSkillLevelTyped(skill.SkillId,0,preserveZero:true);}
        return u;
    }
    private BattleTestFixture Fixture(BattleUnitState[] allies,BattleUnitState enemy)
    {
        var f=BattleTestFixture.CreateFlatBattle("molten_burst",new Vector2I(8,5),allies,new[]{enemy});
        f.Runtime.setup(null,new Dictionary<StringName,SkillDefinition>{{_molten.SkillId,_molten},{_cinder.SkillId,_cinder},{_bleed.SkillId,_bleed}});
        f.Runtime.SetupStateForTests(f.State);
        f.State.active_unit_id=allies[0].unit_id;
        f.State.phase="unit_acting";
        return f;
    }
    private static BattleCommand Command(BattleUnitState actor,BattleUnitState target,SkillDefinition skill) => new(){
        CommandKind=BattleCommandKind.Skill,unit_id=actor.unit_id,skill_id=skill.SkillId,
        skill_entry_id=BattleSkillEntryIds.KnownSkill(skill.SkillId),target_coord=target.GetAnchorCoord(),target_unit_id=target.unit_id};
    private void Cast(BattleTestFixture f,BattleUnitState actor,BattleUnitState target,SkillDefinition skill)
    {
        f.State.active_unit_id=actor.unit_id;
        f.State.phase="unit_acting";
        var command=Command(actor,target,skill);
        try
        {
            var preview=f.Runtime.PreviewCommand(command);
            _test.True(preview.allowed,"施放合法: "+string.Join(" | ",preview.LogLinesTyped));
            BattleTestFixture.DisposeBattlePreview(preview);
            f.Runtime.IssueCommand(command)?.Dispose();
        }
        finally {BattleTestFixture.DisposeBattleCommand(command);}
    }
}
