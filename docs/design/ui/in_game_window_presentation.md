# 游戏功能弹窗呈现

更新日期：2026-09-12

## 范围与显示边界

游戏功能窗口继续以居中弹窗覆盖当前场景。世界地图、战斗棋盘或登录场景保留在后方，窗口只通过半透明 Shade 降低背景亮度。插画限于面板右上角，最大 320 × 96 逻辑像素，不承担全屏背景职责。

以下 14 个场景根引用 `scenes/ui/styles/chronicle_theme.tres`：

- 队伍、共享仓库、据点、商店 / 锻造 / 驿站 / 合约服务。
- 人物信息、触发术、转职、成长奖励。
- 悬赏、NPC 委托、区域进入确认。
- 存档列表、世界预设、显示设置。

`BattleMapPanel.Equipment.cs` 动态创建的战中背包使用同一主题与局部装饰。角色创建界面继续由独立的 `character_creation_theme.tres` 和 `CharacterCreationWindow.Appearance.cs` 拥有，见 [角色创建呈现](character_creation_presentation.md)。

## 呈现 owner

| 文件 | 当前职责 |
| --- | --- |
| `scenes/ui/styles/chronicle_theme.tres` | 深色正文、象牙白文字、金色选中态、标题字体、按钮、列表、标签页、输入控件、滚动条 |
| `scenes/ui/styles/chronicle_panel.tres` | 面板填充、细边、阴影与圆角 |
| `scripts/ui/components/ModalWindowShell.cs` | 原有遮罩 / Escape 关闭语义；读取场景 `HeaderArtwork`，挂载装饰，管理 0.18 秒面板淡入 |
| `scripts/ui/components/ChronicleWindowDecoration.cs` | 仅在面板内部绘制角饰与标题插画；所有装饰节点忽略鼠标，父节点裁剪内容 |
| `scenes/ui/styles/chronicle_header_material.tres`、`assets/ui/windows/header_vignette.gdshader` | 路径资源形式共享的插画边缘淡出材质 |
| `scripts/ui/components/UiListTheme.cs` | 动态 ItemList 与场景列表复用相同选中、悬停和键盘焦点样式 |
| `scripts/ui/components/SelectionCardBuilder.cs` | 转职等选择卡片的薄底线、选中侧线、低对比正文底色 |

场景仍保留根 `Shade` 和 `CenterContainer/Panel` 路径。`HeaderArtwork` 是脚本导出属性，在 `.tscn` 中写于 `script` 绑定之后。装饰作为 PanelContainer 的首个子节点参与填充，实际内容在其后绘制。淡入仅改变面板透明度，不延迟按钮输入；隐藏与离树时取消 tween。

`ChronicleTitle` 使用衬线标题，`ChronicleSection` 用于区块标题，`ChroniclePrimary` 表示提交动作，`ChronicleQuiet` 用于关闭等次要动作。主题绑定在窗口，不修改项目全局 Theme。

## 输入和数据边界

商店交易列表在文字左侧保留 48 × 48 逻辑像素的物品图标位。`SettlementShopService` 将买入与卖出条目的 `ItemDefinition.IconAssetId` 投影到 detached 条目，`ShopWindow` 通过 `EngineAssetAccess` 借用对应纹理；图标为空的商品使用场景绑定的 `shop_item_empty.svg` 空槽。图标只参与显示，不进入交易请求或存档；共用窗口的非商品条目不显示物品空槽。

窗口的确认、取消、保存、换装与服务请求仍由原有 signal / typed C# event 提交。装饰组件不持有业务数据，不发布 gameplay signal，不改变必选奖励的关闭限制。

战中背包根路径仍为 `HudRoot/BattleEquipmentOverlay`。其 `ModalCanvas`（CanvasLayer 20）拥有遮罩和居中面板，面板路径为 `ModalCanvas/BattleEquipmentCenter/BattleEquipmentPanel`。Canvas 的可见性跟随弹窗在树中的可见性；关闭背包或隐藏战斗时一起隐藏。独立 canvas 同时确定绘制和 GUI 输入优先级，避免后加入的 RuntimeLogDock 拦截前方关闭按钮；单独提高 ZIndex 不能解决输入排序。

`WorldMapSystem` 通过 `ContingencySetupWindow.SetDisplayDefinitions(...)` 注入已有 catalog 的只读 skill/item definition 索引。窗口只用 DisplayName 和 typed trigger/target/release 值生成玩家可读文字；列表 metadata 和保存 signal 继续携带稳定 ID。`UiDisplayLabels` 拥有触发条件、释放方式和目标的名称映射，不拥有可用性、消耗或保存规则。

据点窗口保留 DTO 中的状态信息，在标题下合并为紧凑行；成员区只显示当前成员状态，费用区只在存在费用文字时占位。人物详情滚动内容使用水平 ExpandFill，避免值列被压成逐字换行。

## 聚焦检查入口

- `tests/world_map/ui/run_chronicle_window_presentation_regression.cs`：14 个场景在 720p 内的面板范围、插画范围、装饰鼠标过滤、淡入取消、人物详情行宽。
- `tests/world_map/ui/run_contingency_setup_window_regression.cs`：玩家可读名称与稳定 metadata / 提交 ID。
- `tests/world_map/ui/run_modal_window_shell_regression.cs`：普通弹窗与强制确认窗口的关闭语义。
- `tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs`：后置 HUD 重叠时真实鼠标关闭背包，关闭或隐藏战斗后恢复底层输入。
- `tests/e2e/`：新建 / 读取存档与进入战斗的应用级输入流程。

视觉验收需使用原生渲染器查看 3840 × 2160 全屏与 1280 × 720 窗口截图。Headless 的尺寸断言不能证明字体、材质或插画观感。素材与生成提示见 [窗口插画记录](../../content/ui/in_game_window_art.md)。
