using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ItemWheel.ItemWheelSystem;  // 🔧 导入嵌套枚举

namespace ItemWheel
{
    /// <summary>
    /// 轮盘映射持久化
    /// 保存和加载轮盘位置→背包位置的映射关系
    /// </summary>
    public class WheelMappingPersistence
    {
        private readonly string _saveFilePath;

        /// <summary>
        /// 持久化数据结构
        /// </summary>
        [Serializable]
        public class MappingData
        {
            /// <summary>
            /// 医疗轮盘映射（轮盘位置0-7 → 背包位置）
            /// </summary>
            public int[] MedicalMapping = new int[8];

            /// <summary>
            /// 刺激剂轮盘映射
            /// </summary>
            public int[] StimMapping = new int[8];

            /// <summary>
            /// 食物轮盘映射
            /// </summary>
            public int[] FoodMapping = new int[8];

            /// <summary>
            /// 爆炸物轮盘映射
            /// </summary>
            public int[] ExplosiveMapping = new int[8];

            public MappingData()
            {
                // 初始化为-1（空位）
                Array.Fill(MedicalMapping, -1);
                Array.Fill(StimMapping, -1);
                Array.Fill(FoodMapping, -1);
                Array.Fill(ExplosiveMapping, -1);
            }
        }

        public WheelMappingPersistence(string modPath)
        {
            // 保存路径：<ModPath>/wheel_mappings.json
            _saveFilePath = Path.Combine(modPath, "wheel_mappings.json");
            Debug.Log($"[WheelMappingPersistence] Save path: {_saveFilePath}");
        }

        /// <summary>
        /// 保存映射到文件
        /// </summary>
        public void Save(Dictionary<ItemWheelCategory, int[]> mappings)
        {
            if (mappings == null)
            {
                Debug.LogError("[WheelMappingPersistence] Mappings cannot be null");
                return;
            }

            try
            {
                var data = new MappingData();

                // 复制映射数据
                if (mappings.TryGetValue(ItemWheelCategory.Medical, out var medicalMap))
                    Array.Copy(medicalMap, data.MedicalMapping, Math.Min(medicalMap.Length, 8));

                if (mappings.TryGetValue(ItemWheelCategory.Stim, out var stimMap))
                    Array.Copy(stimMap, data.StimMapping, Math.Min(stimMap.Length, 8));

                if (mappings.TryGetValue(ItemWheelCategory.Food, out var foodMap))
                    Array.Copy(foodMap, data.FoodMapping, Math.Min(foodMap.Length, 8));

                if (mappings.TryGetValue(ItemWheelCategory.Explosive, out var explosiveMap))
                    Array.Copy(explosiveMap, data.ExplosiveMapping, Math.Min(explosiveMap.Length, 8));

                // 序列化为JSON
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(_saveFilePath, json);

                Debug.Log($"[WheelMappingPersistence] ✓ Saved mappings to: {_saveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WheelMappingPersistence] ✗ Failed to save: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载映射
        /// </summary>
        public Dictionary<ItemWheelCategory, int[]> Load()
        {
            var mappings = new Dictionary<ItemWheelCategory, int[]>();

            try
            {
                if (!File.Exists(_saveFilePath))
                {
                    Debug.Log($"[WheelMappingPersistence] No saved mappings found at: {_saveFilePath}");
                    return null;
                }

                // 读取JSON
                string json = File.ReadAllText(_saveFilePath);
                var data = JsonUtility.FromJson<MappingData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[WheelMappingPersistence] Failed to parse JSON");
                    return null;
                }

                // 复制映射数据
                mappings[ItemWheelCategory.Medical] = (int[])data.MedicalMapping.Clone();
                mappings[ItemWheelCategory.Stim] = (int[])data.StimMapping.Clone();
                mappings[ItemWheelCategory.Food] = (int[])data.FoodMapping.Clone();
                mappings[ItemWheelCategory.Explosive] = (int[])data.ExplosiveMapping.Clone();

                Debug.Log($"[WheelMappingPersistence] ✓ Loaded mappings from: {_saveFilePath}");
                LogMappings(mappings);

                return mappings;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WheelMappingPersistence] ✗ Failed to load: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查是否存在保存文件
        /// </summary>
        public bool HasSavedMappings()
        {
            return File.Exists(_saveFilePath);
        }

        /// <summary>
        /// 删除保存文件
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    File.Delete(_saveFilePath);
                    Debug.Log($"[WheelMappingPersistence] Deleted: {_saveFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WheelMappingPersistence] Failed to delete: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录映射信息（调试用）
        /// </summary>
        private void LogMappings(Dictionary<ItemWheelCategory, int[]> mappings)
        {
            foreach (var kvp in mappings)
            {
                string mappingStr = string.Join(", ", kvp.Value);
                Debug.Log($"[WheelMappingPersistence]   {kvp.Key}: [{mappingStr}]");
            }
        }
    }
}
