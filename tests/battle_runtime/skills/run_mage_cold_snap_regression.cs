using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_mage_cold_snap_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly StringName Cold = "mage_cold_snap", Bone = "mage_bone_chill";
    private static readonly StringName Vulnerable = "freeze_vulnerability";
    public override void _Initialize() => RunAfterProcessStartup(Run);
    private void Run()
    {
        try
        {
            SkillDefinition cold = TestSkillDefinitionProjection.LoadSkillDefinition(Cold.ToString(), "cold-snap");
            SkillDefinition bone = TestSkillDefinitionProjection.LoadSkillDefinition(Bone.ToString(), "cold-snap-source");
            TestLevelsAndProgression(cold);
            TestRealTeamChain(cold, bone);
            TestConditionalTargets(cold, bone);
            TestVulnerabilityLifecycleAndDamage(cold, bone);
            TestMastery(cold, bone);
            TestAi(cold, bone);
        }
        catch (Exception ex) { _test.Fail($"Unhandled: {ex}"); }
        RequestTestExit(_test.Finish("Mage cold snap regression"));
    }

    private void TestLevelsAndProgression(SkillDefinition cold)
    {
        int[] dice = {4,4,4,6,6,6,6,8}, mp = {70,65,65,65,65,65,60,60};
        for (int level = 0; level <= 7; level++)
        {
            var effects = Effects(cold, level);
            _test.Eq(effects.Length, 2, "每级只有基础伤害与条件易伤");
            _test.Eq(effects[0].DiceCount, dice[level], "伤害等级收益");
            _test.Eq(cold.CombatProfile.GetEffectiveResourceCostValues(level).MpCost, mp[level], "MP等级收益");
            _test.Eq(cold.CombatProfile.GetEffectiveResourceCostValues(level).CooldownTu, level < 4 ? 120 : 80, "冷却");
            _test.Eq(cold.CombatProfile.GetEffectiveAreaValue(level), level < 5 ? 1 : 2, "5级扩大半径");
            _test.Eq(effects[1].RequiredTargetStatusId, new StringName("bone_chill"), "寒蚀条件");
            _test.Eq(effects[1].RequiredTargetStatusMinStacks, 2, "两层门槛");
            _test.Eq(effects[1].DamageTag, new StringName("freeze"), "仅寒冰易伤");
            _test.Eq(effects[1].MitigationTier, new StringName("double"), "正式易伤档位");
            _test.Eq(effects[0].DamageRatioPercent, 100, "基础伤害不采用百分比增伤");
        }
        var progress = new UnitProgress { unit_id = "cold_progress" };
        var progression = new ProgressionService();
        progression.SetupDefinitions(progress, new Dictionary<StringName, SkillDefinition>{{Cold,cold}},
            new Dictionary<StringName, ProfessionDefinition>());
        _test.True(progression.LearnSkill(Cold), "学会生产技能");
        progression.GrantSkillMastery(Cold, 799, "battle");
        _test.Eq(progress.GetSkillProgress(Cold).skill_level, 4, "799点未到5级");
        progression.GrantSkillMastery(Cold, 1, "battle");
        _test.Eq(progress.GetSkillProgress(Cold).skill_level, 5, "800点到非核心上限");
    }

    private void TestRealTeamChain(SkillDefinition cold, SkillDefinition bone)
    {
        var first = Unit("first", "player", new(1,1), bone);
        var second = Unit("second", "player", new(1,2), bone);
        var mage = Unit("snap", "player", new(2,1), cold);
        var target = Unit("marked_enemy", "enemy", new(3,1));
        var other = Unit("plain_enemy", "enemy", new(4,1));
        using var f = Fixture(new[]{cold,bone}, new[]{first,second,mage}, new[]{target,other});
        f.Runtime.ConfigureDamageResolverForTests(new FixedFailedSaveDamageResolver());
        f.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        Cast(f, first, target, bone);
        _test.Eq(target.GetStatusEffect("bone_chill")?.stacks ?? 0, 1, "第一名法师施加一层");
        Cast(f, second, target, bone);
        _test.Eq(target.GetStatusEffect("bone_chill")?.stacks ?? 0, 2, "第二名法师施加到两层");
        _test.Eq(first.GetCurrentMp(), 180, "第一层真实支付20MP");
        _test.Eq(second.GetCurrentMp(), 180, "第二层真实支付20MP");
        _test.Eq(first.GetCooldownTyped(Bone), 180, "前置技能真实冷却");
        _test.Eq(BattleStatusModifierRules.ResolveHealMultiplierPercent(target), 50, "两层不增强减疗");
        int before = target.GetCurrentHp();
        Cast(f, mage, target, cold);
        _test.Eq(before - target.GetCurrentHp(), 24, "新易伤不放大本次4D6霜爆");
        _test.True(target.HasStatusEffect(Vulnerable), "两层目标获得寒冰易伤");
        _test.False(other.HasStatusEffect(Vulnerable), "同范围无寒蚀目标不获得易伤");
        _test.True(other.GetCurrentHp() < 1000, "没有寒蚀也正常受到基础伤害");
        _test.Eq(target.GetStatusEffect("bone_chill").stacks, 2, "不消耗寒蚀层数");
        _test.Eq(target.GetStatusEffect("bone_chill").duration, 60, "不延长寒蚀");
        _test.Eq(mage.GetCurrentMp(), 130, "霜爆真实支付70MP");
        _test.Eq(mage.GetCooldownTyped(Cold), 120, "霜爆真实冷却");
        _test.Eq(first.GetCurrentHp(), 1000, "范围阵营过滤不伤友军");
    }

    private void TestConditionalTargets(SkillDefinition cold, SkillDefinition bone)
    {
        for (int stacks = 0; stacks <= 3; stacks++)
        {
            var mage = Unit("condition_mage", "player", new(1,1), cold);
            var target = Unit("condition_target", "enemy", new(3,1));
            AddChill(target, bone, stacks);
            using var f = Fixture(new[]{cold,bone}, new[]{mage}, new[]{target});
            var command = Command(mage, target, cold);
            try
            {
                var preview = f.Runtime.PreviewCommand(command);
                _test.True(preview.allowed, "不满两层仍允许施法");
                _test.Eq(preview.StatusContributionPreviewsTyped.Count, stacks >= 2 ? 1 : 0, "预览按实际层数过滤易伤");
                _test.False(target.HasStatusEffect(Vulnerable), "预览不写状态");
                _test.Eq(target.GetStatusEffect("bone_chill")?.stacks ?? 0, Math.Min(stacks,2), "寒蚀上限2层");
                BattleTestFixture.DisposeBattlePreview(preview);
            }
            finally { BattleTestFixture.DisposeBattleCommand(command); }
        }
        var caster = Unit("gate_caster", "player", new(1,1));
        var enemy = Unit("gate_enemy", "enemy", new(2,1));
        AddChill(caster, bone, 2);
        var vulnerability = Effects(cold, 0)[1];
        _test.False(BattleDamageResolver.TargetStatusRequirementPasses(caster,enemy,vulnerability), "不能读施法者寒蚀");
        AddChill(enemy, bone, 2);
        enemy.GetStatusEffect("bone_chill").duration = 0;
        _test.False(BattleDamageResolver.TargetStatusRequirementPasses(caster,enemy,vulnerability), "到期状态不触发");
        enemy.GetStatusEffect("bone_chill").duration = 60;
        enemy.SetCurrentHp(1);
        using var resolver = new FixedFailedSaveDamageResolver();
        resolver.ResolveEffects(caster,enemy,Effects(cold,0),DamageResolutionContext.Empty());
        _test.False(enemy.HasStatusEffect(Vulnerable), "伤害杀死目标后不追加易伤");
        enemy.SetCurrentHp(1000);
        AddChill(enemy,bone,2);
        using var saveResolver = new FixedHitMaxDamageResolver();
        var saved = saveResolver.ResolveEffects(caster,enemy,Effects(cold,0),
            DamageResolutionContext.Empty().WithSaveRollOverrides(new[]{20}));
        _test.Eq(saved.Damage,12,"体质豁免成功使基础伤害减半");
        _test.Eq(saved.SaveResults.Length,1,"只有基础伤害的一次豁免");
        _test.True(enemy.HasStatusEffect(Vulnerable),"两层寒蚀易伤不另做豁免");
    }

    private void TestVulnerabilityLifecycleAndDamage(SkillDefinition cold, SkillDefinition bone)
    {
        var source = Unit("damage_source", "player", new(1,1));
        var vulnerability = Effects(cold, 0)[1];
        using var resolver = new FixedHitMaxDamageResolver();
        foreach (var (tier, expected) in new[]{("",12),("half",6),("immune",0),("double",12)})
        {
            var target = Unit("tier_target", "enemy", new(2,1));
            AddChill(target,bone,2);
            if (tier != "") target.SetStatusEffect(new BattleStatusEffectState {
                status_id="existing_tier",duration=120,stacks=1,power=1,damage_tag="freeze",mitigation_tier=tier});
            resolver.ResolveEffects(source,target,new[]{vulnerability},DamageResolutionContext.Empty());
            int before = target.GetCurrentHp();
            resolver.ResolveEffects(source,target,new[]{Damage("freeze")},DamageResolutionContext.Empty());
            _test.Eq(before-target.GetCurrentHp(),expected,$"寒冰易伤与{tier}防护组合");
            before = target.GetCurrentHp();
            resolver.ResolveEffects(source,target,new[]{Damage("fire")},DamageResolutionContext.Empty());
            _test.Eq(before-target.GetCurrentHp(),6,"不影响火焰伤害");
        }
        var unit = Unit("lifecycle_target", "enemy", new(2,1));
        AddChill(unit,bone,2);
        resolver.ResolveEffects(source,unit,new[]{vulnerability},DamageResolutionContext.Empty());
        unit.GetStatusEffect(Vulnerable).duration=100;
        resolver.ResolveEffects(source,unit,new[]{vulnerability},DamageResolutionContext.Empty());
        _test.Eq(unit.GetStatusEffect(Vulnerable).duration,100,"重施不缩短剩余时间");
        _test.Eq(unit.GetStatusEffect(Vulnerable).stacks,1,"易伤本身不叠层");
        _test.True(BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(unit.GetStatusEffect(Vulnerable)),"支持有害魔法驱散");
        var raw = TestResourceOwnership.Own(new CombatEffectDef {effect_type="cleanse_harmful"}, "cold-cleanse");
        var cleanse = CombatEffectDefinition.FromDiagnosticFixture(raw,"cold-cleanse");
        resolver.ResolveEffects(source,unit,new[]{cleanse},DamageResolutionContext.Empty());
        _test.False(unit.HasStatusEffect(Vulnerable),"实际净化移除易伤");
        AddChill(unit,bone,2);
        resolver.ResolveEffects(source,unit,new[]{vulnerability},DamageResolutionContext.Empty());
        new BattleRuntimeSkillTurnResolver().AdvanceUnitStatusDurations(unit,60);
        _test.False(unit.HasStatusEffect(Vulnerable),"60TU后自然到期");
    }

    private void TestMastery(SkillDefinition cold, SkillDefinition bone)
    {
        var source=Unit("mastery_source","player",new(1,1),cold);
        var target=Unit("mastery_target","enemy",new(2,1));
        AddChill(target,bone,2);
        using var damage = new FixedFailedSaveDamageResolver();
        using var mastery = new BattleSkillMasteryService();
        var result=damage.ResolveEffects(source,target,Effects(cold,0),DamageResolutionContext.Empty());
        mastery.RecordTargetResult(source,target,cold,result,Effects(cold,0));
        _test.Eq(mastery.ResolveActiveSkillMasteryAmount(),1,"伤害与易伤同目标只计一次");
        mastery.Clear();
        result=damage.ResolveEffects(source,target,new[]{Effects(cold,0)[1]},DamageResolutionContext.Empty());
        mastery.RecordTargetResult(source,target,cold,result);
        _test.Eq(mastery.ResolveActiveSkillMasteryAmount(),0,"仅刷新易伤不重复奖励熟练度");
    }

    private void TestAi(SkillDefinition cold, SkillDefinition bone)
    {
        SkillDefinition follow = FollowUp();
        var mage=Unit("ai_mage","player",new(1,1),cold);
        var ally=Unit("ai_ally","player",new(2,2),follow);
        var target=Unit("ai_enemy","enemy",new(3,1));
        AddChill(target,bone,2);
        using var f=Fixture(new[]{cold,bone,follow},new[]{mage,ally},new[]{target});
        using var scorer=new BattleAiScoreService();
        var context=new BattleAiContext { state=f.State,unit_state=mage,grid_service=f.Runtime._grid_service };
        context.SetSkillDefinitions(new Dictionary<StringName,SkillDefinition>{{Cold,cold},{Bone,bone},{follow.SkillId,follow}});
        BattleAiScoreInput Score()
        {
            var command=Command(mage,target,cold);
            var preview=f.Runtime.PreviewCommand(command);
            try {return scorer.BuildSkillScoreInput(context,cold,command,preview,Effects(cold,0),
                new Dictionary<string,object>{{"desired_min_distance",0},{"desired_max_distance",5}});}
            finally {BattleTestFixture.DisposeBattlePreview(preview);BattleTestFixture.DisposeBattleCommand(command);}
        }
        var fresh=Score();
        _test.True(fresh.estimated_vulnerability_follow_up_damage>0,"有后续寒冰攻击才有易伤收益");
        _test.Eq(fresh.estimated_control_count,0,"易伤不领取固定控制分");
        _test.False(target.HasStatusEffect(Vulnerable),"AI不写正式状态");
        _test.Eq(target.GetStatusEffect("bone_chill").stacks,2,"AI不消耗寒蚀");
        ally.SetCooldownTyped(follow.SkillId,999);
        _test.Eq(Score().estimated_vulnerability_follow_up_damage,0,"后续技能不可用时收益为零");
        ally.SetCooldownTyped(follow.SkillId,0);
        target.GetStatusEffect("bone_chill").stacks=1;
        _test.Eq(Score().estimated_vulnerability_follow_up_damage,0,"一层寒蚀没有易伤收益");
        target.GetStatusEffect("bone_chill").stacks=2;
        target.SetStatusEffect(BattleStatusSemanticTable.MergeStatus(Effects(cold,0)[1],mage.unit_id));
        _test.Eq(Score().estimated_vulnerability_follow_up_damage,0,"已有同等易伤不重复估值");
    }

    private static CombatEffectDefinition[] Effects(SkillDefinition skill,int level) =>
        skill.CombatProfile.EffectDefinitions.Where(e=>e.IsUnlockedAtSkillLevel(level)).ToArray();
    private static void AddChill(BattleUnitState target,SkillDefinition bone,int count)
    {
        var effect=Effects(bone,0).Single(e=>e.StatusId==new StringName("bone_chill"));
        for(int i=0;i<count;i++) target.SetStatusEffect(BattleStatusSemanticTable.MergeStatus(effect,
            new StringName($"source_{i}"),target.GetStatusEffect("bone_chill")));
    }
    private static CombatEffectDefinition Damage(string tag)
    {
        var raw=TestResourceOwnership.Own(new CombatEffectDef {effect_type="damage",damage_tag=tag,dice_count=1,dice_sides=6},"cold-follow-up");
        return CombatEffectDefinition.FromDiagnosticFixture(raw,"cold-follow-up");
    }
    private static SkillDefinition FollowUp() => TestSkillDefinitionProjection.BuildSkill("cold_test_follow_up",
        combatProfile:TestSkillDefinitionProjection.BuildCombatProfile("cold_test_follow_up",effects:new[]{Damage("freeze")},
            targetMode:"unit",targetSelectionMode:"single_unit",targetTeamFilter:"enemy",rangeValue:5,apCost:1));
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
    private static BattleTestFixture Fixture(SkillDefinition[] skills,BattleUnitState[] allies,BattleUnitState[] enemies)
    {
        var f=BattleTestFixture.CreateFlatBattle("cold_snap",new Vector2I(8,5),allies,enemies);
        f.Runtime.setup(null,skills.ToDictionary(s=>s.SkillId));
        f.Runtime.SetupStateForTests(f.State);
        f.State.active_unit_id=allies[0].unit_id;
        return f;
    }
    private static BattleCommand Command(BattleUnitState actor,BattleUnitState target,SkillDefinition skill) => new() {
        CommandKind=BattleCommandKind.Skill,unit_id=actor.unit_id,skill_id=skill.SkillId,
        skill_entry_id=BattleSkillEntryIds.KnownSkill(skill.SkillId),target_coord=target.GetAnchorCoord(),
        target_unit_id=skill.CombatProfile.TargetModeKind==BattleTargetMode.Unit?target.unit_id:new StringName("")};
    private void Cast(BattleTestFixture f,BattleUnitState actor,BattleUnitState target,SkillDefinition skill)
    {
        f.State.active_unit_id=actor.unit_id;
        f.State.phase="unit_acting";
        var command=Command(actor,target,skill);
        try
        {
            var preview=f.Runtime.PreviewCommand(command);
            _test.True(preview.allowed,"真实施放预览合法: "+string.Join(" | ",preview.LogLinesTyped));
            BattleTestFixture.DisposeBattlePreview(preview);
            f.Runtime.IssueCommand(command)?.Dispose();
        }
        finally {BattleTestFixture.DisposeBattleCommand(command);}
    }
}
