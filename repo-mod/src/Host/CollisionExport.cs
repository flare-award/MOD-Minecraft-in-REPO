// Exports the host's world as voxels so the guest can collide with it.
//
// The guest's player collides using Minecraft's own movement code, so the host
// only has to say which half-block cells are solid. Per column of a region we
// walk down with ray casts and collect solid spans (top and underside), then
// fill the cells between them. Undersides need back faces, which is why
// Physics.queriesHitBackfaces is switched on while sampling.
//
// Cost is bounded by a time budget, not by a count: the export streams a few
// columns per frame. Regions refresh on a timer, so the host's moving geometry
// is picked up without any events.

using System;
using System.Diagnostics;
using MinecraftInRepo.Sync;
using UnityEngine;

namespace MinecraftInRepo.Host
{
    public sealed class CollisionExport
    {
        /// <summary>Regions kept around the player: (2 * radiusXZ + 1) blocks wide, in region units.</summary>
        public const int RadiusXZ = 1;
        public const int RadiusY = 1;

        /// <summary>Milliseconds of ray casting per frame.</summary>
        public const double BudgetMs = 2.5;

        private const int MaxCastsPerColumn = 8;
        private const float Skin = 0.02f;          // step out of a surface before the next cast
        /// <summary>How long a new centre region must hold before we recentre (ms).</summary>
        public const int RecentreDelayMs = 500;

        /// <summary>Distance (weighted, squared) that counts as a jump and recentres at once.</summary>
        public const int JumpDistance = 16;

        private const int NearRefreshMs = 4000;
        private const int FarRefreshMs = 30000;
        private const int LayerMaskAll = ~0;

        private readonly RepoApi api;
        private readonly Action<string> log;
        private readonly GuestLink link;
        private readonly CoordinateMap map;
        private readonly RegionScheduler scheduler = new RegionScheduler(RadiusXZ, RadiusY);
        private readonly Stopwatch watch = new Stopwatch();
        private readonly VoxelRegion region = new VoxelRegion(0, 0, 0);

        private bool pendingValid;
        private RegionKey pendingRegion;
        private int nextColumn;
        private int epoch = 1;

        private RegionKey desiredCentre;
        private bool desiredValid;
        private int desiredSinceMs;

        public CollisionExport(RepoApi repoApi, Action<string> log, GuestLink guestLink, CoordinateMap coordinateMap)
        {
            api = repoApi;
            this.log = log ?? delegate { };
            link = guestLink;
            map = coordinateMap;
        }

        /// <summary>Raised when the host's world changes under the player: the guest drops everything.</summary>
        public int Epoch => epoch;

        /// <summary>True once every region around the player has been sent at least once.</summary>
        public bool Ready => scheduler.Ready;

        public int RegionsInRange => scheduler.Count;

        /// <summary>Call on a new link or a scene change.</summary>
        public void Reset()
        {
            scheduler.Clear();
            pendingValid = false;
            desiredValid = false;
            epoch++;
            if (link != null)
            {
                link.SendVoxelClear(epoch);
            }
        }

        /// <summary>
        /// Stream collision around <paramref name="centreRepo"/>, which should be the
        /// character's position in R.E.P.O. space (the guest's position while it owns
        /// the body, the host's own character during handoff).
        /// </summary>
        public void Tick(Vector3 centreRepo, int nowMs)
        {
            if (link == null || !link.Connected)
            {
                return;
            }

            // Recentring bumps the epoch, which makes the guest drop everything it
            // was told, so it must not happen while the player hovers on a region
            // boundary: wait for the new region to hold steady for a moment.
            Vector3 centreMc = map.RepoToMc(centreRepo);
            RegionKey desired = new RegionKey(
                VoxelRegion.BlockToRegion(centreMc.x),
                VoxelRegion.BlockToRegion(centreMc.y),
                VoxelRegion.BlockToRegion(centreMc.z));

            if (!desiredValid || !desired.Equals(desiredCentre))
            {
                desiredCentre = desired;
                desiredSinceMs = nowMs;
                desiredValid = true;
            }

            if (!desired.Equals(scheduler.Centre))
            {
                bool jumped = scheduler.HasCentre &&
                              desired.WeightedDistanceTo(scheduler.Centre) > JumpDistance;
                bool settled = nowMs - desiredSinceMs >= RecentreDelayMs;
                if (!scheduler.HasCentre || jumped || settled)
                {
                    scheduler.SetCentre(desired.X, desired.Y, desired.Z);
                    epoch++;
                    pendingValid = false;
                    link.SendVoxelClear(epoch);
                    log("[MinecraftInRepo] collision: recentred on region " + scheduler.Centre +
                        " (jumped=" + jumped + "), epoch " + epoch);
                }
            }

            watch.Reset();
            watch.Start();
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                while (watch.Elapsed.TotalMilliseconds < BudgetMs)
                {
                    if (!pendingValid)
                    {
                        if (!scheduler.TryNext(nowMs, out pendingRegion))
                        {
                            return;   // everything is up to date
                        }
                        region.SetRegion(pendingRegion.X, pendingRegion.Y, pendingRegion.Z);
                        region.Clear();
                        nextColumn = 0;
                        pendingValid = true;
                    }

                    while (nextColumn < VoxelRegion.CellsPerAxis * VoxelRegion.CellsPerAxis)
                    {
                        int cx = nextColumn % VoxelRegion.CellsPerAxis;
                        int cz = nextColumn / VoxelRegion.CellsPerAxis;
                        SampleColumn(cx, cz);
                        nextColumn++;
                        if (watch.Elapsed.TotalMilliseconds >= BudgetMs)
                        {
                            break;
                        }
                    }

                    if (nextColumn < VoxelRegion.CellsPerAxis * VoxelRegion.CellsPerAxis)
                    {
                        return;   // out of time, continue next frame
                    }

                    link.SendVoxel(region, epoch);
                    bool near = pendingRegion.Equals(scheduler.Centre);
                    scheduler.MarkSent(pendingRegion, nowMs, near ? NearRefreshMs : FarRefreshMs);
                    pendingValid = false;
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                watch.Stop();
            }
        }

        /// <summary>Walks one half-block column and fills the solid spans it finds.</summary>
        private void SampleColumn(int cx, int cz)
        {
            float blockX = (float)VoxelRegion.CellToBlock(region.Rx, cx) + VoxelRegion.CellSize * 0.5f;
            float blockZ = (float)VoxelRegion.CellToBlock(region.Rz, cz) + VoxelRegion.CellSize * 0.5f;
            float top = (float)VoxelRegion.CellToBlock(region.Ry, VoxelRegion.CellsPerAxis);
            float bottom = (float)VoxelRegion.CellToBlock(region.Ry, 0);

            float y = top + Skin;
            for (int cast = 0; cast < MaxCastsPerColumn; cast++)
            {
                if (y <= bottom)
                {
                    return;
                }

                float hitY;
                if (!CastDown(blockX, blockZ, y, bottom, out hitY))
                {
                    return;   // open air all the way down
                }

                float spanTop = hitY;
                float spanBottom;
                if (CastDown(blockX, blockZ, spanTop - Skin, bottom, out spanBottom))
                {
                    // The ray started inside something solid and hit its underside.
                    if (spanBottom >= spanTop - Skin)
                    {
                        spanBottom = bottom;   // no useful underside: treat as solid to the floor
                    }
                }
                else
                {
                    spanBottom = bottom;      // solid all the way down
                }

                FillSpan(cz, spanBottom, spanTop);
                y = spanBottom - Skin;
            }
        }

        private void FillSpan(int cz, float spanBottom, float spanTop)
        {
            for (int cy = 0; cy < VoxelRegion.CellsPerAxis; cy++)
            {
                float cellBottom = (float)VoxelRegion.CellToBlock(region.Ry, cy);
                float cellTop = cellBottom + VoxelRegion.CellSize;
                if (cellTop > spanBottom + Skin && cellBottom < spanTop - Skin)
                {
                    // Cell index order is x + 16 * (z + 16 * y).
                    for (int cx = 0; cx < VoxelRegion.CellsPerAxis; cx++)
                    {
                        region.SetCell(cx, cy, cz, true);
                    }
                }
            }
        }

        private bool CastDown(float blockX, float blockZ, float fromY, float toY, out float hitY)
        {
            hitY = 0f;
            Vector3 from = map.McToRepo(new Vector3(blockX, fromY, blockZ));
            Vector3 to = map.McToRepo(new Vector3(blockX, toY, blockZ));
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                return false;
            }

            RaycastHit hit;
            if (!Physics.Raycast(from, delta / distance, out hit, distance + Skin, LayerMaskAll))
            {
                return false;
            }
            if (IgnoreHit(hit))
            {
                return false;
            }

            hitY = map.RepoToMc(hit.point).y;
            return true;
        }

        /// <summary>The player's own body and loose props are not world.</summary>
        private bool IgnoreHit(RaycastHit hit)
        {
            if (hit.collider == null)
            {
                return true;
            }
            UnityEngine.Collider collider = hit.collider;
            if (collider.transform == null)
            {
                return true;
            }

            object avatar = api != null ? api.StaticValue("PlayerAvatar", "instance") : null;
            if (avatar is Component)
            {
                Transform avatarTransform = ((Component)avatar).transform;
                for (Transform walk = collider.transform; walk != null; walk = walk.parent)
                {
                    if (walk == avatarTransform)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
