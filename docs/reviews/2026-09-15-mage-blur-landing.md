# 幻身术与熟练度落地记录

日期：2026-09-15。范围：当前共享工作树，用户批准的幻身术重设计及“约60–100场、长期专精”的成长目标。不是全技能审查、全量回归、干净 checkout 交付或平衡通过报告。

## 结果

- 生产 `mage_blur` 改用通用承受攻击劣势状态，0–7级显式配置 MP、持续及冷却。所有等级1AP/即时/自身；无距离门槛，可驱散，刷新不叠加。旧共享 `dodge_bonus_up` 的 AC 语义保留。
- 新 typed 字段贯穿 strict JSON/import/Definition、状态存取/clone/read-view、实际命中 metadata、canonical fate preview。两类强制命中均排除，来源侧明确 false 不会覆盖目标贡献。
- 直接熟练度按实际新增攻击干扰计量，普通/精英/首领1/2/3点；一次检定只发一次。施放与刷新本身0点，冗余劣势/友军/失效/必中为0。源技能需已学习，使用来源归因及通用sink，无运行时技能ID分支。
- AI使用下一次敌方行动的边际防护预算，沿用canonical命中概率及已有伤害预算；已有完整保护或不合格威胁不领固定状态分。完整精确伤害和未来策略预测不在此局部评分内。
- 曲线改为 `[60,100,150,210,280,380,500]`，从新学0级到非核心5级累计800。每场直接6点、另领取评级2/4/6点的固定假设分别得到100/80/67场；无评级约134场。未实测逐级事件频率，训练、任务和不同受击量会改变节奏。
- 技能强度评估规范及repo-local技能流程新增成长评估卡：必须联合列触发事实、去重、每次奖励、逐级成本、事件频率及独立奖励来源。

当前所有权与行为见[实现说明](../design/battle/incoming_attack_defense.md)，作者数值/取舍/历史模型见[设计记录](../proposals/battle/skills/mage_blur.md)。

## 验证

构建：`dotnet build magic.csproj --no-restore`，0警告、0错误。

以下均用 `python tests/run_regression_suite.py --pattern <文件名去扩展名> --fail-on-output-error --log-file <局部日志>`，每项发现并运行1个测试，退出0、Passed 1 / Failed 0：

| 测试 | 验证范围 | 结果 |
|---|---|---|
| `run_mage_blur_regression` | 生产0–7级、实际施法/刷新/驱散/到期、状态strict往返、命中及必中暴击门、防護事件奖励与去重、799/800点正式升级、评级独立性、AI边际 | PASS |
| `run_attack_policy_parity_regression` | 公共命中策略路径 | PASS |
| `run_battle_status_effect_typed_state_regression` | typed状态保存与生命周期 | PASS |
| `run_status_effect_semantics_regression` | 共享状态语义 | PASS |
| `run_fate_attack_formula_regression` | 公共命运/幸运攻击公式 | PASS |
| `run_battle_ai_mutation_guard_regression` | AI状态修改探测与生命周期 | PASS |

mutation guard测试包含主动制造违规的预期 `[ERROR]` 诊断；runner与测试均PASS，不能把这些负向用例日志记为生产故障，也不能声称日志无ERROR。所有上述测试的生命周期报告均无failure和legacy debt。

`python -m unittest discover -s tests/tooling -p test_skill_power_assessment.py`：12项PASS。

静态核对：`git diff --check` 通过；生产family与HEAD的JSON语义对照仅 `mage_blur` 改变。新增依赖C#文件和本次文档单独纳入证据指纹，尚未提交。

证据：[当前检查摘要与源码指纹](evidence/2026-09-15-mage-blur-landing.json)。日志来源为本次运行，受控熟练度算术与引擎行为证据分开保存。

## 局限与已有进度

本次没有执行全量回归、数值BattleSim、CI、原生UI截图或干净工作树构建。专项角色是合成fixture，未证明完整实战平均减伤、胜率或60–100场分布。

改造前30个强度算术案例仍保存原始依赖指纹；当前运行会正确返回 `evidence_stale`，变动源是生产family与 `BattleHitResolver`。未将旧输出改名为当前强度证据，时间规则未改变。

没有新增存档字段或版本，也未增加历史进度重算/迁移。已有技能等级保留，现有当前熟练度在后续入账时按新曲线处理；有效上限仍由晋升历史控制。没有新增每场练习上限或同敌人递减。共享工作树中的人物详情/UI等其他改动不属于本次交付。
