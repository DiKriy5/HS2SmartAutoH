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
using UnityEngine;

namespace HS2SmartAutoH.Hooks
{
    public static class SpnkingHooks
    {

        [HarmonyPostfix, HarmonyPatch(typeof(Spnking), "AfterWaitingAnimation")]
        internal static void SpankingAfterWaitingAnimationAutoSelector()
        {
            if (!TCPain.SpankingMayChange())
            {
                return;   // 1.15.1: spanking lasts until the pain time is up or she climaxes
            }
            bool changeAction = Singleton<HSceneManager>.Instance.Hscene.ctrlAuto.IsChangeActionAtRestart();
            if (changeAction)
            {
                Singleton<HSceneFlagCtrl>.Instance.isAutoActionChange = true;
                Singleton<HSceneManager>.Instance.Hscene.ctrlAuto.Reset();
                Singleton<HSceneManager>.Instance.Hscene.ctrlFlag.speed = 0f;
                Singleton<HSceneManager>.Instance.Hscene.ctrlFlag.loopType = 0;
                Singleton<HSceneManager>.Instance.Hscene.ctrlFlag.isNotCtrl = false;
            }
        }


        private static MethodInfo playMethodInfo = AccessTools.Method(typeof(Spnking), "setPlay");
        private static FieldInfo voiceFieldInfo = AccessTools.Field(typeof(Spnking), "voice");
        private static FieldInfo spriteFieldInfo = AccessTools.Field(typeof(Spnking), "sprite");
        private static FieldInfo upFeelFieldInfo = AccessTools.Field(typeof(Spnking), "upFeel");
        private static FieldInfo chaFemalesFieldInfo = AccessTools.Field(typeof(Spnking), "chaFemales");
        private static FieldInfo timeFeelUpFieldInfo = AccessTools.Field(typeof(Spnking), "timeFeelUp");
        private static FieldInfo backupFieldInfo = AccessTools.Field(typeof(Spnking), "backupFeel");
        private static FieldInfo backupFeelFieldInfo = AccessTools.Field(typeof(Spnking), "backupFeelFlag");
        private static FieldInfo randVoicePlaysFieldInfo = AccessTools.Field(typeof(Spnking), "randVoicePlays");
        private static FieldInfo isAddFieldInfo = AccessTools.Field(typeof(Spnking), "isAddFeel");


        [HarmonyPrefix, HarmonyPatch(typeof(Spnking), "SpankingProc")]
        static bool SpankingAutoProc(int _state, Spnking __instance)
        {

            HSceneFlagCtrl ctrlFlag = Singleton<HSceneFlagCtrl>.Instance;
            if (ctrlFlag.initiative == 0)
            {
                return true;
            }
            HVoiceCtrl voice = (HVoiceCtrl)voiceFieldInfo.GetValue(__instance);
            HSceneSprite sprite = (HSceneSprite)spriteFieldInfo.GetValue(__instance);
            ChaControl[] chaFemales = (ChaControl[])chaFemalesFieldInfo.GetValue(__instance);
            ShuffleRand[] randVoicePlays = (ShuffleRand[])randVoicePlaysFieldInfo.GetValue(__instance);

            if (!ctrlFlag.stopFeelFemale)
            {
                ctrlFlag.feel_f = Mathf.Clamp01(ctrlFlag.feel_f - ctrlFlag.guageDecreaseRate * Time.deltaTime);
            }
            if (_state == 1 && ctrlFlag.feel_f < 0.5f)
            {
                playMethodInfo.Invoke(__instance, new object[] { "WIdle", false });
                voice.HouchiTime = 0f;
                return true;
            }
            if (!GeneralHooks.SpankTimer.IsTime())
            {
                return false;
            }
            else
            {
                GeneralHooks.SpankTimer.Reset();
            }
            if (voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.voice || voice.nowVoices[0].state == HVoiceCtrl.VoiceKind.startVoice)
            {
                Voice.Stop(ctrlFlag.voice.voiceTrs[0]);
                voice.ResetVoice();
            }

            string scene = "D_Action";
            switch (_state)
            {
                case 1:
                    scene = "SAction";
                    break;
                case 0:
                    scene = "WAction";
                    break;
            }

            playMethodInfo.Invoke(__instance, new object[] { scene, false });

            upFeelFieldInfo.SetValue(__instance, true);
            float value = Mathf.Clamp01(chaFemales[0].siriAkaRate + ctrlFlag.siriakaAddRate);
            chaFemales[0].ChangeSiriAkaRate(value);
            timeFeelUpFieldInfo.SetValue(__instance, 0f);
            backupFieldInfo.SetValue(__instance, ctrlFlag.feel_f);
            backupFeelFieldInfo.SetValue(__instance, ctrlFlag.feelPain);
            ctrlFlag.isNotCtrl = false;
            if (randVoicePlays[0].Get() == 0)
            {
                ctrlFlag.voice.playVoices[0] = true;
            }
            ctrlFlag.voice.playShorts[0] = 0;

            bool isAddFeel = (bool)isAddFieldInfo.GetValue(__instance);
            if (!isAddFeel && ctrlFlag.feel_f >= 0.70f)
            {
                SpankingAfterWaitingAnimationAutoSelector();
            }

            return false;
        }


        // Remembers the animation state (Idle, WLoop, SLoop, OLoop, ...) for the voice logic
        [HarmonyPrefix, HarmonyPatch(typeof(Spnking), "setPlay")]
        static void SpnkingSetPlay(string _playAnimation)
        {
            AnimationStatus.PlayingAnimation = _playAnimation;
        }
    }
}
