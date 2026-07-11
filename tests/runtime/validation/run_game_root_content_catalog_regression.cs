using System;
using System.Collections.Generic;
using Godot;

public partial class run_game_root_content_catalog_regression : LifecycleTestSceneTree
{
    private static readonly StringName CatalogProbeItemId = "catalog_regression_probe_item";
    private static readonly StringName FacadeProbeItemId = "facade_catalog_regression_probe_item";
    private static readonly StringName DisposeProbeItemId = "dispose_catalog_regression_probe_item";
    private static readonly StringName DefensiveProbeSkillId = "defensive_catalog_regression_probe_skill";
    private static readonly StringName RollerProbeItemId = "roller_catalog_regression_probe_item";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestGameSessionOwnsUnifiedRootAndContentCatalog();
        TestContentCatalogCachesAndSyncsOnContentRefresh();
        TestContentCatalogReturnsDefensiveReadOnlyViews();
        TestContentCatalogIdentityCatalogReturnsDefensiveReadOnlyViews();
        TestContentCatalogInvalidatedAfterSessionDispose();
        TestRuntimeFacadeBindsUnifiedRootBeforeWorldSetup();
        TestRuntimeFacadeReadsCurrentCatalogAfterContentRefresh();
        TestRuntimeFacadeRebuildsEquipmentTraitRollerWhenCatalogOrSessionChanges();
        TestRuntimeFacadeDiscardsStaleContentCatalog();
        TestRuntimeFacadeDiscardsCatalogBoundToOtherSession();

        RequestTestExit(_test.Finish("Game root content catalog regression"));
    }

    private void TestGameSessionOwnsUnifiedRootAndContentCatalog()
    {
        GameSession gameSession = new();
        try
        {
            GameRoot root = gameSession.GetGameRootTyped();
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();

            _test.True(root != null, "GameSession 应暴露统一 GameRoot。");
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            _test.True(root.HasSessionTyped(), "GameRoot 应绑定当前 GameSession。");
            _test.True(catalog.HasSessionTyped(), "GameContentCatalog 应绑定当前 GameSession。");
            _test.True(
                ReferenceEquals(root.GetSessionTyped(), gameSession),
                "GameRoot 的 session owner 应是创建它的 GameSession。"
            );
            _test.True(
                ReferenceEquals(catalog.GetSessionTyped(), gameSession),
                "GameContentCatalog 的 session owner 应是创建它的 GameSession。"
            );
            _test.True(
                ReferenceEquals(root.GetContentCatalogTyped(), catalog),
                "GameSession 和 GameRoot 应返回同一个 content catalog 实例。"
            );

            _test.Eq(
                gameSession.GetSkillDefinitionsTyped().Count,
                catalog.GetSkillDefinitionsTyped().Count,
                "content catalog skill definitions 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetProfessionDefsTyped().Count,
                catalog.GetProfessionDefsTyped().Count,
                "content catalog profession defs 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetTraitDefsTyped().Count,
                catalog.GetTraitDefsTyped().Count,
                "content catalog trait defs 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetEquipmentAbilityBindingDefinitionsTyped().Count,
                catalog.GetEquipmentAbilityBindingDefinitionsTyped().Count,
                "content catalog equipment ability bindings 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetEquipmentAbilityContentRevision(),
                catalog.GetEquipmentAbilityContentRevision(),
                "content catalog equipment ability revision 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetItemDefsTyped().Count,
                catalog.GetItemDefsTyped().Count,
                "content catalog item defs 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetEnemyTemplatesTyped().Count,
                catalog.GetEnemyTemplatesTyped().Count,
                "content catalog enemy templates 应与 GameSession 正式内容缓存一致。"
            );
            _test.Eq(
                gameSession.GetWildEncounterRostersTyped().Count,
                catalog.GetWildEncounterRostersTyped().Count,
                "content catalog wild encounter rosters 应与 GameSession 正式内容缓存一致。"
            );
            _test.True(
                catalog.GetBattleSpecialProfileRegistrySnapshot() != null,
                "content catalog 应提供 battle special profile snapshot 边界。"
            );

            // catalog 持有的是 typed 快照，而不是 session getter 的 live 转发：
            // 直接往 session 的 DTO 内容缓存塞一个 fake skill，未刷新前 catalog 不应看到，
            // 显式刷新 catalog 后才应看到。
            int baselineSkillCount = catalog.GetSkillDefinitionsTyped().Count;
            StringName fakeSkillId = "regression_fake_skill";
            gameSession.SetSkillDefinitionForTests(
                fakeSkillId,
                BuildProbeSkillDefinition(fakeSkillId)
            );
            _test.Eq(
                catalog.GetSkillDefinitionsTyped().Count,
                baselineSkillCount,
                "未刷新时 content catalog 不应看到直接塞进 GameSession DTO 缓存的 fake skill"
                    + "（证明 catalog getter 不是 session getter 的 live 转发）。"
            );
            gameSession.RefreshContentCatalogForTests();
            _test.Eq(
                catalog.GetSkillDefinitionsTyped().Count,
                baselineSkillCount + 1,
                "显式刷新 content catalog 后应反映新塞入 GameSession DTO 缓存的 fake skill。"
            );
            _test.True(
                catalog.GetSkillDefinitionsTyped().ContainsKey(fakeSkillId),
                "刷新后的 content catalog skill definition 快照应包含 fake skill id。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestContentCatalogCachesAndSyncsOnContentRefresh()
    {
        GameSession gameSession = new();
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (catalog == null)
                return;

            // catalog 不是纯代理：同一份 typed 表跨调用应返回缓存的同一实例。
            _test.True(
                ReferenceEquals(
                    catalog.GetSkillDefinitionsTyped(),
                    catalog.GetSkillDefinitionsTyped()
                ),
                "content catalog 应返回缓存的 typed skill definition 视图，而不是每次重建。"
            );
            _test.True(
                ReferenceEquals(catalog.GetItemDefsTyped(), catalog.GetItemDefsTyped()),
                "content catalog 应返回缓存的 typed item 视图，而不是每次重建。"
            );
            // 对照：GameSession 自身 getter 每次都重建 typed index，证明 catalog 持有独立缓存。
            _test.True(
                !ReferenceEquals(
                    gameSession.GetSkillDefinitionsTyped(),
                    gameSession.GetSkillDefinitionsTyped()
                ),
                "GameSession typed getter 仍每次重建，catalog 不应只是其代理。"
            );
            // battle special profile snapshot 应是防御性副本，而不是别名缓存。
            Godot.Collections.Dictionary snapshotA =
                catalog.GetBattleSpecialProfileRegistrySnapshot();
            Godot.Collections.Dictionary snapshotB =
                catalog.GetBattleSpecialProfileRegistrySnapshot();
            _test.True(snapshotA != null, "content catalog 应提供 battle special profile snapshot。");
            _test.True(
                !ReferenceEquals(snapshotA, snapshotB),
                "battle special profile snapshot 应返回缓存的防御性副本。"
            );

            long revisionBefore = catalog.GetRevision();
            int itemCountBefore = catalog.GetItemDefsTyped().Count;

            _test.Eq(
                gameSession.InstallTestContentDef("item", CatalogProbeItemId, BuildProbeItemDef(CatalogProbeItemId)),
                (int)Error.Ok,
                "应能注册 content catalog 回归用探针物品。"
            );

            // 刷新后 catalog 缓存应与 GameSession 同步，并且 revision 自增。
            _test.True(
                catalog.GetItemDefsTyped().ContainsKey(CatalogProbeItemId),
                "content catalog 在内容刷新后应同步出新装入的物品。"
            );
            _test.Eq(
                catalog.GetItemDefsTyped().Count,
                itemCountBefore + 1,
                "content catalog item 缓存在刷新后应增加一个条目。"
            );
            _test.Eq(
                catalog.GetItemDefsTyped().Count,
                gameSession.GetItemDefsTyped().Count,
                "content catalog item 缓存在刷新后应与 GameSession 一致。"
            );
            _test.True(
                catalog.GetRevision() > revisionBefore,
                "content catalog revision 应在内容刷新后自增。"
            );
            _test.True(
                ReferenceEquals(gameSession.GetContentCatalogTyped(), catalog),
                "刷新内容不应替换 GameSession 持有的 content catalog 实例。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestContentCatalogReturnsDefensiveReadOnlyViews()
    {
        GameSession gameSession = new();
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (catalog == null)
                return;

            // 装入一个探针技能，让只读视图非空，证明防御性包装而不是空集合特例。
            gameSession.SetSkillDefinitionForTests(
                DefensiveProbeSkillId,
                BuildProbeSkillDefinition(DefensiveProbeSkillId)
            );
            gameSession.RefreshContentCatalogForTests();

            IReadOnlyDictionary<StringName, SkillDefinition> skillView =
                catalog.GetSkillDefinitionsTyped();
            _test.True(
                skillView.ContainsKey(DefensiveProbeSkillId),
                "刷新后的只读 skill definition 视图应包含探针技能。"
            );
            _test.True(
                skillView as Dictionary<StringName, SkillDefinition> == null,
                "typed skill definition getter 不应可被 downcast 成内部可变 Dictionary。"
            );

            int skillCountBefore = catalog.GetSkillDefinitionsTyped().Count;
            bool skillMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, SkillDefinition>)skillView)[
                    "defensive_inject_skill"
                ] = skillView[DefensiveProbeSkillId];
            }
            catch (NotSupportedException)
            {
                skillMutationBlocked = true;
            }
            _test.True(
                skillMutationBlocked,
                "通过 IDictionary 接口改写 catalog skill definition 只读视图应抛 NotSupportedException。"
            );
            _test.Eq(
                catalog.GetSkillDefinitionsTyped().Count,
                skillCountBefore,
                "对只读视图的改写尝试不应影响 catalog skill definition 快照。"
            );

            IReadOnlyDictionary<StringName, TraitDef> traitView = catalog.GetTraitDefsTyped();
            _test.True(
                traitView as Dictionary<StringName, TraitDef> == null,
                "typed trait getter 不应可被 downcast 成内部可变 Dictionary。"
            );
            int traitCountBefore = catalog.GetTraitDefsTyped().Count;
            bool traitMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, TraitDef>)traitView)["defensive_inject_trait"] =
                    new TraitDef { trait_id = "defensive_inject_trait" };
            }
            catch (NotSupportedException)
            {
                traitMutationBlocked = true;
            }
            _test.True(
                traitMutationBlocked,
                "通过 IDictionary 接口改写 catalog trait 只读视图应抛 NotSupportedException。"
            );
            _test.Eq(
                catalog.GetTraitDefsTyped().Count,
                traitCountBefore,
                "对只读视图的改写尝试不应影响 catalog trait 快照。"
            );

            IReadOnlyDictionary<StringName, ItemDef> itemView = catalog.GetItemDefsTyped();
            _test.True(
                itemView as Dictionary<StringName, ItemDef> == null,
                "typed item getter 不应可被 downcast 成内部可变 Dictionary。"
            );
            int itemCountBefore = catalog.GetItemDefsTyped().Count;
            bool itemMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, ItemDef>)itemView).Clear();
            }
            catch (NotSupportedException)
            {
                itemMutationBlocked = true;
            }
            _test.True(
                itemMutationBlocked,
                "通过 IDictionary 接口清空 catalog item 只读视图应抛 NotSupportedException。"
            );
            _test.Eq(
                catalog.GetItemDefsTyped().Count,
                itemCountBefore,
                "对只读视图的清空尝试不应影响 catalog item 快照。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestContentCatalogIdentityCatalogReturnsDefensiveReadOnlyViews()
    {
        GameSession gameSession = new();
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (catalog == null)
                return;

            ProgressionIdentityCatalogData identityCatalog =
                catalog.GetProgressionIdentityCatalogTyped();
            _test.True(
                identityCatalog != null,
                "content catalog 应暴露 progression identity catalog 快照。"
            );
            if (identityCatalog == null)
                return;

            IReadOnlyDictionary<StringName, RaceDef> raceView = identityCatalog.RaceDefs;
            _test.True(raceView != null, "identity catalog 应暴露 race defs 只读视图。");
            if (raceView == null)
                return;

            // identity catalog 的 race 视图同样是防御性只读包装：downcast 成内部可变 Dictionary 应失败，
            // 否则下游可绕过只读约束改写 catalog 暴露的 identity snapshot。
            _test.True(
                raceView as Dictionary<StringName, RaceDef> == null,
                "identity catalog race getter 不应可被 downcast 成内部可变 Dictionary。"
            );

            int raceCountBefore = identityCatalog.RaceDefs.Count;
            bool raceMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, RaceDef>)raceView)["identity_inject_race"] =
                    new RaceDef();
            }
            catch (NotSupportedException)
            {
                raceMutationBlocked = true;
            }
            _test.True(
                raceMutationBlocked,
                "通过 IDictionary 接口改写 identity catalog race 只读视图应抛 NotSupportedException。"
            );
            _test.Eq(
                identityCatalog.RaceDefs.Count,
                raceCountBefore,
                "对 identity catalog race 只读视图的改写尝试不应影响 catalog 快照。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestContentCatalogInvalidatedAfterSessionDispose()
    {
        GameSession gameSession = new();
        GameContentCatalog catalog = null;
        bool runtimeResourcesDisposed = false;
        try
        {
            catalog = gameSession.GetContentCatalogTyped();
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (catalog == null)
                return;

            _test.Eq(
                gameSession.InstallTestContentDef(
                    "item",
                    DisposeProbeItemId,
                    BuildProbeItemDef(DisposeProbeItemId)
                ),
                (int)Error.Ok,
                "应能注册 dispose 回归用探针物品。"
            );
            _test.True(catalog.HasSessionTyped(), "dispose 前 catalog 应绑定 session。");
            _test.True(
                catalog.GetItemDefsTyped().ContainsKey(DisposeProbeItemId),
                "dispose 前 catalog 应同步出探针物品。"
            );

            long revisionBefore = catalog.GetRevision();
            int itemCountBefore = catalog.GetItemDefsTyped().Count;
            _test.True(
                itemCountBefore > 0,
                "dispose 前 catalog 应至少持有一个 typed item，便于验证失效语义不是空集合特例。"
            );

            // dispose 拥有的运行期资源：root.DisposeOwnedRuntimeResources -> catalog.ClearSessionBinding。
            gameSession.DisposeOwnedRuntimeResources();
            runtimeResourcesDisposed = true;

            _test.True(
                !catalog.HasSessionTyped(),
                "dispose 后旧 catalog 应解除 session 绑定。"
            );
            _test.Eq(
                catalog.GetItemDefsTyped().Count,
                0,
                "dispose 后旧 catalog 不应再读到 stale typed item 快照。"
            );
            _test.True(
                !catalog.GetItemDefsTyped().ContainsKey(DisposeProbeItemId),
                "dispose 后旧 catalog 不应再含探针物品。"
            );
            _test.Eq(
                catalog.GetSkillDefinitionsTyped().Count,
                0,
                "dispose 后旧 catalog 不应再读到 stale typed skill definition 快照。"
            );
            _test.Eq(
                catalog.GetTraitDefsTyped().Count,
                0,
                "dispose 后旧 catalog 不应再读到 stale typed trait 快照。"
            );
            _test.Eq(
                catalog.GetEquipmentAbilityBindingDefinitionsTyped().Count,
                0,
                "dispose 后旧 catalog 不应再读到 stale equipment ability binding 快照。"
            );
            _test.True(
                catalog.GetProgressionContentRegistryTyped() == null,
                "dispose 后旧 catalog 不应再持有 progression content registry 引用。"
            );
            _test.True(
                catalog.GetRevision() > revisionBefore,
                "dispose 后 catalog revision 应自增以标记失效。"
            );
        }
        finally
        {
            if (!runtimeResourcesDisposed)
                gameSession.DisposeOwnedRuntimeResources();
            gameSession.Dispose();
        }
    }

    private void TestRuntimeFacadeBindsUnifiedRootBeforeWorldSetup()
    {
        GameSession gameSession = new();
        GameRuntimeFacade runtime = new();
        try
        {
            GameRoot root = gameSession.GetGameRootTyped();
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();

            runtime.Setup(gameSession);

            _test.True(
                ReferenceEquals(runtime.GetGameRootTyped(), root),
                "GameRuntimeFacade 应绑定 GameSession 持有的统一 GameRoot。"
            );
            _test.True(
                ReferenceEquals(runtime.GetContentCatalogTyped(), catalog),
                "GameRuntimeFacade 应通过统一 root 绑定 content catalog。"
            );
        }
        finally
        {
            runtime.Dispose();
            CleanupGameSession(gameSession);
        }
    }

    private void TestRuntimeFacadeReadsCurrentCatalogAfterContentRefresh()
    {
        GameSession gameSession = new();
        GameRuntimeFacade runtime = new();
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            runtime.Setup(gameSession);

            long revisionBefore = catalog.GetRevision();
            _test.Eq(
                gameSession.InstallTestContentDef(
                    "item",
                    FacadeProbeItemId,
                    BuildProbeItemDef(FacadeProbeItemId)
                ),
                (int)Error.Ok,
                "应能以 StringName key 注册 facade content catalog 回归用探针物品。"
            );

            GameContentCatalog runtimeCatalog = runtime.GetContentCatalogTyped();
            _test.True(
                ReferenceEquals(runtimeCatalog, catalog),
                "GameRuntimeFacade 应解析到 GameSession 当前的 content catalog 实例。"
            );
            _test.True(
                runtimeCatalog.GetRevision() > revisionBefore,
                "GameRuntimeFacade 看到的 catalog revision 应反映内容刷新。"
            );
            _test.True(
                runtimeCatalog.GetItemDefsTyped().ContainsKey(FacadeProbeItemId),
                "GameRuntimeFacade 读取的当前 catalog 应同步出新装入的物品。"
            );
        }
        finally
        {
            runtime.Dispose();
            CleanupGameSession(gameSession);
        }
    }

    private void TestRuntimeFacadeRebuildsEquipmentTraitRollerWhenCatalogOrSessionChanges()
    {
        GameSession gameSession = new();
        GameSession otherSession = new();
        GameRuntimeFacade runtime = new();
        try
        {
            runtime.Setup(gameSession);
            EquipmentTraitRollService first = runtime.GetEquipmentTraitRollService();
            _test.True(first != null, "facade should build an equipment trait roller for a session.");

            GameContentCatalog catalog = runtime.GetContentCatalogTyped();
            long revisionBefore = catalog.GetRevision();
            _test.Eq(
                gameSession.InstallTestContentDef(
                    "item",
                    RollerProbeItemId,
                    BuildProbeItemDef(RollerProbeItemId)
                ),
                (int)Error.Ok,
                "test content install should advance the content catalog revision."
            );
            _test.True(
                runtime.GetContentCatalogTyped().GetRevision() > revisionBefore,
                "catalog revision should advance after content refresh."
            );

            EquipmentTraitRollService afterRefresh = runtime.GetEquipmentTraitRollService();
            _test.True(
                !ReferenceEquals(afterRefresh, first),
                "facade should rebuild equipment trait roller when catalog revision changes."
            );

            runtime.Setup(otherSession);
            EquipmentTraitRollService afterSessionChange = runtime.GetEquipmentTraitRollService();
            _test.True(
                !ReferenceEquals(afterSessionChange, afterRefresh),
                "facade should rebuild equipment trait roller when setup binds another session."
            );
        }
        finally
        {
            runtime.Dispose();
            CleanupGameSession(gameSession);
            CleanupGameSession(otherSession);
        }
    }

    private void TestRuntimeFacadeDiscardsStaleContentCatalog()
    {
        GameSession gameSession = new();
        GameRuntimeFacade runtime = new();
        try
        {
            GameContentCatalog realCatalog = gameSession.GetContentCatalogTyped();
            runtime.Setup(gameSession);
            _test.True(
                ReferenceEquals(runtime.GetContentCatalogTyped(), realCatalog),
                "facade setup 后应解析到 session 当前 content catalog。"
            );

            // 注入一个未绑定 session 的 stale catalog，模拟 facade 长期持有的已失效引用，
            // GetContentCatalogTyped 应丢弃它并重新解析回 session 当前 catalog。
            GameContentCatalog staleCatalog = new();
            runtime.SetContentCatalogState(staleCatalog);
            _test.True(
                !staleCatalog.HasSessionTyped(),
                "构造的 stale catalog 应未绑定 session。"
            );

            GameContentCatalog resolved = runtime.GetContentCatalogTyped();
            _test.True(
                ReferenceEquals(resolved, realCatalog),
                "facade 不应返回未绑定的 stale catalog，应重新解析到 session 当前 catalog。"
            );
            _test.True(
                !ReferenceEquals(resolved, staleCatalog),
                "facade 应丢弃注入的 stale catalog。"
            );
            _test.True(
                runtime.GetContentCatalogTyped().HasSessionTyped(),
                "facade 重新解析出的 catalog 应绑定 session。"
            );
        }
        finally
        {
            runtime.Dispose();
            CleanupGameSession(gameSession);
        }
    }

    private void TestRuntimeFacadeDiscardsCatalogBoundToOtherSession()
    {
        GameSession gameSession = new();
        GameSession otherSession = new();
        GameRuntimeFacade runtime = new();
        try
        {
            GameContentCatalog realCatalog = gameSession.GetContentCatalogTyped();
            runtime.Setup(gameSession);
            _test.True(
                ReferenceEquals(runtime.GetContentCatalogTyped(), realCatalog),
                "facade setup 后应解析到 session 当前 content catalog。"
            );

            // 注入一个仍绑定“其他 session”的 catalog：它 HasSessionTyped() 为 true，但绑定的
            // 并不是 facade 当前的 gameSession。facade 不能因为它“看起来仍有效”就复用，
            // 应识别出绑定的是别的 session 并重新解析回当前 session 的 catalog。
            GameContentCatalog foreignCatalog = otherSession.GetContentCatalogTyped();
            runtime.SetContentCatalogState(foreignCatalog);
            _test.True(
                foreignCatalog.HasSessionTyped(),
                "注入的 foreign catalog 应仍绑定它自己的 session（证明不是 unbound stale 特例）。"
            );
            _test.True(
                !foreignCatalog.IsBoundToSession(gameSession),
                "foreign catalog 不应绑定 facade 当前的 gameSession。"
            );

            GameContentCatalog resolved = runtime.GetContentCatalogTyped();
            _test.True(
                ReferenceEquals(resolved, realCatalog),
                "facade 不应复用绑定其他 session 的 catalog，应重新解析回当前 session 的 catalog。"
            );
            _test.True(
                !ReferenceEquals(resolved, foreignCatalog),
                "facade 应丢弃绑定其他 session 的 foreign catalog。"
            );
            _test.True(
                resolved.IsBoundToSession(gameSession),
                "facade 重新解析出的 catalog 应绑定当前 gameSession。"
            );
        }
        finally
        {
            runtime.Dispose();
            CleanupGameSession(gameSession);
            CleanupGameSession(otherSession);
        }
    }

    private static ItemDef BuildProbeItemDef(StringName itemId)
    {
        return new ItemDef
        {
            item_id = itemId,
            display_name = "Catalog Regression Probe",
            is_stackable = true,
            item_category = "material",
        };
    }

    private static SkillDefinition BuildProbeSkillDefinition(StringName skillId)
    {
        return new SkillDefinition(
            skillId,
            "Catalog Regression Probe Skill",
            "",
            "",
            "passive",
            1,
            1,
            "",
            0,
            0,
            Array.Empty<int>(),
            Array.Empty<StringName>(),
            "",
            Array.Empty<StringName>(),
            "",
            Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            false,
            "",
            Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, Variant>>(),
            null
        );
    }

    private void CleanupGameSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.DisposeOwnedRuntimeResources();
        gameSession.Dispose();
    }

}
