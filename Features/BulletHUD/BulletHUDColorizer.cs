using UnityEngine;
using UnityEngine.UI;
using ItemStatsSystem;
using Duckov;

namespace ItemWheel.Features.BulletHUD
{
    /// <summary>
    /// 子弹HUD着色器
    /// 根据当前装填子弹的品质，为 BulletTypeHUD 显示对应颜色
    /// </summary>
    public class BulletHUDColorizer
    {
        private Transform _bulletTypeHUDTransform;
        private Image _proceduralImage;
        private int _lastBulletTypeID = -1;
        private int _lastBulletQuality = -1;
        private Item _lastBulletItem = null;
        private object _lastGun = null;
        private bool _isInitialized = false;
        private int _pendingRecolorFrames = 0;

        // HUD 路径：LevelConfig/LevelManager/HUDCanvas/BulletTypeHUD/CurrentBulletType
        private const string HUD_PATH = "LevelConfig/LevelManager/HUDCanvas/BulletTypeHUD/CurrentBulletType";
        private const int POST_UPDATE_STABILIZE_FRAMES = 8;

        public BulletHUDColorizer()
        {
            LevelManager.OnLevelInitialized += HandleLevelInitialized;
            Canvas.willRenderCanvases += HandleWillRenderCanvases;
        }

        /// <summary>
        /// 每帧更新 - 检测子弹类型变化并更新颜色
        /// </summary>
        public void Update()
        {
            // 延迟初始化 UI 元素（场景加载后才能找到）
            if (!_isInitialized)
            {
                TryInitialize();
                if (!_isInitialized) return; // 初始化失败，跳过本帧
            }

            // 检测子弹类型是否变化
            var gun = CharacterMainControl.Main?.GetGun();
            if (gun == null || gun.GunItemSetting == null)
            {
                // 没有装备枪械，重置状态
                if (_lastBulletTypeID != -1 || _lastGun != null)
                {
                    _lastBulletTypeID = -1;
                    _lastBulletQuality = -1;
                    _lastBulletItem = null;
                    _lastGun = null;
                    _pendingRecolorFrames = 0;
                    ResetColor();
                }
                return;
            }

            bool gunChanged = !ReferenceEquals(_lastGun, gun);
            if (gunChanged)
            {
                _lastGun = gun;
            }

            // 获取当前装填的子弹
            Item currentBullet = gun.GunItemSetting.GetCurrentLoadedBullet();
            int currentTypeID = currentBullet?.TypeID ?? -1;
            int currentQuality = currentBullet?.Quality ?? -1;

            bool bulletChanged =
                currentTypeID != _lastBulletTypeID ||
                currentQuality != _lastBulletQuality ||
                !ReferenceEquals(_lastBulletItem, currentBullet);

            if (gunChanged || bulletChanged)
            {
                _lastBulletTypeID = currentTypeID;
                _lastBulletQuality = currentQuality;
                _lastBulletItem = currentBullet;
                _pendingRecolorFrames = POST_UPDATE_STABILIZE_FRAMES;
                UpdateColor(currentBullet);
            }

        }

        /// <summary>
        /// 尝试初始化 UI 元素
        /// </summary>
        private void HandleLevelInitialized()
        {
            if (_isInitialized)
            {
                ResetColor();
            }

            _isInitialized = false;
            _bulletTypeHUDTransform = null;
            _proceduralImage = null;
            _lastBulletTypeID = -1;
            _lastBulletQuality = -1;
            _lastBulletItem = null;
            _lastGun = null;
            _pendingRecolorFrames = 0;
        }

        /// <summary>
        /// 尝试初始化 UI 元素
        /// </summary>
        private void TryInitialize()
        {
            try
            {
                // 方案1: 尝试直接通过完整路径查找
                GameObject target = GameObject.Find(HUD_PATH);
                if (target != null)
                {
                    _bulletTypeHUDTransform = target.transform;
                    _proceduralImage = _bulletTypeHUDTransform.GetComponent<Image>();

                    if (_proceduralImage != null)
                    {
                        _isInitialized = true;
                        Debug.Log($"[BulletHUDColorizer] 初始化成功（直接路径）");
                        SyncCurrentBulletState(true);
                        return;
                    }
                }

                // 方案2: 递归查找所有名为 "CurrentBulletType" 的对象
                GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name == "CurrentBulletType")
                    {
                        // 验证是否在正确的父级下（检查是否有BulletTypeHUD父节点）
                        Transform parent = obj.transform.parent;
                        if (parent != null && parent.name == "BulletTypeHUD")
                        {
                            _bulletTypeHUDTransform = obj.transform;
                            _proceduralImage = _bulletTypeHUDTransform.GetComponent<Image>();

                            if (_proceduralImage != null)
                            {
                                _isInitialized = true;
                                Debug.Log($"[BulletHUDColorizer] 初始化成功（递归查找），完整路径: {GetFullPath(obj.transform)}");
                                SyncCurrentBulletState(true);
                                return;
                            }
                        }
                    }
                }

                // 如果两种方案都失败，只在第一次输出警告
                if (Time.frameCount % 300 == 0) // 每5秒（60fps）输出一次
                {
                    Debug.LogWarning("[BulletHUDColorizer] 未找到 CurrentBulletType UI 元素，可能场景未加载或HUD结构已变化");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BulletHUDColorizer] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取GameObject的完整路径（用于调试）
        /// </summary>
        private string GetFullPath(Transform transform)
        {
            if (transform == null) return "";

            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private void SyncCurrentBulletState(bool paintImmediately)
        {
            var gun = CharacterMainControl.Main?.GetGun();
            if (gun == null || gun.GunItemSetting == null)
            {
                _lastGun = null;
                _lastBulletItem = null;
                _lastBulletTypeID = -1;
                _lastBulletQuality = -1;
                _pendingRecolorFrames = 0;
                if (paintImmediately)
                {
                    ResetColor();
                }
                return;
            }

            _lastGun = gun;
            Item currentBullet = gun.GunItemSetting.GetCurrentLoadedBullet();
            _lastBulletTypeID = currentBullet?.TypeID ?? -1;
            _lastBulletQuality = currentBullet?.Quality ?? -1;
            _lastBulletItem = currentBullet;

            if (paintImmediately)
            {
                _pendingRecolorFrames = POST_UPDATE_STABILIZE_FRAMES;
                UpdateColor(currentBullet);
            }
        }

        /// <summary>
        /// 根据子弹品质更新颜色
        /// </summary>
        private void UpdateColor(Item bullet, bool logChange = true)
        {
            if (_proceduralImage == null) return;

            if (bullet == null)
            {
                ResetColor();
                return;
            }

            try
            {
                // 获取子弹品质（使用int Quality，与轮盘保持一致）
                int quality = bullet.Quality;

                // 使用与轮盘相同的颜色映射方法
                Color tintColor = RarityColorProvider.GetTintByQuality(quality);

                // 设置 Image 颜色
                _proceduralImage.color = tintColor;

                if (logChange)
                {
                    Debug.Log($"[BulletHUDColorizer] 更新子弹HUD颜色: {bullet.DisplayName} (Q{quality}) -> {tintColor}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BulletHUDColorizer] 更新颜色失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置为默认颜色（白色）
        /// </summary>
        private void ResetColor()
        {
            if (_proceduralImage == null) return;

            try
            {
                _proceduralImage.color = Color.white;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BulletHUDColorizer] 重置颜色失败: {ex.Message}");
            }
        }

        private void HandleWillRenderCanvases()
        {
            if (_pendingRecolorFrames <= 0)
            {
                return;
            }

            if (!_isInitialized || _proceduralImage == null)
            {
                _pendingRecolorFrames = 0;
                return;
            }

            _pendingRecolorFrames--;

            Item bullet = _lastBulletItem;
            var gun = CharacterMainControl.Main?.GetGun();
            if (gun != null && gun.GunItemSetting != null)
            {
                bullet = gun.GunItemSetting.GetCurrentLoadedBullet();
            }

            UpdateColor(bullet, logChange: false);
        }

        /// <summary>
        /// 强制刷新颜色（用于手动触发）
        /// </summary>
        public void ForceRefresh()
        {
            _lastBulletTypeID = -1; // 重置状态，强制下次更新
            _lastBulletQuality = -1;
            _lastBulletItem = null;
            _pendingRecolorFrames = POST_UPDATE_STABILIZE_FRAMES;
        }

        public void Dispose()
        {
            LevelManager.OnLevelInitialized -= HandleLevelInitialized;
            Canvas.willRenderCanvases -= HandleWillRenderCanvases;
        }
    }
}
