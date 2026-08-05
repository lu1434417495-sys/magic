# 传奇装备套装效果设计文档

> 本文档为 20 套传奇装备（共 100 件）定义具体的套装触发机制与效果数值，并给出代码实现建议。
> 所有效果均基于现有 `ItemDef.tag` 标记体系设计，可直接在 `PartyEquipmentService.build_attribute_modifiers()` 中落地。
>
> **获取阶段规则**：套装统一属于角色 10 级以后的高阶装备，部分套装可定位于 20 级阶段；不得使用角色创建数值、普通低级装备或 4–5 级敌人作为套装强度基线。每套装备在正式内容冻结前仍须明确自己的推荐等级区间和掉落、任务、锻造或商店获取 owner。

---

## 一、实现方案概要

### 1.1 介入点

在 `PartyEquipmentService.build_attribute_modifiers()` 中，于单件属性收集完成后，追加套装结算逻辑：

```gdscript
# 伪代码
func build_attribute_modifiers(equipment_state) -> Array[AttributeModifier]:
    var modifiers: Array[AttributeModifier] = []
    var equipped_tags: Array[StringName] = []

    # [现有] 收集单件属性
    for slot in equipment_state.get_entry_slot_ids():
        var item = get_item_def(equipment_state.get_equipped_item_id(slot))
        if item != null:
            modifiers.append_array(item.attribute_modifiers)
            equipped_tags.append_array(item.tags)

    # [新增] 追加套装效果
    modifiers.append_array(_resolve_set_bonuses(equipped_tags))
    return modifiers
```

### 1.2 套装定义结构（建议新增 `GearSetDef`）

```gdscript
class_name GearSetDef
extends Resource

@export var set_tag: StringName = &""
@export var set_name: String = ""
@export var threshold_effects: Array[SetThresholdEffect] = []

class SetThresholdEffect:
    @export var required_count: int = 2
    @export var description: String = ""
    @export var attribute_modifiers: Array[AttributeModifier] = []
    @export var special_effect_id: StringName = &""  # 非属性类效果ID
```

### 1.3 效果分类

| 类型 | 说明 | 实现方式 |
|------|------|----------|
| **属性修正型** | 直接加减AC/抗性/攻击等 | `AttributeModifier` 追加到结算列表 |
| **战斗触发型** | 特定条件下触发额外伤害/治疗 | 由战斗系统监听 `special_effect_id` |
| **环境互动型** | 特定地形/天气/时间生效 | 由环境系统监听 `special_effect_id` |
| **光环型** | 影响周围盟友/敌人 | 由战斗/场景系统监听 `special_effect_id` |

---

## 二、护甲套装效果（套装 1–10）

每套 4 件，设定 **2件套** 与 **4件套** 两个阈值。

---

### 套装一：晨光圣骑士（Dawn Paladin）

**主题**：殉道圣骑士的灵魂共鸣，光芒驱散黑暗。

#### 2件套 · 黎明的低语
> *"当第一缕阳光触及铠甲，十二位骑士的灵魂便开始了低语。"*

- **触发条件**：穿戴任意 2 件 `dawn_paladin_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_radiant", mode = "flat", value = 10 }]`
  - 特殊：`saving_throw_wisdom` +1
- **视觉表现**：角色周身环绕淡金色微粒，在暗处尤其明显。

#### 4件套 · 殉道者之光
> *"当十二道光芒汇聚，它们将照亮最黑暗的角落。"*

- **触发条件**：穿戴全部 4 件 `dawn_paladin_set`
- **效果**：
  - 属性：`armor_ac_bonus` +1（套装共鸣强化防御）
  - 战斗触发（`special_effect_id = "dawn_paladin_martyr_light"`）：
    - 日出后 1 小时内，对 alignment 为 evil 的生物，攻击检定 +2
    - HP 降至 20% 以下时，自动触发「殉道者之光」：自身及 10 尺内所有盟友获得 10 点临时 HP（每场战斗一次）
- **视觉表现**：角色周身散发柔和金色光环，攻击邪恶生物时武器拖曳金色残影。

---

### 套装二：暗影刺客（Shadow Assassin）

**主题**：无声之刃的终极隐匿，于阴影中给予致命一击。

#### 2件套 · 影之低语
> *"阴影不是空无一物，而是充满了他人的盲点。"*

- **触发条件**：穿戴任意 2 件 `shadow_assassin_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }]`
  - 特殊：在 dim light 或 darkness 中，隐匿检定额外 +2
- **视觉表现**：角色在阴影中轮廓微微模糊，边缘有黑色微粒飘散。

#### 4件套 · 无形之姿
> *"当阴影成为皮肤，连死亡都无法找到你。"*

- **触发条件**：穿戴全部 4 件 `shadow_assassin_set`
- **效果**：
  - 属性：`movement_speed` +5（阴影推进）
  - 战斗触发（`special_effect_id = "shadow_assassin_intangible"`）：
    - 从隐匿（hidden）或隐形（invisible）状态发动的第一次近战攻击，必定附加 2D6 necrotic 伤害
    - 击杀一个生物后，自动进入隐匿状态（如同 bonus action 使用 Hide）
- **视觉表现**：击杀敌人后，角色瞬间化为黑烟消散，在 10 尺内重新凝聚。

---

### 套装三：霜冻守望者（Frost Warden）

**主题**：北境永恒的冰封，以寒冷冻结一切入侵。

#### 2件套 · 冰封之心
> *"心脏跳动得越慢，世界就冻结得越彻底。"*

- **触发条件**：穿戴任意 2 件 `frost_warden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_cold", mode = "flat", value = 15 }]`
  - 特殊：免疫冰冻地形（ice sheet、frozen ground）的移动力惩罚
- **视觉表现**：角色呼吸时呼出冰晶，脚下地面微微结霜。

#### 4件套 · 霜喉的凝视
> *"当守望者集齐四件，冰龙的吐息也不过是一阵凉风。"*

- **触发条件**：穿戴全部 4 件 `frost_warden_set`
- **效果**：
  - 属性：`armor_ac_bonus` +1
  - 战斗触发（`special_effect_id = "frost_warden_breath"`）：
    - 近战攻击附加 1D6 cold 伤害
    - 受到火焰伤害时，50% 概率触发「冰封抵消」：将该次火焰伤害的 50% 转化为 cold 伤害（先结算抗性）
    - 每日一次，释放「霜冻吐息」：15 尺锥形，3D8 cold 伤害，目标通过 DC14 敏捷豁免则减半
- **视觉表现**：武器表面凝结薄冰，每次挥舞留下白色霜雾轨迹。

---

### 套装四：烈焰法师（Flame Mage）

**主题**：凤凰涅槃的烈焰，于毁灭中重生。

#### 2件套 · 余烬之温
> *"火焰不会真正熄灭，它只是在等待下一次呼吸。"*

- **触发条件**：穿戴任意 2 件 `flame_mage_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_fire", mode = "flat", value = 20 }]`
  - 特殊：施放火焰法术时，法术攻击检定 +1
- **视觉表现**：角色袖口和兜帽边缘不断有火星跳动。

#### 4件套 · 凤凰涅槃
> *"于灰烬中重生者，将带着整个太阳归来。"*

- **触发条件**：穿戴全部 4 件 `flame_mage_set`
- **效果**：
  - 属性：`max_mana` +10（火焰之力的延伸）
  - 战斗触发（`special_effect_id = "flame_mage_phoenix"`）：
    - HP 首次降至 0 时，触发「凤凰涅槃」：恢复至 25% 最大 HP，并释放一圈火焰冲击（15 尺半径，3D6 fire 伤害，盟友免疫），每场战斗一次
    - 施放火焰法术后，下回合火焰法术伤害 +2D6（连续燃烧）
- **视觉表现**：涅槃时角色被金红色火焰包裹，化为凤凰虚影重生。

---

### 套装五：大地守护者（Earth Warden）

**主题**：千年不移的磐石，以不动承受万动。

#### 2件套 · 扎根
> *"树根不需要移动，它们只需要向下。"*

- **触发条件**：穿戴任意 2 件 `earth_warden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_strength", mode = "flat", value = 2 }]`
  - 特殊：站立不动（一回合内未移动）时，AC 临时 +2
- **视觉表现**：角色站立时，双脚周围地面微微隆起，有碎石吸附。

#### 4件套 · 大地之躯
> *"当双脚成为大地的一部分，便没有什么能够动摇你。"*

- **触发条件**：穿戴全部 4 件 `earth_warden_set`
- **效果**：
  - 属性：`max_hp` +10
  - 战斗触发（`special_effect_id = "earth_warden_body"`）：
    - 免疫 prone（击倒）、shove（推离）和一切强制位移效果
    - 被近战攻击命中时，攻击者须通过 DC14 力量豁免，失败则攻击检定 -2（下一回合），因「震反」
    - 每日一次，「大地之握」：15 尺内一个目标必须通过 DC16 力量豁免，失败则 restrained 1 回合（岩石从地面涌出束缚目标）
- **视觉表现**：被攻击时皮肤表面浮现岩石纹理，震动将攻击者弹开。

---

### 套装六：风暴行者（Storm Walker）

**主题**：雷电的化身，于风暴中起舞。

#### 2件套 · 电弧环绕
> *"风在前，雷在后——两者都是你的盟友。"*

- **触发条件**：穿戴任意 2 件 `storm_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_lightning", mode = "flat", value = 15 }]`
  - 特殊：雷暴天气中，先攻检定 +3
- **视觉表现**：角色周身有微弱电弧跳动，金属物品微微震颤。

#### 4件套 · 风暴化身
> *"当雷电成为你的心跳，你便成了风暴本身。"*

- **触发条件**：穿戴全部 4 件 `storm_walker_set`
- **效果**：
  - 属性：`movement_speed` +10
  - 战斗触发（`special_effect_id = "storm_walker_avatar"`）：
    - 近战攻击附加 1D6 lightning 伤害
    - 受到 lightning 伤害时，50% 概率触发「蓄电」：吸收该次伤害的 50%，储存为「电荷」。储存的电荷可在下一次近战攻击时释放，附加等量 lightning 伤害（最高储存 20 点）
    - 雷暴天气中，所有攻击检定和豁免检定 +2
- **视觉表现**：移动时身后拖曳电弧轨迹，攻击时武器裹挟雷霆。

---

### 套装七：亡灵收割者（Death Reaper）

**主题**：死亡的收割者，于收割中延续生命。

#### 2件套 · 亡者的低语
> *"死亡不是终点，而是另一场对话的开始。"*

- **触发条件**：穿戴任意 2 件 `death_reaper_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_necrotic", mode = "flat", value = 15 }]`
  - 特殊：对 undead 类型生物的攻击检定 +2
- **视觉表现**：角色周围 5 尺内温度降低，地面有微弱黑雾缭绕。

#### 4件套 · 收割者之触
> *"收割的灵魂越多，收割者本身就越难以被收割。"*

- **触发条件**：穿戴全部 4 件 `death_reaper_set`
- **效果**：
  - 属性：`max_hp` +10
  - 战斗触发（`special_effect_id = "death_reaper_harvest"`）：
    - 击杀一个生物后，触发「灵魂收割」：恢复 2D8 + 感知修正 HP
    - 对 HP 低于 25% 的目标，近战攻击必定附加 2D6 necrotic 伤害（收割残存生命）
    - 每日一次，「死亡之握」：30 尺内一个目标必须通过 DC16 体质豁免，失败则受到 4D10 necrotic 伤害并 feared 1 回合（necrotic 能量幻化为亡灵之手扼住目标）
- **视觉表现**：击杀敌人时，一道苍白灵魂被吸入角色体内，周身黑雾短暂浓烈。

---

### 套装八：龙鳞铠甲（Dragon Scale）

**主题**：以龙之道还龙之身，屠龙者终成龙的守护。

> **完整落地说明**：本节阈值效果与 `sets_06_to_10.md` 的四件基础属性、单件特殊效果一并交付，不允许只落静态 modifier。旧稿的 `resistance_fire +10` 与当前抗性 schema 冲突，数值直接废止；正式内容通过 threshold trait 的 `damage_resistance_entries` 授予 `fire=half`，不换算为百分比或固定 DR。套装 membership、derived battle source、dragon breath 条件 tier、fear save、每日用量 owner、preview/AI/save/UI 的字段级方案见 [龙鳞铠甲套装完整落地方案](../../proposals/inventory/dragon_scale_set_full_landing.md)。

#### 2件套 · 龙之威慑
> *"龙鳞不只是防御，它是警告——告诉所有龙，有人曾经撕下过它们的逆鳞。"*

- **触发条件**：穿戴任意 2 件 `dragon_scale_set`
- **效果**：
  - threshold trait 授予 `fire=half`
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_fear", mode = "flat", value = 3 }]`
  - `saving_throw_fear +3` 与龙鳞胫甲的同类 `+3` 按 `add` 相加；完整套装的 trait 加值为 `+6`
  - 特殊：免疫龙类「frightful presence」（龙威）
- **视觉表现**：角色面对龙类敌人时，铠甲龙鳞微微竖立，反射对应龙种的颜色。

#### 4件套 · 屠龙者之誓
> *"五龙之力汇聚于一身，你便成了龙的审判者。"*

- **触发条件**：穿戴全部 4 件 `dragon_scale_set`
- **效果**：
  - 属性：对 dragon 类型生物，`attack_bonus` +2
  - 战斗触发（`special_effect_id = "dragon_slayer_oath"`）：
    - 对 dragon 类型生物，每次实际主直接伤害结算额外 +1D4（龙鳞共鸣反制龙血）；重复攻击、随机链与其他多段攻击逐个独立结算触发，不设每次施放上限，但 `extra_damage_segments`、持续/地形/反射伤害和装备生成伤害不再次触发
    - 受到 dragon 的 breath weapon 时，免疫该 breath 对应元素伤害的 50%（龙鳞预判吐息属性）
    - 每日一次，「龙血沸腾」：持续 300 TU，对 dragon 的攻击检定 +3，且每次命中回复 1D6 HP（最高 3 次）；持续时间只按战斗时间线结算，不映射现实秒或分钟
- **视觉表现**：面对龙类时，铠甲五处龙鳞同时发光，胸甲浮现屠龙誓言符文。

---

### 套装九：铁壁要塞（Iron Bulwark）

**主题**：移动的城墙，以不不动承受万动。

#### 2件套 · 盾墙共鸣
> *"一个人的盾是盾牌，两个人的盾是墙壁，四个人的盾是堡垒。"*

- **触发条件**：穿戴任意 2 件 `iron_bulwark_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_hp", mode = "flat", value = 10 }]`
  - 特殊：5 尺内每有一个盟友，AC 临时 +1（最高 +2）
- **视觉表现**：角色与附近盟友之间有微弱的铁灰色光线连接。

#### 4件套 · 不动如山
> *"当三千人的意志凝聚于一人之身，他便是一座要塞。"*

- **触发条件**：穿戴全部 4 件 `iron_bulwark_set`
- **效果**：
  - 属性：`armor_ac_bonus` +1
  - 战斗触发（`special_effect_id = "iron_bulwark_fortress"`）：
    - 免疫 prone（击倒）、shove（推离）和一切强制位移效果
    - 可以主动「坚守阵地」（bonus action）：直到下一回合开始，所有来自前方的攻击伤害减半（reaction 消耗），且 10 尺内盟友共享此效果的一半（伤害减 25%）
    - 被 critical hit 时，50% 概率将其降为普通命中（头盔阻挡致命一击）
- **视觉表现**：坚守阵地时，角色脚下地面龟裂，周身浮现盾墙虚影。

---

### 套装十：古代帝王（Ancient Emperor）

**主题**：永恒王朝的威仪，以帝王之气震慑众生。

#### 2件套 · 帝王之气
> *"皇帝不需要说话，他的存在本身就是命令。"*

- **触发条件**：穿戴任意 2 件 `ancient_emperor_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "persuasion_bonus", mode = "flat", value = 3 }]`
  - 特殊：对 CR 低于穿戴者等级的生物，攻击检定 +2
- **视觉表现**：角色周身散发淡金色威严气息，低等级敌人面对时微微后退。

#### 4件套 · 万民之尊
> *"当十二个省份的力量汇聚于一身，帝王便不只是统治者——他是帝国本身。"*

- **触发条件**：穿戴全部 4 件 `ancient_emperor_set`
- **效果**：
  - 属性：`charisma_bonus` +1（帝王威仪内化）
  - 战斗触发（`special_effect_id = "ancient_emperor_majesty"`）：
    - 5 尺内每有一个敌人，AC 临时 +1（最高 +3，「帝王不惧众敌」）
    - 对 CR 低于穿戴者等级的敌人，伤害额外 +1D6 radiant（「天威」）
    - 每日一次，「帝王敕令」：30 尺内所有 CR 低于穿戴者的敌人必须通过 DC16 魅力豁免，失败则 frightened 1 回合（如同 command 的威压版本）
- **视觉表现**：释放敕令时，角色身后浮现永恒王朝国徽虚影，十二颗宝石同时闪耀。

---

## 三、饰品套装效果（套装 11–20）

每套 6 件，设定 **2件套**、**4件套**、**6件套** 三个阈值。

---

### 套装十一：星辰织者（Star Weaver）

**主题**：命运丝线的编织者，于星辰间预见未来。

#### 2件套 · 星光微照
- `initiative_bonus` +2
- 夜间感知检定 +2

#### 4件套 · 命运编织
- 每日一次，可以「预见」一个敌人的下一次行动：获得该敌人下一次攻击的豁免优势，或该敌人下一次豁免的攻击优势
- 受到致命伤害（HP 降至 0）时，有 20% 概率将该次伤害降为 1（星辰干预）

#### 6件套 · 星辰化身
- 在星光下（夜间户外），所有攻击检定和豁免检定 +1
- 每月一次的「重大抉择」时刻（由 DM 判断），可以强制改变一个骰子结果为重掷（命运之手直接干预）
- 特殊光环：30 尺内盟友先攻检定 +1（星辰指引）

---

### 套装十二：毒蛇之吻（Viper's Kiss）

**主题**：万毒之躯，以毒制毒，以吻封喉。

#### 2件套 · 毒素亲和
- `resistance_poison` +20
- 免疫所有非魔法毒素和疾病

#### 4件套 · 蛇牙之击
- 近战攻击附加 1D4 poison 伤害
- 攻击命中时，目标必须通过 DC14 体质豁免，失败则 poisoned 1 回合
- 每日一次，可以对武器涂覆「混合毒素」：接下来 3 次攻击，每次附加不同效果（麻痹/睡眠/致盲/混乱，随机）

#### 6件套 · 万毒领域
- 5 尺内敌人每回合开始时必须通过 DC13 体质豁免，失败则受到 1D6 poison 伤害并中毒 1 回合（毒雾光环）
- 自身受到毒素伤害时，将其转化为等量 HP 恢复（「以毒为药」）
- 特殊：对已经中毒的目标，所有伤害额外 +1D6（毒素协同）

---

### 套装十三：北风旅者（Northwind Traveler）

**主题**：永不停歇的脚步，于天地间自由穿行。

#### 2件套 · 顺风行
- `movement_speed` +10
- 免疫困难地形的移动力惩罚

#### 4件套 · 无阻之路
- 免疫被 grappled（擒抱）和 restrained（束缚）的移动力限制
- 每日一次，「疾风步」： bonus action 传送至 30 尺内可见位置（如同 misty step）
- 长途旅行时，队伍每日行进距离 +20%

#### 6件套 · 自由之风
- 可以行走于水面（如同 water walk）
- 免疫 falling damage（风中缓降）
- 特殊：在开放空间（户外无天花板），可以召唤「北风之翼」：获得 30 尺飞行速度，持续 1 分钟（每日一次）

---

### 套装十四：虚空行者（Void Walker）

**主题**：虚空的行者，于现实边缘行走。

#### 2件套 · 虚空之肤
- `resistance_force` +15
- 免疫「空间锁定」类效果（如 dimensional anchor）

#### 4件套 · 边界漫步
- 每日两次，短距离传送：30 尺内任意可见位置（bonus action）
- 受到攻击时，有 20% 概率触发「虚空闪避」：攻击穿过身体（如同镜像），本回合 AC +5

#### 6件套 · 虚空化身
- 每日一次，「虚空漫步」：1 分钟内，可以穿过实体障碍物（如同 ethereal jaunt），且免疫非魔法攻击
- 可以看穿所有 illusion、invisibility 和 ethereal plane 生物
- 特殊代价：每次使用虚空能力后，必须通过 DC14 智慧豁免，失败则受到 1D10 psychic 伤害并短暂「迷失」（1 回合内无法区分友敌）

---

### 套装十五：自然之语（Nature's Whisper）

**主题**：自然的聆听者，于万物中寻找和谐。

#### 2件套 · 自然之友
- 动物不会主动攻击
- 可以进行简单的动物交流（理解基本意图，传达简单情感）

#### 4件套 · 森林之子
- 在森林/丛林/草原环境中，所有检定 +1
- 在自然环境中的移动力 +10
- 每日一次，「自然之助」：召唤一只 CR 不超过 1/2 的动物盟友（持续 10 分钟）

#### 6件套 · 翠绿化身
- 每日一次，「自然治愈」：触碰一个生物，恢复 3D8 + 感知修正 HP，并解除一项疾病或毒素效果
- 站立不动时，每回合恢复 1 HP（自然缓慢治愈）
- 特殊：植物和真菌类生物不会主动攻击，甚至可以被简单指挥（如让藤蔓搭桥、让花朵释放香气干扰追踪）

---

### 套装十六：深渊凝视（Abyss Gazer）

**主题**：深渊的凝视者，于恐惧中寻找真理。

#### 2件套 · 深渊之视
- 免疫 blinded（致盲）
- 可以看穿所有非魔法 disguise 和 illusion

#### 4件套 · 不可名状的知识
- 每日一次，可以「凝视深渊」：30 尺内一个目标必须通过 DC16 智慧豁免，失败则 frightened 1 回合（目标看到自己最深层恐惧的幻象）
- 对 aberration 和 eldritch 生物的攻击检定 +2
- 水下可以呼吸（深渊之气）

#### 6件套 · 深渊的低语
- 30 尺内敌人在每个回合开始时必须通过 DC13 智慧豁免，失败则攻击检定 -1（深渊凝视的精神压迫，可叠加至 -3）
- 每日一次，「深渊召唤」：召唤一只 CR 不超过 2 的 aberration（持续 1 分钟，不可控但优先攻击敌人）
- 特殊代价：每长休一次，必须通过 DC14 智慧豁免，失败则受到 2D10 psychic 伤害并陷入 short-term madness（由 DM 决定表现）

---

### 套装十七：圣光使者（Lightbringer）

**主题**：光明的使者，于黑暗中成为灯塔。

#### 2件套 · 微光之环
- 自身持续散发 15 尺半径的微光（如同 candle）
- 免疫 frightened（恐惧）

#### 4件套 · 治愈之触
- 每日三次，「圣光之触」：触碰一个生物，恢复 2D8 + 智慧修正 HP
- 对 undead 和 demon 的攻击检定 +2
- 对 undead 和 demon 的伤害额外 +1D6 radiant

#### 6件套 · 光的化身
- 每日一次，「圣光爆发」：30 尺半径内所有 undead 和 demon 受到 4D6 radiant 伤害并通过 DC16 体质豁免，失败则 blinded 1 回合；所有盟友恢复 2D8 HP
- 可以驱散 15 尺内的魔法 darkness（如同 daylight）
- 特殊光环：10 尺内盟友对 necrotic 和 poison 伤害获得 5 点抗性

---

### 套装十八：永恒学徒（Eternal Apprentice）

**主题**：无限的学习者，于知识中寻找永恒。

#### 2件套 · 求知若渴
- 所有知识检定（arcana, history, nature, religion, investigation）+3
- 学习新技能所需时间减半

#### 4件套 · 顿悟之光
- 每日一次，可以在任何知识检定中自动成功（「突然明白了一切」）
- 法术准备/记忆时间减半
- `max_mana` +15

#### 6件套 · 无限之智
- 每日一次，可以「重新学习」：将一个已知的技能或法术临时替换为另一个同等级的技能或法术（持续至长休结束）
- 所有法术的施法材料消耗减半（向下取整）
- 特殊：可以阅读任何语言的文字（「知识的语言是通用的」）

---

### 套装十九：血月猎人（Blood Moon Hunter）

**主题**：狼人的狩猎者，于血月之夜化身审判。

#### 2件套 · 猎人之眼
- 追踪检定 +3
- 对 shapechanger 类型生物的感知检定 +5（自动识破变形）

#### 4件套 · 银牙之击
- 对 werewolf 和 shapechanger 的攻击检定 +3
- 对 werewolf 和 shapechanger 的伤害额外 +2D6 silver（绕过再生）
- 每日一次，「银光闪耀」：武器注入银光，接下来 3 次攻击对 shapechanger 必定造成 silver 伤害并抑制其再生 1 回合

#### 6件套 · 血月化身
- 血月之夜（由 DM 决定或每月固定日期），所有属性检定 +1
- 血月之夜，对 werewolf 的伤害额外 +1D6（总计 +3D6）
- 特殊：如果穿戴者被狼人感染（lycanthropy），套装效果会压制感染——只要 6 件套齐全，永远不会在满月变身（但感染仍在，只是被「锁定」）

---

### 套装二十：锈蚀齿轮（Rusted Gear）

**主题**：机械的继承者，于齿轮间寻找灵魂。

#### 2件套 · 工匠之手
- 所有工匠工具检定 +3
- 修复机械装置所需时间减半

#### 4件套 · 机械共鸣
- 可以听懂「机器的语言」：自动判断任何机械装置的基本功能、当前状态和故障原因
- 对 construct 类型生物的攻击检定 +2
- 每日一次，可以临时操控一台非敌对 construct（CR 不超过 3）1 分钟（如同 dominate monster 但仅限 construct）

#### 6件套 · 齿轮化身
- 每日一次，「机械强化」：1 分钟内，自身获得 construct 特性——免疫 poison、necrotic、psychic 伤害和疾病，AC +2，但移动力 -5（身体部分机械化）
- 可以临时制作简单机械装置（陷阱、工具、小型载具），制作时间为正常的 1/4
- 特殊：如果穿戴者死亡，6 件套齐全时，有 10% 概率以「半机械」形态复活（HP 恢复至 1，但永久失去 1 点体质，外观部分机械化）

---

## 四、数值平衡总览

### 4.1 护甲套装 4 件套效果汇总

| 套装 | 核心战斗增益 | 定位 |
|------|-------------|------|
| 晨光圣骑士 | 对邪恶 +2 命中，低血量团队护盾 | 坦克/辅助 |
| 暗影刺客 | 首击 +2D6 necrotic，击杀隐身 | 刺客 |
| 霜冻守望者 | 近战 +1D6 cold，火焰抵消，霜冻吐息 | 控场/战士 |
| 烈焰法师 | 涅槃重生，火焰连击 | 法师/爆发 |
| 大地守护者 | 免疫位移，震反，大地之握 | 坦克 |
| 风暴行者 | 近战 +1D6 lightning，蓄电，风暴优势 | 战士/游侠 |
| 亡灵收割者 | 击杀回 2D8 HP，斩杀线 +2D6 necrotic | 战士/死灵 |
| 龙鳞铠甲 | 对龙每次主直接伤害 +1D4，龙息减半，龙血沸腾 | 屠龙特化 |
| 铁壁要塞 | 团队减伤 25%，坚守阵地，免疫暴击 | 纯坦克 |
| 古代帝王 | 众敌 AC +3，低等级 +1D6，帝王敕令 | 领导型战士 |

### 4.2 饰品套装 6 件套效果汇总

| 套装 | 核心战斗增益 | 定位 |
|------|-------------|------|
| 星辰织者 | 先攻 +2，命运干预，团队先攻光环 | 辅助/控制 |
| 毒蛇之吻 | 近战 +1D4+1D6 poison，毒雾光环，以毒为药 | 刺客/毒师 |
| 北风旅者 | 疾风步，水面行走，飞行 1 分钟 | 机动/探索 |
| 虚空行者 | 传送 2 次/日，虚空闪避，虚体化 1 分钟 | 法师/机动 |
| 自然之语 | 自然治愈 3D8，动物召唤，植物操控 | 德鲁伊/辅助 |
| 深渊凝视 | 恐惧光环 -3 命中，深渊召唤，aberration 加成 | 控场/暗法 |
| 圣光使者 | 圣光爆发 4D6，驱散黑暗，团队抗性光环 | 圣骑/牧师 |
| 永恒学徒 | 知识自动成功，法术替换，材料减半 | 法师/学者 |
| 血月猎人 | 对狼人 +3D6，识破变形，血月压制感染 | 猎魔特化 |
| 锈蚀齿轮 | 操控 construct，机械强化，10% 机械复活 | 工匠/工程 |

---

## 五、数据结构建议（供代码实现参考）

### 5.1 套装定义资源配置

建议新增目录 `data/configs/gear_sets/`，存放 `.tres` 或 `.json`：

```gdscript
# data/configs/gear_sets/dawn_paladin_set.tres
set_tag = "dawn_paladin_set"
set_name = "晨光圣骑士"
threshold_effects = [
    {
        required_count = 2,
        description = "黎明的低语：radiant 抗性 +10，wisdom 豁免 +1",
        attribute_modifiers = [
            { attribute_id = "resistance_radiant", mode = "flat", value = 10 },
            { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1 }
        ],
        special_effect_id = ""
    },
    {
        required_count = 4,
        description = "殉道者之光：AC+1，日出对邪恶+2命中，低血量团队护盾",
        attribute_modifiers = [
            { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }
        ],
        special_effect_id = "dawn_paladin_martyr_light"
    }
]
```

### 5.2 PartyEquipmentService 扩展接口

```gdscript
# scripts/systems/inventory/party_equipment_service.gd

@export var gear_set_defs: Array[GearSetDef] = []
var _gear_set_by_tag: Dictionary = {}  # StringName -> GearSetDef

func setup(...):
    # ... 现有初始化 ...
    for def in gear_set_defs:
        _gear_set_by_tag[def.set_tag] = def

func _resolve_set_bonuses(equipped_tags: Array[StringName]) -> Array[AttributeModifier]:
    var result: Array[AttributeModifier] = []
    var tag_counts: Dictionary = {}
    for tag in equipped_tags:
        tag_counts[tag] = tag_counts.get(tag, 0) + 1

    for tag in tag_counts:
        var def = _gear_set_by_tag.get(tag)
        if def == null:
            continue
        var count = tag_counts[tag]
        for effect in def.threshold_effects:
            if count >= effect.required_count:
                result.append_array(effect.attribute_modifiers)
                # 非属性类效果通过 special_effect_id 通知战斗系统
                if effect.special_effect_id != &"":
                    _notify_set_effect_active(effect.special_effect_id, true)
    return result

func _notify_set_effect_active(effect_id: StringName, active: bool):
    # 通过 EventBus 或 Signal 通知战斗/场景系统
    EventBus.emit_signal("gear_set_effect_changed", effect_id, active)
```

### 5.3 战斗系统监听

```gdscript
# scripts/systems/battle/battle_service.gd（或相关脚本）

func _on_gear_set_effect_changed(effect_id: StringName, active: bool):
    match effect_id:
        "dawn_paladin_martyr_light":
            _active_set_effects["dawn_paladin_martyr_light"] = active
        "shadow_assassin_intangible":
            _active_set_effects["shadow_assassin_intangible"] = active
        # ... 其他效果 ...
```

---

*套装效果设计文档 · 20 套 100 件装备 · 完结*
## 三、护甲套装效果（套装 21–60）

每套 4 件护甲 + 6 件饰品，设定 **2件套** 与 **4件套** 两个阈值。

---

### 套装二十一：银月游侠（Silvermoon Ranger）

**主题**：月光下的精准射手，银辉指引箭矢穿透黑暗。

#### 2件套 · 月华指引
> *"月光照亮的不只是道路，还有敌人的心脏。"*

- **触发条件**：穿戴任意 2 件 `silvermoon_ranger_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus_ranged", mode = "flat", value = 1 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 夜间远程攻击检定 +1
- **视觉表现**：箭矢末端拖曳淡银色尾迹。

#### 4件套 · 银月齐射
> *"当银辉满盈，箭雨如月光洒落，无处可逃。"*

- **触发条件**：穿戴全部 4 件 `silvermoon_ranger_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus_ranged", mode = "flat", value = 2 }, { attribute_id = "critical_threat_range", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "silvermoon_volley"`）：
    - 夜间，远程攻击命中时可触发「银月齐射」：对目标及相邻 5 尺内另一敌人各造成 1D8 radiant（每场战斗两次）
- **视觉表现**：拉弓时弓弦凝聚月光，齐射时箭矢分裂成三道银光。

---

### 套装二十二：沙漠蝎（Desert Scorpion）

**主题**：沙海中的致命猎手，毒刺潜伏于炙热之下。

#### 2件套 · 沙隐
> *"沙漠不会留下脚印，正如蝎子的毒刺不会留下痕迹。"*

- **触发条件**：穿戴任意 2 件 `desert_scorpion_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_fire", mode = "flat", value = 10 }, { attribute_id = "stealth_bonus", mode = "flat", value = 2 }]`
  - 沙漠环境中隐匿检定 +3
- **视觉表现**：行走时脚下泛起细微沙粒涟漪。

#### 4件套 · 蝎尾突袭
> *"等待是蝎子的艺术，而它的毒液是最后的句号。"*

- **触发条件**：穿戴全部 4 件 `desert_scorpion_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "scorpion_sting"`）：
    - 从隐匿状态发起的第一次近战攻击附加 2D6 poison，目标须通过 DC15 体质豁免，失败则 poisoned 1 回合
- **视觉表现**：突袭时身后浮现巨型蝎尾虚影。

---

### 套装二十三：丛林黑豹（Jungle Panther）

**主题**：密林深处的潜行猎手，迅捷如电，致命如风。

#### 2件套 · 豹隐
> *"黑豹不追逐猎物，它只是出现在猎物逃跑的路上。"*

- **触发条件**：穿戴任意 2 件 `jungle_panther_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "movement_speed", mode = "flat", value = 10 }, { attribute_id = "stealth_bonus", mode = "flat", value = 2 }]`
  - 森林/丛林地形移动力不受困难地形影响
- **视觉表现**：潜行时身体边缘浮现黑色豹纹波动。

#### 4件套 · 豹袭连击
> *"黑豹的每一次扑击都是致命的，因为它的速度让猎物来不及恐惧。"*

- **触发条件**：穿戴全部 4 件 `jungle_panther_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "movement_speed", mode = "flat", value = 10 }]`
  - 战斗触发（`special_effect_id = "panther_pounce"`）：
    - 移动至少 10 尺后进行的近战攻击，若命中可立即 bonus action 进行一次爪击（1D8 slashing + 1D6 bleed，持续 2 回合）
- **视觉表现**：扑击时身后拖曳三道黑色残影。

---

### 套装二十四：高地雄鹰（Highland Eagle）

**主题**：云霄之上的锐利之眼，俯冲即是裁决。

#### 2件套 · 鹰眼
> *"从云端俯瞰，一切阴谋都无所遁形。"*

- **触发条件**：穿戴任意 2 件 `highland_eagle_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "perception_bonus", mode = "flat", value = 3 }, { attribute_id = "investigation_bonus", mode = "flat", value = 2 }]`
  - 远程攻击射程 +15 尺
- **视觉表现**：双眼在专注时泛起淡金色鹰眼纹路。

#### 4件套 · 天鹰俯冲
> *"雄鹰从不犹豫，因为它知道，俯冲的那一刻，猎物已经属于它了。"*

- **触发条件**：穿戴全部 4 件 `highland_eagle_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus_ranged", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "eagle_dive"`）：
    - 从高处（至少 10 尺）发起的远程攻击，伤害骰翻倍（每场战斗两次）
- **视觉表现**：高处射击时，箭矢/投掷物化作鹰形光影俯冲而下。

---

### 套装二十五：海盗船长（Pirate Captain）

**主题**：七海之上的自由霸主，浪涛与金币皆为其臣属。

#### 2件套 · 海狼气息
> *"大海不认国王，只认强者。"*

- **触发条件**：穿戴任意 2 件 `pirate_captain_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "athletics_bonus", mode = "flat", value = 3 }, { attribute_id = "resistance_acid", mode = "flat", value = 10 }]`
  - 水上/船只上 AC +1
- **视觉表现**：周身环绕海盐微粒，衣摆无风自动如船帆。

#### 4件套 · 船长号令
> *"当船长下令，连海浪都要让路。"*

- **触发条件**：穿戴全部 4 件 `pirate_captain_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "charisma_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "captain_command"`）：
    - 每场战斗一次，bonus action 发出号令：10 尺内所有盟友下一次攻击检定 +2，且自身下一回合可多进行一次 bonus action 攻击（1D8 + 力量调整值 slashing）
- **视觉表现**：号令时身后浮现海盗船旗虚影，盟友武器泛起浪花光芒。

---

### 套装二十六：赏金猎人（Bounty Hunter）

**主题**：追踪与猎杀的专家，没有人能在其契约下逃脱。

#### 2件套 · 猎手直觉
> *"每一个猎物都以为自己藏得很好，直到听见身后的脚步声。"*

- **触发条件**：穿戴任意 2 件 `bounty_hunter_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "survival_bonus", mode = "flat", value = 3 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 追踪检定 +3
- **视觉表现**：锁定目标时，目标轮廓泛起微弱红色标记。

#### 4件套 · 契约处决
> *"契约即命运，而我只是命运的执行者。"*

- **触发条件**：穿戴全部 4 件 `bounty_hunter_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "bounty_execute"`）：
    - 对 HP 低于 30% 的敌人，攻击检定 +3，且命中时附加 2D6 necrotic（每场战斗一次对同一目标）
- **视觉表现**：处决攻击时武器缠绕锁链虚影，击中时迸发血红光芒。

---

### 套装二十七：瘟疫医生（Plague Doctor）

**主题**：死亡与治愈的双面使者，以毒攻毒，以疫制疫。

#### 2件套 · 疫医面具
> *"我不是带来死亡的人，我是见证死亡的人。"*

- **触发条件**：穿戴任意 2 件 `plague_doctor_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_poison", mode = "flat", value = 15 }, { attribute_id = "medicine_bonus", mode = "flat", value = 3 }]`
  - 免疫疾病
- **视觉表现**：面部笼罩鸟嘴面具虚影，呼吸时滤出淡绿色雾气。

#### 4件套 · 瘟疫蔓延
> *"治愈一种瘟疫的最好方法，是让另一种瘟疫取而代之。"*

- **触发条件**：穿戴全部 4 件 `plague_doctor_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "plague_spread"`）：
    - 近战攻击附加 1D6 poison + 1D6 necrotic；被击中的敌人若未通过 DC15 体质豁免，则「感染」：每回合开始受到 1D4 poison，持续 3 回合，且 5 尺内其他生物每回合开始须通过 DC13 体质豁免，失败同样感染
- **视觉表现**：攻击时绿色毒雾扩散，被感染者头顶浮现鸟嘴面具标记。

---

### 套装二十八：月影舞者（Moonshadow Dancer）

**主题**：月光与阴影交织的舞者，每一步都是致命的旋律。

#### 2件套 · 月影步
> *"她在月光下起舞，在阴影中消失。"*

- **触发条件**：穿戴任意 2 件 `moonshadow_dancer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "acrobatics_bonus", mode = "flat", value = 3 }, { attribute_id = "stealth_bonus", mode = "flat", value = 2 }]`
  - 夜间 AC +1
- **视觉表现**：移动时身后留下淡银色与深黑色交替的残影。

#### 4件套 · 影舞终章
> *"当月光与阴影完美重叠，舞者的最后一次转身，即是敌人的终章。"*

- **触发条件**：穿戴全部 4 件 `moonshadow_dancer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "moonshadow_finale"`）：
    - 夜间，每次成功闪避（敌人攻击未命中）后可立即 reaction 进行一次近战反击（1D8 + 敏捷调整值 slashing）；若反击命中，可 bonus action 隐匿（无需检定，自动成功，每场战斗两次）
- **视觉表现**：闪避时身体化作月影消散，反击时从敌人影中浮现。

---

### 套装二十九：血骑士（Blood Knight）

**主题**：以鲜血为燃料的狂战士，越战越勇，不死不休。

#### 2件套 · 血怒
> *"鲜血不是弱点，它是力量的货币。"*

- **触发条件**：穿戴任意 2 件 `blood_knight_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_hp", mode = "flat", value = 15 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - HP 低于 50% 时攻击检定 +1
- **视觉表现**：受伤时伤口溢出暗红色血雾而非流血。

#### 4件套 · 血祭狂欢
> *"当血液沸腾，疼痛化为狂笑，死亡只是开始。"*

- **触发条件**：穿戴全部 4 件 `blood_knight_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "blood_sacrifice"`）：
    - 每次命中敌人时恢复等同于造成伤害 20% 的 HP（最少 1 点）；HP 降至 25% 以下时，攻击检定再 +2，且每次命中恢复 30%（每场战斗一次低血量爆发）
- **视觉表现**：攻击时血雾缠绕武器，恢复 HP 时血雾被吸入体内，低血量时双眼泛红光。

---

### 套装三十：幻影骑士（Phantom Rider）

**主题**：幽灵般的骑兵，穿梭于现实与虚幻之间，无人可挡。

#### 2件套 · 幽影骑术
> *"马匹踏过的不是地面，而是现实的边界。"*

- **触发条件**：穿戴任意 2 件 `phantom_rider_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "movement_speed", mode = "flat", value = 15 }, { attribute_id = "saving_throw_dexterity", mode = "flat", value = 2 }]`
  - 骑乘时移动力再 +10
- **视觉表现**：骑乘时坐骑脚下泛起幽蓝色冥火，移动时拖曳幽灵轨迹。

#### 4件套 · 幻影冲锋
> *"当幻影骑士发起冲锋，物理与虚幻的界限便不复存在。"*

- **触发条件**：穿戴全部 4 件 `phantom_rider_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "phantom_charge"`）：
    - 骑乘时移动至少 20 尺后发起的冲锋攻击，伤害骰翻倍，且可以穿过敌人占据的空间（不引发 opportunity attack），若命中则敌人须通过 DC15 力量豁免，失败则被推离 10 尺并 prone（每场战斗两次）
- **视觉表现**：冲锋时骑士与坐骑化作半透明幽灵形态，穿透敌人时幽蓝光芒爆发。

---

### 套装三十一：风语者（Wind Whisperer）

**主题**：与风对话的行者，借风之力飞翔与感知。

#### 2件套 · 风之低语
> *"风从不说话，但它一直在低语——只是很少有人懂得倾听。"*

- **触发条件**：穿戴任意 2 件 `wind_whisperer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "movement_speed", mode = "flat", value = 10 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 强风天气中移动力再 +10，且免疫远程攻击劣势
- **视觉表现**：周身有微型旋风环绕，衣摆始终飘扬。

#### 4件套 · 风暴化身
> *"当风语者召唤风暴，天空便成为了他的领地。"*

- **触发条件**：穿戴全部 4 件 `wind_whisperer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "movement_speed", mode = "flat", value = 10 }]`
  - 战斗触发（`special_effect_id = "wind_storm"`）：
    - 每日一次，召唤一阵强风：15 尺半径内所有敌人移动力减半，远程攻击对其有劣势，且风语者在该区域内移动力 +20、AC +2，持续 1 分钟
- **视觉表现**：召唤时周身旋风扩大成风暴领域，敌人被风压压制。

---

### 套装三十二：水波行者（Wave Walker）

**主题**：海洋的子民，在水中如鱼，在陆地上亦不逊色。

#### 2件套 · 水之息
> *"海洋不是敌人，它只是另一种形态的大地——学会了游泳，你就学会了飞翔。"*

- **触发条件**：穿戴任意 2 件 `wave_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "athletics_bonus", mode = "flat", value = 3 }]`
  - 水中呼吸，水中移动力 +15
- **视觉表现**：周身有水汽薄膜，干燥环境中不断有细微水珠凝结又蒸发。

#### 4件套 · 潮汐掌控
> *"潮汐听从我的召唤，正如海洋听从月亮。"*

- **触发条件**：穿戴全部 4 件 `wave_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_cold", mode = "flat", value = 10 }, { attribute_id = "saving_throw_strength", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "tidal_control"`）：
    - 每日一次，释放潮汐冲击：20 尺锥形 4D8 bludgeoning，目标须通过 DC16 力量豁免，失败则被推离 15 尺并 prone；若在水中释放，范围变为 30 尺且伤害变为 6D8
- **视觉表现**：释放时身后浮现巨浪虚影，冲击水流将敌人卷走。

---

### 套装三十三：地震先知（Earthquake Seer）

**主题**：感知大地脉动的先知，借地震之力防御与攻击。

#### 2件套 · 地脉感知
> *"大地不会说话，但它会震动——每一次震动都是一条信息。"*

- **触发条件**：穿戴任意 2 件 `earthquake_seer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_dexterity", mode = "flat", value = 2 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 免疫 prone，站立不动时 AC +1
- **视觉表现**：脚下地面不断有微弱震动波纹扩散。

#### 4件套 · 震击波
> *"当地震先知跺脚，大地会替他说话。"*

- **触发条件**：穿戴全部 4 件 `earthquake_seer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "saving_throw_strength", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "seismic_wave"`）：
    - 每日两次，bonus action 跺脚释放震击波：10 尺半径 3D8 bludgeoning，所有生物须通过 DC16 敏捷豁免，失败则 prone 并 stunned 至下回合开始；震击波会破坏该区域内非魔法建筑/地面
- **视觉表现**：跺脚时地面龟裂，冲击波以环形扩散，敌人被震飞。

---

### 套装三十四：夜幕吟游诗人（Night Bard）

**主题**：黑夜中的旋律编织者，以音波魅惑与攻击。

#### 2件套 · 夜曲
> *"黑夜不是沉默的，它在歌唱——只是凡人听不见那频率。"*

- **触发条件**：穿戴任意 2 件 `night_bard_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "performance_bonus", mode = "flat", value = 3 }, { attribute_id = "persuasion_bonus", mode = "flat", value = 2 }]`
  - 夜间魅力检定 +2
- **视觉表现**：夜间周身散发柔和暗紫色音符光点，行走时脚下泛起涟漪状音波。

#### 4件套 · 终焉夜曲
> *"当最后一个音符落下，敌人的意志也随之崩塌。"*

- **触发条件**：穿戴全部 4 件 `night_bard_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "night_finale"`）：
    - 每日一次，演奏终焉夜曲：20 尺半径内所有敌人须通过 DC16 智慧豁免，失败则 charmed 2 回合（期间无法攻击演奏者）并受到 4D8 psychic；友方则获得 10 尺移动力加成和攻击检定 +1，持续 2 回合
- **视觉表现**：演奏时暗紫色音波光环扩张，敌人被音符缠绕，友方被暗紫光晕笼罩。

---

### 套装三十五：白鸦信使（White Raven）

**主题**：穿梭于生死边界的信使，以白鸦之眼洞察一切。

#### 2件套 · 鸦之眼
> *"白鸦不是死亡的使者，它是消息的传递者——只是有些消息，人们不愿意听见。"*

- **触发条件**：穿戴任意 2 件 `white_raven_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "perception_bonus", mode = "flat", value = 3 }, { attribute_id = "investigation_bonus", mode = "flat", value = 2 }]`
  - 可以感知 1 里内的信号、魔法信标和传送门
- **视觉表现**：双眼在专注时泛起白鸦瞳孔的银白色光圈。

#### 4件套 · 白鸦群飞
> *"当白鸦群飞而至，消息便已送达——无论收件人是否愿意。"*

- **触发条件**：穿戴全部 4 件 `white_raven_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "movement_speed", mode = "flat", value = 10 }]`
  - 战斗触发（`special_effect_id = "raven_flock"`）：
    - 每日一次，召唤白鸦群：30 尺半径内所有敌人视野被遮蔽（远程攻击有劣势，且无法对 30 尺外目标施法），持续 1 分钟；白鸦群期间信使移动力 +20，且可以 bonus action 传送到 30 尺内任何位置（每日三次在白鸦群持续期间）
- **视觉表现**：召唤时天空降下白鸦群，羽翼遮天蔽日，信使在鸦群中闪烁移动。

---

### 套装三十六：红狐盗贼（Red Fox Thief）

**主题**：狡诈与敏捷的化身，以诡计和速度取胜。

#### 2件套 · 狐步
> *"红狐不是狡猾，它只是懂得在正确的时间出现在正确的地方——然后带着正确的东西离开。"*

- **触发条件**：穿戴任意 2 件 `red_fox_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }, { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 隐匿（无需检定，自动成功）
- **视觉表现**：潜行时身体边缘浮现赤红色狐火，移动无声。

#### 4件套 · 九尾幻术
> *"当九尾显现，你已分不清哪一个是真身，哪一个是幻影。"*

- **触发条件**：穿戴全部 4 件 `red_fox_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "dexterity_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "fox_illusion"`）：
    - 每日两次，被攻击时可 reaction 召唤一个幻影分身：攻击者须通过 DC16 感知豁免，失败则攻击命中分身（分身拥有 1 HP，被击中即消散）；若成功，攻击照常命中。分身持续 1 回合，可作为诱饵吸引攻击
- **视觉表现**：被攻击时身体分裂成两个红狐身影，其中一个消散为火星。

---

### 套装三十七：青蛇刺客（Green Viper）

**主题**：毒与暗影的结合，一击毙命，无声无息。

#### 2件套 · 蛇鳞
> *"青蛇不是毒物，它只是懂得在正确的时间释放正确的毒素。"*

- **触发条件**：穿戴任意 2 件 `green_viper_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_poison", mode = "flat", value = 15 }, { attribute_id = "stealth_bonus", mode = "flat", value = 2 }]`
  - 近战攻击附加 1D4 poison
- **视觉表现**：皮肤隐约可见青蛇鳞片纹路，呼吸时吐出淡绿色雾气。

#### 4件套 · 毒蛇之吻
> *"毒蛇的吻只有一次机会，但它从不需要第二次。"*

- **触发条件**：穿戴全部 4 件 `green_viper_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "critical_threat_range", mode = "flat", value = 1 }, { attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "viper_kiss"`）：
    - 从隐匿状态发起的第一次近战攻击若命中，自动视为暴击，且附加 3D6 poison + 目标须通过 DC17 体质豁免，失败则 poisoned 2 回合并 paralyzed 1 回合；若未命中，该效果进入 1 分钟冷却
- **视觉表现**：突袭时青蛇虚影缠绕手臂，命中时毒牙刺入目标，绿色毒液喷涌。

---

### 套装三十八：金蝶幻术师（Golden Butterfly）

**主题**：以美丽为武器的幻术师，幻象即是现实。

#### 2件套 · 蝶翼
> *"金蝶不是虚荣，它只是懂得用美丽来掩盖真相。"*

- **触发条件**：穿戴任意 2 件 `golden_butterfly_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "deception_bonus", mode = "flat", value = 3 }, { attribute_id = "performance_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 创造一个自身幻象（持续 1 回合，吸引敌人攻击，拥有 1 HP）
- **视觉表现**：周身环绕金色蝴蝶虚影，移动时洒落金色鳞粉。

#### 4件套 · 幻梦领域
> *"当金蝶展开双翼，现实与梦境的界限便不复存在。"*

- **触发条件**：穿戴全部 4 件 `golden_butterfly_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "butterfly_dream"`）：
    - 每日一次，展开幻梦领域：15 尺半径内所有敌人须通过 DC16 智慧豁免，失败则陷入幻梦（stunned 1 回合，随后 disoriented 2 回合——攻击检定和感知检定有劣势）；友方在领域内 AC +1 且每回合开始时恢复 1D6 HP
- **视觉表现**：领域展开时金色蝴蝶群飞舞，敌人被蝶群缠绕陷入呆滞，友方被金色光晕治愈。

---

### 套装三十九：紫晶心灵师（Amethyst Psychic）

**主题**：精神的守护者，以心灵之力洞察、防御与攻击。

#### 2件套 · 心灵壁垒
> *"紫晶不是宝石，它是凝固的精神——每一块都封存着一个灵魂的碎片。"*

- **触发条件**：穿戴任意 2 件 `amethyst_psychic_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_psychic", mode = "flat", value = 15 }, { attribute_id = "insight_bonus", mode = "flat", value = 3 }]`
  - 免疫 charmed 和 frightened
- **视觉表现**：额头浮现微型紫晶纹路，双眼在施法时泛起淡紫色光芒。

#### 4件套 · 精神风暴
> *"当紫晶心灵师释放精神风暴，整个战场都将成为她的思想延伸。"*

- **触发条件**：穿戴全部 4 件 `amethyst_psychic_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }, { attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "psychic_storm"`）：
    - 每日一次，释放精神风暴：20 尺半径内所有敌人受到 4D8 psychic 并须通过 DC17 智慧豁免，失败则 stunned 1 回合且下一回合开始前无法集中精神（无法维持 concentration）；友方在风暴范围内免疫 psychic 和 charmed，且法术攻击检定 +1
- **视觉表现**：紫晶光芒从双眼爆发，精神波纹以环形扩散，敌人抱头哀嚎。

---

### 套装四十：黑铁咒术师（Black Iron Warlock）

**主题**：契约的束缚者与利用者，以契约之力获取超越常规的力量。

#### 2件套 · 契约烙印
> *"契约不是束缚，它是'交换'——你付出什么，就得到什么。"*

- **触发条件**：穿戴任意 2 件 `black_iron_warlock_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "saving_throw_charisma", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 与 5 尺内的一个存在（敌/友/中立）签订临时契约：你获得该存在的一项抗性（持续 1 分钟），但该存在对你造成的第一次伤害 +2D6
- **视觉表现**：周身环绕黑色铁链虚影，契约签订时铁链缠绕双方。

#### 4件套 · 契约反噬
> *"当契约到期，代价必须支付——但黑铁咒术师从不会自己支付。"*

- **触发条件**：穿戴全部 4 件 `black_iron_warlock_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }, { attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "contract_backlash"`）：
    - 每日一次，强制与一个可见敌人签订「痛苦契约」：该敌人受到 4D10 necrotic，且每回合开始时受到 2D6 necrotic（持续 3 回合）；期间若该敌人对咒术师造成伤害，伤害减半，且咒术师恢复等同于被减免伤害 50% 的 HP；3 回合后契约结束，敌人须通过 DC17 体质豁免，失败则永久降低 1 点 AC（可叠加）
- **视觉表现**：黑色铁链从虚空中射出缠绕敌人，铁链不断收紧吸取生命，契约结束时铁链断裂迸发黑雾。

---

### 套装四十一：太阳圣骑士（Solar Paladin）

**主题**：太阳的化身，以圣光驱散一切黑暗与邪恶。

#### 2件套 · 日冕
> *"太阳不会为任何人停留，但它的光芒会记住每一个信仰它的人。"*

- **触发条件**：穿戴任意 2 件 `solar_paladin_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_radiant", mode = "flat", value = 15 }, { attribute_id = "saving_throw_wisdom", mode = "flat", value = 2 }]`
  - 日间 radiant 伤害 +1D4
- **视觉表现**：周身散发淡金色光芒，暗处如行走的小太阳。

#### 4件套 · 太阳裁决
> *"当太阳圣骑士举起武器，天空便会回应他的信仰。"*

- **触发条件**：穿戴全部 4 件 `solar_paladin_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "solar_judgment"`）：
    - 日间，每场战斗一次，对 alignment 为 evil 的敌人发动「太阳裁决」：攻击检定 +3，命中时附加 3D10 radiant 并驱散目标身上所有非传奇 darkness/暗影效果；若击杀目标，10 尺内所有邪恶生物须通过 DC16 智慧豁免，失败则 frightened 1 回合
- **视觉表现**：裁决时天空降下光柱，武器被太阳之火包裹，击中时圣光爆发。

---

### 套装四十二：虚空守护者（Void Warden）

**主题**：虚空的守望者，以虚无之力吞噬与重构。

#### 2件套 · 虚空护盾
> *"虚空不是空无一物，它是'一切可能性'的集合。"*

- **触发条件**：穿戴任意 2 件 `void_warden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_force", mode = "flat", value = 10 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 免疫被推离、拉动、传送
- **视觉表现**：周身有微型黑洞般的气旋，吸收附近的光线。

#### 4件套 · 虚空吞噬
> *"当虚空守护者张开双手，现实便会缺了一块。"*

- **触发条件**：穿戴全部 4 件 `void_warden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "void_devour"`）：
    - 每日一次，指定 15 尺内一个目标释放「虚空吞噬」：目标受到 4D10 force 并须通过 DC17 体质豁免，失败则其一项随机属性降低 1D4（持续 1 分钟，可叠加）；若目标被击杀，其占据的空间变为「虚空裂隙」3 回合，进入该空间的生物受到 2D8 force 且移动力减半
- **视觉表现**：掌心张开时虚空漩涡吞噬目标，空间如镜面般碎裂。

---

### 套装四十三：自然守护者（Nature Warden）

**主题**：自然的代言人，与大地、植物和野兽共鸣。

#### 2件套 · 自然共鸣
> *"自然不需要守护者，但守护者需要自然。"*

- **触发条件**：穿戴任意 2 件 `nature_warden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_poison", mode = "flat", value = 10 }, { attribute_id = "animal_handling_bonus", mode = "flat", value = 3 }]`
  - 自然环境中（森林/草原/沼泽）AC +1，且不会触发自然陷阱
- **视觉表现**：周身有藤蔓与花瓣环绕，行走时脚下生花。

#### 4件套 · 自然之怒
> *"当自然发怒，山脉会移动，河流会改道，而你最好不要挡路。"*

- **触发条件**：穿戴全部 4 件 `nature_warden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 15 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "nature_wrath"`）：
    - 每日一次，召唤自然之怒：选择以下一种效果——
      - **山崩**：20 尺半径 4D8 bludgeoning，目标 prone
      - **缠绕**：15 尺半径内所有敌人 restrained 2 回合（DC16 力量豁免逃脱）
      - **兽群**：召唤 2 只自然之灵狼（HP 30，AC 14，攻击 +5，1D8+3 piercing），持续 1 分钟
- **视觉表现**：召唤时地面裂开、藤蔓疯长或兽群虚影显现。

---

### 套装四十四：符文铁匠（Rune Smith）

**主题**：符文的雕刻者与解读者，以古老符文强化一切。

#### 2件套 · 符文铭刻
> *"每一个符文都是一个故事，而铁匠的故事总是用金属书写。"*

- **触发条件**：穿戴任意 2 件 `rune_smith_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "investigation_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以为一件武器或护甲临时铭刻符文（持续 8 小时）：武器获得 +1 攻击检定，护甲获得 +1 AC
- **视觉表现**：双手泛起淡蓝色符文光芒，铭刻时符文如活物般流动。

#### 4件套 · 符文风暴
> *"当所有符文同时激活，即使是神也要退避三舍。"*

- **触发条件**：穿戴全部 4 件 `rune_smith_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "rune_storm"`）：
    - 每日一次，释放符文风暴：15 尺半径内所有敌人受到 3D8 force + 3D8 lightning（混合伤害），须分别通过 DC16 敏捷和体质豁免，各失败则受到对应伤害并分别 prone 和 stunned 1 回合；友方在风暴中获得 +1 AC 和 +1 攻击检定，持续 2 回合
- **视觉表现**：符文从地面升起，风暴中闪电与力场符文交错爆发。

---

### 套装四十五：灵魂收割者（Soul Reaper）

**主题**：灵魂的收集者与审判者，以亡者之力对抗生者。

#### 2件套 · 魂锁
> *"灵魂不会死亡，只是换了主人。"*

- **触发条件**：穿戴任意 2 件 `soul_reaper_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_necrotic", mode = "flat", value = 15 }, { attribute_id = "religion_bonus", mode = "flat", value = 3 }]`
  - 可见灵体（幽灵、亡灵）和隐形生物
- **视觉表现**：双眼泛着幽绿色灵魂火焰，可以看见空气中漂浮的灵魂碎片。

#### 4件套 · 灵魂收割
> *"当灵魂收割者挥动镰刀，生与死的账本便要结算。"*

- **触发条件**：穿戴全部 4 件 `soul_reaper_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "soul_harvest"`）：
    - 每次击杀一个生物，收集其灵魂：恢复 2D8 HP，且获得一个「灵魂层数」（最多 5 层）；每层提供 +1 necrotic 伤害（所有攻击附加）；达到 5 层时可 bonus action 释放「灵魂爆发」：15 尺半径 5D8 necrotic，目标须通过 DC17 体质豁免，失败则 max HP 降低 10 直至长休
- **视觉表现**：击杀时灵魂被吸入收割者体内，层数叠加时周身灵魂火焰燃烧，爆发时无数灵魂尖啸冲出。

---

### 套装四十六：时间行者（Chrono Walker）

**主题**：时间的旅者，以时序之力加速、减速与回溯。

#### 2件套 · 时流感知
> *"时间不是河流，它是海洋——你可以在其中游泳，但无法逆流。"*

- **触发条件**：穿戴任意 2 件 `chrono_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "initiative_bonus", mode = "flat", value = 3 }, { attribute_id = "arcana_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 reaction 重掷一次失败的敏捷豁免
- **视觉表现**：周身有微型时钟齿轮虚影旋转，行动时拖曳时间残影。

#### 4件套 · 时间裂隙
> *"当时间行者撕裂时序，因果律也要为之让路。"*

- **触发条件**：穿戴全部 4 件 `chrono_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "movement_speed", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "time_rift"`）：
    - 每日一次，开启时间裂隙：选择以下一种效果——
      - **加速**：自身获得额外 action 和 bonus action，持续 2 回合
      - **减速**：指定一个可见敌人，其每回合只能进行 action 或 bonus action（不能两者兼有），持续 2 回合（DC17 智慧豁免抵抗）
      - **回溯**：将自身状态回溯至上回合开始（HP、位置、法术位等恢复至上回合开始时的状态，负面效果同样回溯）
- **视觉表现**：裂隙开启时周围时间流速扭曲，加速时身影模糊，减速时敌人动作如慢镜头，回溯时周身时间倒流。

---

### 套装四十七：梦境编织者（Dream Weaver）

**主题**：梦境的主宰，以幻梦之力操控现实与睡眠。

#### 2件套 · 入梦
> *"现实只是共识的梦境，而我可以修改共识。"*

- **触发条件**：穿戴任意 2 件 `dream_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "insight_bonus", mode = "flat", value = 3 }, { attribute_id = "deception_bonus", mode = "flat", value = 2 }]`
  - 免疫睡眠和梦境效果，且可以感知 30 尺内睡眠生物的梦境内容
- **视觉表现**：周身有梦幻般的彩色烟雾缭绕，双眼泛着星云般的光芒。

#### 4件套 · 噩梦降临
> *"当梦境编织者将你拉入噩梦，你最好祈祷自己不会醒来。"*

- **触发条件**：穿戴全部 4 件 `dream_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "nightmare_fall"`）：
    - 每日一次，指定一个可见敌人将其拉入噩梦：目标须通过 DC17 智慧豁免，失败则陷入噩梦（stunned 1 回合，随后每回合开始时受到 3D8 psychic 并须再次豁免，连续 3 次失败则 unconscious 1 分钟或直至受到伤害）；成功则只受到 2D8 psychic 和 frightened 1 回合
- **视觉表现**：目标脚下出现梦境漩涡，被拉入时身体僵直、表情扭曲，噩梦中有怪物虚影撕咬。

---

### 套装四十八：深渊潜行者（Abyss Stalker）

**主题**：深渊的猎手，在黑暗与疯狂边缘游走，以疯狂换取力量。

#### 2件套 · 深渊凝视
> *"凝视深渊时，深渊也在凝视你——但猎手从不眨眼。"*

- **触发条件**：穿戴任意 2 件 `abyss_stalker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_psychic", mode = "flat", value = 10 }, { attribute_id = "stealth_bonus", mode = "flat", value = 2 }]`
  - 黑暗视觉 120 尺，且在黑暗中隐匿检定 +3
- **视觉表现**：双眼完全被深渊黑暗取代，周身有触手状暗影蠕动。

#### 4件套 · 深渊吞噬
> *"当深渊潜行者释放疯狂，理智便是最先阵亡的士兵。"*

- **触发条件**：穿戴全部 4 件 `abyss_stalker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "abyss_devour"`）：
    - 每日一次，释放深渊之力：自身获得「深渊形态」1 分钟——AC +2，攻击附加 1D8 psychic + 1D8 necrotic，且每回合开始时须通过 DC15 智慧豁免，失败则受到 1D6 psychic（自身）并对 10 尺内所有敌人释放精神冲击（1D8 psychic）；期间免疫 frightened 和 charmed
- **视觉表现**：深渊形态时身体部分虚化为触手与眼球，攻击时暗影触手缠绕目标。

---

### 套装四十九：星辰观测者（Star Gazer）

**主题**：星空的研究者，以星辰之力预言与引导。

#### 2件套 · 星图
> *"每一颗星星都是一个答案，只是问题往往被遗忘。"*

- **触发条件**：穿戴任意 2 件 `star_gazer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 夜间可以感知天体位置，导航和预言检定 +3
- **视觉表现**：头顶浮现微型星图，双眼映照着星空。

#### 4件套 · 星辰坠落
> *"当星辰观测者指向天空，星星便会听从他的召唤。"*

- **触发条件**：穿戴全部 4 件 `star_gazer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }, { attribute_id = "max_mana", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "star_fall"`）：
    - 夜间，每日一次，召唤星辰坠落：指定 20 尺半径区域，1 回合后星辰坠落，所有生物受到 6D10 radiant + 3D10 fire（混合），须通过 DC18 敏捷豁免，失败则 full damage 并 prone，成功则 half damage；区域内非魔法建筑/地面被摧毁
- **视觉表现**：天空裂开，星辰如流星般坠落，撞击时光芒照亮整个战场。

---

### 套装五十：混沌使者（Chaos Herald）

**主题**：混沌的代言人，以无序之力颠覆一切规则。

#### 2件套 · 混沌印记
> *"秩序是暂时的，混沌是永恒的——而我只是提前宣布了这一点。"*

- **触发条件**：穿戴任意 2 件 `chaos_herald_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_charisma", mode = "flat", value = 3 }, { attribute_id = "arcana_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 随机改变一项属性修正（骰 1D6：1-力量、2-敏捷、3-体质、4-智慧、5-智力、6-魅力），该属性 +2，持续 1 分钟
- **视觉表现**：周身有彩虹色混沌能量流窜，属性改变时身体某部位闪烁对应颜色。

#### 4件套 · 混沌风暴
> *"当混沌使者释放风暴，连概率本身都要重新洗牌。"*

- **触发条件**：穿戴全部 4 件 `chaos_herald_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "chaos_storm"`）：
    - 每日一次，释放混沌风暴：20 尺半径内所有生物（包括友方）受到随机效果——骰 1D6：1-2D10 fire、2-2D10 cold、3-2D10 lightning、4-2D10 acid、5-2D10 thunder、6-heal 2D10；每个生物独立掷骰；随后风暴持续 3 回合，每回合开始时所有生物再次受到随机效果（独立掷骰）
- **视觉表现**：风暴中彩虹色能量乱流，火焰、冰霜、闪电、酸液、音波和治愈光芒交替爆发。

---

### 套装五十一：雷神之甲（Thunder God's Armor）

**主题**：雷霆的化身，以雷电之力粉碎一切敌人。

#### 2件套 · 雷云
> *"雷声不是警告，它是宣判。"*

- **触发条件**：穿戴任意 2 件 `thunder_god_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_lightning", mode = "flat", value = 15 }, { attribute_id = "saving_throw_dexterity", mode = "flat", value = 2 }]`
  - 雷暴天气中 AC +2，且免疫闪电伤害
- **视觉表现**：周身有电弧跳跃，呼吸时口鼻喷出电火花。

#### 4件套 · 雷神之怒
> *"当雷神之甲完全激活，天空便会成为审判之锤。"*

- **触发条件**：穿戴全部 4 件 `thunder_god_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "thunder_wrath"`）：
    - 每日一次，召唤雷神之怒：指定一个可见敌人，天空降下雷霆之锤，目标受到 6D10 lightning 并须通过 DC18 体质豁免，失败则 stunned 2 回合且所有装备过载（1 回合内无法使用魔法装备效果）；10 尺内其他生物受到 3D10 lightning（DC16 敏捷豁免 half）
- **视觉表现**：天空乌云汇聚，雷霆之锤从天而降，击中时雷电呈蛛网状扩散。

---

### 套装五十二：美杜莎之凝视（Medusa's Gaze）

**主题**：石化之眼的继承者，以凝视将敌人化为石头。

#### 2件套 · 蛇发
> *"美杜莎的头发不是蛇，它们是'警告'——不要看她的眼睛。"*

- **触发条件**：穿戴任意 2 件 `medusa_gaze_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_poison", mode = "flat", value = 10 }, { attribute_id = "intimidation_bonus", mode = "flat", value = 3 }]`
  - 免疫石化，且近战攻击附加 1D4 poison
- **视觉表现**：头发化为蛇形虚影蠕动，双眼泛着灰绿色石化光芒。

#### 4件套 · 石化凝视
> *"当美杜莎之凝视完全展开，连时间都要停下来——因为所有目击者都已石化。"*

- **触发条件**：穿戴全部 4 件 `medusa_gaze_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "petrify_gaze"`）：
    - 每日一次，开启石化凝视：15 尺锥形，所有生物须通过 DC17 体质豁免，失败则开始石化（移动力减半，AC +2 但无法 bonus action，持续 2 回合）；2 回合后若仍未通过豁免（每回合结束时可重掷），则完全石化（petrified）1 分钟或直至被 greater restoration 解除；凝视持续期间自身移动力减半
- **视觉表现**：双眼射出灰绿色光束，被照射者身体从脚部开始石化，最终完全凝固。

---

### 套装五十三：凤凰重生（Phoenix Rebirth）

**主题**：不死鸟的化身，于烈焰中毁灭，于灰烬中重生。

#### 2件套 · 余烬
> *"凤凰不是不会死，它只是懂得如何优雅地复活。"*

- **触发条件**：穿戴任意 2 件 `phoenix_rebirth_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_fire", mode = "flat", value = 15 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - HP 降至 0 时，有 25% 概率自动恢复 1D12 HP（每场战斗一次）
- **视觉表现**：周身有火苗余烬飘飞，受伤时伤口被火焰自动灼烧止血。

#### 4件套 · 凤凰涅槃
> *"当凤凰涅槃之时，死亡只是下一场火焰的序曲。"*

- **触发条件**：穿戴全部 4 件 `phoenix_rebirth_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_hp", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "phoenix_nirvana"`）：
    - 每场战斗一次，HP 降至 0 时自动触发「涅槃」：恢复至最大 HP 的 50%，对 10 尺内所有敌人造成 4D10 fire（DC17 敏捷豁免 half），且自身获得「火焰化身」3 回合——攻击附加 2D6 fire，免疫 fire，移动力 +15；涅槃后 24 小时内无法再次触发
- **视觉表现**：HP 归零时身体爆裂成火球，火球中凤凰虚影展翅，重生时火焰缠绕全身。

---

### 套装五十四：塞壬之歌（Siren's Song）

**主题**：海洋魅惑者的传承，以歌声操控心智与海浪。

#### 2件套 · 海妖之声
> *"塞壬的歌声不是音乐，它是'召唤'——召唤船只撞向礁石，召唤水手走向深渊。"*

- **触发条件**：穿戴任意 2 件 `siren_song_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "performance_bonus", mode = "flat", value = 3 }, { attribute_id = "persuasion_bonus", mode = "flat", value = 2 }]`
  - 水中移动力 +15，且可以在水中歌唱（正常施法/表演）
- **视觉表现**：歌唱时周围有水波状音波扩散，头发如海藻般飘动。

#### 4件套 · 塞壬挽歌
> *"当塞壬唱起挽歌，连海神都要沉默。"*

- **触发条件**：穿戴全部 4 件 `siren_song_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "siren_dirge"`）：
    - 每日一次，唱起塞壬挽歌：30 尺半径内所有敌人须通过 DC17 智慧豁免，失败则 charmed 2 回合（期间只能向歌声来源移动，且无法攻击）；若敌人在水中或船只上，豁免 DC +2；charmed 期间敌人每回合开始时受到 2D8 psychic；友方在歌声范围内免疫 charmed 和 frightened，且每回合开始时恢复 1D8 HP
- **视觉表现**：歌声化作实质化的水波光环，敌人被水波缠绕走向歌声来源，友方被治愈光芒笼罩。

---

### 套装五十五：狼人诅咒（Werewolf Curse）

**主题**：月光下的野兽，于人与兽之间挣扎，以野性换取力量。

#### 2件套 · 狼性
> *"狼人不是怪物，它只是被月光揭穿了真面目。"*

- **触发条件**：穿戴任意 2 件 `werewolf_curse_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 嗅觉和听觉感知检定 +3，且可以追踪 24 小时内的气味
- **视觉表现**：面部部分兽化，指甲变长如狼爪，夜间双眼泛琥珀色光芒。

#### 4件套 · 月圆狂化
> *"当月亮圆满，狼人便不再是自己——而是所有猎物的噩梦。"*

- **触发条件**：穿戴全部 4 件 `werewolf_curse_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "strength_bonus", mode = "flat", value = 2 }, { attribute_id = "dexterity_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "werewolf_rage"`）：
    - 夜间，每日一次，变身为狼人形态 1 分钟：HP +20（临时），AC +2，攻击附加 1D8 slashing + 1D6 bleed（持续 2 回合），移动力 +15，且获得「野性直觉」（无法被突袭，反应攻击有优势）；变身期间无法施法，且每回合开始时须通过 DC14 智慧豁免，失败则攻击最近的生物（敌我不分）
- **视觉表现**：变身时骨骼爆裂重组，全身长出灰黑色毛发，面部完全兽化，仰天长啸。

---

### 套装五十六：影舞者（Shadow Dancer）

**主题**：阴影中的艺术家，以暗影为画布，以死亡为舞步。

#### 2件套 · 影步
> *"影舞者不是在阴影中行走——阴影因她而存在。"*

- **触发条件**：穿戴任意 2 件 `shadow_dancer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }, { attribute_id = "acrobatics_bonus", mode = "flat", value = 2 }]`
  - 阴影中（非光照区域）移动力 +10，且可以 bonus action 传送到 15 尺内任何阴影中（每日三次）
- **视觉表现**：身体边缘与阴影融合，移动时如墨汁在水中扩散。

#### 4件套 · 影之终舞
> *"当影舞者跳完最后一支舞，舞台上只剩下影子——而影子也在慢慢消失。"*

- **触发条件**：穿戴全部 4 件 `shadow_dancer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "critical_threat_range", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "shadow_finale"`）：
    - 每日一次，从阴影中发动「影之终舞」：传送到一个可见敌人阴影中并立即进行一次近战攻击，该攻击自动视为偷袭（sneak attack，若已有 sneak attack 则额外增加 2D6）；若击杀目标，可以立即 bonus action 传送到 15 尺内另一阴影中并再次发动（最多连续 3 次）
- **视觉表现**：身影从目标影中浮现，攻击时暗影如利刃刺出，击杀后化作黑雾消散再于另一影中重现。

---

### 套装五十七：水晶先知（Crystal Seer）

**主题**：水晶的解读者，以水晶之力预知、治疗与攻击。

#### 2件套 · 水晶共鸣
> *"水晶不会说谎，它只是把真相折射成了你能理解的颜色。"*

- **触发条件**：穿戴任意 2 件 `crystal_seer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "insight_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 凝视水晶预知：下一回合开始前，对一次攻击或豁免有优势（预知后选择）
- **视觉表现**：手中常握微型水晶球，水晶球内不断有未来片段闪烁。

#### 4件套 · 水晶风暴
> *"当水晶先知释放风暴，每一颗水晶碎片都是一道预言的实现。"*

- **触发条件**：穿戴全部 4 件 `crystal_seer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "crystal_storm"`）：
    - 每日一次，释放水晶风暴：20 尺半径内所有敌人受到 4D8 force + 2D8 radiant（混合），须通过 DC17 敏捷豁免，失败则 full damage 并 blinded 1 回合；友方在风暴中受到 2D8 healing 且下一回合开始时可重掷一次失败的检定；风暴区域内所有非魔法透明物体（玻璃、水晶、冰）碎裂并化作额外伤害（1D8 slashing，范围内的生物）
- **视觉表现**：水晶从地面升起并在空中碎裂，碎片如暴雨般倾泻，光芒折射成彩虹。

---

### 套装五十八：霜巨人（Frost Giant）

**主题**：冰霜的巨人后裔，以严寒之力粉碎与冻结。

#### 2件套 · 霜肤
> *"霜巨人不是冷酷，它只是习惯了温度低到连情感都要结冰。"*

- **触发条件**：穿戴任意 2 件 `frost_giant_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_cold", mode = "flat", value = 15 }, { attribute_id = "strength_bonus", mode = "flat", value = 1 }]`
  - 免疫寒冷环境效果，且在雪地/冰原上不移动力减半
- **视觉表现**：皮肤泛着淡蓝色冰霜光泽，呼吸时喷出白色寒气，脚下留下薄冰。

#### 4件套 · 冰川崩塌
> *"当霜巨人跺脚，冰川便要回应——以崩塌的形式。"*

- **触发条件**：穿戴全部 4 件 `frost_giant_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "glacier_collapse"`）：
    - 每日一次，跺脚引发冰川崩塌：20 尺锥形 5D8 cold + 3D8 bludgeoning（混合），目标须通过 DC18 力量豁免，失败则 full damage、移动力归零 2 回合并 restrained（被冰冻结）；成功则 half damage 和移动力减半 1 回合；区域内地面变为困难地形（冰面）3 回合
- **视觉表现**：跺脚时冰锥从地面爆裂，暴风雪呼啸，敌人被冰柱穿刺冻结。

---

### 套装五十九：木乃伊诅咒（Mummy's Curse）

**主题**：远古诅咒的承载者，以腐朽与疾病侵蚀敌人。

#### 2件套 · 裹尸布
> *"木乃伊的裹尸布不是绷带，它是'封印'——封印着不该被遗忘的愤怒。"*

- **触发条件**：穿戴任意 2 件 `mummy_curse_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_necrotic", mode = "flat", value = 15 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 免疫疾病和诅咒效果削弱（诅咒仍然存在，但负面效果减半）
- **视觉表现**：周身缠绕腐朽绷带虚影，行走时掉落干枯碎片，散发古老防腐剂气味。

#### 4件套 · 腐朽之触
> *"当木乃伊触碰你，时间会在你身上加速一万倍。"*

- **触发条件**：穿戴全部 4 件 `mummy_curse_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "mummy_decay"`）：
    - 近战攻击附加 2D6 necrotic + 目标须通过 DC16 体质豁免，失败则「腐朽」：每回合开始时受到 1D6 necrotic 且 max HP 降低 2（持续 3 回合或直至被 greater restoration 解除）；被腐朽击杀的生物化为沙尘，无法通过常规复活术复活（需要 resurrection 或更高级）
- **视觉表现**：攻击时腐朽绷带缠绕目标，被击中处迅速干枯腐朽，皮肤剥落如古纸。

---

### 套装六十：龙骑士（Dragon Rider）

**主题**：龙族的盟友与驾驭者，以龙之力翱翔与毁灭。

#### 2件套 · 龙息共鸣
> *"龙骑士不是驯服了龙，他只是赢得了龙的尊重——而尊重是龙唯一能给予的东西。"*

- **触发条件**：穿戴任意 2 件 `dragon_rider_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_fire", mode = "flat", value = 10 }, { attribute_id = "intimidation_bonus", mode = "flat", value = 3 }]`
  - 龙类生物对你的态度 +1 等级（敌对→中立，中立→友善）
- **视觉表现**：周身有龙鳞纹路浮现，呼吸时喷出对应龙种颜色的微弱火焰/冰霜/闪电。

#### 4件套 · 龙骑降临
> *"当龙骑士从天而降，天空会为他让路，大地会为他颤抖。"*

- **触发条件**：穿戴全部 4 件 `dragon_rider_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 2 }, { attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "dragon_rider_descent"`）：
    - 每日一次，召唤龙骑降临：获得 60 尺飞行速度 1 分钟，期间可以 action 释放龙息（30 尺锥形 6D8 伤害，类型自选：fire/cold/lightning/acid/poison，DC17 敏捷豁免 half）；飞行期间 AC +2，且着陆时若从至少 20 尺高度俯冲攻击，伤害骰翻倍并附加 2D8 所选龙息类型
- **视觉表现**：龙翼虚影从背部展开，龙息时口中喷出对应元素，俯冲时如流星坠落。

---

*套装效果设计文档 · 套装 1–60 完结*

## 四、护甲套装效果（套装 61–100）

每套 4 件护甲 + 6 件饰品，设定 **2件套** 与 **4件套** 两个阈值。

---

### 套装六十一：血肉编织者（Flesh Weaver）

**主题**：以血肉为材料的疯狂匠人，编织生命与死亡的边界。

#### 2件套 · 血线
> *"血肉编织者不创造生命，他只是重新排列了死亡的顺序。"*

- **触发条件**：穿戴任意 2 件 `flesh_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "medicine_bonus", mode = "flat", value = 3 }, { attribute_id = "resistance_necrotic", mode = "flat", value = 10 }]`
  - 每日一次，可以 bonus action 用血肉缝合：恢复 2D8 HP 或移除一个非魔法流血/中毒效果
- **视觉表现**：双手有血丝缠绕，缝合时血丝如针线般自动穿引。

#### 4件套 · 血肉傀儡
> *"当血肉编织者展开他的'作品'，战场便成了一座移动的屠宰场。"*

- **触发条件**：穿戴全部 4 件 `flesh_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_hp", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "flesh_puppet"`）：
    - 每日一次，将一个被击杀的生物（大型或更小）转化为血肉傀儡：傀儡 HP = 击杀生物 max HP 的 50%，AC 12，攻击 +5，1D10+3 bludgeoning；傀儡持续 1 分钟或直至被摧毁；期间编织者可以 bonus action 命令傀儡移动和攻击；若傀儡被摧毁，爆炸造成 3D6 necrotic（5 尺半径，DC14 敏捷豁免 half）
- **视觉表现**：击杀的生物血肉重组，扭曲成人形怪物，关节处血丝蠕动。

---

### 套装六十二：骸骨领主（Bone Lord）

**主题**：亡骨的统御者，以骸骨为军队，以死亡为领地。

#### 2件套 · 骨甲
> *"骸骨领主不需要盔甲——他穿着亡者的骨头，而亡者从不需要保护。"*

- **触发条件**：穿戴任意 2 件 `bone_lord_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "resistance_piercing", mode = "flat", value = 10 }]`
  - 可见并指挥低级亡灵（骷髅、僵尸），它们不会主动攻击你
- **视觉表现**：皮肤下隐约可见骨骼轮廓，关节活动时发出咔哒声。

#### 4件套 · 骸骨军团
> *"当骸骨领主举起权杖，坟墓便要开门——不是为了让死者安息，而是为了让他们再次行走。"*

- **触发条件**：穿戴全部 4 件 `bone_lord_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_charisma", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "bone_legion"`）：
    - 每日一次，从地面召唤骸骨军团：2D4 只骸骨战士（HP 15，AC 13，攻击 +4，1D6+2 piercing）和 1 只骸骨法师（HP 20，AC 12，法术攻击 +5，可释放 magic missile 或 chill touch）；持续 1 分钟或直至被驱散；骸骨单位受你控制，可用 bonus action 下达群体命令
- **视觉表现**：地面裂开，骸骨从土中爬出，眼眶燃起幽蓝灵魂之火。

---

### 套装六十三：荆棘女王（Thorn Queen）

**主题**：荆棘与玫瑰的主宰，以美丽与痛苦统治一切。

#### 2件套 · 荆棘之肤
> *"玫瑰之所以美丽，是因为你知道触碰它会流血。"*

- **触发条件**：穿戴任意 2 件 `thorn_queen_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "resistance_piercing", mode = "flat", value = 10 }]`
  - 近战攻击你的生物受到 1D6 piercing（荆棘反击）
- **视觉表现**：皮肤上有玫瑰与荆棘藤蔓纹路，移动时脚下生长出微型玫瑰。

#### 4件套 · 荆棘王座
> *"当荆棘女王坐下，大地便要为她生长出王座——而王座的代价，是鲜血。"*

- **触发条件**：穿戴全部 4 件 `thorn_queen_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "thorn_throne"`）：
    - 每日一次，在 15 尺半径内召唤荆棘领域：领域内所有敌人移动力减半，且每回合开始时受到 2D6 piercing（DC15 敏捷豁免 half）；领域内友方 AC +1 且每次被近战攻击时，攻击者受到 1D6 piercing；持续 1 分钟；领域内被击杀的生物化为荆棘肥料，荆棘范围扩大 5 尺
- **视觉表现**：地面爆发荆棘藤蔓，玫瑰盛开，藤蔓缠绕敌人，花瓣化为锋利刀片飞舞。

---

### 套装六十四：灰烬行者（Ash Walker）

**主题**：灰烬中的幸存者，于毁灭后重生，以余烬之力燃烧敌人。

#### 2件套 · 余烬之躯
> *"灰烬行者不是从火中逃生的人——他是火选择留下的人。"*

- **触发条件**：穿戴任意 2 件 `ash_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_fire", mode = "flat", value = 15 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 火焰环境中（温度极高/火山）移动力不受困难地形影响，且可以走过灰烬/岩浆表面（免疫火焰伤害部分）
- **视觉表现**：身体边缘有灰烬剥落，行走时脚下留下焦黑足迹，双眼如余烬般暗红。

#### 4件套 · 灰烬风暴
> *"当灰烬行者释放风暴，世界将回到它最初的模样——一片焦土。"*

- **触发条件**：穿戴全部 4 件 `ash_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "ash_storm"`）：
    - 每日一次，释放灰烬风暴：20 尺半径内所有生物受到 4D8 fire + 2D8 necrotic（混合），须通过 DC17 体质豁免，失败则 full damage 且呼吸困难（下一回合只能进行 action 或 bonus action，不能两者兼有）；风暴持续 3 回合，每回合开始时范围内生物受到 2D8 fire；风暴范围内能见度降至 5 尺
- **视觉表现**：灰烬如暴风雪般旋转，火焰在灰烬中闪烁，天地间一片昏暗焦黑。

---

### 套装六十五：迷雾行者（Mist Walker）

**主题**：迷雾中的幽灵，穿梭于可见与不可见之间。

#### 2件套 · 雾隐
> *"迷雾行者不是在雾中行走——他就是雾本身。"*

- **触发条件**：穿戴任意 2 件 `mist_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }, { attribute_id = "saving_throw_dexterity", mode = "flat", value = 2 }]`
  - 雾/霾/蒸汽环境中完全隐形（如同 greater invisibility），且可以传送到 20 尺内任何雾气中（每日两次）
- **视觉表现**：身体部分半透明，呼吸时吐出雾气，周围空气湿度明显增加。

#### 4件套 · 雾中死神
> *"当迷雾降临，死神便开始了他的巡游——而迷雾行者，是死神的向导。"*

- **触发条件**：穿戴全部 4 件 `mist_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "critical_threat_range", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "mist_reaper"`）：
    - 每日一次，将 30 尺半径区域变为浓雾领域：领域内所有敌人视野降至 5 尺，且 mist walker 在领域内完全隐形并可自由传送；每回合可以用 bonus action 从雾中突袭一个敌人（攻击检定有优势，命中时附加 3D6 necrotic）；持续 1 分钟；领域结束时，所有在领域内停留超过 3 回合的敌人受到 2D6 cold（肺部被雾气侵蚀）
- **视觉表现**：浓雾如潮水般涌来，雾中偶尔闪过镰刀虚影，敌人被突袭时雾气凝结成利刃。

---

### 套装六十六：铁处女（Iron Maiden）

**主题**：钢铁与痛苦的化身，以绝对防御换取毁灭性反击。

#### 2件套 · 铁壳
> *"铁处女不是残忍，她只是把'保护'做到了极致——哪怕保护的方式是痛苦。"*

- **触发条件**：穿戴任意 2 件 `iron_maiden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 2 }, { attribute_id = "resistance_bludgeoning", mode = "flat", value = 10 }]`
  - 站立不动时 AC 再 +2，但移动力 -5
- **视觉表现**：周身覆盖钢铁甲片，甲片上有尖刺装饰，移动时金属摩擦发出刺耳声响。

#### 4件套 · 铁刺刑
> *"当铁处女打开她的外壳，里面不是温柔——而是所有被囚禁的痛苦的释放。"*

- **触发条件**：穿戴全部 4 件 `iron_maiden_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "iron_spike"`）：
    - 被近战攻击命中时，可以 reaction 释放「铁刺刑」：攻击者受到 2D10 piercing（无视护甲），且须通过 DC16 力量豁免，失败则武器被铁壳夹住（下回合无法攻击，需 bonus action 拔出）；每日可反应三次；主动攻击时，可以 bonus action 打开铁壳释放积蓄的痛苦：对 5 尺内一个敌人造成 4D10 piercing + 2D8 psychic（目标须通过 DC17 体质豁免，失败则 frightened 1 回合）
- **视觉表现**：被攻击时铁壳弹开尖刺刺入攻击者，主动释放时铁壳如花朵般绽放，内部尖刺如洪流般涌出。

---

### 套装六十七：蜘蛛女王（Spider Queen）

**主题**：蛛网的统治者，以丝线与毒素编织死亡的陷阱。

#### 2件套 · 蛛丝
> *"蜘蛛女王不是在织网——她在编织命运。"*

- **触发条件**：穿戴任意 2 件 `spider_queen_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }, { attribute_id = "resistance_poison", mode = "flat", value = 10 }]`
  - 可以攀爬任何表面（包括天花板），且不会触发蛛网/丝类陷阱
- **视觉表现**：手指间有蛛丝渗出，行走时身后留下几乎不可见的丝线，瞳孔如蜘蛛般多眼。

#### 4件套 · 蛛网领域
> *"当蜘蛛女王展开她的网，整个战场都将成为她的巢穴。"*

- **触发条件**：穿戴全部 4 件 `spider_queen_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "spider_web"`）：
    - 每日一次，在 20 尺半径内展开蛛网领域：领域内地面覆盖粘性蛛网，所有敌人移动力减半且每回合开始时须通过 DC16 敏捷豁免，失败则 restrained；蜘蛛女王在领域内移动力 +15，且可以 bonus action 传送到领域内任何蛛网位置；领域内敌人被 restrained 时，女王可以 bonus action 注入毒素（2D8 poison + 1D6 每回合持续 3 回合）；持续 1 分钟
- **视觉表现**：蛛网从四面八方蔓延，粘住敌人的手脚，女王在蛛网上如滑行般移动，注入毒素时蛛网泛绿光。

---

### 套装六十八：乌鸦领主（Raven Lord）

**主题**：死亡与预言的使者，以乌鸦之眼洞察命运，以黑翼收割灵魂。

#### 2件套 · 鸦羽
> *"乌鸦领主不是带来死亡的人——他只是比死亡早到一步。"*

- **触发条件**：穿戴任意 2 件 `raven_lord_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "perception_bonus", mode = "flat", value = 3 }, { attribute_id = "insight_bonus", mode = "flat", value = 2 }]`
  - 可以感知 30 尺内濒死生物（HP ≤ 10% max），且对这些生物的攻击检定 +2
- **视觉表现**：黑羽从衣摆飘落，双眼如乌鸦般漆黑反光，头顶常有乌鸦虚影盘旋。

#### 4件套 · 死亡鸦群
> *"当乌鸦群飞而至，死亡便已签收——而乌鸦领主，只是送快递的人。"*

- **触发条件**：穿戴全部 4 件 `raven_lord_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "raven_flock_death"`）：
    - 每日一次，召唤死亡鸦群：指定一个可见敌人，鸦群围绕其攻击 3 回合；每回合鸦群造成 3D8 slashing + 1D8 necrotic（目标须通过 DC16 敏捷豁免，失败则 blinded 该回合）；若目标在鸦群持续期间 HP 降至 0，鸦群立即吞噬其灵魂（无法通过复活术复活，需要 resurrection 或更高级）；鸦群期间乌鸦领主可以 bonus action 命令鸦群转移目标
- **视觉表现**：黑鸦群从四面八方涌来，围绕目标撕咬，目标被黑色羽翼完全遮蔽。

---

### 套装六十九：锈刃骑士（Rust Blade Knight）

**主题**：腐朽与衰败的骑士，以锈铁之力侵蚀敌人的装备与意志。

#### 2件套 · 锈甲
> *"锈刃骑士的盔甲不是旧了——它是'成熟'，就像好酒需要陈酿。"*

- **触发条件**：穿戴任意 2 件 `rust_blade_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "resistance_acid", mode = "flat", value = 10 }]`
  - 近战攻击命中时，目标装备（护甲或武器）有 20% 概率生锈（-1 AC 或 -1 攻击检定，可叠加至 -3，持续至被修复）
- **视觉表现**：盔甲和武器覆盖红褐色锈迹，攻击时锈粉飞扬，金属摩擦发出刺耳锈蚀声。

#### 4件套 · 锈蚀瘟疫
> *"当锈刃骑士释放瘟疫，连钢铁都要哭泣——因为哭泣是钢铁唯一能做的，当它生锈时。"*

- **触发条件**：穿戴全部 4 件 `rust_blade_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "rust_plague"`）：
    - 每日一次，释放锈蚀瘟疫：15 尺半径内所有敌人的金属装备（护甲、武器、盾牌）立即获得 2 层锈蚀（-2 AC 或 -2 攻击）；持续 3 回合，每回合开始时再叠加 1 层；被完全锈蚀（-3）的装备有 50% 概率在受到重击时碎裂（彻底损毁）；非金属构造体/构装生物受到 4D8 acid（DC17 体质豁免 half）
- **视觉表现**：红褐色锈雾扩散，金属装备迅速生锈剥落，构造体表面被腐蚀出孔洞。

---

### 套装七十：镜中恶魔（Mirror Demon）

**主题**：镜中世界的入侵者，以反射与复制玩弄现实。

#### 2件套 · 镜影
> *"镜中恶魔不是在镜子中——镜子中的只是你的倒影，而它，是倒影中的你。"*

- **触发条件**：穿戴任意 2 件 `mirror_demon_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "deception_bonus", mode = "flat", value = 3 }, { attribute_id = "saving_throw_charisma", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 与一个可见敌人「镜像」：2 回合内，该敌人对你使用的任何非传奇能力/法术，你获得一次免费使用机会（复制其效果，DC/攻击检定使用你的数值）
- **视觉表现**：身体边缘有镜面反光，移动时身后留下镜面碎片般的残影。

#### 4件套 · 镜界崩塌
> *"当镜中恶魔打破镜子，碎裂的不只是玻璃——还有现实本身。"*

- **触发条件**：穿戴全部 4 件 `mirror_demon_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "mirror_shatter"`）：
    - 每日一次，打破镜界：创造一个 15 尺半径的镜中领域 1 分钟；领域内所有敌人的攻击有 50% 概率被反射（攻击者自己受到一半伤害）；敌人施法时有 30% 概率被反射回自身；镜中恶魔在领域内可以 bonus action 与一个敌人交换位置（强制，DC17 魅力豁免抵抗）；领域结束时，所有在领域内的敌人受到 3D8 psychic（现实崩塌的冲击）
- **视觉表现**：领域边界由无数镜子组成，攻击被镜子反射，敌人看到无数个自己被碎裂的镜面切割。

---

### 套装七十一：武士之魂（Samurai Spirit）

**主题**：东方武士的英灵，以荣誉与利刃为信仰。

#### 2件套 · 武士道
> *"武士之魂不是不畏惧死亡——他只是把死亡视为最后一场决斗的对手。"*

- **触发条件**：穿戴任意 2 件 `samurai_spirit_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "saving_throw_wisdom", mode = "flat", value = 2 }]`
  - 免疫 frightened 和 charmed（荣誉不可动摇）
- **视觉表现**：周身有樱花花瓣飘落，刀鞘上有家纹浮现，眼神坚毅如 steel。

#### 4件套 · 居合斩
> *"当武士拔刀，时间便要停下来——不是因为魔法，而是因为那一刀，值得被记住。"*

- **触发条件**：穿戴全部 4 件 `samurai_spirit_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "critical_threat_range", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "iaijutsu"`）：
    - 每场战斗一次，可以进行「居合斩」：整轮准备一个 action，下一回合开始时立即拔刀攻击——该攻击检定有优势，且命中时视为暴击（自动触发暴击效果，伤害骰翻倍）；若目标 HP 低于 50%，该攻击附加 3D10 slashing；居合斩后本回合无法再进行其他 action
- **视觉表现**：收刀入鞘，周围樱花静止，拔刀瞬间银光一闪，时间仿佛凝固，随后樱花暴雨般飞散。

---

### 套装七十二：忍者之影（Ninja Shadow）

**主题**：暗影中的刺客大师，以烟雾与幻影完成致命一击。

#### 2件套 · 烟遁
> *"忍者不是在隐藏——他是在重新定义'可见'。"*

- **触发条件**：穿戴任意 2 件 `ninja_shadow_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }, { attribute_id = "acrobatics_bonus", mode = "flat", value = 2 }]`
  - 每日两次，可以 bonus action 释放烟雾弹：5 尺半径浓烟，持续 1 回合，你在烟雾中完全隐形且可以传送到 20 尺内任何位置
- **视觉表现**：周身有黑色烟雾缭绕，移动时几乎不发出声音，双眼如猫般在黑暗中发光。

#### 4件套 · 影分身
> *"当忍者释放影分身，你面对的不是一个人——而是一个连队的幽灵。"*

- **触发条件**：穿戴全部 4 件 `ninja_shadow_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "shadow_clone"`）：
    - 每日一次，创造 1D4+1 个影分身：分身拥有 1 HP，AC = 你的 AC，可以进行一次攻击（+5，1D6+3 piercing）；持续 2 回合；分身与你外观完全相同，敌人无法分辨真假（须通过 DC18 洞察豁免）；你每次攻击时，所有分身同步攻击同一目标（造成额外 1D6 piercing 每个分身）；分身被击破时爆炸成烟雾，5 尺内生物 blinded 1 回合
- **视觉表现**：身体分裂成多个黑影，每个黑影动作同步，攻击时如黑色风暴般席卷目标。

---

### 套装七十三：少林武僧（Shaolin Monk）

**主题**：东方武道的极致，以内力与徒手技艺战胜一切。

#### 2件套 · 气功
> *"少林武僧的拳头不是武器——它是'答案'，而问题，是敌人的存在本身。"*

- **触发条件**：穿戴任意 2 件 `shaolin_monk_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "unarmed_attack_bonus", mode = "flat", value = 2 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 徒手攻击视为魔法武器，且可以 bonus action 进行额外一次徒手攻击
- **视觉表现**：双手有淡金色内力光芒，攻击时空气震荡，周身有佛经符文闪烁。

#### 4件套 · 如来神掌
> *"当少林武僧举起手掌，天空会为之低首——因为那一掌，承载着千年的修行。"*

- **触发条件**：穿戴全部 4 件 `shaolin_monk_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "unarmed_attack_bonus", mode = "flat", value = 2 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "buddha_palm"`）：
    - 每日一次，释放如来神掌：15 尺锥形 5D10 force + 3D10 radiant（混合），目标须通过 DC18 力量豁免，失败则 full damage 并 prone 且 stunned 1 回合；成功则 half damage 和移动力减半 1 回合；被击中的邪恶生物额外受到 2D10 radiant（神圣制裁）；释放后武僧获得「佛光护体」2 回合——AC +2，免疫 necrotic 和 poison
- **视觉表现**：天空降下金色佛掌虚影，掌印覆盖整个区域，击中时金光大盛，佛经吟唱声回荡。

---

### 套装七十四：天狗面具（Tengu Mask）

**主题**：山林之神的化身，以风暴与剑术守护山林。

#### 2件套 · 天狗翼
> *"天狗不是在飞翔——他是在让风托着他，因为风欠他一个人情。"*

- **触发条件**：穿戴任意 2 件 `tengu_mask_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "movement_speed", mode = "flat", value = 10 }, { attribute_id = "perception_bonus", mode = "flat", value = 2 }]`
  - 获得 30 尺飞行速度（山林环境中 60 尺），且在山林中隐匿检定 +3
- **视觉表现**：背后有乌鸦翅膀虚影，面部被红色天狗面具覆盖，长鼻子微微颤动。

#### 4件套 · 天狗风暴
> *"当天狗扇动翅膀，风暴便要从山林中苏醒。"*

- **触发条件**：穿戴全部 4 件 `tengu_mask_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "tengu_storm"`）：
    - 每日一次，扇动天狗之翼释放风暴：20 尺锥形强风，所有生物受到 4D8 bludgeoning 并须通过 DC17 力量豁免，失败则被推离 20 尺并 prone；风暴中你获得 60 尺飞行速度 2 回合，且可以进行「风刃斩」——飞行时经过的每个敌人受到 2D8 slashing（DC15 敏捷豁免 half）；山林中释放时，风暴额外召唤落雷（3D10 lightning，DC16 敏捷豁免 half）
- **视觉表现**：双翼展开，狂风呼啸，树木被连根拔起，飞行时身后拖曳风刃轨迹。

---

### 套装七十五：九尾妖狐（Nine-Tailed Fox）

**主题**：狡诈与魅力的极致，以幻术和火焰玩弄人心。

#### 2件套 · 狐火
> *"九尾狐的火焰不是热——它是'欲望'，而欲望总是温暖的，直到它把你烧成灰。"*

- **触发条件**：穿戴任意 2 件 `nine_tailed_fox_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "charisma_bonus", mode = "flat", value = 2 }, { attribute_id = "deception_bonus", mode = "flat", value = 3 }]`
  - 每日一次，可以 bonus action 创造一个自身幻象（持续 2 回合，吸引攻击，1 HP），且你可以传送到幻象位置（作为幻象消散时的 bonus action）
- **视觉表现**：身后有九条狐尾虚影摇曳，双眼泛着魅惑的琥珀色光芒，周身有蓝色狐火漂浮。

#### 4件套 · 九尾天火
> *"当九尾全部展开，天空便要燃烧——不是因为愤怒，而是因为美丽。"*

- **触发条件**：穿戴全部 4 件 `nine_tailed_fox_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "nine_tail_fire"`）：
    - 每日一次，释放九尾天火：九条狐尾同时喷射火焰，覆盖 30 尺锥形 6D10 fire + 3D10 force（混合），目标须通过 DC18 敏捷豁免，失败则 full damage 并燃烧（每回合 2D6 fire，持续 3 回合或直至被扑灭）；成功则 half damage 和 1D6 fire 燃烧 1 回合；释放后你获得「狐神化身」2 回合——AC +2，移动力 +20，且每次攻击附加 1D10 fire
- **视觉表现**：九尾全部展开如扇，每条尾巴喷射不同颜色火焰，天空被火光染红，地面融化成岩浆。

---

### 套装七十六：阴阳师（Onmyoji）

**主题**：阴阳两界的调和者，以式神与符咒驱邪降妖。

#### 2件套 · 式神契
> *"阴阳师不是在召唤式神——他是在邀请一位老朋友来喝茶，顺便让老朋友帮他打架。"*

- **触发条件**：穿戴任意 2 件 `onmyoji_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "religion_bonus", mode = "flat", value = 2 }]`
  - 每日一次，召唤一只小型式神（选择：火鼠/水虎/风狸/雷犬），持续 10 分钟：式神 HP 20，AC 14，攻击 +5，1D8+2 对应元素伤害；式神可提供协助（help action）或进行攻击
- **视觉表现**：符咒从袖中飞出，式神从符咒中显现，周身有阴阳太极图案旋转。

#### 4件套 · 百鬼夜行
> *"当阴阳师展开百鬼夜行卷轴，连鬼神都要排队领号。"*

- **触发条件**：穿戴全部 4 件 `onmyoji_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "hyakki_yako"`）：
    - 每日一次，展开百鬼夜行卷轴：召唤 1D4+1 只随机式神（从 12 种式神中随机：每种有不同元素和效果，如火鼠造成 fire + 燃烧，雪女造成 cold + 冻结，雷兽造成 lightning + 眩晕等）；式神 HP 25，AC 15，攻击 +6，1D10+3 元素伤害 + 特殊效果；持续 1 分钟；你可以用 bonus action 指挥所有式神攻击同一目标或分散攻击；式神被摧毁时对 5 尺内敌人造成 2D8 对应元素伤害
- **视觉表现**：卷轴展开，无数式神从画中跳出，鬼火、冰霜、雷电交织，百鬼咆哮声震耳欲聋。

---

### 套装七十七：夜叉（Yaksha）

**主题**：战场的恶鬼，以暴怒与力量碾碎一切。

#### 2件套 · 鬼面
> *"夜叉不是在战斗——他是在庆祝，而庆祝的方式，是把敌人撕成碎片。"*

- **触发条件**：穿戴任意 2 件 `yaksha_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "strength_bonus", mode = "flat", value = 2 }, { attribute_id = "intimidation_bonus", mode = "flat", value = 3 }]`
  - HP 低于 50% 时攻击检定 +2，低于 25% 时再 +2（暴怒叠加）
- **视觉表现**：面部浮现恶鬼面具，皮肤泛青，獠牙外露，双眼如血红灯笼。

#### 4件套 · 修罗化身
> *"当夜叉进入修罗状态，连他自己都不知道会做出什么——但他知道，那一定很壮观。"*

- **触发条件**：穿戴全部 4 件 `yaksha_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "max_hp", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "asura_form"`）：
    - 每日一次，进入修罗化身 1 分钟：体型增大一级，力量 +4（上限突破至 24），攻击附加 2D8 bludgeoning + 1D8 necrotic，移动力 +10，且每次命中可以 bonus action 进行擒抱（DC17 力量豁免）；化身期间免疫 frightened 和 charmed，但每回合开始时受到 1D6 necrotic（肉体超负荷）；化身结束后 exhaustion 1 级
- **视觉表现**：身体膨胀，肌肉爆裂铠甲，皮肤裂开露出黑色血管，头顶长出鬼角，手持巨斧虚影。

---

### 套装七十八：机关傀儡师（Karakuri Puppeteer）

**主题**：精密机械的操控者，以傀儡与机关改变战场格局。

#### 2件套 · 傀儡线
> *"机关傀儡师不是在操控傀儡——他是在与傀儡共舞，而舞步，是死亡。"*

- **触发条件**：穿戴任意 2 件 `karakuri_puppeteer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3 }, { attribute_id = "investigation_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action 部署一个机关陷阱（选择：绊线/毒气/爆炸/针刺），陷阱持续 10 分钟，触发时造成 2D8 对应伤害（DC15 相应豁免 half）
- **视觉表现**：手指间有细如发丝的傀儡线，袖中藏有微型机关零件，行走时轻微机械咔哒声。

#### 4件套 · 傀儡剧场
> *"当机关傀儡师展开剧场，所有观众都将参与演出——无论他们是否愿意。"*

- **触发条件**：穿戴全部 4 件 `karakuri_puppeteer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "puppet_theater"`）：
    - 每日一次，展开傀儡剧场：召唤 2 只大型战斗傀儡（HP 40，AC 16，攻击 +7，2D8+4 slashing）；傀儡受你完全控制，可以用 action 指挥一个傀儡进行攻击或使用特殊能力（护盾：为友方提供 +2 AC 1 回合；突刺：30 尺冲锋攻击，伤害翻倍；自爆：3D10 fire，5 尺半径）；持续 1 分钟；剧场内你可以用 bonus action 与任一傀儡交换位置
- **视觉表现**：地面升起木制剧场舞台，傀儡从幕布后走出，关节处有蒸汽喷出，动作精密如钟表。

---

### 套装七十九：雷神之鼓（Raijin Drum）

**主题**：雷神的鼓手，以鼓声召唤天雷，以节奏毁灭敌人。

#### 2件套 · 雷鸣
> *"雷神之鼓不是在演奏——它是在与天空对话，而天空的回答，是闪电。"*

- **触发条件**：穿戴任意 2 件 "raijin_drum_set"`raijin_drum_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "performance_bonus", mode = "flat", value = 3 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 雷暴天气中，你的法术和攻击附加 1D6 lightning；且可以感知 1 里内的雷暴中心
- **视觉表现**：背后有雷鼓虚影，双手持鼓槌，周身有电弧跳动，击鼓时雷声轰鸣。

#### 4件套 · 天雷万鼓
> *"当雷神之鼓敲响万鼓，天空便要倾泻它的愤怒——而 drum，只是开始。"*

- **触发条件**：穿戴全部 4 件 `raijin_drum_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "thunder_drum"`）：
    - 每日一次，敲响天雷万鼓：每回合开始时（持续 3 回合），天空降下 1D4+1 道闪电，每道闪电指定一个可见敌人造成 3D10 lightning（DC17 敏捷豁免 half）；若敌人在同一回合被两道以上闪电击中，额外受到 2D10 lightning 并 stunned 1 回合；友方在鼓声范围内免疫 lightning 和 thunder，且攻击检定 +1
- **视觉表现**：雷鼓悬浮于头顶自动敲响，闪电如暴雨般倾泻，雷声与鼓声合奏成毁灭交响曲。

---

### 套装八十：河童铠甲（Kappa Armor）

**主题**：水域的守护者，以水的力量防御与反击。

#### 2件套 · 水之甲
> *"河童不是在水中游泳——他是在水中行走，因为水是他的领地。"*

- **触发条件**：穿戴任意 2 件 `kappa_armor_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }, { attribute_id = "athletics_bonus", mode = "flat", value = 3 }]`
  - 水中呼吸，水中移动力 +20，且可以在水面行走
- **视觉表现**：头顶有碟状水洼（河童的特征），周身有水膜覆盖，水中移动时如鱼般流畅。

#### 4件套 · 河童夺气
> *"当河童夺取你的'气'，你失去的不只是力量——还有尊严，因为你是被一个头顶水洼的生物打败的。"*

- **触发条件**：穿戴全部 4 件 `kappa_armor_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "strength_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "kappa_drain"`）：
    - 每日一次，对 5 尺内一个敌人使用「夺气」：目标须通过 DC16 力量豁免，失败则失去 1D4 力量（持续 1 分钟，可叠加至 -5）并受到 3D8 necrotic；你获得等同于被吸取力量的数值（力量 +1D4，持续 1 分钟）；若目标力量降至 0，则 prone 并 unconscious 1 分钟；水中使用时，豁免 DC +2 且你可以 bonus action 将目标拖入水中（若附近有水源）
- **视觉表现**：双手插入目标胸口（虚影），绿色「气」被抽出吸入头顶水洼，目标虚弱倒地。

---

### 套装八十一：蒸汽骑士（Steam Knight）

**主题**：蒸汽朋克的战士，以高压蒸汽驱动力量与速度。

#### 2件套 · 蒸汽引擎
> *"蒸汽骑士不是在穿戴盔甲——他是在驾驶一台机器，而机器的动力，是沸腾的水。"*

- **触发条件**：穿戴任意 2 件 `steam_knight_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "strength_bonus", mode = "flat", value = 1 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 移动力 +10，且可以 bonus action 释放蒸汽推进（额外 15 尺直线移动，不引发 opportunity attack）
- **视觉表现**：盔甲背部有蒸汽排气管，关节处有黄铜齿轮转动，移动时喷出白色蒸汽。

#### 4件套 · 蒸汽过载
> *"当蒸汽骑士过载引擎，他要做的不是控制——而是抓紧，因为接下来的一切，都是物理学的报复。"*

- **触发条件**：穿戴全部 4 件 `steam_knight_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "steam_overload"`）：
    - 每日一次，蒸汽过载 3 回合：每回合开始时获得一个额外 action（只能用于攻击或移动），移动力 +20，攻击附加 2D6 fire（蒸汽灼烧）；过载期间每回合结束时受到 1D6 fire（引擎过热）；过载结束后 1 回合内无法行动（引擎冷却）；若过载期间 HP 降至 0，引擎爆炸——10 尺半径 4D10 fire + 2D10 force（DC17 敏捷豁免 half）
- **视觉表现**：排气管喷出赤红色过热蒸汽，齿轮转速加快至模糊，攻击时蒸汽如火焰般喷发。

---

### 套装八十二：发条之心（Clockwork Heart）

**主题**：精密机械的化身，以钟表之力操控时间与节奏。

#### 2件套 · 钟摆
> *"发条之心的每一次跳动，都是一次精准的计算——而计算的结果，总是胜利。"*

- **触发条件**：穿戴任意 2 件 `clockwork_heart_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "initiative_bonus", mode = "flat", value = 3 }, { attribute_id = "investigation_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 bonus action「校准」：下一回合开始前，所有检定 +2（精准时机）
- **视觉表现**：胸口有透明窗口可见发条心脏跳动，周身有微型齿轮虚影旋转，动作如钟表般精确。

#### 4件套 · 时间齿轮
> *"当发条之心释放时间齿轮，因果律便成了可拆卸的零件。"*

- **触发条件**：穿戴全部 4 件 `clockwork_heart_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "time_gear"`）：
    - 每日一次，释放时间齿轮：指定一个可见敌人，其时间被「减速」2 回合——每回合只能进行 action 或 bonus action（不能两者兼有），移动力减半，且攻击检定有劣势；同时指定一个友方，其时间被「加速」2 回合——每回合获得一个额外 bonus action，移动力 +15，攻击检定 +2；时间齿轮效果无法被 dispel，但可以通过 DC18 奥术检定破坏
- **视觉表现**：巨型齿轮虚影笼罩敌友双方，敌人动作如慢镜头，友方动作如快放，齿轮咔哒声支配战场节奏。

---

### 套装八十三：以太行者（Aether Walker）

**主题**：以太位面的旅者，以虚空能量穿越物质与灵界。

#### 2件套 · 以太共鸣
> *"以太行者不是在走路——他是在以太中游泳，而物质世界，只是海底。"*

- **触发条件**：穿戴任意 2 件 `aether_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "saving_throw_dexterity", mode = "flat", value = 2 }]`
  - 每日两次，可以 bonus action 进入以太位面 1 回合：物质世界无法攻击你，你可以穿过墙壁和障碍物，但无法攻击物质生物；结束时选择出现位置（30 尺内）
- **视觉表现**：身体部分虚化如幽灵，颜色褪为灰白，移动时如水中倒影般扭曲。

#### 4件套 · 以太风暴
> *"当以太行者撕开位面壁垒，两个世界都要为此付出代价。"*

- **触发条件**：穿戴全部 4 件 `aether_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "aether_storm"`）：
    - 每日一次，撕开以太裂隙：15 尺半径内所有敌人受到 4D10 force + 2D10 psychic（混合），须通过 DC17 智慧豁免，失败则 full damage 并 disoriented 2 回合（攻击检定和感知检定有劣势）；裂隙持续 3 回合，每回合开始时范围内生物受到 2D10 force；你可以用 bonus action 传送到裂隙内任何位置；裂隙结束时，所有在裂隙内的生物（包括友方）有 20% 概率被短暂拉入以太位面 1 回合（stunned）
- **视觉表现**：空间撕裂，以太能量如银色洪流涌出，现实与灵界交错，生物在裂隙中若隐若现。

---

### 套装八十四：磁力大师（Magnet Master）

**主题**：磁场的操控者，以磁力吸引、排斥与粉碎金属。

#### 2件套 · 磁场
> *"磁力大师不是在操控金属——他是在与金属谈判，而谈判的筹码，是电磁力。"*

- **触发条件**：穿戴任意 2 件 `magnet_master_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_strength", mode = "flat", value = 2 }, { attribute_id = "investigation_bonus", mode = "flat", value = 2 }]`
  - 可以感知 30 尺内所有金属物品和装备，且对金属护甲的敌人攻击检定 +1
- **视觉表现**：周身有蓝色磁力线环绕，金属物品向你微微倾斜，手指间有电火花跳跃。

#### 4件套 · 磁极反转
> *"当磁力大师反转磁极，连大地都要重新思考它的方向。"*

- **触发条件**：穿戴全部 4 件 `magnet_master_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "magnetic_flip"`）：
    - 每日一次，反转磁极：选择以下一种效果——
      - **吸引**：将 30 尺内所有穿着金属护甲或持金属武器的敌人拉向中心（DC17 力量豁免抵抗，失败则移动 15 尺向中心并 prone），被拉至 5 尺内的敌人受到 3D10 bludgeoning（金属碰撞）
      - **排斥**：将 10 尺内所有金属装备敌人推离 20 尺（DC17 力量豁免，失败则推离并 disarmed 武器掉落），被推离敌人撞击障碍物时受到 2D10 bludgeoning
      - **粉碎**：指定一个金属装备敌人，其金属护甲/武器受到 4D10 force（无视护甲 AC，直接伤害），且该装备有 30% 概率当场碎裂（无法使用，需修复）
- **视觉表现**：磁场如漩涡般扩张，金属装备发出刺耳嗡鸣，敌人被磁力拉扯或抛飞。

---

### 套装八十五：光子剑士（Photon Swordsman）

**主题**：光的战士，以纯粹光子凝聚利刃，以光速斩击敌人。

#### 2件套 · 光子刃
> *"光子剑士的剑不是金属——它是'凝固的光'，而光，从不犹豫。"*

- **触发条件**：穿戴任意 2 件 `photon_swordsman_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "dexterity_bonus", mode = "flat", value = 1 }]`
  - 武器被视为魔法武器且附加 1D6 radiant，且可以 bonus action 将武器转化为纯光子形态（无视非魔法护甲 AC 加成，持续 1 回合）
- **视觉表现**：武器被纯净白光包裹，挥动时留下光痕，Photon 形态时武器完全由光构成。

#### 4件套 · 光速斩
> *"当光子剑士达到光速，他的剑便不再存在于空间中——而是存在于'此刻'。"*

- **触发条件**：穿戴全部 4 件 `photon_swordsman_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "critical_threat_range", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "light_speed_slash"`）：
    - 每日一次，释放光速斩：瞬间移动到 30 尺内一个可见敌人面前并立即进行 3 次攻击（每次 +5，2D8+4 radiant + 1D6 force）；目标须通过 DC18 敏捷豁免，失败则无法 reaction（无法反击或闪避）；攻击后你可以立即传送到 15 尺内另一位置；光速斩无视 cover 和 opportunity attack
- **视觉表现**：身影化作光束消失，瞬间出现在目标面前，三道剑光同时斩出，随后再次化作光束消失。

---

### 套装八十六：音波战士（Sonic Warrior）

**主题**：声音的武器化，以音波震碎、眩晕与毁灭。

#### 2件套 · 音刃
> *"音波战士不是在挥剑——他是在演奏一首只有敌人才能听见的交响曲，而最后一个音符，是死亡。"*

- **触发条件**：穿戴任意 2 件 `sonic_warrior_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }, { attribute_id = "performance_bonus", mode = "flat", value = 2 }]`
  - 近战攻击附加 1D6 thunder，且可以 bonus action 释放音波冲击（15 尺锥形 2D8 thunder，DC14 体质豁免 half）
- **视觉表现**：武器周围有音波纹路，攻击时空气震荡如涟漪，呼吸时胸腔发出低沉共鸣。

#### 4件套 · 超声波
> *"当音波战士释放超声波，连空气都要碎裂——而碎裂的空气，会切割一切。"*

- **触发条件**：穿戴全部 4 件 `sonic_warrior_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "ultra_sonic"`）：
    - 每日一次，释放超声波：20 尺半径内所有生物受到 5D8 thunder + 3D8 force（混合），须通过 DC17 体质豁免，失败则 full damage 并 deafened 2 回合且 stunned 1 回合；成功则 half damage 和 deafened 1 回合；区域内所有非魔法玻璃/水晶/陶瓷物品碎裂；超声波可以穿透墙壁（对墙后生物伤害减半，但仍有效果）
- **视觉表现**：空气如玻璃般碎裂，音波呈环形扩散，碎裂的空气碎片如刀片般飞舞。

---

### 套装八十七：重力行者（Gravity Walker）

**主题**：重力的操控者，以引力压碎敌人或让自己飞翔。

#### 2件套 · 引力场
> *"重力行者不是在对抗重力——他只是在告诉重力，今天它该往哪个方向拉。"*

- **触发条件**：穿戴任意 2 件 `gravity_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "saving_throw_strength", mode = "flat", value = 2 }, { attribute_id = "acrobatics_bonus", mode = "flat", value = 3 }]`
  - 免疫 falling damage，且可以 bonus action 改变自身重力方向（可以在墙壁上行走/天花板倒挂，持续 1 回合）
- **视觉表现**：周身有微型引力场扭曲光线，跳跃时滞空时间明显延长，落地时地面轻微下陷。

#### 4件套 · 重力井
> *"当重力行者创造重力井，连光都要弯曲——而敌人，只能被压扁。"*

- **触发条件**：穿戴全部 4 件 `gravity_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "gravity_well"`）：
    - 每日一次，创造重力井：指定 15 尺半径区域，持续 3 回合；区域内所有敌人移动力减半，跳跃高度归零，且每回合开始时受到 2D10 force（被重力压碎）；区域内远程攻击（进入或离开）有劣势（光线弯曲）；你可以用 bonus action 将重力井内一个敌人 prone（DC16 力量豁免抵抗）；重力井结束时，所有在井内的敌人受到 3D10 force 并被推离 10 尺
- **视觉表现**：区域重力扭曲，地面下陷成漏斗状，敌人被压弯腰，光线在区域边缘弯曲。

---

### 套装八十八：量子幽灵（Quantum Ghost）

**主题**：量子态的存在，同时处于存在与不存在之间。

#### 2件套 · 量子叠加
> *"量子幽灵不是在隐身——他是在同时存在于所有位置，只是你观测到的，恰好是这一个。"*

- **触发条件**：穿戴任意 2 件 `quantum_ghost_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "stealth_bonus", mode = "flat", value = 3 }, { attribute_id = "arcana_bonus", mode = "flat", value = 2 }]`
  - 每日两次，可以 bonus action 进入「量子态」1 回合：50% 概率完全闪避任何攻击（包括范围攻击），但 50% 概率攻击也有劣势（自身也处于不确定状态）
- **视觉表现**：身体偶尔出现残影（多个位置的叠加态），颜色不断闪烁，仿佛信号不良的全息影像。

#### 4件套 · 量子坍缩
> *"当量子幽灵决定'确定'自己的位置，概率便要付出代价——而这个代价，是敌人的生命。"*

- **触发条件**：穿戴全部 4 件 `quantum_ghost_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "quantum_collapse"`）：
    - 每日一次，引发量子坍缩：指定 20 尺半径区域，区域内所有敌人受到随机效果——每个敌人独立掷 1D4：1-受到 4D10 force（被空间压缩）、2-被 teleport 随机 15 尺（可能撞墙受到 2D6 bludgeoning）、3-该回合所有行动有劣势（时间线紊乱）、4-受到 2D10 psychic（目睹多重现实）；随后你可以选择区域内一个位置「确定」自己的存在，立即传送到该位置并对 5 尺内一个敌人进行攻击（该攻击检定有优势且视为偷袭）
- **视觉表现**：区域内现实碎裂成无数碎片，敌人同时出现在多个位置又消失，最终一切坍缩成一个点，你从该点浮现。

---

### 套装八十九：辐射行者（Radiation Walker）

**主题**：辐射的化身，以原子之力衰变与毁灭。

#### 2件套 · 衰变光环
> *"辐射行者不是在散发辐射——他是在与原子对话，而原子的回答，是裂变。"*

- **触发条件**：穿戴任意 2 件 `radiation_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_radiant", mode = "flat", value = 15 }, { attribute_id = "resistance_necrotic", mode = "flat", value = 10 }]`
  - 5 尺内所有敌人每回合开始时受到 1D6 radiant（辐射衰变），且你免疫辐射/毒素/疾病
- **视觉表现**：周身有淡绿色辐射光晕， Geiger 计数器般的声音隐约可闻，呼吸时吐出荧光雾气。

#### 4件套 · 临界质量
> *"当辐射行者达到临界质量，他不再是行者——他是一颗行走的恒星。"*

- **触发条件**：穿戴全部 4 件 `radiation_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "critical_mass"`）：
    - 每日一次，达到临界质量并释放：20 尺半径内所有生物受到 6D10 radiant + 4D10 necrotic（混合），须通过 DC18 体质豁免，失败则 full damage 并 radiation sickness（每回合开始时受到 1D8 necrotic 且 max HP 降低 5，持续 3 回合或直至被 greater restoration 解除）；成功则 half damage 和 radiation sickness 1 回合；区域内所有非魔法有机物质（木材、皮革、布料）腐朽，金属生锈（-1 AC 或 -1 攻击，持续至修复）；释放后你 exhaustion 1 级
- **视觉表现**：身体发出刺眼绿光，随后爆发出蘑菇云状辐射冲击波，区域内一切有机物质瞬间枯萎。

---

### 套装九十：赛博行者（Cyber Walker）

**主题**：人机合一的战士，以科技与肉体融合超越极限。

#### 2件套 · 神经链接
> *"赛博行者不是在操控机械——他的思想和机器已经分不清谁是谁了。"*

- **触发条件**：穿戴任意 2 件 `cyber_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "investigation_bonus", mode = "flat", value = 3 }, { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2 }]`
  - 可以感知 30 尺内所有机械/构装/魔法装置的结构弱点，且对这些目标的攻击检定 +2
- **视觉表现**：一只眼睛被机械义眼替代，皮肤下有电路板纹路发光，关节处有金属接口。

#### 4件套 · 系统过载
> *"当赛博行者过载系统，他要做的不是思考——而是计算，然后让计算结果，摧毁一切。"*

- **触发条件**：穿戴全部 4 件 `cyber_walker_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "system_overload"`）：
    - 每日一次，系统过载 3 回合：获得「预测算法」——所有敌人对你进行的第一次攻击每回合自动 miss（你预知并闪避）；你的攻击无视 cover 和 half cover（计算弹道）；每回合可以进行一次「黑客入侵」——指定一个可见机械/构装敌人，DC16 智力豁免，失败则被控制 1 回合（攻击其友方或自毁受到 3D10 force）；过载结束后 1 回合内所有检定有劣势（系统重启）
- **视觉表现**：义眼闪烁数据流，周身电路光芒暴涨，动作如预判般精准，入侵时数据流射向目标。

---

### 套装九十一：创世泰坦（Creation Titan）

**主题**：创造之力的化身，以原始能量塑造与毁灭。

#### 2件套 · 造物之手
> *"创世泰坦不是在建造——他是在回忆，回忆世界诞生时的模样。"*

- **触发条件**：穿戴任意 2 件 `creation_titan_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "strength_bonus", mode = "flat", value = 2 }, { attribute_id = "constitution_bonus", mode = "flat", value = 1 }]`
  - 每日一次，可以 bonus action 创造一件临时物品（武器/护甲/工具/屏障），持续 1 分钟：武器（+1 攻击，1D8+2 force）、护甲（+2 AC）、屏障（5 尺高墙，HP 30，AC 15）
- **视觉表现**：双手散发创世之光，创造时物质从虚空中凝聚成形，如雕塑般精致。

#### 4件套 · 世界重塑
> *"当创世泰坦举起双手，世界便要重新考虑它的形状——因为旧的形状，已经不符合他的审美了。"*

- **触发条件**：穿戴全部 4 件 `creation_titan_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "world_remake"`）：
    - 每日一次，重塑 20 尺半径区域地形：选择以下一种效果——
      - **升起**：创造高地，区域内友方获得 +2 AC 和远程攻击优势，敌人移动力减半
      - **陷落**：制造深坑，区域内敌人须通过 DC18 敏捷豁免，失败则 falling 3D10 bludgeoning 并 prone，成功则移动力减半
      - **壁垒**：创造环形石墙（HP 50，AC 17），将区域分割成内外，友方在墙内 AC +2 且免疫远程攻击
    - 重塑持续 1 分钟，结束后地形缓慢恢复原状
- **视觉表现**：大地如粘土般被双手塑形，山脉升起、深渊裂开或石墙拔地而起，创世之光笼罩整个区域。

---

### 套装九十二：虚空吞噬者（Void Devourer）

**主题**：虚空的终极猎手，以吞噬存在本身为力量来源。

#### 2件套 · 饥饿之胃
> *"虚空吞噬者不是在进食——他是在让东西'从未存在过'，而这，比死亡更彻底。"*

- **触发条件**：穿戴任意 2 件 `void_devourer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_force", mode = "flat", value = 10 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 每次击杀一个生物，恢复 1D10 HP 并获得 1 层「虚空饱食」（最多 5 层，每层 +1 necrotic 伤害）
- **视觉表现**：腹部有微型黑洞漩涡，吞噬时周围光线扭曲，嘴角有黑色虚空能量溢出。

#### 4件套 · 存在抹消
> *"当虚空吞噬者张开大口，存在本身便要颤抖——因为连'存在'这个概念，都可能被他吃掉。"*

- **触发条件**：穿戴全部 4 件 `void_devourer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "void_erasure"`）：
    - 每日一次，指定一个可见敌人进行「存在抹消」：目标须通过 DC18 体质豁免，失败则受到 8D10 necrotic 并「暂时抹消」1 回合（从现实中移除，无法被攻击、无法行动、无法被治疗，但时间仍对其流逝——buff/debuff 持续计时）；成功则受到 4D10 necrotic 且 max HP 降低 10（长休恢复）；若目标被抹消期间 HP 降至 0，则彻底从存在中抹消（无法以任何方式复活，连 wish 都无法找回）
- **视觉表现**：口中张开黑洞，目标被吸入虚空中逐渐透明消失，周围现实如玻璃般碎裂又重组。

---

### 套装九十三：圣光仲裁者（Holy Arbiter）

**主题**：神圣法则的执行者，以审判之光净化一切罪恶。

#### 2件套 · 审判之眼
> *"圣光仲裁者不是在审判——他是在宣读判决书，而判决书，在罪行的那一刻便已写好。"*

- **触发条件**：穿戴任意 2 件 `holy_arbiter_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_radiant", mode = "flat", value = 15 }, { attribute_id = "insight_bonus", mode = "flat", value = 3 }]`
  - 可以感知 30 尺内所有生物的 alignment，且对 evil 生物的攻击检定 +1
- **视觉表现**：双眼被金色审判符文覆盖，周身有金色天平虚影，邪恶生物在其面前不由自主地颤抖。

#### 4件套 · 最终审判
> *"当圣光仲裁者敲响法槌，连神都要沉默——因为审判已经下达，而执行，只是形式。"*

- **触发条件**：穿戴全部 4 件 `holy_arbiter_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "armor_ac_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "final_judgment"`）：
    - 每日一次，对一个 visible evil 生物执行「最终审判」：目标须通过 DC18 智慧豁免，失败则受到 6D10 radiant + 4D10 force（神圣冲击）并「定罪」2 回合（无法使用任何 supernatural/spell-like 能力，且所有攻击检定有劣势）；成功则 half damage 和定罪 1 回合；若目标在定罪期间被击杀，其灵魂被直接送往下层位面（无法通过低于 resurrection 的法术复活）；友方在审判光芒范围内 immune frightened 和 charmed
- **视觉表现**：天空降下金色光柱，光柱中巨大法槌虚影敲下，击中时金色冲击波扩散，邪恶生物在光芒中哀嚎。

---

### 套装九十四：死亡使者（Death Herald）

**主题**：死亡的先驱，以收割与宣告引导灵魂走向终点。

#### 2件套 · 丧钟
> *"死亡使者不是在杀人——他只是在按顺序叫号，而号码，是每个人的生命。"*

- **触发条件**：穿戴任意 2 件 `death_herald_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_necrotic", mode = "flat", value = 15 }, { attribute_id = "religion_bonus", mode = "flat", value = 3 }]`
  - 可以感知 30 尺内所有生物的当前 HP 百分比，且对 HP 低于 25% 的敌人攻击检定 +2
- **视觉表现**：周身有黑色丧钟虚影，每走一步钟声低沉回响，头顶有乌鸦盘旋。

#### 4件套 · 死亡名单
> *"当死亡使者展开名单，上面的名字便要一一划去——而第一个名字，总是最大的那个。"*

- **触发条件**：穿戴全部 4 件 `death_herald_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "death_list"`）：
    - 每场战斗开始时，标记一个可见敌人为「名单首位」：你对该敌人的攻击检定 +3，且每次命中附加 2D6 necrotic；若该敌人在 3 回合内被击杀，你可以选择下一个敌人标记；若 3 回合内未击杀，标记消失且该敌人恢复 2D8 HP（「缓刑」）；每场战斗最多标记 3 个敌人；被标记且击杀的敌人无法通过 raise dead 复活（需要 resurrection 或更高级）
- **视觉表现**：展开黑色羊皮卷，上面用血字写着敌人名字，被标记敌人头顶浮现红色名字虚影，击杀时名字被血划去。

---

### 套装九十五：星辰编织者（Star Weaver）

**主题**：星辰的编织者，以星光为丝线，以命运为图案。

#### 2件套 · 星线
> *"星辰编织者不是在预测命运——她是在编织它，而你，只是她图案中的一根线。"*

- **触发条件**：穿戴任意 2 件 `star_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "insight_bonus", mode = "flat", value = 2 }]`
  - 夜间可以感知 1 里内的天体位置，且所有预言/占卜检定 +3
- **视觉表现**：手指间有星光丝线缠绕，头顶浮现微型星图，眼中映照着银河。

#### 4件套 · 命运织锦
> *"当星辰编织者展开她的织锦，连命运本身都要重新排列——因为她不满意之前的图案。"*

- **触发条件**：穿戴全部 4 件 `star_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 20 }]`
  - 战斗触发（`special_effect_id = "fate_tapestry"`）：
    - 每日一次，展开命运织锦：选择以下一种效果——
      - **重织**：让一个已发生的骰子结果重掷（攻击、豁免、伤害等），新结果取代旧结果
      - **切断**：指定一个敌人的一项 active buff/效果，立即终止（无视持续时间）
      - **编织**：为一个友方编织「命运护甲」：2 回合内，该友方受到的第一次攻击自动 miss，且第一次失败的豁免自动成功
- **视觉表现**：天空星辰连线成巨大织锦，星光丝线从天空垂下，编织或切断现实的纹理。

---

### 套装九十六：命运编织者（Fate Weaver）

**主题**：命运线的操控者，以因果之力改变过去与未来。

#### 2件套 · 因果线
> *"命运编织者不是在改变命运——她只是在整理线头，而线头，总是缠在一起的。"*

- **触发条件**：穿戴任意 2 件 `fate_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "insight_bonus", mode = "flat", value = 3 }, { attribute_id = "persuasion_bonus", mode = "flat", value = 2 }]`
  - 每日一次，可以 reaction 让一个可见敌人的一次攻击检定或豁免重掷（必须使用新结果）
- **视觉表现**：周身有无数细线连接向远方（因果线），双眼可以看到生物头顶的命运线颜色（红-厄运，金-好运）。

#### 4件套 · 因果倒置
> *"当命运编织者倒置因果， effect 便要先于 cause——而敌人，还没出拳就已经输了。"*

- **触发条件**：穿戴全部 4 件 `fate_weaver_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "spell_attack_bonus", mode = "flat", value = 1 }]`
  - 战斗触发（`special_effect_id = "causal_inversion"`）：
    - 每日一次，倒置因果：指定一个可见敌人，其下一回合的第一次行动（攻击、施法、移动等）被「预支」——该行动在本回合结束时提前发生（由命运编织者控制目标，使其攻击最近的生物或向最不利的方向移动）；随后该敌人在其下一回合无法执行该类型的行动（已被预支）；因果倒置期间，该敌人对你造成的任何伤害反弹 50% 给自身
- **视觉表现**：敌人身上因果线被扯出并反向缠绕，敌人动作变得混乱，攻击自己或友方。

---

### 套装九十七：梦境之主（Dream Lord）

**主题**：梦境世界的统治者，以睡梦操控现实。

#### 2件套 · 入梦者
> *"梦境之主不是在睡觉——他是在上班，而他的办公室，是你的梦。"*

- **触发条件**：穿戴任意 2 件 `dream_lord_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "arcana_bonus", mode = "flat", value = 3 }, { attribute_id = "deception_bonus", mode = "flat", value = 2 }]`
  - 免疫睡眠和梦境效果，且可以感知 30 尺内睡眠生物的梦境内容（可用于获取情报或施加暗示）
- **视觉表现**：双眼如星云般旋转，周身有梦幻彩色烟雾，睡眠中的生物附近出现微型梦境投影。

#### 4件套 · 噩梦降临
> *"当梦境之主降临噩梦，连清醒的人都要怀疑——自己是否还在梦中。"*

- **触发条件**：穿戴全部 4 件 `dream_lord_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "max_mana", mode = "flat", value = 25 }]`
  - 战斗触发（`special_effect_id = "dream_lord_nightmare"`）：
    - 每日一次，将 20 尺半径区域变为「梦境领域」1 分钟：领域内所有敌人每回合开始时须通过 DC17 智慧豁免，失败则陷入「半梦半醒」状态（攻击检定和感知检定有劣势，移动力减半，且 50% 概率将友方误认为敌人）；友方在领域内免疫 frightened 和 charmed，且每回合开始时可以选择恢复 1D8 HP 或恢复一个 1 级法术位；梦境之主在领域内可以 bonus action 将自身或一个友方传送到领域内任何位置（梦境跳跃）
- **视觉表现**：区域现实扭曲，天空变成梦境星空，地面如水面般波动，敌人在幻觉中攻击空气。

---

### 套装九十八：时间旅者（Time Traveler）

**主题**：时间的旅者，以时序之力穿梭于过去与未来。

#### 2件套 · 时流感知
> *"时间旅者不是在旅行——他是在翻阅一本书，而书页，是所有人的生命。"*

- **触发条件**：穿戴任意 2 件 `time_traveler_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "initiative_bonus", mode = "flat", value = 3 }, { attribute_id = "history_bonus", mode = "flat", value = 3 }]`
  - 每日一次，可以 bonus action「预见」：下一回合开始前，你可以看到所有敌人的 intended action（攻击目标、移动方向、施法选择等），并据此调整你的行动
- **视觉表现**：周身有微型时钟齿轮旋转，行动时偶尔出现未来残影（提前显示下一步动作）。

#### 4件套 · 时间回溯
> *"当时间旅者回溯时间，因果律便成了可编辑的文档——而他，拥有管理员权限。"*

- **触发条件**：穿戴全部 4 件 `time_traveler_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "movement_speed", mode = "flat", value = 15 }]`
  - 战斗触发（`special_effect_id = "time_rewind"`）：
    - 每日一次，将时间回溯至上回合开始：你和所有 30 尺内友方的 HP、位置、法术位、消耗品使用状态恢复至上回合开始时的状态（负面效果也回溯）；敌人状态不变；回溯后，你们保留对上一回合的记忆，因此本回合所有攻击检定和豁免检定 +2（预知未来）；回溯后 24 小时内无法再次使用
- **视觉表现**：周围一切倒流——伤口愈合、箭矢倒飞、脚步倒退，随后时间再次正向流动，但你们已知晓未来。

---

### 套装九十九：宇宙吞噬者（Cosmic Devourer）

**主题**：宇宙的终结者，以星辰为食物，以黑洞为胃。

#### 2件套 · 星噬
> *"宇宙吞噬者不是在毁灭——他是在进食，而宇宙，恰好是一顿大餐。"*

- **触发条件**：穿戴任意 2 件 `cosmic_devourer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "resistance_force", mode = "flat", value = 10 }, { attribute_id = "saving_throw_constitution", mode = "flat", value = 2 }]`
  - 每次击杀一个生物，恢复 2D8 HP 并获得「星层」（最多 3 层，每层 +1D6 force 伤害，所有攻击附加）
- **视觉表现**：腹部有微型星系漩涡，吞噬时星辰光芒被吸入，周身有星云缭绕。

#### 4件套 · 黑洞降生
> *"当宇宙吞噬者张开大口，连光都要弯曲——因为光知道，被吃掉之前，最好先打个招呼。"*

- **触发条件**：穿戴全部 4 件 `cosmic_devourer_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }]`
  - 战斗触发（`special_effect_id = "black_hole"`）：
    - 每日一次，在 15 尺半径区域创造微型黑洞 3 回合：区域内所有敌人每回合开始时受到 3D10 force（引力撕扯）并向中心移动 10 尺（DC18 力量豁免抵抗，失败则移动并 prone）；区域内远程攻击有劣势（光线弯曲）；黑洞结束时爆炸，5 尺半径内所有生物受到 4D10 force + 2D10 radiant（DC17 敏捷豁免 half）；黑洞期间你可以 bonus action 吸收一个被拉至中心的敌人（5 尺内）——恢复 3D8 HP 并获得 1 星层
- **视觉表现**：空间塌陷成黑色球体，周围星辰光芒被吸入，敌人被引力拉扯向中心，结束时黑洞爆发如超新星。

---

### 套装一百：创世神（Creation Divine）

**主题**：创造的本源，以神性之力重塑世界。

#### 2件套 · 神性光辉
> *"创世神不是在创造——他是在回忆，回忆世界诞生前的那片宁静。"*

- **触发条件**：穿戴任意 2 件 `creation_divine_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "armor_ac_bonus", mode = "flat", value = 2 }, { attribute_id = "saving_throw_wisdom", mode = "flat", value = 3 }]`
  - 免疫所有非传奇诅咒、疾病、毒素和恐惧效果
- **视觉表现**：周身散发神圣金色光辉，头顶有光环虚影，地面在脚下生长出鲜花。

#### 4件套 · 创世之力
> *"当创世神释放全力，世界便要回到它的起点——而这一次，他会做得更好。"*

- **触发条件**：穿戴全部 4 件 `creation_divine_set`
- **效果**：
  - `attribute_modifiers`: `[{ attribute_id = "attack_bonus", mode = "flat", value = 2 }, { attribute_id = "max_mana", mode = "flat", value = 30 }]`
  - 战斗触发（`special_effect_id = "creation_force"`）：
    - 每日一次，释放创世之力：选择以下一种效果——
      - **创生**：为所有 30 尺内友方恢复至 max HP，移除所有非 legendary 负面效果，并赋予 20 点临时 HP 和「神恩」2 回合（攻击检定 +2，AC +2，免疫 frightened 和 charmed）
      - **毁灭**：指定一个可见敌人，受到 10D10 radiant + 5D10 force（神圣毁灭），须通过 DC20 体质豁免，失败则 full damage 并「存在抹消」1 回合（同 void_devourer 的暂时抹消）；成功则 half damage 和 stunned 1 回合
      - **重塑**：改变 30 尺半径区域地形（同 creation_titan 的世界重塑，但效果翻倍且持续 2 分钟）
    - 释放后 exhaustion 2 级（创世之力对凡躯负担极大）
- **视觉表现**：天空裂开，创世之光倾泻而下，创生时金色治愈之雨，毁灭时神圣光柱贯穿天地，重塑时大地如粘土般被神手塑形。

---

*套装效果设计文档 · 套装 1–100 · 完结*
