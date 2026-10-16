using Unity.Mathematics;

namespace AsciiArena
{
    public sealed class ArenaLayout
    {
        public const float HalfWidth = 22;
        public const float HalfHeight = 16;
        public readonly float2[] Pillars = { new(-7, 6), new(-11, 6), new(11, 9), new(15, 9) };
        public const float ProjectionY = .68f;
        public static readonly float2 ChunkSize = new(44, 32);
        // Centered chunks, including negative coordinates. Geometry is derived on demand:
        // there is no growing cache of visited chunks and revisiting never changes obstacles.
        public static int2 ChunkAt(float2 p) => (int2)math.floor((p + ChunkSize * .5f) / ChunkSize);
        public static float2 ChunkCenter(int2 chunk) => (float2)chunk * ChunkSize;
        public static uint Variant(int2 chunk) => math.all(chunk == 0) ? 0 : math.hash(chunk);
        private static float2 Mirror(int2 chunk) => new((Variant(chunk) & 1) == 0 ? 1 : -1, (Variant(chunk) & 2) == 0 ? 1 : -1);
        public static bool HasPond(int2 chunk) => Variant(chunk) % 3 != 1;
        public static float2 PondCenter(int2 chunk) => ChunkCenter(chunk) + new float2(-9, -10) * Mirror(chunk);
        public float2 PillarAt(int2 chunk, int index) => ChunkCenter(chunk) + Pillars[index] * Mirror(chunk);
        public static float2 DecorationAt(int2 chunk, float2 local) => ChunkCenter(chunk) + local * Mirror(chunk);
        public static float2 Project(float2 world) => new(world.x, world.y * ProjectionY);
        public static float2 ScreenToWorld(float2 screen) => new(screen.x, screen.y / ProjectionY);
        public static bool Water(float2 p)
        {
            var chunk = ChunkAt(p);
            return HasPond(chunk) && math.lengthsq((p - PondCenter(chunk)) / new float2(9, 4.5f)) < 1;
        }
        public bool Walkable(float2 p)
        {
            if (Water(p)) return false;
            var chunk = ChunkAt(p);
            for (int i = 0; i < Pillars.Length; i++)
                if (math.distancesq(p, PillarAt(chunk, i)) < .85f) return false;
            return true;
        }
        public float2 Move(float2 position, float2 delta)
        {
            if (Walkable(position + delta)) return position + delta;
            float2 xOnly = position + new float2(delta.x, 0);
            if (Walkable(xOnly)) position = xOnly;
            float2 yOnly = position + new float2(0, delta.y);
            return Walkable(yOnly) ? yOnly : position;
        }
    }
}
