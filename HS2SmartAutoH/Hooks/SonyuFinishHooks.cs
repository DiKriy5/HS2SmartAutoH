using HarmonyLib;
using Manager;
using System;
using System.Collections.Generic;

namespace HS2SmartAutoH.Hooks
{
    // Finish in SEX (Auto mode), modelled on BetterHScenes' auto finish:
    // whoever reaches the top first waits for the partner (the plugin's Update holds the gauges at
    // SyncHoldLevel), and when both are at the top the chosen finish command is given here.
    //
    // Order of checks in the game's Sonyu.AutoOLoopProc (read from the game code):
    //   feel_f >= 1                     -> she finishes alone
    //   click FinishInSide, or Auto and feel_m >= 1 -> he finishes inside (needs canInside)
    //   click FinishOutSide             -> he finishes outside (feel_m >= 0.75)
    //   click FinishSame                -> both finish together (feel_m >= 0.75)
    // So both gauges must stay below 1 when a command is given, or the game takes another branch.
    public static class SonyuFinishHooks
    {
        private static bool logged = false;

        // Seconds to wait for the command to start before another finish type is tried
        public const float FallbackDelay = 5f;
        private const float CommandLevel = 0.95f;

        // Time the finish was requested (-1 = no finish in progress).
        // While a finish is in progress the gauges are not held, or the hold would cancel it.
        public static float FinishRequestedAt = -1f;
        private static bool fallbackGiven = false;

        public static bool FinishInProgress
        {
            get { return FinishRequestedAt >= 0f; }
        }

        public static void ResetFinish()
        {
            FinishRequestedAt = -1f;
            fallbackGiven = false;
        }

        // Called every frame from the plugin's Update while in SEX in Auto mode
        public static void TrackFinish(HSceneFlagCtrl flags)
        {
            if (!FinishInProgress)
            {
                return;
            }
            if (flags.feel_m < 0.5f)
            {
                HS2SmartAutoH.Instance.LogDetailed("[Finish] Finish completed after " + (UnityEngine.Time.time - FinishRequestedAt).ToString("0.0") + " s");
                ResetFinish();
            }
            else if (UnityEngine.Time.time - FinishRequestedAt > FallbackDelay * 4f)
            {
                // Safety net: nothing happened for a long time, go back to normal behaviour
                HS2SmartAutoH.Instance.LogDetailed("[Finish] No finish happened - reset");
                ResetFinish();
            }
        }

        private static string KindName(HSceneFlagCtrl.ClickKind kind)
        {
            switch (kind)
            {
                case HSceneFlagCtrl.ClickKind.FinishInSide: return "inside";
                case HSceneFlagCtrl.ClickKind.FinishOutSide: return "outside";
                case HSceneFlagCtrl.ClickKind.FinishSame: return "together";
                default: return kind.ToString();
            }
        }

        private static HSceneFlagCtrl.ClickKind ChooseKind(SexFinishType type, bool canInside)
        {
            switch (type)
            {
                case SexFinishType.Inside:
                    return canInside ? HSceneFlagCtrl.ClickKind.FinishInSide : HSceneFlagCtrl.ClickKind.FinishOutSide;
                case SexFinishType.Outside:
                    return HSceneFlagCtrl.ClickKind.FinishOutSide;
                case SexFinishType.Random:
                    List<HSceneFlagCtrl.ClickKind> kinds = new List<HSceneFlagCtrl.ClickKind>();
                    kinds.Add(HSceneFlagCtrl.ClickKind.FinishSame);
                    kinds.Add(HSceneFlagCtrl.ClickKind.FinishOutSide);
                    if (canInside)
                    {
                        kinds.Add(HSceneFlagCtrl.ClickKind.FinishInSide);
                    }
                    return kinds[UnityEngine.Random.Range(0, kinds.Count)];
                default:
                    return HSceneFlagCtrl.ClickKind.FinishSame;
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Sonyu), "AutoOLoopProc")]
        static void SonyuCumFix(Sonyu __instance)
        {
            Run(__instance);
        }

        // Also used for the sex positions for three (MultiFinishHooks): the game's code has the same finish commands there
        public static void Run(object __instance)
        {
            try
            {
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                if (flags == null || flags.initiative != 2)
                {
                    return;
                }

                bool canInside = true;
                try
                {
                    Traverse field = Traverse.Create(__instance).Field("canInside");
                    if (field.FieldExists())
                    {
                        canInside = field.GetValue<bool>();
                    }
                }
                catch (Exception)
                {
                    canInside = true;
                }

                SexFinishType type = TCTrait.SexFinish(true);
                if (type == SexFinishType.Game)
                {
                    return;
                }

                if (FinishInProgress)
                {
                    // The command did not start: try outside (possible in almost every position)
                    if (!fallbackGiven && UnityEngine.Time.time - FinishRequestedAt > FallbackDelay && flags.feel_m >= 0.75f)
                    {
                        fallbackGiven = true;
                        flags.feel_m = Math.Min(flags.feel_m, CommandLevel);
                        flags.feel_f = Math.Min(flags.feel_f, CommandLevel);
                        flags.click = HSceneFlagCtrl.ClickKind.FinishOutSide;
                        HS2SmartAutoH.Instance.LogDetailed("[Finish] Finish did not start in " + FallbackDelay + " s - trying outside (SEX)");
                    }
                    return;
                }

                // Both are waiting at the top: give the chosen finish
                float hold = HS2SmartAutoH.SyncHoldLevel;
                if (flags.feel_m >= hold && flags.feel_f >= hold)
                {
                    HSceneFlagCtrl.ClickKind kind = ChooseKind(type, canInside);
                    flags.feel_m = Math.Min(flags.feel_m, CommandLevel);
                    flags.feel_f = Math.Min(flags.feel_f, CommandLevel);
                    flags.click = kind;
                    FinishRequestedAt = UnityEngine.Time.time;
                    fallbackGiven = false;
                    string note = (type == SexFinishType.Inside && !canInside) ? " (inside is not possible in this position)" : "";
                    HS2SmartAutoH.Instance.LogDetailed(string.Format("[Finish] Both at the top - setting {0}, finish {1}{2} (SEX)", type, KindName(kind), note));
                }
            }
            catch (Exception e)
            {
                if (!logged)
                {
                    logged = true;
                    HS2SmartAutoH.Instance.Log.LogWarning("[Finish] SEX finish error (logged once): " + e.Message);
                }
            }
        }
    }
}
