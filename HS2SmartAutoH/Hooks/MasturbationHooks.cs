using AIChara;
using DG.Tweening;
using HarmonyLib;
using Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HS2SmartAutoH.Hooks
{
    public static class MasturbationHooks
    {

        [HarmonyPrefix, HarmonyPatch(typeof(Masturbation), "AutoStartProc")]
        static bool MasturbateAfterAnimChangeFix(int _start, Masturbation __instance)
        {
            if (_start == 0)
            {
                return true;
            }
            else if (Singleton<HSceneManager>.Instance.Hscene.ctrlAuto.IsChangeActionAtRestart())
            {
                Singleton<HSceneFlagCtrl>.Instance.isAutoActionChange = true;
                Singleton<HSceneManager>.Instance.Hscene.ctrlAuto.Reset();
                Singleton<HSceneManager>.Instance.Hscene.ctrlFlag.speed = 0f;
                Singleton<HSceneManager>.Instance.Hscene.ctrlFlag.loopType = 0;
                Singleton<HSceneManager>.Instance.Hscene.ctrlFlag.isNotCtrl = false;
                return false;
            }
            return true;
        }

        private static FieldInfo bAutoField = AccessTools.Field(typeof(Masturbation), "bAuto");

        [HarmonyPrefix, HarmonyPatch(typeof(Masturbation), "Proc")]
        static void MasturbateAutoFix(Masturbation __instance)
        {
            bAutoField.SetValue(__instance, (HS2SmartAutoH.Instance.GetCurrentModeControl() != 4 || Singleton<HSceneFlagCtrl>.Instance.initiative > 0));
        }

        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Masturbation), "setPlay")]
        static void MasturbationSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
