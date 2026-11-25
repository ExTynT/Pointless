using System;
using System.Collections.Generic;
using System.Reflection;
using Pointless.Core;
using UnityEditor;
using UnityEngine;

namespace Pointless.Editor
{
    public static class Headless
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string ScreenshotDir = "Builds/Screenshots";

        static readonly ulong[] DailyDates =
        {
            20260101, 20260228, 20260315, 20260430, 20260519,
            20260607, 20260722, 20260809, 20260930, 20261014,
            20261123, 20261231, 20270101, 20270214, 20270601
        };

        struct Trio
        {
            public int S0, S1, S2;
            public byte T0, T1, T2;

            public static Trio Capture(Game game) => new Trio
            {
                S0 = game.Tray[0], S1 = game.Tray[1], S2 = game.Tray[2],
                T0 = game.TrayTint[0], T1 = game.TrayTint[1], T2 = game.TrayTint[2]
            };

            public bool Matches(Trio other) =>
                S0 == other.S0 && S1 == other.S1 && S2 == other.S2 &&
                T0 == other.T0 && T1 == other.T1 && T2 == other.T2;
        }

        [MenuItem("Pointless/Check Daily Determinism")]
        public static string CheckDaily()
        {
            var botBest = new Rng(11);
            var botRand = new Rng(97);
            int minSharedDeals = int.MaxValue;
            int maxSharedDeals = 0;
            int divergedGames = 0;

            foreach (ulong dateSeed in DailyDates)
            {
                var gameBest = new Game();
                var gameRand = new Game();
                gameBest.Start(dateSeed, Mode.Daily);
                gameRand.Start(dateSeed, Mode.Daily);

                var dealsBest = new List<Trio> { Trio.Capture(gameBest) };
                var dealsRand = new List<Trio> { Trio.Capture(gameRand) };
                bool divergedOnBoard = false;

                for (int moveIndex = 0; moveIndex < 400 && !gameBest.Over; moveIndex++)
                {
                    if (!PickBest(gameBest, ref botBest, out int slot, out int place)) break;
                    Move move = gameBest.Place(slot, place);
                    if (gameBest.CanUndo)
                        throw new InvalidOperationException($"Daily allowed Undo on {dateSeed}.");
                    if (move.Dealt) dealsBest.Add(Trio.Capture(gameBest));
                }

                for (int moveIndex = 0; moveIndex < 400 && !gameRand.Over; moveIndex++)
                {
                    if (!PickRandom(gameRand, ref botRand, out int slot, out int place)) break;
                    Move move = gameRand.Place(slot, place);
                    if (gameRand.CanUndo)
                        throw new InvalidOperationException($"Daily allowed Undo on {dateSeed}.");
                    if (move.Dealt) dealsRand.Add(Trio.Capture(gameRand));
                }

                int shared = Math.Min(dealsBest.Count, dealsRand.Count);
                if (shared < minSharedDeals) minSharedDeals = shared;
                if (shared > maxSharedDeals) maxSharedDeals = shared;
                if (gameBest.Board != gameRand.Board || gameBest.Score != gameRand.Score) divergedOnBoard = true;
                if (divergedOnBoard && shared >= 2) divergedGames++;

                for (int deal = 0; deal < shared; deal++)
                {
                    if (!dealsBest[deal].Matches(dealsRand[deal]))
                        throw new InvalidOperationException($"Daily {dateSeed} diverged at deal {deal}.");
                }
            }

            if (divergedGames == 0)
                throw new InvalidOperationException("Daily check did not reach >= 2 deals with diverged boards.");

            string report = $"Daily determinism OK across {DailyDates.Length} dates (shared deals {minSharedDeals}..{maxSharedDeals}, diverged multi-deal games {divergedGames}).";
            Log.Info(report);
            return report;
        }

        [MenuItem("Pointless/Test Daily Balance (Trial)")]
        public static string CheckDailyBalance()
        {
            string daily = Trial.Run(100, 0.3f, Mode.Daily);
            string arcade = Trial.Run(100, 0.3f, Mode.Arcade);
            string result = $"Daily:  {daily}\nArcade: {arcade}";
            Log.Info(result);
            return result;
        }

        [MenuItem("Pointless/Run Headless Checks (Play Mode)", true)]
        static bool CanRun() => Application.isPlaying && UnityEngine.Object.FindAnyObjectByType<GameView>() != null;

        [MenuItem("Pointless/Run Headless Checks (Play Mode)")]
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            GameView view = UnityEngine.Object.FindAnyObjectByType<GameView>();
            if (view == null) throw new InvalidOperationException("No GameView in scene.");

            string dailyReport = CheckDaily();

            var savedPrefs = SnapshotPrefs();
            try
            {
                Robot.Frame();
                Invoke(view, "Fit");

                string previewReport = CheckPreviewAndPerfectTrio(view);
                string undoReport = CheckUndoPolicies(view);
                string shareReport = CheckDailyEndAndShare(view);
                string expiryReport = CheckDailyExpiry(view);
                string switchReport = CheckSwitchToExpiredDaily(view);
                string freshReport = CheckSwitchToNewGame(view);
                string effectsReport = CheckSwitchClearsEffects(view);
                string trioReport = CheckUndoOncePerTrio(view);
                string recordReport = CheckUndoAfterGameOver(view);

                string summary = $"{dailyReport}\n{previewReport}\n{undoReport}\n{shareReport}\n{expiryReport}\n{switchReport}\n{freshReport}\n{effectsReport}\n{trioReport}\n{recordReport}";
                Log.Info(summary);
                return summary;
            }
            finally
            {
                if (Camera.main != null) Camera.main.targetTexture = null;
                view.enabled = false;
                RestorePrefs(savedPrefs);
                view.enabled = true;
            }
        }

        static string CheckPreviewAndPerfectTrio(GameView view)
        {
            var rng = new Rng(31);
            bool previewChecked = false;
            bool perfectChecked = false;
            string previewShot = string.Empty;

            view.BeginSeed(Mode.Arcade, 12345);
            SkipArrival(view);

            for (int step = 0; step < 180 && !view.State.Over && !(previewChecked && perfectChecked); step++)
            {
                Game game = view.State;
                if (!previewChecked && TryFindClearingMove(game, out int clearSlot, out int clearPlace, out ulong expectedDoomed))
                {
                    Shape clearShape = Shapes.All[game.Tray[clearSlot]];
                    view.Press(view.SlotPoint(clearSlot));
                    view.Drag(view.PlacePoint(clearSlot, clearPlace));
                    ulong actualDoomed = GetField<ulong>(view, "doomed");
                    ulong actualGhost = GetField<ulong>(view, "ghost");
                    if (actualDoomed != expectedDoomed || actualDoomed == 0)
                        throw new InvalidOperationException($"Clear preview mismatch: expected {expectedDoomed}, got {actualDoomed}.");
                    if (actualGhost != clearShape.Mask[clearPlace])
                        throw new InvalidOperationException("Ghost preview mismatch during drag.");

                    StepAndDraw(view, 0.05f);
                    previewShot = Robot.Shoot($"{ScreenshotDir}/arcade-clear-preview.png");
                    view.Release();
                    SkipArrival(view);

                    if ((view.State.Board & expectedDoomed) != 0)
                        throw new InvalidOperationException("Cleared cells remained on board after release.");
                    previewChecked = true;
                    continue;
                }

                if (!PickBest(game, ref rng, out int slot, out int place)) break;
                bool willFinishPerfect = IsPerfectTrioFinish(game, slot, place);
                int scoreBefore = game.Score;
                PlaceThroughView(view, slot, place);
                if (willFinishPerfect)
                {
                    if (view.State.Score - scoreBefore < Game.PerfectBonus)
                        throw new InvalidOperationException("Perfect Trio bonus was not awarded.");
                    perfectChecked = true;
                }
            }

            for (ulong seed = 101; seed <= 105 && !perfectChecked; seed++)
            {
                view.BeginSeed(Mode.Flow, seed);
                SkipArrival(view);
                for (int step = 0; step < 200 && !view.State.Over && !perfectChecked; step++)
                {
                    Game game = view.State;
                    if (!PickBest(game, ref rng, out int slot, out int place)) break;
                    bool willFinishPerfect = IsPerfectTrioFinish(game, slot, place);
                    int scoreBefore = game.Score;
                    PlaceThroughView(view, slot, place);
                    if (willFinishPerfect)
                    {
                        if (view.State.Score - scoreBefore < Game.PerfectBonus)
                            throw new InvalidOperationException("Perfect Trio bonus was not awarded in Flow.");
                        perfectChecked = true;
                    }
                }
            }

            if (!previewChecked) throw new InvalidOperationException("Did not encounter a clearing move to verify preview.");
            if (!perfectChecked) throw new InvalidOperationException("Did not encounter a Perfect Trio to verify bonus.");
            return $"Preview & Perfect Trio OK (screenshot: {previewShot}).";
        }

        static string CheckUndoPolicies(GameView view)
        {
            var rng = new Rng(53);

            // Arcade: once per trio
            view.BeginSeed(Mode.Arcade, 777);
            SkipArrival(view);
            PickBest(view.State, ref rng, out int s0, out int p0);
            PlaceThroughView(view, s0, p0);
            if (!view.State.CanUndo || view.State.Score == 0)
                throw new InvalidOperationException("Arcade should allow Undo after first move.");
            Robot.Undo();
            SkipArrival(view);
            if (view.State.Score != 0 || view.State.Board != 0)
                throw new InvalidOperationException("Arcade Undo did not restore empty board.");
            if (view.State.CanUndo)
                throw new InvalidOperationException("Arcade should not allow a second Undo in the same trio.");

            for (int i = 0; i < Game.Slots; i++)
            {
                PickBest(view.State, ref rng, out int s, out int p);
                PlaceThroughView(view, s, p);
            }
            PickBest(view.State, ref rng, out int sNext, out int pNext);
            PlaceThroughView(view, sNext, pNext);
            if (!view.State.CanUndo)
                throw new InvalidOperationException("Arcade did not re-enable Undo on the next trio.");

            // Flow: unlimited across deals + non-expiring combo
            view.BeginSeed(Mode.Flow, 888);
            SkipArrival(view);
            if (view.State.Rules.ComboExpires)
                throw new InvalidOperationException("Flow combo should not expire.");
            for (int i = 0; i < 5; i++)
            {
                PickBest(view.State, ref rng, out int s, out int p);
                PlaceThroughView(view, s, p);
            }
            StepAndDraw(view, 0.05f);
            string flowShot = Robot.Shoot($"{ScreenshotDir}/flow-play.png");
            for (int i = 0; i < 5; i++)
            {
                if (!view.State.CanUndo)
                    throw new InvalidOperationException($"Flow Undo stopped early at step {i}.");
                Robot.Undo();
                SkipArrival(view);
            }
            if (view.State.Board != 0 || view.State.Score != 0 || view.State.CanUndo)
                throw new InvalidOperationException("Flow did not undo back to start.");

            // Daily: disabled
            view.BeginSeed(Mode.Daily, 20261123);
            SkipArrival(view);
            PickBest(view.State, ref rng, out int sd, out int pd);
            PlaceThroughView(view, sd, pd);
            if (view.State.CanUndo)
                throw new InvalidOperationException("Daily must not enable Undo.");
            int scoreBeforeTap = view.State.Score;
            Robot.Undo();
            if (view.State.Score != scoreBeforeTap)
                throw new InvalidOperationException("Daily Undo tap should have no effect.");

            return $"Undo policies OK for Arcade, Flow, Daily (screenshot: {flowShot}).";
        }

        static string CheckDailyEndAndShare(GameView view)
        {
            SetField(view, "best", 0);
            PlayerPrefs.SetInt("best.arcade", 4242);
            PlayerPrefs.DeleteKey("best.daily.20261123");
            PlayerPrefs.DeleteKey("best.daily.20261122");

            var rng = new Rng(71);
            view.BeginSeed(Mode.Daily, 20261123);
            SkipArrival(view);

            string midShot = string.Empty;
            for (int move = 0; move < 300 && !view.State.Over; move++)
            {
                float sloppy = move < 40 ? 0.3f : 1.0f;
                bool picked = rng.Unit() < sloppy
                    ? PickRandom(view.State, ref rng, out int s, out int p)
                    : PickBest(view.State, ref rng, out s, out p);
                if (!picked) break;
                PlaceThroughView(view, s, p);
                if (move == 8)
                {
                    StepAndDraw(view, 0.05f);
                    midShot = Robot.Shoot($"{ScreenshotDir}/daily-play.png");
                }
            }

            if (!view.State.Over)
                throw new InvalidOperationException("Daily test game did not reach Game Over within move limit.");

            Game game = view.State;
            if (game.Mode != Mode.Daily || game.Seed != 20261123)
                throw new InvalidOperationException("Daily state/seed mismatch at Game Over.");
            if (PlayerPrefs.GetInt("best.arcade", 0) != 4242)
                throw new InvalidOperationException("Daily modified Arcade best score.");
            if (PlayerPrefs.GetInt("best.daily.20261123", 0) != game.Score)
                throw new InvalidOperationException("Daily best score was not saved under date seed.");

            ForceGameOverScreen(view);
            string shareShot = Robot.Shoot($"{ScreenshotDir}/daily-share.png");

            GUIUtility.systemCopyBuffer = string.Empty;
            string copied = Robot.Share();
            string expected = $"Pointless Daily 2026-11-23\nSkóre: {game.Score}\nLínie: {game.Lines}\nMax Kombo: {game.MaxCombo}";
            if (copied != expected)
                throw new InvalidOperationException($"Share clipboard mismatch.\nExpected:\n{expected}\nActual:\n{copied}");

            float now = GetField<float>(view, "now");
            SetField(view, "now", now + 0.12f);
            Invoke(view, "Draw");
            string copiedShot = Robot.Shoot($"{ScreenshotDir}/daily-copied.png");

            // Verify share date comes from game seed even for a different day (midnight crossover)
            view.BeginSeed(Mode.Daily, 20261122);
            SkipArrival(view);
            for (int move = 0; move < 300 && !view.State.Over; move++)
            {
                if (!PickRandom(view.State, ref rng, out int s, out int p)) break;
                PlaceThroughView(view, s, p);
            }
            if (!view.State.Over)
                throw new InvalidOperationException("Second Daily test game did not reach Game Over.");
            ForceGameOverScreen(view);
            string prevDayCopied = Robot.Share();
            if (!prevDayCopied.StartsWith("Pointless Daily 2026-11-22\n", StringComparison.Ordinal))
                throw new InvalidOperationException($"Daily share did not use game seed date: {prevDayCopied}");

            return $"Daily end & SHARE OK (screenshots: {midShot}, {shareShot}, {copiedShot}).";
        }

        static string CheckDailyExpiry(GameView view)
        {
            ulong today = DateSeed(DateTime.Now), yesterday = DateSeed(DateTime.Now.AddDays(-1));
            view.BeginSeed(Mode.Arcade, 4321);
            SkipArrival(view);
            view.enabled = false;
            PlayerPrefs.SetString("saved.daily", StaleDaily(yesterday));
            PlayerPrefs.SetInt("last_mode", (int)Mode.Daily);
            SetField(view, "dyingCount", 0);
            view.enabled = true;
            SkipArrival(view);
            if (view.State.Mode != Mode.Daily || view.State.Seed != today)
                throw new InvalidOperationException($"Startup resumed {view.State.Mode} {view.State.Seed} instead of a new Daily {today}.");
            if (GetField<int>(view, "dyingCount") != 0)
                throw new InvalidOperationException("Startup dissolved the cells of an expired Daily it never showed.");
            return $"Startup replaces the Daily of {yesterday} with {today}.";
        }

        static string CheckSwitchToExpiredDaily(GameView view)
        {
            ulong today = DateSeed(DateTime.Now), yesterday = DateSeed(DateTime.Now.AddDays(-1));
            string key = "best.daily." + yesterday;
            var rng = new Rng(23);
            PlayerPrefs.SetInt("best.arcade", 9999);
            PlayerPrefs.SetInt(key, 100);
            view.BeginSeed(Mode.Arcade, 4321);
            int shown = Fill(view, ref rng);
            PlayerPrefs.SetString("saved.daily", StaleDaily(yesterday));
            SetField(view, "dyingCount", 0);
            view.SwitchMode(Mode.Daily);
            SkipArrival(view);
            if (view.State.Seed != today)
                throw new InvalidOperationException($"Switching to Daily resumed {view.State.Seed} instead of {today}.");
            if (PlayerPrefs.GetInt(key, 0) != 100)
                throw new InvalidOperationException($"Switching to Daily wrote {PlayerPrefs.GetInt(key, 0)} into the record of {yesterday}.");
            int dying = GetField<int>(view, "dyingCount");
            if (dying != shown)
                throw new InvalidOperationException($"Switching to Daily dissolved {dying} cells, the screen showed {shown}.");
            return $"Switching to Daily starts {today}, keeps the record of {yesterday} and dissolves only the {shown} cells on screen.";
        }

        static string CheckSwitchToNewGame(GameView view)
        {
            var rng = new Rng(29);
            view.BeginSeed(Mode.Arcade, 4321);
            int shown = Fill(view, ref rng);
            PlayerPrefs.DeleteKey("saved.flow");
            SetField(view, "dyingCount", 0);
            view.SwitchMode(Mode.Flow);
            int dying = GetField<int>(view, "dyingCount");
            if (dying != shown)
                throw new InvalidOperationException($"Switching to a new Flow game dissolved {dying} cells, the screen showed {shown}.");
            return $"Switching to a new game dissolves the {shown} cells on screen once.";
        }

        static string CheckSwitchClearsEffects(GameView view)
        {
            var rng = new Rng(37);
            view.BeginSeed(Mode.Flow, 5);
            SkipArrival(view);
            PickBest(view.State, ref rng, out int first, out int firstPlace);
            PlaceThroughView(view, first, firstPlace);
            view.SwitchMode(Mode.Arcade);
            view.BeginSeed(Mode.Arcade, 99);
            SkipArrival(view);
            for (int i = 0; i < 300 && view.State.Lines == 0; i++)
            {
                if (!PickBest(view.State, ref rng, out int slot, out int place)) break;
                PlaceThroughView(view, slot, place);
            }
            int popups = GetField<int>(view, "popupCount"), sweeps = GetField<int>(view, "sweepCount");
            if (popups == 0 || sweeps == 0)
                throw new InvalidOperationException("No clearing move before the switch.");
            view.SwitchMode(Mode.Flow);
            int left = GetField<int>(view, "popupCount"), flashes = GetField<int>(view, "sweepCount");
            if (view.State.Mode != Mode.Flow || view.State.Score == 0)
                throw new InvalidOperationException("Switching back did not resume the Flow game.");
            if (left != 0 || flashes != 0)
                throw new InvalidOperationException($"Resumed Flow game still shows {left} popups and {flashes} line flashes from Arcade.");
            return $"Resuming Flow clears {popups} popups and {sweeps} line flashes from Arcade.";
        }

        static string CheckUndoOncePerTrio(GameView view)
        {
            var rng = new Rng(41);
            view.BeginSeed(Mode.Arcade, 2024);
            SkipArrival(view);
            PickBest(view.State, ref rng, out int slot, out int place);
            PlaceThroughView(view, slot, place);
            Robot.Undo();
            SkipArrival(view);
            if (view.State.CanUndo)
                throw new InvalidOperationException("Arcade allowed a second Undo right away.");
            view.SwitchMode(Mode.Flow);
            view.SwitchMode(Mode.Arcade);
            SkipArrival(view);
            PickBest(view.State, ref rng, out slot, out place);
            PlaceThroughView(view, slot, place);
            if (view.State.CanUndo)
                throw new InvalidOperationException("Leaving and resuming Arcade gave a second Undo in the same trio.");
            return "Arcade keeps one Undo per trio across a mode switch.";
        }

        static string CheckUndoAfterGameOver(GameView view)
        {
            var rng = new Rng(17);
            for (ulong seed = 1; seed <= 200; seed++)
            {
                SetField(view, "best", 0);
                PlayerPrefs.DeleteKey("best.arcade");
                view.BeginSeed(Mode.Arcade, seed);
                SkipArrival(view);
                while (!view.State.Over && PickRandom(view.State, ref rng, out int slot, out int place))
                    PlaceThroughView(view, slot, place);
                if (!view.State.Over || !view.State.CanUndo) continue;

                int over = view.State.Score;
                if (PlayerPrefs.GetInt("best.arcade", 0) != over)
                    throw new InvalidOperationException("Arcade game over did not save the record.");
                Robot.Undo();
                SkipArrival(view);
                int kept = PlayerPrefs.GetInt("best.arcade", 0);
                if (view.State.Over || kept != view.State.Score)
                    throw new InvalidOperationException($"Arcade Undo after game over kept record {kept} for score {view.State.Score} (was {over}).");
                if (GetField<bool>(view, "endRecord"))
                    throw new InvalidOperationException("Arcade Undo after game over kept the record glow.");
                return $"Arcade Undo after game over lowers the record from {over} to {kept} (seed {seed}).";
            }
            throw new InvalidOperationException("No Arcade game ended with Undo available.");
        }

        static int Fill(GameView view, ref Rng rng)
        {
            SkipArrival(view);
            while (view.State.Board == 0 && PickRandom(view.State, ref rng, out int slot, out int place))
                PlaceThroughView(view, slot, place);
            if (view.State.Board == 0) throw new InvalidOperationException("Could not leave cells on the board.");
            return Bits.Count(view.State.Board);
        }

        static ulong DateSeed(DateTime day) => (ulong)(day.Year * 10000 + day.Month * 100 + day.Day);

        static string StaleDaily(ulong seed)
        {
            var game = new Game();
            game.Start(seed, Mode.Daily);
            var rng = new Rng(5);
            PickBest(game, ref rng, out int slot, out int place);
            game.Place(slot, place);
            return game.Pack();
        }

        static void PlaceThroughView(GameView view, int slot, int place)
        {
            SkipArrival(view);
            view.Press(view.SlotPoint(slot));
            view.Drag(view.PlacePoint(slot, place));
            view.Release();
            SkipArrival(view);
        }

        static void SkipArrival(GameView view)
        {
            var slotFrom = GetField<float[]>(view, "slotFrom");
            for (int i = 0; i < slotFrom.Length; i++) slotFrom[i] = -1f;
            Invoke(view, "Step", 1f);
        }

        static void StepAndDraw(GameView view, float delta)
        {
            Invoke(view, "Step", delta);
            Invoke(view, "Draw");
        }

        static void ForceGameOverScreen(GameView view)
        {
            float now = GetField<float>(view, "now");
            SetField(view, "overAt", now - 2f);
            SetField(view, "veil", 1f);
            StepAndDraw(view, 0.2f);
        }

        static bool TryFindClearingMove(Game game, out int foundSlot, out int foundPlace, out ulong clearedMask)
        {
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                int id = game.Tray[slot];
                if (id < 0) continue;
                Shape shape = Shapes.All[id];
                for (int place = 0; place < shape.Mask.Length; place++)
                {
                    if ((game.Board & shape.Mask[place]) != 0) continue;
                    if (Lattice.Sweep(game.Board | shape.Mask[place], out clearedMask, out _) > 0)
                    {
                        foundSlot = slot;
                        foundPlace = place;
                        return true;
                    }
                }
            }
            foundSlot = foundPlace = -1;
            clearedMask = 0;
            return false;
        }

        static bool IsPerfectTrioFinish(Game game, int slot, int place)
        {
            int remaining = 0;
            for (int i = 0; i < Game.Slots; i++)
            {
                if (game.Tray[i] >= 0) remaining++;
                else if (!game.Scored[i]) return false;
            }
            if (remaining != 1) return false;
            ulong mask = Shapes.All[game.Tray[slot]].Mask[place];
            return Lattice.Sweep(game.Board | mask, out _, out _) > 0;
        }

        static bool PickBest(Game game, ref Rng rng, out int slot, out int place)
        {
            slot = place = -1;
            int best = int.MinValue;
            for (int i = 0; i < Game.Slots; i++)
            {
                if (game.Tray[i] < 0) continue;
                int found = Generator.Best(game.Board, Shapes.All[game.Tray[i]], ref rng, out int value);
                if (found < 0 || value <= best) continue;
                best = value;
                slot = i;
                place = found;
            }
            return slot >= 0;
        }

        static bool PickRandom(Game game, ref Rng rng, out int slot, out int place)
        {
            slot = place = -1;
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

        static string[] PrefKeys() => new[]
        {
            "best.arcade", "best.flow", "best.daily.20261123", "best.daily.20261122",
            "best.daily." + DateSeed(DateTime.Now), "best.daily." + DateSeed(DateTime.Now.AddDays(-1)),
            "saved", "saved.arcade", "saved.flow", "saved.daily", "goals", "last_mode", "taught", "flow_hints"
        };

        static Dictionary<string, string> SnapshotPrefs()
        {
            var saved = new Dictionary<string, string>();
            foreach (string key in PrefKeys())
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                saved[key] = key.StartsWith("saved", StringComparison.Ordinal) || key == "goals" ? "s:" + PlayerPrefs.GetString(key, string.Empty) : "i:" + PlayerPrefs.GetInt(key, 0);
            }
            return saved;
        }

        static void RestorePrefs(Dictionary<string, string> saved)
        {
            foreach (string key in PrefKeys()) PlayerPrefs.DeleteKey(key);
            foreach (var entry in saved)
            {
                if (entry.Value.StartsWith("s:", StringComparison.Ordinal))
                    PlayerPrefs.SetString(entry.Key, entry.Value.Substring(2));
                else
                    PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value.Substring(2)));
            }
            PlayerPrefs.Save();
        }

        static T GetField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Private).GetValue(target);

        static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);
    }
}