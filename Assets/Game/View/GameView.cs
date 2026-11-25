using System;
using Pointless.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Pointless
{
    public sealed class GameView : MonoBehaviour
    {
        const float Wide = 10f;
        const float Stack = 13.2f;
        const float Root3 = 1.7320508f;
        const float TraySize = 0.56f;
        const float CellHalf = 0.435f;
        const float Corner = 0.2f;
        const float SnapReach = 0.8f;
        const float TouchLift = 0.45f;
        const float DropTime = 0.07f;
        const float PopTime = 0.24f;
        const float FlashTime = 0.08f;
        const float ShrinkTime = 0.2f;
        const float SweepTime = 0.34f;
        const float PopupTime = 0.9f;
        const float ShakeTime = 0.28f;
        const float RestartWait = 0.45f;
        const float OpenTime = 0.45f;
        const float CloseTime = 0.2f;
        const float EndScale = 1.35f;
        const float WidestScore = 0.84f;
        const float HintWait = 1.6f;
        const float HintLoop = 2.2f;
        const int IdleInterval = 4;
        const string LegacyBestKey = "best";
        const string ArcadeBestKey = "best.arcade";
        const string FlowBestKey = "best.flow";
        const string DailyBestPrefix = "best.daily.";
        const string MuteKey = "mute";
        const string TaughtKey = "taught";
        const string FlowHintKey = "flow_hints";
        const string SavedKey = "saved";
        const string SavedArcadeKey = "saved.arcade";
        const string SavedFlowKey = "saved.flow";
        const string SavedDailyKey = "saved.daily";
        const string LastModeKey = "last_mode";
        const string ThemeKey = "theme";
        const string GoalsKey = "goals";

        static readonly Color32[] previewBack = { Theme.Hex(0x0E1015), Theme.Hex(0x160D18), Theme.Hex(0x08131C) };
        static readonly Color32[] previewMain = { Theme.Hex(0x37D9A0), Theme.Hex(0xFF964F), Theme.Hex(0x2EE6D6) };

        struct Dying
        {
            public int Cell;
            public int Tint;
            public int Own;
            public float At;
            public bool Burst;
        }

        struct Sweep
        {
            public int Line;
            public int Tint;
            public float At;
        }

        struct Shard
        {
            public Vector2 At, Speed;
            public float Turn, Spin, Born, Life, Size;
            public Color32 Tint;
            public bool Up;
        }

        struct Popup
        {
            public Vector2 At;
            public int Value;
            public float Born, Size;
            public Color32 Tint;
        }

        [SerializeField] Camera view;
        [SerializeField] MeshFilter surface;

        readonly Game game = new Game();
        readonly Vector2[] slotAt = new Vector2[Game.Slots];
        readonly float[] slotSize = new float[Game.Slots];
        readonly float[] slotFrom = new float[Game.Slots];
        readonly float[] pipAt = new float[Game.Slots];
        readonly bool[] slotOpen = new bool[Game.Slots];
        readonly Dying[] dying = new Dying[128];
        readonly Sweep[] sweeps = new Sweep[16];
        readonly Shard[] shards = new Shard[256];
        readonly Popup[] popups = new Popup[8];
        readonly char[] text = new char[12];
        readonly Goals goals = new Goals();
        public Goals Goals => goals;
        bool menuOpen;
        float menuVeil;
        Vector2 modeButtonAt;
        readonly Vector2[] menuModeAt = new Vector2[3];
        readonly Vector2[] menuThemeAt = new Vector2[3];
        readonly Vector2[] menuGoalAt = new Vector2[4];

        Batch batch;
        Sfx sfx;
        Rng chance;
        int pixelsWide, pixelsHigh;
        Rect safe;
        float high, side, rise, cellHalf, apexY, trayY, slotGap, scoreY, bestY, pipY, endY, lift, veil;
        int endLines, endCombo;
        bool endRecord, feted;
        Vector2 undoAt, soundAt, hintButtonAt, streakAt, dailyAt, shareAt;
        int held = -1, snap = -1, hintSlot = -1, hintPlace;
        Vector2 aim, dropFrom;
        ulong ghost, doomed, dropMask;
        int dyingCount, sweepCount, shardCount, popupCount;
        float now, busyUntil, shown, idleAt, hintAt, dropAt, scoreHit, streakHit, bestHit, shakeAt, shakeSize, overAt, perfectAt, copiedAt;
        int best, record;
        bool pressed, dirty, taught, beaten, flowHints;

        public Game State => game;

        public Vector2 SlotPoint(int slot) => Home(slot);

        public Vector2 PlacePoint(int slot, int place) => Spot(Shapes.All[game.Tray[slot]], place) - new Vector2(0f, lift);

        void OnEnable()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value), 60, 120);
            batch = new Batch();
            surface.sharedMesh = batch.Mesh;
            sfx = new Sfx(gameObject) { Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1 };

            Theme.Use((ThemeId)PlayerPrefs.GetInt(ThemeKey, 0));
            if (view != null) view.backgroundColor = Theme.Back;

            Haptics.Start();
            if (PlayerPrefs.HasKey(LegacyBestKey))
            {
                int legacy = PlayerPrefs.GetInt(LegacyBestKey, 0), arcade = PlayerPrefs.GetInt(ArcadeBestKey, 0);
                bool higher = legacy > arcade;
                if (higher) PlayerPrefs.SetInt(ArcadeBestKey, legacy);
                PlayerPrefs.DeleteKey(LegacyBestKey);
                PlayerPrefs.Save();
                Log.Info(higher ? $"Moved legacy best {legacy} to Arcade" : $"Dropped legacy best {legacy}, kept Arcade best {arcade}");
            }

            if (PlayerPrefs.HasKey(SavedKey))
            {
                string oldSave = PlayerPrefs.GetString(SavedKey, string.Empty);
                var old = new Game();
                Mode oldMode = old.Unpack(oldSave) ? old.Mode : Mode.Arcade;
                bool moved = !string.IsNullOrEmpty(oldSave) && !PlayerPrefs.HasKey(SavedKeyFor(oldMode));
                if (moved) PlayerPrefs.SetString(SavedKeyFor(oldMode), oldSave);
                PlayerPrefs.DeleteKey(SavedKey);
                PlayerPrefs.Save();
                Log.Info(moved ? $"Moved legacy save to {oldMode}" : $"Dropped legacy save, {oldMode} already has one");
            }
            
            string goalsData = PlayerPrefs.GetString(GoalsKey, string.Empty);
            if (!goals.Unpack(goalsData)) Log.Warn($"Goals data damaged: {goalsData}");
            flowHints = PlayerPrefs.GetInt(FlowHintKey, 0) == 1;
            taught = PlayerPrefs.GetInt(TaughtKey, 0) == 1;
            Log.Info($"Start {Application.version} on {SystemInfo.deviceModel}, {Application.targetFrameRate} fps, theme {Theme.Current}, sound {(sfx.Muted ? "off" : "on")}, goal {goals.Active}");
            chance = new Rng((ulong)DateTime.UtcNow.Ticks);
            now = Time.unscaledTime;
            copiedAt = scoreHit = streakHit = bestHit = shakeAt = perfectAt = dropAt = now - 10f;
            pixelsWide = pixelsHigh = 0;
            Fit();
            Open((Mode)Mathf.Clamp(PlayerPrefs.GetInt(LastModeKey, 0), 0, 2));
        }

        void OnDisable()
        {
            Store();
            sfx?.Dispose();
            batch?.Dispose();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Store();
        }

        void Update()
        {
            float before = now;
            now = Time.unscaledTime;
            Fit();
            Read();
            Step(Mathf.Min(now - before, 0.05f));
            Guide();

            bool busy = held >= 0 || now < busyUntil || game.Over || menuOpen || menuVeil > 0f;
            OnDemandRendering.renderFrameInterval = busy ? 1 : IdleInterval;
            if (!busy && !dirty) return;
            dirty = false;
            Draw();
        }

        public void Press(Vector2 at, bool touch = false)
        {
            Busy(0.3f);
            idleAt = now;
            hintSlot = -1;
            if (menuOpen)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Near(at, menuModeAt[i]))
                    {
                        SwitchMode((Mode)i);
                        menuOpen = false;
                        sfx.Pick();
                        Haptics.Click();
                        return;
                    }
                    if (Near(at, menuThemeAt[i]))
                    {
                        Theme.Use((ThemeId)i);
                        Log.Info($"Theme {Theme.Current}");
                        if (view != null) view.backgroundColor = Theme.Back;
                        PlayerPrefs.SetInt(ThemeKey, i);
                        PlayerPrefs.Save();
                        dirty = true;
                        sfx.Pick();
                        Haptics.Click();
                        return;
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    if (Near(at, menuGoalAt[i]))
                    {
                        goals.Select((GoalKind)i);
                        Log.Info($"Goal {goals.Active}");
                        PlayerPrefs.SetString(GoalsKey, goals.Pack());
                        PlayerPrefs.Save();
                        dirty = true;
                        sfx.Pick();
                        Haptics.Click();
                        return;
                    }
                }
                menuOpen = false;
                sfx.Back();
                return;
            }

            if (Near(at, modeButtonAt))
            {
                menuOpen = true;
                sfx.Pick();
                Haptics.Click();
                return;
            }

            // if (NearBox(at, dailyAt, side * 1.1f, side * 0.45f))
            // {
            //     Begin(game.Mode == Mode.Daily ? Mode.Arcade : Mode.Daily);
            //     sfx.Pick();
            //     return;
            // }
            if (game.Rules.Hints == HintPolicy.Optional && Near(at, hintButtonAt))
            {
                flowHints = !flowHints;
                Log.Info($"Flow hints {(flowHints ? "on" : "off")}");
                PlayerPrefs.SetInt(FlowHintKey, flowHints ? 1 : 0);
                PlayerPrefs.Save();
                sfx.Pick();
                return;
            }
            if (Near(at, soundAt))
            {
                sfx.Muted = !sfx.Muted;
                Log.Info($"Sound {(sfx.Muted ? "off" : "on")}");
                PlayerPrefs.SetInt(MuteKey, sfx.Muted ? 1 : 0);
                sfx.Pick();
                return;
            }
            if (game.CanUndo && Near(at, undoAt))
            {
                Undo();
                return;
            }
            if (game.Over)
            {
                if (game.Mode == Mode.Daily && now > overAt + RestartWait && NearBox(at, shareAt, side * 0.9f, side * 0.45f))
                {
                    string shareText = $"Pointless Daily {FormatSeedDate(game.Seed)}\nSkóre: {game.Score}\nLínie: {endLines}\nMax Kombo: {endCombo}";
                    ShareText(shareText);
                    copiedAt = now;
                    sfx.Pick();
                    Haptics.Click();
                    Busy(2.2f);
                    Log.Info("Daily share text\n" + shareText);
                    return;
                }
                if (now > overAt + RestartWait) Begin(game.Mode);
                return;
            }
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                if (game.Tray[slot] < 0 || now < slotFrom[slot]) continue;
                Vector2 home = Home(slot);
                if (Mathf.Abs(at.x - home.x) > slotGap * 0.5f || Mathf.Abs(at.y - home.y) > side * 1.6f) continue;
                held = slot;
                snap = -1;
                ghost = doomed = 0;
                lift = touch ? TouchLift * side : 0f;
                slotAt[slot] = aim = at + new Vector2(0f, lift);
                sfx.Pick();
                Haptics.Tick();
                return;
            }
        }

        public void Drag(Vector2 at)
        {
            if (held < 0) return;
            slotAt[held] = aim = at + new Vector2(0f, lift);
            Seek();
        }

        public void Release()
        {
            if (held < 0) return;
            int slot = held, place = snap;
            held = snap = -1;
            ghost = doomed = 0;
            idleAt = now;
            Busy(0.8f);
            if (place < 0) sfx.Back();
            else Commit(slot, place);
        }

        void Read()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame)
            {
                Log.Info("Back pressed, saving and quitting");
                Store();
                Application.Quit();
            }

            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            bool down = pointer.press.isPressed;
            if (!down && !pressed) return;
            Vector2 pixel = pointer.position.ReadValue();
            var at = new Vector2((pixel.x / pixelsWide - 0.5f) * Wide, (pixel.y / pixelsHigh - 0.5f) * high);
            if (!pressed) Press(at, pointer is Touchscreen);
            else
            {
                Drag(at);
                if (!down) Release();
            }
            pressed = down;
        }

        void Fit()
        {
            int wide = view.pixelWidth, tall = view.pixelHeight;
            Rect area = Screen.safeArea;
            if (wide == pixelsWide && tall == pixelsHigh && area == safe) return;
            pixelsWide = wide;
            pixelsHigh = tall;
            safe = area;
            Log.Info($"Screen {wide}x{tall}, safe area {area}");

            high = Wide * tall / wide;
            view.orthographicSize = high * 0.5f;
            float top = high * 0.5f, bottom = -top;
            if (tall == Screen.height && wide == Screen.width)
            {
                top = high * (area.yMax / tall - 0.5f);
                bottom = high * (area.yMin / tall - 0.5f);
            }

            float room = top - bottom;
            side = Mathf.Min(Wide / 7.9f, room / Stack);
            rise = side * Root3 * 0.5f;
            cellHalf = side * CellHalf;
            float spare = room - Stack * side;

            scoreY = top - 1.7f * side - spare * 0.36f;
            bestY = scoreY - 0.72f * side;
            apexY = bestY - 0.75f * side - spare * 0.15f;
            trayY = apexY - Lattice.Rows * rise - 1.85f * side - spare * 0.12f;
            pipY = trayY - 1.3f * side;
            endY = apexY - 2.1f * side;
            slotGap = Mathf.Min(2.6f * side, Wide / 3.05f);
            undoAt = new Vector2(-Wide * 0.5f + 0.95f * side, scoreY + 0.5f * side);
            soundAt = new Vector2(Wide * 0.5f - 0.95f * side, scoreY + 0.5f * side);
            streakAt = new Vector2(-2.55f * side, apexY - 1.9f * rise);
            dailyAt =   modeButtonAt = new Vector2(-Wide * 0.5f + 0.95f * side, scoreY - 0.55f * side);
            for (int i = 0; i < 3; i++)
            {
                menuModeAt[i] = new Vector2((i - 1) * 2.4f * side, apexY - 1.5f * side);
                menuThemeAt[i] = new Vector2((i - 1) * 2.4f * side, apexY - 3.7f * side);
            }
            for (int i = 0; i < 4; i++)
                menuGoalAt[i] = new Vector2((i - 1.5f) * 1.9f * side, apexY - 5.9f * side);
            hintButtonAt = new Vector2(soundAt.x, dailyAt.y - 0.25f * side);
            shareAt = new Vector2(0f, endY - 3.2f * side);

            for (int slot = 0; slot < Game.Slots; slot++)
                if (slot != held) slotAt[slot] = Home(slot);
            dirty = true;
        }

        public void Begin(Mode mode = Mode.Arcade) => BeginSeed(mode, SeedFor(mode));

        public void BeginSeed(Mode mode, ulong seed)
        {
            Keep();
            Dissolve(0.03f);
            Fresh(mode, seed);
        }

        ulong SeedFor(Mode mode) => mode == Mode.Daily ? TodaySeed() : chance.Next();

        void Fresh(Mode mode, ulong seed)
        {
            PlayerPrefs.SetInt(LastModeKey, (int)mode);
            PlayerPrefs.DeleteKey(SavedKeyFor(mode));
            goals.ClearHistory();
            game.Start(seed, mode);
            best = PlayerPrefs.GetInt(BestKey(), 0);
            Log.Info($"New {mode} game, seed {seed}, best {best}");
            record = best;
            beaten = false;
            shown = 0f;
            Calm();
            Arrive(0.15f);
            Refresh();
        }

        void Resume()
        {
            best = Mathf.Max(PlayerPrefs.GetInt(BestKey(), 0), game.Score);
            Log.Info($"Resume {game.Mode} game, seed {game.Seed}, score {game.Score}, best {best}");
            record = best;
            beaten = record > 0 && game.Score >= record;
            shown = game.Score;
            Calm();
            Arrive(0.1f);
            Refresh();
        }

        void Calm()
        {
            endRecord = feted = false;
            held = snap = -1;
            ghost = doomed = dropMask = 0;
            popupCount = sweepCount = 0;
        }

        void Arrive(float wait)
        {
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                slotAt[slot] = Home(slot) - new Vector2(0f, side * 1.4f);
                slotSize[slot] = 0f;
                slotFrom[slot] = now + wait + slot * 0.07f;
            }
            idleAt = now + wait;
            hintSlot = -1;
            sfx.Deal(wait);
            Busy(wait + 1f);
        }

        void Refresh()
        {
            for (int slot = 0; slot < Game.Slots; slot++) slotOpen[slot] = game.Fits(slot);
        }

        void Guide()
        {
            bool enabled = game.Rules.Hints == HintPolicy.Optional ? flowHints : game.Rules.Hints == HintPolicy.Tutorial && !taught;
            if (!enabled || held >= 0 || game.Over || now - idleAt < HintWait)
            {
                hintSlot = -1;
                return;
            }
            Busy(0.2f);
            if (hintSlot >= 0) return;
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                if (!slotOpen[slot] || now < slotFrom[slot]) continue;
                hintPlace = Generator.Best(game.Board, Shapes.All[game.Tray[slot]], ref chance, out _);
                hintSlot = slot;
                hintAt = now;
                return;
            }
        }

        void Seek()
        {
            Shape shape = Shapes.All[game.Tray[held]];
            ulong board = game.Board;
            float reach = SnapReach * side;
            float nearest = reach * reach;
            int found = -1;
            for (int place = 0; place < shape.Mask.Length; place++)
            {
                if ((board & shape.Mask[place]) != 0) continue;
                Vector2 gap = Spot(shape, place) - aim;
                float distance = gap.sqrMagnitude;
                if (place == snap) distance *= 0.6f;
                if (distance >= nearest) continue;
                nearest = distance;
                found = place;
            }
            if (found == snap) return;

            snap = found;
            ghost = doomed = 0;
            if (found < 0) return;
            ghost = shape.Mask[found];
            Lattice.Sweep(board | ghost, out doomed, out _);
            Haptics.Tick();
        }

        void Commit(int slot, int place)
        {
            Shape shape = Shapes.All[game.Tray[slot]];
            Vector2 target = Spot(shape, place);
            dropFrom = slotAt[slot] - target;
            bool canUndoMove = game.Rules.Undo != UndoPolicy.None;
            Move move = game.Place(slot, place);
            int earnedStars = goals.Record(move, canUndoMove);
            if (earnedStars > 0)
            {
                Log.Info($"Goal stars +{earnedStars}, {goals.TotalStars} in total");
                sfx.Record();
                Haptics.Click();
                for (int i = 0; i < 6; i++)
                    Scatter(new Vector2(0f, trayY + side * 1.45f), Theme.Gold);
            }
            dropMask = move.Mask & game.Board;
            dropAt = scoreHit = now;
            sfx.Place(shape.Size);
            if (!taught && game.Rules.Hints != HintPolicy.Optional)
            {
                taught = true;
                PlayerPrefs.SetInt(TaughtKey, 1);
            }

            if (move.Lines > 0)
            {
                for (ulong cells = move.Cleared; cells != 0; cells &= cells - 1)
                {
                    int cell = Bits.Lowest(cells);
                    Die(cell, move.Tint, now + 0.04f + (Center(cell) - target).magnitude / side * 0.03f);
                }
                for (uint lines = move.LineBits; lines != 0 && sweepCount < sweeps.Length; lines &= lines - 1)
                    sweeps[sweepCount++] = new Sweep { Line = Bits.Lowest(lines), Tint = move.Tint, At = now };
                shakeAt = streakHit = pipAt[slot] = now;
                shakeSize = side * Mathf.Min(0.02f + 0.035f * move.Lines, 0.14f);
                Pop(target, move.Points, 0.4f + 0.08f * Mathf.Min(move.Lines, 4), Theme.Top[move.Tint]);
                sfx.Clear(move.Combo, move.Lines);
                Haptics.Thump();
            }
            else
            {
                Haptics.Click();
                if (move.Broke) sfx.Break();
            }

            if (move.Perfect)
            {
                perfectAt = now;
                Pop(new Vector2(0f, trayY + side * 1.05f), Game.PerfectBonus, 0.5f, Theme.Gold);
                sfx.Perfect();
            }
            if (move.Dealt) Arrive(move.Perfect ? 0.55f : move.Lines > 0 ? 0.3f : 0.12f);
            if (game.Score > best) best = game.Score;
            if (!beaten && record > 0 && game.Score > record)
            {
                beaten = true;
                bestHit = now;
                sfx.Record();
                for (int i = 0; i < 5; i++) Scatter(new Vector2(0f, bestY + side * 0.15f), Theme.Gold);
            }
            Refresh();
            if (!move.Over) return;

            overAt = now + (move.Lines > 0 ? 0.5f : 0.3f);
            endLines = game.Lines;
            endCombo = game.MaxCombo;
            endRecord = game.Score > record;
            feted = false;
            Log.Info($"Game over {game.Mode}: score {game.Score}, lines {endLines}, max combo {endCombo}{(endRecord ? ", new record" : string.Empty)}");
            sfx.Over(overAt - now);
            Haptics.Thump();
            Store();
        }

        void Undo()
        {
            game.Undo();
            goals.Undo();
            Log.Info($"Undo {game.Mode}, score {game.Score}");
            best = Mathf.Max(record, game.Score);
            beaten = beaten && game.Score > record;
            // Game over or a pause may already have saved the move we just undid.
            string key = BestKey();
            if (PlayerPrefs.GetInt(key, 0) > best)
            {
                PlayerPrefs.SetInt(key, best);
                Log.Info($"Best lowered to {best} after undo");
            }
            endRecord = feted = false;
            veil = 0f;
            shardCount = 0;
            hintSlot = -1;
            idleAt = now;
            perfectAt = shakeAt = bestHit = streakHit = scoreHit = now - 10f;
            for (int i = 0; i < pipAt.Length; i++) pipAt[i] = now - 10f;
            sfx.StopVoices();
            held = snap = -1;
            ghost = doomed = dropMask = 0;
            dyingCount = sweepCount = popupCount = 0;
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                slotAt[slot] = Home(slot);
                slotSize[slot] = TraySize * 0.6f;
                slotFrom[slot] = now;
            }
            Refresh();
            sfx.Back();
            Haptics.Click();
            Busy(0.6f);
        }

        void Keep()
        {
            string key = BestKey();
            if (best > PlayerPrefs.GetInt(key, 0)) PlayerPrefs.SetInt(key, best);
        }

        void Store()
        {
            Keep();
            PlayerPrefs.SetString(GoalsKey, goals.Pack());
            string key = SavedKeyFor(game.Mode);
            if (game.Over) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, game.Pack());
            PlayerPrefs.Save();
            Log.Info(game.Over ? $"Cleared finished {game.Mode} save" : $"Saved {game.Mode} game, score {game.Score}");
        }

        void Open(Mode mode)
        {
            if (Load(mode) && !game.Over)
            {
                if (game.Mode != Mode.Daily || game.Seed == TodaySeed())
                {
                    Resume();
                    return;
                }
                Log.Info($"Daily save {game.Seed} expired, today is {TodaySeed()}");
            }
            Fresh(mode, SeedFor(mode));
        }

        bool Load(Mode mode)
        {
            string saved = PlayerPrefs.GetString(SavedKeyFor(mode), string.Empty);
            if (game.Unpack(saved)) return true;
            if (saved.Length > 0) Log.Warn($"Dropped unreadable {mode} save: {saved}");
            return false;
        }

        void Dissolve(float stagger)
        {
            for (ulong old = game.Board; old != 0; old &= old - 1)
            {
                int cell = Bits.Lowest(old);
                Die(cell, game.Tint[cell], now + Lattice.Y[cell] * stagger);
            }
        }

        void Die(int cell, int tint, float at)
        {
            if (dyingCount == dying.Length) return;
            dying[dyingCount++] = new Dying { Cell = cell, Tint = tint, Own = game.Tint[cell], At = at };
            Busy(at - now + FlashTime + ShrinkTime + 0.8f);
        }

        void Pop(Vector2 at, int value, float size, Color32 tint)
        {
            if (popupCount == popups.Length) popupCount--;
            popups[popupCount++] = new Popup { At = at, Value = value, Born = now, Size = size, Tint = tint };
            Busy(PopupTime + 0.1f);
        }

        void Scatter(Vector2 at, Color32 tint)
        {
            for (int i = 0; i < 2 && shardCount < shards.Length; i++)
            {
                float angle = chance.Unit() * Mathf.PI * 2f;
                float speed = side * (2.5f + chance.Unit() * 4f);
                shards[shardCount++] = new Shard
                {
                    At = at,
                    Speed = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + side * 2f),
                    Turn = angle,
                    Spin = (chance.Unit() - 0.5f) * 14f,
                    Born = now,
                    Life = 0.4f + chance.Unit() * 0.3f,
                    Size = side * (0.1f + chance.Unit() * 0.08f),
                    Tint = tint,
                    Up = chance.Range(2) == 0
                };
            }
        }

        void Step(float delta)
        {
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                if (game.Tray[slot] < 0 || now < slotFrom[slot]) continue;
                bool lifted = slot == held;
                float pull = 1f - Mathf.Exp(-delta * (lifted ? 32f : 18f));
                if (!lifted) slotAt[slot] += (Home(slot) - slotAt[slot]) * pull;
                slotSize[slot] += ((lifted ? 1f : TraySize) - slotSize[slot]) * pull;
            }

            float gravity = side * 16f * delta;
            for (int i = shardCount - 1; i >= 0; i--)
            {
                ref Shard shard = ref shards[i];
                if (now - shard.Born >= shard.Life)
                {
                    shards[i] = shards[--shardCount];
                    continue;
                }
                shard.Speed.y -= gravity;
                shard.At += shard.Speed * delta;
                shard.Turn += shard.Spin * delta;
            }

            float goal = game.Over && now >= overAt ? 1f : 0f;
            if (veil != goal)
            {
                veil = Mathf.MoveTowards(veil, goal, delta / (goal > veil ? OpenTime : CloseTime));
                Busy(0.1f);
            }
            if (endRecord && !feted && veil > 0.5f)
            {
                feted = true;
                sfx.Record();
                for (int i = 0; i < 8; i++) Scatter(new Vector2(0f, endY + side * 0.6f), Theme.Gold);
            }

            float lag = game.Score - shown;
            if (lag != 0f)
            {
                shown = Mathf.Abs(lag) < 0.5f ? game.Score : shown + lag * (1f - Mathf.Exp(-delta * 14f));
                Busy(0.1f);
            }
            if (shardCount > 0) Busy(0.1f);
            
            float menuGoal = menuOpen ? 1f : 0f;
            if (menuVeil != menuGoal)
            {
                menuVeil = Mathf.MoveTowards(menuVeil, menuGoal, delta / 0.22f);
                Busy(0.1f);
            }
        }

        void Draw()
        {
            batch.Begin();
            Vector2 jolt = Jolt();
            float sunk = game.Over ? Mathf.Clamp01((now - overAt) / 0.4f) : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 13f);
            ulong board = game.Board;
            int lifted = held >= 0 ? game.TrayTint[held] : 0;
            if (now - dropAt > PopTime) dropMask = 0;

            Color32 heat = Heat();
            float warm = game.Combo > 1 ? 0.14f * Mathf.Clamp01((game.Combo - 1) / 8f) : 0f;
            Color32 cellTop = Theme.Mix(Theme.CellTop, heat, warm), cellLow = Theme.Mix(Theme.CellLow, heat, warm * 0.6f);

            for (int cell = 0; cell < Lattice.Cells; cell++)
            {
                ulong bit = 1UL << cell;
                bool up = Lattice.IsUp(Lattice.X[cell], Lattice.Y[cell]);
                Vector2 at = Center(cell) + jolt;
                batch.Tri(at.x, at.y, cellHalf, up, 0f, cellTop, cellLow, Corner);

                if ((doomed & bit) != 0)
                {
                    float glow = 0.18f + 0.22f * pulse;
                    batch.Tri(at.x, at.y, cellHalf * (1f + 0.05f * pulse), up, 0f,
                        Theme.Mix(Theme.Top[lifted], Theme.White, glow), Theme.Mix(Theme.Low[lifted], Theme.White, glow), Corner);
                }
                else if ((board & bit) != 0)
                {
                    int tint = game.Tint[cell];
                    Color32 top = Theme.Top[tint], low = Theme.Low[tint];
                    float size = 1f;
                    if ((dropMask & bit) != 0)
                    {
                        float slide = Mathf.Clamp01((now - dropAt) / DropTime);
                        float pop = Mathf.Clamp01((now - dropAt) / PopTime);
                        at += dropFrom * ((1f - slide) * (1f - slide));
                        size += 0.2f * Mathf.Sin(pop * Mathf.PI) * (1f - pop);
                        top = Theme.Mix(top, Theme.White, 0.5f * (1f - pop));
                    }
                    if (sunk > 0f)
                    {
                        float ash = 0.82f * Mathf.Clamp01((now - overAt) / 0.3f - Lattice.Y[cell] * 0.12f);
                        top = Theme.Mix(top, Theme.Faint, ash);
                        low = Theme.Mix(low, Theme.Faint, ash);
                    }
                    batch.Tri(at.x, at.y, cellHalf * size, up, 0f, top, low, Corner);
                }
                else if ((ghost & bit) != 0)
                {
                    batch.Tri(at.x, at.y, cellHalf, up, 0f, Theme.Fade(Theme.Top[lifted], 0.45f), Theme.Fade(Theme.Low[lifted], 0.45f), Corner);
                }
            }

            for (int i = dyingCount - 1; i >= 0; i--)
            {
                ref Dying dead = ref dying[i];
                float age = now - dead.At;
                if (age >= FlashTime + ShrinkTime)
                {
                    dying[i] = dying[--dyingCount];
                    continue;
                }
                int cell = dead.Cell;
                bool up = Lattice.IsUp(Lattice.X[cell], Lattice.Y[cell]);
                Vector2 at = Center(cell) + jolt;
                Color32 flash = Theme.Mix(Theme.Top[dead.Tint], Theme.White, 0.55f);
                if (age < 0f)
                {
                    int tint = dead.Own;
                    batch.Tri(at.x, at.y, cellHalf, up, 0f, Theme.Top[tint], Theme.Low[tint], Corner);
                }
                else if (age < FlashTime)
                {
                    float lit = age / FlashTime;
                    int tint = dead.Own;
                    batch.Tri(at.x, at.y, cellHalf * (1f + 0.12f * lit), up, 0f, Theme.Mix(Theme.Top[tint], flash, lit), Theme.Mix(Theme.Low[tint], flash, lit), Corner);
                }
                else
                {
                    if (!dead.Burst)
                    {
                        dead.Burst = true;
                        Scatter(at, Theme.Mix(Theme.Top[dead.Tint], Theme.White, 0.35f));
                    }
                    float gone = (age - FlashTime) / ShrinkTime;
                    batch.Tri(at.x, at.y, cellHalf * 1.12f * (1f - gone * gone), up, gone * 1.2f, flash, flash, Corner);
                }
            }

            for (int i = sweepCount - 1; i >= 0; i--)
            {
                ref Sweep sweep = ref sweeps[i];
                float age = (now - sweep.At) / SweepTime;
                if (age >= 1f)
                {
                    sweeps[i] = sweeps[--sweepCount];
                    continue;
                }
                Vector2 from = Center(Lattice.LineStart[sweep.Line]) + jolt, to = Center(Lattice.LineEnd[sweep.Line]) + jolt;
                float fade = (1f - age) * (1f - age);
                batch.Line(from.x, from.y, to.x, to.y, rise * (0.2f + 0.34f * fade), Theme.Fade(Theme.Mix(Theme.Top[sweep.Tint], Theme.White, 0.7f), 0.6f * fade), 0.5f);
            }

            bool alive = game.TrioAlive;
            float burst = Mathf.Clamp01((now - perfectAt) / 0.6f);
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                Vector2 home = Home(slot);
                bool lit = game.Placed[slot] && game.Scored[slot] && alive;
                float lighting = Mathf.Clamp01((now - pipAt[slot]) / 0.3f);
                float size = side * 0.11f * (1f + (lit ? 0.9f * Mathf.Sin(lighting * Mathf.PI) * (1f - lighting) : 0f));
                Color32 tint = lit ? Theme.Gold : alive ? Theme.Dim : Theme.Faint;
                if (burst < 1f)
                {
                    size *= 1f + 1.6f * Mathf.Sin(burst * Mathf.PI);
                    tint = Theme.Mix(Theme.Gold, tint, burst);
                }
                batch.Tri(home.x, pipY, size, true, 0f, tint, tint, Corner);

                int id = game.Tray[slot];
                if (id < 0 || slot == held || now < slotFrom[slot]) continue;
                int shade = game.TrayTint[slot];
                Color32 top = Theme.Top[shade], low = Theme.Low[shade];
                if (!slotOpen[slot] || sunk > 0f)
                {
                    top = Theme.Mix(top, Theme.Back, 0.72f);
                    low = Theme.Mix(low, Theme.Back, 0.72f);
                }
                Piece(Shapes.All[id], slotAt[slot], slotSize[slot], top, low);
            }

            float open = Mathf.SmoothStep(0f, 1f, veil);
            Streak(heat);
            if (veil > 0f) batch.Line(0f, -high, 0f, high, Wide, Theme.Fade(Theme.Back, 0.9f * open));
            Score(open);
            if (veil > 0f) Finish();
            Icons();
            GoalBar();
            if (hintSlot >= 0) Hint();
            if (menuVeil > 0f) DrawMenu();

            if (held >= 0)
                Piece(Shapes.All[game.Tray[held]], slotAt[held], slotSize[held], Theme.Top[lifted], Theme.Low[lifted]);

            for (int i = 0; i < shardCount; i++)
            {
                ref Shard shard = ref shards[i];
                float left = 1f - (now - shard.Born) / shard.Life;
                batch.Tri(shard.At.x, shard.At.y, shard.Size * left, shard.Up, shard.Turn, shard.Tint, shard.Tint, Corner);
            }

            for (int i = popupCount - 1; i >= 0; i--)
            {
                ref Popup popup = ref popups[i];
                float age = (now - popup.Born) / PopupTime;
                if (age >= 1f)
                {
                    popups[i] = popups[--popupCount];
                    continue;
                }
                float grow = age < 0.18f ? Spring(age / 0.18f) : 1f;
                float fade = age > 0.7f ? 1f - (age - 0.7f) / 0.3f : 1f;
                float climb = 1f - (1f - age) * (1f - age);
                int length = Glyphs.Write(text, popup.Value, Glyphs.Plus);
                float raised = popup.At.y + side * (0.2f + 0.9f * climb), scale = popup.Size * side * grow;
                Glyphs.Draw(batch, text, length, popup.At.x, raised, scale, 0.5f, Theme.Fade(Theme.Back, 0.8f * fade), 0.42f);
                Glyphs.Draw(batch, text, length, popup.At.x, raised, scale, 0.5f, Theme.Fade(popup.Tint, fade), 0.17f);
            }

            batch.End();
        }

        void Streak(Color32 heat)
        {
            if (game.Combo < 2) return;
            float bump = Mathf.Clamp01((now - streakHit) / 0.3f);
            float size = side * 0.62f * (1f + 0.35f * Mathf.Sin(bump * Mathf.PI) * (1f - bump));
            int length = Glyphs.Write(text, game.Combo, Glyphs.Times);
            Glyphs.Draw(batch, text, length, streakAt.x, streakAt.y, size, 0.5f, heat, 0.16f);
            if (!game.Rules.ComboExpires) return;
            for (int i = 0; i < Game.Grace; i++)
                batch.Dot(streakAt.x + (i - 1) * side * 0.26f, streakAt.y - side * 0.34f, side * 0.055f, i < Game.Grace - game.Misses ? heat : Theme.Faint);
        }

        void Score(float open)
        {
            float hit = Mathf.Clamp01((now - scoreHit) / 0.25f);
            float punch = 1f + 0.1f * Mathf.Sin(hit * Mathf.PI) * (1f - hit);
            int value = Mathf.RoundToInt(shown);
            Color32 ink = endRecord ? Theme.Mix(Theme.Ink, Theme.Gold, open) : Theme.Ink;
            int length = Glyphs.Write(text, value);
            float size = side * Mathf.Lerp(1f, EndScale, open) * punch;
            float span = Glyphs.Width(text, length, size);
            float titleY = Mathf.Lerp(scoreY + 1.42f * side, endY + 1.85f * side, open);
            float titleH = side * Mathf.Lerp(0.22f, 0.26f, open);
            Color32 titleColor = game.Mode == Mode.Daily ? Theme.Gold : Theme.Dim;
            string modeLabel = game.Mode switch
            {
                Mode.Flow => "FLOW",
                Mode.Daily => "DAILY",
                _ => "ARCADE"
            };
            DrawWord(modeLabel, new Vector2(0f, titleY), titleH, titleColor);

            if (span > Wide * WidestScore) size *= Wide * WidestScore / span;
            Glyphs.Draw(batch, text, length, 0f, Mathf.Lerp(scoreY, endY, open), size, 0.5f, ink, 0.13f);

            float cheer = Mathf.Clamp01((now - bestHit) / 0.5f);
            Color32 peak = game.Score > record || (veil > 0f && endRecord) ? Theme.Gold : Theme.Dim;
            float small = side * Mathf.Lerp(0.3f, 0.36f, open) * (1f + 0.5f * Mathf.Sin(cheer * Mathf.PI) * (1f - cheer));
            float line = Mathf.Lerp(bestY, endY - 0.8f * side, open);
            length = Glyphs.Write(text, Mathf.Max(best, value));
            float width = Glyphs.Width(text, length, small);
            float mark = small * 0.42f;
            float start = -(width + mark * 3.2f) * 0.5f;
            batch.Tri(start + mark, line + small * 0.42f, mark, true, 0f, peak, peak, Corner);
            Glyphs.Draw(batch, text, length, start + mark * 3.2f, line, small, 0f, peak, 0.17f);
        }

        void Finish()
        {
            float show = Mathf.Clamp01((veil - 0.45f) / 0.4f);
            if (show > 0f)
            {
                float line = endY - 2.15f * side, tall = side * 0.42f * Spring(show);
                Color32 ink = Theme.Fade(Theme.Ink, 0.85f * show);
                int length = Glyphs.Write(text, endLines);
                float strip = tall * 1.5f, gap = tall * 0.5f;
                float left = -1.6f * side - (strip + gap + Glyphs.Width(text, length, tall)) * 0.5f;
                float quarter = strip * 0.25f, floor = line + tall * 0.5f - quarter * 0.866f;
                for (int i = 0; i < 3; i++)
                    batch.Tri(left + quarter * (i + 1), floor + quarter * (i == 1 ? 1.155f : 0.577f), quarter * 0.86f, i != 1, 0f, ink, ink, Corner);
                Glyphs.Draw(batch, text, length, left + strip + gap, line, tall, 0f, ink, 0.16f);

                length = Glyphs.Write(text, endCombo, Glyphs.Times);
                Glyphs.Draw(batch, text, length, 1.6f * side, line, tall, 0.5f, Theme.Fade(Theme.Gold, show), 0.16f);
            }

            float ready = Mathf.Clamp01((veil - 0.6f) / 0.4f);
            if (ready <= 0f) return;

            if (game.Mode == Mode.Daily)
            {
                bool copied = now - copiedAt < 2f;
                float cx = shareAt.x, cy = shareAt.y;
                float s = side * 0.28f * Spring(ready);
                if (copied)
                {
                    float age = Mathf.Clamp01((now - copiedAt) / 0.25f);
                    float cs = s * (1f + 0.22f * Mathf.Sin(age * Mathf.PI) * (1f - age));
                    float lineR = cs * 0.18f;
                    Color32 gold = Theme.Gold;
                    float x0 = cx - 0.75f * cs, y0 = cy - 0.05f * cs;
                    float x1 = cx - 0.15f * cs, y1 = cy - 0.65f * cs;
                    float x2 = cx + 0.85f * cs, y2 = cy + 0.65f * cs;
                    batch.Line(x0, y0, x1, y1, lineR, gold);
                    batch.Line(x1, y1, x2, y2, lineR, gold);
                }
                else
                {
                    float lx = cx - 0.8f * s, rx = cx + 0.75f * s, dy = 0.78f * s;
                    float lineR = s * 0.12f, dotR = s * 0.30f;
                    Color32 ink = Theme.Ink;
                    batch.Line(lx, cy, rx, cy + dy, lineR, ink);
                    batch.Line(lx, cy, rx, cy - dy, lineR, ink);
                    batch.Dot(lx, cy, dotR, ink);
                    batch.Dot(rx, cy + dy, dotR, ink);
                    batch.Dot(rx, cy - dy, dotR, ink);
                }
            }

            float beat = 1f + 0.05f * Mathf.Sin(now * 3.2f);
            batch.Tri(0f, endY - 4.4f * side, side * 0.8f * beat * Spring(ready), true, -Mathf.PI * 0.5f, Theme.Ink, Theme.Ink, 0.22f);
        }

        void Icons()
        {
            if (game.CanUndo)
            {
                Color32 back = game.Over ? Theme.Ink : Theme.Dim;
                batch.Tri(undoAt.x, undoAt.y, side * 0.19f, true, Mathf.PI * 0.5f, back, back, Corner);
            }

            for (int i = 0; i < 3; i++)
            {
                float tall = sfx.Muted ? 0f : side * (i == 1 ? 0.17f : i == 0 ? 0.09f : 0.13f);
                float x = soundAt.x + (i - 1) * side * 0.17f;
                batch.Line(x, soundAt.y - tall, x, soundAt.y + tall, side * 0.045f, sfx.Muted ? Theme.Faint : Theme.Dim);
            }

            if (game.Rules.Hints == HintPolicy.Optional)
            {
                // A light bulb drawn with the same rounded primitives as the sound icon.
                Color32 ink = flowHints ? Theme.Gold : Theme.Dim;
                float x = hintButtonAt.x, y = hintButtonAt.y;
                batch.Dot(x, y + side * 0.06f, side * 0.17f, ink);
                batch.Dot(x, y + side * 0.06f, side * 0.105f, Theme.Back);
                batch.Line(x - side * 0.075f, y - side * 0.15f, x + side * 0.075f, y - side * 0.15f, side * 0.035f, ink);
            }
            DrawModeIcon(game.Mode, modeButtonAt, side * 0.22f, game.Mode == Mode.Daily ? Theme.Gold : Theme.Dim);
            // DrawWord("DAILY", dailyAt, side * 0.3f, game.Mode == Mode.Daily ? Theme.Gold : Theme.Dim);
        }

        void Hint()
        {
            Shape shape = Shapes.All[game.Tray[hintSlot]];
            int tint = game.TrayTint[hintSlot];
            float beat = (now - hintAt) % HintLoop / HintLoop;
            float fade = Mathf.Clamp01(beat / 0.1f) * Mathf.Clamp01((1f - beat) / 0.15f);
            float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beat - 0.12f) / 0.5f));
            Vector2 at = Vector2.Lerp(Home(hintSlot), Spot(shape, hintPlace), travel);

            for (ulong cells = shape.Mask[hintPlace]; cells != 0; cells &= cells - 1)
            {
                int cell = Bits.Lowest(cells);
                Vector2 center = Center(cell);
                Color32 trace = Theme.Fade(Theme.Top[tint], 0.32f * fade * travel);
                batch.Tri(center.x, center.y, cellHalf, Lattice.IsUp(Lattice.X[cell], Lattice.Y[cell]), 0f, trace, trace, Corner);
            }
            Piece(shape, at, Mathf.Lerp(TraySize, 1f, travel), Theme.Fade(Theme.Top[tint], 0.6f * fade), Theme.Fade(Theme.Low[tint], 0.6f * fade));
            batch.Dot(at.x, at.y, side * 0.34f, Theme.Fade(Theme.Ink, 0.2f * fade), 0.7f);
            batch.Dot(at.x, at.y, side * 0.15f, Theme.Fade(Theme.Ink, 0.92f * fade));
        }

        void Piece(Shape shape, Vector2 at, float size, Color32 top, Color32 low)
        {
            float half = side * 0.5f * size, row = rise * size;
            for (int i = 0; i < shape.Size; i++)
            {
                int x = shape.X[i], y = shape.Y[i];
                bool up = Lattice.IsUp(x, y);
                batch.Tri(at.x + (x - shape.CenterX) * half, at.y - (y + (up ? 2f / 3f : 1f / 3f) - shape.CenterY) * row, cellHalf * size, up, 0f, top, low, Corner);
            }
        }

        Color32 Heat() => Theme.Mix(Theme.Gold, Theme.Top[0], Mathf.Clamp01((game.Combo - 2) / 7f));

        Vector2 Jolt()
        {
            float age = (now - shakeAt) / ShakeTime;
            if (age >= 1f) return Vector2.zero;
            float fall = (1f - age) * (1f - age) * shakeSize;
            return new Vector2(Mathf.Sin(now * 71f) * fall, Mathf.Cos(now * 53f) * fall);
        }

        Vector2 Home(int slot) => new Vector2((slot - 1) * slotGap, trayY);

        Vector2 Center(int cell) => new Vector2(Lattice.CenterX(cell) * side, apexY - Lattice.CenterY(cell) * side);

        Vector2 Spot(Shape shape, int place) => new Vector2(
            (shape.OriginX[place] + shape.CenterX) * side * 0.5f,
            apexY - (shape.OriginY[place] + shape.CenterY) * rise);

        bool Near(Vector2 at, Vector2 target) => (at - target).sqrMagnitude < side * side * 0.5f;

        bool NearBox(Vector2 at, Vector2 target, float halfW, float halfH) =>
            Mathf.Abs(at.x - target.x) <= halfW && Mathf.Abs(at.y - target.y) <= halfH;

        static ulong TodaySeed()
        {
            DateTime today = DateTime.Now;
            return (ulong)(today.Year * 10000 + today.Month * 100 + today.Day);
        }

        static string FormatSeedDate(ulong seed)
        {
            if (seed == 0) seed = TodaySeed();
            int year = (int)(seed / 10000);
            int month = (int)((seed / 100) % 100);
            int day = (int)(seed % 100);
            return $"{year:0000}-{month:00}-{day:00}";
        }

        string BestKey() => game.Mode switch
        {
            Mode.Flow => FlowBestKey,
            Mode.Daily => DailyBestPrefix + (game.Seed != 0 ? game.Seed : TodaySeed()),
            _ => ArcadeBestKey
        };

        static void ShareText(string shareText)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                string actionSend = intentClass.GetStatic<string>("ACTION_SEND");
                string extraText = intentClass.GetStatic<string>("EXTRA_TEXT");
                using var intent = new AndroidJavaObject("android.content.Intent");
                using (intent.Call<AndroidJavaObject>("setAction", actionSend)) { }
                using (intent.Call<AndroidJavaObject>("setType", "text/plain")) { }
                using (intent.Call<AndroidJavaObject>("putExtra", extraText, shareText)) { }
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Pointless Daily");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("startActivity", chooser);
                return;
            }
            catch (Exception ex)
            {
                Log.Warn($"Android share failed, copying instead: {ex}");
            }
#endif
            GUIUtility.systemCopyBuffer = shareText;
        }

        // Delete DrawWord once DAILY in Icons() is replaced by the mode selector.
        void DrawWord(string word, Vector2 center, float height, Color32 color)
        {
            float w = height * 0.6f, gap = height * 0.28f, t = height * 0.16f;
            float total = word.Length * w + (word.Length - 1) * gap;
            float ox = center.x - total * 0.5f, oy = center.y - height * 0.5f;

            for (int i = 0; i < word.Length; i++)
            {
                float x = ox + i * (w + gap);
                void Seg(float x0, float y0, float x1, float y1) =>
                    batch.Line(x + x0 * w, oy + y0 * height, x + x1 * w, oy + y1 * height, t, color);

                switch (word[i])
                {
                    case 'A': Seg(0, 0, 0.5f, 1); Seg(0.5f, 1, 1, 0); Seg(0.22f, 0.38f, 0.78f, 0.38f); break;
                    case 'C': Seg(1, 0.82f, 0.65f, 1); Seg(0.65f, 1, 0.35f, 1); Seg(0.35f, 1, 0, 0.65f); Seg(0, 0.65f, 0, 0.35f); Seg(0, 0.35f, 0.35f, 0); Seg(0.35f, 0, 0.65f, 0); Seg(0.65f, 0, 1, 0.18f); break;
                    case 'D': Seg(0, 0, 0, 1); Seg(0, 1, 0.65f, 1); Seg(0.65f, 1, 1, 0.65f); Seg(1, 0.65f, 1, 0.35f); Seg(1, 0.35f, 0.65f, 0); Seg(0.65f, 0, 0, 0); break;
                    case 'E': Seg(0, 0, 0, 1); Seg(0, 1, 0.9f, 1); Seg(0, 0.5f, 0.75f, 0.5f); Seg(0, 0, 0.9f, 0); break;
                    case 'F': Seg(0, 0, 0, 1); Seg(0, 1, 0.9f, 1); Seg(0, 0.5f, 0.75f, 0.5f); break;
                    case 'I': Seg(0.5f, 0, 0.5f, 1); Seg(0.2f, 1, 0.8f, 1); Seg(0.2f, 0, 0.8f, 0); break;
                    case 'L': Seg(0, 1, 0, 0); Seg(0, 0, 0.9f, 0); break;
                    case 'O': Seg(0.35f, 1, 0.65f, 1); Seg(0.65f, 1, 1, 0.65f); Seg(1, 0.65f, 1, 0.35f); Seg(1, 0.35f, 0.65f, 0); Seg(0.65f, 0, 0.35f, 0); Seg(0.35f, 0, 0, 0.35f); Seg(0, 0.35f, 0, 0.65f); Seg(0, 0.65f, 0.35f, 1); break;
                    case 'R': Seg(0, 0, 0, 1); Seg(0, 1, 0.7f, 1); Seg(0.7f, 1, 1, 0.8f); Seg(1, 0.8f, 1, 0.65f); Seg(1, 0.65f, 0.7f, 0.5f); Seg(0, 0.5f, 0.7f, 0.5f); Seg(0.55f, 0.5f, 1, 0); break;
                    case 'W': Seg(0, 1, 0.25f, 0); Seg(0.25f, 0, 0.5f, 0.55f); Seg(0.5f, 0.55f, 0.75f, 0); Seg(0.75f, 0, 1, 1); break;
                    case 'Y': Seg(0, 1, 0.5f, 0.5f); Seg(1, 1, 0.5f, 0.5f); Seg(0.5f, 0.5f, 0.5f, 0); break;
                }
            }
        }

        void Busy(float seconds)
        {
            if (now + seconds > busyUntil) busyUntil = now + seconds;
        }

        static float Spring(float t)
        {
            float back = t - 1f;
            return 1f + back * back * (2.7f * back + 1.7f);
        }

        public void SwitchMode(Mode target)
        {
            if (target == game.Mode) return;
            Log.Info($"Switch {game.Mode} to {target}");
            Store();
            PlayerPrefs.SetInt(LastModeKey, (int)target);
            PlayerPrefs.Save();
            Dissolve(0.02f);
            Open(target);
        }

        static string SavedKeyFor(Mode mode) => mode switch
        {
            Mode.Flow => SavedFlowKey,
            Mode.Daily => SavedDailyKey,
            _ => SavedArcadeKey
        };

        void GoalBar()
        {
            if (goals.Active == GoalKind.None) return;
            float y = trayY + side * 1.42f;
            float halfW = slotGap * 1.15f;
            float r = side * 0.042f;

            // Pás postupu nad ponukou
            batch.Line(-halfW, y, halfW, y, r, Theme.Faint);
            if (goals.Progress > 0f)
                batch.Line(-halfW, y, Mathf.Lerp(-halfW, halfW, goals.Progress), y, r, Theme.Gold);

            // Hviezda a počet hviezd vľavo od pásu
            float sx = -halfW - side * 0.42f;
            DrawStar(sx, y, side * 0.13f, Theme.Gold);
            int len = Glyphs.Write(text, goals.Stars);
            Glyphs.Draw(batch, text, len, sx - side * 0.24f, y - side * 0.11f, side * 0.22f, 1f, Theme.Gold, 0.16f);
        }

        void DrawMenu()
        {
            float open = Mathf.SmoothStep(0f, 1f, menuVeil);
            float scale = Spring(open);
            batch.Line(0f, -high, 0f, high, Wide, Theme.Fade(Theme.Back, 0.94f * open));

            // 1. Riadok: 3 režimy (Pohár, Oblak, Kalendár)
            for (int i = 0; i < 3; i++)
            {
                bool active = (int)game.Mode == i;
                Color32 tint = Theme.Fade(active ? Theme.Gold : Theme.Dim, open);
                DrawModeIcon((Mode)i, menuModeAt[i], side * 0.34f * scale, tint);
                if (active) batch.Dot(menuModeAt[i].x, menuModeAt[i].y - side * 0.55f, side * 0.055f * scale, tint);
            }

            // 2. Riadok: 3 témy (Aurora, Sunset, Ocean)
            for (int i = 0; i < 3; i++)
            {
                bool active = (int)Theme.Current == i;
                Vector2 at = menuThemeAt[i];
                float r = side * 0.36f * scale;
                if (active) batch.Dot(at.x, at.y, r * 1.22f, Theme.Fade(Theme.Gold, open));
                batch.Dot(at.x, at.y, r, Theme.Fade(previewBack[i], open));
                batch.Tri(at.x, at.y, r * 0.58f, true, 0f, Theme.Fade(previewMain[i], open), Theme.Fade(previewMain[i], open), Corner);
            }

            // 3. Riadok: 4 ciele (Žiadny, Clear Lines, Big Clears, Combo Skill) + hviezdy
            for (int i = 0; i < 4; i++)
            {
                GoalKind kind = (GoalKind)i;
                bool active = goals.Active == kind;
                Vector2 at = menuGoalAt[i];
                Color32 tint = Theme.Fade(active ? Theme.Gold : Theme.Dim, open);
                DrawGoalIcon(kind, at, side * 0.28f * scale, tint);

                if (kind != GoalKind.None)
                {
                    float sy = at.y - side * 0.52f;
                    DrawStar(at.x - side * 0.14f, sy, side * 0.09f * scale, Theme.Fade(Theme.Gold, open));
                    int len = Glyphs.Write(text, goals.GetStars(kind));
                    Glyphs.Draw(batch, text, len, at.x + side * 0.04f, sy - side * 0.09f, side * 0.19f * scale, 0f, Theme.Fade(Theme.Ink, open), 0.16f);
                }
            }
        }

        void DrawModeIcon(Mode mode, Vector2 at, float s, Color32 tint)
        {
            float r = s * 0.16f;
            switch (mode)
            {
                case Mode.Arcade: // Pohár
                    batch.Tri(at.x, at.y + s * 0.25f, s * 0.65f, false, 0f, tint, tint, Corner);
                    batch.Line(at.x, at.y - s * 0.15f, at.x, at.y - s * 0.65f, r, tint);
                    batch.Line(at.x - s * 0.45f, at.y - s * 0.65f, at.x + s * 0.45f, at.y - s * 0.65f, r, tint);
                    break;
                case Mode.Flow: // Oblak
                    batch.Dot(at.x - s * 0.42f, at.y - s * 0.15f, s * 0.34f, tint);
                    batch.Dot(at.x + s * 0.42f, at.y - s * 0.15f, s * 0.34f, tint);
                    batch.Dot(at.x, at.y + s * 0.12f, s * 0.46f, tint);
                    batch.Line(at.x - s * 0.42f, at.y - s * 0.33f, at.x + s * 0.42f, at.y - s * 0.33f, s * 0.16f, tint);
                    break;
                case Mode.Daily: // Kalendár
                    float w = s * 0.65f, h = s * 0.58f;
                    batch.Line(at.x - w, at.y + h, at.x + w, at.y + h, r * 1.4f, tint);
                    batch.Line(at.x - w, at.y - h, at.x + w, at.y - h, r, tint);
                    batch.Line(at.x - w, at.y - h, at.x - w, at.y + h, r, tint);
                    batch.Line(at.x + w, at.y - h, at.x + w, at.y + h, r, tint);
                    batch.Line(at.x - w * 0.5f, at.y + h, at.x - w * 0.5f, at.y + h * 1.38f, r, tint);
                    batch.Line(at.x + w * 0.5f, at.y + h, at.x + w * 0.5f, at.y + h * 1.38f, r, tint);
                    batch.Dot(at.x, at.y - h * 0.15f, r * 1.3f, tint);
                    break;
            }
        }

        void DrawGoalIcon(GoalKind kind, Vector2 at, float s, Color32 tint)
        {
            float r = s * 0.16f;
            switch (kind)
            {
                case GoalKind.None: // Prečiarknutý kruh
                    batch.Dot(at.x, at.y, s * 0.72f, tint);
                    batch.Dot(at.x, at.y, s * 0.52f, Theme.Back);
                    batch.Line(at.x - s * 0.5f, at.y - s * 0.5f, at.x + s * 0.5f, at.y + s * 0.5f, r, tint);
                    break;
                case GoalKind.ClearLines: // 3 vodorovné línie
                    for (int row = -1; row <= 1; row++)
                        batch.Line(at.x - s * 0.65f, at.y + row * s * 0.45f, at.x + s * 0.65f, at.y + row * s * 0.45f, r, tint);
                    break;
                case GoalKind.BigClears: // 2 prekrížené línie (multi-clear)
                    batch.Line(at.x - s * 0.65f, at.y - s * 0.38f, at.x + s * 0.65f, at.y + s * 0.38f, r * 1.2f, tint);
                    batch.Line(at.x - s * 0.65f, at.y + s * 0.38f, at.x + s * 0.65f, at.y - s * 0.38f, r * 1.2f, tint);
                    break;
                case GoalKind.ComboSkill: // Dvojitý blesk / kombo šípky
                    batch.Tri(at.x, at.y + s * 0.25f, s * 0.52f, true, 0f, tint, tint, Corner);
                    batch.Tri(at.x, at.y - s * 0.25f, s * 0.52f, true, 0f, tint, tint, Corner);
                    break;
            }
        }

        void DrawStar(float x, float y, float radius, Color32 tint)
        {
            batch.Tri(x, y + radius * 0.15f, radius, true, 0f, tint, tint, 0.1f);
            batch.Tri(x, y - radius * 0.15f, radius, false, 0f, tint, tint, 0.1f);
        }
    }
}