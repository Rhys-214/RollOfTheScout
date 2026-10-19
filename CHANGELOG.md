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
