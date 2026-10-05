using System;
using MinecraftInRepo.Host;
using Xunit;

namespace MinecraftInRepo.Tests
{
    public class VoxelRegionTests
    {
        [Fact]
        public void RegionIsHalfBlockCellsInAnEightBlockCube()
        {
            Assert.Equal(16, VoxelRegion.CellsPerAxis);
            Assert.Equal(4096, VoxelRegion.CellCount);
            Assert.Equal(512, VoxelRegion.ByteCount);
            Assert.Equal(0.5f, VoxelRegion.CellSize);
        }

        [Fact]
        public void CellsSetAndReadBack()
        {
            VoxelRegion region = new VoxelRegion(1, 2, 3);
            Assert.True(region.Empty);

            region.SetCell(0, 0, 0, true);
            region.SetCell(15, 15, 15, true);
            Assert.False(region.Empty);
            Assert.True(region.GetCell(0, 0, 0));
            Assert.True(region.GetCell(15, 15, 15));
            Assert.False(region.GetCell(1, 0, 0));
        }

        [Fact]
        public void OutOfRangeWritesAreIgnored()
        {
            VoxelRegion region = new VoxelRegion(0, 0, 0);
            region.SetCell(-1, 0, 0, true);
            region.SetCell(16, 0, 0, true);
            Assert.True(region.Empty);
            Assert.False(region.GetCell(-1, 0, 0));
        }

        [Fact]
        public void Base64RoundTripKeepsEveryCell()
        {
            VoxelRegion region = new VoxelRegion(-2, 0, 5);
            Random random = new Random(7);
            bool[] expected = new bool[VoxelRegion.CellCount];
            for (int i = 0; i < VoxelRegion.CellCount; i++)
            {
                bool solid = random.Next(4) == 0;
                expected[i] = solid;
                region.SetCell(i % 16, (i / 16) % 16, i / 256, solid);
            }

            byte[] bytes;
            Assert.True(VoxelRegion.TryFromBase64(region.ToBase64(), out bytes));
            Assert.Equal(VoxelRegion.ByteCount, bytes.Length);

            for (int i = 0; i < VoxelRegion.CellCount; i++)
            {
                Assert.Equal(expected[i], VoxelRegion.GetBit(bytes, i % 16, (i / 16) % 16, i / 256));
            }
        }

        [Fact]
        public void BadBase64IsRejected()
        {
            byte[] bytes;
            Assert.False(VoxelRegion.TryFromBase64("not base64 !", out bytes));
            Assert.False(VoxelRegion.TryFromBase64(Convert.ToBase64String(new byte[16]), out bytes));
        }

        [Fact]
        public void RegionCoordinatesMapToBlocks()
        {
            Assert.Equal(0.0, VoxelRegion.CellToBlock(0, 0));
            Assert.Equal(4.0, VoxelRegion.CellToBlock(0, 8));
            Assert.Equal(8.0, VoxelRegion.CellToBlock(1, 0));
            Assert.Equal(-8.0, VoxelRegion.CellToBlock(-1, 0));

            Assert.Equal(0, VoxelRegion.BlockToRegion(0.0));
            Assert.Equal(0, VoxelRegion.BlockToRegion(7.9));
            Assert.Equal(1, VoxelRegion.BlockToRegion(8.0));
            Assert.Equal(-1, VoxelRegion.BlockToRegion(-0.1));
        }

        [Fact]
        public void WireMessageCarriesTheEpochAndRegion()
        {
            VoxelRegion region = new VoxelRegion(2, -1, 3);
            region.SetCell(4, 5, 6, true);
            string json = region.ToJson(9);

            Assert.Contains("\"t\":\"vox\"", json);
            Assert.Contains("\"epoch\":9", json);
            Assert.Contains("\"rx\":2", json);
            Assert.Contains("\"ry\":-1", json);
            Assert.Contains("\"rz\":3", json);
            Assert.Contains("\"cells\":\"", json);
        }
    }
}
