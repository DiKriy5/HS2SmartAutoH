using AIChara;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HS2SmartAutoH
{
    // Game fix (0.9.2, working since 0.9.3): a new position chosen while the characters rest after a climax or a finish
    // (Orgasm_A, Orgasm_IN_A, OrgasmM_OUT_A, Drink_A, Vomit_A, ... - HScene.IsAfterIdle) starts from the pause.
    // The game decides "start from the pause" only by HScene.IsIdle (Idle, Insert, ...) or a change of the kind
    // of position (caress -> sex, ...). The rest states are not in that list, so a position of the same kind
    // started moving at once. A position change in the middle of the act keeps moving, as in the game.
    public static class RestPoseHooks
    {
        private static float restChangeAt = -100f;

        internal static bool Pending
        {
            get { return Time.time - restChangeAt < 10f; }
        }

        internal static void Clear()
        {
            restChangeAt = -100f;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "ChangeAnimation")]
        static void ChangeAnimationPrefix(HScene __instance, object[] __args)
        {
            restChangeAt = -100f;
            if (!HS2SmartAutoH.RestPositionFix.Value)
            {
                return;
            }
            try
            {
                HScene.AnimationListInfo info = __args != null && __args.Length > 0 ? __args[0] as HScene.AnimationListInfo : null;
                HSceneFlagCtrl flags = Traverse.Create(__instance).Field("ctrlFlag").GetValue<HSceneFlagCtrl>();
                if (info == null || flags == null || flags.nowAnimationInfo == info)
                {
                    return;   // the same position again: the game changes nothing
                }
                ChaControl[] females = Traverse.Create(__instance).Field("chaFemales").GetValue<ChaControl[]>();
                if (females == null || females.Length == 0 || females[0] == null || females[0].animBody == null)
                {
                    return;
                }
                bool resting = Traverse.Create(__instance).Method("IsAfterIdle", new Type[] { typeof(Animator) }, new object[] { females[0].animBody }).GetValue<bool>();
                if (resting)
                {
                    restChangeAt = Time.time;
                }
            }
            catch (Exception e)
            {
                HS2SmartAutoH.Instance.LogDetailed("[RestFix] Could not check the rest state: " + e.Message);
            }
        }
    }

    // SetStartMotion(bool _isIdle, ...) of every kind of position: true = start from the pause
    [HarmonyPatch]
    public static class RestPoseStartHooks
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] kinds = { typeof(Aibu), typeof(Houshi), typeof(Sonyu), typeof(Spnking), typeof(Masturbation),
                             typeof(Les), typeof(Peeping), typeof(MultiPlay_F1M2), typeof(MultiPlay_F2M1) };
            foreach (Type t in kinds)
            {
                MethodInfo m = AccessTools.DeclaredMethod(t, "SetStartMotion");
                ParameterInfo[] ps = m != null ? m.GetParameters() : null;
                // the prefix changes the argument by its name, so only methods that use this name are hooked
                if (ps != null && ps.Length > 0 && ps[0].Name == "_isIdle" && ps[0].ParameterType == typeof(bool))
                {
                    yield return m;
                }
            }
        }

        // 0.9.3: the argument is changed by name (0.9.2 changed __args, which did not reach the game)
        static void Prefix(ref bool _isIdle, MethodBase __originalMethod)
        {
            if (!RestPoseHooks.Pending)
            {
                return;
            }
            RestPoseHooks.Clear();
            if (!_isIdle)
            {
                _isIdle = true;
                HS2SmartAutoH.Instance.LogDetailed("[RestFix] New position chosen while resting after a climax / finish: it starts from the pause (" + __originalMethod.DeclaringType.Name + ")");
            }
        }
    }
}
