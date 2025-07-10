using UnityEngine;

namespace NavigationGraph
{
    public interface IAgent
    {
        void RequestPath(Cell start, Cell end);
        void RequestPath(Vector3 start, Vector3 end);
        void SetPath(Cell[] path);
    }
}