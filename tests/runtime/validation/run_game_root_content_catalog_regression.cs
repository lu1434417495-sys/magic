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
        TestContentCatalogCachesBoundSnapshot();
        TestContentCatalogReturnsDefensiveReadOnlyViews();
        TestContentCatalogIdentityCatalogReturnsDefensiveReadOnlyViews();
        TestContentCatalogInvalidatedAfterSessionDispose();
        TestRuntimeFacadeBindsUnifiedRootBeforeWorldSetup();
        TestRuntimeFacadeReadsBoundSyntheticCatalog();
        TestRuntimeFacadeRebuildsEquipmentTraitRollerWhenSessionChanges();
        TestRuntimeFacadeDiscardsStaleContentCatalog();
        TestRuntimeFacadeDiscardsCatalogBoundToOtherSession();

        RequestTestExit(_test.Finish("Game root content catalog regression"));
    }

    private void TestGameSessionOwnsUnifiedRootAndContentCatalog()
    {
        StringName fakeSkillId = "regression_fake_skill";
        int baselineSkillCount = GameSessionTestFactory.GetProcessSnapshot().Skills.Count;
        GameSession gameSession = CreateSyntheticSessionWithSkill(fakeSkillId);
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
                gameSession.GetBarrierProfileDefinitionsTyped().Count,
                catalog.GetBarrierProfileDefinitionsTyped().Count,
                "content catalog barrier profile definitions 应与 GameSession 正式内容缓存一致。"
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
                catalog.GetBattleSpecialProfileView() != null,
                "content catalog 应提供 battle special profile typed view 边界。"
            );

            _test.Eq(
                catalog.GetSkillDefinitionsTyped().Count,
                baselineSkillCount + 1,
                "synthetic snapshot 应在 session bind 前加入一个 fake skill。"
            );
            _test.True(
                catalog.GetSkillDefinitionsTyped().ContainsKey(fakeSkillId),
                "绑定后的 content catalog skill definition 快照应包含 fake skill id。"
            );
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestContentCatalogCachesBoundSnapshot()
    {
        int baselineItemCount = GameSessionTestFactory.GetProcessSnapshot().Items.Count;
        GameSession gameSession = CreateSyntheticSessionWithItem(CatalogProbeItemId);
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
            // Session 与 catalog 共同借用同一份 immutable snapshot，不做运行期重建。
            _test.True(
                ReferenceEquals(
                    gameSession.GetSkillDefinitionsTyped(),
                    gameSession.GetSkillDefinitionsTyped()
                ),
                "GameSession typed getter 应稳定借用同一个 immutable skill index。"
            );
            // battle special profile view 返回不可变深副本，而不是可变字典别名。
            IBattleSpecialProfileView specialProfileView =
                catalog.GetBattleSpecialProfileView();
            _test.True(
                specialProfileView.TryGetMeteorSwarmProfile(
                    "meteor_swarm",
                    out MeteorSwarmProfileData meteorProfileA
                ),
                "battle special profile typed view 应包含 meteor_swarm。"
            );
            _test.True(
                specialProfileView.TryGetMeteorSwarmProfile(
                    "meteor_swarm",
                    out MeteorSwarmProfileData meteorProfileB
                ),
                "battle special profile typed view 应支持重复只读查询。"
            );
            _test.True(
                meteorProfileA != null
                    && meteorProfileB != null
                    && !ReferenceEquals(meteorProfileA, meteorProfileB),
                "battle special profile typed view 应返回深只读副本。"
            );

            _test.True(
                catalog.GetItemDefsTyped().ContainsKey(CatalogProbeItemId),
                "content catalog 应暴露 bind 前写入 synthetic snapshot 的探针物品。"
            );
            _test.Eq(
                catalog.GetItemDefsTyped().Count,
                baselineItemCount + 1,
                "synthetic snapshot item index 应比 process baseline 多一个条目。"
            );
            _test.True(
                ReferenceEquals(catalog.GetItemDefsTyped(), gameSession.GetItemDefsTyped()),
                "content catalog 与 GameSession 应借用同一个 immutable item index。"
            );
            _test.True(
                ReferenceEquals(gameSession.GetContentCatalogTyped(), catalog),
                "session bind 后应稳定返回同一个 content catalog 实例。"
            );
            _test.True(catalog.GetRevision() > 0, "snapshot bind 应建立正数 catalog revision。");
        }
        finally
        {
            CleanupGameSession(gameSession);
        }
    }

    private void TestContentCatalogReturnsDefensiveReadOnlyViews()
    {
        GameSession gameSession = CreateSyntheticSessionWithSkill(DefensiveProbeSkillId);
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (catalog == null)
                return;

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

            IReadOnlyDictionary<StringName, TraitDefinition> traitView =
                catalog.GetTraitDefsTyped();
            _test.True(
                traitView as Dictionary<StringName, TraitDefinition> == null,
                "typed trait getter 不应可被 downcast 成内部可变 Dictionary。"
            );
            int traitCountBefore = catalog.GetTraitDefsTyped().Count;
            bool traitMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, TraitDefinition>)traitView).Clear();
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
            _test.True(
                RejectsMutation(gameSession.GetProfessionDefsTyped()),
                "GameSession profession definition snapshot 应拒绝写入。"
            );
            _test.True(
                RejectsMutation(gameSession.GetAchievementDefsTyped()),
                "GameSession achievement definition snapshot 应拒绝写入。"
            );
            _test.True(
                RejectsMutation(gameSession.GetQuestDefsTyped()),
                "GameSession quest definition snapshot 应拒绝写入。"
            );

            IReadOnlyDictionary<StringName, ItemDefinition> itemView =
                catalog.GetItemDefsTyped();
            _test.True(
                itemView as Dictionary<StringName, ItemDefinition> == null,
                "typed item getter 不应可被 downcast 成内部可变 Dictionary。"
            );
            int itemCountBefore = catalog.GetItemDefsTyped().Count;
            bool itemMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, ItemDefinition>)itemView).Clear();
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
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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

            IReadOnlyDictionary<StringName, RaceDefinition> raceView = identityCatalog.RaceDefs;
            _test.True(raceView != null, "identity catalog 应暴露 race defs 只读视图。");
            if (raceView == null)
                return;

            // identity catalog 的 race 视图同样是防御性只读包装：downcast 成内部可变 Dictionary 应失败，
            // 否则下游可绕过只读约束改写 catalog 暴露的 identity snapshot。
            _test.True(
                raceView as Dictionary<StringName, RaceDefinition> == null,
                "identity catalog race getter 不应可被 downcast 成内部可变 Dictionary。"
            );

            int raceCountBefore = identityCatalog.RaceDefs.Count;
            bool raceMutationBlocked = false;
            try
            {
                ((IDictionary<StringName, RaceDefinition>)raceView).Clear();
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
        GameSession gameSession = CreateSyntheticSessionWithItem(DisposeProbeItemId);
        GameContentCatalog catalog = null;
        bool runtimeResourcesDisposed = false;
        try
        {
            catalog = gameSession.GetContentCatalogTyped();
            _test.True(catalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (catalog == null)
                return;

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
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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

    private void TestRuntimeFacadeReadsBoundSyntheticCatalog()
    {
        GameSession gameSession = CreateSyntheticSessionWithItem(FacadeProbeItemId);
        GameRuntimeFacade runtime = new();
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            runtime.Setup(gameSession);

            GameContentCatalog runtimeCatalog = runtime.GetContentCatalogTyped();
            _test.True(
                ReferenceEquals(runtimeCatalog, catalog),
                "GameRuntimeFacade 应解析到 GameSession 当前的 content catalog 实例。"
            );
            _test.True(
                runtimeCatalog.GetItemDefsTyped().ContainsKey(FacadeProbeItemId),
                "GameRuntimeFacade 读取的 catalog 应包含 bind 前写入 snapshot 的物品。"
            );
        }
        finally
        {
            runtime.Dispose();
            CleanupGameSession(gameSession);
        }
    }

    private void TestRuntimeFacadeRebuildsEquipmentTraitRollerWhenSessionChanges()
    {
        GameSession gameSession = CreateSyntheticSessionWithItem(RollerProbeItemId);
        GameSession otherSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameRuntimeFacade runtime = new();
        try
        {
            runtime.Setup(gameSession);
            EquipmentTraitRollService first = runtime.GetEquipmentTraitRollService();
            _test.True(first != null, "facade should build an equipment trait roller for a session.");

            _test.True(
                runtime.GetContentCatalogTyped().GetItemDefsTyped().ContainsKey(RollerProbeItemId),
                "synthetic session catalog should contain the roller probe item at bind time."
            );

            runtime.Setup(otherSession);
            EquipmentTraitRollService afterSessionChange = runtime.GetEquipmentTraitRollService();
            _test.True(
                !ReferenceEquals(afterSessionChange, first),
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
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        GameSession otherSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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

    private static bool RejectsMutation<T>(IReadOnlyDictionary<StringName, T> snapshot)
    {
        try
        {
            ((IDictionary<StringName, T>)snapshot).Clear();
            return false;
        }
        catch (NotSupportedException)
        {
            return true;
        }
    }

    private static GameSession CreateSyntheticSessionWithSkill(StringName skillId)
    {
        SkillDefinition definition = BuildProbeSkillDefinition(skillId);
        return GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed => seed.Skills = CopyWithEntry(seed.Skills, skillId, definition)
        );
    }

    private static GameSession CreateSyntheticSessionWithItem(StringName itemId)
    {
        ItemDefinition definition = BuildProbeItemDefinition(itemId);
        return GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed => seed.Items = CopyWithEntry(seed.Items, itemId, definition)
        );
    }

    private static IReadOnlyDictionary<StringName, T> CopyWithEntry<T>(
        IReadOnlyDictionary<StringName, T> source,
        StringName key,
        T value
    )
        where T : class
    {
        var copy = source == null
            ? new Dictionary<StringName, T>()
            : new Dictionary<StringName, T>(source);
        copy[key] = value ?? throw new ArgumentNullException(nameof(value));
        return copy;
    }

    private static ItemDefinition BuildProbeItemDefinition(StringName itemId)
    {
        return new ItemDefinition(
            itemId,
            "",
            "Catalog Regression Probe",
            "",
            "",
            true,
            0,
            0,
            0,
            true,
            99,
            "material",
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitRollGroupDefinition>(),
            Array.Empty<string>(),
            Array.Empty<AttributeModifierDefinition>(),
            "",
            Array.Empty<string>(),
            null,
            "",
            null,
            -1
        );
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
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
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
