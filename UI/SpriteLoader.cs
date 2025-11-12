using System;
using System.IO;
using UnityEngine;
using QuickWheel.Utils;

namespace ItemWheel.UI
{
    /// <summary>
    /// 轮盘 Sprite 加载器
    /// 统一管理轮盘格子的 Sprite 加载
    /// </summary>
    public static class WheelSpriteLoader
    {
        private static Sprite _slotNormalSprite;
        private static Sprite _slotHoverSprite;
        private static Sprite _slotSelectedSprite;
        private static bool _isLoaded = false;

        /// <summary>
        /// 正常状态的格子 Sprite
        /// </summary>
        public static Sprite SlotNormal => _slotNormalSprite;

        /// <summary>
        /// 悬停状态的格子 Sprite
        /// </summary>
        public static Sprite SlotHover => _slotHoverSprite;

        /// <summary>
        /// 选中状态的格子 Sprite
        /// </summary>
        public static Sprite SlotSelected => _slotSelectedSprite;

        /// <summary>
        /// 是否已加载
        /// </summary>
        public static bool IsLoaded => _isLoaded;

        /// <summary>
        /// 加载自定义格子 Sprite
        /// 从 Mod 目录的 texture 文件夹加载
        /// </summary>
        public static void Load()
        {
            if (_isLoaded)
            {
                return; // 已经加载过了
            }

            try
            {
                // 获取 Mod 目录路径
                string modPath = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                );
                string texturePath = Path.Combine(modPath, "texture");

                // 加载三个状态的 Sprite
                string normalPath = Path.Combine(texturePath, "WheelSlot_Normal.png");
                string hoverPath = Path.Combine(texturePath, "WheelSlot_Hover.png");
                string selectedPath = Path.Combine(texturePath, "WheelSlot_Selected.png");

                _slotNormalSprite = QuickWheel.Utils.SpriteLoader.LoadFromFile(normalPath, 100f);
                _slotHoverSprite = QuickWheel.Utils.SpriteLoader.LoadFromFile(hoverPath, 100f);
                _slotSelectedSprite = QuickWheel.Utils.SpriteLoader.LoadFromFile(selectedPath, 100f);

                if (_slotNormalSprite != null)
                {
                    Debug.Log("[SpriteLoader] Custom slot sprites loaded successfully");
                    _isLoaded = true;
                }
                else
                {
                    Debug.LogWarning("[SpriteLoader] Failed to load custom slot sprites, will use default colors");
                    _isLoaded = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpriteLoader] Error loading custom sprites: {e}");
                _isLoaded = false;
            }
        }

        /// <summary>
        /// 重新加载 Sprite（用于热重载）
        /// </summary>
        public static void Reload()
        {
            _isLoaded = false;
            _slotNormalSprite = null;
            _slotHoverSprite = null;
            _slotSelectedSprite = null;
            Load();
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            if (_slotNormalSprite != null)
            {
                UnityEngine.Object.Destroy(_slotNormalSprite);
                _slotNormalSprite = null;
            }

            if (_slotHoverSprite != null)
            {
                UnityEngine.Object.Destroy(_slotHoverSprite);
                _slotHoverSprite = null;
            }

            if (_slotSelectedSprite != null)
            {
                UnityEngine.Object.Destroy(_slotSelectedSprite);
                _slotSelectedSprite = null;
            }

            _isLoaded = false;
        }
    }
}
