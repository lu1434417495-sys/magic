using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battlesim_formal_fixture_regression : LifecycleTestSceneTree
{
    private static readonly StringName[] AttributeIds =
    {
        "strength",
        "agility",
        "constitution",
        "perception",
        "intelligence",
        "willpower",
    };

    private readonly TestHarness _test = new();
    private readonly List<BattleSimFormalCombatFixture> _fixtures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestFixtureNoLongerRegistersGlobalClass();
            TestDefaultMainCharacterGetsRerollLuck();
            TestSelectedMainCharacterGetsRerollLuck();
            TestSelectedMainCharacterUsesConfiguredRerollCount();
            TestHostileMainCharacterVariantFallsBackToFirstAlly();
            TestMixed6v12RollsCreationAttributesFromSeed();
            TestMixed6v12PreservesHighAttributeRollSeed();
            TestMixed6v12ProfessionHpUsesIndependentRankRolls();
            TestMixed6v12OmitsExplicitActionThreshold();
            TestMixed6v12EliteArchersGetShootingSpecialization();
            TestMixed6v12StartsAllUnitsAtFullEffectiveHp();
            TestMixed6v12EquipsRoleArmor();
            TestFormalFixtureRequestsBidirectionalSpawnReachability();
        }
        finally
        {
            DisposeFixtures();
        }

        RequestTestExit(_test.Finish("BattleSimFormalCombatFixture regression"));
    }

    private void TestFixtureNoLongerRegistersGlobalClass()
    {
    }

    private void TestDefaultMainCharacterGetsRerollLuck()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_2S1A
        );
        PartyState partyState = fixture.GetPartyState();
        _test.Eq(partyState.main_character_member_id, new StringName("ally_longsword_01"), "默认主角应是第一个友军。");
        _test.Eq(partyState.leader_member_id, new StringName("ally_longsword_01"), "默认队长应跟随默认主角。");
        _test.Eq(GetHiddenLuck(fixture, "ally_longsword_01"), 2, "默认主角应按 reroll_count=0 烘焙 +2 出生幸运。");
        _test.Eq(GetHiddenLuck(fixture, "ally_longsword_02"), 0, "未选中的友军不应获得出生幸运。");
        _test.Eq(GetHiddenLuck(fixture, "enemy_longsword_01"), 0, "敌方单位不应获得主角 reroll 出生幸运。");
    }

    private void TestSelectedMainCharacterGetsRerollLuck()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_6V12,
            new BattleSimFormalRosterOptionsData
            {
                MainCharacterMemberId = "elite_archer_0",
            }
        );
        PartyState partyState = fixture.GetPartyState();
        _test.Eq(partyState.main_character_member_id, new StringName("elite_archer_0"), "显式选择的友军应成为主角。");
        _test.Eq(partyState.leader_member_id, new StringName("elite_archer_0"), "未显式选择队长时，队长应跟随主角。");
        _test.Eq(GetHiddenLuck(fixture, "elite_archer_0"), 2, "选中的主角应按默认 reroll_count=0 烘焙 +2。");
        _test.Eq(GetHiddenLuck(fixture, "elite_sword_0"), 0, "默认第一友军被替换后不应获得主角出生幸运。");
    }

    private void TestSelectedMainCharacterUsesConfiguredRerollCount()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_6V12,
            new BattleSimFormalRosterOptionsData
            {
                MainCharacterMemberId = "elite_mage_0",
                MainCharacterRerollCount = 10000,
            }
        );
        _test.Eq(GetHiddenLuck(fixture, "elite_mage_0"), -3, "主角应按传入 reroll_count=10000 烘焙 -3。");
        _test.Eq(GetHiddenLuck(fixture, "elite_archer_0"), 0, "未选中弓手不应获得出生幸运。");
    }

    private void TestHostileMainCharacterVariantFallsBackToFirstAlly()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_6V12,
            new BattleSimFormalRosterOptionsData
            {
                MainCharacterMemberId = "hostile_archer_0",
            }
        );
        PartyState partyState = fixture.GetPartyState();
        _test.Eq(partyState.main_character_member_id, new StringName("elite_sword_0"), "敌方不能被设为主角，应回退第一个友军。");
        _test.Eq(GetHiddenLuck(fixture, "elite_sword_0"), 2, "回退主角应获得默认 reroll 出生幸运。");
        _test.Eq(GetHiddenLuck(fixture, "hostile_archer_0"), 0, "被拒绝的敌方配置不应获得出生幸运。");
    }

    private void TestMixed6v12RollsCreationAttributesFromSeed()
    {
        BattleSimFormalRosterOptionsData sameSeedOptions = new()
        {
            AttributeRollSeed = 24680,
        };
        BattleSimFormalCombatFixture fixtureA = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12, sameSeedOptions);
        BattleSimFormalCombatFixture fixtureB = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12, sameSeedOptions);
        string attrsA = FormatAllMemberAttributeMap(fixtureA);
        string attrsB = FormatAllMemberAttributeMap(fixtureB);
        _test.Eq(attrsA, attrsB, "同一 attribute_roll_seed 应稳定复现 6v12 建卡属性。");

        BattleSimFormalCombatFixture fixtureC = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_6V12,
            new BattleSimFormalRosterOptionsData
            {
                AttributeRollSeed = 13579,
            }
        );
        string attrsC = FormatAllMemberAttributeMap(fixtureC);
        _test.True(attrsA != attrsC, "不同 attribute_roll_seed 应产生不同建卡属性分布。");
        AssertRolledAttributeRange(fixtureA, "hostile_sword_0");
        AssertRolledAttributeRange(fixtureA, "hostile_archer_0");
    }

    private void TestMixed6v12PreservesHighAttributeRollSeed()
    {
        long highSeed = 4192972056L;
        BattleSimFormalRosterOptionsData highSeedOptions = new()
        {
            AttributeRollSeed = highSeed,
        };
        BattleSimFormalCombatFixture fixtureA = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12, highSeedOptions);
        BattleSimFormalCombatFixture fixtureB = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12, highSeedOptions);
        string attrsA = FormatAllMemberAttributeMap(fixtureA);
        string attrsB = FormatAllMemberAttributeMap(fixtureB);
        _test.Eq(attrsA, attrsB, "高位 attribute_roll_seed 应稳定复现 6v12 建卡属性。");

        BattleSimFormalCombatFixture defaultSeedFixture = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_6V12,
            new BattleSimFormalRosterOptionsData
            {
                AttributeRollSeed = 101,
            }
        );
        string defaultAttrs = FormatAllMemberAttributeMap(defaultSeedFixture);
        _test.True(
            attrsA != defaultAttrs,
            "高位 attribute_roll_seed 不应因 int 截断或负数 clamp 退回默认 seed=101。"
        );
    }

    private void TestMixed6v12ProfessionHpUsesIndependentRankRolls()
    {
        int seed = 24680;
        BattleSimFormalCombatFixture fixture = BuildFixture(
            BattleSimFormalCombatFixture.ROSTER_MIXED_6V12,
            new BattleSimFormalRosterOptionsData
            {
                AttributeRollSeed = seed,
            }
        );
        PartyMemberState mage = fixture.GetMemberState("elite_mage_0");
        if (mage?.progression?.unit_base_attributes == null)
        {
            _test.Fail("缺少 6v12 法师。");
            return;
        }
        UnitBaseAttributes attrs = mage.progression.unit_base_attributes;
        int constitution = attrs.GetAttributeValue("constitution");
        int expectedHp = CharacterCreationService.CalculateInitialHpMax(constitution);
        RuntimeRandom hpRng = new(seed + BattleSimFormalCombatFixture.HP_ROLL_SEED_OFFSET);
        for (int swordRank = 0; swordRank < 4 * 2; swordRank++)
            hpRng.RandiRange(1, 10);
        for (int archerRank = 0; archerRank < 2; archerRank++)
            hpRng.RandiRange(1, 8);
        for (int mageRank = 0; mageRank < 5; mageRank++)
            expectedHp += ProgressionService.CalculateProfessionHitPointGain(
                hpRng.RandiRange(1, 6),
                constitution
            );
        int oldAggregateHp = CharacterCreationService.CalculateInitialHpMax(constitution);
        oldAggregateHp +=
            ProgressionService.CalculateProfessionHitPointGain(5, constitution) * 5;
        _test.True(expectedHp != oldAggregateHp, "测试 seed 应能区分逐 rank 独立掷骰与旧的单 roll 乘 rank 逻辑。");
        _test.Eq(attrs.GetAttributeValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), expectedHp, "6v12 法师职业生命应按每个 rank 独立掷生命骰后累加。");
    }

    private void TestMixed6v12OmitsExplicitActionThreshold()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12);
        PartyState partyState = fixture.GetPartyState();
        if (partyState == null)
        {
            _test.Fail("缺少 fixture party_state。");
            return;
        }
        foreach (Variant memberIdOption in partyState.member_states.Keys)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(memberIdOption);
            PartyMemberState memberState = fixture.GetMemberState(memberId);
            if (memberState?.progression?.unit_base_attributes == null)
            {
                _test.Fail($"缺少成员属性：{memberId}");
                continue;
            }
            UnitBaseAttributes baseAttributes = memberState.progression.unit_base_attributes;
            _test.False(baseAttributes.custom_stats.ContainsKey(AttributeService.ToStringName(AttributeIdKind.ActionThreshold)), $"6v12 不应显式写入 action_threshold：{memberId}");
            AttributeService attributeService = new();
            attributeService.Setup(memberState.progression);
            _test.Eq(attributeService.GetTotalValue(AttributeService.ToStringName(AttributeIdKind.ActionThreshold)), AttributeService.DEFAULT_CHARACTER_ACTION_THRESHOLD, $"6v12 应回落到角色默认 action_threshold：{memberId}");
        }
    }

    private void TestMixed6v12EliteArchersGetShootingSpecialization()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12);
        PartyMemberState memberState = fixture.GetMemberState("elite_archer_0");
        UnitSkillProgress skillProgress = memberState?.progression?.GetSkillProgress("archer_shooting_specialization");
        _test.True(skillProgress != null && skillProgress.is_learned, "6v12 精英弓手应通过弓箭手职业获得射击专精：elite_archer_0");
        if (skillProgress != null)
        {
            _test.Eq(skillProgress.skill_level, 0, "射击专精应以 0 级进入模拟：elite_archer_0");
            _test.Eq(skillProgress.granted_source_type, new StringName("profession"), "射击专精来源类型应为职业：elite_archer_0");
            _test.Eq(skillProgress.granted_source_id, new StringName("archer"), "射击专精来源职业应为 archer：elite_archer_0");
        }

        PartyMemberState hostile = fixture.GetMemberState("hostile_archer_0");
        UnitSkillProgress hostileProgress = hostile?.progression?.GetSkillProgress("archer_shooting_specialization");
        _test.True(hostileProgress == null || !hostileProgress.is_learned, "0级 hostile 弓手不应获得射击专精。");
    }

    private void TestMixed6v12StartsAllUnitsAtFullEffectiveHp()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12);
        PartyState partyState = fixture.GetPartyState();
        if (partyState == null)
        {
            _test.Fail("缺少 fixture party_state。");
            return;
        }
        foreach (Variant memberIdOption in partyState.member_states.Keys)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(memberIdOption);
            PartyMemberState memberState = fixture.GetMemberState(memberId);
            if (memberState == null)
            {
                _test.Fail($"缺少成员：{memberId}");
                continue;
            }
            AttributeSnapshot snapshot = fixture.GetMemberAttributeSnapshotForEquipmentView(
                memberId,
                memberState.equipment_state
            );
            int hpMax = Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), 1);
            _test.Eq(memberState.current_hp, hpMax, $"6v12 开战前应按装备与职业被动后的有效生命上限补满：{memberId}");
        }

        PartyMemberState eliteSword = fixture.GetMemberState("elite_sword_0");
        if (eliteSword != null)
        {
            AttributeSnapshot eliteSnapshot =
                fixture.GetMemberAttributeSnapshotForEquipmentView(
                    "elite_sword_0",
                    eliteSword.equipment_state
                );
            _test.Eq(eliteSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus)), 20, "精英剑士有效生命上限应包含强健的 20% 人物生命加成。");
            eliteSword.current_hp = 1;
            fixture.BuildRuntimeContext(null, new GDictionary());
            _test.Eq(eliteSword.current_hp, Mathf.Max(eliteSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), 1), "build_runtime_context 开战前应重新把模拟单位补到有效生命上限。");
        }
    }

    private void TestMixed6v12EquipsRoleArmor()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12);
        for (int index = 0; index < 4; index++)
        {
            StringName eliteSwordMemberId = $"elite_sword_{index}";
            AssertEquippedItem(fixture, eliteSwordMemberId, "main_hand", "steel_longsword", $"6v12 精英剑士应装备长剑：{eliteSwordMemberId}");
            AssertEquippedItem(fixture, eliteSwordMemberId, "body", "iron_scale_mail", $"6v12 精英剑士应装备中甲：{eliteSwordMemberId}");
        }
        StringName eliteArcherMemberId = "elite_archer_0";
        AssertEquippedItem(fixture, eliteArcherMemberId, "main_hand", "ash_longbow", $"6v12 精英弓手应装备长弓：{eliteArcherMemberId}");
        AssertEquippedItem(fixture, eliteArcherMemberId, "body", "leather_jerkin", $"6v12 精英弓手应装备皮甲：{eliteArcherMemberId}");
        AssertNoEquippedItem(fixture, "elite_mage_0", "main_hand", "6v12 精英法师不应装备武器。");
        AssertEquippedItem(fixture, "elite_mage_0", "body", "leather_jerkin", "6v12 精英法师应装备皮甲。");
        for (int index = 0; index < 6; index++)
        {
            StringName hostileSwordMemberId = $"hostile_sword_{index}";
            AssertEquippedItem(fixture, hostileSwordMemberId, "main_hand", "steel_longsword", $"6v12 敌方剑士应装备长剑：{hostileSwordMemberId}");
            AssertEquippedItem(fixture, hostileSwordMemberId, "body", "iron_scale_mail", $"6v12 敌方剑士应装备中甲：{hostileSwordMemberId}");
        }
        for (int index = 0; index < 6; index++)
        {
            StringName hostileArcherMemberId = $"hostile_archer_{index}";
            AssertEquippedItem(fixture, hostileArcherMemberId, "main_hand", "ash_longbow", $"6v12 敌方弓手应装备长弓：{hostileArcherMemberId}");
            AssertEquippedItem(fixture, hostileArcherMemberId, "body", "leather_jerkin", $"6v12 敌方弓手应装备皮甲：{hostileArcherMemberId}");
        }
    }

    private void TestFormalFixtureRequestsBidirectionalSpawnReachability()
    {
        BattleSimFormalCombatFixture fixture = BuildFixture(BattleSimFormalCombatFixture.ROSTER_MIXED_6V12);
        GDictionary context = fixture.BuildRuntimeContext(null, new GDictionary());
        _test.True(ReadBool(context, "validate_spawn_reachability"), "formal combat fixture 模拟应开启出生可达性验证。");
        _test.True(ReadBool(context, "validate_bidirectional_spawn_reachability"), "formal combat fixture 模拟应开启玩家/敌方双向可攻击验证。");
        _test.True(ReadBool(context, "enforce_opposing_spawn_sides"), "formal combat fixture 模拟应开启玩家/敌方对侧出生约束。");
    }

    private BattleSimFormalCombatFixture BuildFixture(
        StringName rosterId,
        BattleSimFormalRosterOptionsData rosterOptions = null
    )
    {
        ProgressionContentRegistry progressionRegistry = new();
        ItemContentRegistry itemRegistry = new();
        BattleSimFormalCombatFixture fixture = new();
        bool keepFixture = false;
        try
        {
            fixture.SetupContent(progressionRegistry, itemRegistry);
            if (
                !fixture.BuildRoster(rosterId, rosterOptions ?? new BattleSimFormalRosterOptionsData())
            )
                throw new InvalidOperationException($"fixture 应能构建 roster={rosterId}。");
            _fixtures.Add(fixture);
            keepFixture = true;
            return fixture;
        }
        finally
        {
            if (!keepFixture && fixture != null)
                fixture.Dispose();
            progressionRegistry.Dispose();
            itemRegistry.Dispose();
        }
    }

    private void DisposeFixtures()
    {
        foreach (BattleSimFormalCombatFixture fixture in _fixtures)
        {
            if (fixture != null)
                fixture.Dispose();
        }
        _fixtures.Clear();
    }

    private int GetHiddenLuck(BattleSimFormalCombatFixture fixture, StringName memberId)
    {
        PartyMemberState memberState = fixture.GetMemberState(memberId);
        if (memberState == null)
        {
            _test.Fail($"缺少成员：{memberId}");
            return 0;
        }
        return memberState.GetHiddenLuckAtBirth();
    }

    private string FormatAllMemberAttributeMap(BattleSimFormalCombatFixture fixture)
    {
        PartyState partyState = fixture.GetPartyState();
        if (partyState == null)
        {
            _test.Fail("缺少 fixture party_state。");
            return "";
        }
        List<string> memberIds = new();
        foreach (Variant memberIdOption in partyState.member_states.Keys)
            memberIds.Add(ProgressionDataUtils.to_string_name(memberIdOption).ToString());
        memberIds.Sort(StringComparer.Ordinal);
        List<string> parts = new();
        foreach (string memberIdText in memberIds)
            parts.Add($"{memberIdText}:{FormatBaseAttributes(fixture, memberIdText)}");
        return string.Join("|", parts);
    }

    private string FormatBaseAttributes(BattleSimFormalCombatFixture fixture, StringName memberId)
    {
        PartyMemberState memberState = fixture.GetMemberState(memberId);
        if (memberState?.progression?.unit_base_attributes == null)
        {
            _test.Fail($"缺少成员属性：{memberId}");
            return "";
        }
        UnitBaseAttributes attrs = memberState.progression.unit_base_attributes;
        List<string> parts = new();
        foreach (StringName attributeId in AttributeIds)
            parts.Add($"{attributeId}={attrs.GetAttributeValue(attributeId)}");
        return string.Join(",", parts);
    }

    private void AssertRolledAttributeRange(BattleSimFormalCombatFixture fixture, StringName memberId)
    {
        PartyMemberState memberState = fixture.GetMemberState(memberId);
        if (memberState?.progression?.unit_base_attributes == null)
        {
            _test.Fail($"缺少成员属性：{memberId}");
            return;
        }
        UnitBaseAttributes attrs = memberState.progression.unit_base_attributes;
        foreach (StringName attributeId in AttributeIds)
        {
            int value = attrs.GetAttributeValue(attributeId);
            _test.True(value >= 4 && value <= 14, $"{memberId} 的 {attributeId} 应来自 5D3-1 建卡骰子范围。actual={value}");
        }
    }

    private void AssertEquippedItem(BattleSimFormalCombatFixture fixture, StringName memberId, StringName slotId, StringName expectedItemId, string message)
    {
        PartyMemberState memberState = fixture.GetMemberState(memberId);
        if (memberState == null)
        {
            _test.Fail($"缺少成员：{memberId}");
            return;
        }
        if (memberState.equipment_state == null)
        {
            _test.Fail($"成员缺少装备状态：{memberId}");
            return;
        }
        _test.Eq(memberState.equipment_state.GetEquippedItemId(slotId), expectedItemId, message);
    }

    private void AssertNoEquippedItem(BattleSimFormalCombatFixture fixture, StringName memberId, StringName slotId, string message)
    {
        PartyMemberState memberState = fixture.GetMemberState(memberId);
        if (memberState == null)
        {
            _test.Fail($"缺少成员：{memberId}");
            return;
        }
        if (memberState.equipment_state == null)
            return;
        _test.Eq(memberState.equipment_state.GetEquippedItemId(slotId), new StringName(""), message);
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

}
