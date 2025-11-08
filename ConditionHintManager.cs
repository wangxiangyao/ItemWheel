using System;
using System.Collections.Generic;

namespace ItemWheel
{
    /// <summary>
    /// 条件化提示管理器：
    /// - 支持同一条件的多条文案
    /// - 根据触发次数升级情绪（中性 -> 稍急 -> 生气）
    /// - 统一调用 BubbleNotifier 显示（可复用到语音轮盘等）
    /// 目前仅实现：物品不可使用（可扩展更多条件）。
    /// </summary>
    internal static class ConditionHintManager
    {
        public enum HintCondition
        {
            ItemNotUsable = 1,
            // TODO: Cooldown, OutOfAmmo, Blocked, NoTarget, etc.
        }

        private enum Emotion
        {
            Neutral = 0,   // 中性
            Mild = 1,      // 稍急
            Angry = 2      // 生气
        }

        // 每个条件触发计数（用于情绪升级）
        private static readonly Dictionary<HintCondition, int> _conditionCounts = new();

        // 每个 (条件, 情绪) 的轮换索引，避免重复同一句
        private static readonly Dictionary<string, int> _rotationIndex = new();

        // 条件 -> 情绪 -> 文案列表（可包含占位符：{item}）
        private static readonly Dictionary<HintCondition, Dictionary<Emotion, List<string>>> _templates
            = new Dictionary<HintCondition, Dictionary<Emotion, List<string>>>
        {
            {
                HintCondition.ItemNotUsable,
                new Dictionary<Emotion, List<string>>
                {
                    { Emotion.Neutral, new List<string>
                        {
                            "{item}现在用不了。",
                            "好像不能用{item}。",
                            "当前无法使用{item}。",
                        }
                    },
                    { Emotion.Mild, new List<string>
                        {
                            "现在不能用{item}，先看看情况。",
                            "{item}不行，换个办法吧。",
                            "别急，{item}现在用不了。",
                        }
                    },
                    { Emotion.Angry, new List<string>
                        {
                            "别再按了，{item}用不了！",
                            "我都说了用不了{item}！",
                            "{item}现在真的不行！",
                        }
                    },
                }
            }
        };

        /// <summary>
        /// 对外接口：显示“物品不可使用”的提示。
        /// 会根据触发次数选择不同情绪与文案，并轮换同情绪下的句式。
        /// </summary>
        public static void ShowItemNotUsable(string itemName)
        {
            Show(HintCondition.ItemNotUsable, new Dictionary<string, string>
            {
                { "item", string.IsNullOrEmpty(itemName) ? "该物品" : itemName }
            });
        }

        /// <summary>
        /// 主入口：按条件显示提示。
        /// context 可传占位符，例如 {"item", "急救箱"}
        /// </summary>
        private static void Show(HintCondition condition, Dictionary<string, string> context)
        {
            try
            {
                int count = IncrementCount(condition);
                var emotion = SelectEmotion(count);
                var template = PickTemplate(condition, emotion);

                if (string.IsNullOrEmpty(template))
                {
                    // 回退文案
                    template = "当前无法执行。";
                }

                string text = ApplyContext(template, context);
                BubbleNotifier.Show(text);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[ConditionHintManager] 显示提示失败: {e.Message}");
            }
        }

        private static int IncrementCount(HintCondition condition)
        {
            if (!_conditionCounts.TryGetValue(condition, out int c))
            {
                c = 0;
            }
            c++;
            _conditionCounts[condition] = c;
            return c;
        }

        private static Emotion SelectEmotion(int count)
        {
            // 简单门限：1-2 次中性，3-4 次稍急，5+ 次生气
            if (count <= 2) return Emotion.Neutral;
            if (count <= 4) return Emotion.Mild;
            return Emotion.Angry;
        }

        private static string PickTemplate(HintCondition condition, Emotion emotion)
        {
            if (!_templates.TryGetValue(condition, out var byEmotion)) return null;
            if (!byEmotion.TryGetValue(emotion, out var list) || list == null || list.Count == 0) return null;

            string key = condition.ToString() + ":" + emotion.ToString();
            int idx = 0;
            if (_rotationIndex.TryGetValue(key, out int current))
            {
                idx = (current + 1) % list.Count; // 轮换到下一句
            }
            _rotationIndex[key] = idx;
            return list[idx];
        }

        private static string ApplyContext(string template, Dictionary<string, string> context)
        {
            if (context == null || context.Count == 0) return template;
            string result = template;
            foreach (var kv in context)
            {
                result = result.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);
            }
            return result;
        }

        /// <summary>
        /// 将指定条件的情绪计数归零（恢复到平静/中性）。
        /// </summary>
        public static void Reset(HintCondition condition)
        {
            _conditionCounts[condition] = 0;
        }

        /// <summary>
        /// 重置所有条件的情绪计数。
        /// </summary>
        public static void ResetAll()
        {
            _conditionCounts.Clear();
        }
    }
}
