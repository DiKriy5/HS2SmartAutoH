using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    public static class SonyuHooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Sonyu), "AutoStartProcTrigger")]
        static void FixSonyuWheelValue(bool _start, ref float wheel)
        {
            wheel = GeneralHooks.FixWheelValue(wheel);
        }

        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Sonyu), "setPlay")]
        static void SonyuSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
