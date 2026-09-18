# 战斗地图呈现

更新日期：2026-09-17

## 展示边界

`BattleMapPanel` 把地图容器的逻辑尺寸换算为物理像素 SubViewport；3840 × 2160 全屏使用 1920 × 1080 的 HUD 逻辑画布，棋盘继续按原生物理像素绘制。相机缩放与鼠标拾取共同消费 `BattleBoardRenderProfile` 的格子尺寸、高度步长和表面形状。

棋盘只消费 detached `BattleBoardRenderSnapshot`，地形、通行、目标合法性和单位战斗状态仍由 runtime 拥有。以下样式不改变格子坐标、占位、存档或输入信号。

## 战场优先的 HUD 布局

- `MapFrame` 和物理 SubViewport 铺满战斗面板；`TopBar` / `BottomPanel` 为透明布局容器。资源、状态和技能栏高度不再回写地图边界，因此它们变化时不改变棋盘分辨率和相机焦点。空白布局区域忽略鼠标，实际操作面板、按钮和技能滚动区拦截自己的输入。
- 顶部保留目标进度、TU、行动队列、技能命运提示与模式 / 视角 / 结算入口；不再绘制横贯屏幕的底板。重复标题、READY 数量与缩放倍数不常驻显示，READY 信息留在 TU 悬停提示中，视角操作说明留在按钮提示中。
- 底部角色卡和技能栏固定从左下角向右排列，按内容收紧，最大逻辑宽度 1360，距左、下边缘各 12；操作提示跟随底栏宽度放在其上方。角色卡把 HP、体力、MP、斗气数值放在各自资源条内；资源可见性继续消费 typed snapshot。姓名悬停包含角色说明。
- 技能图标保持 72 像素。`SkillScroll` 只显示最多两行，多余技能滚动查看；空槽不创建节点，点击继续提交原始槽位索引。没有技能或可用操作时隐藏技能面板，空集合清除旧滚动位置。
- 取消和变体按钮按 `CommandDock` 的可用状态出现，多目标计数沿用当前快照。单行操作提示悬浮在底栏上方；技能摘要、完整预览、屏障摘要与操作说明保留在技能面板悬停提示中，不持续增加底栏高度。
- 当前单位常驻显示前两个状态名称 / 层数，其余合为 `+N`；持续时间、来源数与完整说明保留在相应悬停提示里。状态数量不会无限增加角色卡高度。
- `RuntimeLogDock` 进入战斗时自动折叠成 72 × 36 的日志入口，点击恢复完整日志，再点击收回；世界态继续使用原有日志标题和透明度按钮。折叠 / 展开沿用 `panel_layout_changed`，容器最小尺寸更新后再次通知宿主布局。

所有状态仍由 `BattleHudAdapter` 投影；本节布局不增加战斗规则、持久化字段或存档迁移。场景中的节点和脚本已完成接线，无需在编辑器补接信号。

高度范围由 `BattleCellState` 的 -5～8 层常量拥有，`BattleGridService` 与 `BattleBoardRenderProfile` 共用该范围。场景为全部有符号高度提供地面、墙、效果和 marker 层；数组下标与真实高度分开转换，地面顶点、单位、道具、相机边界和鼠标拾取都使用真实高度。`BattleEdgeService` 的内部落差岩壁连续跨过零层，不在零层截断。

正式地形技能执行时，`BattleGroundEffectService` 经 grid owner 改变高度 / 基础地形、同步 cell columns、标记 edge cache dirty，并把改变的坐标写入 `BattleEventBatch`。`BattlePresentationDeltaFactory` 将其归为 FullBoard，展示重建 snapshot 后释放旧地形、树木、阴影与格线节点，再上传新地形数据；单位和标记随新高度重画。

## 地形材质

- 当前四个 terrain profile 共用 `canyon_iso64`，由 `BattleBoardRenderProfile.PaintedAssetDirectory` 指向 `assets/main/battle/terrain/canyon_painted/` 的手绘砂岩、岩壁、树木与碎石灌木。素材来源和完整生成提示见 `docs/content/ui/battle_canyon_art.md`。
- `BattleBoardController.TerrainArt` 接收同一 detached snapshot，按地图前后对角线及高度建立 `BattleTerrainPaintLayer` 的 retained polygon / stroke commands。顶部、岩壁、效果与 marker TileMap 保留格子占位和 source 记录；实际地面效果和 marker 使用借用相同 atlas texture、region、origin、modulate 与 material 的 Sprite2D，加入统一的前后绘制顺序。显示完成判定同时检查实际手绘表面数。
- 每个表面的顶点来自既有 `MapToLocal` 轴向与 profile 高度步长，UV 使用连续棋盘坐标。地面纹理跨格采样；同一岩壁沿横向和高度方向连续采样。视觉高度步长保持 80，本次没有改变 runtime 高度或通行规则。
- `battle_painted_ground.gdshader` 读取一张每格一个像素的 RGBA8 数据图：水域类型强度、森林、泥地、编码高度。普通水域 / 浅水为 0.8，流水为 0.9，深水独占 1.0；深水呈深蓝色且减弱水底细节，浅水保持明亮青绿并透出沙底。相邻同高度格平滑混合森林与浅滩，跨高度不混合；深浅水颜色依据所在格类型，不能把深水视觉边界模糊进可通行浅水格。水面动画仅在 shader 内使用 TIME，没有逐帧 CPU 地形遍历。
- 东向与南向岩面使用统一手绘岩石材质和不同光照色。`BattleTerrainGenerator` 的正式初始高度范围是 4～8 层，外围岩壁沿用原绘制的 0 层基底，最低 4 层地面必须保留完整四层厚度。底线不能随正高度最低格抬升；只有地面降到 0 或负高度时，视觉剖面才向下延伸到最低地面下一层，保持实体厚度。内部高差完整显示，技能运行时仍可在 -5～8 层范围升降；初始最低 4 层不限制技能下降。外围使用统一深青灰纯色 `#2B4447`，由 `BattleBoardRenderProfile.BackgroundColor` 供棋盘背景与面板 SubViewport 共用；纯色只替代地图之外的背景，地图地面、实体底座与高差岩壁继续绘制。
- 森林格生成树木节点与接地阴影，坐标散布、尺寸和颜色变化由已有确定性坐标 hash 决定。边缘装饰按 cell / edge snapshot 散布；unit delta 保留本代地形节点与数据图。
- 路径素材、shader 和静态材质由 `EngineAssetAccess.ResolveCodeAssetBorrowed` 借用。board 的单一 render-generation lease 额外持有一个可复用 `ImageTexture` 和一个共享 `ShaderMaterial`；上传使用 Request-domain Image 并当场关闭，重新 Configure 使用 `SetImage` 更新既有纹理。Clear / rebind / exit 先释放绘制节点，再关闭资源 owner。
- 新素材启用 mipmap，绘制节点使用 LinearWithMipmaps，在原生 4K 和缩小时维持稳定细节。没有屏幕取样或逐帧素材加载。

## 阻挡地形辨识

`BattleBoardSnapshotBuilder` 通过正式 `BattleEdgeService.IsTraversableBetween` 投影每条内部边的 `BlocksMovement`；不仅保留可见下落岩面，还包含背向镜头的两层以上上坡，以及没有实体渲染的阻挡边特征。展示不自行按高度阈值复制通行规则。

`BattleBoardController.Obstacles` 把橙红断行边线画在这条边两侧的地面内沿，配深色底线和短横刻度；前后视角都能看出哪些边不能跨过。一层可通行台阶不添加该提示。提示使用各自地面绘制段的格线以上、树木以下位置；不会铺满整个可行走高台或拦截输入。

临时墙使用原有手绘砂岩材质，从两侧较高的地面向上绘制墙身、端面、顶盖与砖缝，不再复用地面以下的落差岩壁。墙位于近侧地面格线以上、树木和单位以下。地形完整刷新会释放本代墙体与阻挡边线，临时墙消失或高差恢复可通行时清除旧提示。

深水与浅水使用明显不同的明度和颜色，不显示无条件禁止进入的标记：深水仍允许具备飞行 / 两栖能力的单位进入。森林和零散灌木碎石仍属于可通行地形及装饰；移动结果继续由 runtime 判定。本节不修改移动、寻路、技能或存档规则。

## 前后遮挡

`BattleBoardController.GetCellDrawDepth` 以等距地图的 `coord.X + coord.Y` 对角线分配前后绘制段，并以地图中心为零点。高度决定顶点和贴图向上偏移多少，不再决定整层压住哪些对象。同一段内依次绘制岩面、地面、岩缘、树影、地面效果、格线、地面标记、树木 / 灌木、单位 / 道具。后方高台不能切掉前方树冠，前方岩壁仍能遮挡后方树木和单位。`BattleBoard2D` 的重叠表面拾取使用同一前后对角线顺序。

绘制段由本代 detached snapshot 的地图大小和格子坐标确定，不按相机位置或单位移动重新分配。单位增量只更新人物节点；地形完整重建时释放旧树木、地面效果和标记。人物血量使用绝对 Z 4094，目标高亮使用 4095；地图外纯色背景使用最低绘制层。

## 战术标记

全部战斗格生成边界，包含水域和森林。`TacticalGridH{height}R{row}` 与手绘地面共享完整格子顶点和连续 UV；`battle_tactical_grid.gdshader` 按半整数坐标描边，用深色主线和浅色衬边分离沙地与水面。屏幕导数把线宽固定为物理像素，缩放时不随棋盘一起变粗或消失。格线贴在地面上，位于地面效果上方、地面标记及树木下方；树冠正常遮挡后方格线，地图外装饰不生成格线。该静态材质通过 engine asset owner 借用，格线随地形重建，unit delta 保留原节点。

`BattleBoardRenderProfile.GetMarkerMaterialPath` 按正式 source key 映射选中、可达、预览和目标出口材质。`battle_tactical_marker.gdshader` 用解析菱形边界和屏幕导数生成描边，不放大原 PNG 的像素边框。状态菱形贴近完整格子边界，使用深色衬线和半透明填色；选中为亮青色、较宽描边，可达为蓝色，预览为琥珀色，目标出口为绿色。命中徽标、目标合法性与 marker 坐标继续由 snapshot / runtime 提供。

## 单位呈现

### 普通移动

普通 `Move` 命令继续由 `BattleMovementService` 同步逐格结算通行、屏障、地形接触及费用。命令将 `ExecutedPath` 复制为不可变 `BattleMovementPresentation`，经 `BattleEventBatch` 和 `BattlePresentationDelta` 按执行顺序传到场景；起步即被阻挡的单点路径不生成播放，技能传送 / 跳跃等位移不使用这一普通移动通道。

`WorldMapSystem` 在刷新最终状态前启动 `BattleMapPanel.Movement`。`BattleBoardController.Movement` 使用命令前的 detached board snapshot、实际路径和单位完整 footprint 计算每格画面位置，沿相邻格逐段线性移动，每格约 0.14 秒；高差沿路径插值，深度随经过的格子更新。低帧率时限制单帧播放进度，每个中间格至少保留一帧，避免直接跨到终点。人物、脚底阴影和血条作为同一个 token 移动。

播放期间 panel 每帧请求 SubViewport 更新；`WorldMapRuntimeProxy` 暂停自动推进并拒绝后续操作，hover 查询暂停，场景刷新合并到播放结束。panel 的 C# 完成事件通知场景重新投影最终 runtime 状态，不在播放队列中保存 live battle state。移动费用和中途接触仍只由正式 runtime 结算一次。隐藏 / 重新配置棋盘、移除场景和 application shutdown 会清理播放与节点引用；未绑定场景的 headless runtime 保持同步命令行为。该事件由代码订阅与解绑，不需要编辑器接线。

### 人物悬停详情

鼠标移到人物身体或其脚下格子时，通过现有 `battle_cell_hovered` 信号显示详情；无需选中人物或技能，不改变当前选择。`BattleBoardController.Hover` 使用实际 Sprite2D 的变换、贴图不透明像素和 AtlasTexture margin 识别人物上半身，避免把背后的地格当作悬停目标。前方地面和岩壁仍会挡住后方人物的身体；没有贴图的单位使用现有文字棋子范围。

alpha 位图复用绘制阶段读取贴图主体范围的同一次 `Image` 读取，保存为 managed BitArray；鼠标移动不重新读图或持有额外 native Image。位图和既有贴图范围缓存一起释放。

`BattleHudAdapter` 从当前 battle unit 投影 detached 悬停数据：姓名 / 阵营、HP、已解锁 MP / 斗气、体力、行动点、移动点、六项基础属性，以及全部状态的名称、增益 / 减益、层数、剩余 TU 和独立来源说明。未解锁资源隐藏。基础属性直接来自单位 attribute snapshot；选中技能时继续保留原命中、伤害与 HP 预扣预览。

详情固定在本次悬停人物旁，并夹在屏幕及底部指令栏边界内，不随鼠标在人物内的小幅移动晃动。长状态列表在受限高度内滚动；鼠标进入详情或滚动条后保留面板，离开人物及详情后 0.2 秒收起，退出战斗立即清除。`WorldMapSystem` 在非纯日志的正式展示刷新后调用 `RefreshCurrentHover`，重读当前悬停对象的状态；单位移动或移除后重新拾取 / 清理，不把旧单位数据留在原格子。逐帧工作只处理面板位置与离开计时，不轮询战斗属性或命中预览。

玩家基础站姿使用 `assets/main/battle/units/player/basic_*.png`，由 typed engine asset catalog 登记为 `battle.unit.player.warrior`、`battle.unit.player.mage`、`battle.unit.player.archer`。`BattleBoardSnapshotBuilder` 在没有显式 `battle_sprite_asset_id` 且存在 `source_member_id` 时按当前 weapon family 选择显示外观：bow / crossbow 为弓箭手，staff 为法师，其余为战士。选择只写入 detached 展示快照；正式指定贴图优先，非队员单位继续消费自己的 asset ID。武器变化随同一 unit delta 更新外观，不写回角色、职业或存档。素材与完整提示见 `docs/content/ui/battle_player_art.md`。

`BattleUnitTokenDecoration` 是 draw-only Node2D，由 controller 创建到每个 token 下。它只接收阵营色、地面锚点、缩放后的双脚接触点、当前行动标记和是否绘制文字徽牌，不持有 runtime 或 Godot Resource。人物仅在贴图下方绘制两块小型鞋底接触阴影，随同一双脚标定移动；普通贴图和文字徽牌使用通用椭圆阴影。单位不绘制阵营光圈。

战士、法师、弓箭手、长枪战士、持盾战士、术士、刺客、狂战士和骑士九种外观各提供四个静态方向的 `AtlasTexture`，登记为 `battle.unit.player.{kind}.{front_left|front_right|back_left|back_right}`；方向按屏幕左前 / 右前 / 左后 / 右后定义。资源统一逻辑画布和主体底边锚点，具体 kind、切片及提示见素材文档。棋盘通过既有显式 asset ID / unit delta 消费这些资源，当前没有自动朝向状态；默认外观选择仍使用初版站姿。

无 sprite asset 的单位显示盾形文字徽牌；有正式 sprite asset 的单位保留原贴图，隐藏替代文字。39 个人物站姿资源使用 `BattleUnitSpriteTexture : AtlasTexture`，在逻辑画布内标定 `LeftFootContact` / `RightFootContact`，controller 将双脚接触点的中点对齐格子表面。武器宽度、袍角和前后脚的高度差不再决定人物的站位；普通 Texture2D 仍以不透明范围底边中点定位。血条顶部根据同一落点与不透明范围顶部计算。范围在每代 board 内按 texture 缓存为 plain `Rect2I`；首读的 `Image` 由 Request-domain `NativeLeaseScope` 当场关闭，unit delta 不重复扫描。血量信息层保持原有绝对 Z 排序，控件忽略鼠标。

sprite 主体范围以 alpha 至少为 0.5 的像素确定，排除柔化阴影与近透明边缘；整张图都低于该阈值时才使用非零 alpha 范围。无贴图单位的文字采用 30 个棋盘本地像素，血量文字为 16 个像素；相机最低 1.25 倍时分别为 37.5 和 20 个物理像素。数值信息只表达 snapshot，不参与命中或伤害计算。

`AtlasTexture.GetImage()` 返回裁取区域而不包含逻辑 margin；controller 为其主体范围加回 margin 偏移，与 Sprite2D 的实际绘制坐标一致。人物的双脚接触点直接保存为含 margin 的逻辑坐标。

人物与动物共用等比缩放：保留原画布宽度上限，同时将不透明主体高度限制为格子宽度的 0.8 倍，避免竖版人物覆盖多行地图。贴图与血条使用同一缩放函数。基础人物导入设置生成 mipmap，Sprite2D 使用 LinearWithMipmaps。

## 验证入口

- `tests/world_map/runtime/run_battle_movement_presentation_regression.cs`：受控战斗前置后，通过真实鼠标点击发起多格绕路，逐帧检查全部中间格、线段插值、费用、播放期间的输入 / 时间推进门禁、最终自动刷新、批次顺序及隐藏 / 重建 / 退出清理。包含 720p / 4K 原生交互；`MAGIC_BATTLE_MOVEMENT_CAPTURE_DIR` 可保存起点、途中与终点截图。另由 presentation delta 回归验证路径独立复制 / 合并顺序，barrier move cost 回归验证只发布实际走过的路径。这些是聚焦场景 / runtime 回归，不等同完整玩家旅程或全量回归。

- `tests/world_map/runtime/run_battle_unit_hover_details_regression.cs`：真实鼠标移动到人物上半身，验证友敌详情切换、资源与状态、鼠标静止时 unit delta 更新、移入详情滚动及移出关闭。原生 720p / 4K 检查面板边界；`MAGIC_BATTLE_HUD_CAPTURE_DIR` 可保存画面。使用受控战斗前置，不等同完整应用玩家旅程。

- `tests/battle_runtime/rendering/run_battle_player_sprite_regression.cs`：三种默认玩家贴图实际加载、透明底、显式素材优先、无 runtime 回写、detached 快照与换装 delta，以及九种外观共 36 个方向帧的实际切换、统一画布、脚底标定像素与格子表面 / 接触阴影对齐；同一人物在 -2、3、0 层重建后继续校验落点。原生渲染时可通过绝对路径 `MAGIC_BATTLE_SPRITE_CAPTURE_DIR` 保存默认站姿和三组三人四方向的 720p / 4K 棋盘截图，并在两种分辨率下重做落点断言。
- `tests/battle_runtime/rendering/run_battle_board_regression.cs`：生成地形、实际手绘表面与常驻格线覆盖、格线中心与拾取对齐及森林层级、单位渲染、4K/720p 地图尺寸下的文字可读性和高低差拾取、unit delta 从文字棋子切换到正式 sprite。
- `tests/battle_runtime/rendering/run_battle_tree_occlusion_regression.cs`：固定森林与高台的前后遮挡、森林格拾取、标记材质，以及原生 720p / 4K 下正常、负高度与最高层场景的树冠像素比对；额外升起前方岩壁，验证正常遮挡仍存在。`MAGIC_BATTLE_TREE_CAPTURE_DIR` 可保存两种分辨率的原生截图；headless 明确跳过像素断言。
- `tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs`：材质借用、重复 redraw、unit delta 不重建手绘地形、clear/rebind/exit 的 owner 回收。
- `tests/battle_runtime/rendering/run_battle_board_terrain_mutation_regression.cs`：正式升墙术 / 化石为泥命令、付费与 FullBoard 信号、连续升降穿过零层并到达 -5 / 8 层、地面 / 格线 / 单位 / marker / 拾取同步、森林升高及替换清理、重复重建 owner 不累积。该入口是 runtime 命令与渲染集成测试，不是技能栏鼠标操作的 application E2E。
- `tests/world_map/runtime/run_battle_height_skill_presentation_regression.cs`：正式生成地图的初始 4～8 层范围、完整 0 层基底；随后在受控最低 4 层地形上通过真实 HUD 输入完成 4→7→逐层降到 -1，检查输入后的 runtime、高度顶点、岩壁与拾取自动更新。原生 720p / 4K 均覆盖，`MAGIC_BATTLE_HUD_CAPTURE_DIR` 可保存画面。
- `tests/battle_runtime/terrain/run_battle_edge_face_service_regression.cs`：包含跨零层岩壁、负高度落差以及地形回升后旧岩壁消失。
- `tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs`：4K 物理 SubViewport 与实际鼠标点击，以及缩回 720p 的相机焦点。

视觉验收需使用原生渲染器检查 3840 × 2160 全屏与 1280 × 720 窗口。Headless 断言不证明 shader 的像素效果。
