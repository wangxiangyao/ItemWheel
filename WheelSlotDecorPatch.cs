using System;
using HarmonyLib;
using QuickWheel.Core.Interfaces;
using QuickWheel.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ItemWheel
{
    [HarmonyPatch]
    internal static class WheelSlotDecorPatch
    {
        private static Transform EnsureChild(this Transform parent, string name)
        {
            if (parent == null) return null;
            var t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // 涓嶅湪鍒涘缓闃舵鏀?UI锛岄伩鍏嶆棭鏈熺敓鍛藉懆鏈熺殑绌哄紩鐢?        
                }

                // 璇诲彇瑁呴グ鏁版嵁
                var decor = wheelItem as IWheelItemDecor;
                if (decor == null)
                {
                    tintImg.color = new Color(1, 1, 1, 0);
                    right.gameObject.SetActive(false);
                    durBg.gameObject.SetActive(false);
                    durFill.gameObject.SetActive(false);
                    return;
                }

                // 绋€鏈夊害搴曡壊
                var tintColor = decor.GetRarityTint();
                tintImg.color = tintColor.HasValue ? tintColor.Value : new Color(1, 1, 1, 0);

                // 鍙充晶鏂囧瓧锛氭暟閲?搴撳瓨
                var rightText = decor.GetRightText();
                if (!string.IsNullOrEmpty(rightText))
                {
                    rightTxt.text = rightText;
                    rightTxt.alignment = TextAnchor.LowerRight;
                    right.gameObject.SetActive(true);
                }
                else
                {
                    right.gameObject.SetActive(false);
                }

                // 鑰愪箙鏉?                var d = decor.GetDurability01();
                if (d.HasValue && d.Value > 0f)
                {
                    durBg.gameObject.SetActive(true);
                    durFill.gameObject.SetActive(true);
                    float pct = Mathf.Clamp01(d.Value);
                    // 浠ラ敋鐐硅〃绀烘瘮渚嬶紝浠庡彸鍚戝乏缂╃煭锛堝崰鍙冲崐鍖哄煙锛?                    durFillRect.anchorMin = new Vector2(1f - 0.5f * pct, durFillRect.anchorMin.y);
                    durFillRect.anchorMax = new Vector2(1f, durFillRect.anchorMax.y);
                }
                else
                {
                    durBg.gameObject.SetActive(false);
                    durFill.gameObject.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WheelDecor] SetData postfix failed: {ex.Message}");
            }
        }
    }
}



