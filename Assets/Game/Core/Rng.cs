namespace Pointless.Core
{
    public struct Rng
    {
        ulong state;

        public Rng(ulong seed)
        {
            state = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            if (state == 0) state = 0x9E3779B97F4A7C15UL;
        }

        public ulong State
        {
            get => state;
            set => state = value == 0 ? 0x9E3779B97F4A7C15UL : value;
        }

        public ulong Next()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 0x2545F4914F6CDD1DUL;
        }

        public int Range(int count) => (int)((Next() >> 33) % (ulong)count);

        public float Unit() => (Next() >> 40) * (1f / 16777216f);
    }
}
