using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    public static class PeepingHooks
    {
        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Peeping), "setPlay")]
        static void PeepingSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
