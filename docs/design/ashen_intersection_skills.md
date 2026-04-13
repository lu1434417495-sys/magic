# 灰烬交界特殊技能策划表

更新日期：`2026-04-13`

说明：
- 本文档收敛 [ashen_intersection_world_map.md](D:/game/magic/docs/design/ashen_intersection_world_map.md) 中提到的 12 个黑魂风格技能。
- 技能不直接照搬黑魂原作判定，而是按当前项目已存在的战棋技能体系重写。
- 这些技能建议作为 `灰烬交界` 特殊地图中的“据点传授技能”存在，通过 `settlement action -> pending character reward -> skill_unlock / skill_mastery` 进入角色成长链。
- `skill_id` 使用 `ashen_` 前缀，表示它们来自灰烬交界的异界战技、咒术或祷告传承。
- 本文档中所有 `状态 / 控制 / 增益 / 减益` 持续时间统一按 `TU` 计算，不使用“1 回合 / 2 回合”写法。

---

## 零、状态计时规则

- 单位状态统一按 `BattleTimeline.current_tu` 结算，持续时间字段统一记为 `duration_tu`。
- 本文档中的 `60 TU / 90 TU / 120 TU` 都表示时间轴长度，不表示单位行动次数。
- 约定基准：
  - `60 TU`：短控或短时窗口型增益，例如 `staggered`、`evasion_up`
  - `90 TU`：标准减益或中等持续增益，例如 `armor_break`、`shocked`
  - `120 TU`：持续灼烧、侵蚀等更长尾的持续效果
- 首版若运行时尚未完成单位状态的 `TU` 化改造，则这些技能只算“设计已定”，不接受回退为“按回合减 1”的实现。

---

## 一、技能策划表

| skill_id | 技能名 | 黑魂原型 | 射程 | 范围 | 消耗 | 冷却 | 状态 / 核心效果 | 推荐归属职业 | 建议学习据点 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ashen_roll` | 灰烬翻滚 | 翻滚 | `self` | `self` | `stamina 1` | `1` | 自身获得 `evasion_up 60 TU`，立即位移最多 `2` 格 | 通用战技；战士 / 盗贼 / 弓箭手优先 | `余烬祭所` |
| `ashen_shield_parry` | 破势盾反 | 盾反 | `1` | `single` | `stamina 1` | `2` | 伤害 `80%`；命中施加 `staggered 60 TU`；对防御姿态目标追加 `armor_break 90 TU` | 战士 / 圣骑士 | `断桥营寨` |
| `ashen_guard_kick` | 踢击破防 | 踢盾 | `1` | `single` | `stamina 1` | `1` | 伤害 `60%`；移除 `guard`；施加 `armor_break 90 TU` | 战士 / 狂战士 / 圣骑士 | `断桥营寨` |
| `ashen_leaping_cleave` | 跳劈重斩 | 跳劈 | `2` | `radius 1` | `stamina 2` | `2` | 伤害 `130%`；短距突进到落点；中心目标额外 `staggered 60 TU` | 战士 / 狂战士 | `断桥营寨` |
| `ashen_soul_arrow` | 灵魂箭 | Soul Arrow | `5` | `single` | `mp 1` | `0` | 伤害 `100%`；稳定远程单体奥术输出 | 法师 | `沉钟书库` |
| `ashen_great_soul_arrow` | 强灵魂箭 | Great Soul Arrow | `5` | `single` | `mp 2` | `1` | 伤害 `130%`；高压单点咒术 | 法师 | `沉钟书库` |
| `ashen_black_fire_orb` | 黑火球 | Black Fire Orb | `4` | `radius 1` | `mp 2` | `2` | 伤害 `125%`；附加 `burning 120 TU`；中心目标可追加 `staggered 60 TU` | 法师 / 盗贼副修 | `坠墓镇` |
| `ashen_firebomb` | 火焰壶 | 火焰壶投掷物 | `4` | `radius 1` | `stamina 1` | `1` | 伤害 `90%`；附加 `burning 120 TU`；通用投掷物手感 | 通用；战士 / 盗贼 / 弓箭手优先 | `坠墓镇` |
| `ashen_heal_prayer` | 治愈祷告 | Heal | `4` | `single ally` | `mp 1` | `1` | 治疗 `14`；稳定单体续航 | 牧师 / 圣骑士 | `炭化大教堂` |
| `ashen_force_burst` | 原力震荡 | Force | `self` | `radius 1` | `mp 2` | `2` | 伤害 `70%`；施加 `staggered 60 TU`；推退 `1` 格 | 牧师 / 圣骑士 | `炭化大教堂` |
| `ashen_lightning_spear` | 雷枪 | Lightning Spear | `5` | `line` | `mp 2` | `2` | 伤害 `125%`；附加 `shocked 90 TU`；直线高压雷击 | 牧师 / 圣骑士 / 法师副修 | `炭化大教堂` |
| `ashen_dark_edge` | 黑暗斩痕 | Dark Edge / 黑暗武器式咒术 | `4` | `single` | `mp 2` | `2` | 伤害 `110%`；施加 `奥脆 90 TU`；精进后可附带少量 `life_drain` | 法师 / 盗贼副修 | `沉钟书库` |

---

## 二、逐技能说明

### 1. `ashen_roll` 灰烬翻滚

- 这是把黑魂“翻滚无敌帧”翻译成当前系统可稳定结算的结果。
- 不做无敌判定，直接改成 `短位移 + 短时闪避上升（60 TU）`，这样既保留魂系回避感，又不会破坏现有命中体系。
- 适合作为所有近中距离职业的通用生存战技，是灰烬交界最基础也最泛用的传授技能。

### 2. `ashen_shield_parry` 破势盾反

- 黑魂里的盾反核心是“赌一次读招成功后，让对方露出巨大破绽”。
- 在战棋体系里，这个意义比“反击伤害”更重要，所以设计成 `staggered` 和对防御姿态的额外 `armor_break`。
- 它不是稳定输出按钮，而是高价值反制技，最适合放在重视正面博弈的营寨类据点。

### 3. `ashen_guard_kick` 踢击破防

- 黑魂踢击的精髓是“低伤、强功能、把龟缩敌人踢开”。
- 因此这里给它低倍率、低冷却，但附上 `移除 guard` 和 `armor_break`。
- 它的用途是开口子，给后续高伤技、弓箭手点杀或法术补刀创造条件。

### 4. `ashen_leaping_cleave` 跳劈重斩

- 跳劈是典型的高风险进场动作，必须体现“跃进”和“砸出压迫”。
- 当前体系里最自然的写法就是 `短距突进 + radius 1 落点伤害`。
- 中心目标附加 `staggered`，这样它既是进场技，也是拆阵型和抢节奏的重击技。

### 5. `ashen_soul_arrow` 灵魂箭

- 灵魂箭不需要复杂机制，它的价值本来就来自稳定、远程、低耗。
- 所以保持 `range 5 / mp 1 / 单体稳定输出` 即可，让它成为灰烬交界法系学习的第一块基石。
- 后续的高级咒术都应围绕它升级，而不是一开始就把魂系法术做成复杂连锁。

### 6. `ashen_great_soul_arrow` 强灵魂箭

- 这是灵魂箭的高压版，不需要多加控制效果，重点是“单体打得更重”。
- 因此只提高伤害、消耗和冷却，形成明确的高阶咒术分层。
- 它和普通灵魂箭的关系应像基础技与主力炮术，而不是两个不同玩法。

### 7. `ashen_black_fire_orb` 黑火球

- 黑火球的重点不是大范围洗地，而是“更沉、更狠、更贴近黑暗咒术的爆压感”。
- 所以它设计成小范围高伤，并附带 `灼烧` 或中心目标失衡。
- 它要和普通 `火球术` 形成区分：火球偏稳定 AOE，黑火球偏高压短窗爆发。

### 8. `ashen_firebomb` 火焰壶

- 火焰壶在魂系里是非常关键的通用投掷物，不应只属于法师。
- 这里用 `stamina` 作为主消耗，让近战和弓手也能学，也能形成“魂味道具”循环。
- 低冷却、小范围、可补刀、可点燃，是它的主要定位。

### 9. `ashen_heal_prayer` 治愈祷告

- 黑魂里的 Heal 更像稳住节奏，而不是瞬间抬满。
- 因此这里保留小额、稳定、低冷却的单体治疗，让它成为圣职职业的标准续航技。
- 这个技能最好维持“朴素但可靠”的气质，不要额外堆太多护盾或群疗效果。

### 10. `ashen_force_burst` 原力震荡

- Force 的核心是“把贴脸敌人震开”，而不是伤害。
- 所以当前设计里伤害较低，真正的价值在 `staggered + 推退 1 格`。
- 它是非常典型的圣职近身解围技，能把敌人从法师或牧师身边掀开。

### 11. `ashen_lightning_spear` 雷枪

- 雷枪在魂系里是最有辨识度的奇迹之一，必须保留“笔直投出去、命中就很痛”的感觉。
- 因此当前体系里优先建议做成 `line`，而不是普通球形落点法术。
- 附加 `shocked` 后，它还能继续接进现有法师雷系联动体系。

### 12. `ashen_dark_edge` 黑暗斩痕

- 这类黑暗系技能的关键不是单纯倍率，而是“打出侵蚀感”。
- 所以这里把核心功能放在 `奥脆` 上，让后续法术和黑暗技能都能顺势接上。
- 若后续做精进形态，再增加轻量 `life_drain`，就能表现出黑魂系黑暗咒术的吸魂感。

---

## 三、落地建议

- 学习来源：
  - 建议全部作为 `据点传授技能` 存在，而不是职业自动授予。
  - 首版仍可在数据上按 `learn_source = book` 处理，再由据点服务发 `skill_unlock` 奖励。
- 推荐标签：
  - `ashen_roll`、`ashen_shield_parry`、`ashen_guard_kick`、`ashen_leaping_cleave`
    - `melee`、`mobility`、`battle_art`
  - `ashen_soul_arrow`、`ashen_great_soul_arrow`
    - `magic`、`arcane`、`sorcery`
  - `ashen_black_fire_orb`、`ashen_firebomb`
    - `fire`、`pyromancy`
  - `ashen_heal_prayer`、`ashen_force_burst`、`ashen_lightning_spear`
    - `holy`、`miracle`
  - `ashen_dark_edge`
    - `dark`、`sorcery`
- 首批优先实现池：
  - `ashen_roll`
  - `ashen_shield_parry`
  - `ashen_soul_arrow`
  - `ashen_black_fire_orb`
  - `ashen_heal_prayer`
  - `ashen_lightning_spear`

这 6 个先做出来，就已经能把黑魂风格的 `战技 / 咒术 / 黑火 / 奇迹` 四条主线跑通。

---

## 四、按现有 skill_id 的实现拆分

判定标准：
- `可直接复用现有 skill_id`：当前 `progression_content_registry.gd` 里已经有足够接近的技能定义，首版只需要在据点奖励里发已有 `skill_unlock`，不需要再新增技能条目。
- `需要新增到 progression_content_registry.gd`：当前没有足够接近的已注册技能，或者一旦借用现有 `skill_id` 就会丢掉这个黑魂技能的核心身份。
- 本节按“当前仓库真实已注册技能”判断，不按其他设计文档里尚未落库的技能名判断。

### 1. 可直接复用现有 `skill_id`

| 灰烬交界技能 | 可复用 skill_id | 结论说明 |
| --- | --- | --- |
| `ashen_shield_parry` | `warrior_shield_bash` | 当前已有 `近战单体 + 伤害 + staggered` 组合，可直接拿来充当首版的“破势盾反”；缺少“对防御姿态追加 armor_break”这一层细化规则，但不影响首版落地。 |
| `ashen_guard_kick` | `warrior_guard_break` | 当前已有 `近战单体 + armor_break` 的破防技能，功能定位和“踢击破防”一致；虽然不是踢击表现，且没有 `移除 guard`，但首版足以承担“开口子”的职能。 |
| `ashen_black_fire_orb` | `mage_fireball` | 当前已有 `地面施法 + 半径 1 火焰范围伤害`，可以直接作为黑火球的现成替身；首版只会缺少“黑火”标签和附带灼烧。 |
| `ashen_heal_prayer` | `priest_healing_light` | 当前已有 `射程 4 单体友军治疗 14`，和治愈祷告几乎一一对应，是最标准的直接复用项。 |

首版如果走这条路径，可以在据点奖励里直接发：
- `warrior_shield_bash`
- `warrior_guard_break`
- `mage_fireball`
- `priest_healing_light`

### 2. 需要新增到 `progression_content_registry.gd`

| 灰烬交界技能 | 需要新增的原因 |
| --- | --- |
| `ashen_roll` | 当前没有任何 `self 位移 + 闪避提升` 的主动技能；这不是换皮问题，而是现有技能表里根本没有对应条目。 |
| `ashen_leaping_cleave` | 当前没有“突进到落点后再打半径 1”的跃进重击；`charge` 是直线冲锋专用，不是跳劈重斩。 |
| `ashen_soul_arrow` | 当前没有干净的“法师系单体奥术箭”；`mage_ice_lance` 自带冰冻，`priest_judgment_ray` 又是圣职光束，直接借用都会偏题。 |
| `ashen_great_soul_arrow` | 当前没有“高伤版灵魂箭”这一条单点咒术分层，必须单独补一个更厚的远程单体法术。 |
| `ashen_firebomb` | 虽然范围结构接近 `mage_fireball`，但它的定位是“通用投掷物”，不应直接继承法师技能的职业标签和法术身份。 |
| `ashen_force_burst` | 当前没有 `self radius 1 + 推退` 的近身震荡技；运行时里也没有通用推退效果条目，不能只靠借现有技能 ID 解决。 |
| `ashen_lightning_spear` | 当前没有真正的“直线雷枪”；`mage_chain_lightning` 是十字 AOE，`priest_judgment_ray` 没有雷系和 `shocked` 身份。 |
| `ashen_dark_edge` | 当前没有“黑暗单体法术 + 奥脆 / 吸魂延展”这一支；连配套状态也不是现成可复用项。 |

### 3. 对接建议

- 如果目标是 `最快可玩版本`，据点先直接发上面 4 个现成 `skill_id`，其余 8 个再逐步补进 `progression_content_registry.gd`。
- 如果目标是 `保留灰烬交界独立技能身份`，那就把这 12 个都做成独立 `ashen_` 条目，只是在效果层尽量复用现有战斗模板。
- 就当前仓库状态看，优先新增顺序建议是：
  - `ashen_roll`
  - `ashen_soul_arrow`
  - `ashen_lightning_spear`
  - `ashen_force_burst`
  - `ashen_dark_edge`
