using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    public static class LesHooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Les), "AutoStartProcTrigger")]
        static void FixLesWheelValue(bool _start, ref float _wheel)
        {
            _wheel = GeneralHooks.FixWheelValue(_wheel);
        }

        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Les), "setPlay")]
        static void LesSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
