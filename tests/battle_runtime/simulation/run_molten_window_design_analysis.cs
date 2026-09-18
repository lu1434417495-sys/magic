using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

// Opt-in design evidence: real source applications and natural forecasts only.
// The proposed window effect is not implemented or installed in production.
public partial class run_molten_window_design_analysis : LifecycleTestSceneTree
{
    private sealed record Cast(int Tu, string Caster, string Skill, int Level);
    private sealed record SourceWindow(string Key, int Stacks, int Power,
        int DurationTu, int NextTickAtTu, Dictionary<int, int> FutureHp);

    private readonly TestHarness _test = new();
    private readonly Dictionary<StringName, SkillDefinition> _skills = new();
    private readonly List<object> _rows = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            foreach (string id in new[] { "mage_molten_burst", "mage_cinder_bolt", "mage_fireball", "mage_burning_hands" })
                _skills[id] = TestSkillDefinitionProjection.LoadSkillDefinition(id);

            foreach (int level in new[] { 3, 5, 7 })
            {
                Case($"one_cinder_l{level}", new[] { new Cast(0, "a", "mage_cinder_bolt", level) });
                Case($"one_fireball_l{level}", new[] { new Cast(0, "a", "mage_fireball", level) });
                Case($"one_hands_l{level}", new[] { new Cast(0, "a", "mage_burning_hands", level) });
            }
            Case("self_two_cinder_l5", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 5), new Cast(40, "a", "mage_cinder_bolt", 5) });
            Case("self_three_cinder_l5", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 5), new Cast(40, "a", "mage_cinder_bolt", 5),
                new Cast(80, "a", "mage_cinder_bolt", 5) });
            Case("self_cinder_then_fireball_l5", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 5), new Cast(40, "a", "mage_fireball", 5) });
            Case("team_two_cinder_l5", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 5), new Cast(0, "b", "mage_cinder_bolt", 5) });
            Case("team_three_cinder_l5", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 5), new Cast(0, "b", "mage_cinder_bolt", 5),
                new Cast(0, "c", "mage_cinder_bolt", 5) });
            Case("team_three_cinder_l7", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 7), new Cast(0, "b", "mage_cinder_bolt", 7),
                new Cast(0, "c", "mage_cinder_bolt", 7) });
            Case("four_sources_l7", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 7), new Cast(0, "b", "mage_cinder_bolt", 7),
                new Cast(0, "c", "mage_cinder_bolt", 7), new Cast(0, "a", "mage_fireball", 7) });
            Case("five_sources_l7", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 7), new Cast(0, "b", "mage_cinder_bolt", 7),
                new Cast(0, "c", "mage_cinder_bolt", 7), new Cast(0, "a", "mage_fireball", 7),
                new Cast(0, "b", "mage_fireball", 7) });
            Case("nine_sources_staggered_l7", new[] { "a", "b", "c" }.SelectMany(caster => new[] {
                new Cast(0, caster, "mage_cinder_bolt", 7), new Cast(40, caster, "mage_fireball", 7),
                new Cast(80, caster, "mage_burning_hands", 7) }).ToArray());
            Case("strong_cinder_weak_fireball", new[] {
                new Cast(0, "a", "mage_cinder_bolt", 7), new Cast(0, "b", "mage_fireball", 3) });

            string path = ProjectSettings.GlobalizePath(
                "res://docs/proposals/battle/skills/mage_molten_burst_assessment/window_sources_v5.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(new {
                kind = "production_source_forecasts_with_unimplemented_candidate_arithmetic",
                assumptions = new[] {
                    "Sources are applied by the production resolver with average damage dice and forced failed saves; attack/critical/fumble checks are outside this controlled probe.",
                    "Target HP1000; no equipment, mitigation, shield, healing, dispel, movement or source refresh during the forecast. Per-source forecasts are additive only under these conditions.",
                    "Source casts have explicit TU/caster/skill/level. Costs are recorded, not paid by commands; the candidate records affordability against 160MP per caster with no recovery.",
                    "Time0 coordinated casts assume enough team AP and legal same-TU order. Source stacks require the listed casts; high-cost stack cases may be unaffordable.",
                    "Candidate HP is the sum of selected natural window forecasts; this does not execute, consume, or validate the proposed new effect." },
                rows = _rows
            }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"MOLTEN_WINDOW_DESIGN rows={_rows.Count} output={path}");
        }
        catch (Exception ex) { _test.Fail(ex.ToString()); }
        RequestTestExit(_test.Finish("Molten window design analysis"));
    }

    private void Case(string id, Cast[] casts)
    {
        foreach (int delay in new[] { 0, 20, 40 })
            Snapshot(id, casts, casts.Max(c => c.Tu) + delay);
    }

    private void Snapshot(string id, Cast[] casts, int atTu)
    {
        var actors = Actors();
        var target = Target();
        using var fixture = Fixture(actors, target);
        using var resolver = new BattleDamageResolver();
        resolver.SetSkillDefinitions(_skills);
        var mp = new Dictionary<string, int>();
        int ap = 0;
        foreach (var cast in casts.OrderBy(c => c.Tu))
        {
            Advance(fixture, target, cast.Tu);
            var context = DamageResolutionContext.Create(false, true, false,
                skillId: cast.Skill, sourceSkillLevel: cast.Level, damageRollMode: "average",
                saveRollOverrides: Enumerable.Repeat(1, 8).ToArray()).WithBattleState(fixture.State);
            resolver.ResolveEffects(actors.Single(a => a.unit_id == cast.Caster), target,
                Effects(cast.Skill, cast.Level), context);
            var cost = _skills[cast.Skill].CombatProfile.GetEffectiveResourceCostValues(cast.Level);
            mp[cast.Caster] = mp.GetValueOrDefault(cast.Caster) + cost.MpCost;
            ap += cost.ApCost;
        }
        Advance(fixture, target, atTu);
        var status = target.GetStatusEffect("burning");
        var sources = new List<SourceWindow>();
        if (status?.HasSourceContributionsTyped() == true)
        {
            foreach (var source in status.GetSourceContributionsTyped())
            {
                var isolated = status.DuplicateState();
                foreach (var other in isolated.GetSourceContributionsTyped().ToArray())
                    if (other.Identity != source.Identity)
                        isolated.RemoveSourceContributionTyped(other.Identity);
                isolated.RebuildSourceContributionAggregateTyped();
                sources.Add(new SourceWindow(source.Identity.StableKey, source.Stacks, source.Power,
                    source.DurationTu, source.NextTickAtTu, Forecast(isolated, atTu)));
            }
        }
        var fullForecast = Forecast(status, atTu);
        foreach (int window in new[] { 20, 40, 60, 80, 160, 320 })
            _test.Eq(sources.Sum(s => s.FutureHp[window]), fullForecast[window],
                $"{id}/{atTu}/{window}: isolated source forecasts must reproduce natural total in this additive fixture");

        var candidate = new List<object>();
        for (int level = 0; level <= 7; level++)
        {
            int window = level < 3 ? 40 : level < 7 ? 60 : 80;
            int? count = level == 7 ? null : level < 5 ? 2 : 3;
            int cost = new[] { 40, 35, 35, 40, 40, 50, 45, 60 }[level];
            var ranked = sources.Where(s => s.FutureHp[window] > 0)
                .OrderByDescending(s => s.FutureHp[window]).ThenBy(s => s.DurationTu)
                .ThenBy(s => s.Key, StringComparer.Ordinal);
            var selected = count.HasValue ? ranked.Take(count.Value).ToArray() : ranked.ToArray();
            int hp = selected.Sum(s => s.FutureHp[window]);
            candidate.Add(new {
                level, window_tu = window, max_sources = count,
                source_selection_mode = count.HasValue ? "highest_window_damage" : "all_eligible", hp,
                selected = selected.Select(s => s.Key).ToArray(),
                total_team_mp = mp.Values.Sum() + cost, total_team_ap = ap + 1,
                self_a_mp = mp.GetValueOrDefault("a") + cost,
                fits_160_mp_each_with_a_finisher = hp > 0
                    && mp.All(p => p.Value + (p.Key == "a" ? cost : 0) <= 160),
                fits_160_mp_each_with_separate_finisher = hp > 0 && mp.All(p => p.Value <= 160)
            });
        }
        var old = resolver.PreviewStatusTickAdvanceTyped(actors[0], target,
            Effects("mage_molten_burst", 5).Single(), fixture.State);
        _rows.Add(new {
            id, at_tu = atTu, casts, source_ap = ap, source_mp_by_caster = mp,
            damage_before_finisher = 1000 - target.GetCurrentHp(), sources,
            natural_future_hp = fullForecast, current_l5_burst_hp = old.HpDamage, candidate
        });
    }

    private Dictionary<int, int> Forecast(BattleStatusEffectState status, int now)
    {
        var actors = Actors();
        var target = Target();
        using var fixture = Fixture(actors, target);
        fixture.State.timeline.current_tu = now;
        if (status != null) target.SetStatusEffect(status.DuplicateState());
        var values = new Dictionary<int, int>();
        foreach (int window in new[] { 20, 40, 60, 80, 160, 320 })
        {
            Advance(fixture, target, now + window);
            values[window] = 1000 - target.GetCurrentHp();
        }
        return values;
    }

    private static void Advance(BattleTestFixture fixture, BattleUnitState target, int until)
    {
        var turn = new BattleRuntimeSkillTurnResolver();
        turn.Setup(fixture.Runtime);
        using var batch = new BattleEventBatch();
        while (fixture.State.timeline.current_tu < until)
        {
            fixture.State.timeline.current_tu += 5;
            turn.ApplyUnitStatusPeriodicTicksResult(target, 5, batch);
            turn.AdvanceUnitStatusDurations(target, 5);
        }
    }

    private BattleTestFixture Fixture(BattleUnitState[] actors, BattleUnitState target)
    {
        var fixture = BattleTestFixture.CreateFlatBattle("molten_window_design",
            new Vector2I(9, 5), actors, new[] { target });
        fixture.Runtime.setup(null, _skills);
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private CombatEffectDefinition[] Effects(string id, int level) => _skills[id].CombatProfile
        .EffectDefinitions.Where(e => e.IsUnlockedAtSkillLevel(level)).ToArray();
    private static BattleUnitState[] Actors() => new[] { "a", "b", "c" }
        .Select((id, i) => BattleTestFixture.BuildUnit(id, "player",
            new Vector2I(1, i + 1), currentAp: 2, currentHp: 1000)).ToArray();
    private static BattleUnitState Target() => BattleTestFixture.BuildUnit("target", "enemy",
        new Vector2I(4, 2), currentAp: 2, currentHp: 1000);
}
