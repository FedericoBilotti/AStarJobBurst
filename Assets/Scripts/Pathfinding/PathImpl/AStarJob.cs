using NavigationGraph;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utilities;

namespace Pathfinding
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

                    int gridX = cell.x + offsetX;
                    int gridZ = cell.y + offsetZ;

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

            int xDistance = Mathf.Abs(cellA.x - cellB.x);
            int zDistance = Mathf.Abs(cellA.y - cellB.y);

            if (xDistance > zDistance) return 14 * zDistance + 10 * (xDistance - zDistance);

            return 14 * xDistance + 10 * (zDistance - xDistance);
        }
    }

    [BurstCompile]
    internal struct PostProcessAStarJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> grid;
        [ReadOnly] public NativeHashMap<int, PathCellData> visitedNodes;
        [ReadOnly] public int endIndex;
        [ReadOnly] public int gridSizeX;

        public NativeList<Cell> finalPath;

        public void Execute()
        {
            ReversePath(endIndex);
        }

        private void ReversePath(int lastIndex)
        {
            int currentIndex = lastIndex;

            while (currentIndex != -1)
            {
                finalPath.Add(grid[currentIndex]);
                currentIndex = visitedNodes[currentIndex].cameFrom;
            }

            var simplified = new NativeList<Cell>(finalPath.Length, Allocator.Temp);

            SimplifyPath(simplified);

            simplified.Reverse();
            finalPath.Clear();
            finalPath.AddRange(simplified.AsArray());
            simplified.Dispose();
        }

        private void SimplifyPath(NativeList<Cell> simplified)
        {
            int j = 0;
            simplified.Add(finalPath[0]);

            for (int i = 1; i < finalPath.Length; i++)
            {
                if (!HasLineOfSight(finalPath[j], finalPath[i]))
                {
                    simplified.Add(finalPath[i - 1]);
                    j = i - 1;
                }
            }
            
            simplified.Add(finalPath[^1]);
        }

        // Bresenham algorithm
        private bool HasLineOfSight(Cell startCell, Cell endCell)
        {
            int startX = startCell.x;
            int startY = startCell.y;
            int endX = endCell.x;
            int endY = endCell.y;

            int deltaX = Mathf.Abs(endX - startX);
            int deltaY = Mathf.Abs(endY - startY);

            int stepX = startX < endX ? 1 : -1;
            int stepY = startY < endY ? 1 : -1;

            int error = deltaX - deltaY;

            while (startX != endX || startY != endY)
            {
                int index = GetIndex(startX, startY);
                
                if (!grid[index].isWalkable) return false;

                int doubleError = 2 * error;

                if (doubleError > -deltaY)
                {
                    error -= deltaY;
                    startX += stepX;
                }

                if (doubleError < deltaX)
                {
                    error += deltaX;
                    startY += stepY;
                }
            }

            return true;
        }

        private int GetIndex(int x, int y)
        {
            return x + y * gridSizeX;
        }
    }
}