using System;
using System.Collections.Generic;
using Godot;

public partial class run_basic_attack_skill_role_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestRuntimeUnitFactoryUsesInjectedRole();
            TestMasteryMappingUsesInjectedRole();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected basic attack role exception: {exception}");
        }

        RequestTestExit(_test.Finish("Basic attack skill role regression"));
    }

    private void TestRuntimeUnitFactoryUsesInjectedRole()
    {
        StringName configuredAttackId = "configured_core_attack";
        StringName legacyLiteralId = "basic_attack";
        var skills = new Dictionary<StringName, SkillDefinition>
        {
            [configuredAttackId] = TestSkillDefinitionProjection.BuildSkill(configuredAttackId),
            [legacyLiteralId] = TestSkillDefinitionProjection.BuildSkill(legacyLiteralId),
        };
        var party = new PartyState();
        var member = new PartyMemberState
        {
            member_id = "role_probe",
            display_name = "Role Probe",
            progression = new UnitProgress(),
            equipment_state = new EquipmentState(),
        };
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        party.leader_member_id = member.member_id;

        using var runtime = new BattleRuntimeModule();
        runtime.setup(
            skill_definitions: skills,
            basic_attack_skill_id: configuredAttackId
        );
        IReadOnlyList<BattleUnitState> units = runtime._unit_factory.BuildAllyUnits(
            party,
            new Godot.Collections.Dictionary()
        );
        _test.Eq(units.Count, 1, "unit factory should build the configured role probe");
        if (units.Count == 1)
        {
            BattleUnitState unit = units[0];
            _test.True(
                unit.KnowsActiveSkill(configuredAttackId),
                "unit factory must grant the injected basic attack role"
            );
            _test.False(
                unit.KnowsActiveSkill(legacyLiteralId),
                "unit factory must not grant the legacy literal when another role is configured"
            );
            _test.Eq(
                unit.GetKnownSkillLevelTyped(configuredAttackId),
                0,
                "configured basic attack role should retain level-zero semantics"
            );
            BattleTestFixture.DisposeBattleUnit(unit);
        }
    }

    private void TestMasteryMappingUsesInjectedRole()
    {
        StringName configuredAttackId = "configured_core_attack";
        StringName legacyLiteralId = "basic_attack";
        using var service = new BattleSkillMasteryService();
        service.Setup(configuredAttackId);
        var unit = new BattleUnitState();
        unit.SetNaturalWeaponProjectionTyped(
            "test_blade",
            "physical_slash",
            1,
            null,
            "sword"
        );

        _test.Eq(
            service.ResolveMasteryRewardSkillId(unit, configuredAttackId),
            new StringName("sword_training"),
            "mastery service should map the injected role to weapon-family training"
        );
        _test.Eq(
            service.ResolveMasteryRewardSkillId(unit, legacyLiteralId),
            legacyLiteralId,
            "mastery service must not special-case an unconfigured legacy literal"
        );
        BattleTestFixture.DisposeBattleUnit(unit);
    }
}
