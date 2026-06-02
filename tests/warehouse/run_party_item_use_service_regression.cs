using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_item_use_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceIsPlainCSharp();
        TestPublicSetupMaterializesTypedIndexes();
        TestTypedUseConsumesSkillBook();
        TestDuplicateLearnDoesNotConsumeInventory();

        if (_failures.Count == 0)
        {
            GD.Print("Party item use service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Party item use service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestServiceIsPlainCSharp()
    {
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(typeof(PartyItemUseService)),
            "PartyItemUseService should not inherit RefCounted."
        );
        AssertTrue(
            typeof(PartyItemUseService).GetCustomAttribute<GlobalClassAttribute>() == null,
            "PartyItemUseService should not be registered as a Godot GlobalClass."
        );
    }

    private void TestPublicSetupMaterializesTypedIndexes()
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
        PartyItemUseService service = new();

        service.setup(partyState, itemDefs, skillDefs, warehouseService, characterManagement);

        AssertEq(
            typeof(PartyItemUseService)
                .GetField("_item_defs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, ItemDef>),
            "PartyItemUseService item defs cache should be a typed dictionary."
        );
        AssertEq(
            typeof(PartyItemUseService)
                .GetField("_skill_defs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "PartyItemUseService skill defs cache should be a typed dictionary."
        );

        var itemDefIndex = GetPrivateField<Dictionary<StringName, ItemDef>>(service, "_item_defs");
        var skillDefIndex = GetPrivateField<Dictionary<StringName, SkillDef>>(service, "_skill_defs");
        AssertTrue(
            itemDefIndex != null && itemDefIndex.ContainsKey("skill_book_focus"),
            "Public setup should materialize item defs into typed index."
        );
        AssertTrue(
            skillDefIndex != null && skillDefIndex.ContainsKey("focus"),
            "Public setup should materialize skill defs into typed index."
        );
    }

    private void TestTypedUseConsumesSkillBook()
    {
        var fixture = BuildFixture();
        fixture.Service.SetupTyped(
            fixture.PartyState,
            fixture.ItemDefIndex,
            fixture.SkillDefIndex,
            fixture.WarehouseService,
            fixture.CharacterManagement
        );
        fixture.WarehouseService.add_item("skill_book_focus", 1);

        var result = fixture.Service.UseItemTyped("skill_book_focus", "reader");

        AssertTrue(result.Success, "First skill book use should succeed.");
        AssertEq(result.Reason, new StringName("ok"), "Successful use should return ok reason.");
        AssertEq(result.SkillId, new StringName("focus"), "Result should keep typed skill id.");
        AssertEq(result.ConsumedQuantity, 1, "Successful use should consume exactly one book.");
        AssertEq(
            fixture.WarehouseService.count_item("skill_book_focus"),
            0,
            "Successful use should remove the book from warehouse."
        );
        UnitSkillProgress skillProgress = fixture
            .PartyState
            .get_member_state("reader")
            .progression
            .get_skill_progress("focus");
        AssertTrue(
            skillProgress != null && skillProgress.is_learned,
            "Skill book use should learn the granted skill."
        );

        GDictionary publicResult = result.ToDictionary();
        AssertTrue(
            publicResult.ContainsKey("success") && publicResult["success"].AsBool(),
            "Typed result should project success to public dictionary boundary."
        );
    }

    private void TestDuplicateLearnDoesNotConsumeInventory()
    {
        var fixture = BuildFixture();
        fixture.Service.SetupTyped(
            fixture.PartyState,
            fixture.ItemDefIndex,
            fixture.SkillDefIndex,
            fixture.WarehouseService,
            fixture.CharacterManagement
        );
        fixture.WarehouseService.add_item("skill_book_focus", 2);

        var first = fixture.Service.UseItemTyped("skill_book_focus", "reader");
        var second = fixture.Service.UseItemTyped("skill_book_focus", "reader");

        AssertTrue(first.Success, "Precondition: first skill book use should succeed.");
        AssertTrue(!second.Success, "Duplicate skill book use should fail.");
        AssertEq(
            second.Reason,
            new StringName("learn_failed"),
            "Duplicate skill learn should return learn_failed."
        );
        AssertEq(
            second.ConsumedQuantity,
            0,
            "Failed duplicate learn should keep typed consumed quantity at zero."
        );
        AssertEq(
            fixture.WarehouseService.count_item("skill_book_focus"),
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
                ["skill_book_focus"] = (ItemDef)itemDefs["skill_book_focus"].AsGodotObject(),
            },
            new Dictionary<StringName, SkillDef>
            {
                ["focus"] = (SkillDef)skillDefs["focus"].AsGodotObject(),
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
            .set_attribute_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(), 4);
        partyState.set_member_state(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs() =>
        new()
        {
            ["skill_book_focus"] = new ItemDef
            {
                item_id = "skill_book_focus",
                display_name = "Focus Manual",
                item_category = ItemDef.ITEM_CATEGORY_SKILL_BOOK(),
                is_stackable = true,
                max_stack = 20,
                granted_skill_id = "focus",
            },
        };

    private static GDictionary BuildSkillDefs() =>
        new()
        {
            ["focus"] = new SkillDef
            {
                skill_id = "focus",
                display_name = "Focus",
                learn_source = "book",
                skill_type = "passive",
                max_level = 1,
            },
        };

    private static PartyWarehouseService BuildWarehouseService(
        PartyState partyState,
        GDictionary itemDefs
    )
    {
        PartyWarehouseService service = new();
        service.setup(partyState, itemDefs);
        return service;
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

    private static T GetPrivateField<T>(object source, string fieldName)
        where T : class
    {
        return typeof(PartyItemUseService)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(source) as T;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} expected={expected} actual={actual}");
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
