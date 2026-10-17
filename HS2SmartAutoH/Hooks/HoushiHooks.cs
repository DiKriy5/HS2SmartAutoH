using AIChara;
using DG.Tweening;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HS2SmartAutoH.Hooks
{
    public static class HoushiHooks
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Houshi), "AutoStartProcTrigger")]
        static void FixHoushiWheelValue(bool _start, ref float _wheel)
        {
            _wheel = GeneralHooks.FixWheelValue(_wheel);
        }

        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Houshi), "setPlay")]
        static void HoushiSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }

        // SERVICE finish (Auto mode). Order in the game's Houshi.AutoOLoopProc:
        //   click FinishOutSide, or Auto and feel_m >= 1 -> outside
        //   click FinishDrink / FinishVomit (only if the position allows it) -> in the mouth
        // So Drink / Vomit are given at 98% with the gauge kept below 1, once per finish.
        // If the position does not take it, the gauge reaches 1 and the game finishes outside.
        private static bool serviceCommandGiven = false;
        public static bool CommandGiven { get { return serviceCommandGiven; } }

        public static void ResetServiceFinish()
        {
            serviceCommandGiven = false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Houshi), "AutoOLoopProc")]
        static void HoushiCumFix(Houshi __instance)
        {
            Run();
        }

        // Also used for the service positions for three (MultiFinishHooks)
        public static void Run()
        {
            HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
            if (flags == null || flags.initiative != 2)
            {
                return;
            }
            if (flags.feel_m < 0.5f)
            {
                serviceCommandGiven = false; // previous finish is over
            }
            ServiceFinishType type = HS2SmartAutoH.ServiceFinish.Value;
            if (serviceCommandGiven || flags.feel_m < HS2SmartAutoH.SyncHoldLevel)
            {
                return;
            }
            if (type == ServiceFinishType.Random)
            {
                ServiceFinishType[] options = new ServiceFinishType[] { ServiceFinishType.Drink, ServiceFinishType.Vomit, ServiceFinishType.Outside };
                type = options[UnityEngine.Random.Range(0, options.Length)];
            }
            serviceCommandGiven = true;
            switch (type)
            {
                case ServiceFinishType.Drink:
                    flags.feel_m = Math.Min(flags.feel_m, 0.95f);
                    flags.click = HSceneFlagCtrl.ClickKind.FinishDrink;
                    break;
                case ServiceFinishType.Vomit:
                    flags.feel_m = Math.Min(flags.feel_m, 0.95f);
                    flags.click = HSceneFlagCtrl.ClickKind.FinishVomit;
                    break;
                default:
                    flags.click = HSceneFlagCtrl.ClickKind.FinishOutSide;
                    break;
            }
            HS2SmartAutoH.Instance.LogDetailed("[Finish] SERVICE finish: " + type + " (if the position does not allow it, outside)");
        }
    }
}
