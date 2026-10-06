// Decides which collision region to export next.
//
// Pure bookkeeping (no Unity, no game API): the export itself lives in
// CollisionExport. Rules from the porting guide - nearest column first, refresh
// on a timer, evict by sending a region empty, and know when the area around the
// player has been filled at least once so the handoff can finish.

using System;
using System.Collections.Generic;

namespace MinecraftInRepo.Host
{
    public struct RegionKey : IEquatable<RegionKey>
    {
        public int X, Y, Z;

        public RegionKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public long Packed
        {
            get
            {
                // 22 bits per axis, signed - plenty for a level.
                long px = (X + 0x200000L) & 0x3FFFFFL;
                long py = (Y + 0x200000L) & 0x3FFFFFL;
                long pz = (Z + 0x200000L) & 0x3FFFFFL;
                return (px << 44) | (py << 22) | pz;
            }
        }

        /// <summary>Squared distance in regions, weighted so vertical distance counts more.</summary>
        public int WeightedDistanceTo(RegionKey other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            int dz = Z - other.Z;
            return dx * dx + dz * dz + 4 * dy * dy;
        }

        public bool Equals(RegionKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is RegionKey && Equals((RegionKey)obj);
        }

        public override int GetHashCode()
        {
            return (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + "," + Z + ")";
        }
    }

    public sealed class RegionScheduler
    {
        private readonly int radiusXZ;
        private readonly int radiusY;
        private readonly Dictionary<long, int> dueTimes = new Dictionary<long, int>();
        private RegionKey centre;
        private bool centreSet;

        public RegionScheduler(int radiusXZ, int radiusY)
        {
            this.radiusXZ = Math.Max(0, radiusXZ);
            this.radiusY = Math.Max(0, radiusY);
        }

        public RegionKey Centre => centre;

        /// <summary>True once SetCentre has been called at least once.</summary>
        public bool HasCentre => centreSet;

        /// <summary>Recentre on the player. Returns true when the centre region changed.</summary>
        public bool SetCentre(int rx, int ry, int rz)
        {
            RegionKey next = new RegionKey(rx, ry, rz);
            if (centreSet && next.Equals(centre))
            {
                return false;
            }
            centre = next;
            centreSet = true;
            // Regions outside the new box are forgotten; the guest keeps what it
            // was last told, and the epoch bump makes it drop stale data.
            dueTimes.Clear();
            return true;
        }

        /// <summary>True once every region in range has been exported at least once.</summary>
        public bool Ready
        {
            get
            {
                if (!centreSet)
                {
                    return false;
                }
                foreach (RegionKey region in Enumerate())
                {
                    if (!dueTimes.ContainsKey(region.Packed))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Nearest region that has never been sent or whose refresh time has come.
        /// </summary>
        public bool TryNext(int nowMs, out RegionKey region)
        {
            region = default(RegionKey);
            if (!centreSet)
            {
                return false;
            }

            bool found = false;
            int bestDistance = int.MaxValue;
            RegionKey best = default(RegionKey);
            foreach (RegionKey candidate in Enumerate())
            {
                int due;
                if (dueTimes.TryGetValue(candidate.Packed, out due) && due > nowMs)
                {
                    continue;
                }
                int distance = candidate.WeightedDistanceTo(centre);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                    found = true;
                }
            }

            region = best;
            return found;
        }

        /// <summary>Record that a region was sent; it becomes due again after refreshMs.</summary>
        public void MarkSent(RegionKey region, int nowMs, int refreshMs)
        {
            dueTimes[region.Packed] = nowMs + Math.Max(250, refreshMs);
        }

        public void Forget(RegionKey region)
        {
            dueTimes.Remove(region.Packed);
        }

        public void Clear()
        {
            dueTimes.Clear();
        }

        public int Count
        {
            get
            {
                int count = 0;
                foreach (RegionKey region in Enumerate())
                {
                    count++;
                }
                return count;
            }
        }

        private IEnumerable<RegionKey> Enumerate()
        {
            for (int y = centre.Y - radiusY; y <= centre.Y + radiusY; y++)
            {
                for (int z = centre.Z - radiusXZ; z <= centre.Z + radiusXZ; z++)
                {
                    for (int x = centre.X - radiusXZ; x <= centre.X + radiusXZ; x++)
                    {
                        yield return new RegionKey(x, y, z);
                    }
                }
            }
        }
    }
}
