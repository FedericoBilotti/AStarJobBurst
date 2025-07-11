using UnityEngine;

namespace NavigationGraph
{
    public interface IAgent
    {
        void RequestPath(Vector3 startPosition, Vector3 endPosition);
        void SetPath(Cell[] path);
    }
}