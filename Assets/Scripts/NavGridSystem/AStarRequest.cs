using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Utilities;

namespace NavGridSystem
{
    public class AStarRequest : MonoBehaviour, INavigation
    {
        private IGridSystem _gridSystem;

        #region Unity Methods

        private void Awake()
        {
            _gridSystem = GetComponent<IGridSystem>();

            _openList = new NativePriorityQueue<PathCellData>(_gridSystem.GetGridSize(), Allocator.Persistent);
            _visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.Persistent);
            _closedList = new NativeHashSet<int>(64, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            _openList.Dispose();
            _visitedNodes.Dispose();
            _closedList.Dispose();
            
            _pendingHandles.Dispose();
            
            foreach (var clo in _closedLists)
            {
                clo.Dispose();
            }
            
            foreach (var ol in _openLists)
            {
                ol.Dispose();
            }
            
            foreach (var vnl in _visitedNodesLists)
            {
                vnl.Dispose();
            }

            foreach (var path in _paths)
            {
                path.Value.Dispose();
            }
        }

        #endregion

        #region old a*

        private NativePriorityQueue<PathCellData> _openList;
        private NativeHashMap<int, PathCellData> _visitedNodes;
        private NativeHashSet<int> _closedList;

        public void RequestPath(ref NativeList<Cell> path, Vector3 start, Vector3 end)
        {
            Cell startCell = _gridSystem.GetCellWithWorldPosition(start);
            Cell endCell = _gridSystem.GetCellWithWorldPosition(end);

            ExecuteJob(ref path, startCell, endCell);
        }

        public void RequestPath(ref NativeList<Cell> path, Cell start, Cell end) => ExecuteJob(ref path, start, end);

        private void ExecuteJob(ref NativeList<Cell> path, Cell start, Cell end)
        {
            if (!end.isWalkable) return;

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
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();

            jobHandle.Complete();
        }

        #endregion

        #region new a*

        private Dictionary<IAgent, NativeList<Cell>> _paths; // every agent has its own path, and it's reference by the index of the agent

        private NativeList<JobHandle> _pendingHandles;
        private List<NativeHashSet<int>> _closedLists;
        private List<NativePriorityQueue<PathCellData>> _openLists;
        private List<NativeHashMap<int, PathCellData>> _visitedNodesLists;

        private void Start()
        {
            ServiceLocator.Instance.RegisterService<INavigation>(this);
            var capacity = 10;
            _paths = new Dictionary<IAgent, NativeList<Cell>>(capacity);
            _pendingHandles = new NativeList<JobHandle>(capacity, Allocator.Persistent);
            _closedLists = new List<NativeHashSet<int>>(capacity);
            _openLists = new List<NativePriorityQueue<PathCellData>>(capacity);
            _visitedNodesLists = new List<NativeHashMap<int, PathCellData>>(capacity);
        }

        public void RequestPath(IAgent agent, Cell start, Cell end)
        {
            if (!end.isWalkable) return;

            var closedList = new NativeHashSet<int>(64, Allocator.Persistent);
            var openList = new NativePriorityQueue<PathCellData>(_gridSystem.GetGridSize(), Allocator.Persistent);
            var visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.Persistent);
            var path = new NativeList<Cell>(30, Allocator.Persistent);

            JobHandle jobHandle = new AStarJob
            {
                grid = _gridSystem.GetGrid(),
                finalPath = path,
                closedList = closedList,
                openList = openList,
                visitedNodes = visitedNodes,
                gridSizeX = _gridSystem.GetGridSizeX(),
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();
            
            _paths.Add(agent, path);
            
            _pendingHandles.Add(jobHandle);
            
            _closedLists.Add(closedList);
            _openLists.Add(openList);
            _visitedNodesLists.Add(visitedNodes);
        }

        private void FinishPaths()
        {
            JobHandle.CompleteAll(_pendingHandles.AsArray());
            
            foreach (var kv in _paths)
                kv.Key.SetPath(kv.Value);

            foreach (var clo in _closedLists)
            {
                clo.Dispose();
            }
            
            foreach (var ol in _openLists)
            {
                ol.Dispose();
            }
            
            foreach (var vnl in _visitedNodesLists)
            {
                vnl.Dispose();
            }
            
            _pendingHandles.Clear();
            _closedLists.Clear();
            _openLists.Clear();
            _visitedNodesLists.Clear();
            _paths.Clear();
        }

        private void LateUpdate() => FinishPaths();

        #endregion

        [BurstCompile]
        private struct AStarJob : IJob
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

            private void ReversePath(int end)
            {
                int currentIndex = end;

                while (currentIndex != -1)
                {
                    finalPath.Add(grid[currentIndex]);
                    currentIndex = visitedNodes[currentIndex].cameFrom;
                }

                finalPath.Reverse();
            }
        }
    }
}