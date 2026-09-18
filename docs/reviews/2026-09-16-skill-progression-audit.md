# 技能等级与熟练度专项审计（2026-09-16）

范围：当前共享工作区的 `data/configs/json/skills/`，先合并文件内模板，再应用正式 DTO 默认值。共 706 项，632 项 `learn_source=book`。本轮新增审计与提案文档，没有修改技能 JSON 或游戏运行时代码。

## 结论

问题确实存在，但不能表述为“大批技能没有 mastery_curve”：当前普通书籍技能全部有非空曲线，静态技能的曲线长度也与默认值解析后的上限一致。主要问题是 **等级没有收益、熟练度触发不匹配、成长链不完整**。

| 类别 | 数量 | 当前证据 | 处理 |
|---|---:|---|---|
| 普通书籍技能只到 1 级 | 7 | 未写 max_level，正式 parser 默认 1；各有一项熟练度阈值，没有分级描述或分级效果配置 | 逐项设计真实成长，并核对特殊运行时及获取方式 |
| 弓手技能到 3 级但成长配置空缺 | 16 | non_core_max_level 默认 0；无 growth_tier/attribute_growth_progress；0–3 级描述完全相同，combat_profile 无等级分支 | 优先修复等级兑现及直接熟练度 |
| 战士技能采用 3→3 | 110 | 静态非核心上限与绝对上限都为 3 | 标准档位候选；逐项补齐 4/5 级效果，不能只抬 max_level |
| 其他静态上限候选 | 2 | 连锁应急术 9→9、真龙斩 2→5 | 按特殊机制单独核查，暂不批量调整 |
| 普通书籍技能缺成长元数据 | 24 | 上述 7+16，以及金刚不坏 | 金刚不坏已有特殊被动成长路径，单列核查 |

类别有重叠，不能相加成待修技能总数。共 135 个书籍条目落在标准静态上限组合之外，其中包含特殊机制待核查项，不是 135 个已经批准的修改。

4 个 max_level=0 条目是普通攻击和 3 个 generated internal 技能；其余 63 个非书籍单级条目涉及装备、种族、职业、升华及内部结算，不能直接套普通书籍技能规则。已完成的魔法飞弹、破阵连矢，以及现有工作区的幻身术、骤冷术、熔爆术不选作本轮首项重做对象。

## 首批清单

### 七个单级书籍技能

| ID | 名称 | 当前 0→1 熟练度 | 路径 |
|---|---|---:|---|
| black_contract_push | 黑契推进 | 48 | misc_01.json |
| black_crown_seal | 黑冠封印 | 52 | misc_01.json |
| black_star_brand | 黑星烙印 | 36 | misc_01.json |
| crown_break | 折冠 | 48 | misc_01.json |
| doom_sentence | 厄命宣判 | 60 | misc_01.json |
| doom_shift | 断命换位 | 48 | misc_01.json |
| misstep_to_scheme | 失手成筹 | 32 | misc_02.json |

这些技能有 `runtime_behavior`，不能仅凭缺少 effect_defs 判断“没有效果”。但特殊运行时也不能自动成为不需要技能成长的理由。黑星烙印、折冠、厄命宣判同时出现在信仰奖励中，而定义仍默认 book。`SkillBookItemFactory` 会为 book 来源生成技能书；新建角色的随机书籍候选还需要检查前置、解锁方式和初始 MP 消耗，不能把这七项当成玩家绝对拿不到的内部占位。

### 十六个弓手技能

都位于 `archer_01.json`，上限 3，曲线 `[28,46,72]`，合计 146；non_core_max_level 默认为 0，直接熟练度默认 `skill_damage_dice_max/per_target_rank`。全套缺少等级战斗差异，不应先统一填 3→5 再声称修复完成。

| ID | 名称 |
|---|---|
| archer_evasive_roll | 翻滚卸力 |
| archer_execution_arrow | 猎手终结 |
| archer_fan_volley | 扇幕齐射 |
| archer_far_horizon | 天际远射 |
| archer_fearsignal_shot | 惊禽哨箭 |
| archer_heartseeker | 追心箭 |
| archer_highground_claim | 抢高位 |
| archer_hunter_feint | 猎步佯退 |
| archer_hunting_grid | 狩猎网阵 |
| archer_killing_field | 猎场封锁 |
| archer_running_shot | 奔袭射击 |
| archer_shield_breaker | 破盾箭 |
| archer_sidewind_slide | 侧滑换位 |
| archer_skirmish_step | 游击步 |
| archer_split_bolt | 裂风重矢 |
| archer_tendon_splitter | 断筋箭 |

翻滚卸力只配置自身状态与后退，却默认要求高伤害骰；`PerTargetRank` 对同阵营还要求支持型触发语义。猎手终结的描述承诺按已损失生命增伤，但定义只有固定 power=14。抢高位的描述承诺移动及高地条件，定义只有 self 预瞄状态。后两项属于额外的效果兑现候选，后续需逐项检查执行、目标、预览和 AI，不能把它们当成单纯等级字段问题。

## 黑契推进：明确的直接熟练度断点

调用链：JSON → `SkillJsonImportParser` → `SkillDefinition` → `BattleSkillExecutionOrchestrator` → `BattleSkillMasteryService.RecordTargetResult` → 成长写入入口 → `ProgressionService.GrantSkillMastery`。

1. JSON 固定 power=12，没有技能伤害骰或武器骰；`force_hit_no_crit` 保证不暴击。
2. 默认熟练度触发是 `skill_damage_dice_max`。
3. `BattleDamageResolver.Dice` 的高骰事实必须先有 regular dice；当前普通施放没有此事实。
4. `BattleSkillMasteryService` 要求有效伤害/护盾吸收且有高骰事实，因而普通施放不给该技能直接熟练度。
5. 战后评级是独立奖励：本场使用过的技能可获得 2/4/6 点，实际领取及入账另算；训练/任务也另算。不能说它永远得不到任何熟练度。

这是源码规则与正式配置共同支持的结论；本轮未新建专门的“黑契推进熟练度为零”运行时探针。既有 FATE_25 检查代价、必定命中和不暴击，没有 mastery 断言。

## 修复顺序与准入要求

1. 首项黑契推进，按[字段级提案](../proposals/battle/skills/black_contract_push_progression.md)评审。
2. 再处理翻滚卸力、抢高位等非伤害技能的有效练习事实，并修复上述弓手内容的实际分级收益。
3. 分组核查其余命运技能：状态施加、位置交换、被动事件各走对应事实，不统一塞进伤害触发。
4. 最后分批处理 110 个 3→3 战士条目，结合原定位补等级收益、成长与描述。

每项完成条件：合法上限 → 各级实际效果 → 可发生的熟练度事实及去重 → 曲线与培养节奏 → 晋升/属性成长 → 描述/预览/执行/AI → 聚焦回归。现有校验允许非负 max_level、non_core≤max 和匹配长度曲线，不强制标准档位，也不证明学习收益。新增准入门禁应区别普通可学习内容与明确特殊来源，先做作者诊断，再逐批清理后收紧正式验证；不可直接让现有 135 项阻断启动。

## 当前验证与证据

- `dotnet build magic.csproj --no-restore -v quiet`：通过，0 警告、0 错误。
- `python tests/run_regression_suite.py --pattern run_skill_json_directory_round_trip_regression --stop-on-failure`：1/1 通过，正式 706 项 JSON 导入及域内验证。
- `python tests/run_regression_suite.py --pattern run_skill_requirements_typed_regression --stop-on-failure`：1/1 通过。
- `python tests/run_regression_suite.py --pattern run_fate_low_luck_tactical_skills_regression --stop-on-failure`：1/1 通过。
- 本轮没有运行全量回归、CI、BattleSim，也没有验证提案的运行时或平衡性。

[完整逐项 JSON](evidence/2026-09-16-skill-progression-audit.json)含原始文件 SHA256、JSON pointer、有效默认值和候选标记；[复现脚本](evidence/2026-09-16-skill-progression-audit.py)只做静态盘点，不是生产 validator。无可见等级字段的候选还须检查特殊 runtime，不能直接等同于缺陷。

当前源码依据：`SkillJsonImportParser.cs:86`、`SkillRootCombatJsonDtos.cs:184`、`SkillImportModelValidator.cs:121`、`SkillEffectiveMaxLevelRules.cs:7`、`PromotionEligibilityRules.cs:8`、`ProgressionService.cs:185`、`BattleSkillMasteryService.cs:498/537/787`、`BattleDamageResolver.Dice.cs:470`、`BattleSkillExecutionOrchestrator.cs:2398`、`SkillBookItemFactory.cs:16`、`GameSession.CharacterCreation.cs:606`。
