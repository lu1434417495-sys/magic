# 战斗地图呈现

更新日期：2026-09-13

## 展示边界

`BattleMapPanel` 把地图容器的逻辑尺寸换算为物理像素 SubViewport；3840 × 2160 全屏使用 1920 × 1080 的 HUD 逻辑画布，棋盘继续按原生物理像素绘制。相机缩放与鼠标拾取共同消费 `BattleBoardRenderProfile` 的格子尺寸、高度步长和表面形状。

棋盘只消费 detached `BattleBoardRenderSnapshot`，地形、通行、目标合法性和单位战斗状态仍由 runtime 拥有。以下样式不改变格子坐标、占位、存档或输入信号。

高度范围由 `BattleCellState` 的 -5～8 层常量拥有，`BattleGridService` 与 `BattleBoardRenderProfile` 共用该范围。场景为全部有符号高度提供地面、墙、效果和 marker 层；数组下标与真实高度分开转换，地面顶点、单位、道具、相机边界和鼠标拾取都使用真实高度。`BattleEdgeService` 的内部落差岩壁连续跨过零层，不在零层截断。

正式地形技能执行时，`BattleGroundEffectService` 经 grid owner 改变高度 / 基础地形、同步 cell columns、标记 edge cache dirty，并把改变的坐标写入 `BattleEventBatch`。`BattlePresentationDeltaFactory` 将其归为 FullBoard，展示重建 snapshot 后释放旧地形、树木、阴影与格线节点，再上传新地形数据；单位和标记随新高度重画。

## 地形材质

- 当前四个 terrain profile 共用 `canyon_iso64`，由 `BattleBoardRenderProfile.PaintedAssetDirectory` 指向 `assets/main/battle/terrain/canyon_painted/` 的手绘砂岩、岩壁、树木与碎石灌木。素材来源和完整生成提示见 `docs/content/ui/battle_canyon_art.md`。
- `BattleBoardController.TerrainArt` 接收同一 detached snapshot，按高度建立 `BattleTerrainPaintLayer` 的 retained polygon / stroke commands。顶部、岩壁 TileMap 保留格子占位供统一坐标表示，但不再显示其小块贴图；显示完成判定同时检查实际手绘表面数。props、terrain effect overlay 和 marker 仍由原层管理。
- 每个表面的顶点来自既有 `MapToLocal` 轴向与 profile 高度步长，UV 使用连续棋盘坐标。地面纹理跨格采样；同一岩壁沿横向和高度方向连续采样。视觉高度步长保持 80，本次没有改变 runtime 高度或通行规则。
- `battle_painted_ground.gdshader` 读取一张每格一个像素的 RGBA8 数据图：水域强度、森林、泥地、编码高度。相邻同高度格平滑混合森林与浅滩，跨高度不混合；水域具有细流纹与水岸亮边。水面动画仅在 shader 内使用 TIME，没有逐帧 CPU 地形遍历。
- 东向与南向岩面使用统一手绘岩石材质和不同光照色。内部高度差完整显示，只有地图外围的剖面截到全图最低高度下一层，下面延展低对比环境地表。环境碎石和灌木完全是绘制装饰，不加入 runtime cell、可达范围或命中集合。
- 森林格生成树木节点与接地阴影，坐标散布、尺寸和颜色变化由已有确定性坐标 hash 决定。边缘装饰按 cell / edge snapshot 散布；unit delta 保留本代地形节点与数据图。
- 路径素材、shader 和静态材质由 `EngineAssetAccess.ResolveCodeAssetBorrowed` 借用。board 的单一 render-generation lease 额外持有一个可复用 `ImageTexture` 和一个共享 `ShaderMaterial`；上传使用 Request-domain Image 并当场关闭，重新 Configure 使用 `SetImage` 更新既有纹理。Clear / rebind / exit 先释放绘制节点，再关闭资源 owner。
- 新素材启用 mipmap，绘制节点使用 LinearWithMipmaps，在原生 4K 和缩小时维持稳定细节。没有屏幕取样或逐帧素材加载。

## 战术标记

全部战斗格常驻显示边界，包含水域和森林。`TacticalGridH` 与手绘地面共享完整格子顶点和连续 UV；`battle_tactical_grid.gdshader` 按半整数坐标描边，用深色主线和浅色衬边分离沙地、水面与树冠。屏幕导数把线宽固定为物理像素，缩放时不随棋盘一起变粗或消失。格线层位于同高度的树木 / 地形效果上方、行动标记和单位下方，高地继续遮挡低地；地图外装饰不生成格线。该静态材质通过 engine asset owner 借用，格线随地形重建，unit delta 保留原节点。

`BattleBoardRenderProfile.GetMarkerMaterialPath` 按正式 source key 映射选中、可达、预览和目标出口材质。`battle_tactical_marker.gdshader` 用解析菱形边界和屏幕导数生成描边，不放大原 PNG 的像素边框。状态菱形贴近完整格子边界，使用深色衬线和半透明填色；选中为亮青色、较宽描边，可达为蓝色，预览为琥珀色，目标出口为绿色。命中徽标、目标合法性与 marker 坐标继续由 snapshot / runtime 提供。

## 单位呈现

玩家基础站姿使用 `assets/main/battle/units/player/basic_*.png`，由 typed engine asset catalog 登记为 `battle.unit.player.warrior`、`battle.unit.player.mage`、`battle.unit.player.archer`。`BattleBoardSnapshotBuilder` 在没有显式 `battle_sprite_asset_id` 且存在 `source_member_id` 时按当前 weapon family 选择显示外观：bow / crossbow 为弓箭手，staff 为法师，其余为战士。选择只写入 detached 展示快照；正式指定贴图优先，非队员单位继续消费自己的 asset ID。武器变化随同一 unit delta 更新外观，不写回角色、职业或存档。素材与完整提示见 `docs/content/ui/battle_player_art.md`。

`BattleUnitTokenDecoration` 是 draw-only Node2D，由 controller 创建到每个 token 下。它只接收阵营色、脚底位置、当前行动标记和是否绘制文字徽牌，不持有 runtime 或 Godot Resource。

三种玩家外观各提供四个静态方向的 `AtlasTexture`，登记为 `battle.unit.player.{kind}.{front_left|front_right|back_left|back_right}`；方向按屏幕左前 / 右前 / 左后 / 右后定义。资源统一逻辑画布和主体底边锚点，具体切片及提示见素材文档。棋盘通过既有显式 asset ID / unit delta 消费这些资源，当前没有自动朝向状态；默认外观选择仍使用初版站姿。

所有单位显示脚底阴影和阵营环；当前行动单位使用金色环。无 sprite asset 的单位显示盾形文字徽牌；有正式 sprite asset 的单位保留原贴图，隐藏替代文字。sprite 的不透明范围底边对齐原有地面锚点，血条贴近不透明范围顶部，避免 PNG 留白把脚底和血量信息推离模型。范围在每代 board 内按 texture 缓存为 plain `Rect2I`；首读的 `Image` 由 Request-domain `NativeLeaseScope` 当场关闭，unit delta 不重复扫描。血量信息层保持原有绝对 Z 排序，控件忽略鼠标。

sprite 主体范围以 alpha 至少为 0.5 的像素确定，排除柔化阴影与近透明边缘；整张图都低于该阈值时才使用非零 alpha 范围。无贴图单位的文字采用 30 个棋盘本地像素，血量文字为 16 个像素；相机最低 1.25 倍时分别为 37.5 和 20 个物理像素。数值信息只表达 snapshot，不参与命中或伤害计算。

`AtlasTexture.GetImage()` 返回裁取区域而不包含逻辑 margin；controller 为其主体范围加回 margin 偏移，与 Sprite2D 的实际绘制坐标一致，因此图集方向帧的脚底和血条不会因留白而错位。

人物与动物共用等比缩放：保留原画布宽度上限，同时将不透明主体高度限制为格子宽度的 0.8 倍，避免竖版人物覆盖多行地图。贴图与血条使用同一缩放函数。基础人物导入设置生成 mipmap，Sprite2D 使用 LinearWithMipmaps。

## 验证入口

- `tests/battle_runtime/rendering/run_battle_player_sprite_regression.cs`：三种玩家贴图实际加载、透明底、显式素材优先、无 runtime 回写、detached 快照与换装 delta，以及 12 个方向帧的实际切换、统一画布与脚底圆环对齐。原生渲染时可通过绝对路径 `MAGIC_BATTLE_SPRITE_CAPTURE_DIR` 保存默认站姿和四方向的 720p / 4K 棋盘截图。
- `tests/battle_runtime/rendering/run_battle_board_regression.cs`：生成地形、实际手绘表面与常驻格线覆盖、格线中心与拾取对齐及森林层级、单位渲染、4K/720p 地图尺寸下的文字可读性和高低差拾取、unit delta 从文字棋子切换到正式 sprite。
- `tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs`：材质借用、重复 redraw、unit delta 不重建手绘地形、clear/rebind/exit 的 owner 回收。
- `tests/battle_runtime/rendering/run_battle_board_terrain_mutation_regression.cs`：正式升墙术 / 化石为泥命令、付费与 FullBoard 信号、连续升降穿过零层并到达 -5 / 8 层、地面 / 格线 / 单位 / marker / 拾取同步、森林升高及替换清理、重复重建 owner 不累积。该入口是 runtime 命令与渲染集成测试，不是技能栏鼠标操作的 application E2E。
- `tests/battle_runtime/terrain/run_battle_edge_face_service_regression.cs`：包含跨零层岩壁、负高度落差以及地形回升后旧岩壁消失。
- `tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs`：4K 物理 SubViewport 与实际鼠标点击，以及缩回 720p 的相机焦点。

视觉验收需使用原生渲染器检查 3840 × 2160 全屏与 1280 × 720 窗口。Headless 断言不证明 shader 的像素效果。
