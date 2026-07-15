# 弹窗复用组件纪要

初次记录：`2026-07-16`

## 状态

- 当前状态：`Active Discussion Record`
- 范围：`scripts/ui/` 下全部模态弹窗与 `scripts/ui/components/` 复用组件。

## 已落地的复用层（2026-07-16）

1. **`components/ModalWindowShell.cs`** — 模态外壳基类，统一两条关闭通道：
   - 遮罩点击（左/右键）与 Esc；子类实现 `_on_modal_close_requested()` 决定关闭语义（隐藏 + 发哪个信号）。
   - `DismissOnShade` / `DismissOnEscape` 可否决关闭（遮罩点击仍被吞掉不漏到底层），用于必须显式确认的窗（MasteryReward）或运行时开关（SubmapEntry 的 `dismiss_on_shade` prompt 字段）。
   - 约定：场景根下有 `Shade` ColorRect；子类 `_Ready` 末尾调 `base._Ready()`；子类如有自己的 `_UnhandledInput` 先调 `base._UnhandledInput(@event)`。
   - 已迁移 12 个窗：CharacterInfo、MasteryReward、SubmapEntry、DisplaySettings、PromotionChoice、NpcQuestOffer、PartyWarehouse、Settlement、Shop、PartyManagement、ContingencySetup、SelectableListWindow（含其子类 SaveList / WorldPresetPicker）。
   - **未迁**：CharacterCreationWindow（多步向导，自有 Esc 分步回退语义与文本输入焦点，收益低风险高）；BattleMapPanel 战中背包浮层（非独立场景，内嵌 shade，后续可评估）。
   - 交互统一结果：此前只有 3 个窗支持 Esc，现全部迁移窗都支持；遮罩右键关闭也一致了。
   - 回归：`tests/world_map/ui/run_modal_window_shell_regression.cs`（Esc 关闭 / 否决 / prompt 开关三态）。

2. **`components/UiListTheme.cs`** — ItemList 统一皮肤（悬停/选中/光标/字色），从 SelectableListWindow 提出。消费方：存档列表、队伍双列表、仓库、商店、触发条件法术列表、战中背包列表。

3. **`components/SelectableListWindow.cs`** — 列表选择窗基类（列表 + 详情 + 确认/取消 + Enter 确认），子类：SaveListWindow、WorldPresetPickerWindow。

4. **`components/SelectionCardBuilder.cs`** — 卡片式选项构建器，消费方：PromotionChoiceWindow、CharacterCreationWindow（2026-07-16 起种族与年龄阶段选择卡片化，替代原下拉；亚种/适应属性仍为下拉，因其是依赖性次级选项）。

5. **`components/UiOptionButtonUtils.cs`**（2026-07-16）— OptionButton 统一填充/按 id 选中工具，id 存 item metadata，取代各窗口的"平行 id 列表 + AddItem + 循环找下标"。消费方：CharacterCreation（亚种/适应属性）、ContingencySetup（单项锁定选项）、PartyWarehouse / Shop / Settlement（成员选择器）。DisplaySettings 的分辨率下拉是纯下标语义（无 id 概念），有意未迁。

## 尚未做（候选）

- **简单确认框归一**：SubmapEntryWindow 已是通用确认模态（进子图/开战/战败返回三流程复用）；新增"标题+正文+确认/取消"需求应直接复用它，不要再开新场景。
