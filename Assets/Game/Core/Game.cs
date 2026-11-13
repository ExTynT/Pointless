using System;
using System.Globalization;
using System.Text;

namespace Pointless.Core
{
    public struct Move
    {
        public int Slot;
        public int Shape;
        public int Tint;
        public ulong Mask;
        public ulong Cleared;
        public uint LineBits;
        public int Lines;
        public int Points;
        public int Combo;
        public bool Broke;
        public bool Perfect;
        public bool Dealt;
        public bool Over;
    }

    public sealed class Game
    {
        public const int Slots = 3;
        public const int Tints = 6;
        public const int Grace = 3;
        public const int PerfectBonus = 200;
        const int CellPoints = 2;
        const int ClearedCellPoints = 7;
        const int LinePoints = 36;
        const float ComboStep = 0.38f;
        const int ComboCap = 10;
        const int SavedFields = 17;

        static readonly string format = $"p1.{Shapes.All.Length}.{Lattice.Lines.Length}";

        public ulong Board;
        public int Score;
        public int Combo;
        public int Misses;
        public int Lines;
        public int MaxCombo;
        public bool Over;
        public readonly byte[] Tint = new byte[Lattice.Cells];
        public readonly int[] Tray = new int[Slots];
        public readonly byte[] TrayTint = new byte[Slots];
        public readonly bool[] Placed = new bool[Slots];
        public readonly bool[] Scored = new bool[Slots];

        Rng rng;
        Game undo;
        bool undoReady;
        bool undoSpent;

        public bool CanUndo => undoReady;

        public bool TrioAlive
        {
            get
            {
                for (int slot = 0; slot < Slots; slot++)
                    if (Placed[slot] && !Scored[slot]) return false;
                return true;
            }
        }

        public void Start(ulong seed)
        {
            rng = new Rng(seed);
            Board = 0;
            Score = Combo = Misses = Lines = MaxCombo = 0;
            Over = false;
            Deal();
        }

        public bool Fits(int slot)
        {
            int id = Tray[slot];
            if (id < 0) return false;
            ulong[] masks = Shapes.All[id].Mask;
            for (int place = 0; place < masks.Length; place++)
                if ((Board & masks[place]) == 0) return true;
            return false;
        }

        public Move Place(int slot, int place)
        {
            Shape shape = Shapes.All[Tray[slot]];
            ulong mask = shape.Mask[place];
            var move = new Move { Slot = slot, Shape = shape.Id, Tint = TrayTint[slot], Mask = mask };

            if (!undoSpent)
            {
                undo ??= new Game();
                undo.Copy(this);
                undoReady = true;
            }

            for (ulong cells = mask; cells != 0; cells &= cells - 1) Tint[Bits.Lowest(cells)] = TrayTint[slot];

            Board |= mask;
            move.Points = CellPoints * shape.Size;
            move.Lines = Lattice.Sweep(Board, out move.Cleared, out move.LineBits);
            Placed[slot] = true;
            Tray[slot] = -1;

            if (move.Lines > 0)
            {
                Combo++;
                Misses = 0;
                Scored[slot] = true;
                float multiplier = 1f + ComboStep * Math.Min(Combo - 1, ComboCap);
                int raw = ClearedCellPoints * Bits.Count(move.Cleared) + LinePoints * move.Lines * move.Lines;
                move.Points += (int)Math.Floor(raw * multiplier + 0.5f);
                Board &= ~move.Cleared;
                Lines += move.Lines;
                if (Combo > MaxCombo) MaxCombo = Combo;
            }
            else if (++Misses >= Grace)
            {
                move.Broke = Combo > 1;
                Combo = 0;
            }

            Score += move.Points;
            move.Combo = Combo;

            if (Tray[0] < 0 && Tray[1] < 0 && Tray[2] < 0)
            {
                move.Perfect = Scored[0] && Scored[1] && Scored[2];
                if (move.Perfect) Score += PerfectBonus;
                Deal();
                move.Dealt = true;
            }

            Over = !(Fits(0) || Fits(1) || Fits(2));
            move.Over = Over;
            return move;
        }

        public void Undo()
        {
            if (!undoReady) return;
            Copy(undo);
            undoReady = false;
            undoSpent = true;
        }

        public string Pack()
        {
            var text = new StringBuilder(192);
            text.Append(format).Append(' ').Append(Board).Append(' ').Append(Score).Append(' ').Append(Combo).Append(' ')
                .Append(Misses).Append(' ').Append(Lines).Append(' ').Append(MaxCombo).Append(' ').Append(rng.State);
            for (int slot = 0; slot < Slots; slot++) text.Append(' ').Append(Tray[slot]);
            for (int slot = 0; slot < Slots; slot++) text.Append(' ').Append(TrayTint[slot]);
            text.Append(' ').Append(Flags(Placed)).Append(' ').Append(Flags(Scored)).Append(' ');
            for (int cell = 0; cell < Lattice.Cells; cell++) text.Append((char)('0' + Tint[cell]));
            return text.ToString();
        }

        public bool Unpack(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return false;
            string[] parts = saved.Split(' ');
            if (parts.Length != SavedFields || parts[0] != format) return false;

            var numbers = new ulong[SavedFields - 2];
            for (int i = 0; i < numbers.Length; i++)
            {
                bool minus = parts[i + 1].StartsWith("-", StringComparison.Ordinal);
                if (!ulong.TryParse(minus ? parts[i + 1].Substring(1) : parts[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return false;
                if (minus && (i < 7 || i > 9 || numbers[i] != 1)) return false;
                if (minus) numbers[i] = ulong.MaxValue;
            }

            string tints = parts[SavedFields - 1];
            if (tints.Length != Lattice.Cells || numbers[0] >> Lattice.Cells != 0) return false;
            for (int cell = 0; cell < Lattice.Cells; cell++)
                if (tints[cell] < '0' || tints[cell] >= '0' + Tints) return false;
            for (int slot = 0; slot < Slots; slot++)
            {
                ulong id = numbers[7 + slot];
                if (id != ulong.MaxValue && id >= (ulong)Shapes.All.Length) return false;
                if (numbers[10 + slot] >= Tints) return false;
            }

            Board = numbers[0];
            Score = (int)numbers[1];
            Combo = (int)numbers[2];
            Misses = (int)numbers[3];
            Lines = (int)numbers[4];
            MaxCombo = (int)numbers[5];
            rng.State = numbers[6];
            for (int slot = 0; slot < Slots; slot++)
            {
                Tray[slot] = numbers[7 + slot] == ulong.MaxValue ? -1 : (int)numbers[7 + slot];
                TrayTint[slot] = (byte)numbers[10 + slot];
                Placed[slot] = (numbers[13] >> slot & 1) != 0;
                Scored[slot] = (numbers[14] >> slot & 1) != 0;
            }
            for (int cell = 0; cell < Lattice.Cells; cell++) Tint[cell] = (byte)(tints[cell] - '0');
            undoReady = undoSpent = false;
            Over = !(Fits(0) || Fits(1) || Fits(2));
            return true;
        }

        void Deal()
        {
            Generator.Deal(Board, Score, ref rng, Tray);
            int first = rng.Range(Tints);
            int stride = 1 + rng.Range(2);
            for (int slot = 0; slot < Slots; slot++)
            {
                TrayTint[slot] = (byte)((first + slot * stride) % Tints);
                Placed[slot] = false;
                Scored[slot] = false;
            }
            undoReady = false;
            undoSpent = false;
        }

        void Copy(Game from)
        {
            Board = from.Board;
            Score = from.Score;
            Combo = from.Combo;
            Misses = from.Misses;
            Lines = from.Lines;
            MaxCombo = from.MaxCombo;
            Over = from.Over;
            rng = from.rng;
            Array.Copy(from.Tint, Tint, Tint.Length);
            Array.Copy(from.Tray, Tray, Slots);
            Array.Copy(from.TrayTint, TrayTint, Slots);
            Array.Copy(from.Placed, Placed, Slots);
            Array.Copy(from.Scored, Scored, Slots);
        }

        static int Flags(bool[] values)
        {
            int bits = 0;
            for (int i = 0; i < values.Length; i++)
                if (values[i]) bits |= 1 << i;
            return bits;
        }
    }
}
