using System;
using NavigationGraph;
using Unity.Collections;
using Unity.Jobs;
using Utilities;

namespace Pathfinding
{
    /// <summary>
    /// A path requester that block the thread to find the path.
    /// </summary>
    public class OnePathRequester : IPathRequest, IDisposable
    {
        private readonly INavigationGraph _navigationGraph;
        
        private NativePriorityQueue<PathCellData> _openList;
        private NativeHashMap<int, PathCellData> _visitedNodes;
        private NativeHashSet<int> _closedList;
        private NativeList<Cell> _path;

        public OnePathRequester(INavigationGraph navigationGraph)
        {
            _navigationGraph = navigationGraph;
            InitializeLists();
        }

        private void InitializeLists()
        {
            _openList = new NativePriorityQueue<PathCellData>(_navigationGraph.GetGridSize(), Allocator.Persistent);
            _visitedNodes = new NativeHashMap<int, PathCellData>(64, Allocator.Persistent);
            _closedList = new NativeHashSet<int>(64, Allocator.Persistent);
            _path = new NativeList<Cell>(100, Allocator.Persistent);
        }
        
        public void RequestPath(IAgent agent, Cell start, Cell end)
        {
            if (!end.isWalkable) return;

            _openList.Clear();
            _visitedNodes.Clear();
            _closedList.Clear();
            _path.Clear();

            JobHandle jobHandle = new AStarJob
            {
                grid = _navigationGraph.GetGrid(),
                finalPath = _path,
                closedList = _closedList,
                openList = _openList,
                visitedNodes = _visitedNodes,
                gridSizeX = _navigationGraph.GetGridSizeX(),
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();

            jobHandle.Complete();
            
            Cell[] pathArray = ConvertPathToArray();
            agent.SetPath(pathArray);
        }

        private Cell[] ConvertPathToArray()
        {
            var pathArray = new Cell[_path.Length];
            for (int i = 0; i < _path.Length; i++)
            {
                pathArray[i] = _path[i];
            }

            return pathArray;
        }

        public void LaunchPath() { }

        public void Dispose()
        {
            _openList.Dispose();
            _visitedNodes.Dispose();
            _closedList.Dispose();
        }

        public void Clear() => Dispose();
    }
}