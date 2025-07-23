using Unity.Collections;
using UnityEngine;

namespace NavigationGraph
{
    public interface IAgent
    {
        bool RequestPath(Vector3 startPosition, Vector3 endPosition);
        void SetPath(NativeList<Cell> path);
    }
}