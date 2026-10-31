# RollOfTheScout · 童子军的命运之骰

**English** ｜ **中文**（见下方）

---

## English

> A **PEAK** mod. At the start of every run, every player is **randomly assigned** a **class**. The host drives class assignment and the host-side effects through PEAK's networked RPCs — **clients need not install anything**. 
> - Clients with the main mod or companion addon additionally settle their own MilesSoul gem effects locally using the host's synchronized configuration.

### Highlights

- **Host-only install, works for everyone.** Only the host needs the mod; other players can join unmodified.
- **Dozens of classes, each with pros and cons** — from serious ones (Explorer / Leader / Logistician / Doctor…) to chaotic ones (Pandora / Pacifist / Qu…).
- **Weighted random assignment**: each class supports its own **max players / min players / draw weight**.
- **Team-oriented design**: many classes buff nearby teammates (rally, nursing, supplies, rescue…).
- **Class change**: eat a configurable item (default: the Glow Mushroom) to change your class. You can also change class by performing a specific emote (default: "No No")—however, this method is disabled by default.
- **Host-screen UI**: a class-assignment panel plus effect notifications (toasts).
- **Overhead class prefix**: shows `【Class】Nickname` above players (drawn locally on each client); Disabled by default.
- **Optional client support** (Photon custom event `197`): the companion addon shows class info and gives MilesSoul clients the same local gem-action detection and settlement as the main mod's client mode.
- **Fully configurable**, and the config file is **bilingual**.

### Requirements

- **PEAK** (Steam)
- **BepInEx** for PEAK (`BepInExPack_PEAK`)

### Installation (host only)

1. Install BepInEx for PEAK.
2. Put the mod file `com.peakmodding.RollOfTheScout.dll` into `PEAK/BepInEx/plugins/`.
3. Launch the game and create a room — everyone is automatically assigned a class at the start of each run.

Clients install **nothing** by default. Optionally, the main mod or the companion addon can be installed for class display and more reliable local MilesSoul gem settlement.

### Configuration

Config files are generated automatically on first run(`BepInEx/config/com.peakmodding.RollOfTheScout.cfg`), and can also be edited directly in-game through the ModConfig config UI.
> If a new version adds options, an existing `.cfg` will **not** pick up the new keys — delete the old `.cfg` and restart to regenerate.
> After the mod is updated to version 1.0.7 or later, the mod configuration file will be automatically backed up and reset during the update.

### Current class list (v1.1.0, as of Oct 31, 2026)

`Explorer`, `Leader`, `Logistician`, `Doctor`, `Assassin`, `Pandora`, `Necromancer`, `M`, `Savage`, `Scout`, `StandardBearer`, `Dancer`, `Daihao`, `Saint`, `Mage`, `Challenger`, `MasterChef`, `Cook`, `Pacifist`, `Hamster`, `Firefighter`, `Narcissist`, `LoneWolf`, `Gunner`, `Band`, `MilesSoul`, `Qu`, `Astronaut`, `Langwang`, `Programmer`, `ChaosEnderman`, `Gargoyle`, `Alchemist`, `Trickster`.

### Building from source

Requirements: **.NET SDK**, **PEAK**, **BepInEx 5**.

```bash
# 1) Point the build at your local paths
#    copy Config.Build.user.props.example  ->  Config.Build.user.props
#    then fill in PeakRoot / BepInExCore

# 2) Build (Release)
dotnet build Mod/RollOfTheScout/RollOfTheScout.csproj -c Release
```

Output: `Mod/RollOfTheScout/bin/Release/com.peakmodding.RollOfTheScout.dll`.

### Repository layout

```
.
├── Directory.Build.props            # repo-level MSBuild config (collects local paths)
├── Config.Build.user.props.example  # template for your local paths (copy to Config.Build.user.props)
├── README.md
├── LICENSE
└── Mod/
    ├── RollOfTheScout/                 # the mod (host-side)
    └── Shared/                      # source shared with the client display mod
```

### Known constraints

- Except for the documented local MilesSoul path on clients running either mod, class effects are host-driven; an unmodded client sees no class text information (the effects still apply).
- Some effects can only be approximated because of engine limits (e.g. passive multipliers use "poll + compensating delta").
- Class display is per-client local UI; there is no way to push arbitrary text into an unmodded client's screen.

### License

Licensed under the **[GNU General Public License v3.0 (GPLv3)](LICENSE)**.
- ✅ You may use, modify and redistribute this mod — **including commercially**.
- ✅ Derivative works must be released under the GPLv3 too (**share-alike**: ship the source).
- ⚠️ Provided **without any warranty**.

### Credits

- The **Silver** mod (by fengsheng404) — the original inspiration for this mod's design.
- [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX).
- This mod was made with **Deepseek V4.1 Flash**.
- Special thanks to the PEAK community friends for their great support of me and this mod:
    - 待皓（🐔待待子的无尽巧思🌟）
    - 熊猫狗旗帜
    - 晨曦
    - ATTacv
    - liu
    - 黑松露味薯片
    - 兰斯
    - pencil_book
    - 晓轩CCA
    - 铃铃猫
    - 伟约·恶和
    - 落羽之月所言
    - 星辰幻玥
    - 御坂御坂
    - 笙十二
    - 救赎要玩剁菜

### Disclaimer

This is an **unofficial third-party mod**, not affiliated with PEAK's developers or publishers. This repository contains **no** game assemblies, decompiled game code, or other copyrighted assets.

### A few words from the author

General contact: email **2043581350@qq.com** (or just add me on QQ to send feedback and suggestions).
Welcome to join the PEAK community group ~ (QQ group: **814891614**; mod updates are playtested there first).

---

## 中文

> 一个 **PEAK** 模组。每局游戏开始时，为每名玩家**随机分配**一个**职业**。职业分配与主机侧效果由**房主（MasterClient）**通过 PEAK 网络 RPC 驱动 —— **客机无需安装**。
> - 若客机安装主模组或配套附属模组，残魂宝石效果会按主机同步的配置在本人客户端结算。

### 特色

- **仅房主安装即可全员生效**：只有房主需要装模组，其他玩家可以原样加入。
- **数十个职业，各有正负效果**：既有正经向的（先驱 / 领袖 / 后勤 / 医生……），也有整活向的（潘多拉之子 / 和平主义者 / 区……）。
- **加权随机分配**：每个职业支持独立的**最大人数 / 最小人数 / 抽取权重**。
- **团队向设计**：许多职业会为附近队友带来增益（号召、护理、补给、救援……）。
- **转职机制**：食用可配置的物品（默认「诡异菇」）即可更换职业。也可以通过做特定的表情动作（默认为“不行不行”）来进行转职——不过该方式默认禁用。
- **主机屏幕 UI**：职业分配面板 + 效果提示（Toast）。
- **头顶职业前缀**：在玩家头顶显示「【职业】昵称」（各客户端本地绘制）；默认禁用。
- **可选客机支持**（Photon 自定义事件 `197`）：配套附属模组可显示职业信息，并让残魂客机使用与主模组客机模式相同的本机宝石检测与结算。
- **全部可配置**，且配置文件为**中英双语**。

### 环境要求

- **PEAK**（Steam）
- PEAK 的 **BepInEx**（`BepInExPack_PEAK`）

### 安装（仅房主）

1. 为 PEAK 安装 BepInEx；
2. 将模组文件 `com.peakmodding.RollOfTheScout.dll` 放入 `PEAK/BepInEx/plugins/`；
3. 启动游戏，并创建房间 —— 每局开始时将会为全员自动分配职业。

客机默认**什么都不用装**。也可选装主模组或配套附属模组，以获得职业显示与更可靠的残魂本机宝石结算。

### 模组配置

配置文件将在首次运行自动生成（`BepInEx/config/com.peakmodding.RollOfTheScout.cfg`），也可在游戏内的 ModConfig 配置界面直接修改。
> 新版本若新增了配置项，**旧的 `.cfg` 不会自动补全新键** —— 请删除旧版 `.cfg` 后重启以重新生成。
> 模组1.0.7版本及以后，模组配置文件将会自动备份与更新重置。

### 当前版本职业一览（v1.1.0，截止至2026年10月31日）

`先驱, 领袖, 后勤, 医生, 刺客, 潘多拉之子, 死灵法师, M, 野人, 童军, 旗手, 舞王, 待皓, 圣人, 法师, 挑战者, 特级厨师, 炊事员, 和平主义者, 仓鼠, 救火队员, 自恋狂, 独狼, 炮手, 乐队成员, 迈尔斯残魂, 区, 太空人, 榔王, 程序员, 混乱末影人, 石像鬼, 炼金术士，捣蛋鬼`


### 从源码编译

前置：**.NET SDK**、**PEAK**、**BepInEx 5**。

```bash
# 1) 指向本机路径
#    复制 Config.Build.user.props.example → Config.Build.user.props
#    填写 PeakRoot / BepInExCore

# 2) 编译（Release）
dotnet build Mod/RollOfTheScout/RollOfTheScout.csproj -c Release
```

产物：`Mod/RollOfTheScout/bin/Release/com.peakmodding.RollOfTheScout.dll`。

### 仓库结构

```
.
├── Directory.Build.props            # 仓库级 MSBuild 配置（集中本机路径）
├── Config.Build.user.props.example  # 本机路径模板（复制为 Config.Build.user.props）
├── README.md
├── LICENSE
└── Mod/
    ├── RollOfTheScout/                 # 模组本体（主机侧）
    └── Shared/                      # 与客机显示模组共用的源码
```

### 已知约束

- 除安装任一模组的客机使用文档所述残魂本机路径外，职业效果由主机驱动；未装模组的客机看不到职业文字信息（效果依旧生效）。
- 部分效果受引擎限制只能近似实现（如被动倍率类采用「轮询 + 补偿增量」）。
- 职业显示属于各客户端本地 UI；原版没有「把任意文本送到未装模组客机屏幕」的通道。

### 许可证

本项目采用 **[GNU 通用公共许可证第 3 版（GPLv3）](LICENSE)**。
- ✅ 可以自由使用、修改与再分发（**包括商业用途**）；
- ✅ 衍生作品必须以 **GPLv3** 发布（**相同方式共享**：需一并提供源码）；
- ⚠️ 本程序**不提供任何担保**。

### 致谢

- 参考模组 **Silver**（作者 fengsheng404）—— 本模组最初的设计灵感来源。
- [BepInEx](https://github.com/BepInEx/BepInEx) 与 [HarmonyX](https://github.com/BepInEx/HarmonyX)。
- 本模组使用 **Deepseek V4.1 Flash** 制作。
- 特别感谢各位PEAK同好群群友对我与本模组的大力支持：
    - 待皓（🐔待待子的无尽巧思🌟）
    - 熊猫狗旗帜
    - 晨曦
    - ATTacv
    - liu
    - 黑松露味薯片
    - 兰斯
    - pencil_book
    - 晓轩CCA
    - 铃铃猫
    - 伟约·恶和
    - 落羽之月所言
    - 星辰幻玥
    - 御坂御坂
    - 笙十二
    - 救赎要玩剁菜

### 免责声明

本模组为**第三方非官方作品**，与 PEAK 的开发 / 发行方**无任何关联**。本仓库**不包含**任何游戏程序集、游戏反编译源码或其它受版权保护的资源。

### 一些作者的话

- 作者通用联系方式：邮箱 **2043581350@qq.com**（当然，你也可以直接加QQ反馈意见与建议）
- 欢迎加入PEAK同好群喵（**QQ群：814891614**，模组更新将优先在本群内测）~
