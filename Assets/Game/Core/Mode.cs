using System;

namespace Pointless.Core
{
    // These numbers are part of the p2 save format.
    public enum Mode { Arcade = 0, Flow = 1, Daily = 2 }
    public enum UndoPolicy { None, OncePerTrio, Unlimited }
    public enum HintPolicy { None, Tutorial, Optional }

    public sealed class ModeRules
    {
        static readonly ModeRules arcade = new ModeRules(true, UndoPolicy.OncePerTrio, HintPolicy.Tutorial, GeneratorProfile.Standard);
        static readonly ModeRules flow = new ModeRules(false, UndoPolicy.Unlimited, HintPolicy.Optional, GeneratorProfile.Relaxed);
        static readonly ModeRules daily = new ModeRules(true, UndoPolicy.None, HintPolicy.None, GeneratorProfile.Standard);

        public bool ComboExpires { get; }
        public UndoPolicy Undo { get; }
        public HintPolicy Hints { get; }
        public GeneratorProfile Generator { get; }

        ModeRules(bool comboExpires, UndoPolicy undo, HintPolicy hints, GeneratorProfile generator)
        {
            ComboExpires = comboExpires;
            Undo = undo;
            Hints = hints;
            Generator = generator;
        }

        public static ModeRules For(Mode mode) => mode switch
        {
            Mode.Arcade => arcade,
            Mode.Flow => flow,
            Mode.Daily => daily,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
