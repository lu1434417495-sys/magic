# 队伍窗口数据边界

`WorldMapRuntimeProxy.GetPartyManagementViewDataTyped()` 使用 `PartyManagementViewBuilder.Capture` 构建 `PartyManagementViewData`，`WorldMapSystem` 在打开或刷新队伍窗口时发布该快照。

快照只包含编队 ID、队长/主角 ID、脱离真实状态的成员副本和派生展示事实。成员的 progression、equipment、trait 等嵌套状态通过 `DuplicateState()` 复制，不携带仓库、任务日志或完整队伍存档图。成员副本可供窗口格式化读取；它不是 canonical state，也不作为业务提交来源。

构建器通过 `IPartyManagementViewQuery` 读取属性、有效特性、套装评价、身份摘要和晋升方案数量。`CharacterManagementModule` 提供这些查询，窗口不保存该模块或查询提供者。身份摘要仍使用现有的 detached Godot 字典合同。

`RefreshView()` 只渲染已发布的快照。真实角色状态改变后，调用者必须发布新快照；测试 fixture 同样通过 `SetPartyView` 更新，不能经窗口取得真实成员再修改。关闭窗口清除快照与本地列表。

编队、队长、仓库、触发术及晋升仍通过原有 signal 提交意图。窗口内的列表调整不会写入真实队伍；队长移入替补仍先发编队信号、再发队长信号。最终命令继续检查当前真实状态。

`PartyRosterRules.MaxActiveMemberCount` 是上阵人数上限的唯一来源。窗口、编队命令和存档校验共用该常量，当前上限为 4；存档版本及序列化格式不变。

相关回归：`run_party_management_window_regression` 检查嵌套状态隔离、快照刷新、信号顺序和详情内容；`run_party_character_details_regression` 检查人物档案页签与原生界面；`run_game_runtime_party_command_handler_regression` 检查正式编队命令。
