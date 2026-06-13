# 英雄联盟技能 — 战棋框架转译设计表

更新日期：`2026-04-14`

说明：
- 本文档将八位英雄联盟代表英雄的技能体系，按当前战棋技能框架重写。
- 不照搬 LoL 数值，而是按战棋体系的 `AP/MP/Stamina/Aura` 资源、TU 冷却、状态语义重新定义。
- `skill_id` 使用 `lol_` 前缀，英雄缩写区分：`garen_` / `lux_` / `ashe_` / `yasuo_` / `darius_` / `annie_` / `katarina_` / `vayne_`。
- 定位不变，但实现方式必须符合现有 `SkillDef → CombatSkillDef → CombatEffectDef` 链路。
- 状态持续时间描述层统一按 `TU` 标注；运行时当前仍按行动轮次（1/2/3）结算，等 TU 化完成后再切换。

---

## 零、LoL 效果 → 战棋状态映射规则

| LoL 效果 | 战棋框架状态 / 机制 | duration（当前轮次） |
| --- | --- | --- |
| 沉默 (Silence) | `staggered`（行动受限，无法使用技能） | 1 轮 |
| 定根 (Root) | `pinned`（禁止主动位移，可使用技能） | 2 轮 |
| 眩晕 (Stun) | `staggered`（行动完全受限，更长） | 2 轮 |
| 减速 (Slow) | `slow`（需新增；移动消耗 +1） | 1 轮 |
| 护甲削减 | `armor_break` | 2 轮 |
| 护盾 | `guard` | 1 轮 |
| 点燃 / 灼烧 | `burning` | 3 轮 |
| 斩杀强化 | 设计说明层触发条件；首版按固定倍率落地 | — |

> **注**：`slow`（减速）和 `pinned`（只禁止移动）目前在状态语义表中尚未独立拆分，首版可用 `staggered` 代替并在注释里标注预期语义，等状态系统完善后替换。

---

## 一、盖伦（近战战士）

**定位**：前排肉盾型战士。高生存、范围清线、有沉默控制、终极技有斩杀强化。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_garen_decisive_strike` | 决然一击 (Decisive Strike) | `1` | `single` | `stamina 1` | `1` | 伤害 `110%`；施加 `staggered 60 TU` |
| `lol_garen_courage` | 勇气 (Courage) | `self` | `self` | `stamina 1` | `2` | 获得 `guard`；同时提升闪避 `evasion_up 90 TU` |
| `lol_garen_judgment` | 审判 (Judgment) | `self` | `radius 1` | `stamina 2` | `2` | 伤害 `90%`；对 `radius 1` 内所有敌人各结算一次 |
| `lol_garen_demacian_justice` | 德玛西亚正义 (Demacian Justice) | `4` | `single` | `aura 2` | `5` | 伤害 `160%`；目标生命越低追加伤害越高（首版按固定倍率） |

### 逐技能说明

**决然一击 (Decisive Strike)**
- Q 技能核心是"短冲 + 沉默 + 重击"。
- 战棋化后沉默翻译成 `staggered`（无法使用技能行动），保留近战接敌感。
- 伤害低于 `ashen_leaping_cleave`，但控制可靠，是稳定的开场技。
- 与 `ashen_shield_parry` 的区别：决然一击是进攻发起方向，破势盾反是反制方向。

**勇气 (Courage)**
- W 核心是"坚不可摧的一瞬"，在战棋里做成 `guard + evasion_up` 双层叠加。
- 让盖伦能在正面硬扛一整轮而不被打穿，与战士系被动护甲积累分层，不重叠。
- 不做被动叠甲，那会破坏战棋的资源消耗节奏。

**审判 (Judgment)**
- 最标志性的旋转技，`radius 1` = 周围最多 6 格，理论同时命中 2~4 个敌人。
- 伤害倍率不高但多目标覆盖，是清线、拆阵型的主力技。
- 与 `ashen_force_burst` 的区别：审判是纯伤害无推退，原力震荡是低伤强推控。

**德玛西亚正义 (Demacian Justice)**
- 超长冷却大招，`aura 2` 代表需要积累状态才能释放。
- "目标生命越低，伤害越高"是设计说明层的斩杀触发条件，运行时首版按固定 `160%` 落地。
- 射程 `4` 是为了保留"从天而降"的感觉，不是全图技，而是需要中等距离投入战场再释放。

---

## 二、拉克丝（后排法师）

**定位**：超远距离控制法师。有穿透定根、友军护盾、延迟区域减速、超远直线大招。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_lux_light_binding` | 光之束缚 (Light Binding) | `5` | `line 1` | `mp 1` | `1` | 伤害 `70%`；命中施加 `pinned 90 TU`；穿透命中第二目标效果减半 |
| `lol_lux_prismatic_barrier` | 棱光天障 (Prismatic Barrier) | `5` | `line 1` | `mp 1` | `2` | 向指定直线上的友军施加 `guard`（含拉克丝自身） |
| `lol_lux_lucent_singularity` | 微光奇点 (Lucent Singularity) | `5` | `radius 1` | `mp 2` | `2` | 伤害 `90%`；区域留下 `slow 60 TU` 减速地场 |
| `lol_lux_final_spark` | 终极闪光 (Final Spark) | `7` | `line 2` | `mp 3` | `5` | 伤害 `220%`；超远直线光束命中路径上所有敌人 |

### 逐技能说明

**光之束缚 (Light Binding)**
- LoL 里最有辨识度的 Q，可穿透命中两个目标。
- 战棋化后 `line 1` 表示从施法者到目标的直线路径上命中最多 2 名敌人。
- 第一目标 `pinned 90 TU`，第二目标 `pinned 60 TU`；首版可简化为对直线上的第一个目标施加 `pinned`，第二个目标施加 `staggered`（方向感不同）。
- 是拉克丝最稳定的控制技，也是配合 R 技前置的最重要搭配。

**棱光天障 (Prismatic Barrier)**
- 独特双段护盾：飞出去和弹回来各结算一次。
- 战棋化简化为对目标直线上所有友军施加 `guard`（含拉克丝自身）。
- 纯功能技，不做伤害结算，是少数 `target_team_filter = ally + line` 组合的技能。

**微光奇点 (Lucent Singularity)**
- E 技核心是"先丢光球到区域，再引爆"。
- 战棋化简化为到点即刻结算：伤害 + 留下减速地场。
- 减速地场语义：敌人进入该格移动消耗 +1，持续 60 TU；首版如果地场系统未完成，用 AOE `slow` 状态替代。

**终极闪光 (Final Spark)**
- R 的核心是超远距离高伤直线光束，是 LoL 里最有辨识度的大招之一。
- `line 2` 表示宽度 2 格的直线扫射，可命中走廊式多目标。
- `mp_cost 3` 是全局最高单次 MP 消耗，体现它是拉克丝一回合的全部赌注。
- **注意**：`line 2`（2 格宽直线）依赖 `area_direction_mode` 扩展，首版可退化为 `line 1` 等 area 扩展完成。

---

## 三、艾希（弓箭手）

**定位**：减速控制型弓手。多段输出、范围减速、侦察视野、超远单体眩晕。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_ashe_focused_fire` | 专注连射 (Ranger's Focus) | `5` | `single` | `stamina 2` | `2` | 连续射出 3 箭各伤害 `60%`；全部命中后附加 `slow 90 TU` |
| `lol_ashe_volley` | 万箭齐发 (Volley) | `4` | `cone 1` | `stamina 1` | `1` | 伤害 `80%`；扇面内所有目标获得 `slow 60 TU` |
| `lol_ashe_hawkshot` | 鹰击长空 (Hawkshot) | `6` | `radius 2` | `stamina 1` | `3` | 非伤害；揭露目标区域 `radius 2` 内隐藏单位与地形，持续 `90 TU` |
| `lol_ashe_crystal_arrow` | 魔法水晶箭 (Enchanted Crystal Arrow) | `7` | `single` | `stamina 3` | `5` | 伤害 `130%`；叠加 `staggered 120 TU` 与 `pinned 90 TU`，完全封锁目标 |

### 逐技能说明

**专注连射 (Ranger's Focus)**
- LoL 里 Q 本质是积累 focus 叠层后爆发输出。
- 战棋化简化为"一次施法触发三段轻伤 + 减速"，保留多段手感。
- 三次攻击分开结算，每次 `60%`，理论总倍率 `180%`。
- `slow` 让被命中目标难以机动，配合 Volley 形成完整的减速控场链。
- **注**：当前运行时单次命令只做一次静态结算（已知缺口），多段效果链首版用单次伤害 + 注释代替。

**万箭齐发 (Volley)**
- 经典艾希 W，扇形打出七箭覆盖一片区域。
- 战棋化为 `cone 1` 范围，中距离清线利器。
- 伤害不高，`slow` 才是核心价值：整体 cone 内的敌人都被减速，适合配合全队集火。

**鹰击长空 (Hawkshot)**
- 纯侦察技能，LoL 里用于地图视野。
- 战棋化后设计为"揭露该区域隐藏状态"，`revealed` 状态让该区域内隐藏单位可被选中和攻击。
- 不做伤害，`stamina_cost` 低，冷却合理，是艾希战场信息优势的来源。
- **注**：`revealed` 视野状态依赖视野/雾战机制，当前战棋无此系统，此技能可暂时挂起或以"移除目标 guard / stealth" 代替。

**魔法水晶箭 (Enchanted Crystal Arrow)**
- 最标志性的招牌 R，超远单体射击 + 双重控制。
- `staggered 120 TU`（行动受限）+ `pinned 90 TU`（无法位移）双控叠加，比单一控制更重。
- `stamina 3` 消耗说明这是不能随便打出去的终极技，必须等准确时机。
- `range 7` 是全系统最远射程，能在战场另一端发动奇袭。

---

## 四、GDScript 注册代码参考

以下代码可加入 `design_skill_catalog.gd`，新增 `register_lol_skills()` 方法：

```gdscript
func register_lol_skills(register_skill: Callable) -> void:
    # ── 盖伦 ──────────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_garen_decisive_strike", "决然一击",
        "冲向目标重击；命中施加 `staggered`（沉默，无法使用技能），持续 `60 TU`。",
        [&"warrior", &"melee", &"control", &"lol"],
        1, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 1, 5,
        [_build_damage_effect(11, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"staggered", 1)],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_garen_courage", "勇气",
        "强化防御姿态；获得 `guard` 并提升闪避（`evasion_up`），持续 `90 TU`。",
        [&"warrior", &"melee", &"defense", &"lol"],
        0, &"unit", &"ally", &"self", 0,
        1, 0, 1, 0, 2, 0,
        [_build_status_effect(&"guard", 1),
         _build_status_effect(&"evasion_up", 1)],
        [30, 50, 80], 3, &"self", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_garen_judgment", "审判",
        "旋转挥砍 `radius 1` 内所有敌人，各造成 `90%` 物理伤害。",
        [&"warrior", &"melee", &"aoe", &"lol"],
        0, &"ground", &"enemy", &"radius", 1,
        2, 0, 2, 0, 2, 0,
        [_build_damage_effect(9, &"physical_attack", &"physical_defense")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_garen_demacian_justice", "德玛西亚正义",
        "召唤巨剑斩落；造成 `160%` 伤害，目标生命越低追加伤害越高（首版固定倍率）。",
        [&"warrior", &"melee", &"output", &"execute", &"lol"],
        4, &"unit", &"enemy", &"single", 0,
        1, 0, 0, 2, 5, 10,
        [_build_damage_effect(16, &"physical_attack", &"physical_defense")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    # ── 拉克丝 ────────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_lux_light_binding", "光之束缚",
        "发射光球穿透直线；命中的目标施加 `pinned 90 TU`（首版以 `staggered` 代替，等 pinned 状态完善后替换）。",
        [&"mage", &"magic", &"arcane", &"control", &"lol"],
        5, &"ground", &"enemy", &"line", 1,
        1, 1, 0, 0, 1, 5,
        [_build_damage_effect(7, &"magic_attack", &"magic_defense"),
         _build_status_effect(&"staggered", 2)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_lux_prismatic_barrier", "棱光天障",
        "向指定方向发射护盾光球；对直线上的所有友军施加 `guard`（含拉克丝自身）。",
        [&"mage", &"magic", &"arcane", &"support", &"lol"],
        5, &"ground", &"ally", &"line", 1,
        1, 1, 0, 0, 2, 0,
        [_build_status_effect(&"guard", 1, 1, &"ally")],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_lux_lucent_singularity", "微光奇点",
        "在目标区域落下光球造成伤害；留下 `slow 60 TU` 减速地场（首版以 `staggered 1` 代替）。",
        [&"mage", &"magic", &"arcane", &"aoe", &"lol"],
        5, &"ground", &"enemy", &"radius", 1,
        1, 2, 0, 0, 2, 0,
        [_build_damage_effect(9, &"magic_attack", &"magic_defense"),
         _build_status_effect(&"staggered", 1)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_lux_final_spark", "终极闪光",
        "发射超远距离光线贯穿整条直线；伤害 `220%`，命中路径上所有敌人。`line 2` 宽度依赖 area 扩展，首版退化为 `line 1`。",
        [&"mage", &"magic", &"arcane", &"output", &"lol"],
        7, &"ground", &"enemy", &"line", 1,
        1, 3, 0, 0, 5, 5,
        [_build_damage_effect(22, &"magic_attack", &"magic_defense")],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    # ── 艾希 ──────────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_ashe_focused_fire", "专注连射",
        "连续射出 3 箭各伤害 `60%`；全部命中后附加 `slow 90 TU`（多段结算首版简化为单次 `60%` + slow）。",
        [&"archer", &"ranged", &"bow", &"output", &"lol"],
        5, &"unit", &"enemy", &"single", 0,
        1, 0, 2, 0, 2, 5,
        [_build_damage_effect(6, &"physical_attack", &"physical_defense"),
         _build_damage_effect(6, &"physical_attack", &"physical_defense"),
         _build_damage_effect(6, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"staggered", 1)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_ashe_volley", "万箭齐发",
        "向前扇面射出七箭；命中的所有敌人获得 `slow 60 TU`（首版以 `staggered 1` 代替）。",
        [&"archer", &"ranged", &"bow", &"aoe", &"control", &"lol"],
        4, &"ground", &"enemy", &"cone", 1,
        1, 0, 1, 0, 1, 0,
        [_build_damage_effect(8, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"staggered", 1)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_ashe_hawkshot", "鹰击长空",
        "侦察鹰飞向目标区域；揭露 `radius 2` 内隐藏单位与地形，持续 `90 TU`。视野系统未完成时此技能挂起。",
        [&"archer", &"ranged", &"bow", &"utility", &"scout", &"lol"],
        6, &"ground", &"ally", &"radius", 2,
        1, 0, 1, 0, 3, 0,
        [],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_ashe_crystal_arrow", "魔法水晶箭",
        "射出超远水晶巨箭；命中叠加 `staggered 120 TU` 与 `pinned 90 TU`，完全封锁目标。首版 pinned 以 `staggered` 延长代替。",
        [&"archer", &"ranged", &"bow", &"control", &"lol"],
        7, &"unit", &"enemy", &"single", 0,
        1, 0, 3, 0, 5, 5,
        [_build_damage_effect(13, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"staggered", 2)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
```

---

## 五、实现优先级与当前缺口

### 优先级表

| 优先级 | skill_id | 原因 |
| --- | --- | --- |
| 高 | `lol_garen_judgment` | `radius 1 AOE` 机制有 `ashen_force_burst` 参考，最容易落地 |
| 高 | `lol_ashe_volley` | `cone + staggered` 组合已有 `archer_fearsignal_shot` 参考 |
| 高 | `lol_garen_decisive_strike` | `melee + staggered` 已有 `warrior_shield_bash` 参考 |
| 中 | `lol_lux_light_binding` | `line + staggered` 已可落地，穿透第二目标逻辑是扩展 |
| 中 | `lol_ashe_focused_fire` | 多段效果链依赖运行时缺口，首版退化为单次 |
| 中 | `lol_ashe_crystal_arrow` | 双控叠加需验证 `staggered` 延长的状态叠层语义 |
| 低 | `lol_garen_demacian_justice` | `aura_cost` 资源链需确认运行时扣费闭环 |
| 低 | `lol_lux_prismatic_barrier` | `ally + line` 组合目前少有参考，需要测试目标选择逻辑 |
| 低 | `lol_lux_final_spark` | `line 2` 宽度依赖 `area_direction_mode` 扩展 |
| 搁置 | `lol_ashe_hawkshot` | 视野 / 雾战系统当前不存在，技能效果无法结算 |

### 当前运行时缺口（参照 skills_implementation_plan.md）

1. **`pinned` 状态独立语义**：当前 `staggered` 是行动完全受限；`pinned`（禁止移动、可使用技能）需要在状态语义表中独立拆分。首版用 `staggered` 代替，后续替换。
2. **`slow` 减速状态**：移动消耗 +1 的状态当前没有独立 ID，首版用 `staggered 1` 近似，后续补入状态表。
3. **多段伤害效果链**：专注连射的三段命中需要运行时支持单次命令多次静态结算，当前已知缺口。
4. **`line 2` 宽度扩展**：终极闪光理想形态，依赖 `area_direction_mode` 字段落地，首版退化为 `line 1`。
5. **`aura_cost` 扣费闭环**：德玛西亚正义用 `aura_cost 2`，需确认 `aura` 资源在运行时已有扣费与恢复逻辑。
6. **视野系统**：鹰击技能完全依赖该系统，当前无法落地。

---

## 六、亚索（风系近战突进）

**定位**：近战突进型，移动灵活，风墙控场，依赖对方失衡（staggered）来发动大招。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_yasuo_steel_tempest` | 斩钢闪 (Steel Tempest) | `3` | `line 1` | `stamina 1` | `1` | 伤害 `100%`；直线气斩；第三次释放升级为龙卷风并施加 `staggered 60 TU` |
| `lol_yasuo_wind_wall` | 风之壁 (Wind Wall) | `3` | `line 1` | `stamina 1` | `3` | 对直线上的友军施加 `evasion_up 60 TU`；概念上阻挡投射物 |
| `lol_yasuo_sweeping_blade` | 踏前斩 (Sweeping Blade) | `2` | `single` | `stamina 1` | `0` | 伤害 `70%`；突进到目标旁并穿越；冷却极短可连用 |
| `lol_yasuo_last_breath` | 狂风绝息斩 (Last Breath) | `4` | `single` | `aura 2` | `5` | 伤害 `200%`；仅可对 `staggered` 状态的目标使用；首版去除前置限制 |

### 逐技能说明

**斩钢闪 (Steel Tempest)**
- LoL 里的 Q 前两次是刺击，第三次变龙卷风（上挑击飞）。
- 战棋化简化为直线斩击，第三次触发带 `staggered`（击飞/失衡）的强化版。
- 首版只实现龙卷风形态：`line 1` + `staggered`，普通两击形态以连续使用低冷却来模拟节奏感。

**风之壁 (Wind Wall)**
- LoL 里 W 生成一道阻挡所有投射物的风墙屏障，是战略性最强的技能之一。
- 当前框架没有"阻断投射物"机制，战棋化为直线上的友军获得 `evasion_up`，在高命中攻击前使用可降低被弓箭或法术命中的概率。
- 未来若实现投射物判定体系，可升级为真正的格挡屏障。

**踏前斩 (Sweeping Blade)**
- LoL 里 E 可以反复穿越不同敌人，是亚索的核心游走工具。
- 战棋化为位移 + 轻伤：使用后单位移动到目标身旁（`forced_move` 到相邻格），顺带对目标结算轻伤。
- 冷却 `0` 表示可以连续穿越多个目标，每次穿越不同目标都可重复激活。

**狂风绝息斩 (Last Breath)**
- 只能对空中（击飞/失衡）的目标发动，这是亚索的核心联动逻辑。
- 战棋化后限制为"优先对 `staggered` 状态目标使用，伤害为 `200%`"——在技能说明层做语义限定。
- 首版去除强制前置条件（避免无法使用），改为"目标处于 `staggered` 时追加额外效果"的描述条件。

### GDScript 代码

```gdscript
    # ── 亚索 ──────────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_yasuo_steel_tempest", "斩钢闪",
        "气剑横扫直线；第三次释放升级为龙卷风，对命中敌人施加 `staggered 60 TU`（首版统一实现龙卷风形态）。",
        [&"warrior", &"melee", &"output", &"lol"],
        3, &"ground", &"enemy", &"line", 1,
        1, 0, 1, 0, 1, 5,
        [_build_damage_effect(10, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"staggered", 1)],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_yasuo_wind_wall", "风之壁",
        "在前方生成风墙；对直线上的友军施加 `evasion_up 60 TU`，概念上阻挡来袭投射物。",
        [&"warrior", &"melee", &"defense", &"lol"],
        3, &"ground", &"ally", &"line", 1,
        1, 0, 1, 0, 3, 0,
        [_build_status_effect(&"evasion_up", 1, 1, &"ally")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_yasuo_sweeping_blade", "踏前斩",
        "突进并穿越目标，造成轻伤；冷却极短，可连续对不同目标使用。",
        [&"warrior", &"melee", &"mobility", &"lol"],
        2, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 0, 5,
        [_build_damage_effect(7, &"physical_attack", &"physical_defense"),
         _build_forced_move_effect(1, &"retreat")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_yasuo_last_breath", "狂风绝息斩",
        "对处于 `staggered` 的目标造成 `200%` 伤害；首版无前置状态限制，伤害固定 `200%`。",
        [&"warrior", &"melee", &"output", &"execute", &"lol"],
        4, &"unit", &"enemy", &"single", 0,
        1, 0, 0, 2, 5, 10,
        [_build_damage_effect(20, &"physical_attack", &"physical_defense")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))
```

---

## 七、诺克萨斯之手（强压肉近战）

**定位**：高压抓人型战士。有流血叠层、拉近机制、击杀刷新大招。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_darius_decimate` | 血与杀戮 (Decimate) | `self` | `radius 1` | `stamina 2` | `2` | 伤害 `100%`；边缘（`radius 1` 外沿）命中追加 `30%` 并施加 `bleeding 120 TU` |
| `lol_darius_crippling_strike` | 致残打击 (Crippling Strike) | `1` | `single` | `stamina 1` | `1` | 伤害 `120%`；施加 `staggered 60 TU` + `armor_break 90 TU` |
| `lol_darius_apprehend` | 掠夺 (Apprehend) | `3` | `single` | `stamina 1` | `2` | 无伤害；将目标强制拉至自身相邻格（`forced_move pull 2`）；施加 `armor_break 60 TU` |
| `lol_darius_guillotine` | 诺克萨斯断头台 (Noxian Guillotine) | `2` | `single` | `aura 2` | `3` | 伤害 `180%`；击杀目标后 `aura` 返还并冷却重置（首版按固定倍率） |

### 逐技能说明

**血与杀戮 (Decimate)**
- LoL 里 Q 的独特之处在于"斧头边缘伤害更高"，中心只吃 `35%`，外沿才是 `100%`。
- 战棋化简化为 `radius 1` 全覆盖，但在设计说明层标注"外沿优势"——首版按统一伤害 `100%` 结算，精进形态再引入内外圈分流。
- 附加 `bleeding`（出血）是达瑞斯流血叠层的核心，运行时可以先用 `burning` 作为占位代理。

**致残打击 (Crippling Strike)**
- W 的核心是"减速 + 重击"的组合，为掠夺或追击创造窗口。
- 战棋化后伤害 `120%` + `staggered`（近似减速限制行动）+ `armor_break`（模拟流血造成的防御削减）。
- 两个状态叠加让它成为达瑞斯进攻链中最有价值的单次指令之一。

**掠夺 (Apprehend)**
- E 的核心是"把逃跑的敌人拉回来"，是达瑞斯最有战略价值的技能。
- 战棋化为 `forced_move pull 2`：将目标从当前位置拉至自身相邻格。
- 不做伤害，纯功能。`armor_break` 模拟 LoL 里的穿甲被动效果。

**诺克萨斯断头台 (Noxian Guillotine)**
- R 的斩杀哲学：流血叠层越高，伤害越高；击杀后重置冷却。
- 战棋化首版固定 `180%` 伤害；击杀重置通过设计说明层描述，运行时首版不实现重置逻辑。
- `aura 2` 代价说明这不是随便打出去的技能，需要积累局面优势。

### GDScript 代码

```gdscript
    # ── 诺克萨斯之手 ──────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_darius_decimate", "血与杀戮",
        "旋转巨斧劈砍周围 `radius 1`；外沿命中追加 `30%` 伤害（首版固定 `100%`）并施加 `burning 120 TU`（流血代理）。",
        [&"warrior", &"melee", &"aoe", &"lol"],
        0, &"ground", &"enemy", &"radius", 1,
        2, 0, 2, 0, 2, 0,
        [_build_damage_effect(10, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"burning", 3)],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_darius_crippling_strike", "致残打击",
        "重击目标造成 `120%` 伤害；施加 `staggered 60 TU` 与 `armor_break 90 TU`。",
        [&"warrior", &"melee", &"control", &"lol"],
        1, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 1, 5,
        [_build_damage_effect(12, &"physical_attack", &"physical_defense"),
         _build_status_effect(&"staggered", 1),
         _build_status_effect(&"armor_break", 2)],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_darius_apprehend", "掠夺",
        "钩爪抓取远处目标并强行拉至身旁（`forced_move pull 2`）；附加 `armor_break 60 TU`。",
        [&"warrior", &"melee", &"control", &"lol"],
        3, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 2, 0,
        [_build_forced_move_effect(2, &"pull"),
         _build_status_effect(&"armor_break", 1)],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_darius_guillotine", "诺克萨斯断头台",
        "斩落目标造成 `180%` 伤害；击杀后 `aura` 返还并重置冷却（首版固定倍率，不实现重置）。",
        [&"warrior", &"melee", &"output", &"execute", &"lol"],
        2, &"unit", &"enemy", &"single", 0,
        1, 0, 0, 2, 3, 10,
        [_build_damage_effect(18, &"physical_attack", &"physical_defense")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))
```

---

## 八、安妮（召唤型法师）

**定位**：近中距离爆发法师。有被动眩晕叠层、近距离喷火、自身/友军护盾、大范围眩晕召唤。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_annie_disintegrate` | 焚烧 (Disintegrate) | `4` | `single` | `mp 1` | `0` | 伤害 `100%`；附加 `burning 90 TU`；击杀目标则返还本次 `mp` 消耗 |
| `lol_annie_incinerate` | 焚化 (Incinerate) | `self` | `cone 1` | `mp 2` | `1` | 伤害 `110%`；近距离喷火，对 cone 内所有敌人各结算；附加 `burning 90 TU` |
| `lol_annie_molten_shield` | 熔岩护盾 (Molten Shield) | `3` | `single` | `mp 1` | `2` | 对自身或友军施加 `guard + evasion_up 60 TU`；攻击持盾者的近战额外受 `反弹伤害`（设计层描述） |
| `lol_annie_tibbers` | 提伯斯！(Summon: Tibbers) | `4` | `radius 1` | `mp 3` | `5` | 伤害 `180%`；对 `radius 1` 内所有敌人施加 `staggered 120 TU`；召唤提伯斯（占位单位） |

### 逐技能说明

**焚烧 (Disintegrate)**
- Q 的精髓是"低耗、快、击杀退费"，这使得安妮可以对残血目标不断补刀而几乎不消耗 MP。
- 战棋化保留击杀返还 MP 这一核心语义，首版通过设计说明层描述，运行时事件钩子完成后再实现。
- `burning` 是火焰叠层的来源，与安妮叠眩晕的被动搭配使用。

**焚化 (Incinerate)**
- W 是安妮最直接的 AOE，近距离扇形烧伤。
- 战棋化为 `cone 1` 喷火，适合贴脸压制，配合 E 护盾的防反使用。
- 与拉克丝 W 不同，这是纯伤害技，不带控制，但覆盖范围和 `burning` 叠层是价值所在。

**熔岩护盾 (Molten Shield)**
- 安妮 E 的特色是"护盾反弹近战伤害"，让攻击者受到惩罚。
- 战棋化为 `guard + evasion_up`；反弹伤害（攻击者受到少量魔法伤害）在设计说明层保留，运行时反弹逻辑等反击体系完善后实现。
- 可以施放在友军身上，是少数对友军有效的辅助型法师技能。

**提伯斯！(Summon: Tibbers)**
- R 是最具辨识度的大招之一：爆炸眩晕 + 召唤熊熊燃烧。
- 战棋化聚焦在核心价值：`radius 1` 范围大伤害 + 群体 `staggered`，直接打断对方的阵型。
- 提伯斯占位单位（召唤物）的实现依赖召唤物系统，首版只做爆炸效果，不生成实体单位。

### GDScript 代码

```gdscript
    # ── 安妮 ──────────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_annie_disintegrate", "焚烧",
        "单体火焰轰击造成 `100%` 伤害，附加 `burning 90 TU`；击杀目标则返还 `1 mp`（首版返还逻辑依赖事件钩子）。",
        [&"mage", &"magic", &"fire", &"lol"],
        4, &"unit", &"enemy", &"single", 0,
        1, 1, 0, 0, 0, 10,
        [_build_damage_effect(10, &"magic_attack", &"magic_defense", &"fire_resistance"),
         _build_status_effect(&"burning", 2)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_annie_incinerate", "焚化",
        "近距离扇形喷火，对 `cone 1` 内所有敌人造成 `110%` 伤害并施加 `burning 90 TU`。",
        [&"mage", &"magic", &"fire", &"aoe", &"lol"],
        0, &"ground", &"enemy", &"cone", 1,
        1, 2, 0, 0, 1, 5,
        [_build_damage_effect(11, &"magic_attack", &"magic_defense", &"fire_resistance"),
         _build_status_effect(&"burning", 2)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_annie_molten_shield", "熔岩护盾",
        "对自身或友军施加 `guard + evasion_up 60 TU`；攻击持盾者的近战受少量反弹伤害（首版反弹依赖反击体系）。",
        [&"mage", &"magic", &"fire", &"support", &"lol"],
        3, &"unit", &"ally", &"single", 0,
        1, 1, 0, 0, 2, 0,
        [_build_status_effect(&"guard", 1, 1, &"ally"),
         _build_status_effect(&"evasion_up", 1, 1, &"ally")],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_annie_tibbers", "提伯斯！",
        "召唤提伯斯砸落落点 `radius 1`，造成 `180%` 伤害并对命中敌人施加 `staggered 120 TU`；召唤物占位首版不实现。",
        [&"mage", &"magic", &"fire", &"aoe", &"summon", &"lol"],
        4, &"ground", &"enemy", &"radius", 1,
        1, 3, 0, 0, 5, 5,
        [_build_damage_effect(18, &"magic_attack", &"magic_defense", &"fire_resistance"),
         _build_status_effect(&"staggered", 2)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))
```

---

## 九、卡特琳娜（近战刺客）

**定位**：连斩型刺客。有飞刃弹射、预备加强、闪现突进、持续旋斩大招。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_katarina_bouncing_blade` | 飞刃 (Bouncing Blade) | `4` | `single` | `stamina 1` | `1` | 伤害 `80%`；短刃弹射命中后在目标格附近落下一枚短刃标记 |
| `lol_katarina_preparation` | 浴血殊途 (Preparation) | `self` | `self` | `stamina 0` | `1` | 在自身格落下短刃，获得 `evasion_up 60 TU`；下次技能命中追加 `30%` 伤害 |
| `lol_katarina_shunpo` | 闪烁匕首 (Shunpo) | `3` | `single` | `stamina 1` | `1` | 伤害 `90%`；突进到目标（敌、友或短刃标记）所在格相邻位置 |
| `lol_katarina_death_lotus` | 死亡莲华 (Death Lotus) | `self` | `radius 1` | `stamina 3` | `4` | 持续旋斩；对 `radius 1` 内所有敌人造成 `3 × 60%` 连续伤害（多段首版退化为单次 `140%`） |

### 逐技能说明

**飞刃 (Bouncing Blade)**
- Q 的独特机制是"短刃弹射后落在地面，之后被 E 或 R 联动"。
- 战棋化为单体伤害 + 在地面留下"短刃"标记（通过 `_build_special_effect` 描述）。
- 标记本身是卡特琳娜套路的核心：它是 E 可以跳过去的落脚点，也触发 R 的联动伤害。

**浴血殊途 (Preparation)**
- W 是"在地上丢一把刀，然后提升自己"的预备技能。
- 战棋化为：自身格落下短刃 + `evasion_up`（准备下次爆发的身位调整）。
- 不做伤害，零消耗，是卡特琳娜建立场控优势的先手动作。

**闪烁匕首 (Shunpo)**
- E 是卡特琳娜最关键的位移工具，可以跳向任何"锚点"（敌人、友军、短刃）。
- 战棋化为突进到目标相邻格并结算轻伤。
- 在战棋里，可以朝短刃标记位置发动 E 来快速靠近，也可以直接跳向敌人背后。

**死亡莲华 (Death Lotus)**
- R 是持续型群体输出技，一旦被打断就中止。
- 战棋化的多段命中依赖运行时多段效果链；首版退化为单次结算 `140%`，设计层保留多段语义。
- `radius 1` 打中所有相邻敌人，让卡特琳娜在突入阵型后能最大化发挥。

### GDScript 代码

```gdscript
    # ── 卡特琳娜 ──────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_katarina_bouncing_blade", "飞刃",
        "投出短刃弹射单体目标造成 `80%` 伤害；命中后在目标格附近留下短刃标记（占位效果，等道具落点系统支持）。",
        [&"warrior", &"melee", &"output", &"lol"],
        4, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 1, 5,
        [_build_damage_effect(8, &"physical_attack", &"physical_defense")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_katarina_preparation", "浴血殊途",
        "在自身格落下短刃（标记），获得 `evasion_up 60 TU`；下次技能命中追加 `30%` 伤害（首版只实现 evasion_up）。",
        [&"warrior", &"melee", &"mobility", &"lol"],
        0, &"unit", &"ally", &"self", 0,
        1, 0, 0, 0, 1, 0,
        [_build_status_effect(&"evasion_up", 1)],
        [30, 50, 80], 3, &"self", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_katarina_shunpo", "闪烁匕首",
        "突进到目标（敌、友或地面短刃位置）相邻格，造成 `90%` 伤害。",
        [&"warrior", &"melee", &"mobility", &"lol"],
        3, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 1, 5,
        [_build_damage_effect(9, &"physical_attack", &"physical_defense"),
         _build_forced_move_effect(1, &"retreat")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_katarina_death_lotus", "死亡莲华",
        "持续旋斩 `radius 1` 内所有敌人，理论 `3 × 60%`；首版退化为单次 `140%`，等多段效果链完成后还原。",
        [&"warrior", &"melee", &"aoe", &"lol"],
        0, &"ground", &"enemy", &"radius", 1,
        2, 0, 3, 0, 4, 0,
        [_build_damage_effect(14, &"physical_attack", &"physical_defense")],
        [30, 50, 80], 3, &"single_unit", 1, 1, &"stable", &"book"))
```

---

## 十、薇恩（弓手刺客）

**定位**：近距离弓手刺客。翻滚灵活、银栓真实伤害、钉墙眩晕、大招进入强化状态。

### 技能速览表

| skill_id | 技能名（LoL 原名） | 射程 | 范围 | 消耗 | 冷却 | 核心效果 |
| --- | --- | --- | --- | --- | --- | --- |
| `lol_vayne_tumble` | 翻滚 (Tumble) | `self` | `self` | `stamina 1` | `1` | 立即位移最多 `2` 格，获得 `evasion_up 60 TU`；下次攻击伤害提升 `+30%` |
| `lol_vayne_silver_bolts` | 银栓 (Silver Bolts) | — | — | — | — | **被动**：每第 3 次命中同一目标追加真实伤害（忽略防御），伤害按目标最大生命比例计算 |
| `lol_vayne_condemn` | 受难 (Condemn) | `1` | `single` | `stamina 1` | `2` | 伤害 `80%`；将目标推退 `2` 格；若被推至地形边缘则追加 `staggered 90 TU` |
| `lol_vayne_final_hour` | 最后时刻 (Final Hour) | `self` | `self` | `stamina 2` | `4` | 获得 `evasion_up 90 TU + armor_break_immunity`；持续期间翻滚冷却减半（首版只实现增益状态） |

### 逐技能说明

**翻滚 (Tumble)**
- Q 是薇恩的核心移动工具，每次翻滚后下次普攻更强。
- 战棋化为 `位移 2 格 + evasion_up`，保留翻滚规避感。
- "下次攻击伤害提升"通过设计说明层标注，运行时等"增强普攻"体系实现后接入。

**银栓 (Silver Bolts)**
- W 是纯被动，不设计为主动技能。
- 战棋化为 `_build_passive_skill`，描述层说明第三次命中追加真实伤害。
- "真实伤害"（忽略防御）在运行时对应 `_build_damage_effect` 不传 `defense_attribute_id`。

**受难 (Condemn)**
- E 的核心是"推撞 + 碰壁眩晕"，是薇恩的主要控制手段。
- 战棋化为 `forced_move push 2` + 设计说明层的"碰墙触发 staggered"——运行时地形边缘碰撞逻辑需要新增。
- 首版推退 `2` 格 + 直接施加 `staggered 1`（不区分是否碰墙，等边缘判定完成后修正）。

**最后时刻 (Final Hour)**
- R 是大招强化状态，给薇恩一段时间的大幅增益。
- 战棋化为 `evasion_up` 叠加（大幅提升闪避）+ 描述层标注翻滚冷却减半。
- `armor_break_immunity`（抵抗护甲削减）当前没有独立状态 ID，首版只实现 `evasion_up`，作为 R 技的核心增益来源。

### GDScript 代码

```gdscript
    # ── 薇恩 ──────────────────────────────────────────────────────────────
    register_skill.call(_build_active_skill(
        &"lol_vayne_tumble", "翻滚",
        "立即位移最多 `2` 格并获得 `evasion_up 60 TU`；下次攻击伤害提升 `+30%`（首版增强普攻逻辑依赖后续系统）。",
        [&"archer", &"ranged", &"bow", &"mobility", &"lol"],
        0, &"unit", &"ally", &"self", 0,
        1, 0, 1, 0, 1, 0,
        [_build_status_effect(&"evasion_up", 1),
         _build_forced_move_effect(2, &"retreat")],
        [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))

    register_skill.call(_build_passive_skill(
        &"lol_vayne_silver_bolts", "银栓",
        "被动：每第 3 次命中同一目标追加真实伤害（不受防御减免），伤害按目标最大生命 `8%` 计算。运行时可用 `power = 8`、无 `defense_attribute_id` 的 damage effect 实现。",
        [&"archer", &"ranged", &"bow", &"passive", &"lol"]))

    register_skill.call(_build_active_skill(
        &"lol_vayne_condemn", "受难",
        "伤害 `80%` 并将目标推退 `2` 格；若推至地形边缘则追加 `staggered 90 TU`（首版统一施加 `staggered 1`）。",
        [&"archer", &"ranged", &"bow", &"control", &"lol"],
        1, &"unit", &"enemy", &"single", 0,
        1, 0, 1, 0, 2, 5,
        [_build_damage_effect(8, &"physical_attack", &"physical_defense"),
         _build_forced_move_effect(2, &"push"),
         _build_status_effect(&"staggered", 1)],
        [28, 46, 72], 3, &"single_unit", 1, 1, &"stable", &"book"))

    register_skill.call(_build_active_skill(
        &"lol_vayne_final_hour", "最后时刻",
        "进入强化状态，获得大幅 `evasion_up 90 TU`；持续期间翻滚冷却减半（首版只实现 evasion_up）。",
        [&"archer", &"ranged", &"bow", &"defense", &"lol"],
        0, &"unit", &"ally", &"self", 0,
        1, 0, 2, 0, 4, 0,
        [_build_status_effect(&"evasion_up", 2)],
        [28, 46, 72], 3, &"self", 1, 1, &"stable", &"book"))
```

---

## 十一、完整实现优先级（全 8 位英雄）

### 按框架支持度分级

| 级别 | 条件 | 代表技能 |
| --- | --- | --- |
| **即可落地** | 使用已有状态 + 已有 area_pattern | `lol_garen_judgment` / `lol_ashe_volley` / `lol_darius_crippling_strike` / `lol_annie_incinerate` |
| **需要微扩展** | 需要新增 `slow` / `pull` forced_move mode / `ally line` | `lol_darius_apprehend` / `lol_lux_prismatic_barrier` / `lol_vayne_condemn` |
| **依赖系统缺口** | 需要多段效果链 / 击杀返还 / 重置逻辑 | `lol_ashe_focused_fire` / `lol_annie_disintegrate` / `lol_darius_guillotine` / `lol_katarina_death_lotus` |
| **需要新系统** | 视野 / 召唤物 / 投射物阻挡 / 增强普攻 | `lol_ashe_hawkshot` / `lol_annie_tibbers`（召唤部分）/ `lol_yasuo_wind_wall`（阻挡语义）/ `lol_vayne_tumble`（增强普攻） |

### 新增的运行时缺口（补充原有六条）

7. **`forced_move pull` 模式**：掠夺（E）需要将目标向自身方向拉近，当前 `forced_move_mode` 已有 `retreat`，需要补充 `pull`。
8. **`burning` 作为流血代理**：达瑞斯流血叠层首版用 `burning` 替代，后续可以补充 `bleeding` 状态 ID 并在状态语义表中独立注册。
9. **击杀事件钩子**：安妮 Q 返还 MP、达瑞斯 R 重置冷却都依赖击杀事件触发；当前 `BattleRuntimeModule` 无对应钩子，需要事件系统扩展。
10. **地形边缘碰撞判定**：薇恩 E 的碰墙眩晕依赖地形边缘判定逻辑，`BattleGridService` 当前无此接口。
11. **被动银栓叠层**：薇恩 W 是对同一目标命中计数的被动，运行时当前没有"对特定目标的命中计数"机制。
