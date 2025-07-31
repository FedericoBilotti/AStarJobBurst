using Agents;
using NavigationGraph;

namespace Pathfinding
{
    public interface IPathfinding
    {
        bool RequestPath(IAgent agent, Cell start, Cell end);
    }
}