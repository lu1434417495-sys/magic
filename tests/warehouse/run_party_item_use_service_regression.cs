using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_item_use_service_regression : LifecycleTestSceneTree
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

        RequestTestExit(_test.Finish("Party item use service regression"));
    }

    private void TestTypedUseConsumesSkillBook()
    {
        var fixture = BuildFixture();
        fixture.Service.Setup(
            fixture.PartyState,
            fixture.ItemDefinitions,
            fixture.SkillDefinitionIndex,
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

        GDictionary publicResult = PartyInventoryProjection.Project(result);
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
            fixture.ItemDefinitions,
            fixture.SkillDefinitionIndex,
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
        Dictionary<StringName, ItemDefinition> itemDefinitions = BuildItemDefinitions();
        Dictionary<StringName, SkillDefinition> skillDefinitions = BuildSkillDefinitions();
        PartyWarehouseService warehouseService = BuildWarehouseService(
            partyState,
            itemDefinitions
        );
        CharacterManagementModule characterManagement = BuildCharacterManagement(
            partyState,
            skillDefinitions,
            itemDefinitions
        );

        return new Fixture(
            partyState,
            warehouseService,
            characterManagement,
            new PartyItemUseService(),
            itemDefinitions,
            skillDefinitions
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

    private static Dictionary<StringName, ItemDefinition> BuildItemDefinitions()
    {
        ItemDef authored = TestResourceOwnership.Own(
            new ItemDef
            {
                item_id = "skill_book_focus",
                display_name = "Focus Manual",
                CategoryKind = ItemCategoryKind.SkillBook,
                is_stackable = true,
                max_stack = 20,
                granted_skill_id = "focus",
            },
            "party_item_use_service.skill_book_focus"
        );
        return new Dictionary<StringName, ItemDefinition>
        {
            ["skill_book_focus"] = authored.ToDefinition(),
        };
    }

    private static Dictionary<StringName, SkillDefinition> BuildSkillDefinitions()
    {
        Dictionary<StringName, SkillDefinition> result = new();
        StringName skillId = "focus";
        result[skillId] = new SkillDefinition(
            skillId,
            "Focus",
            "",
            "",
            "passive",
            1,
            1,
            "",
            0,
            0,
            System.Array.Empty<int>(),
            System.Array.Empty<StringName>(),
            "book",
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            null
        );
        return result;
    }

    private static PartyWarehouseService BuildWarehouseService(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        PartyWarehouseService service = new();
        service.Setup(partyState, itemDefinitions);
        return service;
    }

    private static CharacterManagementModule BuildCharacterManagement(
        PartyState partyState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        CharacterManagementModule module = new();
        module.setup(
            partyState,
            skillDefinitions,
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            itemDefinitions
        );
        return module;
    }



    private sealed class Fixture
    {
        public PartyState PartyState { get; }
        public PartyWarehouseService WarehouseService { get; }
        public CharacterManagementModule CharacterManagement { get; }
        public PartyItemUseService Service { get; }
        public IReadOnlyDictionary<StringName, ItemDefinition> ItemDefinitions { get; }
        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitionIndex { get; }

        public Fixture(
            PartyState partyState,
            PartyWarehouseService warehouseService,
            CharacterManagementModule characterManagement,
            PartyItemUseService service,
            IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitionIndex
        )
        {
            PartyState = partyState;
            WarehouseService = warehouseService;
            CharacterManagement = characterManagement;
            Service = service;
            ItemDefinitions = itemDefinitions;
            SkillDefinitionIndex = skillDefinitionIndex;
        }
    }
}
