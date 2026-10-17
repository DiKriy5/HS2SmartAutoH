using HarmonyLib;
using System;

namespace HS2SmartAutoH.Hooks
{
    // Fix of the game (1.14.0, always on): SetMindJudge writes the four trait flags
    // (breasts, anal, vagina, kiss) from each newly counted position and so overwrites the earlier ones.
    // Only the last new position with a finish decided the bonus of traits 4..7.
    // Here the flags are kept: a flag that was set once in the scene stays set.
    // The game itself clears them at the end of the scene (HSceneFlagCtrl.EndProc).
    public static class TraitHooks
    {
        private static readonly string[] FlagNames = new string[] { "breasts", "anal", "vagina", "kiss" };

        [HarmonyPrefix, HarmonyPatch(typeof(HSceneFlagCtrl), "SetMindJudge")]
        static void KeepBefore(HSceneFlagCtrl __instance, out bool[] __state)
        {
            __state = null;
            try
            {
                bool[] judge = TCTrait.MindJudge(__instance);
                if (judge != null)
                {
                    __state = (bool[])judge.Clone();
                }
            }
            catch (Exception)
            {
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSceneFlagCtrl), "SetMindJudge")]
        static void KeepAfter(HSceneFlagCtrl __instance, bool[] __state)
        {
            try
            {
                bool[] judge = TCTrait.MindJudge(__instance);
                if (judge == null || __state == null)
                {
                    return;
                }
                // Boobjob positions have no breast mark in the game: count them as breasts (1.14.1)
                if (judge.Length > 0 && TCTrait.IsBoobjob(__instance.nowAnimationInfo))
                {
                    judge[0] = true;
                }
                string added = "";
                for (int i = 0; i < judge.Length && i < __state.Length; i++)
                {
                    if (judge[i] && !__state[i] && i < FlagNames.Length)
                    {
                        added += (added.Length > 0 ? ", " : "") + FlagNames[i];
                    }
                    judge[i] = judge[i] || __state[i];
                }
                if (added.Length > 0)
                {
                    HS2SmartAutoH.Instance.LogDetailed("[Trait] Scene now counts a finish in a position with: " + added);
                }
            }
            catch (Exception e)
            {
                HS2SmartAutoH.Instance.Log.LogWarning("[Trait] Could not keep the trait flags: " + e.Message);
            }
        }
    }
}
