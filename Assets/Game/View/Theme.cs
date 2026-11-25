using Pointless.Core;
using UnityEngine;

namespace Pointless
{
    public enum ThemeId { Aurora = 0, Sunset = 1, Ocean = 2 }

    public static class Theme
    {
        public static ThemeId Current { get; private set; }

        public static Color32 Back;
        public static Color32 CellTop;
        public static Color32 CellLow;
        public static Color32 Ink;
        public static Color32 Dim;
        public static Color32 Faint;
        public static Color32 Gold;
        public static Color32 Icon;
        public static readonly Color32 White = Hex(0xFFFFFF);
        public static readonly Color32[] Top = new Color32[Game.Tints];
        public static readonly Color32[] Low = new Color32[Game.Tints];

        struct Palette
        {
            public uint Back, CellTop, CellLow, Ink, Dim, Faint, Gold;
            public uint[] Tints;
        }

        const float Lift = 0.16f;
        const float IconLift = 0.25f;

        static readonly Palette[] palettes =
        {
            new Palette
            {
                Back = 0x0E1015, CellTop = 0x1E222E, CellLow = 0x181B25,
                Ink = 0xF5F6F8, Dim = 0x596173, Faint = 0x2A2F3D, Gold = 0xFFD24C,
                Tints = new uint[] { 0xE84D63, 0xFC8B47, 0x3ED095, 0x48B3FF, 0x8273F7, 0xF683D6 }
            },
            new Palette
            {
                Back = 0x160D18, CellTop = 0x2A1A2E, CellLow = 0x211424,
                Ink = 0xFFF3F0, Dim = 0x7A587D, Faint = 0x3D2642, Gold = 0xFFE566,
                Tints = new uint[] { 0xE54A56, 0xF4853A, 0xFE81B2, 0xB065E2, 0xFFBDA3, 0x545AC9 }
            },
            new Palette
            {
                Back = 0x08131C, CellTop = 0x132636, CellLow = 0x0E1E2B,
                Ink = 0xEBF8FF, Dim = 0x4A708A, Faint = 0x1D374D, Gold = 0xFFD15A,
                Tints = new uint[] { 0x26E8E3, 0x49A9FE, 0xC4EBFE, 0x706DE7, 0x46CA72, 0xD3ACFD }
            }
        };

        static Theme()
        {
            Use(ThemeId.Aurora);
        }

        public static void Use(ThemeId id)
        {
            if ((uint)id >= (uint)palettes.Length) id = ThemeId.Aurora;
            Current = id;
            Palette p = palettes[(int)id];
            Back = Hex(p.Back);
            CellTop = Hex(p.CellTop);
            CellLow = Hex(p.CellLow);
            Ink = Hex(p.Ink);
            Dim = Hex(p.Dim);
            Faint = Hex(p.Faint);
            Gold = Hex(p.Gold);
            Icon = Mix(Dim, Ink, IconLift);

            for (int i = 0; i < Game.Tints; i++)
            {
                Color32 tint = Hex(p.Tints[i]);
                Top[i] = Mix(tint, White, Lift);
                Low[i] = Mix(tint, Back, 0.12f);
            }
        }

        public static Color32 BackOf(ThemeId id) => Hex(palettes[(int)id].Back);

        public static Color32 TintOf(ThemeId id, int tint) => Mix(Hex(palettes[(int)id].Tints[tint]), White, Lift);

        public static Color32 Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static Color32 Mix(Color32 from, Color32 to, float amount) => new Color32(
            (byte)(from.r + (to.r - from.r) * amount),
            (byte)(from.g + (to.g - from.g) * amount),
            (byte)(from.b + (to.b - from.b) * amount),
            (byte)(from.a + (to.a - from.a) * amount));

        public static Color32 Fade(Color32 tint, float alpha) => new Color32(tint.r, tint.g, tint.b, (byte)(tint.a * Mathf.Clamp01(alpha)));
    }
}