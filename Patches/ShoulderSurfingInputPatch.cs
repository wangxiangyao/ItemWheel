using HarmonyLib;

namespace ItemWheel.Patches
{
    /// <summary>
    /// Prevents ShoulderSurfing (and the vanilla aim code) from consuming mouse input while the wheel is open.
    /// </summary>
    [HarmonyPatch(typeof(InputManager), "SetAimInputUsingMouse")]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("com.didiv.ShoulderSurfing")]
    internal static class ShoulderSurfingInputPatch
    {
        static bool Prefix()
        {
            return !WheelInputGuard.IsActive;
        }
    }
}
