#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Godot;

public partial class run_skill_generation_stage3_admission_regression
    : LifecycleTestSceneTree
{
    private static readonly IReadOnlyList<AdmissionCase> Cases = Array.AsReadOnly(
        new[]
        {
            new AdmissionCase(
                "mage_generated_force_needle",
                "res://tests/fixtures/skill_generation/stage3_batch/accepted_force_needle/generated_stage3_force_needle.json",
                "res://data/configs/json/skills/generated_stage3_force_needle.json",
                "FAAB6BABB122123FDF76915C76BBE5B98C0B2D8B8DCB44611E9E1CACD9A5FB9E"
            ),
            new AdmissionCase(
                "mage_generated_ember_orb",
                "res://tests/fixtures/skill_generation/stage3_batch/accepted_ember_orb/generated_stage3_ember_orb.json",
                "res://data/configs/json/skills/generated_stage3_ember_orb.json",
                "0938DE70A557734C475CF5CF3B8D3CC8054337CC74691384F09FE7D2072DF651"
            ),
            new AdmissionCase(
                "mage_generated_reserve_comet",
                "res://tests/fixtures/skill_generation/stage3_batch/expected_accept_reserve_comet/generated_stage3_reserve_comet.json",
                "res://data/configs/json/skills/generated_stage3_reserve_comet.json",
                "2E055356BFAB3DB49E16512AE0F665D944C73197ED0E2BA1FE520DD371061253"
            ),
        }
    );

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunAssertions);
    }

    private void RunAssertions()
    {
        try
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            foreach (AdmissionCase admissionCase in Cases)
            {
                byte[] sourceBytes = ReadBytes(admissionCase.SourcePath);
                byte[] productionBytes = ReadBytes(admissionCase.ProductionPath);
                _test.True(
                    sourceBytes.SequenceEqual(productionBytes),
                    $"{admissionCase.SkillId} production JSON should be the unmodified accepted source bytes"
                );
                _test.Eq(
                    Convert.ToHexString(SHA256.HashData(productionBytes)),
                    admissionCase.ExpectedSha256,
                    $"{admissionCase.SkillId} admitted JSON hash should remain stable"
                );
                if (
                    !snapshot.Skills.TryGetValue(
                        admissionCase.SkillId,
                        out SkillDefinition? skill
                    )
                )
                {
                    _test.Fail(
                        $"{admissionCase.SkillId} should load from the production skill catalog"
                    );
                    continue;
                }
                _test.True(
                    skill.SkillTypeKind == SkillTypeKind.Active
                    && skill.LearnSource == (StringName)"internal"
                    && skill.CombatProfile != null,
                    $"{admissionCase.SkillId} should remain an internal active spell"
                );
                CombatSkillDefinition combat = skill.CombatProfile!;
                CombatSkillResourceCosts costs =
                    combat.GetEffectiveResourceCostValues(1);
                _test.True(
                    costs.MpCost > 0 || combat.GetEffectiveMpCostPerTargetSlot(1) > 0,
                    $"{admissionCase.SkillId} should consume MP"
                );
                _test.True(
                    combat.ProjectileKindTyped == CombatProjectileKind.Magical
                    && combat.DeliveryCategories.Contains((StringName)"spell"),
                    $"{admissionCase.SkillId} should use the magical spell delivery path"
                );
                _test.True(
                    combat.EffectDefinitions.Any(effect =>
                        effect?.EffectKind == BattleEffectKind.Damage
                        && effect.DiceCount > 0
                        && effect.DiceSides > 0
                        && !effect.AddWeaponDice
                    ),
                    $"{admissionCase.SkillId} should use spell damage dice, not weapon dice"
                );
            }
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected stage 3 skill admission exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Stage 3 generated-skill admission regression"));
    }

    private static byte[] ReadBytes(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new InvalidOperationException(
                $"Could not open {path}: {FileAccess.GetOpenError()}."
            );
        }
        long length = (long)file.GetLength();
        byte[] bytes = file.GetBuffer(length);
        if (bytes.LongLength != length)
            throw new InvalidOperationException($"Could not read all bytes from {path}.");
        return bytes;
    }

    private sealed record AdmissionCase(
        StringName SkillId,
        string SourcePath,
        string ProductionPath,
        string ExpectedSha256
    );
}
