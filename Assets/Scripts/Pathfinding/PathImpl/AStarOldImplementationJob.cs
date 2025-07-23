using NavigationGraph;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Utilities;

namespace Pathfinding
{
    public struct AStarOldImplementationJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> grid;

        public NativeList<Cell> finalPath;
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
                    ReversePath(endIndex);
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
        
        private void ReversePath(int lastIndex)
        {
            int currentIndex = lastIndex;

            while (currentIndex != -1)
            {
                finalPath.Add(grid[currentIndex]);
                currentIndex = visitedNodes[currentIndex].cameFrom;
            }
            
            var newPath = new NativeList<Cell>(finalPath.Length, Allocator.TempJob);

            SimplifyPath(newPath);
            newPath.Reverse();
            finalPath.Clear();
            finalPath.AddRange(newPath.AsArray());
            newPath.Dispose();
        }

        private void SimplifyPath(NativeList<Cell> path)
        {
            Vector2 lastDir = Vector2.zero;
            
            path.Add(finalPath[0]);
            for (int i = 1; i < finalPath.Length; i++)
            {
                Vector2 newDir = new Vector2(finalPath[i - 1].x - finalPath[i].x, finalPath[i - 1].y - finalPath[i].y);
                if (newDir != lastDir) 
                    path.Add(finalPath[i]);
                
                lastDir = newDir;
            }
        }
    }
}