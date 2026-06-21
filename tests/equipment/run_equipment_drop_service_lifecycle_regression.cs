using System;
using Godot;

public partial class run_equipment_drop_service_lifecycle_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceOwnsFallbackRngAndBorrowsInjectedRng();
        TestServiceCanTakeOwnershipOfInjectedRng();
        TestServiceCanTakeOwnershipOfPreviouslyBorrowedRng();
        TestBattleRuntimeModuleOwnsOnlySelfCreatedDropService();
        TestBattleRuntimeModuleCanTakeOwnershipOfInjectedDropService();
        TestGameRuntimeFacadeDisposesOwnedDropService();

        Quit(_test.Finish("Equipment drop service lifecycle regression"));
    }

    private void TestServiceOwnsFallbackRngAndBorrowsInjectedRng()
    {
        using EquipmentDropService service = new();
        _test.True(service.OwnsRngForTests, "fallback RNG 应由 EquipmentDropService 拥有。");
        _test.True(
            service.HasLiveOwnedRngForTests,
            "fallback RNG 在 service dispose 前应保持有效。"
        );

        RandomNumberGenerator borrowedRng = new();
        try
        {
            borrowedRng.Randomize();
            service.SetRngForTesting(borrowedRng);

            _test.False(service.OwnsRngForTests, "注入 RNG 应被视作 borrowed。");
            _test.False(
                service.HasLiveOwnedRngForTests,
                "替换成 borrowed RNG 后 service 不应再报告 owned RNG。"
            );
            _test.True(
                ReferenceEquals(service.RngForTests, borrowedRng),
                "service 应切换到 caller 提供的 borrowed RNG。"
            );

            service.Dispose();
            _test.True(service.IsDisposedForTests, "EquipmentDropService dispose 后应标记 disposed。");
            _ = borrowedRng.Randi();

            bool threw = false;
            try
            {
                service.RollDropRarity(0);
            }
            catch (ObjectDisposedException)
            {
                threw = true;
            }
            _test.True(threw, "dispose 后继续使用 EquipmentDropService 应抛 ObjectDisposedException。");
        }
        finally
        {
            GodotSharpCleanup.DisposeGodotObject(borrowedRng);
        }
    }

    private void TestServiceCanTakeOwnershipOfInjectedRng()
    {
        RandomNumberGenerator ownedRng = new();
        ownedRng.Randomize();
        EquipmentDropService service = new(ownedRng, ownsRng: true);

        _test.True(service.OwnsRngForTests, "ownsRng=true 应把传入 RNG 标记为 service-owned。");
        _test.True(
            service.HasLiveOwnedRngForTests,
            "转交所有权的 RNG 在 service dispose 前应保持 live owned。"
        );

        service.Dispose();
        _test.True(service.IsDisposedForTests, "service dispose 后应标记 disposed。");
        _test.True(service.RngForTests == null, "service dispose 应清空已 owned 的 RNG 引用。");
        _test.False(
            service.HasLiveOwnedRngForTests,
            "service dispose 后不应继续报告 live owned RNG。"
        );
    }

    private void TestServiceCanTakeOwnershipOfPreviouslyBorrowedRng()
    {
        RandomNumberGenerator rng = new();
        rng.Randomize();
        EquipmentDropService service = new(rng);

        _test.False(
            service.OwnsRngForTests,
            "普通构造注入 RNG 时 EquipmentDropService 应先保持 borrowed。"
        );
        service.SetOwnedRng(rng);
        _test.True(
            ReferenceEquals(service.RngForTests, rng),
            "同一个 RNG 转交所有权时不应替换或释放当前 wrapper。"
        );
        _test.True(
            service.OwnsRngForTests,
            "SetOwnedRng 应允许 caller 显式转交之前 borrowed 的 RNG。"
        );
        _test.True(
            service.HasLiveOwnedRngForTests,
            "显式转交后的 RNG 在 service dispose 前应保持 live owned。"
        );

        service.Dispose();
        _test.True(service.RngForTests == null, "释放接管的 RNG 后 service 应清空引用。");
        _test.False(
            service.HasLiveOwnedRngForTests,
            "释放接管的 RNG 后 service 不应报告 live owned RNG。"
        );
    }

    private void TestBattleRuntimeModuleOwnsOnlySelfCreatedDropService()
    {
        BattleRuntimeModule module = new();
        EquipmentDropService initialService = module._equipment_drop_service;
        using EquipmentDropService borrowedService = new();

        module.setup(equipment_drop_service: borrowedService);
        _test.True(
            initialService.IsDisposedForTests,
            "BattleRuntimeModule setup 替换 service 时应释放旧的 module-owned service。"
        );
        _test.False(
            borrowedService.IsDisposedForTests,
            "BattleRuntimeModule setup 注入的 service 应保持 caller-owned。"
        );

        module.Dispose();
        _test.False(
            borrowedService.IsDisposedForTests,
            "BattleRuntimeModule dispose 不应释放 borrowed drop service。"
        );
    }

    private void TestBattleRuntimeModuleCanTakeOwnershipOfInjectedDropService()
    {
        BattleRuntimeModule module = new();
        EquipmentDropService transferredService = new();

        module.setup(
            equipment_drop_service: transferredService,
            owns_equipment_drop_service: true
        );
        _test.False(
            transferredService.IsDisposedForTests,
            "转交给 BattleRuntimeModule 后，module dispose 前 service 应仍可用。"
        );

        module.Dispose();
        _test.True(
            transferredService.IsDisposedForTests,
            "owns_equipment_drop_service=true 时 module dispose 应释放注入 service。"
        );
    }

    private void TestGameRuntimeFacadeDisposesOwnedDropService()
    {
        GameRuntimeFacade facade = new();
        EquipmentDropService ownedService = facade._equipment_drop_service;

        facade.Dispose();
        _test.True(
            ownedService.IsDisposedForTests,
            "GameRuntimeFacade dispose 应释放自己持有的 equipment drop service。"
        );
    }
}
