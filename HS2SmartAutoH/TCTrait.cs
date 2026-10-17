using AIChara;
using HarmonyLib;
using Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SmartAutoH
{
    // Sex trait of the main girl (1.14.0).
    // After the scene the game gives a bonus by her trait (HSceneFlagCtrl.EndSetAddMindParam):
    //   1 Horny       - any finish
    //   2 Sadist      - he finished (inside + outside + drink + spit out > 0)
    //   3 Masochist   - she climaxed (numOrgasmTotal > 0)
    //   4..7          - a position with breasts / anal / vagina / kiss had a finish (mindJudge)
    //   8 Clean Freak - any outside finish (a penalty)
    //   10 Lonely     - any finish together
    // With the T button on, Auto mode helps the bonus and avoids the penalty.
    public static class TCTrait
    {
        public const int None = 0, Horny = 1, Sadist = 2, Masochist = 3, Breasts = 4, Ass = 5, Pussy = 6, Kisses = 7, CleanFreak = 8, SexHater = 9, Lonely = 10;

        public static readonly string[] Names = new string[] { "None", "Horny", "Sadist", "Masochist", "Sensitive Breasts", "Sensitive Ass", "Sensitive Pussy", "Love Kisses", "Clean Freak", "Sex Hater", "Lonely" };

        // System type the game checks for traits 4..7 (index = trait - 4): breast 2, anal 3, vagina 1, kiss 0
        private static readonly int[] SystemOfTrait = new int[] { 2, 3, 1, 0 };

        // A position is held at most this long while waiting for the finish the trait needs
        public const float HoldLimit = 240f;

        private static FieldInfo mindJudgeField = AccessTools.Field(typeof(HSceneFlagCtrl), "mindJudge");
        private static FieldInfo numInsideField = AccessTools.Field(typeof(HSceneFlagCtrl), "numInside");
        private static FieldInfo numOutSideField = AccessTools.Field(typeof(HSceneFlagCtrl), "numOutSide");
        private static FieldInfo numDrinkField = AccessTools.Field(typeof(HSceneFlagCtrl), "numDrink");
        private static FieldInfo numVomitField = AccessTools.Field(typeof(HSceneFlagCtrl), "numVomit");
        private static FieldInfo numOrgasmTotalField = AccessTools.Field(typeof(HSceneFlagCtrl), "numOrgasmTotal");

        private static string holdKey = null;
        private static float holdSince = 0f;
        private static bool holdLogged = false;
        private static int holdBaseline = 0;
        private static string stayKey = null;
        private static bool stayResult = false;
        private static bool finishLogged = false;

        public static void ResetScene()
        {
            holdKey = null;
            holdLogged = false;
            stayKey = null;
            stayResult = false;
            finishLogged = false;
        }

        public static string Name(int trait)
        {
            return trait >= 0 && trait < Names.Length ? Names[trait] : "unknown";
        }

        // Trait of the main girl (the one the scene was started with), -1 if not known
        public static int Current()
        {
            try
            {
                HSceneManager manager = Singleton<HSceneManager>.Instance;
                if (manager == null || manager.Hscene == null)
                {
                    return -1;
                }
                ChaControl[] females = manager.Hscene.GetFemales();
                if (females == null || females.Length == 0 || females[0] == null)
                {
                    return -1;
                }
                return Convert.ToInt32(females[0].fileParam2.hAttribute);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static bool HasPositions(int trait)
        {
            return (trait >= Breasts && trait <= Kisses) || trait == CleanFreak;
        }

        public static bool HasFinishes(int trait)
        {
            return trait == Sadist || trait == Masochist;
        }

        // The T button changes something for this trait
        public static bool HasEffect(int trait)
        {
            return HasPositions(trait) || HasFinishes(trait);
        }

        public static bool Active(out int trait)
        {
            trait = Current();
            return HS2SmartAutoH.SexTrait.Value && HasEffect(trait);
        }

        // ---------- game counters ----------

        private static int Int(FieldInfo field, HSceneFlagCtrl flags)
        {
            try
            {
                return field == null ? 0 : Convert.ToInt32(field.GetValue(flags));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static bool HeFinished(HSceneFlagCtrl flags)
        {
            return Int(numInsideField, flags) + Int(numOutSideField, flags) + Int(numDrinkField, flags) + Int(numVomitField, flags) > 0;
        }

        // Her climaxes in this scene so far (used by the pain timer, 1.15.1)
        public static int OrgasmCount(HSceneFlagCtrl flags)
        {
            return Int(numOrgasmTotalField, flags);
        }

        public static bool SheClimaxed(HSceneFlagCtrl flags)
        {
            return Int(numOrgasmTotalField, flags) > 0;
        }

        public static bool[] MindJudge(HSceneFlagCtrl flags)
        {
            try
            {
                return mindJudgeField == null ? null : mindJudgeField.GetValue(flags) as bool[];
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SetMindJudge(HSceneFlagCtrl flags, bool[] value)
        {
            if (mindJudgeField != null)
            {
                mindJudgeField.SetValue(flags, value);
            }
        }

        // Traits 4..7: the scene already has a finish in a position of her kind
        public static bool Done(HSceneFlagCtrl flags, int trait)
        {
            if (trait < Breasts || trait > Kisses)
            {
                return true;
            }
            bool[] judge = MindJudge(flags);
            return judge != null && judge.Length > trait - Breasts && judge[trait - Breasts];
        }

        // Sadist / Masochist: the finish the trait needs has not happened yet
        public static bool FinishPending(HSceneFlagCtrl flags, int trait)
        {
            if (trait == Sadist)
            {
                return !HeFinished(flags);
            }
            if (trait == Masochist)
            {
                return !SheClimaxed(flags);
            }
            return false;
        }

        // ---------- positions ----------

        public static bool HasSystem(HScene.AnimationListInfo info, int system)
        {
            try
            {
                return info != null && info.lstSystem != null && Enumerable.Contains(info.lstSystem, system);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsTraitPosition(HScene.AnimationListInfo info, int trait)
        {
            if (trait < Breasts || trait > Kisses)
            {
                return false;
            }
            if (trait == Breasts && IsBoobjob(info))
            {
                return true;
            }
            if (trait == Ass && info != null && info.nameAnimation != null &&
                (info.nameAnimation.Contains("\u30A2\u30CA\u30EB") || info.nameAnimation.IndexOf("anal", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
            return HasSystem(info, SystemOfTrait[trait - Breasts]);
        }

        // Boobjob positions of the game (service ids) and copies from mods by name (1.14.1)
        private static readonly int[] BoobjobIds = new int[] { 3, 4, 5, 15, 16, 22, 23, 101 };
        private static readonly string[] BoobjobWords = new string[] { "\u30D1\u30A4\u30BA\u30EA", "boobjob", "titjob", "paizuri" };

        public static bool IsBoobjob(HScene.AnimationListInfo info)
        {
            if (info == null)
            {
                return false;
            }
            try
            {
                if (info.ActionCtrl.Item1 == 1 && Array.IndexOf(BoobjobIds, info.id) >= 0)
                {
                    return true;
                }
                string name = info.nameAnimation ?? "";
                foreach (string word in BoobjobWords)
                {
                    if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        // All finishes of the scene so far (his and hers)
        private static int FinishCount(HSceneFlagCtrl flags)
        {
            return Int(numInsideField, flags) + Int(numOutSideField, flags) + Int(numDrinkField, flags) + Int(numVomitField, flags) + Int(numOrgasmTotalField, flags);
        }

        // Positions where his finish can only be outside (the game's finish buttons, HSceneSprite.SetFinishSelect):
        // service without the mouth, sex between the thighs, positions for three with the same
        public static bool IsOutsideOnly(HScene.AnimationListInfo info)
        {
            int item1 = info.ActionCtrl.Item1;
            int item2 = info.ActionCtrl.Item2;
            switch (item1)
            {
                case 1: return item2 != 1 && item2 != 2;
                case 2: return item2 != 0;
                case 3: return item2 == 0;
                case 5:
                case 6: return item2 == 1 || item2 == 4;
                default: return false;
            }
        }

        // Called after the stage filter, before the goal ranking
        public static List<TCCandidate> Apply(List<TCCandidate> candidates, bool firstOfStage)
        {
            int trait;
            HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
            if (candidates.Count == 0 || flags == null || !Active(out trait))
            {
                return candidates;
            }
            if (trait == CleanFreak)
            {
                List<TCCandidate> clean = candidates.FindAll((c) => !IsOutsideOnly(c.Info));
                if (clean.Count > 0 && clean.Count < candidates.Count)
                {
                    HS2SmartAutoH.Instance.LogDetailed(string.Format("[Trait] Clean Freak: {0} positions with an outside finish only are left for last", candidates.Count - clean.Count));
                    return clean;
                }
                return candidates;
            }
            if (trait >= Breasts && trait <= Kisses && firstOfStage)
            {
                List<TCCandidate> mine = candidates.FindAll((c) => IsTraitPosition(c.Info, trait));
                if (mine.Count > 0)
                {
                    HS2SmartAutoH.Instance.LogDetailed(string.Format("[Trait] {0}: first position of the stage is one of hers ({1} positions)", Name(trait), mine.Count));
                    return mine;
                }
            }
            return candidates;
        }

        // ---------- finishes ----------

        // SEX finish used now: Sadist / Masochist get "together" until their finish happened
        public static SexFinishType SexFinish(bool log)
        {
            SexFinishType chosen = HS2SmartAutoH.SexFinish.Value;
            int trait;
            HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
            if (flags != null && Active(out trait) && FinishPending(flags, trait))
            {
                if (log && !finishLogged)
                {
                    finishLogged = true;
                    HS2SmartAutoH.Instance.LogDetailed(string.Format("[Trait] {0}: SEX finish together until {1}", Name(trait), trait == Sadist ? "he finishes once" : "she climaxes once"));
                }
                return SexFinishType.Same;
            }
            return chosen;
        }

        // ---------- holding the position ----------

        // Mid-act change: wait in a position that can give the finish her trait needs
        public static bool HoldPosition(HSceneFlagCtrl flags)
        {
            int trait;
            HScene.AnimationListInfo info = flags == null ? null : flags.nowAnimationInfo;
            if (info == null || !Active(out trait))
            {
                return false;
            }
            string key = info.ActionCtrl.Item1 + ":" + info.ActionCtrl.Item2 + ":" + info.id;
            if (key != holdKey)
            {
                holdKey = key;
                holdSince = Time.time;
                holdLogged = false;
                holdBaseline = FinishCount(flags);
            }
            if (Time.time - holdSince > HoldLimit)
            {
                return false;
            }
            int item1 = info.ActionCtrl.Item1;
            int item2 = info.ActionCtrl.Item2;
            bool service = item1 == 1 || ((item1 == 5 || item1 == 6) && (item2 == 1 || item2 == 2));
            bool sex = item1 == 2 || (item1 == 3 && (item2 == 0 || item2 == 1 || item2 == 7)) || ((item1 == 5 || item1 == 6) && (item2 == 3 || item2 == 4));
            // she can climax in every position except service (caress, sex, pain, toys)
            bool herClimax = !service && item1 != 4;
            bool hold = false;
            if (trait >= Breasts && trait <= Kisses)
            {
                // kept until a finish in this very position (every stage gets its own)
                hold = IsTraitPosition(info, trait) && FinishCount(flags) == holdBaseline;
            }
            else if (trait == Sadist)
            {
                hold = (service || sex) && FinishPending(flags, trait);
            }
            else if (trait == Masochist)
            {
                hold = herClimax && FinishPending(flags, trait);
            }
            if (hold && !holdLogged)
            {
                holdLogged = true;
                HS2SmartAutoH.Instance.LogDetailed(string.Format("[Trait] {0}: staying in '{1}' until its finish", Name(trait), info.nameAnimation));
            }
            return hold;
        }

        // FFM / MMF: only one position for three here - keep repeating it instead of leaving the goal
        public static bool StayInGroupPosition(HSceneFlagCtrl flags)
        {
            TCGoal goal = HS2SmartAutoH.Goal.Value;
            HScene.AnimationListInfo info = flags == null ? null : flags.nowAnimationInfo;
            if (!TCGoals.IsGroup(goal) || info == null)
            {
                return false;
            }
            int wanted = goal == TCGoal.FFM ? 5 : 6;
            if (info.ActionCtrl.Item1 != wanted)
            {
                return false;
            }
            string key = goal + ":" + info.ActionCtrl.Item2 + ":" + info.id;
            if (key != stayKey)
            {
                stayKey = key;
                stayResult = HS2SmartAutoH.Instance.GroupPositionsAvailable(goal) <= 1;
                if (stayResult)
                {
                    HS2SmartAutoH.Instance.LogDetailed(string.Format("[Goal] {0}: '{1}' is the only position for three here, it is repeated", goal, info.nameAnimation));
                }
            }
            return stayResult;
        }
    }
}
