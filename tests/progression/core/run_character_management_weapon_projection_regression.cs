using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class run_character_management_weapon_projection_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<GodotObject> _ownedGodotObjects = new();
    private readonly List<IDisposable> _ownedDisposables = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestWeaponPhysicalDamageTagUsesTypedEquipmentState();
            TestWeaponPhysicalDamageTagRejectsBadMainHandStates();
            TestWeaponProjectionForEquipmentViewUsesTypedDto();
        }
        finally
        {
            DisposeOwned();
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Character management weapon projection regression"));
    }

    private void TestWeaponPhysicalDamageTagUsesTypedEquipmentState()
    {
        PartyState party = BuildPartyWithMember("hero");
        ItemDef spear = MakeWeapon(
            "test_spear",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Pierce)
        );
        CharacterManagementModule manager = BuildManager(party, spear);

        _test.Eq(
            manager.GetMemberWeaponPhysicalDamageTag("missing"),
            new StringName(""),
            "missing member should not report an unarmed damage tag."
        );
        _test.Eq(
            manager.GetMemberWeaponPhysicalDamageTag("hero"),
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Blunt),
            "empty main hand should use the unarmed physical damage tag."
        );

        EquipMainHand(party.GetMemberState("hero"), spear.item_id);

        _test.Eq(
            manager.GetMemberWeaponPhysicalDamageTag("hero"),
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Pierce),
            "equipped weapon should expose its typed physical damage tag."
        );
    }

    private void TestWeaponPhysicalDamageTagRejectsBadMainHandStates()
    {
        PartyState party = BuildPartyWithMember("hero");
        ItemDef ore = TrackOwned(new ItemDef
        {
            item_id = "iron_ore",
            display_name = "Iron Ore",
            CategoryKind = ItemCategoryKind.Misc,
        });
        ItemDef invalidWeapon = MakeWeapon("invalid_blade", "elemental_fire");
        CharacterManagementModule manager = BuildManager(party, ore, invalidWeapon);

        EquipMainHand(party.GetMemberState("hero"), "missing_sword");
        _test.Eq(
            manager.GetMemberWeaponPhysicalDamageTag("hero"),
            new StringName(""),
            "main-hand item without an item definition should not default to unarmed."
        );

        EquipMainHand(party.GetMemberState("hero"), ore.item_id);
        _test.Eq(
            manager.GetMemberWeaponPhysicalDamageTag("hero"),
            new StringName(""),
            "main-hand non-weapon item should not expose a damage tag."
        );

        EquipMainHand(party.GetMemberState("hero"), invalidWeapon.item_id);
        _test.Eq(
            manager.GetMemberWeaponPhysicalDamageTag("hero"),
            new StringName(""),
            "weapon with invalid physical damage tag should fail closed."
        );
    }

    private void TestWeaponProjectionForEquipmentViewUsesTypedDto()
    {
        PartyState party = BuildPartyWithMember("hero");
        ItemDef longsword = MakeWeapon(
            "training_longsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash)
        );
        WeaponProfileDef weaponProfile = longsword.weapon_profile as WeaponProfileDef;
        weaponProfile.attack_range = 2;
        weaponProfile.family = "sword";
        weaponProfile.weapon_type_id = "longsword";
        weaponProfile.one_handed_dice = new WeaponDamageDiceDef
        {
            dice_count = 1,
            dice_sides = 8,
            flat_bonus = 2,
        };
        CharacterManagementModule manager = BuildManager(party, longsword);

        WeaponProjection unarmed = manager.GetMemberWeaponProjectionForEquipmentViewTyped(
            "hero",
            party.GetMemberState("hero").equipment_state
        );
        _test.Eq(
            unarmed.weapon_profile_kind,
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed),
            "empty main hand should project typed unarmed weapon state."
        );
        _test.Eq(
            unarmed.weapon_physical_damage_tag,
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Blunt),
            "typed unarmed projection should keep the blunt damage tag."
        );

        EquipMainHand(party.GetMemberState("hero"), longsword.item_id);
        WeaponProjection equipped = manager.GetMemberWeaponProjectionForEquipmentViewTyped(
            "hero",
            party.GetMemberState("hero").equipment_state
        );
        _test.Eq(
            equipped.weapon_profile_kind,
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            "equipped main hand should project an equipped weapon DTO."
        );
        _test.Eq(
            equipped.weapon_item_id,
            longsword.item_id,
            "typed weapon projection should preserve the equipped item id."
        );
        _test.Eq(
            equipped.weapon_family,
            new StringName("sword"),
            "typed weapon projection should preserve the weapon family."
        );
        _test.Eq(
            equipped.weapon_attack_range,
            2,
            "typed weapon projection should preserve attack range."
        );
        _test.Eq(
            equipped.weapon_one_handed_dice.dice_sides,
            8,
            "typed weapon projection should preserve one-handed dice."
        );
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            "typed weapon projection should preserve physical damage tag."
        );
    }

    private CharacterManagementModule BuildManager(
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

        CharacterManagementModule manager = TrackDisposable(new CharacterManagementModule());
        manager.setup(
            party,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            indexedItemDefs
        );
        return manager;
    }

    private PartyState BuildPartyWithMember(string memberId)
    {
        PartyState party = TrackOwned(new PartyState());
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId,
        };
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private ItemDef MakeWeapon(StringName itemId, StringName damageTag) =>
        TrackOwned(new ItemDef
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            CategoryKind = ItemCategoryKind.Equipment,
            is_stackable = false,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            weapon_profile = new WeaponProfileDef
            {
                weapon_type_id = "test_weapon_type",
                family = "test_family",
                range_type = "melee",
                damage_tag = damageTag,
                attack_range = 1,
                one_handed_dice = new WeaponDamageDiceDef(),
            },
        });

    private void EquipMainHand(PartyMemberState member, StringName itemId)
    {
        _test.True(member != null, "test setup should find party member.");
        if (member == null)
            return;

        _test.True(
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                itemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(itemId, $"eq_{itemId}")
            ),
            $"test setup should equip {itemId}."
        );
    }

    private T TrackOwned<T>(T value)
        where T : GodotObject
    {
        if (value != null)
            _ownedGodotObjects.Add(value);
        return value;
    }

    private T TrackDisposable<T>(T value)
        where T : IDisposable
    {
        if (value != null)
            _ownedDisposables.Add(value);
        return value;
    }

    private void DisposeOwned()
    {
        for (int index = _ownedDisposables.Count - 1; index >= 0; index--)
            _ownedDisposables[index]?.Dispose();
        _ownedDisposables.Clear();

        for (int index = _ownedGodotObjects.Count - 1; index >= 0; index--)
            DisposeOwnedGodotObject(_ownedGodotObjects[index]);
        _ownedGodotObjects.Clear();
    }

    private static void DisposeOwnedGodotObject(GodotObject ownedObject)
    {
        switch (ownedObject)
        {
            case null:
                return;
            case PartyState party:
                GodotRefCountedDisposer.DisposeIfValid(party);
                return;
            default:
                BattleTestFixture.DisposeFixtureObject(ownedObject);
                return;
        }
    }


}
