using System;
using Ragnarok.Network;
using ROIO.Models.FileTypes;

namespace Ragnarok.Maps {
    public static class RoGatWalkSurfaceAdapter {
        public static RoWalkSurfaceGrid Build(GAT altitude) {
            if (altitude == null) {
                throw new ArgumentNullException(nameof(altitude));
            }

            if (altitude.width <= 0 || altitude.width > int.MaxValue ||
                altitude.height <= 0 || altitude.height > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(altitude), "GAT dimensions must fit positive Int32 values.");
            }

            int width = (int) altitude.width;
            int height = (int) altitude.height;
            int count = checked(width * height);
            if (altitude.cells == null || altitude.cells.Length != count) {
                throw new ArgumentException("GAT cell count must match width * height.", nameof(altitude));
            }

            var walkable = new bool[count];
            var heights = new float[count];
            for (int i = 0; i < count; i++) {
                GAT.Cell cell = altitude.cells[i];
                walkable[i] = (cell.type & (byte) GAT.TYPE.WALKABLE) != 0;
                heights[i] = RoMapCoordinateSystem.GatHeight(cell);
            }

            return new RoWalkSurfaceGrid(width, height, walkable, heights);
        }
    }
}
