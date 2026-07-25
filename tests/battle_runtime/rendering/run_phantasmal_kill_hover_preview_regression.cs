using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_phantasmal_kill_hover_preview_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "test_phantasmal_kill_hover";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGroundHoverPreviewReportsFriendlyExecuteAndImmuneRisk();
            RequestTestExit(_test.Finish("Phantasmal Kill hover preview regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Phantasmal Kill hover preview regression"));
        }
    }

    private void TestGroundHoverPreviewReportsFriendlyExecuteAndImmuneRisk()
    {
        SkillDefinition skill = MakeGroundPhantasmalKillSkill();
        BattleUnitState caster = MakeUnit("preview_caster", "player", new Vector2I(0, 4), 200, 200);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 1);

        BattleUnitState weakEnemy = MakeUnit("weak_enemy", "enemy", new Vector2I(4, 4), 200, 40);
        BattleUnitState immuneEnemy = MakeUnit("immune_enemy", "enemy", new Vector2I(5, 4), 200, 40);
        immuneEnemy.AddSaveImmunityTagTyped("illusion");
        BattleUnitState weakAlly = MakeUnit("weak_ally", "player", new Vector2I(6, 4), 200, 45);
        BattleUnitState outsideEnemy = MakeUnit("outside_enemy", "enemy", new Vector2I(8, 4), 200, 40);

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "phantasmal_hover_preview",
            new Vector2I(9, 9),
            new[] { caster, weakAlly },
            new[] { weakEnemy, immuneEnemy, outsideEnemy }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);

        BattleCommand command = MakeGroundCommand(caster, new Vector2I(4, 4));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(
            preview != null && preview.allowed,
            $"Phantasmal Kill ground hover preview should be allowed. log={(preview != null ? string.Join(" | ", preview.LogLinesTyped) : "null")}"
        );
        _test.Eq(preview.TargetCoordsTyped.Count, 49, "Phantasmal Kill hover should expose a 7x7 ground area.");
        _test.True(preview.TargetUnitIdsTyped.Contains(weakEnemy.unit_id), "hover preview should include in-area enemy.");
        _test.True(preview.TargetUnitIdsTyped.Contains(weakAlly.unit_id), "hover preview should include in-area ally.");
        _test.True(preview.TargetUnitIdsTyped.Contains(immuneEnemy.unit_id), "hover preview should include immune no-op unit.");
        _test.False(preview.TargetUnitIdsTyped.Contains(outsideEnemy.unit_id), "hover preview should exclude units outside 7x7.");

        using GodotProjectionLease<GDictionary> branchLease =
            BattlePreviewProjection.BuildSaveBranchLease(preview.SaveBranchPreviewTyped);
        GDictionary branchPreview = branchLease.Value;
        _test.Eq(DictStringName(branchPreview, "kind"), new StringName("graded_save_execute"), "preview should expose graded-save branch kind.");
        _test.Eq(DictStringName(branchPreview, "profile_id"), new StringName("phantasmal_kill"), "preview should expose profile id.");
        _test.Eq(DictStringName(branchPreview, "save_tag"), new StringName("illusion"), "preview should expose save tag.");
        _test.Eq(DictStringName(branchPreview, "save_ability"), new StringName("willpower"), "preview should expose save ability.");
        _test.Eq(DictInt(branchPreview, "enemy_target_count"), 2, "preview should count enemy targets by team, including immune no-op targets.");
        _test.Eq(DictInt(branchPreview, "friendly_target_count"), 1, "preview should count friendly targets by team.");
        _test.Eq(DictInt(branchPreview, "friendly_affected_count"), 1, "preview should count affected non-immune allies.");
        _test.Eq(DictInt(branchPreview, "friendly_execute_risk_count"), 1, "preview should count allies inside execute thresholds.");
        _test.Eq(DictInt(branchPreview, "enemy_execute_risk_count"), 1, "preview should count enemies inside execute thresholds.");
        _test.Eq(DictInt(branchPreview, "immune_count"), 1, "preview should count immune no-op units.");
        _test.True(DictInt(branchPreview, "failure_execute_risk_count") >= 2, "preview should expose failure execute-risk target count.");
        _test.True(DictInt(branchPreview, "critical_failure_execute_risk_count") >= 2, "preview should expose critical-failure execute-risk target count.");
        _test.True(DictInt(branchPreview, "success_aftershock_expected_basis_points") > 0, "preview should expose success aftershock expected basis points.");

        branchPreview["friendly_affected_count"] = 99;
        using GodotProjectionLease<GDictionary> repeatedBranchLease =
            BattlePreviewProjection.BuildSaveBranchLease(preview.SaveBranchPreviewTyped);
        _test.Eq(
            DictInt(repeatedBranchLease.Value, "friendly_affected_count"),
            1,
            "BattlePreview should keep save_branch_preview as copied owner state."
        );

        preview.AddLogLine("POISON_LOG");
        preview.SetDamagePreview(
            new BattleDamagePreviewRangeService.SkillDamagePreview(
                true,
                999,
                999,
                new List<BattleDamagePreviewRangeService.DamageEffectRange>()
            )
        );

        var adapter = new BattleHudAdapter();
        BattleHoverSnapshot hover = adapter.BuildHoverPreview(
            fixture.State,
            new Vector2I(4, 4),
            skill.SkillId,
            "",
            new Godot.Collections.Array<Vector2I> { new Vector2I(4, 4) },
            preview
        );
        BattleHudSnapshot snapshot = adapter.BuildSnapshot(
            fixture.State,
            new Vector2I(4, 4),
            skill.SkillId,
            skill.DisplayName,
            "",
            new Godot.Collections.Array<Vector2I>(),
            1,
            new Godot.Collections.Array<StringName> { weakEnemy.unit_id, weakAlly.unit_id, immuneEnemy.unit_id },
            "",
            "遭遇",
            preview
        );

        string hoverText = hover.SaveBranchPreviewText;
        _test.True(hoverText.Contains("友军"), "hover text should warn about friendly-fire risk.");
        _test.True(hoverText.Contains("处决"), "hover text should mention execute risk.");
        _test.True(hoverText.Contains("免疫"), "hover text should mention immune/no-op targets.");
        _test.False(hoverText.Contains("POISON_LOG"), "hover text should come from structured summary_text, not log_lines.");
        _test.False(hover.DamageText.Contains("999"), "hover should suppress damage preview when save branch summary exists.");

        string snapshotText = snapshot.SelectedSkillSaveBranchPreviewText;
        _test.True(snapshotText.Contains("友军"), "HUD selected-skill text should warn about friendly-fire risk.");
        _test.True(snapshotText.Contains("免疫"), "HUD selected-skill text should mention immune/no-op targets.");

        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static SkillDefinition MakeGroundPhantasmalKillSkill()
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "graded_save_execute",
            effectTargetTeamFilter: "any",
            damageTag: "psychic",
            saveDcMode: "caster_spell",
            saveDc: 0,
            saveDcSourceAbility: "intelligence",
            saveAbility: "willpower",
            saveTag: "illusion",
            savePartialOnSuccess: false,
            parameters: new Dictionary<string, object>
            {
                ["profile_id"] = "phantasmal_kill",
                ["failure_execute_threshold_fixed"] = 50,
                ["failure_execute_threshold_max_hp_percent"] = 25,
                ["failure_damage_dice_count"] = 6,
                ["failure_damage_dice_sides"] = 6,
                ["failure_frightened_duration_tu"] = 60,
                ["failure_reaction_lock_duration_tu"] = 30,
                ["critical_failure_execute_threshold_max_hp_percent"] = 35,
                ["critical_failure_damage_dice_count"] = 10,
                ["critical_failure_damage_dice_sides"] = 6,
                ["critical_failure_frightened_duration_tu"] = 90,
                ["critical_failure_stunned_duration_tu"] = 30,
                ["success_aftershock_duration_tu"] = 30,
            }
        );
        return TestSkillDefinitionProjection.BuildSkill(
            SkillId,
            displayName: "Test Phantasmal Kill Hover",
            maxLevel: 9,
            nonCoreMaxLevel: 7,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                SkillId,
                effects: new[] { effect },
                targetMode: "ground",
                targetTeamFilter: "any",
                targetSelectionMode: "single_coord",
                rangeValue: 12,
                areaPattern: "square",
                areaValue: 3,
                apCost: 0,
                mpCost: 0,
                cooldownTu: 0
            )
        );
    }

    private static BattleUnitState MakeUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int maxHp,
        int currentHp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 3,
            currentHp: currentHp
        );
        unit.SetCurrentMp(2000);
        unit.SetCurrentStamina(20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 2000);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 3);
        unit.attribute_snapshot.SetValue("intelligence", 14);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 2);
        unit.attribute_snapshot.SetValue("spell_proficiency_bonus", 2);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        return unit;
    }

    private static BattleCommand MakeGroundCommand(BattleUnitState source, Vector2I targetCoord)
    {
        BattleCommand command = new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = source.unit_id,
            skill_id = SkillId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static string DictString(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? data[key].AsString() : "";
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new StringName("");
        Variant value = data[key];
        return value.VariantType == Variant.Type.StringName
            ? value.AsStringName()
            : new StringName(value.AsString());
    }

    private static int DictInt(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? data[key].AsInt32() : 0;
    }
}
