using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Utilities;

namespace NavGridSystem
{
    public class AStarRequestPriorityQueue : MonoBehaviour, INavigation
    {
        [SerializeField] private bool _showPath;
        [SerializeField] private bool _requestPath;
        [SerializeField] private Transform _start;
        [SerializeField] private Transform _end;

        private IGridSystem _gridSystem;

        private NativeList<int> _finalPath;

        #region Unity Methods

        private void Awake()
        {
            _gridSystem = GetComponent<IGridSystem>();

            _finalPath = new NativeList<int>(30, Allocator.Persistent);
        }


        private void Update()
        {
            if (!_requestPath) return;
            RequestPath(_start.position, _end.position);
        }

        private void OnDestroy()
        {
            _finalPath.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!_showPath) return;
            if (!_finalPath.IsCreated || _finalPath.Length == 0) return;

            Gizmos.color = Color.red;
            NativeArray<Cell> grid = _gridSystem.GetGrid();

            for (int i = 0; i < _finalPath.Length - 1; i++)
            {
                Vector3 startPos = grid[_finalPath[i]].position;
                Vector3 endPos = grid[_finalPath[i + 1]].position;
                Gizmos.DrawLine(startPos, endPos);
            }
        }

        #endregion

        public void RequestPath(Vector3 start, Vector3 end)
        {
            Cell startCell = _gridSystem.GetCellWithWorldPosition(start);
            Cell endCell = _gridSystem.GetCellWithWorldPosition(end);

            if (!startCell.isWalkable || !endCell.isWalkable) return;

            _finalPath.Clear();

            var visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.TempJob);
            var closedList = new NativeHashSet<int>(64, Allocator.TempJob);
            var priorityQueue = new NativePriorityQueue<PathCellData>(20, Allocator.TempJob);

            new AStarJob
            {
                grid = _gridSystem.GetGrid(),
                finalPath = _finalPath,
                closedList = closedList,
                openList = priorityQueue,
                visitedNodes = visitedNodes,
                gridSizeX = _gridSystem.GetGridSizeX(),
                startIndex = startCell.gridIndex,
                endIndex = endCell.gridIndex
            }.Schedule().Complete();
            
            visitedNodes.Dispose();
            closedList.Dispose();
            priorityQueue.Dispose();
        }

        [BurstCompile(Debug = true)]
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