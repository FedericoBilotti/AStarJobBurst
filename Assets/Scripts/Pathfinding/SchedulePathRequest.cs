using System;
using System.Collections.Generic;
using NavigationGraph;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Pool;
using Utilities;

namespace Pathfinding
{
    public class SchedulePathRequest : IPathRequest, IDisposable
    {
        private readonly INavigationGraph _navigationGraph;

        private List<PathRequest> _requests;
        private IObjectPool<PathRequest> _pathRequestPool;

        public SchedulePathRequest(INavigationGraph navigationGraph)
        {
            _navigationGraph = navigationGraph;
            InitializeRequesters();
        }

        private void InitializeRequesters()
        {
            const int CAPACITY = 100;
            const int MAX_SIZE = 1000;
            _requests = new List<PathRequest>(CAPACITY);
            _pathRequestPool = new ObjectPool<PathRequest>(createFunc: () => new PathRequest
            {
                path = new NativeList<Cell>(30, Allocator.Persistent),
                closedList = new NativeHashSet<int>(64, Allocator.Persistent),
                openList = new NativePriorityQueue<PathCellData>(_navigationGraph.GetGridSize(), Allocator.Persistent),
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

            PathRequest pathRequest = _pathRequestPool.Get();

            JobHandle jobHandle = new AStarJob
            {
                grid = _navigationGraph.GetGrid(),
                finalPath = pathRequest.path,
                closedList = pathRequest.closedList,
                openList = pathRequest.openList,
                visitedNodes = pathRequest.visitedNodes,
                gridSizeX = _navigationGraph.GetGridSizeX(),
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();

            pathRequest.agent = agent;
            pathRequest.handle = jobHandle;
            _requests.Add(pathRequest);
        }

        public void FinishPath()
        {
            for (int i = _requests.Count - 1; i >= 0; i--)
            {
                var req = _requests[i];

                if (!req.handle.IsCompleted) continue;
                
                req.handle.Complete();

                var pathArray = new Cell[req.path.Length];
                for (int j = 0; j < req.path.Length; j++)
                    pathArray[j] = req.path[j];

                req.agent.SetPath(pathArray);

                _pathRequestPool.Release(req);
                _requests.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            foreach (var pathRequest in _requests)
            {
                pathRequest.visitedNodes.Dispose();
                pathRequest.closedList.Dispose();
                pathRequest.openList.Dispose();
                pathRequest.path.Dispose();
            }

            _pathRequestPool.Clear();
        }

        public void Clear() => Dispose();

        private class PathRequest
        {
            public IAgent agent;
            public JobHandle handle;

            public NativeList<Cell> path;
            public NativeHashSet<int> closedList;
            public NativePriorityQueue<PathCellData> openList;
            public NativeHashMap<int, PathCellData> visitedNodes;
        }
    }
}