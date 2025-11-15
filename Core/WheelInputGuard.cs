using System.Threading;

namespace ItemWheel
{
    /// <summary>
    /// Tracks whether any ItemWheel UI currently has exclusive mouse interaction.
    /// </summary>
    internal static class WheelInputGuard
    {
        private static int _activeCount;

        public static bool IsActive => _activeCount > 0;

        public static void OnWheelShown()
        {
            Interlocked.Increment(ref _activeCount);
        }

        public static void OnWheelHidden()
        {
            if (_activeCount == 0)
            {
                return;
            }
            Interlocked.Decrement(ref _activeCount);
        }

        public static void Reset()
        {
            _activeCount = 0;
        }
    }
}
