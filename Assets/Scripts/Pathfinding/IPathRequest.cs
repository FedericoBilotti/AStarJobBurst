using NavigationGraph;

namespace Pathfinding
{
    public interface IPathRequest
    {
        public void RequestPath(IAgent agent, Cell start, Cell end);
        public void LaunchPath();
        public void Clear();
    }
}