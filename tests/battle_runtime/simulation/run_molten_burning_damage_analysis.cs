using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

// Opt-in balance probe. Does not change production content or run a win-rate simulation.
public partial class run_molten_burning_damage_analysis : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly Dictionary<StringName,SkillDefinition> _skills = new();
    private readonly List<object> _sources = new(), _rows = new(), _mixed = new(), _sequences = new(), _naturalTotals = new(), _kills = new();
    public override void _Initialize() => RunAfterProcessStartup(Run);
    private void Run()
    {
        try
        {
            foreach(string id in new[]{"mage_molten_burst","mage_cinder_bolt","mage_fireball","mage_burning_hands","mage_bone_chill"})
                _skills[id]=TestSkillDefinitionProjection.LoadSkillDefinition(id);
            foreach(string id in new[]{"mage_cinder_bolt","mage_fireball","mage_burning_hands"})
                foreach(int level in new[]{3,5,7}) ProbeSource(id,level);
            ProbeMixed();
            foreach(string policy in new[]{"wait","molten","cinder","bone"}) ProbeSequence(policy);
            foreach(string policy in new[]{"wait","molten","cinder","bone"}) ProbeKill(policy);
            var alternatives=new List<object>();
            foreach(string id in new[]{"mage_cinder_bolt","mage_bone_chill"})
                foreach(int level in new[]{3,5,7})
                {
                    var caster=Unit("caster","player");
                    var target=Unit("target","enemy");
                    using var resolver=Resolver();
                    var effect=Effects(id,level).Single(e=>e.EffectKind==BattleEffectKind.Damage);
                    var preview=resolver.PreviewDamageEffectTyped(caster,target,effect,Context(id,level));
                    alternatives.Add(new{skill=id,level,ap=_skills[id].CombatProfile.GetEffectiveResourceCostValues(level).ApCost,
                        mp=_skills[id].CombatProfile.GetEffectiveResourceCostValues(level).MpCost,
                        dice_count=effect.DiceCount,dice_sides=effect.DiceSides,conditional_average_roll_hp=preview.HpDamage});
                }
            var path=ProjectSettings.GlobalizePath("res://docs/reviews/evidence/2026-09-15-molten-burning-damage.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,JsonSerializer.Serialize(new{
                kind="controlled_production_resolver_analysis",source_rows=_sources,natural_total_rows=_naturalTotals,burst_rows=_rows,mixed_rows=_mixed,
                sequence_rows=_sequences,alternative_rows=alternatives,kill_rows=_kills,
                assumptions=new[]{"One target; successful source application; no miss/critical/fumble; saves forced to fail for fire setup.",
                    "HP1000, no equipment, no resistance/shield; average dice mode uses engine integer rounding.",
                    "0TU means immediate second action or ally follow-up; 40TU is one ordinary activation later.",
                    "Sequence costs are recorded from definitions; effects are resolved directly without command hit checks or MP payments. Every sequence fits the same 160MP no-recovery budget.",
                    "Kill probe starts from a controlled 10HP snapshot at 40TU; enemy activations at 45/85TU are an explicit scenario, not AI simulation.",
                    "No production values changed; not a battle win-rate calibration."}},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print($"MOLTEN_ANALYSIS complete sources={_sources.Count} rows={_rows.Count} mixed={_mixed.Count} sequences={_sequences.Count} kills={_kills.Count} output={path}");
        }
        catch(Exception ex) { _test.Fail(ex.ToString()); }
        RequestTestExit(_test.Finish("Molten burning damage analysis"));
    }

    private void ProbeSource(string id,int level)
    {
        var caster=Unit("caster","player");
        var target=Unit("target","enemy");
        using var f=Fixture(caster,target);
        using var resolver=Resolver();
        var result=resolver.ResolveEffects(caster,target,Effects(id,level),Context(id,level).WithBattleState(f.State));
        var burn=target.GetStatusEffect("burning");
        if(burn?.HasSourceContributionsTyped()!=true) throw new InvalidOperationException($"No source from {id} L{level}");
        var source=burn.GetSourceContributionsTyped().Single();
        int initialDuration=source.DurationTu;
        _sources.Add(new{skill=id,level,direct_average_roll_hp=result.Damage,
            source.Power,source.Stacks,source.DurationTu,source.TickIntervalTu,damage_tag=source.DamageTag.ToString(),
            source.TimelineDamageDiceCount,source.TimelineDamageDiceSides,
            ap=_skills[id].CombatProfile.GetEffectiveResourceCostValues(level).ApCost,
            mp=_skills[id].CombatProfile.GetEffectiveResourceCostValues(level).MpCost});
        target.SetCurrentHp(1000);
        foreach(int time in new[]{0,20,40})
        {
            Advance(f,target,time);
            foreach(int moltenLevel in new[]{0,3,5,7})
            {
                var preview=resolver.PreviewStatusTickAdvanceTyped(caster,target,Molten(moltenLevel),f.State);
                _rows.Add(new{source=id,source_level=level,at_tu=time,molten_level=moltenLevel,
                    natural_damage_so_far=1000-target.GetCurrentHp(),preview.TickCount,preview.HpDamage,
                    first_due_tu=resolver.BuildStatusTickAdvancePlanTyped(target,Molten(moltenLevel),f.State).FirstOrDefault()?.DueTu,
                    last_due_tu=resolver.BuildStatusTickAdvancePlanTyped(target,Molten(moltenLevel),f.State).LastOrDefault()?.DueTu});
            }
        }
        Advance(f,target,Math.Max(initialDuration,40));
        _naturalTotals.Add(new{skill=id,level,total_natural_hp=1000-target.GetCurrentHp()});
        GD.Print($"SOURCE {id} L{level}: {source.Power}/tick {initialDuration}TU total={1000-target.GetCurrentHp()}");
    }

    private void ProbeMixed()
    {
        foreach(bool weakFire in new[]{false,true})
        {
            var caster=Unit("caster","player");
            var target=Unit("target","enemy");
            using var f=Fixture(caster,target);
            using var resolver=Resolver();
            resolver.ResolveEffects(caster,target,Effects("mage_cinder_bolt",7),Context("mage_cinder_bolt",7).WithBattleState(f.State));
            if(weakFire) resolver.ResolveEffects(caster,target,Effects("mage_fireball",3),Context("mage_fireball",3).WithBattleState(f.State));
            target.SetCurrentHp(1000);
            foreach(int level in new[]{0,3,5,7})
            {
                var preview=resolver.PreviewStatusTickAdvanceTyped(caster,target,Molten(level),f.State);
                _mixed.Add(new{weak_fireball_added=weakFire,molten_level=level,preview.TickCount,preview.HpDamage,preview.SourceCount});
            }
        }
    }

    private void ProbeSequence(string policy)
    {
        var caster=Unit("caster","player");
        var target=Unit("target","enemy");
        using var f=Fixture(caster,target);
        using var resolver=Resolver();
        resolver.ResolveEffects(caster,target,Effects("mage_cinder_bolt",5),Context("mage_cinder_bolt",5).WithBattleState(f.State));
        Advance(f,target,40);
        int before=1000-target.GetCurrentHp(), secondDamage=0, extraMp=0, extraAp=0;
        if(policy!="wait")
        {
            string id=policy=="molten"?"mage_molten_burst":policy=="cinder"?"mage_cinder_bolt":"mage_bone_chill";
            secondDamage=resolver.ResolveEffects(caster,target,Effects(id,5),Context(id,5).WithBattleState(f.State)).Damage;
            var cost=_skills[id].CombatProfile.GetEffectiveResourceCostValues(5);
            extraMp=cost.MpCost; extraAp=cost.ApCost;
        }
        int at40=1000-target.GetCurrentHp();
        Advance(f,target,80); int at80=1000-target.GetCurrentHp();
        Advance(f,target,160); int at160=1000-target.GetCurrentHp();
        Advance(f,target,320); int at320=1000-target.GetCurrentHp();
        _sequences.Add(new{policy,damage_before_second_action=before,second_action_damage=secondDamage,
            total_ap=1+extraAp,total_mp=60+extraMp,damage_at_40=at40,damage_at_80=at80,damage_at_160=at160,damage_at_320=at320});
    }

    private void Advance(BattleTestFixture f,BattleUnitState target,int until)
    {
        var turn=new BattleRuntimeSkillTurnResolver(); turn.Setup(f.Runtime);
        using var batch=new BattleEventBatch();
        while(f.State.timeline.current_tu<until)
        {
            f.State.timeline.current_tu+=5;
            turn.ApplyUnitStatusPeriodicTicksResult(target,5,batch);
            turn.AdvanceUnitStatusDurations(target,5);
        }
    }

    private void ProbeKill(string policy)
    {
        var caster=Unit("caster","player");
        var target=Unit("target","enemy");
        using var f=Fixture(caster,target);
        using var resolver=Resolver();
        resolver.ResolveEffects(caster,target,Effects("mage_cinder_bolt",5),Context("mage_cinder_bolt",5).WithBattleState(f.State));
        Advance(f,target,40);
        target.SetCurrentHp(10);
        if(policy!="wait")
        {
            string id=policy=="molten"?"mage_molten_burst":policy=="cinder"?"mage_cinder_bolt":"mage_bone_chill";
            resolver.ResolveEffects(caster,target,Effects(id,5),Context(id,5).WithBattleState(f.State));
        }
        while(target.IsAlive() && f.State.timeline.current_tu<160)
            Advance(f,target,f.State.timeline.current_tu+5);
        _kills.Add(new{policy,initial_snapshot_tu=40,initial_hp=10,defeated=!target.IsAlive(),
            defeat_tu=f.State.timeline.current_tu,
            hypothetical_enemy_activations_before_defeat=new[]{45,85,125}.Count(t=>t<f.State.timeline.current_tu)});
    }
    private CombatEffectDefinition[] Effects(string id,int level)=>_skills[id].CombatProfile.EffectDefinitions.Where(e=>e.IsUnlockedAtSkillLevel(level)).ToArray();
    private CombatEffectDefinition Molten(int level)=>Effects("mage_molten_burst",level).Single();
    private static DamageResolutionContext Context(string id,int level)=>DamageResolutionContext.Create(false,true,false,skillId:id,
        sourceSkillLevel:level,damageRollMode:"average",saveRollOverrides:Enumerable.Repeat(1,8).ToArray());
    private BattleDamageResolver Resolver(){var r=new BattleDamageResolver();r.SetSkillDefinitions(_skills);return r;}
    private BattleTestFixture Fixture(BattleUnitState caster,BattleUnitState target)
    {
        var f=BattleTestFixture.CreateFlatBattle("molten_analysis",new Vector2I(8,5),new[]{caster},new[]{target});
        f.Runtime.setup(null,_skills);f.Runtime.SetupStateForTests(f.State);return f;
    }
    private static BattleUnitState Unit(string id,string faction)=>BattleTestFixture.BuildUnit(id,faction,
        faction=="player"?new Vector2I(1,1):new Vector2I(3,1),currentAp:2,currentHp:1000);
}
