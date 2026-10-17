using AIChara;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HS2SmartAutoH.Hooks;
using Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HS2SmartAutoH
{
    // HS2 Smart Auto H (0.9.0): Auto mode for Honey Select 2.
    // 0.9.3: the rest fix changes the start argument by name (0.9.2 did not reach the game).
    // 0.9.2: game fix - a new position chosen while resting after a climax / finish starts from the pause (Hooks/RestPoseHooks.cs).
    // Based on HS2TakeChargePlugin by OrangeSpork (Take Charge / Auto mode framework), reworked and extended.
    [BepInPlugin(GUID,  PluginName, Version)]
    [BepInProcess("HoneySelect2.exe")]
    [BepInProcess("HoneySelect2VR.exe")]
    public class HS2SmartAutoH : BaseUnityPlugin
    {
        public const string GUID = "HS2SmartAutoH";
        // Settings are taken over once from the original plugin (its config file)
        private const string OldGuid = "orange.spork.hs2takechargeplugin";
        public const string PluginName = "HS2 Smart Auto H";
        public const string Version = "1.0.0";

        // Menu sections (the numbers keep them in this order in the F1 menu)
        private const string SecAuto = "1. Auto mode";
        private const string SecStages = "2. Stages";
        private const string SecFinish = "3. Finish";
        private const string SecTimings = "4. Timings";
        private const string SecHotkeys = "5. Hotkeys";
        private const string SecDebug = "6. Debug";
        private const string SecFixes = "7. Game Fixes";

        public static ConfigEntry<bool> ShowPanel { get; set; }
        public static ConfigEntry<KeyboardShortcut> AutoKey { get; set; }

        public static ConfigEntry<KeyboardShortcut> StopMale { get; set; }
        public static ConfigEntry<KeyboardShortcut> StopFemale { get; set; }

        public static ConfigEntry<bool> AllowAllPositions { get; set; }
        public static ConfigEntry<bool> UseWholeLocation { get; set; }

        public static ConfigEntry<int> StartWaitMin { get; set; }
        public static ConfigEntry<int> StartWaitMax { get; set; }
        public static ConfigEntry<int> RestartWaitMin { get; set; }
        public static ConfigEntry<int> RestartWaitMax { get; set; }
        public static ConfigEntry<int> LoopChangeWaitMin { get; set; }
        public static ConfigEntry<int> LoopChangeWaitMax { get; set; }
        public static ConfigEntry<int> PositionChangeWaitMin { get; set; }
        public static ConfigEntry<int> PositionChangeWaitMax { get; set; }
        public static ConfigEntry<int> PulloutWaitMin { get; set; }
        public static ConfigEntry<int> PulloutWaitMax { get; set; }
        public static ConfigEntry<int> SpankingWaitMin { get; set; }
        public static ConfigEntry<int> SpankingWaitMax { get; set; }
        public static ConfigEntry<int> ChanceToRestartPosition { get; set; }
        public static ConfigEntry<int> PreferenceBonus { get; set; }
        public static ConfigEntry<int> SweetSpotChance { get; set; }
        public static ConfigEntry<bool> UseStageSequences { get; set; }
        // Stage slots (1.15.0): type and number of positions of each stage, in order
        public const int StageSlots = 8;
        public static ConfigEntry<StageType>[] StageTypes = new ConfigEntry<StageType>[StageSlots];
        public static ConfigEntry<int>[] StagePositions = new ConfigEntry<int>[StageSlots];
        public static ConfigEntry<AfterLastStageType> AfterLastStage { get; set; }
        public static ConfigEntry<int> PainPositionTime { get; set; }
        public static ConfigEntry<TCGoal> Goal { get; set; }
        public static ConfigEntry<bool> Masochism { get; set; }
        public static ConfigEntry<bool> Sadism { get; set; }
        public static ConfigEntry<bool> AnalAllowed { get; set; }
        public static ConfigEntry<bool> SexTrait { get; set; }
        public static ConfigEntry<bool> ChangePositionOnlyAfterFinish { get; set; }
        public static ConfigEntry<SexFinishType> SexFinish { get; set; }
        public static ConfigEntry<ServiceFinishType> ServiceFinish { get; set; }
        public static ConfigEntry<bool> DetailedLog { get; set; }
        public static ConfigEntry<bool> RestPositionFix { get; set; }
        public static ConfigEntry<int> InsertedChanceToPullOut { get; set; }
        public static ConfigEntry<int> NonInsertedChanceToPullOut { get; set; }

        public static HS2SmartAutoH Instance { get; set; }

        internal BepInEx.Logging.ManualLogSource Log { get { return Logger; } }

        public HS2SmartAutoH()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Singleton Only.");
            }

            Instance = this;

            const string restart = " [Change Requires Restart]";

            // No own settings yet: they are taken over once from HS2TakeChargePlugin after the binds
            bool freshConfig = !File.Exists(Config.ConfigFilePath);

            // Values renamed in 1.12.1: goal Off -> Free, SERVICE finish Game was removed
            ReplaceStoredValue(SecAuto, "Goal", "Off", "Free");
            ReplaceStoredValue(SecFinish, "SERVICE Finish", "Game", "Random");

            // 1. Auto mode
            ShowPanel = Bind(SecAuto, "Show Panel", true, "Show the Auto mode panel in the H scene (top right, left of the game's position menu): goal, Auto on / off, reset of the stages, Masochism and Sadism. It shows the same settings as this menu.", 100, null, null);
            Goal = Bind(SecAuto, "Goal", TCGoal.Free, "Feeling to raise. Positions that raise it strongly are chosen first, then mildly, then neutral; positions that lower it only if nothing else is available. Uses the game's own position table. FFM / MMF = only positions for three (two girls / two men or futas): only the SERVICE and SEX stages are used. A change is used from the next position.", 90, "Options", "Auto Mode - Goal");
            AnalAllowed = Bind(SecAuto, "Anal", false, "Allow positions the game marks as anal (anal sex, anal caress, double penetration, tentacles in the SPECIAL stage, restraint toys). Off: they are not used, and every finish raises the girl's H experience (resistH) instead of her anal experience. A change is used from the next position.", 85, null, null);
            SexTrait = Bind(SecAuto, "Sex Trait", true, "Take the main girl's sex trait into account. Sensitive Breasts / Ass / Pussy, Love Kisses: a position of her kind goes first in each stage and is kept until its finish (Sensitive Ass needs Anal on). Clean Freak: positions with an outside finish only are used last. Sadist / Masochist: SEX finish together until he finishes / she climaxes once. FFM: the game does not count the second girl's trait. The game's own fix (the trait counts every position of the scene, not only the last one) is always on. A change is used from the next position.", 84, null, null);
            Masochism = Bind(SecAuto, "Masochism", false, "Allow pain positions (Desk/Chair Spanking, Whipping, Wooden Horses, G-Spot Attack) in the SPECIAL stage. Each pain position lasts at most 'Pain Position Time'. A girl with the Masochist trait or full pain experience can climax there; others cannot (the game stops her gauge at 74%), but the time still raises her pain experience. A change is used from the next position.", 80, "Options", "Auto Mode - Masochism");
            PainPositionTime = Bind(SecAuto, "Pain Position Time", 90, "Longest time (seconds) in a pain position in Auto mode. If she has not climaxed by then, the position is changed without a finish. Also used when the Masochist trait would keep the position. Spanking ends only by this time or by her climax.", 79, null, null, new AcceptableValueRange<int>(30, 300));
            Sadism = Bind(SecAuto, "Sadism", false, "Positions where she leads go first among the positions that suit the goal. Other positions stay available. A change is used from the next position.", 70, "Options", "Auto Mode - Sadism");
            ChangePositionOnlyAfterFinish = Bind(SecAuto, "Change Position Only After Finish", true, "Do not switch positions in the middle of the act, only after a finish." + restart, 60, "Options", "Auto Mode - Change Position Only After Finish");
            PreferenceBonus = Bind(SecAuto, "Preference Bonus", 100, "Bonus the game gives to positions matching the girl's preferences when it picks the next position (game default 10)." + restart, 50, "Timings", "Auto Mode - Preference Bonus");
            SweetSpotChance = Bind(SecAuto, "Sweet Spot Chance", 100, "How often the autopilot aims for the girl's own sweet-spot speed, set per personality by the game (game default 50). Used from the next H scene.", 45, null, null, new AcceptableValueRange<int>(0, 100));
            UseWholeLocation = Bind(SecAuto, "Use Whole Location", true, "On: Auto mode picks positions from the whole location; the game moves the characters to the right spot when needed. Off: only positions the game plays right where the characters are, without moving them (the spot where Auto mode was turned on, or where they stand when L is turned off). A change is used from the next position. Also the L button of the panel.", 42, null, null);
            AllowAllPositions = Bind(SecAuto, "Ignore Location Animation Limits", false, "Some animations won't fit the location...", 40, "Options", "Auto Mode - Ignore Location Animation Limits");

            // 2. Stages
            UseStageSequences = Bind(SecStages, "Use Stage Sequences", true, "Follow the stages below in order (Stage 1 first). A stage with no available positions is skipped. Off: any position at any time.", 100, "Options", "Auto Mode - Use Stage Sequences");
            StageType[] defaultTypes = new StageType[] { StageType.SERVICE, StageType.CARESS, StageType.SPECIAL, StageType.SEX, StageType.None, StageType.None, StageType.None, StageType.None };
            int[] defaultPositions = new int[] { 2, 2, 1, 3, 0, 0, 0, 0 };
            for (int i = 0; i < StageSlots; i++)
            {
                int slot = i;
                StageTypes[i] = Bind(SecStages, string.Format("Stage {0} Type", i + 1), defaultTypes[i], "What this stage uses. SPECIAL = tentacles, masturbation and (with Masochism) pain positions. None = the stage is not used (its positions are always 0). FFM / MMF use only SERVICE and SEX stages. A stage may repeat, e.g. CARESS -> SEX -> CARESS -> SEX.", 90 - i * 2, null, null);
                StagePositions[i] = Bind(SecStages, string.Format("Stage {0} Positions", i + 1), defaultPositions[i], "Number of positions in this stage: 1 - 10, or 0 for a None stage (set automatically).", 89 - i * 2, null, null, new AcceptableValueRange<int>(0, 10));
                StageTypes[i].SettingChanged += (sender, args) => NormalizeStage(slot, true);
                StagePositions[i].SettingChanged += (sender, args) => NormalizeStage(slot, true);
                NormalizeStage(slot, false);
            }
            AfterLastStage = Bind(SecStages, "After Last Stage", AfterLastStageType.Stay, "What Auto mode does after the last stage: Stay = keeps choosing positions of the last stage (stops when none is left), Loop = starts again from the first stage, Free = any position.", 70, null, null);

            // 3. Finish
            SexFinish = Bind(SecFinish, "SEX Finish", SexFinishType.Same, "Finish in SEX (like BetterHScenes): whoever reaches the top first waits for the partner (98%), then: Same = finish together, Inside / Outside = he finishes inside / outside, Random = any available. If the type is not possible in the position another one is used. Game = the mod does not interfere (whoever is first). Also on the panel.", 100, "Options", "Auto Mode - SEX Finish");
            ServiceFinish = Bind(SecFinish, "SERVICE Finish", ServiceFinishType.Random, "Finish in SERVICE when his gauge reaches 98%: Drink, Vomit (spit out), Outside, Random. If the position does not allow it, he finishes outside. Also on the panel.", 90, "Options", "Auto Mode - SERVICE Finish");
            ChanceToRestartPosition = Bind(SecFinish, "% Position Change After Finish", 80, "Percentage chance to change positions after a finish." + restart, 80, "Timings", "% Position Change");
            InsertedChanceToPullOut = Bind(SecFinish, "% Chance to Pull Out", 70, "Percentage chance to pull out after a finish (does not affect the finish type). Applies only if inserted." + restart, 70, "Timings", "% Chance to Pull Out");
            NonInsertedChanceToPullOut = Bind(SecFinish, "% Chance to Pull Out When Not Inserted", 30, "Some positions have a Pull Out animation even when not inserted. % chance to play it." + restart, 60, "Timings", "% Chance to Pull Out When Not Inserted");

            // 4. Timings
            StartWaitMin = Bind(SecTimings, "Start Wait Min", 3, "Minimum time (seconds) to wait at the start of a position." + restart, 100, "Timings", "Start Wait Min");
            StartWaitMax = Bind(SecTimings, "Start Wait Max", 7, "Maximum time (seconds) to wait at the start of a position." + restart, 99, "Timings", "Start Wait Max");
            RestartWaitMin = Bind(SecTimings, "Restart Wait Min", 10, "Minimum time (seconds) to wait before restarting a position." + restart, 90, "Timings", "Restart Wait Min");
            RestartWaitMax = Bind(SecTimings, "Restart Wait Max", 15, "Maximum time (seconds) to wait before restarting a position." + restart, 89, "Timings", "Restart Wait Max");
            LoopChangeWaitMin = Bind(SecTimings, "Loop Change Wait Min", 4, "Minimum time (seconds) before switching loops (slow -> fast or fast -> slow)." + restart, 80, "Timings", "Loop Change Wait Min");
            LoopChangeWaitMax = Bind(SecTimings, "Loop Change Wait Max", 10, "Maximum time (seconds) before switching loops (slow -> fast or fast -> slow)." + restart, 79, "Timings", "Loop Change Wait Max");
            PulloutWaitMin = Bind(SecTimings, "Pullout Wait Min", 6, "Minimum time (seconds) to wait before pulling out after a finish (if relevant)." + restart, 70, "Timings", "Pullout Wait Min");
            PulloutWaitMax = Bind(SecTimings, "Pullout Wait Max", 9, "Maximum time (seconds) to wait before pulling out after a finish (if relevant)." + restart, 69, "Timings", "Pullout Wait Max");
            SpankingWaitMin = Bind(SecTimings, "Spanking Wait Min", 1, "Minimum time (seconds) between spanks." + restart, 60, "Timings", "Spanking Wait Min");
            SpankingWaitMax = Bind(SecTimings, "Spanking Wait Max", 4, "Maximum time (seconds) between spanks." + restart, 59, "Timings", "Spanking Wait Max");
            PositionChangeWaitMin = Bind(SecTimings, "Position Change Wait Min", 15, "Minimum time (seconds) before switching positions in the middle of the act. Used only when '1. Auto mode / Change Position Only After Finish' is off." + restart, 10, "Timings", "Position Change Wait Min");
            PositionChangeWaitMax = Bind(SecTimings, "Position Change Wait Max", 40, "Maximum time (seconds) before switching positions in the middle of the act. Used only when '1. Auto mode / Change Position Only After Finish' is off." + restart, 9, "Timings", "Position Change Wait Max");

            // 5. Hotkeys
            AutoKey = Bind(SecHotkeys, "Enable Auto Mode", new KeyboardShortcut(KeyCode.None), "Auto mode on / off (same as the Auto button of the panel). Empty by default: Ctrl+A is used by the original Take Charge plugin", 100, "Hotkeys", "Enable Auto Mode");
            StopMale = Bind(SecHotkeys, "Stop Male Arousal", new KeyboardShortcut(KeyCode.None), "Lock / unlock his gauge", 90, "Hotkeys", "Stop Male Arousal");
            StopFemale = Bind(SecHotkeys, "Stop Female Arousal", new KeyboardShortcut(KeyCode.None), "Lock / unlock her gauge", 80, "Hotkeys", "Stop Female Arousal");

            // 6. Debug
            DetailedLog = Bind(SecDebug, "Detailed Log", true, "Write chosen positions, stages, finish sync and finish choice to the BepInEx log", 100, "Options", "Debug - Detailed Log");

            // 7. Game Fixes
            RestPositionFix = Bind(SecFixes, "Start From Pause After Climax", true, "A new position of the same kind chosen while the characters rest after a climax or a finish starts from the pause, like after a normal stop. Off: the game starts it moving at once (its own behaviour). A change in the middle of the act always keeps moving. Manual play and Auto mode.", 100, null, null);

            RemoveOldEntries();
            if (freshConfig)
            {
                TakeOverOldSettings();
            }

            // A change of the goal, Masochism or Sadism is used from the next position: the panel says so
            Goal.SettingChanged += (sender, args) =>
            {
                TCPanel.ShowNextPositionHint();
                HSceneFlagCtrl f;
                if (TCGoals.IsGroup(Goal.Value) && InHScene(out f) && f.initiative == 2 && GroupPositionsAvailable(Goal.Value) == 0)
                {
                    StopAuto("no " + Goal.Value + " positions here");
                }
            };
            Masochism.SettingChanged += (sender, args) => TCPanel.ShowNextPositionHint();
            Sadism.SettingChanged += (sender, args) => TCPanel.ShowNextPositionHint();
            AnalAllowed.SettingChanged += (sender, args) => TCPanel.ShowNextPositionHint();
            SexTrait.SettingChanged += (sender, args) => TCPanel.ShowNextPositionHint();
            UseWholeLocation.SettingChanged += (sender, args) => TCPanel.ShowNextPositionHint();
            UseWholeLocation.SettingChanged += (sender, args) => LogSetting("Use Whole Location", UseWholeLocation.Value);

            // Changes from the panel or the F1 menu are written to the log (1.14.1)
            Goal.SettingChanged += (sender, args) => LogSetting("Goal", Goal.Value);
            AnalAllowed.SettingChanged += (sender, args) => LogSetting("Anal", AnalAllowed.Value);
            SexTrait.SettingChanged += (sender, args) => LogSetting("Sex Trait", SexTrait.Value);
            Masochism.SettingChanged += (sender, args) => LogSetting("Masochism", Masochism.Value);
            Sadism.SettingChanged += (sender, args) => LogSetting("Sadism", Sadism.Value);
            SexFinish.SettingChanged += (sender, args) => LogSetting("SEX Finish", SexFinish.Value);
            PainPositionTime.SettingChanged += (sender, args) => LogSetting("Pain Position Time", PainPositionTime.Value);
            AfterLastStage.SettingChanged += (sender, args) => LogSetting("After Last Stage", AfterLastStage.Value);
            ServiceFinish.SettingChanged += (sender, args) => LogSetting("SERVICE Finish", ServiceFinish.Value);

            PatchMe();
        }

        private void LogSetting(string name, object value)
        {
            Logger.LogInfo(string.Format("[Settings] {0} = {1}", name, value));
        }

        // A None stage has 0 positions, any other stage 1 - 10 (1.15.0)
        private static bool normalizing = false;
        private static readonly string[] stageLogged = new string[StageSlots];
        private void NormalizeStage(int slot, bool log)
        {
            if (normalizing || StageTypes[slot] == null || StagePositions[slot] == null)
            {
                return;
            }
            normalizing = true;
            try
            {
                bool none = StageTypes[slot].Value == StageType.None;
                int positions = StagePositions[slot].Value;
                int wanted = none ? 0 : Math.Min(Math.Max(positions, 1), 10);
                if (positions != wanted)
                {
                    StagePositions[slot].Value = wanted;
                }
            }
            finally
            {
                normalizing = false;
            }
            string now = StageTypes[slot].Value + " " + StagePositions[slot].Value;
            if (log && now != stageLogged[slot])
            {
                // only a real change is written (dragging a slider to 0 used to write the same line many times)
                TCPanel.ShowNextPositionHint();
                LogSetting(string.Format("Stage {0}", slot + 1), now);
            }
            stageLogged[slot] = now;
        }

        // ---------- Config: bind with menu order, move values from the old (1.11) names ----------

        private int movedEntries = 0;
        private static PropertyInfo orphanedEntriesProperty = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");

        private Dictionary<ConfigDefinition, string> OrphanedEntries()
        {
            try
            {
                return orphanedEntriesProperty == null ? null : orphanedEntriesProperty.GetValue(Config, null) as Dictionary<ConfigDefinition, string>;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private readonly List<ConfigEntryBase> allEntries = new List<ConfigEntryBase>();

        private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, int order, string oldSection, string oldKey, AcceptableValueBase range = null)
        {
            ConfigurationManagerAttributes attributes = new ConfigurationManagerAttributes();
            attributes.Order = order;
            ConfigEntry<T> entry = Config.Bind(section, key, defaultValue, new ConfigDescription(description, range, attributes));
            allEntries.Add(entry);
            if (oldSection == null)
            {
                return entry;
            }
            Dictionary<ConfigDefinition, string> orphans = OrphanedEntries();
            ConfigDefinition old = new ConfigDefinition(oldSection, oldKey);
            string value;
            if (orphans != null && orphans.TryGetValue(old, out value))
            {
                try
                {
                    entry.SetSerializedValue(value);
                    movedEntries++;
                }
                catch (Exception e)
                {
                    Logger.LogWarning(string.Format("[Config] Could not move '{0} / {1}': {2}", oldSection, oldKey, e.Message));
                }
                orphans.Remove(old);
            }
            return entry;
        }

        // First start of HS2 Smart Auto H (0.9.0): the settings of HS2TakeChargePlugin are copied once.
        // Its file stays as it is. Not copied: the Auto hotkey (Ctrl+A belongs to the original plugin) and the voice
        // settings (moved to HS2 Girl H Voice Options). Sections 5-7 of the old menu are 4-6 here.
        private void TakeOverOldSettings()
        {
            try
            {
                string oldPath = Path.Combine(Paths.ConfigPath, OldGuid + ".cfg");
                if (!File.Exists(oldPath))
                {
                    return;
                }
                Dictionary<string, string> values = new Dictionary<string, string>();
                string section = null;
                foreach (string raw in File.ReadAllLines(oldPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || section == null)
                    {
                        continue;
                    }
                    values[section + "|" + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                int copied = 0;
                foreach (ConfigEntryBase entry in allEntries)
                {
                    ConfigDefinition def = entry.Definition;
                    if (def.Section == SecHotkeys && def.Key == "Enable Auto Mode")
                    {
                        continue;
                    }
                    string oldSection = def.Section == SecTimings ? "5. Timings" : def.Section == SecHotkeys ? "6. Hotkeys" : def.Section == SecDebug ? "7. Debug" : def.Section;
                    string value;
                    if (values.TryGetValue(oldSection + "|" + def.Key, out value))
                    {
                        try
                        {
                            entry.SetSerializedValue(value);
                            copied++;
                        }
                        catch (Exception e)
                        {
                            Logger.LogWarning(string.Format("[Config] Could not take over '{0} / {1}': {2}", def.Section, def.Key, e.Message));
                        }
                    }
                }
                Config.Save();
                Logger.LogInfo(string.Format("[Config] Settings taken over from HS2TakeChargePlugin: {0}", copied));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[Config] Could not take over the settings of HS2TakeChargePlugin: " + e.Message);
            }
        }

        // A value saved under an old name is changed before the setting is read
        private void ReplaceStoredValue(string section, string key, string oldValue, string newValue)
        {
            Dictionary<ConfigDefinition, string> orphans = OrphanedEntries();
            ConfigDefinition def = new ConfigDefinition(section, key);
            string value;
            if (orphans != null && orphans.TryGetValue(def, out value) && value != null && value.Trim() == oldValue)
            {
                orphans[def] = newValue;
                movedEntries++;
            }
        }

        // Settings removed in 1.12.0 (Take Charge mode, speed override, rules file, ...) and leftovers of older versions
        private void RemoveOldEntries()
        {
            string[,] removed = new string[,]
            {
                { "Hotkeys", "Enable Take Charge Mode" },
                { "Hotkeys", "Manual H-Anim Visual Speed (Increase)" },
                { "Hotkeys", "Manual H-Anim Visual Speed (Decrease)" },
                { "Hotkeys", "Manual H-Anim Visual Speed (Reset)" },
                { "Options", "Manual H-Anim Visual Speed Increment" },
                { "Options", "Reset to Idle on Pos Change" },
                { "Options", "Reset Arousal Meters on Pos Change" },
                { "Options", "Enable Auto Speed Override" },
                { "Options", "Speed Override All Modes" },
                { "Options", "Anim Rules File" },
                { "Timings", "Auto Mode - Sweet Spot Chance" },
                { "Options", "Auto Mode - Sync Finish in SEX" },
                { "Options", "Auto Mode - SERVICE Finish Choice" },
                { "Options", "Auto Mode - Use Position Rules" },
                { SecStages, "Stage 1 SERVICE Changes" },
                { SecStages, "Stage 2 CARESS Changes" },
                { SecStages, "Stage 3 SPECIAL Changes" },
                { SecStages, "Stage 4 SEX Changes" }
            };
            Dictionary<ConfigDefinition, string> orphans = OrphanedEntries();
            int dropped = 0;
            if (orphans != null)
            {
                for (int i = 0; i < removed.GetLength(0); i++)
                {
                    if (orphans.Remove(new ConfigDefinition(removed[i, 0], removed[i, 1])))
                    {
                        dropped++;
                    }
                }
            }
            if (movedEntries > 0 || dropped > 0)
            {
                Config.Save();
                Logger.LogInfo(string.Format("[Config] Settings moved to the new menu: {0}, old settings removed: {1}", movedEntries, dropped));
            }
        }

        // ---------- Auto mode on / off and reset (hotkey and panel) ----------

        private static bool InHScene(out HSceneFlagCtrl flags)
        {
            flags = Singleton<HSceneFlagCtrl>.Instance;
            HSceneManager manager = Singleton<HSceneManager>.Instance;
            return flags != null && manager != null && manager.Hscene != null;
        }

        public void ToggleAuto()
        {
            HSceneFlagCtrl flags;
            if (!InHScene(out flags))
            {
                return;
            }
            if (flags.initiative != 2)
            {
                if (IsFainted(flags))
                {
                    Log.LogMessage("Auto H: not started - she has fainted");
                    return;
                }
                if (TCGoals.IsGroup(Goal.Value) && GroupPositionsAvailable(Goal.Value) == 0)
                {
                    Log.LogMessage("Auto H: not started - no " + Goal.Value + " positions here");
                    return;
                }
                AutoStartPlace = flags.nPlace;
                Log.LogMessage("Auto H: Enabled");
                flags.initiative = 2;
            }
            else
            {
                Log.LogMessage("Auto H: Disabled");
                flags.initiative = 0;
                GeneralHooks.ForcePositionChange = false;
                RestorePositionMenu();
                RefreshFinishButtons(flags);
            }
        }

        // Auto mode off with a reason shown in the corner; the finish buttons are rebuilt for manual play
        public void StopAuto(string reason)
        {
            HSceneFlagCtrl flags;
            if (!InHScene(out flags) || flags.initiative != 2)
            {
                return;
            }
            flags.initiative = 0;
            GeneralHooks.ForcePositionChange = false;
            Log.LogMessage("Auto H: Disabled (" + reason + ")");
            RestorePositionMenu();
            RefreshFinishButtons(flags);
        }

        // ---------- She fainted (1.13.0) ----------
        private static FieldInfo faintField = AccessTools.Field(typeof(HSceneFlagCtrl), "isFaintness");

        public static bool IsFainted(HSceneFlagCtrl flags)
        {
            try
            {
                return flags != null && faintField != null && (bool)faintField.GetValue(flags);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Auto mode ends where she faints: the player goes on by hand
        private void FaintUpdate()
        {
            HSceneFlagCtrl flags;
            if (InHScene(out flags) && flags.initiative == 2 && IsFainted(flags))
            {
                StopAuto("she fainted");
            }
        }

        // The game builds the finish buttons only when a position starts or she faints, and shows none in Auto mode.
        // After Auto mode is turned off in the middle of a position it is asked to build them again.
        private static MethodInfo finishSelect = AccessTools.Method(typeof(HSceneSprite), "SetFinishSelect", new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) });

        private void RefreshFinishButtons(HSceneFlagCtrl flags)
        {
            try
            {
                HSceneSprite sprite = Singleton<HSceneSprite>.Instance;
                if (sprite == null || finishSelect == null || flags.nowAnimationInfo == null)
                {
                    return;
                }
                int item1 = flags.nowAnimationInfo.ActionCtrl.Item1;
                int item2 = flags.nowAnimationInfo.ActionCtrl.Item2;
                int ctrl = GetCurrentModeControl();
                if (item1 == 2)
                {
                    finishSelect.Invoke(sprite, new object[] { 2, ctrl, item1, item2 });     // sex (as Sonyu does)
                }
                else if (item1 == 1)
                {
                    finishSelect.Invoke(sprite, new object[] { 1, ctrl, item1, item2 });     // service (1.14.1)
                }
                else if (item1 == 5 || item1 == 6)
                {
                    finishSelect.Invoke(sprite, new object[] { 7, ctrl, -1, -1 });          // positions for three
                }
                else
                {
                    return;
                }
                LogDetailed("[Finish] Finish buttons rebuilt for manual play");
            }
            catch (Exception e)
            {
                Log.LogWarning("[Finish] Could not rebuild the finish buttons: " + e.Message);
            }
        }

        // ---------- Goals FFM / MMF (1.13.0) ----------
        private static FieldInfo sceneAnimListField = AccessTools.Field(typeof(HScene), "lstAnimInfo");

        // How many positions for three of this goal the game's menu allows now
        public int GroupPositionsAvailable(TCGoal goal)
        {
            int count = 0;
            try
            {
                HScene scene = Singleton<HSceneManager>.Instance.Hscene;
                List<HScene.AnimationListInfo>[] lists = sceneAnimListField == null ? null : sceneAnimListField.GetValue(scene) as List<HScene.AnimationListInfo>[];
                if (lists == null)
                {
                    return 1;   // cannot tell: let the selection decide
                }
                int wanted = goal == TCGoal.FFM ? 7 : 8;
                ChaControl[] females = scene.GetFemales();
                ChaControl[] males = scene.GetMales();
                bool present = wanted == 7 ? (females.Length > 1 && females[1] != null) : (males.Length > 1 && males[1] != null);
                foreach (List<HScene.AnimationListInfo> list in lists)
                {
                    foreach (HScene.AnimationListInfo info in list)
                    {
                        if (DetermineModeByActionCtrl(info.ActionCtrl.Item1, info.ActionCtrl.Item2) != wanted)
                        {
                            continue;
                        }
                        int inMenu = GameMenuCheck(info);
                        if (inMenu == 1 || (inMenu == -1 && present))
                        {
                            count++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("[Goal] Could not count the positions for three: " + e.Message);
                return 1;
            }
            LogDetailed(string.Format("[Goal] {0}: {1} positions for three allowed here", goal, count));
            return count;
        }

        // Reset button: stages start again from Stage 1, position use counts are cleared.
        // With Auto on the position changes at once (in a loop) or right after the finish / when the movement starts.
        public void ResetStages()
        {
            HSceneFlagCtrl flags;
            if (!InHScene(out flags))
            {
                return;
            }
            TCSequenceTracker.Reset();
            ResetPositionUse();
            if (flags.initiative == 2)
            {
                Log.LogMessage("Auto H: stages reset, changing the position");
                GeneralHooks.RequestPositionChange("reset button");
            }
            else
            {
                GeneralHooks.ForcePositionChange = false;
                Log.LogMessage("Auto H: stages reset");
            }
        }

        void OnGUI()
        {
            TCPanel.Draw();
        }

        void Update()
        {
            if (TCSequenceTracker.StopRequested)
            {
                TCSequenceTracker.StopRequested = false;
                try
                {
                    if (Singleton<HSceneFlagCtrl>.Instance != null && Singleton<HSceneFlagCtrl>.Instance.initiative == 2)
                    {
                        StopAuto(TCSequenceTracker.StopReason ?? "no positions left in the final stage");
                    }
                    TCSequenceTracker.StopReason = null;
                }
                catch (Exception e)
                {
                    Log.LogWarning("[Sequence] Could not stop Auto mode: " + e.Message);
                }
            }
            FaintUpdate();
            TCWatch.Update();
            TCPain.Update();
            FinishSyncUpdate();
            try
            {
                // SERVICE: a new finish can be commanded once the previous one is over
                HSceneFlagCtrl serviceFlags = Singleton<HSceneFlagCtrl>.Instance;
                if (serviceFlags != null && serviceFlags.feel_m < 0.5f)
                {
                    HoushiHooks.ResetServiceFinish();
                }
            }
            catch (Exception)
            {
            }
            if (AutoKey.Value.IsUp())
            {
                ToggleAuto();
            }
            HSceneFlagCtrl flags;
            if (StopMale.Value.IsUp() && InHScene(out flags) && Singleton<HSceneSprite>.Instance != null)
            {
                Singleton<HSceneSprite>.Instance.objGaugeLockM.isOn = !Singleton<HSceneSprite>.Instance.objGaugeLockM.isOn;
                Log.LogMessage("Male Arousal " + (flags.stopFeelMale ? "Locked" : "Unlocked"));
            }
            if (StopFemale.Value.IsUp() && InHScene(out flags) && Singleton<HSceneSprite>.Instance != null)
            {
                Singleton<HSceneSprite>.Instance.objGaugeLockF.isOn = !Singleton<HSceneSprite>.Instance.objGaugeLockF.isOn;
                Log.LogMessage("Female Arousal " + (flags.stopFeelFemale ? "Locked" : "Unlocked"));
            }
        }

        public void PatchMe()
        {
            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(GeneralHooks));
            harmony.PatchAll(typeof(AibuHooks));
            harmony.PatchAll(typeof(HoushiHooks));
            harmony.PatchAll(typeof(LesHooks));
            harmony.PatchAll(typeof(MasturbationHooks));
            harmony.PatchAll(typeof(MultiplayF1M2Hooks));
            harmony.PatchAll(typeof(MultiplayF2M1Hooks));
            harmony.PatchAll(typeof(PeepingHooks));
            harmony.PatchAll(typeof(SonyuHooks));
            harmony.PatchAll(typeof(SpnkingHooks));
            try
            {
                harmony.PatchAll(typeof(RestPoseHooks));
                harmony.PatchAll(typeof(RestPoseStartHooks));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[RestFix] Could not hook the position start, the rest fix is off: " + e.Message);
            }
            try
            {
                harmony.PatchAll(typeof(SonyuFinishHooks));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[Finish] Could not hook the SEX finish point, man's finish fix is off: " + e.Message);
            }
            try
            {
                harmony.PatchAll(typeof(TraitHooks));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[Trait] Could not hook the trait flags, the game's fix is off: " + e.Message);
            }
            try
            {
                harmony.PatchAll(typeof(MultiFinishHooks));
            }
            catch (Exception e)
            {
                Logger.LogWarning("[Finish] Could not hook the finish of the positions for three: " + e.Message);
            }
        }

        public void LogDetailed(string message)
        {
            if (DetailedLog.Value)
            {
                Log.LogInfo(message);
            }
        }

        private static FieldInfo modeControlFieldInfo = AccessTools.Field(typeof(HScene), "modeCtrl");
        public int GetCurrentModeControl()
        {
            return (int)modeControlFieldInfo.GetValue(Singleton<HSceneManager>.Instance.Hscene);
        }        

        public int DetermineModeByActionCtrl(int info1, int info2)
        {
            switch (info1)
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 2;
                case 3:
                    {
                        switch (info2)
                        {
                            case 0: return 2;
                            case 1: return 2;
                            case 2: return 3;
                            case 3: return 0;
                            case 4: return 4;
                            case 5: return 5;
                            case 6: return 5;
                            case 7: return 2;
                            default: return 2;
                        }
                    }
                case 4: return 6;
                case 5: return 7;
                case 6: return 8;
                default: return 0;
            }
        }

        // ---------- Finish sync (SEX, Auto mode) ----------
        // Whoever is about to finish first waits just below the top of the gauge.
        // The finish itself is started at the game's decision point (SonyuFinishHooks).
        public const float SyncHoldLevel = 0.98f;
        private bool syncErrorLogged = false;

        private void FinishSyncUpdate()
        {
            try
            {
                if (TCTrait.SexFinish(false) == SexFinishType.Game)
                {
                    return;
                }
                HSceneManager manager = Singleton<HSceneManager>.Instance;
                if (manager == null || manager.Hscene == null)
                {
                    return;
                }
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                if (flags == null || flags.initiative != 2)
                {
                    return;
                }
                int mode = DetermineModeByActionCtrl(flags.nowAnimationInfo.ActionCtrl.Item1, flags.nowAnimationInfo.ActionCtrl.Item2);
                int sub = flags.nowAnimationInfo.ActionCtrl.Item2;
                bool groupSex = (mode == 7 || mode == 8) && (sub == 3 || sub == 4);
                if (mode != (int)PositionCategories.SEX && !groupSex)
                {
                    return;
                }
                // A finish was requested: do not hold the gauges, or the finish gets cancelled
                if (SonyuFinishHooks.FinishInProgress)
                {
                    SonyuFinishHooks.TrackFinish(flags);
                    return;
                }
                if (flags.feel_m > SyncHoldLevel)
                {
                    flags.feel_m = SyncHoldLevel;
                }
                if (flags.feel_f > SyncHoldLevel)
                {
                    flags.feel_f = SyncHoldLevel;
                }
            }
            catch (Exception e)
            {
                if (!syncErrorLogged)
                {
                    syncErrorLogged = true;
                    Log.LogWarning("[Finish] Finish sync error (logged once): " + e.Message);
                }
            }
        }

        // Logged once per H scene so the user can find character names and personality numbers
        internal static bool sequenceInfoLogged = false;

        // What the game was allowed to pick from on the last call
        private List<TCCandidate> lastCandidates = new List<TCCandidate>();
        private List<HScene.AnimationListInfo>[] lastFilteredList = null;
        // Spot of the location where Auto mode started; positions are checked against it,
        // so a special spot (e.g. a wall) does not lock the autopilot into that spot's positions
        public static int AutoStartPlace = -1;
        private bool startMotionFieldsLogged = false;

        // Step 1 (before the game picks): build the list of positions the game may choose from.
        public List<HScene.AnimationListInfo>[] FilterAnimationList(List<HScene.AnimationListInfo>[] animList)
        {
            List<TCCandidate> candidates = BuildCandidates(animList);

            // Never offer the position that is playing now: the game cannot switch to the same
            // position and the animation breaks (seen in 1.11.0 with Analingus twice in a row)
            candidates = RemoveCurrentPosition(candidates);
            candidates = TCWatch.RemoveFailed(candidates);
            List<TCCandidate> beforeStages = candidates;
            bool firstOfStage = positionUse.Count == 0;

            // Stage sequence from the menu: keep only the positions of the current stage
            if (UseStageSequences.Value)
            {
                TCSequence sequence = BuildMenuSequence();
                if (!sequenceInfoLogged)
                {
                    sequenceInfoLogged = true;
                    LogSceneSettings(sequence);
                }
                candidates = TCSequenceTracker.Apply(candidates, sequence);
                firstOfStage = TCSequenceTracker.FirstOfStage;
            }
            else if (!sequenceInfoLogged)
            {
                sequenceInfoLogged = true;
                LogSceneSettings(null);
            }

            // FFM / MMF: every position of the stage was used - repeat the positions for three instead of stopping
            if (TCGoals.IsGroup(Goal.Value) && candidates.Count == 0 && beforeStages.Count > 0)
            {
                TCSequenceTracker.StopRequested = false;
                TCSequenceTracker.StopReason = null;
                candidates = beforeStages;
                LogDetailed(string.Format("[Goal] {0}: no new position in this stage, positions for three are repeated", Goal.Value));
            }

            // Sex trait (1.14.0): her kind of position first in the stage, Clean Freak avoids outside-only positions
            candidates = TCTrait.Apply(candidates, firstOfStage);

            // Positions not used yet in this scene go first (1.15.0), so a repeated stage does not repeat its positions.
            // The goal stays more important: used positions are taken if the new ones would only lower the goal.
            List<TCCandidate> fresh = candidates.FindAll((c) => PositionUses(c) == 0);
            if (fresh.Count > 0 && fresh.Count < candidates.Count)
            {
                int freshGroup;
                int allGroup;
                ApplyGoal(fresh, false, out freshGroup);
                ApplyGoal(candidates, false, out allGroup);
                if (freshGroup == GoalLowersGroup && allGroup >= 0 && allGroup < GoalLowersGroup)
                {
                    LogDetailed(string.Format("[Select] The {0} positions not used yet would lower the goal: used positions are taken again", fresh.Count));
                }
                else
                {
                    LogDetailed(string.Format("[Select] {0} of {1} positions were not used yet in this scene: they go first", fresh.Count, candidates.Count));
                    candidates = fresh;
                }
            }

            // Goal (and Sadism): keep only the best group of positions that is available
            int goalGroup;
            candidates = ApplyGoal(candidates, true, out goalGroup);

            lastCandidates = candidates;
            if (DetailedLog.Value)
            {
                Log.LogInfo(string.Format("[Select] Game may pick from {0} positions", candidates.Count));
            }
            if (candidates.Count == 0 && TCGoals.IsGroup(Goal.Value))
            {
                // No (other) positions for three here: stop Auto mode (the game still makes this one change)
                TCSequenceTracker.StopReason = "no other " + Goal.Value + " position here";
                TCSequenceTracker.StopRequested = true;
            }
            if (candidates.Count == 0)
            {
                // Nothing left: do not break the game, let it use its own full list
                lastFilteredList = null;
                return animList;
            }

            List<HScene.AnimationListInfo>[] filtered = new List<HScene.AnimationListInfo>[animList.Length];
            lastFilteredList = filtered;
            for (int n = 0; n < animList.Length; n++)
            {
                filtered[n] = new List<HScene.AnimationListInfo>();
            }
            foreach (TCCandidate candidate in candidates)
            {
                filtered[candidate.Date.mode].Add(candidate.Info);
            }
            return filtered;
        }

        // Step 2 (after the game picked): keep the game's choice if it is allowed, otherwise pick ourselves.
        public HScene.StartMotion CheckGameChoice(HAutoCtrl autoCtrl, HScene.StartMotion gameChoice, bool isFirst)
        {
            int mode;
            int id;
            bool readable = TryReadStartMotion(gameChoice, out mode, out id);
            TCCandidate chosen = readable ? lastCandidates.Find((c) => c.Date.mode == mode && c.Date.id == id) : null;
            if (chosen != null)
            {
                LogGameChoice(chosen, 2);
                RecordUse(chosen);
                TCWatch.Picked(chosen.Info);
                return gameChoice;
            }

            if (lastCandidates.Count == 0)
            {
                return gameChoice;
            }

            // The game returns nothing usable in Auto mode: pick among the allowed positions ourselves
            HAutoCtrl.AutoRandom autoRandom = new HAutoCtrl.AutoRandom();
            foreach (TCCandidate candidate in lastCandidates)
            {
                autoRandom.Add(candidate.Date, 10f);
            }
            HAutoCtrl.AutoRandom.AutoRandomDate picked = autoRandom.Random();
            TCCandidate pickedCandidate = lastCandidates.Find((c) => c.Date.mode == picked.mode && c.Date.id == picked.id);
            RecordUse(pickedCandidate);
            if (pickedCandidate != null)
            {
                TCWatch.Picked(pickedCandidate.Info);
            }
            if (DetailedLog.Value)
            {
                Log.LogInfo(string.Format("[Select] Picked '{0}' (game choice {1})",
                    pickedCandidate == null ? "?" : pickedCandidate.Info.nameAnimation,
                    readable ? string.Format("mode {0} id {1} was not allowed", mode, id) : "could not be read"));
            }
            return new HScene.StartMotion(picked.mode, picked.id);
        }

        private void LogGameChoice(TCCandidate chosen, int initiative)
        {
            if (DetailedLog.Value)
            {
                Log.LogInfo(string.Format("[Select] GAME (initiative {0}) picked '{1}' ({2})", initiative, chosen.Info.nameAnimation, chosen.Category));
            }
        }

        private bool TryReadStartMotion(HScene.StartMotion motion, out int mode, out int id)
        {
            mode = -1;
            id = -1;
            object boxed = motion;
            if (boxed == null)
            {
                return false;
            }
            try
            {
                Traverse traverse = Traverse.Create(boxed);
                if (traverse.Field("mode").FieldExists() && traverse.Field("id").FieldExists())
                {
                    mode = traverse.Field("mode").GetValue<int>();
                    id = traverse.Field("id").GetValue<int>();
                    return true;
                }
                if (!startMotionFieldsLogged)
                {
                    startMotionFieldsLogged = true;
                    Log.LogWarning("[Select] Unknown StartMotion fields: " + string.Join(", ", AccessTools.GetFieldNames(boxed).ToArray()));
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("[Select] Could not read game choice: " + e.Message);
            }
            return false;
        }

        // ---------- Stages and goal (1.9.0) ----------
        public const string SpecialCategory = "SPECIAL";

        // How many times each position was used in this H scene (key "mode:id").
        // Every use lowers the position by one level for the goal (not below neutral).
        private static Dictionary<string, int> positionUse = new Dictionary<string, int>();
        private static string lastPickedKey = null;

        public static void ResetPositionUse()
        {
            positionUse.Clear();
            lastPickedKey = null;
        }

        private static string PositionKey(TCCandidate candidate)
        {
            return candidate.Date.mode + ":" + candidate.Date.id;
        }

        private static int PositionUses(TCCandidate candidate)
        {
            int uses;
            return positionUse.TryGetValue(PositionKey(candidate), out uses) ? uses : 0;
        }

        private static void RecordUse(TCCandidate candidate)
        {
            if (candidate == null)
            {
                return;
            }
            string key = PositionKey(candidate);
            positionUse[key] = PositionUses(candidate) + 1;
            lastPickedKey = key;
        }

        private static bool IsWoodenHorse(string name)
        {
            return name != null && (name.Contains("Horse") || name.Contains("\u6728\u99ac"));
        }

        // The game marks anal positions with system type 3; a finish in them raises resistAnal instead of resistH
        private static bool IsAnalPosition(HScene.AnimationListInfo info)
        {
            try
            {
                if (info.lstSystem != null && Enumerable.Contains(info.lstSystem, 3))
                {
                    return true;
                }
                // Some added positions have no anal mark (e.g. AnimationeXtension 204): check the name too (1.14.1)
                string name = info.nameAnimation ?? "";
                return name.Contains("\u30A2\u30CA\u30EB") || name.IndexOf("anal", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Pain positions are marked by the game with system type 4 (G-Spot Attack, Wooden Horses).
        // In them the girl's gauge stops at 74% unless she is a masochist or fully used to pain.
        internal static bool IsPainPosition(HScene.AnimationListInfo info)
        {
            try
            {
                if (info.lstSystem != null && Enumerable.Contains(info.lstSystem, 4))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return IsWoodenHorse(info.nameAnimation);
        }

        // Spanking, or a pain position that is not masturbation / toys
        internal static bool IsPainMode(HScene.AnimationListInfo info, int mode)
        {
            return mode == 3 || (mode != 4 && IsPainPosition(info));
        }

        // Tentacles A / B (game code 3:1 and 3:7): SPECIAL stage since 1.15.0
        internal static bool IsTentacle(HScene.AnimationListInfo info)
        {
            return info != null && info.ActionCtrl.Item1 == 3 && (info.ActionCtrl.Item2 == 1 || info.ActionCtrl.Item2 == 7);
        }

        // The game narrows the position menu while Auto mode is on; give back the full menu
        private static MethodInfo leaveItToYouMenu = AccessTools.Method(typeof(HSceneSprite), "MainCategoryOfLeaveItToYou");
        private void RestorePositionMenu()
        {
            try
            {
                HSceneSprite sprite = Singleton<HSceneSprite>.Instance;
                if (sprite != null && leaveItToYouMenu != null)
                {
                    leaveItToYouMenu.Invoke(sprite, new object[] { false });
                    LogDetailed("[Menu] Full position menu restored");
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("[Menu] Could not restore the position menu: " + e.Message);
            }
        }

        // Stage order from the menu slots (1.15.0). None slots are left out; FFM / MMF use only SERVICE and SEX.
        private string sequenceNote = null;
        private TCSequence BuildMenuSequence()
        {
            bool group = TCGoals.IsGroup(Goal.Value);
            TCSequence sequence = new TCSequence();
            sequence.OnEnd = AfterLastStage.Value.ToString();
            sequenceNote = null;
            int skippedForGroup = 0;
            for (int i = 0; i < StageSlots; i++)
            {
                StageType type = StageTypes[i].Value;
                if (type == StageType.None)
                {
                    continue;
                }
                if (group && (type == StageType.CARESS || type == StageType.SPECIAL))
                {
                    skippedForGroup++;   // there is no caress or special position for three
                    continue;
                }
                AddStep(sequence, type.ToString(), Math.Min(Math.Max(StagePositions[i].Value, 1), 10));
            }
            if (group && skippedForGroup > 0)
            {
                sequenceNote = string.Format("{0}: {1} CARESS / SPECIAL stages are not used for three", Goal.Value, skippedForGroup);
            }
            if (group && sequence.Steps.Count == 0)
            {
                AddStep(sequence, "SERVICE", 2);
                AddStep(sequence, "SEX", 3);
                sequence.OnEnd = "Stay";
                sequenceNote = string.Format("{0}: no SERVICE or SEX stage set, using SERVICE 2 -> SEX 3", Goal.Value);
            }
            else if (sequence.Steps.Count == 0)
            {
                sequenceNote = "all stages are None: any position at any time";
            }
            // The key changes when the stage layout changes, so the tracker continues in the new layout then
            StringBuilder key = new StringBuilder("Menu");
            foreach (TCSequenceStep step in sequence.Steps)
            {
                key.Append(" ").Append(step.Categories).Append(":").Append(step.Changes);
            }
            key.Append(" then ").Append(sequence.OnEnd);
            sequence.Character = key.ToString();
            return sequence;
        }

        private static void AddStep(TCSequence sequence, string category, int changes)
        {
            TCSequenceStep step = new TCSequenceStep();
            step.Categories = category;
            step.Changes = changes;
            sequence.Steps.Add(step);
        }

        private void LogSceneSettings(TCSequence sequence)
        {
            string femaleName = "";
            try
            {
                ChaControl female = Singleton<HSceneManager>.Instance.Hscene.GetFemales()[0];
                if (female != null)
                {
                    femaleName = string.Format("'{0}' (H attribute {1}, state {2})", female.fileParam.fullname, female.fileParam2.hAttribute, (int)female.fileGameInfo2.nowState);
                }
            }
            catch (Exception)
            {
            }
            int trait = TCTrait.Current();
            Log.LogInfo(string.Format("[Goal] Female {0}: goal {1}, Masochism {2}, Sadism {3}, Anal {4}, sex trait {5} ({6}), stages: {7}", femaleName, Goal.Value, Masochism.Value ? "on" : "off", Sadism.Value ? "on" : "off",
                AnalAllowed.Value ? "on" : "off", TCTrait.Name(trait), !SexTrait.Value ? "off" : (TCTrait.HasEffect(trait) ? "on" : "no effect"),
                sequence == null ? "off" : sequence.Character.Replace("Menu ", "")));
            if (sequence != null && sequenceNote != null)
            {
                Log.LogInfo("[Sequence] " + sequenceNote);
            }
            if (Masochism.Value)
            {
                Log.LogInfo(string.Format("[Pain] Masochism on: pain positions last at most {0} s; she can climax there: {1}", PainPositionTime.Value, TCPain.CanClimaxFromPain() ? "yes" : "no (her gauge stops at 74%)"));
            }
            if (Goal.Value == TCGoal.Broken && !Masochism.Value)
            {
                Log.LogWarning("[Goal] Goal Broken without Masochism: Broken grows mainly when she climaxes in pain positions, which are off");
            }
        }

        private List<TCCandidate> RemoveCurrentPosition(List<TCCandidate> candidates)
        {
            try
            {
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                HScene.AnimationListInfo now = flags == null ? null : flags.nowAnimationInfo;
                if (now == null || candidates.Count == 0)
                {
                    return candidates;
                }
                int nowMode = DetermineModeByActionCtrl(now.ActionCtrl.Item1, now.ActionCtrl.Item2);
                List<TCCandidate> others = candidates.FindAll((c) => !(c.Info == now || (c.Info.id == now.id && DetermineModeByActionCtrl(c.Info.ActionCtrl.Item1, c.Info.ActionCtrl.Item2) == nowMode && c.Info.nameAnimation == now.nameAnimation)));
                if (others.Count < candidates.Count)
                {
                    LogDetailed(string.Format("[Select] Current position '{0}' is not offered again", now.nameAnimation));
                }
                return others;
            }
            catch (Exception)
            {
                return candidates;
            }
        }

        // Splits the positions into groups by how they change the goal and keeps the best group that is not empty.
        // With Sadism the positions where she leads come first among those that raise the goal.
        private const int GoalLowersGroup = 5;

        // group = index of the group that was kept (5 = lowers the goal), -1 if the goal did not sort the positions
        private List<TCCandidate> ApplyGoal(List<TCCandidate> candidates, bool log, out int chosenGroup)
        {
            chosenGroup = -1;
            TCGoal goal = Goal.Value;
            bool groupGoal = TCGoals.IsGroup(goal);
            bool sadism = Sadism.Value && !groupGoal;   // no position for three is marked as led by her
            if (groupGoal)
            {
                goal = TCGoal.Free;   // positions for three have no feeling ranking: random, not the last one again
            }
            if (goal == TCGoal.Free && lastPickedKey != null && candidates.Count > 1)
            {
                // No goal: just do not repeat the position that was used last
                List<TCCandidate> notLast = candidates.FindAll((c) => PositionKey(c) != lastPickedKey);
                if (notLast.Count > 0)
                {
                    candidates = notLast;
                }
            }
            if (candidates.Count == 0 || (goal == TCGoal.Free && !sadism))
            {
                return candidates;
            }
            string[] groupNames = new string[] { "she leads + strong", "she leads + mild", "strong", "mild", "neutral", "lowers goal" };
            List<TCCandidate>[] groups = new List<TCCandidate>[groupNames.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                groups[g] = new List<TCCandidate>();
            }
            foreach (TCCandidate candidate in candidates)
            {
                int tier = TCGoals.Tier(candidate.Info.ParmID, goal);
                if (tier < TCGoals.Lowers)
                {
                    // Every use lowers the position one level, but not below neutral
                    tier = Math.Min(tier + PositionUses(candidate), TCGoals.Neutral);
                }
                bool sheLeads = sadism && candidate.Info.nInitiativeFemale > 0;
                int group;
                if (goal == TCGoal.Free)
                {
                    group = sheLeads ? 0 : 4;
                }
                else if (sheLeads && tier == TCGoals.Strong)
                {
                    group = 0;
                }
                else if (sheLeads && tier == TCGoals.Mild)
                {
                    group = 1;
                }
                else
                {
                    group = tier + 1; // Strong 2, Mild 3, Neutral 4, Lowers 5
                }
                groups[group].Add(candidate);
            }
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Count > 0)
                {
                    chosenGroup = g;
                    if (log && DetailedLog.Value)
                    {
                        List<string> names = groups[g].ConvertAll((c) => PositionUses(c) > 0 ? string.Format("{0} (used {1})", c.Info.nameAnimation, PositionUses(c)) : c.Info.nameAnimation);
                        Log.LogInfo(string.Format("[Goal] {0}: group '{1}', {2} positions: {3}", goal, groupNames[g], groups[g].Count, string.Join(", ", names.ToArray())));
                    }
                    if (log && g == groups.Length - 1 && goal != TCGoal.Free)
                    {
                        Log.LogInfo(string.Format("[Goal] {0} cannot be raised at this stage, only positions that lower it are available", goal));
                    }
                    return groups[g];
                }
            }
            return candidates;
        }

        // Diagnostics (1.12.1): who is in the scene and how many positions for three the game offers
        private static string Describe(string slot, ChaControl c)
        {
            if (c == null)
            {
                return slot + " -";
            }
            string name = "?";
            bool visible = false;
            try
            {
                name = c.fileParam.fullname;
                visible = c.visibleAll;
            }
            catch (Exception)
            {
            }
            return string.Format("{0} '{1}' sex {2}{3}", slot, name, Convert.ToInt32(c.sex), visible ? ", visible" : ", hidden");
        }

        // The game's check whether the location has a spot for a position (HSceneSprite.CheckPlace, 1.15.1).
        // When a position needs another spot, the game moves the characters there itself (HScene.ChangeAnimation -> HPointList.GetStartPoint).
        private static MethodInfo placeCheck = AccessTools.Method(typeof(HSceneSprite), "CheckPlace", new Type[] { typeof(HScene.AnimationListInfo) });
        private static bool placeCheckFailed = false;
        private static bool placeCheckMissingLogged = false;

        private bool FitsLocation(HScene.AnimationListInfo info, int startPlace)
        {
            if (UseWholeLocation.Value && placeCheck == null && !placeCheckMissingLogged)
            {
                placeCheckMissingLogged = true;
                Log.LogWarning("[Select] The game's location check (HSceneSprite.CheckPlace) was not found, only the start spot is used");
            }
            if (UseWholeLocation.Value && placeCheck != null && !placeCheckFailed)
            {
                try
                {
                    HSceneSprite sprite = Singleton<HSceneSprite>.Instance;
                    if (sprite != null)
                    {
                        return (bool)placeCheck.Invoke(sprite, new object[] { info });
                    }
                }
                catch (Exception e)
                {
                    placeCheckFailed = true;
                    Log.LogWarning("[Select] The game's location check could not be used, only the start spot is used: " + e.Message);
                }
            }
            return StaysHere(info, startPlace);
        }

        // L off (0.9.0): only positions the game plays at the current point, without moving the characters.
        // The game (HScene.ChangeAnimation) keeps the point when the current place type is one of the first
        // lstOffset.Count place types of the position; otherwise it moves them to the start point that
        // HPointList.GetStartPoint finds - which is fine only when that is the point they are at now.
        private static MethodInfo startPointMethod = null;
        private static bool startPointSearched = false;
        private static bool stayCheckFailed = false;

        private bool StaysHere(HScene.AnimationListInfo info, int place)
        {
            if (info.nPositons == null || !info.nPositons.Contains(place))
            {
                return false;
            }
            if (stayCheckFailed)
            {
                return true;   // fall back to the place type only
            }
            try
            {
                int offsets = info.lstOffset == null ? 0 : info.lstOffset.Count;
                for (int i = 0; i < offsets && i < info.nPositons.Count; i++)
                {
                    if (info.nPositons[i] == place)
                    {
                        return true;
                    }
                }
                HSceneFlagCtrl flags = Singleton<HSceneFlagCtrl>.Instance;
                object hPointList = Traverse.Create(Singleton<HSceneManager>.Instance.Hscene).Field("hPointCtrl").Property("HPointList").GetValue();
                if (flags == null || hPointList == null)
                {
                    return true;
                }
                if (!startPointSearched)
                {
                    startPointSearched = true;
                    startPointMethod = AccessTools.Method(hPointList.GetType(), "GetStartPoint");
                    if (startPointMethod == null)
                    {
                        Log.LogWarning("[Select] HPointList.GetStartPoint not found: L off checks only the place type");
                    }
                }
                if (startPointMethod == null)
                {
                    stayCheckFailed = true;
                    return true;
                }
                object[] args = new object[] { info.nPositons, null, -1, info.ActionCtrl.Item1, info.id };
                int point = (int)startPointMethod.Invoke(hPointList, args);
                if (point < 0)
                {
                    return true;   // no start point: the game keeps the characters where they are
                }
                return Convert.ToInt32(args[2]) == place && point == flags.HPointID;
            }
            catch (Exception e)
            {
                stayCheckFailed = true;
                Log.LogWarning("[Select] The stay-here check could not be used, only the place type is checked: " + e.Message);
                return true;
            }
        }

        // The game's own check for its position menu (map, girl's state, events, who is in the scene)
        private static MethodInfo menuCheck = AccessTools.Method(typeof(HSceneSprite), "CheckMotionLimit", new Type[] { typeof(HScene.AnimationListInfo) });
        private static bool menuCheckFailed = false;

        // 1 = the game's menu offers the position, 0 = it does not, -1 = the check is not available
        private int GameMenuCheck(HScene.AnimationListInfo info)
        {
            if (menuCheck == null || menuCheckFailed)
            {
                return -1;
            }
            try
            {
                HSceneSprite sprite = Singleton<HSceneSprite>.Instance;
                if (sprite == null)
                {
                    return -1;
                }
                return (bool)menuCheck.Invoke(sprite, new object[] { info }) ? 1 : 0;
            }
            catch (Exception e)
            {
                menuCheckFailed = true;
                Log.LogWarning("[Scene] The game's menu check could not be used, Auto mode checks only the location: " + e.Message);
                return -1;
            }
        }

        private void LogParticipants(int offered3P, int kept3P, int menuAll, List<string> menu3P)
        {
            if (!DetailedLog.Value)
            {
                return;
            }
            try
            {
                HSceneManager manager = Singleton<HSceneManager>.Instance;
                List<string> parts = new List<string>();
                ChaControl[] males = manager.Hscene.GetMales();
                ChaControl[] females = manager.Hscene.GetFemales();
                for (int i = 0; males != null && i < males.Length; i++)
                {
                    parts.Add(Describe("M" + i, males[i]));
                }
                for (int i = 0; females != null && i < females.Length; i++)
                {
                    parts.Add(Describe("F" + i, females[i]));
                }
                object second = Traverse.Create(manager).Field("bFutanariSecond").GetValue();
                Log.LogInfo(string.Format("[Scene] {0}; futa flags {1}/{2}; positions for three offered by the game: {3}, kept: {4}",
                    string.Join(" | ", parts.ToArray()), manager.bFutanari, second, offered3P, kept3P));
                Log.LogInfo(string.Format("[Scene] Game menu allows {0} positions{1}; for three: {2}{3}",
                    menuAll, menuCheck == null ? " (menu check not found)" : "", menu3P.Count,
                    menu3P.Count > 0 ? " (" + string.Join(", ", menu3P.ToArray()) + ")" : ""));
            }
            catch (Exception e)
            {
                Log.LogInfo("[Scene] Could not list the participants: " + e.Message);
            }
        }

        // All positions that are valid here, minus the excluded ones
        private List<TCCandidate> BuildCandidates(List<HScene.AnimationListInfo>[] animList)
        {
            List<TCCandidate> candidates = new List<TCCandidate>();

            bool male = Singleton<HSceneManager>.Instance.player.sex == 0;
            // A female player card always plays as a futa (the game's bFutanari flag is not reliable: seen False with a futa)
            bool futa = !male;
            TCGoal goal = Goal.Value;
            bool groupGoal = TCGoals.IsGroup(goal);
            int groupMode = goal == TCGoal.FFM ? 7 : 8;
            // The arrays always have two slots, so check the second slot, not the length
            bool fem1Present = Singleton<HSceneManager>.Instance.Hscene.GetFemales().Length > 1 && Singleton<HSceneManager>.Instance.Hscene.GetFemales()[1] != null;
            bool multipleFemales = fem1Present;
            ChaControl[] males = Singleton<HSceneManager>.Instance.Hscene.GetMales();
            bool multipleMales = males.Length > 1 && males[1] != null;

            // Current state of the main girl (0 Normal, 1 Love, 2 Hedonist, 3 Hate, 4 Slave, 5 Broken, 6 Dependency)
            ChaControl mainFemale = Singleton<HSceneManager>.Instance.Hscene.GetFemales()[0];
            int femaleState = -1;
            try
            {
                if (mainFemale != null)
                {
                    femaleState = (int)mainFemale.fileGameInfo2.nowState;
                }
            }
            catch (Exception)
            {
                femaleState = -1;
            }

            int currentPlace = Singleton<HSceneFlagCtrl>.Instance.nPlace;
            int place = AutoStartPlace >= 0 ? AutoStartPlace : currentPlace;
            LogDetailed(UseWholeLocation.Value && placeCheck != null && !placeCheckFailed
                ? string.Format("[Select] Location: whole location (current spot {0})", currentPlace)
                : string.Format("[Select] Location: stay here (place {0}, point {1})", currentPlace, Singleton<HSceneFlagCtrl>.Instance.HPointID));
            int offered3P = 0;
            int kept3P = 0;
            int analSkipped = 0;
            int notInMenu = 0;
            int menuAll = 0;
            List<string> menu3P = new List<string>();

            for (int info1 = 0; info1 < animList.Length; info1++)
            {
                for (int pos = 0; pos < animList[info1].Count; pos++)
                {
                    HScene.AnimationListInfo info = animList[info1][pos];
                    int mode = DetermineModeByActionCtrl(info.ActionCtrl.Item1, info.ActionCtrl.Item2);
                    int inMenu = GameMenuCheck(info);
                    if (inMenu == 1)
                    {
                        menuAll++;
                        if (mode == 7 || mode == 8)
                        {
                            menu3P.Add(info.nameAnimation);
                        }
                    }
                    if (!AllowAllPositions.Value)
                    {
                        // 0.9.1: only positions the player's menu offers now (the game's own check: map, event,
                        // girl's state, fainting, unlocked positions). Events like the toilet or the bath allow only a few.
                        if (inMenu == 0)
                        {
                            notInMenu++;
                            continue;
                        }
                        if (!FitsLocation(info, currentPlace))
                        {
                            continue; // not available here (whole location or the start spot, 1.15.1)
                        }
                    }
                    if (femaleState >= 0 && info.nStatePtns != null && Enumerable.Any(info.nStatePtns) && !Enumerable.Contains(info.nStatePtns, femaleState))
                    {
                        continue; // not available in the girl's current state
                    }
                    // Anal off: no position the game marks as anal (it raises only resistAnal, not resistH)
                    if (!AnalAllowed.Value && IsAnalPosition(info))
                    {
                        analSkipped++;
                        continue;
                    }
                    // SPECIAL positions (1.15.0): tentacles and masturbation / restraint toys for every goal, pain only with Masochism
                    bool tentacle = IsTentacle(info);
                    bool painPosition = !tentacle && IsPainMode(info, mode);
                    bool toyPosition = mode == 4;
                    if (painPosition && !Masochism.Value)
                    {
                        continue;
                    }
                    if (mode == 5 && (male || futa) && !fem1Present)
                    {
                        continue;
                    }
                    if (mode == 6 && (futa || !multipleFemales))
                    {
                        continue; // lesbian: two other girls; not with a futa player (her body breaks in them)
                    }
                    if (mode == 7 || mode == 8)
                    {
                        offered3P++;
                    }
                    if (!multipleFemales && mode == 7)
                    {
                        continue; // needs two girls
                    }
                    if (!multipleMales && mode == 8)
                    {
                        continue; // needs two men
                    }
                    // Goals FFM / MMF use only their positions for three; the other goals never use positions for three
                    if (groupGoal ? mode != groupMode : (mode == 7 || mode == 8))
                    {
                        continue;
                    }

                    if (mode == (int)PositionCategories.PEEPING)
                    {
                        continue; // peeping is never chosen by Auto mode
                    }
                    string category = ((PositionCategories)mode).ToString();
                    if (groupGoal)
                    {
                        // The game's code of a position for three: 1, 2 = service, 3, 4 = sex
                        int sub = info.ActionCtrl.Item2;
                        category = (sub == 1 || sub == 2) ? "SERVICE" : "SEX";
                    }
                    else if (painPosition || toyPosition || tentacle)
                    {
                        category = SpecialCategory;
                    }

                    HAutoCtrl.AutoRandom.AutoRandomDate date = new HAutoCtrl.AutoRandom.AutoRandomDate();
                    date.mode = info1;
                    date.id = info.id;
                    candidates.Add(new TCCandidate(date, false, category, info));
                    if (mode == 7 || mode == 8)
                    {
                        kept3P++;
                    }
                }
            }
            LogParticipants(offered3P, kept3P, menuAll, menu3P);
            if (analSkipped > 0)
            {
                LogDetailed(string.Format("[Select] Anal off: {0} anal positions not used", analSkipped));
            }
            if (notInMenu > 0)
            {
                LogDetailed(string.Format("[Select] {0} positions are not in the player's menu now (map, event, state) - not used", notInMenu));
            }
            return candidates;
        }
    }

    public enum StageType
    {
        None,
        SERVICE,
        CARESS,
        SPECIAL,
        SEX
    }

    public enum AfterLastStageType
    {
        Stay,
        Loop,
        Free
    }

    public enum SexFinishType
    {
        Same,
        Inside,
        Outside,
        Random,
        Game
    }

    public enum ServiceFinishType
    {
        Random,
        Drink,
        Vomit,
        Outside
    }

    enum PositionCategories : int
    {
        CARESS = 0,
        SERVICE = 1,
        SEX = 2,
        SPANKING = 3,
        MASTURBATION = 4,
        PEEPING = 5,
        LESBIAN = 6,
        MULTI_F2M1 = 7,
        MULTI_F1M2 = 8
    }

    // Read by the Configuration Manager (F1 menu): order of the settings inside a section
    internal sealed class ConfigurationManagerAttributes
    {
        public int? Order;
    }
}
