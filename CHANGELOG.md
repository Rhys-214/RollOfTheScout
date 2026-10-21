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
    - 现已全部修正为紫荔莓（`Kingberry Purple`），配置项改为 `[榔王] 精品大果-紫荔莓物品` / `精品大果-紫荔莓权重`，`荔莓物品` ， `后勤保障-食物池` 不再包含 `Kingberry Yellow`。
- **修复** 残魂【封印之力】：客机残魂连续高频使用童军宝石道具时，石化值可能被额外扣除甚至清零的问题。
    - 根因：绝对写回+异步同步的丢更新——客机的 `petrifyAmount` 由**属主客户端**异步同步回主机。原实现把每次判定的减石化量累积到待办，延迟 0.35s 后用 `HostApi.SetPetrify`（`RPC_SyncPetrify`，**绝对赋值**）写回：
        1. 高频使用时，主机读到的 `cur` 落后于客机真实值；
        2. 待办累积了多次 -5 后，按过期的 `cur - pending` 绝对写回；
        3. 该绝对赋值把「已经使用、但尚未同步到主机」的在途 +7 增量一并覆盖 → **额外扣除**；
        4. 反复循环即可把石化扣到 0。
    - 减石化的绝对写回现在会等待石化值**安静 0.75秒**后执行（持续使用时最多推迟4秒），不再覆盖尚未同步到主机的在途增量；判定阈值改为严格大于2.5。
- **新增** 残魂【众魂阻挠】效果生成的童军领队 的 场上存在数量上限，固定为4。
    - 此项为硬编码，无配置项。
