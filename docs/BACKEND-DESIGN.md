# 归秦 · 后端设计（手游网游形态）

目标：和主流手机网游（梦幻 / 问道 / 大话一类的回合制 MMO）一样的结构——**服务端说了算，客户端只负责表现和输入**。
客户端侧的接口层已经在 `unity/Assets/Scripts/Net/`：`IGameBackend`（接口）、`MockBackend`（本地假后端，等于服务端行为的参考实现）、`WsBackend`（WebSocket 实现骨架）。

## 1. 总体架构

```
 Unity 客户端
   │  HTTPS（登录前）                    │  WSS 长连接（登录后全部玩法）
   ▼                                     ▼
 网关 / 账号服                      游戏服（按“区”部署，一个区一套）
  · 账号注册 / 登录 / 第三方登录          ├ 接入层：连接管理、鉴权、心跳、限流、编解码
  · 区服列表、公告、维护开关              ├ 场景服务：每张地图一个“实例”（单线程事件循环），管 AOI、NPC、移动
  · 版本检查、配置表 hash、热更地址        ├ 逻辑服务：角色 / 背包 / 任务 / 战斗 / 商店 / 锻造 / 宠物 / 社交 / 邮件
  · 支付回调（对接渠道）                  └ 定时任务：天气、刷怪、活动、排行结算
        │                                     │
        └──────────── MySQL / PostgreSQL（权威数据，事务）+ Redis（会话、在线表、排行榜、分布式锁）+ 消息队列（跨服务事件、日志）
```

- **一个区一套游戏服**，区之间数据隔离（和手游“XX区”一致）；账号服全局唯一。
- 游戏服内部：同一张地图的所有玩家在同一个场景实例里，**同一时刻只有一个线程改它的状态**（避免锁），跨实例通过消息传递。
- 语言不限；推荐 Go / Java / C#（.NET）/ Rust 任选，团队熟哪个用哪个。协议是 JSON 文本帧起步，量大后换 protobuf（见 §3.6）。

## 2. 权威划分（谁说了算）

| 内容 | 服务端 | 客户端 |
|---|---|---|
| 账号、角色、属性、等级、经验 | 唯一权威 | 只显示 |
| 背包 / 装备 / 强化 / 宝石 / 金币 / 掉落 | 唯一权威，所有变更走事务 | 只显示；操作 = 发请求 |
| 任务与主线进度（story beats） | 唯一权威；传送阵任务锁也在服务端判 | 显示任务、触发对话 |
| **战斗** | **整场在服务端结算**（随机数、伤害、暴击、技能、状态） | 提交指令、播放服务端下发的战斗日志 |
| 移动 | 校验：速度、碰撞（同一份 mask / walk 数据）、传送合法性 | 本地预测（点击立刻走），服务端不同意就拉回 |
| 其它玩家 / NPC 位置 | 通过 AOI 推送 | 只显示 |
| 天气 | 服务端按地图 roll（`data.weather`），推送给客户端 | 显示 |
| 画面、特效、音效、UI | — | 全部客户端 |

铁律：**客户端发来的任何数字都不可信**。客户端只能说“我想做什么”（走到哪、打哪个敌人、用哪个技能、买哪个物品），结果由服务端算。

## 3. 连接与协议

### 3.1 登录流程
1. `GET https://gw.xxx/api/config`：返回维护状态、最低客户端版本、配置表 hash、区服列表、公告。版本过低 → 强更；hash 不同 → 提示更新数据（`data.json` / `assets.json` 是客户端与服务端共用的同一份，服务端也加载它）。
2. `POST https://gw.xxx/api/login`：账号密码 / 手机验证码 / 微信 / Apple 登录 → 返回 `accountToken`（JWT，有效期数小时）。
3. 选区 → 连接 `wss://zoneN.xxx/ws`，首条消息 `auth.login{token,device,platform,clientVer,dataHash}`，服务端验签后返回角色列表和 `session`（用于断线续传）。
4. `role.enter{roleId}`：服务端装载角色，返回完整 `RoleState`，客户端进入对应地图。同账号再次登录 → 老连接收到 `push.kick` 被顶下线。

（当前骨架把 1–2 步折叠进 `auth.login` 一条消息；真实接入时在 `WsBackend.Login` 前加一步 HTTPS 即可，接口不变。）

### 3.2 帧格式
JSON 文本帧，一条 = 一个 `Envelope`：

```json
{"seq": 12, "cmd": "map.portal", "data": {"from":"village","to":"outskirts"}}
{"seq": 12, "cmd": "map.portal", "code": 0, "msg": "", "ts": 1763400000000, "data": {"ok":true,"map":"outskirts","x":120,"y":340}}
{"seq": 0,  "cmd": "push.weather", "ts": 1763400001000, "data": {"map":"village","kind":"rain","until":1763400300000}}
```

- `seq > 0`：请求，服务端用同一个 `seq` 回；`seq = 0`：服务端主动推送。
- `code = 0` 成功；非 0 失败，`msg` 是可直接弹给玩家的中文提示。码表见 `Protocol.cs` 的 `Code`。
- `ts`：服务端毫秒时间。客户端用它算 `ServerTimeOffsetMs`，天气到期、活动倒计时都用服务端时间。

### 3.3 心跳 / 超时 / 重连
- 客户端每 15 秒发 `sys.ping`；服务端 45 秒没收到任何消息就断开连接。
- 请求 8 秒无回包 → 客户端按超时处理（`Code.Timeout`）。
- **幂等**：会改状态的请求带 `seq`，服务端对每个会话缓存最近 N 条 `(seq → 回包)`，重复 `seq` 直接返回缓存结果（防止重连后重发导致重复扣钱 / 重复领奖）。
- 断线重连：保留 `session` 60 秒；重连时发 `auth.resume{session, lastSeq}`，服务端恢复角色在线状态并补发错过的推送。超过 60 秒按下线处理，战斗中的角色由服务端托管（见 §5）。
- **客户端还没实现自动重连**，目前断线只提示（`OnDisconnected`）。

### 3.4 限流与安全
- 每连接每秒请求数有上限（建议 20），超限回 `Code.Busy`，持续超限踢线。
- 所有写操作先校验：角色是否在线、是否在战斗中、目标是否存在、资源是否足够、冷却。校验失败回明确错误码，不静默忽略。
- 传输走 TLS（wss）。可选：对关键请求加签名（`HMAC(session_key, seq|cmd|data)`），防篡改重放。
- 日志：所有货币 / 物品变更写流水表（谁、何时、为什么、前后值），用来查外挂和回档。

### 3.5 命令目录

已有（客户端接口已实现，`Protocol.cs`）：

| cmd | 方向 | 说明 |
|---|---|---|
| `auth.login` | 请求 | 登录，返回角色列表 |
| `role.enter` | 请求 | 进入角色，返回 `RoleState` |
| `role.look` | 请求 | 换外观（`cls`） |
| `map.move` | 请求 | 上报行走目的地 / 到达点，服务端校验，不合法回 `ok=false` + 应回到的位置 |
| `map.portal` | 请求 | 走传送阵（服务端判 `needQuest`/`needStep`/等级） |
| `map.teleport` | 请求 | 世界地图传送（服务端判等级 / 费用） |
| `sys.ping` | 请求 | 心跳 |
| `push.kick` | 推送 | 被顶号 / 封禁 / 维护 |

战斗（R3，客户端接口 + 参考实现已写，`Battle/` 目录）：

| cmd | 方向 | 请求 → 回包 |
|---|---|---|
| `battle.start` | 请求 | `{stage}` → `{ok, msg, battle:{id,title,mode,bg,turnSeconds, units:[{id,side,row,name,sprite,h,flip,me,boss,hp,maxHp,mp,maxMp}], skills:[{id,name,target,mp,usable,note}]}}` |
| `battle.cmd` | 请求 | `{battleId, cmd:{type: skill\|defend\|flee\|auto, skill, target}}` → `{round, events:[…], over, end}` |

- 一个回合 = 一次 `battle.cmd` 请求/应答：客户端提交主角指令，服务端结算**整个回合**（所有单位按速度出手），把事件列表一次回给客户端按序播放。回合内客户端不再和服务端交互，所以延迟不影响出手节奏。
- 事件类型（`BattleEvent.t`）：`round` `act`（出手者 / 技能 / 目标）`defend` `hit`（受击者、伤害、暴击、剩余气血）`dot` `heal` `miss` `float` `mp` `status` `unstatus` `die` `revive` `maxhp` `stun` `flee`。所有数值都是最终值，客户端不做计算。
- 结算：`over=true` 时带 `end:{result: win|lose|flee, exp, coin, items, levelUp, lv, respawnMap}`，服务端已先入库（经验 / 掉落 / 升级 / 战败回城）。
- 并发：一个角色同时只能有一场战斗（`Code.InBattle`）；`battleId` 不匹配或已结束回 `Code.NoBattle`。
- 重连：战斗中断线，服务端保留战斗，重连后用 `battle.resume`（待加）取回当前战场快照；超时无指令按 `auto` 托管。
- 参考实现：`Battle/BattleEngine.cs`（纯逻辑，无 Unity 依赖，种子固定可重放；技能 / 被动 / AI 全按网页版移植）。服务端用别的语言重写时，用同一份 `data.json` 并拿这个引擎的输出做对拍。
- 还没做：道具指令（要等背包 R4）、竞技场（5 人 + 5 宠）、多人同场战斗、任务 / 玄兵塔进度的结算、装备 / 宝石 / 亲密度对属性的加成。

后续按阶段加（命名先占位）：

| 模块 | cmd |
|---|---|
| 任务 / 剧情 | `quest.list`、`quest.accept`、`story.beat`（推进剧情节拍）、`npc.talk`（服务端决定对话分支）、`push.quest` |
| 战斗（补充） | `battle.resume`（断线重连）、`battle.item`（道具）、`arena.*`（竞技场） |
| 背包 / 装备 | `bag.list`、`bag.use`、`bag.equip`、`bag.sell`、`equip.enhance`、`equip.gem` |
| 商店 / 锻造 | `shop.list`、`shop.buy`、`forge.make` |
| 宠物 | `pet.list`、`pet.battleSet`、`pet.feed`、`pet.rename` |
| 玄兵塔 / 竞技场 | `tower.enter`、`arena.match`、`arena.rank` |
| 社交 | `chat.send`（世界 / 地图 / 私聊 / 帮派）、`push.chat`、`friend.*`、`mail.*` |
| 地图同屏 | `push.map`（AOI 内玩家 / NPC 的出现、移动、消失增量） |
| 系统 | `push.weather`、`push.role`（属性变化）、`push.announce` |

### 3.6 编码演进
- 起步：JSON（调试方便，当前实现）。
- 上线前：换 protobuf（`.proto` 作为客户端 / 服务端唯一协议源，生成两端代码），`Envelope` 字段不变；可再加 zstd / deflate 压缩大包（背包列表等）。
- 命令名保持字符串，调试时好读；性能敏感的推送（`push.map`）可以用整数 id。

## 4. 地图与移动

- **坐标**：协议里一律用“地图数据坐标”（舞台像素 ÷ `scale×k`），服务端不需要知道客户端缩放。`x = -1` 表示出生点。
- **点击移动**：客户端本地 A* 立刻开走（预测），到达或改道时发 `map.move{map,from,to}`。服务端用**同一份碰撞数据**（`data/collision/*.json` 位图 + `walk/roads/blocks` 多边形，规则和 `MapRuntime.Inside` 一致）：
  - 校验 `to` 在可走区域；
  - 校验距离 ≤ 速度(230 px/s) × 距上次上报的时间 × 容差（1.3）；
  - 不合法 → 回 `ok=false` 和应回到的位置，客户端 `Stop()` 并瞬移回去。
- **AOI（视野）**：场景实例按格子（如 600×600 舞台像素）划分；玩家只收到自己九宫格内的 `push.map`。同屏玩家上限（如 50），超出的按距离裁剪。
- **NPC / 传送阵 / 敌人站位**：来自 `data.maps[id]`，服务端也加载；任务相关的显隐（头顶“!”、敌人是否已通关）由服务端随角色状态下发。
- **切图**：客户端发 `map.portal`，服务端校验后把角色从旧场景实例移到新实例，回包带新地图和落点；失败回 `ok=false + msg`（任务锁文案来自 `PortalDef.lockMsg`）。

## 5. 战斗（回合制，服务端结算）

1. 触发：点击敌人 → `battle.start{stageId}`。服务端校验敌人存在、等级 / 体力 / 任务条件，创建战斗实例，回传初始布阵（敌我单位、站位、属性快照、随机种子可选）。
2. 每回合：服务端等所有玩家提交 `battle.cmd`（30 秒倒计时，超时按“自动”托管）→ 按速度排序依次结算 → 下发 `push.battle.round{actions:[{actor,skill,targets,dmg,crit,miss,buffs,...}]}`。
3. 客户端**只播放**这些动作（动画、飘字、特效），不做任何伤害计算。
4. 结束：`push.battle.end{win,exp,gold,drops,levelUp}`，服务端先入库（经验 / 掉落 / 任务进度）再下发。
5. 断线：战斗不中止，该玩家被托管；重连后 `battle.reconnect` 拿到当前战斗快照继续。
6. 反作弊：服务端保存每场战斗的种子和指令序列，可重放校验。

玩法参数（伤害公式、成长曲线、掉率）来自 `Balance-Sheet.md` / `data.json`，服务端和客户端用同一份表，**以服务端为准**；改表后重新发版，配置表 hash 不一致的客户端被提示更新。

## 6. 数据库（概要）

| 表 | 关键字段 |
|---|---|
| `account` | id、渠道、第三方 openid、状态（正常 / 封禁）、创建时间 |
| `role` | id、account_id、zone、name、cls（职业外观）、lv、exp、gold、map、x、y、创建 / 最后登录时间 |
| `role_item` | role_id、item_id、数量、强化等级、宝石、绑定、来源 |
| `role_equip` | role_id、槽位、item 实例 id |
| `role_pet` | role_id、pet_id、等级、品质、技能、出战位 |
| `role_quest` | role_id、quest_id、状态、进度（JSON） |
| `role_story` | role_id、已完成节拍（位图 / 列表） |
| `battle_log` | id、role_id、stage、种子、指令序列、结果、时间（保留 N 天） |
| `item_flow` | role_id、变更类型、物品 / 货币、前后值、原因、时间（只增不改） |
| `mail`、`chat_log`、`rank_*` | 按需 |

Redis：在线表（role_id → 连接 / 场景）、会话、排行榜（zset）、分布式锁（防同一角色并发操作）、限流计数。

## 7. 配置表与热更

- `data/data.json`、`data/story.json`、`data/collision/*` 是**客户端与服务端共用的数据**；服务端启动时加载，客户端从包里读（`Resources/GuiQin/Data`）。
- 登录时客户端上报 `dataHash`；不一致 → 服务端回 `Code.VersionTooOld` 或“数据需更新”。
- 美术 / 素材包可以走 Addressables + CDN 热更，和玩法数据版本解耦。

## 8. 客户端分层（已落地的部分）

```
GameController（表现 + 输入）
      │ 只依赖
      ▼
IGameBackend（Net/IGameBackend.cs）──┬─ MockBackend（本地内存；无服务器时运行，等同服务端的参考实现）
                                     └─ WsBackend（WebSocket；配置 Resources/GuiQin/Config/backend.json 启用）
```

- 没有 `backend.json` → 用 `MockBackend`，进游戏就是新角色、状态不保存（退出即丢，符合“不做本地存档”）。
- 配置 `{"mode":"ws","url":"wss://zone1.example.com/ws"}` → 走 `WsBackend`。
- 新增玩法 = ① 在 `Protocol.cs` 加命令和 DTO；② 在 `IGameBackend` 加方法；③ `MockBackend` 先写个宽松实现让客户端能跑；④ `WsBackend` 加一行 `Call<T>`；⑤ 服务端实现同名命令。

## 9. 落地顺序建议

1. **S1 账号 + 角色 + 进图**：HTTPS 网关登录、`auth.login` / `role.enter`、角色表。客户端已就绪。
2. **S2 移动与切图**：`map.move` 校验（把 `MapRuntime` 的碰撞逻辑在服务端复刻一份，或服务端直接用同一份数据）、`map.portal` / `map.teleport`。
3. **S3 任务与剧情**：任务表、`npc.talk` / `story.beat`，传送阵任务锁生效。
4. **S4 战斗**：服务端战斗引擎 + `battle.*`，客户端补战斗播放（R3）。
5. **S5 背包 / 商店 / 锻造 / 宠物**（R4）。
6. **S6 多人同屏 + 聊天**：AOI、`push.map`、`chat.*`。
7. **S7 运营能力**：公告、邮件、GM 后台（封号 / 发奖 / 改表）、活动、充值（渠道 SDK）、日志与监控。
