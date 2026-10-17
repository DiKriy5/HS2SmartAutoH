using AIChara;
using HS2SmartAutoH.Hooks;
using Manager;
using System;
using UnityEngine;

namespace HS2SmartAutoH
{
    // Pain training (1.15.0).
    // In a pain position the game stops the girl's gauge at 74% unless she has the Masochist trait
    // or full pain experience (resistPain 100), and the man has no finish there. Such a position never ends by itself,
    // so in Auto mode it lasts at most 'Pain Position Time'; then the position is changed without a finish.
    // The time in the position still raises her pain experience at the end of the scene.
    public static class TCPain
    {
        private static string key = null;
        private static float since = 0f;
        private static int orgasmsAtStart = 0;
        private static bool spankLogged = false;
        private static bool errorLogged = false;

        public static void ResetScene()
        {
            key = null;
        }

        // The main girl can climax in pain positions (the game's own condition)
        public static bool CanClimaxFromPain()
        {
            try
            {
                HSceneManager manager = Singleton<HSceneManager>.Instance;
                if (manager == null || manager.Hscene == null)
                {
                    return false;
                }
                ChaControl[] females = manager.Hscene.GetFemales();
                if (females == null || females.Length == 0 || females[0] == null)
                {
                    return false;
                }
                return Convert.ToInt32(females[0].fileParam2.hAttribute) == TCTrait.Masochist || females[0].fileGameInfo2.resistPain >= 100;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsPain(HScene.AnimationListInfo info)
        {
            if (info == null || HS2SmartAutoH.IsTentacle(info))
            {
                return false;
            }
            int mode = HS2SmartAutoH.Instance.DetermineModeByActionCtrl(info.ActionCtrl.Item1, info.ActionCtrl.Item2);
            return HS2SmartAutoH.IsPainMode(info, mode);
        }

        // Spanking in Auto mode (1.15.1): the position changes only when the pain time is up (the timer asks for it)
        // or after her climax in it. The game's own reasons (70% of her gauge, "change after a finish") are ignored,
        // so spanking always gives its full time of pain.
        public static bool SpankingMayChange()
        {
            try
            {
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                if (flags == null || flags.initiative != 2 || GeneralHooks.ForcePositionChange)
                {
                    return true;
                }
                if (key != null && TCTrait.OrgasmCount(flags) > orgasmsAtStart)
                {
                    return true;
                }
                if (!spankLogged)
                {
                    spankLogged = true;
                    HS2SmartAutoH.Instance.LogDetailed("[Pain] Spanking: the game wants to change the position - kept until the pain time is up or she climaxes");
                }
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static void Update()
        {
            try
            {
                HSceneManager manager = Singleton<HSceneManager>.Instance;
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                if (manager == null || manager.Hscene == null || flags == null || flags.initiative != 2)
                {
                    key = null;
                    return;
                }
                HScene.AnimationListInfo info = flags.nowAnimationInfo;
                if (info == null || !IsPain(info))
                {
                    key = null;
                    return;
                }
                string now = info.ActionCtrl.Item1 + ":" + info.ActionCtrl.Item2 + ":" + info.id;
                int limit = HS2SmartAutoH.PainPositionTime.Value;
                if (now != key)
                {
                    key = now;
                    since = Time.time;
                    orgasmsAtStart = TCTrait.OrgasmCount(flags);
                    spankLogged = false;
                    HS2SmartAutoH.Instance.LogDetailed(string.Format("[Pain] '{0}': pain position, at most {1} s; she can climax here: {2}",
                        info.nameAnimation, limit, CanClimaxFromPain() ? "yes" : "no"));
                    return;
                }
                if (Time.time - since < limit)
                {
                    return;
                }
                // Asked again after another full time if the position did not change
                since = Time.time;
                GeneralHooks.RequestPositionChange(string.Format("pain: time is up in '{0}' ({1} s)", info.nameAnimation, limit));
                if (info.ActionCtrl.Item1 == 3 && info.ActionCtrl.Item2 == 2)
                {
                    // Spanking has no loop where the change is asked: ask for it the way the game does after a spank
                    SpnkingHooks.SpankingAfterWaitingAnimationAutoSelector();
                }
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    HS2SmartAutoH.Instance.Log.LogWarning("[Pain] Error (logged once): " + e);
                }
            }
        }
    }
}
