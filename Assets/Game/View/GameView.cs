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
        const float WidestPlay = 0.62f;
        const float HintWait = 1.6f;
        const float HintLoop = 2.2f;
        const float ChipSize = 0.44f;
        const float IconSize = 0.23f;
        const float ButtonReach = 0.55f;
        const float TapTime = 0.25f;
        const float ToastTime = 1.1f;
        const float HelpStep = 0.82f;
        const float GoalRow = 1.42f;
        const float GoalHalf = 2.2f;
        const float GoalLabel = 2.52f;
        const float ModeLabel = 1.78f;
        const float ModeName = 1.2f;
        const float GoalLabelDrop = 1.5f;
        const float GoalName = 0.98f;
        const float NameSize = 0.27f;
        const float GoalNameSize = 0.23f;
        const float CaptionSize = 0.21f;

        static readonly string[] modeNames = { "Arcade", "Flow", "Denná" };
        static readonly string[] goalNames = { "Bez cieľa", "Línie", "Viac naraz", "Kombo" };

        enum Sign { Trophy, Cloud, Calendar, Rise, Undo, NoUndo, Combo, Bulb, Share, Piece, Strip, Stuck, Trio, Directions }

        static readonly Sign[][] helpSigns =
        {
            new[] { Sign.Trophy, Sign.Rise, Sign.Undo, Sign.Combo },
            new[] { Sign.Cloud, Sign.Undo, Sign.Combo, Sign.Bulb },
            new[] { Sign.Calendar, Sign.Piece, Sign.Trio, Sign.NoUndo }
        };

        static readonly string[][] helpRules =
        {
            new[] { "Klasika: hraj na najvyššie skóre", "Vyššie skóre, ťažšie dieliky", "Jeden krok späť na každú trojicu", "Kombo vydrží 3 ťahy bez línie" },
            new[] { "Pokojná hra s ľahšími dielikmi", "Krok späť kedykoľvek, aj viackrát", "Kombo nikdy nevyprší", "Žiarovka ukáže, kam dať dielik" },
            new[] { "Každý deň nová výzva, jeden pokus", "Dnes majú všetci rovnaké dieliky", "Cieľ dňa: čo najviac Perfect Trio", "Bez kroku späť a bez nápovedy" }
        };

        static readonly Sign[] basicSigns = { Sign.Piece, Sign.Strip, Sign.Directions, Sign.Trio, Sign.Stuck };
        static readonly string[] basicRules =
        {
            "Ťahaj dieliky na dosku",
            "Plná línia zmizne a dá body",
            "Línie vo všetkých troch smeroch naraz: +300",
            "Perfect Trio: všetky 3 dieliky trojice čistia, +200",
            "Koniec, keď sa nič nezmestí"
        };
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
        const string HelpSeenKey = "help_seen";
        const string DailyFinalKey = "daily.final";

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
            public bool Gold;
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
        bool menuOpen, helpOpen;
        float menuVeil, helpVeil;
        int helpSeen;
        Vector2 modeButtonAt;
        readonly Vector2[] menuModeAt = new Vector2[3];
        readonly Vector2[] menuThemeAt = new Vector2[3];
        readonly Vector2[] menuGoalAt = new Vector2[4];
        readonly int[] menuBest = new int[3];

        Batch batch;
        Labels labels;
        Sfx sfx;
        Rng chance;
        int pixelsWide, pixelsHigh;
        Rect safe;
        float high, side, rise, cellHalf, apexY, trayY, slotGap, scoreY, bestY, pipY, endY, lift, veil;
        int endLines, endCombo;
        bool endRecord, feted;
        Vector2 undoAt, soundAt, helpAt, hintButtonAt, streakAt, shareAt, tapped;
        int held = -1, snap = -1, hintSlot = -1, hintPlace;
        Vector2 aim, dropFrom;
        ulong ghost, doomed, dropMask;
        int dyingCount, sweepCount, shardCount, popupCount;
        float now, busyUntil, shown, idleAt, hintAt, dropAt, scoreHit, streakHit, bestHit, shakeAt, shakeSize, overAt, perfectAt, copiedAt, tappedAt, deniedAt, starAt, modeShownAt;
        int best, record, nextMinutes = -1;
        string nextLabel = string.Empty;
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
            labels = new Labels(surface.transform, 24, surface.GetComponent<Renderer>().sortingOrder + 1);
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
            helpSeen = PlayerPrefs.GetInt(HelpSeenKey, 0);
            taught = PlayerPrefs.GetInt(TaughtKey, 0) == 1;
            Log.Info($"Start {Application.version} on {SystemInfo.deviceModel}, {Application.targetFrameRate} fps, theme {Theme.Current}, sound {(sfx.Muted ? "off" : "on")}, goal {goals.Active}");
            chance = new Rng((ulong)DateTime.UtcNow.Ticks);
            now = Time.unscaledTime;
            copiedAt = scoreHit = streakHit = bestHit = shakeAt = perfectAt = dropAt = tappedAt = deniedAt = starAt = modeShownAt = now - 10f;
            pixelsWide = pixelsHigh = 0;
            Fit();
            Open((Mode)Mathf.Clamp(PlayerPrefs.GetInt(LastModeKey, 0), 0, 2));
        }

        void OnDisable()
        {
            Store();
            sfx?.Dispose();
            batch?.Dispose();
            labels?.Dispose();
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

            bool busy = held >= 0 || now < busyUntil || game.Over || menuOpen || menuVeil > 0f || helpOpen || helpVeil > 0f;
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
            if (helpOpen)
            {
                helpOpen = false;
                sfx.Back();
                return;
            }
            if (menuOpen)
            {
                PressMenu(at);
                return;
            }
            if (Hit(at, modeButtonAt))
            {
                OpenMenu();
                return;
            }
            if (Hit(at, helpAt))
            {
                OpenHelp();
                return;
            }
            if (game.Rules.Hints == HintPolicy.Optional && Hit(at, hintButtonAt))
            {
                flowHints = !flowHints;
                Log.Info($"Flow hints {(flowHints ? "on" : "off")}");
                PlayerPrefs.SetInt(FlowHintKey, flowHints ? 1 : 0);
                PlayerPrefs.Save();
                Tap(hintButtonAt);
                sfx.Pick();
                return;
            }
            if (Hit(at, soundAt))
            {
                sfx.Muted = !sfx.Muted;
                Log.Info($"Sound {(sfx.Muted ? "off" : "on")}");
                PlayerPrefs.SetInt(MuteKey, sfx.Muted ? 1 : 0);
                Tap(soundAt);
                sfx.Pick();
                return;
            }
            if (game.Rules.Undo != UndoPolicy.None && Hit(at, undoAt))
            {
                if (game.CanUndo)
                {
                    Tap(undoAt);
                    Undo();
                }
                else
                {
                    deniedAt = now;
                    Haptics.Tick();
                }
                return;
            }
            if (game.Over)
            {
                if (game.Mode == Mode.Daily && now > overAt + RestartWait && Hit(at, shareAt))
                {
                    string shareText = $"Pointless Daily {FormatSeedDate(game.Seed)}\nPerfect Trio: {game.Perfects}\nSkóre: {game.Score}\nLínie: {endLines}\nMax Kombo: {endCombo}";
                    ShareText(shareText);
                    copiedAt = now;
                    Tap(shareAt);
                    sfx.Pick();
                    Haptics.Click();
                    Busy(2.2f);
                    Log.Info("Daily share text\n" + shareText);
                    return;
                }
                if (now <= overAt + RestartWait) return;
                if (Locked()) OpenMenu();
                else Begin(game.Mode);
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

        void PressMenu(Vector2 at)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Hit(at, menuModeAt[i], 0.8f) || HitLabel(at, menuModeAt[i], 1.42f, 1f, 0.5f))
                {
                    Tap(menuModeAt[i]);
                    menuOpen = false;
                    SwitchMode((Mode)i);
                    sfx.Pick();
                    Haptics.Click();
                    return;
                }
                if (Hit(at, menuThemeAt[i], 0.75f))
                {
                    Theme.Use((ThemeId)i);
                    Log.Info($"Theme {Theme.Current}");
                    if (view != null) view.backgroundColor = Theme.Back;
                    PlayerPrefs.SetInt(ThemeKey, i);
                    PlayerPrefs.Save();
                    Tap(menuThemeAt[i]);
                    dirty = true;
                    sfx.Pick();
                    Haptics.Click();
                    return;
                }
            }
            for (int i = 0; i < Goals.KindCount; i++)
            {
                if (!Hit(at, menuGoalAt[i], 0.65f) && !HitLabel(at, menuGoalAt[i], 1.18f, 0.65f, 0.45f)) continue;
                goals.Select((GoalKind)i);
                Log.Info($"Goal {goals.Active}");
                PlayerPrefs.SetString(GoalsKey, goals.Pack());
                PlayerPrefs.Save();
                Tap(menuGoalAt[i]);
                dirty = true;
                sfx.Pick();
                Haptics.Click();
                return;
            }
            menuOpen = false;
            sfx.Back();
        }

        void OpenMenu()
        {
            menuOpen = true;
            menuBest[(int)Mode.Arcade] = PlayerPrefs.GetInt(ArcadeBestKey, 0);
            menuBest[(int)Mode.Flow] = PlayerPrefs.GetInt(FlowBestKey, 0);
            menuBest[(int)Mode.Daily] = PlayerPrefs.GetInt(DailyBestPrefix + TodaySeed(), 0);
            if (game.Mode != Mode.Daily || game.Seed == TodaySeed())
                menuBest[(int)game.Mode] = Mathf.Max(menuBest[(int)game.Mode], best);
            Tap(modeButtonAt);
            sfx.Pick();
            Haptics.Click();
        }

        void OpenHelp()
        {
            helpOpen = true;
            helpSeen |= 1 << (int)game.Mode;
            PlayerPrefs.SetInt(HelpSeenKey, helpSeen);
            PlayerPrefs.Save();
            Log.Info($"Help {game.Mode}");
            Tap(helpAt);
            sfx.Pick();
            Haptics.Click();
        }

        void Tap(Vector2 button)
        {
            tapped = button;
            tappedAt = now;
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
            modeButtonAt = new Vector2(-Wide * 0.5f + 0.95f * side, scoreY + 0.5f * side);
            soundAt = new Vector2(Wide * 0.5f - 0.95f * side, scoreY + 0.5f * side);
            streakAt = new Vector2(-2.55f * side, apexY - 1.9f * rise);
            undoAt = new Vector2(modeButtonAt.x, scoreY - 0.65f * side);
            helpAt = new Vector2(soundAt.x, undoAt.y);
            hintButtonAt = new Vector2(soundAt.x, undoAt.y - 1.15f * side);
            for (int i = 0; i < 3; i++)
            {
                menuModeAt[i] = new Vector2((i - 1) * 2.6f * side, apexY - 1f * side);
                menuThemeAt[i] = new Vector2((i - 1) * 2.6f * side, apexY - 4f * side);
            }
            for (int i = 0; i < 4; i++)
                menuGoalAt[i] = new Vector2((i - 1.5f) * 1.95f * side, apexY - 6.4f * side);
            shareAt = new Vector2(0f, endY - 3.3f * side);

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
            int shownStars = goals.Stars;
            int earnedStars = goals.Record(move, canUndoMove);
            if (earnedStars > 0) Log.Info($"Goal stars +{earnedStars}, {goals.TotalStars} in total");
            if (goals.Active != GoalKind.None && goals.Stars > shownStars)
            {
                sfx.Record();
                Haptics.Click();
                starAt = now;
                Busy(0.6f);
                for (int i = 0; i < 6; i++) Scatter(GoalStar(), Theme.Gold);
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
                    sweeps[sweepCount++] = new Sweep { Line = Bits.Lowest(lines), Tint = move.Tint, At = now, Gold = move.Triple };
                shakeAt = streakHit = pipAt[slot] = now;
                shakeSize = side * Mathf.Min(0.02f + 0.035f * move.Lines, 0.14f);
                Pop(target, move.Points, 0.4f + 0.08f * Mathf.Min(move.Lines, 4), Theme.Top[move.Tint]);
                sfx.Clear(move.Combo, move.Lines);
                Haptics.Thump();
                if (move.Triple)
                {
                    var middle = new Vector2(0f, apexY - 4.04f * side);
                    Pop(middle, Game.TripleBonus, 0.6f, Theme.Gold);
                    for (int i = 0; i < 8; i++) Scatter(middle, Theme.Gold);
                    sfx.Perfect();
                    Log.Info($"Triple clear +{Game.TripleBonus}, {game.Triples} in this game");
                }
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
            if (!beaten && record > 0 && game.Score > record && game.Mode != Mode.Daily)
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
            endRecord = game.Mode != Mode.Daily && game.Score > record;
            feted = false;
            Log.Info($"Game over {game.Mode}: score {game.Score}, lines {endLines}, max combo {endCombo}{(endRecord ? ", new record" : string.Empty)}");
            if (game.Mode == Mode.Daily) PlayerPrefs.SetString(DailyFinalKey, game.Pack());
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
            if (mode == Mode.Daily && Played())
            {
                Review();
                return;
            }
            Fresh(mode, SeedFor(mode));
        }

        bool Played()
        {
            string final = PlayerPrefs.GetString(DailyFinalKey, string.Empty);
            return final.Length > 0 && game.Unpack(final) && game.Over && game.Mode == Mode.Daily && game.Seed == TodaySeed();
        }

        bool Locked() => game.Mode == Mode.Daily && game.Over && game.Seed == TodaySeed();

        void Review()
        {
            PlayerPrefs.SetInt(LastModeKey, (int)Mode.Daily);
            best = Mathf.Max(PlayerPrefs.GetInt(BestKey(), 0), game.Score);
            record = best;
            beaten = false;
            shown = game.Score;
            Calm();
            endLines = game.Lines;
            endCombo = game.MaxCombo;
            overAt = now - RestartWait - 1f;
            veil = 1f;
            for (int slot = 0; slot < Game.Slots; slot++)
            {
                slotAt[slot] = Home(slot);
                slotSize[slot] = TraySize;
                slotFrom[slot] = now;
            }
            hintSlot = -1;
            Refresh();
            Busy(1f);
            Log.Info($"Daily {game.Seed} already played, score {game.Score}");
        }

        string NextLabel()
        {
            int minutes = Mathf.Max(1, (int)Math.Ceiling((DateTime.Today.AddDays(1) - DateTime.Now).TotalMinutes));
            if (minutes == nextMinutes) return nextLabel;
            nextMinutes = minutes;
            nextLabel = minutes >= 60 ? $"ďalšia výzva o {minutes / 60} h {minutes % 60} min" : $"ďalšia výzva o {minutes} min";
            return nextLabel;
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
            float helpGoal = helpOpen ? 1f : 0f;
            if (helpVeil != helpGoal)
            {
                helpVeil = Mathf.MoveTowards(helpVeil, helpGoal, delta / 0.22f);
                Busy(0.1f);
            }
        }

        void Draw()
        {
            batch.Begin();
            labels.Begin();
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
                Color32 shine = sweep.Gold ? Theme.Gold : Theme.Mix(Theme.Top[sweep.Tint], Theme.White, 0.7f);
                batch.Line(from.x, from.y, to.x, to.y, rise * (0.2f + 0.34f * fade), Theme.Fade(shine, 0.6f * fade), 0.5f);
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
            GoalBar();
            Streak(heat);
            Toast();
            if (veil > 0f) batch.Line(0f, -high, 0f, high, Wide, Theme.Fade(Theme.Back, 0.9f * open));
            Score(open);
            if (veil > 0f) Finish();
            Icons();
            if (hintSlot >= 0) Hint();
            if (menuVeil > 0f) DrawMenu();
            if (helpVeil > 0f) DrawHelp();

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

            labels.End();
            batch.End();
        }

        void Streak(Color32 heat)
        {
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, veil);
            if (game.Combo < 2 || alpha <= 0f) return;
            heat = Theme.Fade(heat, alpha);
            float bump = Mathf.Clamp01((now - streakHit) / 0.3f);
            float size = side * 0.62f * (1f + 0.35f * Mathf.Sin(bump * Mathf.PI) * (1f - bump));
            int length = Glyphs.Write(text, game.Combo, Glyphs.Times);
            Glyphs.Draw(batch, text, length, streakAt.x, streakAt.y, size, 0.5f, heat, 0.16f);
            if (!game.Rules.ComboExpires) return;
            for (int i = 0; i < Game.Grace; i++)
                batch.Dot(streakAt.x + (i - 1) * side * 0.26f, streakAt.y - side * 0.34f, side * 0.055f, i < Game.Grace - game.Misses ? heat : Theme.Fade(Theme.Faint, alpha));
        }

        void Score(float open)
        {
            float hit = Mathf.Clamp01((now - scoreHit) / 0.25f);
            float punch = 1f + 0.1f * Mathf.Sin(hit * Mathf.PI) * (1f - hit);
            int value = Mathf.RoundToInt(shown);
            Color32 ink = endRecord ? Theme.Mix(Theme.Ink, Theme.Gold, open) : Theme.Ink;
            if (open > 0f)
            {
                float titleY = Mathf.Lerp(scoreY, endY, open) + side * (Mathf.Lerp(1f, EndScale, open) + 0.6f);
                ModeIcon(game.Mode, new Vector2(0f, titleY), side * 0.3f * Spring(open), Theme.Fade(Theme.Icon, open), game.Mode == Mode.Daily ? DayOf(game.Seed) : 0);
            }

            int length = Glyphs.Write(text, value);
            float size = side * Mathf.Lerp(1f, EndScale, open) * punch;
            float span = Glyphs.Width(text, length, size);
            float widest = Wide * Mathf.Lerp(WidestPlay, WidestScore, open);
            if (span > widest) size *= widest / span;
            Glyphs.Draw(batch, text, length, 0f, Mathf.Lerp(scoreY, endY, open), size, 0.5f, ink, 0.13f);

            float cheer = Mathf.Clamp01((now - bestHit) / 0.5f);
            Color32 peak = game.Score > record || (veil > 0f && endRecord) ? Theme.Gold : Theme.Icon;
            float small = side * Mathf.Lerp(0.3f, 0.36f, open) * (1f + 0.5f * Mathf.Sin(cheer * Mathf.PI) * (1f - cheer));
            float recordY = Mathf.Lerp(bestY, endY - 0.8f * side, open);
            if (game.Mode != Mode.Daily) Record(0f, recordY, Mathf.Max(best, value), small, peak);
            else TrioCount(0f, recordY, game.Perfects, small, game.Perfects > 0 ? Theme.Gold : Theme.Icon);
        }

        void TrioCount(float x, float y, int value, float size, Color32 tint)
        {
            int length = Glyphs.Write(text, value);
            float icon = size * 0.62f, gap = size * 0.3f;
            float start = x - (icon * 2f + gap + Glyphs.Width(text, length, size)) * 0.5f;
            Trio(start + icon, y + size * 0.5f, icon, tint);
            Glyphs.Draw(batch, text, length, start + icon * 2f + gap, y, size, 0f, tint, 0.17f);
        }

        void Trio(float x, float y, float s, Color32 tint)
        {
            for (int i = 0; i < 3; i++)
                batch.Tri(x + (i - 1) * s * 0.72f, y - s * 0.08f, s * 0.3f, true, 0f, tint, tint, Corner);
        }

        void Directions(float x, float y, float s, Color32 tint)
        {
            float r = s * 0.12f;
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.PI * i / 3f;
                float dx = Mathf.Cos(angle) * s * 0.72f, dy = Mathf.Sin(angle) * s * 0.72f;
                batch.Line(x - dx, y - dy, x + dx, y + dy, r, tint);
            }
        }

        void Record(float x, float y, int value, float size, Color32 tint)
        {
            int length = Glyphs.Write(text, value);
            float crown = size * 0.62f, gap = size * 0.3f;
            float start = x - (crown * 2f + gap + Glyphs.Width(text, length, size)) * 0.5f;
            Crown(start + crown, y + size * 0.5f, crown, tint);
            Glyphs.Draw(batch, text, length, start + crown * 2f + gap, y, size, 0f, tint, 0.17f);
        }

        void Finish()
        {
            float show = Mathf.Clamp01((veil - 0.45f) / 0.4f);
            if (show > 0f)
            {
                float line = endY - 2.15f * side, tall = side * 0.42f * Spring(show);
                Color32 ink = Theme.Fade(Theme.Ink, 0.85f * show);
                int length = Glyphs.Write(text, endLines);
                float icon = tall * 0.72f, gap = tall * 0.45f;
                float left = -1.6f * side - (icon * 2f + gap + Glyphs.Width(text, length, tall)) * 0.5f;
                Strip(left + icon, line + tall * 0.5f, icon, ink);
                Glyphs.Draw(batch, text, length, left + icon * 2f + gap, line, tall, 0f, ink, 0.16f);

                length = Glyphs.Write(text, endCombo, Glyphs.Times);
                Glyphs.Draw(batch, text, length, 1.6f * side, line, tall, 0.5f, ink, 0.16f);

                Color32 caption = Theme.Fade(Theme.Icon, show * Uncovered());
                float cap = side * CaptionSize;
                labels.Put(game.Mode == Mode.Daily ? "perfect trio" : "rekord", 0f, endY - 1.08f * side, cap, caption);
                labels.Put("línie", -1.6f * side, endY - 2.42f * side, cap, caption);
                labels.Put("max kombo", 1.6f * side, endY - 2.42f * side, cap, caption);
            }

            float ready = Mathf.Clamp01((veil - 0.6f) / 0.4f);
            if (ready <= 0f) return;
            float grow = Spring(ready);

            if (game.Mode == Mode.Daily)
            {
                Chip(shareAt, side * 0.5f * grow, ready);
                float s = side * 0.26f * grow;
                if (now - copiedAt < 2f)
                {
                    float age = Mathf.Clamp01((now - copiedAt) / 0.25f);
                    Check(shareAt, s * (1f + 0.22f * Mathf.Sin(age * Mathf.PI) * (1f - age)), Theme.Gold);
                }
                else Share(shareAt, s, Theme.Fade(Theme.Ink, ready));
                labels.Put("zdieľať", shareAt.x, shareAt.y - 0.78f * side, side * CaptionSize, Theme.Fade(Theme.Icon, ready * Uncovered()));
            }

            Vector2 restart = new Vector2(0f, endY - (game.Mode == Mode.Daily ? 5.15f : 3.8f) * side);
            Color32 note = Theme.Fade(Theme.Icon, ready * Uncovered()), glyph = Theme.Fade(Theme.Ink, ready);
            if (Locked())
            {
                Chip(restart, side * 0.78f * grow, ready);
                Clock(restart, side * 0.4f * grow, glyph);
                labels.Put(NextLabel(), restart.x, restart.y - 1.08f * side, side * CaptionSize, note);
                labels.Put("ťukni a vyber iný režim", restart.x, restart.y - 1.43f * side, side * CaptionSize, note);
                return;
            }
            float beat = 1f + 0.04f * Mathf.Sin(now * 3.2f);
            Chip(restart, side * 0.78f * grow * beat, ready);
            batch.Tri(restart.x, restart.y, side * 0.4f * grow * beat, true, -Mathf.PI * 0.5f, glyph, glyph, 0.22f);
            labels.Put("nová hra", restart.x, restart.y - 1.08f * side, side * CaptionSize, note);
        }

        void Icons()
        {
            float s = side * IconSize, chip = side * ChipSize;
            Chip(modeButtonAt, chip, 1f);
            ModeIcon(game.Mode, modeButtonAt, s, Theme.Icon, 0);

            Chip(soundAt, chip, 1f);
            Speaker(soundAt, s, sfx.Muted, Theme.Icon);

            Chip(helpAt, chip, 1f);
            Question(helpAt, s, Theme.Icon);
            if ((helpSeen & (1 << (int)game.Mode)) == 0)
                batch.Dot(helpAt.x + chip * 0.68f, helpAt.y + chip * 0.68f, side * 0.075f, Theme.Ink);

            if (game.Rules.Undo != UndoPolicy.None)
            {
                float wobble = Mathf.Clamp01((now - deniedAt) / 0.3f);
                Vector2 at = undoAt + new Vector2(Mathf.Sin(wobble * Mathf.PI * 4f) * (1f - wobble) * side * 0.08f, 0f);
                Color32 ink = !game.CanUndo ? Theme.Mix(Theme.Dim, Theme.CellTop, 0.75f) : game.Over ? Theme.Ink : Theme.Icon;
                Chip(undoAt, chip, 1f);
                UndoArrow(at, s, ink);
            }

            if (game.Rules.Hints == HintPolicy.Optional)
            {
                Chip(hintButtonAt, chip, 1f);
                Bulb(hintButtonAt, s, flowHints, flowHints ? Theme.Ink : Theme.Icon);
            }
        }

        float Uncovered() => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(menuVeil, helpVeil));

        void Toast()
        {
            float age = (now - modeShownAt) / ToastTime;
            if (age >= 1f) return;
            float fade = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01((1f - age) / 0.35f);
            float grow = Spring(Mathf.Clamp01(age / 0.3f));
            var at = new Vector2(0f, apexY - 4.04f * side);
            batch.Dot(at.x, at.y, side * 1.2f * grow, Theme.Fade(Theme.CellTop, 0.92f * fade));
            ModeIcon(game.Mode, at, side * 0.6f * grow, Theme.Fade(Theme.Ink, fade), game.Mode == Mode.Daily ? DayOf(game.Seed) : 0);
            labels.Put(modeNames[(int)game.Mode], at.x, at.y - 0.85f * side, side * NameSize * grow, Theme.Fade(Theme.Ink, fade * Uncovered() * (1f - veil)));
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

        bool Hit(Vector2 at, Vector2 target, float reach = ButtonReach) => (at - target).sqrMagnitude < side * side * reach * reach;

        bool HitLabel(Vector2 at, Vector2 center, float drop, float halfWide, float halfHigh) =>
            Mathf.Abs(at.x - center.x) < halfWide * side && Mathf.Abs(at.y - (center.y - drop * side)) < halfHigh * side;

        static ulong TodaySeed()
        {
            DateTime today = DateTime.Now;
            return (ulong)(today.Year * 10000 + today.Month * 100 + today.Day);
        }

        static int DayOf(ulong seed) => (int)((seed == 0 ? TodaySeed() : seed) % 100);

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
            modeShownAt = now;
            Busy(ToastTime);
        }

        static string SavedKeyFor(Mode mode) => mode switch
        {
            Mode.Flow => SavedFlowKey,
            Mode.Daily => SavedDailyKey,
            _ => SavedArcadeKey
        };

        Vector2 GoalStar()
        {
            int length = Glyphs.Write(text, goals.CurrentTarget, goals.Active == GoalKind.ComboSkill ? Glyphs.Times : '\0');
            return new Vector2(side * GoalLabel + Glyphs.Width(text, length, side * 0.24f) + side * 0.24f, trayY + side * GoalRow);
        }

        void GoalBar()
        {
            float alpha = (1f - Mathf.SmoothStep(0f, 1f, veil)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(menuVeil, helpVeil)));
            if (goals.Active == GoalKind.None || alpha <= 0f) return;
            Color32 dim = Theme.Fade(Theme.Icon, alpha), gold = Theme.Fade(Theme.Gold, alpha);
            float y = trayY + side * GoalRow, half = side * GoalHalf, r = side * 0.042f;
            GoalIcon(goals.Active, new Vector2(-half - side * 0.5f, y), side * 0.26f, dim);
            batch.Line(-half, y, half, y, r, Theme.Fade(Theme.Faint, alpha));
            float progress = goals.Progress;
            if (progress > 0f) batch.Line(-half, y, Mathf.Lerp(-half, half, progress), y, r, gold);

            float tall = side * 0.24f;
            int length = Glyphs.Write(text, goals.CurrentTarget, goals.Active == GoalKind.ComboSkill ? Glyphs.Times : '\0');
            Glyphs.Draw(batch, text, length, side * GoalLabel, y - tall * 0.5f, tall, 0f, dim, 0.17f);
            float pop = Mathf.Clamp01((now - starAt) / 0.45f);
            Vector2 star = GoalStar();
            Star(star.x, star.y, side * 0.15f * (1f + 0.7f * Mathf.Sin(pop * Mathf.PI) * (1f - pop)), gold);
        }

        void DrawMenu()
        {
            float open = Mathf.SmoothStep(0f, 1f, menuVeil);
            float grow = Spring(open);
            batch.Line(0f, -high, 0f, high, Wide, Theme.Fade(Theme.Back, open));
            Color32 ink = Theme.Fade(Theme.Ink, open), icon = Theme.Fade(Theme.Icon, open), gold = Theme.Fade(Theme.Gold, open);
            float stroke = side * 0.035f;

            for (int i = 0; i < 3; i++)
            {
                bool active = (int)game.Mode == i;
                Vector2 at = menuModeAt[i];
                Chip(at, side * 0.75f * grow, open);
                if (active) Ring(at, side * 0.87f * grow, stroke, ink);
                ModeIcon((Mode)i, at, side * 0.4f * grow, active ? ink : icon, (Mode)i == Mode.Daily ? DayOf(0) : 0);
                labels.Put(modeNames[i], at.x, at.y - side * ModeName, side * NameSize * grow, active ? ink : icon);
                if (menuBest[i] > 0) Record(at.x, at.y - side * ModeLabel, menuBest[i], side * 0.24f * grow, icon);
            }

            for (int i = 0; i < 3; i++)
            {
                var id = (ThemeId)i;
                Vector2 at = menuThemeAt[i];
                float r = side * 0.62f * grow * (1f + 0.14f * Pulse(at));
                batch.Dot(at.x, at.y, r, Theme.Fade(Theme.BackOf(id), open));
                Ring(at, r, side * 0.03f, Theme.Fade(Theme.Faint, open));
                if (id == Theme.Current) Ring(at, r + side * 0.13f * grow, stroke, ink);
                Swatch(id, at, r * 0.6f, open);
            }

            for (int i = 0; i < Goals.KindCount; i++)
            {
                var kind = (GoalKind)i;
                bool active = goals.Active == kind;
                Vector2 at = menuGoalAt[i];
                Chip(at, side * 0.6f * grow, open);
                if (active) Ring(at, side * 0.71f * grow, stroke, ink);
                GoalIcon(kind, at, side * 0.3f * grow, active ? ink : icon);
                labels.Put(goalNames[i], at.x, at.y - side * GoalName, side * GoalNameSize * grow, active ? ink : icon);
                if (kind == GoalKind.None) continue;
                float size = side * 0.22f * grow, y = at.y - side * GoalLabelDrop;
                int length = Glyphs.Write(text, goals.GetStars(kind));
                float star = size * 0.6f, gap = size * 0.25f;
                float start = at.x - (star * 2f + gap + Glyphs.Width(text, length, size)) * 0.5f;
                Star(start + star, y + size * 0.5f, star, gold);
                Glyphs.Draw(batch, text, length, start + star * 2f + gap, y, size, 0f, ink, 0.17f);
            }
        }

        void DrawHelp()
        {
            float open = Mathf.SmoothStep(0f, 1f, helpVeil);
            float grow = Spring(open);
            batch.Line(0f, -high, 0f, high, Wide, Theme.Fade(Theme.Back, open));
            Color32 ink = Theme.Fade(Theme.Ink, open);
            int mode = (int)game.Mode;
            var head = new Vector2(0f, side * 4.6f);
            Chip(head, side * 0.75f * grow, open);
            ModeIcon(game.Mode, head, side * 0.42f * grow, ink, game.Mode == Mode.Daily ? DayOf(game.Seed) : 0);
            labels.Put(modeNames[mode], 0f, side * 3.55f, side * 0.32f * grow, ink);
            for (int i = 0; i < helpRules[mode].Length; i++)
                HelpRow(side * (2.55f - HelpStep * i), helpSigns[mode][i], helpRules[mode][i], open, grow);
            batch.Line(-side * 3.2f, -side * 0.5f, side * 3.2f, -side * 0.5f, side * 0.02f, Theme.Fade(Theme.Faint, open));
            for (int i = 0; i < basicRules.Length; i++)
                HelpRow(-side * (1.15f + HelpStep * i), basicSigns[i], basicRules[i], open, grow);
            labels.Put("ťukni kamkoľvek", 0f, -side * 5.3f, side * CaptionSize * grow, Theme.Fade(Theme.Icon, open));
        }

        void HelpRow(float y, Sign sign, string rule, float alpha, float grow)
        {
            var at = new Vector2(-side * 3.1f, y);
            float s = side * 0.17f * grow;
            Color32 tint = Theme.Fade(Theme.Icon, alpha);
            Chip(at, side * 0.34f * grow, alpha);
            switch (sign)
            {
                case Sign.Trophy: Trophy(at.x, at.y, s, tint); break;
                case Sign.Cloud: Cloud(at.x, at.y, s, tint); break;
                case Sign.Calendar: Calendar(at.x, at.y, s, tint, 0); break;
                case Sign.Rise:
                    for (int i = 0; i < 3; i++)
                        batch.Tri(at.x + (i - 1) * s * 0.62f, at.y - s * 0.5f + s * (0.18f + 0.14f * i), s * (0.22f + 0.12f * i), true, 0f, tint, tint, Corner);
                    break;
                case Sign.Undo: UndoArrow(at, s, tint); break;
                case Sign.NoUndo:
                    UndoArrow(at, s, Theme.Fade(Theme.Mix(Theme.Dim, Theme.CellTop, 0.5f), alpha));
                    batch.Line(at.x - s * 0.7f, at.y - s * 0.7f, at.x + s * 0.7f, at.y + s * 0.7f, s * 0.12f, tint);
                    break;
                case Sign.Combo:
                    batch.Line(at.x - s * 0.48f, at.y - s * 0.48f, at.x + s * 0.48f, at.y + s * 0.48f, s * 0.15f, tint);
                    batch.Line(at.x - s * 0.48f, at.y + s * 0.48f, at.x + s * 0.48f, at.y - s * 0.48f, s * 0.15f, tint);
                    break;
                case Sign.Bulb: Bulb(at, s, true, tint); break;
                case Sign.Share: Share(at, s * 0.9f, tint); break;
                case Sign.Piece:
                    batch.Tri(at.x - s * 0.3f, at.y - s * 0.12f, s * 0.42f, true, 0f, tint, tint, Corner);
                    batch.Tri(at.x + s * 0.3f, at.y + s * 0.12f, s * 0.42f, false, 0f, tint, tint, Corner);
                    break;
                case Sign.Strip: Strip(at.x, at.y, s * 0.85f, tint); break;
                case Sign.Stuck: GoalIcon(GoalKind.None, at, s, tint); break;
                case Sign.Trio: Trio(at.x, at.y, s * 1.1f, Theme.Fade(Theme.Gold, alpha)); break;
                case Sign.Directions: Directions(at.x, at.y, s, tint); break;
            }
            labels.Put(rule, -side * 2.5f, y, side * 0.22f * grow, Theme.Fade(Theme.Ink, alpha), true);
        }

        void Clock(Vector2 at, float s, Color32 tint)
        {
            float r = s * 0.13f;
            Ring(at, s * 0.68f, r, tint);
            batch.Line(at.x, at.y, at.x, at.y + s * 0.42f, r, tint);
            batch.Line(at.x, at.y, at.x + s * 0.3f, at.y - s * 0.12f, r, tint);
        }

        void Question(Vector2 at, float s, Color32 tint)
        {
            float r = s * 0.13f;
            Arc(at.x, at.y + s * 0.3f, s * 0.36f, 165f, -55f, r, tint);
            float bend = -55f * Mathf.Deg2Rad;
            float ex = at.x + Mathf.Cos(bend) * s * 0.36f, ey = at.y + s * 0.3f + Mathf.Sin(bend) * s * 0.36f;
            batch.Line(ex, ey, at.x, at.y - s * 0.22f, r, tint);
            batch.Dot(at.x, at.y - s * 0.62f, r * 1.2f, tint);
        }

        void Swatch(ThemeId id, Vector2 at, float half, float alpha)
        {
            float y = at.y - half * 0.29f, cell = half * 0.43f;
            Color32 a = Theme.Fade(Theme.TintOf(id, 0), alpha), b = Theme.Fade(Theme.TintOf(id, 2), alpha);
            Color32 c = Theme.Fade(Theme.TintOf(id, 3), alpha), d = Theme.Fade(Theme.TintOf(id, 5), alpha);
            batch.Tri(at.x - half * 0.5f, y - half * 0.289f, cell, true, 0f, a, a, Corner);
            batch.Tri(at.x + half * 0.5f, y - half * 0.289f, cell, true, 0f, b, b, Corner);
            batch.Tri(at.x, y + half * 0.577f, cell, true, 0f, c, c, Corner);
            batch.Tri(at.x, y, cell, false, 0f, d, d, Corner);
        }

        void ModeIcon(Mode mode, Vector2 at, float s, Color32 tint, int day)
        {
            switch (mode)
            {
                case Mode.Arcade:
                    Trophy(at.x, at.y, s, tint);
                    break;
                case Mode.Flow:
                    Cloud(at.x, at.y, s, tint);
                    break;
                case Mode.Daily:
                    Calendar(at.x, at.y, s, tint, day);
                    break;
            }
        }

        void GoalIcon(GoalKind kind, Vector2 at, float s, Color32 tint)
        {
            float r = s * 0.13f;
            switch (kind)
            {
                case GoalKind.None:
                    Ring(at, s * 0.62f, r, tint);
                    batch.Line(at.x - s * 0.44f, at.y - s * 0.44f, at.x + s * 0.44f, at.y + s * 0.44f, r, tint);
                    break;
                case GoalKind.ClearLines:
                    Strip(at.x, at.y, s * 0.85f, tint);
                    break;
                case GoalKind.BigClears:
                    Strip(at.x, at.y + s * 0.3f, s * 0.6f, tint);
                    Strip(at.x, at.y - s * 0.3f, s * 0.6f, tint);
                    break;
                case GoalKind.ComboSkill:
                    batch.Line(at.x - s * 0.48f, at.y - s * 0.48f, at.x + s * 0.48f, at.y + s * 0.48f, s * 0.15f, tint);
                    batch.Line(at.x - s * 0.48f, at.y + s * 0.48f, at.x + s * 0.48f, at.y - s * 0.48f, s * 0.15f, tint);
                    break;
            }
        }

        float Pulse(Vector2 at)
        {
            float age = (now - tappedAt) / TapTime;
            if (age >= 1f || (tapped - at).sqrMagnitude > 1e-6f) return 0f;
            return Mathf.Sin(age * Mathf.PI) * (1f - age);
        }

        void Chip(Vector2 at, float radius, float alpha)
        {
            float bump = Pulse(at);
            Color32 tint = Theme.Mix(Theme.CellTop, Theme.Faint, Mathf.Clamp01(bump * 2.5f));
            batch.Dot(at.x, at.y, radius * (1f + 0.14f * bump), Theme.Fade(tint, alpha));
        }

        void Ring(Vector2 at, float radius, float stroke, Color32 tint) => Arc(at.x, at.y, radius, 0f, 360f, stroke, tint);

        void Arc(float x, float y, float radius, float from, float to, float stroke, Color32 tint)
        {
            int steps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(to - from) / 15f));
            float angle = from * Mathf.Deg2Rad;
            float px = x + Mathf.Cos(angle) * radius, py = y + Mathf.Sin(angle) * radius;
            for (int i = 1; i <= steps; i++)
            {
                angle = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
                float nx = x + Mathf.Cos(angle) * radius, ny = y + Mathf.Sin(angle) * radius;
                batch.Line(px, py, nx, ny, stroke, tint);
                px = nx;
                py = ny;
            }
        }

        void Star(float x, float y, float radius, Color32 tint)
        {
            batch.Dot(x, y, radius * 0.42f, tint);
            for (int i = 0; i < 5; i++)
            {
                float angle = Mathf.PI * (0.5f + 0.4f * i);
                batch.Tri(x + Mathf.Cos(angle) * radius * 0.5f, y + Mathf.Sin(angle) * radius * 0.5f, radius * 0.433f, true, angle - Mathf.PI * 0.5f, tint, tint, 0.12f);
            }
        }

        void Crown(float x, float y, float s, Color32 tint)
        {
            batch.Line(x - s * 0.72f, y - s * 0.38f, x + s * 0.72f, y - s * 0.38f, s * 0.2f, tint);
            batch.Tri(x, y - s * 0.02f, s * 0.46f, true, 0f, tint, tint, Corner);
            batch.Tri(x - s * 0.58f, y - s * 0.1f, s * 0.36f, true, 0f, tint, tint, Corner);
            batch.Tri(x + s * 0.58f, y - s * 0.1f, s * 0.36f, true, 0f, tint, tint, Corner);
        }

        void Trophy(float x, float y, float s, Color32 tint)
        {
            float r = s * 0.11f;
            batch.Line(x - s * 0.5f, y + s * 0.62f, x + s * 0.5f, y + s * 0.62f, s * 0.17f, tint);
            batch.Dot(x, y + s * 0.28f, s * 0.5f, tint);
            batch.Line(x - s * 0.52f, y + s * 0.56f, x - s * 0.8f, y + s * 0.46f, r, tint);
            batch.Line(x - s * 0.8f, y + s * 0.46f, x - s * 0.46f, y + s * 0.08f, r, tint);
            batch.Line(x + s * 0.52f, y + s * 0.56f, x + s * 0.8f, y + s * 0.46f, r, tint);
            batch.Line(x + s * 0.8f, y + s * 0.46f, x + s * 0.46f, y + s * 0.08f, r, tint);
            batch.Line(x, y - s * 0.15f, x, y - s * 0.55f, s * 0.12f, tint);
            batch.Line(x - s * 0.42f, y - s * 0.68f, x + s * 0.42f, y - s * 0.68f, s * 0.14f, tint);
        }

        void Cloud(float x, float y, float s, Color32 tint)
        {
            y -= s * 0.09f;
            batch.Dot(x - s * 0.42f, y - s * 0.14f, s * 0.36f, tint);
            batch.Dot(x + s * 0.4f, y - s * 0.1f, s * 0.42f, tint);
            batch.Dot(x - s * 0.02f, y + s * 0.2f, s * 0.5f, tint);
            batch.Line(x - s * 0.42f, y - s * 0.28f, x + s * 0.4f, y - s * 0.28f, s * 0.24f, tint);
        }

        void Calendar(float x, float y, float s, Color32 tint, int day)
        {
            y -= s * 0.08f;
            float r = s * 0.1f, w = s * 0.72f, top = y + s * 0.52f, floor = y - s * 0.72f;
            batch.Line(x - w, top, x + w, top, s * 0.2f, tint);
            batch.Line(x - w, top, x - w, floor, r, tint);
            batch.Line(x + w, top, x + w, floor, r, tint);
            batch.Line(x - w, floor, x + w, floor, r, tint);
            batch.Line(x - s * 0.38f, top + s * 0.1f, x - s * 0.38f, top + s * 0.36f, r, tint);
            batch.Line(x + s * 0.38f, top + s * 0.1f, x + s * 0.38f, top + s * 0.36f, r, tint);
            if (day <= 0)
            {
                batch.Dot(x, y - s * 0.12f, s * 0.17f, tint);
                return;
            }
            int length = Glyphs.Write(text, day);
            float tall = s * 0.66f;
            Glyphs.Draw(batch, text, length, x, y - s * 0.12f - tall * 0.5f, tall, 0.5f, tint, 0.2f);
        }

        void Strip(float x, float y, float s, Color32 tint)
        {
            float floor = y - s * 0.433f, cell = s * 0.43f;
            batch.Tri(x - s * 0.5f, floor + s * 0.289f, cell, true, 0f, tint, tint, Corner);
            batch.Tri(x, floor + s * 0.577f, cell, false, 0f, tint, tint, Corner);
            batch.Tri(x + s * 0.5f, floor + s * 0.289f, cell, true, 0f, tint, tint, Corner);
        }

        void UndoArrow(Vector2 at, float s, Color32 tint)
        {
            float x = at.x + s * 0.06f, y = at.y;
            Arc(x, y - s * 0.05f, s * 0.52f, 180f, -50f, s * 0.12f, tint);
            batch.Tri(x - s * 0.52f, y - s * 0.22f, s * 0.3f, false, 0f, tint, tint, Corner);
        }

        void Speaker(Vector2 at, float s, bool muted, Color32 tint)
        {
            float x = at.x - s * 0.08f, y = at.y;
            batch.Line(x - s * 0.6f, y, x - s * 0.44f, y, s * 0.2f, tint);
            batch.Tri(x - s * 0.22f, y, s * 0.48f, true, Mathf.PI * 0.5f, tint, tint, Corner);
            float r = s * 0.1f;
            if (muted)
            {
                batch.Line(x + s * 0.38f, y - s * 0.2f, x + s * 0.78f, y + s * 0.2f, r, tint);
                batch.Line(x + s * 0.38f, y + s * 0.2f, x + s * 0.78f, y - s * 0.2f, r, tint);
                return;
            }
            Arc(x, y, s * 0.46f, -42f, 42f, r, tint);
            Arc(x, y, s * 0.78f, -45f, 45f, r, tint);
        }

        void Bulb(Vector2 at, float s, bool on, Color32 tint)
        {
            float x = at.x, y = at.y - s * 0.06f;
            batch.Dot(x, y + s * 0.2f, s * 0.44f, tint);
            batch.Line(x - s * 0.18f, y - s * 0.36f, x + s * 0.18f, y - s * 0.36f, s * 0.11f, tint);
            batch.Line(x - s * 0.1f, y - s * 0.58f, x + s * 0.1f, y - s * 0.58f, s * 0.09f, tint);
            if (!on) return;
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.PI * (0.18f + 0.32f * i);
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                batch.Line(x + cos * s * 0.66f, y + s * 0.2f + sin * s * 0.66f, x + cos * s * 0.86f, y + s * 0.2f + sin * s * 0.86f, s * 0.08f, tint);
            }
        }

        void Share(Vector2 at, float s, Color32 tint)
        {
            float lx = at.x - 0.8f * s, rx = at.x + 0.75f * s, dy = 0.78f * s;
            batch.Line(lx, at.y, rx, at.y + dy, s * 0.12f, tint);
            batch.Line(lx, at.y, rx, at.y - dy, s * 0.12f, tint);
            batch.Dot(lx, at.y, s * 0.3f, tint);
            batch.Dot(rx, at.y + dy, s * 0.3f, tint);
            batch.Dot(rx, at.y - dy, s * 0.3f, tint);
        }

        void Check(Vector2 at, float s, Color32 tint)
        {
            float r = s * 0.18f;
            batch.Line(at.x - 0.75f * s, at.y - 0.05f * s, at.x - 0.15f * s, at.y - 0.65f * s, r, tint);
            batch.Line(at.x - 0.15f * s, at.y - 0.65f * s, at.x + 0.85f * s, at.y + 0.65f * s, r, tint);
        }
    }
}