using UnityEngine;
using UnityEngine.EventSystems;

namespace ItemWheel.Utils
{
    /// <summary>
    /// UI焦点检测器
    /// 用于检测UI输入框是否获得焦点，避免快捷键与UI输入冲突
    /// </summary>
    public static class UIFocusDetector
    {
        /// <summary>
        /// 检测当前是否有UI输入框获得焦点
        /// </summary>
        /// <returns>true表示有输入框获得焦点，应禁用快捷键</returns>
        public static bool IsInputFieldFocused()
        {
            try
            {
                // 1. 检查EventSystem是否存在
                var eventSystem = EventSystem.current;
                if (eventSystem == null)
                {
                    return false;
                }

                // 2. 获取当前选中的GameObject
                var selectedObject = eventSystem.currentSelectedGameObject;
                if (selectedObject == null)
                {
                    return false;
                }

                // 3. 检查是否是InputField（UnityEngine.UI）
                var inputField = selectedObject.GetComponent<UnityEngine.UI.InputField>();
                if (inputField != null && inputField.isFocused)
                {
                    return true;
                }

                // 4. 检查是否是TMP_InputField（TextMeshPro）
                // 使用反射避免硬依赖TMPro程序集
                var tmpInputField = selectedObject.GetComponent("TMP_InputField");
                if (tmpInputField != null)
                {
                    // 通过反射获取isFocused属性
                    var isFocusedProperty = tmpInputField.GetType().GetProperty("isFocused");
                    if (isFocusedProperty != null)
                    {
                        var isFocused = (bool)isFocusedProperty.GetValue(tmpInputField);
                        return isFocused;
                    }

                    // 备用方案：只要选中了TMP_InputField就认为有焦点
                    return true;
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UIFocusDetector] 检测输入框焦点失败: {ex.Message}");
                return false;
            }
        }
    }
}
