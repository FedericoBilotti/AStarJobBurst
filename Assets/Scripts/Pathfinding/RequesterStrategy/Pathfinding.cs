using System;
using System.Collections.Generic;
using Agents;
using NavigationGraph;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Pool;
using Utilities;

namespace Pathfinding.RequesterStrategy
{
    public abstract class Pathfinding : IPathRequest, IDisposable
    {
        protected readonly INavigationGraph navigationGraph;

        protected List<PathRequest> requests;
        protected IObjectPool<PathRequest> pathRequestPool;

        protected Pathfinding(INavigationGraph navigationGraph)
        {
            this.navigationGraph = navigationGraph;
            InitializeRequesters();
        }

        private void InitializeRequesters()
        {
            const int CAPACITY = 100;
            const int MAX_SIZE = 1000;
            requests = new List<PathRequest>(CAPACITY);
            pathRequestPool = new ObjectPool<PathRequest>(createFunc: () => new PathRequest
            {
                path = new NativeList<Cell>(30, Allocator.Persistent),
                simplified = new NativeList<Cell>(30, Allocator.Persistent),
                closedList = new NativeHashSet<int>(64, Allocator.Persistent),
                openList = new NativePriorityQueue<PathCellData>(navigationGraph.GetGridSize(), Allocator.Persistent),
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

        public abstract bool RequestPath(IAgent agent, Cell start, Cell end);

        public virtual void FinishPath()
        {
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                PathRequest req = requests[i];

                if (!req.handle.IsCompleted) continue;

                req.handle.Complete();
                req.agent.SetPath(req.path);

                pathRequestPool.Release(req);
                requests.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            foreach (var pathRequest in requests)
            {
                pathRequest.handle.Complete();
                pathRequest.visitedNodes.Dispose();
                pathRequest.closedList.Dispose();
                pathRequest.openList.Dispose();
                pathRequest.path.Dispose();
            }

            pathRequestPool.Clear();
        }

        public void Clear() => Dispose();

        protected class PathRequest
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