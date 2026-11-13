namespace Pointless.Core
{
    public static class Bits
    {
        const ulong Sequence = 0x022FDD63CC95386DUL;
        static readonly byte[] position = new byte[64];

        static Bits()
        {
            for (int i = 0; i < 64; i++) position[((1UL << i) * Sequence) >> 58] = (byte)i;
        }

        public static int Count(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }

        public static int Lowest(ulong value) => position[((value & (0UL - value)) * Sequence) >> 58];
    }
}
