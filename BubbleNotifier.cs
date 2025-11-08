using UnityEngine;

namespace ItemWheel
{
    /// <summary>
    /// 通用气泡/提示管理器：封装游戏内现成提示能力，便于复用（物品轮盘、语音轮盘等）
    /// 参考 Backpack_QuickWheel 的 VoiceBubbleManager 写法。
    /// </summary>
    internal static class BubbleNotifier
    {
        public enum BubbleType
        {
            Notification,   // 屏幕通知（NotificationText.Push）
            Dialogue        // 角色头顶气泡（CharacterMainControl.PopText）
        }

        // 默认使用对话气泡（头顶气泡），与参考实现一致
        private static readonly BubbleType DefaultType = BubbleType.Dialogue;

        /// <summary>
        /// 显示提示文本
        /// </summary>
        public static void Show(string text, BubbleType? typeOverride = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            var type = typeOverride ?? DefaultType;

            try
            {
                switch (type)
                {
                    case BubbleType.Notification:
                        // 游戏的屏幕通知（如果可用）
                        try
                        {
                            Duckov.UI.NotificationText.Push(text);
                        }
                        catch { /* 安全回退到对话气泡 */ Show(text, BubbleType.Dialogue); }
                        break;
                    case BubbleType.Dialogue:
                        // 角色头顶气泡：使用官方 CharacterMainControl.PopText
                        var player = CharacterMainControl.Main;
                        if (player != null)
                        {
                            player.PopText(text, 50f); // 使用较快速度
                        }
                        else
                        {
                            // 无法定位玩家时，回退为屏幕通知
                            Show(text, BubbleType.Notification);
                        }
                        break;
                    default:
                        Show(text, BubbleType.Notification);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BubbleNotifier] 显示提示失败: {e.Message}");
            }
        }

        /// <summary>
        /// 物品不可使用时的标准提示
        /// </summary>
        public static void ShowItemNotUsable(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                Show("当前物品无法使用");
            }
            else
            {
                Show($"无法使用：{itemName}");
            }
        }
    }
}

