using System;
using System.Globalization;

namespace Pointless.Core
{
    public enum GoalKind { None = 0, ClearLines = 1, BigClears = 2, ComboSkill = 3 }

    public sealed class Goals
    {
        public const int KindCount = 4;

        static readonly int[][] tiers =
        {
            Array.Empty<int>(),
            new[] { 10, 25, 50, 100, 200, 350, 500, 750, 1000 },
            new[] { 1, 3, 5, 10, 20, 35, 50, 75, 100 },
            new[] { 3, 5, 7, 10, 15, 20, 25, 30 }
        };

        const int HistoryLimit = 512;
        readonly int[] totals = new int[KindCount];
        readonly int[] stars = new int[KindCount];
        readonly int[,] histTotals = new int[HistoryLimit, KindCount];
        readonly int[,] histStars = new int[HistoryLimit, KindCount];
        int histNext, histCount;

        public GoalKind Active { get; private set; }

        public int Total => totals[(int)Active];
        public int Stars => stars[(int)Active];
        public int TotalStars => stars[1] + stars[2] + stars[3];

        public int GetTotal(GoalKind kind) => totals[(int)kind];
        public int GetStars(GoalKind kind) => stars[(int)kind];

        public int CurrentTarget => TargetFor(Active, stars[(int)Active]);
        public int PreviousTarget => stars[(int)Active] == 0 ? 0 : TargetFor(Active, stars[(int)Active] - 1);

        public float Progress
        {
            get
            {
                if (Active == GoalKind.None) return 0f;
                int prev = PreviousTarget;
                int next = CurrentTarget;
                if (next <= prev) return 1f;
                return Math.Clamp((float)(Total - prev) / (next - prev), 0f, 1f);
            }
        }

        public static int TargetFor(GoalKind kind, int starIndex)
        {
            if (kind == GoalKind.None) return 0;
            int[] list = tiers[(int)kind];
            if (starIndex < list.Length) return list[starIndex];
            int last = list[list.Length - 1];
            int step = kind == GoalKind.ClearLines ? 250 : kind == GoalKind.BigClears ? 25 : 5;
            return last + (starIndex - list.Length + 1) * step;
        }

        public void Select(GoalKind kind)
        {
            if ((uint)kind >= KindCount) kind = GoalKind.None;
            Active = kind;
        }

        public void ClearHistory()
        {
            histNext = histCount = 0;
        }

        /// <summary>
        /// Zaznamená ťah pre všetky tri ciele naraz. Vráti počet novozískaných hviezd v tomto ťahu.
        /// </summary>
        public int Record(Move move, bool canUndo)
        {
            if (canUndo)
            {
                for (int i = 1; i < KindCount; i++)
                {
                    histTotals[histNext, i] = totals[i];
                    histStars[histNext, i] = stars[i];
                }
                histNext = (histNext + 1) % HistoryLimit;
                if (histCount < HistoryLimit) histCount++;
            }
            else
            {
                ClearHistory();
            }

            totals[(int)GoalKind.ClearLines] += move.Lines;
            if (move.Lines >= 2) totals[(int)GoalKind.BigClears]++;
            if (move.Combo > totals[(int)GoalKind.ComboSkill]) totals[(int)GoalKind.ComboSkill] = move.Combo;

            int earned = 0;
            for (int i = 1; i < KindCount; i++)
            {
                GoalKind kind = (GoalKind)i;
                while (totals[i] >= TargetFor(kind, stars[i]))
                {
                    stars[i]++;
                    earned++;
                }
            }
            return earned;
        }

        public void Undo()
        {
            if (histCount == 0) return;
            histNext = (histNext + HistoryLimit - 1) % HistoryLimit;
            histCount--;
            for (int i = 1; i < KindCount; i++)
            {
                totals[i] = histTotals[histNext, i];
                stars[i] = histStars[histNext, i];
            }
        }

        public string Pack() =>
            $"{(int)Active}|{totals[1]},{totals[2]},{totals[3]}|{stars[1]},{stars[2]},{stars[3]}";

        public bool Unpack(string data)
        {
            ClearHistory();
            Active = GoalKind.None;
            Array.Clear(totals, 0, KindCount);
            Array.Clear(stars, 0, KindCount);
            if (string.IsNullOrEmpty(data)) return true;
            string[] parts = data.Split('|');
            if (parts.Length != 3) return false;
            bool read = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int active) && (uint)active < KindCount;
            if (read) Active = (GoalKind)active;

            read &= ParseTriple(parts[1], totals);
            read &= ParseTriple(parts[2], stars);
            return read;
        }

        static bool ParseTriple(string csv, int[] target)
        {
            string[] items = csv.Split(',');
            if (items.Length != 3) return false;
            bool read = true;
            for (int i = 0; i < 3; i++)
            {
                if (int.TryParse(items[i], NumberStyles.None, CultureInfo.InvariantCulture, out int val)) target[i + 1] = val;
                else read = false;
            }
            return read;
        }
    }
}
