using System.Collections.Generic;
using Godot;

public partial class run_battle_on_kill_gain_resources_regression : SceneTree
{
    private static readonly StringName SkillId = "mage_death_reap";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDeathReapTypedDefinitionRestoresResourcesOnKill();
        Quit(_test.Finish("Battle on-kill gain resources regression"));
    }

    private void TestDeathReapTypedDefinitionRestoresResourcesOnKill()
    {
        SkillDefinition skillDefinition = LoadDeathReapDefinition();
        BattleUnitState caster = BuildUnit(
            "death_reap_caster",
            "player",
            new Vector2I(0, 0),
            currentHp: 80,
            currentAp: 2,
            currentMovePoints: 1,
            currentMp: 150
        );
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 1);
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));

        BattleUnitState target = BuildUnit(
            "death_reap_target",
            "enemy",
            new Vector2I(1, 0),
            currentHp: 5,
            currentAp: 1,
            currentMovePoints: 1,
            currentMp: 0
        );

        BattleCommand command = null;
        BattleEventBatch batch = null;
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "death_reap_typed_definition",
            new Vector2I(4, 2),
            new[] { caster },
            new[] { target }
        );

        try
        {
            fixture.Runtime.setup(
                skill_definitions: new Dictionary<StringName, SkillDefinition>
                {
                    [SkillId] = skillDefinition,
                }
            );
            fixture.Runtime.SetupStateForTests(fixture.State);
            BattleTestFixture.ConfigureDamageResolverForTests(
                fixture.Runtime,
                new FixedHitMaxDamageResolver()
            );
            BattleTestFixture.ConfigureHitResolverForTests(fixture.Runtime, new FixedHitResolver(10));

            command = new BattleCommand
            {
                command_type = "skill",
                unit_id = caster.unit_id,
                skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
                skill_id = SkillId,
                target_unit_id = target.unit_id,
                target_coord = target.coord,
            };
            command.AddTargetUnitId(target.unit_id);

            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                preview?.allowed == true,
                $"死亡收割 preview 应允许执行。logs={JoinLogs(preview?.LogLinesTyped)}"
            );
            batch = fixture.Runtime.IssueCommand(command);
            string issueLogs = JoinLogs(batch?.LogLinesTyped);

            _test.False(
                target.is_alive,
                $"死亡收割应击杀低生命目标。target_hp={target.current_hp} logs={issueLogs}"
            );
            _test.Eq(
                caster.current_ap,
                1,
                $"死亡收割击杀后应在 2 AP 成本后返还 1 AP。ap={caster.current_ap} logs={issueLogs}"
            );
            _test.Eq(
                caster.current_move_points,
                3,
                $"死亡收割击杀后应返还 2 点免费移动力。move={caster.current_move_points} logs={issueLogs}"
            );
            _test.True(
                caster.can_use_locked_move_points_this_turn,
                $"死亡收割击杀后应允许本回合行动后移动。logs={issueLogs}"
            );
            _test.True(
                batch != null && batch.ContainsChangedUnitId(caster.unit_id),
                $"死亡收割返还资源应标记施法者为 changed unit。logs={issueLogs}"
            );
        }
        finally
        {
            GodotSharpCleanup.DisposeBatch(batch);
            GodotSharpCleanup.ClearRuntimeReferences(command);
        }
    }

    private static SkillDefinition LoadDeathReapDefinition()
    {
        const string resourcePath = "res://data/configs/skills/mage_death_reap.tres";
        return TestSkillDefinitionProjection.LoadSkillDefinition(resourcePath, resourcePath);
    }

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentHp,
        int currentAp,
        int currentMovePoints,
        int currentMp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: currentAp,
            currentHp: currentHp
        );
        unit.SetCurrentMovePoints(currentMovePoints);
        unit.SetCurrentMp(currentMp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), currentHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 200);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        return unit;
    }
}
