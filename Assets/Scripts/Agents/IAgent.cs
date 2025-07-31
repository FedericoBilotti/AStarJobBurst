using NavigationGraph;
using Unity.Collections;
using UnityEngine;

namespace Agents
{
    public interface IAgent
    {
        bool RequestPath(Vector3 startPosition, Vector3 endPosition);
        void SetPath(NativeList<Cell> path);
    }
}