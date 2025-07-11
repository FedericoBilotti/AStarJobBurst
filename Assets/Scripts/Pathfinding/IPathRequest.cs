using NavigationGraph;

namespace Pathfinding
{
    public interface IPathRequest
    {
        public void RequestPath(IAgent agent, Cell start, Cell end);
        public void FinishPath();
        public void Clear();
    }
}