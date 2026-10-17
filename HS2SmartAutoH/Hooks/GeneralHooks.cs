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

namespace HS2SmartAutoH
{

    public static class GeneralHooks
    {

        public static HAutoCtrl.AutoTime SpankTimer;

        [HarmonyPrefix, HarmonyPatch(typeof(HSceneManager), "Start")]
        static void SetupHResourceTables(HSceneManager __instance)
        {
            HAutoCtrl.HAutoInfo autoInfo = new HAutoCtrl.HAutoInfo();
            autoInfo.start.minmax = new Vector2(HS2SmartAutoH.StartWaitMin.Value, HS2SmartAutoH.StartWaitMax.Value);
            autoInfo.start.time = (HS2SmartAutoH.StartWaitMin.Value + HS2SmartAutoH.StartWaitMax.Value) / 2;
            autoInfo.reStart.minmax = new Vector2(HS2SmartAutoH.RestartWaitMin.Value, HS2SmartAutoH.RestartWaitMax.Value);
            autoInfo.reStart.time = (HS2SmartAutoH.RestartWaitMin.Value + HS2SmartAutoH.RestartWaitMax.Value) / 2;
            autoInfo.speed.minmax = new Vector2(3, 8);
            autoInfo.speed.time = 5;
            autoInfo.lerpTimeSpeed = 2f;
            autoInfo.loopChange.minmax = new Vector2(HS2SmartAutoH.LoopChangeWaitMin.Value, HS2SmartAutoH.LoopChangeWaitMax.Value);
            autoInfo.loopChange.time = (HS2SmartAutoH.LoopChangeWaitMax.Value + HS2SmartAutoH.LoopChangeWaitMin.Value) / 2;
            if (HS2SmartAutoH.ChangePositionOnlyAfterFinish.Value)
            {
                // The mid-act position timer never runs out, so positions change only after a finish
                autoInfo.motionChange.minmax = new Vector2(99999, 99999);
                autoInfo.motionChange.time = 99999;
            }
            else
            {
                autoInfo.motionChange.minmax = new Vector2(HS2SmartAutoH.PositionChangeWaitMin.Value, HS2SmartAutoH.PositionChangeWaitMax.Value);
                autoInfo.motionChange.time = (HS2SmartAutoH.PositionChangeWaitMax.Value + HS2SmartAutoH.PositionChangeWaitMin.Value) / 2;
            }
            autoInfo.rateWeakLoop = 50;
            autoInfo.rateHit = HS2SmartAutoH.SweetSpotChance.Value;
            autoInfo.rateAddMotionChange = HS2SmartAutoH.PreferenceBonus.Value;
            autoInfo.rateRestartMotionChange = HS2SmartAutoH.ChanceToRestartPosition.Value;
            autoInfo.pull.minmax = new Vector2(HS2SmartAutoH.PulloutWaitMin.Value, HS2SmartAutoH.PulloutWaitMax.Value);
            autoInfo.pull.time = (HS2SmartAutoH.PulloutWaitMin.Value + HS2SmartAutoH.PulloutWaitMax.Value) / 2;
            autoInfo.rateInsertPull = HS2SmartAutoH.InsertedChanceToPullOut.Value;
            autoInfo.rateNotInsertPull = HS2SmartAutoH.NonInsertedChanceToPullOut.Value;

            HAutoCtrl.AutoLeaveItToYou autoLeaveItToYou = new HAutoCtrl.AutoLeaveItToYou();
            autoLeaveItToYou.time.minmax = new Vector2(30, 50);
            autoLeaveItToYou.time.Reset();
            autoLeaveItToYou.baseTime = autoLeaveItToYou.time.minmax;
            autoLeaveItToYou.rate = 50;

            HSceneManager.HResourceTables.HAutoInfo = autoInfo;
            HSceneManager.HResourceTables.HAutoLeaveItToYou = autoLeaveItToYou;

            SpankTimer = new HAutoCtrl.AutoTime();
            SpankTimer.minmax = new Vector2(HS2SmartAutoH.SpankingWaitMin.Value, HS2SmartAutoH.SpankingWaitMax.Value);
            SpankTimer.time = (HS2SmartAutoH.SpankingWaitMin.Value + HS2SmartAutoH.SpankingWaitMax.Value);
            SpankTimer.Reset();
        }


        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "Start")]
        static void SetupHScene(HScene __instance)
        {
            __instance.ctrlAuto.Load(null, 0);
            TCSequenceTracker.Reset();
            HS2SmartAutoH.AutoStartPlace = -1;
            HS2SmartAutoH.sequenceInfoLogged = false;
            Hooks.SonyuFinishHooks.ResetFinish();
            Hooks.HoushiHooks.ResetServiceFinish();
            HS2SmartAutoH.ResetPositionUse();
            TCTrait.ResetScene();
            TCWatch.ResetScene();
            TCPain.ResetScene();
            ForcePositionChange = false;
        }

        public static float FixWheelValue(float _wheel)
        {
            if (Singleton<HSceneFlagCtrl>.Instance.initiative > 0)
            {
                _wheel = 0.1f;
            }
            return _wheel;
        }
   
        // Before the game picks the next position: narrow its list by our stages and rules
        [HarmonyPrefix, HarmonyPatch(typeof(HAutoCtrl), "GetAnimation")]
        static void FilterAutoAnimation(ref List<HScene.AnimationListInfo>[] _listAnim, int _initiative)
        {
            if (_initiative == 2)
            {
                _listAnim = HS2SmartAutoH.Instance.FilterAnimationList(_listAnim);
            }
        }

        // After the game picked: keep its choice (with its own preference bonus) if it is allowed
        [HarmonyPostfix, HarmonyPatch(typeof(HAutoCtrl), "GetAnimation")]
        static void SelectAutoAnimation(HAutoCtrl __instance, int _initiative, bool _isFirst, ref HScene.StartMotion __result)
        {
            if (_initiative == 2)
            {
                __result = HS2SmartAutoH.Instance.CheckGameChoice(__instance, __result, _isFirst);
            }
        }

        // Reset button of the panel: change the position now (in a loop) or at the next restart (after a finish).
        // The game asks HAutoCtrl in every Auto loop frame whether the position should change.
        public static bool ForcePositionChange = false;
        // Who asked for the change (1.14.2): written to the log when it is asked and when it happens
        public static string ForceReason = "";

        public static void RequestPositionChange(string reason)
        {
            ForcePositionChange = true;
            ForceReason = reason;
            HS2SmartAutoH.Instance.Log.LogMessage("[Change] Position change asked: " + reason);
        }
        private static FieldInfo lerpField = AccessTools.Field(typeof(HAutoCtrl), "lerp");

        private static bool SpeedChangeRunning(HAutoCtrl auto)
        {
            try
            {
                if (lerpField == null)
                {
                    return false;
                }
                Vector2 lerp = (Vector2)lerpField.GetValue(auto);
                // The game itself waits while its speed change is running (lerp is negative when idle)
                return !(lerp.x < 0f || lerp.y < 0f);
            }
            catch (Exception)
            {
                return false;
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HAutoCtrl), "IsChangeActionAtLoop")]
        static bool ForceChangeAtLoop(HAutoCtrl __instance, ref bool __result)
        {
            HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
            if (flags == null || flags.initiative != 2)
            {
                return true;
            }
            if (!ForcePositionChange)
            {
                // Sex trait / FFM, MMF: no change in the middle of the act while the position is still needed
                try
                {
                    if (TCTrait.HoldPosition(flags) || TCTrait.StayInGroupPosition(flags))
                    {
                        __result = false;
                        return false;
                    }
                }
                catch (Exception)
                {
                }
                return true;
            }
            if (SpeedChangeRunning(__instance))
            {
                return true;
            }
            ForcePositionChange = false;
            __result = true;
            HS2SmartAutoH.Instance.Log.LogMessage("[Change] Changing the position now (" + ForceReason + ")");
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HAutoCtrl), "IsChangeActionAtRestart")]
        static void ForceChangeAtRestart(ref bool __result)
        {
            HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
            if (flags == null || flags.initiative != 2)
            {
                return;
            }
            if (!ForcePositionChange)
            {
                // FFM / MMF with only one position for three here: it is repeated, the goal is not left
                try
                {
                    if (__result && TCTrait.StayInGroupPosition(flags))
                    {
                        __result = false;
                    }
                }
                catch (Exception)
                {
                }
                return;
            }
            ForcePositionChange = false;
            if (!__result)
            {
                __result = true;
                HS2SmartAutoH.Instance.Log.LogMessage("[Change] Changing the position after the finish (" + ForceReason + ")");
            }
        }
    }
}
