using ItemStatsSystem;
using QuickWheel.Core.Interfaces;
using ItemWheel.UI;

namespace ItemWheel
{
    /// <summary>
    /// Minimal item adapter that renders items with default WheelItemWithDecor visuals.
    /// </summary>
    internal sealed class SimpleItemAdapter : IWheelItemAdapter<Item>
    {
        public IWheelItem ToWheelItem(Item item)
        {
            if (item == null)
            {
                return null;
            }

            return new WheelItemWithDecor(item);
        }

        public Item FromWheelItem(IWheelItem item) => null;
    }
}
