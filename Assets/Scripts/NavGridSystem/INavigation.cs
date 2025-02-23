using Unity.Collections;
using UnityEngine;

namespace NavGridSystem
{
    public interface INavigation
    {
        void RequestPath(ref NativeList<Cell> path, Vector3 start, Vector3 end);
        void RequestPath(ref NativeList<Cell> path, Cell start, Cell end);
    }
}