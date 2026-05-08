# 魔法反噬机制与命运系统接入设计讨论纪要

> 状态：设计定稿，待实现评审  
> 涉及系统：命中/豁免检定、命运模块（fate）、精通系统（mastery）、技能执行链、AI评分  
> 关键决策：豁免检定**不接入**命运模块；法师大失败走**精通保护 + 失效模式**路线

---

## 一、背景与问题陈述

### 1.1 当前命中检定已接入命运模块

命中检定的 `battle_damage_resolver.gd` 中，攻击者掷出 `d20` 后要走三层命运判定：

| 机制 | 计算方式 | 影响 |
|------|----------|------|
| **失手保护（Fumble）** | `fumble_low_end = 1 + clamp(-effective_luck - 4, 0, 2)` | 低幸运扩大失手区间 |
| **高位大成功（High Threat Crit）** | `crit_threshold = 20 - combat_luck_score` | 高幸运降低暴击门槛 |
| **门骰大成功（Gate Die Crit）** | `crit_gate_die = 20 << max(0, -effective_luck - 3)` | 极低幸运触发补偿门骰 |

**effective_luck 范围**：-6 ~ +7（由 hidden_luck_at_birth + faith_luck_bonus 决定）

### 1.2 当前豁免检定完全不走幸运

```gdscript
# battle_save_resolver.gd
var natural_roll := _roll_save_die(advantage_state, context)  # 纯 D20
var roll_total := natural_roll + ability_modifier + save_bonus
var success := roll_total >= DC
```

- 天然1：必定失败
- 天然20：必定成功
- 中间区间：纯数值对抗
- **没有任何幸运属性参与**

### 1.3 当前大失败的即时惩罚为零

命中检定大失败（`roll <= fumble_low_end`）时：
- 战斗中：仅返回 `ATTACK_RESOLUTION_CRITICAL_FAIL`，**零额外惩罚**
- 战斗后：`misfortune_service` 累加灾厄值，`low_luck_event_service` 追踪用于「借来的路」等战后奖励

### 1.4 核心待决策问题

1. **豁免检定**是否应该像命中检定一样接入命运模块？
2. **法师技能**大失败时是否应该引入「魔力失控反噬」机制？
3. 如果引入反噬，**失效模式**应该如何设计（伤害自己 vs 效果偏移 vs 其他）？

---

## 二、豁免检定是否接入命运模块

### 2.1 核心判断：豁免不应直接接入命中同款命运模块

**理由**：

1. **设计语义不同**：命运模块管理的是「暴击/失手」。命中检定中暴击有明确收益（伤害翻倍/触发失衡），豁免检定没有「暴击成功」概念——只有成功/失败/半伤。硬套是削足适履。

2. **D&D 5e 对齐**：你明确要求火球术对齐 D&D，D&D 5e 豁免纯看属性修正，不天然受幸运影响（除非专长 Lucky）。

3. **法师威胁感**：战士是近战职业，防御应该是高 AC、高体质、高血量。如果战士靠幸运就能稳定过火球豁免，法师的威胁感就没了。

4. **已有优势机制**：战士可以通过 `guard`/`magic_shield` 等状态获得豁免优势，这已经覆盖了「关键时刻更容易成功」的需求。

5. **低幸运角色的生存空间**：命中走幸运已经让低幸运角色在进攻端处于劣势。如果豁免也走，低幸运角色将同时面临「更容易失手 + 更容易被AOE秒杀」的双重惩罚。

### 2.2 三种方案对比

| 方案 | 描述 | 一致性 | 复杂度 | 平衡性 | 推荐度 |
|------|------|--------|--------|--------|--------|
| **A：不走幸运** | 保持现状，纯 D20 + 修正 | ❌ | ✅ 最低 | ✅ 最稳定 | **⭐ 推荐** |
| **B：轻量幸运** | 只引入 `fumble_low_end`，低幸运更容易大失败 | ⚠️ | ✅ 低 | ⚠️ 需调 | 可选 |
| **C：完整幸运** | 双向尾部调控（fumble + crit_threshold） | ✅ | ❌ 高 | ❌ 波动大 | 不推荐 |

### 2.3 最终建议

**豁免检定不走命运模块（方案A）**。

> 战士的幸运应该让他在**挥刀时更猛、更不容易砸脚**，而不是让他在**躲法术时更灵活**。后者是敏捷/体质该干的事。如果两者都让幸运管，幸运就变成唯一核心属性了。

---

## 三、法师魔法反噬机制设计

### 3.1 设计愿景

- 法师「成长难但强」：新手法师容易失控，老手法师游刃有余
- 渐进式惩罚：先软惩罚（吞MP），再硬惩罚（失效模式）
- 不破坏现有战后命运奖励系统：大失败仍然标记 `critical_fail`，战后照样拿「借来的路」
- 有戏剧性和法师职业特色

### 3.2 核心循环

```
法师释放魔法技能 → 命中检定大失败
  ↓
检查「本场该技能已用保护次数」<「该技能精通保护次数」？
  ├─ 是（受保护）：技能白放 + 额外吞噬 MP + 标记 critical_fail
  └─ 否（无保护）：技能进入【失效模式】+ 标记 critical_fail
```

### 3.3 保护次数的获取

#### 绑定对象：按技能独立计算

每个魔法技能有自己的保护次数，基于该技能的 **mastery level**。

**为什么不搞全局池？**
- 全局池会让玩家只练一个技能，其他技能不敢用
- 按技能独立更真实："火球术练得熟，火球不容易失控；但第一次用陨石术还是会炸"

#### 配置方式

在 `CombatSkillDef` 中新增字段：

```gdscript
@export var fumble_protection_curve: PackedInt32Array = PackedInt32Array()
```

`fumble_protection_curve[i]` 表示技能达到 **level i** 时，本场战斗提供几次大失败保护。

**火球术示例**：

```gdscript
mastery_curve = [400, 1000, 2200, 4000, 6500, 9500, 13000]
max_level = 7
fumble_protection_curve = [0, 0, 0, 1, 1, 2, 3]
```

| 火球术等级 | 保护次数 | 含义 |
|-----------|---------|------|
| 0-2 | 0 | 新手法师，每次大失败都失控 |
| 3-4 | 1 | 入门，本场第1次大失败被压制 |
| 5-6 | 2 | 熟练，前2次大失败被压制 |
| 7 | 3 | 大师，前3次大失败被压制 |

**对于没有 mastery_curve 的基础技能**（如奥术飞弹、霜击术）：
- 默认 `fumble_protection_curve = [0]`（无保护）
- 或给它们补上 mastery_curve，让基础技能也有成长空间

### 3.4 受保护时的「MP 吞噬」规则

```
额外 MP 损失 = 技能基础 MP 消耗 × 100%
```

**为什么选100%？**
- 保护不是免费午餐，是"用更多MP买平安"
- 火球术100MP + 额外100MP = 200MP，高等级法师能承受，但会压缩后续施法空间
- 低等级法师本来就放不了几次，protected fumble 让他们至少不会死

**MP 不够时**：
- 吸干为止（`current_mp = 0`）
- 仍然算"受保护"，**不触发失效模式**
- 保护次数的优先级高于一切

### 3.5 与现有系统的交互

#### 逆命护符
- 逆命护符触发时，**完全不进入反噬逻辑**
- 不扣额外 MP，不消耗保护次数，不触发失效模式

#### 战后「借来的路」奖励
- **完全保留**。无论是否被保护、是否触发失效，只要 `critical_fail = true`，战后奖励逻辑不变。

#### 灾厄值（misfortune_service）
- 被保护的大失败仍然算"命运中的污点"
- 灾厄值仍然累加，首次 critical_fail 仍然 grant reverse_fortune

---

## 四、失效模式（Misdirection/Backlash）详细设计

### 4.1 核心原则

保护次数外，不走"伤害自己"，而是"技能放歪了"：
- 技能**不会白放**（有伤害/有效果）
- 但**可能打错人/炸到队友**

### 4.2 按技能类型的失效模式

#### P0：地面AOE — 「无差别轰炸」（最高优先级）

**失效表现**：火球在预定位置爆炸，但失控的火焰不分敌我。

**实现方式**：
- 不改变落点，不改变范围
- 临时把效果过滤从 `enemy` 改为 `any`
- 范围内**所有存活单位**（敌人、队友、法师自己）都受影响

**实现难度**：⭐（极低，改filter即可）

**示例战报**：
> "魔力暴走！火球在预定位置炸开，但失控的火焰吞噬了范围内的一切。敌人受到 28 点火焰伤害，队友「艾琳」受到 14 点火焰伤害（友军减免50%），法师自己也受到 28 点火焰伤害！"

**为什么是最优失效模式**：
- 不改坐标逻辑，不需要处理偏移后的地形碰撞
- 不改伤害计算，正常走豁免/命中
- 戏剧效果极强（"法师炸队友"是经典名场面）
- 玩家可以通过**站位**来管理风险

---

#### P1：单体命中 — 「目标偏移」

**失效表现**：魔力没有打中原目标，而是偏移到了附近的随机单位。

**实现方式**：
- 以原目标为中心，半径1格内随机选择一个存活单位
- 如果范围内没有单位 → fallback 为 miss
- 对新目标正常做命中/伤害结算

**实现难度**：⭐⭐⭐（中）

---

#### P2：自身增益 — 「增益反转」

**失效表现**：给自己加的 buff 变成了 debuff。

**实现方式**：
- 定义反效果映射表：护盾→易伤、加速→减速、隐身→暴露

**实现难度**：⭐⭐（低）

---

#### P3：直线/锥形 — 「方向偏转」

**失效表现**：发射方向随机偏转 ±45° 或 ±90°。

**实现方式**：
- 原方向向量随机旋转
- 沿新方向重新计算路径/锥形区域

**实现难度**：⭐⭐⭐⭐（高）

---

#### P4：多目标 — 「目标重分配」

**失效表现**：多个子效果中有部分打错目标。

**实现方式**：
- 对每个子效果独立判定是否偏移
- 偏移的子效果重新选择随机目标（不分敌我）

**实现难度**：⭐⭐⭐（中）

### 4.3 关键设计决策

#### 友军伤害是否减免？

**建议：友军受到的伤害为原伤害的 50%**

- 完全伤害太致命，一个火球可能秒掉脆皮队友
- 50% 减免体现"队友只是被波及，不是主要目标"
- 对敌人仍然是 100% 伤害

#### 法师自己是否会被波及？

**建议：会**

- 如果火球在法师脚边爆炸，法师也在范围内，理应受伤
- 增加策略深度：法师不能无脑贴脸放火球
- 符合"魔力失控"的语义——失控的魔力不认主

#### 偏移后是否还做正常的命中/豁免检定？

**建议：正常做**

- "失控"的是瞄准精度，不是魔力强度
- 魔力丢歪了打到人，该闪避的还是可以闪避
- 如果偏移后强制命中，玩家完全无法防御，挫败感太强

#### 失效的AOE打到队友，法师获得精通吗？

**建议：不获得**

- 打队友是负面事件，不应该给奖励
- 需要确保 `battle_skill_mastery_service.gd` 中的 faction 检查在失效模式下仍然生效

### 4.4 实现层面的具体改动

#### 配置层

```gdscript
# combat_skill_def.gd 新增
@export var backlash_mode: StringName = &""           # 失效模式类型
@export var backlash_target_filter: StringName = &""   # 失效时的目标过滤覆盖
```

**火球术配置**：
```gdscript
backlash_mode = &"indiscriminate"   # 无差别轰炸
backlash_target_filter = &"any"      # 不分敌我
```

#### 运行时状态

```gdscript
# battle_unit_state.gd 新增（纯运行时，不序列化）
var fumble_protection_used: Dictionary = {}  # skill_id → int
```

#### 伤害结算层

在 `battle_damage_resolver.gd` 的 Step 3（fumble 处理）中：

```gdscript
if hit_roll <= fumble_low_end:
    if _try_apply_reverse_fate_amulet(source_unit):
        # ... 降级逻辑，保持不变 ...
        return metadata
    
    if _is_magic_attack(skill_def):
        var protection_remaining := _get_fumble_protection_remaining(source_unit, skill_def)
        if protection_remaining > 0:
            # 受保护：miss + 吞MP
            _consume_fumble_protection(source_unit, skill_def)
            _apply_mp_backlash(source_unit, skill_def)
            metadata["critical_fail"] = true
            metadata["fumble_protected"] = true
            metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_FAIL
            return metadata
        else:
            # 无保护：触发失效模式
            metadata["critical_fail"] = true
            metadata["fumble_protected"] = false
            metadata["backlash_triggered"] = true
            metadata["backlash_mode"] = skill_def.combat_profile.backlash_mode
            # 不 return，让上层继续执行技能效果
    
    # 物理攻击：保持现状
    metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_FAIL
    metadata["critical_fail"] = true
    return metadata
```

#### 技能执行层

在技能效果应用前，检查 `backlash_triggered`：

```gdscript
# 如果触发失效模式，临时覆盖 target_filter
if bool(attack_result.get("backlash_triggered", false)):
    for effect_def in effect_defs:
        var original_filter := effect_def.effect_target_team_filter
        effect_def.effect_target_team_filter = skill_def.combat_profile.backlash_target_filter
        _apply_effect(...)
        effect_def.effect_target_team_filter = original_filter  # 恢复！
```

---

## 五、对AI的影响

### 5.1 当前AI的问题

当前AI：
- 选择技能时只看"期望伤害最大化"
- 不考虑友军位置
- 不考虑自己的保护次数

### 5.2 AI需要的新能力

1. **知道保护次数**：评估火球术时，检查还剩几次保护
2. **保护用完时重新评估**：
   - 火球范围内有队友 → 大幅降低评分
   - 范围内只有自己 → 中等降低
   - 范围内只有敌人 → 正常评分
3. **备选策略**：
   - 优先使用还有保护的技能
   - 所有高伤害技能都没保护时，改用低耗技能或普通攻击

### 5.3 实现建议

在 `battle_ai_decision.gd` 的评分函数中：

```gdscript
if skill.has_backlash_mode and skill.protection_remaining <= 0:
    var friendly_units_in_range := count_friendly_units_in_aoe_range(caster, skill)
    score -= friendly_units_in_range * 1000  # 大幅降低评分
```

---

## 六、数值平衡参考

### 6.1 火球术（满级 8d6，100MP）

| 火球术等级 | 保护次数 | 第1次大失败 | 第2次大失败 | 第3次大失败 | 第4次大失败 |
|-----------|---------|-----------|-----------|-----------|-----------|
| 0-2 | 0 | 无差别轰炸 | - | - | - |
| 3-4 | 1 | 保护：吞100MP | 无差别轰炸 | - | - |
| 5-6 | 2 | 保护：吞100MP | 保护：吞100MP | 无差别轰炸 | - |
| 7 | 3 | 保护：吞100MP | 保护：吞100MP | 保护：吞100MP | 无差别轰炸 |

### 6.2 概率参考

**高幸运法师（fumble_low_end=1）**：
- 单次大失败概率 = 5%
- 连续2次大失败 = 0.25%
- 连续4次大失败 ≈ 0.000625（约 0.06%）

**低幸运法师（fumble_low_end=3）**：
- 单次大失败概率 = 15%
- 连续2次大失败 = 2.25%

### 6.3 友军伤害减免

| 对象 | 伤害比例 | 理由 |
|------|---------|------|
| 敌人 | 100% | 主要目标 |
| 队友 | 50% | 被波及，非主要目标 |
| 法师自己 | 100% | 贴脸施法的惩罚 |

---

## 七、实现优先级与工作量估算

### 7.1 最小可行版本（MVP）

**只做 P0：地面AOE「无差别轰炸」**

| 文件 | 改动内容 | 预估时间 |
|------|---------|---------|
| `combat_skill_def.gd` | 加 `backlash_mode` 和 `backlash_target_filter` | 5分钟 |
| `skill_def.gd` | 加 `fumble_protection_curve` | 5分钟 |
| `battle_unit_state.gd` | 加 `fumble_protection_used` 运行时字典 | 5分钟 |
| `battle_damage_resolver.gd` | fumble 处理中增加保护检查和失效标记 | 30分钟 |
| `battle_runtime_module.gd` | 技能执行时检查失效标记并临时改filter | 30分钟 |
| `battle_report_formatter.gd` | 新增失效模式战报文本 | 15分钟 |
| `mage_fireball.tres` | 配置保护曲线和失效模式 | 5分钟 |
| **总计** | | **约1.5小时** |

### 7.2 完整版本（Full Implementation）

在上述基础上追加：

| 功能 | 额外工作量 |
|------|-----------|
| 单体技能「目标偏移」 | +2小时 |
| 自身增益「增益反转」 | +1小时（需配置映射表） |
| 多目标「目标重分配」 | +2小时 |
| 直线/锥形「方向偏转」 | +4小时（几何计算复杂） |
| AI 评分适配 | +4小时 |
| 战报与UI提示 | +2小时 |
| **完整版总计** | **约16-20小时** |

---

## 八、附录：与现有系统的完整交互矩阵

| 系统 | 保护内大失败 | 保护外大失败（失效模式） | 逆命护符触发 |
|------|-------------|------------------------|-------------|
| **战斗中结果** | miss + 吞MP | 失效模式生效 | 普通miss |
| **critical_fail 标记** | ✅ 是 | ✅ 是 | ❌ 否（已降级） |
| **战后「借来的路」** | ✅ 触发 | ✅ 触发 | ❌ 不触发 |
| **灾厄值（calamity）** | ✅ 累加 | ✅ 累加 | ❌ 不累加 |
| **逆命护符掉落判定** | ✅ 计入 | ✅ 计入 | ❌ 不计入 |
| **精通获取** | 正常 | 打队友不获取 | 正常 |
| **战报文本** | "魔力被压制" | "魔力暴走/偏移" | "逆命护符生效" |

---

## 九、关键结论速查

| 问题 | 结论 |
|------|------|
| 豁免是否接命运模块？ | **不接**（保持现状） |
| 法师大失败是否反噬？ | **是，但通过精通保护缓冲** |
| 保护次数用尽后怎么办？ | **触发失效模式（效果偏移/无差别轰炸）** |
| 失效模式首选实现？ | **地面AOE「无差别轰炸」**（改filter即可） |
| 友军是否受伤害？ | **是，但减免50%** |
| 法师自己是否受伤害？ | **是，100%** |
| 战后奖励是否保留？ | **完全保留** |
| AI是否需要适配？ | **需要，保护用完时避免在队友身边放AOE** |
