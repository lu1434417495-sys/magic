using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class run_character_management_weapon_projection_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestWeaponPhysicalDamageTagUsesTypedEquipmentState();
        TestWeaponPhysicalDamageTagRejectsBadMainHandStates();

        if (_failures.Count == 0)
        {
            GD.Print("Character management weapon projection regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Character management weapon projection regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestWeaponPhysicalDamageTagUsesTypedEquipmentState()
    {
        PartyState party = BuildPartyWithMember("hero");
        ItemDef spear = MakeWeapon("test_spear", ItemDef.DAMAGE_TAG_PHYSICAL_PIERCE());
        CharacterManagementModule manager = BuildManager(party, spear);

        AssertEq(
            manager.get_member_weapon_physical_damage_tag("missing"),
            new StringName(""),
            "missing member should not report an unarmed damage tag."
        );
        AssertEq(
            manager.get_member_weapon_physical_damage_tag("hero"),
            ItemDef.DAMAGE_TAG_PHYSICAL_BLUNT(),
            "empty main hand should use the unarmed physical damage tag."
        );

        EquipMainHand(party.get_member_state("hero"), spear.item_id);

        AssertEq(
            manager.get_member_weapon_physical_damage_tag("hero"),
            ItemDef.DAMAGE_TAG_PHYSICAL_PIERCE(),
            "equipped weapon should expose its typed physical damage tag."
        );
    }

    private void TestWeaponPhysicalDamageTagRejectsBadMainHandStates()
    {
        PartyState party = BuildPartyWithMember("hero");
        ItemDef ore = new()
        {
            item_id = "iron_ore",
            display_name = "Iron Ore",
            item_category = ItemDef.ITEM_CATEGORY_MISC(),
        };
        ItemDef invalidWeapon = MakeWeapon("invalid_blade", "elemental_fire");
        CharacterManagementModule manager = BuildManager(party, ore, invalidWeapon);

        EquipMainHand(party.get_member_state("hero"), "missing_sword");
        AssertEq(
            manager.get_member_weapon_physical_damage_tag("hero"),
            new StringName(""),
            "main-hand item without an item definition should not default to unarmed."
        );

        EquipMainHand(party.get_member_state("hero"), ore.item_id);
        AssertEq(
            manager.get_member_weapon_physical_damage_tag("hero"),
            new StringName(""),
            "main-hand non-weapon item should not expose a damage tag."
        );

        EquipMainHand(party.get_member_state("hero"), invalidWeapon.item_id);
        AssertEq(
            manager.get_member_weapon_physical_damage_tag("hero"),
            new StringName(""),
            "weapon with invalid physical damage tag should fail closed."
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        params ItemDef[] itemDefs
    )
    {
        GDictionary indexedItemDefs = new();
        foreach (ItemDef itemDef in itemDefs)
        {
            if (itemDef != null)
                indexedItemDefs[itemDef.item_id] = itemDef;
        }

        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            indexedItemDefs
        );
        return manager;
    }

    private static PartyState BuildPartyWithMember(string memberId)
    {
        PartyState party = new();
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId,
        };
        party.set_member_state(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static ItemDef MakeWeapon(StringName itemId, StringName damageTag) =>
        new()
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
            is_stackable = false,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
            weapon_profile = new WeaponProfileDef
            {
                weapon_type_id = "test_weapon_type",
                family = "test_family",
                range_type = "melee",
                damage_tag = damageTag,
                attack_range = 1,
                one_handed_dice = new WeaponDamageDiceDef(),
            },
        };

    private void EquipMainHand(PartyMemberState member, StringName itemId)
    {
        AssertTrue(member != null, "test setup should find party member.");
        if (member == null)
            return;

        AssertTrue(
            member.equipment_state.set_equipped_entry(
                "main_hand",
                itemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.create_instance(itemId, $"eq_{itemId}")
            ),
            $"test setup should equip {itemId}."
        );
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }
}
