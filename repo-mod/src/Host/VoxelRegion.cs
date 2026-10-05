// One region of host collision, voxelised and sent to the guest.
//
// A region is 8 x 8 x 8 blocks, sampled on a half-block grid: 16 x 16 x 16 =
// 4096 cells, one bit each, 512 bytes. The guest turns solid cells into
// invisible barrier blocks in its mirror world, so Minecraft's own movement
// code does the collision: ledges, sneaking on edges, walls and falling all
// behave like Minecraft without either side re-implementing the other's physics.
//
// Wire message: {"t":"vox","epoch":n,"rx":..,"ry":..,"rz":..,"cells":"<base64 512 bytes>"}
//               {"t":"voxclear","epoch":n}
//
// Cell order: index = cx + 16 * (cz + 16 * cy); bit 0 of byte 0 is cell 0.

using System;

namespace MinecraftInRepo.Host
{
    public sealed class VoxelRegion
    {
        public const int BlocksPerAxis = 8;
        public const int CellsPerAxis = 16;
        public const float CellSize = 0.5f;
        public const int CellCount = CellsPerAxis * CellsPerAxis * CellsPerAxis;
        public const int ByteCount = CellCount / 8;

        public int Rx { get; private set; }
        public int Ry { get; private set; }
        public int Rz { get; private set; }

        private readonly byte[] bits = new byte[ByteCount];
        private int solidCount;

        public VoxelRegion(int rx, int ry, int rz)
        {
            Rx = rx;
            Ry = ry;
            Rz = rz;
        }

        public void SetRegion(int rx, int ry, int rz)
        {
            Rx = rx;
            Ry = ry;
            Rz = rz;
        }

        public void Clear()
        {
            Array.Clear(bits, 0, ByteCount);
            solidCount = 0;
        }

        public bool Empty => solidCount == 0;

        public static int Index(int cx, int cy, int cz)
        {
            return cx + CellsPerAxis * (cz + CellsPerAxis * cy);
        }

        public void SetCell(int cx, int cy, int cz, bool solid)
        {
            if ((uint)cx >= CellsPerAxis || (uint)cy >= CellsPerAxis || (uint)cz >= CellsPerAxis)
            {
                return;
            }

            int index = Index(cx, cy, cz);
            int byteIndex = index >> 3;
            int mask = 1 << (index & 7);
            bool was = (bits[byteIndex] & mask) != 0;
            if (solid && !was)
            {
                bits[byteIndex] |= (byte)mask;
                solidCount++;
            }
            else if (!solid && was)
            {
                bits[byteIndex] &= (byte)~mask;
                solidCount--;
            }
        }

        public bool GetCell(int cx, int cy, int cz)
        {
            if ((uint)cx >= CellsPerAxis || (uint)cy >= CellsPerAxis || (uint)cz >= CellsPerAxis)
            {
                return false;
            }

            int index = Index(cx, cy, cz);
            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }

        public byte[] ToBytes()
        {
            byte[] copy = new byte[ByteCount];
            Array.Copy(bits, copy, ByteCount);
            return copy;
        }

        public string ToBase64()
        {
            return Convert.ToBase64String(bits);
        }

        public static bool TryFromBase64(string text, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            try
            {
                byte[] decoded = Convert.FromBase64String(text);
                if (decoded.Length != ByteCount)
                {
                    return false;
                }
                bytes = decoded;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool GetBit(byte[] bytes, int cx, int cy, int cz)
        {
            if (bytes == null || bytes.Length != ByteCount)
            {
                return false;
            }
            if ((uint)cx >= CellsPerAxis || (uint)cy >= CellsPerAxis || (uint)cz >= CellsPerAxis)
            {
                return false;
            }
            int index = Index(cx, cy, cz);
            return (bytes[index >> 3] & (1 << (index & 7))) != 0;
        }

        /// <summary>World block coordinate of a cell's lower corner, for the given region axis.</summary>
        public static double CellToBlock(int region, int cell)
        {
            return region * BlocksPerAxis + cell * CellSize;
        }

        /// <summary>Region index that contains the given block coordinate.</summary>
        public static int BlockToRegion(double block)
        {
            return (int)Math.Floor(block / BlocksPerAxis);
        }

        public string ToJson(int epoch)
        {
            return "{\"t\":\"vox\",\"epoch\":" + epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"rx\":" + Rx.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"ry\":" + Ry.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"rz\":" + Rz.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ",\"cells\":\"" + ToBase64() + "\"}";
        }
    }
}
