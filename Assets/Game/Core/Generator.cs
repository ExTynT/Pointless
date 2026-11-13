namespace Pointless.Core
{
    public static class Generator
    {
        const int Tries = 10;
        const int HardScore = 12000;
        const int CrowdedCells = 21;
        const float EasyHelp = 0.9f;
        const float HardHelp = 0.45f;

        static readonly float[] easy = { 0f, 9f, 28f, 32f, 20f, 8f, 3f };
        static readonly float[] hard = { 0f, 3f, 12f, 24f, 30f, 20f, 11f };

        public static void Deal(ulong board, int score, ref Rng rng, int[] trio)
        {
            float level = score >= HardScore ? 1f : (float)score / HardScore;
            float help = EasyHelp + (HardHelp - EasyHelp) * level;

            for (int slot = 0; slot < trio.Length; slot++)
            {
                bool crowded = Bits.Count(board) > CrowdedCells;
                int best = -1, bestPlace = -1, bestValue = int.MinValue;
                int any = -1, anyPlace = -1, fitting = 0;

                for (int attempt = 0; attempt < Tries; attempt++)
                {
                    int id = Sample(level, crowded, ref rng);
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

        static int Sample(float level, bool crowded, ref Rng rng)
        {
            float total = 0f;
            for (int size = 1; size <= Shapes.MaxSize; size++) total += Weight(size, level, crowded);
            float roll = rng.Unit() * total;
            int chosen = Shapes.MaxSize;
            for (int size = 1; size <= Shapes.MaxSize; size++)
            {
                roll -= Weight(size, level, crowded);
                if (roll >= 0f) continue;
                chosen = size;
                break;
            }
            int[] ids = Shapes.BySize[chosen];
            return ids[rng.Range(ids.Length)];
        }

        static float Weight(int size, float level, bool crowded)
        {
            float weight = easy[size] + (hard[size] - easy[size]) * level;
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
