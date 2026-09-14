using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_skill_attack_defense_mode_schema_regression : LifecycleTestSceneTree
{
    private const string ArcaneMissilePath =
        "mage_arcane_missile";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypedValuesRoundTrip();
        TestInvalidBaseAndLevelOverrideModesAreRejected();
        TestLevelOverrideProjection();
        TestArcaneMissileForceHitSupersedesDefenseMode();

        RequestTestExit(_test.Finish("Skill attack defense mode schema regression"));
    }

    private void TestTypedValuesRoundTrip()
    {
        foreach (
            CombatSkillAttackDefenseMode mode in new[]
            {
                CombatSkillAttackDefenseMode.Normal,
                CombatSkillAttackDefenseMode.Touch,
                CombatSkillAttackDefenseMode.FlatFooted,
            }
        )
        {
            StringName id = CombatSkillContentRules.ToStringName(mode);
            _test.Eq(
                CombatSkillContentRules.ToAttackDefenseMode(id),
                mode,
                $"{id} attack defense mode should round-trip through the typed owner."
            );
        }
        _test.Eq(
            CombatSkillContentRules.ToAttackDefenseMode(""),
            CombatSkillAttackDefenseMode.Normal,
            "An omitted attack_defense_mode should preserve normal AC behavior."
        );
    }

    private void TestInvalidBaseAndLevelOverrideModesAreRejected()
    {
        using CombatSkillDef invalidBase = BuildProfile();
        invalidBase.attack_defense_mode = "reflex";
        string baseErrors = FormatErrors(Validate(invalidBase));
        _test.True(
            baseErrors.Contains("unsupported attack_defense_mode reflex"),
            $"Unknown base attack_defense_mode should fail schema validation. errors={baseErrors}"
        );

        using CombatSkillDef invalidOverride = BuildProfile();
        invalidOverride.level_overrides = new GDictionary
        {
            [2] = new GDictionary
            {
                ["attack_defense_mode"] = "reflex",
            },
        };
        string overrideErrors = FormatErrors(Validate(invalidOverride));
        _test.True(
            overrideErrors.Contains(
                "level override 2.attack_defense_mode uses unsupported value reflex"
            ),
            $"Unknown level attack_defense_mode should fail schema validation. errors={overrideErrors}"
        );
    }

    private void TestLevelOverrideProjection()
    {
        using CombatSkillDef profile = BuildProfile();
        profile.level_overrides = new GDictionary
        {
            [2] = new GDictionary
            {
                ["attack_defense_mode"] = "touch",
            },
            [4] = new GDictionary
            {
                ["attack_defense_mode"] = "flat_footed",
            },
        };
        using SkillDef skill = new()
        {
            skill_id = "attack_defense_projection_probe",
            combat_profile = profile,
        };
        SkillDefinition definition = SkillDefinition.FromDiagnosticFixture(skill);

        _test.Eq(
            definition.CombatProfile.GetEffectiveAttackDefenseMode(1),
            CombatSkillAttackDefenseMode.Normal,
            "The projected base mode should remain normal."
        );
        _test.Eq(
            definition.CombatProfile.GetEffectiveAttackDefenseMode(2),
            CombatSkillAttackDefenseMode.Touch,
            "The projected level-2 override should select touch AC."
        );
        _test.Eq(
            definition.CombatProfile.GetEffectiveAttackDefenseMode(4),
            CombatSkillAttackDefenseMode.FlatFooted,
            "The projected level-4 override should select flat-footed AC."
        );
    }

    private void TestArcaneMissileForceHitSupersedesDefenseMode()
    {
        SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
            ArcaneMissilePath,
            "attack_defense_mode_schema_arcane_missile"
        );
        _test.True(skill?.CombatProfile != null, "Arcane Missile should load as typed content.");
        if (skill?.CombatProfile == null)
            return;

        _test.Eq(
            skill.CombatProfile.AttackDefenseModeKind,
            CombatSkillAttackDefenseMode.Normal,
            "A forced-hit Arcane Missile should not advertise an alternate AC mode."
        );
        _test.Eq(
            skill.CombatProfile.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.ForceHitNoCrit,
            "Arcane Missile should explicitly force a hit without allowing critical hits."
        );
        _test.True(
            skill.Description.Contains("必定命中")
                && skill.Description.Contains("不能暴击"),
            $"Arcane Missile description should disclose forced-hit/no-crit behavior. description={skill.Description}"
        );
        _test.True(
            skill.LevelDescriptionTemplate.Contains("必定命中")
                && skill.LevelDescriptionTemplate.Contains("不能暴击"),
            "Arcane Missile level text should identify forced-hit/no-crit resolution."
        );
    }

    private static CombatSkillDef BuildProfile() => new()
    {
        skill_id = "attack_defense_schema_probe",
        projectile_kind = "none",
        attack_defense_mode = "normal",
    };

    private static GStringArray Validate(CombatSkillDef profile)
    {
        var errors = new GStringArray();
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        validator.AppendCombatProfileValidationErrors(
            errors,
            "attack_defense_schema_probe",
            profile
        );
        return errors;
    }

    private static string FormatErrors(GStringArray errors) =>
        string.Join(" | ", errors ?? new GStringArray());
}
