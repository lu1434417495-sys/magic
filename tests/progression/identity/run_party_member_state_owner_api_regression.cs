using Godot;

public partial class run_party_member_state_owner_api_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestVitalsApiOwnsDeathState();
        TestIdentityAndAgeApiNormalizeValues();
        TestBodySizeApiKeepsProjectionConsistent();
        TestBloodlineAndAscensionApiOwnRelatedFields();

        Quit(_test.Finish("Party member state owner API regression"));
    }

    private void TestVitalsApiOwnsDeathState()
    {
        PartyMemberState member = new();

        member.SetVitals(-1, -2, -3);
        _test.Eq(member.GetCurrentHp(), 0, "SetVitals 应 clamp HP。");
        _test.Eq(member.GetCurrentMp(), 0, "SetVitals 应 clamp MP。");
        _test.Eq(member.GetCurrentAura(), 0, "SetVitals 应 clamp aura。");
        _test.True(member.IsDead(), "HP 为 0 时人物长期状态应同步 is_dead=true。");

        member.ReviveWithVitals(0, 5, 2);
        _test.Eq(member.GetCurrentHp(), 1, "ReviveWithVitals 应保证 HP 至少为 1。");
        _test.False(member.IsDead(), "ReviveWithVitals 应同步 is_dead=false。");

        member.SetVitals(30, 20, 10);
        member.ClampVitals(12, 8, 3);
        _test.Eq(member.GetCurrentHp(), 12, "ClampVitals 应 clamp HP。");
        _test.Eq(member.GetCurrentMp(), 8, "ClampVitals 应 clamp MP。");
        _test.Eq(member.GetCurrentAura(), 3, "ClampVitals 应 clamp aura。");

        member.MarkDead();
        _test.Eq(member.GetCurrentHp(), 0, "MarkDead 应清空 HP。");
        _test.Eq(member.GetCurrentMp(), 0, "MarkDead 应清空 MP。");
        _test.Eq(member.GetCurrentAura(), 0, "MarkDead 应清空 aura。");
        _test.True(member.IsDead(), "MarkDead 应同步 is_dead=true。");
    }

    private void TestIdentityAndAgeApiNormalizeValues()
    {
        PartyMemberState member = new();
        member.SetIdentity("elf", "moon_elf");
        _test.Eq(member.race_id, new StringName("elf"), "SetIdentity 应写 race_id。");
        _test.Eq(member.subrace_id, new StringName("moon_elf"), "SetIdentity 应写 subrace_id。");

        member.SetAgeProjection(-10, -20, -30, -40);
        _test.Eq(member.age_years, 0, "SetAgeProjection 应 clamp age_years。");
        _test.Eq(member.biological_age_years, 0, "SetAgeProjection 应 clamp biological_age_years。");
        _test.Eq(member.astral_memory_years, 0, "SetAgeProjection 应 clamp astral_memory_years。");
        _test.Eq(member.birth_at_world_step, 0, "SetAgeProjection 应 clamp birth_at_world_step。");
    }

    private void TestBodySizeApiKeepsProjectionConsistent()
    {
        PartyMemberState member = new();
        _test.True(member.SetBodySizeCategory("large"), "SetBodySizeCategory 应接受合法 category。");
        _test.Eq(member.body_size_category, new StringName("large"), "SetBodySizeCategory 应写 category。");
        _test.Eq(member.body_size, BodySizeContentRules.GetBodySizeForCategory("large"), "SetBodySizeCategory 应同步 body_size。");

        int previousBodySize = member.body_size;
        StringName previousCategory = member.body_size_category;
        _test.False(member.SetBodySizeCategory("missing"), "SetBodySizeCategory 应拒绝非法 category。");
        _test.Eq(member.body_size, previousBodySize, "非法 category 不应改 body_size。");
        _test.Eq(member.body_size_category, previousCategory, "非法 category 不应改 body_size_category。");
    }

    private void TestBloodlineAndAscensionApiOwnRelatedFields()
    {
        PartyMemberState member = new();
        member.SetBloodline("titan", "awakened");
        _test.Eq(member.bloodline_id, new StringName("titan"), "SetBloodline 应写 bloodline_id。");
        _test.Eq(member.bloodline_stage_id, new StringName("awakened"), "SetBloodline 应写 bloodline_stage_id。");

        member.ClearBloodline();
        _test.Eq(member.bloodline_id, new StringName(""), "ClearBloodline 应清空 bloodline_id。");
        _test.Eq(member.bloodline_stage_id, new StringName(""), "ClearBloodline 应清空 bloodline_stage_id。");

        member.SetAscension("dragon_ascension", "stage_1", 12, "human");
        _test.Eq(member.ascension_id, new StringName("dragon_ascension"), "SetAscension 应写 ascension_id。");
        _test.Eq(member.ascension_stage_id, new StringName("stage_1"), "SetAscension 应写 ascension_stage_id。");
        _test.Eq(member.ascension_started_at_world_step, 12, "SetAscension 应写开始 world step。");
        _test.Eq(member.original_race_id_before_ascension, new StringName("human"), "SetAscension 应写原 race。");

        member.ClearAscension();
        _test.Eq(member.ascension_id, new StringName(""), "ClearAscension 应清空 ascension_id。");
        _test.Eq(member.ascension_stage_id, new StringName(""), "ClearAscension 应清空 ascension_stage_id。");
        _test.Eq(member.ascension_started_at_world_step, -1, "ClearAscension 应恢复未开始 step。");
        _test.Eq(member.original_race_id_before_ascension, new StringName(""), "ClearAscension 应清空原 race。");
    }
}
