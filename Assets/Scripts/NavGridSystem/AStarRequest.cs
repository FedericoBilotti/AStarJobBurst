using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Pool;
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

            foreach (var pathRequest in _requests)
            {
                pathRequest.visitedNodes.Dispose();
                pathRequest.closedList.Dispose();
                pathRequest.openList.Dispose();
                pathRequest.path.Dispose();
            }
            
            _pathRequestPool.Clear();
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

        private class PathRequest
        {
            public IAgent agent;
            public JobHandle handle;
            
            public NativeList<Cell> path;
            public NativeHashSet<int> closedList;
            public NativePriorityQueue<PathCellData> openList;
            public NativeHashMap<int, PathCellData> visitedNodes;
        }
        
        private List<PathRequest> _requests;
        private IObjectPool<PathRequest> _pathRequestPool;

        private void Start()
        {
            ServiceLocator.Instance.RegisterService<INavigation>(this);
            
            // Initialize the pool
            const int CAPACITY = 10;
            const int MAX_SIZE = 100;
            _requests = new List<PathRequest>(CAPACITY);
            _pathRequestPool = new ObjectPool<PathRequest>(createFunc: () => new PathRequest
            {
                path = new NativeList<Cell>(30, Allocator.Persistent),
                closedList = new NativeHashSet<int>(64, Allocator.Persistent),
                openList = new NativePriorityQueue<PathCellData>(_gridSystem.GetGridSize(), Allocator.Persistent),
                visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.Persistent)
            }, actionOnGet: pathReq =>
            {
                pathReq.path.Clear();
                pathReq.closedList.Clear();
                pathReq.openList.Clear();
                pathReq.visitedNodes.Clear();
                pathReq.agent = null;
            }, actionOnRelease: null, actionOnDestroy: pathReq =>
            {
                if (pathReq.path.IsCreated) pathReq.path.Dispose();
                if (pathReq.closedList.IsCreated) pathReq.closedList.Dispose();
                if (pathReq.openList.IsCreated) pathReq.openList.Dispose();
                if (pathReq.visitedNodes.IsCreated) pathReq.visitedNodes.Dispose();
            }, defaultCapacity: CAPACITY, maxSize: MAX_SIZE);
        }

        public void RequestPath(IAgent agent, Cell start, Cell end)
        {
            if (!end.isWalkable) return;
            
            var req = _pathRequestPool.Get();

            JobHandle jobHandle = new AStarJob
            {
                grid = _gridSystem.GetGrid(),
                finalPath = req.path,
                closedList = req.closedList,
                openList = req.openList,
                visitedNodes = req.visitedNodes,
                gridSizeX = _gridSystem.GetGridSizeX(),
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();

            req.agent = agent;
            req.handle = jobHandle;
            _requests.Add(req);
        }

        private void FinishPaths()
        {
            NativeArray<JobHandle> handles = new(_requests.Count, Allocator.TempJob);
            handles.CopyFrom(_requests.Select(r => r.handle).ToArray()); // aloc
            JobHandle.CompleteAll(handles);

            foreach (var req in _requests)
            {
                var pathArray = new Cell[req.path.Length]; // aloc
                for (int i = 0; i < req.path.Length; i++)
                {
                    pathArray[i] = req.path[i];
                }

                req.agent.SetPath(pathArray); // aloc
                _pathRequestPool.Release(req);
            }
            _requests.Clear();
            handles.Dispose();
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