using HarmonyLib;

namespace HS2SmartAutoH.Hooks
{
    // Finish in the positions for three (1.13.0). The game's code there has the same finish commands
    // as the normal positions (sex: inside / outside / together, service: drink / spit out / outside),
    // so the same finish logic of Auto mode is used.
    public static class MultiFinishHooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F2M1), "AutoOLoopProc")]
        static void F2M1SexFinish(MultiPlay_F2M1 __instance)
        {
            SonyuFinishHooks.Run(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F1M2), "AutoOLoopProc")]
        static void F1M2SexFinish(MultiPlay_F1M2 __instance)
        {
            SonyuFinishHooks.Run(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F2M1), "AutoOLoopHoushiProc")]
        static void F2M1ServiceFinish()
        {
            HoushiHooks.Run();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MultiPlay_F1M2), "AutoOLoopHoushiProc")]
        static void F1M2ServiceFinish()
        {
            HoushiHooks.Run();
        }
    }
}
