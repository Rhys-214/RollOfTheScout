# Changelog · 更新日志

## 1.0.0

### English

- Initial public release.
- The mod is named **RollOfTheScout (童子军的命运之骰)**, GUID `com.peakmodding.RollOfTheScout`.
- 30 classes, each with benefits and drawbacks; host-only install (clients install nothing).
- Bilingual configuration (Chinese / English), selected automatically by the game language.
- Host-screen class list panel, effect toasts and overhead class prefix; optional host → client broadcast (Photon custom event 197).

### 中文

- 首个公开发布版本。
- 模组定名为 **RollOfTheScout（童子军的命运之骰）**，GUID 为 `com.peakmodding.RollOfTheScout`。
- 30 个职业，各有正负效果；仅房主安装（客机零安装）。
- 配置文件中英双语，按游戏语言自动切换。
- 主机屏幕支持职业分配面板 / 效果提示 / 头顶职业前缀；另有可选的主机→客机广播（Photon 自定义事件 197）。

## 1.0.1

### English

- Urgently fixed some issues with the mod configuration file.

### 中文

- 紧急修复了模组配置文件的一些问题。

## 1.0.2

### English

- Add a class introduction document for this mod (**ModClassIntroduction.md**, bilingual in Chinese and English).

### 中文

- 为本模组补充职业介绍文档（**ModClassIntroduction.md**，中英双语）。

## 1.0.3

### English

- Attempted to optimize the mod to some extent:
  - This initial optimization attempt includes a total of 6 optimization changes (involving `ClassLedger.cs` / `Config.cs` / `HostClassManager.cs`):
  - 1. O(1) class presence check: added a player-count index, replacing the original `HasClassInRoom` that traversed the ledger.
  - 2. Added early returns to 16 per-frame functions: if a class is not present, the entire traversal block is skipped → the number of per-frame traversals is roughly halved (exclusive classes drop directly to zero).
  - 3. Reduced the frequency of two unconsciousness state-transition checks: Fireline Rescue / Protocol Zero changed from per-frame to 0.15s sampling (about 1/9 of the cost, with imperceptible latency).
  - 4. State-transition detection no longer writes to a string dictionary every frame: writes occur only when the state changes, so steady-state writes are ≈0.
  - 5. Amortized temporary-item feeding across frames: 4 instantiation entry points were consolidated into one, and the queue processes at most 3 per frame → single-frame peak reduced from 10–30 to 3; a burst of 30 finishes within at most 0.17 seconds.
  - 6. The default class draw weight of Qu changed from 10 to 5.
  - Also eliminated: the per-frame `ParseList` allocations in the Scout's luggage item pool / Cook's food item pool, as well as the per-frame service lookup + traversal in `ApplyDaihaoAvatar`.

### 中文

- 尝试对模组进行了一定优化：
  - 本次初步优化尝试总计6项优化改动（涉及`ClassLedger.cs` /`Config.cs` /`HostClassManager.cs` ）：
  - 1. 职业在场判定 O(1) ：新增人数索引，替掉原来遍历账本的`HasClassInRoom` 。
  - 2. 16 个每帧函数加提前返回 ：职业不在场就整块跳过遍历 → 每帧遍历次数约减半（独占职业直接归零）。
  - 3. 两处昏迷跳变检测降频 ：火线救援 / 零号守则由每帧 → 0.15s 采样（约 1/9 开销，延迟不可感知）。
  - 4. 跳变检测不再每帧写字符串字典 ：仅状态变化时写，稳态≈0 次。
  - 5. 临时物品喂食摊帧 ：4 个实例化入口收敛为一处，队列 每帧最多 3 次 → 单帧峰值 10~30 → 3 ，30 次爆发最迟 0.17 秒发完。
  - 6. 区的默认职业抽取权重 10 → 5 。
  - 顺带消除：童军行李物品池 / 炊事员食物物品池的 每帧`ParseList` 分配 ，以及`ApplyDaihaoAvatar` 的每帧服务查找+遍历。


## 1.0.4

### English

- **Fixed** Astronaut 【NASA】: the repeated, abnormal stacking of the extra bonus for cobweb / flytrap / curse. The detection logic has been improved: 
    - Root cause: The host reads the state value synchronized back from the remote character, which is subject to delay. The original implementation appended +2.5 and then wrote the baseline as "current value + 2.5"; if the next round still read the old value that had not yet been synchronized, the baseline would be lowered, and when the synchronization arrived it would be recognized as "yet another new increase" → repeated appends every round.
    - Each of these three statuses now enters its own cooldown after the extra bonus triggers, during which it is neither detected nor increased again. 
    - Accordingly, a new config entry `[Astronaut] nasaExtraIncreaseCooldown` has been added, default 1.0 (seconds).
- **Fixed** Leader 【Morale】: the curse and petrify refund applied to affected players when the leader revives could fail to take full effect. The refund logic has been strengthened: 
    - Root cause: The old implementation read the current value only once at "the moment the Leader revives" to calculate the difference. If the +10 issued when the Leader died had not yet been synchronized back to the host, the difference would be calculated as 0 (or only partially calculated), this rollback would be skipped, and the penalty would remain permanently on the player, accumulating over multiple times into abnormal curse values. This issue is unrelated to the frame-amortization optimization—[Morale] is issued directly via ApplyStatus and does not pass through the temporary-item feeding queue; the real cause is the timing of asynchronous state synchronization.
    - When the leader revives and triggers the negative-status removal, the amount to refund is now recorded and repeatedly corrected until confirmed complete.
- **New** **Emote reclass mechanic** (optional, **disabled by default**): 
    - Performing the 【No No】 emote (`A_Scout_Emote_Nono`) immediately reclasses the player once (same effect as consuming the reclass item), and shares the reclass count with item-based reclass. 
    - If this emote conflicts with an emote forcibly played by a class effect, the class effect takes priority and no reclass is triggered. 
    - New config entries: `[General] reclassEmoteEnabled` (default false) and `[General] reclassEmote` (default No No).

### 中文

- **修复** 太空人【我去，老美的NASA这么厉害】的蛛网/捕蝇草/诅咒值额外增加效果的异常连加问题，优化了判定机制：
    - 根因 ：主机读到的是远端角色同步回来的状态值，存在延迟。原实现追加 +2.5 后把基线写成「当前值+2.5」；下一轮若仍读到尚未同步的旧值，基线就被下调，等同步到达时又被识别成「又一笔新增加」→ 每轮反复追加。
    - 现在这三种状态在触发额外增加后会各自进入冷却，冷却期内不再检测、也不再追加。
    - 相应地，新增配置项 `[太空人] NASA-额外增加-冷却`，默认值1.0（秒）。
- **修复** 领袖【士气】在领袖复活时，对受影响玩家的诅咒与石化回退未能完全生效的问题，加强了回退逻辑：
    - 根因 ：旧实现只在「领袖复活那一刻」读一次当前值算差额。若领袖死亡时下发的 +10 尚未同步回主机，差额会算成 0（或只算出一部分），这次回退被跳过，惩罚就永久留在玩家身上，多次累积出现异常诅咒值。该问题与摊帧优化无关 —— 【士气】走`ApplyStatus` 直接下发，不经过临时物品喂食队列；真正原因是异步状态同步的时序。
    - 现在领袖复活触发负面消除效果时，会登记应回退量并反复校正（每0.6秒；6秒超时告警），直至确认完成。
- **新增** **表情转职机制**（可选，**默认禁用**）：
    - 玩家做出【不行不行】表情（`A_Scout_Emote_Nono`）后会立即转职一次（与食用转职物品一致），并与物品转职共用转职次数。
    - 若转职表情与职业效果强制播放的表情重复冲突，则以职业效果优先，不会触发转职。
    - 新增配置项 `[通用] 转职-表情启用`（默认 false）与 `[通用] 转职-表情`（默认 不行不行）。

## 1.0.5

### English

- **Fixed** Langwang 【Premium Berry】: the "purple kingberry" was mistakenly written as the "yellow kingberry", and the internal-test change had not been fully applied. 
    - All occurrences have now been corrected to the purple kingberry (`Kingberry Purple`); the config entries have been changed to `[Langwang] premiumBerryPurpleKingberryItem` / `premiumBerryPurpleKingberryWeight`, and `[Langwang] kingberryItems` / `[Cook] logisticsFoodPool` no longer contain `Kingberry Yellow`.
- **Fixed** MilesSoul 【Seal Power】: when a client MilesSoul player used Scout gem items repeatedly at high frequency, the petrify value could be deducted extra or even reduced to zero. 
    - Root cause: lost updates caused by absolute write-back combined with asynchronous synchronization** — a client's `petrifyAmount` is asynchronously synced back to the host by the **owner client**. The original implementation accumulated each evaluated petrification reduction into a pending queue, then after a 0.35s delay wrote it back using `HostApi.SetPetrify` (`RPC_SyncPetrify`, an **absolute assignment**):
        1. Under high-frequency use, the `cur` value read by the host lags behind the client's true value;
        2. After the pending queue has accumulated multiple -5s, it writes back absolutely using the stale `cur - pending`;
        3. This absolute assignment also overwrites the in-flight +7 increment that has already been used but not yet synced to the host → **an extra deduction**;
        4. Repeating this cycle can drive petrification down to 0.
    - The absolute write-back for the petrify reduction now waits until the petrify value has been **quiet for 0.75 seconds** (deferred by at most 4 seconds during continuous use), so it no longer overwrites in-flight increases that have not yet been synced to the host; the detection threshold is now strictly greater than 2.5.
- **New** The number of Scoutmasters spawned by MilesSoul 【Soul Hindrance】 present on the field at the same time is now capped at 4. 
    - This cap is hard-coded and has no config entry.

### 中文

- **修复** 榔王【精品大果】：“紫荔莓”误写为“黄荔莓”，内测修改未完全更新的问题。
    - 现已全部修正为紫荔莓（`Kingberry Purple`），配置项改为 `[榔王] 精品大果-紫荔莓物品` / `精品大果-紫荔莓权重`，`荔莓物品` 与 `后勤保障-食物池` 不再包含 `Kingberry Yellow`。
- **修复** 残魂【封印之力】：客机残魂连续高频使用童军宝石道具时，石化值可能被额外扣除甚至清零的问题。
    - 根因：绝对写回+异步同步的丢更新——客机的 `petrifyAmount` 由**属主客户端**异步同步回主机。原实现把每次判定的减石化量累积到待办，延迟 0.35s 后用 `HostApi.SetPetrify`（`RPC_SyncPetrify`，**绝对赋值**）写回：
        1. 高频使用时，主机读到的 `cur` 落后于客机真实值；
        2. 待办累积了多次 -5 后，按过期的 `cur - pending` 绝对写回；
        3. 该绝对赋值把「已经使用、但尚未同步到主机」的在途 +7 增量一并覆盖 → **额外扣除**；
        4. 反复循环即可把石化扣到 0。
    - 减石化的绝对写回现在会等待石化值**安静 0.75秒**后执行（持续使用时最多推迟4秒），不再覆盖尚未同步到主机的在途增量；判定阈值改为严格大于2.5。
- **新增** 残魂【众魂阻挠】效果生成的童军领队 的 场上存在数量上限，固定为4。
    - 此项为硬编码，无配置项。

## 1.0.6

### English

- **New** **Class introduction panel**: while the class-assignment panel is shown (default key: Tab), an extra panel now appears right below it showing **your own class's** keywords and a short introduction, so players can understand their class without opening the class document. The text follows the game language (Chinese / English). This is available on the host's screen, in the main mod's client mode, and in the optional client display addon. Only the local player's class is shown, not the whole class list.
- **New** **Class intro compatibility**: the host now also broadcasts the class **keywords** and **intro** tables (Photon custom event 197, kinds 4 / 5, throttled to "class set changed" or once per 30 s). A client addon or older local copy that does not know a class added by a newer main mod now falls back to the host-provided text — so **updating the main mod no longer requires updating the client display addon** to see the new class's introduction. Local text is still preferred, so each client keeps its own language for known classes.
- **Fixed** Firefighter 【Fireline Rescue】: a player carried by the firefighter could stay "attached" to the carrier after waking up and could not climb; the same happened again after death and revive.
    - Root cause: in vanilla, `CharacterCarrying.Update` drops the carried player **only on the carrier's owning client**, while `RPCA_Die` / `ReviveCharacter` / `RPCA_ReviveAtPosition` never clear `isCarried` / `carrier`. If that single `RPCA_Drop` is missed, the victim keeps `carrier` forever; `CharacterCarrying.FixedUpdate` then applies a force of 500 toward the carrier every frame (looks like being stuck to the carrier, and climbing is impaired). Death and revive reuse the same Character object, so the state survives them. This mod makes that moment common: 【Fireline Rescue】 teleports the firefighter right onto the fainted player, and 【Warm Embrace】 heals the victim **on the carrier's back**, so the victim often wakes up exactly there — the very instants when vanilla would drop them.
    - The host now broadcasts the vanilla `RPCA_Drop` (`RpcTarget.All`) whenever a drop was due but did not happen, so players who installed nothing are fixed as well.
    - Also hardened in 1.0.6: the victim-side cleanup now runs for the **host's own character** too (previously local characters were skipped entirely, so a host carried by a client could stay glued forever); the watchdog now requires the inconsistent state to persist for 0.5 s before acting, so an in-flight `RPCA_StartCarry` / `RPCA_PassOut` (sent by different clients, with no cross-sender ordering guarantee) is never undone by mistake.
- **Fixed** Firefighter 【Fireline Rescue】: the "cannot trigger while carrying a player" rule is now also enforced at execution time. Previously only the scheduling step checked it, so a firefighter who picked a player up during the 3 s delay was still teleported — dragging the carried player along through the per-frame carry force.
- **Fixed** MilesSoul 【Cursed Soul】: a client MilesSoul could start with **15** curse instead of the intended **20** (15 from 【Cursed Soul】 plus 5 from 【Soul Hindrance】 on entering the level).
    - Root cause: the initial 15 and the per-level 5 were sent through **different channels**. The initial value used an absolute write with idempotent corrections; the +5 used a relative increment. A relative increment can be silently dropped by the vanilla `AddStatus` guards while a client is still initialising, and the correction loop only knew about the 15 — so it could also overwrite the +5 back to 15. In addition, the protection baseline was recorded from a stale host-side copy of a remote client (remote status changes made through the RPC path are not pushed back to the host by vanilla), producing baseline values like 17.5.
    - Fix: the initial curse and this level's +5 are now **merged into one absolute, idempotent write** (`15 + 5 = 20`), which the existing correction loop re-asserts. 【Soul Hindrance】 now records the **intended** baseline (`baseline + 5`) instead of reading back a possibly half-synced value, and the same-frame initial grant is skipped by the new-level handler so it can never become 25.
- **Improved** **Performance / bandwidth** (no gameplay change):
    - The class panel text is cached by a `(ViewID, class, nickname)` signature and the TMP panel update is version-gated — holding the panel key no longer rebuilds every line (and re-lays out TMP) every frame.
    - Class name / keyword / intro tables are no longer re-sent with every 2 s snapshot; they are sent only when the set of classes changes or every 30 s (late joiners still receive them). This removes roughly 4-5 kbit/s of redundant reliable traffic per client in a full room.
    - The carry-state watchdog reads the vanilla `CharacterCarrying.character` field directly instead of calling `GetComponent<Character>()` for **every character every frame**.
    - The overhead name prefix caches its per-name state component (previously `GetComponent` for every player every frame).
    - MilesSoul curse protection now throttles repeat restores (2.5 s fast retries, then every 20 s) and logs sparsely; a stale host-side copy previously caused two RPCs **and** one log line per second, indefinitely.
    - Observed in the tested session: a third-party mod (`AlwaysDisplayPlayerName`) emitted ~79,000 error lines, which is a major external stutter source; this mod emitted ~3,700 log lines in total.
### 中文

- **新增** **职业介绍板块**：按键显示职业分配面板时（默认 Tab），面板下方会额外显示**本人职业**的关键词与简介，玩家不必翻阅职业文档就能快速了解自己的职业。文本随游戏语言（中文 / 英文）。主机屏幕、主模组客机模式、以及可选的客机显示附属模组均支持；只显示本机玩家自己的职业，不列出全部职业。
- **新增** **职业介绍兼容**：主机现在额外广播职业**关键词**与**简介**表（Photon 自定义事件 197，Kind 4 / 5；仅在「职业集合变化」或每 30 秒发送一次）。当主模组新增职业、而客机上的附属模组 / 旧版本地文案表还不认识该职业时，会**退回使用主机广播的文本** —— 因此**主模组更新后不必同步更新附属模组**也能看到新职业的介绍。已知职业仍优先使用本地文案，各客户端保持自己的语言。
- **修复** 救火队员【火线救援】：被救火队员背起的玩家苏醒后仍"吸附"在背人者身上、无法攀爬；死亡复活后再次吸附。
    - 根因：原版 `CharacterCarrying.Update` 的自动放下**只在背人者的拥有者客户端执行**，而 `RPCA_Die` / `ReviveCharacter` / `RPCA_ReviveAtPosition` 都不会清理 `isCarried` / `carrier`。一旦那一次 `RPCA_Drop` 没有执行，被背者就永久保留 `carrier`；`CharacterCarrying.FixedUpdate` 随后每帧朝背人者施加 500 的力（表现为吸附、无法攀爬），且死亡复活沿用同一个 Character 对象，状态不会清零。本模组的【火线救援】恰好让该时机变得高频：先把救火队员传送到昏迷者身上，再由【温暖臂膀】在背上治疗促使受害者在背上苏醒 —— 苏醒正是原版唯一会触发放下的时刻。
    - 现在改由主机在"本该放下却没放下"时代为广播原版 `RPCA_Drop`（`RpcTarget.All`），因此未安装任何模组的玩家也能被修复。
    - 1.0.6 进一步加固：被背者侧的清理现在对**主机自己的角色**同样执行（此前完全跳过本地角色，导致主机被客机背起时同样可能永久吸附）；并改为"异常状态持续 0.5 秒"才动手，避免与正在进行的 `RPCA_StartCarry` / `RPCA_PassOut`（由不同客户端发送，跨发送端无顺序保证）抢跑、误拆正常的背人。
- **修复** 救火队员【火线救援】：在"自身背着玩家时无法触发"这一限制之外，执行阶段也会再检查一次。此前只有登记阶段检查，若救火队员在 3 秒延迟内背起了别的玩家，仍会被强行传送 —— 被背者会因每帧的背人追力被一起拖走。
- **修复** 残魂【受诅残魂】：客机的残魂开局诅咒可能是 **15** 而不是应有的 **20**（【受诅残魂】15 + 【众魂阻挠】本层 +5）。
    - 根因：初始 15 与每层 +5 走了**两条不同通道** —— 初始值用「绝对赋值 + 幂等校正」，+5 用「相对增量」。相对增量在客机尚未初始化完成时会被原版 `AddStatus` 的守卫静默丢弃；而校正循环只知道目标 15，甚至会把已经加上去的 +5 覆盖回 15。另外，诅咒保护基准是**从主机侧读到的远端副本**记录的（原版在 RPC 路径下不会把主机下发的状态变化回推给主机），会记录出 17.5 这类"只加了一半"的值。
    - 修复：初始诅咒与本层 +5 现在**合并为一次绝对、幂等的写入**（15 + 5 = 20），由既有校正循环反复校正；【众魂阻挠】改为直接记录**期望基准**（基准 + 5），不再读回可能只同步了一半的值；开局同帧的初始发放会登记并让新关卡处理跳过，因此也不会变成 25。
- **优化** **性能 / 带宽**（不改变任何职业效果）：
    - 职业分配面板文本按 `(ViewID, 职业, 昵称)` 签名缓存，TMP 面板按版本号跳过重排 —— 按住面板键时不再每帧重建每一行。
    - 职业名 / 关键词 / 简介表不再随每 2 秒快照重发，只在职业集合变化或每 30 秒补发一次（中途加入者仍能收到）；满员房约占去掉每客户端 4~5 kbit/s 的冗余可靠流量。
    - 背人状态自愈补丁改为直接读取原版 `CharacterCarrying.character` 字段，不再对**每帧每个角色**调用 `GetComponent<Character>()`。
    - 头顶职业前缀的状态组件改为缓存（此前每帧每个玩家都调用一次 `GetComponent`）。
    - 残魂诅咒保护改为节流重试（前几次 2.5 秒、之后 20 秒）并稀疏打日志；此前主机副本滞后会导致**每秒 2 条 RPC + 1 条日志**且无限持续。
    - 本次实测日志中：第三方模组 `AlwaysDisplayPlayerName` 产生了约 **79000** 条 Error 行，是明显的卡顿外部来源；本模组自身日志总量约 **3700** 行。

## 1.0.7

### English

- **Fixed** MilesSoul 【Seal Power】 / 【Scout's Power】: picking up the **4th** Scout gem only granted that gem's own first-pickup curse reduction and never triggered 【Scout's Power】.
    - Root cause: the "already picked up" set was **shared by the whole room** and keyed only by gem name. If another player (or the same player earlier) had already picked that gem, `Add` returned false and the handler **returned before** the "all four collected" check — so a client MilesSoul could never complete the set.
    - Fix: first pickup and "all four" are now tracked **per player** (`player id → gem set`), while a separate room-wide set is kept only for the 【Rule Zero】 unlock condition. The "collect all four" bonus now fires on the 4th gem as intended.
- **Changed** 【Scout's Power】 additional curse reduction default **5 → 10**, and it now shows a bilingual notice (`MilesSoul … has collected all Scout Gems and received the 【Scout's Power】 inspiration. / 残魂 … 已集齐童军宝石，获得【童军的力量】激励。`).
- **New** Dancer 【Frenzy Dancer】 and Daihao 【Slippery Feet】 now show a **targeted** warning 10 seconds before each forced backflip (`！即将强制后空翻！/ ! Forced backflip incoming !`). Only the player of that class receives it (new targeted display event; other clients ignore it).
- **Fixed** The class panel could flood itself with the `[职业分配 / Class List]` title line while the key was held, pushing the player rows off screen.
    - Root cause: the title was inserted into the **cached** line list (`lines.Insert(0, title)`) on every GUI frame, so the cache grew by one title per frame. The title is now composed into a separate list without mutating the cache.
- **Changed** **Configuration is now permanently bilingual** (no longer generated per game language):
    - Section names are `English中文` (e.g. `Explorer先驱`, `General通用`);
    - keys are `English中文键名` where the English part is split to match the Chinese segments and capitalized (e.g. `Hypermetabolism-HungerIntervalSeconds高代谢-饥饿间隔`);
    - comments are one English line followed by one Chinese line.
    - `职业显示-模式` values are now **1 / 2 / 3** (1 = hold, 2 = press for 5 s, 3 = toggle); `转职-表情` defaults to the raw animation name `A_Scout_Emote_Nono`.
    - Existing config files are **backed up automatically** (`*.cfg.old-<timestamp>`) and regenerated on the first launch after this update — no manual deletion needed.
- **Changed** Bilingual in-game display:
    - class panel rows now read `Name：EnglishClass · 中文职业` (e.g. `Rhys：Qu · 区`);
    - class effect notices show one English line followed by one Chinese line;
    - the class intro panel now shows `Keywords:` + `【English intro】` + `关键词：` + `【中文简介】` together.
- **Changed** The overhead class prefix is now **disabled by default** in both the main mod and the client addon.
    - Reason: it runs per frame for every player; if another overhead-name mod is installed (e.g. `AlwaysDisplayPlayerName`), both rewrite the name text every frame and the resulting TMP rebuilds cause visible frame drops.
    - Optimisations (still apply when enabled): the per-name state component is cached (no per-frame `GetComponent`), currently hidden names are skipped, and class names are returned from a cache (no per-frame string building).
- **Changed** Config key names now mirror the Chinese key's hyphen segments with capitalized English parts: `Hypermetabolism-HungerIntervalSeconds高代谢-饥饿间隔` (previously `hypermetabolismHungerIntervalSeconds高代谢-饥饿间隔`). Existing config files are backed up and regenerated automatically (the config revision was bumped).
- **Changed** On-screen overlay text is no longer bold, and a **thinner sans-serif font** is preferred when the game provides one (falling back to the original font otherwise). English lines are noticeably clearer; IMGUI fallback titles are normal weight too.


### 中文

- **修复** 残魂【封印之力】/【童军的力量】：拾取**第 4 颗**童军宝石时，只触发该宝石自身的「首次拾取减诅咒」，始终不触发【童军的力量】的问题。
    - 根因：「已首拾」集合是**全场共用**、且只按宝石名记录。若别的玩家（或自己此前）已经拾过这颗宝石，`Add` 返回 false，处理函数在「集齐四种」判定**之前就 return** —— 因此客机残魂永远凑不齐。
    - 修复：首拾与「集齐四种」改为**按玩家**记录（玩家标识 → 宝石集合）；另保留一个全场集合，仅用于【零号守则】的解锁前置。第 4 颗宝石现在会正常触发【童军的力量】。
- **调整** 【童军的力量】额外减诅咒默认值 **5 → 10**，并新增中英双语提示（`MilesSoul … has collected all Scout Gems and received the 【Scout's Power】 inspiration. / 残魂 … 已集齐童军宝石，获得【童军的力量】激励。`）。
- **新增** 舞王【狂热舞者】与待皓【脚滑】在每次强制后空翻**前 10 秒**发出**定向**提示（`！即将强制后空翻！/ ! Forced backflip incoming !`）。只有对应职业的玩家本人会收到（新增定向显示事件，其余客机收到即忽略）。
- **修复** 按住显示键时，职业面板会被 `[职业分配 / Class List]` 标题刷屏、把玩家分配信息挤出屏幕的问题。
    - 根因：标题被 `lines.Insert(0, title)` 插进了**缓存列表**，每个 GUI 帧插一次 → 缓存每帧长一行。现改为「另建显示列表」拼接标题，不再修改缓存。
- **调整** **配置文件改为固定中英双语**（不再按游戏语言生成）：
    - 板块名为「英文+中文」（如 `Explorer先驱`、`General通用`）；
    - 键名为「英文按中文分段、每段首字母大写 + 中文」（如 `Hypermetabolism-HungerIntervalSeconds高代谢-饥饿间隔`）；
    - 注释为「英文一行 + 中文一行」。
    - `职业显示-模式` 取值改为 **1 / 2 / 3**（1=按住、2=按下显示 5 秒、3=按下切换）；`转职-表情` 默认 `A_Scout_Emote_Nono`。
    - 旧配置文件会在更新后首次启动时**自动备份**（`*.cfg.old-时间戳`）并重新生成，无需手动删除。
- **调整** 游戏内显示改为中英双语：
    - 职业面板每行改为 `玩家名：EnglishClass · 中文职业`（如 `Rhys：Qu · 区`）；
    - 职业效果提示改为「英文一行 + 中文一行」；
    - 职业介绍板块改为 `Keywords:` + `【英文简介】` + `关键词：` + `【中文简介】` 同时显示。
- **调整** **头顶职业前缀**在主模组与附属模组中**默认关闭**。
    - 原因：它每帧、每个玩家都会执行；若同时装有其它的头顶名称模组（如 `AlwaysDisplayPlayerName`），双方每帧互相改写文本，TMP 重建会造成明显掉帧。
    - 优化（开启时仍生效）：状态组件改为缓存（不再每帧 `GetComponent`）、跳过当前隐藏的名字、职业名走缓存（不再每帧拼字符串）。
- **调整** 配置键名的英文部分改为**与中文键相同的分段**、每段首字母大写：`Hypermetabolism-HungerIntervalSeconds高代谢-饥饿间隔`（此前为 `hypermetabolismHungerIntervalSeconds高代谢-饥饿间隔`）。**旧配置会在首次启动时自动备份并重建**（配置版本标记已递增）。
- **调整** 屏幕叠加层的文字**不再加粗**，并在游戏提供更细的无衬线字体时**优先使用**（没有则回退原字体）。英文行更清晰易读；IMGUI 回退路径的标题也改为常规字重。

## 1.0.8

### English

- **Fixed** **Emote reclass** did not work for anyone (host or client) even with the option enabled; only the consumable-item reclass worked.
    - Root cause: "class effects force an emote" was implemented with a **global suppression counter**. The host increments it while broadcasting a forced emote and decrements it afterwards; once that counter is left above zero for any reason, **every** player's emote reclass is silently blocked for the rest of the run. The counter could also block real emotes if a forced-emote callback was delivered later than the decrement.
    - Fix: suppression is now an **exact match** on `(target character ViewID, emote name)` with the previous values restored in `finally`, so no suppression state can leak. Emote names are compared after normalisation (prefix, case, underscore and `NoNo` / `Nope` aliases are all treated as the same emote).
    - Diagnostics: a new one-time log per observed emote prints the emote name **and** whether it matches the configured reclass emote; a second log at the start of each run prints the **effective** `reclassEmoteEnabled` / `reclassEmote` / `reclassCount` values, so a stale config can be spotted immediately.
- **Fixed** **Class intro could show another class's description** (usually after a reclass: the new class name in the title was correct, but the description was **M's**).
    - Root cause: `ClassType.M = 8` and `ClassType.Savage = 9`, but the intro table had **case 8 and case 9 swapped**. A Savage (9) therefore rendered M's description (and vice versa). Fixed in both the main mod and the client addon.
- **Fixed** Leader 【Divine Inspiration】 always granted the same mystical item (e.g. Ancient Idol) in multiplayer instead of a random item from the configured pool, while single-player worked.
    - The draw used the **global `UnityEngine.Random`**, which is shared with the game and every other mod. The pool parser also only recognised half-width commas — a full-width comma `，` turned the whole value into one unknown name, and an all-invalid pool silently returned "no item". The draw is now a mod-private `System.Random` seeded per process; the parser accepts `,` `，` `、` `;` and newlines; and an all-invalid pool falls back to the vanilla `Mystical`-tag random with a one-time warning instead of granting nothing. The resolved pool and the item actually picked are logged once per config change for future diagnosis.
- **Fixed** Incompatibility with **Piggyback** (`nakazora.peak-piggyback`): in a multiplayer room nobody could carry an alive, non-passed-out player.
    - Root cause: the carry watchdog treated "the carried player is not passed out" as a stale state and broadcast the vanilla `RPCA_Drop` about 0.5 s after the carry starts — which is exactly what Piggyback does by design.
    - Fix: the watchdog now records whether the carried player **was ever downed during this carry** and only drops an awake player if they had been downed in the same carry (the original "woke up while carried" fix). A pure awake carry is left untouched. When Piggyback is detected (assembly name / plugin GUID / its config file), the watchdog additionally never drops on "woke up"; only death, the carrier being downed, or the carrier disappearing still triggers a cleanup.
- **Performance** (effects unchanged) **Emote handling is now allocation-free in steady state**: dance-emote names and the reclass-emote config are parsed once per config change, the reclass match uses a fast ordinal comparison, 【Dance Youth】 checks its cooldown before scanning players, the nearby-player list is no longer allocated, and rooms without a Dancer skip repeated emotes entirely. This matters because a Dancer spams emotes and 【Cheer】 turns one emote into N forced emotes; the diagnostic logs still fire only once per distinct emote name.
- **Changed** **The class intro panel moved from below the class list to the top-left corner**, so it can no longer be pushed off-screen when a large lobby makes the class panel very tall. While it is visible, the top-left notifications (toasts) are temporarily shifted below it to avoid overlap, then return to the top-left as soon as the panel closes. The client addon uses the same layout.

### 中文

- **修复** 即使开启了表情转职，**所有玩家**（主机与客机）做「不行不行」表情都无法转职，只有食用转职物品可以。
    - 根因：「职业效果强制播放表情」用的是**全局抑制计数**：主机广播强制表情时 +1、播完 -1；一旦该计数因任何原因没有减回 0，本局内**所有人**的表情转职都会被静默阻止。此外若强制表情的回调晚于减计数到达，也会误伤玩家自己做的表情。
    - 修复：抑制改为**精确匹配 `(目标角色 ViewID, 表情名)`**，并在 `finally` 中恢复前后值，不可能残留抑制状态；表情名比较前先归一化（前缀、大小写、下划线，以及 `NoNo` / `Nope` 等别名都视为同一表情）。
    - 诊断：新增「每个新表情只打印一次」的日志，直接标出该表情名**是否匹配**配置的转职表情；每局开始时再打印一次**实际生效**的 `转职-表情启用` / `转职-表情` / `转职-次数`，配置没读对可一眼看出。
- **修复** **职业简介板块有时显示成其他职业的简介**（多数发生在转职后：新职业标题正确，简介却显示成 **M** 的简介）。
    - 根因：`ClassType.M = 8`、`ClassType.Savage = 9`，但简介表的 **case 8 / case 9 写反了** —— 野人（9）因此显示 M 的简介（反之亦然）。主模组与附属模组均已修正。
- **修复** 领袖【神启】在联机中额外神秘物品**固定为同一件**（如古老玩偶），而非从配置物品池随机；单人离线却正常。
    - 抽取原来使用**全局 `UnityEngine.Random`**，与游戏及其它模组共用同一状态；物品池解析也只认半角逗号 —— 中文输入法的全角逗号「，」会把整串当成一个物品名，全部无法解析时又静默返回「不发放」。现改为模组私有的 `System.Random`（每进程独立种子）；解析同时接受 `,` `，` `、` `;` 与换行；全部无效时退回「原版 Mystical 标签」随机并只警告一次，不再一件都不发。解析出的物品池与实际抽中物品会在配置变化后打印一次，便于后续排查。
- **修复** 与 **Piggyback**（`nakazora.peak-piggyback`）不兼容：联机房间内所有玩家都无法背起**未昏迷的存活玩家**。
    - 根因：背人自愈把「被背者未昏迷」一律当作异常，背起约 0.5 秒后由主机广播原版 `RPCA_Drop` 强制拆散 —— 而这正是 Piggyback 的设计功能（允许背起清醒玩家）。
    - 修复：背人自愈现在记录「**本次背人期间被背者是否曾昏迷 / 死亡**」，只有经历过昏迷的被背者苏醒后才代为放下（保留原「背上苏醒仍吸附」的修复）；全程清醒的背人不再干预。检测到 Piggyback（程序集名 / 插件 GUID / 其配置文件任一命中）时，连「昏迷后苏醒」也不再拆散，只在被背者死亡、背人者倒地、背人者消失时清理。
- **性能**（效果不变）**表情处理热路径改为稳态零分配**：跳舞动作名与转职表情配置只在配置变化时解析一次；转职匹配走无分配的快路径；【舞动青春】把冷却检查提前到扫描玩家之前；不再为「全场欢呼」分配半径内角色列表；房间内没有舞王时重复表情直接丢弃。舞王会高频连续做表情、且【全场欢呼】会把一条表情放大成 N 条，因此这条优化对实际帧率有意义。诊断日志仍只在每个**不同**表情名首次出现时打印一次。
- **调整** **职业介绍板块从职业分配面板下方移至屏幕左上角**：人数较多、右上角面板很长时，介绍板块不再被挤出屏幕。该板块显示期间，原本在左上角的提示信息会**临时下移到它下方**，避免重叠；面板关闭后提示立即回到左上角。附属模组采用相同布局。

## 1.0.9

### English

- **Fixed** MilesSoul first-pickup curse reduction could be incomplete or later undone on clients.
    - Root cause: the host-issued reduction reaches the owner before the updated curse value is synchronized back to the host. The curse-protection logic could accept the host's stale pre-reduction value as its baseline during the allow window, and consecutive reductions could also be calculated from that old value; the later protection pass could then restore the wrong amount. An unfinished initial-curse retry could also write its old absolute target over the pickup reduction.
    - The mod now keeps an expected reduction target until the synchronized value confirms it, calculates consecutive reductions from the expected target rather than the stale copy, and shifts that target if a genuine curse increase arrives meanwhile. Stale copies can no longer raise the baseline or trigger a restoration while confirmation is pending; any pending initial-curse target is lowered by the same amount.
- **New** Reliable MilesSoul gem settlement for clients running either the **main mod** or the **client addon** (addon v1.0.6+; the other client need only have one of them enabled).
    - Root cause of the previous high-frequency-use failures: Scout gem actions execute on the owner's client, while the host used a separate petrify-increase heuristic and delayed absolute writebacks for clients. A writeback based on the host's delayed copy could overwrite petrify increases still in transit, causing extra reduction or even a complete clear.
    - Both mods now compile the same local detection and settlement source. The host synchronizes the effective MilesSoul configuration, the owner client applies hunger / petrify / extra-stamina changes directly through the original local paths, and usage reports are sent back only for 【Hindrance of the Souls】 counts. The host skips duplicate effect settlement for clients that completed the capability handshake.
    - Photon event 197 kinds 7 / 8 / 9 / 10 carry the targeted hello, configuration, usage report and configuration ACK. The host switches to local-client settlement only after the ACK, so uses during the handshake remain covered by the host fallback. This gameplay sync is independent of the class-display sync option; older clients without either mod remain on the host fallback.
- **Changed** For clients without either mod, 【Scout's Perseverance】【Scout's Initiative】【Scout's Hospitality】【Scout's Ambition】 now have a **1.5 s trigger cooldown per player + gem**. Using that gem during its cooldown does not trigger the class effect (including petrify reduction) or count toward the gem-use tally. Reliable RPCs, feeding and the petrify heuristic all pass through the same guard, so none can bypass it. The existing config entry `sealPowerClientDetectCooldown` / `封印之力-客机判定冷却` now defaults to 1.5.
- **Changed** Astronaut 【Wow, NASA Really Is Great】 no longer adds extra curse. Its status snapshot and cooldown arrays now contain only cobweb and flytrap; the existing increase-delta detection, post-bonus baseline update and independent per-status cooldown remain unchanged for those two statuses.
    - Root cause of the reported 10 → 25 curse case: the host polled delayed remote curse copies and could reinterpret the same increase after its own +2.5 bonus, applying the bonus repeatedly. Removing curse from this polling path ensures Leader 【Morale】 only applies its intended +10.
- **Fixed** MilesSoul 【Rule Zero】 now grants the +30 extra stamina **after** the petrify added by feeding 【Scout's Perseverance】 to a downed player has been cleared.
    - Root cause: the old order granted extra stamina immediately and performed the delayed petrify correction afterwards. The subsequent petrify / stamina-cap update could clamp the freshly granted extra stamina. Both host and shared client settlement now confirm the pre-use petrify target first and grant the configured extra stamina once, only after that confirmation.
- **Changed** Astronaut 【Space Food Safety】 default poison for eating unqualified food is now **20** (`0.20`), up from 10 (`0.10`). The Mushroom Glow / Cure-All exemptions and packaged-or-cooked checks are unchanged.
- **Optimised** MilesSoul client sync (no gameplay change): the host no longer pushes the same state to every registered client every 2 s — the client's own 2 s hello already triggers a reply, and a low-frequency pass now only prunes stale registrations. The effective config string is cached and rebuilt only when a value changes; clients skip re-parsing an unchanged state and re-ACK every 8 s (within the 12 s host timeout); scene checks are cached and hello is limited to gameplay. Handshake / ACK state is also cleared per run, so a new run cannot inherit a stale "settle locally" flag.
- **Fixed** The top-left notification box could be only one line tall on some clients, so a two-line bilingual notice was clipped to "English lower half + Chinese upper half".
    - Root cause: the IMGUI fallback (used when the native TMP font overlay is unavailable) drew **each queue entry** at a fixed single-line height. One entry may contain an explicit newline (English + Chinese) or wrap on a narrow screen; with middle-left alignment the taller text was centred and clipped symmetrically. The box height had the same defect.
    - Each entry is now measured with `GUIStyle.CalcHeight` and the box height is the sum of the real entry heights; word wrapping is enabled and the fallback text is no longer bold, matching the native overlay. The native TMP path already measured the joined text correctly.

### 中文

- **修复** 残魂在客机首次拾取童军宝石时，减诅咒可能不完整或随后被撤销的问题。
    - 根因：主机下发的减量先到达客机属主端，更新后的诅咒值再异步同步回主机。诅咒保护逻辑会在允许窗口内把主机尚未更新的旧高值重新接受为保护基准，连续减量也可能继续以该旧值计算；后续保护流程便可能恢复成错误数值。尚未结束的初始诅咒补发也可能用旧绝对目标覆盖首拾减量。
    - 现在会登记预期减量目标并等待同步值确认；连续减量从预期目标累减，不再使用滞后副本；等待期间若有真实新增诅咒，则等量平移目标。确认前旧副本不会抬升基准，也不会触发恢复；未完成的初始诅咒目标也会同步减量。
- **新增** 残魂客机可靠本机结算：客机只要安装并启用**主模组或附属模组其中之一**（附属模组需 v1.0.6+），即改用与主模组客机模式相同的实现。
    - 旧高频使用问题的根因：童军宝石动作在属主客户端本地执行，主机却对客机另用「石化增量启发式 + 延迟绝对写回」。主机按滞后副本写回时，可能覆盖仍在网络传输中的石化增量，造成额外减石化乃至清零。
    - 主模组与附属模组现在编译同一份本机检测 / 结算源码。主机同步实际生效的残魂配置，属主客户端直接走原版本地路径修改饥饿 / 石化 / 额外体力；使用报告只用于主机的【众魂阻挠】次数累计。完成能力握手的客机，主机不再重复结算其宝石效果。
    - Photon 事件 197 的 Kind 7 / 8 / 9 / 10 分别承载定向 Hello、配置、使用报告与配置 ACK；主机收到 ACK 后才切换到客机本机结算，握手期间的使用仍由主机兜底。该玩法同步不受「职业显示-网络同步」开关影响。未安装任一模组的客机仍由主机兜底。
- **调整** 对未安装任一模组的客机，【童军的毅力】【童军的进取】【童军的盛情】【童军的野心】新增**每名玩家、每件宝石各自 1.5 秒触发冷却**。冷却期间使用该宝石不会触发对应职业效果（包括减石化），也不计入宝石使用次数。可靠 RPC、喂食与石化启发式统一经过该限制，无法绕过；原配置项 `sealPowerClientDetectCooldown` / `封印之力-客机判定冷却` 默认值改为 1.5。
- **调整** 太空人【我去，老美的NASA这么厉害】删除「获得诅咒时额外加诅咒」。状态快照与冷却数组现在只包含蛛网、捕蝇草；这两种状态仍保留原有的增量检测、追加后基线后移与各自独立冷却。
    - 「士气 +10 最终变 25」的根因：主机轮询客机延迟同步的诅咒副本，可能在自身追加 +2.5 后再次把同一笔增加识别成新增，反复追加。删除该诅咒轮询路径后，领袖【士气】只保留其应有的 +10。
- **修复** 残魂【零号守则】的 +30 额外体力改为**在本次喂食毅力产生的石化已等量清除之后**发放。
    - 根因：旧顺序先立即发放额外体力，再延迟校正石化；随后的石化 / 体力上限更新会把刚获得的额外体力 clamp 掉。主机与共用客机结算现在都先确认石化回到使用前目标，再且仅再发放一次配置的额外体力。
- **调整** 太空人【太空食品安全】食用不符合条件食物的默认中毒由 **10（0.10）改为 20（0.20）**；诡异菇 / 万灵药豁免与包装食物 / 熟食判定不变。
- **优化** 残魂客机同步（不改变任何效果）：主机不再每 2 秒向所有已注册客机重复推送同一份状态 —— 客机自身的 2 秒 Hello 已会触发一次回复，低频轮询现在只负责清理过期注册项。生效配置串改为缓存（仅在数值变化时重建）；客机对内容未变的状态跳过 Split / 逐项解析，ACK 改为每 8 秒一次（仍小于主机 12 秒超时）；场景判定改为 0.5 秒缓存，Hello 仅在关卡内发送。握手 / ACK 状态也会在每局开始与结束时清空，避免新一局继承上一局残留的「本机结算」资格。
- **修复** 部分客机上左上角提示窗口高度不足，导致中英双语两行提示被裁成「英文只露下半、中文只露上半」。
    - 根因：原生 TMP 字体叠加层不可用时会走 IMGUI 回退绘制，而该路径把**每一条提示**都按固定单行高度绘制。一条提示本身可能含换行（英文 + 中文两行），在窄屏上还可能继续折行；配合垂直居中，超高的文本就被上下对称裁掉。窗口背景高度也有同样缺陷。
    - 现在逐条用 `GUIStyle.CalcHeight` 测量实际排版高度，窗口高度取各条实际高度之和；同时启用自动折行，并取消回退文本的加粗（与原生叠加层一致）。原生 TMP 路径本就会按整段文本测量，无需改动。

## 1.1.0 - Halloween Update

### English

- **New** **Chaos Enderman (混乱末影人)**, **Gargoyle (石像鬼)**, **Alchemist (炼金术士)** and **Trickster (捣蛋鬼)** classes. Class details (keywords, effects and values) are documented in **ModClassIntroduction.md** bundled with the mod package; this changelog only lists what was added.
- **Fixed** Older client addons (e.g. 1.0.6) could show class codes 31 / 32 as `None · 无职业`, and their class-intro panel could be missing or incomplete.
    - Root cause: the addon's local text tables stop at code 30, while the host only resent class name / keywords / intro when the class-code set changed or every 30 s, so a client joining after the first send got nothing until the next resend.
    - Fix: the host now tracks a `(player, class code)` snapshot hash and resends Kind 3 / 4 / 5 immediately on any change; Kind 3 is sent with every 5 s snapshot and the fallback interval is 10 s. The event format is unchanged, so older addons need no update.
- **Changed** Explorer / Leader class-intro summaries (class document + in-game panel, Chinese / English); effects are unchanged, only the intro text was improved.
- **Changed** Pacifist 【Peace?】: a successful kick grants +5 extra stamina (was +2.5); a missed kick deducts 5 extra stamina (was 2.5) and still adds 20 drowsiness. Defaults `PeaceKickExtraStamina和平？-踢中体力` / `PeaceMissDeductStamina和平？-未中扣体力` are now 0.05.
- **Changed** Scout 【Collective】: radius 15 → 25 m; with another Scout / Leader nearby, the 30 s hunger settlement also grants +5 extra stamina (new key `CollectiveScoutExtraStamina集体-童军额外体力`, 0.05). The intro summary now says "fellow Scouts **or** Leaders".
- **Fixed** Reclassing could bring the previous class's initial curse / petrify back after the status clear.
    - Root cause: `ApplyClassChange` cleared statuses but did not cancel the old class's still-pending absolute write-back corrections (`_initialCurse` / `_petrifyRestores`), which kept re-writing their target for up to ~20 s.
    - Fix: those pending entries are removed before `ClearAllNegativeStatuses`; the new class's initial status is then registered by `EnsureInitialStatuses` as usual.
- **New** `[General] halloweenPumpkinHat万圣节-南瓜头` (host-only, **disabled by default**): when enabled, every player's hat in the room is forced to the pumpkin head (hat ID 27), including the airport lobby as well as players joining mid-session or leaving and rejoining; when disabled, each player's recorded original hat is restored. Toggling the option has a built-in **60-second cooldown**: changes made during the cooldown are ignored outright (they are not queued to apply later), so toggle once more after the cooldown ends. Performing the **Shrug** emote (`A_Scout_Emote_Shrug`) switches that player's hat between the originally recorded hat and the pumpkin head; this shrug toggle has a per-player **30-second** cooldown, and during the cooldown no check or decision is performed and nothing is queued. Original hats are tracked by Steam user id, so a rejoin keeps the same original; the effect re-checks once per second while active to recover from client-side customization syncs. Each affected player receives a targeted bilingual notice — **"Happy Halloween!"** on the first line and **"万圣节快乐！"** on the second — the first time their hat is refreshed while the option is enabled. Host-only, no client install needed.
- **Fixed** MilesSoul 【Rule Zero】(feeding Perseverance to a downed player): the petrify added by that use is supposed to be cleared, but two edge cases could break it.
    - Root cause 1: the 3-second correction window could expire before the owner / host had synced the cost, leaving the increase in place. In addition, if the value never returned to the baseline, the pending entry was never removed, which could keep suppressing later petrify gains indefinitely.
    - Root cause 2: when the pre-use petrify snapshot was missing (patch not applied / feed target not resolved), the code silently used the post-cost value as the baseline, so nothing was effectively cleared.
    - Fix: the correction window now always ends and cleans up; if the value did not return in time, the extra stamina is granted once as a fallback and a warning is logged. A missing pre-use snapshot is now logged and the cancellation is skipped instead of pretending to succeed. Fixed both in the host fallback path and in the shared client-side settlement (`Shared/MilesSoulLocal.cs`, shipped in main mod client mode and addon 1.0.7). Protocol unchanged.
    - Compatibility: clients still running main mod 1.0.9 or addon 1.0.6 keep their own older local-settlement code; upgrading the addon to 1.0.7 (or using the main mod's client mode) is required to get this fix.
- **Note** Class count is now 34. The config structure has changed, so `ConfigRevision` was bumped (currently `2026-10-31-halloween-hat-1`): an existing `com.peakmodding.RollOfTheScout.cfg` is backed up to `.old-<timestamp>` and regenerated on first launch.

### 中文

- **新增** 职业**混乱末影人**、**石像鬼**、**炼金术士**、**捣蛋鬼**，详见（模组包中附带的）职业介绍文档**ModClassIntroduction.md**；本更新日志只列出新增内容，不介绍具体职业效果。
- **修复** 旧版附属模组（如 1.0.6）可能会把职业码 31 / 32 显示成 `None · 无职业`，以及职业介绍板块缺失未正常显示的问题。
    - 根因：附属模组本地文案表只覆盖到职业码 30，而主机只在职业码集合变化时或每 30 秒才重发职业名 / 关键词 / 简介，首次广播之后加入的客机拿不到文本，只能等下一次重发。
    - 修复：主机新增 `(玩家, 职业码)` 快照哈希，任何变化都立即重发 Kind 3 / 4 / 5；Kind 3 随每次快照发送（每 5 秒），保底间隔改为 10 秒。事件格式不变，旧附属模组无需更新。
- **调整** 先驱 / 领袖职业简介（文档 + 游戏内板块，中英双语）；实际效果未变，仅优化介绍文本。
- **调整** 和平主义者【和平？】：踢中玩家 +2.5 → +5 额外体力，踢空 -2.5 → -5 额外体力、+20 困倦。配置项 `PeaceKickExtraStamina和平？-踢中体力` / `PeaceMissDeductStamina和平？-未中扣体力` 默认值改为 0.05。
- **调整** 童军【集体】半径 15 → 25 米；有童军 / 领袖同伴时 30 秒饥饿结算同时 +5 额外体力（新增 `CollectiveScoutExtraStamina集体-童军额外体力`，0.05）。简介改为「与同职业【童军】**或**【领袖】」。
- **修复** 转职后旧职业的初始诅咒 / 石化可能被写回。
    - 根因：`ApplyClassChange` 清除状态时未取消旧职业仍在排队的绝对写回校正（`_initialCurse` / `_petrifyRestores`），它们会在约 20 秒内反复把目标值写回。
    - 修复：在 `ClearAllNegativeStatuses` 之前先移除这些条目；新职业的初始状态随后仍由 `EnsureInitialStatuses` 正常登记并施加。
- **新增** 通用配置项 `万圣节-南瓜头`（`halloweenPumpkinHat`，仅主机生效，**默认关闭**）：开启后，房间内所有玩家的头饰被强制换成南瓜头（帽子编号 27），包括机场大厅、中途加入与退出重进的玩家；关闭后按记录的原本头饰逐一恢复。配置项切换带 **60 秒内置冷却**：冷却期间的修改会被**直接忽略**（不会排队到冷却结束后生效），需在冷却结束后再次切换。玩家做**耸肩**表情（`A_Scout_Emote_Shrug`）会在「原本帽子」与「南瓜头」之间切换；每名玩家的该切换有 **30 秒**内置冷却，冷却期间不做任何检测与判断、也不会排队生效。原帽按 Steam 用户 id 记录，退出重进仍沿用同一份原帽；开启期间每秒复核一次，以覆盖客户端自身的装扮同步。每名被成功换上南瓜头的玩家在本次开启期间会收到一次定向中英双语提示——第一行 **“Happy Halloween!”**、第二行 **“万圣节快乐！”**。客机零安装。
- **修复** 残魂【零号守则】（把童军的毅力喂给昏迷玩家）本应等量清除该次使用追加的石化，但有两个边缘缺陷。
    - 根因 1：3 秒校正窗口可能在属主 / 主机副本同步到代价之前就到期，增量因此残留；并且若石化始终没有回到基准，待处理条目永远不会被移除，可能对之后的所有石化增长持续压制。
    - 根因 2：未记录到「使用前」石化快照时（补丁未生效 / 未解析到喂食目标），旧代码静默以代价后的当前值当基准，等于没有清除。
    - 修复：校正窗口到期必定收尾；若届时仍未回到基准，则兜底发放一次额外体力并写警告日志。缺失使用前快照时改为写警告并跳过抵消，不再假装成功。主机兜底路径与客机共用本机结算（`Shared/MilesSoulLocal.cs`，随主模组客机模式与附属 1.0.7 发布）均已修复。事件协议不变。
    - 兼容性：仍在使用主模组 1.0.9 或附属 1.0.6 的客机会继续运行其自带的旧本机结算代码，需要将附属升级到 1.0.7（或使用主模组的客机模式）才能获得该修复。
- **注意** 职业总数变为 34。配置结构已变化，`ConfigRevision` 已递增（当前 `2026-10-31-halloween-hat-1`）：旧配置文件 `com.peakmodding.RollOfTheScout.cfg` 会在首次启动时备份为 `.old-时间戳` 并重新生成。
