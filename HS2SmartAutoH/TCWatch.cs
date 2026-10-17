using HarmonyLib;
using HS2SmartAutoH.Hooks;
using Manager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SmartAutoH
{
    // Diagnostics for rare hangs in Auto mode (1.14.1):
    //  - a position stays in the near-finish loop (OLoop) too long: the gauges and the finish state are logged
    //  - a chosen position does not start: it is logged, not offered again in this scene, and another change is asked
    public static class TCWatch
    {
        public const float OLoopLimit = 20f;
        public const float StartLimit = 25f;

        private static string loopAnim = null;
        private static float loopSince = 0f;
        private static bool loopLogged = false;

        private static HScene.AnimationListInfo picked = null;
        private static float pickedAt = 0f;
        private static int pickedPlace = -1;
        private static int pickedPoint = -1;
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static bool errorLogged = false;

        public static void ResetScene()
        {
            loopAnim = null;
            loopLogged = false;
            picked = null;
            failed.Clear();
        }

        private static string Key(HScene.AnimationListInfo info)
        {
            return info.ActionCtrl.Item1 + ":" + info.ActionCtrl.Item2 + ":" + info.id;
        }

        public static void Picked(HScene.AnimationListInfo info)
        {
            picked = info;
            pickedAt = Time.time;
            HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
            pickedPlace = flags == null ? -1 : flags.nPlace;
            pickedPoint = flags == null ? -1 : flags.HPointID;
            HS2SmartAutoH.Instance.LogDetailed("[Watch] Waiting for '" + info.nameAnimation + "' to start");
        }

        public static List<TCCandidate> RemoveFailed(List<TCCandidate> candidates)
        {
            if (failed.Count == 0)
            {
                return candidates;
            }
            List<TCCandidate> ok = candidates.FindAll((c) => !failed.Contains(Key(c.Info)));
            return ok.Count > 0 ? ok : candidates;
        }

        public static void Update()
        {
            try
            {
                HSceneManager manager = Singleton<HSceneManager>.Instance;
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                if (manager == null || manager.Hscene == null || flags == null)
                {
                    return;
                }
                if (flags.initiative != 2)
                {
                    picked = null;
                    loopAnim = null;
                    return;
                }
                CheckStart(flags);
                CheckLoop(flags);
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    HS2SmartAutoH.Instance.Log.LogWarning("[Watch] Error (logged once): " + e);
                }
            }
        }

        private static void CheckStart(HSceneFlagCtrl flags)
        {
            if (picked == null)
            {
                return;
            }
            HScene.AnimationListInfo now = flags.nowAnimationInfo;
            if (now != null && (now == picked || Key(now) == Key(picked)))
            {
                HS2SmartAutoH.Instance.LogDetailed(string.Format("[Watch] '{0}' started after {1:0.0} s", picked.nameAnimation, Time.time - pickedAt));
                if (!HS2SmartAutoH.UseWholeLocation.Value && pickedPlace >= 0 && (flags.nPlace != pickedPlace || flags.HPointID != pickedPoint))
                {
                    HS2SmartAutoH.Instance.Log.LogWarning(string.Format("[Select] moved to another spot: '{0}' (place {1} point {2} -> place {3} point {4}) although L is off",
                        picked.nameAnimation, pickedPlace, pickedPoint, flags.nPlace, flags.HPointID));
                }
                picked = null;   // it started
                return;
            }
            if (Time.time - pickedAt < StartLimit)
            {
                return;
            }
            string name = picked.nameAnimation;
            failed.Add(Key(picked));
            picked = null;
            HS2SmartAutoH.Instance.Log.LogWarning(string.Format("[Watch] '{0}' did not start in {1} s (now '{2}', animation {3}) - it is not offered again in this scene, asking another change",
                name, StartLimit, now == null ? "-" : now.nameAnimation, AnimationStatus.PlayingAnimation));
            GeneralHooks.RequestPositionChange("watch: '" + name + "' did not start");
        }

        private static void CheckLoop(HSceneFlagCtrl flags)
        {
            string anim = AnimationStatus.PlayingAnimation;
            bool oloop = anim != null && anim.EndsWith("OLoop");
            if (!oloop)
            {
                loopAnim = null;
                loopLogged = false;
                return;
            }
            if (anim != loopAnim)
            {
                loopAnim = anim;
                loopSince = Time.time;
                loopLogged = false;
                return;
            }
            if (loopLogged || Time.time - loopSince < OLoopLimit)
            {
                return;
            }
            loopLogged = true;
            HScene.AnimationListInfo now = flags.nowAnimationInfo;
            HS2SmartAutoH.Instance.Log.LogWarning(string.Format(
                "[Watch] '{0}' ({1}/{2}/{3}) is in {4} for {5} s: his gauge {6:0.00}, hers {7:0.00}, gauge lock him {8} / her {9}, click {10}, SEX finish in progress {11}, SERVICE command given {12}, SEX finish {13}, SERVICE finish {14}",
                now == null ? "-" : now.nameAnimation,
                now == null ? -1 : now.ActionCtrl.Item1, now == null ? -1 : now.ActionCtrl.Item2, now == null ? -1 : now.id,
                anim, OLoopLimit, flags.feel_m, flags.feel_f, flags.stopFeelMale, flags.stopFeelFemale, flags.click,
                SonyuFinishHooks.FinishInProgress, HoushiHooks.CommandGiven,
                TCTrait.SexFinish(false), HS2SmartAutoH.ServiceFinish.Value));
        }
    }
}
