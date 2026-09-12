# 创建角色呈现

`scenes/ui/character_creation_window.tscn` 是登录场景内的全屏创建界面。背景插画和文字控件分层：左侧呈现星仪、古书与天文馆，右侧呈现当前章节；底图不包含文字、输入框或按钮。

## 当前章节与交互

界面依次确认姓名、初始属性、种族 / 亚种、年龄和最终身份。只显示当前章节的标题与正文，不显示步骤栏、步骤编号、未执行阶段的标签或上一步 / 下一步导航。

主按钮描述当前动作：铭刻姓名、接受天赋、确认血脉、确定年龄、踏入世界。各阶段的放弃创建均进入原有 `_cancel`，发出 `cancelled` 并关闭界面。确认身份仍发出原有 `character_confirmed` payload；没有改变属性掷骰、身份合法性或存档格式。

种族与年龄的原生 Button 在 `Pressed` 时进入已有选择处理函数。显示文字和装饰节点忽略鼠标事件，让整项都能点击。键盘焦点有下划线反馈；种族选项重建后恢复到已选项，滚动容器跟随焦点。

## 所有权

- `CharacterCreationWindow.cs`：当前创建状态、身份选项投影、属性掷骰、确认 / 放弃信号。
- `CharacterCreationWindow.Layout.cs`：正文滚动与独立操作行、阅读区锚点、窗口尺寸、遮罩位置参数。
- `CharacterCreationWindow.Appearance.cs`：当前章节文字、标题与选项布局、主题接入、章节淡入。只改变展示，不复制创建规则。
- `character_creation_theme.tres`：局部字体、颜色、输入框与按钮状态。标题使用系统宋体优先的衬线字体；所有文字仍由 Godot 实时绘制。
- `fate_observatory.png`：生成的全屏背景插画，按视口等比覆盖。
- `astral_rule.svg`：可缩放金色分隔纹饰。
- `reading_veil.gdshader`：文字区域的渐变暗化。材质设置 `resource_local_to_scene`，每个窗口实例独立拥有其参数。

窗口的隐藏阶段节点不会通过可见控件提前展示。章节淡入绑定当前阶段节点，隐藏窗口时终止；既有属性滚动与扫描光效继续由创建窗口管理。

## 布局与验证入口

高分屏继续使用 `DisplaySettingsService` 的逻辑高度 1080，4K 下 UI 为 2 倍渲染。宽度小于 1500 个逻辑像素时，右侧阅读区适当向左扩展。正文按可用空间滚动，确认 / 放弃操作留在正文之外。

回归入口：`tests/world_map/ui/run_character_creation_window_payload_regression.cs` 验证未解锁内容不可见、无向导式导航、鼠标 / 键盘选择、取消信号、创建 payload 和操作按钮的可见边界。规则入口仍为 `tests/progression/core/run_character_creation_service_regression.cs`。

美术来源与提示词记录在 `docs/content/ui/character_creation_art.md`。
