using HarmonyLib;
using QuickWheel.UI;
using QuickWheel.Core.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace ItemWheel
{
    // 极小变更：仅在 SetData 后置将 Label 设为右下对齐，配合多行文本实现“上行数量/百分比、下行名称”。
    [HarmonyPatch]
    internal static class LabelAlignPatch
    {
        [HarmonyPatch(typeof(WheelSlotDisplay), nameof(WheelSlotDisplay.SetData))]
        [HarmonyPostfix]
        private static void SetData_Postfix(WheelSlotDisplay __instance, IWheelItem wheelItem)
        {
            if (__instance == null) return;
            try
            {
                var t = __instance.transform.Find("Label");
                if (t == null) return;
                var txt = t.GetComponent<Text>();
                if (txt == null) return;
                txt.alignment = TextAnchor.LowerRight; // 右下对齐，多行时底行为名称
                txt.supportRichText = true;            // 启用富文本（用于名称上色）
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                txt.verticalOverflow = VerticalWrapMode.Overflow; // 允许两行完全显示
                txt.lineSpacing = 0.9f;               // 略微收紧行距，避免被裁切

                // 扩大 Label 文本区域高度（仅在底部区域内增加一些高度）
                var rt = t as RectTransform;
                if (rt != null)
                {
                    // 将 anchorMax.y 从 ~0.25 提升到 ~0.36，给两行留足空间
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0.36f);
                    rt.offsetMin = new Vector2(6f, 2f);
                    rt.offsetMax = new Vector2(-6f, 0f);
                }
            }
            catch { }
        }
    }
}
