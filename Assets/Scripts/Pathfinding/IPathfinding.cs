using NavigationGraph;

namespace Pathfinding
{
    public interface IPathfinding
    {
        void RequestPath(IAgent agent, Cell start, Cell end);
    }
}