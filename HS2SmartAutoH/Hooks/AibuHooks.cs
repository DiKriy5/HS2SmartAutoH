using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    public static class AibuHooks
    {
        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Aibu), "setPlay")]
        static void AibuSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
