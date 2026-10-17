using HarmonyLib;
using Manager;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HS2SmartAutoH
{
    // Auto mode panel in the H scene, drawn left of the game's position-category menu (top right).
    //   top row:    < [goal icons] >      mouse wheel or arrows change the goal
    //   goal bar:   name of the goal
    //   bottom bar: Auto ON/OFF, reset (stages from Stage 1), T (sex trait), A (Anal), M (Masochism), S (Sadism), L (whole location)
    //   finish bar: SEX finish and SERVICE finish (click or wheel to change)
    // Everything reads and writes the same config entries as the F1 menu, so both stay in sync.
    // Sizes are given for 1920x1080 and scaled with the screen like the game's own UI.
    public static class TCPanel
    {
        private static readonly Color Pink = new Color32(236, 96, 124, 255);
        private static readonly Color White = new Color32(240, 240, 240, 255);
        private static readonly Color Dim = new Color32(120, 120, 130, 255);
        private static readonly Color BarColor = new Color32(24, 22, 28, 115);   // like the game's category bar
        private static readonly Color Outline = new Color32(15, 15, 20, 190);
        private static readonly Color LabelColor = new Color32(175, 175, 185, 255);
        private static readonly Color HintColor = new Color32(255, 170, 190, 255);
        private static readonly Color NoEffect = new Color32(80, 80, 88, 255);

        // Panel area at 1920x1080: from x 1446 to 1672, from y 8; rows line up with the game menu (measured on the game screen)
        private const float RefRightGap = 1920f - 1446f;
        private const float RefTop = 8f;
        private const float RefWidth = 226f;

        private static Texture2D leftArrow;
        private static Texture2D rightArrow;
        private static Texture2D white;
        private static readonly Dictionary<string, Texture2D> bars = new Dictionary<string, Texture2D>();
        private static GUIStyle textStyle;
        private static float hintUntil = -1f;
        private static bool errorLogged = false;

        private static readonly string[] GoalNames = new string[] { "Free", "Favour", "Enjoyment", "Slavery", "Aversion", "Broken", "Dependence", "FFM", "MMF" };
        // Tooltip of each goal (1.0.0): shown for the goal under the mouse
        private static readonly string[] GoalTips = new string[]
        {
            "Free: no goal - any position the stages allow.",
            "Favour: positions the game rewards with Favour go first.",
            "Enjoyment: positions that make her love the pleasure itself go first.",
            "Slavery: positions of obedience and submission go first.",
            "Aversion: positions that raise Aversion go first.\nShe will like you less.",
            "Broken: positions that push her towards Broken go first.\nThey usually raise Aversion too.",
            "Dependence: positions that make her need you go first.",
            "FFM: two girls and you - only positions for three,\nSERVICE and SEX stages. Needs a second girl.",
            "MMF: her, you and a second man or futa - only positions\nfor three, SERVICE and SEX stages. Needs a second male."
        };

        private static FieldInfo uiGroupField = AccessTools.Field(typeof(HSceneSprite), "UIGroup");
        private static PropertyInfo alphaProperty;

        // ---------- textures ----------

        public static Texture2D MakeTexture(int width, int height, Color32[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        // White triangle pointing left or right (tinted when drawn)
        private static Texture2D MakeArrow(bool right)
        {
            const int size = 64;
            Color32[] pixels = new Color32[size * size];
            // triangle (in a 64x64 box): tip at x 56, base at x 10, from y 6 to y 58
            Vector2 a = new Vector2(10f, 6f);
            Vector2 b = new Vector2(10f, 58f);
            Vector2 c = new Vector2(56f, 32f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                            if (Side(a, b, p) <= 0f && Side(b, c, p) <= 0f && Side(c, a, p) <= 0f)
                            {
                                inside++;
                            }
                        }
                    }
                    int px = right ? x : size - 1 - x;
                    pixels[y * size + px] = new Color32(255, 255, 255, (byte)(inside * 255 / 16));
                }
            }
            return MakeTexture(size, size, pixels);
        }

        private static float Side(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        // Dark bar with a slanted left edge, like the game's category bar. Made at the exact pixel size.
        private static Texture2D Bar(int width, int height)
        {
            width = Math.Max(width, 2);
            height = Math.Max(height, 2);
            string key = width + "x" + height;
            Texture2D texture;
            if (bars.TryGetValue(key, out texture) && texture != null)
            {
                return texture;
            }
            float slant = height * 0.55f;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // Unity rows go from the bottom: the bar is narrow at the bottom-left, like "/"
                float edge = slant * (1f - (y + 0.5f) / height);
                for (int x = 0; x < width; x++)
                {
                    float alpha = Mathf.Clamp01(x + 0.5f - edge);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture = MakeTexture(width, height, pixels);
            bars[key] = texture;
            return texture;
        }

        private static void LoadTextures()
        {
            if (leftArrow == null)
            {
                leftArrow = MakeArrow(false);
            }
            if (rightArrow == null)
            {
                rightArrow = MakeArrow(true);
            }
            if (white == null)
            {
                white = MakeTexture(1, 1, new Color32[] { new Color32(255, 255, 255, 255) });
            }
            if (TCIcons.Goals == null || TCIcons.Goals[0] == null || TCIcons.Reset == null)
            {
                TCIcons.Goals = null;
                TCIcons.Load();
            }
        }

        // ---------- state ----------

        // Goal, Masochism and Sadism are used from the next position on: tell the user for a few seconds
        public static void ShowNextPositionHint()
        {
            hintUntil = Time.realtimeSinceStartup + 4f;
        }

        private static bool PanelVisible(out HSceneFlagCtrl flags)
        {
            flags = null;
            if (!HS2SmartAutoH.ShowPanel.Value)
            {
                return false;
            }
            HSceneManager manager = Singleton<HSceneManager>.Instance;
            if (manager == null || manager.Hscene == null)
            {
                return false;
            }
            flags = Singleton<HSceneFlagCtrl>.Instance;
            HSceneSprite sprite = Singleton<HSceneSprite>.Instance;
            if (flags == null || sprite == null || !sprite.isActiveAndEnabled)
            {
                return false;
            }
            // Hidden together with the game's UI (the game fades its UI group out)
            try
            {
                object group = uiGroupField == null ? null : uiGroupField.GetValue(sprite);
                if (group != null)
                {
                    if (alphaProperty == null)
                    {
                        alphaProperty = group.GetType().GetProperty("alpha");
                    }
                    if (alphaProperty != null && (float)alphaProperty.GetValue(group, null) < 0.5f)
                    {
                        return false;
                    }
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        // ---------- drawing ----------

        public static void Draw()
        {
            HSceneFlagCtrl flags;
            try
            {
                if (!PanelVisible(out flags))
                {
                    return;
                }
                LoadTextures();
                DrawPanel(flags);
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    HS2SmartAutoH.Instance.Log.LogWarning("[Panel] Error (logged once): " + e);
                }
            }
        }

        private static void DrawPanel(HSceneFlagCtrl flags)
        {
            float s = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            float x0 = Screen.width - RefRightGap * s;
            float y0 = RefTop * s;
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;
            string tooltip = null;

            if (textStyle == null)
            {
                textStyle = new GUIStyle();
                textStyle.richText = true;
                textStyle.wordWrap = false;
                textStyle.normal.textColor = White;
            }

            bool autoOn = flags.initiative == 2;
            int goal = (int)HS2SmartAutoH.Goal.Value;
            if (goal < 0 || goal >= GoalNames.Length)
            {
                goal = 0;
            }
            Color oldColor = GUI.color;

            // --- top row: arrows and goal icons ---
            Rect row = new Rect(x0, y0, RefWidth * s, 58f * s);
            Rect leftRect = new Rect(x0, y0 + 18f * s, 22f * s, 24f * s);
            Rect rightRect = new Rect(x0 + (RefWidth - 22f) * s, y0 + 18f * s, 22f * s, 24f * s);
            float centerX = x0 + RefWidth / 2f * s;
            float centerY = y0 + 30f * s;
            float big = 46f * s;
            float small = 28f * s;
            float side = 50f * s;

            // Dark outline behind the arrows, like the game's own arrows
            DrawTinted(leftArrow, Grow(leftRect, 2.5f * s), Outline);
            DrawTinted(rightArrow, Grow(rightRect, 2.5f * s), Outline);
            DrawTinted(leftArrow, leftRect, leftRect.Contains(mouse) ? Pink : White);
            DrawTinted(rightArrow, rightRect, rightRect.Contains(mouse) ? Pink : White);
            int prev = (goal + GoalNames.Length - 1) % GoalNames.Length;
            int next = (goal + 1) % GoalNames.Length;
            Rect prevRect = new Rect(centerX - side - small / 2f, centerY - small / 2f, small, small);
            Rect nextRect = new Rect(centerX + side - small / 2f, centerY - small / 2f, small, small);
            Rect goalRect = new Rect(centerX - big / 2f, centerY - big / 2f, big, big);
            DrawGoalIcon(prev, prevRect, false, prevRect.Contains(mouse));
            DrawGoalIcon(next, nextRect, false, nextRect.Contains(mouse));
            DrawGoalIcon(goal, goalRect, true, false);

            // Every button is called on every event (IMGUI needs the same controls each time)
            bool leftClick = GUI.Button(leftRect, GUIContent.none, GUIStyle.none);
            bool prevClick = GUI.Button(prevRect, GUIContent.none, GUIStyle.none);
            bool rightClick = GUI.Button(rightRect, GUIContent.none, GUIStyle.none);
            bool nextClick = GUI.Button(nextRect, GUIContent.none, GUIStyle.none);
            if (leftClick || prevClick)
            {
                SetGoal(prev);
            }
            else if (rightClick || nextClick)
            {
                SetGoal(next);
            }
            if (e.type == EventType.ScrollWheel && row.Contains(mouse))
            {
                SetGoal(e.delta.y > 0f ? next : prev);
                e.Use();
            }
            if (row.Contains(mouse))
            {
                int shown = prevRect.Contains(mouse) ? prev : nextRect.Contains(mouse) ? next : (int)HS2SmartAutoH.Goal.Value;
                if (shown < 0 || shown >= GoalTips.Length) shown = 0;
                tooltip = GoalTips[shown] + "\nMouse wheel or arrows to change.";
            }

            // --- goal bar ---
            Rect goalBar = new Rect(x0, y0 + 67f * s, RefWidth * s, 34f * s);
            DrawBar(goalBar);
            DrawCentered(goalBar, "<b>" + GoalNames[goal] + "</b>", 21f * s, White);

            // --- bottom bar: Auto ON/OFF, reset, M, S ---
            Rect bottom = new Rect(x0 + 18f * s, y0 + 105f * s, (RefWidth - 18f) * s, 26f * s);
            DrawBar(bottom);
            float font = 17f * s;
            float cy = bottom.y + bottom.height / 2f;

            Rect autoRect = new Rect(bottom.x + 14f * s, bottom.y, 72f * s, bottom.height);
            string autoText = "Auto " + Colored(autoOn ? "ON" : "OFF", autoOn ? Pink : Dim);
            DrawLeft(autoRect, autoText, font, autoRect.Contains(mouse) ? Pink : White);
            if (GUI.Button(autoRect, GUIContent.none, GUIStyle.none))
            {
                HS2SmartAutoH.Instance.ToggleAuto();
            }
            if (autoRect.Contains(mouse))
            {
                string stage = TCSequenceTracker.Status;
                tooltip = "Auto mode on / off" + (HS2SmartAutoH.AutoKey.Value.MainKey == KeyCode.None ? "" : " (" + HS2SmartAutoH.AutoKey.Value + ")") +
                    (string.IsNullOrEmpty(stage) ? "" : "\n" + stage);
            }

            float icon = 18f * s;
            Rect resetRect = new Rect(bottom.x + 90f * s, cy - icon / 2f, icon, icon);
            Rect resetHit = new Rect(resetRect.x - 3f * s, bottom.y, icon + 3f * s, bottom.height);
            DrawTinted(TCIcons.Reset, resetRect, resetHit.Contains(mouse) ? Pink : White);
            if (GUI.Button(resetHit, GUIContent.none, GUIStyle.none))
            {
                HS2SmartAutoH.Instance.ResetStages();
            }
            if (resetHit.Contains(mouse))
            {
                tooltip = autoOn
                    ? "Start the stages again from Stage 1.\nThe position changes right away."
                    : "Start the stages again from Stage 1.";
            }

            // centers at 1920x1080: T 116, A 136, M 156, S 176, L 196 from the left of the bar (1.15.1: 20 px apart to fit L)
            Rect tRect = new Rect(bottom.x + 107f * s, bottom.y, 18f * s, bottom.height);
            Rect aRect = new Rect(bottom.x + 127f * s, bottom.y, 18f * s, bottom.height);
            Rect mRect = new Rect(bottom.x + 147f * s, bottom.y, 18f * s, bottom.height);
            Rect sRect = new Rect(bottom.x + 167f * s, bottom.y, 18f * s, bottom.height);
            Rect lRect = new Rect(bottom.x + 187f * s, bottom.y, 18f * s, bottom.height);
            int trait = TCTrait.Current();
            bool traitEffect = TCTrait.HasEffect(trait);
            if (traitEffect)
            {
                DrawToggle(tRect, "T", HS2SmartAutoH.SexTrait.Value, mouse, font);
            }
            else
            {
                DrawCentered(tRect, "<b>T</b>", font, tRect.Contains(mouse) ? Dim : NoEffect);
            }
            DrawToggle(aRect, "A", HS2SmartAutoH.AnalAllowed.Value, mouse, font);
            DrawToggle(mRect, "M", HS2SmartAutoH.Masochism.Value, mouse, font);
            DrawToggle(sRect, "S", HS2SmartAutoH.Sadism.Value, mouse, font);
            DrawToggle(lRect, "L", HS2SmartAutoH.UseWholeLocation.Value, mouse, font);
            bool tClick = GUI.Button(tRect, GUIContent.none, GUIStyle.none);
            bool aClick = GUI.Button(aRect, GUIContent.none, GUIStyle.none);
            bool mClick = GUI.Button(mRect, GUIContent.none, GUIStyle.none);
            bool sClick = GUI.Button(sRect, GUIContent.none, GUIStyle.none);
            bool lClick = GUI.Button(lRect, GUIContent.none, GUIStyle.none);
            if (tClick)
            {
                HS2SmartAutoH.SexTrait.Value = !HS2SmartAutoH.SexTrait.Value;
            }
            if (aClick)
            {
                HS2SmartAutoH.AnalAllowed.Value = !HS2SmartAutoH.AnalAllowed.Value;
            }
            if (mClick)
            {
                HS2SmartAutoH.Masochism.Value = !HS2SmartAutoH.Masochism.Value;
            }
            if (sClick)
            {
                HS2SmartAutoH.Sadism.Value = !HS2SmartAutoH.Sadism.Value;
            }
            if (lClick)
            {
                HS2SmartAutoH.UseWholeLocation.Value = !HS2SmartAutoH.UseWholeLocation.Value;
            }
            bool group = TCGoals.IsGroup(HS2SmartAutoH.Goal.Value);
            if (tRect.Contains(mouse))
            {
                tooltip = TraitText(trait);
            }
            if (aRect.Contains(mouse))
            {
                tooltip = "Anal: positions the game marks as anal are allowed.\nOff: they are skipped, finishes raise resistH.";
            }
            if (mRect.Contains(mouse))
            {
                tooltip = "Masochism: pain positions in the SPECIAL stage, at most " + HS2SmartAutoH.PainPositionTime.Value + " s each." +
                    (TCPain.CanClimaxFromPain() ? "\nShe can climax from pain." : "\nShe cannot climax from pain yet (no Masochist trait, pain experience below 100):\nthe time still trains her pain experience.") +
                    (group ? "\nNo effect with FFM / MMF." : "");
            }
            if (lRect.Contains(mouse))
            {
                tooltip = HS2SmartAutoH.UseWholeLocation.Value
                    ? "Location: positions from the whole location.\nThe game moves you both to the right spot when a position needs it.\nOff: only the spot where Auto was turned on."
                    : "Location: only the spot where Auto was turned on.\nTo stay on the bed, turn Auto on while on the bed.\nOn: positions from the whole location.";
            }
            if (sRect.Contains(mouse))
            {
                tooltip = "Sadism: positions where she leads go first." + (group ? "\nNo effect with FFM / MMF." : "");
            }

            // --- finish bar: SEX finish, SERVICE finish ---
            Rect finish = new Rect(x0 + 18f * s, bottom.yMax + 4f * s, (RefWidth - 18f) * s, 24f * s);
            DrawBar(finish);
            float finishFont = 13f * s;
            float sexX = finish.x + 16f * s;
            // The SERVICE part starts at a fixed place, so it does not jump when the SEX value changes
            float servX = sexX + Measure("SEX <b>Random</b>", finishFont).x + 12f * s;
            Rect sexRect = new Rect(sexX - 4f * s, finish.y, servX - sexX, finish.height);
            Rect servRect = new Rect(servX - 4f * s, finish.y, finish.xMax - servX + 4f * s, finish.height);
            SexFinishType sexFinish = HS2SmartAutoH.SexFinish.Value;
            ServiceFinishType serviceFinish = HS2SmartAutoH.ServiceFinish.Value;
            DrawLeft(new Rect(sexX, finish.y, servX - sexX, finish.height), "SEX " + Colored(sexFinish.ToString(), sexRect.Contains(mouse) ? HintColor : Pink), finishFont, sexRect.Contains(mouse) ? White : LabelColor);
            DrawLeft(new Rect(servX, finish.y, finish.xMax - servX, finish.height), "SERV " + Colored(serviceFinish.ToString(), servRect.Contains(mouse) ? HintColor : Pink), finishFont, servRect.Contains(mouse) ? White : LabelColor);
            bool sexClick = GUI.Button(sexRect, GUIContent.none, GUIStyle.none);
            bool servClick = GUI.Button(servRect, GUIContent.none, GUIStyle.none);
            int sexCount = Enum.GetValues(typeof(SexFinishType)).Length;
            int servCount = Enum.GetValues(typeof(ServiceFinishType)).Length;
            if (sexClick)
            {
                HS2SmartAutoH.SexFinish.Value = (SexFinishType)(((int)sexFinish + 1) % sexCount);
            }
            if (servClick)
            {
                HS2SmartAutoH.ServiceFinish.Value = (ServiceFinishType)(((int)serviceFinish + 1) % servCount);
            }
            if (e.type == EventType.ScrollWheel && sexRect.Contains(mouse))
            {
                int step = e.delta.y > 0f ? 1 : sexCount - 1;
                HS2SmartAutoH.SexFinish.Value = (SexFinishType)(((int)sexFinish + step) % sexCount);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && servRect.Contains(mouse))
            {
                int step = e.delta.y > 0f ? 1 : servCount - 1;
                HS2SmartAutoH.ServiceFinish.Value = (ServiceFinishType)(((int)serviceFinish + step) % servCount);
                e.Use();
            }
            if (sexRect.Contains(mouse))
            {
                tooltip = "SEX finish (click or wheel to change):\n" + SexFinishText(sexFinish);
            }
            if (servRect.Contains(mouse))
            {
                tooltip = "SERVICE finish (click or wheel to change):\n" + ServiceFinishText(serviceFinish);
            }

            float below = finish.yMax + 4f * s;
            // --- hint after a change ---
            if (autoOn && Time.realtimeSinceStartup < hintUntil)
            {
                Rect hintRect = new Rect(bottom.x, below, bottom.width, 18f * s);
                DrawCentered(hintRect, "from next position", 14f * s, HintColor);
                below = hintRect.yMax + 2f * s;
            }

            // --- tooltip ---
            if (tooltip != null)
            {
                DrawTooltip(tooltip, x0, below + 2f * s, s);
            }

            GUI.color = oldColor;
        }

        // Tooltip of T: her trait and what Auto mode does with it
        private static string TraitText(int trait)
        {
            string name = TCTrait.Name(trait);
            if (trait < 0)
            {
                return "Sex trait: not known.";
            }
            if (!TCTrait.HasEffect(trait))
            {
                return "Sex trait: " + name + " - no effect for this girl.";
            }
            string text = "Sex trait: " + name + (HS2SmartAutoH.SexTrait.Value ? " (ON)" : " (OFF)");
            if (trait == TCTrait.CleanFreak)
            {
                text += "\nPositions are chosen for her trait. Outside finishes upset her.";
            }
            else if (TCTrait.HasFinishes(trait))
            {
                text += "\nFinishes are chosen for her trait.";
            }
            else
            {
                text += "\nPositions are chosen for her trait.";
                if (trait == TCTrait.Ass && !HS2SmartAutoH.AnalAllowed.Value)
                {
                    text += "\nA is off: no anal positions.";
                }
            }
            if (HS2SmartAutoH.Goal.Value == TCGoal.FFM)
            {
                text += "\nThe game does not count the second girl's trait.";
            }
            return text;
        }

        private static string SexFinishText(SexFinishType type)
        {
            switch (type)
            {
                case SexFinishType.Same: return "both finish together";
                case SexFinishType.Inside: return "he finishes inside (outside if the position does not allow it)";
                case SexFinishType.Outside: return "he finishes outside";
                case SexFinishType.Random: return "any finish the position allows";
                default: return "the mod does not interfere: whoever is first";
            }
        }

        private static string ServiceFinishText(ServiceFinishType type)
        {
            switch (type)
            {
                case ServiceFinishType.Drink: return "she swallows (outside if the position does not allow it)";
                case ServiceFinishType.Vomit: return "she spits it out (outside if the position does not allow it)";
                case ServiceFinishType.Outside: return "he finishes outside";
                default: return "swallow, spit out or outside at random";
            }
        }

        private static Rect Grow(Rect r, float by)
        {
            return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        }

        // Our goal icons are white shapes tinted pink / gray; FFM and MMF are the game's icons in their own colors
        private static void DrawGoalIcon(int goal, Rect rect, bool active, bool hover)
        {
            if (TCIcons.IsColored(goal))
            {
                Color color = active ? Color.white : (hover ? new Color(0.9f, 0.9f, 0.95f, 1f) : new Color(0.55f, 0.55f, 0.6f, 0.85f));
                DrawTinted(TCIcons.Goals[goal], rect, color);
            }
            else
            {
                DrawTinted(TCIcons.Goals[goal], rect, active ? Pink : (hover ? White : Dim));
            }
        }

        private static void SetGoal(int goal)
        {
            HS2SmartAutoH.Goal.Value = (TCGoal)goal;
        }

        private static void DrawToggle(Rect rect, string letter, bool on, Vector2 mouse, float font)
        {
            Color color = on ? Pink : Dim;
            if (rect.Contains(mouse))
            {
                color = on ? HintColor : White;
            }
            DrawCentered(rect, "<b>" + letter + "</b>", font, color);
        }

        private static string Colored(string text, Color color)
        {
            Color32 c = color;
            return string.Format("<b><color=#{0:X2}{1:X2}{2:X2}>{3}</color></b>", c.r, c.g, c.b, text);
        }

        private static void DrawTinted(Texture2D texture, Rect rect, Color color)
        {
            if (texture == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = Color.white;
        }

        private static void DrawBar(Rect rect)
        {
            DrawTinted(Bar(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)), rect, BarColor);
        }

        private static Vector2 Measure(string text, float size)
        {
            textStyle.fontSize = Math.Max(8, Mathf.RoundToInt(size));
            return textStyle.CalcSize(new GUIContent(text));
        }

        private static void DrawCentered(Rect rect, string text, float size, Color color)
        {
            Vector2 measured = Measure(text, size);
            Rect r = new Rect(rect.x + (rect.width - measured.x) / 2f, rect.y + (rect.height - measured.y) / 2f, measured.x, measured.y);
            textStyle.normal.textColor = color;
            GUI.Label(r, text, textStyle);
        }

        private static void DrawLeft(Rect rect, string text, float size, Color color)
        {
            Vector2 measured = Measure(text, size);
            Rect r = new Rect(rect.x, rect.y + (rect.height - measured.y) / 2f, measured.x, measured.y);
            textStyle.normal.textColor = color;
            GUI.Label(r, text, textStyle);
        }

        private static void DrawTooltip(string text, float x, float y, float s)
        {
            Vector2 measured = Measure(text, 14f * s);
            float pad = 6f * s;
            Rect box = new Rect(x, y, measured.x + pad * 2f, measured.y + pad * 2f);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color32(20, 20, 24, 230);
                GUI.DrawTexture(box, white);
                GUI.color = Color.white;
            }
            textStyle.normal.textColor = White;
            GUI.Label(new Rect(box.x + pad, box.y + pad, measured.x, measured.y), text, textStyle);
        }
    }
}
