using System.Collections.Generic;

namespace Pointless.Core
{
    public static class Lattice
    {
        public const int Rows = 7;
        public const int Cells = Rows * Rows;
        public const int ShortestLine = 5;
        public const float RowHeight = 0.8660254f;

        public static readonly sbyte[] X = new sbyte[Cells];
        public static readonly sbyte[] Y = new sbyte[Cells];
        public static readonly ulong[] Near = new ulong[Cells];
        public static readonly byte[] Walls = new byte[Cells];
        public static readonly ulong[] Lines;
        public static readonly byte[] LineStart;
        public static readonly byte[] LineEnd;

        static Lattice()
        {
            for (int y = 0; y < Rows; y++)
                for (int x = -y; x <= y; x++)
                {
                    int cell = Index(x, y);
                    X[cell] = (sbyte)x;
                    Y[cell] = (sbyte)y;
                }

            var strips = new ulong[Rows * 3];
            for (int cell = 0; cell < Cells; cell++)
            {
                int x = X[cell], y = Y[cell];
                ulong bit = 1UL << cell;
                strips[y] |= bit;
                strips[Rows + ((x + y) >> 1)] |= bit;
                strips[2 * Rows + y - ((x + y + 1) >> 1)] |= bit;

                ulong near = Bit(x - 1, y) | Bit(x + 1, y) | Bit(x, IsUp(x, y) ? y + 1 : y - 1);
                Near[cell] = near;
                Walls[cell] = (byte)(3 - Bits.Count(near));
            }

            var lines = new List<ulong>();
            foreach (ulong strip in strips)
                if (Bits.Count(strip) >= ShortestLine) lines.Add(strip);
            Lines = lines.ToArray();
            LineStart = new byte[Lines.Length];
            LineEnd = new byte[Lines.Length];

            for (int line = 0; line < Lines.Length; line++)
            {
                float longest = -1f;
                for (ulong first = Lines[line]; first != 0; first &= first - 1)
                    for (ulong second = first & (first - 1); second != 0; second &= second - 1)
                    {
                        int from = Bits.Lowest(first), to = Bits.Lowest(second);
                        float acrossX = CenterX(from) - CenterX(to), acrossY = CenterY(from) - CenterY(to);
                        float length = acrossX * acrossX + acrossY * acrossY;
                        if (length <= longest) continue;
                        longest = length;
                        LineStart[line] = (byte)from;
                        LineEnd[line] = (byte)to;
                    }
            }
        }

        public static bool IsUp(int x, int y) => ((x + y) & 1) == 0;

        public static int Index(int x, int y) => (uint)y < Rows && x >= -y && x <= y ? y * y + y + x : -1;

        public static float CenterX(int cell) => X[cell] * 0.5f;

        public static float CenterY(int cell) => (Y[cell] + (IsUp(X[cell], Y[cell]) ? 2f / 3f : 1f / 3f)) * RowHeight;

        public static int Sweep(ulong board, out ulong cleared, out uint lines)
        {
            int count = 0;
            cleared = 0;
            lines = 0;
            for (int line = 0; line < Lines.Length; line++)
            {
                ulong mask = Lines[line];
                if ((board & mask) != mask) continue;
                cleared |= mask;
                lines |= 1u << line;
                count++;
            }
            return count;
        }

        public static ulong Settle(ulong board)
        {
            Sweep(board, out ulong cleared, out _);
            return board & ~cleared;
        }

        static ulong Bit(int x, int y)
        {
            int cell = Index(x, y);
            return cell < 0 ? 0UL : 1UL << cell;
        }
    }
}
