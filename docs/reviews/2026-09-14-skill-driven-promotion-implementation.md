# 技能驱动晋升实现验收

日期：2026-09-14。对象：当前共享工作区，非干净提交或 CI。

## 已落地行为

玩家练到基础门槛后，从人物管理或 G 选择成长技能与职业，确认时只将本次技能核心化，并一起结算职业 rank、人物等级、HP、职业授予技能与属性成长。可暂缓、重开；读档重新计算机会，旧窗口请求失效。战斗内打开暂停，暂缓或确认恢复 timeline，存档继续等既有战斗结算边界。

永久消费事实集中在晋升历史。同一技能只推动一次人物成长；3/5 的已完成核心保持职业资格，无须为下一次晋升重新练至 5。核心切换、融合移除、遗忘和重学不删除已经发生的晋升。

规则先在纯 UnitProgress 副本结算，成功才一次替换成员的 progression。非法请求、过期 token、重复触发和配置错误不发布部分成长。磁盘保存失败会明确返回 PersistenceFailure，内存保留成功结果，再次确认不会重新掷 HP 或结算属性。

存档变更已获用户明确授权：SaveVersion **21**、UnitProgress **2**；PartyState **9**、索引 **5** 不变。新增历史触发 ID／达成等级；移除 active trigger、重复 locked／claimed 字段及持久 pending choice。新 decoder 拒绝旧版本，旧文件不删除，没有迁移或兼容分支。

当前结构与责任见 [技能驱动晋升](../design/progression/skill_driven_promotion.md)、[角色模块](../design/progression/character_module.md)。未来自然游玩时间测量保留在 [提案 S4](../proposals/progression/first_promotion_closed_loop.md)。

## 对成长长度的影响

本次保持熟练度曲线、职业数量条件和收益数值，只消除旧核心因上限扩展而被重复要求训练的问题。

已核对当前 JSON：重击 `warrior_heavy_strike` 的基础 3 级需要 `100 + 250 + 550 = 900` 熟练度，扩展 4／5 级另需 `1000 + 1600 = 2600`；格挡 `warrior_guard` 基础 3 级需要 `300 + 750 + 1650 = 2700`。已满足三个已学战士技能及其他条件时：

| 示例 | 旧规则 | 本次规则 |
| --- | ---: | ---: |
| 重击推动第一次晋升 | 900 | 900 |
| 格挡推动第二次晋升 | 2700 + 2600 | 2700 |
| 两次累计 | 6200 | 3600 |

此组合累计减少约 **42%** 的必要熟练度，减少部分全部来自重击的重复补练。它不代表所有职业统一缩短 42%，也不包括学习第三个技能的获取成本。

新模型的基础成本近似为“各次新成长技能的门槛成本之和”；总时长还取决于技能获取、有效熟练度／分钟、战斗和探索占比。当前七职业各 5 rank，静态 rank 总量上界为 35；并不保证同一构筑可无条件取得所有职业。没有实际游玩时间证据，不能据此断言整个游戏已经足够短或给出实测分钟数。

## 验证

最终 `dotnet build magic.csproj` 通过，**0 警告／0 错误**；最终定向回归 **48/48 通过**，其中 application lifecycle soak 完成 110 轮循环。各测试使用独立用户目录，运行前后 C# 源码、内容 JSON 和 engine asset catalog 文件指纹没有变化。`git diff --check` 通过。

原始记录：[构建](evidence/2026-09-14-promotion/build-final.txt)、[48 项定向回归](evidence/2026-09-14-promotion/final-focused.txt)、[测试清单与结果](evidence/2026-09-14-promotion/final-focused-result.json)、[选集脚本归档](evidence/2026-09-14-promotion/run_final_focused.py.txt)。选集脚本复用 `tests/run_regression_suite.py` 的执行与隔离函数，不是另建测试框架或强制命令包装。

- 完整新档流程：使用正式学习、熟练度、文本命令和保存／加载入口完成首升与战斗二升；验证旧核心 3/5 保留资格、battle save lock、暂缓／确认恢复、奖励继续领取、旧请求重放拒绝。保存锁注入产生真实持久化拒绝，验证已发布成长不重复结算，恢复保存后可重载。
- 场景输入：真实 WorldMapSystem 场景，Godot 输入点击人物管理与晋升按钮、暂缓、按 G、确认成功。720p 无溢出，4K 仍可确认；使用 Vulkan Forward+ 实际截图。
- 规则与 schema：不可变完整请求、字段缺失／多余／重复拒绝，源对象不变、历史连续性和唯一性，CoreQualified／CoreMax 分离、融合后历史、新旧版本边界、多标签交叠的确定性补全。

场景证据：[人物管理 720p](evidence/2026-09-14-promotion/party-720.png)、[晋升 720p](evidence/2026-09-14-promotion/promotion-720.png)、[晋升 4K](evidence/2026-09-14-promotion/promotion-2160.png)。

这些流程夹具通过正式服务加速熟练度，并补齐技能学习前提；没有测试 setter 核心化、伪造 prompt 或额外奖励唤醒。它们不是自然技能获取、主场景冷启动全旅程或成长分钟数测量。未运行数值 BattleSim、benchmark、application E2E 或 CI。

## 工作区与失败记录

本轮开始前工作区已有 UI、美术、地图等大量修改；工作期间还新增技能图标目录和引用。原有修改保留，未执行提交或广泛暂存。

第一轮全量结果为 417 成功／110 失败，期间新增图标引用尚未被 Godot 导入，造成后半段大批启动失败；另暴露了本轮新测试前提和迁移夹具缺口。完成资源导入与修正后另起全量回归，第一轮失败不会改记为通过。

第二轮完整全量结果为 **519/527，通过 519、失败 8**，原始日志见 [full-2.txt](evidence/2026-09-14-promotion/full-2.txt)。其中 3 个旧存档版本断言和 1 个晋升窗口展示断言已随后修正，并包含在最终 48 项通过结果内。其余四项属于共享工作区其他修改的验证缺口：

| 测试 | 观察到的失败 |
| --- | --- |
| `run_equipment_movement_trail_overlay_regression.cs` | 危险地格 OverlayH source ID 未写入 |
| `run_skill_definition_projector_parity_regression.cs` | 技能 Definition 全域 hash 与 golden 不同 |
| `run_skill_generation_stage3_admission_regression.cs` | 三个生成技能 JSON 新增图标引用后，源字节与准入 hash 不同 |
| `run_world_map_runtime_log_dock_regression.cs` | 日志窗口默认为折叠高度 56，而断言要求 600，及战斗 meta 文本差异 |

第三轮在执行期间再次遇到 `mage_force_wall.png` 等新图标引用尚未导入，出现 process-content-startup 错误；已中止，不计作完整全量结果。之后完成资源导入，并在最终构建上运行前述稳定的 48 项选集。**没有最终全量 PASS、干净检出验证或 CI PASS 的结论。**

危险地格 overlay 的独立渲染测试失败点是 OverlayH 中实际 source ID 为 -1，预期火步 36、火焰冲锋 37。该测试仅构造 terrain effect、board snapshot 和 BattleBoard2D，不调用人物成长链；其渲染 owner 已有其他工作区修改。本轮保留该失败并单独报告，未修改这条渲染逻辑。

BattleSim 装配已改用显式晋升历史；不足当前 rank 的历史技能由夹具按职业标签稳定选取，再按历史应用成长。这属于场景种子构造，未读取或迁移旧存档。本轮没有做数值模拟，因此历史平衡报告不能当作新模型的平衡结论。
