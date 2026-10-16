using System.Collections.Generic;
using Unity.Mathematics;
namespace AsciiArena
{
    public sealed class SeparationGrid
    {
        private readonly Dictionary<int2, List<float2>> buckets = new();
        public void Clear() { foreach (var bucket in buckets.Values) bucket.Clear(); }
        public void Add(float2 position)
        {
            var key = (int2)math.floor(position);
            if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = new List<float2>(8);
            bucket.Add(position);
        }
        public float2 At(float2 position)
        {
            float2 force = 0; var cell = (int2)math.floor(position);
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                if (buckets.TryGetValue(cell + new int2(x, y), out var bucket))
                    foreach (var other in bucket)
                    {
                        var away = position - other; float square = math.lengthsq(away);
                        if (square > .001f && square < .65f) force += away * (.65f - square) * 2;
                    }
            return force;
        }
    }
}
