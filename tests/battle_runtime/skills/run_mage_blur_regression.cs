using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_mage_blur_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "mage_blur";
    private static readonly StringName StatusId = "blurred_form";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);
    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition("mage_blur", "blur");
            TestDefinition(skill);
            TestProgression(skill);
            TestCastAndStatus(skill);
            TestAttackAndMastery(skill);
            TestAi(skill);
        }
        catch (Exception ex) { _test.Fail($"Unhandled: {ex}"); }
        RequestTestExit(_test.Finish("Mage blur regression"));
    }

    private void TestDefinition(SkillDefinition skill)
    {
        int[] mp = {60,55,55,55,50,50,50,45};
        int[] duration = {60,60,80,80,80,100,100,100};
        int[] cooldown = {180,180,180,160,160,160,120,120};
        _test.Eq(skill.NonCoreMaxLevel, 5, "非核心上限");
        _test.Eq(skill.MaxLevel, 7, "绝对上限");
        _test.Eq(skill.MasteryCurve.Take(5).Sum(), 800, "0到5级累计需求");
        _test.Eq(skill.CombatProfile.MasteryTriggerModeKind,
            CombatSkillMasteryTriggerMode.IncomingAttackDisadvantage, "实际干扰触发");
        for (int level=0; level<=7; level++)
        {
            SkillEffectiveCombatDefinition effective = SkillEffectiveCombatDefinition.BuildUncached(skill, level);
            var effects = skill.CombatProfile.EffectDefinitions.Where(x=>x.IsUnlockedAtSkillLevel(level)).ToArray();
            _test.Eq(effects.Length, 1, $"L{level}恰有一个状态");
            _test.True(effects[0].IncomingAttackRollDisadvantage, "typed能力");
            _test.Eq(effects[0].DurationTu, duration[level], "持续");
            _test.Eq(effective.ResourceCosts.MpCost, mp[level], "MP");
            _test.Eq(effective.ResourceCosts.CooldownTu, cooldown[level], "冷却");
            _test.Eq(effective.ResourceCosts.ApCost, 1, "AP");
            using var config = new GDictionary();
            string description = SkillLevelDescriptionFormatter.BuildLevelDescription(skill, level, config);
            _test.True(description.Contains($"{duration[level]}TU") && description.Contains("劣势"), "准确等级描述");
        }
        var invalid = TestResourceOwnership.Own(new CombatEffectDef
        {
            effect_type = "damage", damage_tag = "fire", dice_count = 1, dice_sides = 6,
            incoming_attack_roll_disadvantage = true,
        }, "blur-invalid");
        var effect = CombatEffectDefinition.FromDiagnosticFixture(invalid, "blur-invalid");
        var errors = new Godot.Collections.Array<string>();
        var validator = new SkillDefinitionCombatProfileValidator(
            new SkillDefinitionDamageEffectValidator(), new SkillDefinitionExecuteEffectValidator());
        validator.AppendEffectValidationErrors(errors, SkillId, effect, "invalid", skill);
        _test.True(errors.Any(x=>x.Contains("incoming_attack_roll_disadvantage")), "非状态字段应拒绝");
    }

    private void TestProgression(SkillDefinition skill)
    {
        var progress = new UnitProgress { unit_id = "blur_progression" };
        var service = new ProgressionService();
        service.SetupDefinitions(progress,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill },
            new Dictionary<StringName, ProfessionDefinition>());
        _test.True(service.LearnSkill(SkillId), "生产技能可正常学习");
        _test.Eq(progress.GetSkillProgress(SkillId)?.skill_level ?? -1, 0, "学会从0级开始");
        _test.True(service.GrantSkillMastery(SkillId, 799, "battle"), "战斗来源可入账");
        _test.Eq(progress.GetSkillProgress(SkillId).skill_level, 4, "799点尚未达到5级");
        _test.True(service.GrantSkillMastery(SkillId, 1, "battle"), "最后一点可入账");
        _test.Eq(progress.GetSkillProgress(SkillId).skill_level, 5, "800点达到非核心上限");
        service.GrantSkillMastery(SkillId, 1000, "battle");
        _test.Eq(progress.GetSkillProgress(SkillId).skill_level, 5, "额外熟练度不得绕过晋升门槛");
        _test.Eq(progress.GetSkillProgress(SkillId).current_mastery, 0, "到达上限不囤积当前进度");
        using var mastery = new BattleSkillMasteryService();
        var stats = new BattleRatingMemberStats();
        stats.cast_counts[SkillId] = 1;
        _test.Eq(mastery.BuildBattleRatingMasteryRewardEntries(stats, 4, "测试")[0].amount,
            4, "评级奖励独立于防护事件");
        stats.cast_counts[SkillId] = 10;
        _test.Eq(mastery.BuildBattleRatingMasteryRewardEntries(stats, 4, "测试")[0].amount,
            4, "多次施放不倍增评级奖励");
    }

    private void TestCastAndStatus(SkillDefinition skill)
    {
        var mage=Unit("blur_cast", "player", Vector2I.Zero, skill,7);
        var enemy=Unit("blur_threat","enemy",new Vector2I(2,0),Shot(),1);
        var gateway=new MasteryGatewayStub();
        using BattleTestFixture f=Fixture(skill,mage,enemy,gateway);
        BattleCommand command=Self(mage);
        try
        {
            BattlePreview preview=f.Runtime.PreviewCommand(command);
            _test.True(preview.allowed, "自身施法预览合法: "+string.Join(" | ",preview.LogLinesTyped));
            _test.False(mage.HasStatusEffect(StatusId),"preview不写状态");
            _test.Eq(gateway.Grants.Count,0,"preview不发熟练度");
            f.Runtime.IssueCommand(command);
            var status=mage.GetStatusEffect(StatusId);
            _test.True(status?.incoming_attack_roll_disadvantage==true,"执行带入typed能力");
            _test.Eq(status?.source_skill_id??new StringName(""),SkillId,"记录来源技能");
            _test.Eq(status?.duration??0,100,"满级持续");
            _test.Eq(mage.GetCurrentMp(),75,"实际扣45MP");
            _test.Eq(gateway.Grants.Count,0,"施放本身0熟练度");
            if(status==null)return;
            _test.True(status.DuplicateState().incoming_attack_roll_disadvantage,"clone保真");
            _test.True(status.DuplicateForMutationSnapshotExact().incoming_attack_roll_disadvantage,"mutation快照保真");
            using var lease=status.ToDictionaryLease();
            _test.True(BattleStatusEffectState.FromDictionary(lease.Value)?.incoming_attack_roll_disadvantage==true,"strict往返保真");
            lease.Value["incoming_attack_roll_disadvantage"]="true";
            _test.True(BattleStatusEffectState.FromDictionary(lease.Value)==null,"拒绝错误类型");

            status.duration=20;
            mage.ResetTurnStateForTurnStartTyped();
            mage.SetCurrentAp(2);
            mage.SetCooldownTyped(SkillId,0);
            f.State.active_unit_id=mage.unit_id;
            f.State.PhaseKind=BattlePhaseKind.UnitActing;
            f.Runtime.IssueCommand(command);
            _test.Eq(mage.GetStatusEffect(StatusId)?.duration??0,100,"刷新完整窗口");
            _test.Eq(mage.GetStatusEffect(StatusId)?.stacks??0,1,"刷新不叠加");
            _test.Eq(gateway.Grants.Count,0,"刷新本身0熟练度");
            new BattleRuntimeSkillTurnResolver().AdvanceUnitStatusDurations(mage,100);
            _test.False(mage.HasStatusEffect(StatusId),"到期清除");
            mage.SetCurrentMp(0);
            mage.SetCooldownTyped(SkillId,0);
            mage.SetCurrentAp(2);
            _test.False(f.Runtime.PreviewCommand(command).allowed,"无MP不可用");
            _test.Eq(gateway.Grants.Count,0,"失败不发熟练度");
        }
        finally { BattleTestFixture.DisposeBattleCommand(command); }
    }

    private void TestAttackAndMastery(SkillDefinition skill)
    {
        SkillDefinition shot=Shot();
        var mage=Unit("blur_defender","player",Vector2I.Zero,skill,7);
        var enemy=Unit("blur_attacker","enemy",new Vector2I(2,0),shot,1);
        var gateway=new MasteryGatewayStub();
        using BattleTestFixture f=Fixture(skill,mage,enemy,gateway);
        Protect(mage);
        using var hit=new BattleHitResolver();
        var raw=hit.BuildSkillAttackCheck(enemy,mage,shot);
        int protectedChance=hit.BuildFateAwareAttackCheckPreview(f.State,enemy,mage,raw).SuccessRatePercent;
        var unprotected=mage.DuplicateForPreview();
        unprotected.EraseStatusEffect(StatusId);
        int normalChance=hit.BuildFateAwareAttackCheckPreview(f.State,enemy,unprotected,raw).SuccessRatePercent;
        _test.True(protectedChance<normalChance,"canonical普通命中率下降");
        _test.Eq(gateway.Grants.Count,0,"概率preview无熟练度副作用");
        AttackContext CheckContext(bool force = false) => new(new[] {18,5,18,5})
        {
            BattleState = f.State, HasIsDisadvantage = true, IsDisadvantage = false,
            ForceHitAllowCrit = force,
        };
        var normalMetadata = hit.ResolveAttackMetadata(enemy, unprotected, raw, CheckContext());
        var protectedMetadata = hit.ResolveAttackMetadata(enemy, mage, raw, CheckContext());
        _test.False(normalMetadata.IsDisadvantage, "无保护的实际检定无劣势");
        _test.True(protectedMetadata.IsDisadvantage, "来源明确false仍保留目标带来的实际劣势");
        _test.Eq(protectedMetadata.IncomingDisadvantageStatusId, StatusId, "metadata保留唯一贡献者");
        var forcedBefore = hit.ResolveAttackMetadata(enemy, unprotected, raw, CheckContext(true));
        var forcedAfter = hit.ResolveAttackMetadata(enemy, mage, raw, CheckContext(true));
        _test.Eq(forcedAfter.CritGateDie, forcedBefore.CritGateDie, "必中可暴击的门骰不受防护改变");
        _test.Eq(forcedAfter.CriticalHit, forcedBefore.CriticalHit, "同一固定序列的必中暴击结果一致");
        _test.False(forcedAfter.IsDisadvantage, "必中没有目标劣势贡献");

        Attack(f,enemy,mage,shot,gateway,1);
        enemy.attribute_snapshot.SetValue("fortune_mark_target",1);
        Attack(f,enemy,mage,shot,gateway,2);
        enemy.attribute_snapshot.SetValue("boss_target",1);
        Attack(f,enemy,mage,shot,gateway,3);
        Attack(f,enemy,mage,shot,gateway,0,sourceDisadvantage:true);
        Attack(f,enemy,mage,shot,gateway,0,forceHit:true);
        Attack(f,enemy,mage,shot,gateway,0,forceAllowCrit:true);
        Attack(f,enemy,mage,shot,gateway,3,advantage:true);
        enemy.faction_id="player";
        Attack(f,enemy,mage,shot,gateway,0);
        enemy.faction_id="enemy";
        var dispel = TestResourceOwnership.Own(new CombatEffectDef
        {
            effect_type = "dispel_magic", remove_beneficial_from_enemies = true,
            power = 1, max_status_removed = 1,
        }, "blur-dispel");
        f.Runtime._damage_resolver.ResolveEffects(enemy, mage,
            new[] { CombatEffectDefinition.FromDiagnosticFixture(dispel, "blur-dispel") });
        _test.False(mage.HasStatusEffect(StatusId), "通用驱散实际移除幻身");
        Attack(f,enemy,mage,shot,gateway,0);
        Protect(mage);
        mage.GetStatusEffect(StatusId).duration=0;
        Attack(f,enemy,mage,shot,gateway,0);

        var status=new BattleStatusEffectState
        {
            status_id="other_defense",incoming_attack_roll_disadvantage=true,duration=60,
            source_unit_id=mage.unit_id,source_skill_id="not_learned",
        };
        using var mastery=new BattleSkillMasteryService();
        _test.True(mastery.BuildIncomingAttackDisadvantageGrant(mage,enemy,status,
            new Dictionary<StringName,SkillDefinition>{{SkillId,skill}})==null,"未知来源不得领熟练度");
    }

    private void Attack(BattleTestFixture f,BattleUnitState enemy,BattleUnitState mage,
        SkillDefinition shot,MasteryGatewayStub gateway,int expected,
        bool sourceDisadvantage=false,bool forceHit=false,bool forceAllowCrit=false,bool advantage=false)
    {
        int prior=gateway.Grants.Count;
        using var batch=new BattleEventBatch();
        var check=new AttackCheckInput(requiredRoll:10,hitRatePercent:55,successRatePercent:55,
            naturalOneAutoMiss:true,naturalTwentyAutoHit:true,isAdvantage:advantage);
        BattleReactionRootTestHelper.ExecuteLogicalAttack(f.Runtime,batch,enemy,shot.CombatProfile.EffectDefinitions,
            action=>f.Runtime._damage_resolver.ResolveAttackEffects(enemy,mage,shot.CombatProfile.EffectDefinitions,
                check,new AttackContext(new[]{18,5,18,5})
                {
                    Action=action,BattleState=f.State,EventBatch=batch,SkillId=shot.SkillId,
                    HasIsDisadvantage=true,IsDisadvantage=sourceDisadvantage,
                    ForceHitNoCrit=forceHit,ForceHitAllowCrit=forceAllowCrit,
                    DamageOriginKind=BattleDamageOriginKind.MainDirectEffect,
                }));
        _test.Eq(gateway.Grants.Count-prior,expected>0?1:0,"每次攻击一次授予，绕过/冗余为零");
        if(expected>0 && gateway.Grants.Count>prior)
        {
            _test.Eq(gateway.Grants[^1].MasteryAmount,expected,"按敌人等级计量");
            _test.Eq(gateway.Grants[^1].SkillId,SkillId,"授予状态来源技能");
        }
    }

    private void TestAi(SkillDefinition skill)
    {
        SkillDefinition shot=Shot();
        var mage=Unit("blur_ai","player",Vector2I.Zero,skill,7);
        var enemy=Unit("blur_ai_enemy","enemy",new Vector2I(2,0),shot,1);
        using BattleTestFixture f=Fixture(skill,mage,enemy,new MasteryGatewayStub());
        using var scorer=new BattleAiScoreService();
        var context=new BattleAiContext{state=f.State,unit_state=mage,grid_service=f.Runtime._grid_service};
        context.SetSkillDefinitions(new Dictionary<StringName,SkillDefinition>{{SkillId,skill},{shot.SkillId,shot}});
        BattleCommand cmd=Self(mage);
        try
        {
            BattleAiScoreInput Score()=>scorer.BuildSkillScoreInput(context,skill,cmd,
                f.Runtime.PreviewCommand(cmd),skill.CombatProfile.EffectDefinitions,
                new Dictionary<string,object>{{"desired_min_distance",0},{"desired_max_distance",4}});
            BattleAiScoreInput fresh=Score();
            _test.True(fresh.estimated_incoming_attack_damage_relief>0,"真实威胁有防护收益");
            _test.Eq(fresh.estimated_control_count,0,"不得追加控制分");
            _test.Eq(fresh.estimated_status_count,0,"不得追加固定状态分");
            _test.False(mage.HasStatusEffect(StatusId),"AI不写正式状态");
            Protect(mage);
            _test.Eq(Score().estimated_incoming_attack_damage_relief,0,"既有保护不重复计价");
            mage.EraseStatusEffect(StatusId);
            enemy.SetCooldownTyped(shot.SkillId,999);
            _test.Eq(Score().estimated_incoming_attack_damage_relief,0,"不可用攻击不计威胁");
        }
        finally {BattleTestFixture.DisposeBattleCommand(cmd);}
    }

    private static SkillDefinition Shot()
    {
        var effects = new List<CombatEffectDefinition>();
        foreach (int count in new[] {2,1})
        {
            var raw = TestResourceOwnership.Own(new CombatEffectDef
            {
                effect_type="damage",effect_target_team_filter="enemy",damage_tag="fire",
                dice_count=count,dice_sides=6,min_skill_level=0,max_skill_level=-1,
            }, "blur-shot");
            effects.Add(CombatEffectDefinition.FromDiagnosticFixture(raw,"blur-shot"));
        }
        return TestSkillDefinitionProjection.BuildSkill("blur_test_attack",
            combatProfile:TestSkillDefinitionProjection.BuildCombatProfile("blur_test_attack",
                effects:effects,targetMode:"unit",targetTeamFilter:"enemy",targetSelectionMode:"single_unit",
                rangeValue:4,attackResolutionMode:"fate_attack"));
    }
    private static BattleUnitState Unit(string id,string faction,Vector2I coord,SkillDefinition skill,int level)
    {
        var unit=BattleTestFixture.BuildUnit(id,faction,coord,currentAp:2,currentHp:1000);
        unit.source_member_id=id;
        unit.SetCombatResources(1000,120,100,0,2,2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX,1000);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX,120);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX,100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS,14);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS,3);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS,3);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.AddKnownActiveSkill(skill.SkillId);
        unit.SetKnownSkillLevelTyped(skill.SkillId,level);
        return unit;
    }
    private static void Protect(BattleUnitState mage)=>mage.SetStatusEffect(new BattleStatusEffectState
    {
        status_id=StatusId,source_unit_id=mage.unit_id,source_skill_id=SkillId,
        source_skill_level=7,incoming_attack_roll_disadvantage=true,duration=100,power=1,stacks=1,
    });
    private static BattleTestFixture Fixture(SkillDefinition skill,BattleUnitState mage,BattleUnitState enemy,
        MasteryGatewayStub gateway)
    {
        var f=BattleTestFixture.CreateFlatBattle("blur_test",new Vector2I(8,4),new[]{mage},new[]{enemy});
        var shot=Shot();
        f.Runtime.setup(gateway,new Dictionary<StringName,SkillDefinition>{{skill.SkillId,skill},{shot.SkillId,shot}});
        f.Runtime.SetupStateForTests(f.State);
        return f;
    }
    private static BattleCommand Self(BattleUnitState mage)
    {
        var cmd=new BattleCommand{CommandKind=BattleCommandKind.Skill,unit_id=mage.unit_id,
            skill_id=SkillId,skill_entry_id=BattleSkillEntryIds.KnownSkill(SkillId),target_unit_id=mage.unit_id};
        cmd.AddTargetUnitId(mage.unit_id);
        return cmd;
    }
    private sealed class MasteryGatewayStub : IBattleRuntimeCharacterGateway
    {
        internal List<CharacterMasteryChangeFact> Grants { get; } = new();
        public PartyState GetPartyState() => null;
        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() => new Dictionary<StringName, ItemDefinition>();
        public bool HasItemDefCatalog() => false;
        public ItemDefinition GetItemDef(StringName itemId) => null;
        public PartyMemberState GetMemberState(StringName memberId) => null;
        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(StringName memberId, EquipmentState equipmentView) => null;
        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(StringName memberId, EquipmentState equipmentView) => new();
        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(StringName memberId, EquipmentState equipmentView) => BattleEffectiveTraitProjection.Empty;
        public PassiveSourceContext BuildPassiveSourceContext(StringName memberId, UnitProgress progressionState) => null;
        public CharacterProgressionDelta PromoteProfession(StringName memberId, StringName professionId, PromotionCommitRequest selection) => new() { member_id = memberId };
        public BattleResourceCommitResult CommitBattleResources(StringName memberId, int currentHp, int currentMp, int currentAura) => BattleResourceCommitResult.Success(memberId);
        public ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(StringName memberId, IReadOnlyCollection<StringName> consumedSetupIds) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public ContingencyConsumedCommitResult CommitContingencyConsumedSetups(StringName memberId, IReadOnlyCollection<StringName> consumedSetupIds) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public void CommitBattleDeath(StringName memberId) { }
        public int FlushAfterBattle() => (int)Error.Ok;
        public CharacterProgressionDelta GrantBattleMastery(StringName memberId, StringName skillId, int amount) => RecordGrant(memberId, skillId, amount, "battle");
        public CharacterProgressionDelta GrantSkillMasteryFromSource(StringName memberId, StringName skillId, int amount, StringName sourceType, string sourceLabel, string reasonText, bool emitAchievementEvent) => RecordGrant(memberId, skillId, amount, sourceType);
        public IReadOnlyList<StringName> RecordAchievementEvent(StringName memberId, StringName eventType, int amount) => Array.Empty<StringName>();
        public IReadOnlyList<StringName> RecordAchievementEvent(StringName memberId, StringName eventType, int amount, StringName subjectId, GDictionary meta) => Array.Empty<StringName>();
        public PendingCharacterReward BuildPendingSkillMasteryReward(StringName memberId, StringName sourceType, string sourceLabel, IEnumerable<PendingCharacterRewardEntry> entryOptions, string summaryText) => null;

        private CharacterProgressionDelta RecordGrant(StringName memberId, StringName skillId, int amount, StringName sourceType)
        {
            var change = new CharacterMasteryChangeFact(skillId, skillId.ToString(), amount, sourceType, sourceType.ToString(), "");
            Grants.Add(change);
            var delta = new CharacterProgressionDelta { member_id = memberId };
            delta.AddMasteryChange(change);
            return delta;
        }
    }
}
