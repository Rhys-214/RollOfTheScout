namespace RollOfTheScout
{
    /// <summary>
    /// 主机侧职业类型。当前共 30 个职业。
    /// 该枚举只存在于主机内存中，不会写入 Photon 自定义属性（客户端零安装，无接收方）。
    /// </summary>
    public enum ClassType
    {
        None = 0,
        Explorer = 1,     // 先驱
        Leader = 2,       // 领袖
        Logistician = 3,  // 后勤
        Doctor = 4,       // 医生
        Assassin = 5,     // 刺客
        Pandora = 6,      // 潘多拉之子
        Necromancer = 7,  // 死灵法师
        M = 8,            // M（享受）
        Savage = 9,       // 野人（自然韧性 / 野性未泯 / 不拿拿 / 知识污染）
        Scout = 10,       // 童军（集体）
        StandardBearer = 11, // 旗手（执旗）
        Dancer = 12,      // 舞王（舞动青春 / 全场欢呼 / 狂热舞者）
        Daihao = 13,      // 待皓（化身 / 喂喂喂，听得见吗 / 脚滑）
        Saint = 14,       // 圣人（教诲 / 完美主义 / 辟谷养生 / 跌落神坛）
        Mage = 15,        // 法师（魔法卷轴 / 黑魔法）
        Challenger = 16,  // 挑战者（极限 / 顶级玩家 / 不屑）
        MasterChef = 17,  // 特级厨师（自带厨具 / 手暖炉 / 烹饪 / 煤气中毒 / 破坏大师）
        Cook = 18,        // 炊事员（后勤保障 / 饮食供给 / 食品安全 / 创伤应激）
        Pacifist = 19,    // 和平主义者（和平？：初始诅咒 / 右键改踢人）
        Hamster = 20,     // 仓鼠（我全都要：初始背包 / 负重达标加速）
        Firefighter = 21, // 救火队员（温暖臂膀 / 火线救援）
        Narcissist = 22,  // 自恋狂（化妆盒 / 精致 / 化学中毒）
        LoneWolf = 23,    // 独狼（独行长路）
        Gunner = 24,      // 炮手（开炮 / 炸膛）
        Band = 25,        // 乐队成员（乐器 / 演奏 / 乐队 / 体弱）
        MilesSoul = 26,   // 领队·迈尔斯残魂（受诅残魂 / 众魂阻挠 / 封印之力 / 零号守则 / 封印解放）
        Qu = 27,          // 区（！？区区？！ / 分解者 / 区块链 / 好多区 / 区毒不食子）
        Astronaut = 28,   // 太空人（轻飘飘 / 飞起来 / 太空食品安全 / 我去，老美的NASA这么厉害）
        Langwang = 29,    // 榔王（精品大果 / 上瘾 / 纤维化 / 没吃的赶紧戒）
        Programmer = 30   // 程序员（BUG / 溢出 / 熬夜 / 开挂）
    }
}