using ItemStatsSystem;
using ItemWheel.Data;

namespace ItemWheel.Handlers
{
    /// <summary>
    /// 物品处理器接口
    /// 负责处理特定类别物品的使用、选择等逻辑
    /// </summary>
    public interface IItemHandler
    {
        /// <summary>
        /// 处理器负责的物品类别
        /// </summary>
        ItemWheelSystem.ItemWheelCategory Category { get; }

        /// <summary>
        /// 使用物品
        /// </summary>
        /// <param name="item">要使用的物品</param>
        /// <param name="character">角色</param>
        /// <param name="wheel">轮盘上下文</param>
        void UseItem(Item item, CharacterMainControl character, CategoryWheel wheel);

        /// <summary>
        /// 物品被选中回调（点击或hover确认）
        /// </summary>
        /// <param name="item">选中的物品</param>
        /// <param name="index">槽位索引</param>
        /// <param name="wheel">轮盘上下文</param>
        void OnItemSelected(Item item, int index, CategoryWheel wheel);

        /// <summary>
        /// 轮盘显示前回调
        /// 用于初始化状态或执行特殊逻辑
        /// </summary>
        /// <param name="wheel">轮盘上下文</param>
        void OnWheelShown(CategoryWheel wheel);

        /// <summary>
        /// 获取首选选中索引
        /// 用于自动选择最合适的物品
        /// </summary>
        /// <param name="wheel">轮盘上下文</param>
        /// <returns>首选索引，-1表示无首选</returns>
        int GetPreferredIndex(CategoryWheel wheel);
    }
}
