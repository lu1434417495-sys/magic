# 熔爆术第三版落地复核

2026-09-15，当前共享 dirty checkout；用户批准“提前结算燃烧并扣除未来次数”后完成。不是全套回归、CI、数值战斗模拟或干净检出交付证明。[结构化证据及日志](evidence/2026-09-15-mage-molten-burst-landing.json)。

## 结果

生产熔爆由范围基础火伤/额外燃烧改为单敌燃烧收割：0/3/5/7级合计提前3/4/6/8次，来源上限1→2；1AP、20→18→16MP、80→40TU冷却。对下一次尚未消费的来源 tick 操作，不新增伤害倍率、不新增燃烧、不删除剩余状态。字段、所有权和支持范围见[当前系统文档](../design/battle/status_tick_advance.md)；完整等级/培养/强度输入见[技能输入卡](../content/skills/mage_molten_burst.md)。

复核 `mage_04.json` 相对 HEAD 的内容差异只涉及 `mage_molten_burst`。保留共享树内此前幻身术、寒蚀/霜爆及无关任务改动；没有提交或暂存。新共享源码在工作树中，无新的 save schema 或迁移。

## 跨路径矩阵

| 面 | 证据与边界 |
|---|---|
| Authoring / projection / closed kind | 正式 JSON payload → source-generated DTO → immutable import/definition；Resource fixture 用明确字段；enum 追加成员保留既有 ordinals |
| Schema / canonical writer | 缺成员/闭合类型沿用 strict parser；新增 payload 的三个字段进入独立 getter/default oracle；正式目录 JSON 往返及706项定义 golden 通过 |
| Validator | 来源型状态、正数有界预算、来源 tag、单敌即时独立效果；非法地面、混合、变体、特殊 profile、重复目标、伤害/豁免参数拒绝 |
| 目标与正常执行 | 实际余烬飞弹3级铺火→熔爆0级；无燃烧、到期、耗尽、已登记非火系流血来源在付费前拒绝 |
| 时间轴 | 先消费 next tick 再应用伤害；两个来源共享上限；实际重施不会恢复已经消费的 tick；80/160/320TU对照总量守恒 |
| 伤害 / mastery | 每跳抵抗取整、免疫、double、护盾、死亡停止；普通/精英/Boss1/2/3点，不按跳数重复；实际进度累计600到非核心5级 |
| Preview / HUD | 完整 detached 副本；三种骰模式无 RNG；逐免死分支继续剩余 tick；HUD次数和消耗提示、HP/护盾估计 |
| AI | 识别 offensive/damage/hostile affordance；共享平均/最小骰预览；已有伤害不重复领取普通输出分，确定收割保留收益；缓存含来源 next tick |
| 自动 / 反应 | 在正式 reaction root 中测试 AutoCast，复用 unit gate 与同一消耗规则；耗尽后自动再触发被拒绝；不为技能新增预存资格 |
| 地面 / 读条 / 特殊入口 | 已检查路由后由内容契约拒绝相关组合，不将未支持的混合路径默认为已落地 |
| Report / 生命周期 | 普通编排提交逐跳 DamageEvents、伤害/护盾/移除状态与变更；shutdown报告零失败、零legacy debt |

## 验证

`dotnet build magic.csproj` 成功，0警告0错误。以下11项专项全部通过，均启用 `--fail-on-output-error`：

- `run_mage_molten_burst_regression`
- `run_skill_canonical_json_schema_regression`
- `run_skill_json_import_contract_regression`
- `run_skill_json_directory_round_trip_regression`
- `run_skill_definition_projector_parity_regression`
- `run_mage_burning_hands_regression`
- `run_battle_status_source_contribution_regression`
- `run_post_save_vulnerability_regression`
- `run_battle_ai_skill_affordance_classifier_regression`
- `run_battle_ai_score_input_metrics_regression`
- `run_battle_ai_score_selection_regression`

Schema首轮失败源于旧枚举/字段清单预期；新增29种effect、10种payload、128个nested/payload字段oracle，并保留之前幻身术新增的第195个effect字段后重跑通过。定义golden仅在检查生产差异及typed payload后更新。自动施放测试曾因缺反应root报错，按正式调用约束补齐fixture后通过；没有改生产代码来绕过调用约束。

20项固定算术卡返回 `arithmetic_only`，另有7个正式机制断点探针；无模拟胜率或“平衡PASS”结论。最后仅补AI空效果列表守卫后重跑build、熔爆和AI输入专项；其他已通过且未受影响的共享检查未重复。

## 实际限制

旧燃烧来源缺少元素标签时保持原来的无标签伤害；本轮没有批量修复所有火系技能。火球/火墙/焚掌联动仅核查供火配置与调用链，具体完整命令连招未逐条实测。AI采用保守收割/破盾时机估值，没有预测未来治疗、驱散或抗性变化。培养100～120场为每战1～2次普通有效施放并领取4点评级的情景假设，非遥测。

未运行全套回归、CI、数值BattleSim、原生画面检查或干净检出交付检查。
