using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;

namespace RollOfTheScout
{
    /// <summary>
    /// 配置文件「板块顺序」修正器。
    ///
    /// 背景：BepInEx 的 <see cref="ConfigFile"/> 在写盘时会**按 Section 名排序**
    /// （中文字段按 Unicode 码点乱序、英文字段按字母序），因此生成出来的 .cfg 里各板块顺序
    /// 并不是我们绑定的业务顺序。本类在保存后把文件重新按「绑定时的出现顺序」排回来：
    /// 通用 → 先驱 → 领袖 → 后勤 → … → 程序员。
    ///
    /// 纯文本后处理：只在文件确实被改动过时执行一次，失败静默；不影响 BepInEx 的加载与 ModConfig 的读写。
    /// </summary>
    internal static class ConfigFileOrder
    {
        private static string _lastStamp;
        private static float _nextTick;

        /// <summary>按 <paramref name="sectionOrder"/> 重排配置文件（幂等）。</summary>
        internal static void Apply(ConfigFile config, IList<string> sectionOrder)
        {
            try
            {
                string path = config != null ? config.ConfigFilePath : null;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string stamp = File.GetLastWriteTimeUtc(path).Ticks + ":" + new FileInfo(path).Length;
                if (stamp == _lastStamp) return;   // 文件没变 → 无需重排
                _lastStamp = stamp;

                string text = File.ReadAllText(path);
                string nl = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
                string[] lines = text.Replace("\r\n", "\n").Split('\n');

                var preamble = new List<string>();
                var blocks = new List<KeyValuePair<string, List<string>>>();
                List<string> cur = null;
                string curName = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        if (cur != null) blocks.Add(new KeyValuePair<string, List<string>>(curName, cur));
                        curName = line.Trim().TrimStart('[').TrimEnd(']');
                        cur = new List<string>();
                    }
                    else if (cur == null)
                    {
                        preamble.Add(line);
                    }
                    else
                    {
                        cur.Add(line);
                    }
                }
                if (cur != null) blocks.Add(new KeyValuePair<string, List<string>>(curName, cur));
                if (blocks.Count == 0) return;

                var used = new bool[blocks.Count];
                var sb = new StringBuilder();
                foreach (var l in preamble) sb.Append(l).Append(nl);

                if (sectionOrder != null)
                {
                    foreach (var want in sectionOrder)
                    {
                        if (string.IsNullOrEmpty(want)) continue;
                        for (int i = 0; i < blocks.Count; i++)
                        {
                            if (used[i] || blocks[i].Key != want) continue;
                            used[i] = true;
                            AppendBlock(sb, blocks[i], nl);
                        }
                    }
                }
                for (int i = 0; i < blocks.Count; i++)   // 未登记在顺序表里的板块：保持原相对顺序，排在最后
                {
                    if (used[i]) continue;
                    used[i] = true;
                    AppendBlock(sb, blocks[i], nl);
                }

                File.WriteAllText(path, sb.ToString());
            }
            catch
            {
                // 排序失败不影响功能，静默忽略。
            }
        }

        private static void AppendBlock(StringBuilder sb, KeyValuePair<string, List<string>> block, string nl)
        {
            sb.Append('[').Append(block.Key).Append(']').Append(nl);
            foreach (var l in block.Value) sb.Append(l).Append(nl);
        }

        /// <summary>由 HostClassManager.Update 每帧调用（内部按 2 秒节流）：保证游戏中改完配置后文件顺序也被纠正。</summary>
        internal static void Tick()
        {
            if (UnityEngine.Time.unscaledTime < _nextTick) return;
            _nextTick = UnityEngine.Time.unscaledTime + 2f;
            Apply(ModConfig.File, ModConfig.SectionOrder);
        }
    }
}
