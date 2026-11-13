using System.Collections.Generic;

namespace Pointless.Core
{
    public sealed class Shape
    {
        public int Id;
        public int Size;
        public sbyte[] X;
        public sbyte[] Y;
        public float CenterX;
        public float CenterY;
        public ulong[] Mask;
        public ulong[] Halo;
        public byte[] Wall;
        public sbyte[] OriginX;
        public sbyte[] OriginY;
    }

    public static class Shapes
    {
        public const int MaxSize = 6;
        const int CompactSpan = 7;

        public static readonly Shape[] All;
        public static readonly int[][] BySize = new int[MaxSize + 1][];

        static Shapes()
        {
            var all = new List<Shape>();
            var seen = new HashSet<ulong>();
            var level = new List<int[]>();
            Grow(level, seen, new int[0], 0, 0);
            Grow(level, seen, new int[0], 1, 0);

            BySize[0] = new int[0];
            for (int size = 1; size <= MaxSize; size++)
            {
                var ids = new List<int>();
                foreach (int[] cells in level)
                {
                    Shape shape = Build(cells);
                    if (shape == null) continue;
                    shape.Id = all.Count;
                    ids.Add(shape.Id);
                    all.Add(shape);
                }
                BySize[size] = ids.ToArray();
                if (size == MaxSize) break;

                var next = new List<int[]>();
                foreach (int[] cells in level)
                    for (int i = 0; i < cells.Length; i += 2)
                    {
                        int x = cells[i], y = cells[i + 1];
                        Grow(next, seen, cells, x - 1, y);
                        Grow(next, seen, cells, x + 1, y);
                        Grow(next, seen, cells, x, Lattice.IsUp(x, y) ? y + 1 : y - 1);
                    }
                level = next;
            }
            All = all.ToArray();
        }

        static void Grow(List<int[]> into, HashSet<ulong> seen, int[] cells, int x, int y)
        {
            for (int i = 0; i < cells.Length; i += 2)
                if (cells[i] == x && cells[i + 1] == y) return;

            var grown = new int[cells.Length + 2];
            cells.CopyTo(grown, 0);
            grown[cells.Length] = x;
            grown[cells.Length + 1] = y;

            int minX = int.MaxValue, minY = int.MaxValue;
            for (int i = 0; i < grown.Length; i += 2)
            {
                if (grown[i] < minX) minX = grown[i];
                if (grown[i + 1] < minY) minY = grown[i + 1];
            }
            int shiftX = -minX, shiftY = -minY;
            if (((shiftX + shiftY) & 1) != 0) shiftX++;
            for (int i = 0; i < grown.Length; i += 2)
            {
                grown[i] += shiftX;
                grown[i + 1] += shiftY;
            }

            for (int i = 2; i < grown.Length; i += 2)
                for (int j = i; j > 0 && Order(grown, j - 2) > Order(grown, j); j -= 2)
                {
                    (grown[j - 2], grown[j]) = (grown[j], grown[j - 2]);
                    (grown[j - 1], grown[j + 1]) = (grown[j + 1], grown[j - 1]);
                }

            ulong key = (ulong)(grown.Length / 2);
            for (int i = 0; i < grown.Length; i += 2) key = (key << 8) | (uint)Order(grown, i);
            if (seen.Add(key)) into.Add(grown);
        }

        static int Order(int[] cells, int i) => (cells[i + 1] << 4) | (cells[i] + 1);

        static Shape Build(int[] cells)
        {
            int size = cells.Length / 2;
            if (size == MaxSize && Sprawls(cells)) return null;

            var shape = new Shape { Size = size, X = new sbyte[size], Y = new sbyte[size] };
            for (int i = 0; i < size; i++)
            {
                int x = cells[i * 2], y = cells[i * 2 + 1];
                shape.X[i] = (sbyte)x;
                shape.Y[i] = (sbyte)y;
                shape.CenterX += x;
                shape.CenterY += y + (Lattice.IsUp(x, y) ? 2f / 3f : 1f / 3f);
            }
            shape.CenterX /= size;
            shape.CenterY /= size;

            var masks = new List<ulong>();
            var halos = new List<ulong>();
            var walls = new List<byte>();
            var originsX = new List<sbyte>();
            var originsY = new List<sbyte>();
            for (int originY = 0; originY < Lattice.Rows; originY++)
                for (int originX = -2 * Lattice.Rows; originX <= 2 * Lattice.Rows; originX++)
                {
                    if (((originX + originY) & 1) != 0) continue;
                    ulong mask = 0, halo = 0;
                    int wall = 0;
                    bool inside = true;
                    for (int i = 0; i < size && inside; i++)
                    {
                        int cell = Lattice.Index(shape.X[i] + originX, shape.Y[i] + originY);
                        if (cell < 0) { inside = false; break; }
                        mask |= 1UL << cell;
                        halo |= Lattice.Near[cell];
                        wall += Lattice.Walls[cell];
                    }
                    if (!inside) continue;
                    masks.Add(mask);
                    halos.Add(halo & ~mask);
                    walls.Add((byte)wall);
                    originsX.Add((sbyte)originX);
                    originsY.Add((sbyte)originY);
                }
            if (masks.Count == 0) return null;

            shape.Mask = masks.ToArray();
            shape.Halo = halos.ToArray();
            shape.Wall = walls.ToArray();
            shape.OriginX = originsX.ToArray();
            shape.OriginY = originsY.ToArray();
            return shape;
        }

        static bool Sprawls(int[] cells)
        {
            uint rows = 0, rising = 0, falling = 0;
            for (int i = 0; i < cells.Length; i += 2)
            {
                int x = cells[i], y = cells[i + 1];
                rows |= 1u << y;
                rising |= 1u << ((x + y) >> 1);
                falling |= 1u << (y - ((x + y + 1) >> 1) + 16);
            }
            int a = Bits.Count(rows), b = Bits.Count(rising), c = Bits.Count(falling);
            bool straight = a == 1 || b == 1 || c == 1;
            return !straight && a + b + c > CompactSpan;
        }
    }
}
