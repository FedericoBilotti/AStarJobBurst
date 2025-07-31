using System;
using System.Collections.Generic;
using NavigationGraph;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Pool;
using Utilities;

namespace Pathfinding.RequesterStrategy
{
    public class ThetaStarRequester : IPathRequest, IDisposable
    {
        private readonly INavigationGraph _navigationGraph;

        private List<PathRequest> _requests;
        private IObjectPool<PathRequest> _pathRequestPool;

        public ThetaStarRequester(INavigationGraph navigationGraph)
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
                simplified = new NativeList<Cell>(30, Allocator.Persistent),
                closedList = new NativeHashSet<int>(64, Allocator.Persistent),
                openList = new NativePriorityQueue<PathCellData>(_navigationGraph.GetGridSize(), Allocator.Persistent),
                visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.Persistent)
            }, actionOnGet: pathReq =>
            {
                pathReq.path.Clear();
                pathReq.simplified.Clear();
                pathReq.closedList.Clear();
                pathReq.openList.Clear();
                pathReq.visitedNodes.Clear();
                pathReq.agent = null;
            }, actionOnRelease: null, actionOnDestroy: pathReq =>
            {
                if (pathReq.path.IsCreated) pathReq.path.Dispose();
                if (pathReq.simplified.IsCreated) pathReq.simplified.Dispose();
                if (pathReq.closedList.IsCreated) pathReq.closedList.Dispose();
                if (pathReq.openList.IsCreated) pathReq.openList.Dispose();
                if (pathReq.visitedNodes.IsCreated) pathReq.visitedNodes.Dispose();
            }, defaultCapacity: CAPACITY, maxSize: MAX_SIZE);
        }

        public bool RequestPath(IAgent agent, Cell start, Cell end)
        {
            if (!end.isWalkable) return false;

            PathRequest pathRequest = _pathRequestPool.Get();

            JobHandle aStarJob = new AStarJob
            {
                grid = _navigationGraph.GetGrid(),
                closedList = pathRequest.closedList,
                openList = pathRequest.openList,
                visitedNodes = pathRequest.visitedNodes,
                gridSizeX = _navigationGraph.GetGridSizeX(),
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();
            
            JobHandle addPath = new AddPath(
                    _navigationGraph.GetGrid(),
                    pathRequest.path,
                    pathRequest.visitedNodes,
                    end.gridIndex)
                    .Schedule(aStarJob);

            JobHandle thetaStarJob = new ThetaStarJob(
                    _navigationGraph.GetGrid(),
                    _navigationGraph.GetGridSizeX(),
                    pathRequest.path,
                    pathRequest.simplified)
                    .Schedule(addPath);
            
            JobHandle reversePath = new ReversePath
                    {
                        finalPath = pathRequest.path
                    }
                    .Schedule(thetaStarJob);

            pathRequest.agent = agent;
            pathRequest.handle = reversePath;
            _requests.Add(pathRequest);

            return true;
        }

        public void FinishPath()
        {
            for (int i = _requests.Count - 1; i >= 0; i--)
            {
                var req = _requests[i];

                if (!req.handle.IsCompleted) continue;

                req.handle.Complete();
                req.agent.SetPath(req.path);

                _pathRequestPool.Release(req);
                _requests.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            foreach (var pathRequest in _requests)
            {
                pathRequest.handle.Complete();
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
            public NativeList<Cell> simplified;
            public NativeHashSet<int> closedList;
            public NativePriorityQueue<PathCellData> openList;
            public NativeHashMap<int, PathCellData> visitedNodes;
        }
    }
}