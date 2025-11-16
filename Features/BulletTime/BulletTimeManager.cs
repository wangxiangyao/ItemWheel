using UnityEngine;

namespace ItemWheel.Features.BulletTime
{
    /// <summary>
    /// 子弹时间管理器
    /// 负责在轮盘开启时减慢游戏速度，关闭时恢复正常
    /// </summary>
    public class BulletTimeManager
    {
        // ==================== 字段 ====================

        private bool _isEnabled = false;
        private float _originalTimeScale = 1f;
        private float _originalAudioPitch = 1f;

        // 配置
        private float _targetTimeScale = 0.3f;
        private float _transitionSpeed = 5f;
        private bool _adjustAudioPitch = true;

        // 物理时间步长
        private const float NormalFixedDeltaTime = 0.02f;  // Unity默认值
        private float _targetFixedDeltaTime = 0.01f;

        // ==================== 公共方法 ====================

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="targetTimeScale">目标时间缩放比例 (0.1-1.0)</param>
        /// <param name="transitionSpeed">过渡速度</param>
        /// <param name="adjustAudioPitch">是否调整音效音调</param>
        public BulletTimeManager(float targetTimeScale = 0.3f, float transitionSpeed = 5f, bool adjustAudioPitch = true)
        {
            _targetTimeScale = Mathf.Clamp(targetTimeScale, 0.01f, 1f);
            _transitionSpeed = Mathf.Max(transitionSpeed, 0.1f);
            _adjustAudioPitch = adjustAudioPitch;

            // 记录原始值
            _originalTimeScale = Time.timeScale;
            _originalAudioPitch = GetCurrentAudioPitch();
        }

        /// <summary>
        /// 启用子弹时间
        /// </summary>
        public void Enable()
        {
            if (_isEnabled) return;

            _isEnabled = true;
            _originalTimeScale = Time.timeScale;
            _originalAudioPitch = GetCurrentAudioPitch();

            // 立即设置时间缩放
            Time.timeScale = _targetTimeScale;
            Time.fixedDeltaTime = _targetFixedDeltaTime;
        }

        /// <summary>
        /// 禁用子弹时间
        /// </summary>
        public void Disable()
        {
            if (!_isEnabled) return;

            _isEnabled = false;

            // 立即恢复正常时间
            Time.timeScale = 1f;
            Time.fixedDeltaTime = NormalFixedDeltaTime;
        }

        /// <summary>
        /// 每帧更新 - 确保时间缩放生效（游戏可能会重置）
        /// 必须在Update()中调用
        /// </summary>
        public void Update()
        {
            // 🆕 检查游戏是否暂停（timeScale=0），如果暂停了就不要覆盖
            if (Mathf.Abs(Time.timeScale) < 0.01f)
            {
                // 游戏暂停中（ESC菜单等），不要干扰
                return;
            }

            // 计算目标值
            float targetTimeScale = _isEnabled ? _targetTimeScale : 1f;
            float targetFixedDelta = _isEnabled ? _targetFixedDeltaTime : NormalFixedDeltaTime;

            // 检查是否被游戏重置，如果是则重新设置
            bool timeScaleChanged = Mathf.Abs(Time.timeScale - targetTimeScale) > 0.01f;
            bool fixedDeltaChanged = Mathf.Abs(Time.fixedDeltaTime - targetFixedDelta) > 0.001f;

            if (timeScaleChanged || fixedDeltaChanged)
            {
                Time.timeScale = targetTimeScale;
                Time.fixedDeltaTime = targetFixedDelta;
            }

            // 调整音效
            if (_adjustAudioPitch)
            {
                UpdateAudioPitch(Time.timeScale);
            }
        }

        /// <summary>
        /// 立即恢复正常时间（用于紧急情况）
        /// </summary>
        public void ForceRestore()
        {
            _isEnabled = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = NormalFixedDeltaTime;
            SetAudioPitch(1f);
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public bool IsActive => _isEnabled;

        // ==================== 私有方法 ====================

        /// <summary>
        /// 更新音频音调
        /// </summary>
        private void UpdateAudioPitch(float timeScale)
        {
            // 音调跟随时间缩放
            // 例如：timeScale=0.3时，pitch也为0.3，音效变低沉
            float targetPitch = timeScale;
            float currentPitch = GetCurrentAudioPitch();
            float newPitch = Mathf.Lerp(currentPitch, targetPitch, Time.unscaledDeltaTime * _transitionSpeed);

            SetAudioPitch(newPitch);
        }

        /// <summary>
        /// 获取当前音频音调
        /// </summary>
        private float GetCurrentAudioPitch()
        {
            // Unity的AudioListener没有直接的pitch属性
            // 这里需要通过AudioSource来控制，或者使用AudioMixer
            // 简化实现：假设默认为1.0
            // 如果游戏有AudioManager，需要调用其接口

            // TODO: 根据实际游戏的音频系统调整
            // 如果游戏使用了AudioMixer，可以通过Mixer.SetFloat("Pitch", value)

            return 1f;
        }

        /// <summary>
        /// 设置音频音调
        /// </summary>
        private void SetAudioPitch(float pitch)
        {
            // Unity没有全局音频Pitch设置
            // 通常有几种方案：
            // 1. 修改所有AudioSource的pitch
            // 2. 使用AudioMixer的Pitch Shifter效果
            // 3. 游戏可能有自己的AudioManager

            // 简化实现：通过AudioListener查找所有AudioSource
            // 注意：这个方法比较耗性能，建议在实际项目中优化

            if (!_adjustAudioPitch) return;

            // 方案1：修改所有活跃的AudioSource（性能较低，但兼容性好）
            AudioSource[] sources = Object.FindObjectsOfType<AudioSource>();
            foreach (var source in sources)
            {
                if (source != null && source.isPlaying)
                {
                    source.pitch = pitch;
                }
            }

            // 方案2：如果游戏有AudioMixer（需要确认游戏是否使用）
            // AudioMixer mixer = ...;
            // mixer.SetFloat("MasterPitch", pitch);
        }
    }
}
