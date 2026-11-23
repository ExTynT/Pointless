namespace Pointless.Core
{
    public sealed class GeneratorProfile
    {
        public static readonly GeneratorProfile Standard = new GeneratorProfile(
            new float[] { 0, 9, 28, 32, 20, 8, 3 }, new float[] { 0, 3, 12, 24, 30, 20, 11 }, 12000, 0.9f, 0.45f);
        public static readonly GeneratorProfile Relaxed = new GeneratorProfile(
            new float[] { 0, 57, 33, 9, 0.8f, 0.15f, 0.05f }, new float[] { 0, 54, 34, 11, 0.8f, 0.15f, 0.05f }, 12000, 0.96f, 0.96f);

        readonly float[] easy, hard;
        readonly int hardScore;
        readonly float easyHelp, hardHelp;

        GeneratorProfile(float[] easy, float[] hard, int hardScore, float easyHelp, float hardHelp)
        {
            this.easy = easy;
            this.hard = hard;
            this.hardScore = hardScore;
            this.easyHelp = easyHelp;
            this.hardHelp = hardHelp;
        }

        internal float Level(int score) => score >= hardScore ? 1f : (float)score / hardScore;
        internal float Help(float level) => easyHelp + (hardHelp - easyHelp) * level;
        internal float Weight(int size, float level) => easy[size] + (hard[size] - easy[size]) * level;
    }

    public static class Generator
    {
        const int Tries = 10;
        const int CrowdedCells = 21;

        public static void Deal(ulong board, int score, ref Rng rng, int[] trio, GeneratorProfile profile)
        {
            float level = profile.Level(score);
            float help = profile.Help(level);

            for (int slot = 0; slot < trio.Length; slot++)
            {
                bool crowded = Bits.Count(board) > CrowdedCells;
                int best = -1, bestPlace = -1, bestValue = int.MinValue;
                int any = -1, anyPlace = -1, fitting = 0;

                for (int attempt = 0; attempt < Tries; attempt++)
                {
                    int id = Sample(profile, level, crowded, ref rng);
                    if (Taken(trio, slot, id)) continue;
                    int place = Best(board, Shapes.All[id], ref rng, out int value);
                    if (place < 0) continue;
                    fitting++;
                    if (value > bestValue)
                    {
                        bestValue = value;
                        best = id;
                        bestPlace = place;
                    }
                    if (rng.Range(fitting) == 0)
                    {
                        any = id;
                        anyPlace = place;
                    }
                }

                if (best < 0) best = Rescue(board, ref rng, out bestPlace);
                else if (rng.Unit() >= help)
                {
                    best = any;
                    bestPlace = anyPlace;
                }

                trio[slot] = best;
                if (bestPlace >= 0) board = Lattice.Settle(board | Shapes.All[best].Mask[bestPlace]);
            }

            for (int slot = trio.Length - 1; slot > 0; slot--)
            {
                int other = rng.Range(slot + 1);
                (trio[slot], trio[other]) = (trio[other], trio[slot]);
            }
        }

        public static int Best(ulong board, Shape shape, ref Rng rng, out int value)
        {
            int best = -1;
            value = int.MinValue;
            ulong[] masks = shape.Mask;
            for (int place = 0; place < masks.Length; place++)
            {
                ulong mask = masks[place];
                if ((board & mask) != 0) continue;

                ulong filled = board | mask;
                int lines = Lattice.Sweep(filled, out ulong cleared, out _);
                ulong after = filled & ~cleared;
                int worth = lines * 120 + lines * lines * 30
                    + Bits.Count(shape.Halo[place] & board) * 6
                    + shape.Wall[place] * 3
                    + rng.Range(10);

                ulong open = shape.Halo[place] & ~after;
                while (open != 0)
                {
                    int cell = Bits.Lowest(open);
                    open &= open - 1;
                    if ((Lattice.Near[cell] & ~after) == 0) worth -= 20;
                }

                if (worth <= value) continue;
                value = worth;
                best = place;
            }
            return best;
        }

        static int Rescue(ulong board, ref Rng rng, out int place)
        {
            for (int size = 1; size <= Shapes.MaxSize; size++)
            {
                int[] ids = Shapes.BySize[size];
                int start = rng.Range(ids.Length);
                for (int step = 0; step < ids.Length; step++)
                {
                    int id = ids[(start + step) % ids.Length];
                    place = Best(board, Shapes.All[id], ref rng, out _);
                    if (place >= 0) return id;
                }
            }
            place = -1;
            int[] smallest = Shapes.BySize[1];
            return smallest[rng.Range(smallest.Length)];
        }

        static int Sample(GeneratorProfile profile, float level, bool crowded, ref Rng rng)
        {
            float total = 0f;
            for (int size = 1; size <= Shapes.MaxSize; size++) total += Weight(profile, size, level, crowded);
            float roll = rng.Unit() * total;
            int chosen = Shapes.MaxSize;
            for (int size = 1; size <= Shapes.MaxSize; size++)
            {
                roll -= Weight(profile, size, level, crowded);
                if (roll >= 0f) continue;
                chosen = size;
                break;
            }
            int[] ids = Shapes.BySize[chosen];
            return ids[rng.Range(ids.Length)];
        }

        static float Weight(GeneratorProfile profile, int size, float level, bool crowded)
        {
            float weight = profile.Weight(size, level);
            if (!crowded) return weight;
            return size >= 5 ? weight * 0.3f : size <= 2 ? weight * 1.7f : weight;
        }

        static bool Taken(int[] trio, int count, int id)
        {
            for (int i = 0; i < count; i++)
                if (trio[i] == id) return true;
            return false;
        }
    }
}
