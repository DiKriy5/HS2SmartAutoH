using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    public static class MultiplayF1M2Hooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F1M2), "AutoStartProcTrigger")]
        static void FixF1M2WheelValue(bool _start, ref float _wheel)
        {
            _wheel = GeneralHooks.FixWheelValue(_wheel);
        }

        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F1M2), "setPlay")]
        static void MultiPlayF1M2SetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
