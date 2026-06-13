using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_item_use_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedUseConsumesSkillBook();
        TestDuplicateLearnDoesNotConsumeInventory();

        Quit(_test.Finish("Party item use service regression"));
    }

    private void TestTypedUseConsumesSkillBook()
    {
        var fixture = BuildFixture();
        fixture.Service.Setup(
            fixture.PartyState,
            fixture.ItemDefIndex,
            fixture.SkillDefIndex,
            fixture.WarehouseService,
            fixture.CharacterManagement
        );
        fixture.WarehouseService.AddItemTyped("skill_book_focus", 1);

        var result = fixture.Service.UseItemTyped("skill_book_focus", "reader");

        _test.True(result.Success, "First skill book use should succeed.");
        _test.Eq(result.Reason, new StringName("ok"), "Successful use should return ok reason.");
        _test.Eq(result.SkillId, new StringName("focus"), "Result should keep typed skill id.");
        _test.Eq(result.ConsumedQuantity, 1, "Successful use should consume exactly one book.");
        _test.Eq(
            fixture.WarehouseService.CountItem("skill_book_focus"),
            0,
            "Successful use should remove the book from warehouse."
        );
        UnitSkillProgress skillProgress = fixture
            .PartyState
            .GetMemberState("reader")
            .progression
            .GetSkillProgress("focus");
        _test.True(
            skillProgress != null && skillProgress.is_learned,
            "Skill book use should learn the granted skill."
        );

        GDictionary publicResult = result.ToDictionary();
        _test.True(
            publicResult.ContainsKey("success") && publicResult["success"].AsBool(),
            "Typed result should project success to public dictionary boundary."
        );
    }

    private void TestDuplicateLearnDoesNotConsumeInventory()
    {
        var fixture = BuildFixture();
        fixture.Service.Setup(
            fixture.PartyState,
            fixture.ItemDefIndex,
            fixture.SkillDefIndex,
            fixture.WarehouseService,
            fixture.CharacterManagement
        );
        fixture.WarehouseService.AddItemTyped("skill_book_focus", 2);

        var first = fixture.Service.UseItemTyped("skill_book_focus", "reader");
        var second = fixture.Service.UseItemTyped("skill_book_focus", "reader");

        _test.True(first.Success, "Precondition: first skill book use should succeed.");
        _test.True(!second.Success, "Duplicate skill book use should fail.");
        _test.Eq(
            second.Reason,
            new StringName("learn_failed"),
            "Duplicate skill learn should return learn_failed."
        );
        _test.Eq(
            second.ConsumedQuantity,
            0,
            "Failed duplicate learn should keep typed consumed quantity at zero."
        );
        _test.Eq(
            fixture.WarehouseService.CountItem("skill_book_focus"),
            1,
            "Failed duplicate learn should not consume another book."
        );
    }

    private static Fixture BuildFixture()
    {
        PartyState partyState = BuildPartyState();
        GDictionary itemDefs = BuildItemDefs();
        GDictionary skillDefs = BuildSkillDefs();
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        CharacterManagementModule characterManagement = BuildCharacterManagement(
            partyState,
            skillDefs,
            itemDefs
        );

        return new Fixture(
            partyState,
            warehouseService,
            characterManagement,
            new PartyItemUseService(),
            new Dictionary<StringName, ItemDef>
            {
                ["skill_book_focus"] = (ItemDef)
                    itemDefs[new StringName("skill_book_focus")].AsGodotObject(),
            },
            new Dictionary<StringName, SkillDef>
            {
                ["focus"] = (SkillDef)skillDefs[new StringName("focus")].AsGodotObject(),
            }
        );
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "reader",
            main_character_member_id = "reader",
            active_member_ids = new GStringNameArray { "reader" },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState memberState = new()
        {
            member_id = "reader",
            display_name = "Reader",
        };
        memberState.progression.unit_id = "reader";
        memberState.progression.display_name = "Reader";
        memberState
            .progression
            .unit_base_attributes
            .SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, 4);
        partyState.SetMemberState(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs()
    {
        GDictionary result = new();
        result[new StringName("skill_book_focus")] = new ItemDef
        {
            item_id = "skill_book_focus",
            display_name = "Focus Manual",
            CategoryKind = ItemCategoryKind.SkillBook,
            is_stackable = true,
            max_stack = 20,
            granted_skill_id = "focus",
        };
        return result;
    }

    private static GDictionary BuildSkillDefs()
    {
        GDictionary result = new();
        result[new StringName("focus")] = new SkillDef
        {
            skill_id = "focus",
            display_name = "Focus",
            learn_source = "book",
            skill_type = "passive",
            max_level = 1,
        };
        return result;
    }

    private static PartyWarehouseService BuildWarehouseService(
        PartyState partyState,
        GDictionary itemDefs
    )
    {
        PartyWarehouseService service = new();
        service.Setup(partyState, BuildItemDefIndex(itemDefs));
        return service;
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }

    private static CharacterManagementModule BuildCharacterManagement(
        PartyState partyState,
        GDictionary skillDefs,
        GDictionary itemDefs
    )
    {
        CharacterManagementModule module = new();
        module.setup(partyState, skillDefs, new GDictionary(), new GDictionary(), itemDefs);
        return module;
    }



    private sealed class Fixture
    {
        public PartyState PartyState { get; }
        public PartyWarehouseService WarehouseService { get; }
        public CharacterManagementModule CharacterManagement { get; }
        public PartyItemUseService Service { get; }
        public Dictionary<StringName, ItemDef> ItemDefIndex { get; }
        public Dictionary<StringName, SkillDef> SkillDefIndex { get; }

        public Fixture(
            PartyState partyState,
            PartyWarehouseService warehouseService,
            CharacterManagementModule characterManagement,
            PartyItemUseService service,
            Dictionary<StringName, ItemDef> itemDefIndex,
            Dictionary<StringName, SkillDef> skillDefIndex
        )
        {
            PartyState = partyState;
            WarehouseService = warehouseService;
            CharacterManagement = characterManagement;
            Service = service;
            ItemDefIndex = itemDefIndex;
            SkillDefIndex = skillDefIndex;
        }
    }
}
