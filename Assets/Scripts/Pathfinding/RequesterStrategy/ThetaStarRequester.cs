using Agents;
using NavigationGraph;
using Pathfinding.PathImplementation;
using Unity.Jobs;

namespace Pathfinding.RequesterStrategy
{
    public class ThetaStarRequester : Pathfinding
    {
        public ThetaStarRequester(INavigationGraph navigationGraph) : base(navigationGraph) { }

        public override bool RequestPath(IAgent agent, Cell start, Cell end)
        {
            if (!end.isWalkable) return false;

            PathRequest pathRequest = pathRequestPool.Get();

            JobHandle aStarJob = new AStarJob
            {
                grid = navigationGraph.GetGrid(),
                closedList = pathRequest.closedList,
                openList = pathRequest.openList,
                visitedNodes = pathRequest.visitedNodes,
                gridSizeX = navigationGraph.GetGridSizeX(),
                startIndex = start.gridIndex,
                endIndex = end.gridIndex
            }.Schedule();

            JobHandle addPath = new AddPath
            {
                grid = navigationGraph.GetGrid(),
                finalPath = pathRequest.path,
                visitedNodes = pathRequest.visitedNodes,
                endIndex = end.gridIndex
            }.Schedule(aStarJob);

            JobHandle thetaStarJob = new ThetaStarJob
            {
                grid = navigationGraph.GetGrid(),
                gridSizeX = navigationGraph.GetGridSizeX(),
                finalPath = pathRequest.path,
                simplified = pathRequest.simplified
            }.Schedule(addPath);

            JobHandle reversePath = new ReversePath
            {
                finalPath = pathRequest.path
            }.Schedule(thetaStarJob);

            pathRequest.agent = agent;
            pathRequest.handle = reversePath;
            requests.Add(pathRequest);

            return true;
        }
    }
}