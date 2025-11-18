using HarmonyLib;
using ItemWheel.Integration.Compatibility;

namespace ItemWheel.Patches
{
    /// <summary>
    /// Prevents ShoulderSurfing (and the vanilla aim code) from consuming mouse input while the wheel is open.
    /// Only active when the Shoulder Surfing mod is present.
    /// </summary>
    [HarmonyPatch(typeof(InputManager), "SetAimInputUsingMouse")]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("com.didiv.ShoulderSurfing")]
    internal static class ShoulderSurfingInputPatch
    {
        static bool Prefix()
        {
            if (!ShoulderSurfingCompatibility.IsActive)
            {
                return true;
            }

            return !WheelInputGuard.IsActive;
        }
    }
}
