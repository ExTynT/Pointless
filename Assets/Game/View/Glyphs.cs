using System.Collections.Generic;
using UnityEngine;

namespace Pointless
{
    public static class Glyphs
    {
        public const char Plus = '+';
        public const char Times = 'x';
        const int Count = 12;
        const float Gap = 0.3f;
        const int ArcSteps = 6;

        static readonly float[][] strokes = new float[Count][];
        static readonly float[] widths = new float[Count];
        static readonly List<float> pen = new List<float>();
        static float penX, penY;

        static Glyphs()
        {
            Arc(0.29f, 0.71f, 0.29f, 0f, 180f, true);
            To(0f, 0.29f);
            Arc(0.29f, 0.29f, 0.29f, 180f, 360f, false);
            To(0.58f, 0.71f);
            Close(0, 0.58f);

            From(0.04f, 0.78f);
            To(0.28f, 1f);
            To(0.28f, 0f);
            Close(1, 0.34f);

            Arc(0.29f, 0.71f, 0.29f, 165f, -40f, true);
            To(0f, 0f);
            To(0.58f, 0f);
            Close(2, 0.58f);

            Arc(0.29f, 0.76f, 0.24f, 150f, -90f, true);
            Arc(0.29f, 0.26f, 0.26f, 90f, -150f, true);
            Close(3, 0.55f);

            From(0.44f, 0f);
            To(0.44f, 1f);
            To(0f, 0.3f);
            To(0.6f, 0.3f);
            Close(4, 0.6f);

            From(0.52f, 1f);
            To(0.1f, 1f);
            Arc(0.27f, 0.31f, 0.31f, 131f, -145f, false);
            Close(5, 0.58f);

            From(0.36f, 1f);
            Arc(0.29f, 0.29f, 0.29f, 150f, 510f, false);
            Close(6, 0.58f);

            From(0f, 1f);
            To(0.56f, 1f);
            To(0.18f, 0f);
            Close(7, 0.56f);

            Arc(0.29f, 0.765f, 0.235f, -90f, 270f, true);
            Arc(0.29f, 0.265f, 0.265f, 90f, 450f, true);
            Close(8, 0.58f);

            From(0.22f, 0f);
            Arc(0.29f, 0.71f, 0.29f, -30f, 330f, false);
            Close(9, 0.58f);

            From(0.06f, 0.5f);
            To(0.46f, 0.5f);
            From(0.26f, 0.3f);
            To(0.26f, 0.7f);
            Close(10, 0.52f);

            From(0.06f, 0.22f);
            To(0.42f, 0.58f);
            From(0.06f, 0.58f);
            To(0.42f, 0.22f);
            Close(11, 0.48f);
        }

        public static int Write(char[] text, int value, char sign = '\0')
        {
            int length = 0;
            if (sign != '\0') text[length++] = sign;
            int start = length;
            if (value < 0) value = 0;
            do
            {
                text[length++] = (char)('0' + value % 10);
                value /= 10;
            } while (value > 0 && length < text.Length);
            for (int i = start, j = length - 1; i < j; i++, j--) (text[i], text[j]) = (text[j], text[i]);
            return length;
        }

        public static float Width(char[] text, int length, float size)
        {
            float width = Gap * (length - 1);
            for (int i = 0; i < length; i++) width += widths[Index(text[i])];
            return width * size;
        }

        public static void Draw(Batch batch, char[] text, int length, float x, float y, float size, float align, Color32 tint, float weight)
        {
            float radius = size * weight * 0.5f;
            x -= Width(text, length, size) * align;
            for (int i = 0; i < length; i++)
            {
                int glyph = Index(text[i]);
                float[] lines = strokes[glyph];
                for (int j = 0; j < lines.Length; j += 4)
                    batch.Line(x + lines[j] * size, y + lines[j + 1] * size, x + lines[j + 2] * size, y + lines[j + 3] * size, radius, tint);
                x += (widths[glyph] + Gap) * size;
            }
        }

        static int Index(char sign) => sign == Plus ? 10 : sign == Times ? 11 : sign - '0';

        static void From(float x, float y)
        {
            penX = x;
            penY = y;
        }

        static void To(float x, float y)
        {
            if (Mathf.Abs(x - penX) + Mathf.Abs(y - penY) < 1e-4f) return;
            pen.Add(penX);
            pen.Add(penY);
            pen.Add(x);
            pen.Add(y);
            penX = x;
            penY = y;
        }

        static void Arc(float x, float y, float radius, float fromDegrees, float toDegrees, bool lift)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(toDegrees - fromDegrees) / 90f * ArcSteps));
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, (float)i / steps) * Mathf.Deg2Rad;
                float nextX = x + Mathf.Cos(angle) * radius, nextY = y + Mathf.Sin(angle) * radius;
                if (i == 0 && lift) From(nextX, nextY);
                else To(nextX, nextY);
            }
        }

        static void Close(int glyph, float width)
        {
            strokes[glyph] = pen.ToArray();
            widths[glyph] = width;
            pen.Clear();
        }
    }
}
