using Pointless.Core;
using UnityEngine;

namespace Pointless
{
    public static class Theme
    {
        public static readonly Color32 Back = Hex(0x0E1015);
        public static readonly Color32 CellTop = Hex(0x1E222E);
        public static readonly Color32 CellLow = Hex(0x181B25);
        public static readonly Color32 Ink = Hex(0xF5F6F8);
        public static readonly Color32 Dim = Hex(0x596173);
        public static readonly Color32 Faint = Hex(0x2A2F3D);
        public static readonly Color32 Gold = Hex(0xFFC94A);
        public static readonly Color32 White = Hex(0xFFFFFF);
        public static readonly Color32[] Top = new Color32[Game.Tints];
        public static readonly Color32[] Low = new Color32[Game.Tints];

        static readonly uint[] tints = { 0xFF6B6B, 0xFFB547, 0x37D9A0, 0x3FB6FF, 0x8E7BFF, 0xFF74B8 };

        static Theme()
        {
            for (int i = 0; i < Game.Tints; i++)
            {
                Color32 tint = Hex(tints[i]);
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
