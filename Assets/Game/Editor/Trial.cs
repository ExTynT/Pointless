using System.Text;
using Pointless.Core;

namespace Pointless.Editor
{
    public static class Trial
    {
        const int MoveCap = 4000;

        public static string Run(int games, float sloppy)
        {
            var rng = new Rng(99);
            var lengths = new int[games];
            long moves = 0, score = 0, lines = 0, perfect = 0;
            int capped = 0, topCombo = 0;
            for (int i = 0; i < games; i++)
            {
                var game = new Game();
                game.Start((ulong)(i * 7919 + 1));
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
