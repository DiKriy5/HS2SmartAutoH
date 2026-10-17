using AIChara;
using Manager;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HS2SmartAutoH
{
    // One stage of a sequence: which categories are allowed and for how many position changes.
    [XmlType("TCSequenceStep")]
    public class TCSequenceStep
    {
        // Comma separated category names, e.g. "SERVICE" or "CARESS,SERVICE"
        [XmlAttribute("Categories")]
        public string Categories = "";

        // Number of position changes spent in this stage
        [XmlAttribute("Changes")]
        public int Changes = 1;

        public bool Matches(string categoryName)
        {
            if (string.IsNullOrEmpty(Categories))
            {
                return false;
            }
            foreach (string category in Categories.Split(','))
            {
                if (category.Trim().Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    // A sequence of stages applied to a character (by name), a personality (by number) or everyone ("*").
    [XmlType("TCSequence")]
    public class TCSequence
    {
        // Comma separated full names as shown on the character card, or "*" for the default sequence
        [XmlAttribute("Character")]
        public string Character = "";

        // Comma separated personality numbers (see BepInEx log for the number of each girl)
        [XmlAttribute("Personality")]
        public string Personality = "";

        // Comma separated H attribute numbers (3 = Masochist, 2 = Sadist, ...)
        [XmlAttribute("HAttribute")]
        public string HAttribute = "";

        // What happens after the last stage: Free (normal random choice), Loop (start again), Stay (keep last stage)
        [XmlAttribute("OnEnd")]
        public string OnEnd = "Free";

        [XmlElement("Step")]
        public List<TCSequenceStep> Steps = new List<TCSequenceStep>();

        public string Key()
        {
            return string.Format("Character='{0}' HAttribute='{1}' Personality='{2}'", Character, HAttribute, Personality);
        }

        public bool MatchesCharacter(string fullName)
        {
            return ListContains(Character, fullName);
        }

        public bool MatchesPersonality(int personality)
        {
            return ListContains(Personality, personality.ToString());
        }

        public bool MatchesHAttribute(int hAttribute)
        {
            return ListContains(HAttribute, hAttribute.ToString());
        }

        public bool IsDefault()
        {
            return Character != null && Character.Trim() == "*";
        }

        private static bool ListContains(string list, string value)
        {
            if (string.IsNullOrEmpty(list) || string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (string item in list.Split(','))
            {
                if (item.Trim().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    // A position the game could switch to, before the sequence filter is applied.
    public class TCCandidate
    {
        public HAutoCtrl.AutoRandom.AutoRandomDate Date;
        public bool Included;
        public string Category;

        public HScene.AnimationListInfo Info;

        public TCCandidate(HAutoCtrl.AutoRandom.AutoRandomDate date, bool included, string category, HScene.AnimationListInfo info)
        {
            Date = date;
            Included = included;
            Category = category;
            Info = info;
        }
    }

    // Tracks progress through the active sequence during one H scene.
    public static class TCSequenceTracker
    {
        private static string activeKey;
        // Set when the final stage (OnEnd="Stay") has nothing left: Auto mode is switched off
        public static bool StopRequested = false;
        // Why Auto mode is stopped (shown in the corner); null = the final stage has nothing left
        public static string StopReason = null;
        private static int stepIndex;
        private static int changesInStep;
        private static bool finished;
        // Category of the stage in use, kept so a new stage layout can continue from the same stage
        private static string currentCategory;
        // Short text for the panel, e.g. "Stage 3/5: CARESS, position 1/1"
        public static string Status = "";
        // True when the last Apply gave the first position of a stage (used by the sex trait, 1.14.0)
        public static bool FirstOfStage = false;

        public static void Reset()
        {
            activeKey = null;
            stepIndex = 0;
            changesInStep = 0;
            finished = false;
            currentCategory = null;
            Status = "";
            FirstOfStage = false;
        }

        // The stage layout changed (goal or stage slots, 1.15.0): continue from the same stage number.
        // The count is kept if that stage has the same type, otherwise the stage starts from its first position.
        private static void Remap(TCSequence sequence)
        {
            int last = sequence.Steps.Count - 1;
            int newIndex = Math.Min(stepIndex, last);
            if (finished && stepIndex < last)
            {
                // stages were added after the end: go on with them
                finished = false;
                newIndex = stepIndex + 1;
                changesInStep = 0;
            }
            else if (finished)
            {
                newIndex = last;
            }
            else if (currentCategory == null || !sequence.Steps[newIndex].Matches(currentCategory))
            {
                changesInStep = 0;
            }
            stepIndex = newIndex;
            currentCategory = sequence.Steps[stepIndex].Categories;
            Log(string.Format("Stage layout changed, continuing from stage {0}/{1} ({2}), position {3}/{4}{5}", stepIndex + 1, sequence.Steps.Count, currentCategory, changesInStep, sequence.Steps[stepIndex].Changes, finished ? ", after the last stage" : ""));
        }

        // Returns the candidates allowed by the current stage and counts this change.
        public static List<TCCandidate> Apply(List<TCCandidate> all, TCSequence sequence)
        {
            FirstOfStage = false;
            if (sequence == null || sequence.Steps == null || sequence.Steps.Count == 0)
            {
                if (sequence != null)
                {
                    Status = "No stages: any position";
                }
                return all;
            }

            string key = sequence.Key();
            if (activeKey != key)
            {
                if (activeKey == null)
                {
                    Reset();
                    Log(string.Format("Sequence started: {0}, {1} stages, OnEnd={2}", key, sequence.Steps.Count, sequence.OnEnd));
                }
                else
                {
                    Remap(sequence);
                }
                activeKey = key;
            }
            if (stepIndex >= sequence.Steps.Count)
            {
                stepIndex = sequence.Steps.Count - 1;
            }

            // Guard against endless looping when no stage has anything available
            int guard = sequence.Steps.Count * 2 + 1;
            while (guard-- > 0)
            {
                if (finished)
                {
                    if (IsOnEnd(sequence, "Stay"))
                    {
                        TCSequenceStep last = sequence.Steps[sequence.Steps.Count - 1];
                        currentCategory = last.Categories;
                        Status = string.Format("After the last stage: {0} (stay)", last.Categories);
                        List<TCCandidate> stay = Filter(all, last);
                        if (stay.Count == 0)
                        {
                            Log(TCGoals.IsGroup(HS2SmartAutoH.Goal.Value)
                                ? string.Format("Final stage ({0}) has no new position, positions for three are repeated", last.Categories)
                                : string.Format("Final stage ({0}) has no available positions, stopping Auto mode", last.Categories));
                            StopRequested = true;
                        }
                        return stay;
                    }
                    Status = "After the last stage: any position";
                    return all;
                }

                TCSequenceStep step = sequence.Steps[stepIndex];
                currentCategory = step.Categories;
                if (changesInStep >= Math.Max(step.Changes, 0))
                {
                    Advance(sequence);
                    continue;
                }

                List<TCCandidate> filtered = Filter(all, step);
                if (filtered.Count == 0)
                {
                    Log(string.Format("Stage {0} ({1}) has no available positions, skipping", stepIndex + 1, step.Categories));
                    Advance(sequence);
                    continue;
                }

                changesInStep++;
                FirstOfStage = changesInStep == 1;
                Status = string.Format("Stage {0}/{1}: {2}, position {3}/{4}", stepIndex + 1, sequence.Steps.Count, step.Categories, changesInStep, step.Changes);
                Log(string.Format("Stage {0}/{1} ({2}), change {3}/{4}", stepIndex + 1, sequence.Steps.Count, step.Categories, changesInStep, step.Changes));
                return filtered;
            }

            return all;
        }

        private static void Advance(TCSequence sequence)
        {
            stepIndex++;
            changesInStep = 0;
            if (stepIndex >= sequence.Steps.Count)
            {
                if (IsOnEnd(sequence, "Loop"))
                {
                    stepIndex = 0;
                    Log("Sequence finished, looping");
                }
                else
                {
                    stepIndex = sequence.Steps.Count - 1;
                    finished = true;
                    Log(string.Format("Sequence finished, OnEnd={0}", sequence.OnEnd));
                }
            }
            currentCategory = sequence.Steps[stepIndex].Categories;
        }

        private static List<TCCandidate> Filter(List<TCCandidate> all, TCSequenceStep step)
        {
            List<TCCandidate> result = new List<TCCandidate>();
            foreach (TCCandidate candidate in all)
            {
                if (step.Matches(candidate.Category))
                {
                    result.Add(candidate);
                }
            }
            return result;
        }

        private static bool IsOnEnd(TCSequence sequence, string value)
        {
            return sequence.OnEnd != null && sequence.OnEnd.Trim().Equals(value, StringComparison.OrdinalIgnoreCase);
        }

        private static void Log(string message)
        {
            if (HS2SmartAutoH.Instance != null)
            {
                HS2SmartAutoH.Instance.Log.LogInfo("[Sequence] " + message);
            }
        }
    }
}
