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
        public static readonly Color32 White = Hex(0xFFFFFF);
        public static readonly Color32[] Top = new Color32[Game.Tints];
        public static readonly Color32[] Low = new Color32[Game.Tints];

        struct Palette
        {
            public uint Back, CellTop, CellLow, Ink, Dim, Faint, Gold;
            public uint[] Tints;
        }

        static readonly Palette[] palettes =
        {
            // 0: Aurora (pôvodné farby)
            new Palette
            {
                Back = 0x0E1015, CellTop = 0x1E222E, CellLow = 0x181B25,
                Ink = 0xF5F6F8, Dim = 0x596173, Faint = 0x2A2F3D, Gold = 0xFFC94A,
                Tints = new uint[] { 0xFF6B6B, 0xFFB547, 0x37D9A0, 0x3FB6FF, 0x8E7BFF, 0xFF74B8 }
            },
            // 1: Sunset (teplá tmavá slivková, oranžové, ružové, koralové)
            new Palette
            {
                Back = 0x160D18, CellTop = 0x2A1A2E, CellLow = 0x211424,
                Ink = 0xFFF3F0, Dim = 0x7A587D, Faint = 0x3D2642, Gold = 0xFFE066,
                Tints = new uint[] { 0xFF5E6C, 0xFF964F, 0xFF5CA8, 0xC86DFB, 0xFFBE55, 0xE84A5F }
            },
            // 2: Ocean (chladná hlbinná modrá, tyrkysové, ľadové)
            new Palette
            {
                Back = 0x08131C, CellTop = 0x132636, CellLow = 0x0E1E2B,
                Ink = 0xEBF8FF, Dim = 0x4A708A, Faint = 0x1D374D, Gold = 0xFFD15C,
                Tints = new uint[] { 0x2EE6D6, 0x38A3FF, 0x5CE1E6, 0x6C7BFF, 0x38D996, 0x99C2FF }
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

            for (int i = 0; i < Game.Tints; i++)
            {
                Color32 tint = Hex(p.Tints[i]);
                Top[i] = Mix(tint, White, 0.16f);
                Low[i] = Mix(tint, Back, 0.12f);
            }
        }

        public static Color32 Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static Color32 Mix(Color32 from, Color32 to, float amount) => new Color32(
            (byte)(from.r + (to.r - from.r) * amount),
            (byte)(from.g + (to.g - from.g) * amount),
            (byte)(from.b + (to.b - from.b) * amount),
            (byte)(from.a + (to.a - from.a) * amount));

        public static Color32 Fade(Color32 tint, float alpha) => new Color32(tint.r, tint.g, tint.b, (byte)(tint.a * Mathf.Clamp01(alpha)));
    }
}