using Unity.Collections;
using UnityEngine;

namespace NavGridSystem
{
    public interface INavigation
    {
        bool RequestPath(ref NativeList<Cell> path, Vector3 start, Vector3 end);
    }
}