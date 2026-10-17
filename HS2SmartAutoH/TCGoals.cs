using System;

namespace HS2SmartAutoH
{
    // Which feeling Auto mode tries to raise
    public enum TCGoal
    {
        Free = 0,      // no goal: any position (was "Off" before 1.12.1)
        Favour = 1,
        Enjoyment = 2,
        Slavery = 3,
        Aversion = 4,
        Broken = 5,
        Dependence = 6,
        FFM = 7,       // positions for three: girl + girl + player (1.13.0)
        MMF = 8        // positions for three: girl + player + second man / futa (1.13.0)
    }

    public static class TCGoals
    {
        // The game's own table (abdata/list/h/param, Taii_30): how each position type changes the feelings.
        // Each position has its type in AnimationListInfo.ParmID.
        // Per type: Favour, Enjoyment, Slavery, Aversion, Broken, Dependence as min,max pairs.
        private static readonly int[,] Changes = new int[,]
        {
            /*  0 */ {  5, 10,  -3, -1,  -3, -1,  -3, -1,  -5, -1,   5,  10 },
            /*  1 */ {  2,  5,   2,  5,  -3, -1,  -3, -1,  -5, -1,   1,   5 },
            /*  2 */ {  2,  5,  -3, -1,   2,  5,  -3, -1,  -5, -1,   1,   5 },
            /*  3 */ {  2,  5,  -3, -1,  -3, -1,   2,  5,  -5, 10, -10,   5 },
            /*  4 */ { -3, -1,   5, 10,  -3, -1,  -3, -1,  -5, -1,   5,  10 },
            /*  5 */ {  2,  5,   2,  5,  -3, -1,  -3, -1,  -5, -1,   1,   5 },
            /*  6 */ { -3, -1,   2,  5,   2,  5,  -3, -1,  -5, -1,   1,   5 },
            /*  7 */ { -3, -1,   2,  5,  -3, -1,   2,  5,  -5, 10, -10,   5 },
            /*  8 */ { -3, -1,  -3, -1,   5, 10,  -3, -1,  -5, -1,   5,  10 },
            /*  9 */ {  2,  5,  -3, -1,   2,  5,  -3, -1,  -5, -1,   1,   5 },
            /* 10 */ { -3, -1,   2,  5,   2,  5,  -3, -1,  -5, -1,   1,   5 },
            /* 11 */ { -3, -1,  -3, -1,   2,  5,   2,  5,  -5, 10, -10,   5 },
            /* 12 */ { -3, -1,  -3, -1,  -3, -1,   5, 10,   5, 10, -20, -10 },
            /* 13 */ {  2,  5,  -3, -1,  -3, -1,   2,  5,   1,  5,  -5,  -1 },
            /* 14 */ { -3, -1,   2,  5,  -3, -1,   2,  5,   1,  5,  -5,  -1 },
            /* 15 */ { -3, -1,  -3, -1,   2,  5,   2,  5,   1,  5,  -5,  -1 },
            /* 16 */ { -1,  3,  -1,  3,  -1,  3,  -7, -2,  -5,  5,   5,  10 },
            /* 17 */ { -7, -2,  -1,  3,  -1,  3,  -1,  3,  -5, 10, -10,   5 },
            /* 18 */ { -1,  3,  -7, -2,  -1,  3,  -1,  3,  -5,  5,  -5,   5 },
            /* 19 */ { -1,  3,  -1,  3,  -7, -2,  -1,  3,  -5,  5,  -5,   5 },
            /* 20 */ { -5,  5,  -5,  5,  -5,  5,  -5,  5, -10, 10, -15,  15 }
        };

        public const int Strong = 1;   // raises the goal by +5..10
        public const int Mild = 2;     // raises the goal by +1..5
        public const int Neutral = 3;  // random result, or the position has no type
        public const int Lowers = 4;   // lowers the goal

        // FFM and MMF are not feelings: they choose the kind of scene
        public static bool IsGroup(TCGoal goal)
        {
            return goal == TCGoal.FFM || goal == TCGoal.MMF;
        }

        public static int Tier(int positionType, TCGoal goal)
        {
            if (goal == TCGoal.Free || IsGroup(goal) || positionType < 0 || positionType >= Changes.GetLength(0))
            {
                return Neutral;
            }
            int stat = (int)goal - 1;
            int min = Changes[positionType, stat * 2];
            int max = Changes[positionType, stat * 2 + 1];
            if (min >= 5)
            {
                return Strong;
            }
            if (min >= 1)
            {
                return Mild;
            }
            if (max <= 0)
            {
                return Lowers;
            }
            return Neutral;
        }

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case Strong: return "strong";
                case Mild: return "mild";
                case Lowers: return "lowers goal";
                default: return "neutral";
            }
        }
    }
}
