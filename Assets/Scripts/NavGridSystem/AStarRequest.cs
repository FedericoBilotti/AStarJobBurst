using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Utilities;

namespace NavGridSystem
{
    public class AStarRequest : Singleton<AStarRequest>, INavigation
    {
        [SerializeField] private Transform _start;
        [SerializeField] private Transform _end;

        // Create a service locator for the grid system and the navigation -> suscribe the interfaces of both.
        private IGridSystem _gridSystem;

        private NativePriorityQueue<PathCellData> _openList;
        private NativeHashMap<int, PathCellData> _visitedNodes;
        private NativeHashSet<int> _closedList;

        #region Unity Methods

        private void Awake()
        {
            _gridSystem = GetComponent<IGridSystem>();

            _openList = new NativePriorityQueue<PathCellData>(_gridSystem.GetGridSize(), Allocator.Persistent);
            _visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.Persistent); ;
            _closedList = new NativeHashSet<int>(64, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            _openList.Dispose();
            _visitedNodes.Dispose();
            _closedList.Dispose();
        }

        #endregion

        public bool RequestPath(ref NativeList<int> path, Vector3 start, Vector3 end)
        {
            Cell startCell = _gridSystem.GetCellWithWorldPosition(start);
            Cell endCell = _gridSystem.GetCellWithWorldPosition(end);

            if (!startCell.isWalkable || !endCell.isWalkable) return false;

            _openList.Clear();
            _visitedNodes.Clear();
            _closedList.Clear();

            JobHandle jobHandle = new AStarJob
            {
                grid = _gridSystem.GetGrid(),
                finalPath = path,
                closedList = _closedList,
                openList = _openList,
                visitedNodes = _visitedNodes,
                gridSizeX = _gridSystem.GetGridSizeX(),
                startIndex = startCell.gridIndex,
                endIndex = endCell.gridIndex
            }.Schedule();
            
            jobHandle.Complete();

            return true;
        }

        [BurstCompile]
        private struct AStarJob : IJob
        {
            [ReadOnly] public NativeArray<Cell> grid;
            public NativeList<int> finalPath;
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

                if (xDistance > zDistance) 
                    return 14 * zDistance + 10 * (xDistance - zDistance);

                return 14 * xDistance + 10 * (zDistance - xDistance);
            }

            private void ReversePath(int end)
            {
                int currentIndex = end;
                while (currentIndex != -1)
                {
                    finalPath.Add(currentIndex);
                    currentIndex = visitedNodes[currentIndex].cameFrom;
                }

                finalPath.Reverse();
            }
        }
    }
}