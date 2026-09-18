# 技能强度评估机制落地与幻身术算术示例

日期：2026-09-15。作者流程与 Python 计算工具已落地；幻身术玩法仍未修改。规范见 [技能强度评估机制](../content/skills/skill_power_assessment.md)，完整输出见 [JSON 证据](evidence/2026-09-15-skill-power-assessment.json)。

## 本轮交付

- 固定时间基准、显式依赖指纹、场景轴、收益向量和同类锚点比较流程。
- 可直接运行的算术工具：即时施放、最早复施、无恢复的固定资源池、持续区间并集、周期事件的边际效果、资源与 AP 机会成本。
- 已接入 `design-godot-skill`；常规评估读目标与局部基准，不再先全库扫描。旧 TRES 审计脚本明确不作为当前 JSON 证据。
- 未实现或声称拥有全技能自动评分、全职责合格区间、完整战斗模拟或自动平衡 PASS。

## 幻身术受控示例

这些案例来自本次已撤回的设计，用于检验评估机制，不是批准的新技能参数。敌人事件默认从 5TU 开始，每 30TU 一次；每次符合条件的攻击预期减少 2.4 HP。施法者行动间隔 40TU，默认观察 320TU、初始 MP 120、无恢复。参考一次攻击预期伤害为 6 HP，均为显式分析假设。

| 案例 | 实际复施间隔 | 施放时刻 | 观察窗覆盖 TU | 首次施法减少预期伤害 | 观察期减少预期伤害 | MP 支出 |
|---|---:|---|---:|---:|---:|---:|
| 旧距离门槛 0 级，只有 1 个敌人符合条件 | 200 | 0、200 | 120/320 | 4.8 | 9.6 | 120 |
| 已撤回的全距离 6 级 | 160 | 0、160 | 300/320 | 36.0 | 72.0 | 100 |
| 已撤回的全距离 7 级 | 160 | 0、160 | 320/320 | 43.2 | 79.2 | 90 |
| 7 级，仅有 45MP | 160 | 0 | 180/320 | 43.2 | 43.2 | 45 |
| 7 级，敌人首次行动推迟至 30TU | 160 | 0、160 | 320/320 | 36.0 | 72.0 | 90 |
| 7 级，观察窗缩短为 80TU | 160 | 0 | 80/80 | 21.6 | 21.6 | 45 |

单次收益受观察窗截断。表中没有合并 AP、MP 与伤害为一个分数，也没有将这些假设称为实测强度。同等级、同资源、同职责的审定基准才能支持最终偏强/偏弱结论；此处主要证明时间、相位和预算不能省略。

## 验证范围

```powershell
python tests/tooling/test_skill_power_assessment.py
python -X utf8 C:/Users/lu/.codex/skills/.system/skill-creator/scripts/quick_validate.py .codex/skills/design-godot-skill
python .codex/skills/design-godot-skill/scripts/assess_skill_power.py .codex/skills/design-godot-skill/assets/power-assessment/blur-example.json --output docs/reviews/evidence/2026-09-15-skill-power-assessment.json
```

12 项工具回归通过；技能文件校验通过；六个例子的 CLI 运行完成，状态为 `arithmetic_only`。覆盖冷却量化、刷新并集、到期与同刻顺序、资源/AP 不足、成本为零、未知机制输入拒绝、证据过期与输入不变性。

未修改 C# 或玩法资源；本轮未运行 Godot 行为回归、完整回归、BattleSim 或 CI。共享工作区中已有的角色详情等修改不属于本次交付。
