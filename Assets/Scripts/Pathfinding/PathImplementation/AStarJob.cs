using NavigationGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Utilities;

namespace Pathfinding.PathImplementation
{
    [BurstCompile]
    internal struct AStarJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> grid;

        public NativeHashSet<int> closedList;
        public NativePriorityQueue<PathCellData> openList;
        public NativeHashMap<int, PathCellData> visitedNodes;

        public int gridSizeX;

        public int startIndex;
        public int endIndex;

        public void Execute()
        {
            var startData = new PathCellData { cellIndex = startIndex, gCost = 0, hCost = GetDistance(startIndex, endIndex), cameFrom = -1, HeapIndex = int.MaxValue };
            openList.Enqueue(startData);
            visitedNodes.Add(startIndex, startData);

            while (openList.Length > 0)
            {
                PathCellData currentData = openList.Dequeue();
                int currentIndex = currentData.cellIndex;
                closedList.Add(currentIndex);

                if (currentIndex == endIndex)
                {
                    return;
                }

                NativeList<int> neighbors = new NativeList<int>(8, Allocator.Temp);
                GetNeighbors(currentIndex, ref neighbors);

                foreach (int neighborIndex in neighbors)
                {
                    if (!grid[neighborIndex].isWalkable || closedList.Contains(neighborIndex)) continue;

                    int costToNeighbor = currentData.gCost + GetDistance(currentIndex, neighborIndex);
                    if (visitedNodes.TryGetValue(neighborIndex, out PathCellData neighborData))
                    {
                        if (costToNeighbor >= neighborData.gCost) continue;
                    }

                    var newNeighborData = new PathCellData { cellIndex = neighborIndex, gCost = costToNeighbor, hCost = GetDistance(neighborIndex, endIndex), cameFrom = currentIndex, HeapIndex = int.MaxValue };
                    visitedNodes[neighborIndex] = newNeighborData;

                    openList.Enqueue(newNeighborData);
                }

                neighbors.Dispose();
            }
        }

        private void GetNeighbors(int indexCell, ref NativeList<int> neighbors)
        {
            Cell cell = grid[indexCell];

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                {
                    if (offsetX == 0 && offsetZ == 0) continue;

                    int gridX = cell.gridX + offsetX;
                    int gridZ = cell.gridZ + offsetZ;

                    if (gridX >= 0 && gridX < gridSizeX && gridZ >= 0 && gridZ < grid.Length / gridSizeX)
                    {
                        neighbors.Add(gridZ * gridSizeX + gridX);
                    }
                }
            }
        }

        private int GetDistance(int indexCellA, int indexCellB)
        {
            Cell cellA = grid[indexCellA];
            Cell cellB = grid[indexCellB];

            int xDistance = Mathf.Abs(cellA.gridX - cellB.gridX);
            int zDistance = Mathf.Abs(cellA.gridZ - cellB.gridZ);

            if (xDistance > zDistance) return 14 * zDistance + 10 * (xDistance - zDistance);

            return 14 * xDistance + 10 * (zDistance - xDistance);
        }
    }

    [BurstCompile]
    internal struct AddPath : IJob
    {
        [ReadOnly] public NativeArray<Cell> grid;
        public NativeList<Cell> finalPath;
        public NativeHashMap<int, PathCellData> visitedNodes;
        
        [ReadOnly] public int endIndex;

        public void Execute()
        {
            AddFinalPath(endIndex);
        }

        private void AddFinalPath(int lastIndex)
        {
            int currentIndex = lastIndex;

            while (currentIndex != -1)
            {
                finalPath.Add(grid[currentIndex]);
                currentIndex = visitedNodes[currentIndex].cameFrom;
            }
            
            finalPath.RemoveAt(finalPath.Length - 1);
        }
    }

    [BurstCompile]
    internal struct ReversePath : IJob
    {
        public NativeList<Cell> finalPath;

        public void Execute()
        {
            finalPath.Reverse();
        }
    }
}