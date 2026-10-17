using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    public static class MultiplayF2M1Hooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F2M1), "AutoStartProcTrigger")]
        static void FixF2M1WheelValue(bool _start, ref float wheel)
        {
            wheel = GeneralHooks.FixWheelValue(wheel);
        }

        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F2M1), "setPlay")]
        static void MultiPlayF2M1SetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
