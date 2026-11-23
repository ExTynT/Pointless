using System.Text;
using Pointless.Core;

namespace Pointless.Editor
{
    public static class Trial
    {
        const int MoveCap = 4000;

        public static string Run(int games, float sloppy, Mode mode = Mode.Arcade)
        {
            var rng = new Rng(99);
            var lengths = new int[games];
            long moves = 0, score = 0, lines = 0, perfect = 0;
            int capped = 0, topCombo = 0;
            for (int i = 0; i < games; i++)
            {
                var game = new Game();
                game.Start((ulong)(i * 7919 + 1), mode);
                int count = 0;
                while (!game.Over && count < MoveCap)
                {
                    if (!Choose(game, sloppy, ref rng, out int slot, out int place)) break;
                    if (game.Place(slot, place).Perfect) perfect++;
                    count++;
                }
                lengths[i] = count;
                moves += count;
                score += game.Score;
                lines += game.Lines;
                if (count >= MoveCap) capped++;
                if (game.MaxCombo > topCombo) topCombo = game.MaxCombo;
            }
            System.Array.Sort(lengths);
            var report = new StringBuilder();
            report.Append($"sloppy {sloppy:0.00}: moves avg {moves / games} median {lengths[games / 2]} p10 {lengths[games / 10]} p90 {lengths[games * 9 / 10]}");
            report.Append($" | score avg {score / games} | clears per move {(float)lines / moves:0.00} | perfect per game {(float)perfect / games:0.0}");
            report.Append($" | capped {capped} | top combo {topCombo}");
            return report.ToString();
        }

        public static string CheckFlow()
        {
            var game = new Game();
            game.Start(1, Mode.Flow);
            var bot = new Rng(99);
            var states = new string[16];
            var slots = new int[15];
            var places = new int[15];
            states[0] = game.Pack();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!Choose(game, 0f, ref bot, out slots[i], out places[i]))
                    throw new System.InvalidOperationException("Flow bot stopped before move 15.");
                game.Place(slots[i], places[i]);
                states[i + 1] = game.Pack();
            }
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                game.Undo();
                if (game.Pack() != states[i])
                    throw new System.InvalidOperationException($"Wrong state after undoing move {i + 1}.");
            }
            if (game.CanUndo) throw new System.InvalidOperationException("Undo past the start.");
            for (int i = 0; i < slots.Length; i++)
            {
                game.Place(slots[i], places[i]);
                if (game.Pack() != states[i + 1])
                    throw new System.InvalidOperationException($"Replaying move {i + 1} changed the state.");
            }

            game.Start(1, Mode.Flow);
            int misses = 0;
            for (int i = 0; i < MoveCap && Choose(game, 0.6f, ref bot, out int slot, out int place); i++)
            {
                int combo = game.Combo;
                Move move = game.Place(slot, place);
                if (move.Lines == 0)
                {
                    if (game.Combo != combo) throw new System.InvalidOperationException("Flow combo expired.");
                    misses++;
                }
                else
                {
                    if (combo > 0 && misses >= 5 && game.Combo == combo + 1)
                        return $"15 undos and replays matched; combo {combo} continued after {misses} misses.";
                    misses = 0;
                }
            }
            throw new System.InvalidOperationException("The bot did not exercise a combo after five misses.");
        }

        static bool Choose(Game game, float sloppy, ref Rng rng, out int slot, out int place)
        {
            slot = place = -1;
            if (rng.Unit() < sloppy)
            {
                int start = rng.Range(Game.Slots);
                for (int step = 0; step < Game.Slots; step++)
                {
                    int candidate = (start + step) % Game.Slots;
                    if (game.Tray[candidate] < 0) continue;
                    ulong[] masks = Shapes.All[game.Tray[candidate]].Mask;
                    int first = rng.Range(masks.Length);
                    for (int offset = 0; offset < masks.Length; offset++)
                    {
                        int index = (first + offset) % masks.Length;
                        if ((game.Board & masks[index]) != 0) continue;
                        slot = candidate;
                        place = index;
                        return true;
                    }
                }
                return false;
            }

            int best = int.MinValue;
            for (int candidate = 0; candidate < Game.Slots; candidate++)
            {
                if (game.Tray[candidate] < 0) continue;
                int found = Generator.Best(game.Board, Shapes.All[game.Tray[candidate]], ref rng, out int value);
                if (found < 0 || value <= best) continue;
                best = value;
                slot = candidate;
                place = found;
            }
            return slot >= 0;
        }
    }
}
